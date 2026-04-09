# NuSpec.AI Pro — Plan A: Attributes Package + Formatters

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship NuSpec.AI.Attributes as a standalone NuGet package and implement all four output formatters (JSON, YAML, compact JSON, ultra-compact) inside NuSpec.AI.Tool, ready to be wired up by Plan B.

**Architecture:** NuSpec.AI.Attributes is a tiny netstandard2.0 class library with three attribute classes and no dependencies. The formatters live in `src/NuSpec.AI.Tool/Formats/` and implement a shared `IFormatter` interface. The existing `ProjectAnalyzer.SerializeToJson()` is refactored into `JsonFormatter`. CLI args are extended with `--formats` (semicolon-separated, defaults to `json`). Plan B wires in licensing and the Pro MSBuild package.

**Tech Stack:** .NET 8, C# 12, YamlDotNet (YAML serialization), System.Text.Json (JSON), xUnit

---

## File Map

### New files
| File | Responsibility |
|------|---------------|
| `src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj` | netstandard2.0 class library, no deps |
| `src/NuSpec.AI.Attributes/AiRoleAttribute.cs` | `[AiRole("role1", "role2")]` |
| `src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs` | `[AiIgnore]` |
| `src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs` | `[AiDescription("...")]` |
| `src/NuSpec.AI.Tool/Formats/IFormatter.cs` | Formatter contract |
| `src/NuSpec.AI.Tool/Formats/JsonFormatter.cs` | Standard JSON (extracted from ProjectAnalyzer) |
| `src/NuSpec.AI.Tool/Formats/YamlFormatter.cs` | YAML output via YamlDotNet |
| `src/NuSpec.AI.Tool/Formats/CompactJsonFormatter.cs` | Short-key minified JSON |
| `src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs` | Custom terse text format |
| `src/NuSpec.AI.Tool/Formats/FormatterRegistry.cs` | Resolves format names to IFormatter instances |
| `tests/NuSpec.AI.Tool.Tests/Formats/FormatterTestBase.cs` | Shared test fixture (sample PackageMap) |
| `tests/NuSpec.AI.Tool.Tests/Formats/JsonFormatterTests.cs` | |
| `tests/NuSpec.AI.Tool.Tests/Formats/YamlFormatterTests.cs` | |
| `tests/NuSpec.AI.Tool.Tests/Formats/CompactJsonFormatterTests.cs` | |
| `tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterTests.cs` | |

### Modified files
| File | Change |
|------|--------|
| `src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj` | Add YamlDotNet package reference |
| `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs` | Replace `SerializeToJson()` with formatter dispatch; add `--formats` support |
| `src/NuSpec.AI.Tool/Program.cs` | Parse `--formats` arg; pass to ProjectAnalyzer |
| `NuSpec.AI.slnx` | Add NuSpec.AI.Attributes project |

---

## Task 1: Create NuSpec.AI.Attributes project

**Files:**
- Create: `src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj`
- Create: `src/NuSpec.AI.Attributes/AiRoleAttribute.cs`
- Create: `src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs`
- Create: `src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs`
- Modify: `NuSpec.AI.slnx`

- [ ] **Create the project file**

`src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>NuSpec.AI</RootNamespace>

    <PackageId>NuSpec.AI.Attributes</PackageId>
    <Version>1.0.0</Version>
    <Description>Attributes for controlling NuSpec.AI Pro output: [AiRole], [AiIgnore], [AiDescription].</Description>
    <Authors>Sean Cooper</Authors>
    <PackageTags>AI;NuGet;attributes;NuSpec.AI</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
</Project>
```

- [ ] **Create AiRoleAttribute.cs**

`src/NuSpec.AI.Attributes/AiRoleAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Specifies one or more semantic roles for a type, overriding NuSpec.AI's automatic role inference.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
/// <example>[AiRole("aggregate-root", "audited")]</example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false)]
public sealed class AiRoleAttribute : Attribute
{
    public AiRoleAttribute(params string[] roles) => Roles = roles;
    public string[] Roles { get; }
}
```

- [ ] **Create AiIgnoreAttribute.cs**

`src/NuSpec.AI.Attributes/AiIgnoreAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Excludes the target type or member from the NuSpec.AI generated package map.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiIgnoreAttribute : Attribute { }
```

- [ ] **Create AiDescriptionAttribute.cs**

