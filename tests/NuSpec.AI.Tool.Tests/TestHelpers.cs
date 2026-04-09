using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NuSpec.AI.Tool.Tests;

public static class TestHelpers
{
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources
            .Select((source, i) => CSharpSyntaxTree.ParseText(source,
                options: new CSharpParseOptions(LanguageVersion.Latest),
                path: $"TestFile{i}.cs"))
            .ToList();

        var references = GetCoreReferences();

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> GetCoreReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "";
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
