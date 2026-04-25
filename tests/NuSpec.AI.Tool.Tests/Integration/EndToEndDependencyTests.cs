using NuSpec.AI.Tool.Analysis;

namespace NuSpec.AI.Tool.Tests.Integration;

public class EndToEndDependencyTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheRoot;

    public EndToEndDependencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nuspecai-e2e-" + Guid.NewGuid().ToString("N")[..8]);
        _cacheRoot = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(Path.Combine(_tempDir, "obj"));
        Directory.CreateDirectory(_cacheRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Analyze_ProducesV2SchemaWithEnrichedDeps()
    {
        var csproj = Path.Combine(_tempDir, "Sample.csproj");
        File.WriteAllText(csproj, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <PackageId>Sample</PackageId>
                <Version>1.0.0</Version>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Acme.OrdersCore" Version="1.2.0" />
                <PackageReference Include="Some.Other" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_tempDir, "Sample.cs"), """
            namespace Sample;
            public class Thing { public int Id { get; set; } }
            """);

        var rootJson = System.Text.Json.JsonSerializer.Serialize(_cacheRoot);
        File.WriteAllText(Path.Combine(_tempDir, "obj", "project.assets.json"), $$"""
            {
              "version": 3,
              "packageFolders": { {{rootJson}}: {} },
              "targets": {
                "net8.0": {
                  "Acme.OrdersCore/1.2.0": { "type": "package" },
                  "Some.Other/2.0.0": { "type": "package" }
                }
              }
            }
            """);

        // Stage Acme.OrdersCore as having a NuSpec.AI map; Some.Other has none.
        var mapDir = Path.Combine(_cacheRoot, "acme.orderscore", "1.2.0", "ai");
        Directory.CreateDirectory(mapDir);
        File.WriteAllText(Path.Combine(mapDir, "package-map.json"), "{}");

        var map = ProjectAnalyzer.Analyze(csproj);

        Assert.Equal(2, map.SchemaVersion);
        Assert.Equal(2, map.Dependencies.PackageReferences.Count);

        var acme = map.Dependencies.PackageReferences.Single(p => p.Id == "Acme.OrdersCore");
        Assert.Equal("1.2.0", acme.Version);
        Assert.True(acme.HasNuSpecAiMap);

        var other = map.Dependencies.PackageReferences.Single(p => p.Id == "Some.Other");
        Assert.Equal("2.0.0", other.Version);
        Assert.False(other.HasNuSpecAiMap);
    }
}