`src/NuSpec.AI.Attributes/AiDescriptionAttribute.cs`:
```csharp
namespace NuSpec.AI;

/// <summary>
/// Provides a description for AI context, overriding any XML doc comment summary.
/// Useful when the AI-facing description should differ from IntelliSense documentation.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiDescriptionAttribute : Attribute
{
    public AiDescriptionAttribute(string description) => Description = description;
    public string Description { get; }
}
```

- [ ] **Add to solution**

Edit `NuSpec.AI.slnx` to add the Attributes project inside a new `<Folder Name="/src/">` entry (it already has one — add the project path alongside the existing ones):
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj" />
    <Project Path="src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Build to verify**
```bash
cd E:\repos\NuSpec.AI
dotnet build NuSpec.AI.slnx
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Attributes/ NuSpec.AI.slnx
git commit -m "feat: add NuSpec.AI.Attributes package with AiRole, AiIgnore, AiDescription"
```

---

## Task 2: Define the IFormatter interface and test fixture

**Files:**
- Create: `src/NuSpec.AI.Tool/Formats/IFormatter.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Formats/FormatterTestBase.cs`

- [ ] **Create IFormatter.cs**

`src/NuSpec.AI.Tool/Formats/IFormatter.cs`:
```csharp
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
```

- [ ] **Create shared test fixture**

`tests/NuSpec.AI.Tool.Tests/Formats/FormatterTestBase.cs`:
```csharp
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Tests.Formats;

public abstract class FormatterTestBase
{
    protected static PackageMap BuildSamplePackageMap() => new()
    {
        Package = new PackageInfo
        {
            Id = "Acme.Orders",
            Version = "1.0.0",
            Description = "Order management library.",
            Tags = ["orders", "dal"],
            TargetFrameworks = ["net8.0"]
        },
        Dependencies = new DependencyInfo
        {
            PackageReferences = ["Microsoft.EntityFrameworkCore"],
            FrameworkReferences = []
        },
        PublicSurface = new PublicSurfaceInfo
        {
            Namespaces = ["Acme.Orders", "Acme.Orders.Models"],
            Types =
            [
                new TypeInfo
                {
                    Name = "Order",
                    FullName = "Acme.Orders.Models.Order",
                    Namespace = "Acme.Orders.Models",
                    Kind = "class",
                    Roles = ["entity"],
                    Documentation = "Represents a customer order.",
                    Members =
                    [
                        new MemberInfo { Kind = "property", Name = "Id", Signature = "public int Id { get; set; }" },
                        new MemberInfo { Kind = "property", Name = "Status", Signature = "public OrderStatus Status { get; set; }", Documentation = "Current status." }
                    ]
                },
                new TypeInfo
                {
                    Name = "OrderStatus",
                    FullName = "Acme.Orders.Models.OrderStatus",
                    Namespace = "Acme.Orders.Models",
                    Kind = "enum",
                    Roles = [],
                    Members =
                    [
                        new MemberInfo { Kind = "enum-value", Name = "Pending", Signature = "Pending = 0" },
                        new MemberInfo { Kind = "enum-value", Name = "Confirmed", Signature = "Confirmed = 1" }
                    ]
                },
                new TypeInfo
                {
                    Name = "IOrderRepository",
                    FullName = "Acme.Orders.IOrderRepository",
                    Namespace = "Acme.Orders",
                    Kind = "interface",
                    Roles = ["repository"],
                    Members =
                    [
                        new MemberInfo
                        {
                            Kind = "method",
                            Name = "GetByIdAsync",
                            Signature = "Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)",
                            Documentation = "Gets an order by ID."
                        }
                    ]
                }
            ]
        }
    };
}
```

- [ ] **Build to verify**
```bash
dotnet build NuSpec.AI.slnx
```
Expected: `Build succeeded.`

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/Formats/IFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/FormatterTestBase.cs
git commit -m "feat: add IFormatter interface and shared test fixture"
```

---

## Task 3: Extract JsonFormatter from ProjectAnalyzer

**Files:**
- Create: `src/NuSpec.AI.Tool/Formats/JsonFormatter.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Formats/JsonFormatterTests.cs`
- Modify: `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`

- [ ] **Write the failing test**

`tests/NuSpec.AI.Tool.Tests/Formats/JsonFormatterTests.cs`:
```csharp
using System.Text.Json;
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class JsonFormatterTests : FormatterTestBase
{
    private readonly JsonFormatter _formatter = new();

    [Fact]
    public void FormatId_IsJson()
    {
        Assert.Equal("json", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapJson()
    {
        Assert.Equal("package-map.json", _formatter.FileName);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result); // throws if invalid
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void Serialize_IncludesPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_IsIndented()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        // OrderStatus has no documentation
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result);
        var orderStatus = doc.RootElement
            .GetProperty("publicSurface")
            .GetProperty("types")
            .EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "OrderStatus");
        Assert.False(orderStatus.TryGetProperty("documentation", out _));
    }
}
```

- [ ] **Run test to verify it fails**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "JsonFormatterTests" -v n
```
Expected: FAIL — `JsonFormatter` does not exist yet.

