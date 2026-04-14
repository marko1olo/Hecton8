using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path input for one scatter backend schedule attempt.
    /// Keeps director-side orchestration thin while preserving director ownership.
    /// </summary>
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
            ObserverPosition = observerPosition;
            TotalCells = totalCells;
            CellSize = cellSize;
            RadiusCells = radiusCells;
            GroundBudget = groundBudget;
            ClusterBudget = clusterBudget;
            StructureStride = structureStride;
            SpawnStride = spawnStride;
            SurfaceYOffset = surfaceYOffset;
            Seed = seed;
            EligibilityMask = eligibilityMask;
            DefaultSuppressionState = defaultSuppressionState;
            DirtyFlags = dirtyFlags;
            ParityReference = parityReference;
        }

        public Vector3 ObserverPosition { get; }
        public int TotalCells { get; }
        public float CellSize { get; }
        public int RadiusCells { get; }
        public int GroundBudget { get; }
        public int ClusterBudget { get; }
        public int StructureStride { get; }
        public int SpawnStride { get; }
        public float SurfaceYOffset { get; }
        public uint Seed { get; }
        public ScatterSimulationEligibilityFlags EligibilityMask { get; }
        public ScatterSimulationSuppressionState DefaultSuppressionState { get; }
        public ScatterSimulationDirtyFlags DirtyFlags { get; }
        public ScatterBackendParityReference ParityReference { get; }
    }
}
