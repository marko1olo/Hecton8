using System.Runtime.InteropServices;

namespace Hecton8.World
{
    internal static class ScatterBackendShadowCompletionLayout
    {
        public const int ScatterBackendShadowCompletionStrideBytes = 128;
    }

    /// <summary>
    /// Typed shadow-pass completion payload from the scatter backend host.
    /// Keeps parity bookkeeping out of the director partial.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ScatterBackendShadowCompletionLayout.ScatterBackendShadowCompletionStrideBytes)]
    internal readonly struct ScatterBackendShadowCompletion
    {
        public const byte ParityStatusMatch = 0;
        public const byte ParityStatusCandidateCountMismatch = 1;
        public const byte ParityStatusGroundCountMismatch = 2;
        public const byte ParityStatusClusterCountMismatch = 3;
        public const byte ParityStatusStructureCountMismatch = 4;
        public const byte ParityStatusSpawnCountMismatch = 5;
        public const byte ParityStatusCandidateChecksumMismatch = 6;
        public const byte ParityStatusBackendCandidateCapacitySaturated = 7;

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
            CandidateChecksumMatchFlag = backendParity.CandidateChecksum == classicParity.CandidateChecksum ? (byte)1 : (byte)0;
            ParityStatusCode = ResolveParityStatusCode(
                CandidateDelta,
                GroundDelta,
                ClusterDelta,
                StructureDelta,
                SpawnDelta,
                CandidateChecksumMatchFlag != 0,
                ScatterSimulationParitySnapshot.HasCandidateCapacitySaturated(in backendParity));
            HasParityMatchFlag = ParityStatusCode == ParityStatusMatch ? (byte)1 : (byte)0;
            IsJobActiveFlag = isJobActive ? (byte)1 : (byte)0;
        }

        [FieldOffset(0)]
        public readonly ScatterSimulationParitySnapshot BackendParity;

        [FieldOffset(64)]
        public readonly ScatterBackendParityReference ClassicParity;

        [FieldOffset(96)]
        public readonly int CandidateCount;

        [FieldOffset(100)]
        public readonly int ClassicQueuedCandidateCount;

        [FieldOffset(104)]
        public readonly int CandidateDelta;

        [FieldOffset(108)]
        public readonly int GroundDelta;

        [FieldOffset(112)]
        public readonly int ClusterDelta;

        [FieldOffset(116)]
        public readonly int StructureDelta;

        [FieldOffset(120)]
        public readonly int SpawnDelta;

        [FieldOffset(124)]
        public readonly byte CandidateChecksumMatchFlag;

        [FieldOffset(125)]
        public readonly byte HasParityMatchFlag;

        [FieldOffset(126)]
        public readonly byte IsJobActiveFlag;

        [FieldOffset(127)]
        public readonly byte ParityStatusCode;

        public static bool CandidateChecksumMatches(in ScatterBackendShadowCompletion completion)
        {
            return completion.CandidateChecksumMatchFlag != 0;
        }

        public static bool HasParityMatch(in ScatterBackendShadowCompletion completion)
        {
            return completion.HasParityMatchFlag != 0;
        }

        public static bool IsJobActive(in ScatterBackendShadowCompletion completion)
        {
            return completion.IsJobActiveFlag != 0;
        }

        public static string GetParityStatusLabel(byte statusCode)
        {
            switch (statusCode)
            {
                case ParityStatusCandidateCountMismatch:
                    return "CandidateCountMismatch";
                case ParityStatusGroundCountMismatch:
                    return "GroundCountMismatch";
                case ParityStatusClusterCountMismatch:
                    return "ClusterCountMismatch";
                case ParityStatusStructureCountMismatch:
                    return "StructureCountMismatch";
                case ParityStatusSpawnCountMismatch:
                    return "SpawnCountMismatch";
                case ParityStatusCandidateChecksumMismatch:
                    return "CandidateChecksumMismatch";
                case ParityStatusBackendCandidateCapacitySaturated:
                    return "BackendCandidateCapacitySaturated";
                case ParityStatusMatch:
                default:
                    return "Match";
            }
        }

        private static byte ResolveParityStatusCode(
            int candidateDelta,
            int groundDelta,
            int clusterDelta,
            int structureDelta,
            int spawnDelta,
            bool candidateChecksumMatch,
            bool backendCandidateCapacitySaturated)
        {
            if (backendCandidateCapacitySaturated)
                return ParityStatusBackendCandidateCapacitySaturated;
            if (candidateDelta != 0)
                return ParityStatusCandidateCountMismatch;
            if (groundDelta != 0)
                return ParityStatusGroundCountMismatch;
            if (clusterDelta != 0)
                return ParityStatusClusterCountMismatch;
            if (structureDelta != 0)
                return ParityStatusStructureCountMismatch;
            if (spawnDelta != 0)
                return ParityStatusSpawnCountMismatch;
            if (!candidateChecksumMatch)
                return ParityStatusCandidateChecksumMismatch;

            return ParityStatusMatch;
        }
    }
}