- [ ] **Create JsonFormatter.cs**

`src/NuSpec.AI.Tool/Formats/JsonFormatter.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

public sealed class JsonFormatter : IFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FormatId => "json";
    public string FileName => "package-map.json";

    public string Serialize(PackageMap packageMap) =>
        JsonSerializer.Serialize(packageMap, Options);
}
```

- [ ] **Run tests to verify they pass**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "JsonFormatterTests" -v n
```
Expected: All 6 tests PASS.

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/Formats/JsonFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/JsonFormatterTests.cs
git commit -m "feat: add JsonFormatter implementing IFormatter"
```

---

## Task 4: Add YamlDotNet and build YamlFormatter

**Files:**
- Modify: `src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj`
- Create: `src/NuSpec.AI.Tool/Formats/YamlFormatter.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Formats/YamlFormatterTests.cs`

- [ ] **Add YamlDotNet to the tool project**

Edit `src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj` to add:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
  <PackageReference Include="YamlDotNet" Version="16.3.0" />
</ItemGroup>
```

- [ ] **Restore packages**
```bash
dotnet restore src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj
```

- [ ] **Write the failing tests**

`tests/NuSpec.AI.Tool.Tests/Formats/YamlFormatterTests.cs`:
```csharp
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class YamlFormatterTests : FormatterTestBase
{
    private readonly YamlFormatter _formatter = new();

    [Fact]
    public void FormatId_IsYaml()
    {
        Assert.Equal("yaml", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapYaml()
    {
        Assert.Equal("package-map.yaml", _formatter.FileName);
    }

    [Fact]
    public void Serialize_ContainsPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_ContainsSchemaVersion()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("schemaVersion: 1", result);
    }

    [Fact]
    public void Serialize_ContainsTypeKind()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("kind: class", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var yaml = _formatter.Serialize(map);
        Assert.True(yaml.Length < json.Length,
            $"YAML ({yaml.Length} chars) should be smaller than JSON ({json.Length} chars)");
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        // OrderStatus has no documentation — the key should not appear for it
        // Check that "documentation:" does not appear directly after "OrderStatus"
        var orderStatusIdx = result.IndexOf("OrderStatus", StringComparison.Ordinal);
        var nextTypeIdx = result.IndexOf("- name:", orderStatusIdx + 1, StringComparison.Ordinal);
        var orderStatusSection = nextTypeIdx > 0
            ? result[orderStatusIdx..nextTypeIdx]
            : result[orderStatusIdx..];
        Assert.DoesNotContain("documentation:", orderStatusSection);
    }
}
```

- [ ] **Run to verify they fail**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "YamlFormatterTests" -v n
```
Expected: FAIL — `YamlFormatter` does not exist.

- [ ] **Create YamlFormatter.cs**

`src/NuSpec.AI.Tool/Formats/YamlFormatter.cs`:
```csharp
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

    public string Serialize(PackageMap packageMap) =>
        Serializer.Serialize(packageMap);
}
```

- [ ] **Run tests to verify they pass**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "YamlFormatterTests" -v n
```
Expected: All 7 tests PASS.

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj src/NuSpec.AI.Tool/Formats/YamlFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/YamlFormatterTests.cs
git commit -m "feat: add YamlFormatter with YamlDotNet"
```

---

## Task 5: Build CompactJsonFormatter

**Files:**
- Create: `src/NuSpec.AI.Tool/Formats/CompactJsonFormatter.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Formats/CompactJsonFormatterTests.cs`

- [ ] **Write the failing tests**

`tests/NuSpec.AI.Tool.Tests/Formats/CompactJsonFormatterTests.cs`:
```csharp
using System.Text.Json;
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class CompactJsonFormatterTests : FormatterTestBase
{
    private readonly CompactJsonFormatter _formatter = new();

    [Fact]
    public void FormatId_IsCompact()
    {
        Assert.Equal("compact", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsCompactJson()
    {
        Assert.Equal("package-map.compact.json", _formatter.FileName);
    }

    [Fact]
    public void Serialize_IsValidJson()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        JsonDocument.Parse(result); // throws if invalid
    }

    [Fact]
    public void Serialize_UsesShortKeys()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\"v\":", result);      // schemaVersion
        Assert.Contains("\"n\":", result);      // name
        Assert.Contains("\"fn\":", result);     // fullName
        Assert.DoesNotContain("\"schemaVersion\":", result);
        Assert.DoesNotContain("\"fullName\":", result);
    }

    [Fact]
    public void Serialize_HasNoWhitespace()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var compact = _formatter.Serialize(map);
        Assert.True(compact.Length < json.Length,
            $"Compact ({compact.Length}) should be smaller than JSON ({json.Length})");
    }

    [Fact]
    public void Serialize_ContainsPackageId()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("Acme.Orders", result);
    }

    [Fact]
    public void Serialize_OmitsNullDocumentation()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        var doc = JsonDocument.Parse(result);
        var types = doc.RootElement.GetProperty("s").GetProperty("t").EnumerateArray().ToList();
        var orderStatus = types.First(t => t.GetProperty("n").GetString() == "OrderStatus");
        Assert.False(orderStatus.TryGetProperty("doc", out _));
    }
}
```

- [ ] **Run to verify they fail**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "CompactJsonFormatterTests" -v n
```
Expected: FAIL.

- [ ] **Create CompactJsonFormatter.cs**

`src/NuSpec.AI.Tool/Formats/CompactJsonFormatter.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

/// <summary>
/// Produces minified JSON with abbreviated keys to reduce token count by ~40-50%.
/// Key mapping: schemaVersion→v, package→p, dependencies→d, publicSurface→s,
/// namespaces→nss, types→t, name→n, fullName→fn, namespace→ns, kind→k,
/// roles→r, documentation→doc, members→m, signature→sig,
/// packageReferences→pr, frameworkReferences→fr, description→desc,
/// tags→tg, targetFrameworks→tfm, id→id, version→ver
/// </summary>
public sealed class CompactJsonFormatter : IFormatter
{
    private static readonly JsonSerializerOptions FullOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FormatId => "compact";
    public string FileName => "package-map.compact.json";

    public string Serialize(PackageMap packageMap)
    {
        // Serialize to full camelCase JSON first, then remap keys
        var fullJson = JsonSerializer.Serialize(packageMap, FullOptions);
        var node = JsonNode.Parse(fullJson)!;
        var remapped = RemapNode(node);
        return remapped.ToJsonString();
    }

    private static JsonNode RemapNode(JsonNode node)
    {
        return node switch
        {
            JsonObject obj => RemapObject(obj),
            JsonArray arr => RemapArray(arr),
            _ => node.DeepClone()
        };
    }

    private static JsonObject RemapObject(JsonObject obj)
    {
        var result = new JsonObject();
        foreach (var (key, value) in obj)
        {
            var shortKey = ShortenKey(key);
            result[shortKey] = value is null ? null : RemapNode(value);
        }
        return result;
    }

    private static JsonArray RemapArray(JsonArray arr)
    {
        var result = new JsonArray();
        foreach (var item in arr)
            result.Add(item is null ? null : RemapNode(item));
        return result;
    }

    private static string ShortenKey(string key) => key switch
    {
        "schemaVersion"     => "v",
        "package"           => "p",
        "dependencies"      => "d",
        "publicSurface"     => "s",
        "namespaces"        => "nss",
        "types"             => "t",
        "name"              => "n",
        "fullName"          => "fn",
        "namespace"         => "ns",
        "kind"              => "k",
        "roles"             => "r",
        "documentation"     => "doc",
        "members"           => "m",
        "signature"         => "sig",
        "packageReferences" => "pr",
        "frameworkReferences" => "fr",
        "description"       => "desc",
        "tags"              => "tg",
        "targetFrameworks"  => "tfm",
        "version"           => "ver",
        _                   => key
    };
}
```

- [ ] **Run tests to verify they pass**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "CompactJsonFormatterTests" -v n
```
Expected: All 8 tests PASS.

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/Formats/CompactJsonFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/CompactJsonFormatterTests.cs
git commit -m "feat: add CompactJsonFormatter with short key mapping"
```

---

## Task 6: Build UltraCompactFormatter

**Files:**
- Create: `src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterTests.cs`

The ultra-compact format grammar (from spec):
```
#NuSpec.AI/v1 {PackageId} {Version}
#desc {Description}          (omitted if no description)
#tfm {TFM1};{TFM2}
#dep {PackageRef1};{PackageRef2}   (omitted if empty)
#fref {FrameworkRef1}              (omitted if empty)
@c {TypeName} [{role1},{role2}] "{documentation}"
 .p {Name}:{Type} "{documentation}"
 .m {Name}:{ReturnType}({params}) "{documentation}"
 .ctor ({params}) "{documentation}"
 .f {Name}:{Type} "{documentation}"
 .ev {Name}={Value},{Name}={Value}
```
Type prefixes: `@c` class, `@i` interface, `@e` enum, `@s` struct, `@r` record, `@rs` record-struct.
Roles `[...]` omitted when empty. Documentation `"..."` omitted when absent.

- [ ] **Write the failing tests**

`tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterTests.cs`:
```csharp
using NuSpec.AI.Tool.Formats;

namespace NuSpec.AI.Tool.Tests.Formats;

public class UltraCompactFormatterTests : FormatterTestBase
{
    private readonly UltraCompactFormatter _formatter = new();

    [Fact]
    public void FormatId_IsUltra()
    {
        Assert.Equal("ultra", _formatter.FormatId);
    }

    [Fact]
    public void FileName_IsPackageMapUltra()
    {
        Assert.Equal("package-map.ultra", _formatter.FileName);
    }

    [Fact]
    public void Serialize_StartsWithHeader()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.StartsWith("#NuSpec.AI/v1 Acme.Orders 1.0.0", result);
    }

    [Fact]
    public void Serialize_IncludesDescription()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#desc Order management library.", result);
    }

    [Fact]
    public void Serialize_IncludesTargetFramework()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#tfm net8.0", result);
    }

    [Fact]
    public void Serialize_IncludesDependency()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("#dep Microsoft.EntityFrameworkCore", result);
    }

    [Fact]
    public void Serialize_ClassType_UsesAtC()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@c Order [entity]", result);
    }

    [Fact]
    public void Serialize_InterfaceType_UsesAtI()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@i IOrderRepository [repository]", result);
    }

    [Fact]
    public void Serialize_EnumType_UsesAtE()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@e OrderStatus", result);
    }

    [Fact]
    public void Serialize_EnumValues_OnOneLine()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(".ev Pending=0,Confirmed=1", result);
    }

    [Fact]
    public void Serialize_Properties_UseDotP()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(" .p Id:int", result);
    }

    [Fact]
    public void Serialize_Methods_UseDotM()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains(" .m GetByIdAsync:", result);
    }

    [Fact]
    public void Serialize_TypeDocumentation_Included()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("@c Order [entity] \"Represents a customer order.\"", result);
    }

    [Fact]
    public void Serialize_MemberDocumentation_Included()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        Assert.Contains("\"Gets an order by ID.\"", result);
    }

    [Fact]
    public void Serialize_EmptyRoles_OmitsBrackets()
    {
        var map = BuildSamplePackageMap();
        var result = _formatter.Serialize(map);
        // OrderStatus has no roles
        Assert.Contains("@e OrderStatus\n", result);
        Assert.DoesNotContain("@e OrderStatus []", result);
    }

    [Fact]
    public void Serialize_IsSmallerThanJson()
    {
        var map = BuildSamplePackageMap();
        var json = new JsonFormatter().Serialize(map);
        var ultra = _formatter.Serialize(map);
        Assert.True(ultra.Length < json.Length,
            $"Ultra ({ultra.Length}) should be smaller than JSON ({json.Length})");
    }
}
```

- [ ] **Run to verify they fail**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "UltraCompactFormatterTests" -v n
```
Expected: FAIL.

