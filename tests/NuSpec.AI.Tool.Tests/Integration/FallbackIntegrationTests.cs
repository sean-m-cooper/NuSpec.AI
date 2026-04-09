using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Licensing;
using NuSpec.AI.Tool.Tests.Licensing;

namespace NuSpec.AI.Tool.Tests.Integration;

/// <summary>
/// End-to-end integration tests that exercise fallback behavior when a Pro
/// license is absent, expired, or signed with an unrecognised key.
/// These tests replicate the logic of Program.cs using real disk I/O and
/// a Roslyn-compiled in-memory C# project.
/// </summary>
public class FallbackIntegrationTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _outputDir;
    private readonly string _csprojPath;
    private readonly string? _savedLicenseKey;

    private const string PackageId = "Acme.TestLib";

    // File names mirror the FileName property on each formatter.
    // If a formatter's FileName changes, update the constant here too.
    private const string FileNameJson    = "package-map.json";
    private const string FileNameYaml    = "package-map.yaml";
    private const string FileNameCompact = "package-map.compact.json";
    private const string FileNameUltra   = "package-map.ultra";

    public FallbackIntegrationTests()
    {
        _savedLicenseKey = Environment.GetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY");
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);

        var baseDir = Path.Combine(Path.GetTempPath(), "nuspecai-fallback-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectDir = Path.Combine(baseDir, "src");
        _outputDir  = Path.Combine(baseDir, "out");
        Directory.CreateDirectory(_projectDir);
        Directory.CreateDirectory(_outputDir);

        // Write a minimal .csproj that the CsprojReader can parse.
        _csprojPath = Path.Combine(_projectDir, $"{PackageId}.csproj");
        File.WriteAllText(_csprojPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>{PackageId}</PackageId>
                <Version>1.0.0</Version>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        // Write a small C# source file so the Roslyn compilation has something.
        File.WriteAllText(Path.Combine(_projectDir, "Widget.cs"), """
            namespace Acme.TestLib
            {
                /// <summary>A sample widget.</summary>
                public class Widget
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = string.Empty;
                }
            }
            """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", _savedLicenseKey);
        var baseDir = Path.GetDirectoryName(_projectDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helper — runs the same pipeline as Program.cs but in-process.
    // Returns (exitLikeSuccess, stderrLines).
    // -------------------------------------------------------------------------

    /// <remarks>
    /// This helper intentionally mirrors the routing logic in <c>Program.cs</c>
    /// (license validation → fallback to json + NSPECAI001 if unlicensed Pro formats requested).
    /// If that logic changes, update this helper to match.
    /// </remarks>
    private (bool success, List<string> stderrLines) RunPipeline(
        string? licenseArg,
        string formatsArg,
        string? packageId = null)
    {
        var stderr = new List<string>();

        var resolvedPackageId = packageId ?? PackageId;
        var licenseInfo = LicenseValidator.ValidateForPackage(licenseArg, resolvedPackageId, out var failureReason);
        var hasProLicense = licenseInfo is not null;

        var effectiveFormats = formatsArg;

        var isProFormatsRequested = effectiveFormats
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(f => !f.Equals("json", StringComparison.OrdinalIgnoreCase));

        if (!hasProLicense && isProFormatsRequested)
        {
            stderr.Add(
                $"NuSpec.AI.Tool : warning NSPECAI001: NuSpec.AI.Pro license is " +
                $"{(failureReason ?? "invalid")}. Falling back to standard JSON output.");
            effectiveFormats = "json";
        }

        if (hasProLicense)
        {
            stderr.Add(
                "NuSpec.AI.Tool : message NSPECAI002: NuSpec.AI.Pro is active. " +
                "The NuSpec.AI package reference can be removed.");
        }

        try
        {
            var packageMap = ProjectAnalyzer.Analyze(_csprojPath);
            var formatters = FormatterRegistry.Resolve(effectiveFormats);

            Directory.CreateDirectory(_outputDir);
            foreach (var formatter in formatters)
            {
                var outputPath = Path.Combine(_outputDir, formatter.FileName);
                File.WriteAllText(outputPath, formatter.Serialize(packageMap));
                stderr.Add($"Package map written to: {outputPath}");
            }

            return (true, stderr);
        }
        catch (Exception ex)
        {
            stderr.Add($"Error: {ex.Message}");
            return (false, stderr);
        }
    }

    private IReadOnlyList<string> OutputFiles() =>
        Directory.GetFiles(_outputDir).Select(p => Path.GetFileName(p)!).ToList();

    // -------------------------------------------------------------------------
    // Test 1: No license, Pro formats requested (yaml) → only json + NSPECAI001
    // -------------------------------------------------------------------------

    [Fact]
    public void NoLicense_ProFormatsRequested_FallsBackToJsonWithWarning()
    {
        var (success, stderrLines) = RunPipeline(null, "yaml");

        Assert.True(success);
        Assert.Contains(stderrLines, line => line.Contains("NSPECAI001"));
        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.DoesNotContain(FileNameYaml, files);
        Assert.DoesNotContain(FileNameCompact, files);
        Assert.DoesNotContain(FileNameUltra, files);
        Assert.Single(files);
    }

    // -------------------------------------------------------------------------
    // Test 2: Expired license, Pro formats requested → only json + NSPECAI001
    // -------------------------------------------------------------------------

    [Fact]
    public void ExpiredLicense_ProFormatsRequested_FallsBackToJsonWithWarning()
    {
        var expiredLicense = LicenseTestHelpers.CreateExpiredToken(packages: "*");

        var (success, stderrLines) = RunPipeline(expiredLicense, "yaml;compact;ultra");

        Assert.True(success);
        Assert.Contains(stderrLines, line => line.Contains("NSPECAI001"));
        Assert.DoesNotContain(stderrLines, line => line.Contains("NSPECAI002"));
        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.DoesNotContain(FileNameYaml, files);
        Assert.DoesNotContain(FileNameCompact, files);
        Assert.DoesNotContain(FileNameUltra, files);
        Assert.Single(files);
    }

    // -------------------------------------------------------------------------
    // Test 3: License signed with wrong key, Pro formats requested → only json + NSPECAI001
    // -------------------------------------------------------------------------

    [Fact]
    public void WrongKeyLicense_ProFormatsRequested_FallsBackToJsonWithWarning()
    {
        var wrongKeyLicense = LicenseTestHelpers.CreateTokenWithWrongKey();

        var (success, stderrLines) = RunPipeline(wrongKeyLicense, "yaml;compact;ultra");

        Assert.True(success);
        Assert.Contains(stderrLines, line => line.Contains("NSPECAI001"));
        Assert.DoesNotContain(stderrLines, line => line.Contains("NSPECAI002"));
        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.DoesNotContain(FileNameYaml, files);
        Assert.DoesNotContain(FileNameCompact, files);
        Assert.DoesNotContain(FileNameUltra, files);
        Assert.Single(files);
    }

    // -------------------------------------------------------------------------
    // Test 4: No license, formats=json → json generated, no NSPECAI001
    // -------------------------------------------------------------------------

    [Fact]
    public void NoLicense_FormatsJson_GeneratesJsonWithoutWarning()
    {
        var (success, stderrLines) = RunPipeline(null, "json");

        Assert.True(success);
        Assert.DoesNotContain(stderrLines, line => line.Contains("NSPECAI001"));
        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.Single(files);
    }

    // -------------------------------------------------------------------------
    // Test 5: No license, formats=all → only json + NSPECAI001 (Pro formats dropped)
    // -------------------------------------------------------------------------

    [Fact]
    public void NoLicense_FormatsAll_FallsBackToJsonWithWarning()
    {
        var (success, stderrLines) = RunPipeline(null, "all");

        Assert.True(success);
        Assert.Contains(stderrLines, line => line.Contains("NSPECAI001"));
        Assert.DoesNotContain(stderrLines, line => line.Contains("NSPECAI002"));
        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.DoesNotContain(FileNameYaml, files);
        Assert.DoesNotContain(FileNameCompact, files);
        Assert.DoesNotContain(FileNameUltra, files);
        Assert.Single(files);
    }
}
