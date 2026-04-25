using System.Text;
using System.Text.RegularExpressions;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

public sealed class UltraCompactFormatter : IFormatter
{
    public string FormatId => "ultra";
    public string FileName => "package-map.ultra";

    public string Serialize(PackageMap packageMap)
    {
        var sb = new StringBuilder();
        var pkg = packageMap.Package;
        var deps = packageMap.Dependencies;
        var surface = packageMap.PublicSurface;

        // Header
        sb.AppendLine($"#NuSpec.AI/v1 {pkg.Id} {pkg.Version}");
        if (!string.IsNullOrWhiteSpace(pkg.Description))
            sb.AppendLine($"#desc {pkg.Description}");
        if (pkg.TargetFrameworks.Count > 0)
            sb.AppendLine($"#tfm {string.Join(";", pkg.TargetFrameworks)}");
        if (deps.PackageReferences.Count > 0)
            sb.AppendLine($"#dep {string.Join(";", deps.PackageReferences.Select(FormatDep))}");
        if (deps.FrameworkReferences.Count > 0)
            sb.AppendLine($"#fref {string.Join(";", deps.FrameworkReferences)}");

        // Types
        foreach (var type in surface.Types)
        {
            var prefix = type.Kind switch
            {
                "class"         => "@c",
                "interface"     => "@i",
                "enum"          => "@e",
                "struct"        => "@s",
                "record"        => "@r",
                "record-struct" => "@rs",
                _               => "@c"
            };

            var roles = type.Roles.Count > 0
                ? $" [{string.Join(",", type.Roles)}]"
                : "";

            var doc = !string.IsNullOrWhiteSpace(type.Documentation)
                ? $" \"{type.Documentation}\""
                : "";

            sb.AppendLine($"{prefix} {type.Name}{roles}{doc}");

            if (type.Kind == "enum")
            {
                // All enum values on one .ev line: Pending=0,Confirmed=1
                var enumValues = string.Join(",",
                    type.Members.Select(m => FormatEnumValue(m.Signature)));
                sb.AppendLine($" .ev {enumValues}");
            }
            else
            {
                foreach (var member in type.Members)
                {
                    var memberDoc = !string.IsNullOrWhiteSpace(member.Documentation)
                        ? $" \"{member.Documentation}\""
                        : "";

                    var line = member.Kind switch
                    {
                        "property"    => $" .p {FormatPropertySignature(member.Signature)}{memberDoc}",
                        "method"      => $" .m {FormatMethodSignature(member.Signature)}{memberDoc}",
                        "constructor" => $" .ctor {FormatCtorSignature(member.Signature)}{memberDoc}",
                        "field"       => $" .f {FormatFieldSignature(member.Signature)}{memberDoc}",
                        _             => null
                    };

                    if (line is not null)
                        sb.AppendLine(line);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    // "Acme.OrdersCore" v "1.2.0" map=true → "Acme.OrdersCore|1.2.0|1"
    private static string FormatDep(PackageReferenceInfo p)
    {
        var version = p.Version ?? "";
        var flag = p.HasNuSpecAiMap ? "1" : "0";
        return $"{p.Id}|{version}|{flag}";
    }

    // "Pending = 0" → "Pending=0"
    private static string FormatEnumValue(string sig) =>
        sig.Replace(" = ", "=").Trim();

    // "public int Id { get; set; }" → "Id:int"
    private static string FormatPropertySignature(string sig)
    {
        // Remove accessor block e.g. "{ get; set; }"
        var withoutAccessors = Regex.Replace(sig, @"\s*\{[^}]*\}", "").Trim();
        // Split on space: ["public", "int", "Id"] or ["public", "Task<Order?>", "GetById", ...]
        var parts = withoutAccessors.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var name = parts[^1];
            var type = parts[^2];
            return $"{name}:{type}";
        }
        return sig;
    }

    // "Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)" → "GetByIdAsync:Task<Order?>(int id, CancellationToken ct = default)"
    // Also handles: "public Task<Order?> GetByIdAsync(...)" with modifiers
    private static string FormatMethodSignature(string sig)
    {
        // Strip access modifiers
        var noMods = Regex.Replace(sig,
            @"^(public|private|protected|internal|static|virtual|override|abstract|sealed|async|\s)+",
            "").Trim();
        var parenIdx = noMods.IndexOf('(');
        if (parenIdx < 0) return sig;
        var beforeParen = noMods[..parenIdx].Trim();
        var paramsAndRest = noMods[parenIdx..];
        var lastSpace = beforeParen.LastIndexOf(' ');
        if (lastSpace < 0) return sig;
        var returnType = beforeParen[..lastSpace].Trim();
        var name = beforeParen[(lastSpace + 1)..];
        return $"{name}:{returnType}{paramsAndRest}";
    }

    // "public MyClass(string foo)" → "(string foo)"
    private static string FormatCtorSignature(string sig)
    {
        var parenIdx = sig.IndexOf('(');
        return parenIdx >= 0 ? sig[parenIdx..] : sig;
    }

    // "public static readonly int MaxValue" → "MaxValue:int"
    private static string FormatFieldSignature(string sig)
    {
        var parts = sig.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[^1]}:{parts[^2]}";
        return sig;
    }
}
