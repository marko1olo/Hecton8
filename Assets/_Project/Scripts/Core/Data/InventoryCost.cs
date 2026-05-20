using System.Runtime.InteropServices;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Minimal inventory cost payload used by core data assemblies.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InventoryCost
    {
        [FieldOffset(0)] public int ItemId;
        [FieldOffset(4)] public int Amount;
        [FieldOffset(8)] private ulong _pad0;
    }
}
