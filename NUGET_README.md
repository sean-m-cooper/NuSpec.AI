# NuSpec.AI

**An AI assistant working with your package shouldn't have to guess.**

When your library ships as a `.nupkg`, the AI helping a downstream developer can't read your source. It sees an opaque assembly reference — type names if it's lucky, nothing else. NuSpec.AI fixes that. During `dotnet pack` it analyzes your public API surface with Roslyn and writes a structured map (`ai/package-map.json`) into the `.nupkg`. AI tools read it and instantly know your types, signatures, doc comments, and inferred semantic roles.

## Quick Start

Add the package — that's the whole setup:

```xml
<PackageReference Include="NuSpec.AI" Version="3.1.0" PrivateAssets="all" />
```

Then pack:

```bash
dotnet pack
```

Your `.nupkg` now contains `ai/package-map.json`. NuSpec.AI is a development dependency — it runs at pack time and never ships into your consumer's bin folder.

## What Changes for the AI

Given this code in your package:

```csharp
namespace Acme.Orders;

/// <summary>Represents a customer order.</summary>
public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; }
    public OrderStatus Status { get; set; }
}

public interface IOrderRepository
{
    /// <summary>Gets an order by its unique identifier.</summary>
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
}
```

**Without NuSpec.AI**, an AI helping a consumer of your package sees `Acme.Orders.Order` as a referenced symbol with no structure. It guesses from the name. It doesn't know `Order` has an `Id`, a `CustomerId`, or a `Status`. It doesn't know `IOrderRepository.GetByIdAsync` exists, returns `Task<Order?>`, or accepts a `CancellationToken`. There is no doc comment to read. The AI either asks the user to paste source or writes code against a name and a vibe.

**With NuSpec.AI**, the same AI reads `ai/package-map.json` from inside the `.nupkg` and gets:

```json
{
  "schemaVersion": 3,
  "package": {
    "id": "Acme.Orders",
    "version": "1.0.0",
    "targetFrameworks": ["net8.0"]
  },
  "publicSurface": {
    "types": [
      {
        "name": "Order",
        "kind": "class",
        "roles": ["entity"],
        "documentation": "Represents a customer order.",
        "members": [
          { "kind": "property", "name": "Id", "signature": "public int Id { get; set; }" },
          { "kind": "property", "name": "CustomerId", "signature": "public string CustomerId { get; set; }" },
          { "kind": "property", "name": "Status", "signature": "public OrderStatus Status { get; set; }" }
        ]
      },
      {
        "name": "IOrderRepository",
        "kind": "interface",
        "roles": ["repository"],
        "members": [
          {
            "kind": "method",
            "name": "GetByIdAsync",
            "signature": "Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)",
            "documentation": "Gets an order by its unique identifier."
          }
        ]
      }
    ]
  }
}
```

Every type, every public member, every signature, every doc comment, plus inferred semantic roles (`entity`, `repository`, `service`, `db-context`, etc.). The AI can call your API correctly on the first try.

## Output Formats

NuSpec.AI emits the same data in four formats. Pick whichever your AI tooling prefers — you can emit several at once.

| Format    | File                          | Sample size¹ | Notes |
|-----------|-------------------------------|--------------|-------|
| `json`    | `ai/package-map.json`         | 3,536 B      | Default. Standard JSON, easiest to inspect. |
| `yaml`    | `ai/package-map.yaml`         | 2,218 B (-37%) | Human-readable, less punctuation. |
| `compact` | `ai/package-map.compact.json` | 1,684 B (-52%) | Minified JSON. Same schema as `json`. |
| `ultra`   | `ai/package-map.ultra`        |   644 B (-82%) | Positional text format. Smallest token count. |

¹ Sizes from a small sample project (4 types, 10 members). Larger packages compress proportionally.

Configure via `<NuSpecAiFormats>` in your `.csproj`:

```xml
<PropertyGroup>
  <NuSpecAiFormats>json;ultra</NuSpecAiFormats>
</PropertyGroup>
```

Use `all` to emit every format.

## Transitive Map Discovery

NuSpec.AI 3.0 adds dependency awareness. Each emitted map records your package's direct dependencies along with their resolved versions and a flag for whether the dependency itself ships a NuSpec.AI map:

```json
"dependencies": {
  "packageReferences": [
    {
      "id": "Microsoft.EntityFrameworkCore",
      "version": "8.0.0",
      "hasNuSpecAiMap": false
    },
    {
      "id": "Acme.OrdersCore",
      "version": "1.2.0",
      "hasNuSpecAiMap": true
    }
  ]
}
```

When `hasNuSpecAiMap` is `true`, an AI tool can resolve the standard NuGet cache path and read that dependency's map too:

```
<NuGet packages root>/<id-lowercase>/<version>/ai/package-map.json
```

It can recurse the same way through that map's own `packageReferences`. The result: structured API knowledge propagates transitively through your dependency graph automatically — no extra work from you or your consumers.

