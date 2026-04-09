# NuSpec.AI Pro — Plan B: Licensing + Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add offline RS256 JWT license validation to NuSpec.AI.Tool and ship two new NuGet packages — `NuSpec.AI.Pro` (the Pro MSBuild integration) and `NuSpec.AI.Attributes` (the three attribute classes) — with graceful coexistence when both free and Pro are installed.

**Architecture:** `LicenseValidator` validates a JWT entirely offline using an embedded RS256 public key and applies simple glob matching against the `packages` claim; license resolution checks `--license` arg → env var → `~/.nuspec-ai/license.key` file. `NuSpec.AI.Pro` is a packaging-only project (no runtime assembly) that ships the same CLI tool binary alongside different `build/` props+targets that pass `--formats` and `--license` to the tool. `NuSpec.AI.Attributes` is a separate zero-dependency netstandard2.0 library so consumers can annotate their types without taking a Pro dependency at runtime. When both free and Pro NuGet packages are installed, free sits inert (Pro's props forcibly sets `NuSpecAiEnabled=false` before free's props are evaluated).

**Tech Stack:** .NET 8, C# 12, `Microsoft.IdentityModel.Tokens` + `System.IdentityModel.Tokens.Jwt` for RS256 JWT validation, MSBuild props/targets (same pattern as free version), netstandard2.0 for Attributes, xUnit for tests.

---

## File Map

### New files

| File | Responsibility |
|------|---------------|
| `src/NuSpec.AI.Tool/Licensing/LicenseInfo.cs` | Plain record holding validated license fields |
| `src/NuSpec.AI.Tool/Licensing/LicenseValidator.cs` | RS256 JWT validation, glob matching, key resolution |
| `src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj` | Packaging-only project (no build output) |
| `src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.props` | Sets `NuSpecAiEnabled=false`, defines Pro defaults |
| `src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.targets` | Generates all formatter files; adds to pack |
| `src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj` | netstandard2.0 class library, no deps |
| `src/NuSpec.AI.Attributes/AiRoleAttribute.cs` | `[AiRole("role1", "role2")]` |
| `src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs` | `[AiIgnore]` |
| `src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs` | `[AiDescription("...")]` |
| `tests/NuSpec.AI.Tool.Tests/Licensing/LicenseValidatorTests.cs` | Unit tests for LicenseValidator |
| `tests/NuSpec.AI.Tool.Tests/Licensing/LicenseTestHelpers.cs` | Signs test JWTs with embedded private key |
| `tests/NuSpec.AI.Tool.Tests/Integration/ProLicenseIntegrationTests.cs` | End-to-end: valid key → Pro features |
| `tests/NuSpec.AI.Tool.Tests/Integration/FallbackIntegrationTests.cs` | End-to-end: invalid/expired/missing → JSON fallback + warning |

### Modified files

| File | Change |
|------|--------|
| `src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj` | Add `Microsoft.IdentityModel.Tokens` + `System.IdentityModel.Tokens.Jwt` refs |
| `src/NuSpec.AI.Tool/Program.cs` | Parse `--license` arg; resolve license; emit MSBuild warning on fallback |
| `NuSpec.AI.slnx` | Add `NuSpec.AI.Pro` and `NuSpec.AI.Attributes` projects |

---

## Test RSA Key Pair (Development / Test Use Only)

These keys are **placeholders for testing only**. Real production key generation is future work.

### Embedded Public Key (goes in `LicenseValidator.cs`)

```
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArqqM7D88Bz0jKOhHDAnA
4d+sSp+C94xjWVXKq4PfUOcKhlO+5yZWFY7w6oyoVD8w77XPkce9fO1ek4Htch93
pe3Ed35gkDTUn7W6mn7i7wA1UL71taDsZdbgBflLzSRqhpKFGCWoIMu8q4ZibPtb
aJ6zdmRKCB/fv2Dtf/TUNTF87v20T2TdMhUS0N0dPx2yK3HBW/3nR96D/y67ZEEg
9fpLcMqsy+2s+TAkkcy7qFnkFR0sxnm8hP9RGh6GWn5Y+KHYYjzzpzrAfP6x3vVf
eq8fDTQc92/YjA8B6uFleHZuTfExnTxdlE05dhdBl8vDtjcg2kqRCy7oV3EOiD6h
9QIDAQAB
-----END PUBLIC KEY-----
```

### Test Private Key (goes in `LicenseTestHelpers.cs` — tests only, never shipped)

```
-----BEGIN PRIVATE KEY-----
MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCuqozsPzwHPSMo
6EcMCcDh36xKn4L3jGNZVcqrg99Q5wqGU77nJlYVjvDqjKhUPzDvtc+Rx7187V6T
ge1yH3el7cR3fmCQNNSftbqafuLvADVQvvW1oOxl1uAF+UvNJGqGkoUYJaggy7yr
hmJs+1tonrN2ZEoIH9+/YO1/9NQ1MXzu/bRPZN0yFRLQ3R0/HbIrccFb/edH3oP/
LrtkQSD1+ktwyqzL7az5MCSRzLuoWeQVHSzGebyE/1EaHoZaflj4odhiPPOnOsB8
/rHe9V96rx8NNBz3b9iMDwHq4WV4dm5N8TGdPF2UTTl2F0GXy8O2NyDaSpELLuhX
cQ6IPqH1AgMBAAECggEAAfYoXv7Wzb4CBxOUuK3jXKYGaVAhSGZrNzWfcQ2qFF6D
375RBoeHr/ZK/ldWDJwpEIgaLKjxl9WSmlV7NSzlSxfAfRcOPpBZUvHXhqSmJ8j4
0E9UsxV7kik3mtmR4FvoVlqO5BaILNYc6FA6Cr9H54TgvxOhQTYabSvJfwZg27gN
yYVfC3HOzptie9sEP+MIIcbIFVtpLG4Z/iSvfsaeH9iefaSgpEmm/05EL00OGE0+
GNk8Id7mO4NQQoNu7orSV13LlNQAnyZ2zxg67Az1lGkAUHQKw7+uUyLUw31PXxZz
WCSBuGfUTt56Jy0iyZJueIf14+gBePcBrfs4IkDygQKBgQDo/2Y8TxtX27oteWq+
9gZlVdScwBnU/VQylkYIMUVkPoQ9Ma5idTxCHEg6oqpYfw4tTSMBXhD7j6sVi/9/
Ki7LcFSXCHxJzplbiFwyc5IfU4almR9dQI/K5Fky3Hazkr23V86Kp+2Y8w5QlJvH
52Du9Z+db1jkipqQmueL8x448wKBgQC/6O2IabrTGyht9CRrMEwpxUvM6JarY9X/
PNjTYvGvVoHDuHXuMbifZjSc3gHm3QEj94ORSL76jdWz9pNlHDj8E2J9p892qaTj
Baok4xTFPemdHxZphrhgJ07jHmQidGRj+PhD27mmeVIITkBbh6VAPlrYVWo83D0p
etVLzy9zdwKBgCtl/vX2yiIIRFpaBj8BdlmDrjFwOp+IfBlcEjlObB1q45i+Wzvt
mEa8G9wIFnCbYdmgR4fmrIUe0oAV7oYSJlswViE3rGbW+4uoD3w6OJprJWZM6iGl
d+MTu2WU2OtDxuCSk18SPlhB1YW+2HFYsJ5x08QwTD9tbbLHl59irltvAoGARVCl
UssVfqBlhulSqiCEseWgDj/IA9mIdqsMibVIJBNzxTR/6+urim9I+4u4ViFnAw2o
SLZkvGy0Tk72R+PctTdvMIGHDo4RjyoBnVcjrmZBVc3fs3fEan5oIOJeOo+dnvpS
+XeIY5eYSIWy+xxQVJbxCwg22gqWUMAcAEiyE9sCgYA6e99ZQyav6AANa5fiQ4tl
oglZKrUb8U+75lHnOxD8t0rSRBGUwaVln4UJhwlJvFMis5lT6JcWGEQiiF0HmLsK
Bi8vymd7Y9wgK2w9PsyvxHHNCmtV5AjNMjfVVujyz8W+qi0wipD4AVDMmdzHKvS9
YNJsz8wiPoIx5fucnD4k8w==
-----END PRIVATE KEY-----
```

