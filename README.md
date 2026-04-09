# NuSpec.AI

**Give AI coding assistants instant context about your NuGet package.**

NuSpec.AI automatically generates a structured JSON map of your package's public API surface — types, members, signatures, documentation, and inferred roles — and embeds it in your `.nupkg` during `dotnet pack`. AI tools can read this file to understand your package without reflection, decompilation, or guesswork.

## Quick Start

1. Add the package:

```xml
<PackageReference Include="NuSpec.AI" Version="1.0.1" PrivateAssets="all" />
```

2. Pack your project:

```bash
dotnet pack
```

That's it. Your `.nupkg` now contains `ai/package-map.json`.

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
        "name": "OrderStatus",
        "fullName": "Acme.Orders.OrderStatus",
        "namespace": "Acme.Orders",
        "kind": "enum",
        "roles": [],
        "members": [
          { "kind": "enum-value", "name": "Pending", "signature": "Pending = 0" },
          { "kind": "enum-value", "name": "Confirmed", "signature": "Confirmed = 1" },
          { "kind": "enum-value", "name": "Shipped", "signature": "Shipped = 2" },
          { "kind": "enum-value", "name": "Cancelled", "signature": "Cancelled = 3" }
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

## How It Works

NuSpec.AI is a **development dependency** — it hooks into `dotnet pack` via MSBuild targets and does not ship as a runtime dependency of your package.

1. During pack, the MSBuild `.targets` file invokes the **NuSpec.AI CLI tool**
2. The CLI tool parses your project's `.cs` files using **Roslyn** and builds a `CSharpCompilation` with a semantic model
3. It walks the compilation's namespace tree in a **single pass**, collecting public types, members, signatures, XML doc comments, and inferred roles
4. Package metadata (id, version, description, tags, target frameworks, dependencies) is read from the `.csproj`
5. The result is written to `ai/package-map.json` and included in the `.nupkg`

### Why a Semantic Model?

NuSpec.AI goes beyond syntax parsing. By creating a Roslyn `CSharpCompilation`, it can:

- **Resolve type hierarchies** — know that `OrdersContext` inherits from `DbContext`
- **Detect interface implementations** — recognize repository patterns even when the class name doesn't match
- **Merge partial classes** — automatically combine partial declarations into a single type entry
- **Format signatures accurately** — use `ISymbol.ToDisplayString()` for correct generic type representations

## Schema Reference

### Root

| Field | Type | Description |
|-------|------|-------------|
| `schemaVersion` | `int` | Always `1`. Will increment on breaking changes. |
| `package` | `object` | Package identity and metadata. |
| `dependencies` | `object` | Package and framework references. |
| `publicSurface` | `object` | All public types, members, and namespaces. |

### Package

| Field | Type | Source |
|-------|------|--------|
| `id` | `string` | `<PackageId>` or `<AssemblyName>` or filename |
| `version` | `string` | `<PackageVersion>` or `<Version>` |
| `description` | `string?` | `<Description>` |
| `tags` | `string[]` | `<PackageTags>` (semicolon-separated) |
| `targetFrameworks` | `string[]` | `<TargetFramework>` or `<TargetFrameworks>` |

### Dependencies

| Field | Type | Notes |
|-------|------|-------|
| `packageReferences` | `string[]` | Package IDs. Excludes `PrivateAssets="all"`. |
| `frameworkReferences` | `string[]` | Framework reference names. |

### Types

| Field | Type | Description |
|-------|------|-------------|
| `name` | `string` | Type name including generic parameters (e.g., `Repository<T>`) |
| `fullName` | `string` | Fully qualified name (e.g., `Acme.Orders.Models.Order`) |
| `namespace` | `string` | Containing namespace |
| `kind` | `string` | `class`, `interface`, `enum`, `struct`, `record`, or `record-struct` |
| `roles` | `string[]` | Inferred semantic roles (see below) |
| `documentation` | `string?` | Content of `<summary>` XML doc comment. Omitted if absent. |
| `members` | `array` | Public members of the type |

### Members

| Field | Type | Description |
|-------|------|-------------|
| `kind` | `string` | `method`, `property`, `constructor`, `field`, or `enum-value` |
| `name` | `string` | Member name |
| `signature` | `string` | Full signature including modifiers, types, and parameters |
| `documentation` | `string?` | Content of `<summary>` XML doc comment. Omitted if absent. |

## Role Inference

NuSpec.AI infers semantic roles for types using the Roslyn semantic model and naming conventions. A type can have multiple roles.

| Role | How It's Detected |
|------|-------------------|
| `entity` | Class/struct with 2+ properties and no public methods |
| `dto` | Record type with only properties and no methods |
| `repository` | Name contains "Repository" or implements an interface containing "Repository" |
| `db-context` | Inherits from `DbContext` (resolved via semantic model) |
| `service` | Name ends with "Service" |
| `factory` | Name ends with "Factory" |
| `middleware` | Implements `IMiddleware` or has `Invoke`/`InvokeAsync` with `HttpContext` parameter |
| `entry-point` | Static class with `Add*`/`Use*` extension methods on `IServiceCollection` |
| `service-collection-extension` | Static class with extension methods on `IServiceCollection` |

## Configuration

### Disabling Generation

Set the `NuSpecAiEnabled` MSBuild property to `false`:

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

- **.NET 8.0 SDK** or later (for the CLI tool that runs during pack)
- Your project must be **packable** (`<IsPackable>true</IsPackable>` or default)

The package itself uses `PrivateAssets="all"` so it will **not** become a transitive dependency of consumers.

## Building from Source

```bash
# Clone the repo
git clone https://github.com/sean-m-cooper/NuSpec.AI.git
cd NuSpec.AI

# Build and run tests
dotnet build NuSpec.AI.slnx
dotnet test NuSpec.AI.slnx

# Publish the CLI tool and create the NuGet package
dotnet publish src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj -c Release -o src/NuSpec.AI/tools/net8.0 --no-self-contained
dotnet pack src/NuSpec.AI/NuSpec.AI.csproj -c Release -o artifacts --no-build
```

The resulting `.nupkg` will be in the `artifacts/` directory.

## Project Structure

```
NuSpec.AI/
├── NuSpec.AI.slnx
├── src/
│   ├── NuSpec.AI.Tool/           # CLI tool (Roslyn-based analyzer)
│   │   ├── Analysis/
│   │   │   ├── ApiSurfaceCollector.cs   # Core: symbol-based public API extraction
│   │   │   └── ProjectAnalyzer.cs       # Orchestrator: compilation + serialization
│   │   ├── Models/                      # JSON output models
│   │   ├── ProjectMetadata/
│   │   │   └── CsprojReader.cs          # Reads package metadata from .csproj
│   │   └── Program.cs                   # CLI entry point
│   └── NuSpec.AI/                # NuGet packaging project
│       └── build/
│           ├── NuSpec.AI.props          # Default MSBuild properties
│           └── NuSpec.AI.targets        # Pack hook: invokes CLI tool
└── tests/
    ├── NuSpec.AI.Tool.Tests/     # Unit tests (34 tests)
    └── SampleProject/            # Integration test project
```

## License

[MIT](LICENSE)