## Context Window Impact

A consumer's AI doesn't have your source. Its only structural alternative to NuSpec.AI is to **decompile your DLL** — which produces full method bodies, async state machines, backing fields, and compiler-generated artifacts. Two real-world packages, packed locally with NuSpec.AI:

**Newtonsoft.Json** (127 public types, 1,198 members):

| Source for the AI                       | Bytes     | ≈ Tokens† | vs. decompilation |
|-----------------------------------------|----------:|----------:|------------------:|
| ILSpy-decompiled `Newtonsoft.Json.dll`  | 1,863,427 |  ~466,000 | —                 |
| `package-map.json`                      |   328,155 |   ~82,000 | **-82%**          |
| `package-map.json` with `--full-docs` (v3.1, opt-in) |   636,713 |  ~159,000 | **-66%**          |
| `package-map.yaml`                      |   239,209 |   ~60,000 | **-87%**          |
| `package-map.compact.json`              |   195,481 |   ~49,000 | **-90%**          |
| `package-map.ultra`                     |   106,418 |   ~27,000 | **-94%**          |

**Microsoft.EntityFrameworkCore 8.0.10** (a much larger surface):

| Source for the AI                              | Bytes     | ≈ Tokens† | vs. decompilation |
|------------------------------------------------|----------:|----------:|------------------:|
| ILSpy-decompiled `Microsoft.EntityFrameworkCore.dll` | 9,574,757 | ~2,394,000 | —                 |
| `package-map.json`                             | 4,262,664 | ~1,066,000 | **-55%**          |
| `package-map.yaml`                             | 3,535,139 |   ~884,000 | **-63%**          |
| `package-map.compact.json`                     | 3,244,493 |   ~811,000 | **-66%**          |
| `package-map.ultra`                            | 2,483,495 |   ~621,000 | **-74%**          |

† Bytes ÷ 4 is the standard rough estimate for English/code text in modern LLM tokenizers. The decompiled row is what actually lands in the AI's context window when it tries to inspect your package; the map rows are what NuSpec.AI provides instead.

The map's footprint inside the `.nupkg` itself is small — JSON gzips well inside the zip envelope. Newtonsoft.Json grew from 332 KB to 360 KB (+8.4%); EF Core from 1.19 MB to 2.09 MB (+76%, dominated by ~7 MB of XML documentation comments preserved in the map).

NuSpec.AI 3.1 adds `<NuSpecAiIncludeFullDocs>true</NuSpecAiIncludeFullDocs>` (or `--full-docs` on the CLI) to opt into capturing structured `<param>`, `<returns>`, `<remarks>`, `<example>`, and `<exception>` text on every type and member. The default `documentation` summary stays the same — turning the flag on roughly doubles the JSON map's size in exchange for a complete picture of the API contract. The `ultra` format ignores the flag by design.

And decompiled output omits two things the AI needs most: **XML doc comments** (those live in a sibling `.xml` file that isn't always shipped to nuget.org) and **inferred semantic roles** (a decompiler has no notion of "this is a repository" or "this is a `DbContext`"). NuSpec.AI bundles both into the map directly.

## Configuration

### Disable generation

```xml
<PropertyGroup>
  <NuSpecAiEnabled>false</NuSpecAiEnabled>
</PropertyGroup>
```

Or per-invocation: `dotnet pack -p:NuSpecAiEnabled=false`.

### Fine-grained control

Three opt-in attributes ship as `internal` source compiled into your assembly:

```csharp
[AiRole("aggregate-root")]   public class Order { }
[AiIgnore]                   internal class Helper { }
[AiDescription("Stream consumers must handle ordering.")]
public interface IEventStream { }
```

Full documentation including the role inference rules, every configuration option, and the complete schema reference lives in the [GitHub README](https://github.com/sean-m-cooper/NuSpec.AI#readme).

### Capture full XML doc comments

By default, NuSpec.AI extracts the `<summary>` element from each type/member's XML doc comment. To also capture `<param>`, `<typeparam>`, `<returns>`, `<remarks>`, `<example>`, and `<exception>`:

```xml
<PropertyGroup>
  <NuSpecAiIncludeFullDocs>true</NuSpecAiIncludeFullDocs>
</PropertyGroup>
```

This populates a structured `docs` object on each type and member. The `documentation` field continues to carry the bare summary string for back-compat. Note: the `ultra` format ignores this property — it's a positional minimum-tokens format and stays summaries-only by design.

Expect the map to grow ~1.6–2× when enabled, depending on how much `<param>` / `<remarks>` text your library has.

## Requirements

- **.NET 8.0 SDK or later** (build-time only — your package can target anything from netstandard2.0 onward)
- Project must be packable

## Links

- [GitHub repo, full docs, and issues](https://github.com/sean-m-cooper/NuSpec.AI)
- [License (MIT)](https://github.com/sean-m-cooper/NuSpec.AI/blob/main/LICENSE)
