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

            var iatClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat);
            var iat = iatClaim is not null
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(iatClaim.Value))
                : DateTimeOffset.UtcNow;

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
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            // Malformed JWT structure or claim parsing failures
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