- [ ] **Create UltraCompactFormatter.cs**

`src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs`:
```csharp
using System.Text;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Formats;

public sealed class UltraCompactFormatter : IFormatter
{
    public string FormatId => "ultra";
    public string FileName => "package-map.ultra";

    public string Serialize(PackageMap packageMap)
    {
        var sb = new StringBuilder();
        var pkg = packageMap.Package;
        var deps = packageMap.Dependencies;
        var surface = packageMap.PublicSurface;

        // Header
        sb.AppendLine($"#NuSpec.AI/v1 {pkg.Id} {pkg.Version}");
        if (!string.IsNullOrWhiteSpace(pkg.Description))
            sb.AppendLine($"#desc {pkg.Description}");
        if (pkg.TargetFrameworks.Count > 0)
            sb.AppendLine($"#tfm {string.Join(";", pkg.TargetFrameworks)}");
        if (deps.PackageReferences.Count > 0)
            sb.AppendLine($"#dep {string.Join(";", deps.PackageReferences)}");
        if (deps.FrameworkReferences.Count > 0)
            sb.AppendLine($"#fref {string.Join(";", deps.FrameworkReferences)}");

        // Types
        foreach (var type in surface.Types)
        {
            var prefix = type.Kind switch
            {
                "class"         => "@c",
                "interface"     => "@i",
                "enum"          => "@e",
                "struct"        => "@s",
                "record"        => "@r",
                "record-struct" => "@rs",
                _               => "@c"
            };

            var roles = type.Roles.Count > 0
                ? $" [{string.Join(",", type.Roles)}]"
                : "";

            var doc = !string.IsNullOrWhiteSpace(type.Documentation)
                ? $" \"{type.Documentation}\""
                : "";

            sb.AppendLine($"{prefix} {type.Name}{roles}{doc}");

            if (type.Kind == "enum")
            {
                var enumValues = string.Join(",",
                    type.Members.Select(m => m.Signature));
                sb.AppendLine($" .ev {enumValues}");
            }
            else
            {
                foreach (var member in type.Members)
                {
                    var memberDoc = !string.IsNullOrWhiteSpace(member.Documentation)
                        ? $" \"{member.Documentation}\""
                        : "";

                    var line = member.Kind switch
                    {
                        "property"    => $" .p {FormatPropertySignature(member.Signature)}{memberDoc}",
                        "method"      => $" .m {FormatMethodSignature(member.Signature)}{memberDoc}",
                        "constructor" => $" .ctor {FormatCtorSignature(member.Signature)}{memberDoc}",
                        "field"       => $" .f {FormatFieldSignature(member.Signature)}{memberDoc}",
                        _             => null
                    };

                    if (line is not null)
                        sb.AppendLine(line);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    // "public int Id { get; set; }" → "Id:int"
    private static string FormatPropertySignature(string sig)
    {
        // Remove accessor block
        var withoutAccessors = System.Text.RegularExpressions.Regex
            .Replace(sig, @"\s*\{[^}]*\}", "").Trim();
        // Split on space to get modifiers, type, name
        var parts = withoutAccessors.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var name = parts[^1];
            var type = parts[^2];
            return $"{name}:{type}";
        }
        return sig;
    }

    // "public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)" → "GetByIdAsync:Task<Order?>(int id, CancellationToken ct = default)"
    private static string FormatMethodSignature(string sig)
    {
        // Remove access modifiers and find return type + name(params)
        var noMods = System.Text.RegularExpressions.Regex
            .Replace(sig, @"^(public|private|protected|internal|static|virtual|override|abstract|sealed|async|\s)+", "").Trim();
        // Find first '(' to locate name boundary
        var parenIdx = noMods.IndexOf('(');
        if (parenIdx < 0) return sig;
        var beforeParen = noMods[..parenIdx].Trim();
        var paramsAndRest = noMods[parenIdx..];
        // Last word before paren is name, everything before is return type
        var lastSpace = beforeParen.LastIndexOf(' ');
        if (lastSpace < 0) return sig;
        var returnType = beforeParen[..lastSpace].Trim();
        var name = beforeParen[(lastSpace + 1)..];
        return $"{name}:{returnType}{paramsAndRest}";
    }

    // "public MyClass(string foo, int bar)" → "(string foo, int bar)"
    private static string FormatCtorSignature(string sig)
    {
        var parenIdx = sig.IndexOf('(');
        return parenIdx >= 0 ? sig[parenIdx..] : sig;
    }

    // "public static readonly int MaxValue" → "MaxValue:int"
    private static string FormatFieldSignature(string sig)
    {
        var parts = sig.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[^1]}:{parts[^2]}";
        return sig;
    }
}
```

