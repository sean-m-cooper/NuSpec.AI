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

| Format | File | Avg. Token Savings |
|--------|------|--------------------|
| `json` | `ai/package-map.json` | Baseline (same as free) |
| `yaml` | `ai/package-map.yaml` | **29%** fewer tokens |
| `compact` | `ai/package-map.compact.json` | **40%** fewer tokens |
| `ultra` | `ai/package-map.ultra` | **71%** fewer tokens |

Use `<NuSpecAiFormats>all</NuSpecAiFormats>` to generate all four. Mix and match as needed — `json;yaml`, `ultra` only, etc.

### Token count comparison

Measured across 42 production projects (3 real codebases). Token counts are approximate (chars ÷ 4).

| Project profile | Types | JSON | YAML | Compact | Ultra |
|----------------|------:|-----:|-----:|--------:|------:|
| Models library | 723 | 215,221 | 140,003 (−35%) | 110,848 (−48%) | **32,154 (−85%)** |
| Large shared library | 673 | 225,503 | 149,950 (−34%) | 122,876 (−46%) | **44,468 (−80%)** |
| Worker service | 131 | 45,684 | 32,490 (−29%) | 28,652 (−37%) | **13,866 (−70%)** |
| Providers library | 116 | 73,632 | 53,987 (−27%) | 49,229 (−33%) | **28,364 (−61%)** |
| DAL / repositories | 73 | 30,746 | 19,887 (−35%) | 16,969 (−45%) | **6,795 (−78%)** |
| Common services | 44 | 30,571 | 23,088 (−24%) | 21,122 (−31%) | **13,076 (−57%)** |
| Web API surface | 50 | 15,163 | 10,228 (−33%) | 8,447 (−44%) | **3,283 (−78%)** |
| Azure Functions | 16 | 6,677 | 4,926 (−26%) | 4,540 (−32%) | **2,367 (−65%)** |
| Exception types | 8 | 3,504 | 2,433 (−31%) | 2,009 (−43%) | **679 (−81%)** |
| **All 42 projects** | **—** | **789,131** | **536,463 (−32%)** | **449,049 (−43%)** | **184,630 (−77%)** |

**What drives savings:** `ultra` encodes the same data in a custom terse text format — no JSON structure overhead, abbreviated type prefixes, member prefixes instead of full keys. The gains compound with project size: larger APIs have more repeated structural tokens to eliminate.

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
