using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;

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
    Console.WriteLine("                      (or 'all' for every format; default: json)");
    Console.WriteLine("  --full-docs         Capture <param>, <returns>, <remarks>, etc.");
    Console.WriteLine("                      (default: only <summary>; increases map size)");
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
bool includeFullDocs = false;

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
        case "--full-docs":
            includeFullDocs = true;
            break;
    }
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

// -------------------------------------------------------------------------
// Analyze + format + output
// -------------------------------------------------------------------------

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile, includeFullDocs);
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
