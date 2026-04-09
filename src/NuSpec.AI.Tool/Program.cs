using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Licensing;

// -------------------------------------------------------------------------
// Help / version
// -------------------------------------------------------------------------

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: NuSpec.AI.Tool <project-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  project-file        Path to the .csproj file to analyze");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <dir>      Output directory (omit to write to stdout)");
    Console.WriteLine("  --formats <list>    Semicolon-separated format list: json;yaml;compact;ultra");
    Console.WriteLine("                      (default: json — Pro license required for yaml/compact/ultra)");
    Console.WriteLine("  --license <key>     NuSpec.AI Pro license JWT");
    Console.WriteLine("  --package-id <id>   NuGet package ID used for license scope validation");
    Console.WriteLine("  --help              Show help");
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "--version")
{
    var version = typeof(ProjectAnalyzer).Assembly.GetName().Version;
    Console.WriteLine($"NuSpec.AI.Tool {version}");
    return 0;
}

// -------------------------------------------------------------------------
// Argument parsing
// -------------------------------------------------------------------------

var projectFile = args[0];
string? outputDir = null;
string? formatsArg = null;
string? licenseArg = null;
string? packageIdArg = null;

for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            outputDir = args[++i];
            break;
        case "--output-dir" when i + 1 < args.Length:  // backward compat alias
            outputDir = args[++i];
            break;
        case "--formats" when i + 1 < args.Length:
            formatsArg = args[++i];
            break;
        case "--license" when i + 1 < args.Length:
            licenseArg = args[++i];
            break;
        case "--package-id" when i + 1 < args.Length:
            packageIdArg = args[++i];
            break;
    }
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

// -------------------------------------------------------------------------
// License resolution
// -------------------------------------------------------------------------

var packageId = packageIdArg ?? Path.GetFileNameWithoutExtension(projectFile);
var licenseInfo = LicenseValidator.ValidateForPackage(licenseArg, packageId, out var licenseFailureReason);
var hasProLicense = licenseInfo is not null;

// Only warn when Pro formats were explicitly requested but license is invalid/missing.
var requestedFormats = formatsArg ?? "json";
var isProFormatsRequested = requestedFormats
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(f => !f.Equals("json", StringComparison.OrdinalIgnoreCase));

if (!hasProLicense && isProFormatsRequested)
{
    Console.Error.WriteLine(
        $"NuSpec.AI.Tool : warning NSPECAI001: NuSpec.AI.Pro license is " +
        $"{(licenseFailureReason ?? "invalid")}. Falling back to standard JSON output.");
    formatsArg = "json"; // force fallback
}

// Low-priority coexistence hint (shown when Pro license is present and valid).
if (hasProLicense)
{
    Console.Error.WriteLine(
        "NuSpec.AI.Tool : message NSPECAI002: NuSpec.AI.Pro is active. " +
        "The NuSpec.AI package reference can be removed.");
}

// -------------------------------------------------------------------------
// Analyze + format + output
// -------------------------------------------------------------------------

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile);
    var formatters = FormatterRegistry.Resolve(formatsArg ?? "json");

    if (outputDir is not null)
    {
        Directory.CreateDirectory(outputDir);
        foreach (var formatter in formatters)
        {
            var outputPath = Path.Combine(outputDir, formatter.FileName);
            File.WriteAllText(outputPath, formatter.Serialize(packageMap));
            Console.Error.WriteLine($"Package map written to: {outputPath}");
        }
    }
    else
    {
        Console.WriteLine(formatters.First().Serialize(packageMap));
    }

    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
