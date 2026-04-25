# Transitive Map Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enrich `package-map.json` so each direct package reference includes its resolved version and a flag indicating whether the dependency itself ships a NuSpec.AI map, enabling AI tools to recurse into dependency package maps via the standard NuGet cache layout.

**Architecture:** New `AssetsReader` parses `obj/project.assets.json` for the resolved dep graph and packages roots. New `DependencyResolver` combines `CsprojReader` declared refs with `AssetsReader` resolved versions, then probes the NuGet cache for `ai/package-map.json` per dep. `ProjectAnalyzer` switches from `CsprojReader.ReadDependencies` to `DependencyResolver.Resolve`. Schema bumps to 2; package version bumps to 3.0.0.

**Tech Stack:** .NET 8.0, Roslyn (`Microsoft.CodeAnalysis.CSharp`), `System.Text.Json`, YamlDotNet, xUnit.

**Spec:** `docs/superpowers/specs/2026-04-25-transitive-map-discovery-design.md`

---

## File Map

**Models (`src/NuSpec.AI.Tool/Models/`)**
- Modify: `DependencyInfo.cs` — `PackageReferences` type changes from `IReadOnlyList<string>` to `IReadOnlyList<PackageReferenceInfo>`.
- Modify: `PackageMap.cs` — `SchemaVersion` default changes from `1` to `2`.
- Create: `PackageReferenceInfo.cs` — per-package entry with `Id`, `Version`, `HasNuSpecAiMap`.

**ProjectMetadata (`src/NuSpec.AI.Tool/ProjectMetadata/`)**
- Modify: `CsprojReader.cs` — replace `ReadDependencies` with `ReadDeclaredPackageReferences` (returns `IReadOnlyList<DeclaredPackageReference>`) and `ReadFrameworkReferences` (returns `IReadOnlyList<string>`).
- Create: `DeclaredPackageReference.cs` — `Id`, `IsPrivateAssetsAll`.
- Create: `AssetsReader.cs` — parses `obj/project.assets.json`, returns `AssetsInfo`.
- Create: `AssetsInfo.cs` — `PackageFolders` (ordered), `ResolvedVersions` (id → version).
- Create: `DependencyResolver.cs` — orchestrates csproj + assets + filesystem checks, returns `DependencyInfo`.

**Analysis (`src/NuSpec.AI.Tool/Analysis/`)**
- Modify: `ProjectAnalyzer.cs` — `Analyze` uses `DependencyResolver.Resolve` instead of `CsprojReader.ReadDependencies`.

**Formats (`src/NuSpec.AI.Tool/Formats/`)**
- Modify: `UltraCompactFormatter.cs` — emit `id|version|flag` per dep on the `#dep` line.
- (`JsonFormatter`, `CompactJsonFormatter`, `YamlFormatter` — no source change; serializers handle the model change.)

**Tests (`tests/NuSpec.AI.Tool.Tests/`)**
- Modify: `CsprojReaderTests.cs` — update existing dependency tests to use new method names; add tests for `IsPrivateAssetsAll`.
- Create: `AssetsReaderTests.cs`.
- Create: `DependencyResolverTests.cs`.
- Create: `Formats/UltraCompactFormatterDependencyTests.cs` (or extend existing if present).
- Modify: `Integration/FormatsIntegrationTests.cs` — update fixtures for new dep shape.

**Sample (`tests/SampleProject/`)**
- No structural change required. Schema regression captured in unit/integration tests.

**Packaging (`src/NuSpec.AI/`)**
- Modify: `NuSpec.AI.csproj` — bump `<Version>` from `2.0.0` to `3.0.0`.

**Docs**
- Modify: `README.md` — bump schemaVersion mention, update Schema Reference → Dependencies table, update example snippet, add "Transitive Map Discovery" section, bump version in Quick Start.
- Modify: `NUGET_README.md` — bump schemaVersion, update example, bump version.
- Modify: `CLAUDE.md` — note `schemaVersion: 2` and one-sentence transitive map discovery summary.

---

