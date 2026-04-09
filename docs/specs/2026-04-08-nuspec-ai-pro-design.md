# NuSpec.AI Pro — Design Specification

## Context

NuSpec.AI (free) generates `ai/package-map.json` during `dotnet pack`, giving AI coding assistants structured metadata about a package's public API surface. This spec defines the monetization layer: a paid Pro package with token-optimized output formats, attribute-based control, and offline licensing.

**Target buyer:** Enterprise teams and library authors with internal NuGet feeds and many packages. Token costs are a real line item for enterprises using AI assistants, and individual developers on usage-capped plans also feel the pressure.

## Package Architecture

Three NuGet packages:

| Package | Price | Purpose |
|---------|-------|---------|
| **NuSpec.AI** | Free | Standard JSON output, basic role inference, doc comments. Unchanged from today. |
| **NuSpec.AI.Pro** | Paid | Token-optimized formats (YAML, compact JSON, ultra-compact), attribute support, offline license validation. |
| **NuSpec.AI.Attributes** | Free | `[AiRole]`, `[AiIgnore]`, `[AiDescription]` attribute classes. Tiny runtime dependency. Only honored by Pro. |

### Consumer Installation

```xml
<!-- Free tier (existing) -->
<PackageReference Include="NuSpec.AI" PrivateAssets="all" />

<!-- Pro tier -->
<PackageReference Include="NuSpec.AI.Pro" PrivateAssets="all" />
<PackageReference Include="NuSpec.AI.Attributes" />  <!-- optional, for attribute support -->
```

### Package Interaction

- When NuSpec.AI.Pro is installed, its `.props` set `NuSpecAiEnabled=false` to disable the free version's pack hook. Pro takes over completely.
- **Both installed (free + Pro):** Pro wins gracefully. No error, no warning. The free package sits inert. Pro emits a low-priority MSBuild message (visible only in verbose/diagnostic output): `NuSpec.AI.Pro is active. The NuSpec.AI package reference can be removed.`
- NuSpec.AI.Pro can be installed without NuSpec.AI — it is fully standalone.
- NuSpec.AI.Attributes can be installed without either analyzer package — it's just attribute definitions.
- If NuSpec.AI.Attributes is present but only the free NuSpec.AI is installed, the attributes are ignored (compiled into the assembly but not read during analysis).

## NuSpec.AI.Attributes Package

A zero-dependency class library targeting `netstandard2.0` for maximum compatibility.

### Attribute Definitions

```csharp
namespace NuSpec.AI;

/// <summary>
/// Specifies one or more semantic roles for a type, overriding automatic inference.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false)]
public sealed class AiRoleAttribute : Attribute
{
    public AiRoleAttribute(params string[] roles) => Roles = roles;
    public string[] Roles { get; }
}

/// <summary>
/// Excludes the target type or member from the generated package map.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum |
                AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Constructor | AttributeTargets.Field, AllowMultiple = false)]
public sealed class AiIgnoreAttribute : Attribute { }

/// <summary>
/// Provides a description for AI context, overriding or supplementing XML doc comments.
/// Useful when the AI-facing description should differ from IntelliSense documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum |
                AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Constructor | AttributeTargets.Field, AllowMultiple = false)]
public sealed class AiDescriptionAttribute : Attribute
{
    public AiDescriptionAttribute(string description) => Description = description;
    public string Description { get; }
}
```

### Attribute Behavior (Pro Only)

| Attribute | Effect |
|-----------|--------|
| `[AiRole("aggregate-root", "audited")]` | Replaces inferred roles with the specified roles. |
| `[AiIgnore]` | Type or member is excluded from all output formats entirely. |
| `[AiDescription("Use this to...")]` | Replaces the `documentation` field. If both `[AiDescription]` and XML `<summary>` exist, the attribute wins. |

## Output Formats

### Format Selection

Controlled via `<NuSpecAiFormats>` MSBuild property (semicolon-separated list):

```xml
<!-- Defaults to json when empty/null -->
<NuSpecAiFormats>json</NuSpecAiFormats>

<!-- Multiple formats -->
<NuSpecAiFormats>json;yaml;ultra</NuSpecAiFormats>

<!-- All four formats -->
<NuSpecAiFormats>all</NuSpecAiFormats>
```

**Defaults:**
- **Free (NuSpec.AI):** Always `json`. Not configurable.
- **Pro (NuSpec.AI.Pro):** Defaults to `json` when `<NuSpecAiFormats>` is empty or unset. `all` expands to all four formats.

Pro users have full control — if they only want `ultra`, they get only `ultra`. No forced formats.

### Format Specifications

#### Standard JSON (`json`)

File: `ai/package-map.json`

Same format as the free version. Human-readable, verbose. Baseline compatibility — any AI tool that reads NuSpec.AI output can read this.

