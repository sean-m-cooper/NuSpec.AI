using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: NuSpec.AI.Tool <project-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  project-file         Path to the .csproj file to analyze");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <path>      Output file path for single format (default: stdout)");
    Console.WriteLine("  --output-dir <dir>   Output directory for multiple formats");
    Console.WriteLine("  --formats <list>     Semicolon-separated formats: json, yaml, compact, ultra, all (default: json)");
    Console.WriteLine("  --help               Show help");
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "--version")
{
    var version = typeof(ProjectAnalyzer).Assembly.GetName().Version;
    Console.WriteLine($"NuSpec.AI.Tool {version}");
    return 0;
}

var projectFile = args[0];
string? outputPath = null;
string? outputDir = null;
string? formatsArg = null;

for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
        outputPath = args[++i];
    else if (args[i] == "--output-dir" && i + 1 < args.Length)
        outputDir = args[++i];
    else if (args[i] == "--formats" && i + 1 < args.Length)
        formatsArg = args[++i];
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile);
    var formatters = FormatterRegistry.Resolve(formatsArg);

    if (outputDir is not null)
    {
        ProjectAnalyzer.WriteFormats(packageMap, formatters, outputDir);
    }
    else if (outputPath is not null)
    {
        var formatter = formatters[0];
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, formatter.Serialize(packageMap));
        Console.Error.WriteLine($"NuSpec.AI: Written {outputPath}");
    }
    else
    {
        Console.WriteLine(formatters[0].Serialize(packageMap));
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
    Console.Error.WriteLine($"Error analyzing project: {ex.Message}");
    return 1;
}