## Task 1: Add `PackageReferenceInfo` model

**Files:**
- Create: `src/NuSpec.AI.Tool/Models/PackageReferenceInfo.cs`
- Test: `tests/NuSpec.AI.Tool.Tests/Models/PackageReferenceInfoTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/NuSpec.AI.Tool.Tests/Models/PackageReferenceInfoTests.cs`:

```csharp
using System.Text.Json;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Tests.Models;

public class PackageReferenceInfoTests
{
    [Fact]
    public void SerializesAllFieldsAsJson()
    {
        var info = new PackageReferenceInfo
        {
            Id = "Acme.OrdersCore",
            Version = "1.2.0",
            HasNuSpecAiMap = true
        };

        var json = JsonSerializer.Serialize(info);

        Assert.Contains("\"id\":\"Acme.OrdersCore\"", json);
        Assert.Contains("\"version\":\"1.2.0\"", json);
        Assert.Contains("\"hasNuSpecAiMap\":true", json);
    }

    [Fact]
    public void Version_CanBeNull()
    {
        var info = new PackageReferenceInfo
        {
            Id = "Acme.OrdersCore",
            Version = null,
            HasNuSpecAiMap = false
        };

        var json = JsonSerializer.Serialize(info);

        Assert.Contains("\"id\":\"Acme.OrdersCore\"", json);
        Assert.Contains("\"version\":null", json);
        Assert.Contains("\"hasNuSpecAiMap\":false", json);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~PackageReferenceInfoTests"
```

Expected: build error — `PackageReferenceInfo` not found.

- [ ] **Step 3: Create the model**

Create `src/NuSpec.AI.Tool/Models/PackageReferenceInfo.cs`:

```csharp
using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class PackageReferenceInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string? Version { get; init; }

    [JsonPropertyName("hasNuSpecAiMap")]
    public required bool HasNuSpecAiMap { get; init; }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~PackageReferenceInfoTests"
```

Expected: 2/2 passing.

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/Models/PackageReferenceInfo.cs tests/NuSpec.AI.Tool.Tests/Models/PackageReferenceInfoTests.cs
git commit -m "feat: add PackageReferenceInfo model"
```

---

## Task 2: Bump schema version to 2 and update `DependencyInfo`

This task changes `DependencyInfo.PackageReferences` from `IReadOnlyList<string>` to `IReadOnlyList<PackageReferenceInfo>`. That breaks the existing `CsprojReader.ReadDependencies` (it builds `DependencyInfo` directly with strings). To keep tests green between commits, this task includes a temporary adapter inside `CsprojReader` that wraps each declared id in a `PackageReferenceInfo { Id, Version=null, HasNuSpecAiMap=false }`. Task 6 removes this adapter once `DependencyResolver` is wired in.

**Files:**
- Modify: `src/NuSpec.AI.Tool/Models/PackageMap.cs:8`
- Modify: `src/NuSpec.AI.Tool/Models/DependencyInfo.cs:8`
- Modify: `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs:79-83`
- Modify: `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs:155, 174-175`

- [ ] **Step 1: Write failing test for SchemaVersion**

Add to a new file `tests/NuSpec.AI.Tool.Tests/Models/SchemaVersionTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test — expect compile failure**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~SchemaVersionTests"
```

Expected: build error because `DependencyInfo.PackageReferences` is `IReadOnlyList<string>`, can't accept `PackageReferenceInfo[]`.

- [ ] **Step 3: Update `DependencyInfo` model**

Replace `src/NuSpec.AI.Tool/Models/DependencyInfo.cs` contents with:

```csharp
using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class DependencyInfo
{
    [JsonPropertyName("packageReferences")]
    public required IReadOnlyList<PackageReferenceInfo> PackageReferences { get; init; }

    [JsonPropertyName("frameworkReferences")]
    public required IReadOnlyList<string> FrameworkReferences { get; init; }
}
```

- [ ] **Step 4: Update `PackageMap.SchemaVersion` default**

In `src/NuSpec.AI.Tool/Models/PackageMap.cs`, change line 8:

```csharp
public int SchemaVersion { get; init; } = 2;
```

- [ ] **Step 5: Update `CsprojReader.ReadDependencies` to wrap declared refs**

In `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs`, replace the `return new DependencyInfo { ... }` block (lines ~79-83) with:

```csharp
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
```

This is a temporary shim — Task 6 replaces the call to `ReadDependencies` with `DependencyResolver.Resolve` and Task 4 removes `ReadDependencies` entirely.

- [ ] **Step 6: Update existing `CsprojReaderTests` to compile**

In `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs`:

Replace line 155 (`ReadsPackageReferences` assertion) with:

```csharp
        Assert.Equal(new[] { "Newtonsoft.Json", "Serilog" }, deps.PackageReferences.Select(p => p.Id).ToArray());
