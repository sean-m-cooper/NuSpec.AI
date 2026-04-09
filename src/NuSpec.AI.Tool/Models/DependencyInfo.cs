using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class DependencyInfo
{
    [JsonPropertyName("packageReferences")]
    public required IReadOnlyList<string> PackageReferences { get; init; }

    [JsonPropertyName("frameworkReferences")]
    public required IReadOnlyList<string> FrameworkReferences { get; init; }
}
