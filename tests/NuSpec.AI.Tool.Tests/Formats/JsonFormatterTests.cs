using System.Text.Json;
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class JsonFormatterTests : FormatterTestBase
{
    private readonly JsonFormatter _formatter = new();

    [Fact]
    public void FormatId_IsJson()
    {
        Assert.Equal("json", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapJson()
    {
        Assert.Equal("package-map.json", _formatter.FileName);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result); // throws if invalid
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void Serialize_IncludesPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_IsIndented()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        // OrderStatus has no documentation
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result);
        var orderStatus = doc.RootElement
            .GetProperty("publicSurface")
            .GetProperty("types")
            .EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "OrderStatus");
        Assert.False(orderStatus.TryGetProperty("documentation", out _));
    }
}
