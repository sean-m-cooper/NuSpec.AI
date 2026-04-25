using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class YamlFormatterTests : FormatterTestBase
{
    private readonly YamlFormatter _formatter = new();

    [Fact]
    public void FormatId_IsYaml()
    {
        Assert.Equal("yaml", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapYaml()
    {
        Assert.Equal("package-map.yaml", _formatter.FileName);
    }

    [Fact]
    public void Serialize_ContainsPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_ContainsSchemaVersion()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("schemaVersion: 3", result);
    }

    [Fact]
    public void Serialize_ContainsTypeKind()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("kind: class", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var yaml = _formatter.Serialize(map);
        Assert.True(yaml.Length < json.Length,
            $"YAML ({yaml.Length} chars) should be smaller than JSON ({json.Length} chars)");
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        // Find the OrderStatus type entry specifically (not just any occurrence of the name)
        var typeEntryMarker = "- name: OrderStatus";
        var typeEntryIdx = result.IndexOf(typeEntryMarker, StringComparison.Ordinal);
        Assert.True(typeEntryIdx >= 0, "OrderStatus type entry not found in YAML");
        // Find the end of the OrderStatus type section (next type entry or end of string)
        var nextTypeIdx = result.IndexOf("- name:", typeEntryIdx + typeEntryMarker.Length, StringComparison.Ordinal);
        var orderStatusSection = nextTypeIdx > 0
            ? result[typeEntryIdx..nextTypeIdx]
            : result[typeEntryIdx..];
        Assert.DoesNotContain("documentation:", orderStatusSection);
    }
}
