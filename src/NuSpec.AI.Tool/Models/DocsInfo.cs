using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class DocsInfo
{
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Params { get; init; }

    [JsonPropertyName("typeparams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? TypeParams { get; init; }

    [JsonPropertyName("returns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Returns { get; init; }

    [JsonPropertyName("remarks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; init; }

    [JsonPropertyName("example")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    [JsonPropertyName("exceptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DocsExceptionInfo>? Exceptions { get; init; }

    /// <summary>
    /// True when no fields are populated. Callers use this to decide whether to attach the object at all.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Summary is null
        && (Params is null || Params.Count == 0)
        && (TypeParams is null || TypeParams.Count == 0)
        && Returns is null
        && Remarks is null
        && Example is null
        && (Exceptions is null || Exceptions.Count == 0);
}

public sealed class DocsExceptionInfo
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("when")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? When { get; init; }
}
