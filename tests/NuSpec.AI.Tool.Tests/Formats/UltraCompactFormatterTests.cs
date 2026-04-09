using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class UltraCompactFormatterTests : FormatterTestBase
{
    private readonly UltraCompactFormatter _formatter = new();

    [Fact]
    public void FormatId_IsUltra()
    {
        Assert.Equal("ultra", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapUltra()
    {
        Assert.Equal("package-map.ultra", _formatter.FileName);
    }

    [Fact]
    public void Serialize_StartsWithHeader()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.StartsWith("#NuSpec.AI/v1 Acme.Orders 1.0.0", result);
    }

    [Fact]
    public void Serialize_IncludesDescription()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#desc Order management library.", result);
    }

    [Fact]
    public void Serialize_IncludesTargetFramework()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#tfm net8.0", result);
    }

    [Fact]
    public void Serialize_IncludesDependency()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#dep Microsoft.EntityFrameworkCore", result);
    }

    [Fact]
    public void Serialize_ClassType_UsesAtC()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@c Order [entity]", result);
    }

    [Fact]
    public void Serialize_InterfaceType_UsesAtI()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@i IOrderRepository [repository]", result);
    }

    [Fact]
    public void Serialize_EnumType_UsesAtE()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@e OrderStatus", result);
    }

    [Fact]
    public void Serialize_EnumValues_OnOneLine()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(".ev Pending=0,Confirmed=1", result);
    }

    [Fact]
    public void Serialize_Properties_UseDotP()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(" .p Id:int", result);
    }

    [Fact]
    public void Serialize_Methods_UseDotM()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(" .m GetByIdAsync:", result);
    }

    [Fact]
    public void Serialize_TypeDocumentation_Included()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@c Order [entity] \"Represents a customer order.\"", result);
    }

    [Fact]
    public void Serialize_MemberDocumentation_Included()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\"Gets an order by ID.\"", result);
    }

    [Fact]
    public void Serialize_EmptyRoles_OmitsBrackets()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        // OrderStatus has no roles — should be "@e OrderStatus\n", not "@e OrderStatus []"
        Assert.Contains("@e OrderStatus" + Environment.NewLine, result);
        Assert.DoesNotContain("@e OrderStatus []", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var ultra = _formatter.Serialize(map);
        Assert.True(ultra.Length < json.Length,
            $"Ultra ({ultra.Length}) should be smaller than JSON ({json.Length})");
    }
}
