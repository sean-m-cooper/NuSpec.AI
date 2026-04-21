using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Integration;

/// <summary>
/// End-to-end integration tests verifying the full pipeline:
/// project analysis → format resolution → file output. All formats are
/// available to all consumers.
/// </summary>
public class FormatsIntegrationTests : IDisposable
{
    private readonly string _projectDir;
    private readonly string _outputDir;
    private readonly string _csprojPath;

    private const string PackageId = "Acme.TestLib";

    // File names mirror the FileName property on each formatter.
    private const string FileNameJson    = "package-map.json";
    private const string FileNameYaml    = "package-map.yaml";
    private const string FileNameCompact = "package-map.compact.json";
    private const string FileNameUltra   = "package-map.ultra";

    public FormatsIntegrationTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "nuspecai-formats-test-" + Guid.NewGuid().ToString("N")[..8]);
        _projectDir = Path.Combine(baseDir, "src");
        _outputDir  = Path.Combine(baseDir, "out");
        Directory.CreateDirectory(_projectDir);
        Directory.CreateDirectory(_outputDir);

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
        var baseDir = Path.GetDirectoryName(_projectDir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }

    private void RunPipeline(string formatsArg)
    {
        var packageMap = ProjectAnalyzer.Analyze(_csprojPath);
        var formatters = FormatterRegistry.Resolve(formatsArg);

        Directory.CreateDirectory(_outputDir);
        foreach (var formatter in formatters)
        {
            var outputPath = Path.Combine(_outputDir, formatter.FileName);
            File.WriteAllText(outputPath, formatter.Serialize(packageMap));
        }
    }

    private IReadOnlyList<string> OutputFiles() =>
        Directory.GetFiles(_outputDir).Select(p => Path.GetFileName(p)!).ToList();

    [Fact]
    public void FormatsAll_GeneratesFourFiles()
    {
        RunPipeline("all");

        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.Contains(FileNameYaml, files);
        Assert.Contains(FileNameCompact, files);
        Assert.Contains(FileNameUltra, files);
        Assert.Equal(4, files.Count);
    }

    [Fact]
    public void FormatsJson_GeneratesOnlyJson()
    {
        RunPipeline("json");

        var files = OutputFiles();
        Assert.Contains(FileNameJson, files);
        Assert.Single(files);
    }

    [Fact]
    public void FormatsYamlCompactUltra_GeneratesThreeFiles()
    {
        RunPipeline("yaml;compact;ultra");

        var files = OutputFiles();
        Assert.DoesNotContain(FileNameJson, files);
        Assert.Contains(FileNameYaml, files);
        Assert.Contains(FileNameCompact, files);
        Assert.Contains(FileNameUltra, files);
        Assert.Equal(3, files.Count);
    }

    [Fact]
    public void FormatsYaml_GeneratesOnlyYaml()
    {
        RunPipeline("yaml");

        var files = OutputFiles();
        Assert.Contains(FileNameYaml, files);
        Assert.Single(files);
    }
}
