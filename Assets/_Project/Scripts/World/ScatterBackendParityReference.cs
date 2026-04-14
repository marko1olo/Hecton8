namespace Hecton8.World
{
    /// <summary>
    /// Classic owner-side shadow parity reference captured when a backend shadow pass is scheduled.
    /// This remains shadow-only and never grants backend ownership of live placements.
    /// </summary>
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
            CandidateCount = candidateCount;
            GroundCount = groundCount;
            ClusterCount = clusterCount;
            StructureCount = structureCount;
            SpawnCount = spawnCount;
            CandidateChecksum = candidateChecksum;
        }

        public int CandidateCount { get; }
        public int GroundCount { get; }
        public int ClusterCount { get; }
        public int StructureCount { get; }
        public int SpawnCount { get; }
        public ulong CandidateChecksum { get; }
    }
}
