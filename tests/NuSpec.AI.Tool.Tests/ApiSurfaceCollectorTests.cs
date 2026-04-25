using NuSpec.AI.Tool.Analysis;

namespace NuSpec.AI.Tool.Tests;

public class ApiSurfaceCollectorTests
{
    [Fact]
    public void CollectsPublicClass()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class MyClass
                {
                    public int Id { get; set; }
                    public string Name { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var type = result.Types[0];
        Assert.Equal("MyClass", type.Name);
        Assert.Equal("TestNs.MyClass", type.FullName);
        Assert.Equal("TestNs", type.Namespace);
        Assert.Equal("class", type.Kind);
        Assert.Equal(2, type.Members.Count);
        Assert.All(type.Members, m => Assert.Equal("property", m.Kind));
    }

    [Fact]
    public void IgnoresInternalAndPrivateClasses()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                internal class InternalClass { }
                public class PublicClass { }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        Assert.Equal("PublicClass", result.Types[0].Name);
    }

    [Fact]
    public void CollectsInterface()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public interface IMyService
                {
                    void DoWork();
                    string GetResult(int id);
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var type = result.Types[0];
        Assert.Equal("interface", type.Kind);
        Assert.Equal(2, type.Members.Count);
        Assert.All(type.Members, m => Assert.Equal("method", m.Kind));
    }

    [Fact]
    public void CollectsEnum()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public enum Status
                {
                    Pending = 0,
                    Active = 1,
                    Closed = 2
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var type = result.Types[0];
        Assert.Equal("enum", type.Kind);
        Assert.Equal(3, type.Members.Count);
        Assert.All(type.Members, m => Assert.Equal("enum-value", m.Kind));
        Assert.Equal("Pending = 0", type.Members[0].Signature);
    }

    [Fact]
    public void CollectsStruct()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public struct Point
                {
                    public double X { get; set; }
                    public double Y { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        Assert.Equal("struct", result.Types[0].Kind);
    }

    [Fact]
    public void CollectsRecord()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public record Person(string Name, int Age);
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var type = result.Types[0];
        Assert.Equal("record", type.Kind);
        // Records should have properties for positional parameters
        Assert.Contains(type.Members, m => m.Kind == "property" && m.Name == "Name");
        Assert.Contains(type.Members, m => m.Kind == "property" && m.Name == "Age");
    }

