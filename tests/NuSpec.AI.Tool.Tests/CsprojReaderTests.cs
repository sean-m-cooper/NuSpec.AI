using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Tests;

public class CsprojReaderTests : IDisposable
{
    private readonly string _tempDir;

    public CsprojReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nuspecai-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteCsproj(string content)
    {
        var path = Path.Combine(_tempDir, "Test.csproj");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadsPackageId()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>My.Package</PackageId>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal("My.Package", info.Id);
    }

    [Fact]
    public void FallsBackToAssemblyName()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>MyAssembly</AssemblyName>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal("MyAssembly", info.Id);
    }

    [Fact]
    public void FallsBackToFileName()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal("Test", info.Id);
    }

    [Fact]
    public void ReadsVersion()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Version>2.3.4</Version>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal("2.3.4", info.Version);
    }

    [Fact]
    public void PrefersPackageVersionOverVersion()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageVersion>3.0.0</PackageVersion>
                <Version>2.0.0</Version>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal("3.0.0", info.Version);
    }

    [Fact]
    public void ReadsTags()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageTags>AI;NuGet;tools</PackageTags>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal(new[] { "AI", "NuGet", "tools" }, info.Tags);
    }

    [Fact]
    public void ReadsMultipleTargetFrameworks()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        var info = CsprojReader.ReadPackageInfo(path);
        Assert.Equal(new[] { "net8.0", "netstandard2.0" }, info.TargetFrameworks);
    }

    [Fact]
    public void ReadDeclaredPackageReferences_ReturnsIdAndPrivateAssetsFlag()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Visible.Package" Version="1.0.0" />
                <PackageReference Include="Hidden.Package" Version="1.0.0" PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadDeclaredPackageReferences(path);

        Assert.Equal(2, refs.Count);
        var visible = refs.Single(r => r.Id == "Visible.Package");
        Assert.False(visible.IsPrivateAssetsAll);
        var hidden = refs.Single(r => r.Id == "Hidden.Package");
        Assert.True(hidden.IsPrivateAssetsAll);
    }

    [Fact]
    public void ReadDeclaredPackageReferences_DetectsChildElementPrivateAssets()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="A">
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadDeclaredPackageReferences(path);

        Assert.Single(refs);
        Assert.True(refs[0].IsPrivateAssetsAll);
    }

    [Fact]
    public void ReadFrameworkReferences_ReturnsNames()
    {
        var path = WriteCsproj("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadFrameworkReferences(path);

        Assert.Single(refs);
        Assert.Equal("Microsoft.AspNetCore.App", refs[0]);
    }
}
