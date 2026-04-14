namespace Hecton8.World
{
    /// <summary>
    /// Typed shadow-pass completion payload from the scatter backend host.
    /// Keeps parity bookkeeping out of the director partial.
    /// </summary>
    internal readonly struct ScatterBackendShadowCompletion
    {
        public ScatterBackendShadowCompletion(
            in ScatterSimulationParitySnapshot backendParity,
            in ScatterBackendParityReference classicParity,
            bool isJobActive)
        {
            BackendParity = backendParity;
            ClassicParity = classicParity;
            CandidateCount = backendParity.CandidateCount;
            ClassicQueuedCandidateCount = classicParity.CandidateCount;
            CandidateDelta = backendParity.CandidateCount - classicParity.CandidateCount;
            GroundDelta = backendParity.GroundCount - classicParity.GroundCount;
            ClusterDelta = backendParity.ClusterCount - classicParity.ClusterCount;
            StructureDelta = backendParity.StructureCount - classicParity.StructureCount;
            SpawnDelta = backendParity.SpawnCount - classicParity.SpawnCount;
            CandidateChecksumMatch = backendParity.CandidateChecksum == classicParity.CandidateChecksum;
            ParityStatusLabel = ResolveParityStatusLabel(
                CandidateDelta,
                GroundDelta,
                ClusterDelta,
                StructureDelta,
                SpawnDelta,
                CandidateChecksumMatch);
            HasParityMatch = ParityStatusLabel == "Match";
            IsJobActive = isJobActive;
        }

        public ScatterSimulationParitySnapshot BackendParity { get; }
        public ScatterBackendParityReference ClassicParity { get; }
        public int CandidateCount { get; }
        public int ClassicQueuedCandidateCount { get; }
        public int CandidateDelta { get; }
        public int GroundDelta { get; }
        public int ClusterDelta { get; }
        public int StructureDelta { get; }
        public int SpawnDelta { get; }
        public bool CandidateChecksumMatch { get; }
        public string ParityStatusLabel { get; }
        public bool HasParityMatch { get; }
        public bool IsJobActive { get; }

        private static string ResolveParityStatusLabel(
            int candidateDelta,
            int groundDelta,
            int clusterDelta,
            int structureDelta,
            int spawnDelta,
            bool candidateChecksumMatch)
        {
            if (candidateDelta != 0)
                return "CandidateCountMismatch";
            if (groundDelta != 0)
                return "GroundCountMismatch";
            if (clusterDelta != 0)
                return "ClusterCountMismatch";
            if (structureDelta != 0)
                return "StructureCountMismatch";
            if (spawnDelta != 0)
                return "SpawnCountMismatch";
            if (!candidateChecksumMatch)
                return "CandidateChecksumMismatch";

            return "Match";
        }
    }
}
