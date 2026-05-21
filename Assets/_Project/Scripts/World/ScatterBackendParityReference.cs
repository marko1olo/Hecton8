using System.Runtime.InteropServices;

namespace Hecton8.World
{
    /// <summary>
    /// Classic owner-side shadow parity reference captured when a backend shadow pass is scheduled.
    /// This remains shadow-only and never grants backend ownership of live placements.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal readonly struct ScatterBackendParityReference
    {
        public ScatterBackendParityReference(
            int candidateCount,
            int groundCount,
            int clusterCount,
            int structureCount,
            int spawnCount,
            ulong candidateChecksum)
        {
            CandidateChecksum = candidateChecksum;
            CandidateCount = candidateCount;
            GroundCount = groundCount;
            ClusterCount = clusterCount;
            StructureCount = structureCount;
            SpawnCount = spawnCount;
            _pad0 = 0u;
        }

        [FieldOffset(0)]
        public readonly ulong CandidateChecksum;

        [FieldOffset(8)]
        public readonly int CandidateCount;

        [FieldOffset(12)]
        public readonly int GroundCount;

        [FieldOffset(16)]
        public readonly int ClusterCount;

        [FieldOffset(20)]
        public readonly int StructureCount;

        [FieldOffset(24)]
        public readonly int SpawnCount;

        [FieldOffset(28)]
        private readonly uint _pad0;
    }
}
