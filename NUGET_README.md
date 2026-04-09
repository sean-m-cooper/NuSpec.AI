# NuSpec.AI

**Give AI coding assistants instant context about your NuGet package.**

NuSpec.AI automatically generates a structured JSON map of your package's public API surface — types, members, signatures, documentation, and inferred semantic roles — and embeds it in your `.nupkg` during `dotnet pack`. AI tools can read this file to understand your package without reflection, decompilation, or guesswork.

## Quick Start

1. Add the package to your `.csproj`:

```xml
<PackageReference Include="NuSpec.AI" Version="1.0.1" PrivateAssets="all" />
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

## Configuration

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
