using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuSpec.AI.Tool.Models;
using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Analysis;

public static class ProjectAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static PackageMap Analyze(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
            ?? throw new InvalidOperationException($"Cannot determine directory for: {csprojPath}");

        var packageInfo = CsprojReader.ReadPackageInfo(csprojPath);
        var dependencies = CsprojReader.ReadDependencies(csprojPath);

        var compilation = BuildCompilation(projectDir, packageInfo.Id);
        var publicSurface = ApiSurfaceCollector.Collect(compilation);

        return new PackageMap
        {
            Package = packageInfo,
            Dependencies = dependencies,
            PublicSurface = publicSurface
        };
    }

    public static string SerializeToJson(PackageMap packageMap)
    {
        return JsonSerializer.Serialize(packageMap, JsonOptions);
    }

    private static CSharpCompilation BuildCompilation(string projectDir, string assemblyName)
    {
        var sourceFiles = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relativePath = Path.GetRelativePath(projectDir, f);
                return !relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase);
            });

        var syntaxTrees = sourceFiles
            .AsParallel()
            .Select(file =>
            {
                var text = File.ReadAllText(file);
                return CSharpSyntaxTree.ParseText(text, path: file,
                    options: new CSharpParseOptions(LanguageVersion.Latest));
            })
            .ToList();

        var references = GetCoreMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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
