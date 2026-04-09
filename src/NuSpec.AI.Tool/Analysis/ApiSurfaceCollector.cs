using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuSpec.AI.Tool.Models;
using MemberInfo = NuSpec.AI.Tool.Models.MemberInfo;
using TypeInfo = NuSpec.AI.Tool.Models.TypeInfo;

namespace NuSpec.AI.Tool.Analysis;

public static class ApiSurfaceCollector
{
    private static readonly SymbolDisplayFormat SignatureFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeExtensionThis,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat TypeNameFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static PublicSurfaceInfo Collect(CSharpCompilation compilation)
    {
        var types = new List<TypeInfo>();
        var namespaces = new HashSet<string>();

        // Build a set of syntax trees that belong to the project (not metadata references)
        var projectTrees = new HashSet<SyntaxTree>(compilation.SyntaxTrees);

        CollectPublicTypes(compilation.GlobalNamespace, types, namespaces, projectTrees);

        return new PublicSurfaceInfo
        {
            Namespaces = namespaces.OrderBy(n => n).ToList(),
            Types = types.OrderBy(t => t.FullName).ToList()
        };
    }

    private static void CollectPublicTypes(
        INamespaceSymbol namespaceSymbol,
        List<TypeInfo> types,
        HashSet<string> namespaces,
        HashSet<SyntaxTree> projectTrees)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            CollectType(type, types, namespaces, projectTrees);
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            CollectPublicTypes(childNamespace, types, namespaces, projectTrees);
        }
    }

    private static void CollectType(
        INamedTypeSymbol typeSymbol,
        List<TypeInfo> types,
        HashSet<string> namespaces,
        HashSet<SyntaxTree> projectTrees)
    {
        if (typeSymbol.DeclaredAccessibility != Accessibility.Public)
            return;

        // Skip compiler-generated types
        if (typeSymbol.IsImplicitlyDeclared)
            return;

        // Only include types declared in the project's own source files
        if (!typeSymbol.DeclaringSyntaxReferences
                .Any(r => projectTrees.Contains(r.SyntaxTree)))
            return;

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrEmpty(ns))
            namespaces.Add(ns);

        var typeName = typeSymbol.ToDisplayString(TypeNameFormat);
        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");

        types.Add(new TypeInfo
        {
            Name = typeName,
            FullName = fullName,
            Namespace = ns,
            Kind = GetTypeKind(typeSymbol),
            Roles = InferRoles(typeSymbol),
            Documentation = ExtractDocumentation(typeSymbol),
            Members = CollectMembers(typeSymbol)
        });

        // Recurse into nested public types
        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            CollectType(nestedType, types, namespaces, projectTrees);
        }
    }

    private static IReadOnlyList<MemberInfo> CollectMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberInfo>();

        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.IsImplicitlyDeclared)
                    continue;

                var signature = member.HasConstantValue
                    ? $"{member.Name} = {member.ConstantValue}"
                    : member.Name;

                members.Add(new MemberInfo
                {
                    Kind = "enum-value",
                    Name = member.Name,
                    Signature = signature,
                    Documentation = ExtractDocumentation(member)
                });
            }
            return members;
        }

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
                continue;

            if (!IsPublicMember(member, typeSymbol))
                continue;

            var memberInfo = member switch
            {
                IMethodSymbol method when method.MethodKind == MethodKind.Ordinary =>
                    new MemberInfo
                    {
                        Kind = "method",
                        Name = method.Name,
                        Signature = method.ToDisplayString(SignatureFormat),
                        Documentation = ExtractDocumentation(method)
                    },
                IMethodSymbol method when method.MethodKind == MethodKind.Constructor =>
                    new MemberInfo
                    {
                        Kind = "constructor",
                        Name = method.ContainingType.Name,
                        Signature = method.ToDisplayString(SignatureFormat),
                        Documentation = ExtractDocumentation(method)
                    },
                IPropertySymbol property =>
                    new MemberInfo
                    {
                        Kind = "property",
                        Name = property.Name,
                        Signature = property.ToDisplayString(SignatureFormat),
                        Documentation = ExtractDocumentation(property)
                    },
                IFieldSymbol field when !field.IsConst && field.AssociatedSymbol is null =>
                    new MemberInfo
                    {
                        Kind = "field",
                        Name = field.Name,
                        Signature = field.ToDisplayString(SignatureFormat),
                        Documentation = ExtractDocumentation(field)
                    },
                IFieldSymbol field when field.IsConst =>
                    new MemberInfo
                    {
                        Kind = "field",
                        Name = field.Name,
                        Signature = field.ToDisplayString(SignatureFormat),
                        Documentation = ExtractDocumentation(field)
                    },
                _ => null
            };

            if (memberInfo is not null)
                members.Add(memberInfo);
        }

        return members;
    }

    private static bool IsPublicMember(ISymbol member, INamedTypeSymbol containingType)
    {
        // Interface members are implicitly public unless explicitly marked otherwise
        if (containingType.TypeKind == TypeKind.Interface)
        {
            return member.DeclaredAccessibility is Accessibility.Public
                or Accessibility.NotApplicable;
        }

        return member.DeclaredAccessibility == Accessibility.Public;
    }

    private static string GetTypeKind(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsRecord)
        {
            return typeSymbol.TypeKind == TypeKind.Struct ? "record-struct" : "record";
        }

        return typeSymbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Struct => "struct",
            _ => "type"
        };
    }

    private static IReadOnlyList<string> InferRoles(INamedTypeSymbol typeSymbol)
    {
        var roles = new List<string>();

        // Check for DbContext
        var baseType = typeSymbol.BaseType;
        while (baseType is not null)
        {
            if (baseType.Name == "DbContext")
            {
                roles.Add("db-context");
                break;
            }
            baseType = baseType.BaseType;
        }

        // Check for repository pattern
        if (typeSymbol.Name.Contains("Repository") ||
            typeSymbol.AllInterfaces.Any(i => i.Name.Contains("Repository")))
        {
            roles.Add("repository");
        }

        // Check for service
        if (typeSymbol.Name.EndsWith("Service", StringComparison.Ordinal) &&
            typeSymbol.TypeKind is TypeKind.Class or TypeKind.Interface)
        {
            roles.Add("service");
        }

        // Check for factory
        if (typeSymbol.Name.EndsWith("Factory", StringComparison.Ordinal) &&
            typeSymbol.TypeKind is TypeKind.Class or TypeKind.Interface)
        {
            roles.Add("factory");
        }

        // Check for middleware
        if (typeSymbol.AllInterfaces.Any(i => i.Name == "IMiddleware") ||
            typeSymbol.GetMembers().OfType<IMethodSymbol>().Any(m =>
                m.Name is "Invoke" or "InvokeAsync" &&
                m.Parameters.Any(p => p.Type.Name == "HttpContext")))
        {
            roles.Add("middleware");
        }

        // Check for IServiceCollection extension methods (entry-point / service-collection-extension)
        if (typeSymbol.IsStatic && typeSymbol.TypeKind == TypeKind.Class)
        {
            var extensionMethods = typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.IsExtensionMethod)
                .ToList();

            if (extensionMethods.Any(m =>
                m.Parameters.Length > 0 &&
                m.Parameters[0].Type.Name == "IServiceCollection"))
            {
                roles.Add("entry-point");
                roles.Add("service-collection-extension");
            }
        }

        // Check for entity (class/record with mostly properties, no complex methods)
        if (typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct &&
            !typeSymbol.IsStatic &&
            roles.Count == 0) // Don't tag as entity if it already has a more specific role
        {
            var publicMembers = typeSymbol.GetMembers()
                .Where(m => !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public)
                .ToList();

            var properties = publicMembers.OfType<IPropertySymbol>().Count();
            var methods = publicMembers.OfType<IMethodSymbol>()
                .Count(m => m.MethodKind == MethodKind.Ordinary);

            if (properties >= 2 && methods == 0)
            {
                roles.Add(typeSymbol.IsRecord ? "dto" : "entity");
            }
        }

        roles.Sort(StringComparer.Ordinal);
        return roles;
    }

    private static string? ExtractDocumentation(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var doc = XDocument.Parse(xml);
            var summary = doc.Descendants("summary").FirstOrDefault();
            if (summary is null)
                return null;

            var text = summary.Value.Trim();
            // Normalize whitespace (collapse multiple spaces/newlines into single space)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