```

Replace lines 174-175 (`ExcludesPrivateAssetsAll` assertion) with:

```csharp
        Assert.Single(deps.PackageReferences);
        Assert.Equal("Visible.Package", deps.PackageReferences[0].Id);
```

- [ ] **Step 7: Run all tests — expect green**

```bash
dotnet test NuSpec.AI.slnx
```

Expected: all tests pass (existing 88 + 2 new from Task 1 + 1 new from this task).

- [ ] **Step 8: Commit**

```bash
git add src/NuSpec.AI.Tool/Models/DependencyInfo.cs src/NuSpec.AI.Tool/Models/PackageMap.cs src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs tests/NuSpec.AI.Tool.Tests/Models/SchemaVersionTests.cs tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs
git commit -m "feat: bump schema to v2; DependencyInfo.PackageReferences uses PackageReferenceInfo"
```

---

## Task 3: Add `DeclaredPackageReference` and `CsprojReader.ReadDeclaredPackageReferences`

**Files:**
- Create: `src/NuSpec.AI.Tool/ProjectMetadata/DeclaredPackageReference.cs`
- Modify: `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs`
- Test: `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs`

- [ ] **Step 1: Write failing test**

Append to `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs` before the closing `}`:

```csharp
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
```

- [ ] **Step 2: Run tests — expect failure**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~CsprojReaderTests.ReadDeclaredPackageReferences|FullyQualifiedName~CsprojReaderTests.ReadFrameworkReferences"
```

Expected: build error — methods don't exist.

- [ ] **Step 3: Create `DeclaredPackageReference`**

Create `src/NuSpec.AI.Tool/ProjectMetadata/DeclaredPackageReference.cs`:

```csharp
namespace NuSpec.AI.Tool.ProjectMetadata;

public sealed class DeclaredPackageReference
{
    public required string Id { get; init; }
    public required bool IsPrivateAssetsAll { get; init; }
}
```

- [ ] **Step 4: Add new methods to `CsprojReader`**

In `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs`, add these two public static methods (place above `GetProperty`):

```csharp
public static IReadOnlyList<DeclaredPackageReference> ReadDeclaredPackageReferences(string csprojPath)
{
    var doc = XDocument.Load(csprojPath);
    var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

    return doc.Descendants(ns + "PackageReference")
        .Select(el =>
        {
            var id = el.Attribute("Include")?.Value ?? "";
            var privateAssets = el.Attribute("PrivateAssets")?.Value
                ?? el.Element(ns + "PrivateAssets")?.Value;
            var isPrivate = string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase);
            return new DeclaredPackageReference
            {
                Id = id,
                IsPrivateAssetsAll = isPrivate
            };
        })
        .Where(r => !string.IsNullOrWhiteSpace(r.Id))
        .ToList();
}

public static IReadOnlyList<string> ReadFrameworkReferences(string csprojPath)
{
    var doc = XDocument.Load(csprojPath);
    var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

    return doc.Descendants(ns + "FrameworkReference")
        .Select(el => el.Attribute("Include")?.Value ?? "")
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct()
        .OrderBy(name => name)
        .ToList();
}
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~CsprojReaderTests"
```

Expected: all CsprojReaderTests pass (existing + 3 new).

