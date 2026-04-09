using System;

namespace NuSpec.AI;

/// <summary>
/// Provides a description for AI context, overriding any XML doc comment summary.
/// Useful when the AI-facing description should differ from IntelliSense documentation.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiDescriptionAttribute : Attribute
{
    public AiDescriptionAttribute(string description) => Description = description;
    public string Description { get; }
}
