using System;
using GPUInstancer;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        internal sealed class ScatterWorkingMemory : IDisposable
        {
            private const int InitialGridPlacementNativeCapacity = 16384;
            private const int InitialCandidateAcceptanceBatchCapacity = 256;
            private const string NativeMemoryOwner = "WorldProceduralScatterDirector.ScatterWorkingMemory";
            private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // COLD ALLOC: HashSet<long>[256] - one-shot strict substrate missing logs per scatter chunk - owner: WorldProceduralScatterDirector.ScatterWorkingMemory
            public readonly System.Collections.Generic.HashSet<long> StrictSubstrateMissingLoggedChunks = new System.Collections.Generic.HashSet<long>(256);
#endif
            public readonly System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<ScatterPlacement>> GridPlacements = new System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<ScatterPlacement>>(512);
            public readonly System.Collections.Generic.List<System.Collections.Generic.List<ScatterPlacement>> GridPlacementBuckets = new System.Collections.Generic.List<System.Collections.Generic.List<ScatterPlacement>>(512);
            public NativeList<ScatterPlacementSpatialMetadata> GridPlacementSpatialMetadata;
            public NativeParallelMultiHashMap<int, float3> GridPlacementPositionBuckets;
            public NativeParallelMultiHashMap<int, int> GridPlacementMetadataBuckets;
            public NativeArray<int> CandidateAcceptanceResult;
            public NativeList<ScatterCellCandidateAcceptanceInput> CandidateAcceptanceBatchInputs;
            public NativeList<byte> CandidateAcceptanceBatchResults;
            public NativeList<ScatterPlacementSpatialMetadata> CandidateAcceptanceBatchPendingMetadata;
            public NativeParallelMultiHashMap<int, float3> CandidateAcceptanceBatchPendingPositionBuckets;
            public NativeParallelMultiHashMap<int, int> CandidateAcceptanceBatchPendingMetadataBuckets;
            public NativeArray<int> CandidateAcceptanceClusterAccentCountsScratch;
            public NativeArray<int> CandidateAcceptanceStructureAccentCountsScratch;
            public NativeArray<float> CandidateAcceptanceClusterAccentRoleMaxRatiosScratch;
            public NativeArray<int> CandidateAcceptanceStructureAccentRoleMaxCountsScratch;
            public readonly System.Collections.Generic.Dictionary<long, ScatterCandidate> StructureRescueCandidates = new System.Collections.Generic.Dictionary<long, ScatterCandidate>(64);
            public readonly System.Collections.Generic.Dictionary<long, ScatterCandidate> SpawnRescueCandidates = new System.Collections.Generic.Dictionary<long, ScatterCandidate>(64);
            public readonly System.Collections.Generic.Dictionary<int, int> PrefabWarmupCounts = new System.Collections.Generic.Dictionary<int, int>(32);
            public readonly System.Collections.Generic.Dictionary<int, GameObject> PrefabWarmupPrefabs = new System.Collections.Generic.Dictionary<int, GameObject>(32);
            public readonly System.Collections.Generic.Dictionary<int, int> PrefabWarmupFamilyHashes = new System.Collections.Generic.Dictionary<int, int>(32);
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
            public bool GridPlacementNativeOverflowed;
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

            public ScatterWorkingMemory()
            {
                // COLD ALLOC: NativeList<ScatterPlacementSpatialMetadata>[16384] — native scatter spacing cache — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                GridPlacementSpatialMetadata = new NativeList<ScatterPlacementSpatialMetadata>(InitialGridPlacementNativeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelMultiHashMap<int, float3>[16384] — native scatter cell position buckets — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                GridPlacementPositionBuckets = new NativeParallelMultiHashMap<int, float3>(InitialGridPlacementNativeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelMultiHashMap<int, int>[16384] — native scatter cell metadata buckets — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                GridPlacementMetadataBuckets = new NativeParallelMultiHashMap<int, int>(InitialGridPlacementNativeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeArray<int>[1] — candidate acceptance result scratch — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                CandidateAcceptanceResult = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CandidateAcceptanceBatchInputs = new NativeList<ScatterCellCandidateAcceptanceInput>(InitialCandidateAcceptanceBatchCapacity, Allocator.Persistent);
                CandidateAcceptanceBatchResults = new NativeList<byte>(InitialCandidateAcceptanceBatchCapacity, Allocator.Persistent);
                CandidateAcceptanceBatchPendingMetadata = new NativeList<ScatterPlacementSpatialMetadata>(InitialCandidateAcceptanceBatchCapacity, Allocator.Persistent);
                CandidateAcceptanceBatchPendingPositionBuckets = new NativeParallelMultiHashMap<int, float3>(InitialCandidateAcceptanceBatchCapacity, Allocator.Persistent);
                CandidateAcceptanceBatchPendingMetadataBuckets = new NativeParallelMultiHashMap<int, int>(InitialCandidateAcceptanceBatchCapacity, Allocator.Persistent);
                CandidateAcceptanceClusterAccentCountsScratch = new NativeArray<int>(_ClusterAccentRoleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CandidateAcceptanceStructureAccentCountsScratch = new NativeArray<int>(_StructureAccentRoleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CandidateAcceptanceClusterAccentRoleMaxRatiosScratch = new NativeArray<float>(_ClusterAccentRoleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CandidateAcceptanceStructureAccentRoleMaxCountsScratch = new NativeArray<int>(_StructureAccentRoleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterNativeMemorySentinel();
            }

            public void EnsureCellSamplingCapacity(int requiredCapacity)
            {
                if (requiredCapacity <= 0)
                    return;

                EnsureCapacity(ref CellSamplingInputs, requiredCapacity, nameof(CellSamplingInputs));
                EnsureCapacity(ref CellSamplingOutputs, requiredCapacity, nameof(CellSamplingOutputs));
                EnsureCapacity(ref ScatterBackendCellStates, requiredCapacity, nameof(ScatterBackendCellStates));
            }

            public void ResetGridPlacementSpatialCache()
            {
                if (GridPlacementSpatialMetadata.IsCreated)
                    GridPlacementSpatialMetadata.Clear();
                if (GridPlacementPositionBuckets.IsCreated)
                    GridPlacementPositionBuckets.Clear();
                if (GridPlacementMetadataBuckets.IsCreated)
                    GridPlacementMetadataBuckets.Clear();
                if (CandidateAcceptanceResult.IsCreated)
                    CandidateAcceptanceResult[0] = 1;
                if (CandidateAcceptanceBatchInputs.IsCreated)
                    CandidateAcceptanceBatchInputs.Clear();
                if (CandidateAcceptanceBatchResults.IsCreated)
                    CandidateAcceptanceBatchResults.Clear();
                if (CandidateAcceptanceBatchPendingMetadata.IsCreated)
                    CandidateAcceptanceBatchPendingMetadata.Clear();
                if (CandidateAcceptanceBatchPendingPositionBuckets.IsCreated)
                    CandidateAcceptanceBatchPendingPositionBuckets.Clear();
                if (CandidateAcceptanceBatchPendingMetadataBuckets.IsCreated)
                    CandidateAcceptanceBatchPendingMetadataBuckets.Clear();
                if (CandidateAcceptanceClusterAccentCountsScratch.IsCreated)
                    ClearNativeArray(CandidateAcceptanceClusterAccentCountsScratch, 0);
                if (CandidateAcceptanceStructureAccentCountsScratch.IsCreated)
                    ClearNativeArray(CandidateAcceptanceStructureAccentCountsScratch, 0);
                if (CandidateAcceptanceClusterAccentRoleMaxRatiosScratch.IsCreated)
                    ClearNativeArray(CandidateAcceptanceClusterAccentRoleMaxRatiosScratch, 0f);
                if (CandidateAcceptanceStructureAccentRoleMaxCountsScratch.IsCreated)
                    ClearNativeArray(CandidateAcceptanceStructureAccentRoleMaxCountsScratch, 0);

                GridPlacementNativeOverflowed = false;
            }

            public bool PrepareScatterCellCandidateAcceptanceBatch(int requiredCapacity)
            {
                if (!CandidateAcceptanceBatchInputs.IsCreated ||
                    !CandidateAcceptanceBatchResults.IsCreated ||
                    !CandidateAcceptanceBatchPendingMetadata.IsCreated ||
                    !CandidateAcceptanceBatchPendingPositionBuckets.IsCreated ||
                    !CandidateAcceptanceBatchPendingMetadataBuckets.IsCreated ||
                    !CandidateAcceptanceClusterAccentCountsScratch.IsCreated ||
                    !CandidateAcceptanceStructureAccentCountsScratch.IsCreated ||
                    !CandidateAcceptanceClusterAccentRoleMaxRatiosScratch.IsCreated ||
                    !CandidateAcceptanceStructureAccentRoleMaxCountsScratch.IsCreated)
                {
                    return false;
                }

                if (requiredCapacity <= 0)
                {
                    CandidateAcceptanceBatchInputs.Clear();
                    CandidateAcceptanceBatchResults.Clear();
                    CandidateAcceptanceBatchPendingMetadata.Clear();
                    CandidateAcceptanceBatchPendingPositionBuckets.Clear();
                    CandidateAcceptanceBatchPendingMetadataBuckets.Clear();
                    return true;
                }

                if (CandidateAcceptanceBatchInputs.Capacity < requiredCapacity)
                {
                    CandidateAcceptanceBatchInputs.Capacity = math.max(requiredCapacity, CandidateAcceptanceBatchInputs.Capacity * 2);
                    NativeMemorySentinel.RefreshNativeList(CandidateAcceptanceBatchInputs, NativeMemoryOwner, nameof(CandidateAcceptanceBatchInputs));
                }

                if (CandidateAcceptanceBatchResults.Capacity < requiredCapacity)
                {
                    CandidateAcceptanceBatchResults.Capacity = math.max(requiredCapacity, CandidateAcceptanceBatchResults.Capacity * 2);
                    NativeMemorySentinel.RefreshNativeList(CandidateAcceptanceBatchResults, NativeMemoryOwner, nameof(CandidateAcceptanceBatchResults));
                }

                if (CandidateAcceptanceBatchPendingMetadata.Capacity < requiredCapacity)
                {
                    CandidateAcceptanceBatchPendingMetadata.Capacity = math.max(requiredCapacity, CandidateAcceptanceBatchPendingMetadata.Capacity * 2);
                    NativeMemorySentinel.RefreshNativeList(CandidateAcceptanceBatchPendingMetadata, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingMetadata));
                }

                if (CandidateAcceptanceBatchPendingPositionBuckets.Capacity < requiredCapacity)
                {
                    CandidateAcceptanceBatchPendingPositionBuckets.Capacity = math.max(requiredCapacity, CandidateAcceptanceBatchPendingPositionBuckets.Capacity * 2);
                    NativeMemorySentinel.RefreshNativeParallelMultiHashMap(CandidateAcceptanceBatchPendingPositionBuckets, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingPositionBuckets));
                }

                if (CandidateAcceptanceBatchPendingMetadataBuckets.Capacity < requiredCapacity)
                {
                    CandidateAcceptanceBatchPendingMetadataBuckets.Capacity = math.max(requiredCapacity, CandidateAcceptanceBatchPendingMetadataBuckets.Capacity * 2);
                    NativeMemorySentinel.RefreshNativeParallelMultiHashMap(CandidateAcceptanceBatchPendingMetadataBuckets, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingMetadataBuckets));
                }

                CandidateAcceptanceBatchInputs.Clear();
                CandidateAcceptanceBatchInputs.ResizeUninitialized(requiredCapacity);
                CandidateAcceptanceBatchResults.Clear();
                CandidateAcceptanceBatchResults.ResizeUninitialized(requiredCapacity);
                CandidateAcceptanceBatchPendingMetadata.Clear();
                CandidateAcceptanceBatchPendingPositionBuckets.Clear();
                CandidateAcceptanceBatchPendingMetadataBuckets.Clear();
                return true;
            }

            public bool TryRegisterGridPlacement(ScatterPlacement placement)
            {
                if (placement == null)
                    return false;

                if (!GridPlacementSpatialMetadata.IsCreated ||
                    !GridPlacementPositionBuckets.IsCreated ||
                    !GridPlacementMetadataBuckets.IsCreated)
                {
                    GridPlacementNativeOverflowed = true;
                    return false;
                }

                int requiredCapacity = GridPlacementSpatialMetadata.Length + 1;
                if (!EnsureGridPlacementSpatialCapacity(requiredCapacity))
                    return false;

                int metadataIndex = GridPlacementSpatialMetadata.Length;
                float3 position = new float3(placement.Position.x, placement.Position.y, placement.Position.z);
                GridPlacementSpatialMetadata.AddNoResize(new ScatterPlacementSpatialMetadata(
                    position,
                    placement.EffectiveSpacing,
                    placement.Family != null ? placement.Family.FamilyHash : 0,
                    placement.Family != null ? (int)placement.Family.scatterLayer : 0,
                    placement.Family != null ? (int)placement.Family.proceduralDomain : 0,
                    (byte)ResolveFloraBudgetClass(placement.Family)));

                int cellKey = ComposeScatterGridNativeKey(placement.CellX, placement.CellZ);
                GridPlacementPositionBuckets.Add(cellKey, position);
                GridPlacementMetadataBuckets.Add(cellKey, metadataIndex);
                return true;
            }

            private bool EnsureGridPlacementSpatialCapacity(int requiredCapacity)
            {
                if (!GridPlacementSpatialMetadata.IsCreated ||
                    !GridPlacementPositionBuckets.IsCreated ||
                    !GridPlacementMetadataBuckets.IsCreated)
                {
                    GridPlacementNativeOverflowed = true;
                    return false;
                }

                if (requiredCapacity <= GridPlacementSpatialMetadata.Capacity &&
                    requiredCapacity <= GridPlacementPositionBuckets.Capacity &&
                    requiredCapacity <= GridPlacementMetadataBuckets.Capacity)
                {
                    return true;
                }

                int newCapacity = math.max(
                    InitialGridPlacementNativeCapacity,
                    math.max(requiredCapacity, GridPlacementSpatialMetadata.Capacity * 2));
                GridPlacementSpatialMetadata.Capacity = newCapacity;
                GridPlacementPositionBuckets.Capacity = newCapacity;
                GridPlacementMetadataBuckets.Capacity = newCapacity;
                NativeMemorySentinel.RefreshNativeList(GridPlacementSpatialMetadata, NativeMemoryOwner, nameof(GridPlacementSpatialMetadata));
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(GridPlacementPositionBuckets, NativeMemoryOwner, nameof(GridPlacementPositionBuckets));
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(GridPlacementMetadataBuckets, NativeMemoryOwner, nameof(GridPlacementMetadataBuckets));
                GridPlacementNativeOverflowed = false;
                return true;
            }

            private static void ClearNativeArray<T>(NativeArray<T> array, T value) where T : struct
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = value;
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
                DisposeNativeArray(ref CellSamplingInputs);
                DisposeNativeArray(ref CellSamplingOutputs);
                DisposeNativeArray(ref ScatterBackendCellStates);
                DisposeNativeList(ref GridPlacementSpatialMetadata, nameof(GridPlacementSpatialMetadata));
                DisposeNativeParallelMultiHashMap(ref GridPlacementPositionBuckets, nameof(GridPlacementPositionBuckets));
                DisposeNativeParallelMultiHashMap(ref GridPlacementMetadataBuckets, nameof(GridPlacementMetadataBuckets));
                DisposeNativeArray(ref CandidateAcceptanceResult);
                DisposeNativeList(ref CandidateAcceptanceBatchInputs, nameof(CandidateAcceptanceBatchInputs));
                DisposeNativeList(ref CandidateAcceptanceBatchResults, nameof(CandidateAcceptanceBatchResults));
                DisposeNativeList(ref CandidateAcceptanceBatchPendingMetadata, nameof(CandidateAcceptanceBatchPendingMetadata));
                DisposeNativeParallelMultiHashMap(ref CandidateAcceptanceBatchPendingPositionBuckets, nameof(CandidateAcceptanceBatchPendingPositionBuckets));
                DisposeNativeParallelMultiHashMap(ref CandidateAcceptanceBatchPendingMetadataBuckets, nameof(CandidateAcceptanceBatchPendingMetadataBuckets));
                DisposeNativeArray(ref CandidateAcceptanceClusterAccentCountsScratch);
                DisposeNativeArray(ref CandidateAcceptanceStructureAccentCountsScratch);
                DisposeNativeArray(ref CandidateAcceptanceClusterAccentRoleMaxRatiosScratch);
                DisposeNativeArray(ref CandidateAcceptanceStructureAccentRoleMaxCountsScratch);

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
                PrefabWarmupFamilyHashes.Clear();
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
                ReleaseFloraGpuiMatrices();
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
                GridPlacementNativeOverflowed = false;
                MaxRegisteredPlacementSpacingMeters = 0f;
            }

            private void RegisterNativeMemorySentinel()
            {
                NativeMemorySentinel.RegisterNativeList(GridPlacementSpatialMetadata, NativeMemoryOwner, nameof(GridPlacementSpatialMetadata), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeParallelMultiHashMap(GridPlacementPositionBuckets, NativeMemoryOwner, nameof(GridPlacementPositionBuckets), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeParallelMultiHashMap(GridPlacementMetadataBuckets, NativeMemoryOwner, nameof(GridPlacementMetadataBuckets), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeArray(CandidateAcceptanceResult, NativeMemoryOwner, nameof(CandidateAcceptanceResult), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeList(CandidateAcceptanceBatchInputs, NativeMemoryOwner, nameof(CandidateAcceptanceBatchInputs), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeList(CandidateAcceptanceBatchResults, NativeMemoryOwner, nameof(CandidateAcceptanceBatchResults), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeList(CandidateAcceptanceBatchPendingMetadata, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingMetadata), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeParallelMultiHashMap(CandidateAcceptanceBatchPendingPositionBuckets, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingPositionBuckets), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeParallelMultiHashMap(CandidateAcceptanceBatchPendingMetadataBuckets, NativeMemoryOwner, nameof(CandidateAcceptanceBatchPendingMetadataBuckets), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeArray(CandidateAcceptanceClusterAccentCountsScratch, NativeMemoryOwner, nameof(CandidateAcceptanceClusterAccentCountsScratch), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeArray(CandidateAcceptanceStructureAccentCountsScratch, NativeMemoryOwner, nameof(CandidateAcceptanceStructureAccentCountsScratch), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeArray(CandidateAcceptanceClusterAccentRoleMaxRatiosScratch, NativeMemoryOwner, nameof(CandidateAcceptanceClusterAccentRoleMaxRatiosScratch), NativeMemoryLifetime);
                NativeMemorySentinel.RegisterNativeArray(CandidateAcceptanceStructureAccentRoleMaxCountsScratch, NativeMemoryOwner, nameof(CandidateAcceptanceStructureAccentRoleMaxCountsScratch), NativeMemoryLifetime);
            }

            private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(array);
                array.Dispose();
                array = default;
            }

            private static void DisposeNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged
            {
                if (!list.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
                list.Dispose();
                list = default;
            }

            private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(ref NativeParallelMultiHashMap<TKey, TValue> map, string label)
                where TKey : unmanaged, IEquatable<TKey>
                where TValue : unmanaged
            {
                if (!map.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner, label);
                map.Dispose();
                map = default;
            }

            private static void EnsureCapacity<T>(ref NativeArray<T> array, int requiredCapacity, string label) where T : struct
            {
                if (array.IsCreated && array.Length >= requiredCapacity)
                    return;

                if (array.IsCreated)
                    DisposeNativeArray(ref array);

                // COLD ALLOC: NativeArray<T>[NextPowerOfTwo(requiredCapacity)] — scatter sampling working memory — owner: WorldProceduralScatterDirector.ScatterWorkingMemory
                array = new NativeArray<T>(Mathf.NextPowerOfTwo(requiredCapacity), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
            }

            private static void EnsureCandidateMapInitialized(ref FastCandidateMap map, int capacity)
            {
                if (map.IsInitialized)
                    return;

                map.Init(capacity * 2, Allocator.Persistent);
            }

            private void ReleaseFloraGpuiMatrices()
            {
                System.Collections.Generic.Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]>.Enumerator enumerator = FloraGpuiMatrices.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Matrix4x4[] matrices = enumerator.Current.Value;
                    if (matrices != null)
                        System.Buffers.ArrayPool<Matrix4x4>.Shared.Return(matrices, clearArray: false);
                }
            }
        }
    }
}
