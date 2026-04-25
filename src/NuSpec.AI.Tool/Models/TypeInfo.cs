using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class TypeInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; init; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; init; }

    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("roles")]
    public required IReadOnlyList<string> Roles { get; init; }

    [JsonPropertyName("documentation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Documentation { get; init; }

    [JsonPropertyName("docs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DocsInfo? Docs { get; init; }

    [JsonPropertyName("members")]
    public required IReadOnlyList<MemberInfo> Members { get; init; }
}