- [ ] **Step 6: Commit**

```bash
git add src/NuSpec.AI.Tool/ProjectMetadata/DeclaredPackageReference.cs src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs
git commit -m "feat: add ReadDeclaredPackageReferences and ReadFrameworkReferences"
```

---

## Task 4: Add `AssetsReader`

Parses `obj/project.assets.json` and returns the package folders + resolved versions.

**Files:**
- Create: `src/NuSpec.AI.Tool/ProjectMetadata/AssetsInfo.cs`
- Create: `src/NuSpec.AI.Tool/ProjectMetadata/AssetsReader.cs`
- Test: `tests/NuSpec.AI.Tool.Tests/AssetsReaderTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/NuSpec.AI.Tool.Tests/AssetsReaderTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~AssetsReaderTests"
```

Expected: build error — `AssetsReader` not found.

- [ ] **Step 3: Create `AssetsInfo`**

Create `src/NuSpec.AI.Tool/ProjectMetadata/AssetsInfo.cs`:

```csharp
namespace NuSpec.AI.Tool.ProjectMetadata;

public sealed class AssetsInfo
{
    public required IReadOnlyList<string> PackageFolders { get; init; }
    public required IReadOnlyDictionary<string, string> ResolvedVersions { get; init; }

    public static AssetsInfo Empty { get; } = new()
    {
        PackageFolders = Array.Empty<string>(),
        ResolvedVersions = new Dictionary<string, string>()
    };
}
```

- [ ] **Step 4: Create `AssetsReader`**

Create `src/NuSpec.AI.Tool/ProjectMetadata/AssetsReader.cs`:

```csharp
using System.Text.Json;

namespace NuSpec.AI.Tool.ProjectMetadata;

public static class AssetsReader
{
    public static AssetsInfo Read(string projectDir)
    {
        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
            return AssetsInfo.Empty;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = doc.RootElement;

            var folders = new List<string>();
            if (root.TryGetProperty("packageFolders", out var foldersEl) &&
                foldersEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var folder in foldersEl.EnumerateObject())
                    folders.Add(folder.Name);
            }

            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("targets", out var targetsEl) &&
                targetsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var tfm in targetsEl.EnumerateObject())
                {
                    if (tfm.Value.ValueKind != JsonValueKind.Object) continue;
                    foreach (var pkg in tfm.Value.EnumerateObject())
                    {
                        if (pkg.Value.TryGetProperty("type", out var typeEl) &&
                            !string.Equals(typeEl.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var key = pkg.Name;
                        var slash = key.IndexOf('/');
                        if (slash <= 0 || slash == key.Length - 1) continue;

                        var id = key[..slash];
                        var version = key[(slash + 1)..];

                        // First TFM wins
                        if (!resolved.ContainsKey(id))
                            resolved[id] = version;
                    }
                }
            }

            return new AssetsInfo
            {
                PackageFolders = folders,
                ResolvedVersions = resolved
            };
        }
        catch
        {
            return AssetsInfo.Empty;
        }
    }
}
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~AssetsReaderTests"
```

Expected: 6/6 passing.

- [ ] **Step 6: Commit**

```bash
git add src/NuSpec.AI.Tool/ProjectMetadata/AssetsInfo.cs src/NuSpec.AI.Tool/ProjectMetadata/AssetsReader.cs tests/NuSpec.AI.Tool.Tests/AssetsReaderTests.cs
git commit -m "feat: add AssetsReader to parse project.assets.json"
```

---

## Task 5: Add `DependencyResolver`

Combines csproj declared refs + assets resolved versions + filesystem cache probes to produce a final `DependencyInfo`.

**Files:**
- Create: `src/NuSpec.AI.Tool/ProjectMetadata/DependencyResolver.cs`
- Test: `tests/NuSpec.AI.Tool.Tests/DependencyResolverTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/NuSpec.AI.Tool.Tests/DependencyResolverTests.cs`:

```csharp
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
        // No assets file written.

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
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~DependencyResolverTests"
```

Expected: build error — `DependencyResolver` not found.

