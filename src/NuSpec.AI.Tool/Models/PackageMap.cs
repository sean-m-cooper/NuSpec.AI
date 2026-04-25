using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class PackageMap
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 3;

    [JsonPropertyName("package")]
    public required PackageInfo Package { get; init; }

    [JsonPropertyName("dependencies")]
    public required DependencyInfo Dependencies { get; init; }

    [JsonPropertyName("publicSurface")]
    public required PublicSurfaceInfo PublicSurface { get; init; }
}
