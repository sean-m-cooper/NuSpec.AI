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

    public static string CreateExpiredToken(string packages = "*")
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, "test-customer@example.com"),
            new System.Security.Claims.Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now.AddHours(-2)).ToUnixTimeSeconds().ToString(),
                System.Security.Claims.ClaimValueTypes.Integer64),
            new System.Security.Claims.Claim("scope", "pro"),
            new System.Security.Claims.Claim("packages", packages),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now.AddHours(-2),
            expires: now.AddHours(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

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
