using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class PackageInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("tags")]
    public required IReadOnlyList<string> Tags { get; init; }

    [JsonPropertyName("targetFrameworks")]
    public required IReadOnlyList<string> TargetFrameworks { get; init; }
}
