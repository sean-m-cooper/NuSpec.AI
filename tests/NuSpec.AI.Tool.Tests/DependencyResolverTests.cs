using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Tests;

public class DependencyResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectDir;
    private readonly string _cacheRoot;

    public DependencyResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nuspecai-resolver-" + Guid.NewGuid().ToString("N")[..8]);
        _projectDir = Path.Combine(_tempDir, "proj");
        _cacheRoot = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(Path.Combine(_projectDir, "obj"));
        Directory.CreateDirectory(_cacheRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteCsproj(string content)
    {
        var path = Path.Combine(_projectDir, "Test.csproj");
        File.WriteAllText(path, content);
        return path;
    }

    private void WriteAssets(string targetsBlock, string? extraFolders = null)
    {
        var folders = extraFolders ?? "";
        var json = $$"""
            {
              "version": 3,
              "packageFolders": {
                "{{_cacheRoot.Replace("\\", "\\\\")}}\\": {}{{folders}}
              },
              "targets": {
                "net8.0": {
                  {{targetsBlock}}
                }
              }
            }
            """;
        File.WriteAllText(Path.Combine(_projectDir, "obj", "project.assets.json"), json);
    }

    private void WritePackageMap(string idLower, string version)
    {
        var dir = Path.Combine(_cacheRoot, idLower, version, "ai");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "package-map.json"), "{}");
    }

    [Fact]
    public void ResolvesVersionFromAssets()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.*" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"Newtonsoft.Json/13.0.3\": { \"type\": \"package\" }");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.Single(deps.PackageReferences);
        Assert.Equal("Newtonsoft.Json", deps.PackageReferences[0].Id);
        Assert.Equal("13.0.3", deps.PackageReferences[0].Version);
        Assert.False(deps.PackageReferences[0].HasNuSpecAiMap);
    }

    [Fact]
    public void HasNuSpecAiMap_TrueWhenFileExists()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Acme.OrdersCore" Version="1.2.0" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"Acme.OrdersCore/1.2.0\": { \"type\": \"package\" }");
        WritePackageMap("acme.orderscore", "1.2.0");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.True(deps.PackageReferences[0].HasNuSpecAiMap);
    }

    [Fact]
    public void HasNuSpecAiMap_FalseWhenFileMissing()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Acme.OrdersCore" Version="1.2.0" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"Acme.OrdersCore/1.2.0\": { \"type\": \"package\" }");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.False(deps.PackageReferences[0].HasNuSpecAiMap);
    }

    [Fact]
    public void LowercasesIdSegmentForCachePath()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MixedCase.Package" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"MixedCase.Package/2.0.0\": { \"type\": \"package\" }");
        WritePackageMap("mixedcase.package", "2.0.0");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.True(deps.PackageReferences[0].HasNuSpecAiMap);
        Assert.Equal("MixedCase.Package", deps.PackageReferences[0].Id); // declared casing preserved
    }

    [Fact]
    public void ExcludesPrivateAssetsAll()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Visible" Version="1.0.0" />
                <PackageReference Include="Hidden" Version="1.0.0" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"Visible/1.0.0\": { \"type\": \"package\" }, \"Hidden/1.0.0\": { \"type\": \"package\" }");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.Single(deps.PackageReferences);
        Assert.Equal("Visible", deps.PackageReferences[0].Id);
    }

    [Fact]
    public void MissingAssetsFile_ReturnsRefsWithNullVersion()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Foo" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var deps = DependencyResolver.Resolve(csproj);

        Assert.Single(deps.PackageReferences);
        Assert.Equal("Foo", deps.PackageReferences[0].Id);
        Assert.Null(deps.PackageReferences[0].Version);
        Assert.False(deps.PackageReferences[0].HasNuSpecAiMap);
    }

    [Fact]
    public void IncludesFrameworkReferences()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.Single(deps.FrameworkReferences);
        Assert.Equal("Microsoft.AspNetCore.App", deps.FrameworkReferences[0]);
    }

    [Fact]
    public void DeduplicatesAndSortsPackageRefs()
    {
        var csproj = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Zeta" Version="1.0.0" />
                <PackageReference Include="Alpha" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        WriteAssets("\"Zeta/1.0.0\": { \"type\": \"package\" }, \"Alpha/1.0.0\": { \"type\": \"package\" }");

        var deps = DependencyResolver.Resolve(csproj);

        Assert.Equal(2, deps.PackageReferences.Count);
        Assert.Equal("Alpha", deps.PackageReferences[0].Id);
        Assert.Equal("Zeta", deps.PackageReferences[1].Id);
    }
}
