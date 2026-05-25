using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    internal static class MacroSwarmContractLayout
    {
        public const int SwarmStrideBytes = 48;
        public const int ArrivalStrideBytes = 16;
    }

    /// <summary>
    /// Absolute macro-ecology swarm DTO. Coordinates are 50 m biomass macro-cells, not shifted runtime transforms.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = MacroSwarmContractLayout.SwarmStrideBytes)]
    public struct MacroSwarm
    {
        [FieldOffset(0)] public uint HashId;
        [FieldOffset(4)] public int2 SectorAup;
        [FieldOffset(12)] public int2 TargetSectorAup;
        [FieldOffset(20)] public float2 CurrentSectorAup;
        [FieldOffset(28)] public float BiomassValue;
        [FieldOffset(32)] public float Speed;
        [FieldOffset(36)] public ushort Flags;
        [FieldOffset(38)] public ushort Reserved;
        [FieldOffset(40)] public ulong Genome;
    }

    /// <summary>
    /// Arrival packet emitted by the Burst travel pass and consumed by the ecology owner in the late-frame swap window.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = MacroSwarmContractLayout.ArrivalStrideBytes)]
    public struct MacroSwarmArrival
    {
        [FieldOffset(0)] public int2 TargetSectorAup;
        [FieldOffset(8)] public float BiomassValue;
        [FieldOffset(12)] public uint HashId;
    }
}
