using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private struct ScatterPoolWarmupContext
        {
            public ObjectPoolManager Pool;
            public Dictionary<int, int> PrefabCreateAllowances;
            public Dictionary<int, int> PrefabWarmupCounts;
            public Dictionary<int, GameObject> PrefabWarmupPrefabs;
            public Dictionary<int, int> PrefabWarmupFamilyHashes;
            public Vector3 ObserverPosition;
            public bool HasObserverPosition;
            public bool InitialWarmupPass;
            public bool UseExactStartupWarmup;
            public int RemainingWarmupBudget;
            public int PerPrefabWarmupLimit;
            public int RemainingInitialWarmupCreates;
            public bool DiagnosticsTraceActive;

            public ScatterPoolWarmupContext(
                ObjectPoolManager pool,
                Dictionary<int, int> prefabCreateAllowances,
                Dictionary<int, int> prefabWarmupCounts,
                Dictionary<int, GameObject> prefabWarmupPrefabs,
                Dictionary<int, int> prefabWarmupFamilyHashes,
                Vector3 observerPosition,
                bool hasObserverPosition,
                bool initialWarmupPass,
                bool useExactStartupWarmup,
                int remainingWarmupBudget,
                int perPrefabWarmupLimit,
                int remainingInitialWarmupCreates,
                bool diagnosticsTraceActive)
            {
                Pool = pool;
                PrefabCreateAllowances = prefabCreateAllowances;
                PrefabWarmupCounts = prefabWarmupCounts;
                PrefabWarmupPrefabs = prefabWarmupPrefabs;
                PrefabWarmupFamilyHashes = prefabWarmupFamilyHashes;
                ObserverPosition = observerPosition;
                HasObserverPosition = hasObserverPosition;
                InitialWarmupPass = initialWarmupPass;
                UseExactStartupWarmup = useExactStartupWarmup;
                RemainingWarmupBudget = remainingWarmupBudget;
                PerPrefabWarmupLimit = perPrefabWarmupLimit;
                RemainingInitialWarmupCreates = remainingInitialWarmupCreates;
                DiagnosticsTraceActive = diagnosticsTraceActive;
            }
        }
    }
}
