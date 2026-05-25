using System.Runtime.InteropServices;

namespace Hecton8.Core.Data
{
    internal static class InventoryCostLayout
    {
        internal const int CostStrideBytes = 16;
    }

    /// <summary>
    /// Minimal inventory cost payload used by core data assemblies.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = InventoryCostLayout.CostStrideBytes)]
    public struct InventoryCost
    {
        [FieldOffset(0)] public int ItemId;
        [FieldOffset(4)] public int Amount;
        [FieldOffset(8)] private ulong _pad0;
    }
}
