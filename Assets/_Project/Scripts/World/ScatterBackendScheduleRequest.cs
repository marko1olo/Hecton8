using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path input for one scatter backend schedule attempt.
    /// Keeps director-side orchestration thin while preserving director ownership.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal readonly struct ScatterBackendScheduleRequest
    {
        public ScatterBackendScheduleRequest(
            Vector3 observerPosition,
            int totalCells,
            float cellSize,
            int radiusCells,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int spawnStride,
            float surfaceYOffset,
            uint seed,
            ScatterSimulationEligibilityFlags eligibilityMask,
            ScatterSimulationSuppressionState defaultSuppressionState,
            ScatterSimulationDirtyFlags dirtyFlags,
            ScatterBackendParityReference parityReference)
        {
            ParityReference = parityReference;
            ObserverPosition = observerPosition;
            CellSize = cellSize;
            SurfaceYOffset = surfaceYOffset;
            Seed = seed;
            TotalCells = totalCells;
            RadiusCells = radiusCells;
            GroundBudget = groundBudget;
            ClusterBudget = clusterBudget;
            StructureStride = structureStride;
            SpawnStride = spawnStride;
            EligibilityMask = eligibilityMask;
            DefaultSuppressionState = defaultSuppressionState;
            DirtyFlags = dirtyFlags;
            _pad0 = 0;
            _pad1 = 0u;
            _pad2 = 0UL;
        }

        [FieldOffset(0)]
        public readonly ScatterBackendParityReference ParityReference;

        [FieldOffset(32)]
        public readonly Vector3 ObserverPosition;

        [FieldOffset(44)]
        public readonly float CellSize;

        [FieldOffset(48)]
        public readonly float SurfaceYOffset;

        [FieldOffset(52)]
        public readonly uint Seed;

        [FieldOffset(56)]
        public readonly int TotalCells;

        [FieldOffset(60)]
        public readonly int RadiusCells;

        [FieldOffset(64)]
        public readonly int GroundBudget;

        [FieldOffset(68)]
        public readonly int ClusterBudget;

        [FieldOffset(72)]
        public readonly int StructureStride;

        [FieldOffset(76)]
        public readonly int SpawnStride;

        [FieldOffset(80)]
        public readonly ScatterSimulationEligibilityFlags EligibilityMask;

        [FieldOffset(81)]
        public readonly ScatterSimulationSuppressionState DefaultSuppressionState;

        [FieldOffset(82)]
        public readonly ScatterSimulationDirtyFlags DirtyFlags;

        [FieldOffset(83)]
        private readonly byte _pad0;

        [FieldOffset(84)]
        private readonly uint _pad1;

        [FieldOffset(88)]
        private readonly ulong _pad2;
    }
}