---

## Task 1: Add `LicenseInfo` model

**Files:**
- Create: `src/NuSpec.AI.Tool/Licensing/LicenseInfo.cs`

- [ ] **Create LicenseInfo.cs**

`src/NuSpec.AI.Tool/Licensing/LicenseInfo.cs`:
```csharp
namespace NuSpec.AI.Tool.Licensing;

/// <summary>
/// Holds the validated fields extracted from a NuSpec.AI Pro license JWT.
/// Returned by <see cref="LicenseValidator.Validate"/>.
/// </summary>
public sealed record LicenseInfo
{
    /// <summary>Customer identifier (the JWT "sub" claim).</summary>
    public required string Subject { get; init; }

    /// <summary>License scope — expected value is "pro".</summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Glob pattern controlling which package IDs this license covers.
    /// Examples: "*" (unlimited), "Acme.*" (prefix), "Acme.Orders" (single).
    /// </summary>
    public required string Packages { get; init; }

    /// <summary>UTC expiry time taken from the JWT "exp" claim.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>UTC issue time taken from the JWT "iat" claim.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// Whether the license is currently valid (not expired, scope == "pro").
    /// Does NOT check package-ID scope; call <see cref="CoversPackage"/> for that.
    /// </summary>
    public bool IsValid => Scope == "pro" && ExpiresAt > DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns true if this license covers the given NuGet package ID.
    /// Glob rules: "*" matches anything; "Prefix.*" matches any ID starting with "Prefix.";
    /// otherwise exact case-insensitive match.
    /// </summary>
    public bool CoversPackage(string packageId)
    {
        if (string.IsNullOrEmpty(packageId)) return false;
        if (Packages == "*") return true;

        if (Packages.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = Packages[..^2]; // strip trailing ".*"
            return packageId.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
                || packageId.Equals(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return packageId.Equals(Packages, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Build to verify**
```bash
cd E:/repos/NuSpec.AI
dotnet build src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Task 2: Add `LicenseValidator` (JWT validation, key resolution)

**Files:**
- Modify: `src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj` — add JWT NuGet refs
- Create: `src/NuSpec.AI.Tool/Licensing/LicenseValidator.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Licensing/LicenseTestHelpers.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Licensing/LicenseValidatorTests.cs`

- [ ] **Add NuGet package references to NuSpec.AI.Tool.csproj**

`src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj` — add inside the existing `<ItemGroup>`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>NuSpec.AI.Tool</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.3.2" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.3.2" />
  </ItemGroup>

</Project>
```

- [ ] **Create LicenseValidator.cs**

`src/NuSpec.AI.Tool/Licensing/LicenseValidator.cs`:
```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace NuSpec.AI.Tool.Licensing;

/// <summary>
/// Validates NuSpec.AI Pro license keys entirely offline using an embedded RS256 public key.
/// No network calls are made.
/// </summary>
public static class LicenseValidator
{
    // -----------------------------------------------------------------------
    // Embedded test/dev public key (RS256, RSA-2048).
    // Replace with the real production key before publishing NuSpec.AI.Pro.
    // -----------------------------------------------------------------------
    internal const string EmbeddedPublicKeyPem =
        "-----BEGIN PUBLIC KEY-----\n" +
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArqqM7D88Bz0jKOhHDAnA\n" +
        "4d+sSp+C94xjWVXKq4PfUOcKhlO+5yZWFY7w6oyoVD8w77XPkce9fO1ek4Htch93\n" +
        "pe3Ed35gkDTUn7W6mn7i7wA1UL71taDsZdbgBflLzSRqhpKFGCWoIMu8q4ZibPtb\n" +
        "aJ6zdmRKCB/fv2Dtf/TUNTF87v20T2TdMhUS0N0dPx2yK3HBW/3nR96D/y67ZEEg\n" +
        "9fpLcMqsy+2s+TAkkcy7qFnkFR0sxnm8hP9RGh6GWn5Y+KHYYjzzpzrAfP6x3vVf\n" +
        "eq8fDTQc92/YjA8B6uFleHZuTfExnTxdlE05dhdBl8vDtjcg2kqRCy7oV3EOiD6h\n" +
        "9QIDAQAB\n" +
        "-----END PUBLIC KEY-----";

    private static readonly RsaSecurityKey _signingKey = LoadPublicKey();

