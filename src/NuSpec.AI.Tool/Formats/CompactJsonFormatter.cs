using System.Text.Json;
using System.Text.Json.Nodes;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

/// <summary>
/// Produces minified JSON with abbreviated keys to reduce token count by ~40-50%.
/// Key mapping: schemaVersion→v, package→p, dependencies→d, publicSurface→s,
/// namespaces→nss, types→t, name→n, fullName→fn, namespace→ns, kind→k,
/// roles→r, documentation→doc, members→m, signature→sig,
/// packageReferences→pr, frameworkReferences→fr, description→desc,
/// tags→tg, targetFrameworks→tfm, id→id (unchanged), version→ver
/// </summary>
public sealed class CompactJsonFormatter : IFormatter
{
    private static readonly JsonSerializerOptions FullOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FormatId => "compact";
    public string FileName => "package-map.compact.json";

    public string Serialize(PackageMap packageMap)
    {
        // Serialize to full JSON first (JsonPropertyName attributes control key names), then remap keys
        var fullJson = JsonSerializer.Serialize(packageMap, FullOptions);
        var node = JsonNode.Parse(fullJson)!;
        var remapped = RemapNode(node);
        return remapped.ToJsonString();
    }

    private static JsonNode RemapNode(JsonNode node)
    {
        return node switch
        {
            JsonObject obj => RemapObject(obj),
            JsonArray arr => RemapArray(arr),
            _ => node.DeepClone()
        };
    }

    private static JsonObject RemapObject(JsonObject obj)
    {
        var result = new JsonObject();
        foreach (var (key, value) in obj)
        {
            var shortKey = ShortenKey(key);
            result[shortKey] = value is null ? null : RemapNode(value);
        }
        return result;
    }

    private static JsonArray RemapArray(JsonArray arr)
    {
        var result = new JsonArray();
        foreach (var item in arr)
            result.Add(item is null ? null : RemapNode(item));
        return result;
    }

    private static string ShortenKey(string key) => key switch
    {
        "schemaVersion"       => "v",
        "package"             => "p",
        "dependencies"        => "d",
        "publicSurface"       => "s",
        "namespaces"          => "nss",
        "types"               => "t",
        "name"                => "n",
        "fullName"            => "fn",
        "namespace"           => "ns",
        "kind"                => "k",
        "roles"               => "r",
        "documentation"       => "doc",
        "members"             => "m",
        "signature"           => "sig",
        "packageReferences"   => "pr",
        "frameworkReferences" => "fr",
        "description"         => "desc",
        "tags"                => "tg",
        "targetFrameworks"    => "tfm",
        "version"             => "ver",
        _                     => key
    };
}
