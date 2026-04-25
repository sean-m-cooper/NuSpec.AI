using System.Text.Json;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Tests.Models;

public class PackageReferenceInfoTests
{
    [Fact]
    public void SerializesAllFieldsAsJson()
    {
        var info = new PackageReferenceInfo
        {
            Id = "Acme.OrdersCore",
            Version = "1.2.0",
            HasNuSpecAiMap = true
        };

        var json = JsonSerializer.Serialize(info);

        Assert.Contains("\"id\":\"Acme.OrdersCore\"", json);
        Assert.Contains("\"version\":\"1.2.0\"", json);
        Assert.Contains("\"hasNuSpecAiMap\":true", json);
    }

    [Fact]
    public void Version_CanBeNull()
    {
        var info = new PackageReferenceInfo
        {
            Id = "Acme.OrdersCore",
            Version = null,
            HasNuSpecAiMap = false
        };

        var json = JsonSerializer.Serialize(info);

        Assert.Contains("\"id\":\"Acme.OrdersCore\"", json);
        Assert.Contains("\"version\":null", json);
        Assert.Contains("\"hasNuSpecAiMap\":false", json);
    }
}