- [ ] **Run tests to verify they pass**
```bash
dotnet test tests/NuSpec.AI.Tool.Tests/NuSpec.AI.Tool.Tests.csproj --filter "UltraCompactFormatterTests" -v n
```
Expected: All 15 tests PASS.

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs tests/NuSpec.AI.Tool.Tests/Formats/UltraCompactFormatterTests.cs
git commit -m "feat: add UltraCompactFormatter with custom terse text format"
```

---

## Task 7: Add FormatterRegistry and wire --formats into CLI

**Files:**
- Create: `src/NuSpec.AI.Tool/Formats/FormatterRegistry.cs`
- Modify: `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`
- Modify: `src/NuSpec.AI.Tool/Program.cs`

- [ ] **Create FormatterRegistry.cs**

`src/NuSpec.AI.Tool/Formats/FormatterRegistry.cs`:
```csharp
namespace NuSpec.AI.Tool.Formats;

public static class FormatterRegistry
{
    private static readonly Dictionary<string, IFormatter> Formatters = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["json"]    = new JsonFormatter(),
        ["yaml"]    = new YamlFormatter(),
        ["compact"] = new CompactJsonFormatter(),
        ["ultra"]   = new UltraCompactFormatter()
    };

    /// <summary>
    /// Parses a semicolon-separated format string (e.g., "json;yaml;ultra").
    /// "all" expands to all four formats. Empty/null returns json only.
    /// </summary>
    public static IReadOnlyList<IFormatter> Resolve(string? formatsArg)
    {
        if (string.IsNullOrWhiteSpace(formatsArg))
            return [Formatters["json"]];

        if (formatsArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            return Formatters.Values.ToList();

        var result = new List<IFormatter>();
        foreach (var id in formatsArg.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = id.Trim();
            if (Formatters.TryGetValue(trimmed, out var formatter))
                result.Add(formatter);
            else
                throw new ArgumentException($"Unknown format: '{trimmed}'. Valid: json, yaml, compact, ultra, all");
        }

        return result.Count > 0 ? result : [Formatters["json"]];
    }
}
```

- [ ] **Update ProjectAnalyzer to accept formatters and write multiple files**

Replace the contents of `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs` with:
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuSpec.AI.Tool.Formats;
using NuSpec.AI.Tool.Models;
using NuSpec.AI.Tool.ProjectMetadata;

namespace NuSpec.AI.Tool.Analysis;

public static class ProjectAnalyzer
{
    public static PackageMap Analyze(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(csprojPath))
            ?? throw new InvalidOperationException($"Cannot determine directory for: {csprojPath}");

        var packageInfo = CsprojReader.ReadPackageInfo(csprojPath);
        var dependencies = CsprojReader.ReadDependencies(csprojPath);
        var compilation = BuildCompilation(projectDir, packageInfo.Id);
        var publicSurface = ApiSurfaceCollector.Collect(compilation);

        return new PackageMap
        {
            Package = packageInfo,
            Dependencies = dependencies,
            PublicSurface = publicSurface
        };
    }

    public static void WriteFormats(
        PackageMap packageMap,
        IReadOnlyList<IFormatter> formatters,
        string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        foreach (var formatter in formatters)
        {
            var path = Path.Combine(outputDir, formatter.FileName);
            File.WriteAllText(path, formatter.Serialize(packageMap));
            Console.Error.WriteLine($"Package map written to: {path}");
        }
    }

    private static CSharpCompilation BuildCompilation(string projectDir, string assemblyName)
    {
        var sourceFiles = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relativePath = Path.GetRelativePath(projectDir, f);
                return !relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase);
            });

        var syntaxTrees = sourceFiles
            .AsParallel()
            .Select(file =>
            {
                var text = File.ReadAllText(file);
                return CSharpSyntaxTree.ParseText(text, path: file,
                    options: new CSharpParseOptions(LanguageVersion.Latest));
            })
            .ToList();

        var references = GetCoreMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> GetCoreMetadataReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(trustedAssemblies))
            return [];

        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is
                "System.Runtime.dll" or
                "System.Collections.dll" or
                "System.Linq.dll" or
                "System.Threading.Tasks.dll" or
                "System.ComponentModel.dll" or
                "System.ComponentModel.Annotations.dll" or
                "netstandard.dll" or
                "mscorlib.dll" or
                "System.Private.CoreLib.dll")
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
```

