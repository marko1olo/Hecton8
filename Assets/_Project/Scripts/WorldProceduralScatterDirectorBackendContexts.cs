using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private readonly struct ScatterBackendShadowScheduleContext
        {
            public ScatterBackendShadowScheduleContext(
                Vector3 observerPosition,
                int totalCells,
                int groundBudget,
                int clusterBudget,
                int structureStride,
                int spawnStride,
                ScatterBackendParityReference classicParityReference)
            {
                ObserverPosition = observerPosition;
                TotalCells = totalCells;
                GroundBudget = groundBudget;
                ClusterBudget = clusterBudget;
                StructureStride = structureStride;
                SpawnStride = spawnStride;
                ClassicParityReference = classicParityReference;
            }

            public Vector3 ObserverPosition { get; }
            public int TotalCells { get; }
            public int GroundBudget { get; }
            public int ClusterBudget { get; }
            public int StructureStride { get; }
            public int SpawnStride { get; }
            public ScatterBackendParityReference ClassicParityReference { get; }
        }
    }
}
