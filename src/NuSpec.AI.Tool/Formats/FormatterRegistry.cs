namespace NuSpec.AI.Tool.Formats;

public static class FormatterRegistry
{
    private static readonly Dictionary<string, IFormatter> Formatters = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["json"]    = new JsonFormatter(),
        ["yaml"]    = new YamlFormatter(),
        ["compact"] = new CompactJsonFormatter(),
        ["ultra"]   = new UltraCompactFormatter()
    };

    /// <summary>
    /// Parses a semicolon-separated format string (e.g., "json;yaml;ultra").
    /// "all" expands to all four formats in a consistent order.
    /// Empty/null returns json only.
    /// </summary>
    public static IReadOnlyList<IFormatter> Resolve(string? formatsArg)
    {
        if (string.IsNullOrWhiteSpace(formatsArg))
            return [Formatters["json"]];

        if (formatsArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            return [Formatters["json"], Formatters["yaml"], Formatters["compact"], Formatters["ultra"]];

        var result = new List<IFormatter>();
        foreach (var id in formatsArg.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = id.Trim();
            if (Formatters.TryGetValue(trimmed, out var formatter))
                result.Add(formatter);
            else
                throw new ArgumentException($"Unknown format: '{trimmed}'. Valid formats: json, yaml, compact, ultra, all");
        }

        return result.Count > 0 ? result : [Formatters["json"]];
    }
}
