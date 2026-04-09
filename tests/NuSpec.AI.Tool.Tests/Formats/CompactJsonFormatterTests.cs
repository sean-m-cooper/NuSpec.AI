using System.Text.Json;
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class CompactJsonFormatterTests : FormatterTestBase
{
    private readonly CompactJsonFormatter _formatter = new();

    [Fact]
    public void FormatId_IsCompact()
    {
        Assert.Equal("compact", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsCompactJson()
    {
        Assert.Equal("package-map.compact.json", _formatter.FileName);
    }

    [Fact]
    public void Serialize_IsValidJson()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        JsonDocument.Parse(result); // throws if invalid
    }

    [Fact]
    public void Serialize_UsesShortKeys()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\"v\":", result);      // schemaVersion
        Assert.Contains("\"n\":", result);      // name
        Assert.Contains("\"fn\":", result);     // fullName
        Assert.DoesNotContain("\"schemaVersion\":", result);
        Assert.DoesNotContain("\"fullName\":", result);
    }

    [Fact]
    public void Serialize_HasNoWhitespace()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var compact = _formatter.Serialize(map);
        Assert.True(compact.Length < json.Length,
            $"Compact ({compact.Length}) should be smaller than JSON ({json.Length})");
    }

    [Fact]
    public void Serialize_ContainsPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result);
        var types = doc.RootElement.GetProperty("s").GetProperty("t").EnumerateArray().ToList();
        var orderStatus = types.First(t => t.GetProperty("n").GetString() == "OrderStatus");
        Assert.False(orderStatus.TryGetProperty("doc", out _));
    }
}
