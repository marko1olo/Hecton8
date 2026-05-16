using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Absolute macro-ecology swarm DTO. Coordinates are 50 m biomass macro-cells, not shifted runtime transforms.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
    public struct MacroSwarm
    {
        public uint HashId;
        public int2 SectorAup;
        public int2 TargetSectorAup;
        public float2 CurrentSectorAup;
        public float BiomassValue;
        public float Speed;
        public ushort Flags;
        public ushort Reserved;
        public ulong Genome;
    }

    /// <summary>
    /// Arrival packet emitted by the Burst travel pass and consumed by the ecology owner in the late-frame swap window.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    public struct MacroSwarmArrival
    {
        public int2 TargetSectorAup;
        public float BiomassValue;
        public uint HashId;
    }
}