- [ ] **Update Program.cs to parse --formats and --output-dir**

Replace the contents of `src/NuSpec.AI.Tool/Program.cs` with:
```csharp
using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Formats;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: NuSpec.AI.Tool <project-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  project-file         Path to the .csproj file to analyze");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <path>      Output file path for single format (default: stdout)");
    Console.WriteLine("  --output-dir <dir>   Output directory for multiple formats");
    Console.WriteLine("  --formats <list>     Semicolon-separated formats: json, yaml, compact, ultra, all (default: json)");
    Console.WriteLine("  --help               Show help");
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "--version")
{
    var version = typeof(ProjectAnalyzer).Assembly.GetName().Version;
    Console.WriteLine($"NuSpec.AI.Tool {version}");
    return 0;
}

var projectFile = args[0];
string? outputPath = null;
string? outputDir = null;
string? formatsArg = null;

for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
        outputPath = args[++i];
    else if (args[i] == "--output-dir" && i + 1 < args.Length)
        outputDir = args[++i];
    else if (args[i] == "--formats" && i + 1 < args.Length)
        formatsArg = args[++i];
}

if (!File.Exists(projectFile))
{
    Console.Error.WriteLine($"Error: Project file not found: {projectFile}");
    return 1;
}

try
{
    var packageMap = ProjectAnalyzer.Analyze(projectFile);
    var formatters = FormatterRegistry.Resolve(formatsArg);

    if (outputDir is not null)
    {
        // Multiple formats → write each to outputDir/{filename}
        ProjectAnalyzer.WriteFormats(packageMap, formatters, outputDir);
    }
    else if (outputPath is not null)
    {
        // Single file path → use first formatter
        var formatter = formatters[0];
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(outputPath, formatter.Serialize(packageMap));
        Console.Error.WriteLine($"Package map written to: {outputPath}");
    }
    else
    {
        // Stdout → first formatter
        Console.WriteLine(formatters[0].Serialize(packageMap));
    }

    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
```

