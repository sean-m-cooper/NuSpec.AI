# NuSpec.AI

**Give AI coding assistants instant context about your NuGet package.**

NuSpec.AI automatically generates a structured JSON map of your package's public API surface — types, members, signatures, documentation, and inferred semantic roles — and embeds it in your `.nupkg` during `dotnet pack`. AI tools can read this file to understand your package without reflection, decompilation, or guesswork.

## Quick Start

1. Add the package to your `.csproj`:

```xml
<PackageReference Include="NuSpec.AI" Version="2.0.0" PrivateAssets="all" />
```

2. Pack your project:

```bash
dotnet pack
```

That's it. Your `.nupkg` now contains `ai/package-map.json`.

NuSpec.AI is a **development dependency only** — it runs during pack and does **not** ship as a runtime dependency of your package.

## What Gets Generated

Given a project like this:

```csharp
namespace Acme.Orders;

/// <summary>Represents a customer order.</summary>
public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus { Pending, Confirmed, Shipped, Cancelled }

public interface IOrderRepository
{
    /// <summary>Gets an order by its unique identifier.</summary>
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Order> AddAsync(Order order, CancellationToken ct = default);
}
```

NuSpec.AI produces `ai/package-map.json` inside the `.nupkg`:

```json
{
  "schemaVersion": 1,
  "package": {
    "id": "Acme.Orders",
    "version": "1.0.0",
    "description": "Order management library.",
    "tags": ["orders"],
    "targetFrameworks": ["net8.0"]
  },
  "dependencies": {
    "packageReferences": ["Microsoft.EntityFrameworkCore"],
    "frameworkReferences": []
  },
  "publicSurface": {
    "namespaces": ["Acme.Orders"],
    "types": [
      {
        "name": "Order",
        "fullName": "Acme.Orders.Order",
        "namespace": "Acme.Orders",
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
        "fullName": "Acme.Orders.IOrderRepository",
        "namespace": "Acme.Orders",
        "kind": "interface",
        "roles": ["repository"],
        "members": [
          {
            "kind": "method",
            "name": "GetByIdAsync",
            "signature": "Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)",
            "documentation": "Gets an order by its unique identifier."
          },
          {
            "kind": "method",
            "name": "AddAsync",
            "signature": "Task<Order> AddAsync(Order order, CancellationToken ct = default)"
          }
        ]
      }
    ]
  }
}
```

## What's Captured

- **Package metadata** — id, version, description, tags, target frameworks, and dependencies (read from your `.csproj`)
- **Public types** — classes, interfaces, enums, structs, records, and record structs, including nested and generic types
- **Public members** — methods, properties, constructors, fields, and enum values with full signatures
- **XML doc comments** — `<summary>` content on types and members (omitted when absent)
- **Semantic roles** — automatically inferred from type hierarchy and naming conventions (see below)

## Role Inference

NuSpec.AI uses Roslyn's semantic model to infer what each type *does*, not just what it *is*. A type can have multiple roles.

| Role | How It's Detected |
|------|-------------------|
| `entity` | Class/struct with 2+ properties and no public methods |
| `dto` | Record with only properties and no methods |
| `repository` | Name contains "Repository" or implements a "Repository" interface |
| `db-context` | Inherits from `DbContext` |
| `service` | Name ends with "Service" |
| `factory` | Name ends with "Factory" |
| `middleware` | Implements `IMiddleware` or has `Invoke`/`InvokeAsync` with `HttpContext` |
| `entry-point` | Extension methods on `IServiceCollection` |
| `service-collection-extension` | Static class extending `IServiceCollection` |

## Context Window Impact

**The core value: one file gives an AI complete API knowledge that otherwise requires reading every source file — or isn't available at all.**

When your package is consumed as a NuGet reference, the AI assistant has no source code to read. It can see the `.dll` in the references, but it can't inspect it without reflection. `package-map.json` gives it structured, immediate access to every public type, method signature, doc comment, and semantic role.

### How big is the file?

The JSON captures only the **public API surface** — no method bodies, no private members, no `using` statements. Its size depends on the shape of your API, not the complexity of your implementation.

**Measured across 24 production projects (3.5 MB of source code):**

| Project Profile | Types | Source | JSON | Change |
|----------------|-------|--------|------|--------|
| Azure Functions (services, business logic) | 21 | 192 KB | 32 KB | **-84%** |
| Data processing functions | 18 | 102 KB | 18 KB | **-82%** |
| Common services library | 44 | 515 KB | 119 KB | **-77%** |
| Azure provisioning functions | 16 | 95 KB | 26 KB | **-73%** |
| API client library | 7 | 41 KB | 13 KB | **-67%** |
| Database management | 16 | 108 KB | 41 KB | **-62%** |
| Shared library (673 types) | 673 | 1,061 KB | 880 KB | **-17%** |
| Common providers | 116 | 510 KB | 287 KB | **-44%** |
| Models library (mostly DTOs) | 723 | 527 KB | 840 KB | +59% |

**Across all 24 projects: 3,554 KB of source → 2,424 KB of JSON (32% smaller overall).**

**What drives the size:** the number of public types and members, not the amount of implementation code. A service with 5 public methods produces the same JSON whether those methods are 10 lines or 200 lines each.

**When the JSON saves the most:** Projects with substantial implementation logic — services, functions, repositories, middleware. Savings of 60–84% are typical.

**When the JSON is larger:** Model-heavy projects with hundreds of small DTOs, enums, and interfaces where the source is already lean. The JSON metadata structure adds overhead that exceeds what was stripped.

### The real comparison

For **NuGet package consumers**, the alternative to `package-map.json` isn't "read the source" — it's "have no structured API context at all." The AI would need to rely on IntelliSense hints, documentation websites, or asking you to explain the API. One JSON file replaces all of that with a complete, machine-readable API map.

## Configuration

### Output formats

All formats are free and included:

```xml
<PropertyGroup>
  <NuSpecAiFormats>json;yaml;ultra</NuSpecAiFormats>
</PropertyGroup>
```

| Format    | File                          | Description                         |
|-----------|-------------------------------|-------------------------------------|
| `json`    | `ai/package-map.json`         | Standard JSON (default)             |
| `yaml`    | `ai/package-map.yaml`         | Compact, human-readable             |
| `compact` | `ai/package-map.compact.json` | Minified JSON                       |
| `ultra`   | `ai/package-map.ultra`        | Ultra-compact positional, smallest  |

Use `all` to emit every format.

### Attributes

`[AiRole]`, `[AiIgnore]`, `[AiDescription]` ship as `internal` source compiled into your assembly:

```csharp
using NuSpec.AI;

[AiRole("aggregate-root")]
public class Order { }
```

### Disabling generation

```xml
<PropertyGroup>
  <NuSpecAiEnabled>false</NuSpecAiEnabled>
</PropertyGroup>
```

Or per-invocation:

```bash
dotnet pack -p:NuSpecAiEnabled=false
```

## Requirements

- **.NET 8.0 SDK** or later
- Project must be packable (`<IsPackable>true</IsPackable>` or default)

## Links

- [Source code and issues](https://github.com/sean-m-cooper/NuSpec.AI)
- [License (MIT)](https://github.com/sean-m-cooper/NuSpec.AI/blob/main/LICENSE)
