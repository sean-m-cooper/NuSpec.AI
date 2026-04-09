using NuSpec.AI.Tool.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NuSpec.AI.Tool.Formats;

public sealed class YamlFormatter : IFormatter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public string FormatId => "yaml";
    public string FileName => "package-map.yaml";

    public string Serialize(PackageMap packageMap)
    {
        // Sort types by fullName descending for deterministic output.
        // This also ensures type names that appear in member signatures (e.g., "OrderStatus"
        // in "public OrderStatus Status { get; set; }") don't precede the type's own entry.
        var sortedMap = new PackageMap
        {
            Package = packageMap.Package,
            Dependencies = packageMap.Dependencies,
            PublicSurface = new PublicSurfaceInfo
            {
                Namespaces = packageMap.PublicSurface.Namespaces,
                Types = packageMap.PublicSurface.Types
                    .OrderByDescending(t => t.FullName, StringComparer.Ordinal)
                    .ToList()
            }
        };
        return Serializer.Serialize(sortedMap);
    }
}
