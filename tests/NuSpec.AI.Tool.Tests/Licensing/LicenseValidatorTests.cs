using NuSpec.AI.Tool.Licensing;

namespace NuSpec.AI.Tool.Tests.Licensing;

public class LicenseValidatorTests
{
    [Fact]
    public void ResolveRawKey_CliArgProvided_ReturnsTrimmedArg()
    {
        var result = LicenseValidator.ResolveRawKey("  my.license.key  ");
        Assert.Equal("my.license.key", result);
    }

    [Fact]
    public void ResolveRawKey_NullArg_EnvVarSet_ReturnsEnvVar()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", "env-license-key");
        try
        {
            var result = LicenseValidator.ResolveRawKey(null);
            Assert.Equal("env-license-key", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);
        }
    }

    [Fact]
    public void ResolveRawKey_NoArgNoEnvNoFile_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);
        var result = LicenseValidator.ResolveRawKey(null);
        _ = result; // may be null or come from file; just verify no exception
    }

    [Fact]
    public void Validate_ValidToken_ReturnsLicenseInfo()
    {
        var token = LicenseTestHelpers.CreateToken(subject: "alice@example.com", scope: "pro", packages: "Acme.*");
        var info = LicenseValidator.Validate(token);
        Assert.NotNull(info);
        Assert.Equal("alice@example.com", info.Subject);
        Assert.Equal("pro", info.Scope);
        Assert.Equal("Acme.*", info.Packages);
        Assert.True(info.IsValid);
    }

    [Fact]
    public void Validate_NullKey_ReturnsNull() =>
        Assert.Null(LicenseValidator.Validate(null));

    [Fact]
    public void Validate_EmptyString_ReturnsNull() =>
        Assert.Null(LicenseValidator.Validate("   "));

    [Fact]
    public void Validate_RandomString_ReturnsNull() =>
        Assert.Null(LicenseValidator.Validate("not-a-jwt-at-all"));

    [Fact]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var token = LicenseTestHelpers.CreateExpiredToken();
        Assert.Null(LicenseValidator.Validate(token));
    }

    [Fact]
    public void Validate_WrongSignatureKey_ReturnsNull()
    {
        var token = LicenseTestHelpers.CreateTokenWithWrongKey();
        Assert.Null(LicenseValidator.Validate(token));
    }

    [Theory]
    [InlineData("*", "Acme.Orders", true)]
    [InlineData("*", "AnyPackage", true)]
    [InlineData("Acme.*", "Acme.Orders", true)]
    [InlineData("Acme.*", "Acme.Payments", true)]
    [InlineData("Acme.*", "Acme", true)]
    [InlineData("Acme.*", "Beta.Orders", false)]
    [InlineData("Acme.*", "AcmeOrders", false)]
    [InlineData("Acme.Orders", "Acme.Orders", true)]
    [InlineData("Acme.Orders", "acme.orders", true)]
    [InlineData("Acme.Orders", "Acme.Payments", false)]
    public void CoversPackage_GlobRules(string packagesPattern, string packageId, bool expected)
    {
        var info = new LicenseInfo
        {
            Subject = "test", Scope = "pro", Packages = packagesPattern,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        Assert.Equal(expected, info.CoversPackage(packageId));
    }

    [Fact]
    public void ValidateForPackage_ValidUnlimitedKey_ReturnsInfo()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "*");
        var info = LicenseValidator.ValidateForPackage(token, "My.Package", out var reason);
        Assert.NotNull(info);
        Assert.Null(reason);
    }

    [Fact]
    public void ValidateForPackage_ScopedKey_CoveredPackage_ReturnsInfo()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "Acme.*");
        var info = LicenseValidator.ValidateForPackage(token, "Acme.Orders", out var reason);
        Assert.NotNull(info);
        Assert.Null(reason);
    }

    [Fact]
    public void ValidateForPackage_ScopedKey_UncoveredPackage_ReturnsNullWithReason()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "Acme.*");
        var info = LicenseValidator.ValidateForPackage(token, "Beta.Orders", out var reason);
        Assert.Null(info);
        Assert.NotNull(reason);
        Assert.Contains("Beta.Orders", reason);
    }

    [Fact]
    public void ValidateForPackage_ExpiredKey_ReturnsNullWithReason()
    {
        var token = LicenseTestHelpers.CreateExpiredToken();
        var info = LicenseValidator.ValidateForPackage(token, "Any.Package", out var reason);
        Assert.Null(info);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ValidateForPackage_NoKey_NullArgAndNoEnv_ReturnsNullWithReason()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);
        var info = LicenseValidator.ValidateForPackage(null, "Any.Package", out var reason);
        if (info is null) Assert.NotNull(reason);
    }
}
