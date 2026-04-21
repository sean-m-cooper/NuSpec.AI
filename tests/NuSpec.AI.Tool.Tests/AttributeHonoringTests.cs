using NuSpec.AI.Tool.Analysis;

namespace NuSpec.AI.Tool.Tests;

/// <summary>
/// Tests verifying that [AiIgnore], [AiRole], and [AiDescription] attributes
/// are honored by the symbol analyzer.
/// </summary>
public class AttributeHonoringTests
{
    /// <summary>
    /// Minimal copies of the attribute types that NuSpec.AI ships as contentFiles.
    /// These are included in every test compilation so symbol analysis sees them
    /// in the same form real consumers would (internal, same namespace).
    /// </summary>
    private const string AttributeSource = """
        namespace NuSpec.AI
        {
            using System;

            [AttributeUsage(
                AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum,
                AllowMultiple = false)]
            internal sealed class AiRoleAttribute : Attribute
            {
                public AiRoleAttribute(params string[] roles) => Roles = roles;
                public string[] Roles { get; }
            }

            [AttributeUsage(
                AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum |
                AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Constructor | AttributeTargets.Field,
                AllowMultiple = false)]
            internal sealed class AiIgnoreAttribute : Attribute { }

            [AttributeUsage(
                AttributeTargets.Class | AttributeTargets.Struct |
                AttributeTargets.Interface | AttributeTargets.Enum |
                AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Constructor | AttributeTargets.Field,
                AllowMultiple = false)]
            internal sealed class AiDescriptionAttribute : Attribute
            {
                public AiDescriptionAttribute(string description) => Description = description;
                public string Description { get; }
            }
        }
        """;

    // --------------------------------------------------------------------
    // [AiIgnore]
    // --------------------------------------------------------------------

    [Fact]
    public void AiIgnore_OnType_ExcludesEntireType()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                [AiIgnore]
                public class HiddenClass
                {
                    public int Id { get; set; }
                }

                public class VisibleClass
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        Assert.Equal("VisibleClass", result.Types[0].Name);
    }

    [Fact]
    public void AiIgnore_OnNestedType_ExcludesNested()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                public class Outer
                {
                    [AiIgnore]
                    public class HiddenNested { }

                    public class VisibleNested { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var types = result.Types.Select(t => t.Name).ToList();
        Assert.Contains("Outer", types);
        Assert.Contains("VisibleNested", types);
        Assert.DoesNotContain("HiddenNested", types);
    }

    [Fact]
    public void AiIgnore_OnMethod_ExcludesMethod()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                public class Svc
                {
                    public void Visible() { }

                    [AiIgnore]
                    public void Hidden() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var svc = result.Types.Single(t => t.Name == "Svc");
        var methodNames = svc.Members.Where(m => m.Kind == "method").Select(m => m.Name).ToList();
        Assert.Contains("Visible", methodNames);
        Assert.DoesNotContain("Hidden", methodNames);
    }

    [Fact]
    public void AiIgnore_OnProperty_ExcludesProperty()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                public class Model
                {
                    public int Id { get; set; }

                    [AiIgnore]
                    public string Secret { get; set; } = "";
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var model = result.Types.Single();
        var propNames = model.Members.Select(m => m.Name).ToList();
        Assert.Contains("Id", propNames);
        Assert.DoesNotContain("Secret", propNames);
    }

    // --------------------------------------------------------------------
    // [AiRole]
    // --------------------------------------------------------------------

    [Fact]
    public void AiRole_ReplacesInferredRoles()
    {
        // OrderService would normally get role "service"; AiRole replaces that.
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                [AiRole("aggregate-root")]
                public class OrderService
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Equal(new[] { "aggregate-root" }, type.Roles);
    }

    [Fact]
    public void AiRole_WithMultipleValues_ReturnsAllSorted()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                [AiRole("audited", "aggregate-root")]
                public class Order
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Equal(new[] { "aggregate-root", "audited" }, type.Roles);
    }

    [Fact]
    public void AiRole_WithNoArgs_ReturnsEmpty()
    {
        // Consumers can explicitly suppress inference with [AiRole()]
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                [AiRole]
                public class OrderService
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Empty(type.Roles);
    }

    [Fact]
    public void NoAiRole_StillInfersRoles()
    {
        // Regression guard: existing inference must still fire when no AiRole present.
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                public class OrderService
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Contains("service", type.Roles);
    }

    // --------------------------------------------------------------------
    // [AiDescription]
    // --------------------------------------------------------------------

    [Fact]
    public void AiDescription_OverridesXmlDoc()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                /// <summary>XML doc summary.</summary>
                [AiDescription("AI-friendly description.")]
                public class Thing
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Equal("AI-friendly description.", type.Documentation);
    }

    [Fact]
    public void AiDescription_WithoutXmlDoc_UsedAnyway()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                [AiDescription("Only source of docs.")]
                public class Thing
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Equal("Only source of docs.", type.Documentation);
    }

    [Fact]
    public void AiDescription_OnMember_OverridesMemberXmlDoc()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            using NuSpec.AI;
            namespace TestNs
            {
                public class Svc
                {
                    /// <summary>XML doc.</summary>
                    [AiDescription("AI override.")]
                    public void Method() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var svc = result.Types.Single();
        var method = svc.Members.Single(m => m.Kind == "method" && m.Name == "Method");
        Assert.Equal("AI override.", method.Documentation);
    }

    [Fact]
    public void NoAiDescription_FallsBackToXmlDoc()
    {
        var compilation = TestHelpers.CreateCompilation(AttributeSource, """
            namespace TestNs
            {
                /// <summary>The only doc.</summary>
                public class Thing
                {
                    public int Id { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types.Single();
        Assert.Equal("The only doc.", type.Documentation);
    }
}
