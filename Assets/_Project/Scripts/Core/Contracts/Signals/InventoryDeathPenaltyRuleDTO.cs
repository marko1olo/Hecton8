using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Blittable per-item death penalty rule row consumed through Vault snapshots. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InventoryDeathPenaltyRuleDTO
    {
        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public byte DropOnDeath;
        [FieldOffset(5)] public byte RetainIfEquipped;
        [FieldOffset(6)] public ushort Reserved0;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint _pad0;
    }
}
