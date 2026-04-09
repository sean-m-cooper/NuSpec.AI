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
        return Serializer.Serialize(packageMap);
    }
}
