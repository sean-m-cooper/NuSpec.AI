using NuSpec.AI.Tool.Licensing;
using Xunit;

namespace NuSpec.AI.Tool.Tests.Licensing;

public class LicenseInfoTests
{
    private static LicenseInfo ValidProLicense(string packages = "*") => new()
    {
        Subject = "test@example.com",
        Scope = "pro",
        Packages = packages,
        IssuedAt = DateTimeOffset.UtcNow.AddHours(-1),
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
    };

    [Fact]
    public void IsValid_ValidProLicense_ReturnsTrue()
    {
        var info = ValidProLicense();
        Assert.True(info.IsValid);
    }

    [Fact]
    public void IsValid_WrongScope_ReturnsFalse()
    {
        var info = ValidProLicense() with { Scope = "basic" };
        Assert.False(info.IsValid);
    }

    [Fact]
    public void IsValid_ExpiredToken_ReturnsFalse()
    {
        var info = ValidProLicense() with
        {
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        Assert.False(info.IsValid);
    }

    [Theory]
    [InlineData("*", "Anything.Goes", true)]
    [InlineData("Acme.*", "Acme.Orders", true)]
    [InlineData("Acme.*", "Acme", true)]
    [InlineData("Acme.*", "Other.Package", false)]
    [InlineData("Acme.Orders", "Acme.Orders", true)]
    [InlineData("Acme.Orders", "acme.orders", true)]   // case-insensitive
    [InlineData("Acme.Orders", "Acme.Shipping", false)]
    public void CoversPackage_GlobRules(string packages, string packageId, bool expected)
    {
        var info = ValidProLicense(packages);
        Assert.Equal(expected, info.CoversPackage(packageId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CoversPackage_NullOrEmptyPackageId_ReturnsFalse(string? packageId)
    {
        var info = ValidProLicense();
        Assert.False(info.CoversPackage(packageId!));
    }
}
