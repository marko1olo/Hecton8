using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private readonly struct ScatterPlacementReconcilePlan
        {
            public readonly ScatterPlacement Placement;
            public readonly WorldProceduralProxyInstance Instance;
            public readonly WorldPrefabFamilyProfile.VariantEntry RuntimeVariant;
            public readonly byte FinalVariantActive;
            public readonly byte RequiresSpawn;
            public readonly byte ShouldApplyGeneratedGeology;
            public readonly int SyncSignature;
            public readonly byte AllowInitialWarmupCreate;

            public ScatterPlacementReconcilePlan(
                ScatterPlacement placement,
                WorldProceduralProxyInstance instance,
                WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
                bool finalVariantActive,
                bool requiresSpawn,
                bool shouldApplyGeneratedGeology,
                int syncSignature,
                bool allowInitialWarmupCreate)
            {
                Placement = placement;
                Instance = instance;
                RuntimeVariant = runtimeVariant;
                FinalVariantActive = finalVariantActive ? (byte)1 : (byte)0;
                RequiresSpawn = requiresSpawn ? (byte)1 : (byte)0;
                ShouldApplyGeneratedGeology = shouldApplyGeneratedGeology ? (byte)1 : (byte)0;
                SyncSignature = syncSignature;
                AllowInitialWarmupCreate = allowInitialWarmupCreate ? (byte)1 : (byte)0;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ScatterReconcileExecutionContext
        {
            public Transform Root;
            public Vector3 ObserverPosition;
            public bool HasObserverPosition;
            public WorldGenerativeGeologyService CachedGeologyService;
            public bool InitialWarmupPass;
            public int RemainingInitialCreateBudget;
            public int RebuiltCount;
            public int CreatedCount;
            public int ReusedCount;

            public ScatterReconcileExecutionContext(
                Transform root,
                Vector3 observerPosition,
                bool hasObserverPosition,
                WorldGenerativeGeologyService cachedGeologyService,
                bool initialWarmupPass,
                int remainingInitialCreateBudget)
            {
                Root = root;
                ObserverPosition = observerPosition;
                HasObserverPosition = hasObserverPosition;
                CachedGeologyService = cachedGeologyService;
                InitialWarmupPass = initialWarmupPass;
                RemainingInitialCreateBudget = remainingInitialCreateBudget;
                RebuiltCount = 0;
                CreatedCount = 0;
                ReusedCount = 0;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ScatterReconcileCleanupContext
        {
            public Dictionary<long, WorldProceduralProxyInstance> ActiveInstances;
            public Dictionary<long, ScatterPlacement> DesiredPlacements;
            public List<long> RemovalBuffer;
            public int RemovedCount;

            public ScatterReconcileCleanupContext(
                Dictionary<long, WorldProceduralProxyInstance> activeInstances,
                Dictionary<long, ScatterPlacement> desiredPlacements,
                List<long> removalBuffer)
            {
                ActiveInstances = activeInstances;
                DesiredPlacements = desiredPlacements;
                RemovalBuffer = removalBuffer;
                RemovedCount = 0;
            }
        }
    }
}