- [ ] **Step 3: Create `DependencyResolver`**

Create `src/NuSpec.AI.Tool/ProjectMetadata/DependencyResolver.cs`:

```csharp
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.ProjectMetadata;

public static class DependencyResolver
{
    public static DependencyInfo Resolve(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
            ?? throw new InvalidOperationException($"Cannot determine directory for: {csprojPath}");

        var declared = CsprojReader.ReadDeclaredPackageReferences(csprojPath);
        var frameworkRefs = CsprojReader.ReadFrameworkReferences(csprojPath);
        var assets = AssetsReader.Read(projectDir);

        var packageRefs = declared
            .Where(r => !r.IsPrivateAssetsAll)
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r =>
            {
                assets.ResolvedVersions.TryGetValue(r.Id, out var version);
                var hasMap = version is not null && PackageMapExists(assets.PackageFolders, r.Id, version);
                return new PackageReferenceInfo
                {
                    Id = r.Id,
                    Version = version,
                    HasNuSpecAiMap = hasMap
                };
            })
            .ToList();

        return new DependencyInfo
        {
            PackageReferences = packageRefs,
            FrameworkReferences = frameworkRefs
        };
    }

    private static bool PackageMapExists(IReadOnlyList<string> packageFolders, string id, string version)
    {
        var idLower = id.ToLowerInvariant();
        foreach (var root in packageFolders)
        {
            var path = Path.Combine(root, idLower, version, "ai", "package-map.json");
            if (File.Exists(path))
                return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~DependencyResolverTests"
```

Expected: 8/8 passing.

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/ProjectMetadata/DependencyResolver.cs tests/NuSpec.AI.Tool.Tests/DependencyResolverTests.cs
git commit -m "feat: add DependencyResolver for cache-aware dep resolution"
```

---

## Task 6: Wire `DependencyResolver` into `ProjectAnalyzer` and remove the shim

**Files:**
- Modify: `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs:17`
- Modify: `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs` (remove `ReadDependencies`)
- Modify: `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs` (delete tests for removed method)

- [ ] **Step 1: Switch `ProjectAnalyzer.Analyze` to use `DependencyResolver`**

In `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`, change line 17:

```csharp
        var dependencies = DependencyResolver.Resolve(csprojPath);
```

(Replaces `var dependencies = CsprojReader.ReadDependencies(csprojPath);`.)

- [ ] **Step 2: Remove `ReadDependencies` from `CsprojReader`**

In `src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs`, delete the entire `ReadDependencies(string csprojPath)` method (the one that returns `DependencyInfo`). Keep `ReadPackageInfo`, `ReadDeclaredPackageReferences`, `ReadFrameworkReferences`, and `GetProperty`.

- [ ] **Step 3: Remove obsolete tests for `ReadDependencies`**

In `tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs`, delete the `ReadsPackageReferences`, `ExcludesPrivateAssetsAll`, and `ReadsFrameworkReferences` test methods. Their behavior is covered by `ReadDeclaredPackageReferences` tests (Task 3) and `DependencyResolverTests` (Task 5).

- [ ] **Step 4: Run all tests — expect green**

```bash
dotnet test NuSpec.AI.slnx
```

Expected: all tests pass. The shim from Task 2 is no longer used; the real resolver runs.

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs src/NuSpec.AI.Tool/ProjectMetadata/CsprojReader.cs tests/NuSpec.AI.Tool.Tests/CsprojReaderTests.cs
git commit -m "refactor: use DependencyResolver in ProjectAnalyzer; drop ReadDependencies shim"
```

---

## Task 7: Update `UltraCompactFormatter` for the new dep shape

The `#dep` line currently emits `id;id;id`. New format: `id|version|flag` per dep, separated by `;`. `flag` is `1` (has map) or `0` (no map). `version` is empty string when null.

**Files:**
- Modify: `src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs:25-26`
- Test: `tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterDepTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterDepTests.cs`:

