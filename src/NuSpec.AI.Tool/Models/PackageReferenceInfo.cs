using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class PackageReferenceInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string? Version { get; init; }

    [JsonPropertyName("hasNuSpecAiMap")]
    public required bool HasNuSpecAiMap { get; init; }
}
