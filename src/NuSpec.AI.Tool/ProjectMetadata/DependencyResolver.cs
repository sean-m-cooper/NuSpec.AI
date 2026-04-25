using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.ProjectMetadata;

public static class DependencyResolver
{
    public static DependencyInfo Resolve(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
            ?? throw new InvalidOperationException($"Cannot determine directory for: {csprojPath}");

        var declared = CsprojReader.ReadDeclaredPackageReferences(csprojPath);
        var frameworkRefs = CsprojReader.ReadFrameworkReferences(csprojPath);
        var assets = AssetsReader.Read(projectDir);

        var packageRefs = declared
            .Where(r => !r.IsPrivateAssetsAll)
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r =>
            {
                assets.ResolvedVersions.TryGetValue(r.Id, out var version);
                var hasMap = version is not null && PackageMapExists(assets.PackageFolders, r.Id, version);
                return new PackageReferenceInfo
                {
                    Id = r.Id,
                    Version = version,
                    HasNuSpecAiMap = hasMap
                };
            })
            .ToList();

        return new DependencyInfo
        {
            PackageReferences = packageRefs,
            FrameworkReferences = frameworkRefs
        };
    }

    private static bool PackageMapExists(IReadOnlyList<string> packageFolders, string id, string version)
    {
        var idLower = id.ToLowerInvariant();
        foreach (var root in packageFolders)
        {
            var path = Path.Combine(root, idLower, version, "ai", "package-map.json");
            if (File.Exists(path))
                return true;
        }
        return false;
    }
}