```csharp
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
            PackageReferences = deps,
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
            new() { Id = "Newtonsoft.Json", Version = "13.0.3", HasNuSpecAiMap = false },
            new() { Id = "Acme.OrdersCore", Version = "1.2.0", HasNuSpecAiMap = true });

        var output = new UltraCompactFormatter().Serialize(map);

        Assert.Contains("#dep Newtonsoft.Json|13.0.3|0;Acme.OrdersCore|1.2.0|1", output);
    }

    [Fact]
    public void NullVersion_EmitsEmptyVersionField()
    {
        var map = MakeMap(
            new() { Id = "Foo", Version = null, HasNuSpecAiMap = false });

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
```

- [ ] **Step 2: Run tests — expect failure**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~UltraCompactFormatterDepTests"
```

Expected: 2 failures (current formatter still emits `id;id;id`).

- [ ] **Step 3: Update `UltraCompactFormatter`**

In `src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs`, replace lines 25-26:

```csharp
        if (deps.PackageReferences.Count > 0)
            sb.AppendLine($"#dep {string.Join(";", deps.PackageReferences.Select(FormatDep))}");
```

Then add this helper method to the class (near the bottom, beside the other `Format*` helpers):

```csharp
    // "Acme.OrdersCore" v "1.2.0" map=true → "Acme.OrdersCore|1.2.0|1"
    private static string FormatDep(PackageReferenceInfo p)
    {
        var version = p.Version ?? "";
        var flag = p.HasNuSpecAiMap ? "1" : "0";
        return $"{p.Id}|{version}|{flag}";
    }
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~UltraCompactFormatterDepTests"
```

Expected: 3/3 passing.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test NuSpec.AI.slnx
```

Expected: all tests pass. (Some existing integration/format tests may need fixture updates — see Task 8.)

- [ ] **Step 6: Commit**

```bash
git add src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterDepTests.cs
git commit -m "feat: ultra format emits id|version|flag per dependency"
```

---

## Task 8: Repair existing format/integration test fixtures

Existing tests in `tests/NuSpec.AI.Tool.Tests/Formats/` and `tests/NuSpec.AI.Tool.Tests/Integration/` may construct `DependencyInfo` with `string[]` for `PackageReferences`. Those won't compile after Task 2 unless they were already updated. This task sweeps any remaining failures.

**Files:**
- Modify: any failing test under `tests/NuSpec.AI.Tool.Tests/Formats/` or `tests/NuSpec.AI.Tool.Tests/Integration/`

- [ ] **Step 1: Run full suite to identify failures**

```bash
dotnet test NuSpec.AI.slnx
```

Note: if Task 2's shim kept things compiling, all formatter/integration tests should already pass. If any fail (typically because they assert against the old `string[]` JSON shape), proceed to Step 2.

- [ ] **Step 2: For each failing test, update fixture/assertion**

Pattern: any place that constructs `new DependencyInfo { PackageReferences = new[] { "Foo", "Bar" }, ... }` needs:

```csharp
PackageReferences = new[]
{
    new PackageReferenceInfo { Id = "Foo", Version = "1.0.0", HasNuSpecAiMap = false },
    new PackageReferenceInfo { Id = "Bar", Version = "1.0.0", HasNuSpecAiMap = false }
}
```

Any place that asserts JSON output containing `"packageReferences":["Foo","Bar"]` should expect the object form, e.g.:

```csharp
Assert.Contains("\"packageReferences\":[", output);
Assert.Contains("\"id\":\"Foo\"", output);
```

- [ ] **Step 3: Re-run full suite — expect green**

```bash
dotnet test NuSpec.AI.slnx
```

Expected: all tests pass.

- [ ] **Step 4: Commit (only if changes made)**

```bash
git add -A tests/
git commit -m "test: update fixtures for v2 schema dep shape"
```

If no files changed, skip the commit.

---

## Task 9: End-to-end smoke test against `SampleProject`

Verifies the whole pipeline (csproj → assets → resolver → formatters) produces the expected v2 shape on a real project. Uses the existing `SampleProject` plus a hand-written `obj/project.assets.json` fixture so the test is deterministic.