    [Fact]
    public void CollectsRecordStruct()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public record struct Coordinate(double Lat, double Lng);
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        Assert.Equal("record-struct", result.Types[0].Kind);
    }

    [Fact]
    public void CollectsGenericClass()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class Repository<T> where T : class
                {
                    public T GetById(int id) => default;
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var type = result.Types[0];
        Assert.Equal("Repository<T>", type.Name);
        Assert.Contains("Repository<T>", type.FullName);
    }

    [Fact]
    public void CollectsNestedPublicTypes()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class Outer
                {
                    public class Inner
                    {
                        public int Value { get; set; }
                    }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Equal(2, result.Types.Count);
        Assert.Contains(result.Types, t => t.Name == "Outer");
        Assert.Contains(result.Types, t => t.Name == "Inner");
    }

    [Fact]
    public void CollectsConstructors()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class MyService
                {
                    public MyService(string connectionString) { }
                    public void DoWork() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types[0];
        Assert.Contains(type.Members, m => m.Kind == "constructor" && m.Name == "MyService");
        Assert.Contains(type.Members, m => m.Kind == "method" && m.Name == "DoWork");
    }

    [Fact]
    public void CollectsStaticMembers()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public static class Helpers
                {
                    public static int Add(int a, int b) => a + b;
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types);
        var method = result.Types[0].Members[0];
        Assert.Equal("method", method.Kind);
        Assert.Contains("static", method.Signature);
    }

    [Fact]
    public void CollectsNamespaces()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace Acme.Core { public class A { } }
            namespace Acme.Core.Models { public class B { } }
            namespace Acme.Utils { public class C { } }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Equal(3, result.Namespaces.Count);
        Assert.Contains("Acme.Core", result.Namespaces);
        Assert.Contains("Acme.Core.Models", result.Namespaces);
        Assert.Contains("Acme.Utils", result.Namespaces);
    }

    [Fact]
    public void IgnoresPrivateMembers()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class MyClass
                {
                    public int PublicProp { get; set; }
                    private int PrivateProp { get; set; }
                    internal int InternalProp { get; set; }
                    protected int ProtectedProp { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Single(result.Types[0].Members);
        Assert.Equal("PublicProp", result.Types[0].Members[0].Name);
    }

    [Fact]
    public void ExtractsDocComments()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                /// <summary>
                /// A service that does important work.
                /// </summary>
                public class MyService
                {
                    /// <summary>Gets a thing by ID.</summary>
                    public int GetById(int id) => id;
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        var type = result.Types[0];
        Assert.Equal("A service that does important work.", type.Documentation);
        Assert.Equal("Gets a thing by ID.", type.Members[0].Documentation);
    }

    [Fact]
    public void OmitsDocumentationWhenAbsent()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class NoDoc
                {
                    public int Value { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        Assert.Null(result.Types[0].Documentation);
        Assert.Null(result.Types[0].Members[0].Documentation);
    }

    // --- Role Inference Tests ---

    [Fact]
    public void InfersEntityRole()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class Order
                {
                    public int Id { get; set; }
                    public string Name { get; set; }
                    public decimal Price { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Contains("entity", result.Types[0].Roles);
    }

    [Fact]
    public void InfersRepositoryRole()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class OrderRepository
                {
                    public void Save() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Contains("repository", result.Types[0].Roles);
    }

    [Fact]
    public void InfersRepositoryRoleFromInterface()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public interface IOrderRepository
                {
                    void Save();
                }
                public class OrderRepo : IOrderRepository
                {
                    public void Save() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.All(result.Types, t => Assert.Contains("repository", t.Roles));
    }

    [Fact]
    public void InfersServiceRole()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class OrderService
                {
                    public void Process() { }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Contains("service", result.Types[0].Roles);
    }

    [Fact]
    public void InfersFactoryRole()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class WidgetFactory
                {
                    public object Create() => new object();
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Contains("factory", result.Types[0].Roles);
    }

    [Fact]
    public void InfersDtoRoleForRecord()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public record OrderDto(int Id, string Name);
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Contains("dto", result.Types[0].Roles);
    }

    [Fact]
    public void NoRolesForGenericUtilityClass()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class StringHelpers
                {
                    public static string Trim(string input) => input.Trim();
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);
        Assert.Empty(result.Types[0].Roles);
    }

    [Fact]
    public void HandlesPartialClasses()
    {
        var compilation = TestHelpers.CreateCompilation(
            """
            namespace TestNs
            {
                public partial class MyClass
                {
                    public int Id { get; set; }
                }
            }
            """,
            """
            namespace TestNs
            {
                public partial class MyClass
                {
                    public string Name { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        // Partial classes should merge into one type
        Assert.Single(result.Types);
        Assert.Equal(2, result.Types[0].Members.Count);
    }

    [Fact]
    public void DoesNotIncludeTypesFromReferences()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace TestNs
            {
                public class MyClass
                {
                    public string Value { get; set; }
                }
            }
            """);

        var result = ApiSurfaceCollector.Collect(compilation);

        // Should not contain System.String, System.Object, etc.
        Assert.All(result.Types, t => Assert.DoesNotContain("System", t.Namespace));
        Assert.Single(result.Types);
    }

    [Fact]
    public void Collector_FullDocsTrue_PopulatesDocsObject()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace Foo
            {
                /// <summary>A widget.</summary>
                /// <remarks>Use carefully.</remarks>
                public class Widget
                {
                    /// <summary>Spin it.</summary>
                    /// <param name="speed">RPM.</param>
                    /// <returns>Final state.</returns>
                    public int Spin(int speed) => speed;
                }
            }
            """);

        var surface = ApiSurfaceCollector.Collect(compilation, includeFullDocs: true);

        var widget = surface.Types.Single(t => t.Name == "Widget");
        Assert.Equal("Use carefully.", widget.Docs!.Remarks);

        var spin = widget.Members.Single(m => m.Name == "Spin");
        Assert.Equal("RPM.", spin.Docs!.Params!["speed"]);
        Assert.Equal("Final state.", spin.Docs.Returns);
    }

    [Fact]
    public void Collector_FullDocsFalse_OmitsDocsObject()
    {
        var compilation = TestHelpers.CreateCompilation("""
            namespace Foo
            {
                /// <summary>A widget.</summary>
                /// <remarks>Use carefully.</remarks>
                public class Widget {}
            }
            """);

        var surface = ApiSurfaceCollector.Collect(compilation, includeFullDocs: false);
        var widget = surface.Types.Single(t => t.Name == "Widget");
        Assert.Null(widget.Docs);
        Assert.Equal("A widget.", widget.Documentation);
    }
}
