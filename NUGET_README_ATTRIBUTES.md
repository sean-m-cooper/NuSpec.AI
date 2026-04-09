# NuSpec.AI.Attributes

**Attribute classes for controlling NuSpec.AI Pro output.**

This package provides three attributes — `[AiRole]`, `[AiIgnore]`, and `[AiDescription]` — that let you fine-tune what NuSpec.AI Pro includes in generated package maps and how it describes your types and members.

> **Requires [NuSpec.AI.Pro](https://www.nuget.org/packages/NuSpec.AI.Pro)** to have any effect at pack time. The attributes compile into your assembly harmlessly if only the free NuSpec.AI is installed — they're just ignored.

## Installation

```xml
<!-- The attributes ship as a regular runtime dependency -->
<PackageReference Include="NuSpec.AI.Attributes" Version="1.0.2" />

<!-- Pro is required to honor them at pack time -->
<PackageReference Include="NuSpec.AI.Pro" Version="1.0.2" PrivateAssets="all" />
```

## Attributes

### `[AiRole]`

Overrides automatically inferred semantic roles for a type.

```csharp
using NuSpec.AI;

[AiRole("aggregate-root", "audited")]
public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; }
}
```

Without this attribute, NuSpec.AI Pro infers roles automatically (e.g., `entity`, `repository`, `service`). Use `[AiRole]` when the inferred roles are wrong or when you want to apply domain-specific vocabulary.

Applies to: classes, structs, interfaces, enums.

---

### `[AiIgnore]`

Excludes a type or member from all generated output formats entirely.

```csharp
public class OrderService
{
    public Task<Order> PlaceOrderAsync(OrderRequest request) { ... }

    [AiIgnore]
    public string _internalCorrelationToken { get; set; }

    [AiIgnore]
    internal class DebugContext { ... }
}
```

Use this to hide implementation details, internal helpers, or members that are public for technical reasons but irrelevant to AI consumers.

Applies to: classes, structs, interfaces, enums, methods, properties, constructors, fields.

---

### `[AiDescription]`

Provides an AI-facing description that overrides or supplements XML doc comments.

```csharp
[AiDescription("Use this to initiate a refund. Do not call for subscription orders — use CancelSubscriptionAsync instead.")]
public Task<RefundResult> RefundAsync(int orderId, string reason) { ... }
```

If both `[AiDescription]` and an XML `<summary>` are present, the attribute wins. Use this when your IntelliSense documentation is accurate but you want a different (often more directive) description in the AI context.

Applies to: classes, structs, interfaces, enums, methods, properties, constructors, fields.

## Namespace

All attributes live in the `NuSpec.AI` namespace:

```csharp
using NuSpec.AI;
```

## Requirements

- **.NET Standard 2.0** or later (compatible with all modern .NET and .NET Framework 4.6.1+)
- [NuSpec.AI.Pro](https://www.nuget.org/packages/NuSpec.AI.Pro) to honor the attributes at pack time

## Links

- [NuSpec.AI.Pro](https://www.nuget.org/packages/NuSpec.AI.Pro) — paid tier that reads these attributes
- [NuSpec.AI](https://www.nuget.org/packages/NuSpec.AI) — free tier (attributes ignored)
- [Source code and issues](https://github.com/sean-m-cooper/NuSpec.AI)
- [License (MIT)](https://github.com/sean-m-cooper/NuSpec.AI/blob/main/LICENSE)