**Files:**
- Create: `tests/NuSpec.AI.Tool.Tests/Integration/EndToEndDependencyTests.cs`

- [ ] **Step 1: Write the test**

Create `tests/NuSpec.AI.Tool.Tests/Integration/EndToEndDependencyTests.cs`:

```csharp
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

        var assetsRoot = _cacheRoot.Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(_tempDir, "obj", "project.assets.json"), $$"""
            {
              "version": 3,
              "packageFolders": { "{{assetsRoot}}\\": {} },
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
```

- [ ] **Step 2: Run test — expect PASS**

```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "FullyQualifiedName~EndToEndDependencyTests"
```

Expected: 1/1 passing.

- [ ] **Step 3: Commit**

```bash
git add tests/NuSpec.AI.Tool.Tests/Integration/EndToEndDependencyTests.cs
git commit -m "test: end-to-end v2 schema verification with mocked NuGet cache"
```

---

## Task 10: Bump package version to 3.0.0

**Files:**
- Modify: `src/NuSpec.AI/NuSpec.AI.csproj` (the `<Version>` element)

- [ ] **Step 1: Update the csproj**

In `src/NuSpec.AI/NuSpec.AI.csproj`, change `<Version>2.0.0</Version>` to `<Version>3.0.0</Version>`.

- [ ] **Step 2: Verify build**

```bash
dotnet build NuSpec.AI.slnx
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/NuSpec.AI/NuSpec.AI.csproj
git commit -m "chore: bump version to 3.0.0 for v2 schema"
```

---

## Task 11: Update `README.md`

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update Quick Start version**

Change `Version="2.0.0"` to `Version="3.0.0"` in the `PackageReference` example.

- [ ] **Step 2: Update example `package-map.json` snippet**

In the "What Gets Generated" section, change `"schemaVersion": 1` to `"schemaVersion": 2`.

If the example shows `"packageReferences": ["Microsoft.EntityFrameworkCore"]`, replace with:

```json
    "packageReferences": [
      {
        "id": "Microsoft.EntityFrameworkCore",
        "version": "8.0.0",
        "hasNuSpecAiMap": false
      }
    ],
```

- [ ] **Step 3: Update Schema Reference → Root table**

Change the description for `schemaVersion`: `Always 2. Will increment on breaking changes.`

- [ ] **Step 4: Update Schema Reference → Dependencies table**

Replace the `packageReferences` row with:

```markdown
| `packageReferences` | `object[]` | One entry per direct package reference (excluding `PrivateAssets="all"`). See "Package References" below. |
```

Add a new "Package References" sub-section after the "Dependencies" table:

```markdown
### Package References

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Package id, declared casing preserved. |
| `version` | `string \| null` | Resolved version from `obj/project.assets.json`. `null` if restore has not run. |
| `hasNuSpecAiMap` | `bool` | `true` if the dependency itself ships a NuSpec.AI map. AI tools resolve `<NuGet packages root>/<id-lowercase>/<version>/ai/package-map.json` to read it. |
```

- [ ] **Step 5: Add "Transitive Map Discovery" subsection**

Append under "How It Works" (or as a sibling section before "Schema Reference"):

````markdown
## Transitive Map Discovery

Each emitted `package-map.json` lists its direct package dependencies along with a `hasNuSpecAiMap` flag. AI tools that want detail about a flagged dependency `A` should resolve:

```
<NuGet packages root>/<A.id-lowercase>/<A.version>/ai/package-map.json
```

The NuGet packages root is `~/.nuget/packages` on macOS/Linux and `%USERPROFILE%\.nuget\packages` on Windows by default; it can be overridden via the `NUGET_PACKAGES` environment variable or the `globalPackagesFolder` setting in `nuget.config`. Tools that read `obj/project.assets.json` (preferred) get the canonical `packageFolders` list directly — iterate them in order; the first match wins.

