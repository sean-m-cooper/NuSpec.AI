using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Tests;

public class AssetsReaderTests : IDisposable
{
    private readonly string _tempDir;

    public AssetsReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nuspecai-assets-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_tempDir, "obj"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteAssets(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "obj", "project.assets.json"), content);
    }

    [Fact]
    public void MissingAssetsFile_ReturnsEmpty()
    {
        var info = AssetsReader.Read(_tempDir);

        Assert.Empty(info.PackageFolders);
        Assert.Empty(info.ResolvedVersions);
    }

    [Fact]
    public void ParsesPackageFoldersInDeclarationOrder()
    {
        WriteAssets("""
            {
              "version": 3,
              "packageFolders": {
                "C:\\Users\\test\\.nuget\\packages\\": {},
                "C:\\fallback\\": {}
              },
              "targets": {}
            }
            """);

        var info = AssetsReader.Read(_tempDir);

        Assert.Equal(2, info.PackageFolders.Count);
        Assert.Equal(@"C:\Users\test\.nuget\packages\", info.PackageFolders[0]);
        Assert.Equal(@"C:\fallback\", info.PackageFolders[1]);
    }

    [Fact]
    public void ParsesResolvedVersionsFromTargets()
    {
        WriteAssets("""
            {
              "version": 3,
              "packageFolders": { "C:\\nuget\\": {} },
              "targets": {
                "net8.0": {
                  "Newtonsoft.Json/13.0.3": { "type": "package" },
                  "Acme.OrdersCore/1.2.0": { "type": "package" }
                }
              }
            }
            """);

        var info = AssetsReader.Read(_tempDir);

        Assert.Equal("13.0.3", info.ResolvedVersions["Newtonsoft.Json"]);
        Assert.Equal("1.2.0", info.ResolvedVersions["Acme.OrdersCore"]);
    }

    [Fact]
    public void MultipleTfms_FirstWinsForVersionLookup()
    {
        WriteAssets("""
            {
              "version": 3,
              "packageFolders": { "C:\\nuget\\": {} },
              "targets": {
                "net6.0": { "Foo/1.0.0": { "type": "package" } },
                "net8.0": { "Foo/1.0.0": { "type": "package" } }
              }
            }
            """);

        var info = AssetsReader.Read(_tempDir);

        Assert.Equal("1.0.0", info.ResolvedVersions["Foo"]);
    }

    [Fact]
    public void IgnoresProjectTypeEntries()
    {
        WriteAssets("""
            {
              "version": 3,
              "packageFolders": { "C:\\nuget\\": {} },
              "targets": {
                "net8.0": {
                  "MyLib/1.0.0": { "type": "project" },
                  "Newtonsoft.Json/13.0.3": { "type": "package" }
                }
              }
            }
            """);

        var info = AssetsReader.Read(_tempDir);

        Assert.False(info.ResolvedVersions.ContainsKey("MyLib"));
        Assert.True(info.ResolvedVersions.ContainsKey("Newtonsoft.Json"));
    }

    [Fact]
    public void MalformedJson_ReturnsEmpty()
    {
        WriteAssets("not valid json {{{");

        var info = AssetsReader.Read(_tempDir);

        Assert.Empty(info.PackageFolders);
        Assert.Empty(info.ResolvedVersions);
    }
}
