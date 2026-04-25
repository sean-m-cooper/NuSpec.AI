using System;

namespace NuSpec.AI
{
    /// <summary>
    /// Excludes the target type or member from the NuSpec.AI generated package map.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Interface | AttributeTargets.Enum |
        AttributeTargets.Method | AttributeTargets.Property |
        AttributeTargets.Constructor | AttributeTargets.Field,
        AllowMultiple = false)]
    internal sealed class AiIgnoreAttribute : Attribute { }
}
