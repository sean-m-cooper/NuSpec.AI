using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Tests.Formats;

public class UltraCompactFormatterDepTests
{
    private static PackageMap MakeMap(params PackageReferenceInfo[] deps) => new()
    {
        Package = new PackageInfo
        {
            Id = "X", Version = "1.0.0",
            Description = null, Tags = Array.Empty<string>(),
            TargetFrameworks = Array.Empty<string>()
        },
        Dependencies = new DependencyInfo
        {
            PackageReferences = deps.ToList(),
            FrameworkReferences = Array.Empty<string>()
        },
        PublicSurface = new PublicSurfaceInfo
        {
            Namespaces = Array.Empty<string>(),
            Types = Array.Empty<TypeInfo>()
        }
    };

    [Fact]
    public void EmitsIdVersionFlag_PerDep()
    {
        var map = MakeMap(
            new PackageReferenceInfo { Id = "Newtonsoft.Json", Version = "13.0.3", HasNuSpecAiMap = false },
            new PackageReferenceInfo { Id = "Acme.OrdersCore", Version = "1.2.0", HasNuSpecAiMap = true });

        var output = new UltraCompactFormatter().Serialize(map);

        Assert.Contains("#dep Newtonsoft.Json|13.0.3|0;Acme.OrdersCore|1.2.0|1", output);
    }

    [Fact]
    public void NullVersion_EmitsEmptyVersionField()
    {
        var map = MakeMap(
            new PackageReferenceInfo { Id = "Foo", Version = null, HasNuSpecAiMap = false });

        var output = new UltraCompactFormatter().Serialize(map);

        Assert.Contains("#dep Foo||0", output);
    }

    [Fact]
    public void NoDeps_OmitsDepLine()
    {
        var map = MakeMap();

        var output = new UltraCompactFormatter().Serialize(map);

        Assert.DoesNotContain("#dep", output);
    }
}
