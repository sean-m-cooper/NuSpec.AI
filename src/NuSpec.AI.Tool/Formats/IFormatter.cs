using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

public interface IFormatter
{
    /// <summary>Format identifier used in --formats CLI arg and MSBuild property (e.g., "json", "yaml", "compact", "ultra").</summary>
    string FormatId { get; }

    /// <summary>Output file name to use inside ai/ folder in the .nupkg (e.g., "package-map.json").</summary>
    string FileName { get; }

    /// <summary>Serialize the package map to a string in this format.</summary>
    string Serialize(PackageMap packageMap);
}