#### YAML (`yaml`)

File: `ai/package-map.yaml`

Same data structure as JSON, serialized as YAML. Eliminates braces, brackets, and key quoting. Expected savings: ~30-40% fewer tokens than standard JSON.

```yaml
schemaVersion: 1
package:
  id: Acme.Orders
  version: 1.0.0
  description: Order management library.
  tags: [orders]
  targetFrameworks: [net8.0]
dependencies:
  packageReferences: [Microsoft.EntityFrameworkCore]
  frameworkReferences: []
publicSurface:
  namespaces: [Acme.Orders]
  types:
    - name: Order
      fullName: Acme.Orders.Order
      namespace: Acme.Orders
      kind: class
      roles: [entity]
      documentation: Represents a customer order.
      members:
        - kind: property
          name: Id
          signature: "public int Id { get; set; }"
```

#### Compact JSON (`compact`)

File: `ai/package-map.compact.json`

JSON with abbreviated keys, no whitespace, no null fields. Expected savings: ~40-50% fewer tokens than standard JSON.

Key mapping:
| Full Key | Compact Key |
|----------|-------------|
| `schemaVersion` | `v` |
| `package` | `p` |
| `dependencies` | `d` |
| `publicSurface` | `s` |
| `namespaces` | `nss` |
| `types` | `t` |
| `name` | `n` |
| `fullName` | `fn` |
| `namespace` | `ns` |
| `kind` | `k` |
| `roles` | `r` |
| `documentation` | `doc` |
| `members` | `m` |
| `signature` | `sig` |
| `packageReferences` | `pr` |
| `frameworkReferences` | `fr` |
| `description` | `desc` |
| `tags` | `tg` |
| `targetFrameworks` | `tfm` |
| `id` | `id` |
| `version` | `ver` |

Example: `{"v":1,"p":{"id":"Acme.Orders","ver":"1.0.0"},"s":{"t":[{"n":"Order","k":"class","r":["entity"],"m":[{"k":"property","n":"Id","sig":"public int Id { get; set; }"}]}]}}`

#### Ultra-Compact Text (`ultra`)

File: `ai/package-map.ultra`

Custom terse text format optimized for minimal token count. Expected savings: ~70-80% fewer tokens than standard JSON.

**Format grammar:**

```
#NuSpec.AI/v1 {PackageId} {Version}
#desc {Description}
#tfm {TFM1};{TFM2}
#dep {PackageRef1};{PackageRef2}
#fref {FrameworkRef1};{FrameworkRef2}
@{kind} {TypeName} [{role1},{role2}] "{documentation}"
 .p {Name}:{Type} "{documentation}"
 .m {Name}:{ReturnType}({params}) "{documentation}"
 .ctor ({params}) "{documentation}"
 .f {Name}:{Type} "{documentation}"
 .ev {Name}={Value},{Name}={Value}
```

**Type prefixes:** `@c` class, `@i` interface, `@e` enum, `@s` struct, `@r` record, `@rs` record struct

**Member prefixes:** `.p` property, `.m` method, `.ctor` constructor, `.f` field, `.ev` enum values (all on one line, comma-separated)

**Rules:**
- Roles section `[...]` omitted when empty
- Documentation `"..."` omitted when absent
- Enum values are a single `.ev` line per enum, not one line per value
- Indentation is single space for members
- Signatures in `.m` use compact form: `Name:ReturnType(ParamType paramName, ...)`
- Type in `.p` is the short type name (e.g., `int`, `string`, `Order?`)

**Full example:**

```
#NuSpec.AI/v1 Acme.Orders 1.0.0
#desc Order management library.
#tfm net8.0
#dep Microsoft.EntityFrameworkCore
@c Order [entity] "Represents a customer order."
 .p Id:int
 .p CustomerId:string
 .p Status:OrderStatus
@e OrderStatus
 .ev Pending=0,Confirmed=1,Shipped=2,Cancelled=3
@i IOrderRepository [repository]
 .m GetByIdAsync:Task<Order?>(int id, CancellationToken ct = default) "Gets an order by its unique identifier."
 .m AddAsync:Task<Order>(Order order, CancellationToken ct = default)
@c OrderRepository [repository]
 .ctor (IDbContext context)
 .m GetByIdAsync:Task<Order?>(int id, CancellationToken ct = default)
 .m AddAsync:Task<Order>(Order order, CancellationToken ct = default)
```

## Licensing

### License Key Format

Signed JWT (RS256) containing:

```json
{
  "sub": "customer-id-or-email",
  "iat": 1712534400,
  "exp": 1744070400,
  "scope": "pro",
  "packages": "*"
}
```

