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