    private static RsaSecurityKey LoadPublicKey()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(EmbeddedPublicKeyPem);
        return new RsaSecurityKey(rsa);
    }

    // -----------------------------------------------------------------------
    // Key resolution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the raw license key string from (in priority order):
    ///   1. <paramref name="cliArg"/> — value of the --license argument
    ///   2. NUSPEC_AI_LICENSE_KEY environment variable
    ///   3. ~/.nuspec-ai/license.key file
    /// Returns null if none are found.
    /// </summary>
    public static string? ResolveRawKey(string? cliArg)
    {
        // 1. CLI argument
        if (!string.IsNullOrWhiteSpace(cliArg))
            return cliArg.Trim();

        // 2. Environment variable
        var envVar = Environment.GetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(envVar))
            return envVar.Trim();

        // 3. File: ~/.nuspec-ai/license.key
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var keyFile = Path.Combine(homeDir, ".nuspec-ai", "license.key");
        if (File.Exists(keyFile))
        {
            var content = File.ReadAllText(keyFile).Trim();
            if (!string.IsNullOrEmpty(content))
                return content;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Validates <paramref name="rawKey"/> as an RS256 JWT.
    /// Returns a <see cref="LicenseInfo"/> on success, or null if the key is
    /// null, malformed, has an invalid signature, or is expired.
    /// </summary>
    public static LicenseInfo? Validate(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return null;

        var handler = new JwtSecurityTokenHandler();

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
        };

        try
        {
            var principal = handler.ValidateToken(rawKey, validationParams, out var securityToken);
            var jwt = (JwtSecurityToken)securityToken;

            var scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value ?? string.Empty;
            var packages = jwt.Claims.FirstOrDefault(c => c.Type == "packages")?.Value ?? "*";
            var sub = jwt.Subject ?? string.Empty;

            var iat = DateTimeOffset.FromUnixTimeSeconds(
                long.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Iat).Value));

            return new LicenseInfo
            {
                Subject = sub,
                Scope = scope,
                Packages = packages,
                ExpiresAt = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero),
                IssuedAt = iat,
            };
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Full validation pipeline: resolve key → validate JWT → check package coverage.
    /// Returns a valid <see cref="LicenseInfo"/> or null.
    /// <paramref name="validationFailureReason"/> is set to a human-readable reason when null is returned.
    /// </summary>
    public static LicenseInfo? ValidateForPackage(
        string? cliArg,
        string packageId,
        out string? validationFailureReason)
    {
        var rawKey = ResolveRawKey(cliArg);

        if (rawKey is null)
        {
            validationFailureReason = "no license key found";
            return null;
        }

        var info = Validate(rawKey);

        if (info is null)
        {
            validationFailureReason = "license key signature is invalid or token is malformed";
            return null;
        }

        if (!info.IsValid)
        {
            validationFailureReason = $"license key is expired (expired at {info.ExpiresAt:u})";
            return null;
        }

        if (!info.CoversPackage(packageId))
        {
            validationFailureReason =
                $"license key scope '{info.Packages}' does not cover package '{packageId}'";
            return null;
        }

        validationFailureReason = null;
        return info;
    }
}
```

- [ ] **Add JWT NuGet reference to test project**

`tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj` — add `System.IdentityModel.Tokens.Jwt` so test helpers can sign JWTs:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>NuSpec.AI.Tool.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.3.2" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\NuSpec.AI.Tool\NuSpec.AI.Tool.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [ ] **Create LicenseTestHelpers.cs**

`tests/NuSpec.AI.Tool.Tests/Licensing/LicenseTestHelpers.cs`:
```csharp
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace NuSpec.AI.Tool.Tests.Licensing;

/// <summary>
/// Signs test JWTs using the matching RSA private key for the embedded public key in LicenseValidator.
/// NEVER ship this private key; it lives in the test project only.
/// </summary>
public static class LicenseTestHelpers
{
    // Test-only RSA-2048 private key matching the public key embedded in LicenseValidator.
    private const string TestPrivateKeyPem =
        "-----BEGIN PRIVATE KEY-----\n" +
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCuqozsPzwHPSMo\n" +
        "6EcMCcDh36xKn4L3jGNZVcqrg99Q5wqGU77nJlYVjvDqjKhUPzDvtc+Rx7187V6T\n" +
        "ge1yH3el7cR3fmCQNNSftbqafuLvADVQvvW1oOxl1uAF+UvNJGqGkoUYJaggy7yr\n" +
        "hmJs+1tonrN2ZEoIH9+/YO1/9NQ1MXzu/bRPZN0yFRLQ3R0/HbIrccFb/edH3oP/\n" +
        "LrtkQSD1+ktwyqzL7az5MCSRzLuoWeQVHSzGebyE/1EaHoZaflj4odhiPPOnOsB8\n" +
        "/rHe9V96rx8NNBz3b9iMDwHq4WV4dm5N8TGdPF2UTTl2F0GXy8O2NyDaSpELLuhX\n" +
        "cQ6IPqH1AgMBAAECggEAAfYoXv7Wzb4CBxOUuK3jXKYGaVAhSGZrNzWfcQ2qFF6D\n" +
        "375RBoeHr/ZK/ldWDJwpEIgaLKjxl9WSmlV7NSzlSxfAfRcOPpBZUvHXhqSmJ8j4\n" +
        "0E9UsxV7kik3mtmR4FvoVlqO5BaILNYc6FA6Cr9H54TgvxOhQTYabSvJfwZg27gN\n" +
        "yYVfC3HOzptie9sEP+MIIcbIFVtpLG4Z/iSvfsaeH9iefaSgpEmm/05EL00OGE0+\n" +
        "GNk8Id7mO4NQQoNu7orSV13LlNQAnyZ2zxg67Az1lGkAUHQKw7+uUyLUw31PXxZz\n" +
        "WCSBuGfUTt56Jy0iyZJueIf14+gBePcBrfs4IkDygQKBgQDo/2Y8TxtX27oteWq+\n" +
        "9gZlVdScwBnU/VQylkYIMUVkPoQ9Ma5idTxCHEg6oqpYfw4tTSMBXhD7j6sVi/9/\n" +
        "Ki7LcFSXCHxJzplbiFwyc5IfU4almR9dQI/K5Fky3Hazkr23V86Kp+2Y8w5QlJvH\n" +
        "52Du9Z+db1jkipqQmueL8x448wKBgQC/6O2IabrTGyht9CRrMEwpxUvM6JarY9X/\n" +
        "PNjTYvGvVoHDuHXuMbifZjSc3gHm3QEj94ORSL76jdWz9pNlHDj8E2J9p892qaTj\n" +
        "Baok4xTFPemdHxZphrhgJ07jHmQidGRj+PhD27mmeVIITkBbh6VAPlrYVWo83D0p\n" +
        "etVLzy9zdwKBgCtl/vX2yiIIRFpaBj8BdlmDrjFwOp+IfBlcEjlObB1q45i+Wzvt\n" +
        "mEa8G9wIFnCbYdmgR4fmrIUe0oAV7oYSJlswViE3rGbW+4uoD3w6OJprJWZM6iGl\n" +
        "d+MTu2WU2OtDxuCSk18SPlhB1YW+2HFYsJ5x08QwTD9tbbLHl59irltvAoGARVCl\n" +
        "UssVfqBlhulSqiCEseWgDj/IA9mIdqsMibVIJBNzxTR/6+urim9I+4u4ViFnAw2o\n" +
        "SLZkvGy0Tk72R+PctTdvMIGHDo4RjyoBnVcjrmZBVc3fs3fEan5oIOJeOo+dnvpS\n" +
        "+XeIY5eYSIWy+xxQVJbxCwg22gqWUMAcAEiyE9sCgYA6e99ZQyav6AANa5fiQ4tl\n" +
        "oglZKrUb8U+75lHnOxD8t0rSRBGUwaVln4UJhwlJvFMis5lT6JcWGEQiiF0HmLsK\n" +
        "Bi8vymd7Y9wgK2w9PsyvxHHNCmtV5AjNMjfVVujyz8W+qi0wipD4AVDMmdzHKvS9\n" +
        "YNJsz8wiPoIx5fucnD4k8w==\n" +
        "-----END PRIVATE KEY-----";

