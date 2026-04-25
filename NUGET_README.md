# NuSpec.AI

**An AI assistant working with your package shouldn't have to guess.**

When your library ships as a `.nupkg`, the AI helping a downstream developer can't read your source. It sees an opaque assembly reference — type names if it's lucky, nothing else. NuSpec.AI fixes that. During `dotnet pack` it analyzes your public API surface with Roslyn and writes a structured map (`ai/package-map.json`) into the `.nupkg`. AI tools read it and instantly know your types, signatures, doc comments, and inferred semantic roles.

## Quick Start

Add the package — that's the whole setup:

```xml
<PackageReference Include="NuSpec.AI" Version="3.0.1" PrivateAssets="all" />
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
  "schemaVersion": 2,
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

A consumer's AI doesn't have your source. Its only structural alternative to NuSpec.AI is to **decompile your DLL** — which produces full method bodies, async state machines, backing fields, and compiler-generated artifacts. For a real-world project, NuSpec.AI's own ~50 KB CLI assembly:

| Source for the AI                       | Bytes  | ≈ Tokens† | vs. decompilation |
|-----------------------------------------|-------:|----------:|------------------:|
| ILSpy-decompiled DLL                    | 52,919 |   ~13,200 | —                 |
| `package-map.json`                      | 17,027 |    ~4,300 | **-68%**          |
| `package-map.compact.json`              |  9,389 |    ~2,300 | **-82%**          |
| `package-map.ultra`                     |  3,381 |      ~850 | **-94%**          |

† Bytes ÷ 4 is the standard rough estimate for English/code text in modern LLM tokenizers. The decompiled row is what actually lands in the AI's context window when it tries to inspect your package; the map rows are what NuSpec.AI provides instead.

And decompiled output omits two things the AI needs most: **XML doc comments** (those live in a sibling `.xml` file that isn't always shipped to nuget.org) and **inferred semantic roles** (a decompiler has no notion of "this is a repository" or "this is a `DbContext`"). NuSpec.AI bundles both into the map directly.

For the size of `package-map.json` itself: across 24 production projects (3.5 MB of source), it averages **32% smaller than the source code** with savings of 60–84% typical for service- and logic-heavy projects.

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

## Requirements

- **.NET 8.0 SDK or later** (build-time only — your package can target anything from netstandard2.0 onward)
- Project must be packable

## Links

- [GitHub repo, full docs, and issues](https://github.com/sean-m-cooper/NuSpec.AI)
- [License (MIT)](https://github.com/sean-m-cooper/NuSpec.AI/blob/main/LICENSE)
