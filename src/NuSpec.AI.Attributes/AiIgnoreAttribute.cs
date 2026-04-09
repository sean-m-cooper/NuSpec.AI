using System;

namespace NuSpec.AI;

/// <summary>
/// Excludes the target type or member from the NuSpec.AI generated package map.
/// Only honored when NuSpec.AI.Pro is installed.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property |
    AttributeTargets.Constructor | AttributeTargets.Field,
    AllowMultiple = false)]
public sealed class AiIgnoreAttribute : Attribute { }
