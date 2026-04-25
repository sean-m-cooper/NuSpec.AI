using System.Text.Json;

namespace NuSpec.AI.Tool.ProjectMetadata;

public static class AssetsReader
{
    public static AssetsInfo Read(string projectDir)
    {
        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
            return AssetsInfo.Empty;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = doc.RootElement;

            var folders = new List<string>();
            if (root.TryGetProperty("packageFolders", out var foldersEl) &&
                foldersEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var folder in foldersEl.EnumerateObject())
                    folders.Add(folder.Name);
            }

            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("targets", out var targetsEl) &&
                targetsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var tfm in targetsEl.EnumerateObject())
                {
                    if (tfm.Value.ValueKind != JsonValueKind.Object) continue;
                    foreach (var pkg in tfm.Value.EnumerateObject())
                    {
                        if (pkg.Value.TryGetProperty("type", out var typeEl) &&
                            !string.Equals(typeEl.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var key = pkg.Name;
                        var slash = key.IndexOf('/');
                        if (slash <= 0 || slash == key.Length - 1) continue;

                        var id = key[..slash];
                        var version = key[(slash + 1)..];

                        // First TFM wins
                        if (!resolved.ContainsKey(id))
                            resolved[id] = version;
                    }
                }
            }

            return new AssetsInfo
            {
                PackageFolders = folders,
                ResolvedVersions = resolved
            };
        }
        catch
        {
            return AssetsInfo.Empty;
        }
    }
}
