# NuSpec.AI.Pro

**Token-optimized AI context for your NuGet packages — with offline license validation.**

NuSpec.AI.Pro is the paid tier of NuSpec.AI. It generates `ai/package-map.json` during `dotnet pack` (same as the free version) plus three additional token-optimized formats: YAML, compact JSON, and ultra-compact text. Pro also supports `[AiRole]`, `[AiIgnore]`, and `[AiDescription]` attributes for fine-grained control over what gets included and how it's described.

> **Free version:** If you don't need token optimization or attribute support, [NuSpec.AI](https://www.nuget.org/packages/NuSpec.AI) is free and generates standard JSON automatically.

## Quick Start

```xml
<PackageReference Include="NuSpec.AI.Pro" Version="1.0.2" PrivateAssets="all" />

<!-- Optional: attribute support -->
<PackageReference Include="NuSpec.AI.Attributes" Version="1.0.2" />
```

```xml
<PropertyGroup>
  <!-- Choose your formats (default: json) -->
  <NuSpecAiFormats>yaml;ultra</NuSpecAiFormats>

  <!-- License key (or use NUSPEC_AI_LICENSE_KEY env var) -->
  <NuSpecAiLicenseKey>$(NUSPEC_AI_LICENSE_KEY)</NuSpecAiLicenseKey>
</PropertyGroup>
```

```bash
dotnet pack
```

## Output Formats

| Format | File | Token Savings |
|--------|------|--------------|
| `json` | `ai/package-map.json` | Baseline (same as free) |
| `yaml` | `ai/package-map.yaml` | ~30–40% fewer tokens |
| `compact` | `ai/package-map.compact.json` | ~40–50% fewer tokens |
| `ultra` | `ai/package-map.ultra` | ~70–80% fewer tokens |

Use `<NuSpecAiFormats>all</NuSpecAiFormats>` to generate all four. Mix and match as needed — `json;yaml`, `ultra` only, etc.

### Ultra-compact example

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
 .m GetByIdAsync:Task<Order?>(int id, CancellationToken ct = default)
 .m AddAsync:Task<Order>(Order order, CancellationToken ct = default)
```

## License Key

Set your license key in one of three ways (highest to lowest priority):

1. **MSBuild property** (recommended for CI/CD):
   ```xml
   <NuSpecAiLicenseKey>$(NUSPEC_AI_LICENSE_KEY)</NuSpecAiLicenseKey>
   ```
   Set `NUSPEC_AI_LICENSE_KEY` as a secret in your CI environment.

2. **Environment variable:**
   ```bash
   export NUSPEC_AI_LICENSE_KEY=<your-key>
   ```

3. **User file:** `~/.nuspec-ai/license.key`

License validation is **entirely offline** — no network calls during pack.

### Missing or invalid license

If the license is missing, expired, or doesn't cover the current package, NuSpec.AI.Pro falls back to standard JSON output and emits a build warning:

```
warning NSPECAI001: NuSpec.AI.Pro license is expired/invalid/missing. Falling back to standard JSON output.
```

The build and pack always succeed — your CI/CD pipeline is never blocked.

## Attribute Support

Install [NuSpec.AI.Attributes](https://www.nuget.org/packages/NuSpec.AI.Attributes) to use `[AiRole]`, `[AiIgnore]`, and `[AiDescription]` on your types and members. These are honored by Pro and ignored by the free version.

```csharp
[AiRole("aggregate-root", "audited")]
public class Order { ... }

[AiIgnore]
public string InternalToken { get; set; }

[AiDescription("Use this to initiate a refund. Do not call for subscription orders.")]
public Task RefundAsync(int orderId) { ... }
```

## Coexistence with NuSpec.AI (free)

If both packages are referenced, Pro wins automatically. The free package sits inert. You'll see a low-priority diagnostic message in verbose build output suggesting you remove the redundant reference.

## Requirements

- **.NET 8.0 SDK** or later
- A valid NuSpec.AI.Pro license key
- Project must be packable (`<IsPackable>true</IsPackable>` or default)

## Links

- [NuSpec.AI.Attributes](https://www.nuget.org/packages/NuSpec.AI.Attributes) — free attribute package
- [NuSpec.AI](https://www.nuget.org/packages/NuSpec.AI) — free tier
- [Source code and issues](https://github.com/sean-m-cooper/NuSpec.AI)
- [License (MIT)](https://github.com/sean-m-cooper/NuSpec.AI/blob/main/LICENSE)
