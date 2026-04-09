using NuSpec.AI.Tool.Analysis;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: NuSpec.AI.Tool <project-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  project-file    Path to the .csproj file to analyze");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <path>    Output file path (default: stdout)");
    Console.WriteLine("  --help             Show help");
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

for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
    {
        outputPath = args[++i];
    }
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile);
    var json = ProjectAnalyzer.SerializeToJson(packageMap);

    if (outputPath is not null)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        File.WriteAllText(outputPath, json);
        Console.Error.WriteLine($"Package map written to: {outputPath}");
    }
    else
    {
        Console.WriteLine(json);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
