using System;

namespace NuSpec.AI
{
    /// <summary>
    /// Specifies one or more semantic roles for a type, overriding NuSpec.AI's automatic role inference.
    /// </summary>
    /// <example>[AiRole("aggregate-root", "audited")]</example>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Interface | AttributeTargets.Enum,
        AllowMultiple = false)]
    internal sealed class AiRoleAttribute : Attribute
    {
        public AiRoleAttribute(params string[] roles) => Roles = roles;
        public string[] Roles { get; }
    }
}
