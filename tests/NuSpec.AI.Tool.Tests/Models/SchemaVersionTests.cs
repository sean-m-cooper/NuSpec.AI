using NuSpec.AI.Tool.Models;
using NuSpec.AI.Tool.Analysis;

namespace NuSpec.AI.Tool.Tests.Models;

public class SchemaVersionTests
{
    [Fact]
    public void DefaultPackageMapHasSchemaVersion2()
    {
        var map = new PackageMap
        {
            Package = new PackageInfo
            {
                Id = "X", Version = "1.0.0",
                Description = null,
                Tags = Array.Empty<string>(),
                TargetFrameworks = Array.Empty<string>()
            },
            Dependencies = new DependencyInfo
            {
                PackageReferences = Array.Empty<PackageReferenceInfo>(),
                FrameworkReferences = Array.Empty<string>()
            },
            PublicSurface = new PublicSurfaceInfo
            {
                Namespaces = Array.Empty<string>(),
                Types = Array.Empty<TypeInfo>()
            }
        };

        Assert.Equal(2, map.SchemaVersion);
    }
}