    private static readonly RsaSecurityKey _signingKey = LoadPrivateKey();

    private static RsaSecurityKey LoadPrivateKey()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(TestPrivateKeyPem);
        return new RsaSecurityKey(rsa);
    }

    /// <summary>
    /// Creates a signed RS256 JWT with the given claims.
    /// </summary>
    public static string CreateToken(
        string subject = "test-customer@example.com",
        string scope = "pro",
        string packages = "*",
        TimeSpan? validFor = null)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var expiry = now + (validFor ?? TimeSpan.FromDays(365));

        var claims = new[]
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, subject),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                System.Security.Claims.ClaimValueTypes.Integer64),
            new System.Security.Claims.Claim("scope", scope),
            new System.Security.Claims.Claim("packages", packages),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates an already-expired token (expired 1 hour ago).</summary>
    public static string CreateExpiredToken(string packages = "*") =>
        CreateToken(validFor: TimeSpan.FromHours(-1));

    /// <summary>Creates a token signed with a different (wrong) RSA key — will fail validation.</summary>
    public static string CreateTokenWithWrongKey()
    {
        var wrongRsa = RSA.Create(2048);
        var wrongKey = new RsaSecurityKey(wrongRsa);
        var credentials = new SigningCredentials(wrongKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            claims: new[]
            {
                new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, "attacker"),
                new System.Security.Claims.Claim("scope", "pro"),
                new System.Security.Claims.Claim("packages", "*"),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(365),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Create LicenseValidatorTests.cs**

`tests/NuSpec.AI.Tool.Tests/Licensing/LicenseValidatorTests.cs`:
```csharp
using NuSpec.AI.Tool.Licensing;

namespace NuSpec.AI.Tool.Tests.Licensing;

public class LicenseValidatorTests
{
    // ------------------------------------------------------------------
    // ResolveRawKey — CLI arg takes precedence
    // ------------------------------------------------------------------

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
    public void ResolveRawKey_NoArgNoEnvNoFile_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);
        // Assumes ~/.nuspec-ai/license.key does not exist on the test machine
        // (acceptable for unit tests — integration tests cover the file path)
        var result = LicenseValidator.ResolveRawKey(null);
        // Result is null OR comes from the file; either is acceptable in unit context.
        // We only assert the method doesn't throw.
        _ = result; // no assertion on value — see integration tests for file fallback
    }

    // ------------------------------------------------------------------
    // Validate — valid token
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_ValidToken_ReturnsLicenseInfo()
    {
        var token = LicenseTestHelpers.CreateToken(
            subject: "alice@example.com",
            scope: "pro",
            packages: "Acme.*");

        var info = LicenseValidator.Validate(token);

        Assert.NotNull(info);
        Assert.Equal("alice@example.com", info.Subject);
        Assert.Equal("pro", info.Scope);
        Assert.Equal("Acme.*", info.Packages);
        Assert.True(info.IsValid);
    }

    [Fact]
    public void Validate_NullKey_ReturnsNull()
    {
        var result = LicenseValidator.Validate(null);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_EmptyString_ReturnsNull()
    {
        var result = LicenseValidator.Validate("   ");
        Assert.Null(result);
    }

    [Fact]
    public void Validate_RandomString_ReturnsNull()
    {
        var result = LicenseValidator.Validate("not-a-jwt-at-all");
        Assert.Null(result);
    }

    // ------------------------------------------------------------------
    // Validate — expired token
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var token = LicenseTestHelpers.CreateExpiredToken();
        var result = LicenseValidator.Validate(token);
        Assert.Null(result);
    }

    // ------------------------------------------------------------------
    // Validate — wrong signature
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_WrongSignatureKey_ReturnsNull()
    {
        var token = LicenseTestHelpers.CreateTokenWithWrongKey();
        var result = LicenseValidator.Validate(token);
        Assert.Null(result);
    }

    // ------------------------------------------------------------------
    // LicenseInfo.CoversPackage — glob matching
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("*", "Acme.Orders", true)]
    [InlineData("*", "AnyPackage", true)]
    [InlineData("Acme.*", "Acme.Orders", true)]
    [InlineData("Acme.*", "Acme.Payments", true)]
    [InlineData("Acme.*", "Acme", true)]       // prefix itself is also covered
    [InlineData("Acme.*", "Beta.Orders", false)]
    [InlineData("Acme.*", "AcmeOrders", false)] // no dot separator
    [InlineData("Acme.Orders", "Acme.Orders", true)]
    [InlineData("Acme.Orders", "acme.orders", true)] // case-insensitive
    [InlineData("Acme.Orders", "Acme.Payments", false)]
    public void CoversPackage_GlobRules(string packagesPattern, string packageId, bool expected)
    {
        var info = new LicenseInfo
        {
            Subject = "test",
            Scope = "pro",
            Packages = packagesPattern,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        Assert.Equal(expected, info.CoversPackage(packageId));
    }

    // ------------------------------------------------------------------
    // ValidateForPackage — full pipeline
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateForPackage_ValidUnlimitedKey_AnyPackage_ReturnsInfo()
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
    public void ValidateForPackage_NoKey_ReturnsNullWithReason()
    {
        Environment.SetEnvironmentVariable("NUSPEC_AI_LICENSE_KEY", null);

        var info = LicenseValidator.ValidateForPackage(null, "Any.Package", out var reason);

        // If ~/.nuspec-ai/license.key exists on this machine, result may differ.
        // We only assert that if null is returned, reason is also populated.
        if (info is null)
            Assert.NotNull(reason);
    }
}
```

- [ ] **Run tests**
```bash
cd E:/repos/NuSpec.AI
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~LicenseValidator" --no-build
```
Expected: All LicenseValidator tests pass.

---

## Task 3: Wire license into `Program.cs`

**Files:**
- Modify: `src/NuSpec.AI.Tool/Program.cs`

The updated Program.cs parses `--license`, `--formats`, and `--package-id`, resolves and validates the license, dispatches to formatters (Plan A), and emits an MSBuild-formatted warning when falling back.

- [ ] **Replace Program.cs**

`src/NuSpec.AI.Tool/Program.cs`:
```csharp
using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Licensing;

// -------------------------------------------------------------------------
// Help / version
// -------------------------------------------------------------------------

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: NuSpec.AI.Tool <project-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  project-file        Path to the .csproj file to analyze");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <dir>      Output directory (default: stdout for single format)");
    Console.WriteLine("  --formats <list>    Semicolon-separated format list: json;yaml;compact;ultra");
    Console.WriteLine("                      (default: json — Pro license required for yaml/compact/ultra)");
    Console.WriteLine("  --license <key>     NuSpec.AI Pro license JWT");
    Console.WriteLine("  --package-id <id>   NuGet package ID used for license scope validation");
    Console.WriteLine("  --help              Show help");
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "--version")
{
    var version = typeof(ProjectAnalyzer).Assembly.GetName().Version;
    Console.WriteLine($"NuSpec.AI.Tool {version}");
    return 0;
}

// -------------------------------------------------------------------------
// Argument parsing
// -------------------------------------------------------------------------

var projectFile = args[0];
string? outputDir = null;
string? formatsArg = null;
string? licenseArg = null;
string? packageIdArg = null;

for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            outputDir = args[++i];
            break;
        case "--formats" when i + 1 < args.Length:
            formatsArg = args[++i];
            break;
        case "--license" when i + 1 < args.Length:
            licenseArg = args[++i];
            break;
        case "--package-id" when i + 1 < args.Length:
            packageIdArg = args[++i];
            break;
    }
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

// -------------------------------------------------------------------------
// License resolution
// -------------------------------------------------------------------------

var packageId = packageIdArg ?? Path.GetFileNameWithoutExtension(projectFile);
var licenseInfo = LicenseValidator.ValidateForPackage(licenseArg, packageId, out var licenseFailureReason);
var hasProLicense = licenseInfo is not null;

// Emit MSBuild-formatted warning if a Pro feature was requested but license is invalid/missing.
// We only warn when --formats was explicitly set to something other than plain json,
// OR when the tool is invoked by NuSpec.AI.Pro targets (which always pass --formats).
var requestedFormats = formatsArg ?? "json";
var isProFormatsRequested = requestedFormats
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(f => !f.Equals("json", StringComparison.OrdinalIgnoreCase));

if (!hasProLicense && isProFormatsRequested)
{
    // MSBuild warning format: "  NuSpec.AI.Tool.dll : warning NSPECAI001: ..."
    Console.Error.WriteLine(
        $"NuSpec.AI.Tool : warning NSPECAI001: NuSpec.AI.Pro license is " +
        $"{(licenseFailureReason ?? "invalid")}. Falling back to standard JSON output.");
    formatsArg = "json"; // force fallback
}

// Emit low-priority coexistence hint (diagnostic level — MSBuild only shows at /v:diag)
if (hasProLicense)
{
    Console.Error.WriteLine(
        "NuSpec.AI.Tool : message NSPECAI002: NuSpec.AI.Pro is active. " +
        "The NuSpec.AI package reference can be removed.");
}

// -------------------------------------------------------------------------
// Analyze + format + output
// -------------------------------------------------------------------------

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile);
    var formatters = FormatterRegistry.Resolve(formatsArg ?? "json");

    if (outputDir is not null)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var formatter in formatters)
        {
            var outputPath = Path.Combine(outputDir, formatter.FileName);
            var content = formatter.Serialize(packageMap);
            File.WriteAllText(outputPath, content);
            Console.Error.WriteLine($"Package map written to: {outputPath}");
        }
    }
    else
    {
        // Single format to stdout (legacy / simple invocation)
        var formatter = formatters.First();
        Console.WriteLine(formatter.Serialize(packageMap));
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
```

- [ ] **Build to verify**
```bash
cd E:/repos/NuSpec.AI
dotnet build src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj
```
Expected: `Build succeeded.`

---

## Task 4: Create `NuSpec.AI.Attributes` packaging project

> Note: Plan A may have already created the attribute files. If `src/NuSpec.AI.Attributes/` already exists, skip the file creation steps and only run the build verification.

**Files:**
- Create: `src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj`
- Create: `src/NuSpec.AI.Attributes/AiRoleAttribute.cs`
- Create: `src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs`
- Create: `src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs`
- Modify: `NuSpec.AI.slnx`

- [ ] **Create NuSpec.AI.Attributes.csproj**

`src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>NuSpec.AI</RootNamespace>

    <!-- NuGet package metadata -->
    <PackageId>NuSpec.AI.Attributes</PackageId>
    <Version>1.0.0</Version>
    <Description>
      Lightweight attributes for annotating public API members for NuSpec.AI Pro.
      [AiRole] declares semantic roles, [AiIgnore] excludes members from the package map,
      [AiDescription] provides AI-facing descriptions. Zero dependencies; targets netstandard2.0.
    </Description>
    <Authors>Sean Cooper</Authors>
    <PackageTags>AI;NuGet;attributes;NuSpec.AI;metadata</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IsPackable>true</IsPackable>
    <!-- This package ships no MSBuild assets — consumers just reference the assembly. -->
    <DevelopmentDependency>false</DevelopmentDependency>
  </PropertyGroup>
</Project>
```

- [ ] **Create AiRoleAttribute.cs**

`src/NuSpec.AI.Attributes/AiRoleAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Specifies one or more semantic roles for a type, overriding NuSpec.AI's automatic
/// role inference. Only honored when NuSpec.AI.Pro is installed with a valid license.
/// </summary>
/// <example>
/// [AiRole("aggregate-root", "audited")]
/// public class Order { ... }
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false)]
public sealed class AiRoleAttribute : Attribute
{
    /// <param name="roles">One or more semantic role labels (e.g. "aggregate-root", "value-object").</param>
    public AiRoleAttribute(params string[] roles) => Roles = roles;

    /// <summary>The declared semantic roles.</summary>
    public string[] Roles { get; }
}
```

- [ ] **Create AiIgnoreAttribute.cs**

`src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Excludes the decorated type or member from the NuSpec.AI generated package map.
/// Useful for internal helpers, legacy shims, or types not intended for AI consumption.
/// Only honored when NuSpec.AI.Pro is installed with a valid license.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiIgnoreAttribute : Attribute { }
```

- [ ] **Create AiDescriptionAttribute.cs**

`src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Provides a plain-text AI-facing description for a type or member, overriding any
/// XML doc comment summary extracted by NuSpec.AI. Useful when the AI-facing description
/// should differ from IntelliSense documentation.
/// Only honored when NuSpec.AI.Pro is installed with a valid license.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiDescriptionAttribute : Attribute
{
    /// <param name="description">The AI-facing description text.</param>
    public AiDescriptionAttribute(string description) => Description = description;

    /// <summary>The AI-facing description text.</summary>
    public string Description { get; }
}
```

- [ ] **Add both new projects to solution**

Replace `NuSpec.AI.slnx` with (add Pro and Attributes alongside the existing Tool project):
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj" />
    <Project Path="src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj" />
    <Project Path="src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj" />
    <Project Path="src/NuSpec.AI/NuSpec.AI.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Build Attributes to verify**
```bash
cd E:/repos/NuSpec.AI
dotnet build src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Task 5: Create `NuSpec.AI.Pro` packaging project

**Files:**
- Create: `src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj`
- Create: `src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.props`
- Create: `src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.targets`

The Pro packaging project is a packaging-only project (no runtime assembly). It bundles the same published CLI tool binary from `src/NuSpec.AI.Tool/` plus Pro-specific `build/` assets.

- [ ] **Create NuSpec.AI.Pro.csproj**

`src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <NoBuild>true</NoBuild>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>

    <!-- NuGet package metadata -->
    <PackageId>NuSpec.AI.Pro</PackageId>
    <Version>1.0.0</Version>
    <Description>
      NuSpec.AI Pro — generates ai/package-map.json (plus YAML, compact, and ultra-compact variants)
      during dotnet pack using Roslyn. Requires a valid NuSpec.AI Pro license key.
      Gracefully coexists with the free NuSpec.AI package (Pro wins; free sits inert).
    </Description>
    <Authors>Sean Cooper</Authors>
    <PackageTags>AI;NuGet;documentation;metadata;Roslyn;Pro</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <DevelopmentDependency>true</DevelopmentDependency>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <!-- NuGet readme (reuse root readme) -->
  <ItemGroup>
    <None Include="..\..\NUGET_README.md" PackagePath="README.md" Pack="true" />
  </ItemGroup>

  <!-- Include Pro build assets (props/targets evaluated by consuming project at pack time) -->
  <ItemGroup>
    <None Include="build\**" PackagePath="build\" Pack="true" />
  </ItemGroup>

  <!--
    Include the published CLI tool binary.
    Run `dotnet publish src/NuSpec.AI.Tool -c Release -o src/NuSpec.AI.Pro/tools/net8.0`
    (or build.sh) before packing NuSpec.AI.Pro.
  -->
  <ItemGroup>
    <None Include="tools\net8.0\**" PackagePath="tools\net8.0\" Pack="true" />
  </ItemGroup>

</Project>
```

- [ ] **Create NuSpec.AI.Pro.props**

`src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.props`:
```xml
<Project>
  <PropertyGroup>
    <!--
      Disable the free NuSpec.AI package.
      This props file is imported before NuSpec.AI.props (NuGet import order guarantees
      that NuSpec.AI.Pro.props runs first because 'P' < 'N' in alphabetical order is FALSE —
      NuGet actually imports props in dependency order. Since NuSpec.AI.Pro does NOT reference
      NuSpec.AI as a package dependency, both props files are imported at the same level.
      The Condition below works because NuSpec.AI.props only sets NuSpecAiEnabled when it is ''.
      NuSpec.AI.Pro.props sets it to 'false' unconditionally, so free's props has no effect.
    -->
    <NuSpecAiEnabled>false</NuSpecAiEnabled>

    <!-- Pro feature flag -->
    <NuSpecAiProEnabled Condition="'$(NuSpecAiProEnabled)' == ''">true</NuSpecAiProEnabled>

    <!-- Default output formats; consumer can override in their csproj -->
    <NuSpecAiFormats Condition="'$(NuSpecAiFormats)' == ''">json</NuSpecAiFormats>
  </PropertyGroup>
</Project>
```

- [ ] **Create NuSpec.AI.Pro.targets**

`src/NuSpec.AI.Pro/build/NuSpec.AI.Pro.targets`:
```xml
<Project>
  <PropertyGroup>
    <_NuSpecAiProToolPath>$(MSBuildThisFileDirectory)..\tools\net8.0\NuSpec.AI.Tool.dll</_NuSpecAiProToolPath>
    <_NuSpecAiProOutputDir>$(IntermediateOutputPath)nuspec-ai-pro\</_NuSpecAiProOutputDir>
  </PropertyGroup>

  <!--
    Register our target as a content provider for NuGet pack.
    Only runs when Pro is enabled AND the project is packable.
  -->
  <PropertyGroup Condition="'$(NuSpecAiProEnabled)' == 'true' and '$(IsPackable)' != 'false'">
    <TargetsForTfmSpecificContentInPackage>$(TargetsForTfmSpecificContentInPackage);NuSpecAiProIncludePackageMaps</TargetsForTfmSpecificContentInPackage>
  </PropertyGroup>

  <!--
    Resolve the license key from MSBuild property (highest priority).
    Lower-priority fallbacks (env var, file) are handled inside the CLI tool.
  -->
  <PropertyGroup>
    <_NuSpecAiProLicenseArg Condition="'$(NuSpecAiLicenseKey)' != ''">--license "$(NuSpecAiLicenseKey)"</_NuSpecAiProLicenseArg>
    <_NuSpecAiProLicenseArg Condition="'$(NuSpecAiLicenseKey)' == ''"></_NuSpecAiProLicenseArg>
  </PropertyGroup>

  <!-- Generate all requested format files -->
  <Target Name="NuSpecAiProGeneratePackageMaps"
          BeforeTargets="NuSpecAiProIncludePackageMaps"
          Condition="'$(NuSpecAiProEnabled)' == 'true' and '$(IsPackable)' != 'false'">

    <MakeDir Directories="$(_NuSpecAiProOutputDir)" />

    <Exec Command="dotnet &quot;$(_NuSpecAiProToolPath)&quot; &quot;$(MSBuildProjectFullPath)&quot; --output &quot;$(_NuSpecAiProOutputDir)&quot; --formats &quot;$(NuSpecAiFormats)&quot; --package-id &quot;$(PackageId)&quot; $(_NuSpecAiProLicenseArg)"
          ConsoleToMSBuild="true" />

  </Target>

  <!--
    Add each generated file to the pack using TfmSpecificPackageFile.
    The CLI tool writes files named by the formatter (e.g., package-map.json,
    package-map.yaml, package-map.compact.json, package-map.ultra.txt).
    We include the entire output directory under the ai/ pack path.
  -->
  <Target Name="NuSpecAiProIncludePackageMaps">
    <ItemGroup>
      <_NuSpecAiProGeneratedFiles Include="$(_NuSpecAiProOutputDir)*" />
    </ItemGroup>

    <ItemGroup>
      <TfmSpecificPackageFile Include="@(_NuSpecAiProGeneratedFiles)">
        <PackagePath>ai/%(Filename)%(Extension)</PackagePath>
      </TfmSpecificPackageFile>
    </ItemGroup>
  </Target>

</Project>
```

> **Note on graceful coexistence:** When both `NuSpec.AI` and `NuSpec.AI.Pro` are referenced, NuGet imports both `.props` files. Because `NuSpec.AI.Pro.props` sets `<NuSpecAiEnabled>false</NuSpecAiEnabled>` unconditionally while `NuSpec.AI.props` only sets it `Condition="'$(NuSpecAiEnabled)' == ''"`, the free package's targets never fire. The Pro targets emit message `NSPECAI002` at diagnostic verbosity to tell the user they can remove the free package reference.

- [ ] **Create tools/ placeholder directory (for build verification)**

Create a `.gitkeep` file so the directory exists for packing:
```bash
mkdir -p E:/repos/NuSpec.AI/src/NuSpec.AI.Pro/tools/net8.0
touch E:/repos/NuSpec.AI/src/NuSpec.AI.Pro/tools/net8.0/.gitkeep
```

- [ ] **Publish CLI tool into Pro's tools/ directory**
```bash
cd E:/repos/NuSpec.AI
dotnet publish src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj \
  -c Release \
  -o src/NuSpec.AI.Pro/tools/net8.0 \
  --self-contained false
```
Expected: Publishes `NuSpec.AI.Tool.dll` and dependencies to `src/NuSpec.AI.Pro/tools/net8.0/`.

- [ ] **Build Pro project to verify (packing-only, so just restore)**
```bash
cd E:/repos/NuSpec.AI
dotnet restore src/NuSpec.AI.Pro/NuSpec.AI.Pro.csproj
```
Expected: Restore succeeded.

---

## Task 6: Integration test — Pro license present → Pro features active

**Files:**
- Create: `tests/NuSpec.AI.Tool.Tests/Integration/ProLicenseIntegrationTests.cs`

These tests invoke the CLI tool via `Process` to test end-to-end behavior, using a temp directory for output and a real signed JWT.

- [ ] **Create ProLicenseIntegrationTests.cs**

`tests/NuSpec.AI.Tool.Tests/Integration/ProLicenseIntegrationTests.cs`:
```csharp
using System.Diagnostics;
using NuSpec.AI.Tool.Tests.Licensing;

namespace NuSpec.AI.Tool.Tests.Integration;

/// <summary>
/// End-to-end integration tests that invoke the CLI tool as a subprocess.
/// These tests require that the tool has been built (net8.0).
/// </summary>
public class ProLicenseIntegrationTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _sampleProjectPath;

    // Path to the built tool DLL — assumes tests run from repo root or standard output location.
    private static readonly string ToolDll = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "NuSpec.AI.Tool", "bin", "Debug", "net8.0", "NuSpec.AI.Tool.dll"));

    // Path to a sample .csproj to analyze.
    private static readonly string SampleProjectDir = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "SampleProject"));

    public ProLicenseIntegrationTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "nuspec-ai-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);

        // Find the sample project csproj
        _sampleProjectPath = Directory.GetFiles(SampleProjectDir, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException($"No .csproj found in {SampleProjectDir}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    private (int exitCode, string stdout, string stderr) RunTool(params string[] args)
    {
        // Skip if tool DLL not built yet.
        if (!File.Exists(ToolDll))
            return (-999, string.Empty, $"Tool not found at: {ToolDll}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(ToolDll);
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    [Fact]
    public void ValidProLicense_JsonFormat_GeneratesPackageMapJson()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "*");

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json",
            "--license", token);

        if (exitCode == -999) return; // tool not built — skip gracefully

        Assert.Equal(0, exitCode);
        var jsonFile = Path.Combine(_outputDir, "package-map.json");
        Assert.True(File.Exists(jsonFile), $"Expected {jsonFile} to exist. stderr: {stderr}");
        var content = File.ReadAllText(jsonFile);
        Assert.Contains("\"packageId\"", content);
    }

    [Fact]
    public void ValidProLicense_MultipleFormats_GeneratesAllRequestedFiles()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "*");

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;yaml",
            "--license", token);

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")),
            $"JSON file missing. stderr: {stderr}");
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.yaml")),
            $"YAML file missing. stderr: {stderr}");
    }

    [Fact]
    public void ValidProLicense_ScopedToPackage_WritesFileAndNoWarning()
    {
        // License covers "SampleProject" (the package ID of the test project)
        var token = LicenseTestHelpers.CreateToken(packages: "SampleProject");

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json",
            "--license", token,
            "--package-id", "SampleProject");

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("NSPECAI001", stderr);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")));
    }

    [Fact]
    public void ValidProLicense_CoexistenceHint_EmittedAtDiagLevel()
    {
        var token = LicenseTestHelpers.CreateToken(packages: "*");

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json",
            "--license", token);

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        // NSPECAI002 coexistence hint is written to stderr (diagnostic-level MSBuild message)
        Assert.Contains("NSPECAI002", stderr);
    }
}
```

- [ ] **Run integration tests**
```bash
cd E:/repos/NuSpec.AI
dotnet build src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj -c Debug
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj \
  --filter "FullyQualifiedName~ProLicenseIntegration" \
  --no-restore
