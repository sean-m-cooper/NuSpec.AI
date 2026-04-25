using System.Xml.Linq;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.ProjectMetadata;

public static class CsprojReader
{
    public static PackageInfo ReadPackageInfo(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var id = GetProperty(doc, ns, "PackageId")
              ?? GetProperty(doc, ns, "AssemblyName")
              ?? Path.GetFileNameWithoutExtension(csprojPath);

        var version = GetProperty(doc, ns, "PackageVersion")
                   ?? GetProperty(doc, ns, "Version")
                   ?? "1.0.0";

        var description = GetProperty(doc, ns, "Description");

        var tagsRaw = GetProperty(doc, ns, "PackageTags") ?? "";
        var tags = tagsRaw
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        var tfm = GetProperty(doc, ns, "TargetFramework");
        var tfms = GetProperty(doc, ns, "TargetFrameworks");
        var targetFrameworks = new List<string>();
        if (!string.IsNullOrWhiteSpace(tfms))
        {
            targetFrameworks.AddRange(
                tfms.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()));
        }
        else if (!string.IsNullOrWhiteSpace(tfm))
        {
            targetFrameworks.Add(tfm.Trim());
        }

        return new PackageInfo
        {
            Id = id,
            Version = version,
            Description = description,
            Tags = tags,
            TargetFrameworks = targetFrameworks
        };
    }

    public static DependencyInfo ReadDependencies(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var packageRefs = doc.Descendants(ns + "PackageReference")
            .Where(el =>
            {
                var privateAssets = el.Attribute("PrivateAssets")?.Value
                    ?? el.Element(ns + "PrivateAssets")?.Value;
                return !string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase);
            })
            .Select(el => el.Attribute("Include")?.Value ?? "")
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var frameworkRefs = doc.Descendants(ns + "FrameworkReference")
            .Select(el => el.Attribute("Include")?.Value ?? "")
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var wrapped = packageRefs
            .Select(id => new PackageReferenceInfo
            {
                Id = id,
                Version = null,
                HasNuSpecAiMap = false
            })
            .ToList();

        return new DependencyInfo
        {
            PackageReferences = wrapped,
            FrameworkReferences = frameworkRefs
        };
    }

    private static string? GetProperty(XDocument doc, XNamespace ns, string propertyName)
    {
        var value = doc.Descendants(ns + propertyName).FirstOrDefault()?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
