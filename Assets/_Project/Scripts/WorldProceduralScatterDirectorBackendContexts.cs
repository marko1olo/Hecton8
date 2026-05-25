using System.Runtime.InteropServices;
using UnityEngine;

namespace Hecton8.World
{
    internal static class WorldProceduralScatterDirectorBackendContextsLayout
    {
        public const int ScatterBackendShadowScheduleContextStrideBytes = 80;
    }

    public sealed partial class WorldProceduralScatterDirector
    {
        [StructLayout(LayoutKind.Explicit, Size = WorldProceduralScatterDirectorBackendContextsLayout.ScatterBackendShadowScheduleContextStrideBytes)]
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
                ClassicParityReference = classicParityReference;
                ObserverPosition = observerPosition;
                TotalCells = totalCells;
                GroundBudget = groundBudget;
                ClusterBudget = clusterBudget;
                StructureStride = structureStride;
                SpawnStride = spawnStride;
                _pad0 = 0UL;
                _pad1 = 0UL;
            }

            [FieldOffset(0)]
            public readonly ScatterBackendParityReference ClassicParityReference;

            [FieldOffset(32)]
            public readonly Vector3 ObserverPosition;

            [FieldOffset(44)]
            public readonly int TotalCells;

            [FieldOffset(48)]
            public readonly int GroundBudget;

            [FieldOffset(52)]
            public readonly int ClusterBudget;

            [FieldOffset(56)]
            public readonly int StructureStride;

            [FieldOffset(60)]
            public readonly int SpawnStride;

            [FieldOffset(64)]
            private readonly ulong _pad0;

            [FieldOffset(72)]
            private readonly ulong _pad1;
        }
    }
}
