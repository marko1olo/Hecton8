using System;
using GPUInstancer;
using Hecton8.Environment;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        internal sealed class ScatterWorkingMemory : IDisposable
        {
            public NativeArray<WorldProceduralFieldSampler.CellInputData> CellSamplingInputs;
            public NativeArray<WorldProceduralFieldSampler.CellOutputData> CellSamplingOutputs;
            public NativeArray<ScatterSimulationCellState> ScatterBackendCellStates;
            public readonly System.Collections.Generic.Dictionary<long, ScatterPlacement> DesiredPlacements = new System.Collections.Generic.Dictionary<long, ScatterPlacement>(2048);
            public readonly System.Collections.Generic.Dictionary<long, ScatterPlacement> RetainedPlacements = new System.Collections.Generic.Dictionary<long, ScatterPlacement>(4096);
            public readonly System.Collections.Generic.Dictionary<long, float> PlacementLastSeenTimes = new System.Collections.Generic.Dictionary<long, float>(4096);
            public readonly System.Collections.Generic.Stack<ScatterPlacement> PlacementPool = new System.Collections.Generic.Stack<ScatterPlacement>(4096);
            public readonly System.Collections.Generic.Dictionary<long, int> StructureWindowCounts = new System.Collections.Generic.Dictionary<long, int>(256);
            public readonly System.Collections.Generic.Dictionary<long, int> SpawnWindowCounts = new System.Collections.Generic.Dictionary<long, int>(256);
            public readonly System.Collections.Generic.List<ScatterCandidate> CandidateBuffer = new System.Collections.Generic.List<ScatterCandidate>(256);
            public readonly System.Collections.Generic.List<long> RemovalBuffer = new System.Collections.Generic.List<long>(256);
            public readonly System.Collections.Generic.List<WorldFaunaSpawnRegistry.Anchor> FaunaAnchorBuffer = new System.Collections.Generic.List<WorldFaunaSpawnRegistry.Anchor>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> ClusterAccentOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> ClusterOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> ExactClusterOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> GroundOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> WindowOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> PatternStructureOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> StructureAccentOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> PatternSpawnOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(128);
            public readonly System.Collections.Generic.List<ScatterCandidate> PatternSpawnPassiveOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(96);
            public readonly System.Collections.Generic.List<ScatterCandidate> PatternSpawnPredatorOrderedCandidates = new System.Collections.Generic.List<ScatterCandidate>(64);
            public readonly System.Collections.Generic.List<ScatterRuntimeRuleEntry> RuntimeRuleBuffer = new System.Collections.Generic.List<ScatterRuntimeRuleEntry>(256);
            public readonly System.Collections.Generic.HashSet<long> OccupiedCellBuffer = new System.Collections.Generic.HashSet<long>(1024);
            public readonly System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<ScatterPlacement>> GridPlacements = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<ScatterPlacement>>(512);
            public readonly System.Collections.Generic.List<System.Collections.Generic.List<ScatterPlacement>> GridPlacementBuckets = new System.Collections.Generic.List<System.Collections.Generic.List<ScatterPlacement>>(512);
            public readonly System.Collections.Generic.Dictionary<long, ScatterCandidate> StructureRescueCandidates = new System.Collections.Generic.Dictionary<long, ScatterCandidate>(64);
            public readonly System.Collections.Generic.Dictionary<long, ScatterCandidate> SpawnRescueCandidates = new System.Collections.Generic.Dictionary<long, ScatterCandidate>(64);
            public readonly System.Collections.Generic.Dictionary<int, int> PrefabWarmupCounts = new System.Collections.Generic.Dictionary<int, int>(32);
            public readonly System.Collections.Generic.Dictionary<int, GameObject> PrefabWarmupPrefabs = new System.Collections.Generic.Dictionary<int, GameObject>(32);
            public readonly System.Collections.Generic.Dictionary<int, string> PrefabWarmupFamilyIds = new System.Collections.Generic.Dictionary<int, string>(32);
            public readonly System.Collections.Generic.Dictionary<int, int> PrefabCreateAllowances = new System.Collections.Generic.Dictionary<int, int>(32);
            public readonly System.Collections.Generic.Dictionary<int, int> PreferredFamilyPlacementCounts = new System.Collections.Generic.Dictionary<int, int>(16);
            public readonly WorldProceduralPatternProfile[] PatternProfileCache = new WorldProceduralPatternProfile[16];
            public readonly System.Collections.Generic.Dictionary<Hecton8.Environment.HectonBiomeFamilyProfile, WorldProceduralBiomeFamilyContextProfile> BiomeContextCache = new System.Collections.Generic.Dictionary<Hecton8.Environment.HectonBiomeFamilyProfile, WorldProceduralBiomeFamilyContextProfile>(32);
            public bool HasCachedPatternQuota;
            public WorldProceduralPattern CachedPatternQuotaPattern;
            public HectonBiomeMatrixProfile CachedPatternQuotaBiomeProfile;
            public int CachedPatternClusterRatioStart;
            public int CachedPatternPassiveSpawnMin;
            public int CachedPatternPredatorSpawnMax;
            public bool HasCachedBudgetScales;
            public WorldProceduralPatternProfile CachedBudgetScalePatternProfile;
            public WorldProceduralBiomeFamilyContextProfile CachedBudgetScaleBiomeContext;
            public float CachedGroundBudgetScale;
            public float CachedClusterBudgetScale;
            public float CachedStructureBudgetScale;
            public float CachedSpawnBudgetScale;
            public readonly float[] LayerNearRadii = new float[8];
            public readonly float[] LayerMidRadii = new float[8];
            public readonly float[] LayerFarRadii = new float[8];
            public readonly System.Collections.Generic.List<GPUInstancerPrefabPrototype> FloraGpuiKnownPrototypes = new System.Collections.Generic.List<GPUInstancerPrefabPrototype>(96);
            public readonly System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> FloraGpuiMatrices = new System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]>(96);
            public readonly System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, int> FloraGpuiCounts = new System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, int>(96);
            public readonly System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, int> FloraGpuiBufferCapacities = new System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, int>(96);
            public readonly System.Collections.Generic.HashSet<GPUInstancerPrefabPrototype> FloraGpuiInitializedPrototypes = new System.Collections.Generic.HashSet<GPUInstancerPrefabPrototype>();
            public float CachedLayerRadiiCellSize = -1f;
            public WorldChunkStreamingProfile CachedLayerRadiiProfile;
            public readonly ScatterCandidate[] LayerTopCandidatesBuffer = new ScatterCandidate[ScatterLayerCount];
            public readonly bool[] LayerTopValidBuffer = new bool[ScatterLayerCount];
            public readonly int[] LayerPlacementCountsBuffer = new int[ScatterLayerCount];
            public readonly int[] PatternLayerTargetMaxBuffer = new int[ScatterLayerCount];
            public readonly int[] ClusterAccentCountsBuffer = new int[_ClusterAccentRoleCount];
            public readonly float[] ClusterAccentRoleMaxRatioBuffer = new float[_ClusterAccentRoleCount];
            public readonly int[] StructureAccentCountsBuffer = new int[_StructureAccentRoleCount];
            public readonly int[] StructureAccentRoleMaxBuffer = new int[_StructureAccentRoleCount];
            public readonly System.Collections.Generic.Dictionary<string, int>[] LayerFamilyCountsBuffer = CreateLayerFamilyCounters();
            public readonly System.Collections.Generic.Dictionary<string, int>[] LayerBiomeCountsBuffer = CreateLayerFamilyCounters();
            public readonly System.Collections.Generic.Dictionary<HectonBiomeMatrixProfile, int> SampledMatrixProfileCounts = new System.Collections.Generic.Dictionary<HectonBiomeMatrixProfile, int>(16);
            public readonly System.Collections.Generic.Dictionary<string, int> SampledMatrixBiomeCounts = new System.Collections.Generic.Dictionary<string, int>(16);
            public readonly System.Collections.Generic.Dictionary<string, int> SampledBiomeCounts = new System.Collections.Generic.Dictionary<string, int>(16);
            public readonly System.Collections.Generic.Dictionary<string, int> SampledPatternCounts = new System.Collections.Generic.Dictionary<string, int>(8);
            public readonly System.Collections.Generic.Dictionary<string, int> SampledZoneCounts = new System.Collections.Generic.Dictionary<string, int>(8);
            public bool FaunaSnapshotDirty = true;
            public int GridPlacementBucketCount;
            public float MaxRegisteredPlacementSpacingMeters;
            public FastCandidateMap GroundRescueCandidates;
            public FastCandidateMap ClusterRescueCandidates;
            public FastCandidateMap ClusterFertileCandidates;
            public FastCandidateMap ClusterNestCandidates;
            public FastCandidateMap ClusterResourceCandidates;
            public FastCandidateMap ClusterShelterCandidates;
            public FastCandidateMap ClusterHazardCandidates;
            public FastCandidateMap ClusterDebrisCandidates;
            public FastCandidateMap ClusterRockCandidates;
            public FastCandidateMap StructureNaturalCandidates;
            public FastCandidateMap StructureTechCandidates;
            public FastCandidateMap StructureCaveCandidates;
            public FastCandidateMap StructureBioCandidates;
            public FastCandidateMap PassiveSpawnCandidates;
            public FastCandidateMap PredatorSpawnCandidates;

            public void EnsureCellSamplingCapacity(int requiredCapacity)
            {
                if (requiredCapacity <= 0)
                    return;

                EnsureCapacity(ref CellSamplingInputs, requiredCapacity);
                EnsureCapacity(ref CellSamplingOutputs, requiredCapacity);
                EnsureCapacity(ref ScatterBackendCellStates, requiredCapacity);
            }

            public void EnsureCandidateMapsInitialized()
            {
                // COLD ALLOC: editor domain reload can zero struct-backed caches before menu-driven preview calls.
                EnsureCandidateMapInitialized(ref GroundRescueCandidates, 512);
                EnsureCandidateMapInitialized(ref ClusterRescueCandidates, 512);
                EnsureCandidateMapInitialized(ref ClusterFertileCandidates, 192);
                EnsureCandidateMapInitialized(ref ClusterNestCandidates, 128);
                EnsureCandidateMapInitialized(ref ClusterResourceCandidates, 192);
                EnsureCandidateMapInitialized(ref ClusterShelterCandidates, 192);
                EnsureCandidateMapInitialized(ref ClusterHazardCandidates, 128);
                EnsureCandidateMapInitialized(ref ClusterDebrisCandidates, 192);
                EnsureCandidateMapInitialized(ref ClusterRockCandidates, 128);
                EnsureCandidateMapInitialized(ref StructureNaturalCandidates, 128);
                EnsureCandidateMapInitialized(ref StructureTechCandidates, 128);
                EnsureCandidateMapInitialized(ref StructureCaveCandidates, 192);
                EnsureCandidateMapInitialized(ref StructureBioCandidates, 128);
                EnsureCandidateMapInitialized(ref PassiveSpawnCandidates, 128);
                EnsureCandidateMapInitialized(ref PredatorSpawnCandidates, 96);
            }

            public void Dispose()
            {
                if (CellSamplingInputs.IsCreated)
                    CellSamplingInputs.Dispose();
                if (CellSamplingOutputs.IsCreated)
                    CellSamplingOutputs.Dispose();
                if (ScatterBackendCellStates.IsCreated)
                    ScatterBackendCellStates.Dispose();

                GroundRescueCandidates.Dispose();
                ClusterRescueCandidates.Dispose();
                ClusterFertileCandidates.Dispose();
                ClusterNestCandidates.Dispose();
                ClusterResourceCandidates.Dispose();
                ClusterShelterCandidates.Dispose();
                ClusterHazardCandidates.Dispose();
                ClusterDebrisCandidates.Dispose();
                ClusterRockCandidates.Dispose();
                StructureNaturalCandidates.Dispose();
                StructureTechCandidates.Dispose();
                StructureCaveCandidates.Dispose();
                StructureBioCandidates.Dispose();
                PassiveSpawnCandidates.Dispose();
                PredatorSpawnCandidates.Dispose();

                DesiredPlacements.Clear();
                RetainedPlacements.Clear();
                PlacementLastSeenTimes.Clear();
                PlacementPool.Clear();
                StructureWindowCounts.Clear();
                SpawnWindowCounts.Clear();
                CandidateBuffer.Clear();
                RemovalBuffer.Clear();
                FaunaAnchorBuffer.Clear();
                ClusterAccentOrderedCandidates.Clear();
                ClusterOrderedCandidates.Clear();
                ExactClusterOrderedCandidates.Clear();
                GroundOrderedCandidates.Clear();
                WindowOrderedCandidates.Clear();
                PatternStructureOrderedCandidates.Clear();
                StructureAccentOrderedCandidates.Clear();
                PatternSpawnOrderedCandidates.Clear();
                PatternSpawnPassiveOrderedCandidates.Clear();
                PatternSpawnPredatorOrderedCandidates.Clear();
                RuntimeRuleBuffer.Clear();
                OccupiedCellBuffer.Clear();
                int gridBucketCount = GridPlacementBuckets.Count;
                for (int i = 0; i < gridBucketCount; i++)
                    GridPlacementBuckets[i].Clear();
                GridPlacements.Clear();
                StructureRescueCandidates.Clear();
                SpawnRescueCandidates.Clear();
                PrefabWarmupCounts.Clear();
                PrefabWarmupPrefabs.Clear();
                PrefabWarmupFamilyIds.Clear();
                PrefabCreateAllowances.Clear();
                PreferredFamilyPlacementCounts.Clear();
                Array.Clear(PatternProfileCache, 0, PatternProfileCache.Length);
                BiomeContextCache.Clear();
                HasCachedPatternQuota = false;
                CachedPatternQuotaPattern = default;
                CachedPatternQuotaBiomeProfile = null;
                CachedPatternClusterRatioStart = 0;
                CachedPatternPassiveSpawnMin = 0;
                CachedPatternPredatorSpawnMax = 0;
                HasCachedBudgetScales = false;
                CachedBudgetScalePatternProfile = null;
                CachedBudgetScaleBiomeContext = null;
                CachedGroundBudgetScale = 0f;
                CachedClusterBudgetScale = 0f;
                CachedStructureBudgetScale = 0f;
                CachedSpawnBudgetScale = 0f;
                Array.Clear(LayerNearRadii, 0, LayerNearRadii.Length);
                Array.Clear(LayerMidRadii, 0, LayerMidRadii.Length);
                Array.Clear(LayerFarRadii, 0, LayerFarRadii.Length);
                FloraGpuiKnownPrototypes.Clear();
                FloraGpuiMatrices.Clear();
                FloraGpuiCounts.Clear();
                FloraGpuiBufferCapacities.Clear();
                FloraGpuiInitializedPrototypes.Clear();
                CachedLayerRadiiCellSize = -1f;
                CachedLayerRadiiProfile = null;
                Array.Clear(LayerTopCandidatesBuffer, 0, LayerTopCandidatesBuffer.Length);
                Array.Clear(LayerTopValidBuffer, 0, LayerTopValidBuffer.Length);
                Array.Clear(LayerPlacementCountsBuffer, 0, LayerPlacementCountsBuffer.Length);
                Array.Clear(PatternLayerTargetMaxBuffer, 0, PatternLayerTargetMaxBuffer.Length);
                Array.Clear(ClusterAccentCountsBuffer, 0, ClusterAccentCountsBuffer.Length);
                Array.Clear(ClusterAccentRoleMaxRatioBuffer, 0, ClusterAccentRoleMaxRatioBuffer.Length);
                Array.Clear(StructureAccentCountsBuffer, 0, StructureAccentCountsBuffer.Length);
                Array.Clear(StructureAccentRoleMaxBuffer, 0, StructureAccentRoleMaxBuffer.Length);
                ClearDictionaryArray(LayerFamilyCountsBuffer);
                ClearDictionaryArray(LayerBiomeCountsBuffer);
                SampledMatrixProfileCounts.Clear();
                SampledMatrixBiomeCounts.Clear();
                SampledBiomeCounts.Clear();
                SampledPatternCounts.Clear();
                SampledZoneCounts.Clear();
                FaunaSnapshotDirty = true;
                GridPlacementBucketCount = 0;
                MaxRegisteredPlacementSpacingMeters = 0f;
            }

            private static void EnsureCapacity<T>(ref NativeArray<T> array, int requiredCapacity) where T : struct
            {
                if (array.IsCreated && array.Length >= requiredCapacity)
                    return;

                if (array.IsCreated)
                    array.Dispose();

                // COLD ALLOC: NativeArray<T>[NextPowerOfTwo(requiredCapacity)] — scatter sampling working memory — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                array = new NativeArray<T>(Mathf.NextPowerOfTwo(requiredCapacity), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            private static void EnsureCandidateMapInitialized(ref FastCandidateMap map, int capacity)
            {
                if (map.IsInitialized)
                    return;

                map.Init(capacity * 2, Allocator.Persistent);
            }
        }
    }
}