To traverse the full dependency tree, recurse into each found map's own `packageReferences` entries.
````

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: update README for v2 schema and transitive map discovery"
```

---

## Task 12: Update `NUGET_README.md`

**Files:**
- Modify: `NUGET_README.md`

- [ ] **Step 1: Bump version reference**

If the file references `Version="2.0.0"`, change to `3.0.0`.

- [ ] **Step 2: Update example snippet**

Change `"schemaVersion": 1` to `"schemaVersion": 2`. If the file shows a sample `packageReferences` array, update it to the object form (same as Task 11 Step 2).

- [ ] **Step 3: Commit**

```bash
git add NUGET_README.md
git commit -m "docs: update NUGET_README for v2 schema"
```

---

## Task 13: Update `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update "Conventions" section**

Change `schemaVersion in the JSON is 1 — increment on breaking schema changes` to `schemaVersion in the JSON is 2 — increment on breaking schema changes`.

- [ ] **Step 2: Add transitive discovery note under "Architecture"**

Append a new bullet after the existing Architecture bullets:

> - **Transitive map discovery.** Each emitted `package-map.json` enriches its direct deps with resolved version and a `hasNuSpecAiMap` flag. AI tools resolve `<NuGet packages root>/<id-lower>/<version>/ai/package-map.json` to recurse. `DependencyResolver` orchestrates this at pack time using `CsprojReader` (declared refs) + `AssetsReader` (`obj/project.assets.json`) + filesystem probes.

- [ ] **Step 3: Remove the now-implemented bullet from "Planned Enhancements"**

If the "Planned Enhancements" section still lists transitive discovery, remove that line. (It currently lists MSBuild exclusions; leave that alone.)

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: note v2 schema and transitive discovery in CLAUDE.md"
```

---

## Task 14: Final verification

- [ ] **Step 1: Full test suite**

```bash
dotnet test NuSpec.AI.slnx
```

Expected: all tests pass.

- [ ] **Step 2: Build the package end-to-end**

```bash
dotnet publish src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj -c Release -o src/NuSpec.AI/tools/net8.0 --no-self-contained
dotnet pack src/NuSpec.AI/NuSpec.AI.csproj -c Release -o artifacts --no-build
```

Expected: a `NuSpec.AI.3.0.0.nupkg` is produced in `artifacts/`.

- [ ] **Step 3: Manual SampleProject pack**

```bash
dotnet pack tests/SampleProject/SampleProject.csproj -c Release -o artifacts/sample
```

Inspect the generated `ai/package-map.json` inside the produced `.nupkg`. Confirm:
- `"schemaVersion": 2`
- `packageReferences` entries are objects with `id`, `version`, `hasNuSpecAiMap`

(If `SampleProject` has any direct PackageReferences other than NuSpec.AI itself — which is `PrivateAssets="all"` and excluded — they should appear in the enriched form.)

- [ ] **Step 4: Hand off to finishing-a-development-branch**

After the suite passes, invoke `superpowers:finishing-a-development-branch` to choose merge / PR / keep / discard.

---

## Self-review

**Spec coverage:** Each spec section maps to tasks:
- Schema change → Tasks 1, 2
- CLI changes (AssetsReader, DependencyResolver, CsprojReader split, Models, ProjectAnalyzer) → Tasks 3, 4, 5, 6
- Formatters → Task 7 (ultra explicit; JSON/compact/yaml auto via model change, fixture sweep in Task 8)
- Edge cases → covered across DependencyResolverTests (Task 5) and AssetsReaderTests (Task 4)
- Testing strategy → Tasks 1, 4, 5, 7, 9 (all unit + smoke layers)
- Documentation → Tasks 11, 12, 13
- Versioning → Task 10

**No placeholders:** every step contains either exact code, exact commands, or exact file/line references.

**Type consistency:** `PackageReferenceInfo`, `DeclaredPackageReference`, `AssetsInfo`, `DependencyResolver`, `DependencyInfo` (existing) — names used consistently across tasks. Property names (`Id`, `Version`, `HasNuSpecAiMap`, `IsPrivateAssetsAll`, `PackageFolders`, `ResolvedVersions`, `PackageReferences`, `FrameworkReferences`) are stable across every reference.