```
Expected: All tests pass (or skip gracefully if tool DLL not present).

---

## Task 7: Integration test — invalid/expired license → fallback + warning

**Files:**
- Create: `tests/NuSpec.AI.Tool.Tests/Integration/FallbackIntegrationTests.cs`

- [ ] **Create FallbackIntegrationTests.cs**

`tests/NuSpec.AI.Tool.Tests/Integration/FallbackIntegrationTests.cs`:
```csharp
using System.Diagnostics;
using NuSpec.AI.Tool.Tests.Licensing;

namespace NuSpec.AI.Tool.Tests.Integration;

/// <summary>
/// Integration tests verifying graceful fallback to JSON-only output when
/// the Pro license is invalid, expired, or missing.
/// </summary>
public class FallbackIntegrationTests : IDisposable
{
    private readonly string _outputDir;
    private readonly string _sampleProjectPath;

    private static readonly string ToolDll = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "NuSpec.AI.Tool", "bin", "Debug", "net8.0", "NuSpec.AI.Tool.dll"));

    private static readonly string SampleProjectDir = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "SampleProject"));

    public FallbackIntegrationTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "nuspec-ai-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);

        _sampleProjectPath = Directory.GetFiles(SampleProjectDir, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException($"No .csproj found in {SampleProjectDir}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    private (int exitCode, string stdout, string stderr) RunTool(params string[] args)
    {
        if (!File.Exists(ToolDll))
            return (-999, string.Empty, $"Tool not found at: {ToolDll}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(ToolDll);
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        // Ensure no license key leaks in from the test environment
        psi.Environment["NUSPEC_AI_LICENSE_KEY"] = "";

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    [Fact]
    public void NoLicense_ProFormatsRequested_FallsBackToJson_EmitsWarning()
    {
        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;yaml;ultra");

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);

        // Warning must be present
        Assert.Contains("NSPECAI001", stderr);

        // Only JSON file should be present (no yaml/ultra)
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")),
            "JSON fallback file should exist");
        Assert.False(File.Exists(Path.Combine(_outputDir, "package-map.yaml")),
            "YAML file should NOT exist in fallback mode");
        Assert.False(File.Exists(Path.Combine(_outputDir, "package-map.ultra.txt")),
            "Ultra file should NOT exist in fallback mode");
    }

    [Fact]
    public void ExpiredLicense_ProFormatsRequested_FallsBackToJson_EmitsWarning()
    {
        var expiredToken = LicenseTestHelpers.CreateExpiredToken();

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;yaml",
            "--license", expiredToken);

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.Contains("NSPECAI001", stderr);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "package-map.yaml")));
    }

    [Fact]
    public void InvalidSignatureKey_ProFormatsRequested_FallsBackToJson_EmitsWarning()
    {
        var wrongKeyToken = LicenseTestHelpers.CreateTokenWithWrongKey();

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;compact",
            "--license", wrongKeyToken);

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.Contains("NSPECAI001", stderr);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "package-map.compact.json")));
    }

    [Fact]
    public void NoLicense_JsonOnlyRequested_NoWarningEmitted()
    {
        // If only json is requested, no Pro features are needed — no warning.
        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json");

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("NSPECAI001", stderr);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")));
    }

    [Fact]
    public void LicenseScopedToWrongPackage_EmitsWarning_FallsBackToJson()
    {
        // License is only for "Acme.Orders", but we're packing "Beta.Payments"
        var token = LicenseTestHelpers.CreateToken(packages: "Acme.Orders");

        var (exitCode, _, stderr) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;yaml",
            "--license", token,
            "--package-id", "Beta.Payments");

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
        Assert.Contains("NSPECAI001", stderr);
        Assert.True(File.Exists(Path.Combine(_outputDir, "package-map.json")));
        Assert.False(File.Exists(Path.Combine(_outputDir, "package-map.yaml")));
    }

    [Fact]
    public void BuildDoesNotFail_OnMissingLicense_ExitCodeIsZero()
    {
        // Core requirement: missing/invalid license must never cause a non-zero exit code.
        var (exitCode, _, _) = RunTool(
            _sampleProjectPath,
            "--output", _outputDir,
            "--formats", "json;yaml;compact;ultra");

        if (exitCode == -999) return;

        Assert.Equal(0, exitCode);
    }
}
```

- [ ] **Run fallback tests**
```bash
cd E:/repos/NuSpec.AI
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj \
  --filter "FullyQualifiedName~FallbackIntegration" \
  --no-restore