| Field | Description |
|-------|-------------|
| `sub` | Customer identifier (email or ID) |
| `iat` | Issued-at timestamp |
| `exp` | Expiration timestamp |
| `scope` | License tier: `pro` |
| `packages` | Glob pattern for allowed package IDs: `*` (unlimited), `Acme.*` (org-scoped), `Acme.Orders` (single) |

### Key Validation

- The Pro package embeds an RS256 public key
- At pack time, the tool reads the license key, verifies the JWT signature, checks expiry, and validates the current package ID against the `packages` pattern
- No network calls at any point

### Key Configuration (precedence order)

1. `<NuSpecAiLicenseKey>` MSBuild property (typically set via CI/CD variable)
2. `NUSPEC_AI_LICENSE_KEY` environment variable
3. `~/.nuspec-ai/license.key` file (user-level default)

### Behavior on Invalid/Expired/Missing License

- Falls back to free behavior: standard JSON output only, no attribute support
- Emits MSBuild warning: `NSPECAI001: NuSpec.AI.Pro license is expired/invalid/missing. Falling back to standard JSON output.`
- Build and pack do NOT fail — never block a customer's CI/CD pipeline

## Solution Structure (additions to existing repo)

```
NuSpec.AI/
├── src/
│   ├── NuSpec.AI.Tool/              # Existing CLI tool (updated)
│   │   ├── Analysis/
│   │   │   ├── ApiSurfaceCollector.cs   # Updated: attribute support
│   │   │   └── ProjectAnalyzer.cs       # Updated: format selection
│   │   ├── Formats/                     # NEW
│   │   │   ├── JsonFormatter.cs         # Standard JSON (existing logic extracted)
│   │   │   ├── YamlFormatter.cs         # YAML output
│   │   │   ├── CompactJsonFormatter.cs  # Short-key JSON
│   │   │   └── UltraCompactFormatter.cs # Custom terse text format
│   │   ├── Licensing/                   # NEW
│   │   │   ├── LicenseValidator.cs      # JWT validation with embedded public key
│   │   │   └── LicenseInfo.cs           # Parsed license data model
│   │   └── ...
│   ├── NuSpec.AI/                   # Existing packaging project (unchanged)
│   ├── NuSpec.AI.Pro/               # NEW: Pro packaging project
│   │   ├── NuSpec.AI.Pro.csproj
│   │   └── build/
│   │       ├── NuSpec.AI.Pro.props
│   │       └── NuSpec.AI.Pro.targets
│   └── NuSpec.AI.Attributes/       # NEW: Attributes package
│       ├── NuSpec.AI.Attributes.csproj
│       ├── AiRoleAttribute.cs
│       ├── AiIgnoreAttribute.cs
│       └── AiDescriptionAttribute.cs
└── tests/
    ├── NuSpec.AI.Tool.Tests/        # Updated with Pro feature tests
    └── ...
```

### Shared Tool Binary

Both NuSpec.AI and NuSpec.AI.Pro ship the same CLI tool binary. The tool detects which features to enable based on:
1. Whether a valid Pro license is present
2. Which MSBuild properties are set (format, license key)
3. Whether attribute types are resolvable in the compilation

This avoids maintaining two separate tool codebases.

## MSBuild Integration (Pro)

### NuSpec.AI.Pro.props

```xml
<Project>
  <PropertyGroup>
    <!-- Disable free version when Pro is installed -->
    <NuSpecAiEnabled>false</NuSpecAiEnabled>
    <!-- Pro defaults -->
    <NuSpecAiProEnabled Condition="'$(NuSpecAiProEnabled)' == ''">true</NuSpecAiProEnabled>
    <NuSpecAiFormats Condition="'$(NuSpecAiFormats)' == ''">json</NuSpecAiFormats>
  </PropertyGroup>
</Project>
```

### NuSpec.AI.Pro.targets

Same `TargetsForTfmSpecificContentInPackage` pattern as the free version. The CLI tool receives additional arguments:

```
dotnet NuSpec.AI.Tool.dll <project> --output <dir> --formats "json;yaml;ultra" --license <key>
```

The tool generates one file per requested format. The .targets file adds each generated file to `TfmSpecificPackageFile` with the appropriate `PackagePath` (`ai/package-map.json`, `ai/package-map.yaml`, etc.).

## Future Items (out of scope for Pro v1)

- **Key generation tooling** — CLI tool or web app to issue license keys
- **Payment/storefront** — Stripe integration, license delivery, customer management
- **Cross-package relationship mapping** — Solution-level manifests for enterprise package ecosystems
- **Custom role taxonomies** — Enterprise-defined role vocabularies
- **Usage examples extraction** — `<example>` tag and `/ai/examples/` folder support
- **Change detection / diff mode** — Version-to-version API diff
- **Multi-framework aware output** — Per-TFM type availability
- **AI prompt templates** — `ai/system-prompt.md` in the package
