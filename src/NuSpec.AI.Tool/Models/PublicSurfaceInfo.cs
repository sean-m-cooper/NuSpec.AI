using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class PublicSurfaceInfo
{
    [JsonPropertyName("namespaces")]
    public required IReadOnlyList<string> Namespaces { get; init; }

    [JsonPropertyName("types")]
    public required IReadOnlyList<TypeInfo> Types { get; init; }
}