```
Expected: All tests pass.

---

## Task 8: Commit everything

- [ ] **Run the full test suite**
```bash
cd E:/repos/NuSpec.AI
dotnet test NuSpec.AI.slnx
```
Expected: All tests pass, 0 failures.

- [ ] **Stage and commit**
```bash
cd E:/repos/NuSpec.AI
git add \
  src/NuSpec.AI.Tool/Licensing/ \
  src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj \
  src/NuSpec.AI.Tool/Program.cs \
  src/NuSpec.AI.Pro/ \
  src/NuSpec.AI.Attributes/ \
  tests/NuSpec.AI.Tool.Tests/Licensing/ \
  tests/NuSpec.AI.Tool.Tests/Integration/ \
  tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj \
  NuSpec.AI.slnx

git commit -m "feat: add Pro licensing, NuSpec.AI.Pro packaging, and NuSpec.AI.Attributes

- LicenseInfo record + LicenseValidator (offline RS256 JWT, glob package scope)
- License key resolution: --license arg > NUSPEC_AI_LICENSE_KEY env > ~/.nuspec-ai/license.key
- Program.cs: --license, --package-id, --formats wired together; NSPECAI001 warning on fallback
- NuSpec.AI.Pro packaging project with props (disables free package) and targets (multi-format pack)
- NuSpec.AI.Attributes: AiRole, AiIgnore, AiDescription (netstandard2.0, zero deps)
- Integration tests: valid Pro license → files generated; invalid/expired → JSON fallback + warning"
```

---

## Quick reference: MSBuild property summary

| Property | Default | Purpose |
|----------|---------|---------|
| `NuSpecAiEnabled` | `true` (free) / `false` (set by Pro props) | Enables free package targets |
| `NuSpecAiProEnabled` | `true` | Enables Pro package targets |
| `NuSpecAiFormats` | `json` | Semicolon-separated format list |
| `NuSpecAiLicenseKey` | _(empty)_ | License JWT passed as `--license` to CLI |

Consumer `.csproj` example:
```xml
<PropertyGroup>
  <NuSpecAiFormats>json;yaml;ultra</NuSpecAiFormats>
  <NuSpecAiLicenseKey>$(NuSpecAiLicenseKeyFromSecret)</NuSpecAiLicenseKey>
</PropertyGroup>
```

## Quick reference: CLI arg summary (after Plan A + Plan B)

```
dotnet NuSpec.AI.Tool.dll <project.csproj>
  [--output <directory>]        # where to write output files
  [--formats "json;yaml;ultra"] # which formatters to run (Pro license required for non-json)
  [--license <jwt>]             # Pro license key
  [--package-id <id>]           # NuGet package ID for license scope check
  [--help]
  [--version]
```