- [ ] **Build and run all tests**
```bash
dotnet build NuSpec.AI.slnx
dotnet test NuSpec.AI.slnx
```
Expected: Build succeeded, all tests pass.

- [ ] **Smoke test with multiple formats**
```bash
cd E:\repos\NuSpec.AI
dotnet run --project src/NuSpec.AI.Tool -- tests/SampleProject/SampleProject.csproj --output-dir /tmp/nuspecai-smoke --formats json;yaml;compact;ultra
ls /tmp/nuspecai-smoke/
```
Expected: `package-map.json  package-map.yaml  package-map.compact.json  package-map.ultra`

- [ ] **Commit**
```bash
git add src/NuSpec.AI.Tool/Formats/FormatterRegistry.cs src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs src/NuSpec.AI.Tool/Program.cs
git commit -m "feat: add FormatterRegistry, wire --formats into CLI"
```

---

## Task 8: Pack NuSpec.AI.Attributes and verify

**Files:**
- No new files — packing the existing Attributes project

- [ ] **Pack the Attributes package**
```bash
cd E:\repos\NuSpec.AI
dotnet pack src/NuSpec.AI.Attributes/NuSpec.AI.Attributes.csproj -c Release -o artifacts
```
Expected: `Successfully created package 'artifacts/NuSpec.AI.Attributes.1.0.0.nupkg'`

