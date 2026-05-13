using System;

namespace Hecton8.Core.Memory.Layout
{
    /// <summary>
    /// Marks a struct whose byte layout is explicitly owned and safe for guarded binary blits.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, Inherited = false)]
    public sealed class BinaryBlittableSafeAttribute : Attribute
    {
    }
}
