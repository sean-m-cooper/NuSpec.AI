using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Models;
using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Analysis;

public static class ProjectAnalyzer
{
    public static PackageMap Analyze(string csprojPath, bool includeFullDocs = false)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
            ?? throw new InvalidOperationException($"Cannot determine directory for: {csprojPath}");

        var packageInfo = CsprojReader.ReadPackageInfo(csprojPath);
        var dependencies = DependencyResolver.Resolve(csprojPath);

        var (compilation, projectTrees) = BuildCompilation(projectDir, packageInfo.Id);
        var publicSurface = ApiSurfaceCollector.Collect(compilation, projectTrees, includeFullDocs);

        return new PackageMap
        {
            Package = packageInfo,
            Dependencies = dependencies,
            PublicSurface = publicSurface
        };
    }

    public static void WriteFormats(
        PackageMap packageMap,
        IReadOnlyList<IFormatter> formatters,
        string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        foreach (var formatter in formatters)
        {
            var path = Path.Combine(outputDir, formatter.FileName);
            File.WriteAllText(path, formatter.Serialize(packageMap));
            Console.Error.WriteLine($"NuSpec.AI: Written {formatter.FileName}");
        }
    }

    private static (CSharpCompilation compilation, HashSet<SyntaxTree> projectTrees) BuildCompilation(
        string projectDir,
        string assemblyName)
    {
        var projectSources = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relativePath = Path.GetRelativePath(projectDir, f);
                return !relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var nugetSources = GetNuGetContentFiles(projectDir).ToList();

        var projectTrees = new HashSet<SyntaxTree>(
            projectSources
                .AsParallel()
                .Select(file => CSharpSyntaxTree.ParseText(
                    File.ReadAllText(file),
                    path: file,
                    options: new CSharpParseOptions(LanguageVersion.Latest))));

        var nugetTrees = nugetSources
            .AsParallel()
            .Select(file => CSharpSyntaxTree.ParseText(
                File.ReadAllText(file),
                path: file,
                options: new CSharpParseOptions(LanguageVersion.Latest)))
            .ToList();

        var allTrees = new List<SyntaxTree>(projectTrees);
        allTrees.AddRange(nugetTrees);

        var references = GetCoreMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            allTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (compilation, projectTrees);
    }

    /// <summary>
    /// Finds NuGet-supplied source files (contentFiles with buildAction=Compile)
    /// that the consumer's project pulls in via PackageReference. Reads the
    /// authoritative paths from obj/project.assets.json.
    /// </summary>
    private static IEnumerable<string> GetNuGetContentFiles(string projectDir)
    {
        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
            return [];

        string? packagesRoot = null;
        var contentFileEntries = new List<(string packageKey, string relativePath)>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = doc.RootElement;

            // packageFolders holds the configured NuGet package root(s); we use the first.
            if (root.TryGetProperty("packageFolders", out var folders) &&
                folders.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var folder in folders.EnumerateObject())
                {
                    packagesRoot = folder.Name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(packagesRoot))
                return [];

            // targets.<tfm>.<package>/<version>.contentFiles.<file>.buildAction = "Compile"
            if (root.TryGetProperty("targets", out var targets))
            {
                foreach (var tfm in targets.EnumerateObject())
                {
                    foreach (var pkg in tfm.Value.EnumerateObject())
                    {
                        if (!pkg.Value.TryGetProperty("contentFiles", out var cfs) ||
                            cfs.ValueKind != System.Text.Json.JsonValueKind.Object)
                            continue;

                        foreach (var cf in cfs.EnumerateObject())
                        {
                            if (!cf.Value.TryGetProperty("buildAction", out var ba))
                                continue;
                            if (!string.Equals(ba.GetString(), "Compile", StringComparison.OrdinalIgnoreCase))
                                continue;

                            contentFileEntries.Add((pkg.Name, cf.Name));
                        }
                    }
                }
            }
        }
        catch
        {
            return [];
        }

        var results = new List<string>();
        foreach (var (packageKey, relPath) in contentFileEntries)
        {
            // packageKey is "PackageId/Version"; folder layout is lower-case id/version
            var parts = packageKey.Split('/');
            if (parts.Length != 2) continue;
            var folder = Path.Combine(packagesRoot!, parts[0].ToLowerInvariant(), parts[1]);
            var fullPath = Path.Combine(folder, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                results.Add(fullPath);
        }
        return results;
    }

    private static IEnumerable<MetadataReference> GetCoreMetadataReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(trustedAssemblies))
            return [];

        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is
                "System.Runtime.dll" or
                "System.Collections.dll" or
                "System.Linq.dll" or
                "System.Threading.Tasks.dll" or
                "System.ComponentModel.dll" or
                "System.ComponentModel.Annotations.dll" or
                "netstandard.dll" or
                "mscorlib.dll" or
                "System.Private.CoreLib.dll")
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
