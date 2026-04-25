namespace NuSpec.AI.Tool.ProjectMetadata;

public sealed class AssetsInfo
{
    public required IReadOnlyList<string> PackageFolders { get; init; }
    public required IReadOnlyDictionary<string, string> ResolvedVersions { get; init; }

    public static AssetsInfo Empty { get; } = new()
    {
        PackageFolders = Array.Empty<string>(),
        ResolvedVersions = new Dictionary<string, string>()
    };
}
