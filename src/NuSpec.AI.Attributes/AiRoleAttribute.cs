using System;

namespace NuSpec.AI;

/// <summary>
/// Specifies one or more semantic roles for a type, overriding NuSpec.AI's automatic role inference.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
/// <example>[AiRole("aggregate-root", "audited")]</example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum,
    AllowMultiple = false)]
public sealed class AiRoleAttribute : Attribute
{
    public AiRoleAttribute(params string[] roles) => Roles = roles;
    public string[] Roles { get; }
}
