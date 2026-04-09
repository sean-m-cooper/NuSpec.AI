using System.Text.Json;
using System.Text.Json.Serialization;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

public sealed class JsonFormatter : IFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FormatId => "json";
    public string FileName => "package-map.json";

    public string Serialize(PackageMap packageMap) =>
        JsonSerializer.Serialize(packageMap, Options);
}
