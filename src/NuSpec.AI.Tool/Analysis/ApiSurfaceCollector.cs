using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuSpec.AI.Tool.Models;
using MemberInfo = NuSpec.AI.Tool.Models.MemberInfo;
using TypeInfo = NuSpec.AI.Tool.Models.TypeInfo;

namespace NuSpec.AI.Tool.Analysis;

public static class ApiSurfaceCollector
{
    // Fully-qualified names of the attributes shipped as contentFiles by NuSpec.AI.
    // We match by name rather than by type identity so the check works regardless
    // of which assembly the attributes live in (each consumer gets its own internal copy).
    private const string AiIgnoreAttributeName = "NuSpec.AI.AiIgnoreAttribute";
    private const string AiRoleAttributeName = "NuSpec.AI.AiRoleAttribute";
    private const string AiDescriptionAttributeName = "NuSpec.AI.AiDescriptionAttribute";

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

    public static PublicSurfaceInfo Collect(CSharpCompilation compilation, HashSet<SyntaxTree>? projectTrees = null)
    {
        var types = new List<TypeInfo>();
        var namespaces = new HashSet<string>();

        // Default: treat every syntax tree in the compilation as part of the project.
        // ProjectAnalyzer passes an explicit set that excludes NuGet-sourced contentFiles.
        projectTrees ??= new HashSet<SyntaxTree>(compilation.SyntaxTrees);

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

        // [AiIgnore] excludes the type entirely (and, by not recursing, any nested types)
        if (HasAttribute(typeSymbol, AiIgnoreAttributeName))
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

                if (HasAttribute(member, AiIgnoreAttributeName))
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

            if (HasAttribute(member, AiIgnoreAttributeName))
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
        // [AiRole(...)] replaces inference entirely. [AiRole] with no args means "no roles".
        if (TryGetAiRoleOverride(typeSymbol, out var overrideRoles))
        {
            return overrideRoles;
        }

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
        // [AiDescription("...")] always wins over XML doc summaries.
        if (TryGetAiDescription(symbol, out var description))
        {
            return description;
        }

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

    // --------------------------------------------------------------------
    // Attribute helpers
    //
    // Attributes ship as contentFiles and are compiled into each consumer's
    // assembly as internal types. We match by fully-qualified name so the
    // lookup works regardless of which assembly the attribute lives in.
    // --------------------------------------------------------------------

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (IsAttributeNamed(attr, attributeFullName))
                return true;
        }
        return false;
    }

    private static bool IsAttributeNamed(AttributeData attr, string attributeFullName)
    {
        if (attr.AttributeClass is null)
            return false;

        var name = attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        return name == attributeFullName;
    }

    private static bool TryGetAiRoleOverride(ISymbol symbol, out IReadOnlyList<string> roles)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (!IsAttributeNamed(attr, AiRoleAttributeName))
                continue;

            // Constructor is `params string[] roles` — Roslyn surfaces that as a single
            // array constructor argument.
            if (attr.ConstructorArguments.Length == 0)
            {
                roles = Array.Empty<string>();
                return true;
            }

            var arg = attr.ConstructorArguments[0];
            if (arg.Kind == TypedConstantKind.Array)
            {
                var values = arg.Values
                    .Select(v => v.Value?.ToString())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Select(v => v!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToList();
                roles = values;
                return true;
            }

            // Fallback: single-string form, shouldn't occur given the params signature.
            if (arg.Value is string single && !string.IsNullOrEmpty(single))
            {
                roles = new[] { single };
                return true;
            }

            roles = Array.Empty<string>();
            return true;
        }

        roles = Array.Empty<string>();
        return false;
    }

    private static bool TryGetAiDescription(ISymbol symbol, out string? description)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (!IsAttributeNamed(attr, AiDescriptionAttributeName))
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is string value)
            {
                description = value;
                return true;
            }
        }

        description = null;
        return false;
    }
}
