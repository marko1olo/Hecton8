using System.Runtime.InteropServices;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Minimal inventory cost payload used by core data assemblies.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InventoryCost
    {
        public int ItemId;
        public int Amount;
    }
}