- [ ] **Verify package contents**
```bash
powershell -Command "Add-Type -A 'System.IO.Compression.FileSystem'; [IO.Compression.ZipFile]::OpenRead('E:\repos\NuSpec.AI\artifacts\NuSpec.AI.Attributes.1.0.0.nupkg').Entries | ForEach-Object { $_.FullName }"
```
Expected output to include:
```
lib/netstandard2.0/NuSpec.AI.Attributes.dll
```

- [ ] **Run all tests one final time**
```bash
dotnet test NuSpec.AI.slnx
```
Expected: All tests pass.

- [ ] **Final commit**
```bash
git add artifacts/NuSpec.AI.Attributes.1.0.0.nupkg
git commit -m "chore: pack NuSpec.AI.Attributes 1.0.0"
```

---

## Self-Review

**Spec coverage:**
- ✅ NuSpec.AI.Attributes package with `[AiRole]`, `[AiIgnore]`, `[AiDescription]`
- ✅ YAML formatter (~30-40% token savings)
- ✅ Compact JSON formatter (~40-50% token savings, short keys)
- ✅ Ultra-compact formatter (~70-80% token savings)
- ✅ `<NuSpecAiFormats>` with `all` shorthand and `json` default
- ✅ Multiple files written to output directory
- ⏭️ Attribute reading in ApiSurfaceCollector — deferred to Plan B (requires Pro license gate)
- ⏭️ Licensing — Plan B
- ⏭️ NuSpec.AI.Pro packaging — Plan B

**Placeholder scan:** No TBDs. All code blocks are complete.

**Type consistency:** `PackageMap`, `PackageInfo`, `DependencyInfo`, `PublicSurfaceInfo`, `TypeInfo`, `MemberInfo` used consistently throughout. `IFormatter` interface implemented by all four formatters. `FormatterRegistry.Resolve()` returns `IReadOnlyList<IFormatter>` consumed by `ProjectAnalyzer.WriteFormats()`.
