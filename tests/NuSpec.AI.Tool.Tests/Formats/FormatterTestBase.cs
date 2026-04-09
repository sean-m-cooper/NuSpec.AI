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
                        new MemberInfo
                        {
                            Kind = "property",
                            Name = "Id",
                            Signature = "public int Id { get; set; }"
                        },
                        new MemberInfo
                        {
                            Kind = "property",
                            Name = "Status",
                            Signature = "public OrderStatus Status { get; set; }",
                            Documentation = "Current status."
                        }
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
                        new MemberInfo
                        {
                            Kind = "enum-value",
                            Name = "Pending",
                            Signature = "Pending = 0"
                        },
                        new MemberInfo
                        {
                            Kind = "enum-value",
                            Name = "Confirmed",
                            Signature = "Confirmed = 1"
                        }
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
