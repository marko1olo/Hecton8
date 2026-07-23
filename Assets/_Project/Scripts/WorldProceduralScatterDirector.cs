using System;
using System.Collections.Generic;
using System.Diagnostics;
using GPUInstancer;
using Hecton8.Dev;
using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
using CandidateMap = Hecton8.World.WorldProceduralScatterDirector.FastCandidateMap;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4036)]
    public sealed partial class WorldProceduralScatterDirector : MonoBehaviour, ITickable, ISlowTickable, IUpdatable, ILateFrameTickable, IGameBootstrapperEventListener, IWorldGenService, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const float StartupScatterStabilizationDelaySeconds = 2f;
        private const int MaxRegisteredScatterDirectors = 4;
        private const string TectonicSpineBiomeFamilyId = "biome.family.tectonic_spine";

        private static WorldProceduralScatterDirector s_activeRuntimeInstance;

        internal static WorldProceduralScatterDirector ActiveRuntimeInstance => s_activeRuntimeInstance;

        // COLD ALLOC: RegistryBucket<WorldProceduralScatterDirector>[4] - active scatter directors for bootstrap lookup without scene scans - owner: WorldProceduralScatterDirector
        private static readonly RegistryBucket<WorldProceduralScatterDirector> _registeredScatterDirectors = new RegistryBucket<WorldProceduralScatterDirector>(MaxRegisteredScatterDirectors);

        private static float RuntimeNowSeconds()
        {
            return HasRuntimeScatterOwner() ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
        }

        private static bool HasRuntimeScatterOwner()
        {
            WorldProceduralScatterDirector owner = s_activeRuntimeInstance;
            return owner != null && owner._runtimeScatterCallbacksActive;
        }

        /// <summary>
        /// True once the world-generation owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.WorldGen, this);

        /// <summary>
        /// Number of packed biome influence cells published to the global scatter shader buffer.
        /// </summary>
        public int DebugBiomeInfluenceGridCells => _debugBiomeInfluenceGridCells;

        /// <summary>
        /// Flora GPUI placements rejected by the coarse CPU frustum pass during the last reconcile.
        /// </summary>
        public int DebugFloraGpuiFrustumRejected => _debugFloraGpuiFrustumRejected;

        /// <summary>
        /// Current capacity of the packed biome influence GPU buffer.
        /// </summary>
        public int DebugBiomeInfluenceGpuBufferCapacity => _debugBiomeInfluenceGpuBufferCapacity;
        internal float CurrentSpawnBudgetScale => math.max(0.35f, _debugPatternSpawnBudgetScale);
        internal float CurrentFaunaActivationScale
        {
            get
            {
                if (chunkStreamingProfile == null)
                    return 1f;

                WorldChunkStreamingProfile.LayerProfile layerProfile = chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Fauna);
                return math.lerp(0.7f, 1.45f, math.saturate(layerProfile.maxActivationsPerTick / 24f));
            }
        }
        private const string ScatterRootName = "__PROCEDURAL_SCATTER_WORLD";
        private const string GeneratedGeologyRootName = "__GENERATED_GEOLOGY";
        private const string CandidateMapCapacityExceededWarning =
            "[CandidateMap] Capacity exceeded. Increase capacity or reduce candidates.";
        private const string CandidateMapNearCapacityWarning =
            "[CandidateMap] Approaching capacity. Increase capacity or reduce candidates.";
        private const string PlacementPoolExhaustedWarning =
            "[WorldScatter] Placement pool exhausted. Candidate dropped; increase placement pool capacity.";
        private const int ScatterLayerCount = 4;
        private const int ProxyOptimizationRefreshLowTierBudget = 8;
        private const int ProxyOptimizationRefreshUltraTierBudget = 64;
        private const string ScatterPatternNoneLabel = "None";
        private const string ScatterPatternSedimentResourcesLabel = "SedimentResources";
        private const string ScatterPatternFertileShallowsLabel = "FertileShallows";
        private const string ScatterPatternReefNavigationLabel = "ReefNavigation";
        private const string ScatterPatternIndustrialServiceLabel = "IndustrialService";
        private const string ScatterPatternBrineToxicLabel = "BrineToxic";
        private const string ScatterPatternVolcanicPressureLabel = "VolcanicPressure";
        private const string ScatterPatternRiftHazardLabel = "RiftHazard";
        private const string ScatterPatternAbyssSparseLabel = "AbyssSparse";
        private const string ScatterPatternLandmarkCorridorLabel = "LandmarkCorridor";
        private const string ScatterLayerTerrainLodLabel = "TerrainLod";
        private const string ScatterLayerFloraLabel = "Flora";
        private const string ScatterLayerDebrisLabel = "Debris";
        private const string ScatterLayerResourcesLabel = "Resources";
        private const string ScatterLayerFaunaLabel = "Fauna";
        private const string ScatterLayerConstructionLabel = "Construction";
        private const string ScatterLayerLargeThreatsLabel = "LargeThreats";
        // Bump when the reconcile-signature field contract changes.
        private const int ScatterPlacementSyncSignatureVersion = 2;
        private const int MaxFloraInstancesPerStreamCellPerBiome = 4096;
        private const float FloraScatterMaxTiltAngleDegrees = 30f;
        private const float ScatterMinimumSurfaceNormalUpDot = 0.2f;
        private const float DeterministicClutterSpawnThreshold = 0.99f;
        private const float FloraMicroClusterPatchThreshold = 0.36f;
        private const float FloraMacroClusterPatchThreshold = 0.42f;
        private const float FloraFallbackClusterNoiseScale = 0.009f;
        private static bool _candidateMapCapacityExceededWarningLogged;
        private static bool _candidateMapNearCapacityWarningLogged;
        private static bool _placementPoolExhaustedWarningLogged;
        private int _observerAbsolutePositionCacheFrame = -1;
        private bool _observerAbsolutePositionCacheValid;
        private Vector3 _observerAbsolutePositionCache;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IObjectPoolService _cachedObjectPool;
        private bool _runtimeScatterCallbacksActive;
        private bool _registeredHotSwapListener;
#if UNITY_EDITOR
        private static bool _assemblyReloadHookRegistered;
#endif
        private const int _ClusterAccentRoleCount = 8;
        private const int _StructureAccentRoleCount = 5;
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityDefault =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityFertileShallows =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityReefNavigation =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityIndustrialService =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityBrineToxic =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityVolcanicPressure =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityRiftHazard =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityAbyssSparse =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.StructureAccentRole[] _PatternAccentPriorityLandmarkCorridor =
        {
            WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
            WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
            WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
            WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityDefault =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityFertileShallows =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
            WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityReefNavigation =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
            WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPrioritySedimentResources =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
            WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityIndustrialService =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityBrineToxic =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
            WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityVolcanicPressure =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
            WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityRiftHazard =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityAbyssSparse =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
        };
        private static readonly WorldPrefabFamilyProfile.ClusterAccentRole[] _PatternClusterAccentPriorityLandmarkCorridor =
        {
            WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
            WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
            WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
        };
        private static readonly ProfilerMarker _scatterTickProfilerMarker = new("WorldScatter.Tick");
        private static readonly ProfilerMarker _scatterSlowTickProfilerMarker = new("WorldScatter.SlowTick");
        private static readonly ProfilerMarker _scatterRebuildDispatcherProfilerMarker = new("WorldScatter.Rebuild.Dispatch");
        private static readonly ProfilerMarker _scatterStateMachineProfilerMarker = new("WorldScatter.StateMachine");
        private static readonly ProfilerMarker _scatterSamplingBeginProfilerMarker = new("WorldScatter.Sampling.Begin");
        private static readonly ProfilerMarker _scatterSamplingInputBuildProfilerMarker = new("WorldScatter.Sampling.BuildInputs");
        private static readonly ProfilerMarker _scatterSamplingScheduleProfilerMarker = new("WorldScatter.Sampling.Schedule");
        private static readonly ProfilerMarker _scatterProcessingProfilerMarker = new("WorldScatter.Processing.Total");
        private static readonly ProfilerMarker _scatterProcessingCellEvaluationProfilerMarker = new("WorldScatter.Processing.Cells");
        private static readonly ProfilerMarker _scatterProcessingRescueProfilerMarker = new("WorldScatter.Processing.Rescue");
        private static readonly ProfilerMarker _scatterProcessingRestoreProfilerMarker = new("WorldScatter.Processing.Restore");
        private static readonly ProfilerMarker _scatterPendingReconcileProfilerMarker = new("WorldScatter.Reconcile.Pending");
        private static readonly ProfilerMarker _scatterReconcileProfilerMarker = new("WorldScatter.Reconcile.Total");
        private static readonly ProfilerMarker _scatterReconcileCleanupProfilerMarker = new("WorldScatter.Reconcile.Cleanup");
        private static readonly ProfilerMarker _scatterReconcileSpawnProfilerMarker = new("WorldScatter.Reconcile.Spawn");
        private static readonly ProfilerMarker _scatterReconcileFaunaProfilerMarker = new("WorldScatter.Reconcile.Fauna");
        private static readonly int _ScatterBiomeInfluenceGridId = Shader.PropertyToID("_HectonScatterBiomeInfluenceGrid");
        private static readonly int _ScatterBiomeInfluenceGridCountId = Shader.PropertyToID("_HectonScatterBiomeInfluenceGridCount");
        private static readonly int _ScatterBiomeInfluenceGridOriginId = Shader.PropertyToID("_HectonScatterBiomeInfluenceGridOrigin");
        private static readonly int _ScatterBiomeInfluenceGridParamsId = Shader.PropertyToID("_HectonScatterBiomeInfluenceGridParams");
        private enum ScatterState
        {
            Idle,
            Sampling,
            Processing,
            Spawning
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldProceduralFieldSampler fieldSampler;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private WorldProceduralFillDirector proceduralFillDirector;
        [SerializeField] private WorldProceduralPatternCatalog patternCatalog;
        [SerializeField] private WorldProceduralBiomeFamilyContextCatalog biomeContextCatalog;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;
        [SerializeField] private WorldFaunaSpawnRegistry faunaSpawnRegistry;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;
        [SerializeField] private WorldGenerativeGeologyService generativeGeologyService;
        [SerializeField] private GPUInstancerPrefabManager floraGpuiManager;

        [Header("Scatter Grid")]
        [SerializeField] private float cellSize = 22f;
        [SerializeField] private int radiusCells = 7;
        [SerializeField] private int groundPlacementsPerCell = 2;
        [SerializeField] private int clusterPlacementsPerCell = 1;
        [SerializeField] private int structureCellStride = 2;
        [SerializeField] private int structurePlacementsPerWindow = 1;
        [SerializeField] private int spawnCellStride = 3;
        [SerializeField] private int spawnPlacementsPerWindow = 1;
        [SerializeField] private float surfaceYOffset = 0.2f;
        [SerializeField] private bool enableFloraGpuiCpuFrustumCulling = true;
        [SerializeField, Min(0f)] private float floraGpuiFrustumPaddingMeters = 24f;
        [SerializeField] private float missingPlacementGraceSeconds = 8f;
        [SerializeField] private bool waitForGameBootstrapper = true;
        [SerializeField, Tooltip("Caps bootstrap prime scatter radius so pre-activation warmup does not block on the full runtime sampling window.")]
        private int bootstrapPrimeRadiusCells = 3;
        [SerializeField] private float scatterRefreshDistanceThreshold = 8f;
        [SerializeField] private bool enableForcedScatterRefresh = false;
        [SerializeField] private float scatterForcedRefreshInterval = 0f;
        [SerializeField] private bool spreadInitialScatterWarmupAcrossTicks = true;
        [Tooltip("Maksimum obektov, kotorye scatter razreshaet dogret v pule za odin startup rebuild.")]
        #pragma warning disable CS0414
        [SerializeField] private int maxPoolWarmupPerRebuild = 10;
        [Tooltip("Ogranichenie startup warmup dlya odnogo prefab za odin rebuild, chtoby ne bylo rezkih allokatsiy pachkami.")]
        [SerializeField] private int maxPoolWarmupPerPrefabPerRebuild = 4;
        [SerializeField] private int maxInitialScatterCreatesPerRebuild = 24;
        [Tooltip("Legacy runtime warmup tuning. Runtime warmup disabled by zero-instantiate policy.")]
        [SerializeField] private int hotPrefabWarmupThreshold = 6;
        [Tooltip("Legacy runtime warmup tuning. Runtime warmup disabled by zero-instantiate policy.")]
        [SerializeField] private int hotPrefabWarmupCap = 12;
        [SerializeField] private int startupVariantWarmupReserve = 8;
        [Tooltip("Legacy runtime warmup tuning. Runtime warmup disabled by zero-instantiate policy.")]
        [SerializeField] private int maxRuntimeReserveTopUpPerPrefabPerRebuild = 4;
        [Tooltip("Legacy runtime warmup tuning. Runtime warmup disabled by zero-instantiate policy.")]
        [SerializeField] private float runtimeWarmupCooldownSeconds = 2f;
        #pragma warning restore CS0414
        [SerializeField, Range(0.25f, 1f)] private float coralLowFinalRadiusScale = 0.58f;
        [SerializeField, Range(0.25f, 3f)] private float rockSmallFloorFinalRadiusScale = 1.9f;
        [SerializeField, Range(0.25f, 3f)] private float rockClusterMediumFinalRadiusScale = 1.65f;
        [SerializeField, Range(0.25f, 3f)] private float rockArchLargeFinalRadiusScale = 1.25f;
        [Tooltip("Kakuyu dolyu near-radiusa razresheno tratit na proxy generated geology do perehoda v final variant.")]
        [SerializeField, Range(0f, 1f)] private float proxyGeneratedGeologyNearRadiusScale = 0.45f;
        [SerializeField] private bool enableScatterRebuildProfiling = true;
        [SerializeField] private float scatterRebuildSpikeThresholdMs = 40f;
        [Tooltip("Vklyuchaet podrobnuyu strokovuyu diagnostiku sampling/rebuild. Derzhi vyklyuchennoy v obychnom runtime, chtoby ne tratit CPU na hot path.")]
        [SerializeField] private bool enableScatterDetailedDiagnostics = false;

        [Header("Biome Volume Overrides")]
        [SerializeField] private bool enableAbyssalSiltFalseCeiling = true;
        [SerializeField] private float abyssalSiltFalseCeilingY = -200f;

        // Inspector-only scatter diagnostics are intentionally serialized for live tuning.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugEvaluatedCells;
        [SerializeField] private int _debugDesiredPlacements;
        [SerializeField] private int _debugActivePlacements;
        [SerializeField] private int _debugGroundPlacements;
        [SerializeField] private int _debugClusterPlacements;
        [SerializeField] private int _debugStructurePlacements;
        [SerializeField] private int _debugSpawnPlacements;
        [SerializeField] private int _debugMapMagicSamples;
        [SerializeField] private int _debugSceneProbeLegacySamples;
        [SerializeField] private int _debugFallbackSamples;
        [SerializeField] private int _debugMatchedScatterRules;
        [SerializeField] private int _debugHeatPassedRules;
        [SerializeField] private int _debugGatePassedRules;
        [SerializeField] private int _debugResidencyPassedCandidates;
        [SerializeField] private int _debugPostBuildGateRejectedCandidates;
        [SerializeField] private int _debugQueuedCandidates;
        [SerializeField] private int _debugBiomeInfluenceGridCells;
        [SerializeField] private int _debugBiomeInfluenceGpuBufferCapacity;
        [SerializeField] private int _debugBiomeInfluenceTransitionCells;
        [SerializeField] private int _debugFloraQuotaRejectedCandidates;
        [SerializeField] private string _debugRejectedResidencyFamily = "None";
        [SerializeField] private float _debugRejectedResidencyDistance;
        [SerializeField] private float _debugRejectedResidencyRadius;
        [SerializeField] private int _debugMaxCandidatesBeforePrunePerCell;
        [SerializeField] private int _debugMaxCandidatesAfterPrunePerCell;
        [SerializeField] private int _debugTrackedSpawnRescueCandidates;
        [SerializeField] private int _debugInjectedSpawnRescuePlacements;
        [SerializeField] private string _debugZone = "None";
        [SerializeField] private string _debugBiomeMatrixProfile = "None";
        [SerializeField] private string _debugBiomeFamily = "None";
        [SerializeField] private string _debugPattern = "None";
        [SerializeField] private string _debugResolvedPatternProfile = "None";
        [SerializeField] private bool _debugUsedFallbackPatternProfile;
        [SerializeField] private string _debugResolvedBiomeContextProfile = "None";
        [SerializeField] private bool _debugUsedFallbackBiomeContextProfile;
        [SerializeField] private string _debugTopRule = "None";
        [SerializeField] private string _debugTopFamily = "None";
        [SerializeField] private string _debugTopHeatmap = "None";
        [SerializeField] private string _debugGroundTopFamily = "None";
        [SerializeField] private string _debugClusterTopFamily = "None";
        [SerializeField] private string _debugStructureTopFamily = "None";
        [SerializeField] private string _debugSpawnTopFamily = "None";
        [SerializeField] private string _debugGroundDominantFamily = "None";
        [SerializeField] private string _debugClusterDominantFamily = "None";
        [SerializeField] private string _debugStructureDominantFamily = "None";
        [SerializeField] private string _debugSpawnDominantFamily = "None";
        [SerializeField] private string _debugSampleDominantMatrixBiome = "None";
        [SerializeField] private string _debugSampleDominantBiomeFamily = "None";
        [SerializeField] private string _debugSampleDominantPattern = "None";
        [SerializeField] private string _debugSampleDominantZone = "None";
        [SerializeField] private string _debugGroundDominantBiomeFamily = "None";
        [SerializeField] private string _debugClusterDominantBiomeFamily = "None";
        [SerializeField] private string _debugStructureDominantBiomeFamily = "None";
        [SerializeField] private string _debugSpawnDominantBiomeFamily = "None";
        [SerializeField] private string _debugClusterDominantAccentRole = "None";
        [SerializeField] private string _debugStructureDominantAccentRole = "None";
        [SerializeField] private int _debugGroundDominantCount;
        [SerializeField] private int _debugClusterDominantCount;
        [SerializeField] private int _debugStructureDominantCount;
        [SerializeField] private int _debugSpawnDominantCount;
        [SerializeField] private int _debugSampleDominantMatrixCount;
        [SerializeField] private int _debugSampleDominantBiomeCount;
        [SerializeField] private int _debugSampleDominantPatternCount;
        [SerializeField] private int _debugSampleDominantZoneCount;
        [SerializeField] private int _debugClusterDominantAccentCount;
        [SerializeField] private int _debugStructureDominantAccentCount;
        [SerializeField] private int _debugClusterFertileGrowthCount;
        [SerializeField] private int _debugClusterBiologicalNestCount;
        [SerializeField] private int _debugClusterResourcePocketCount;
        [SerializeField] private int _debugClusterShelterPocketCount;
        [SerializeField] private int _debugClusterHazardPocketCount;
        [SerializeField] private int _debugClusterDebrisFieldCount;
        [SerializeField] private int _debugClusterRockCoverCount;
        [SerializeField] private int _debugStructureNaturalLandmarkCount;
        [SerializeField] private int _debugStructureTechFragmentCount;
        [SerializeField] private int _debugStructureCaveReadCount;
        [SerializeField] private int _debugStructureBiologicalSilhouetteCount;
        [SerializeField] private int _debugSpawnPassiveCount;
        [SerializeField] private int _debugSpawnPredatorCount;
        [SerializeField] private int _debugTargetGroundMin;
        [SerializeField] private int _debugTargetGroundMax;
        [SerializeField] private int _debugTargetClusterMin;
        [SerializeField] private int _debugTargetClusterMax;
        [SerializeField] private int _debugTargetStructureMin;
        [SerializeField] private int _debugTargetStructureMax;
        [SerializeField] private int _debugTargetSpawnMin;
        [SerializeField] private int _debugTargetSpawnMax;
        [SerializeField] private float _debugPatternGroundBudgetScale = 1f;
        [SerializeField] private float _debugPatternClusterBudgetScale = 1f;
        [SerializeField] private float _debugPatternStructureBudgetScale = 1f;
        [SerializeField] private float _debugPatternSpawnBudgetScale = 1f;
        [SerializeField] private float _debugTopHeat;
        [SerializeField] private float _debugTopScore;
        [SerializeField] private float _debugRuntimeCellSize = 22f;
        [SerializeField] private int _debugRuntimeRadiusCells = 7;
        [SerializeField] private float _debugRuntimeChunkSize = 192f;
        [SerializeField] private float _debugRuntimeMacroZoneSize = 768f;
        [SerializeField] private int _debugGeneratedGeologyCount;
        [SerializeField] private int _debugPublishedFaunaAnchors;
        [SerializeField] private int _debugPublishedLargeThreatZones;
        [SerializeField] private int _debugActiveGpuiFloraPlacements;
        [SerializeField] private int _debugFloraGpuiPrototypeCount;
        [SerializeField] private bool _debugFloraGpuiReady;
        [SerializeField] private float _debugLastScatterRebuildMs;
        [SerializeField] private float _debugSamplingStageMs;
        [SerializeField] private float _debugRescueStageMs;
        [SerializeField] private float _debugRestoreStageMs;
        [SerializeField] private float _debugReconcileStageMs;
        [SerializeField] private float _debugDiagnosticsStageMs;
        [SerializeField] private float _debugReconcileCleanupStageMs;
        [SerializeField] private float _debugReconcileSpawnStageMs;
        [SerializeField] private float _debugReconcileFaunaStageMs;
        [SerializeField] private int _debugReconcileRemovedCount;
        [SerializeField] private int _debugReconcileRebuiltCount;
        [SerializeField] private int _debugReconcileCreatedCount;
        [SerializeField] private int _debugReconcileReusedCount;
        [Header("── Scatter Backend Shadow ──────────────────")]
        [Tooltip("Requested rollout mode for the scatter hybrid backend. ReservedLiveOwnership is intentionally gated back to shadow until parity/runtime proof exists.")]
        [SerializeField] private ScatterBackendExecutionMode scatterBackendRequestedExecutionMode = ScatterBackendExecutionMode.Disabled;
        [Tooltip("Legacy compatibility toggle. If requested execution mode stays Disabled and this flag is true, rollout resolves to Shadow.")]
        [SerializeField] private bool enableScatterBackendShadowPass;
        [Tooltip("Requested simulation backend kind for the scatter backend facade. EntitiesDots is prototype-only and remains shadow-safe with no live ownership.")]
        [SerializeField] private ScatterSimulationBackendKind scatterBackendRequestedKind = ScatterSimulationBackendKind.ClassicJobs;
        [Tooltip("Resolved execution mode for scatter backend rollout.")]
        [SerializeField] private string _debugScatterBackendExecutionMode = "Disabled";
        [Tooltip("Current backend kind bound to the scatter backend facade.")]
        [SerializeField] private string _debugScatterBackendKind = "ClassicJobs";
        [Tooltip("Resolved hybrid entry point reason for the current scatter backend rollout.")]
        [SerializeField] private string _debugScatterBackendResolutionReason = "backend-rollout-disabled";
        [Tooltip("True while a shadow backend pass is scheduled and not yet completed.")]
        [SerializeField] private bool _debugScatterBackendShadowPending;
        [Tooltip("Number of shadow backend passes scheduled since the last diagnostics reset.")]
        [SerializeField] private int _debugScatterBackendShadowPassesScheduled;
        [Tooltip("Number of shadow backend passes completed since the last diagnostics reset.")]
        [SerializeField] private int _debugScatterBackendShadowPassesCompleted;
        [Tooltip("Number of pending shadow backend passes interrupted by backend facade replacement since the last diagnostics reset.")]
        [SerializeField] private int _debugScatterBackendShadowInterruptedCount;
        [Tooltip("Candidate count reported by the last completed shadow backend pass.")]
        [SerializeField] private int _debugScatterBackendShadowLastCandidateCount;
        [Tooltip("Classic queued-candidate count captured when the last shadow backend pass was scheduled.")]
        [SerializeField] private int _debugScatterBackendShadowLastClassicQueuedCandidates;
        [Tooltip("Delta between last shadow backend candidate count and classic queued-candidate count.")]
        [SerializeField] private int _debugScatterBackendShadowLastCandidateDelta;
        [Tooltip("Delta between backend and classic ground placement counts in the last shadow parity pass.")]
        [SerializeField] private int _debugScatterBackendShadowLastGroundDelta;
        [Tooltip("Delta between backend and classic cluster placement counts in the last shadow parity pass.")]
        [SerializeField] private int _debugScatterBackendShadowLastClusterDelta;
        [Tooltip("Delta between backend and classic structure placement counts in the last shadow parity pass.")]
        [SerializeField] private int _debugScatterBackendShadowLastStructureDelta;
        [Tooltip("Delta between backend and classic spawn placement counts in the last shadow parity pass.")]
        [SerializeField] private int _debugScatterBackendShadowLastSpawnDelta;
        [Tooltip("True when backend and classic candidate checksums matched for the last shadow parity pass.")]
        [SerializeField] private bool _debugScatterBackendShadowLastChecksumMatch;
        [Tooltip("Last shadow parity verdict label for the scatter backend seam.")]
        [SerializeField] private string _debugScatterBackendShadowLastParityStatus = "NotRun";
        [Tooltip("Number of completed shadow passes that ended with a parity mismatch verdict.")]
        [SerializeField] private int _debugScatterBackendShadowParityMismatchCount;
        [SerializeField] private string _debugLastScatterRefreshReason = "None";
        [SerializeField] private string _debugLastScatterInvalidationReason = "None";
#pragma warning restore CS0414

        private readonly Dictionary<long, WorldProceduralProxyInstance> _activeInstances = new Dictionary<long, WorldProceduralProxyInstance>(1024);
        private static WorldProceduralPatternProfile _emergencyPatternProfile;
        private static WorldGenerativeGeologyProfile _emergencyArchGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyCanopyGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyLandmarkGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyCaveGeologyProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _emergencyPatternProfile = null;
            _emergencyArchGeologyProfile = null;
            _emergencyCanopyGeologyProfile = null;
            _emergencyLandmarkGeologyProfile = null;
            _emergencyCaveGeologyProfile = null;
            _placementPoolExhaustedWarningLogged = false;
            _registeredScatterDirectors.Clear();
            s_activeRuntimeInstance = null;
        }
        private Transform _scatterRootTransform;
        private readonly List<GameObject> _sceneRootScratch = new List<GameObject>(32); // COLD ALLOC: scene root scan for scatter root resolve.
        private ScatterBootstrapRuntimeState _bootstrapRuntimeState;
        private ScatterRefreshSampleState _scatterRefreshSampleState;
        private ScatterResolvedRuntimeSettings _runtimeStreamingState;
        private ScatterStartupRuntimeState _startupRuntimeState;
        private int _activeGpuiFloraPlacements;
        private ScatterReconcileRuntimeState _reconcileRuntimeState;
        private ScatterLifecycleRuntimeState _lifecycleRuntimeState;
        private ScatterWorkingMemory _memory;
        private ScatterInstancingService _instancingService;
        private bool _pendingScatterVisualSync;
        private bool _pendingScatterVisualSyncForceRebuild;
        private bool _originShiftListenerRegistered;
        private bool _registeredRuntimeDirector;
        private bool _floraGpuiFrustumPlanesValid;
        private int _debugFloraGpuiFrustumRejected;
        private WorldChunkCoordinate _floraGpuiLastFrustumChunk;
        private bool _floraGpuiLastFrustumChunkVisible;
        private bool _floraGpuiHasLastFrustumChunk;
        private readonly Plane[] _floraGpuiFrustumPlanes = new Plane[6]; // COLD ALLOC: Plane[6] — reusable GPUI flora frustum planes — owner: WorldProceduralScatterDirector
        private ref CandidateMap _groundRescueCandidates => ref _memory.GroundRescueCandidates;
        private ref CandidateMap _clusterRescueCandidates => ref _memory.ClusterRescueCandidates;
        private ref CandidateMap _clusterFertileCandidates => ref _memory.ClusterFertileCandidates;
        private ref CandidateMap _clusterNestCandidates => ref _memory.ClusterNestCandidates;
        private ref CandidateMap _clusterResourceCandidates => ref _memory.ClusterResourceCandidates;
        private ref CandidateMap _clusterShelterCandidates => ref _memory.ClusterShelterCandidates;
        private ref CandidateMap _clusterHazardCandidates => ref _memory.ClusterHazardCandidates;
        private ref CandidateMap _clusterDebrisCandidates => ref _memory.ClusterDebrisCandidates;
        private ref CandidateMap _clusterRockCandidates => ref _memory.ClusterRockCandidates;
        private ref CandidateMap _structureNaturalCandidates => ref _memory.StructureNaturalCandidates;
        private ref CandidateMap _structureTechCandidates => ref _memory.StructureTechCandidates;
        private ref CandidateMap _structureCaveCandidates => ref _memory.StructureCaveCandidates;
        private ref CandidateMap _structureBioCandidates => ref _memory.StructureBioCandidates;
        private ref CandidateMap _passiveSpawnCandidates => ref _memory.PassiveSpawnCandidates;
        private ref CandidateMap _predatorSpawnCandidates => ref _memory.PredatorSpawnCandidates;
        private Dictionary<long, ScatterPlacement> _desiredPlacements => _memory != null ? _memory.DesiredPlacements : null;
        private Dictionary<long, ScatterPlacement> _retainedPlacements => _memory.RetainedPlacements;
        private Dictionary<long, float> _placementLastSeenTimes => _memory.PlacementLastSeenTimes;
        private Stack<ScatterPlacement> _placementPool => _memory.PlacementPool;
        private Dictionary<long, int> _structureWindowCounts => _memory.StructureWindowCounts;
        private Dictionary<long, int> _spawnWindowCounts => _memory.SpawnWindowCounts;
        private List<ScatterCandidate> _candidateBuffer => _memory.CandidateBuffer;
        private List<long> _removalBuffer => _memory.RemovalBuffer;
        private List<WorldFaunaSpawnRegistry.Anchor> _faunaAnchorBuffer => _memory.FaunaAnchorBuffer;
        private List<GPUInstancerPrefabPrototype> _floraGpuiKnownPrototypes => _memory.FloraGpuiKnownPrototypes;
        private Dictionary<GPUInstancerPrefabPrototype, Matrix4x4[]> _floraGpuiMatrices => _memory.FloraGpuiMatrices;
        private Dictionary<GPUInstancerPrefabPrototype, int> _floraGpuiCounts => _memory.FloraGpuiCounts;
        private Dictionary<GPUInstancerPrefabPrototype, int> _floraGpuiBufferCapacities => _memory.FloraGpuiBufferCapacities;
        private HashSet<GPUInstancerPrefabPrototype> _floraGpuiInitializedPrototypes => _memory.FloraGpuiInitializedPrototypes;
        private List<ScatterCandidate> _clusterAccentOrderedCandidates => _memory.ClusterAccentOrderedCandidates;
        private List<ScatterCandidate> _clusterOrderedCandidates => _memory.ClusterOrderedCandidates;
        private List<ScatterCandidate> _exactClusterOrderedCandidates => _memory.ExactClusterOrderedCandidates;
        private List<ScatterCandidate> _groundOrderedCandidates => _memory.GroundOrderedCandidates;
        private List<ScatterCandidate> _windowOrderedCandidates => _memory.WindowOrderedCandidates;
        private List<ScatterCandidate> _patternStructureOrderedCandidates => _memory.PatternStructureOrderedCandidates;
        private List<ScatterCandidate> _structureAccentOrderedCandidates => _memory.StructureAccentOrderedCandidates;
        private List<ScatterCandidate> _patternSpawnOrderedCandidates => _memory.PatternSpawnOrderedCandidates;
        private List<ScatterCandidate> _patternSpawnPassiveOrderedCandidates => _memory.PatternSpawnPassiveOrderedCandidates;
        private List<ScatterCandidate> _patternSpawnPredatorOrderedCandidates => _memory.PatternSpawnPredatorOrderedCandidates;
        private List<ScatterRuntimeRuleEntry> _runtimeRuleBuffer => _memory.RuntimeRuleBuffer;
        private HashSet<long> _occupiedCellBuffer => _memory.OccupiedCellBuffer;
        private Dictionary<long, List<ScatterPlacement>> _gridPlacements => _memory.GridPlacements;
        private List<List<ScatterPlacement>> _gridPlacementBuckets => _memory.GridPlacementBuckets;
        private Dictionary<long, ScatterCandidate> _structureRescueCandidates => _memory.StructureRescueCandidates;
        private Dictionary<long, ScatterCandidate> _spawnRescueCandidates => _memory.SpawnRescueCandidates;
        private WorldProceduralPatternProfile[] _patternProfileCache => _memory.PatternProfileCache;
        private Dictionary<Hecton8.Environment.HectonBiomeFamilyProfile, WorldProceduralBiomeFamilyContextProfile> _biomeContextCache => _memory.BiomeContextCache;
        private float[] _layerNearRadii => _memory.LayerNearRadii;
        private float[] _layerMidRadii => _memory.LayerMidRadii;
        private float[] _layerFarRadii => _memory.LayerFarRadii;
        private float _cachedLayerRadiiCellSize
        {
            get => _memory.CachedLayerRadiiCellSize;
            set => _memory.CachedLayerRadiiCellSize = value;
        }
        private WorldChunkStreamingProfile _cachedLayerRadiiProfile
        {
            get => _memory.CachedLayerRadiiProfile;
            set => _memory.CachedLayerRadiiProfile = value;
        }
        private ScatterCandidate[] _layerTopCandidatesBuffer => _memory.LayerTopCandidatesBuffer;
        private bool[] _layerTopValidBuffer => _memory.LayerTopValidBuffer;
        private int[] _layerPlacementCountsBuffer => _memory.LayerPlacementCountsBuffer;
        private int[] _patternLayerTargetMaxBuffer => _memory.PatternLayerTargetMaxBuffer;
        private int[] _clusterAccentCountsBuffer => _memory.ClusterAccentCountsBuffer;
        private float[] _clusterAccentRoleMaxRatioBuffer => _memory.ClusterAccentRoleMaxRatioBuffer;
        private int[] _structureAccentCountsBuffer => _memory.StructureAccentCountsBuffer;
        private int[] _structureAccentRoleMaxBuffer => _memory.StructureAccentRoleMaxBuffer;
        private Dictionary<string, int>[] _layerFamilyCountsBuffer => _memory.LayerFamilyCountsBuffer;
        private Dictionary<string, int>[] _layerBiomeCountsBuffer => _memory.LayerBiomeCountsBuffer;
        private Dictionary<HectonBiomeMatrixProfile, int> _sampledMatrixProfileCounts => _memory.SampledMatrixProfileCounts;
        private Dictionary<string, int> _sampledMatrixBiomeCounts => _memory.SampledMatrixBiomeCounts;
        private Dictionary<string, int> _sampledBiomeCounts => _memory.SampledBiomeCounts;
        private Dictionary<string, int> _sampledPatternCounts => _memory.SampledPatternCounts;
        private Dictionary<string, int> _sampledZoneCounts => _memory.SampledZoneCounts;
        private bool _faunaSnapshotDirty
        {
            get => _memory.FaunaSnapshotDirty;
            set => _memory.FaunaSnapshotDirty = value;
        }
        private int _gridPlacementBucketCount
        {
            get => _memory.GridPlacementBucketCount;
            set => _memory.GridPlacementBucketCount = value;
        }
        private float _maxRegisteredPlacementSpacingMeters
        {
            get => _memory.MaxRegisteredPlacementSpacingMeters;
            set => _memory.MaxRegisteredPlacementSpacingMeters = value;
        }
        private ScatterState _scatterState;
        private JobHandle _samplingJobHandle;
        private bool _isSamplingJobRunning;
        private SamplingSnapshot _samplingSnapshot;
        private int _samplingTotalCells;
        private int _samplingCellDiameter;
        private int _samplingRadiusCells;
        private float _samplingCellSize;
        private float _samplingNow;
        private int _samplingGroundBudget;
        private int _samplingClusterBudget;
        private int _samplingStructureStride;
        private int _samplingStructureBudget;
        private int _samplingSpawnStride;
        private int _samplingSpawnBudget;
        private long _samplingRebuildStartTimestamp;
        private long _samplingInputsEndTimestamp;
        private bool _loggedMissingPrefabRegistry;
        private float _nextScatterLifecycleLogTime;

        public int ActivePlacementCount => _activeInstances.Count + _activeGpuiFloraPlacements;
        public bool HasPendingStartupPlacements => _reconcileRuntimeState.HasPendingStartupPlacements != 0;
        internal bool HasBootstrapPrimeWork =>
            _isSamplingJobRunning ||
            _scatterState != ScatterState.Idle ||
            _reconcileRuntimeState.HasPendingStartupPlacements != 0;

        internal IReadOnlyDictionary<long, List<ScatterPlacement>> GetGridPlacements()
        {
            return _gridPlacements;
        }

        public void Tick(float dt)
        {
            if (!_runtimeScatterCallbacksActive)
                return;

            if (ShouldDeferUntilBootstrapReady())
                return;

            if (_scatterState == ScatterState.Sampling && _isSamplingJobRunning)
                return;

            float now = RuntimeNowSeconds();
            if (now < _lifecycleRuntimeState.NextTickDrivenScatterAttemptTime)
                return;

            using (_scatterTickProfilerMarker.Auto())
            {
                _lifecycleRuntimeState.NextTickDrivenScatterAttemptTime = now + 0.25f;
                RefreshRuntimeStreamingSettings();

                if (ShouldSkipScatterRefresh())
                {
                    if (HasPendingScatterReconcileWork())
                        QueueScatterVisualSync(forceRebuild: false);

                    return;
                }

                QueueScatterVisualSync(forceRebuild: true);
            }
        }

        private void Awake()
        {
            _runtimeScatterCallbacksActive = Application.isPlaying;
            _scatterState = ScatterState.Idle;
            _isSamplingJobRunning = false;
            CachePlayerContextCold();
            EnsureWorkingMemory();
            ResolveReferences();
            ResolveGenerativeGeologyService(createIfMissing: true);
            CacheMigratorySargassumOrganicManagerCold();
            RegisterProceduralStateRegistryCallbacks();
            SubscribeToBootstrap();
            RegisterOriginShiftListener();
            TryEnsureTickRegistration();
            RefreshRuntimeStreamingSettings();
            EnsureScatterBackendFacadeInitialized();
            if (Application.isPlaying)
                EnsureMigratorySargassumLane();

            EnsureCandidateMapsInitialized();
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif

            if (!Application.isPlaying && !ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private void OnEnable()
        {
            _runtimeScatterCallbacksActive = Application.isPlaying;
            PublishActiveRuntimeInstance();
            CachePlayerContextCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntimeDirector();
            EnsureWorkingMemory();
            ResolveReferences();
            ResolveGenerativeGeologyService(createIfMissing: true);
            CacheMigratorySargassumOrganicManagerCold();
            RegisterProceduralStateRegistryCallbacks();
            SubscribeToBootstrap();
            TryEnsureTickRegistration();
            EnsureScatterBackendFacadeInitialized();
            if (Application.isPlaying)
                EnsureMigratorySargassumLane();
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
        }

        private void Start()
        {
            TryEnsureTickRegistration();

            if (Application.isPlaying)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_lifecycleRuntimeState.LoggedRuntimeStartState == 0 && enableScatterRebuildProfiling)
                    _lifecycleRuntimeState.LoggedRuntimeStartState = 1;
#endif
                _startupRuntimeState.StabilizationPending = 1;
                _startupRuntimeState.StartTime = RuntimeNowSeconds();
                InvalidateScatterRefreshSample("startup");
                return;
            }

            if (!ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private void OnDisable()
        {
            _runtimeScatterCallbacksActive = false;
            TryUnregisterHotSwapListener();
            ClearActiveRuntimeInstance();
            TryUnregisterRuntimeDirector();
            UnsubscribeFromBootstrap();
            UnregisterOriginShiftListener();
            UnregisterProceduralStateRegistryCallbacks();
            CompleteSamplingJobForTeardown();
            DisposeMigratorySargassumLane();
            DisposeScatterBackendFacade();
            DisposeCellSamplingArrays();
            ClearFloraGpuiVisibility();

            if (_lifecycleRuntimeState.RegisteredToTickManager != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

                _lifecycleRuntimeState.RegisteredToTickManager = 0;
            }
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        private void OnDestroy()
        {
            _runtimeScatterCallbacksActive = false;
            TryUnregisterHotSwapListener();
            ClearActiveRuntimeInstance();
            TryUnregisterRuntimeDirector();
            UnregisterOriginShiftListener();
            CompleteSamplingJobForTeardown();
            DisposeMigratorySargassumLane();
            DisposeScatterBackendFacade();
            ClearFloraGpuiVisibility();

            if (_lifecycleRuntimeState.RegisteredToTickManager != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lifecycleRuntimeState.RegisteredToTickManager = 0;
            }

            DisposeCellSamplingArrays();
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        internal static int RegisteredDirectorCount => _registeredScatterDirectors.Count;

        internal static WorldProceduralScatterDirector GetRegisteredDirectorAt(int index)
        {
            return _registeredScatterDirectors.GetAt(index);
        }

        private void TryRegisterRuntimeDirector()
        {
            if (_registeredRuntimeDirector || !_runtimeScatterCallbacksActive)
                return;

            _registeredRuntimeDirector = _registeredScatterDirectors.TryRegister(this);
        }

        private void TryUnregisterRuntimeDirector()
        {
            if (!_registeredRuntimeDirector)
                return;

            _registeredScatterDirectors.Unregister(this);
            _registeredRuntimeDirector = false;
        }

        private void PublishActiveRuntimeInstance()
        {
            GlobalRegistry.RegisterWorldGenService(this);
            if (ReferenceEquals(GlobalRegistry.ProceduralScatter, this))
                s_activeRuntimeInstance = this;
        }

        private void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            if (ReferenceEquals(GlobalRegistry.ProceduralScatter, this))
                GlobalRegistry.UnregisterWorldGenService(this);
        }

#if UNITY_EDITOR
        private static void EnsureAssemblyReloadHook()
        {
            if (_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
            _assemblyReloadHookRegistered = true;
        }

        private static void ReleaseAssemblyReloadHook()
        {
            if (!_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _assemblyReloadHookRegistered = false;
        }

        private static void HandleBeforeAssemblyReload()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void HandleEditorQuitting()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void TeardownActiveRuntimeInstanceForEditorReload()
        {
            WorldProceduralScatterDirector activeInstance = ActiveRuntimeInstance;
            if (activeInstance == null)
                return;

            activeInstance.PrepareForEditorReload();
            activeInstance.ClearActiveRuntimeInstance();
        }
#endif

        internal void PrepareForEditorReload()
        {
            _runtimeScatterCallbacksActive = false;
            UnsubscribeFromBootstrap();
            UnregisterProceduralStateRegistryCallbacks();
            CompleteSamplingJobForTeardown();
            DisposeMigratorySargassumLane();
            DisposeScatterBackendFacade();
            ClearFloraGpuiVisibility();
            DisposeCellSamplingArrays();
            if (_lifecycleRuntimeState.RegisteredToTickManager != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            }
            _lifecycleRuntimeState.RegisteredToTickManager = 0;
        }

        private void EnsureCellSamplingArrayCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return;

            EnsureWorkingMemory();
            _memory.EnsureCellSamplingCapacity(requiredCapacity);
        }

        private void EnsureWorkingMemory()
        {
            if (_memory != null)
            {
                if (_instancingService == null)
                    _instancingService = new ScatterInstancingService();

                return;
            }

            _memory = new ScatterWorkingMemory();
            _instancingService = new ScatterInstancingService();
        }

        private void CompleteSamplingJobForTeardown()
        {
            if (!_isSamplingJobRunning)
            {
                ResetSamplingState();
                return;
            }

            TryCompleteScatterSamplingJobForTeardown(ref _samplingJobHandle);
            if (fieldSampler != null)
            {
                fieldSampler.MarkScatterSamplingJobCompleted();
                fieldSampler.EndScatterSamplingFrame();
            }
            _isSamplingJobRunning = false;
            ResetSamplingState();
        }

        private static bool TryCompleteScatterSamplingJobForTeardown(ref JobHandle handle)
        {
            bool completed;
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                completed = DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            return completed;
        }

        private void DisposeCellSamplingArrays()
        {
            if (_memory == null)
                return;

            _memory.Dispose();
            _memory = null;
        }

        public void SlowTick()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_runtimeScatterCallbacksActive && _lifecycleRuntimeState.LoggedFirstSlowTick == 0 && ShouldLogScatterLifecycleDiagnostics())
            {
                _lifecycleRuntimeState.LoggedFirstSlowTick = 1;
                _nextScatterLifecycleLogTime = RuntimeNowSeconds() + 5f;
                LogFirstSlowTick(this);
            }
#endif
            using (_scatterSlowTickProfilerMarker.Auto())
            {
                if (ShouldDeferUntilBootstrapReady())
                    return;

                FlushProxyOptimizationRegistrationSlow();
                TickMigratorySargassumLane(RuntimeNowSeconds());

                if (_scatterState == ScatterState.Sampling && _isSamplingJobRunning)
                    return;

                if (_scatterState != ScatterState.Idle)
                {
                    QueueScatterVisualSync(forceRebuild: true);
                    return;
                }

                RefreshRuntimeStreamingSettings();
                if (ShouldSkipScatterRefresh())
                {
                    if (HasPendingScatterReconcileWork())
                        QueueScatterVisualSync(forceRebuild: false);

                    return;
                }

                QueueScatterVisualSync(forceRebuild: true);
            }
        }

        private void FlushProxyOptimizationRegistrationSlow()
        {
            if (_activeInstances.Count == 0)
                return;

            int processed = 0;
            int budget = ResolveProxyOptimizationRefreshBudget();
            Dictionary<long, WorldProceduralProxyInstance>.Enumerator enumerator = _activeInstances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                WorldProceduralProxyInstance instance = enumerator.Current.Value;
                if (instance == null || !instance.HasPendingOptimizationRegistration)
                    continue;

                instance.RefreshOptimizationRegistrationCold();
                processed++;
                if (processed >= budget)
                    break;
            }
        }

        private static int ResolveProxyOptimizationRefreshBudget()
        {
            float rawQuality = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.select(rawQuality, 1f, !math.isfinite(rawQuality)));
            float curve = quality * quality * (3f - 2f * quality);
            return math.max(
                1,
                (int)math.round(math.lerp(
                    ProxyOptimizationRefreshLowTierBudget,
                    ProxyOptimizationRefreshUltraTierBudget,
                    curve)));
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogFirstSlowTick(UnityEngine.Object context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[WorldScatterRuntime] First slow tick reached.", context);
#endif
        }

        public void LateFrameTick()
        {
            if (!_runtimeScatterCallbacksActive)
                return;

            FlushScatterVisualSync();
            PumpScatterBackendShadowPass();
            CompleteScatterSamplingJobIfReady();
            CompleteMigratorySargassumJobIfReady();
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            InvalidateLayerRadiiCache();
            RefreshRuntimeStreamingSettings();
            InvalidateScatterRefreshSample("chunk-profile");
        }

        public void SetFaunaSpawnRegistry(WorldFaunaSpawnRegistry registry)
        {
            if (ReferenceEquals(faunaSpawnRegistry, registry))
            {
                if (faunaSpawnRegistry != null)
                    faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
                return;
            }

            faunaSpawnRegistry = registry;
            if (faunaSpawnRegistry != null)
                faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
            PublishFaunaRegistrySnapshot();
            InvalidateScatterRefreshSample("fauna-registry");
        }

        public void SetProceduralStateRegistry(WorldProceduralStateRegistry registry)
        {
            if (ReferenceEquals(proceduralStateRegistry, registry))
            {
                if (faunaSpawnRegistry != null)
                    faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
                return;
            }

            UnregisterProceduralStateRegistryCallbacks();
            proceduralStateRegistry = registry;
            RegisterProceduralStateRegistryCallbacks();

            if (faunaSpawnRegistry != null)
                faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);

            RequestScatterRefresh("state-registry");
        }

        public void RebuildScatterPreview()
        {
            if (ShouldDeferUntilBootstrapReady())
            {
                ResetDiagnostics();
                return;
            }

            using (_scatterRebuildDispatcherProfilerMarker.Auto())
            {
                if (!_runtimeScatterCallbacksActive)
                {
                    if (TryRunScatterSamplingSynchronously())
                        return;
                }
                else if (_bootstrapRuntimeState.AllowPrimePass != 0)
                {
                    if (HandleScatterStateMachine())
                        return;

                    if (TryBeginScatterSampling())
                        return;
                }
                else
                {
                    if (HandleScatterStateMachine())
                        return;

                    if (TryBeginScatterSampling())
                        return;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[WorldScatter] RebuildScatterPreview fell through dispatcher. This path is invalid.", this);
#endif
            ResetDiagnostics();
        }

        private void ForceRefreshProceduralContext()
        {
            WorldZoneDirector zoneDirector = null;
            WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref zoneDirector);
            if (zoneDirector != null)
                zoneDirector.ForceRefresh();

            BiomeMatrixDirector matrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref matrixDirector);
            if (matrixDirector != null)
                matrixDirector.ForceRefresh();

            WorldContentDirector contentDirector = null;
            WorldRuntimeReferenceUtility.TryResolveWorldContentDirector(ref contentDirector);
            if (contentDirector != null)
                contentDirector.ForceRefresh();

            if (proceduralFillDirector != null)
                proceduralFillDirector.ForceRefresh();
        }

        public bool TryPrimeBootstrapScatterPass()
        {
            if (!_runtimeScatterCallbacksActive)
                return false;

            ResolveReferences();
            RefreshRuntimeStreamingSettings();

            IReadOnlyList<WorldProceduralPlacementRule> rules = proceduralFillDirector != null
                ? proceduralFillDirector.Rules
                : null;

            if (playerTransform == null || fieldSampler == null || rules == null || rules.Count == 0)
                return false;

            if (!CanPrimeBootstrapScatterFromCurrentTerrainSource())
                return false;

            byte previousAllowBootstrapPrimePass = _bootstrapRuntimeState.AllowPrimePass;
            _bootstrapRuntimeState.AllowPrimePass = 1;

            try
            {
                if (!HasBootstrapPrimeWork)
                    InvalidateScatterRefreshSample("scene-bootstrap-prime");

                RebuildScatterPreview();
                return true;
            }
            finally
            {
                _bootstrapRuntimeState.AllowPrimePass = previousAllowBootstrapPrimePass;
            }
        }

        private bool CanPrimeBootstrapScatterFromCurrentTerrainSource()
        {
            if (fieldSampler == null ||
                !TryGetObserverAbsolutePosition(out Vector3 observerAbsolutePosition))
                return false;

            if (!fieldSampler.TryResolveSeafloorSource(
                    ToRuntimeScatterPosition(observerAbsolutePosition),
                    out WorldProceduralFieldSampler.SeafloorSource seafloorSource))
            {
                return false;
            }

            return seafloorSource != WorldProceduralFieldSampler.SeafloorSource.FallbackSynthetic;
        }

        internal bool TryPrewarmBootstrapSamplingPipeline()
        {
            if (!_runtimeScatterCallbacksActive)
                return false;

            if (_bootstrapRuntimeState.SamplingPipelinePrewarmed != 0)
                return true;

            ResolveReferences();
            if (fieldSampler == null)
                return false;

            bool warmed = fieldSampler.TryPrewarmSamplingJob();
            if (warmed)
                _bootstrapRuntimeState.SamplingPipelinePrewarmed = 1;

            return warmed;
        }

        private bool ShouldDeferUntilBootstrapReady()
        {
            if (_bootstrapRuntimeState.AllowPrimePass != 0)
                return false;

            if (!_runtimeScatterCallbacksActive || !waitForGameBootstrapper || BootstrapState.IsGameReady)
                return false;

            if (_bootstrapRuntimeState.Failed != 0)
                return false;

            return ResolveGameBootstrapperPresence();
        }

        private void HandleGameBootstrapperReady()
        {
            _bootstrapRuntimeState.PresenceResolved = 1;
            _bootstrapRuntimeState.Present = 1;
            _bootstrapRuntimeState.Failed = 0;
            TryEnsureTickRegistration();
            RefreshRuntimeStreamingSettings();
            RequestScatterRefresh("scene-bootstrap");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShouldLogScatterLifecycleDiagnostics())
            {
                _nextScatterLifecycleLogTime = RuntimeNowSeconds() + 5f;
                Hecton8.Core.H8Debug.Log(
                        $"[WorldScatterRuntime] bootstrap-ready registered={_lifecycleRuntimeState.RegisteredToTickManager != 0} dilation={SimulationSignalRoute.TimeDilationScalar:0.###}",
                    this);
            }
#endif
            if (_runtimeScatterCallbacksActive && !ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
        {
            GameBootstrapperEventType eventType = (GameBootstrapperEventType)payload.EventType;
            if (eventType == GameBootstrapperEventType.GameReady)
            {
                HandleGameBootstrapperReady();
                return;
            }

            if (eventType != GameBootstrapperEventType.BootstrapFailed)
                return;

            if (payload.ErrorHash != 0u && GameBootstrapper.TryResolveBootstrapFailureReason(payload.ErrorHash, out string reason))
            {
                HandleGameBootstrapperFailed(reason);
                return;
            }

            HandleGameBootstrapperFailed(string.Empty);
        }

        private void HandleGameBootstrapperFailed(string reason)
        {
            _bootstrapRuntimeState.PresenceResolved = 1;
            _bootstrapRuntimeState.Present = 1;
            _bootstrapRuntimeState.Failed = 1;
            TryEnsureTickRegistration();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Hecton8.Core.H8Debug.LogWarning(
                    $"[WorldScatter] Scene bootstrap failed and scatter fallback was enabled. Reason: {reason}",
                    this);
            }
#endif

            RefreshRuntimeStreamingSettings();
            RequestScatterRefresh("scene-bootstrap-failed");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShouldLogScatterLifecycleDiagnostics())
            {
                _nextScatterLifecycleLogTime = RuntimeNowSeconds() + 5f;
                Hecton8.Core.H8Debug.Log(
                        $"[WorldScatterRuntime] bootstrap-failed registered={_lifecycleRuntimeState.RegisteredToTickManager != 0} dilation={SimulationSignalRoute.TimeDilationScalar:0.###}",
                    this);
            }
#endif
            if (Application.isPlaying && !ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private bool ResolveGameBootstrapperPresence()
        {
            if (BootstrapState.HasActiveInstance)
            {
                _bootstrapRuntimeState.PresenceResolved = 1;
                _bootstrapRuntimeState.Present = 1;
                return true;
            }

            if (_bootstrapRuntimeState.PresenceResolved != 0)
                return _bootstrapRuntimeState.Present != 0;

            _bootstrapRuntimeState.PresenceResolved = 1;
            _bootstrapRuntimeState.Present = BootstrapState.HasActiveInstance ? (byte)1 : (byte)0;
            return _bootstrapRuntimeState.Present != 0;
        }

        private bool ShouldSkipScatterRefresh()
        {
            if (_scatterRefreshSampleState.HasSample == 0 || !TryGetObserverAbsolutePosition(out Vector3 observerAbsolutePosition))
            {
                _debugLastScatterRefreshReason = ShouldCollectScatterDetailedDiagnostics() ? _debugLastScatterInvalidationReason : "dirty";
                return false;
            }

            int activeRadiusCells = ResolveActiveScatterSamplingRadiusCells(_runtimeStreamingState.RadiusCells);
            if (_scatterRefreshSampleState.RadiusCells != activeRadiusCells)
            {
                _debugLastScatterRefreshReason = "radius-changed";
                return false;
            }

            Vector3 observerRuntimePosition = ToRuntimeScatterPosition(observerAbsolutePosition);
            if (_scatterRefreshSampleState.UsedFallbackOnly != 0 &&
                fieldSampler != null &&
                fieldSampler.TryResolveSeafloorSource(observerRuntimePosition, out WorldProceduralFieldSampler.SeafloorSource upgradedSource) &&
                IsAuthoritativeTerrainSource(upgradedSource))
            {
                _debugLastScatterRefreshReason = "terrain-source-upgraded";
                return false;
            }

            if (_startupRuntimeState.StabilizationPending != 0 &&
                _runtimeScatterCallbacksActive &&
                RuntimeNowSeconds() - _startupRuntimeState.StartTime >= StartupScatterStabilizationDelaySeconds)
            {
                _startupRuntimeState.StabilizationPending = 0;
                _debugLastScatterRefreshReason = "startup-settle";
                return false;
            }

            if (enableForcedScatterRefresh && scatterForcedRefreshInterval > 0f)
            {
                float forcedInterval = math.max(0.5f, scatterForcedRefreshInterval);
                if (_runtimeScatterCallbacksActive && RuntimeNowSeconds() - _scatterRefreshSampleState.Time >= forcedInterval)
                {
                    _debugLastScatterRefreshReason = "forced-interval";
                    return false;
                }
            }

            if (TryGetScatterCenterCell(out int centerCellX, out int centerCellZ))
            {
                if (centerCellX != _scatterRefreshSampleState.CenterCellX || centerCellZ != _scatterRefreshSampleState.CenterCellZ)
                {
                    int cellDeltaX = math.abs(centerCellX - _scatterRefreshSampleState.CenterCellX);
                    int cellDeltaZ = math.abs(centerCellZ - _scatterRefreshSampleState.CenterCellZ);
                    int maxCellDelta = math.max(cellDeltaX, cellDeltaZ);
                    if (_runtimeStreamingState.RadiusCells > 2 && maxCellDelta <= 1)
                    {
                        _debugLastScatterRefreshReason = "cell-drift-buffer";
                        return true;
                    }

                    float cellRefreshThreshold = math.max(math.max(0.5f, scatterRefreshDistanceThreshold), math.max(1f, _runtimeStreamingState.CellSize));
                    if ((observerAbsolutePosition - _scatterRefreshSampleState.AbsolutePosition).sqrMagnitude < cellRefreshThreshold * cellRefreshThreshold)
                    {
                        _debugLastScatterRefreshReason = "cell-hysteresis";
                        return true;
                    }

                    _debugLastScatterRefreshReason = "cell-changed";
                    return false;
                }

                _debugLastScatterRefreshReason = "same-cell";
                return true;
            }

            float threshold = math.max(0.5f, scatterRefreshDistanceThreshold);
            bool sameDistanceBucket = (observerAbsolutePosition - _scatterRefreshSampleState.AbsolutePosition).sqrMagnitude < threshold * threshold;
            _debugLastScatterRefreshReason = sameDistanceBucket ? "same-distance" : "distance-threshold";
            return sameDistanceBucket;
        }

        private bool HasPendingScatterReconcileWork()
        {
            return _reconcileRuntimeState.HasPendingStartupPlacements != 0 || _reconcileRuntimeState.HasPendingRuntimePlacements != 0;
        }

        private int ResolveActiveScatterSamplingRadiusCells(int runtimeRadiusCells)
        {
            int resolvedRadiusCells = math.max(2, runtimeRadiusCells);
            if (_runtimeScatterCallbacksActive && _bootstrapRuntimeState.AllowPrimePass != 0)
                resolvedRadiusCells = math.min(resolvedRadiusCells, math.max(2, bootstrapPrimeRadiusCells));

            return resolvedRadiusCells;
        }

        private void ContinuePendingScatterReconcile()
        {
            if (_desiredPlacements.Count == 0)
            {
                _reconcileRuntimeState.HasPendingStartupPlacements = 0;
                _reconcileRuntimeState.HasPendingRuntimePlacements = 0;
                return;
            }

            using (_scatterPendingReconcileProfilerMarker.Auto())
            {
                ReconcileInstances(enableScatterRebuildProfiling);
                _debugLastScatterRefreshReason = _reconcileRuntimeState.HasPendingStartupPlacements != 0
                    ? "pending-startup-batch"
                    : (_reconcileRuntimeState.HasPendingRuntimePlacements != 0 ? "pending-runtime-budget" : "pending-complete");
            }
        }

        private void QueueScatterVisualSync(bool forceRebuild)
        {
            _pendingScatterVisualSync = true;
            _pendingScatterVisualSyncForceRebuild |= forceRebuild;
        }

        private void FlushScatterVisualSync()
        {
            if (!_pendingScatterVisualSync)
                return;

            bool forceRebuild = _pendingScatterVisualSyncForceRebuild;
            _pendingScatterVisualSync = false;
            _pendingScatterVisualSyncForceRebuild = false;

            if (forceRebuild || _scatterState != ScatterState.Idle)
            {
                RebuildScatterPreview();
                return;
            }

            if (HasPendingScatterReconcileWork())
                ContinuePendingScatterReconcile();
        }

        private void RetainTopCandidate(
            List<ScatterCandidate> candidates,
            in ScatterCandidate candidate,
            int maxCount,
            ref int worstIndex,
            ref float worstScore)
        {
            if (maxCount <= 0)
            {
                ReleasePlacement(candidate.Placement);
                return;
            }

            if (candidates.Count < maxCount)
            {
                candidates.Add(candidate);
                if (worstIndex < 0 || candidate.Score < worstScore)
                {
                    worstIndex = candidates.Count - 1;
                    worstScore = candidate.Score;
                }

                return;
            }

            if (worstIndex < 0 || worstIndex >= candidates.Count)
                RefreshWorstCandidate(candidates, out worstIndex, out worstScore);

            if (candidate.Score <= worstScore)
            {
                ReleasePlacement(candidate.Placement);
                return;
            }

            ReleasePlacement(candidates[worstIndex].Placement);
            candidates[worstIndex] = candidate;
            RefreshWorstCandidate(candidates, out worstIndex, out worstScore);
        }

        private static int ResolvePerCellCandidateBufferLimit(
            int localGroundBudget,
            int localClusterBudget,
            int localStructureBudget,
            int localSpawnBudget)
        {
            int placementBudget = localGroundBudget + localClusterBudget + localStructureBudget + localSpawnBudget;
            return math.clamp(placementBudget * 2 + 6, 12, 64);
        }

        private static void RefreshWorstCandidate(
            List<ScatterCandidate> candidates,
            out int worstIndex,
            out float worstScore)
        {
            worstIndex = -1;
            worstScore = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float candidateScore = candidates[i].Score;
                if (candidateScore >= worstScore)
                    continue;

                worstScore = candidateScore;
                worstIndex = i;
            }
        }

        private static void SortCandidateBufferByScore(List<ScatterCandidate> candidates)
        {
            candidates.Sort();
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogCandidateMapCapacityExceeded(int capacity, long key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_candidateMapCapacityExceededWarningLogged)
                return;

            _candidateMapCapacityExceededWarningLogged = true;
            Hecton8.Core.H8Debug.LogWarning(CandidateMapCapacityExceededWarning);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogCandidateMapNearCapacity(int count, int capacity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_candidateMapNearCapacityWarningLogged)
                return;

            _candidateMapNearCapacityWarningLogged = true;
            Hecton8.Core.H8Debug.LogWarning(CandidateMapNearCapacityWarning);
#endif
        }

        private void ResolveCombinedBudgetScales(
            WorldProceduralPatternProfile patternProfile,
            WorldProceduralBiomeFamilyContextProfile biomeContext,
            out float groundScale,
            out float clusterScale,
            out float structureScale,
            out float spawnScale)
        {
            EnsureWorkingMemory();
            if (_memory.HasCachedBudgetScales &&
                ReferenceEquals(_memory.CachedBudgetScalePatternProfile, patternProfile) &&
                ReferenceEquals(_memory.CachedBudgetScaleBiomeContext, biomeContext))
            {
                groundScale = _memory.CachedGroundBudgetScale;
                clusterScale = _memory.CachedClusterBudgetScale;
                structureScale = _memory.CachedStructureBudgetScale;
                spawnScale = _memory.CachedSpawnBudgetScale;
                return;
            }

            groundScale = GetCombinedBudgetScale(patternProfile, biomeContext, WorldPrefabFamilyProfile.ScatterLayer.Ground);
            clusterScale = GetCombinedBudgetScale(patternProfile, biomeContext, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            structureScale = GetCombinedBudgetScale(patternProfile, biomeContext, WorldPrefabFamilyProfile.ScatterLayer.Structure);
            spawnScale = GetCombinedBudgetScale(patternProfile, biomeContext, WorldPrefabFamilyProfile.ScatterLayer.Spawn);

            _memory.HasCachedBudgetScales = true;
            _memory.CachedBudgetScalePatternProfile = patternProfile;
            _memory.CachedBudgetScaleBiomeContext = biomeContext;
            _memory.CachedGroundBudgetScale = groundScale;
            _memory.CachedClusterBudgetScale = clusterScale;
            _memory.CachedStructureBudgetScale = structureScale;
            _memory.CachedSpawnBudgetScale = spawnScale;
        }

        private void RecordScatterRefreshSample()
        {
            if (!TryGetObserverAbsolutePosition(out Vector3 observerAbsolutePosition))
                return;

            _scatterRefreshSampleState.HasSample = 1;
            _scatterRefreshSampleState.AbsolutePosition = observerAbsolutePosition;
            _scatterRefreshSampleState.Time = RuntimeNowSeconds();
            _scatterRefreshSampleState.RadiusCells = ResolveActiveScatterSamplingRadiusCells(_runtimeStreamingState.RadiusCells);
            if (TryGetScatterCenterCell(out int centerCellX, out int centerCellZ))
            {
                _scatterRefreshSampleState.CenterCellX = centerCellX;
                _scatterRefreshSampleState.CenterCellZ = centerCellZ;
            }

            _debugLastScatterInvalidationReason = "None";
        }

        private void InvalidateScatterRefreshSample(string reason = "manual")
        {
            _scatterRefreshSampleState.HasSample = 0;
            _scatterRefreshSampleState.UsedFallbackOnly = 0;
            _scatterRefreshSampleState.RadiusCells = 0;
            _scatterRefreshSampleState.Time = float.NegativeInfinity;
            _debugLastScatterInvalidationReason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason;
        }

        private void RequestScatterRefresh(string reason)
        {
            InvalidateScatterRefreshSample(reason);
            TryEnsureTickRegistration();

            if (_runtimeScatterCallbacksActive)
                return;

            RefreshRuntimeStreamingSettings();
            if (!ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private void TryEnsureTickRegistration()
        {
            if (_lifecycleRuntimeState.RegisteredToTickManager != 0 || !_runtimeScatterCallbacksActive || GlobalRegistry.Dispatcher == null)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            bool slowRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            bool lateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            bool registered = updateRegistered && slowRegistered && lateRegistered;
            if (!registered)
            {
                if (lateRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                if (slowRegistered)
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                if (updateRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            }

            _lifecycleRuntimeState.RegisteredToTickManager = registered ? (byte)1 : (byte)0;
        }

        private bool TryGetScatterCenterCell(out int centerCellX, out int centerCellZ)
        {
            centerCellX = 0;
            centerCellZ = 0;
            if (!TryGetObserverAbsolutePosition(out Vector3 center))
                return false;

            float size = math.max(6f, _runtimeStreamingState.CellSize);
            centerCellX = WorldToScatterCellIndex(center.x, size);
            centerCellZ = WorldToScatterCellIndex(center.z, size);
            return true;
        }

        private static int WorldToScatterCellIndex(float coordinate, float size)
        {
            return (int)math.floor(coordinate / size);
        }

        private static int WorldToScatterCellIndex(double coordinate, float size)
        {
            return (int)math.floor(coordinate / math.max(0.001d, (double)size));
        }

        private void ClearScatterWorkingBuffers()
        {
            Array.Clear(_layerTopCandidatesBuffer, 0, _layerTopCandidatesBuffer.Length);
            Array.Clear(_layerTopValidBuffer, 0, _layerTopValidBuffer.Length);
            Array.Clear(_layerPlacementCountsBuffer, 0, _layerPlacementCountsBuffer.Length);
            Array.Clear(_clusterAccentCountsBuffer, 0, _clusterAccentCountsBuffer.Length);
            Array.Clear(_structureAccentCountsBuffer, 0, _structureAccentCountsBuffer.Length);
            ClearDictionaryArray(_layerFamilyCountsBuffer);
            ClearDictionaryArray(_layerBiomeCountsBuffer);
            ReleaseRescueCandidateBuffers();
            _sampledMatrixProfileCounts.Clear();
            _sampledMatrixBiomeCounts.Clear();
            _sampledBiomeCounts.Clear();
            _sampledPatternCounts.Clear();
            _sampledZoneCounts.Clear();
            _memory.HasCachedPatternQuota = false;
            _memory.HasCachedBudgetScales = false;
        }

        private static void ClearDictionaryArray(Dictionary<string, int>[] dictionaries)
        {
            if (dictionaries == null)
                return;

            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i]?.Clear();
        }

        private ScatterPlacement GetPooledPlacement()
        {
            ScatterPlacement placement;
            if (_placementPool.Count > 0)
            {
                placement = _placementPool.Pop();
            }
#if UNITY_EDITOR
            else if (!Application.isPlaying)
            {
                // COLD ALLOC: editor-only preview rebuilds may exceed runtime pool sizing without affecting player streaming GC.
                placement = new ScatterPlacement();
            }
#endif
            else
            {
                LogPlacementPoolExhausted();
                return null;
            }

            placement.IsPooled = false;
            placement.ReferenceCount = 1;
            return placement;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlacementPoolExhausted()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_placementPoolExhaustedWarningLogged)
                return;

            _placementPoolExhaustedWarningLogged = true;
            Hecton8.Core.H8Debug.LogWarning(PlacementPoolExhaustedWarning);
#endif
        }

        private static void RetainPlacement(ScatterPlacement placement)
        {
            if (placement == null || placement.IsPooled)
                return;

            placement.ReferenceCount++;
        }

        private void ReleasePlacement(ScatterPlacement placement)
        {
            if (placement == null || placement.IsPooled)
                return;

            if (placement.ReferenceCount > 0)
            {
                placement.ReferenceCount--;
                if (placement.ReferenceCount > 0)
                    return;
            }

            placement.Reset();
            placement.IsPooled = true;
            _placementPool.Push(placement);
        }

        private void ReleaseCandidateListPlacements(List<ScatterCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            for (int i = 0; i < candidates.Count; i++)
                ReleasePlacement(candidates[i].Placement);

            candidates.Clear();
        }

        private void ReleaseCandidateDictionaryPlacements(Dictionary<long, ScatterCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return;

            Dictionary<long, ScatterCandidate>.Enumerator enumerator = candidates.GetEnumerator();
            while (enumerator.MoveNext())
                ReleasePlacement(enumerator.Current.Value.Placement);

            candidates.Clear();
        }

        private void ReleaseCandidateMapPlacements(ref CandidateMap candidates)
        {
            for (int i = 0; i < candidates.count; i++)
                ReleasePlacement(candidates.GetValueAtOrderedIndex(i).Placement);

            candidates.Clear();
        }

        private void ReleasePlacementDictionaryValues(Dictionary<long, ScatterPlacement> placements)
        {
            if (placements == null || placements.Count == 0)
                return;

            Dictionary<long, ScatterPlacement>.Enumerator enumerator = placements.GetEnumerator();
            while (enumerator.MoveNext())
                ReleasePlacement(enumerator.Current.Value);

            placements.Clear();
        }

        private void ReleaseRescueCandidateBuffers()
        {
            if (_memory != null)
            {
                ReleaseCandidateMapPlacements(ref _groundRescueCandidates);
                ReleaseCandidateMapPlacements(ref _clusterRescueCandidates);
                ReleaseCandidateDictionaryPlacements(_structureRescueCandidates);
                ReleaseCandidateDictionaryPlacements(_spawnRescueCandidates);
                ReleaseCandidateMapPlacements(ref _clusterFertileCandidates);
                ReleaseCandidateMapPlacements(ref _clusterNestCandidates);
                ReleaseCandidateMapPlacements(ref _clusterResourceCandidates);
                ReleaseCandidateMapPlacements(ref _clusterShelterCandidates);
                ReleaseCandidateMapPlacements(ref _clusterHazardCandidates);
                ReleaseCandidateMapPlacements(ref _clusterDebrisCandidates);
                ReleaseCandidateMapPlacements(ref _clusterRockCandidates);
                ReleaseCandidateMapPlacements(ref _structureNaturalCandidates);
                ReleaseCandidateMapPlacements(ref _structureTechCandidates);
                ReleaseCandidateMapPlacements(ref _structureCaveCandidates);
                ReleaseCandidateMapPlacements(ref _structureBioCandidates);
                ReleaseCandidateMapPlacements(ref _passiveSpawnCandidates);
                ReleaseCandidateMapPlacements(ref _predatorSpawnCandidates);
            }
        }

        private void PrepareRuntimeRuleBuffer(IReadOnlyList<WorldProceduralPlacementRule> rules)
        {
            _runtimeRuleBuffer.Clear();
            if (rules == null)
            {
                RebuildScatterBackendLookup();
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                WorldProceduralPlacementRule rule = rules[i];
                if (rule == null || rule.familyProfile == null || !rule.familyProfile.allowRuntimeScatter)
                    continue;

                WorldPrefabFamilyProfile family = rule.familyProfile;
                _instancingService?.PrewarmFamilyPrototypeCacheCold(family);
                _instancingService?.PrewarmFamilyAggregationStorageCold(
                    family,
                    _floraGpuiKnownPrototypes,
                    _floraGpuiMatrices,
                    _floraGpuiCounts,
                    _floraGpuiBufferCapacities,
                    ResolveFloraGpuiPrewarmCapacity());
                float scoreBaseBonus = GetPlacementModeBonus(family.placementMode) + GetScatterLayerBonus(family.scatterLayer);
                string heatmapChannel = !string.IsNullOrWhiteSpace(rule.requiredHeatmapChannel)
                    ? rule.requiredHeatmapChannel
                    : family.heatmapChannel;
                bool hasGameplayIntent = !string.IsNullOrWhiteSpace(rule.gameplayIntent);
                int ruleIdHash = ComputeRuleIdHash(rule.ruleId);
                WorldStreamingLayer streamingLayer = family.ResolveStreamingLayer();
                WorldGenerativeGeologyProfile geologyProfile = ResolveEffectiveGenerativeGeologyProfile(family);
                bool hasMacroZone = streamingLayer == WorldStreamingLayer.LargeThreats || family.ResolveContributesLargeThreatZone();
                bool supportsFinalVariant = FamilySupportsFinalVariant(family);
                WorldPrefabFamilyProfile.ClusterAccentRole clusterAccentRole = GetClusterAccentRole(family);
                WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole = GetStructureAccentRole(family);
                bool passiveSpawnFamily = IsPassiveSpawnFamily(family);
                bool predatorSpawnFamily = IsPredatorSpawnFamily(family);
                float biomeAffinityWeight = Mathf.Clamp01(family.biomeAffinityWeight);
                float zoneAffinityWeight = Mathf.Clamp01(family.zoneAffinityWeight);
                float acceptedFamilyAffinityBonus = 0f;
                if (rule.preferredBiomeFamilies != null && rule.preferredBiomeFamilies.Length > 0)
                    acceptedFamilyAffinityBonus += biomeAffinityWeight;
                if (rule.preferredZoneKinds != null && rule.preferredZoneKinds.Length > 0)
                    acceptedFamilyAffinityBonus += zoneAffinityWeight;
                float patternAffinityWeight = Mathf.Clamp01(family.patternAffinityWeight);
                float patternMismatchScale = family.scatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Ground => 0.42f,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => 0.36f,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => 0.48f,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => 0.44f,
                    _ => 0.32f
                };

                _runtimeRuleBuffer.Add(new ScatterRuntimeRuleEntry(
                    rule,
                    family,
                    family.placementMode,
                    family.scatterLayer,
                    family.proceduralDomain,
                    rule.GetScatterContentKind(),
                    ruleIdHash,
                    heatmapChannel,
                    WorldProceduralFieldSampler.ResolveHeatmapChannelIndex(heatmapChannel),
                    scoreBaseBonus,
                    streamingLayer,
                    geologyProfile,
                    hasMacroZone,
                    supportsFinalVariant,
                    clusterAccentRole,
                    structureAccentRole,
                    passiveSpawnFamily,
                    predatorSpawnFamily,
                    family.primaryPattern,
                    family.secondaryPattern,
                    biomeAffinityWeight,
                    zoneAffinityWeight,
                    acceptedFamilyAffinityBonus,
                    patternAffinityWeight,
                    patternMismatchScale,
                    geologyProfile != null ? Mathf.Max(0.15f, geologyProfile.compositionWeight) : 0f,
                    hasGameplayIntent ? 0.95f + Mathf.Clamp01(rule.densityScale * 0.12f) : 1f,
                    rule.minDepthMeters,
                    rule.maxDepthMeters,
                    rule.minSlopeDegrees,
                    rule.maxSlopeDegrees,
                    rule.requiredSubstrate,
                    rule.maxTiltAngleDegrees,
                    rule.clusterNoiseScale,
                    rule.clusterNoiseThreshold,
                    rule.strictEnvelopeMapping));
            }

            RebuildScatterBackendLookup();
        }

        private void CommitScatterRebuildProfile(
            long rebuildStartTimestamp,
            long samplingInputsEndTimestamp,
            long samplingCompleteEndTimestamp,
            long samplingEndTimestamp,
            long rescueEndTimestamp,
            long restoreEndTimestamp,
            in ScatterReconcileMetrics reconcileMetrics,
            long diagnosticsEndTimestamp,
            int evaluatedCells)
        {
            ScatterDiagnosticsCommitContext context = BuildScatterDiagnosticsCommitContext(
                rebuildStartTimestamp,
                samplingInputsEndTimestamp,
                samplingCompleteEndTimestamp,
                samplingEndTimestamp,
                rescueEndTimestamp,
                restoreEndTimestamp,
                in reconcileMetrics,
                diagnosticsEndTimestamp,
                evaluatedCells);
            ScatterRebuildProfileSnapshot snapshot = ScatterDiagnosticsTracker.BuildRebuildProfileSnapshot(in context);
            RuntimePerformanceProfiler.RecordScatterRebuildProfile(in snapshot);
            ApplyScatterRebuildProfileSnapshot(in snapshot);
            EmitScatterRebuildProfileSnapshot(in snapshot);
        }

        private ScatterDiagnosticsCommitContext BuildScatterDiagnosticsCommitContext(
            long rebuildStartTimestamp,
            long samplingInputsEndTimestamp,
            long samplingCompleteEndTimestamp,
            long samplingEndTimestamp,
            long rescueEndTimestamp,
            long restoreEndTimestamp,
            in ScatterReconcileMetrics reconcileMetrics,
            long diagnosticsEndTimestamp,
            int evaluatedCells)
        {
            return new ScatterDiagnosticsCommitContext(
                rebuildStartTimestamp,
                samplingInputsEndTimestamp,
                samplingCompleteEndTimestamp,
                samplingEndTimestamp,
                rescueEndTimestamp,
                restoreEndTimestamp,
                in reconcileMetrics,
                diagnosticsEndTimestamp,
                evaluatedCells,
                _desiredPlacements.Count,
                _activeInstances.Count,
                _activeGpuiFloraPlacements,
                _floraGpuiKnownPrototypes.Count,
                floraGpuiManager != null,
                _debugZone,
                _debugBiomeFamily,
                _debugPattern,
                _debugTopFamily,
                _debugLastScatterRefreshReason);
        }

        private void ApplyScatterRebuildProfileSnapshot(in ScatterRebuildProfileSnapshot snapshot)
        {
            _debugLastScatterRebuildMs = snapshot.TotalMs;
            _debugSamplingStageMs = snapshot.SamplingMs;
            _debugRescueStageMs = snapshot.RescueMs;
            _debugRestoreStageMs = snapshot.RestoreMs;
            _debugReconcileStageMs = snapshot.ReconcileMs;
            _debugDiagnosticsStageMs = snapshot.DiagnosticsMs;
            _debugReconcileCleanupStageMs = snapshot.ReconcileCleanupMs;
            _debugReconcileSpawnStageMs = snapshot.ReconcileSpawnMs;
            _debugReconcileFaunaStageMs = snapshot.ReconcileFaunaMs;
            _debugReconcileRemovedCount = snapshot.RemovedCount;
            _debugReconcileRebuiltCount = snapshot.RebuiltCount;
            _debugReconcileCreatedCount = snapshot.CreatedCount;
            _debugReconcileReusedCount = snapshot.ReusedCount;
        }

        private void EmitScatterRebuildProfileSnapshot(in ScatterRebuildProfileSnapshot snapshot)
        {
            bool traceActive = RuntimeDiagnosticsTrace.IsActive;
            bool spikeDetected = snapshot.TotalMs >= Mathf.Max(1f, scatterRebuildSpikeThresholdMs);
            bool shouldLog = ShouldLogScatterRebuildSpike(spikeDetected);
            if (!traceActive && !shouldLog)
                return;

            ScatterDiagnosticsTracker.EmitRebuildReport(
                this,
                traceActive,
                shouldLog,
                snapshot);
        }

        private bool ShouldLogScatterRebuildSpike(bool spikeDetected)
        {
            if (!Application.isPlaying)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return spikeDetected ||
                   (enableScatterDetailedDiagnostics &&
                    (_activeGpuiFloraPlacements > 0 ||
                     _debugLastScatterRefreshReason == "startup" ||
                     _debugLastScatterRefreshReason == "scene-bootstrap" ||
                     _debugLastScatterRefreshReason == "scene-bootstrap-failed"));
#else
            return false;
#endif
        }

        private bool ShouldLogScatterLifecycleDiagnostics()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying || !enableScatterDetailedDiagnostics)
                return false;

            float now = RuntimeNowSeconds();
            return now >= _nextScatterLifecycleLogTime;
#else
            return false;
#endif
        }

        private bool ShouldCollectScatterDetailedDiagnostics()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return enableScatterDetailedDiagnostics;
#else
            return false;
#endif
        }

        private void SubscribeToBootstrap()
        {
            if (_lifecycleRuntimeState.SubscribedToBootstrap != 0)
                return;

            GameBootstrapper.Register(this);
            _lifecycleRuntimeState.SubscribedToBootstrap = 1;
        }

        private void UnsubscribeFromBootstrap()
        {
            if (_lifecycleRuntimeState.SubscribedToBootstrap == 0)
                return;

            GameBootstrapper.Unregister(this);
            _lifecycleRuntimeState.SubscribedToBootstrap = 0;
        }

        public void ClearScatterPreview()
        {
            EnsureWorkingMemory();

            GameObject root = GetScatterRoot(false);
            if (root != null && _activeInstances.Count == 0)
                ClearRootChildren(root.transform);

            CollectAllActiveScatterInstanceKeys(_removalBuffer);
            RemoveBufferedActiveScatterInstances(_removalBuffer);
            ClearFloraGpuiVisibility();
            _removalBuffer.Clear();
            ReleaseCandidateListPlacements(_candidateBuffer);
            ReleaseRescueCandidateBuffers();
            ReleasePlacementDictionaryValues(_desiredPlacements);
            ReleasePlacementDictionaryValues(_retainedPlacements);
            _placementLastSeenTimes.Clear();
            ResetPlacementGrid();
            faunaSpawnRegistry?.Clear();
            _faunaSnapshotDirty = false;
            ResetDiagnostics();
        }

        /// <summary>
        /// Copies the current edit/play scatter preview placements into a caller-owned buffer for SceneView gizmo drawing.
        /// </summary>
        public void BuildScatterPreviewGizmoSnapshot(List<ScatterPreviewGizmoRecord> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            ScatterWorkingMemory memory = _memory;
            if (memory == null)
                return;

            Dictionary<long, ScatterPlacement> desiredPlacements = memory.DesiredPlacements;
            if (desiredPlacements == null || desiredPlacements.Count == 0)
                return;

            Dictionary<long, ScatterPlacement>.Enumerator enumerator = desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement == null || placement.Family == null || placement.Rule == null)
                    continue;

                buffer.Add(new ScatterPreviewGizmoRecord(
                    placement.Position,
                    placement.EffectiveSpacing,
                    placement.DepthMeters,
                    placement.SlopeDegrees,
                    placement.Family.scatterLayer,
                    placement.Rule.requiredSubstrate,
                    placement.Rule.maxTiltAngleDegrees));
            }
        }

        private void EnsureCandidateMapsInitialized()
        {
            EnsureWorkingMemory();
            _memory.EnsureCandidateMapsInitialized();
        }

        private void ResetDiagnostics()
        {
            ResetCoreDiagnostics();
            ResetStringDiagnostics();
            ResetCountDiagnostics();
            ResetTimingAndReconcileDiagnostics();

            ScatterHybridRuntimePlan backendPlan = RefreshScatterBackendPlan();
            _scatterBackendHost?.ResetTelemetry();
            ResetScatterBackendDebugTelemetry(backendPlan);
        }

        private void ResetCoreDiagnostics()
        {
            _debugReady = false;
            _debugEvaluatedCells = 0;
            _debugDesiredPlacements = 0;
            _debugActivePlacements = 0;
            _debugGroundPlacements = 0;
            _debugClusterPlacements = 0;
            _debugStructurePlacements = 0;
            _debugSpawnPlacements = 0;
            _debugMapMagicSamples = 0;
            _debugSceneProbeLegacySamples = 0;
            _debugFallbackSamples = 0;
            _debugMatchedScatterRules = 0;
            _debugHeatPassedRules = 0;
            _debugGatePassedRules = 0;
            _debugResidencyPassedCandidates = 0;
            _debugPostBuildGateRejectedCandidates = 0;
            _debugQueuedCandidates = 0;
            _debugBiomeInfluenceGridCells = 0;
            _debugBiomeInfluenceGpuBufferCapacity = 0;
            _debugBiomeInfluenceTransitionCells = 0;
            _debugFloraQuotaRejectedCandidates = 0;
            _debugRejectedResidencyDistance = 0f;
            _debugRejectedResidencyRadius = 0f;
            _debugMaxCandidatesBeforePrunePerCell = 0;
            _debugMaxCandidatesAfterPrunePerCell = 0;
            _debugTrackedSpawnRescueCandidates = 0;
            _debugInjectedSpawnRescuePlacements = 0;
        }

        private void ResetStringDiagnostics()
        {
            _debugRejectedResidencyFamily = "None";
            _debugZone = "None";
            _debugBiomeMatrixProfile = "None";
            _debugBiomeFamily = "None";
            _debugPattern = "None";
            _debugResolvedPatternProfile = "None";
            _debugUsedFallbackPatternProfile = false;
            _debugResolvedBiomeContextProfile = "None";
            _debugUsedFallbackBiomeContextProfile = false;
            _debugTopRule = "None";
            _debugTopFamily = "None";
            _debugTopHeatmap = "None";
            _debugGroundTopFamily = "None";
            _debugClusterTopFamily = "None";
            _debugStructureTopFamily = "None";
            _debugSpawnTopFamily = "None";
            _debugGroundDominantFamily = "None";
            _debugClusterDominantFamily = "None";
            _debugStructureDominantFamily = "None";
            _debugSpawnDominantFamily = "None";
            _debugSampleDominantMatrixBiome = "None";
            _debugSampleDominantBiomeFamily = "None";
            _debugSampleDominantPattern = "None";
            _debugSampleDominantZone = "None";
            _debugGroundDominantBiomeFamily = "None";
            _debugClusterDominantBiomeFamily = "None";
            _debugStructureDominantBiomeFamily = "None";
            _debugSpawnDominantBiomeFamily = "None";
            _debugClusterDominantAccentRole = "None";
            _debugStructureDominantAccentRole = "None";
        }

        private void ResetCountDiagnostics()
        {
            _debugGroundDominantCount = 0;
            _debugClusterDominantCount = 0;
            _debugStructureDominantCount = 0;
            _debugSpawnDominantCount = 0;
            _debugSampleDominantMatrixCount = 0;
            _debugSampleDominantBiomeCount = 0;
            _debugSampleDominantPatternCount = 0;
            _debugSampleDominantZoneCount = 0;
            _debugClusterDominantAccentCount = 0;
            _debugStructureDominantAccentCount = 0;
            _debugClusterFertileGrowthCount = 0;
            _debugClusterBiologicalNestCount = 0;
            _debugClusterResourcePocketCount = 0;
            _debugClusterShelterPocketCount = 0;
            _debugClusterHazardPocketCount = 0;
            _debugClusterDebrisFieldCount = 0;
            _debugClusterRockCoverCount = 0;
            _debugStructureNaturalLandmarkCount = 0;
            _debugStructureTechFragmentCount = 0;
            _debugStructureCaveReadCount = 0;
            _debugStructureBiologicalSilhouetteCount = 0;
            _debugSpawnPassiveCount = 0;
            _debugSpawnPredatorCount = 0;
            _debugTargetGroundMin = 0;
            _debugTargetGroundMax = 0;
            _debugTargetClusterMin = 0;
            _debugTargetClusterMax = 0;
            _debugTargetStructureMin = 0;
            _debugTargetStructureMax = 0;
            _debugTargetSpawnMin = 0;
            _debugTargetSpawnMax = 0;
            _debugPatternGroundBudgetScale = 1f;
            _debugPatternClusterBudgetScale = 1f;
            _debugPatternStructureBudgetScale = 1f;
            _debugPatternSpawnBudgetScale = 1f;
            _debugTopHeat = 0f;
            _debugTopScore = 0f;
        }

        private void ResetTimingAndReconcileDiagnostics()
        {
            _debugRuntimeCellSize = _runtimeStreamingState.CellSize;
            _debugRuntimeRadiusCells = _runtimeStreamingState.RadiusCells;
            _debugRuntimeChunkSize = _runtimeStreamingState.ChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeStreamingState.MacroZoneSize;
            _debugGeneratedGeologyCount = 0;
            _debugPublishedFaunaAnchors = 0;
            _debugPublishedLargeThreatZones = 0;
            _debugLastScatterRebuildMs = 0f;
            _debugSamplingStageMs = 0f;
            _debugRescueStageMs = 0f;
            _debugRestoreStageMs = 0f;
            _debugReconcileStageMs = 0f;
            _debugDiagnosticsStageMs = 0f;
            _debugReconcileCleanupStageMs = 0f;
            _debugReconcileSpawnStageMs = 0f;
            _debugReconcileFaunaStageMs = 0f;
            _debugReconcileRemovedCount = 0;
            _debugReconcileRebuiltCount = 0;
            _debugReconcileCreatedCount = 0;
            _debugReconcileReusedCount = 0;
        }

        private ScatterCandidatePreview BuildCandidatePreview(
            int cellXIndex,
            int cellZIndex,
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule,
            float size)
        {
            WorldPrefabFamilyProfile family = runtimeRule.Family;
            WorldProceduralPlacementRule rule = runtimeRule.Rule;
            int heightLayerIndex = ResolveHeightLayerIndex(fieldSample, runtimeRule);
            int stableHash = ComputePlacementStableHash(runtimeRule.RuleIdHash, cellXIndex, cellZIndex, heightLayerIndex);
            Vector3 position = ResolvePlacementPosition(fieldSample.position, family, rule, stableHash, size);
            position.y = ShouldProjectToAbyssalSiltFalseCeiling(fieldSample, family)
                ? abyssalSiltFalseCeilingY
                : fieldSample.seafloorHeight + surfaceYOffset;
            return new ScatterCandidatePreview(
                stableHash,
                ToAbsoluteScatterPosition(position),
                heightLayerIndex,
                cellXIndex,
                cellZIndex);
        }

        private bool ShouldProjectToAbyssalSiltFalseCeiling(in WorldProceduralFieldSampler.FieldSample fieldSample, WorldPrefabFamilyProfile family)
        {
            if (!enableAbyssalSiltFalseCeiling || family == null)
                return false;

            if (family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.Kelp &&
                family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.Plant &&
                family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.Coral)
            {
                return false;
            }

            if ((fieldSample.biomeInfluence.Flags & (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.SargassumCanopy) != 0)
                return true;

            return IsAbyssalSiltProfile(fieldSample.biomeProfile) || IsAbyssalSiltFamily(fieldSample.biomeFamily);
        }

        private static bool IsAbyssalSiltProfile(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (string.Equals(profile.familyId, "biome.family.abyssal_silt", System.StringComparison.OrdinalIgnoreCase))
                return true;

            return IsAbyssalSiltFamily(profile.familyProfile);
        }

        private static bool IsAbyssalSiltFamily(HectonBiomeFamilyProfile family)
        {
            return family != null &&
                   string.Equals(family.familyId, "biome.family.abyssal_silt", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool TryBuildCandidate(
            int cellXIndex,
            int cellZIndex,
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterCandidatePreview preview,
            string biomeContextLabel,
            float heat,
            float score,
            out ScatterCandidate candidate)
        {
            candidate = default;
            WorldPrefabFamilyProfile family = runtimeRule.Family;
            WorldProceduralPlacementRule rule = runtimeRule.Rule;
            WorldStreamingLayer streamingLayer = runtimeRule.StreamingLayer;
            WorldGenerativeGeologyProfile geologyProfile = runtimeRule.GeologyProfile;
            bool hasMacroZone = runtimeRule.HasMacroZone;
            bool supportsFinalVariant = runtimeRule.SupportsFinalVariant;

            ScatterPlacement placement = GetPooledPlacement();
            if (placement == null)
                return false;

            placement.Initialize(
                ComposePlacementKey(cellXIndex, cellZIndex, runtimeRule.RuleIdHash, preview.HeightLayerIndex),
                preview.StableHash,
                family,
                rule,
                fieldSample.zone,
                fieldSample.biomeFamily,
                fieldSample.biomeProfile,
                fieldSample.resolvedPattern,
                biomeContextLabel,
                streamingLayer,
                geologyProfile,
                null,
                supportsFinalVariant,
                runtimeRule.HeatmapChannel,
                heat,
                fieldSample.seafloorSource,
                fieldSample.seafloorHeight,
                fieldSample.depthMeters,
                fieldSample.slopeDegrees,
                fieldSample.curvature,
                fieldSample.caveProximity,
                fieldSample.ridgeSignal,
                fieldSample.canyonSignal,
                fieldSample.compositionPotential,
                preview.HeightLayerIndex,
                cellXIndex,
                cellZIndex,
                default,
                hasMacroZone,
                default,
                preview.Position,
                Quaternion.identity,
                1f,
                false);

            candidate = new ScatterCandidate(placement, family, rule, runtimeRule.HeatmapChannel, heat, score);
            return true;
        }

        private static bool MatchesScatter(
            in ScatterRuntimeRuleEntry runtimeRule,
            HectonBiomeFamilyProfile biomeFamily,
            HectonBiomeFamilyProfile secondaryBiomeFamily,
            bool hasSecondaryBiome,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind zoneKindHint,
            float depthMeters,
            float slopeDegrees,
            ulong biomeFamilyFlags,
            WorldTerrainDetailEligibilityFlags terrainEligibilityFlags,
            WorldTerrainSurfaceMaterialClass terrainMaterialClass,
            byte hasTerrainDetailSample)
        {
            if (depthMeters < runtimeRule.MinDepthMeters || depthMeters > runtimeRule.MaxDepthMeters)
                return false;

            if (slopeDegrees < runtimeRule.MinSlopeDegrees || slopeDegrees > runtimeRule.MaxSlopeDegrees)
                return false;

            if (!PassesTerrainDetailEligibility(
                    in runtimeRule,
                    terrainEligibilityFlags,
                    terrainMaterialClass,
                    hasTerrainDetailSample))
            {
                return false;
            }

            if (!runtimeRule.StrictEnvelopeMapping &&
                runtimeRule.PreferredBiomeFamilies != null &&
                runtimeRule.PreferredBiomeFamilies.Length > 0)
            {
                bool primaryMatched = MatchesPreferredBiomeFamily(runtimeRule.PreferredBiomeFamilies, biomeFamily);
                bool secondaryMatched = hasSecondaryBiome &&
                                        secondaryBiomeFamily != null &&
                                        MatchesPreferredBiomeFamily(runtimeRule.PreferredBiomeFamilies, secondaryBiomeFamily);
                if (!primaryMatched &&
                    !secondaryMatched &&
                    !AllowsTectonicSpineRockBoulderOverride(runtimeRule.Family, slopeDegrees, biomeFamilyFlags))
                {
                    return false;
                }
            }

            if (!runtimeRule.StrictEnvelopeMapping &&
                runtimeRule.PreferredZoneKinds != null &&
                runtimeRule.PreferredZoneKinds.Length > 0)
            {
                bool zoneMatched = false;
                WorldZoneAnchor.ZoneKind effectiveZoneKind = zone != null ? zone.Kind : zoneKindHint;
                for (int i = 0; i < runtimeRule.PreferredZoneKinds.Length; i++)
                {
                    if (runtimeRule.PreferredZoneKinds[i] != effectiveZoneKind)
                        continue;

                    zoneMatched = true;
                    break;
                }

                if (!zoneMatched)
                    return false;
            }

            if (!runtimeRule.StrictEnvelopeMapping &&
                runtimeRule.PreferredSocketKinds != null &&
                runtimeRule.PreferredSocketKinds.Length > 0)
            {
                bool kindMatched = false;
                for (int i = 0; i < runtimeRule.PreferredSocketKinds.Length; i++)
                {
                    if (runtimeRule.PreferredSocketKinds[i] != runtimeRule.ScatterKind)
                        continue;

                    kindMatched = true;
                    break;
                }

                if (!kindMatched)
                    return false;
            }

            return true;
        }

        private static bool PassesTerrainDetailEligibility(
            in ScatterRuntimeRuleEntry runtimeRule,
            WorldTerrainDetailEligibilityFlags terrainEligibilityFlags,
            WorldTerrainSurfaceMaterialClass terrainMaterialClass,
            byte hasTerrainDetailSample)
        {
            if (hasTerrainDetailSample == 0)
                return true;

            WorldTerrainDetailEligibilityFlags required = ResolveTerrainDetailEligibilityRequirement(in runtimeRule);
            if (required == WorldTerrainDetailEligibilityFlags.None)
                return true;

            if ((terrainEligibilityFlags & required) != 0)
                return true;

            return AllowsDominantMaterialFallback(required, terrainMaterialClass);
        }

        private static WorldTerrainDetailEligibilityFlags ResolveTerrainDetailEligibilityRequirement(
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            WorldTerrainDetailEligibilityFlags required = WorldTerrainDetailEligibilityFlags.None;
            switch (runtimeRule.ProceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Rock:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockCluster:
                case WorldPrefabFamilyProfile.ProceduralDomain.RockShelf:
                    required |= WorldTerrainDetailEligibilityFlags.RockScatter |
                                WorldTerrainDetailEligibilityFlags.TalusBoulder |
                                WorldTerrainDetailEligibilityFlags.RubblePebble;
                    break;
                case WorldPrefabFamilyProfile.ProceduralDomain.RockArch:
                case WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance:
                    required |= WorldTerrainDetailEligibilityFlags.VoxelAnchor |
                                WorldTerrainDetailEligibilityFlags.CaveMouthCandidate |
                                WorldTerrainDetailEligibilityFlags.RockScatter;
                    break;
                case WorldPrefabFamilyProfile.ProceduralDomain.Coral:
                    required |= WorldTerrainDetailEligibilityFlags.ReefScatter |
                                WorldTerrainDetailEligibilityFlags.RubblePebble;
                    break;
                case WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket:
                    required |= WorldTerrainDetailEligibilityFlags.NoduleScatter |
                                WorldTerrainDetailEligibilityFlags.SeepDeposit |
                                WorldTerrainDetailEligibilityFlags.RockScatter;
                    break;
                case WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket:
                    required |= WorldTerrainDetailEligibilityFlags.BrineDeposit |
                                WorldTerrainDetailEligibilityFlags.SeepDeposit;
                    break;
            }

            required |= ResolveTerrainDetailKeywordRequirement(runtimeRule.HeatmapChannel);
            WorldPrefabFamilyProfile family = runtimeRule.Family;
            if (family != null)
            {
                required |= ResolveTerrainDetailKeywordRequirement(family.familyId);
                required |= ResolveTerrainDetailKeywordRequirement(family.familyLabel);
                required |= ResolveTerrainDetailKeywordRequirement(family.gameplayRole);
            }

            WorldProceduralPlacementRule rule = runtimeRule.Rule;
            if (rule != null)
                required |= ResolveTerrainDetailKeywordRequirement(rule.gameplayIntent);

            return required;
        }

        private static WorldTerrainDetailEligibilityFlags ResolveTerrainDetailKeywordRequirement(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return WorldTerrainDetailEligibilityFlags.None;

            WorldTerrainDetailEligibilityFlags flags = WorldTerrainDetailEligibilityFlags.None;
            if (ContainsToken(value, "nodule") || ContainsToken(value, "manganese"))
                flags |= WorldTerrainDetailEligibilityFlags.NoduleScatter;
            if (ContainsToken(value, "reef") || ContainsToken(value, "coral"))
                flags |= WorldTerrainDetailEligibilityFlags.ReefScatter;
            if (ContainsToken(value, "brine") || ContainsToken(value, "salt"))
                flags |= WorldTerrainDetailEligibilityFlags.BrineDeposit;
            if (ContainsToken(value, "seep") || ContainsToken(value, "methane"))
                flags |= WorldTerrainDetailEligibilityFlags.SeepDeposit;
            if (ContainsToken(value, "talus") || ContainsToken(value, "boulder"))
                flags |= WorldTerrainDetailEligibilityFlags.TalusBoulder;
            if (ContainsToken(value, "rubble") || ContainsToken(value, "pebble"))
                flags |= WorldTerrainDetailEligibilityFlags.RubblePebble;
            if (ContainsToken(value, "rock") || ContainsToken(value, "stone") || ContainsToken(value, "limestone"))
                flags |= WorldTerrainDetailEligibilityFlags.RockScatter;
            if (ContainsToken(value, "sand") || ContainsToken(value, "silt") || ContainsToken(value, "sediment"))
                flags |= WorldTerrainDetailEligibilityFlags.SandScatter;
            if (ContainsToken(value, "cave") || ContainsToken(value, "arch") || ContainsToken(value, "pillar"))
                flags |= WorldTerrainDetailEligibilityFlags.VoxelAnchor |
                         WorldTerrainDetailEligibilityFlags.CaveMouthCandidate;

            return flags;
        }

        private static bool ContainsToken(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool AllowsDominantMaterialFallback(
            WorldTerrainDetailEligibilityFlags required,
            WorldTerrainSurfaceMaterialClass material)
        {
            if ((required & WorldTerrainDetailEligibilityFlags.RockScatter) != 0 &&
                (material == WorldTerrainSurfaceMaterialClass.HardRock ||
                 material == WorldTerrainSurfaceMaterialClass.LimestoneShelf ||
                 material == WorldTerrainSurfaceMaterialClass.ReefRubble))
            {
                return true;
            }

            if ((required & WorldTerrainDetailEligibilityFlags.SandScatter) != 0 &&
                (material == WorldTerrainSurfaceMaterialClass.ShellSand ||
                 material == WorldTerrainSurfaceMaterialClass.ClaySilt))
            {
                return true;
            }

            if ((required & WorldTerrainDetailEligibilityFlags.ReefScatter) != 0 &&
                material == WorldTerrainSurfaceMaterialClass.ReefRubble)
            {
                return true;
            }

            if ((required & WorldTerrainDetailEligibilityFlags.BrineDeposit) != 0 &&
                material == WorldTerrainSurfaceMaterialClass.BrineSaltCrust)
            {
                return true;
            }

            if ((required & WorldTerrainDetailEligibilityFlags.SeepDeposit) != 0 &&
                material == WorldTerrainSurfaceMaterialClass.SeepCrust)
            {
                return true;
            }

            if ((required & WorldTerrainDetailEligibilityFlags.NoduleScatter) != 0 &&
                material == WorldTerrainSurfaceMaterialClass.ManganeseNodulePlain)
            {
                return true;
            }

            return false;
        }

        private static bool AllowsTectonicSpineRockBoulderOverride(
            WorldPrefabFamilyProfile family,
            float slopeDegrees,
            ulong biomeFamilyFlags)
        {
            return slopeDegrees >= 45f &&
                   (((WorldProceduralFieldSampler.BiomeFamilyFlags)biomeFamilyFlags & WorldProceduralFieldSampler.BiomeFamilyFlags.Tectonic) != 0) &&
                   IsRiftSideRockBoulderDomain(family);
        }

        private static bool AllowsTectonicSpineRockBoulderOverride(
            WorldPrefabFamilyProfile family,
            float slopeDegrees,
            bool isTectonicSpineBiome)
        {
            return slopeDegrees >= 45f &&
                   isTectonicSpineBiome &&
                   IsRiftSideRockBoulderDomain(family);
        }

        private static bool IsTectonicSpineBiomeFamily(HectonBiomeFamilyProfile biomeFamily)
        {
            return biomeFamily != null &&
                   string.Equals(biomeFamily.familyId, TectonicSpineBiomeFamilyId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRiftSideRockBoulderDomain(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Rock ||
                   family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.RockCluster ||
                   family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.RockArch ||
                   family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Landmark;
        }

        private static float GetTectonicSpineRockBoulderScoreBonus(
            in WorldProceduralFieldSampler.FieldSample sample,
            WorldPrefabFamilyProfile family)
        {
            if (!AllowsTectonicSpineRockBoulderOverride(family, sample.slopeDegrees, sample.biomeFamilyFlags))
                return 0f;

            return math.lerp(0.24f, 0.42f, math.saturate((sample.slopeDegrees - 45f) / 30f));
        }

        private static bool MatchesPreferredBiomeFamily(
            HectonBiomeFamilyProfile[] preferredBiomeFamilies,
            HectonBiomeFamilyProfile biomeFamily)
        {
            if (preferredBiomeFamilies == null || preferredBiomeFamilies.Length == 0 || biomeFamily == null)
                return false;

            for (int i = 0; i < preferredBiomeFamilies.Length; i++)
            {
                HectonBiomeFamilyProfile preferredBiomeFamily = preferredBiomeFamilies[i];
                if (preferredBiomeFamily == null || preferredBiomeFamily != biomeFamily)
                    continue;

                return true;
            }

            return false;
        }

        private static int ResolveHeightLayerIndex(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterCandidateEvaluator.ResolveHeightLayerIndex(fieldSample, runtimeRule);
        }

        private static int ResolveHeightLayerIndex(ScatterPlacement placement)
        {
            return placement != null ? placement.HeightLayerIndex : 0;
        }

        private static int ResolveHeightLayerIndex(
            float caveProximity,
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole)
        {
            return ScatterCandidateEvaluator.ResolveHeightLayerIndex(caveProximity, family, structureAccentRole);
        }

        private static bool ShouldEvaluateScatterDomain(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterCandidateEvaluator.ShouldEvaluateScatterDomain(fieldSample, runtimeRule);
        }

        private ScatterReconcileMetrics ReconcileInstances(bool captureProfiling)
        {
            using (_scatterReconcileProfilerMarker.Auto())
            {
                long reconcileStartTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : 0L;
                Transform root = GetOrCreateRoot().transform;
                bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
                WorldGenerativeGeologyService cachedGeologyService = generativeGeologyService;
                if (_activeInstances.Count == 0)
                    ClearRootChildren(root);

                bool initialWarmupPass = Application.isPlaying &&
                                         (_activeInstances.Count == 0 || _reconcileRuntimeState.HasPendingStartupPlacements != 0);
                int remainingInitialCreateBudget = initialWarmupPass
                    ? (spreadInitialScatterWarmupAcrossTicks
                        ? math.max(1, maxInitialScatterCreatesPerRebuild)
                        : int.MaxValue)
                    : int.MaxValue;
                _reconcileRuntimeState.HasPendingStartupPlacements = 0;
                _reconcileRuntimeState.HasPendingRuntimePlacements = 0;
                ResetFloraGpuiAggregation();

                ScatterReconcileCleanupContext cleanupContext = new ScatterReconcileCleanupContext(
                    _activeInstances,
                    _desiredPlacements,
                    _removalBuffer);
                using (_scatterReconcileCleanupProfilerMarker.Auto())
                {
                    RemoveStaleScatterInstances(ref cleanupContext);
                }

                long cleanupEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : reconcileStartTimestamp;
                if (!hasObserverPosition ||
                    _reconcileRuntimeState.HasObserverSample == 0 ||
                    _reconcileRuntimeState.LastObserverPosition != observerPosition)
                {
                    InvalidateResolvedPlacementVariantCache();
                    _reconcileRuntimeState.HasObserverSample = hasObserverPosition ? (byte)1 : (byte)0;
                    _reconcileRuntimeState.LastObserverPosition = observerPosition;
                }
                _reconcileRuntimeState.PlanVersion = _reconcileRuntimeState.PlanVersion == int.MaxValue ? 1 : _reconcileRuntimeState.PlanVersion + 1;
                PrepareScatterPoolWarmup(initialWarmupPass, remainingInitialCreateBudget, observerPosition, hasObserverPosition);
                ScatterReconcileExecutionContext reconcileContext = new ScatterReconcileExecutionContext(
                    root,
                    observerPosition,
                    hasObserverPosition,
                    cachedGeologyService,
                    initialWarmupPass,
                    remainingInitialCreateBudget);

                using (_scatterReconcileSpawnProfilerMarker.Auto())
                {
                    ReconcileDesiredPlacements(ref reconcileContext);
                }

                long spawnEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : cleanupEndTimestamp;
                using (_scatterReconcileFaunaProfilerMarker.Auto())
                {
                    PublishFaunaRegistrySnapshot();
                }

                long faunaEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : spawnEndTimestamp;

                _debugReconcileRemovedCount = cleanupContext.RemovedCount;
                _debugReconcileRebuiltCount = reconcileContext.RebuiltCount;
                _debugReconcileCreatedCount = reconcileContext.CreatedCount;
                _debugReconcileReusedCount = reconcileContext.ReusedCount;

                return new ScatterReconcileMetrics(
                    cleanupContext.RemovedCount,
                    reconcileContext.RebuiltCount,
                    reconcileContext.CreatedCount,
                    reconcileContext.ReusedCount,
                    cleanupEndTimestamp,
                    spawnEndTimestamp,
                    faunaEndTimestamp);
            }
        }

        private void ReconcileDesiredPlacements(ref ScatterReconcileExecutionContext reconcileContext)
        {
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, ScatterPlacement> pair = enumerator.Current;
                ScatterPlacement placement = pair.Value;
                ScatterPlacementReconcilePlan plan = ResolveReconcilePlan(
                    placement,
                    reconcileContext.ObserverPosition,
                    reconcileContext.HasObserverPosition);

                if (TryHandleFloraGpuiReconcile(pair.Key, placement, plan, ref reconcileContext))
                    continue;

                if (TryHandleExistingInstanceReconcile(pair.Key, placement, plan, ref reconcileContext))
                    continue;

                if (!TryHandleNewInstanceReconcile(pair.Key, placement, plan, ref reconcileContext))
                    break;
            }

            FlushFloraGpuiBuffers();
        }

        private void RemoveStaleScatterInstances(ref ScatterReconcileCleanupContext cleanupContext)
        {
            CollectStaleActiveScatterInstanceKeys(
                cleanupContext.ActiveInstances,
                cleanupContext.DesiredPlacements,
                cleanupContext.RemovalBuffer);
            cleanupContext.RemovedCount += RemoveBufferedActiveScatterInstances(cleanupContext.RemovalBuffer);
        }

        private void CollectAllActiveScatterInstanceKeys(List<long> removalBuffer)
        {
            removalBuffer.Clear();
            Dictionary<long, WorldProceduralProxyInstance>.Enumerator enumerator = _activeInstances.GetEnumerator();
            while (enumerator.MoveNext())
                removalBuffer.Add(enumerator.Current.Key);
        }

        private static void CollectStaleActiveScatterInstanceKeys(
            Dictionary<long, WorldProceduralProxyInstance> activeInstances,
            Dictionary<long, ScatterPlacement> desiredPlacements,
            List<long> removalBuffer)
        {
            removalBuffer.Clear();
            Dictionary<long, WorldProceduralProxyInstance>.Enumerator enumerator = activeInstances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, WorldProceduralProxyInstance> pair = enumerator.Current;
                if (desiredPlacements.ContainsKey(pair.Key))
                    continue;

                removalBuffer.Add(pair.Key);
            }
        }

        private int RemoveBufferedActiveScatterInstances(List<long> removalBuffer)
        {
            int removedCount = 0;
            for (int i = 0; i < removalBuffer.Count; i++)
            {
                long key = removalBuffer[i];
                if (!_activeInstances.TryGetValue(key, out WorldProceduralProxyInstance instance))
                    continue;

                if (instance != null)
                    DestroyProxyInstance(instance);

                _activeInstances.Remove(key);
                removedCount++;
            }

            return removedCount;
        }

        private bool TryHandleFloraGpuiReconcile(
            long placementKey,
            ScatterPlacement placement,
            ScatterPlacementReconcilePlan plan,
            ref ScatterReconcileExecutionContext reconcileContext)
        {
            if (!TryRegisterFloraGpuiPlacement(placement, plan.RuntimeVariant, out GPUInstancerPrefabPrototype floraPrototype))
                return false;

            if (plan.Instance != null)
            {
                DestroyProxyInstance(plan.Instance);
                _activeInstances.Remove(placementKey);
                reconcileContext.RebuiltCount++;
            }
            else
            {
                reconcileContext.CreatedCount++;
            }

            return true;
        }

        private bool TryHandleExistingInstanceReconcile(
            long placementKey,
            ScatterPlacement placement,
            ScatterPlacementReconcilePlan plan,
            ref ScatterReconcileExecutionContext reconcileContext)
        {
            if (plan.Instance == null)
                return false;

            if (plan.RequiresSpawn != 0)
            {
                if (!TryReserveScatterCreate(plan.RuntimeVariant, reconcileContext.InitialWarmupPass))
                {
                    MarkPendingScatterReconcile(reconcileContext.InitialWarmupPass);
                    return true;
                }

                DestroyProxyInstance(plan.Instance);
                GameObject rebuilt = CreateScatterInstance(
                    reconcileContext.Root,
                    placement,
                    plan.RuntimeVariant,
                    plan.FinalVariantActive != 0,
                    out WorldProceduralProxyInstance rebuiltMetadata);
                if (rebuilt == null)
                {
                    MarkPendingScatterReconcile(reconcileContext.InitialWarmupPass);
                    return true;
                }

                ApplyScatterReconcileSync(rebuiltMetadata, placement, plan, ref reconcileContext);
                _activeInstances[placementKey] = rebuiltMetadata;
                reconcileContext.RebuiltCount++;
                return true;
            }

            if (plan.Instance.IsScatterSyncCurrent(plan.SyncSignature, plan.ShouldApplyGeneratedGeology != 0))
            {
                reconcileContext.ReusedCount++;
                return true;
            }

            ApplyScatterReconcileSync(plan.Instance, placement, plan, ref reconcileContext);
            reconcileContext.ReusedCount++;
            return true;
        }

        private bool TryHandleNewInstanceReconcile(
            long placementKey,
            ScatterPlacement placement,
            ScatterPlacementReconcilePlan plan,
            ref ScatterReconcileExecutionContext reconcileContext)
        {
            if (reconcileContext.InitialWarmupPass && plan.AllowInitialWarmupCreate == 0)
                return true;

            if (reconcileContext.InitialWarmupPass && reconcileContext.RemainingInitialCreateBudget <= 0)
            {
                _reconcileRuntimeState.HasPendingStartupPlacements = 1;
                return false;
            }

            if (!TryReserveScatterCreate(plan.RuntimeVariant, reconcileContext.InitialWarmupPass))
            {
                MarkPendingScatterReconcile(reconcileContext.InitialWarmupPass);
                return true;
            }

            if (!TryCreateScatterReconcileInstance(
                    placementKey,
                    placement,
                    plan,
                    ref reconcileContext,
                    out WorldProceduralProxyInstance metadata))
            {
                return true;
            }

            reconcileContext.CreatedCount++;
            if (reconcileContext.InitialWarmupPass)
                reconcileContext.RemainingInitialCreateBudget--;

            return true;
        }

        private bool TryCreateScatterReconcileInstance(
            long placementKey,
            ScatterPlacement placement,
            ScatterPlacementReconcilePlan plan,
            ref ScatterReconcileExecutionContext reconcileContext,
            out WorldProceduralProxyInstance metadata)
        {
            GameObject go = CreateScatterInstance(
                reconcileContext.Root,
                placement,
                plan.RuntimeVariant,
                    plan.FinalVariantActive != 0,
                out metadata);
            if (go == null)
            {
                MarkPendingScatterReconcile(reconcileContext.InitialWarmupPass);
                return false;
            }

            ApplyScatterReconcileSync(metadata, placement, plan, ref reconcileContext);
            _activeInstances[placementKey] = metadata;
            return true;
        }

        private void ApplyScatterReconcileSync(
            WorldProceduralProxyInstance instance,
            ScatterPlacement placement,
            ScatterPlacementReconcilePlan plan,
            ref ScatterReconcileExecutionContext reconcileContext)
        {
            ApplyPlacement(instance, placement, plan.RuntimeVariant, plan.FinalVariantActive != 0);
            ApplyGeneratedGeology(
                instance,
                placement,
                plan.FinalVariantActive != 0,
                plan.ShouldApplyGeneratedGeology != 0,
                reconcileContext.CachedGeologyService,
                reconcileContext.ObserverPosition,
                reconcileContext.HasObserverPosition);
            instance.MarkScatterSync(plan.SyncSignature, plan.ShouldApplyGeneratedGeology != 0);
        }

        private void MarkPendingScatterReconcile(bool initialWarmupPass)
        {
            if (initialWarmupPass)
                _reconcileRuntimeState.HasPendingStartupPlacements = 1;
            else
                _reconcileRuntimeState.HasPendingRuntimePlacements = 1;
        }

        private void PublishFaunaRegistrySnapshot()
        {
            if (!_faunaSnapshotDirty)
                return;

            _faunaAnchorBuffer.Clear();

            if (faunaSpawnRegistry == null)
            {
                ResetPublishedFaunaSnapshotCounts();
                return;
            }

            int faunaAnchorCount = 0;
            int largeThreatZoneCount = 0;
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (!TryBuildFaunaRegistryAnchor(enumerator.Current.Value, out WorldFaunaSpawnRegistry.Anchor anchor, out bool largeThreatZone))
                    continue;

                _faunaAnchorBuffer.Add(anchor);
                if (largeThreatZone)
                    largeThreatZoneCount++;
                else
                    faunaAnchorCount++;
            }

            faunaSpawnRegistry.ReplaceProceduralAnchors(_faunaAnchorBuffer);
            _debugPublishedFaunaAnchors = faunaAnchorCount;
            _debugPublishedLargeThreatZones = largeThreatZoneCount;
            _faunaSnapshotDirty = false;
        }

        private void ResetPublishedFaunaSnapshotCounts()
        {
            _debugPublishedFaunaAnchors = 0;
            _debugPublishedLargeThreatZones = 0;
        }

        private bool TryBuildFaunaRegistryAnchor(
            ScatterPlacement placement,
            out WorldFaunaSpawnRegistry.Anchor anchor,
            out bool largeThreatZone)
        {
            anchor = default;
            largeThreatZone = false;
            if (!placement.IsFaunaAnchor)
                return false;

            largeThreatZone = placement.IsLargeThreatZone;
            Vector3 anchorAbsolutePosition = placement.Position;
            bool hasAnchorAup = math.all(math.isfinite(new float3(
                anchorAbsolutePosition.x,
                anchorAbsolutePosition.y,
                anchorAbsolutePosition.z)));
            AbsoluteUniversePosition anchorAup = hasAnchorAup
                ? AbsoluteUniversePosition.FromAbsolutePosition(new double3(anchorAbsolutePosition.x, anchorAbsolutePosition.y, anchorAbsolutePosition.z))
                : default;
            anchor = new WorldFaunaSpawnRegistry.Anchor
            {
                runtimeKey = placement.Key,
                position = placement.ReadRuntimePosition(),
                positionAup = anchorAup,
                hasPositionAup = hasAnchorAup,
                radius = placement.FaunaAnchorRadius,
                chunkCoord = placement.ChunkCoord,
                macroZoneCoord = placement.HasMacroZone
                    ? placement.MacroZoneCoord
                    : WorldMacroZoneCoordinate.FromWorldPosition(placement.Position, _runtimeStreamingState.MacroZoneSize),
                streamingLayer = placement.StreamingLayer,
                familyId = placement.Family != null ? placement.Family.familyId : "world.family.generic",
                isLargeThreatZone = largeThreatZone
            };
            return true;
        }

        private void PrepareScatterPoolWarmup(
            bool initialWarmupPass,
            int initialCreateBudget,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (!TryBuildScatterPoolWarmupContext(
                    initialWarmupPass,
                    initialCreateBudget,
                    observerPosition,
                    hasObserverPosition,
                    out ScatterPoolWarmupContext warmupContext))
            {
                return;
            }

            CollectScatterPoolWarmupDemand(ref warmupContext);
            ApplyScatterPoolWarmupAllowances(ref warmupContext);
            ClearScatterPoolWarmupScratch();
        }

        private bool TryBuildScatterPoolWarmupContext(
            bool initialWarmupPass,
            int initialCreateBudget,
            Vector3 observerPosition,
            bool hasObserverPosition,
            out ScatterPoolWarmupContext warmupContext)
        {
            warmupContext = default;
            if (_memory == null)
                return false;

            Dictionary<int, int> prefabCreateAllowances = _memory.PrefabCreateAllowances;
            prefabCreateAllowances.Clear();

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;

            ClearScatterPoolWarmupScratch();
            bool useExactStartupWarmup = initialWarmupPass && spreadInitialScatterWarmupAcrossTicks;
            warmupContext = new ScatterPoolWarmupContext(
                pool,
                prefabCreateAllowances,
                _memory.PrefabWarmupCounts,
                _memory.PrefabWarmupPrefabs,
                _memory.PrefabWarmupFamilyHashes,
                observerPosition,
                hasObserverPosition,
                initialWarmupPass,
                useExactStartupWarmup,
                initialWarmupPass
                    ? (useExactStartupWarmup
                        ? math.max(0, maxPoolWarmupPerRebuild)
                        : int.MaxValue)
                    : 0,
                initialWarmupPass
                    ? (useExactStartupWarmup
                        ? math.max(0, maxPoolWarmupPerPrefabPerRebuild)
                        : int.MaxValue)
                    : 0,
                initialWarmupPass
                    ? math.max(0, initialCreateBudget)
                    : int.MaxValue,
                RuntimeDiagnosticsTrace.IsActive);
            return true;
        }

        private void ClearScatterPoolWarmupScratch()
        {
            if (_memory == null)
                return;

            _memory.PrefabWarmupCounts.Clear();
            _memory.PrefabWarmupPrefabs.Clear();
            _memory.PrefabWarmupFamilyHashes.Clear();
        }

        private void CollectScatterPoolWarmupDemand(ref ScatterPoolWarmupContext warmupContext)
        {
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (warmupContext.InitialWarmupPass &&
                    !ShouldCreateDuringInitialWarmup(
                        placement,
                        warmupContext.ObserverPosition,
                        warmupContext.HasObserverPosition))
                {
                    continue;
                }

                if (warmupContext.InitialWarmupPass && warmupContext.RemainingInitialWarmupCreates <= 0)
                    break;

                ScatterPlacementReconcilePlan plan = ResolveReconcilePlan(
                    placement,
                    warmupContext.ObserverPosition,
                    warmupContext.HasObserverPosition);

                if (warmupContext.InitialWarmupPass && plan.AllowInitialWarmupCreate == 0)
                    continue;

                if (ShouldUseFloraGpuiPath(placement, plan.RuntimeVariant, out _))
                    continue;

                GameObject prefab = plan.RuntimeVariant != null ? plan.RuntimeVariant.prefab : null;
                if (prefab == null || plan.RequiresSpawn == 0)
                    continue;

                int familyHash = placement.Family != null ? placement.Family.FamilyHash : 0;
                RegisterWarmupPrefab(prefab, familyHash, 1);
                if (warmupContext.InitialWarmupPass)
                    warmupContext.RemainingInitialWarmupCreates--;
            }
        }

        private void ApplyScatterPoolWarmupAllowances(ref ScatterPoolWarmupContext warmupContext)
        {
            Dictionary<int, int>.Enumerator enumerator = warmupContext.PrefabWarmupCounts.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, int> pair = enumerator.Current;
                if (warmupContext.RemainingWarmupBudget <= 0)
                    break;

                if (!warmupContext.PrefabWarmupPrefabs.TryGetValue(pair.Key, out GameObject prefab) || prefab == null)
                    continue;

                warmupContext.PrefabWarmupFamilyHashes.TryGetValue(pair.Key, out int familyHash);
                int directDemandCount = math.max(0, pair.Value);
                int reserveCount = ResolveWarmupReserveCount(
                    familyHash,
                    directDemandCount,
                    warmupContext.InitialWarmupPass);
                int availableCount = warmupContext.Pool.GetAvailableCount(prefab);
                int reserveTopUp = reserveCount;

                int missingCount = directDemandCount + reserveTopUp - availableCount;
                if (warmupContext.UseExactStartupWarmup && missingCount > 0)
                {
                    int effectivePerPrefabLimit = warmupContext.PerPrefabWarmupLimit;
                    if (effectivePerPrefabLimit <= 0 || warmupContext.RemainingWarmupBudget <= 0)
                        continue;

                    int warmupCount = math.min(
                        math.min(missingCount, effectivePerPrefabLimit),
                        warmupContext.RemainingWarmupBudget);
                    if (warmupCount > 0)
                    {
                        int availableBeforeWarmup = availableCount;
                        warmupContext.Pool.Warmup(prefab, warmupCount);
                        warmupContext.RemainingWarmupBudget = math.max(0, warmupContext.RemainingWarmupBudget - warmupCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (warmupContext.DiagnosticsTraceActive)
                        {
                            int prefabInstanceId = unchecked((int)EntityId.ToULong(prefab.GetEntityId()));
                            RuntimeDiagnosticsTrace.WriteEvent(
                                "pool",
                                $"warmup familyHash={familyHash} prefabId={prefabInstanceId} count={warmupCount} availableBefore={availableBeforeWarmup} reserve={reserveCount} reserveTopUp={reserveTopUp} missing={missingCount} startup={warmupContext.UseExactStartupWarmup}");
                        }
#endif
                    }
                }

                int availableAfterWarmup = warmupContext.Pool.GetAvailableCount(prefab);
                int allowedCount = math.min(directDemandCount, availableAfterWarmup);
                if (allowedCount > 0)
                    warmupContext.PrefabCreateAllowances[pair.Key] = allowedCount;
            }
        }

        private bool TryReserveScatterCreate(
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool initialWarmupPass)
        {
            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            if (prefab == null)
                return true;

            if (!Application.isPlaying)
                return true;

            if (!TryGetPrefabRegistry(out PrefabRegistry prefabRegistry))
                return false;

            int prefabId = prefabRegistry.GetOrRegisterPrefab(prefab);
            if (_memory == null)
                return false;

            Dictionary<int, int> prefabCreateAllowances = _memory.PrefabCreateAllowances;
            if (!prefabCreateAllowances.TryGetValue(prefabId, out int remainingCount))
                return false;

            if (remainingCount <= 0)
                return false;

            if (remainingCount == 1)
                prefabCreateAllowances.Remove(prefabId);
            else
                prefabCreateAllowances[prefabId] = remainingCount - 1;

            return true;
        }

        private void RegisterWarmupPrefab(GameObject prefab, int familyHash, int requiredCount)
        {
            if (prefab == null || _memory == null)
                return;

            if (!TryGetPrefabRegistry(out PrefabRegistry prefabRegistry))
                return;

            int prefabId = prefabRegistry.GetOrRegisterPrefab(prefab);
            Dictionary<int, int> prefabWarmupCounts = _memory.PrefabWarmupCounts;
            Dictionary<int, GameObject> prefabWarmupPrefabs = _memory.PrefabWarmupPrefabs;
            Dictionary<int, int> prefabWarmupFamilyHashes = _memory.PrefabWarmupFamilyHashes;
            if (prefabWarmupCounts.TryGetValue(prefabId, out int count))
                prefabWarmupCounts[prefabId] = count + math.max(0, requiredCount);
            else
                prefabWarmupCounts.Add(prefabId, math.max(0, requiredCount));

            prefabWarmupPrefabs[prefabId] = prefab;
            prefabWarmupFamilyHashes[prefabId] = familyHash;
        }

        private bool TryGetPrefabRegistry(out PrefabRegistry prefabRegistry)
        {
            prefabRegistry = null;
            if (PrefabRegistry.TryResolveActiveRuntime(ref prefabRegistry))
                return true;

            if (!_loggedMissingPrefabRegistry)
            {
                _loggedMissingPrefabRegistry = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    "[WorldScatter] PrefabRegistry not initialized. Scatter create/warmup path aborted.",
                    this);
#endif
            }

            return false;
        }

        private int GetStartupWarmupReserve(int familyHash)
        {
            int configuredReserve = math.max(0, startupVariantWarmupReserve);
            if (configuredReserve <= 0)
                return 0;

            return math.min(configuredReserve, GetHotspotWarmupReserve(familyHash));
        }

        private int ResolveWarmupReserveCount(int familyHash, int directDemandCount, bool initialWarmupPass)
        {
            if (directDemandCount <= 0)
                return 0;

            if (!initialWarmupPass)
                return 0;

            int configuredReserve = GetStartupWarmupReserve(familyHash);
            if (configuredReserve <= 0)
                return 0;

            return math.max(configuredReserve, directDemandCount);
        }

        private ScatterPlacementReconcilePlan ResolveReconcilePlan(
            ScatterPlacement placement,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (placement == null)
            {
                return new ScatterPlacementReconcilePlan(
                    null,
                    null,
                    null,
                    false,
                    false,
                    false,
                    0,
                    false);
            }

            if (placement.TryGetCachedReconcilePlan(
                    _reconcileRuntimeState.PlanVersion,
                    out WorldProceduralProxyInstance cachedInstance,
                    out WorldPrefabFamilyProfile.VariantEntry cachedRuntimeVariant,
                    out bool cachedFinalVariantActive,
                    out bool cachedRequiresSpawn,
                    out bool cachedShouldApplyGeneratedGeology,
                    out int cachedSyncSignature,
                    out bool cachedAllowInitialWarmupCreate))
            {
                return new ScatterPlacementReconcilePlan(
                    placement,
                    cachedInstance,
                    cachedRuntimeVariant,
                    cachedFinalVariantActive,
                    cachedRequiresSpawn,
                    cachedShouldApplyGeneratedGeology,
                    cachedSyncSignature,
                    cachedAllowInitialWarmupCreate);
            }

            WorldPrefabFamilyProfile.VariantEntry runtimeVariant = GetResolvedPlacementVariant(
                placement,
                observerPosition,
                hasObserverPosition,
                out bool finalVariantActive);

            WorldProceduralProxyInstance instance;
            bool requiresSpawn;
            if (_activeInstances.TryGetValue(placement.Key, out instance) && instance != null)
                requiresSpawn = RequiresInstanceRebuild(instance, placement, runtimeVariant, finalVariantActive);
            else
                requiresSpawn = true;

            bool shouldApplyGeneratedGeology = ShouldApplyGeneratedGeology(placement, finalVariantActive, observerPosition, hasObserverPosition);
            int syncSignature = ComputePlacementSyncSignature(placement, runtimeVariant, finalVariantActive);
            bool allowInitialWarmupCreate = ShouldCreateDuringInitialWarmup(placement, observerPosition, hasObserverPosition);

            placement.CacheReconcilePlan(
                _reconcileRuntimeState.PlanVersion,
                instance,
                runtimeVariant,
                finalVariantActive,
                requiresSpawn,
                shouldApplyGeneratedGeology,
                syncSignature,
                allowInitialWarmupCreate);

            return new ScatterPlacementReconcilePlan(
                placement,
                instance,
                runtimeVariant,
                finalVariantActive,
                requiresSpawn,
                shouldApplyGeneratedGeology,
                syncSignature,
                allowInitialWarmupCreate);
        }

        private bool ShouldCreateDuringInitialWarmup(ScatterPlacement placement)
        {
            bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
            return ShouldCreateDuringInitialWarmup(placement, observerPosition, hasObserverPosition);
        }

        private bool ShouldCreateDuringInitialWarmup(
            ScatterPlacement placement,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (!hasObserverPosition)
                return true;

            ResolveLayerRadii(placement.StreamingLayer, out float nearRadius, out float midRadius, out _);
            float allowedRadius = math.max(nearRadius, midRadius);
            return GetHorizontalDistanceSqr(placement.Position, observerPosition) <= allowedRadius * allowedRadius;
        }

        private bool TryRegisterDesiredPlacement(
            ScatterPlacement placement,
            in ScatterPlacementRegistrationContext registrationContext)
        {
            if (placement == null || placement.Key == 0L)
                return false;

            if (placement.IsPooled)
                return false;

            if (proceduralStateRegistry != null && proceduralStateRegistry.IsPlacementSuppressed(placement.Key))
                return false;

            EnsurePlacementRuntimeStateResolved(placement);

            Dictionary<long, ScatterPlacement> desiredPlacements = registrationContext.DesiredPlacements;
            Dictionary<long, ScatterPlacement> retainedPlacements = registrationContext.RetainedPlacements;
            Dictionary<long, float> placementLastSeenTimes = registrationContext.PlacementLastSeenTimes;
            bool alreadyRegistered = desiredPlacements.TryGetValue(placement.Key, out ScatterPlacement existingDesired);
            if (!alreadyRegistered && !HasFloraStreamCellBiomeQuota(placement))
                return false;

            if (alreadyRegistered)
            {
                if (!ReferenceEquals(existingDesired, placement))
                {
                    ReleasePlacement(existingDesired);
                    RetainPlacement(placement);
                    desiredPlacements[placement.Key] = placement;
                    _faunaSnapshotDirty = true;
                }
            }
            else
            {
                RetainPlacement(placement);
                desiredPlacements[placement.Key] = placement;
                _faunaSnapshotDirty = true;
            }

            if (retainedPlacements.TryGetValue(placement.Key, out ScatterPlacement existingRetained))
            {
                if (!ReferenceEquals(existingRetained, placement))
                {
                    ReleasePlacement(existingRetained);
                    RetainPlacement(placement);
                    retainedPlacements[placement.Key] = placement;
                }
            }
            else
            {
                RetainPlacement(placement);
                retainedPlacements[placement.Key] = placement;
            }

            placementLastSeenTimes[placement.Key] = registrationContext.Now;
            if (!alreadyRegistered)
            {
                RegisterPlacementInGrid(placement);
                RegisterFloraStreamCellBiomeQuota(placement);
            }
            return true;
        }

        private bool TryRegisterDesiredPlacement(ScatterPlacement placement, float now)
        {
            ScatterPlacementRegistrationContext registrationContext = new ScatterPlacementRegistrationContext(
                _desiredPlacements,
                _retainedPlacements,
                _placementLastSeenTimes,
                now);
            return TryRegisterDesiredPlacement(placement, in registrationContext);
        }

        private void EnsurePlacementRuntimeStateResolved(ScatterPlacement placement)
        {
            if (placement == null || placement.HasRuntimeStateResolved)
                return;

            WorldPrefabFamilyProfile.VariantEntry variant = ResolveRuntimeVariant(
                placement.Family,
                placement.StableHash,
                preferFinalVariant: false);
            Vector3 resolvedPosition = placement.Position;
            bool floraFamily = ScatterMath.ResolveFloraBudgetClassId(placement.Family) != 0;
            float scale = ResolveScaleMultiplier(variant, placement.StableHash, resolvedPosition, floraFamily);
            float yawDegrees = floraFamily
                ? ScatterMath.ResolveDeterministicFloraYawDegrees(
                    placement.StableHash,
                    new Unity.Mathematics.float3(resolvedPosition.x, resolvedPosition.y, resolvedPosition.z))
                : math.abs(placement.StableHash % 360);
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            bool snappedToTerrain = TrySnapFloraPlacementToMapMagicTerrain(
                placement,
                floraFamily,
                yawDegrees,
                ref resolvedPosition,
                ref rotation);
            if (!snappedToTerrain)
            {
                snappedToTerrain = TrySnapRiftSideDebrisPlacementToMapMagicTerrain(
                    placement,
                    yawDegrees,
                    ref resolvedPosition,
                    ref rotation);
            }

            if (!snappedToTerrain &&
                placement.Rule != null &&
                EnsureEnvironmentalVegetationBridgeResolved() &&
                environmentalVegetationBridge.TrySnapScatterPlacement(
                    placement.ReadRuntimePosition(),
                    surfaceYOffset,
                    floraFamily ? math.min(placement.Rule.maxTiltAngleDegrees, FloraScatterMaxTiltAngleDegrees) : placement.Rule.maxTiltAngleDegrees,
                    yawDegrees,
                    out Vector3 snappedRuntimePosition,
                    out Quaternion snappedRotation))
            {
                resolvedPosition = ToAbsoluteScatterPosition(snappedRuntimePosition);
                rotation = snappedRotation;
            }

            WorldChunkCoordinate chunkCoord = WorldChunkCoordinate.FromWorldPosition(resolvedPosition, _runtimeStreamingState.ChunkSize);
            WorldMacroZoneCoordinate macroZoneCoord = placement.HasMacroZone
                ? WorldMacroZoneCoordinate.FromWorldPosition(resolvedPosition, _runtimeStreamingState.MacroZoneSize)
                : default;
            placement.ResolveDeferredRuntimeState(variant, resolvedPosition, rotation, scale, chunkCoord, macroZoneCoord);
        }

        private bool TrySnapFloraPlacementToMapMagicTerrain(
            ScatterPlacement placement,
            bool floraFamily,
            float yawDegrees,
            ref Vector3 resolvedPosition,
            ref Quaternion rotation)
        {
            if (!floraFamily ||
                placement == null ||
                placement.FieldSource != WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight)
            {
                return false;
            }

            Vector3 runtimePosition = ToRuntimeScatterPosition(resolvedPosition);
            if (enableAbyssalSiltFalseCeiling &&
                math.abs(runtimePosition.y - abyssalSiltFalseCeilingY) <= 0.5f)
            {
                return false;
            }

            if (mapMagicBridge == null &&
                !WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge))
            {
                return false;
            }

            if (mapMagicBridge == null ||
                !mapMagicBridge.TryGetHeightAUP(resolvedPosition, out float terrainHeight) ||
                !mapMagicBridge.TryGetNormalAUP(resolvedPosition, math.max(1f, cellSize * 0.25f), out Vector3 terrainNormal) ||
                !IsScatterSurfaceNormalSpawnable(terrainNormal))
            {
                return false;
            }

            float maxTiltAngleDegrees = placement.Rule != null
                ? math.min(placement.Rule.maxTiltAngleDegrees, FloraScatterMaxTiltAngleDegrees)
                : FloraScatterMaxTiltAngleDegrees;
            Vector3 clampedUp = ClampScatterUpVector(terrainNormal, maxTiltAngleDegrees);
            Quaternion alignRotation = Quaternion.FromToRotation(Vector3.up, clampedUp);
            Quaternion yawRotation = ApproximateAngleAxisDegreesNoTrig(RepeatDegrees360(yawDegrees), clampedUp);
            runtimePosition.y = terrainHeight + surfaceYOffset;
            resolvedPosition = ToAbsoluteScatterPosition(runtimePosition);
            rotation = yawRotation * alignRotation;
            return true;
        }

        private static float RepeatDegrees360(float degrees)
        {
            return degrees - math.floor(degrees * (1f / 360f)) * 360f;
        }

        private static Quaternion ApproximateAngleAxisDegreesNoTrig(float angleDegrees, Vector3 axis)
        {
            float axisLengthSq = axis.sqrMagnitude;
            if (axisLengthSq <= 0.000001f || !float.IsFinite(axisLengthSq))
                return Quaternion.identity;

            Vector3 safeAxis = axis * math.rsqrt(axisLengthSq);
            MathLodApproximation.ApproxSinCosBhaskara(angleDegrees * Mathf.Deg2Rad * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                safeAxis.x * sinHalf,
                safeAxis.y * sinHalf,
                safeAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternion(rotation);
        }

        private static Quaternion NormalizeQuaternion(Quaternion value)
        {
            float lengthSq =
                (value.x * value.x) +
                (value.y * value.y) +
                (value.z * value.z) +
                (value.w * value.w);
            if (lengthSq <= 0.000001f || !float.IsFinite(lengthSq))
                return Quaternion.identity;

            float invLength = math.rsqrt(lengthSq);
            return new Quaternion(value.x * invLength, value.y * invLength, value.z * invLength, value.w * invLength);
        }

        private bool TrySnapRiftSideDebrisPlacementToMapMagicTerrain(
            ScatterPlacement placement,
            float yawDegrees,
            ref Vector3 resolvedPosition,
            ref Quaternion rotation)
        {
            if (placement == null ||
                placement.FieldSource != WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight ||
                !AllowsTectonicSpineRockBoulderOverride(placement.Family, placement.SlopeDegrees, placement.IsTectonicSpineBiome))
            {
                return false;
            }

            if (mapMagicBridge == null &&
                !WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge))
            {
                return false;
            }

            if (mapMagicBridge == null ||
                !mapMagicBridge.TryGetHeightAUP(resolvedPosition, out float terrainHeight))
            {
                return false;
            }

            Vector3 softenedSlopeUp = ResolveRiftSideDebrisFakeUpVector(
                placement.SlopeDegrees,
                placement.StableHash,
                yawDegrees);
            float maxTiltAngleDegrees = placement.Rule != null
                ? math.min(62f, placement.Rule.maxTiltAngleDegrees)
                : 62f;
            Vector3 clampedUp = ClampScatterUpVector(softenedSlopeUp, maxTiltAngleDegrees);
            float wrappedYawDegrees = yawDegrees - math.floor(yawDegrees / 360f) * 360f;
            Quaternion alignRotation = Quaternion.FromToRotation(Vector3.up, clampedUp);
            Quaternion yawRotation = ApproximateAngleAxisDegreesNoTrig(wrappedYawDegrees, clampedUp);
            Vector3 runtimePosition = ToRuntimeScatterPosition(resolvedPosition);
            runtimePosition.y = terrainHeight + surfaceYOffset;
            resolvedPosition = ToAbsoluteScatterPosition(runtimePosition);
            rotation = yawRotation * alignRotation;
            return true;
        }

        private static Vector3 ResolveRiftSideDebrisFakeUpVector(
            float slopeDegrees,
            int stableHash,
            float yawDegrees)
        {
            float fakeAzimuthDegrees = yawDegrees + (stableHash & 31) * 11.25f;
            float fakeAzimuthRadians = math.radians(fakeAzimuthDegrees);
            MathLodApproximation.ApproxSinCosBhaskara(fakeAzimuthRadians, out float azimuthSin, out float azimuthCos);
            float slopeRadians = math.radians(math.clamp(slopeDegrees, 45f, 62f));
            MathLodApproximation.ApproxSinCosBhaskara(slopeRadians, out float slopeSin, out float slopeCos);
            float scaledUpX = azimuthCos * slopeSin * 0.5f;
            float scaledUpY = math.lerp(1f, slopeCos, 0.5f);
            float scaledUpZ = azimuthSin * slopeSin * 0.5f;
            float invMagnitude = math.rsqrt(math.max(0.0001f, scaledUpX * scaledUpX + scaledUpY * scaledUpY + scaledUpZ * scaledUpZ));
            return new Vector3(
                scaledUpX * invMagnitude,
                scaledUpY * invMagnitude,
                scaledUpZ * invMagnitude);
        }

        private static Vector3 ClampScatterUpVector(Vector3 normal, float maxTiltAngleDegrees)
        {
            float normalMagnitudeSqr = normal.sqrMagnitude;
            Vector3 safeNormal = Vector3.up;
            if (normalMagnitudeSqr > 0.0001f)
            {
                float normalInvMagnitude = math.rsqrt(normalMagnitudeSqr);
                safeNormal = new Vector3(
                    normal.x * normalInvMagnitude,
                    normal.y * normalInvMagnitude,
                    normal.z * normalInvMagnitude);
            }

            float safeMaxTilt = math.clamp(maxTiltAngleDegrees, 0f, 89.5f);
            float safeMaxTiltRadians = math.radians(safeMaxTilt);
            float minUpDot = MathLodApproximation.ApproxCosBhaskara(safeMaxTiltRadians);
            if (safeNormal.y >= minUpDot)
                return safeNormal;

            float horizontalMagnitudeSq = safeNormal.x * safeNormal.x + safeNormal.z * safeNormal.z;
            if (horizontalMagnitudeSq <= 0.000001f)
                return Vector3.up;

            float horizontalInvMagnitude = math.rsqrt(horizontalMagnitudeSq);
            float horizontalScale = MathLodApproximation.ApproxSinBhaskara(safeMaxTiltRadians);
            return new Vector3(
                safeNormal.x * horizontalInvMagnitude * horizontalScale,
                minUpDot,
                safeNormal.z * horizontalInvMagnitude * horizontalScale);
        }

        private static bool IsScatterSurfaceNormalSpawnable(Vector3 normal)
        {
            float normalMagnitudeSqr = normal.sqrMagnitude;
            if (normalMagnitudeSqr <= 0.0001f || normal.y <= 0f)
                return false;

            float minimumUpDot = ScatterMinimumSurfaceNormalUpDot;
            return normal.y * normal.y >= minimumUpDot * minimumUpDot * normalMagnitudeSqr;
        }

        private bool TryRegisterDesiredPlacement(ScatterPlacement placement)
        {
            float now = RuntimeNowSeconds();
            return TryRegisterDesiredPlacement(placement, now);
        }

        private void InvalidateResolvedPlacementVariantCache()
        {
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                placement?.InvalidateResolvedVariantState();
            }
        }

        private void EvictStaleRetainedPlacements(in ScatterRetentionEvictionContext evictionContext)
        {
            List<long> removalBuffer = evictionContext.RemovalBuffer;
            Dictionary<long, float> placementLastSeenTimes = evictionContext.PlacementLastSeenTimes;
            Dictionary<long, ScatterPlacement> retainedPlacements = evictionContext.RetainedPlacements;
            removalBuffer.Clear();

            Dictionary<long, float>.Enumerator enumerator = placementLastSeenTimes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, float> pair = enumerator.Current;
                if (evictionContext.Now - pair.Value > evictionContext.RemovalThresholdSeconds)
                    removalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < removalBuffer.Count; i++)
            {
                long key = removalBuffer[i];
                if (retainedPlacements.TryGetValue(key, out ScatterPlacement placement))
                    ReleasePlacement(placement);
                retainedPlacements.Remove(key);
                placementLastSeenTimes.Remove(key);
            }

            removalBuffer.Clear();
        }

        private void EvictStaleRetainedPlacements(float now)
        {
            ScatterRetentionEvictionContext evictionContext = new ScatterRetentionEvictionContext(
                _retainedPlacements,
                _placementLastSeenTimes,
                _removalBuffer,
                now,
                math.max(0.25f, missingPlacementGraceSeconds) * 1.5f);
            EvictStaleRetainedPlacements(in evictionContext);
        }

        private void ResetPlacementGrid()
        {
            int bucketCount = math.min(_gridPlacementBucketCount, _gridPlacementBuckets.Count);
            for (int i = 0; i < bucketCount; i++)
                _gridPlacementBuckets[i].Clear();

            _gridPlacementBucketCount = 0;
            _maxRegisteredPlacementSpacingMeters = 0f;
            _gridPlacements.Clear();
            _memory.ResetGridPlacementSpatialCache();
        }

        private void RegisterPlacementInGrid(ScatterPlacement placement)
        {
            long cellKey = ComposeScatterGridKey(placement.CellX, placement.CellZ);
            if (!_gridPlacements.TryGetValue(cellKey, out List<ScatterPlacement> bucket))
            {
                if (_gridPlacementBucketCount < _gridPlacementBuckets.Count)
                {
                    bucket = _gridPlacementBuckets[_gridPlacementBucketCount];
                }
                else
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        // COLD ALLOC: editor-only scene preview can exceed runtime bucket sizing without affecting player streaming GC.
                        bucket = new List<ScatterPlacement>(8);
                        _gridPlacementBuckets.Add(bucket);
                    }
                    else
#endif
                    {
                        _memory.GridPlacementNativeOverflowed = true;
                        _memory.TryRegisterGridPlacement(placement);
                        float overflowSpacing = placement.EffectiveSpacing;
                        if (overflowSpacing > _maxRegisteredPlacementSpacingMeters)
                            _maxRegisteredPlacementSpacingMeters = overflowSpacing;
                        return;
                    }
                }

                _gridPlacementBucketCount++;
                _gridPlacements[cellKey] = bucket;
            }

            bucket.Add(placement);
            _memory.TryRegisterGridPlacement(placement);
            float spacing = placement.EffectiveSpacing;
            if (spacing > _maxRegisteredPlacementSpacingMeters)
                _maxRegisteredPlacementSpacingMeters = spacing;
        }

        private void RestoreRecentDesiredPlacements(in ScatterRetentionRestoreContext restoreContext)
        {
            if (_activeInstances.Count == 0)
                return;

            Dictionary<long, ScatterPlacement> desiredPlacements = restoreContext.DesiredPlacements;
            Dictionary<long, ScatterPlacement> retainedPlacements = restoreContext.RetainedPlacements;
            Dictionary<long, float> placementLastSeenTimes = restoreContext.PlacementLastSeenTimes;
            Dictionary<long, WorldProceduralProxyInstance>.Enumerator enumerator = _activeInstances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, WorldProceduralProxyInstance> pair = enumerator.Current;
                long runtimeKey = pair.Key;
                if (desiredPlacements.ContainsKey(runtimeKey))
                    continue;

                if (!placementLastSeenTimes.TryGetValue(runtimeKey, out float lastSeenTime) ||
                    restoreContext.Now - lastSeenTime > restoreContext.GraceSeconds)
                {
                    continue;
                }

                if (!retainedPlacements.TryGetValue(runtimeKey, out ScatterPlacement placement))
                    continue;

                if (!IsPlacementWithinResidency(placement, restoreContext.ObserverPosition))
                    continue;

                EnsurePlacementRuntimeStateResolved(placement);
                RetainPlacement(placement);
                desiredPlacements[runtimeKey] = placement;
                _faunaSnapshotDirty = true;
            }
        }

        private void RestoreRecentDesiredPlacements(Vector3 observerPosition, float now)
        {
            ScatterRetentionRestoreContext restoreContext = new ScatterRetentionRestoreContext(
                _desiredPlacements,
                _retainedPlacements,
                _placementLastSeenTimes,
                observerPosition,
                now,
                math.max(0.25f, missingPlacementGraceSeconds));
            RestoreRecentDesiredPlacements(in restoreContext);
        }

        private bool IsPlacementWithinResidency(ScatterPlacement placement, Vector3 observerPosition)
        {
            ResolveLayerRadii(placement.StreamingLayer, out _, out _, out float farRadius);
            if (farRadius <= 0f)
                return false;

            return GetHorizontalDistanceSqr(placement.Position, observerPosition) <= farRadius * farRadius;
        }

        private bool IsPlacementWithinResidency(Vector3 position, WorldStreamingLayer streamingLayer, Vector3 observerPosition)
        {
            ResolveLayerRadii(streamingLayer, out _, out _, out float farRadius);
            if (farRadius <= 0f)
                return false;

            return GetHorizontalDistanceSqr(position, observerPosition) <= farRadius * farRadius;
        }

        private void ResolveLayerRadii(
            WorldStreamingLayer layer,
            out float nearRadius,
            out float midRadius,
            out float farRadius)
        {
            float runtimeCellSize = math.max(6f, _runtimeStreamingState.CellSize > 0f ? _runtimeStreamingState.CellSize : cellSize);
            int index = (int)layer;
            if (index < 0 || index >= 8)
            {
                nearRadius = math.max(24f, runtimeCellSize * 2f);
                midRadius = math.max(nearRadius + runtimeCellSize, nearRadius * 1.8f);
                farRadius = math.max(midRadius + runtimeCellSize, midRadius * 1.5f);
                return;
            }

            bool radiiCacheMissing = _layerNearRadii[index] <= 0f || _layerMidRadii[index] <= 0f || _layerFarRadii[index] <= 0f;
            if (_cachedLayerRadiiCellSize != runtimeCellSize || !ReferenceEquals(_cachedLayerRadiiProfile, chunkStreamingProfile) || radiiCacheMissing)
            {
                _cachedLayerRadiiCellSize = runtimeCellSize;
                _cachedLayerRadiiProfile = chunkStreamingProfile;
                for (int i = 0; i < 8; i++)
                {
                    WorldStreamingLayer l = (WorldStreamingLayer)i;
                    float n = math.max(24f, runtimeCellSize * 2f);
                    float m = math.max(n + runtimeCellSize, n * 1.8f);
                    float f = math.max(m + runtimeCellSize, m * 1.5f);

                    if (chunkStreamingProfile != null)
                    {
                        WorldChunkStreamingProfile.LayerProfile layerProfile = chunkStreamingProfile.GetLayerProfileOrDefault(l);
                        n = math.max(24f, chunkStreamingProfile.fullSimulationRadius * math.max(0.35f, layerProfile.nearRadiusScale));
                        m = math.max(n + runtimeCellSize, chunkStreamingProfile.midSimulationRadius * math.max(0.35f, layerProfile.midRadiusScale));
                        f = math.max(m + runtimeCellSize, chunkStreamingProfile.visualResidencyRadius * math.max(0.35f, layerProfile.farRadiusScale));
                    }
                    _layerNearRadii[i] = n;
                    _layerMidRadii[i] = m;
                    _layerFarRadii[i] = f;
                }
            }

            nearRadius = _layerNearRadii[index];
            midRadius = _layerMidRadii[index];
            farRadius = _layerFarRadii[index];
        }

        private void InvalidateLayerRadiiCache()
        {
            if (_memory == null)
                return;

            _cachedLayerRadiiCellSize = -1f;
            _cachedLayerRadiiProfile = null;
            for (int i = 0; i < 8; i++)
            {
                _layerNearRadii[i] = 0f;
                _layerMidRadii[i] = 0f;
                _layerFarRadii[i] = 0f;
            }
        }

        private int ResolveRuntimeBudget(int authoredBudget, WorldStreamingLayer layer, int minValue, int maxValue)
        {
            int clampedBudget = math.clamp(authoredBudget, minValue, maxValue);
            if (chunkStreamingProfile == null)
                return clampedBudget;

            WorldChunkStreamingProfile.LayerProfile layerProfile = chunkStreamingProfile.GetLayerProfileOrDefault(layer);
            float densityScale = math.lerp(0.7f, 1.45f, math.saturate(layerProfile.maxActivationsPerTick / 24f));
            int scaledBudget = (int)math.round(clampedBudget * densityScale);
            return math.clamp(scaledBudget, minValue, maxValue);
        }

        private void RefreshRuntimeStreamingSettings()
        {
            _runtimeStreamingState.CellSize = math.max(6f, cellSize);
            _runtimeStreamingState.RadiusCells = math.max(2, radiusCells);
            _runtimeStreamingState.ChunkSize = 192f;
            _runtimeStreamingState.MacroZoneSize = 768f;

            if (chunkStreamingProfile != null)
            {
                _runtimeStreamingState.ChunkSize = math.max(_runtimeStreamingState.CellSize, chunkStreamingProfile.chunkSizeMeters);
                _runtimeStreamingState.MacroZoneSize = math.max(_runtimeStreamingState.ChunkSize, chunkStreamingProfile.macroZoneSizeMeters);
            }

            _debugRuntimeCellSize = _runtimeStreamingState.CellSize;
            _debugRuntimeRadiusCells = _runtimeStreamingState.RadiusCells;
            _debugRuntimeChunkSize = _runtimeStreamingState.ChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeStreamingState.MacroZoneSize;
        }

        private WorldPrefabFamilyProfile.VariantEntry ResolvePlacementVariant(ScatterPlacement placement)
        {
            return ResolvePlacementVariant(placement, ShouldUseFinalVariant(placement));
        }

        private WorldPrefabFamilyProfile.VariantEntry GetResolvedPlacementVariant(
            ScatterPlacement placement,
            out bool finalVariantActive)
        {
            bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
            return GetResolvedPlacementVariant(placement, observerPosition, hasObserverPosition, out finalVariantActive);
        }

        private WorldPrefabFamilyProfile.VariantEntry GetResolvedPlacementVariant(
            ScatterPlacement placement,
            Vector3 observerPosition,
            bool hasObserverPosition,
            out bool finalVariantActive)
        {
            if (placement == null)
            {
                finalVariantActive = false;
                return null;
            }

            bool supportsFinalVariant = ResolvePlacementSupportsFinalVariant(placement);
            if (placement.HasResolvedVariantState
                && placement.CachedSupportsFinalVariant == supportsFinalVariant)
            {
                finalVariantActive = placement.CachedFinalVariantActive;
                return placement.CachedResolvedVariant;
            }

            finalVariantActive = ShouldUseFinalVariant(placement, observerPosition, hasObserverPosition);
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant = ResolvePlacementVariant(placement, finalVariantActive);
            placement.CacheResolvedVariantState(runtimeVariant, finalVariantActive, supportsFinalVariant);
            return runtimeVariant;
        }

        private WorldPrefabFamilyProfile.VariantEntry ResolvePlacementVariant(
            ScatterPlacement placement,
            bool finalVariantActive)
        {
            if (!finalVariantActive)
            {
                WorldPrefabFamilyProfile.VariantEntry preferredProxyVariant =
                    ResolvePreferredCheapProxyVariant(placement.Family, placement.StableHash);
                if (preferredProxyVariant != null)
                    return preferredProxyVariant;

                if (placement.Variant != null)
                    return placement.Variant;
            }

            return ResolveRuntimeVariant(placement.Family, placement.StableHash, finalVariantActive);
        }

        private enum VariantFilterMode
        {
            Any,
            FinalReady,
            ProxyOnly,
            CheapProxy
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolvePreferredCheapProxyVariant(
            WorldPrefabFamilyProfile family,
            int stableHash)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return null;

            if (!family.IsCheapProxyFamily)
                return null;

            return ResolveVariantFiltered(family, stableHash, VariantFilterMode.CheapProxy);
        }

        private float GetGenerativeGeologyContextBonus(
            in WorldProceduralFieldSampler.FieldSample sample,
            WorldPrefabFamilyProfile family)
        {
            WorldGenerativeGeologyProfile geologyProfile = ResolveEffectiveGenerativeGeologyProfile(family);
            if (family == null || geologyProfile == null || !family.UsesGenerativeGeology())
                return 0f;

            float fit = geologyProfile.EvaluatePlacementFitness(
                sample.slopeDegrees,
                sample.curvature,
                sample.caveProximity,
                sample.ridgeSignal,
                sample.canyonSignal,
                sample.compositionPotential);

            return fit * math.max(0.15f, geologyProfile.compositionWeight);
        }

        private static float GetGenerativeGeologyContextBonus(
            in WorldProceduralFieldSampler.FieldSample sample,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            WorldGenerativeGeologyProfile geologyProfile = runtimeRule.GeologyProfile;
            if (geologyProfile == null || runtimeRule.GeologyScoreScale <= 0f)
                return 0f;

            float fit = geologyProfile.EvaluatePlacementFitness(
                sample.slopeDegrees,
                sample.curvature,
                sample.caveProximity,
                sample.ridgeSignal,
                sample.canyonSignal,
                sample.compositionPotential);

            return fit * runtimeRule.GeologyScoreScale;
        }

        private static float GetCachedGenerativeGeologyContextBonus(
            in WorldProceduralFieldSampler.FieldSample sample,
            in ScatterRuntimeRuleEntry runtimeRule,
            ref GeologyBonusCache cache)
        {
            WorldGenerativeGeologyProfile geologyProfile = runtimeRule.GeologyProfile;
            if (geologyProfile == null)
                return 0f;

            if (cache.TryGet(geologyProfile, out float cachedBonus))
                return cachedBonus;

            float bonus = GetGenerativeGeologyContextBonus(sample, runtimeRule);
            cache.Store(geologyProfile, bonus);
            return bonus;
        }

        private void ApplyGeneratedGeology(
            WorldProceduralProxyInstance metadata,
            ScatterPlacement placement,
            bool finalVariantActive)
        {
            bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
            WorldGenerativeGeologyService cachedService = generativeGeologyService;
            bool shouldApplyGeneratedGeology = ShouldApplyGeneratedGeology(placement, finalVariantActive, observerPosition, hasObserverPosition);
            ApplyGeneratedGeology(metadata, placement, finalVariantActive, shouldApplyGeneratedGeology, cachedService, observerPosition, hasObserverPosition);
        }

        private void ApplyGeneratedGeology(
            WorldProceduralProxyInstance metadata,
            ScatterPlacement placement,
            bool finalVariantActive,
            bool shouldApplyGeneratedGeology,
            WorldGenerativeGeologyService geologyService,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (metadata == null)
                return;

            if (!shouldApplyGeneratedGeology)
            {
                ClearGeneratedGeologyInstance(metadata);
                return;
            }

            WorldGenerativeGeologyProfile geologyProfile = placement.GeologyProfile ?? ResolveEffectiveGenerativeGeologyProfile(placement.Family);
            if (placement.Family == null || !placement.Family.UsesGenerativeGeology() || geologyProfile == null)
            {
                ClearGeneratedGeologyInstance(metadata);
                return;
            }

            if (geologyService == null)
                return;

            WorldGenerativeGeologyRequest request = new WorldGenerativeGeologyRequest(
                placement.Key,
                placement.StableHash,
                placement.Family,
                geologyProfile,
                finalVariantActive,
                placement.SlopeDegrees,
                placement.Curvature,
                placement.CaveProximity,
                placement.RidgeSignal,
                placement.CanyonSignal,
                placement.CompositionPotential,
                placement.Position,
                placement.Rotation,
                placement.Scale);

            if (geologyService.TryApplyPreparedGeneratedGeologyHot(metadata, request))
            {
                metadata.ResolveGeneratedGeologyRoot(GeneratedGeologyRootName);
                _debugGeneratedGeologyCount++;
            }
        }

        private bool ShouldApplyGeneratedGeology(ScatterPlacement placement, bool finalVariantActive)
        {
            bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
            return ShouldApplyGeneratedGeology(placement, finalVariantActive, observerPosition, hasObserverPosition);
        }

        private bool ShouldApplyGeneratedGeology(
            ScatterPlacement placement,
            bool finalVariantActive,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (placement.Family == null || !placement.Family.UsesGenerativeGeology())
                return false;

            if (finalVariantActive || !hasObserverPosition)
                return true;

            ResolveLayerRadii(placement.StreamingLayer, out float nearRadius, out _, out _);
            float allowedRadius = nearRadius * math.saturate(proxyGeneratedGeologyNearRadiusScale);
            if (allowedRadius <= 0.01f)
                return false;

            return (placement.Position - observerPosition).sqrMagnitude <= allowedRadius * allowedRadius;
        }

        private static void ClearGeneratedGeologyInstance(WorldProceduralProxyInstance metadata)
        {
            if (metadata == null)
                return;

            Transform generatedRoot = metadata.ResolveGeneratedGeologyRoot(GeneratedGeologyRootName);
            if (generatedRoot == null)
                return;

            if (generatedRoot.gameObject.activeSelf)
                generatedRoot.gameObject.SetActive(false);
        }

        private static int ComputePlacementSyncSignature(
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            unchecked
            {
                bool supportsFinalVariant = ResolvePlacementSupportsFinalVariant(placement);
                int signature = ScatterPlacementSyncSignatureVersion;
                signature = signature * 31 + FoldLongHash(placement.Key);
                signature = signature * 31 + (int)placement.StreamingLayer;
                signature = signature * 31 + (supportsFinalVariant ? 1 : 0);
                signature = signature * 31 + (finalVariantActive ? 1 : 0);
                signature = signature * 31 + placement.CellX;
                signature = signature * 31 + placement.CellZ;
                signature = signature * 31 + placement.HeightLayerIndex;
                signature = signature * 31 + placement.ChunkCoord.x;
                signature = signature * 31 + placement.ChunkCoord.z;
                signature = signature * 31 + (placement.HasMacroZone ? 1 : 0);
                signature = signature * 31 + placement.MacroZoneCoord.x;
                signature = signature * 31 + placement.MacroZoneCoord.z;
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Position.x);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Position.y);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Position.z);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Rotation.x);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Rotation.y);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Rotation.z);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Rotation.w);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Scale);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.SeafloorHeight);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.DepthMeters);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.SlopeDegrees);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Curvature);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.CaveProximity);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.RidgeSignal);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.CanyonSignal);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.CompositionPotential);
                signature = signature * 31 + BitConverter.SingleToInt32Bits(placement.Heat);
                signature = signature * 31 + (int)placement.FieldSource;
                signature = signature * 31 + placement.StableHash;
                signature = signature * 31 + GetVariantHash(runtimeVariant);
                return signature;
            }
        }

        private static int FoldLongHash(long value)
        {
            unchecked
            {
                return (int)value ^ (int)(value >> 32);
            }
        }

        private static int ComputeStableStringHash(string value)
        {
            return Hecton.Localization.LocHash.Compute(value);
        }

        private WorldGenerativeGeologyService ResolveGenerativeGeologyService(bool createIfMissing)
        {
            if (generativeGeologyService != null)
                return generativeGeologyService;

            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyService(ref generativeGeologyService);
            if (generativeGeologyService != null || !createIfMissing)
                return generativeGeologyService;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject root = GetOrCreateRoot();
            Transform serviceTransform = FindDirectChildByName(root.transform, "__GENERATIVE_GEOLOGY_SERVICE");
            if (serviceTransform == null)
            {
                serviceTransform = new GameObject("__GENERATIVE_GEOLOGY_SERVICE").transform;
                serviceTransform.SetParent(root.transform, false);
            }

            if (!serviceTransform.TryGetComponent(out generativeGeologyService))
                generativeGeologyService = serviceTransform.gameObject.AddComponent<WorldGenerativeGeologyService>();

            return generativeGeologyService;
#else
            return null;
#endif
        }

        private static WorldGenerativeGeologyProfile ResolveEffectiveGenerativeGeologyProfile(WorldPrefabFamilyProfile family)
        {
            if (family == null || !family.UsesGenerativeGeology())
                return null;

            if (family.generativeGeologyProfile != null && family.generativeGeologyProfile.IsEnabled)
                return family.generativeGeologyProfile;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return family.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => GetOrCreateEmergencyGeologyProfile(
                    ref _emergencyArchGeologyProfile,
                    "geology.emergency.arch",
                    "Emergency Arch Geology",
                    WorldGenerativeGeologyProfile.ShapeArchetype.Arch,
                    WorldGenerativeGeologyProfile.CompositionMode.ContextPack),
                WorldPrefabFamilyProfile.ProceduralDomain.RockShelf => GetOrCreateEmergencyGeologyProfile(
                    ref _emergencyCanopyGeologyProfile,
                    "geology.emergency.canopy",
                    "Emergency Canopy Geology",
                    WorldGenerativeGeologyProfile.ShapeArchetype.Canopy,
                    WorldGenerativeGeologyProfile.CompositionMode.PairedFeature),
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => GetOrCreateEmergencyGeologyProfile(
                    ref _emergencyCaveGeologyProfile,
                    "geology.emergency.cave",
                    "Emergency Cave Bridge Geology",
                    WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge,
                    WorldGenerativeGeologyProfile.CompositionMode.ContextPack),
                _ => GetOrCreateEmergencyGeologyProfile(
                    ref _emergencyLandmarkGeologyProfile,
                    "geology.emergency.landmark",
                    "Emergency Landmark Geology",
                    WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock,
                    WorldGenerativeGeologyProfile.CompositionMode.PairedFeature)
            };
#else
            return null;
#endif
        }

        private static WorldGenerativeGeologyProfile GetOrCreateEmergencyGeologyProfile(
            ref WorldGenerativeGeologyProfile cachedProfile,
            string profileId,
            string label,
            WorldGenerativeGeologyProfile.ShapeArchetype archetype,
            WorldGenerativeGeologyProfile.CompositionMode compositionMode)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return null;
#else
            if (cachedProfile != null)
                return cachedProfile;

            cachedProfile = ScriptableObject.CreateInstance<WorldGenerativeGeologyProfile>();
            cachedProfile.hideFlags = HideFlags.DontSave;
            cachedProfile.profileId = profileId;
            cachedProfile.profileLabel = label;
            cachedProfile.generatorMode = WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback;
            cachedProfile.shapeArchetype = archetype;
            cachedProfile.compositionMode = compositionMode;
            cachedProfile.terrainSeamMode = WorldGenerativeGeologyProfile.TerrainSeamMode.SdfBlend;
            cachedProfile.caveBlendMode = WorldGenerativeGeologyProfile.CaveBlendMode.SdfBlend;
            cachedProfile.idealSlopeRange = new Vector2(8f, 46f);
            cachedProfile.idealCurvatureRange = new Vector2(-0.42f, 0.42f);
            cachedProfile.idealCaveProximityRange = new Vector2(0.18f, 0.92f);
            cachedProfile.idealRidgeSignalRange = new Vector2(0.16f, 1f);
            cachedProfile.idealCanyonSignalRange = new Vector2(0.08f, 0.96f);
            cachedProfile.contextPackThreshold = 0.58f;
            cachedProfile.seamBlendRadius = 14f;
            cachedProfile.terrainRaiseMeters = 3f;
            cachedProfile.terrainCutMeters = 2.5f;
            cachedProfile.debrisCountMin = 4;
            cachedProfile.debrisCountMax = 9;
            cachedProfile.lodCount = 3;
            cachedProfile.lodScreenHeights = new Vector3(0.62f, 0.27f, 0.08f);
            return cachedProfile;
#endif
        }

        private bool ShouldUseFinalVariant(ScatterPlacement placement)
        {
            bool hasObserverPosition = TryGetObserverAbsolutePosition(out Vector3 observerPosition);
            return ShouldUseFinalVariant(placement, observerPosition, hasObserverPosition);
        }

        private bool ShouldUseFinalVariant(
            ScatterPlacement placement,
            Vector3 observerPosition,
            bool hasObserverPosition)
        {
            if (!ResolvePlacementSupportsFinalVariant(placement) || !hasObserverPosition)
                return false;

            ResolveLayerRadii(placement.StreamingLayer, out float nearRadius, out _, out _);
            nearRadius *= ResolveFinalVariantRadiusScale(placement.Family);
            return GetHorizontalDistanceSqr(placement.Position, observerPosition) <= nearRadius * nearRadius;
        }

        private static float GetHorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            return ScatterCandidateEvaluator.GetHorizontalDistanceSqr(a, b);
        }

        private float ResolveFinalVariantRadiusScale(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            int familyHash = family.FamilyHash;
            if (familyHash == _FamilyCoralLowHash)
                return math.clamp(coralLowFinalRadiusScale, 0.25f, 1f);

            if (familyHash == _FamilyRockSmallFloorHash)
                return math.clamp(rockSmallFloorFinalRadiusScale, 0.25f, 3f);

            if (familyHash == _FamilyRockClusterMediumHash)
                return math.clamp(rockClusterMediumFinalRadiusScale, 0.25f, 3f);

            if (familyHash == _FamilyRockArchLargeHash)
                return math.clamp(rockArchLargeFinalRadiusScale, 0.25f, 3f);

            return 1f;
        }

        private static int GetHotspotWarmupReserve(int familyHash)
        {
            if (familyHash == 0)
                return 0;

            if (familyHash == _FamilyKelpTallHash)
                return 24;

            if (familyHash == _FamilyKelpPatchDenseHash)
                return 18;

            if (familyHash == _FamilyKelpCanopyHash)
                return 12;

            if (familyHash == _FamilyCoralLowHash)
                return 20;

            if (familyHash == _FamilyCoralMassiveHash)
                return 12;

            if (familyHash == _FamilyRockSmallFloorHash)
                return 12;

            if (familyHash == _FamilyRockClusterMediumHash)
                return 8;

            if (familyHash == _FamilyRockArchLargeHash)
                return 4;

            if (familyHash == _FamilyCoralBranchingHash)
                return 8;

            if (familyHash == _FamilyCoralPlateHash)
                return 6;

            if (familyHash == _FamilyCreatureSpawnPassiveHash)
                return 6;

            if (familyHash == _FamilyPocketSafeHash)
                return 6;

            if (familyHash == _FamilyEggClusterHash)
                return 4;

            if (familyHash == _FamilyLandmarkSpireHash ||
                familyHash == _FamilyCaveEntranceHash)
            {
                return 4;
            }

            return 0;
        }

        private static bool RequiresInstanceRebuild(
            WorldProceduralProxyInstance instance,
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            if (instance == null)
                return true;

            bool supportsFinalVariant = ResolvePlacementSupportsFinalVariant(placement);

            if (placement.Family != null && placement.Family.UsesGenerativeGeology())
            {
                return instance.ActiveStreamingLayer != placement.StreamingLayer;
            }

            int runtimeVariantHash = runtimeVariant != null
                ? runtimeVariant.VariantHash
                : (placement.Family != null ? ComputeStableStringHash(placement.Family.GeneratedVariantId) : 0);
            return instance.ActiveVariantHash != runtimeVariantHash
                || instance.IsFinalVariantActive != finalVariantActive
                || instance.SupportsFinalVariant != supportsFinalVariant
                || instance.ActiveStreamingLayer != placement.StreamingLayer;
        }

        private static bool IsFaunaAnchorPlacement(ScatterPlacement placement)
        {
            return placement.IsFaunaAnchor;
        }

        private static bool IsLargeThreatZonePlacement(ScatterPlacement placement)
        {
            return placement.IsLargeThreatZone;
        }

        private static float ResolveFaunaAnchorRadius(ScatterPlacement placement)
        {
            return placement.FaunaAnchorRadius;
        }

        private static long ComposeScatterGridKey(int cellX, int cellZ)
        {
            return ScatterCandidateEvaluator.ComposeScatterGridKey(cellX, cellZ);
        }

        private static float ResolveRequiredDistance(ScatterPlacement candidate, ScatterPlacement existing)
        {
            return ScatterCandidateEvaluator.ResolveRequiredDistance(candidate, existing);
        }

        private static bool IsPocket(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain == WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket
                || domain == WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket
                || domain == WorldPrefabFamilyProfile.ProceduralDomain.SafePocket;
        }

        private static bool IsStructure(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer == WorldPrefabFamilyProfile.ScatterLayer.Structure;
        }

        private static float GetEffectiveSpacing(WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule)
        {
            return ScatterCandidateEvaluator.GetEffectiveSpacing(family, rule);
        }

        private bool HasLayerBudget(
            in ScatterCandidate candidate,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int structureBudget,
            int spawnStride,
            int spawnBudget,
            int groundCount,
            int clusterCount,
            int structureCount,
            int spawnCount)
        {
            WorldPrefabFamilyProfile.ScatterLayer layer = candidate.Family.scatterLayer;
            return HasLayerBudget(
                layer,
                candidate.Placement.CellX,
                candidate.Placement.CellZ,
                ResolveHeightLayerIndex(candidate.Placement),
                groundBudget,
                clusterBudget,
                structureStride,
                structureBudget,
                spawnStride,
                spawnBudget,
                groundCount,
                clusterCount,
                structureCount,
                spawnCount);
        }

        private bool HasLayerBudget(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule,
            int cellX,
            int cellZ,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int structureBudget,
            int spawnStride,
            int spawnBudget,
            int groundCount,
            int clusterCount,
            int structureCount,
            int spawnCount)
        {
            return HasLayerBudget(
                runtimeRule.ScatterLayer,
                cellX,
                cellZ,
                ResolveHeightLayerIndex(fieldSample, runtimeRule),
                groundBudget,
                clusterBudget,
                structureStride,
                structureBudget,
                spawnStride,
                spawnBudget,
                groundCount,
                clusterCount,
                structureCount,
                spawnCount);
        }

        private bool HasLayerBudget(
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int cellX,
            int cellZ,
            int heightLayerIndex,
            int groundBudget,
            int clusterBudget,
            int structureStride,
            int structureBudget,
            int spawnStride,
            int spawnBudget,
            int groundCount,
            int clusterCount,
            int structureCount,
            int spawnCount)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => groundCount < groundBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => clusterCount < clusterBudget,

                WorldPrefabFamilyProfile.ScatterLayer.Structure => structureCount < structureBudget && GetWindowPlacementCount(new ScatterWindowContext(cellX, cellZ, structureStride, heightLayerIndex), _structureWindowCounts) < structureBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => spawnCount < spawnBudget && GetWindowPlacementCount(new ScatterWindowContext(cellX, cellZ, spawnStride, heightLayerIndex), _spawnWindowCounts) < spawnBudget,

                _ => false
            };
        }

        private int InjectFilteredWindowCandidatesBatch(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            List<ScatterCandidate> ordered,
            in ScatterRescueCandidateFilter filter,
            int acceptLimit,
            int stride,
            int perWindowBudget,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ref int structureCount,
            ref int spawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (ordered == null || ordered.Count == 0 || acceptLimit <= 0)
                return 0;

            int currentLayerCount = layer == WorldPrefabFamilyProfile.ScatterLayer.Structure
                ? structureCount
                : spawnCount;
            int layerTargetMax = layer == WorldPrefabFamilyProfile.ScatterLayer.Structure
                ? ResolvePatternStructureTargetMax(pattern, biomeProfile)
                : ResolvePatternSpawnTargetMax(pattern, biomeProfile);

            if (!TryEvaluateScatterRescueCandidateAcceptanceBatch(
                    ordered,
                    filter,
                    pattern,
                    biomeProfile,
                    layer,
                    acceptLimit,
                    stride,
                    perWindowBudget,
                    layerTargetMax,
                    currentLayerCount,
                    null,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount))
            {
                return 0;
            }

            NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
            int candidateCount = math.min(ordered.Count, acceptanceResults.Length);
            Dictionary<long, int> windowCounts = layer == WorldPrefabFamilyProfile.ScatterLayer.Structure
                ? _structureWindowCounts
                : _spawnWindowCounts;
            int added = 0;
            for (int i = 0; i < candidateCount && added < acceptLimit; i++)
            {
                if (acceptanceResults[i] == 0)
                    continue;

                ScatterCandidate candidate = ordered[i];
                if (!TryRegisterDesiredPlacement(candidate.Placement))
                    continue;


                long windowKey = ComposeWindowKey(new ScatterWindowContext(

                    candidate.Placement.CellX,
                    candidate.Placement.CellZ,
                    stride,
                    candidate.Placement.HeightLayerIndex));
                RegisterWindowPlacement(windowKey, windowCounts);

                int layerIndex = (int)layer;
                if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                {
                    layerTopCandidates[layerIndex] = candidate;
                    layerTopValid[layerIndex] = true;
                }

                RegisterLayerFamilyCount(layerFamilyCounts, layer, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, layer, candidate.Placement.BiomeFamily);
                RegisterAccentAndSpawnCounts(candidate.Family, null, structureAccentCounts, ref passiveSpawnCount, ref predatorSpawnCount);

                if (layer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
                    structureCount++;
                else
                    spawnCount++;

                added++;
            }

            return added;
        }

        private bool CanAcceptPatternAccentBudget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterCandidate candidate,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            int clusterCount,
            int structureCount,
            int spawnCount)
        {
            return CanAcceptPatternAccentBudget(
                UsesPatternAccentQuotas(pattern),
                candidate.Family,
                clusterAccentCounts,
                structureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                clusterCount,
                structureCount,
                spawnCount,
                ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster),
                ResolvePatternStructureTargetMax(pattern, biomeProfile),
                ResolvePatternSpawnTargetMax(pattern, biomeProfile),
                ResolvePatternClusterRatioStart(pattern),
                null,
                null,
                math.max(ResolvePatternPassiveSpawnMin(pattern, biomeProfile), ResolvePatternSpawnTargetMax(pattern, biomeProfile)),
                math.max(0, ResolvePatternPredatorSpawnMax(pattern, biomeProfile)));
        }

        private bool CanAcceptPatternAccentBudget(
            bool usesPatternAccentQuotas,
            in ScatterCandidate candidate,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            int clusterCount,
            int structureCount,
            int spawnCount,
            int clusterTargetMax,
            int structureTargetMax,
            int spawnTargetMax,
            int clusterRatioStart,
            float[] clusterAccentRoleMaxRatioBuffer,
            int[] structureAccentRoleMaxBuffer,
            int passiveSpawnMax,
            int predatorSpawnMax)
        {
            return CanAcceptPatternAccentBudget(
                usesPatternAccentQuotas,
                candidate.Family,
                clusterAccentCounts,
                structureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                clusterCount,
                structureCount,
                spawnCount,
                clusterTargetMax,
                structureTargetMax,
                spawnTargetMax,
                clusterRatioStart,
                clusterAccentRoleMaxRatioBuffer,
                structureAccentRoleMaxBuffer,
                passiveSpawnMax,
                predatorSpawnMax);
        }

        private bool CanAcceptPatternAccentBudget(
            bool usesPatternAccentQuotas,
            WorldPrefabFamilyProfile family,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            int clusterCount,
            int structureCount,
            int spawnCount,
            int clusterTargetMax,
            int structureTargetMax,
            int spawnTargetMax,
            int clusterRatioStart,
            float[] clusterAccentRoleMaxRatioBuffer,
            int[] structureAccentRoleMaxBuffer,
            int passiveSpawnMax,
            int predatorSpawnMax)
        {
            if (!usesPatternAccentQuotas || family == null)
                return true;

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
            {
                if (clusterCount >= clusterTargetMax)
                    return false;

                WorldPrefabFamilyProfile.ClusterAccentRole role = GetClusterAccentRole(family);
                if (role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                    return true;

                if (clusterCount < clusterRatioStart)
                    return true;

                int roleIndex = (int)role;
                float maxRatio = clusterAccentRoleMaxRatioBuffer != null &&
                                 roleIndex >= 0 &&
                                 roleIndex < clusterAccentRoleMaxRatioBuffer.Length
                    ? clusterAccentRoleMaxRatioBuffer[roleIndex]
                    : 0f;
                if (maxRatio <= 0f)
                    return false;

                int roleCount = GetClusterAccentCount(clusterAccentCounts, role);
                int totalAfterPlacement = math.max(1, clusterCount + 1);
                int allowed = math.max(1, (int)math.ceil(maxRatio * totalAfterPlacement));
                return roleCount < allowed;
            }

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
            {
                if (structureCount >= structureTargetMax)
                    return false;

                WorldPrefabFamilyProfile.StructureAccentRole role = GetStructureAccentRole(family);
                int roleCount = GetStructureAccentCount(structureAccentCounts, role);
                int roleIndex = (int)role;
                int roleMax = structureAccentRoleMaxBuffer != null &&
                              roleIndex >= 0 &&
                              roleIndex < structureAccentRoleMaxBuffer.Length
                    ? structureAccentRoleMaxBuffer[roleIndex]
                    : 0;
                if (roleMax <= 0)
                    return false;

                return role switch
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => roleCount < roleMax,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => roleCount < roleMax,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => roleCount < roleMax,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => roleCount < roleMax,
                    _ => true
                };
            }

            if (family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return true;

            if (spawnCount >= spawnTargetMax)
                return false;

            if (IsPredatorSpawnFamily(family))
                return predatorSpawnCount < predatorSpawnMax;

            if (IsPassiveSpawnFamily(family))
                return passiveSpawnCount < passiveSpawnMax;

            return true;
        }


        private static int GetWindowPlacementCount(in ScatterWindowContext context, Dictionary<long, int> counts)
        {
            long key = ComposeWindowKey(in context);

            return GetWindowPlacementCount(key, counts);
        }

        private static int GetWindowPlacementCount(long key, Dictionary<long, int> counts)
        {
            return counts.TryGetValue(key, out int count) ? count : 0;
        }


        private static void RegisterWindowPlacement(in ScatterWindowContext context, Dictionary<long, int> counts)
        {
            long key = ComposeWindowKey(in context);

            RegisterWindowPlacement(key, counts);
        }

        private static void RegisterWindowPlacement(long key, Dictionary<long, int> counts)
        {
            counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        private static int EstimateScatterWindowCapacity(int cellDiameter, int stride)
        {
            int safeStride = math.max(1, stride);
            int windowsPerAxis = (int)math.ceil(cellDiameter / (float)safeStride) + 2;
            return math.max(16, windowsPerAxis * windowsPerAxis);
        }

        private static void EnsureScatterWindowBudgetCapacity(Dictionary<long, int> counts, int requiredCapacity)
        {
            if (counts == null || requiredCapacity <= 0)
                return;

            counts.EnsureCapacity(requiredCapacity);
        }


        private static long ComposeWindowKey(in ScatterWindowContext context)
        {
            int safeStride = math.max(1, context.Stride);
            ulong windowX = (uint)(int)math.floor(context.CellX / (float)safeStride);
            ulong windowZ = (uint)(int)math.floor(context.CellZ / (float)safeStride);
            ulong strideBits = (uint)safeStride;
            ulong heightBits = (uint)math.max(0, context.HeightLayerIndex);

            return (long)(
                (windowX & 0xFFFFFUL) |
                ((windowZ & 0xFFFFFUL) << 20) |
                ((strideBits & 0xFFUL) << 40) |
                ((heightBits & 0xFFUL) << 48));
        }

        private static void CountSeafloorSource(
            WorldProceduralFieldSampler.SeafloorSource source,
            ref int mapMagicCount,
            ref int sceneProbeLegacyCount,
            ref int fallbackCount)
        {
            switch (source)
            {
                case WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight:
                    mapMagicCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.TerrainProviderHeight:
                    mapMagicCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.SceneProbeLegacy:
                    sceneProbeLegacyCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.MacroGeologyFallback:
                    fallbackCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.FallbackSynthetic:
                    fallbackCount++;
                    break;
            }
        }

        private static bool IsAuthoritativeTerrainSource(WorldProceduralFieldSampler.SeafloorSource source)
        {
            return source == WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight ||
                   source == WorldProceduralFieldSampler.SeafloorSource.TerrainProviderHeight;
        }

        private static string ResolveLayerTopFamily(
            IReadOnlyList<ScatterCandidate> topCandidates,
            IReadOnlyList<bool> validFlags,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            int index = (int)layer;
            if (index < 0 || index >= topCandidates.Count || index >= validFlags.Count || !validFlags[index])
                return "None";

            return topCandidates[index].Family != null ? topCandidates[index].Family.familyLabel : "None";
        }

        private static void FillOrderedCandidateBuffer(
            Dictionary<long, ScatterCandidate> source,
            List<ScatterCandidate> buffer)
        {
            buffer.Clear();
            if (source == null || source.Count == 0)
                return;

            Dictionary<long, ScatterCandidate>.Enumerator enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
                buffer.Add(enumerator.Current.Value);

            buffer.Sort();
        }

        private static void FillOrderedCandidateBuffer(
            CandidateMap source,
            List<ScatterCandidate> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < source.count; i++)
                buffer.Add(source.GetValueAtOrderedIndex(i));

            buffer.Sort();
        }

        private void RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            _occupiedCellBuffer.Clear();
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement.Family == null || placement.Family.scatterLayer != layer)
                    continue;


                _occupiedCellBuffer.Add(ComposeWindowKey(new ScatterWindowContext(placement.CellX, placement.CellZ, 1, placement.HeightLayerIndex)));

            }
        }

        private ScatterBackendParityReference BuildScatterBackendParityReferenceFromDesiredPlacements()
        {
            ScatterClassicParityAccumulator accumulator = default;
            Dictionary<long, ScatterPlacement> desiredPlacements = _desiredPlacements;
            if (desiredPlacements == null || desiredPlacements.Count == 0)
                return accumulator.ToReference();

            List<long> sortedPlacementKeys = _removalBuffer;
            if (sortedPlacementKeys == null)
                return accumulator.ToReference();

            sortedPlacementKeys.Clear();
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement == null || placement.Family == null)
                    continue;

                sortedPlacementKeys.Add(enumerator.Current.Key);
            }

            sortedPlacementKeys.Sort();
            for (int i = 0; i < sortedPlacementKeys.Count; i++)
            {
                if (!desiredPlacements.TryGetValue(sortedPlacementKeys[i], out ScatterPlacement placement) ||
                    placement == null ||
                    placement.Family == null)
                {
                    continue;
                }

                accumulator.Register(placement, placement.Family.scatterLayer);
            }

            sortedPlacementKeys.Clear();
            return accumulator.ToReference();
        }

        private static void RegisterLayerFamilyCount(
            IReadOnlyList<Dictionary<string, int>> counters,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            WorldPrefabFamilyProfile family)
        {
            int index = (int)layer;
            if (family == null || counters == null || index < 0 || index >= counters.Count || counters[index] == null)
                return;

            string familyLabel = string.IsNullOrWhiteSpace(family.familyLabel) ? family.familyId : family.familyLabel;
            if (string.IsNullOrWhiteSpace(familyLabel))
                familyLabel = "Unknown";

            counters[index].TryGetValue(familyLabel, out int count);
            counters[index][familyLabel] = count + 1;
        }

        private static void RegisterLayerBiomeCount(
            IReadOnlyList<Dictionary<string, int>> counters,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily)
        {
            int index = (int)layer;
            if (counters == null || index < 0 || index >= counters.Count || counters[index] == null)
                return;

            string biomeLabel = ResolveBiomeLabel(biomeFamily);
            counters[index].TryGetValue(biomeLabel, out int count);
            counters[index][biomeLabel] = count + 1;
        }

        private static void RegisterAccentAndSpawnCounts(
            WorldPrefabFamilyProfile family,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount)
        {
            if (family == null)
                return;

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
            {
                RegisterClusterAccentCount(clusterAccentCounts, GetClusterAccentRole(family));
                return;
            }

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
            {
                RegisterStructureAccentCount(structureAccentCounts, GetStructureAccentRole(family));
                return;
            }

            if (family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return;

            if (IsPassiveSpawnFamily(family))
                passiveSpawnCount++;
            else if (IsPredatorSpawnFamily(family))
                predatorSpawnCount++;
        }

        private static void RegisterClusterAccentCount(int[] counts, WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (counts == null)
                return;

            int index = (int)role;
            if (index < 0 || index >= counts.Length)
                return;

            counts[index]++;
        }

        private static int GetClusterAccentCount(int[] counts, WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (counts == null)
                return 0;

            int index = (int)role;
            return index >= 0 && index < counts.Length ? counts[index] : 0;
        }

        private static void RegisterStructureAccentCount(int[] counts, WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (counts == null)
                return;

            int index = (int)role;
            if (index < 0 || index >= counts.Length)
                return;

            counts[index]++;
        }

        private static int GetStructureAccentCount(int[] counts, WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (counts == null)
                return 0;

            int index = (int)role;
            return index >= 0 && index < counts.Length ? counts[index] : 0;
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole GetStructureAccentRole(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return WorldPrefabFamilyProfile.StructureAccentRole.None;

            if (family.structureAccentRole != WorldPrefabFamilyProfile.StructureAccentRole.None)
                return family.structureAccentRole;

            return family.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
                _ => WorldPrefabFamilyProfile.StructureAccentRole.None
            };
        }

        private static WorldPrefabFamilyProfile.ClusterAccentRole GetClusterAccentRole(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return WorldPrefabFamilyProfile.ClusterAccentRole.None;

            if (family.clusterAccentRole != WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return family.clusterAccentRole;

            if (family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                return WorldPrefabFamilyProfile.ClusterAccentRole.None;

            return family.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest,
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                _ => WorldPrefabFamilyProfile.ClusterAccentRole.None
            };
        }

        private static bool IsPassiveSpawnFamily(WorldPrefabFamilyProfile family)
        {
            return family != null && family.IsPassiveSpawnFamily;
        }

        private static bool IsPredatorSpawnFamily(WorldPrefabFamilyProfile family)
        {
            return family != null && family.IsPredatorSpawnFamily;
        }

        private string ResolveDominantStructureAccentRole(int[] counts, out int count)
        {
            count = 0;
            if (counts == null || counts.Length == 0)
                return "None";

            WorldPrefabFamilyProfile.StructureAccentRole dominantRole = WorldPrefabFamilyProfile.StructureAccentRole.None;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= count)
                    continue;

                count = counts[i];
                dominantRole = (WorldPrefabFamilyProfile.StructureAccentRole)i;
            }

            return dominantRole == WorldPrefabFamilyProfile.StructureAccentRole.None
                ? "None"
                : GetStructureAccentRoleLabel(dominantRole);
        }

        private string ResolveDominantClusterAccentRole(int[] counts, out int count)
        {
            count = 0;
            if (counts == null || counts.Length == 0)
                return "None";

            WorldPrefabFamilyProfile.ClusterAccentRole dominantRole = WorldPrefabFamilyProfile.ClusterAccentRole.None;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= count)
                    continue;

                count = counts[i];
                dominantRole = (WorldPrefabFamilyProfile.ClusterAccentRole)i;
            }

            return dominantRole == WorldPrefabFamilyProfile.ClusterAccentRole.None
                ? "None"
                : GetClusterAccentRoleLabel(dominantRole);
        }

        private static void RegisterStringCount(Dictionary<string, int> counters, string label)
        {
            if (counters == null)
                return;

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "None" : label;
            counters.TryGetValue(safeLabel, out int count);
            counters[safeLabel] = count + 1;
        }

        private static void RegisterProfileCount(
            Dictionary<HectonBiomeMatrixProfile, int> counters,
            HectonBiomeMatrixProfile profile)
        {
            if (counters == null || profile == null)
                return;

            counters.TryGetValue(profile, out int count);
            counters[profile] = count + 1;
        }

        private static string ResolveDominantLayerFamily(
            IReadOnlyList<Dictionary<string, int>> counters,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            out int count)
        {
            count = 0;
            int index = (int)layer;
            if (counters == null || index < 0 || index >= counters.Count || counters[index] == null || counters[index].Count == 0)
                return "None";

            string bestFamily = "None";
            Dictionary<string, int>.Enumerator enumerator = counters[index].GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> pair = enumerator.Current;
                if (pair.Value <= count)
                    continue;

                count = pair.Value;
                bestFamily = pair.Key;
            }

            return bestFamily;
        }

        private static string ResolveDominantCounter(
            Dictionary<string, int> counters,
            out int count)
        {
            count = 0;
            if (counters == null || counters.Count == 0)
                return "None";

            string bestLabel = "None";
            Dictionary<string, int>.Enumerator enumerator = counters.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> pair = enumerator.Current;
                if (pair.Value <= count)
                    continue;

                count = pair.Value;
                bestLabel = pair.Key;
            }

            return bestLabel;
        }

        private static HectonBiomeMatrixProfile ResolveDominantBiomeMatrixProfile(
            Dictionary<HectonBiomeMatrixProfile, int> counters,
            HectonBiomeMatrixProfile fallbackProfile)
        {
            if (counters == null || counters.Count == 0)
                return fallbackProfile;

            HectonBiomeMatrixProfile bestProfile = fallbackProfile;
            int bestCount = -1;
            int bestTieBreaker = int.MinValue;
            Dictionary<HectonBiomeMatrixProfile, int>.Enumerator enumerator = counters.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<HectonBiomeMatrixProfile, int> pair = enumerator.Current;
                HectonBiomeMatrixProfile profile = pair.Key;
                if (profile == null)
                    continue;

                int tieBreaker = GetBiomeMatrixTieBreaker(profile);
                if (pair.Value > bestCount || (pair.Value == bestCount && tieBreaker > bestTieBreaker))
                {
                    bestProfile = profile;
                    bestCount = pair.Value;
                    bestTieBreaker = tieBreaker;
                }
            }

            return bestProfile ?? fallbackProfile;
        }

        private static string ResolveBiomeLabel(Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return "None";

            return string.IsNullOrWhiteSpace(biomeFamily.familyLabel)
                ? biomeFamily.familyId
                : biomeFamily.familyLabel;
        }

        private static string ResolveBiomeMatrixLabel(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return "None";

            return string.IsNullOrWhiteSpace(biomeProfile.biomeName)
                ? "Unnamed Matrix Biome"
                : biomeProfile.biomeName;
        }

        private static int GetBiomeMatrixTieBreaker(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return int.MinValue;

            int placeholderPenalty = profile.isPlaceholder ? -1000 : 0;
            return placeholderPenalty
                   + (profile.rewardPull * 5)
                   + (profile.landmarkStrength * 4)
                   + (profile.salvageBias * 3)
                   + profile.commonResourceBias
                   + profile.uncommonResourceBias
                   + profile.rareResourceBias;
        }

        private void TrackRescueCandidate(
            in ScatterCandidate candidate,
            bool needsPreviewRescue,
            bool needsRuntimeSpawnRescue,
            ref ScatterRescueTrackingContext trackingContext)
        {
            if (!needsPreviewRescue && !needsRuntimeSpawnRescue)
                return;

            switch (candidate.Family.scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    TrackWindowCandidate(candidate, 1, ref trackingContext.GroundCandidates);
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterCandidates);
                    switch (GetClusterAccentRole(candidate.Family))
                    {
                        case WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterFertileCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterNestCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterResourceCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterShelterCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterHazardCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterDebrisCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.RockCover:
                            TrackWindowCandidate(candidate, 1, ref trackingContext.ClusterRockCandidates);
                            break;
                    }
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    TrackWindowCandidate(candidate, trackingContext.StructureStride, trackingContext.StructureCandidates);
                    switch (GetStructureAccentRole(candidate.Family))
                    {
                        case WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark:
                            TrackWindowCandidate(candidate, trackingContext.StructureStride, ref trackingContext.StructureNaturalCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.TechFragment:
                            TrackWindowCandidate(candidate, trackingContext.StructureStride, ref trackingContext.StructureTechCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.CaveRead:
                            TrackWindowCandidate(candidate, trackingContext.StructureStride, ref trackingContext.StructureCaveCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette:
                            TrackWindowCandidate(candidate, trackingContext.StructureStride, ref trackingContext.StructureBioCandidates);
                            break;
                    }
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    TrackWindowCandidate(candidate, trackingContext.SpawnStride, trackingContext.SpawnCandidates);
                    if (IsPassiveSpawnFamily(candidate.Family))
                        TrackWindowCandidate(candidate, trackingContext.SpawnStride, ref trackingContext.PassiveSpawnCandidates);
                    else if (IsPredatorSpawnFamily(candidate.Family))
                        TrackWindowCandidate(candidate, trackingContext.SpawnStride, ref trackingContext.PredatorSpawnCandidates);
                    break;
            }
        }

        private void TrackWindowCandidate(
            in ScatterCandidate candidate,
            int stride,
            Dictionary<long, ScatterCandidate> windowCandidates)
        {

            long windowKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, stride, candidate.Placement.HeightLayerIndex));

            if (windowCandidates.TryGetValue(windowKey, out ScatterCandidate existing))
            {
                if (candidate.Score <= existing.Score)
                    return;

                ReleasePlacement(existing.Placement);
                windowCandidates[windowKey] = candidate;
                RetainPlacement(candidate.Placement);
                return;
            }

            windowCandidates[windowKey] = candidate;
            RetainPlacement(candidate.Placement);
        }

        private void TrackWindowCandidate(
            in ScatterCandidate candidate,
            int stride,
            ref CandidateMap windowCandidates)
        {

            long windowKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, stride, candidate.Placement.HeightLayerIndex));

            if (windowCandidates.TryGetIndex(windowKey, out int existingIndex))
            {
                ScatterCandidate existing = windowCandidates.GetValueAtIndex(existingIndex);
                if (candidate.Score <= existing.Score)
                    return;

                ReleasePlacement(existing.Placement);
                windowCandidates.SetValueAtIndex(existingIndex, candidate);
                RetainPlacement(candidate.Placement);
                return;
            }

            if (windowCandidates.TryAppendKnownUnique(windowKey, candidate))
            {
                RetainPlacement(candidate.Placement);
            }
        }

        private void InjectRescuePlacementsIfNeeded(
            in ScatterRescueContext rescueContext,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            out int injectedSpawnPlacements)
        {
            InjectRescueGroundPlacements(in rescueContext);
            InjectRescueClusterPlacements(in rescueContext, ref passiveSpawnCount, ref predatorSpawnCount);
            InjectRescueStructurePlacements(in rescueContext, ref passiveSpawnCount, ref predatorSpawnCount);
            InjectRescueSpawnPlacements(in rescueContext, ref passiveSpawnCount, ref predatorSpawnCount, out injectedSpawnPlacements);
        }

        private void InjectRescueGroundPlacements(in ScatterRescueContext rescueContext)
        {
            int groundLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Ground;
            int minimumGroundCount = ResolveMinimumGroundPlacements(rescueContext.Pattern, rescueContext.BiomeProfile);
            if (rescueContext.LayerPlacementCounts[groundLayerIndex] < minimumGroundCount)
            {
                int added = InjectGroundCandidates(
                    minimumGroundCount - rescueContext.LayerPlacementCounts[groundLayerIndex],
                    rescueContext.GroundCandidates,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts);
                rescueContext.LayerPlacementCounts[groundLayerIndex] += added;
            }
        }

        private void InjectRescueClusterPlacements(in ScatterRescueContext rescueContext, ref int passiveSpawnCount, ref int predatorSpawnCount)
        {
            int clusterLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster;

            InjectExistingRescueClusterPlacements(in rescueContext, clusterLayerIndex, ref passiveSpawnCount, ref predatorSpawnCount);
            InjectMinimumRescueClusterPlacements(in rescueContext, clusterLayerIndex, ref passiveSpawnCount, ref predatorSpawnCount);
        }

        private void InjectExistingRescueClusterPlacements(in ScatterRescueContext rescueContext, int clusterLayerIndex, ref int passiveSpawnCount, ref int predatorSpawnCount)
        {
            if (rescueContext.BiomeProfile == null || rescueContext.LayerPlacementCounts[clusterLayerIndex] <= 0)
            {
                return;
            }

            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            int added = InjectPreferredClusterFamilyCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                Mathf.Max(1, rescueContext.ClusterBudget),
                rescueContext.ClusterCandidates,
                false,
                rescueContext.LayerPlacementCounts,
                rescueContext.ClusterAccentCounts,
                rescueContext.StructureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[clusterLayerIndex] += added;

            added = InjectServiceClusterAccentCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                Mathf.Max(1, rescueContext.ClusterBudget),
                rescueContext.ClusterCandidates,
                rescueContext.ClusterResourceCandidates,
                rescueContext.ClusterDebrisCandidates,
                false,
                rescueContext.LayerPlacementCounts,
                rescueContext.ClusterAccentCounts,
                rescueContext.StructureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[clusterLayerIndex] += added;

            added = InjectLandmarkCorridorClusterAccentCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                Mathf.Max(1, rescueContext.ClusterBudget),
                rescueContext.ClusterCandidates,
                rescueContext.ClusterResourceCandidates,
                false,
                rescueContext.LayerPlacementCounts,
                rescueContext.ClusterAccentCounts,
                rescueContext.StructureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[clusterLayerIndex] += added;
        }

        private void InjectMinimumRescueClusterPlacements(in ScatterRescueContext rescueContext, int clusterLayerIndex, ref int passiveSpawnCount, ref int predatorSpawnCount)
        {
            int minimumClusterCount = ResolveMinimumClusterPlacements(rescueContext.Pattern, rescueContext.BiomeProfile);
            if (rescueContext.LayerPlacementCounts[clusterLayerIndex] >= minimumClusterCount)
            {
                return;
            }

            int added = UsesPatternAccentQuotas(rescueContext.Pattern)
                ? InjectPatternClusterAccentCandidates(
                    rescueContext.Pattern,
                    rescueContext.BiomeProfile,
                    Mathf.Max(1, rescueContext.ClusterBudget),
                    minimumClusterCount,
                    rescueContext.ClusterCandidates,
                    rescueContext.ClusterFertileCandidates,
                    rescueContext.ClusterNestCandidates,
                    rescueContext.ClusterResourceCandidates,
                    rescueContext.ClusterShelterCandidates,
                    rescueContext.ClusterHazardCandidates,
                    rescueContext.ClusterDebrisCandidates,
                    rescueContext.ClusterRockCandidates,
                    rescueContext.LayerPlacementCounts,
                    rescueContext.ClusterAccentCounts,
                    rescueContext.StructureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts)
                : InjectClusterCandidates(
                    rescueContext.Pattern,
                    rescueContext.BiomeProfile,
                    minimumClusterCount - rescueContext.LayerPlacementCounts[clusterLayerIndex],
                    Mathf.Max(1, rescueContext.ClusterBudget),
                    rescueContext.ClusterCandidates,
                    rescueContext.ClusterAccentCounts,
                    rescueContext.StructureAccentCounts,
                    rescueContext.LayerPlacementCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    true,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[clusterLayerIndex] += added;
        }

        private void InjectRescueStructurePlacements(in ScatterRescueContext rescueContext, ref int passiveSpawnCount, ref int predatorSpawnCount)
        {
            int structureLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Structure;

            InjectExistingRescueStructurePlacements(in rescueContext, structureLayerIndex);
            InjectMinimumRescueStructurePlacements(in rescueContext, structureLayerIndex, ref passiveSpawnCount, ref predatorSpawnCount);
        }

        private void InjectExistingRescueStructurePlacements(in ScatterRescueContext rescueContext, int structureLayerIndex)
        {
            if (rescueContext.BiomeProfile == null)
            {
                return;
            }

            List<ScatterCandidate> orderedStructureCandidates = _windowOrderedCandidates;
            FillOrderedCandidateBuffer(rescueContext.StructureCandidates, orderedStructureCandidates);

            int added = InjectPreferredStructureFamilyCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                rescueContext.StructureStride,
                Mathf.Max(1, rescueContext.StructureBudget),
                orderedStructureCandidates,
                rescueContext.LayerPlacementCounts,
                rescueContext.StructureAccentCounts,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[structureLayerIndex] += added;

            added = InjectServiceStructureDomainCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                rescueContext.StructureStride,
                Mathf.Max(1, rescueContext.StructureBudget),
                orderedStructureCandidates,
                rescueContext.LayerPlacementCounts,
                rescueContext.StructureAccentCounts,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[structureLayerIndex] += added;

            added = InjectRuinPlacementModeCandidates(
                rescueContext.Pattern,
                rescueContext.BiomeProfile,
                rescueContext.StructureStride,
                Mathf.Max(1, rescueContext.StructureBudget),
                orderedStructureCandidates,
                rescueContext.LayerPlacementCounts,
                rescueContext.StructureAccentCounts,
                rescueContext.LayerTopCandidates,
                rescueContext.LayerTopValid,
                rescueContext.LayerFamilyCounts,
                rescueContext.LayerBiomeCounts);
            rescueContext.LayerPlacementCounts[structureLayerIndex] += added;
        }

        private void InjectMinimumRescueStructurePlacements(in ScatterRescueContext rescueContext, int structureLayerIndex, ref int passiveSpawnCount, ref int predatorSpawnCount)
        {
            int minimumStructureCount = ResolveMinimumStructurePlacements(rescueContext.Pattern, rescueContext.BiomeProfile);
            if (rescueContext.LayerPlacementCounts[structureLayerIndex] >= minimumStructureCount)
            {
                return;
            }

            int added = UsesPatternAccentQuotas(rescueContext.Pattern)
                ? InjectPatternStructureAccentCandidates(
                    rescueContext.Pattern,
                    rescueContext.BiomeProfile,
                    rescueContext.StructureStride,
                    Mathf.Max(1, rescueContext.StructureBudget),
                    minimumStructureCount,
                    rescueContext.StructureCandidates,
                    rescueContext.StructureNaturalCandidates,
                    rescueContext.StructureTechCandidates,
                    rescueContext.StructureCaveCandidates,
                    rescueContext.StructureBioCandidates,
                    rescueContext.LayerPlacementCounts,
                    rescueContext.StructureAccentCounts,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts)
                : InjectWindowCandidates(
                    minimumStructureCount - rescueContext.LayerPlacementCounts[structureLayerIndex],
                    rescueContext.StructureStride,
                    Mathf.Max(1, rescueContext.StructureBudget),
                    rescueContext.StructureCandidates,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts,
                    rescueContext.LayerPlacementCounts,
                    rescueContext.StructureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    rescueContext.Pattern,
                    rescueContext.BiomeProfile,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure);
            rescueContext.LayerPlacementCounts[structureLayerIndex] += added;
        }

        private void InjectRescueSpawnPlacements(in ScatterRescueContext rescueContext, ref int passiveSpawnCount, ref int predatorSpawnCount, out int injectedSpawnPlacements)
        {
            injectedSpawnPlacements = 0;
            int spawnLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            if (rescueContext.BiomeProfile != null)
            {
                List<ScatterCandidate> orderedSpawnCandidates = _windowOrderedCandidates;
                FillOrderedCandidateBuffer(rescueContext.SpawnCandidates, orderedSpawnCandidates);

                int added = InjectPreferredSpawnFamilyCandidates(
                    rescueContext.Pattern,
                    rescueContext.BiomeProfile,
                    rescueContext.SpawnStride,
                    Mathf.Max(1, rescueContext.SpawnBudget),
                    orderedSpawnCandidates,
                    rescueContext.LayerPlacementCounts,
                    rescueContext.StructureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    rescueContext.LayerTopCandidates,
                    rescueContext.LayerTopValid,
                    rescueContext.LayerFamilyCounts,
                    rescueContext.LayerBiomeCounts);
                rescueContext.LayerPlacementCounts[spawnLayerIndex] += added;
                injectedSpawnPlacements += added;
            }

            int minimumSpawnCount = ResolveMinimumSpawnPlacements(rescueContext.Pattern, rescueContext.BiomeProfile);
            if (rescueContext.LayerPlacementCounts[spawnLayerIndex] < minimumSpawnCount)
            {
                int added = UsesPatternAccentQuotas(rescueContext.Pattern)
                    ? InjectPatternSpawnCandidates(
                        rescueContext.Pattern,
                        rescueContext.BiomeProfile,
                        rescueContext.SpawnStride,
                        Mathf.Max(1, rescueContext.SpawnBudget),
                        minimumSpawnCount,
                        rescueContext.SpawnCandidates,
                        rescueContext.PassiveSpawnCandidates,
                        rescueContext.PredatorSpawnCandidates,
                        rescueContext.LayerPlacementCounts,
                        rescueContext.StructureAccentCounts,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        rescueContext.LayerTopCandidates,
                        rescueContext.LayerTopValid,
                        rescueContext.LayerFamilyCounts,
                        rescueContext.LayerBiomeCounts)
                    : InjectWindowCandidates(
                        minimumSpawnCount - rescueContext.LayerPlacementCounts[spawnLayerIndex],
                        rescueContext.SpawnStride,
                        Mathf.Max(1, rescueContext.SpawnBudget),
                        rescueContext.SpawnCandidates,
                        rescueContext.LayerTopCandidates,
                        rescueContext.LayerTopValid,
                        rescueContext.LayerFamilyCounts,
                        rescueContext.LayerBiomeCounts,
                        rescueContext.LayerPlacementCounts,
                        rescueContext.StructureAccentCounts,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        rescueContext.Pattern,
                        rescueContext.BiomeProfile,
                        WorldPrefabFamilyProfile.ScatterLayer.Spawn);
                rescueContext.LayerPlacementCounts[spawnLayerIndex] += added;
                injectedSpawnPlacements += added;
            }
        }

        private int InjectPatternClusterAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int perCellBudget,
            int targetClusterCount,
            CandidateMap rescueCandidates,
            CandidateMap fertileCandidates,
            CandidateMap nestCandidates,
            CandidateMap resourceCandidates,
            CandidateMap shelterCandidates,
            CandidateMap hazardCandidates,
            CandidateMap debrisCandidates,
            CandidateMap rockCandidates,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates.count == 0)
                return 0;

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            foreach (WorldPrefabFamilyProfile.ClusterAccentRole accentRole in GetPatternClusterAccentPriority(pattern))
            {
                int requiredCount = ResolvePatternClusterAccentMin(pattern, biomeProfile, accentRole);
                if (requiredCount <= 0)
                    continue;

                CandidateMap sourceCandidates = GetClusterAccentCandidatePool(
                    accentRole,
                    fertileCandidates,
                    nestCandidates,
                    resourceCandidates,
                    shelterCandidates,
                    hazardCandidates,
                    debrisCandidates,
                    rockCandidates,
                    rescueCandidates);
                if (sourceCandidates.count == 0)
                    continue;

                added += InjectClusterAccentRoleCandidates(
                    pattern,
                    biomeProfile,
                    sourceCandidates,
                    perCellBudget,
                    accentRole,
                    requiredCount,
                    ref clusterCount,
                    structureCount,
                    spawnCount,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    false,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
            }

            int remaining = Mathf.Max(0, targetClusterCount - clusterCount);
            if (remaining > 0)
            {
                int originalClusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
                layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster] = clusterCount;
                added += InjectClusterCandidates(
                    pattern,
                    biomeProfile,
                    remaining,
                    perCellBudget,
                    rescueCandidates,
                    clusterAccentCounts,
                    structureAccentCounts,
                    layerPlacementCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    false,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster] = originalClusterCount;
            }

            return added;
        }

        private int InjectClusterAccentRoleCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            CandidateMap rescueCandidates,
            int perCellBudget,
            WorldPrefabFamilyProfile.ClusterAccentRole accentRole,
            int requiredCount,
            ref int clusterCount,
            int structureCount,
            int spawnCount,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            bool rebuildOccupiedBuffer,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates.count == 0 || requiredCount <= 0)
                return 0;

            List<ScatterCandidate> ordered = _clusterAccentOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int currentCount = GetClusterAccentCount(clusterAccentCounts, accentRole);
            int needed = Mathf.Max(0, requiredCount - currentCount);
            if (needed <= 0)
                return 0;

            if (!TryEvaluateScatterRescueCandidateAcceptanceBatch(
                    ordered,
                    ScatterRescueCandidateFilter.ClusterAccentFilter(accentRole),
                    pattern,
                    biomeProfile,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster,
                    needed,
                    1,
                    0,
                    ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster),
                    clusterCount,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount))
            {
                return 0;
            }

            NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
            int candidateCount = Mathf.Min(ordered.Count, acceptanceResults.Length);
            int added = 0;
            if (rebuildOccupiedBuffer)
                RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            for (int i = 0; i < candidateCount && needed > 0; i++)
            {
                if (acceptanceResults[i] == 0)
                    continue;

                ScatterCandidate candidate = ordered[i];

                long cellKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, 1, candidate.Placement.HeightLayerIndex));


                if (!TryRegisterDesiredPlacement(candidate.Placement))
                    continue;
                _occupiedCellBuffer.Add(cellKey);
                int layerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster;
                if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                {
                    layerTopCandidates[layerIndex] = candidate;
                    layerTopValid[layerIndex] = true;
                }

                RegisterLayerFamilyCount(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Placement.BiomeFamily);
                RegisterClusterAccentCount(clusterAccentCounts, accentRole);

                clusterCount++;
                added++;
                needed--;
            }

            return added;
        }

        private int InjectClusterCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int targetCount,
            int perCellBudget,
            CandidateMap rescueCandidates,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int[] layerPlacementCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            bool rebuildOccupiedBuffer,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (targetCount <= 0 || rescueCandidates.count == 0)
                return 0;

            List<ScatterCandidate> ordered = _clusterOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            if (rebuildOccupiedBuffer)
                RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            if (!TryEvaluateScatterRescueCandidateAcceptanceBatch(
                    ordered,
                    ScatterRescueCandidateFilter.None,
                    pattern,
                    biomeProfile,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster,
                    targetCount,
                    1,
                    0,
                    ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster),
                    clusterCount,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount))
            {
                return 0;
            }

            NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
            int candidateCount = Mathf.Min(ordered.Count, acceptanceResults.Length);
            for (int i = 0; i < candidateCount && added < targetCount; i++)
            {
                if (acceptanceResults[i] == 0)
                    continue;

                ScatterCandidate candidate = ordered[i];

                long cellKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, 1, candidate.Placement.HeightLayerIndex));


                if (!TryRegisterDesiredPlacement(candidate.Placement))
                    continue;
                _occupiedCellBuffer.Add(cellKey);
                int layerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster;
                if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                {
                    layerTopCandidates[layerIndex] = candidate;
                    layerTopValid[layerIndex] = true;
                }
                RegisterLayerFamilyCount(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Placement.BiomeFamily);
                RegisterClusterAccentCount(clusterAccentCounts, GetClusterAccentRole(candidate.Family));

                clusterCount++;
                added++;
            }

            return added;
        }

        private int InjectPreferredClusterFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int perCellBudget,
            CandidateMap rescueCandidates,
            bool rebuildOccupiedBuffer,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (biomeProfile?.preferredClusterFamilies == null || rescueCandidates.count == 0)
                return 0;

            List<ScatterCandidate> ordered = _exactClusterOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);
            if (rebuildOccupiedBuffer)
                RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            Dictionary<int, int> familyCounts = _memory != null ? _memory.PreferredFamilyPlacementCounts : null;
            if (familyCounts == null)
                return 0;

            BuildPreferredFamilyPlacementCounts(
                biomeProfile.preferredClusterFamilies,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster,
                familyCounts);

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            for (int i = 0; i < biomeProfile.preferredClusterFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferredFamily = biomeProfile.preferredClusterFamilies[i];
                int targetCount = ResolvePreferredClusterFamilyTarget(pattern, biomeProfile, i);
                if (preferredFamily == null || targetCount <= 0)
                    continue;

                int familyAdded = InjectExactClusterFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    perCellBudget,
                    ordered,
                    GetPreferredFamilyPlacementCount(familyCounts, preferredFamily),
                    targetCount,
                    ref clusterCount,
                    structureCount,
                    spawnCount,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                added += familyAdded;
                IncrementPreferredFamilyPlacementCount(familyCounts, preferredFamily, familyAdded);
            }

            return added;
        }

        private int InjectExactClusterFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile preferredFamily,
            int perCellBudget,
            List<ScatterCandidate> ordered,
            int currentFamilyCount,
            int targetCount,
            ref int clusterCount,
            int structureCount,
            int spawnCount,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (preferredFamily == null || ordered == null || ordered.Count == 0 || targetCount <= 0)
                return 0;

            int needed = Mathf.Max(0, targetCount - currentFamilyCount);
            if (needed <= 0)
                return 0;

            if (!TryEvaluateScatterRescueCandidateAcceptanceBatch(
                    ordered,
                    ScatterRescueCandidateFilter.ExactFamilyFilter(preferredFamily),
                    pattern,
                    biomeProfile,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster,
                    needed,
                    1,
                    0,
                    ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster),
                    clusterCount,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount))
            {
                return 0;
            }

            NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
            int candidateCount = Mathf.Min(ordered.Count, acceptanceResults.Length);
            int added = 0;
            for (int i = 0; i < candidateCount && needed > 0; i++)
            {
                if (acceptanceResults[i] == 0)
                    continue;

                ScatterCandidate candidate = ordered[i];

                long cellKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, 1, candidate.Placement.HeightLayerIndex));


                if (!TryRegisterDesiredPlacement(candidate.Placement))
                    continue;
                _occupiedCellBuffer.Add(cellKey);
                int layerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster;
                if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                {
                    layerTopCandidates[layerIndex] = candidate;
                    layerTopValid[layerIndex] = true;
                }

                RegisterLayerFamilyCount(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, candidate.Placement.BiomeFamily);
                RegisterClusterAccentCount(clusterAccentCounts, GetClusterAccentRole(candidate.Family));

                clusterCount++;
                added++;
                needed--;
            }

            return added;
        }

        private int InjectGroundCandidates(
            int targetCount,
            CandidateMap rescueCandidates,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (targetCount <= 0 || rescueCandidates.count == 0)
                return 0;

            List<ScatterCandidate> ordered = _groundOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Ground);

            if (!TryEvaluateScatterRescueCandidateAcceptanceBatch(
                    ordered,
                    ScatterRescueCandidateFilter.None,
                    default,
                    null,
                    WorldPrefabFamilyProfile.ScatterLayer.Ground,
                    targetCount,
                    1,
                    0,
                    int.MaxValue,
                    0,
                    null,
                    null,
                    0,
                    0))
            {
                return 0;
            }

            NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
            int candidateCount = Mathf.Min(ordered.Count, acceptanceResults.Length);
            for (int i = 0; i < candidateCount && added < targetCount; i++)
            {
                if (acceptanceResults[i] == 0)
                    continue;

                ScatterCandidate candidate = ordered[i];

                long cellKey = ComposeWindowKey(new ScatterWindowContext(candidate.Placement.CellX, candidate.Placement.CellZ, 1, candidate.Placement.HeightLayerIndex));


                if (!TryRegisterDesiredPlacement(candidate.Placement))
                    continue;
                _occupiedCellBuffer.Add(cellKey);
                int layerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Ground;
                if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                {
                    layerTopCandidates[layerIndex] = candidate;
                    layerTopValid[layerIndex] = true;
                }

                RegisterLayerFamilyCount(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, candidate.Placement.BiomeFamily);
                added++;
            }

            return added;
        }

        private int InjectWindowCandidates(
            int targetCount,
            int stride,
            int perWindowBudget,
            Dictionary<long, ScatterCandidate> rescueCandidates,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            if (targetCount <= 0 || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            List<ScatterCandidate> ordered = _windowOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            return InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.None,
                targetCount,
                stride,
                perWindowBudget,
                layer,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
        }

        private int InjectPreferredStructureFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (biomeProfile?.preferredStructureFamilies == null || ordered == null || ordered.Count == 0)
                return 0;
            Dictionary<int, int> familyCounts = _memory != null ? _memory.PreferredFamilyPlacementCounts : null;
            if (familyCounts == null)
                return 0;

            BuildPreferredFamilyPlacementCounts(
                biomeProfile.preferredStructureFamilies,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                familyCounts);

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int passiveUnused = 0;
            int predatorUnused = 0;
            for (int i = 0; i < biomeProfile.preferredStructureFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferredFamily = biomeProfile.preferredStructureFamilies[i];
                int targetCount = ResolvePreferredStructureFamilyTarget(pattern, biomeProfile, i);
                if (preferredFamily == null || targetCount <= 0)
                    continue;

                int familyAdded = InjectExactWindowFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure,
                    ordered,
                    GetPreferredFamilyPlacementCount(familyCounts, preferredFamily),
                    targetCount,
                    structureAccentCounts,
                    ref passiveUnused,
                    ref predatorUnused,
                    ref structureCount,
                    ref spawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                added += familyAdded;
                IncrementPreferredFamilyPlacementCount(familyCounts, preferredFamily, familyAdded);
            }

            return added;
        }

        private int InjectServiceStructureDomainCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (!IsServiceLikePattern(pattern) || ordered == null || ordered.Count == 0)
                return 0;

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int passiveUnused = 0;
            int predatorUnused = 0;
            CountPlacedServiceStructureDomains(
                out int serviceScarCount,
                out int powerRouteCount,
                out int ruinModuleCount);

            added += InjectStructureDomainCandidates(
                pattern,
                biomeProfile,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar,
                ResolveServiceStructureDomainTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar),
                stride,
                perWindowBudget,
                ordered,
                ref serviceScarCount,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            added += InjectStructureDomainCandidates(
                pattern,
                biomeProfile,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute,
                ResolveServiceStructureDomainTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute),
                stride,
                perWindowBudget,
                ordered,
                ref powerRouteCount,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            added += InjectStructureDomainCandidates(
                pattern,
                biomeProfile,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule,
                ResolveServiceStructureDomainTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ProceduralDomain.RuinModule),
                stride,
                perWindowBudget,
                ordered,
                ref ruinModuleCount,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            return added;
        }

        private int InjectPreferredSpawnFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (biomeProfile?.preferredSpawnFamilies == null || ordered == null || ordered.Count == 0)
                return 0;
            Dictionary<int, int> familyCounts = _memory != null ? _memory.PreferredFamilyPlacementCounts : null;
            if (familyCounts == null)
                return 0;

            BuildPreferredFamilyPlacementCounts(
                biomeProfile.preferredSpawnFamilies,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                familyCounts);

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            for (int i = 0; i < biomeProfile.preferredSpawnFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferredFamily = biomeProfile.preferredSpawnFamilies[i];
                int targetCount = ResolvePreferredSpawnFamilyTarget(pattern, biomeProfile, preferredFamily, i);
                if (preferredFamily == null || targetCount <= 0)
                    continue;

                int familyAdded = InjectExactWindowFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                    ordered,
                    GetPreferredFamilyPlacementCount(familyCounts, preferredFamily),
                    targetCount,
                    structureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    ref structureCount,
                    ref spawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                added += familyAdded;
                IncrementPreferredFamilyPlacementCount(familyCounts, preferredFamily, familyAdded);
            }

            return added;
        }

        private int InjectServiceClusterAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int perCellBudget,
            CandidateMap rescueCandidates,
            CandidateMap resourceCandidates,
            CandidateMap debrisCandidates,
            bool rebuildOccupiedBuffer,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (!IsServiceLikePattern(pattern) || rescueCandidates.count == 0)
                return 0;

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            if (rebuildOccupiedBuffer)
                RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            added += InjectClusterAccentRoleCandidates(
                pattern,
                biomeProfile,
                debrisCandidates.count > 0 ? debrisCandidates : rescueCandidates,
                perCellBudget,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                ResolveServiceClusterAccentTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField),
                ref clusterCount,
                structureCount,
                spawnCount,
                clusterAccentCounts,
                structureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                false,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            added += InjectClusterAccentRoleCandidates(
                pattern,
                biomeProfile,
                resourceCandidates.count > 0 ? resourceCandidates : rescueCandidates,
                perCellBudget,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                ResolveServiceClusterAccentTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket),
                ref clusterCount,
                structureCount,
                spawnCount,
                clusterAccentCounts,
                structureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                false,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            return added;
        }

        private int InjectLandmarkCorridorClusterAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int perCellBudget,
            CandidateMap rescueCandidates,
            CandidateMap resourceCandidates,
            bool rebuildOccupiedBuffer,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (pattern != WorldProceduralPattern.LandmarkCorridor || rescueCandidates.count == 0)
                return 0;

            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            if (rebuildOccupiedBuffer)
                RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            return InjectClusterAccentRoleCandidates(
                pattern,
                biomeProfile,
                resourceCandidates.count > 0 ? resourceCandidates : rescueCandidates,
                perCellBudget,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                ResolveLandmarkCorridorClusterAccentTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket),
                ref clusterCount,
                structureCount,
                spawnCount,
                clusterAccentCounts,
                structureAccentCounts,
                passiveSpawnCount,
                predatorSpawnCount,
                false,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
        }

        private int InjectRuinPlacementModeCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (!ShouldRescueRuinPlacementModes(pattern) || ordered == null || ordered.Count == 0)
                return 0;

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int passiveUnused = 0;
            int predatorUnused = 0;
            CountPlacedRuinStructurePlacementModes(
                out int ruinClusterModeCount,
                out int ruinLandmarkModeCount);

            added += InjectRuinPlacementModeCandidates(
                pattern,
                biomeProfile,
                WorldPrefabFamilyProfile.PlacementMode.Cluster,
                ResolveRuinPlacementModeTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.PlacementMode.Cluster),
                stride,
                perWindowBudget,
                ordered,
                ref ruinClusterModeCount,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            added += InjectRuinPlacementModeCandidates(
                pattern,
                biomeProfile,
                WorldPrefabFamilyProfile.PlacementMode.Landmark,
                ResolveRuinPlacementModeTarget(pattern, biomeProfile, WorldPrefabFamilyProfile.PlacementMode.Landmark),
                stride,
                perWindowBudget,
                ordered,
                ref ruinLandmarkModeCount,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            return added;
        }

        private int InjectRuinPlacementModeCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.PlacementMode placementMode,
            int targetCount,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            ref int currentCount,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ref int structureCount,
            ref int spawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (ordered == null || ordered.Count == 0 || targetCount <= 0)
                return 0;

            int needed = Mathf.Max(0, targetCount - currentCount);
            if (needed <= 0)
                return 0;

            int added = InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.DomainPlacementModeFilter(
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule,
                    placementMode),
                needed,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            currentCount += added;
            return added;
        }

        private int InjectExactWindowFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile preferredFamily,
            int stride,
            int perWindowBudget,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            List<ScatterCandidate> ordered,
            int currentFamilyCount,
            int targetCount,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ref int structureCount,
            ref int spawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (preferredFamily == null || ordered == null || ordered.Count == 0 || targetCount <= 0)
                return 0;

            int needed = Mathf.Max(0, targetCount - currentFamilyCount);
            if (needed <= 0)
                return 0;

            return InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.ExactFamilyFilter(preferredFamily),
                needed,
                stride,
                perWindowBudget,
                layer,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
        }

        private int InjectStructureDomainCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ProceduralDomain domain,
            int targetCount,
            int stride,
            int perWindowBudget,
            List<ScatterCandidate> ordered,
            ref int currentCount,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ref int structureCount,
            ref int spawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (ordered == null || ordered.Count == 0 || targetCount <= 0)
                return 0;

            int needed = Mathf.Max(0, targetCount - currentCount);
            if (needed <= 0)
                return 0;

            int added = InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.DomainFilter(domain),
                needed,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            currentCount += added;
            return added;
        }

        private int InjectPatternStructureAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            int targetStructureCount,
            Dictionary<long, ScatterCandidate> rescueCandidates,
            CandidateMap naturalCandidates,
            CandidateMap techCandidates,
            CandidateMap caveCandidates,
            CandidateMap bioCandidates,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            List<ScatterCandidate> ordered = _patternStructureOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            WorldPrefabFamilyProfile.StructureAccentRole[] rolePriority = GetPatternAccentPriority(pattern);
            for (int roleIndex = 0; roleIndex < rolePriority.Length; roleIndex++)
            {
                WorldPrefabFamilyProfile.StructureAccentRole role = rolePriority[roleIndex];
                int requiredCount = ResolvePatternAccentRoleMin(pattern, biomeProfile, role);
                if (requiredCount <= 0)
                    continue;

                switch (role)
                {
                    case WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark when naturalCandidates.count > 0:
                        added += InjectStructureAccentRoleCandidates(
                            pattern,
                            biomeProfile,
                            naturalCandidates,
                            stride,
                            perWindowBudget,
                            role,
                            requiredCount,
                            ref structureCount,
                            ref spawnCount,
                            structureAccentCounts,
                            layerTopCandidates,
                            layerTopValid,
                            layerFamilyCounts,
                            layerBiomeCounts);
                        break;
                    case WorldPrefabFamilyProfile.StructureAccentRole.TechFragment when techCandidates.count > 0:
                        added += InjectStructureAccentRoleCandidates(
                            pattern,
                            biomeProfile,
                            techCandidates,
                            stride,
                            perWindowBudget,
                            role,
                            requiredCount,
                            ref structureCount,
                            ref spawnCount,
                            structureAccentCounts,
                            layerTopCandidates,
                            layerTopValid,
                            layerFamilyCounts,
                            layerBiomeCounts);
                        break;
                    case WorldPrefabFamilyProfile.StructureAccentRole.CaveRead when caveCandidates.count > 0:
                        added += InjectStructureAccentRoleCandidates(
                            pattern,
                            biomeProfile,
                            caveCandidates,
                            stride,
                            perWindowBudget,
                            role,
                            requiredCount,
                            ref structureCount,
                            ref spawnCount,
                            structureAccentCounts,
                            layerTopCandidates,
                            layerTopValid,
                            layerFamilyCounts,
                            layerBiomeCounts);
                        break;
                    case WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette when bioCandidates.count > 0:
                        added += InjectStructureAccentRoleCandidates(
                            pattern,
                            biomeProfile,
                            bioCandidates,
                            stride,
                            perWindowBudget,
                            role,
                            requiredCount,
                            ref structureCount,
                            ref spawnCount,
                            structureAccentCounts,
                            layerTopCandidates,
                            layerTopValid,
                            layerFamilyCounts,
                            layerBiomeCounts);
                        break;
                    default:
                        added += InjectStructureAccentRoleCandidates(
                            pattern,
                            biomeProfile,
                            rescueCandidates,
                            stride,
                            perWindowBudget,
                            role,
                            requiredCount,
                            ref structureCount,
                            ref spawnCount,
                            structureAccentCounts,
                            layerTopCandidates,
                            layerTopValid,
                            layerFamilyCounts,
                            layerBiomeCounts);
                        break;
                }
            }

            added = structureCount - layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int remaining = Mathf.Max(0, targetStructureCount - structureCount);
            if (remaining <= 0)
                return added;

            int passiveUnused = 0;
            int predatorUnused = 0;
            added += InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.None,
                remaining,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);

            return added;
        }

        private int InjectStructureAccentRoleCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            Dictionary<long, ScatterCandidate> rescueCandidates,
            int stride,
            int perWindowBudget,
            WorldPrefabFamilyProfile.StructureAccentRole accentRole,
            int requiredCount,
            ref int structureCount,
            ref int spawnCount,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates == null || rescueCandidates.Count == 0 || requiredCount <= 0)
                return 0;

            List<ScatterCandidate> ordered = _structureAccentOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int currentCount = GetStructureAccentCount(structureAccentCounts, accentRole);
            int needed = Mathf.Max(0, requiredCount - currentCount);
            if (needed <= 0)
                return 0;

            int added = 0;
            int passiveUnused = 0;
            int predatorUnused = 0;
            added += InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.StructureAccentFilter(accentRole),
                needed,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            return added;
        }

        private int InjectStructureAccentRoleCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            CandidateMap rescueCandidates,
            int stride,
            int perWindowBudget,
            WorldPrefabFamilyProfile.StructureAccentRole accentRole,
            int requiredCount,
            ref int structureCount,
            ref int spawnCount,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates.count == 0 || requiredCount <= 0)
                return 0;

            List<ScatterCandidate> ordered = _structureAccentOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int currentCount = GetStructureAccentCount(structureAccentCounts, accentRole);
            int needed = Mathf.Max(0, requiredCount - currentCount);
            if (needed <= 0)
                return 0;

            int added = 0;
            int passiveUnused = 0;
            int predatorUnused = 0;
            added += InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                ordered,
                ScatterRescueCandidateFilter.StructureAccentFilter(accentRole),
                needed,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure,
                structureAccentCounts,
                ref passiveUnused,
                ref predatorUnused,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            return added;
        }

        private int InjectPatternSpawnCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            int targetSpawnCount,
            Dictionary<long, ScatterCandidate> rescueCandidates,
            CandidateMap passiveCandidates,
            CandidateMap predatorCandidates,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            List<ScatterCandidate> ordered = _patternSpawnOrderedCandidates;
            List<ScatterCandidate> orderedPassive = _patternSpawnPassiveOrderedCandidates;
            List<ScatterCandidate> orderedPredator = _patternSpawnPredatorOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);
            FillOrderedCandidateBuffer(passiveCandidates, orderedPassive);
            FillOrderedCandidateBuffer(predatorCandidates, orderedPredator);

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int neededPredators = Mathf.Max(0, ResolvePatternPredatorSpawnMin(pattern, biomeProfile) - predatorSpawnCount);
            int initialPredatorCount = predatorSpawnCount;
            InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                orderedPredator,
                ScatterRescueCandidateFilter.PredatorSpawnFilter(),
                neededPredators,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            neededPredators = Mathf.Max(0, neededPredators - (predatorSpawnCount - initialPredatorCount));

            int neededPassive = Mathf.Max(0, ResolvePatternPassiveSpawnMin(pattern, biomeProfile) - passiveSpawnCount);
            int initialPassiveCount = passiveSpawnCount;
            InjectFilteredWindowCandidatesBatch(
                pattern,
                biomeProfile,
                orderedPassive,
                ScatterRescueCandidateFilter.PassiveSpawnFilter(),
                neededPassive,
                stride,
                perWindowBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                ref structureCount,
                ref spawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts);
            neededPassive = Mathf.Max(0, neededPassive - (passiveSpawnCount - initialPassiveCount));

            added = spawnCount - layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int remaining = Mathf.Max(0, targetSpawnCount - spawnCount);
            neededPredators = Mathf.Max(0, Mathf.Min(
                ResolvePatternPredatorSpawnMax(pattern, biomeProfile),
                targetSpawnCount / 3) - predatorSpawnCount);
            if (remaining > 0 && neededPredators > 0)
            {
                int acceptedPredators = InjectFilteredWindowCandidatesBatch(
                    pattern,
                    biomeProfile,
                    orderedPredator,
                    ScatterRescueCandidateFilter.PredatorSpawnFilter(),
                    Mathf.Min(remaining, neededPredators),
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                    structureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    ref structureCount,
                    ref spawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                added += acceptedPredators;
                remaining -= acceptedPredators;
            }

            if (remaining > 0)
            {
                added += InjectFilteredWindowCandidatesBatch(
                    pattern,
                    biomeProfile,
                    ordered,
                    ScatterRescueCandidateFilter.None,
                    remaining,
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                    structureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    ref structureCount,
                    ref spawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
            }

            return added;
        }

        private int ResolveMinimumClusterPlacements(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.GetMinimumPlacements(WorldPrefabFamilyProfile.ScatterLayer.Cluster) : 4;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster));
        }

        private int ResolveMinimumGroundPlacements(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.GetMinimumPlacements(WorldPrefabFamilyProfile.ScatterLayer.Ground) : 12;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Ground));
        }

        private int ResolveMinimumStructurePlacements(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? Mathf.Max(0, profile.GetMinimumPlacements(WorldPrefabFamilyProfile.ScatterLayer.Structure)) : 6;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Structure));
        }

        private int ResolveMinimumSpawnPlacements(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? Mathf.Max(0, profile.GetMinimumPlacements(WorldPrefabFamilyProfile.ScatterLayer.Spawn)) : 4;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Spawn));
        }

        private static bool UsesPatternAccentQuotas(WorldProceduralPattern pattern)
        {
            return true;
        }

        private bool HasPatternLayerGlobalBudget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int currentCount)
        {
            int targetMax = ResolvePatternLayerTargetMax(pattern, biomeProfile, layer);
            return targetMax <= 0 || currentCount < targetMax;
        }

        private static bool HasPatternLayerGlobalBudget(
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int currentCount,
            int[] layerTargetMaxBuffer)
        {
            int layerIndex = (int)layer;
            if (layerTargetMaxBuffer == null || layerIndex < 0 || layerIndex >= layerTargetMaxBuffer.Length)
                return true;

            int targetMax = layerTargetMaxBuffer[layerIndex];
            return targetMax <= 0 || currentCount < targetMax;
        }

        private void PopulatePatternQuotaCache(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile)
        {
            EnsureWorkingMemory();
            if (_memory.HasCachedPatternQuota &&
                _memory.CachedPatternQuotaPattern == pattern &&
                ReferenceEquals(_memory.CachedPatternQuotaBiomeProfile, biomeProfile))
            {
                return;
            }

            _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Ground] =
                ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Ground);
            _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster] =
                ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure] =
                ResolvePatternStructureTargetMax(pattern, biomeProfile);
            _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn] =
                ResolvePatternSpawnTargetMax(pattern, biomeProfile);

            _clusterAccentRoleMaxRatioBuffer[(int)WorldPrefabFamilyProfile.ClusterAccentRole.None] = 0f;
            for (int i = 1; i < _clusterAccentRoleMaxRatioBuffer.Length; i++)
            {
                _clusterAccentRoleMaxRatioBuffer[i] = ResolvePatternClusterAccentRoleMaxRatio(
                    pattern,
                    biomeProfile,
                    (WorldPrefabFamilyProfile.ClusterAccentRole)i);
            }

            _structureAccentRoleMaxBuffer[(int)WorldPrefabFamilyProfile.StructureAccentRole.None] = 0;
            for (int i = 1; i < _structureAccentRoleMaxBuffer.Length; i++)
            {
                _structureAccentRoleMaxBuffer[i] = ResolvePatternAccentRoleMax(
                    pattern,
                    biomeProfile,
                    (WorldPrefabFamilyProfile.StructureAccentRole)i);
            }

            _memory.CachedPatternClusterRatioStart = ResolvePatternClusterRatioStart(pattern);
            _memory.CachedPatternPassiveSpawnMin = ResolvePatternPassiveSpawnMin(pattern, biomeProfile);
            _memory.CachedPatternPredatorSpawnMax = Mathf.Max(0, ResolvePatternPredatorSpawnMax(pattern, biomeProfile));

            _memory.HasCachedPatternQuota = true;
            _memory.CachedPatternQuotaPattern = pattern;
            _memory.CachedPatternQuotaBiomeProfile = biomeProfile;
        }

        private CandidateMap GetAccentRoleCandidatePool(
            WorldPrefabFamilyProfile.StructureAccentRole role,
            CandidateMap naturalCandidates,
            CandidateMap techCandidates,
            CandidateMap caveCandidates,
            CandidateMap bioCandidates,
            CandidateMap fallbackCandidates)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => naturalCandidates.count > 0 ? naturalCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => techCandidates.count > 0 ? techCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => caveCandidates.count > 0 ? caveCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => bioCandidates.count > 0 ? bioCandidates : fallbackCandidates,
                _ => fallbackCandidates
            };
        }

        private CandidateMap GetClusterAccentCandidatePool(
            WorldPrefabFamilyProfile.ClusterAccentRole role,
            CandidateMap fertileCandidates,
            CandidateMap nestCandidates,
            CandidateMap resourceCandidates,
            CandidateMap shelterCandidates,
            CandidateMap hazardCandidates,
            CandidateMap debrisCandidates,
            CandidateMap rockCandidates,
            CandidateMap fallbackCandidates)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => fertileCandidates.count > 0 ? fertileCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => nestCandidates.count > 0 ? nestCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => resourceCandidates.count > 0 ? resourceCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => shelterCandidates.count > 0 ? shelterCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => hazardCandidates.count > 0 ? hazardCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => debrisCandidates.count > 0 ? debrisCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => rockCandidates.count > 0 ? rockCandidates : fallbackCandidates,
                _ => fallbackCandidates
            };
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole[] GetPatternAccentPriority(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => _PatternAccentPriorityFertileShallows,
                WorldProceduralPattern.ReefNavigation => _PatternAccentPriorityReefNavigation,
                WorldProceduralPattern.IndustrialService => _PatternAccentPriorityIndustrialService,
                WorldProceduralPattern.BrineToxic => _PatternAccentPriorityBrineToxic,
                WorldProceduralPattern.VolcanicPressure => _PatternAccentPriorityVolcanicPressure,
                WorldProceduralPattern.RiftHazard => _PatternAccentPriorityRiftHazard,
                WorldProceduralPattern.AbyssSparse => _PatternAccentPriorityAbyssSparse,
                WorldProceduralPattern.LandmarkCorridor => _PatternAccentPriorityLandmarkCorridor,
                _ => _PatternAccentPriorityDefault
            };
        }

        private static WorldPrefabFamilyProfile.ClusterAccentRole[] GetPatternClusterAccentPriority(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => _PatternClusterAccentPriorityFertileShallows,
                WorldProceduralPattern.ReefNavigation => _PatternClusterAccentPriorityReefNavigation,
                WorldProceduralPattern.SedimentResources => _PatternClusterAccentPrioritySedimentResources,
                WorldProceduralPattern.IndustrialService => _PatternClusterAccentPriorityIndustrialService,
                WorldProceduralPattern.BrineToxic => _PatternClusterAccentPriorityBrineToxic,
                WorldProceduralPattern.VolcanicPressure => _PatternClusterAccentPriorityVolcanicPressure,
                WorldProceduralPattern.RiftHazard => _PatternClusterAccentPriorityRiftHazard,
                WorldProceduralPattern.AbyssSparse => _PatternClusterAccentPriorityAbyssSparse,
                WorldProceduralPattern.LandmarkCorridor => _PatternClusterAccentPriorityLandmarkCorridor,
                _ => _PatternClusterAccentPriorityDefault
            };
        }

        private float ResolvePatternClusterAccentRoleMaxRatio(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            float baseValue = profile != null ? profile.GetClusterAccentMaxRatio(role) : 1f;
            return Mathf.Clamp01(
                baseValue
                + GetMatrixBiomeClusterAccentRatioDelta(biomeProfile, role)
                + GetServiceWaterClusterAccentRatioDelta(pattern, biomeProfile, role));
        }

        private int ResolvePatternClusterRatioStart(WorldProceduralPattern pattern)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            if (profile == null)
                return 8;

            return Mathf.Max(2, Mathf.CeilToInt(profile.GetTargetMin(WorldPrefabFamilyProfile.ScatterLayer.Cluster) * 0.5f));
        }

        private int ResolvePatternClusterAccentMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.GetClusterAccentMin(role) : 0;
            return Mathf.Max(
                0,
                value
                + GetMatrixBiomeClusterAccentMinDelta(biomeProfile, role)
                + GetServiceWaterClusterAccentMinDelta(pattern, biomeProfile, role));
        }

        private int ResolvePatternStructureTargetMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.structureTargetMin : 0;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Structure));
        }

        private int ResolvePatternStructureTargetMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minValue = ResolvePatternStructureTargetMin(pattern, biomeProfile);
            int value = profile != null ? Mathf.Max(profile.structureTargetMin, profile.structureTargetMax) : 0;
            return Mathf.Max(minValue, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Structure));
        }

        private int ResolvePatternSpawnTargetMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.spawnTargetMin : 0;
            return Mathf.Max(0, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Spawn));
        }

        private int ResolvePatternSpawnTargetMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minValue = ResolvePatternSpawnTargetMin(pattern, biomeProfile);
            int value = profile != null ? Mathf.Max(profile.spawnTargetMin, profile.spawnTargetMax) : 0;
            return Mathf.Max(minValue, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Spawn));
        }

        private int ResolvePatternPassiveSpawnMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.passiveSpawnMin : 0;
            return Mathf.Max(0, value + GetMatrixBiomePassiveSpawnMinDelta(biomeProfile));
        }

        private int ResolvePatternPredatorSpawnMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minValue = ResolvePatternPredatorSpawnMin(pattern, biomeProfile);
            int value = profile != null ? Mathf.Max(profile.predatorSpawnMin, profile.predatorSpawnMax) : 0;
            return Mathf.Max(minValue, value + GetMatrixBiomePredatorSpawnMaxDelta(biomeProfile));
        }

        private int ResolvePatternPredatorSpawnMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile = null)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.predatorSpawnMin : 0;
            return Mathf.Max(0, value + GetMatrixBiomePredatorSpawnMinDelta(biomeProfile));
        }

        private int ResolvePatternLayerTargetMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minimum = layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => ResolveMinimumGroundPlacements(pattern, biomeProfile),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => ResolveMinimumClusterPlacements(pattern, biomeProfile),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => ResolvePatternStructureTargetMin(pattern, biomeProfile),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => ResolvePatternSpawnTargetMin(pattern, biomeProfile),
                _ => 0
            };
            int value = profile != null ? profile.GetTargetMax(layer) : 0;
            return Mathf.Max(minimum, value + GetMatrixBiomeLayerTargetDelta(pattern, biomeProfile, layer));
        }

        private int ResolvePatternAccentRoleMin(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int value = profile != null ? profile.GetStructureAccentMin(role) : 0;
            return Mathf.Max(
                0,
                value
                + GetMatrixBiomeStructureAccentMinDelta(biomeProfile, role)
                + GetSoftWaterStructureRoleMinDelta(pattern, biomeProfile, role)
                + GetLandmarkSoftWaterStructureRoleMinDelta(pattern, biomeProfile, role)
                + GetServiceWaterStructureRoleMinDelta(pattern, biomeProfile, role));
        }

        private int ResolvePatternAccentRoleMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minValue = ResolvePatternAccentRoleMin(pattern, biomeProfile, role);
            int value = profile != null ? profile.GetStructureAccentMax(role) : 0;
            return Mathf.Max(
                minValue,
                value
                + GetMatrixBiomeStructureAccentMaxDelta(biomeProfile, role)
                + GetSoftWaterStructureRoleMaxDelta(pattern, biomeProfile, role)
                + GetLandmarkSoftWaterStructureRoleMaxDelta(pattern, biomeProfile, role)
                + GetServiceWaterStructureRoleMaxDelta(pattern, biomeProfile, role));
        }

        private static float ResolveEffectiveMinHeat(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            in WorldProceduralFieldSampler.FieldSample sample)
        {
            return ResolveEffectiveMinHeat(
                rule,
                family,
                sample,
                NeedsPreviewRescue(sample, family));
        }

        private static float ResolveEffectiveMinHeat(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            in WorldProceduralFieldSampler.FieldSample sample,
            bool needsPreviewRescue)
        {
            float value = rule != null ? Mathf.Clamp01(rule.minHeatmapValue) : 0f;
            if (AllowsTectonicSpineRockBoulderOverride(family, sample.slopeDegrees, sample.biomeFamilyFlags))
                value = Mathf.Min(value, 0.18f);

            if (!needsPreviewRescue)
                return value;

            float previewValue = family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Structure => value * 0.42f,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => value * 0.5f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster when IsPocket(family.proceduralDomain) => value * 0.68f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => value * 0.84f,
                _ => value
            };

            if (sample.resolvedPattern == WorldProceduralPattern.SedimentResources &&
                family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure &&
                family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Plant)
            {
                previewValue = Mathf.Max(previewValue, 0.72f);
            }

            previewValue *= GetPatternFallbackMinHeatScale(sample.resolvedPattern, family);
            previewValue *= GetPreferredPreviewMinHeatScale(sample.biomeProfile, family);
            return Mathf.Clamp01(previewValue);
        }

        private static float ResolveEffectiveDensityScale(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            in WorldProceduralFieldSampler.FieldSample sample)
        {
            return ResolveEffectiveDensityScale(
                rule,
                family,
                sample,
                NeedsPreviewRescue(sample, family));
        }

        private static float ResolveEffectiveDensityScale(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            in WorldProceduralFieldSampler.FieldSample sample,
            bool needsPreviewRescue)
        {
            float value = rule != null ? Mathf.Max(0.1f, rule.densityScale) : 1f;
            if (AllowsTectonicSpineRockBoulderOverride(family, sample.slopeDegrees, sample.biomeFamilyFlags))
                value *= 1.85f;

            if (!needsPreviewRescue)
                return value;

            float previewValue = family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Structure => value * 1.35f,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => value * 1.4f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster when IsPocket(family.proceduralDomain) => value * 1.18f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => value * 1.14f,
                _ => value
            };

            previewValue *= GetPatternFallbackDensityScale(sample.resolvedPattern, family);
            previewValue *= GetPreferredPreviewDensityScale(sample.biomeProfile, family);
            return Mathf.Max(0.1f, previewValue);
        }

        private static bool NeedsPreviewRescue(
            in WorldProceduralFieldSampler.FieldSample sample,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            if (sample.isPreviewOverride != 0)
                return true;

            if (sample.seafloorSource != WorldProceduralFieldSampler.SeafloorSource.FallbackSynthetic)
                return false;

            return sample.zone == null ||
                   family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                   family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn ||
                   family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster;
        }

        private static float GetPatternFallbackMinHeatScale(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.84f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.84f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.88f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.9f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.18f,
                    _ => 1f
                },
                WorldProceduralPattern.ReefNavigation => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.88f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.9f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.9f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.92f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.12f,
                    _ => 1f
                },
                WorldProceduralPattern.SedimentResources => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.56f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.84f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.72f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.88f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 0.74f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.90f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.92f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.86f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.92f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.92f,
                    _ => 1f
                },
                WorldProceduralPattern.IndustrialService => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.74f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.74f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.2f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.22f,
                    _ => 1f
                },
                WorldProceduralPattern.BrineToxic => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.84f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.80f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.90f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.94f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.32f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.26f,
                    _ => 1f
                },
                WorldProceduralPattern.VolcanicPressure => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.86f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.74f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.86f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.90f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.96f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.96f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.34f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.38f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.22f,
                    _ => 1f
                },
                WorldProceduralPattern.RiftHazard => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.92f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.26f,
                    _ => 1f
                },
                WorldProceduralPattern.AbyssSparse => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.88f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.94f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.26f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.34f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.28f,
                    _ => 1f
                },
                WorldProceduralPattern.LandmarkCorridor => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.92f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.08f,
                    _ => 1f
                },
                _ => 1f
            };
        }

        private static float GetPatternFallbackDensityScale(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.76f,
                    _ => 1f
                },
                WorldProceduralPattern.ReefNavigation => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.86f,
                    _ => 1f
                },
                WorldProceduralPattern.SedimentResources => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.62f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.34f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.62f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.26f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.18f,
                    _ => 1f
                },
                WorldProceduralPattern.IndustrialService => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.72f,
                    _ => 1f
                },
                WorldProceduralPattern.BrineToxic => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.26f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.76f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 0.90f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.44f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.58f,
                    _ => 1f
                },
                WorldProceduralPattern.VolcanicPressure => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.40f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.38f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.54f,
                    _ => 1f
                },
                WorldProceduralPattern.RiftHazard => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.72f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.66f,
                    _ => 1f
                },
                WorldProceduralPattern.AbyssSparse => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.58f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.56f,
                    _ => 1f
                },
                WorldProceduralPattern.LandmarkCorridor => family.proceduralDomain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.24f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.84f,
                    _ => 1f
                },
                _ => 1f
            };
        }

        private static void ApplyPlacement(
            WorldProceduralProxyInstance metadata,
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            bool supportsFinalVariant = ResolvePlacementSupportsFinalVariant(placement);
            Transform transform = metadata.transform;
            transform.SetPositionAndRotation(placement.ReadRuntimePosition(), placement.Rotation);
            transform.localScale = Vector3.one * placement.Scale;
            metadata.ConfigureScatter(
                placement.Family,
                placement.Rule,
                placement.Zone,
                runtimeVariant != null
                    ? runtimeVariant.variantId
                    : (placement.Family != null ? placement.Family.GeneratedVariantId : "world.family.generic.generated"),
                runtimeVariant == null || runtimeVariant.proxyOnly,
                0,
                0,
                placement.HeatmapChannel,
                placement.Heat,
                placement.FieldSource,
                placement.SeafloorHeight,
                placement.DepthMeters,
                placement.SlopeDegrees,
                placement.Curvature,
                placement.CaveProximity,
                placement.RidgeSignal,
                placement.CanyonSignal,
                placement.CompositionPotential,
                placement.CachedBiomeProfileLabel,
                placement.CachedBiomeFamilyLabel,
                placement.CachedPatternLabel,
                placement.BiomeContextLabel,
                placement.CellX,
                placement.CellZ,
                placement.Key,
                placement.StreamingLayer,
                placement.ChunkCoord,
                placement.HasMacroZone,
                placement.MacroZoneCoord,
                supportsFinalVariant,
                finalVariantActive,
                ShouldKeepScatterCollision(placement));
        }

        private static bool ShouldKeepScatterCollision(ScatterPlacement placement)
        {
            if (placement == null || placement.Family == null)
                return false;

            WorldPrefabFamilyProfile.ProceduralDomain domain = placement.Family.proceduralDomain;
            if (domain == WorldPrefabFamilyProfile.ProceduralDomain.RockArch ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.Landmark ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.RuinModule)
            {
                return true;
            }

            if (domain == WorldPrefabFamilyProfile.ProceduralDomain.Rock ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.RockCluster)
            {
                return placement.Scale >= 1.1f;
            }

            if (domain == WorldPrefabFamilyProfile.ProceduralDomain.Kelp ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.Plant ||
                domain == WorldPrefabFamilyProfile.ProceduralDomain.Coral)
            {
                return placement.Scale >= 1.15f &&
                       StableRandom01(placement.StableHash, placement.CellZ, placement.CellX) > 0.9f;
            }

            return false;
        }

        private GameObject CreateScatterInstance(
            Transform parent,
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive,
            out WorldProceduralProxyInstance metadata)
        {
            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            GameObject instance = null;
            metadata = null;
            Vector3 runtimePosition = placement.ReadRuntimePosition();
            bool poolManaged = false;
            IObjectPoolService pool = null;

            if (prefab != null)
            {
                if (TryResolveCachedObjectPool(out pool))
                {
                    instance = pool.Spawn(prefab, runtimePosition, placement.Rotation, !Application.isPlaying);
                    if (instance != null)
                    {
                        poolManaged = true;
                        instance.transform.SetParent(parent, false);
                    }
                }

                if (instance == null)
                {
                    if (Application.isPlaying)
                        return null;

                    instance = Instantiate(prefab, runtimePosition, placement.Rotation, parent);
                }
            }
            else
            {
                if (Application.isPlaying)
                    return null;

                instance = new GameObject();
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(runtimePosition, placement.Rotation);
            }

            if (!Application.isPlaying)
            {
                string layerLabel = GetStreamingLayerLabel(placement.StreamingLayer);
                string finalLabel = finalVariantActive ? "FINAL" : "PROXY";
                instance.name = $"SCATTER_{layerLabel}_{finalLabel}_{placement.Family.familyId}_{placement.CellX}_{placement.CellZ}";
            }

            if (instance != null &&
                Application.isPlaying &&
                !WorldProceduralProxyInstance.TryGetCached(instance, out metadata))
            {
                if (poolManaged && pool != null)
                    pool.Despawn(instance);
                else
                    Destroy(instance);
                return null;
            }

#if UNITY_EDITOR
            if (instance != null &&
                !Application.isPlaying &&
                !TryResolveEditorProxyMetadata(instance, out metadata))
            {
                return null;
            }
#endif

            if (metadata != null)
                metadata.SetPoolManaged(poolManaged);

            return instance;
        }

#if UNITY_EDITOR
        private static bool TryResolveEditorProxyMetadata(GameObject instance, out WorldProceduralProxyInstance metadata)
        {
            if (WorldProceduralProxyInstance.TryGetCached(instance, out metadata))
                return true;

            if (!instance.TryGetComponent(out metadata))
                metadata = instance.AddComponent<WorldProceduralProxyInstance>();

            return metadata != null;
        }
#endif

        private void DestroyProxyInstance(WorldProceduralProxyInstance proxy)
        {
            if (proxy == null)
                return;

            GameObject instance = proxy.gameObject;
            TryResolveCachedObjectPool(out IObjectPoolService pool);
            if (pool != null && proxy.IsPoolManaged)
            {
                pool.Despawn(instance);
                return;
            }

            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }

        private static Vector3 ResolvePlacementPosition(Vector3 origin, WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule, int stableHash, float size)
        {
            float clusterRadius = rule != null && rule.clusterRadiusOverrideMeters > 0f
                ? rule.clusterRadiusOverrideMeters
                : family.clusterRadiusMeters;

            float baseRadius = family.placementMode switch
            {
                WorldPrefabFamilyProfile.PlacementMode.Cluster => Mathf.Max(1.5f, clusterRadius * 0.24f),
                WorldPrefabFamilyProfile.PlacementMode.Patch => Mathf.Max(1.5f, clusterRadius * 0.32f),
                WorldPrefabFamilyProfile.PlacementMode.Landmark => Mathf.Max(0.5f, size * 0.14f),
                WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => Mathf.Max(1f, clusterRadius * 0.18f),
                _ => Mathf.Max(0.5f, size * 0.18f)
            };

            float angle = Mathf.Abs(stableHash % 360) * Mathf.Deg2Rad;
            int familyHash = GetPreferredFamilyInstanceId(family);
            float radiusT = StableRandom01(stableHash, stableHash >> 4, familyHash);
            float radius = baseRadius * math.lerp(0.18f, 1f, math.saturate(radiusT));
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
            Vector3 offset = new Vector3(cos * radius, 0f, sin * radius);
            return origin + offset;
        }

        private float ResolveScaleMultiplier(
            WorldPrefabFamilyProfile.VariantEntry variant,
            int stableHash,
            Vector3 absolutePosition,
            bool floraFamily)
        {
            Vector2 range = variant != null ? variant.uniformScaleRange : new Vector2(0.9f, 1.1f);
            float min = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            float variantScale;
            if (Mathf.Approximately(min, max))
            {
                variantScale = min;
            }
            else
            {
                variantScale = ScatterMath.ResolveDeterministicFloraScaleMultiplier(
                    min,
                    max,
                    stableHash,
                    new Unity.Mathematics.float3(absolutePosition.x, absolutePosition.y, absolutePosition.z));
            }

            if (!floraFamily)
                return variantScale;

            float floraScale = ScatterMath.ResolveDeterministicFloraSizeVariance(
                stableHash,
                new Unity.Mathematics.float3(absolutePosition.x, absolutePosition.y, absolutePosition.z));
            return variantScale * floraScale;
        }

        private static bool FamilySupportsFinalVariant(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return false;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly)
                    return true;
            }

            return false;
        }

        private static bool ResolvePlacementSupportsFinalVariant(ScatterPlacement placement)
        {
            if (placement == null)
                return false;

            return placement.SupportsFinalVariant || FamilySupportsFinalVariant(placement.Family);
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolveRuntimeVariant(
            WorldPrefabFamilyProfile family,
            int stableHash,
            bool preferFinalVariant)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return null;

            if (preferFinalVariant)
            {
                WorldPrefabFamilyProfile.VariantEntry finalVariant = ResolveVariantFiltered(family, stableHash, VariantFilterMode.FinalReady);
                if (finalVariant != null)
                    return finalVariant;
            }

            WorldPrefabFamilyProfile.VariantEntry proxyVariant = ResolveVariantFiltered(family, stableHash, VariantFilterMode.ProxyOnly);
            if (proxyVariant != null)
                return proxyVariant;

            return ResolveVariantFiltered(family, stableHash, VariantFilterMode.Any);
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolveVariantFiltered(
            WorldPrefabFamilyProfile family,
            int stableHash,
            VariantFilterMode mode)
        {
            int totalWeight = 0;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant == null)
                    continue;

                bool match = mode switch
                {
                    VariantFilterMode.FinalReady => variant.finalReady && !variant.proxyOnly,
                    VariantFilterMode.ProxyOnly => variant.proxyOnly || !variant.finalReady,
                    VariantFilterMode.CheapProxy => variant.proxyOnly && variant.IsCheapProxy,
                    _ => true
                };

                if (match)
                    totalWeight += Mathf.Max(1, variant.weight);
            }

            if (totalWeight <= 0)
                return null;

            int pick = Mathf.Abs(stableHash % totalWeight);
            int cursor = 0;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant == null)
                    continue;

                bool match = mode switch
                {
                    VariantFilterMode.FinalReady => variant.finalReady && !variant.proxyOnly,
                    VariantFilterMode.ProxyOnly => variant.proxyOnly || !variant.finalReady,
                    VariantFilterMode.CheapProxy => variant.proxyOnly && variant.IsCheapProxy,
                    _ => true
                };

                if (!match)
                    continue;

                cursor += Mathf.Max(1, variant.weight);
                if (pick < cursor)
                    return variant;
            }

            return null;
        }

        private static float GetPlacementModeBonus(WorldPrefabFamilyProfile.PlacementMode placementMode)
        {
            return placementMode switch
            {
                WorldPrefabFamilyProfile.PlacementMode.Landmark => 0.24f,
                WorldPrefabFamilyProfile.PlacementMode.Cluster => 0.12f,
                WorldPrefabFamilyProfile.PlacementMode.Patch => 0.1f,
                WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => 0.14f,
                _ => 0f
            };
        }

        private static float GetScatterLayerBonus(WorldPrefabFamilyProfile.ScatterLayer scatterLayer)
        {
            return scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => 0.02f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => 0.08f,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => 0.16f,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => 0.1f,
                _ => 0f
            };
        }

        private int ResolveScaledBudget(
            int baseBudget,
            WorldProceduralPattern pattern,
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int maxBudget)
        {
            float scale = GetCombinedBudgetScale(pattern, biomeFamily, layer);
            return ResolveScaledBudget(baseBudget, scale, maxBudget);
        }

        private static int ResolveScaledBudget(
            int baseBudget,
            float scale,
            int maxBudget)
        {
            int scaledBudget = Mathf.RoundToInt(Mathf.Max(0f, baseBudget * scale));
            if (baseBudget > 0)
                scaledBudget = Mathf.Max(1, scaledBudget);

            return Mathf.Clamp(scaledBudget, 0, Mathf.Max(1, maxBudget));
        }

        private float GetCombinedBudgetScale(
            WorldProceduralPattern pattern,
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralPatternProfile patternProfile = ResolvePatternProfile(pattern, out _);
            WorldProceduralBiomeFamilyContextProfile biomeContext = ResolveBiomeContextProfile(biomeFamily, out _);
            return GetCombinedBudgetScale(patternProfile, biomeContext, layer);
        }

        private static float GetCombinedBudgetScale(
            WorldProceduralPatternProfile patternProfile,
            WorldProceduralBiomeFamilyContextProfile biomeContext,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return GetPatternBudgetScale(patternProfile, layer) * GetBiomeContextBudgetScale(biomeContext, layer);
        }

        private float GetPatternBudgetScale(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            return GetPatternBudgetScale(profile, layer);
        }

        private static float GetPatternBudgetScale(
            WorldProceduralPatternProfile profile,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return profile != null ? profile.GetBudgetScale(layer) : 1f;
        }

        private float GetBiomeContextBudgetScale(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralBiomeFamilyContextProfile context = ResolveBiomeContextProfile(biomeFamily, out _);
            return GetBiomeContextBudgetScale(context, layer);
        }

        private static float GetBiomeContextBudgetScale(
            WorldProceduralBiomeFamilyContextProfile context,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return context != null ? context.GetBudgetScale(layer) : 1f;
        }

        private static float GetFamilyAffinityBonus(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0f;

            float bonus = 0f;
            Hecton8.Environment.HectonBiomeFamilyProfile[] preferredBiomeFamilies = family.preferredBiomeFamilies;
            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0 && fieldSample.biomeFamily != null)
            {
                bool biomeMatch = false;
                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == fieldSample.biomeFamily)
                    {
                        biomeMatch = true;
                        break;
                    }
                }

                float biomeWeight = Mathf.Clamp01(family.biomeAffinityWeight);
                bonus += biomeMatch ? biomeWeight : -biomeWeight * 0.18f;
            }

            WorldZoneAnchor.ZoneKind[] preferredZoneKinds = family.preferredZoneKinds;
            if (preferredZoneKinds != null && preferredZoneKinds.Length > 0)
            {
                WorldZoneAnchor.ZoneKind effectiveZoneKind = fieldSample.zone != null
                    ? fieldSample.zone.Kind
                    : fieldSample.resolvedZoneKind;
                bool zoneMatch = false;
                for (int i = 0; i < preferredZoneKinds.Length; i++)
                {
                    if (preferredZoneKinds[i] == effectiveZoneKind)
                    {
                        zoneMatch = true;
                        break;
                    }
                }

                float zoneWeight = Mathf.Clamp01(family.zoneAffinityWeight);
                bonus += zoneMatch ? zoneWeight : -zoneWeight * 0.18f;
            }

            return bonus;
        }

        private static float GetFamilyAffinityBonus(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            float bonus = 0f;
            Hecton8.Environment.HectonBiomeFamilyProfile[] preferredBiomeFamilies = runtimeRule.PreferredBiomeFamilies;
            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0 && fieldSample.biomeFamily != null)
            {
                bool biomeMatch = false;
                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == fieldSample.biomeFamily)
                    {
                        biomeMatch = true;
                        break;
                    }
                }

                bonus += biomeMatch
                    ? runtimeRule.BiomeAffinityWeight
                    : -runtimeRule.BiomeAffinityWeight * 0.18f;
            }

            WorldZoneAnchor.ZoneKind[] preferredZoneKinds = runtimeRule.PreferredZoneKinds;
            if (preferredZoneKinds != null && preferredZoneKinds.Length > 0)
            {
                WorldZoneAnchor.ZoneKind effectiveZoneKind = fieldSample.zone != null
                    ? fieldSample.zone.Kind
                    : fieldSample.resolvedZoneKind;
                bool zoneMatch = false;
                for (int i = 0; i < preferredZoneKinds.Length; i++)
                {
                    if (preferredZoneKinds[i] == effectiveZoneKind)
                    {
                        zoneMatch = true;
                        break;
                    }
                }

                bonus += zoneMatch
                    ? runtimeRule.ZoneAffinityWeight
                    : -runtimeRule.ZoneAffinityWeight * 0.18f;
            }

            return bonus;
        }

        private static float GetPatternAffinityBonus(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0f;

            float weight = Mathf.Clamp01(family.patternAffinityWeight);
            if (weight <= 0f)
                return 0f;

            if (fieldSample.resolvedPattern == family.primaryPattern)
                return weight;

            if (fieldSample.resolvedPattern == family.secondaryPattern)
                return weight * 0.6f;

            float mismatchScale = family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => 0.42f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => 0.36f,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => 0.48f,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => 0.44f,
                _ => 0.32f
            };
            return -weight * mismatchScale;
        }

        private static float GetPatternAffinityBonus(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            float weight = runtimeRule.PatternAffinityWeight;
            if (weight <= 0f)
                return 0f;

            if (pattern == runtimeRule.PrimaryPattern)
                return weight;

            if (pattern == runtimeRule.SecondaryPattern)
                return weight * 0.6f;

            return -weight * runtimeRule.PatternMismatchScale;
        }

        private float GetBiomeContextBonus(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile family)
        {
            WorldProceduralBiomeFamilyContextProfile context = ResolveBiomeContextProfile(biomeFamily, out _);
            return GetBiomeContextBonus(context, family);
        }

        private static float GetBiomeContextBonus(
            WorldProceduralBiomeFamilyContextProfile context,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0f;

            if (context == null)
                return 0f;

            float bonus = context.GetDomainBias(family.proceduralDomain);
            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                bonus += context.GetClusterAccentBias(GetClusterAccentRole(family));

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
                bonus += context.GetStructureAccentBias(GetStructureAccentRole(family));

            if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                bonus += context.GetSpawnBias(IsPassiveSpawnFamily(family), IsPredatorSpawnFamily(family));

            return bonus;
        }

        private static float GetBiomeContextBonus(
            WorldProceduralBiomeFamilyContextProfile context,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            if (context == null)
                return 0f;

            float bonus = context.GetDomainBias(runtimeRule.ProceduralDomain);
            if (runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                bonus += context.GetClusterAccentBias(runtimeRule.ClusterAccentRole);

            if (runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
                bonus += context.GetStructureAccentBias(runtimeRule.StructureAccentRole);

            if (runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                bonus += context.GetSpawnBias(runtimeRule.PassiveSpawnFamily, runtimeRule.PredatorSpawnFamily);

            return bonus;
        }

        private static float GetBiomeMatrixBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float survival = NormalizeMatrixBias(biomeProfile.survivalPressure);
            float pressure = GetMatrixPressureSignal(biomeProfile);

            float bonus = family.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => (resource * 0.12f) + (survival * 0.03f),
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => (survival * 0.1f) + (resource * 0.03f),
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => pressure * 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => salvage * 0.1f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => salvage * 0.08f + landmark * 0.02f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => salvage * 0.06f + landmark * 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => salvage * 0.08f + landmark * 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => landmark * 0.08f + pressure * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => landmark * 0.1f + resource * 0.02f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => resource * 0.03f + (1f - pressure) * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => resource * 0.03f + (1f - pressure) * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => resource * 0.03f + landmark * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => survival * 0.03f + resource * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => IsPredatorSpawnFamily(family)
                    ? pressure * 0.08f - (resource * 0.02f)
                    : (1f - pressure) * 0.06f + (resource * 0.02f),
                _ => 0f
            };

            bonus += GetMatrixClusterFocusScoreBonus(biomeProfile, family);
            bonus += GetMatrixStructureFocusScoreBonus(biomeProfile, family);
            bonus += GetMatrixFaunaMoodScoreBonus(biomeProfile, family);
            bonus += GetPreferredContentScoreBonus(biomeProfile, family);
            bonus += GetPatternSpecificPreferredCategoryScoreBonus(pattern, biomeProfile, family);
            bonus += GetRuinMemoryPlaceBonus(pattern, family, salvage, landmark);
            return bonus;
        }

        private static float GetBiomeMatrixBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext,
            int layerPreferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            WorldPrefabFamilyProfile family = runtimeRule.Family;
            if (biomeScoreContext.HasBiomeProfile == 0 || family == null)
                return 0f;

            float bonus = runtimeRule.ProceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => (biomeScoreContext.ResourceSignal * 0.12f) + (biomeScoreContext.SurvivalSignal * 0.03f),
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => (biomeScoreContext.SurvivalSignal * 0.1f) + (biomeScoreContext.ResourceSignal * 0.03f),
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => biomeScoreContext.PressureSignal * 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => biomeScoreContext.SalvageSignal * 0.1f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => biomeScoreContext.SalvageSignal * 0.08f + biomeScoreContext.LandmarkSignal * 0.02f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => biomeScoreContext.SalvageSignal * 0.06f + biomeScoreContext.LandmarkSignal * 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => biomeScoreContext.SalvageSignal * 0.08f + biomeScoreContext.LandmarkSignal * 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => biomeScoreContext.LandmarkSignal * 0.08f + biomeScoreContext.PressureSignal * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => biomeScoreContext.LandmarkSignal * 0.1f + biomeScoreContext.ResourceSignal * 0.02f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => biomeScoreContext.ResourceSignal * 0.03f + (1f - biomeScoreContext.PressureSignal) * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => biomeScoreContext.ResourceSignal * 0.03f + (1f - biomeScoreContext.PressureSignal) * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => biomeScoreContext.ResourceSignal * 0.03f + biomeScoreContext.LandmarkSignal * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => biomeScoreContext.SurvivalSignal * 0.03f + biomeScoreContext.ResourceSignal * 0.03f,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => runtimeRule.PredatorSpawnFamily
                    ? biomeScoreContext.PressureSignal * 0.08f - (biomeScoreContext.ResourceSignal * 0.02f)
                    : (1f - biomeScoreContext.PressureSignal) * 0.06f + (biomeScoreContext.ResourceSignal * 0.02f),
                _ => 0f
            };

            bonus += GetMatrixClusterFocusScoreBonus(biomeProfile, runtimeRule, biomeScoreContext);
            bonus += GetMatrixStructureFocusScoreBonus(biomeProfile, runtimeRule, biomeScoreContext);
            bonus += GetMatrixFaunaMoodScoreBonus(biomeProfile, runtimeRule);
            bonus += GetPreferredContentScoreBonus(runtimeRule, layerPreferredFamilyIndex);
            bonus += GetPatternSpecificPreferredCategoryScoreBonus(runtimeRule, layerPreferredFamilyIndex, patternScoreContext);
            bonus += GetRuinMemoryPlaceBonus(
                pattern,
                runtimeRule.PlacementMode,
                runtimeRule.ScatterLayer,
                runtimeRule.ProceduralDomain,
                biomeScoreContext.SalvageSignal,
                biomeScoreContext.LandmarkSignal);
            return bonus;
        }

        private static float ResolveBiomeMatrixScoreUpperBound(
            in ScatterRuntimeRuleEntry runtimeRule,
            bool hasBiomeProfile,
            int preferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (!hasBiomeProfile)
                return 0f;

            float bonus = ResolveBiomeMatrixBaseScoreUpperBound(runtimeRule);
            if (runtimeRule.ClusterAccentRole != WorldPrefabFamilyProfile.ClusterAccentRole.None)
                bonus += 0.22f;

            if (runtimeRule.StructureAccentRole != WorldPrefabFamilyProfile.StructureAccentRole.None)
                bonus += 0.28f;

            if (runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn &&
                (runtimeRule.PassiveSpawnFamily || runtimeRule.PredatorSpawnFamily))
            {
                bonus += 0.12f;
            }

            bonus += GetPreferredContentScoreBonus(runtimeRule, preferredFamilyIndex);
            bonus += GetPatternSpecificPreferredCategoryScoreBonus(runtimeRule, preferredFamilyIndex, patternScoreContext);
            bonus += ResolveRuinMemoryPlaceScoreUpperBound(patternScoreContext.Pattern, runtimeRule.PlacementMode, runtimeRule.ScatterLayer, runtimeRule.ProceduralDomain);
            return bonus;
        }

        private static float GetRuinMemoryPlaceBonus(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family,
            float salvageSignal,
            float landmarkSignal)
        {
            if (family == null)
                return 0f;

            return GetRuinMemoryPlaceBonus(
                pattern,
                family.placementMode,
                family.scatterLayer,
                family.proceduralDomain,
                salvageSignal,
                landmarkSignal);
        }

        private static float GetRuinMemoryPlaceBonus(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile.PlacementMode placementMode,
            WorldPrefabFamilyProfile.ScatterLayer scatterLayer,
            WorldPrefabFamilyProfile.ProceduralDomain domain,
            float salvageSignal,
            float landmarkSignal)
        {
            if (scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                domain != WorldPrefabFamilyProfile.ProceduralDomain.RuinModule)
            {
                return 0f;
            }

            if (pattern == WorldProceduralPattern.LandmarkCorridor)
            {
                return placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => 0.12f + (landmarkSignal * 0.12f) + (salvageSignal * 0.04f),
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => 0.06f + (landmarkSignal * 0.08f) + (salvageSignal * 0.04f),
                    WorldPrefabFamilyProfile.PlacementMode.Solitary => landmarkSignal >= 0.6f ? -0.04f : 0f,
                    _ => 0f
                };
            }

            if (pattern == WorldProceduralPattern.IndustrialService || pattern == WorldProceduralPattern.BrineToxic)
            {
                return placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => 0.06f + (salvageSignal * 0.08f) + (landmarkSignal * 0.04f),
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => 0.04f + (salvageSignal * 0.06f) + (landmarkSignal * 0.04f),
                    WorldPrefabFamilyProfile.PlacementMode.Solitary => salvageSignal >= 0.6f ? -0.02f : 0f,
                    _ => 0f
                };
            }

            return 0f;
        }

        private static float ResolveRuinMemoryPlaceScoreUpperBound(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile.PlacementMode placementMode,
            WorldPrefabFamilyProfile.ScatterLayer scatterLayer,
            WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return GetRuinMemoryPlaceBonus(pattern, placementMode, scatterLayer, domain, 1f, 1f);
        }

        private static float ResolveBiomeMatrixBaseScoreUpperBound(in ScatterRuntimeRuleEntry runtimeRule)
        {
            return runtimeRule.ProceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 0.15f,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.13f,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.11f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 0.08f,
                _ => 0f
            };
        }

        private static float GetMatrixResourceSignal(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0f;

            return NormalizeMatrixBias(
                (biomeProfile.commonResourceBias
                 + biomeProfile.uncommonResourceBias
                 + biomeProfile.rareResourceBias
                 + biomeProfile.rewardPull) * 0.25f);
        }

        private static float GetMatrixSalvageSignal(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0f;

            return NormalizeMatrixBias((biomeProfile.salvageBias + biomeProfile.nodeExtractionBias) * 0.5f);
        }

        private static float GetMatrixLandmarkSignal(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0f;

            return NormalizeMatrixBias(Mathf.Max(biomeProfile.landmarkStrength, biomeProfile.routePressure));
        }

        private static float GetMatrixPressureSignal(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0f;

            return NormalizeMatrixBias((biomeProfile.survivalPressure + biomeProfile.routePressure) * 0.5f);
        }

        private static float GetMatrixCalmLifeSignal(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0.5f;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);
            return Mathf.Clamp01((resource * 0.58f) + ((1f - pressure) * 0.42f));
        }

        private static float GetMatrixClusterFocusScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            WorldPrefabFamilyProfile.ClusterAccentRole role = GetClusterAccentRole(family);
            if (role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return 0f;

            float bonus = 0f;
            if (role == ConvertClusterFocusToAccentRole(biomeProfile.primaryClusterFocus))
                bonus += 0.14f;
            if (role == ConvertClusterFocusToAccentRole(biomeProfile.secondaryClusterFocus))
                bonus += 0.08f;

            return bonus;
        }

        private static float GetMatrixClusterFocusScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext)
        {
            if (biomeScoreContext.HasBiomeProfile == 0 || runtimeRule.ClusterAccentRole == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return 0f;

            float bonus = 0f;
            if (runtimeRule.ClusterAccentRole == biomeScoreContext.PrimaryClusterFocusRole)
                bonus += 0.14f;
            if (runtimeRule.ClusterAccentRole == biomeScoreContext.SecondaryClusterFocusRole)
                bonus += 0.08f;

            return bonus;
        }

        private static float GetMatrixStructureFocusScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            WorldPrefabFamilyProfile.StructureAccentRole role = GetStructureAccentRole(family);
            if (role == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0f;

            float bonus = 0f;
            if (role == ConvertStructureFocusToAccentRole(biomeProfile.primaryStructureFocus))
                bonus += 0.18f;
            if (role == ConvertStructureFocusToAccentRole(biomeProfile.secondaryStructureFocus))
                bonus += 0.10f;

            return bonus;
        }

        private static float GetMatrixStructureFocusScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext)
        {
            if (biomeScoreContext.HasBiomeProfile == 0 || runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0f;

            float bonus = 0f;
            if (runtimeRule.StructureAccentRole == biomeScoreContext.PrimaryStructureFocusRole)
                bonus += 0.18f;
            if (runtimeRule.StructureAccentRole == biomeScoreContext.SecondaryStructureFocusRole)
                bonus += 0.10f;

            return bonus;
        }

        private static float GetMatrixFaunaMoodScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null || family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return 0f;

            bool passive = IsPassiveSpawnFamily(family);
            bool predator = IsPredatorSpawnFamily(family);
            if (!passive && !predator)
                return 0f;

            return biomeProfile.faunaMood switch
            {
                WorldProceduralFaunaMood.Calm => passive ? 0.10f : 0f,
                WorldProceduralFaunaMood.Lively => passive ? 0.10f : 0f,
                WorldProceduralFaunaMood.Mixed => passive || predator ? 0.05f : 0f,
                WorldProceduralFaunaMood.Hostile => predator ? 0.12f : 0f,
                _ => 0f
            };
        }

        private static float GetMatrixFaunaMoodScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            if (biomeProfile == null || runtimeRule.ScatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return 0f;

            bool passive = runtimeRule.PassiveSpawnFamily;
            bool predator = runtimeRule.PredatorSpawnFamily;
            if (!passive && !predator)
                return 0f;

            return biomeProfile.faunaMood switch
            {
                WorldProceduralFaunaMood.Calm => passive ? 0.10f : 0f,
                WorldProceduralFaunaMood.Lively => passive ? 0.10f : 0f,
                WorldProceduralFaunaMood.Mixed => passive || predator ? 0.05f : 0f,
                WorldProceduralFaunaMood.Hostile => predator ? 0.12f : 0f,
                _ => 0f
            };
        }

        private static float GetPreferredContentScoreBonus(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            return family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => GetPreferredFamilyScoreBonus(biomeProfile.preferredGroundFamilies, family, 0.20f, 0.12f, 0.06f, 0f),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(biomeProfile.preferredClusterFamilies, family, 0.24f, 0.14f, 0.08f, 0.04f),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(biomeProfile.preferredStructureFamilies, family, 0.26f, 0.16f, 0.10f, 0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(biomeProfile.preferredSpawnFamilies, family, 0.20f, 0.10f, 0f, 0f),
                _ => 0f
            };
        }

        private static float GetPreferredContentScoreBonus(
            in ScatterRuntimeRuleEntry runtimeRule,
            int preferredFamilyIndex)
        {
            return runtimeRule.ScatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.20f, 0.12f, 0.06f, 0f),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.24f, 0.14f, 0.08f, 0.04f),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.26f, 0.16f, 0.10f, 0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.20f, 0.10f, 0f, 0f),
                _ => 0f
            };
        }

        private static float GetBiomeSignatureScoreBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            if (pattern != WorldProceduralPattern.IndustrialService &&
                pattern != WorldProceduralPattern.BrineToxic)
            {
                return 0f;
            }

            return family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(
                    biomeProfile.preferredStructureFamilies,
                    family,
                    0.34f,
                    0.18f,
                    0.10f,
                    0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(
                    biomeProfile.preferredClusterFamilies,
                    family,
                    0.28f,
                    0.18f,
                    0.10f,
                    0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(
                    biomeProfile.preferredSpawnFamilies,
                    family,
                    0.18f,
                    0.10f,
                    0f,
                    0f),
                _ => 0f
            };
        }

        private static float GetBiomeSignatureScoreBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return GetBiomeSignatureScoreBonus(pattern, biomeProfile, runtimeRule.Family);
        }

        private static float GetBiomeSignatureScoreBonus(
            in ScatterRuntimeRuleEntry runtimeRule,
            int preferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (patternScoreContext.IsIndustrialSignature == 0)
            {
                return 0f;
            }

            return runtimeRule.ScatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.34f, 0.18f, 0.10f, 0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.28f, 0.18f, 0.10f, 0.05f),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.18f, 0.10f, 0f, 0f),
                _ => 0f
            };
        }

        private static float GetPatternSpecificPreferredCategoryScoreBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return 0f;

            if (IsSoftWaterPattern(pattern))
            {
                return family.scatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredClusterFamilies,
                        family,
                        0.18f,
                        0.10f,
                        0.04f,
                        0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredStructureFamilies,
                        family,
                        0.20f,
                        0.12f,
                        0.06f,
                        0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredSpawnFamilies,
                        family,
                        0.12f,
                        0.06f,
                        0f,
                        0f),
                    _ => 0f
                };
            }

            if (IsServiceLikePattern(pattern))
            {
                return family.scatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredClusterFamilies,
                        family,
                        0.16f,
                        0.10f,
                        0.05f,
                        0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredStructureFamilies,
                        family,
                        0.22f,
                        0.14f,
                        0.08f,
                        0.04f),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredSpawnFamilies,
                        family,
                        0.12f,
                        0.08f,
                        0f,
                        0f),
                    _ => 0f
                };
            }

            if (pattern == WorldProceduralPattern.SedimentResources)
            {
                return family.scatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredClusterFamilies,
                        family,
                        0.12f,
                        0.07f,
                        0.03f,
                        0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(
                        biomeProfile.preferredStructureFamilies,
                        family,
                        0.14f,
                        0.08f,
                        0.04f,
                        0f),
                    _ => 0f
                };
            }

            return 0f;
        }

        private static float GetPatternSpecificPreferredCategoryScoreBonus(
            in ScatterRuntimeRuleEntry runtimeRule,
            int preferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (patternScoreContext.IsSoftWater != 0)
            {
                return runtimeRule.ScatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.18f, 0.10f, 0.04f, 0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.20f, 0.12f, 0.06f, 0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.12f, 0.06f, 0f, 0f),
                    _ => 0f
                };
            }

            if (patternScoreContext.IsServiceLike != 0)
            {
                return runtimeRule.ScatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.16f, 0.10f, 0.05f, 0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.22f, 0.14f, 0.08f, 0.04f),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.12f, 0.08f, 0f, 0f),
                    _ => 0f
                };
            }

            if (patternScoreContext.IsSedimentResources != 0)
            {
                return runtimeRule.ScatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.12f, 0.07f, 0.03f, 0f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyScoreBonus(preferredFamilyIndex, 0.14f, 0.08f, 0.04f, 0f),
                    _ => 0f
                };
            }

            return 0f;
        }

        private static float GetSoftWaterStructureFamilyBonus(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null ||
                family == null ||
                family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                !IsSoftWaterPattern(pattern))
            {
                return 0f;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 0f;

            WorldPrefabFamilyProfile.StructureAccentRole role = GetStructureAccentRole(family);
            if (role == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
            {
                int preferredIndex = GetPreferredFamilyIndex(biomeProfile.preferredStructureFamilies, family);
                return preferredIndex switch
                {
                    0 => pattern == WorldProceduralPattern.ReefNavigation ? 0.28f : 0.24f,
                    1 => pattern == WorldProceduralPattern.ReefNavigation ? 0.20f : 0.16f,
                    2 => 0.10f,
                    _ => 0.06f
                };
            }

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                return pattern == WorldProceduralPattern.ReefNavigation ? -0.16f : -0.12f;

            return 0f;
        }

        private static float GetSoftWaterStructureFamilyBonus(
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext,
            int preferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (biomeScoreContext.HasBiomeProfile == 0 ||
                runtimeRule.Family == null ||
                runtimeRule.ScatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                patternScoreContext.IsSoftWater == 0)
            {
                return 0f;
            }

            if (biomeScoreContext.PreferredStructureRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 0f;

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
            {
                return GetSoftWaterStructurePreferredBonus(patternScoreContext.Pattern, preferredFamilyIndex);
            }

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                return patternScoreContext.Pattern == WorldProceduralPattern.ReefNavigation ? -0.16f : -0.12f;

            return 0f;
        }

        private static float GetSoftWaterStructurePreferredBonus(
            WorldProceduralPattern pattern,
            int preferredFamilyIndex)
        {
            return preferredFamilyIndex switch
            {
                0 => pattern == WorldProceduralPattern.ReefNavigation ? 0.28f : 0.24f,
                1 => pattern == WorldProceduralPattern.ReefNavigation ? 0.20f : 0.16f,
                2 => 0.10f,
                _ => 0.06f
            };
        }

        private static float GetLandmarkSoftWaterStructureFamilyBonus(
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext,
            int preferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (patternScoreContext.IsLandmarkCorridor == 0 ||
                biomeScoreContext.HasBiomeProfile == 0 ||
                runtimeRule.Family == null ||
                runtimeRule.ScatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure)
            {
                return 0f;
            }

            if (biomeScoreContext.PreferredStructureRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 0f;

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
            {
                return preferredFamilyIndex switch
                {
                    0 => 0.68f,
                    1 => 0.46f,
                    2 => 0.26f,
                    _ => 0.14f
                };
            }

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                return -0.62f;

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                return -0.22f;

            if (runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark)
                return -0.16f;

            return 0f;
        }

        private static float GetLandmarkSoftWaterHeatScale(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            if (pattern != WorldProceduralPattern.LandmarkCorridor || biomeProfile == null)
                return 1f;

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 1f;

            return runtimeRule.ProceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.45f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.28f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.18f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.45f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.78f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.82f,
                _ => 1f
            };
        }

        private static float GetLandmarkSoftWaterHeatScale(
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext,
            in ScatterPatternScoreContext patternScoreContext)
        {
            if (patternScoreContext.IsLandmarkCorridor == 0 || biomeScoreContext.HasBiomeProfile == 0)
                return 1f;

            if (biomeScoreContext.PreferredStructureRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 1f;

            return runtimeRule.ProceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.45f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.28f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.18f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.45f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.78f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.82f,
                _ => 1f
            };
        }

        private static ScatterBiomeScoreContext BuildScatterBiomeScoreContext(HectonBiomeMatrixProfile biomeProfile)
        {
            return new ScatterBiomeScoreContext(biomeProfile);
        }

        private static ScatterPatternScoreContext BuildScatterPatternScoreContext(WorldProceduralPattern pattern)
        {
            return new ScatterPatternScoreContext(pattern);
        }

        private static float GetPreferredFamilyScoreBonus(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile family,
            float index0Bonus,
            float index1Bonus,
            float index2Bonus,
            float index3Bonus)
        {
            int index = GetPreferredFamilyIndex(preferredFamilies, family);
            return index switch
            {
                0 => index0Bonus,
                1 => index1Bonus,
                2 => index2Bonus,
                3 => index3Bonus,
                _ => 0f
            };
        }

        private static float GetPreferredFamilyScoreBonus(
            int preferredFamilyIndex,
            float index0Bonus,
            float index1Bonus,
            float index2Bonus,
            float index3Bonus)
        {
            return preferredFamilyIndex switch
            {
                0 => index0Bonus,
                1 => index1Bonus,
                2 => index2Bonus,
                3 => index3Bonus,
                _ => 0f
            };
        }

        private static int GetPreferredFamilyIndexForLayer(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            if (biomeProfile == null || family == null)
                return -1;

            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => GetPreferredFamilyIndex(biomeProfile.preferredGroundFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyIndex(biomeProfile.preferredClusterFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyIndex(biomeProfile.preferredStructureFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyIndex(biomeProfile.preferredSpawnFamilies, family),
                _ => -1
            };
        }

        private static int GetPreferredFamilyIndex(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile family)
        {
            if (preferredFamilies == null || family == null)
                return -1;

            int familyInstanceId = GetPreferredFamilyInstanceId(family);
            for (int i = 0; i < preferredFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferred = preferredFamilies[i];
                if (preferred == null)
                    continue;

                if (ReferenceEquals(preferred, family) || GetPreferredFamilyInstanceId(preferred) == familyInstanceId)
                    return i;
            }

            return -1;
        }

        private static bool IsSameFamily(WorldPrefabFamilyProfile a, WorldPrefabFamilyProfile b)
        {
            if (a == null || b == null)
                return false;

            return ReferenceEquals(a, b) || a.GetEntityId() == b.GetEntityId();
        }

        private void BuildPreferredFamilyPlacementCounts(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            Dictionary<int, int> counts)
        {
            counts.Clear();
            if (preferredFamilies == null || preferredFamilies.Length == 0 || _desiredPlacements.Count == 0)
                return;

            for (int i = 0; i < preferredFamilies.Length; i++)
            {
                int familyId = GetPreferredFamilyInstanceId(preferredFamilies[i]);
                if (familyId == 0 || counts.ContainsKey(familyId))
                    continue;

                counts.Add(familyId, 0);
            }

            if (counts.Count == 0)
                return;

            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                WorldPrefabFamilyProfile family = placement.Family;
                if (family == null || family.scatterLayer != layer)
                    continue;

                int familyId = GetPreferredFamilyInstanceId(family);
                if (!counts.TryGetValue(familyId, out int currentCount))
                    continue;

                counts[familyId] = currentCount + 1;
            }
        }

        private static int GetPreferredFamilyPlacementCount(
            Dictionary<int, int> counts,
            WorldPrefabFamilyProfile family)
        {
            int familyId = GetPreferredFamilyInstanceId(family);
            if (familyId == 0)
                return 0;

            return counts.TryGetValue(familyId, out int currentCount) ? currentCount : 0;
        }

        private static void IncrementPreferredFamilyPlacementCount(
            Dictionary<int, int> counts,
            WorldPrefabFamilyProfile family,
            int delta)
        {
            if (delta <= 0)
                return;

            int familyId = GetPreferredFamilyInstanceId(family);
            if (familyId == 0 || !counts.TryGetValue(familyId, out int currentCount))
                return;

            counts[familyId] = currentCount + delta;
        }

        private static int GetPreferredFamilyInstanceId(WorldPrefabFamilyProfile family)
        {
            return GetFamilyHash(family);
        }

        private void CountPlacedServiceStructureDomains(
            out int serviceScarCount,
            out int powerRouteCount,
            out int ruinModuleCount)
        {
            serviceScarCount = 0;
            powerRouteCount = 0;
            ruinModuleCount = 0;

            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                WorldPrefabFamilyProfile family = placement.Family;
                if (family == null || family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure)
                    continue;

                switch (family.proceduralDomain)
                {
                    case WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar:
                        serviceScarCount++;
                        break;
                    case WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute:
                        powerRouteCount++;
                        break;
                    case WorldPrefabFamilyProfile.ProceduralDomain.RuinModule:
                        ruinModuleCount++;
                        break;
                }
            }
        }

        private void CountPlacedRuinStructurePlacementModes(
            out int ruinClusterModeCount,
            out int ruinLandmarkModeCount)
        {
            ruinClusterModeCount = 0;
            ruinLandmarkModeCount = 0;

            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                WorldPrefabFamilyProfile family = placement.Family;
                if (family == null ||
                    family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                    family.proceduralDomain != WorldPrefabFamilyProfile.ProceduralDomain.RuinModule)
                {
                    continue;
                }

                if (family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Cluster)
                {
                    ruinClusterModeCount++;
                }
                else if (family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark)
                {
                    ruinLandmarkModeCount++;
                }
            }
        }

        private int CountPlacedStructureDomain(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            if (domain == WorldPrefabFamilyProfile.ProceduralDomain.Generic)
                return 0;

            int count = 0;
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement.Family == null ||
                    placement.Family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                    placement.Family.proceduralDomain != domain)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private int CountPlacedStructureDomainPlacementMode(
            WorldPrefabFamilyProfile.ProceduralDomain domain,
            WorldPrefabFamilyProfile.PlacementMode placementMode)
        {
            if (domain == WorldPrefabFamilyProfile.ProceduralDomain.Generic)
                return 0;

            int count = 0;
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement.Family == null ||
                    placement.Family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                    placement.Family.proceduralDomain != domain ||
                    placement.Family.placementMode != placementMode)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private int ResolvePreferredClusterFamilyTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int preferredIndex)
        {
            int total = ResolveMinimumClusterPlacements(pattern, biomeProfile);
            if (IsSoftWaterPattern(pattern))
            {
                return preferredIndex switch
                {
                    0 => Mathf.Clamp(Mathf.RoundToInt(total * 0.30f), 3, 5),
                    1 => Mathf.Clamp(Mathf.RoundToInt(total * 0.18f), 2, 3),
                    2 => total >= 10 ? 1 : 0,
                    3 => total >= 12 ? 1 : 0,
                    _ => 0
                };
            }

            if (IsServiceLikePattern(pattern))
            {
                return preferredIndex switch
                {
                    0 => Mathf.Clamp(Mathf.RoundToInt(total * 0.38f), 3, 5),
                    1 => Mathf.Clamp(Mathf.RoundToInt(total * 0.22f), 2, 3),
                    2 => total >= 7 ? 1 : 0,
                    3 => total >= 9 ? 1 : 0,
                    _ => 0
                };
            }

            if (pattern == WorldProceduralPattern.SedimentResources)
            {
                return preferredIndex switch
                {
                    0 => Mathf.Clamp(Mathf.RoundToInt(total * 0.32f), 3, 5),
                    1 => Mathf.Clamp(Mathf.RoundToInt(total * 0.18f), 2, 3),
                    2 => total >= 8 ? 1 : 0,
                    3 => total >= 11 ? 1 : 0,
                    _ => 0
                };
            }

            return preferredIndex switch
            {
                0 => Mathf.Clamp(Mathf.RoundToInt(total * 0.24f), 2, 4),
                1 => Mathf.Clamp(Mathf.RoundToInt(total * 0.14f), 1, 3),
                2 => total >= 10 ? 1 : 0,
                3 => total >= 14 ? 1 : 0,
                _ => 0
            };
        }

        private int ResolvePreferredStructureFamilyTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int preferredIndex)
        {
            int total = ResolvePatternStructureTargetMin(pattern, biomeProfile);
            if (IsSoftWaterPattern(pattern))
            {
                return preferredIndex switch
                {
                    0 => total >= 7 ? 3 : total >= 5 ? 2 : total > 0 ? 1 : 0,
                    1 => total >= 6 ? 2 : total >= 4 ? 1 : 0,
                    2 => total >= 6 ? 1 : 0,
                    _ => 0
                };
            }

            if (IsServiceLikePattern(pattern) || pattern == WorldProceduralPattern.SedimentResources)
            {
                return preferredIndex switch
                {
                    0 => total >= 9 ? 4 : total >= 7 ? 3 : total > 0 ? 2 : 0,
                    1 => total >= 8 ? 2 : total >= 5 ? 1 : 0,
                    2 => total >= 8 ? 1 : 0,
                    _ => 0
                };
            }

            return preferredIndex switch
            {
                0 => total >= 10 ? 3 : total >= 7 ? 2 : total > 0 ? 1 : 0,
                1 => total >= 9 ? 2 : total >= 6 ? 1 : 0,
                2 => total >= 9 ? 1 : 0,
                _ => 0
            };
        }

        private int ResolveServiceStructureDomainTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            if (!IsServiceLikePattern(pattern))
                return 0;

            int total = ResolvePatternStructureTargetMin(pattern, biomeProfile);
            if (total <= 0)
                return 0;

            return pattern switch
            {
                WorldProceduralPattern.IndustrialService => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => total >= 5 ? 1 : 0,
                    _ => 0
                },
                WorldProceduralPattern.BrineToxic => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => total >= 6 ? 1 : 0,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => total >= 4 ? 1 : 0,
                    _ => 0
                },
                _ => 0
            };
        }

        private int ResolvePreferredSpawnFamilyTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile preferredFamily,
            int preferredIndex)
        {
            if (preferredFamily == null)
                return 0;

            if (IsPredatorSpawnFamily(preferredFamily))
            {
                int predatorMax = ResolvePatternPredatorSpawnMax(pattern, biomeProfile);
                if (predatorMax <= 0)
                    return 0;

                return preferredIndex == 0 ? 1 : 0;
            }

            if (!IsPassiveSpawnFamily(preferredFamily))
                return 0;

            int total = ResolvePatternSpawnTargetMin(pattern, biomeProfile);
            return preferredIndex switch
            {
                0 => Mathf.Clamp(Mathf.RoundToInt(total * 0.35f), 2, 4),
                1 => total >= 4 ? 1 : 0,
                _ => 0
            };
        }

        private int ResolveServiceClusterAccentTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (!IsServiceLikePattern(pattern))
                return 0;

            int total = ResolveMinimumClusterPlacements(pattern, biomeProfile);
            if (total <= 0)
                return 0;

            return pattern switch
            {
                WorldProceduralPattern.IndustrialService => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => total >= 4 ? 1 : 0,
                    _ => 0
                },
                WorldProceduralPattern.BrineToxic => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => total >= 5 ? 1 : 0,
                    _ => 0
                },
                _ => 0
            };
        }

        private int ResolveLandmarkCorridorClusterAccentTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (pattern != WorldProceduralPattern.LandmarkCorridor)
                return 0;

            int total = ResolveMinimumClusterPlacements(pattern, biomeProfile);
            if (total <= 0)
                return 0;

            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => total >= 4 ? 1 : 0,
                _ => 0
            };
        }

        private static bool ShouldRescueRuinPlacementModes(WorldProceduralPattern pattern)
        {
            return pattern == WorldProceduralPattern.LandmarkCorridor
                || pattern == WorldProceduralPattern.IndustrialService
                || pattern == WorldProceduralPattern.BrineToxic;
        }

        private int ResolveRuinPlacementModeTarget(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.PlacementMode placementMode)
        {
            int total = ResolvePatternStructureTargetMin(pattern, biomeProfile);
            if (total <= 0 || !ShouldRescueRuinPlacementModes(pattern))
                return 0;

            return pattern switch
            {
                WorldProceduralPattern.LandmarkCorridor => placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => total >= 4 ? 1 : 0,
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => total >= 6 ? 1 : 0,
                    _ => 0
                },
                WorldProceduralPattern.IndustrialService => placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => total >= 5 ? 1 : 0,
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => total >= 8 ? 1 : 0,
                    _ => 0
                },
                WorldProceduralPattern.BrineToxic => placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => total >= 4 ? 1 : 0,
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => total >= 7 ? 1 : 0,
                    _ => 0
                },
                _ => 0
            };
        }

        private static float GetPreferredPreviewMinHeatScale(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            int preferredIndex = GetPreferredPreviewFamilyIndex(biomeProfile, family);
            if (preferredIndex < 0)
                return 1f;

            return family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => preferredIndex == 0 ? 0.52f : 0.74f,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => preferredIndex == 0 ? 0.62f : preferredIndex == 1 ? 0.76f : 0.88f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => preferredIndex == 0 ? 0.60f : preferredIndex == 1 ? 0.74f : 0.86f,
                WorldPrefabFamilyProfile.ScatterLayer.Ground => preferredIndex == 0 ? 0.9f : 0.96f,
                _ => 1f
            };
        }

        private static float GetPreferredPreviewDensityScale(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            int preferredIndex = GetPreferredPreviewFamilyIndex(biomeProfile, family);
            if (preferredIndex < 0)
                return 1f;

            return family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => preferredIndex == 0 ? 1.62f : 1.22f,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => preferredIndex == 0 ? 1.42f : preferredIndex == 1 ? 1.20f : 1.08f,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => preferredIndex == 0 ? 1.38f : preferredIndex == 1 ? 1.18f : 1.08f,
                WorldPrefabFamilyProfile.ScatterLayer.Ground => preferredIndex == 0 ? 1.08f : 1.03f,
                _ => 1f
            };
        }

        private static bool IsSoftWaterPattern(WorldProceduralPattern pattern)
        {
            return pattern == WorldProceduralPattern.FertileShallows
                || pattern == WorldProceduralPattern.ReefNavigation;
        }

        private static bool IsServiceLikePattern(WorldProceduralPattern pattern)
        {
            return pattern == WorldProceduralPattern.IndustrialService
                || pattern == WorldProceduralPattern.BrineToxic;
        }

        private static int GetPreferredPreviewFamilyIndex(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile family)
        {
            if (biomeProfile == null || family == null)
                return -1;

            return family.scatterLayer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => GetPreferredFamilyIndex(biomeProfile.preferredGroundFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => GetPreferredFamilyIndex(biomeProfile.preferredClusterFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => GetPreferredFamilyIndex(biomeProfile.preferredStructureFamilies, family),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => GetPreferredFamilyIndex(biomeProfile.preferredSpawnFamilies, family),
                _ => -1
            };
        }

        private static int GetMatrixBiomeLayerTargetDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            if (biomeProfile == null)
                return 0;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);
            float calmLife = GetMatrixCalmLifeSignal(biomeProfile);

            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => 0,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Clamp(
                    Mathf.RoundToInt(((resource - 0.5f) * 3.4f) + ((calmLife - 0.5f) * 1.2f) + ((salvage - 0.5f) * 0.8f)),
                    -2,
                    3),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Clamp(
                    Mathf.RoundToInt(((salvage - 0.5f) * 3.2f) + ((landmark - 0.5f) * 2.6f) - ((resource - 0.5f) * 1.4f)),
                    -2,
                    3),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Clamp(
                    Mathf.RoundToInt(((pressure - 0.45f) * 3.6f) + ((resource - 0.5f) * 0.8f)),
                    -1,
                    3),
                _ => 0
            };
        }

        private static int GetMatrixBiomeClusterAccentMinDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null)
                return 0;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);
            float calmLife = GetMatrixCalmLifeSignal(biomeProfile);

            int delta = role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => calmLife >= 0.66f ? 1 : pressure >= 0.78f ? -1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => resource >= 0.64f && pressure <= 0.64f ? 1 : pressure >= 0.82f ? -1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => resource >= 0.66f ? 1 : resource <= 0.34f ? -1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => pressure >= 0.62f || calmLife >= 0.72f ? 1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => pressure >= 0.66f ? 1 : pressure <= 0.28f ? -1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => salvage >= 0.64f ? 1 : salvage <= 0.28f ? -1 : 0,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => landmark >= 0.68f || pressure >= 0.66f ? 1 : 0,
                _ => 0
            };

            if (role == ConvertClusterFocusToAccentRole(biomeProfile.primaryClusterFocus))
                delta += 2;
            if (role == ConvertClusterFocusToAccentRole(biomeProfile.secondaryClusterFocus))
                delta += 1;

            delta += GetPreferredClusterAccentMinDelta(biomeProfile, role);

            return Mathf.Clamp(delta, -2, 4);
        }

        private static float GetMatrixBiomeClusterAccentRatioDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null)
                return 0f;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);
            float calmLife = GetMatrixCalmLifeSignal(biomeProfile);

            float delta = role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => calmLife >= 0.7f ? 0.12f : pressure >= 0.8f ? -0.12f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => resource >= 0.68f && pressure <= 0.62f ? 0.08f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => resource >= 0.7f ? 0.14f : resource <= 0.34f ? -0.1f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => pressure >= 0.64f || calmLife >= 0.72f ? 0.08f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => pressure >= 0.7f ? 0.14f : pressure <= 0.3f ? -0.12f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => salvage >= 0.68f ? 0.14f : salvage <= 0.32f ? -0.12f : 0f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => landmark >= 0.7f ? 0.1f : 0f,
                _ => 0f
            };

            if (role == ConvertClusterFocusToAccentRole(biomeProfile.primaryClusterFocus))
                delta += 0.12f;
            if (role == ConvertClusterFocusToAccentRole(biomeProfile.secondaryClusterFocus))
                delta += 0.06f;

            delta += GetPreferredClusterAccentRatioDelta(biomeProfile, role);

            return Mathf.Clamp(delta, -0.2f, 0.32f);
        }

        private static int GetMatrixBiomeStructureAccentMinDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null)
                return 0;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);

            int delta = role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => landmark >= 0.72f ? 1 : landmark <= 0.26f ? -1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => salvage >= 0.62f ? 1 : salvage <= 0.28f ? -1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => landmark >= 0.64f || (pressure >= 0.78f && salvage <= 0.48f) ? 1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => resource >= 0.72f && pressure <= 0.42f && salvage <= 0.5f ? 1 : pressure >= 0.6f || salvage >= 0.6f ? -1 : 0,
                _ => 0
            };

            if (role == ConvertStructureFocusToAccentRole(biomeProfile.primaryStructureFocus))
                delta += 1;

            delta += GetPreferredStructureAccentMinDelta(biomeProfile, role);

            return Mathf.Clamp(delta, -2, 4);
        }

        private static int GetMatrixBiomeStructureAccentMaxDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null)
                return 0;

            float resource = GetMatrixResourceSignal(biomeProfile);
            float salvage = GetMatrixSalvageSignal(biomeProfile);
            float landmark = GetMatrixLandmarkSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);

            int delta = role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => landmark >= 0.78f ? 1 : landmark <= 0.22f ? -1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => salvage >= 0.68f ? 1 : salvage <= 0.24f ? -1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => landmark >= 0.68f && pressure >= 0.62f ? 2 : landmark >= 0.64f || (pressure >= 0.82f && salvage <= 0.48f) ? 1 : 0,
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => resource >= 0.76f && pressure <= 0.38f && salvage <= 0.46f ? 1 : pressure >= 0.64f || salvage >= 0.64f ? -1 : 0,
                _ => 0
            };

            if (role == ConvertStructureFocusToAccentRole(biomeProfile.primaryStructureFocus))
                delta += 1;
            if (role == ConvertStructureFocusToAccentRole(biomeProfile.secondaryStructureFocus))
                delta += 1;

            delta += GetPreferredStructureAccentMaxDelta(biomeProfile, role);

            return Mathf.Clamp(delta, -2, 5);
        }

        private static int GetPreferredClusterAccentMinDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null || role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return 0;

            int delta = 0;
            int primaryIndex = GetPreferredClusterAccentIndex(biomeProfile.preferredClusterFamilies, role);
            if (primaryIndex == 0)
                delta += 2;
            else if (primaryIndex == 1)
                delta += 1;

            return delta;
        }

        private static float GetPreferredClusterAccentRatioDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null || role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return 0f;

            int index = GetPreferredClusterAccentIndex(biomeProfile.preferredClusterFamilies, role);
            return index switch
            {
                0 => 0.12f,
                1 => 0.06f,
                _ => 0f
            };
        }

        private static int GetPreferredStructureAccentMinDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null || role == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            return GetPreferredStructureAccentIndex(biomeProfile.preferredStructureFamilies, role) == 0 ? 1 : 0;
        }

        private static int GetPreferredStructureAccentMaxDelta(
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null || role == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            return GetPreferredStructureAccentIndex(biomeProfile.preferredStructureFamilies, role) switch
            {
                0 => 2,
                1 => 1,
                _ => 0
            };
        }

        private static int GetServiceWaterClusterAccentMinDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.ClusterAccentRole.None ||
                (pattern != WorldProceduralPattern.IndustrialService && pattern != WorldProceduralPattern.BrineToxic))
            {
                return 0;
            }

            return pattern switch
            {
                WorldProceduralPattern.IndustrialService => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -1,
                    _ => 0
                },
                WorldProceduralPattern.BrineToxic => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -1,
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -1,
                    _ => 0
                },
                _ => 0
            };
        }

        private static float GetServiceWaterClusterAccentRatioDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.ClusterAccentRole.None ||
                (pattern != WorldProceduralPattern.IndustrialService && pattern != WorldProceduralPattern.BrineToxic))
            {
                return 0f;
            }

            return pattern switch
            {
                WorldProceduralPattern.IndustrialService => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.10f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.08f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.10f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.08f,
                    _ => 0f
                },
                WorldProceduralPattern.BrineToxic => role switch
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.10f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.06f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.08f,
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.10f,
                    _ => 0f
                },
                _ => 0f
            };
        }

        private static int GetServiceWaterStructureRoleMinDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None ||
                (pattern != WorldProceduralPattern.IndustrialService && pattern != WorldProceduralPattern.BrineToxic))
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            if (pattern == WorldProceduralPattern.IndustrialService &&
                role == preferredRole &&
                preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
            {
                return 2;
            }

            if (role == preferredRole && preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                return 1;

            return 0;
        }

        private static int GetSoftWaterStructureRoleMinDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None ||
                !IsSoftWaterPattern(pattern))
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
            {
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                    return pattern == WorldProceduralPattern.ReefNavigation ? 2 : 1;

                if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                    return -1;
            }

            if (role == preferredRole)
                return 1;

            return 0;
        }

        private static int GetLandmarkSoftWaterStructureRoleMinDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (pattern != WorldProceduralPattern.LandmarkCorridor ||
                biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None)
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 0;

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 2;

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                return -1;

            return 0;
        }

        private static int GetSoftWaterStructureRoleMaxDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None ||
                !IsSoftWaterPattern(pattern))
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
            {
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                    return pattern == WorldProceduralPattern.ReefNavigation ? 4 : 3;

                if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                    return -2;

                if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                    return -2;
            }

            if (role == preferredRole)
                return 1;

            return 0;
        }

        private static int GetLandmarkSoftWaterStructureRoleMaxDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (pattern != WorldProceduralPattern.LandmarkCorridor ||
                biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None)
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole != WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 0;

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette)
                return 3;

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                return -2;

            if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                return -1;

            return 0;
        }

        private static int GetServiceWaterStructureRoleMaxDelta(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (biomeProfile == null ||
                role == WorldPrefabFamilyProfile.StructureAccentRole.None ||
                (pattern != WorldProceduralPattern.IndustrialService && pattern != WorldProceduralPattern.BrineToxic))
            {
                return 0;
            }

            WorldPrefabFamilyProfile.StructureAccentRole preferredRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return 0;

            if (pattern == WorldProceduralPattern.IndustrialService)
            {
                if (role == preferredRole)
                    return preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment ? 1 : 3;

                if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark)
                {
                    if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                        return -3;
                    if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                        return -1;
                }

                if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                {
                    if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                        return -3;
                    if (role == WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark)
                        return -1;
                }
            }

            if (role == preferredRole)
                return preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment ? 1 : 2;

            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark)
            {
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                    return -2;
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
                    return -1;
            }

            if (preferredRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead)
            {
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.TechFragment)
                    return -2;
                if (role == WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark)
                    return -1;
            }

            return 0;
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole GetPrimaryPreferredStructureAccentRole(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile?.preferredStructureFamilies == null || biomeProfile.preferredStructureFamilies.Length == 0)
                return WorldPrefabFamilyProfile.StructureAccentRole.None;

            return GetStructureAccentRole(biomeProfile.preferredStructureFamilies[0]);
        }

        private static int GetPreferredClusterAccentIndex(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            if (preferredFamilies == null || role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                return -1;

            for (int i = 0; i < preferredFamilies.Length; i++)
            {
                if (GetClusterAccentRole(preferredFamilies[i]) == role)
                    return i;
            }

            return -1;
        }

        private static int GetPreferredStructureAccentIndex(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            if (preferredFamilies == null || role == WorldPrefabFamilyProfile.StructureAccentRole.None)
                return -1;

            for (int i = 0; i < preferredFamilies.Length; i++)
            {
                if (GetStructureAccentRole(preferredFamilies[i]) == role)
                    return i;
            }

            return -1;
        }

        private static int GetMatrixBiomePassiveSpawnMinDelta(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0;

            float calmLife = GetMatrixCalmLifeSignal(biomeProfile);
            float pressure = GetMatrixPressureSignal(biomeProfile);
            int delta = 0;
            if (calmLife >= 0.76f)
                delta += 2;
            else if (calmLife >= 0.62f)
                delta += 1;

            if (pressure >= 0.84f)
                delta -= 1;

            delta += biomeProfile.faunaMood switch
            {
                WorldProceduralFaunaMood.Calm => 1,
                WorldProceduralFaunaMood.Lively => 2,
                WorldProceduralFaunaMood.Mixed => 1,
                WorldProceduralFaunaMood.Hostile => -1,
                _ => 0
            };

            return Mathf.Clamp(delta, -2, 4);
        }

        private static int GetMatrixBiomePredatorSpawnMinDelta(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0;

            float pressure = GetMatrixPressureSignal(biomeProfile);
            int delta = pressure >= 0.78f ? 1 : 0;
            if (biomeProfile.faunaMood == WorldProceduralFaunaMood.Hostile)
                delta += 1;

            return delta;
        }

        private static int GetMatrixBiomePredatorSpawnMaxDelta(HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0;

            float pressure = GetMatrixPressureSignal(biomeProfile);
            float calmLife = GetMatrixCalmLifeSignal(biomeProfile);
            int delta = 0;
            if (pressure >= 0.86f)
                delta += 2;
            else if (pressure >= 0.68f)
                delta += 1;

            if (calmLife >= 0.74f)
                delta -= 1;

            delta += biomeProfile.faunaMood switch
            {
                WorldProceduralFaunaMood.Calm => -1,
                WorldProceduralFaunaMood.Mixed => 1,
                WorldProceduralFaunaMood.Hostile => 1,
                _ => 0
            };

            return Mathf.Clamp(delta, -2, 4);
        }

        private static WorldPrefabFamilyProfile.ClusterAccentRole ConvertClusterFocusToAccentRole(WorldProceduralClusterFocus focus)
        {
            return focus switch
            {
                WorldProceduralClusterFocus.FertileGrowth => WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
                WorldProceduralClusterFocus.BiologicalNest => WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest,
                WorldProceduralClusterFocus.ResourcePocket => WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                WorldProceduralClusterFocus.ShelterPocket => WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                WorldProceduralClusterFocus.HazardPocket => WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                WorldProceduralClusterFocus.DebrisField => WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                WorldProceduralClusterFocus.RockCover => WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                _ => WorldPrefabFamilyProfile.ClusterAccentRole.None
            };
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole ConvertStructureFocusToAccentRole(WorldProceduralStructureFocus focus)
        {
            return focus switch
            {
                WorldProceduralStructureFocus.NaturalLandmark => WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                WorldProceduralStructureFocus.TechFragment => WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                WorldProceduralStructureFocus.CaveRead => WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                WorldProceduralStructureFocus.BiologicalSilhouette => WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
                _ => WorldPrefabFamilyProfile.StructureAccentRole.None
            };
        }

        private static float NormalizeMatrixBias(float value)
        {
            return Mathf.Clamp01(value / 5f);
        }

        private static float GetPatternContextBonus(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0f;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => GetFertilePatternBonus(family.proceduralDomain),
                WorldProceduralPattern.ReefNavigation => GetReefPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.SedimentResources => GetSedimentPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.IndustrialService => GetIndustrialPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.BrineToxic => GetBrinePatternBonus(family.proceduralDomain),
                WorldProceduralPattern.VolcanicPressure => GetVolcanicPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.RiftHazard => GetHazardPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.AbyssSparse => GetAbyssPatternBonus(family.proceduralDomain),
                WorldProceduralPattern.LandmarkCorridor => GetLandmarkPatternBonus(family.proceduralDomain),
                _ => 0f
            };
        }

        private static float GetPatternContextBonus(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => GetFertilePatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.ReefNavigation => GetReefPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.SedimentResources => GetSedimentPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.IndustrialService => GetIndustrialPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.BrineToxic => GetBrinePatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.VolcanicPressure => GetVolcanicPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.RiftHazard => GetHazardPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.AbyssSparse => GetAbyssPatternBonus(runtimeRule.ProceduralDomain),
                WorldProceduralPattern.LandmarkCorridor => GetLandmarkPatternBonus(runtimeRule.ProceduralDomain),
                _ => 0f
            };
        }

        private static float GetCombinedPatternScoreBonus(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return GetPatternAffinityBonus(pattern, runtimeRule)
                   + GetClusterAccentPatternBonus(pattern, runtimeRule)
                   + GetSpawnFamilyPatternBonus(pattern, runtimeRule)
                   + GetPatternContextBonus(pattern, runtimeRule);
        }

        private static float GetClusterAccentPatternBonus(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null || family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                return 0f;

            WorldPrefabFamilyProfile.ClusterAccentRole role = GetClusterAccentRole(family);
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => GetFertileClusterBonus(role),
                WorldProceduralPattern.ReefNavigation => GetReefClusterBonus(role),
                WorldProceduralPattern.SedimentResources => GetSedimentClusterBonus(role),
                WorldProceduralPattern.IndustrialService => GetIndustrialClusterBonus(role),
                WorldProceduralPattern.BrineToxic => GetBrineClusterBonus(role),
                WorldProceduralPattern.VolcanicPressure => GetVolcanicClusterBonus(role),
                WorldProceduralPattern.RiftHazard => GetHazardClusterBonus(role),
                WorldProceduralPattern.AbyssSparse => GetAbyssClusterBonus(role),
                WorldProceduralPattern.LandmarkCorridor => GetLandmarkClusterBonus(role),
                _ => 0f
            };
        }

        private static float GetClusterAccentPatternBonus(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            if (runtimeRule.ScatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                return 0f;

            WorldPrefabFamilyProfile.ClusterAccentRole role = runtimeRule.ClusterAccentRole;
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => GetFertileClusterBonus(role),
                WorldProceduralPattern.ReefNavigation => GetReefClusterBonus(role),
                WorldProceduralPattern.SedimentResources => GetSedimentClusterBonus(role),
                WorldProceduralPattern.IndustrialService => GetIndustrialClusterBonus(role),
                WorldProceduralPattern.BrineToxic => GetBrineClusterBonus(role),
                WorldProceduralPattern.VolcanicPressure => GetVolcanicClusterBonus(role),
                WorldProceduralPattern.RiftHazard => GetHazardClusterBonus(role),
                WorldProceduralPattern.AbyssSparse => GetAbyssClusterBonus(role),
                WorldProceduralPattern.LandmarkCorridor => GetLandmarkClusterBonus(role),
                _ => 0f
            };
        }

        private static float GetSpawnFamilyPatternBonus(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null || family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return 0f;

            bool passive = IsPassiveSpawnFamily(family);
            bool predator = IsPredatorSpawnFamily(family);
            if (!passive && !predator)
                return 0f;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => passive ? 0.12f : -0.08f,
                WorldProceduralPattern.ReefNavigation => passive ? 0.08f : -0.06f,
                WorldProceduralPattern.SedimentResources => passive ? 0.06f : -0.02f,
                WorldProceduralPattern.IndustrialService => passive ? 0.02f : 0.04f,
                WorldProceduralPattern.BrineToxic => passive ? 0.02f : 0.08f,
                WorldProceduralPattern.VolcanicPressure => passive ? 0.02f : 0.10f,
                WorldProceduralPattern.RiftHazard => passive ? 0.04f : 0.14f,
                WorldProceduralPattern.AbyssSparse => passive ? -0.02f : 0.02f,
                WorldProceduralPattern.LandmarkCorridor => passive ? 0.04f : -0.02f,
                _ => 0f
            };
        }

        private static float GetSpawnFamilyPatternBonus(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            if (runtimeRule.ScatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return 0f;

            bool passive = runtimeRule.PassiveSpawnFamily;
            bool predator = runtimeRule.PredatorSpawnFamily;
            if (!passive && !predator)
                return 0f;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => passive ? 0.12f : -0.08f,
                WorldProceduralPattern.ReefNavigation => passive ? 0.08f : -0.06f,
                WorldProceduralPattern.SedimentResources => passive ? 0.06f : -0.02f,
                WorldProceduralPattern.IndustrialService => passive ? 0.02f : 0.04f,
                WorldProceduralPattern.BrineToxic => passive ? 0.02f : 0.08f,
                WorldProceduralPattern.VolcanicPressure => passive ? 0.02f : 0.10f,
                WorldProceduralPattern.RiftHazard => passive ? 0.04f : 0.14f,
                WorldProceduralPattern.AbyssSparse => passive ? -0.02f : 0.02f,
                WorldProceduralPattern.LandmarkCorridor => passive ? 0.04f : -0.02f,
                _ => 0f
            };
        }

        private static float GetPatternHeatScale(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            WorldPrefabFamilyProfile.ProceduralDomain domain = family.proceduralDomain;
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.14f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.72f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.78f,
                    _ => 1f
                },
                WorldProceduralPattern.ReefNavigation => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.78f,
                    _ => 1f
                },
                WorldProceduralPattern.SedimentResources => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.56f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 1.30f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.34f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.04f,
                    _ => 1f
                },
                WorldProceduralPattern.IndustrialService => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.70f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.74f,
                    _ => 1f
                },
                WorldProceduralPattern.BrineToxic => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.54f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.58f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.64f,
                    _ => 1f
                },
                WorldProceduralPattern.VolcanicPressure => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.44f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.58f,
                    _ => 1f
                },
                WorldProceduralPattern.RiftHazard => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.66f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.66f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.72f,
                    _ => 1f
                },
                WorldProceduralPattern.AbyssSparse => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.58f,
                    _ => 1f
                },
                WorldProceduralPattern.LandmarkCorridor => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.88f,
                    _ => 1f
                },
                _ => 1f
            };
        }

        private static float GetPatternHeatScale(
            WorldProceduralPattern pattern,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            WorldPrefabFamilyProfile.ProceduralDomain domain = runtimeRule.ProceduralDomain;
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.14f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.72f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.78f,
                    _ => 1f
                },
                WorldProceduralPattern.ReefNavigation => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.82f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.78f,
                    _ => 1f
                },
                WorldProceduralPattern.SedimentResources => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.56f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.28f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.10f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 1.30f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.34f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.04f,
                    _ => 1f
                },
                WorldProceduralPattern.IndustrialService => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.68f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.70f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.74f,
                    _ => 1f
                },
                WorldProceduralPattern.BrineToxic => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Debris => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.54f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.58f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.64f,
                    _ => 1f
                },
                WorldProceduralPattern.VolcanicPressure => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.18f,
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.12f,
                    WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.44f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.58f,
                    _ => 1f
                },
                WorldProceduralPattern.RiftHazard => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 1.22f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 1.16f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.78f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.66f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.66f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.72f,
                    _ => 1f
                },
                WorldProceduralPattern.AbyssSparse => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.Rock => 1.08f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 1.04f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.52f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.42f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.58f,
                    _ => 1f
                },
                WorldProceduralPattern.LandmarkCorridor => domain switch
                {
                    WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 1.20f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Plant => 1.06f,
                    WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 1.02f,
                    WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.88f,
                    _ => 1f
                },
                _ => 1f
            };
        }

        private static float GetCombinedHeatScale(
            WorldProceduralPattern pattern,
            float depthMeters,
            in ScatterRuntimeRuleEntry runtimeRule,
            in ScatterBiomeScoreContext biomeScoreContext,
            in ScatterPatternScoreContext patternScoreContext)
        {
            return GetPatternHeatScale(pattern, runtimeRule)
                   * GetLandmarkSoftWaterHeatScale(runtimeRule, biomeScoreContext, patternScoreContext)
                   * GetDepthDomainScale(depthMeters, runtimeRule);
        }

        private static float GetDepthDomainScale(
            float depthMeters,
            WorldPrefabFamilyProfile family)
        {
            return ScatterHeuristicsUtility.GetDepthDomainScale(depthMeters, family);
        }

        private static float GetDepthDomainScale(
            float depthMeters,
            in ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterHeuristicsUtility.GetDepthDomainScale(depthMeters, runtimeRule);
        }

        private static float EvaluateSpawnDepthScale(float depthMeters, WorldProceduralPattern primaryPattern)
        {
            return ScatterHeuristicsUtility.EvaluateSpawnDepthScale(depthMeters, primaryPattern);
        }

        private static float EvaluateDepthBand(
            float depthMeters,
            float nearEnd,
            float midEnd,
            float deepEnd,
            float shallowScale,
            float midScale,
            float deepScale,
            float abyssScale)
        {
            return ScatterHeuristicsUtility.EvaluateDepthBand(
                depthMeters,
                nearEnd,
                midEnd,
                deepEnd,
                shallowScale,
                midScale,
                deepScale,
                abyssScale);
        }

        private static float GetFertilePatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 0.05f,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => -0.08f,
                _ => 0f
            };
        }

        private static float GetFertileClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => 0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => 0.06f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.03f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => 0.05f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => -0.02f,
                _ => 0f
            };
        }

        private static float GetReefClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => 0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => 0.06f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.03f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => 0.07f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.07f,
                _ => 0f
            };
        }

        private static float GetSedimentClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => 0.06f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => -0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.04f,
                _ => 0f
            };
        }

        private static float GetIndustrialClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => -0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.05f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.12f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.01f,
                _ => 0f
            };
        }

        private static float GetBrineClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.14f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => -0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => 0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.12f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.02f,
                _ => 0f
            };
        }

        private static float GetVolcanicClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.12f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.06f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.00f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => 0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.06f,
                _ => 0f
            };
        }

        private static float GetHazardClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => -0.06f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.08f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => 0.12f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.04f,
                _ => 0f
            };
        }

        private static float GetAbyssClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.14f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.10f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => -0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.05f,
                _ => 0f
            };
        }

        private static float GetLandmarkClusterBonus(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => 0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => -0.02f,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => -0.04f,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => 0.06f,
                _ => 0f
            };
        }

        private static float GetReefPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => -0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => -0.08f,
                _ => 0f
            };
        }

        private static float GetSedimentPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.24f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.20f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => 0.18f,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => 0.00f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.22f,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.18f,
                _ => 0f
            };
        }

        private static float GetIndustrialPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.06f,
                _ => 0f
            };
        }

        private static float GetBrinePatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => -0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.08f,
                _ => 0f
            };
        }

        private static float GetVolcanicPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.18f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.08f,
                _ => 0f
            };
        }

        private static float GetHazardPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => 0.14f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => -0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.06f,
                _ => 0f
            };
        }

        private static float GetAbyssPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => 0.06f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.10f,
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => -0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => -0.12f,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => -0.08f,
                _ => 0f
            };
        }

        private static float GetLandmarkPatternBonus(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => 0.16f,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => 0.08f,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => 0.04f,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => -0.04f,
                _ => 0f
            };
        }

        private static long ComposeKey(int cellX, int cellZ, int ruleIdHash)
        {
            unchecked
            {
                ulong packedCellX = (uint)cellX & 0xFFFFFUL;
                ulong packedCellZ = (uint)cellZ & 0xFFFFFUL;
                ulong packedRule = (uint)ruleIdHash & 0xFFFFFUL;
                return (long)(packedCellX | (packedCellZ << 20) | (packedRule << 40));
            }
        }

        private static long ComposePlacementKey(int cellX, int cellZ, int ruleIdHash, int heightLayerIndex)
        {
            ulong baseKey = (ulong)ComposeKey(cellX, cellZ, ruleIdHash);
            if (heightLayerIndex <= 0)
                return (long)baseKey;

            unchecked
            {
                return (long)(baseKey | ((ulong)(heightLayerIndex & 0xF) << 60));
            }
        }

        private static int ComputeRuleIdHash(string ruleId)
        {
            unchecked
            {
                int hash = 23;
                if (string.IsNullOrEmpty(ruleId))
                    return hash;

                for (int i = 0; i < ruleId.Length; i++)
                    hash = (hash * 31) + ruleId[i];

                return hash;
            }
        }

        private static int ComputeStableHash(int ruleIdHash, int cellX, int cellZ)
        {
            unchecked
            {
                int hash = ruleIdHash;
                hash = (hash * 31) + cellX;
                hash = (hash * 31) + cellZ;
                return hash;
            }
        }

        private static int ComputePlacementStableHash(int ruleIdHash, int cellX, int cellZ, int heightLayerIndex)
        {
            if (heightLayerIndex <= 0)
                return ComputeStableHash(ruleIdHash, cellX, cellZ);

            unchecked
            {
                int saltedRuleHash = ruleIdHash ^ (heightLayerIndex * 73856093);
                return ComputeStableHash(saltedRuleHash, cellX, cellZ);
            }
        }

        private static float StableRandom01(int cellX, int cellZ, int saltHash)
        {
            int hash = ComputeStableHash(saltHash, cellX, cellZ);
            uint normalized = (uint)(hash & 0x7fffffff);
            return normalized / (float)int.MaxValue;
        }

        private static float StablePlacementRandom01(int cellX, int cellZ, int saltHash, int heightLayerIndex)
        {
            if (heightLayerIndex <= 0)
                return StableRandom01(cellX, cellZ, saltHash);

            unchecked
            {
                return StableRandom01(cellX, cellZ, saltHash ^ (heightLayerIndex * 19349663));
            }
        }

        private GameObject GetOrCreateRoot()
        {
            GameObject root = GetScatterRoot(true);
            return root;
        }

        private GameObject GetScatterRoot(bool createIfMissing)
        {
            if (_scatterRootTransform != null)
                return _scatterRootTransform.gameObject;

            GameObject root = FindExistingScatterRoot();
            if (root == null)
            {
                if (!createIfMissing)
                    return null;

                root = new GameObject(ScatterRootName);
            }

            _scatterRootTransform = root.transform;

            return root;
        }

        private GameObject FindExistingScatterRoot()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                _sceneRootScratch.Clear();
                scene.GetRootGameObjects(_sceneRootScratch);

                int rootCount = _sceneRootScratch.Count;
                for (int rootIndex = 0; rootIndex < rootCount; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    if (root != null && string.Equals(root.name, ScatterRootName, StringComparison.Ordinal))
                        return root;
                }
            }

            return null;
        }

        private void ClearRootChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                ClearScatterHierarchy(root.GetChild(i));
            }
        }

        private void ClearScatterHierarchy(Transform node)
        {
            if (node == null)
                return;

            for (int i = node.childCount - 1; i >= 0; i--)
                ClearScatterHierarchy(node.GetChild(i));

            if (Application.isPlaying)
                Destroy(node.gameObject);
            else
                DestroyImmediate(node.gameObject);
        }

        private WorldProceduralPatternProfile ResolvePatternProfile(WorldProceduralPattern pattern, out bool usedFallbackPatternProfile)
        {
            usedFallbackPatternProfile = false;
            int index = (int)pattern;
            if (index >= 0 && index < _patternProfileCache.Length)
            {
                WorldProceduralPatternProfile cached = _patternProfileCache[index];
                if (cached != null) return cached;
            }

            if (patternCatalog != null)
            {
                WorldProceduralPatternProfile profile = patternCatalog.GetProfile(pattern, out usedFallbackPatternProfile);
                if (profile != null)
                {
                    if (index >= 0 && index < _patternProfileCache.Length)
                        _patternProfileCache[index] = profile;
                    return profile;
                }
            }

            usedFallbackPatternProfile = true;
            return GetEmergencyPatternProfile();
        }

        private WorldProceduralBiomeFamilyContextProfile ResolveBiomeContextProfile(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            out bool usedFallbackBiomeContextProfile)
        {
            usedFallbackBiomeContextProfile = false;
            if (biomeFamily == null) return null;

            if (_biomeContextCache.TryGetValue(biomeFamily, out WorldProceduralBiomeFamilyContextProfile cached))
            {
                if (cached == null) usedFallbackBiomeContextProfile = true;
                return cached;
            }

            if (biomeContextCatalog != null)
            {
                WorldProceduralBiomeFamilyContextProfile profile = biomeContextCatalog.GetProfile(biomeFamily, out usedFallbackBiomeContextProfile);
                if (profile != null)
                {
                    _biomeContextCache[biomeFamily] = profile;
                    return profile;
                }
            }

            usedFallbackBiomeContextProfile = true;
            _biomeContextCache[biomeFamily] = null;
            return null;
        }

        private static WorldProceduralPatternProfile GetEmergencyPatternProfile()
        {
            if (_emergencyPatternProfile != null)
                return _emergencyPatternProfile;

            _emergencyPatternProfile = ScriptableObject.CreateInstance<WorldProceduralPatternProfile>();
            _emergencyPatternProfile.hideFlags = HideFlags.HideAndDontSave;
            _emergencyPatternProfile.pattern = WorldProceduralPattern.SedimentResources;
            _emergencyPatternProfile.label = "Emergency Fallback";
            _emergencyPatternProfile.summary = "Runtime fallback profile used when the authored pattern catalog is missing.";
            _emergencyPatternProfile.groundBudgetScale = 1f;
            _emergencyPatternProfile.clusterBudgetScale = 1f;
            _emergencyPatternProfile.structureBudgetScale = 1f;
            _emergencyPatternProfile.spawnBudgetScale = 1f;
            _emergencyPatternProfile.minGroundPlacements = 12;
            _emergencyPatternProfile.groundTargetMax = 16;
            _emergencyPatternProfile.minClusterPlacements = 4;
            _emergencyPatternProfile.clusterTargetMax = 6;
            _emergencyPatternProfile.minStructurePlacements = 4;
            _emergencyPatternProfile.minSpawnPlacements = 4;
            _emergencyPatternProfile.structureTargetMin = 4;
            _emergencyPatternProfile.structureTargetMax = 6;
            _emergencyPatternProfile.naturalLandmarkMin = 1;
            _emergencyPatternProfile.naturalLandmarkMax = 2;
            _emergencyPatternProfile.techFragmentMin = 1;
            _emergencyPatternProfile.techFragmentMax = 2;
            _emergencyPatternProfile.caveReadMin = 1;
            _emergencyPatternProfile.caveReadMax = 2;
            _emergencyPatternProfile.biologicalSilhouetteMin = 0;
            _emergencyPatternProfile.biologicalSilhouetteMax = 1;
            _emergencyPatternProfile.resourcePocketMin = 1;
            _emergencyPatternProfile.shelterPocketMin = 1;
            _emergencyPatternProfile.rockCoverMin = 1;
            _emergencyPatternProfile.resourcePocketMaxRatio = 0.4f;
            _emergencyPatternProfile.shelterPocketMaxRatio = 0.25f;
            _emergencyPatternProfile.rockCoverMaxRatio = 0.35f;
            _emergencyPatternProfile.spawnTargetMin = 4;
            _emergencyPatternProfile.spawnTargetMax = 5;
            _emergencyPatternProfile.passiveSpawnMin = 3;
            _emergencyPatternProfile.predatorSpawnMin = 0;
            _emergencyPatternProfile.predatorSpawnMax = 1;
            return _emergencyPatternProfile;
        }

        private void ResolveReferences()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerTransform == null && playerContext != null)
                playerTransform = playerContext.PlayerTransform;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler(ref fieldSampler);
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralFillDirector(ref proceduralFillDirector);
            WorldRuntimeReferenceUtility.TryResolveWorldFaunaSpawnRegistry(ref faunaSpawnRegistry);
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralStateRegistry(ref proceduralStateRegistry);
            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyService(ref generativeGeologyService);
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref environmentalVegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            HectonRockManager rockManager = HectonRockManager.Instance;
            if (floraGpuiManager == null &&
                rockManager != null &&
                rockManager.GpuInstancerManager != null)
            {
                floraGpuiManager = rockManager.GpuInstancerManager;
            }
            ApplyVendorGpuiManagerAdmission();

            if (faunaSpawnRegistry != null)
                faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void ApplyVendorGpuiManagerAdmission()
        {
            if (floraGpuiManager == null)
                return;

            if (!SystemInfo.supportsComputeShaders)
                floraGpuiManager.enabled = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
                    if (previousContext != null && ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                        playerTransform = null;

                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (_cachedPlayerContext != null && _cachedPlayerContext.PlayerTransform != null)
                        playerTransform = _cachedPlayerContext.PlayerTransform;

                    InvalidateObserverAbsolutePositionCache();
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    OnMigratorySargassumDataVaultReplaced(currentService as Hecton8.Core.Memory.IDataVault);
                    break;
            }
        }

        private void CachePlayerContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            CacheObjectPoolService(null);
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _cachedObjectPool = candidate;
                return;
            }

            ObjectPoolManager pool = null;
            _cachedObjectPool = ObjectPoolManager.TryResolveActiveRuntime(ref pool)
                ? pool
                : null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = null;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ResetFloraGpuiAggregation()
        {
            _debugFloraGpuiFrustumRejected = 0;
            _floraGpuiHasLastFrustumChunk = false;
            RefreshFloraGpuiFrustumPlanes();
            if (_instancingService == null)
            {
                _activeGpuiFloraPlacements = 0;
                return;
            }

            _instancingService.ResetAggregation(
                _floraGpuiKnownPrototypes,
                _floraGpuiCounts,
                ref _activeGpuiFloraPlacements);
        }

        private static int ResolveFloraGpuiPrewarmCapacity()
        {
            float rawQuality = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.select(rawQuality, 1f, !math.isfinite(rawQuality)));
            float curve = quality * quality * (3f - 2f * quality);
            return Mathf.NextPowerOfTwo((int)math.round(math.lerp(64f, 512f, curve)));
        }

        private bool TryRegisterFloraGpuiPlacement(
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            prototype = null;
            if (_instancingService == null)
                return false;

            if (!_instancingService.CanUseFloraGpuiPath(
                    floraGpuiManager,
                    placement,
                    runtimeVariant,
                    out prototype))
            {
                return false;
            }

            if (ShouldCullFloraGpuiPlacement(placement))
            {
                _debugFloraGpuiFrustumRejected++;
                return true;
            }

            return _instancingService.TryRegisterPlacement(
                floraGpuiManager,
                placement,
                runtimeVariant,
                _floraGpuiKnownPrototypes,
                _floraGpuiMatrices,
                _floraGpuiCounts,
                _floraGpuiBufferCapacities,
                ref _activeGpuiFloraPlacements,
                out prototype);
        }

        private void RefreshFloraGpuiFrustumPlanes()
        {
            _floraGpuiFrustumPlanesValid = false;
            if (!enableFloraGpuiCpuFrustumCulling || !Application.isPlaying)
                return;

            Camera cullingCamera = ResolveFloraGpuiCullingCamera();
            if (cullingCamera == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(cullingCamera, _floraGpuiFrustumPlanes);
            _floraGpuiFrustumPlanesValid = true;
        }

        private Camera ResolveFloraGpuiCullingCamera()
        {
            IPlayerRuntimeContext player = _cachedPlayerContext;
            if (player != null && player.PlayerCamera != null)
                return player.PlayerCamera;

            return GlobalRenderContext.CurrentCamera;
        }

        private bool ShouldCullFloraGpuiPlacement(ScatterPlacement placement)
        {
            if (!enableFloraGpuiCpuFrustumCulling || placement == null)
                return false;

            if (!_floraGpuiFrustumPlanesValid)
                RefreshFloraGpuiFrustumPlanes();

            if (!_floraGpuiFrustumPlanesValid)
                return false;

            WorldChunkCoordinate chunkCoord = placement.ChunkCoord;
            if (_floraGpuiHasLastFrustumChunk &&
                _floraGpuiLastFrustumChunk.x == chunkCoord.x &&
                _floraGpuiLastFrustumChunk.z == chunkCoord.z)
            {
                return !_floraGpuiLastFrustumChunkVisible;
            }

            float chunkSize = math.max(1f, _runtimeStreamingState.ChunkSize);
            float padding = math.max(0f, floraGpuiFrustumPaddingMeters);
            Vector3 absoluteChunkCenter = WorldChunkCoordinate.ToWorldCenter(chunkCoord, chunkSize, placement.Position.y);
            Vector3 runtimeChunkCenter = ToRuntimeScatterPosition(absoluteChunkCenter);
            float horizontalSize = chunkSize + padding + padding;
            float verticalSize = math.max(96f, chunkSize * 0.75f) + padding + padding;
            Bounds chunkBounds = new Bounds(
                runtimeChunkCenter,
                new Vector3(horizontalSize, verticalSize, horizontalSize));

            _floraGpuiLastFrustumChunk = chunkCoord;
            _floraGpuiLastFrustumChunkVisible = GeometryUtility.TestPlanesAABB(_floraGpuiFrustumPlanes, chunkBounds);
            _floraGpuiHasLastFrustumChunk = true;
            return !_floraGpuiLastFrustumChunkVisible;
        }

        private void FlushFloraGpuiBuffers()
        {
            if (_instancingService == null)
                return;

            _instancingService.FlushBuffers(
                floraGpuiManager,
                _floraGpuiKnownPrototypes,
                _floraGpuiMatrices,
                _floraGpuiCounts,
                _floraGpuiBufferCapacities,
                _floraGpuiInitializedPrototypes);
        }

        private void ClearFloraGpuiVisibility()
        {
            EnsureWorkingMemory();
            _instancingService.ClearVisibility(
                floraGpuiManager,
                _floraGpuiKnownPrototypes,
                _floraGpuiInitializedPrototypes,
                ref _activeGpuiFloraPlacements);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            InvalidateObserverAbsolutePositionCache();
            RebuildFloraGpuiMatricesForCommittedOrigin();
        }

        private void RegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void RebuildFloraGpuiMatricesForCommittedOrigin()
        {
            if (_desiredPlacements == null || _desiredPlacements.Count <= 0)
                return;

            ResetFloraGpuiAggregation();
            Dictionary<long, ScatterPlacement>.Enumerator enumerator = _desiredPlacements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ScatterPlacement placement = enumerator.Current.Value;
                if (placement == null)
                    continue;

                WorldPrefabFamilyProfile.VariantEntry variant = placement.CachedReconcileVariant;
                if (variant == null)
                    variant = placement.CachedResolvedVariant;
                if (variant == null)
                    variant = placement.Variant;

                _instancingService.PrewarmVariantPrototypeCacheCold(variant);
                _instancingService.PrewarmVariantAggregationStorageCold(
                    variant,
                    _floraGpuiKnownPrototypes,
                    _floraGpuiMatrices,
                    _floraGpuiCounts,
                    _floraGpuiBufferCapacities,
                    ResolveFloraGpuiPrewarmCapacity());
                TryRegisterFloraGpuiPlacement(placement, variant, out _);
            }

            FlushFloraGpuiBuffers();
        }

        private bool ShouldUseFloraGpuiPath(
            ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            out GPUInstancerPrefabPrototype prototype)
        {
            prototype = null;
            if (_instancingService == null)
                return false;

            return _instancingService.CanUseFloraGpuiPath(
                floraGpuiManager,
                placement,
                runtimeVariant,
                out prototype);
        }

        private void RegisterProceduralStateRegistryCallbacks()
        {
            if (proceduralStateRegistry == null)
                return;

            proceduralStateRegistry.PlacementStateChanged -= HandleProceduralPlacementStateChanged;
            proceduralStateRegistry.PlacementStateChanged += HandleProceduralPlacementStateChanged;
        }

        private void UnregisterProceduralStateRegistryCallbacks()
        {
            if (proceduralStateRegistry == null)
                return;

            proceduralStateRegistry.PlacementStateChanged -= HandleProceduralPlacementStateChanged;
        }

        private void HandleProceduralPlacementStateChanged()
        {
            InvalidateScatterRefreshSample("placement-state");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RefreshRuntimeStreamingSettings();
        }
#endif

        private readonly struct ScatterBiomeScoreContext
        {
            public ScatterBiomeScoreContext(HectonBiomeMatrixProfile biomeProfile)
            {
                HasBiomeProfile = biomeProfile != null ? (byte)1 : (byte)0;
                ResourceSignal = GetMatrixResourceSignal(biomeProfile);
                SalvageSignal = GetMatrixSalvageSignal(biomeProfile);
                LandmarkSignal = GetMatrixLandmarkSignal(biomeProfile);
                SurvivalSignal = biomeProfile != null ? NormalizeMatrixBias(biomeProfile.survivalPressure) : 0f;
                PressureSignal = GetMatrixPressureSignal(biomeProfile);
                PrimaryClusterFocusRole = biomeProfile != null
                    ? ConvertClusterFocusToAccentRole(biomeProfile.primaryClusterFocus)
                    : WorldPrefabFamilyProfile.ClusterAccentRole.None;
                SecondaryClusterFocusRole = biomeProfile != null
                    ? ConvertClusterFocusToAccentRole(biomeProfile.secondaryClusterFocus)
                    : WorldPrefabFamilyProfile.ClusterAccentRole.None;
                PrimaryStructureFocusRole = biomeProfile != null
                    ? ConvertStructureFocusToAccentRole(biomeProfile.primaryStructureFocus)
                    : WorldPrefabFamilyProfile.StructureAccentRole.None;
                SecondaryStructureFocusRole = biomeProfile != null
                    ? ConvertStructureFocusToAccentRole(biomeProfile.secondaryStructureFocus)
                    : WorldPrefabFamilyProfile.StructureAccentRole.None;
                PreferredStructureRole = GetPrimaryPreferredStructureAccentRole(biomeProfile);
            }

            public readonly byte HasBiomeProfile;
            public readonly float ResourceSignal;
            public readonly float SalvageSignal;
            public readonly float LandmarkSignal;
            public readonly float SurvivalSignal;
            public readonly float PressureSignal;
            public readonly WorldPrefabFamilyProfile.ClusterAccentRole PrimaryClusterFocusRole;
            public readonly WorldPrefabFamilyProfile.ClusterAccentRole SecondaryClusterFocusRole;
            public readonly WorldPrefabFamilyProfile.StructureAccentRole PrimaryStructureFocusRole;
            public readonly WorldPrefabFamilyProfile.StructureAccentRole SecondaryStructureFocusRole;
            public readonly WorldPrefabFamilyProfile.StructureAccentRole PreferredStructureRole;
        }

        private readonly struct ScatterPatternScoreContext
        {
            public ScatterPatternScoreContext(WorldProceduralPattern pattern)
            {
                Pattern = pattern;
                IsSoftWater = IsSoftWaterPattern(pattern) ? (byte)1 : (byte)0;
                IsServiceLike = IsServiceLikePattern(pattern) ? (byte)1 : (byte)0;
                IsLandmarkCorridor = pattern == WorldProceduralPattern.LandmarkCorridor ? (byte)1 : (byte)0;
                IsIndustrialSignature = pattern == WorldProceduralPattern.IndustrialService || pattern == WorldProceduralPattern.BrineToxic ? (byte)1 : (byte)0;
                IsSedimentResources = pattern == WorldProceduralPattern.SedimentResources ? (byte)1 : (byte)0;
            }

            public readonly WorldProceduralPattern Pattern;
            public readonly byte IsSoftWater;
            public readonly byte IsServiceLike;
            public readonly byte IsLandmarkCorridor;
            public readonly byte IsIndustrialSignature;
            public readonly byte IsSedimentResources;
        }

        private struct GeologyBonusCache
        {
            public bool TryGet(WorldGenerativeGeologyProfile profile, out float bonus)
            {
                if (ReferenceEquals(profile, _profileA))
                {
                    bonus = _bonusA;
                    return true;
                }

                if (ReferenceEquals(profile, _profileB))
                {
                    bonus = _bonusB;
                    return true;
                }

                if (ReferenceEquals(profile, _profileC))
                {
                    bonus = _bonusC;
                    return true;
                }

                if (ReferenceEquals(profile, _profileD))
                {
                    bonus = _bonusD;
                    return true;
                }

                bonus = 0f;
                return false;
            }

            public void Store(WorldGenerativeGeologyProfile profile, float bonus)
            {
                _profileD = _profileC;
                _bonusD = _bonusC;
                _profileC = _profileB;
                _bonusC = _bonusB;
                _profileB = _profileA;
                _bonusB = _bonusA;
                _profileA = profile;
                _bonusA = bonus;
            }

            private WorldGenerativeGeologyProfile _profileA;
            private float _bonusA;
            private WorldGenerativeGeologyProfile _profileB;
            private float _bonusB;
            private WorldGenerativeGeologyProfile _profileC;
            private float _bonusC;
            private WorldGenerativeGeologyProfile _profileD;
            private float _bonusD;
        }

        internal readonly struct ScatterRuntimeRuleEntry
        {
            public ScatterRuntimeRuleEntry(
                WorldProceduralPlacementRule rule,
                WorldPrefabFamilyProfile family,
                WorldPrefabFamilyProfile.PlacementMode placementMode,
                WorldPrefabFamilyProfile.ScatterLayer scatterLayer,
                WorldPrefabFamilyProfile.ProceduralDomain proceduralDomain,
                WorldContentSocket.ContentKind scatterKind,
                int ruleIdHash,
                string heatmapChannel,
                int heatmapChannelIndex,
                float scoreBaseBonus,
                WorldStreamingLayer streamingLayer,
                WorldGenerativeGeologyProfile geologyProfile,
                bool hasMacroZone,
                bool supportsFinalVariant,
                WorldPrefabFamilyProfile.ClusterAccentRole clusterAccentRole,
                WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole,
                bool passiveSpawnFamily,
                bool predatorSpawnFamily,
                WorldProceduralPattern primaryPattern,
                WorldProceduralPattern secondaryPattern,
                float biomeAffinityWeight,
                float zoneAffinityWeight,
                float acceptedFamilyAffinityBonus,
                float patternAffinityWeight,
                float patternMismatchScale,
                float geologyScoreScale,
                float densityScaleFactor,
                float minDepthMeters,
                float maxDepthMeters,
                float minSlopeDegrees,
                float maxSlopeDegrees,
                WorldProceduralPlacementRule.FloraSubstrateMask requiredSubstrate,
                float maxTiltAngleDegrees,
                float clusterNoiseScale,
                float clusterNoiseThreshold,
                bool strictEnvelopeMapping)
            {
                Rule = rule;
                Family = family;
                PlacementMode = placementMode;
                ScatterLayer = scatterLayer;
                ProceduralDomain = proceduralDomain;
                ScatterKind = scatterKind;
                RuleIdHash = ruleIdHash;
                HeatmapChannel = heatmapChannel;
                HeatmapChannelIndex = heatmapChannelIndex;
                ScoreBaseBonus = scoreBaseBonus;
                StreamingLayer = streamingLayer;
                GeologyProfile = geologyProfile;
                HasMacroZone = hasMacroZone;
                SupportsFinalVariant = supportsFinalVariant;
                ClusterAccentRole = clusterAccentRole;
                StructureAccentRole = structureAccentRole;
                PassiveSpawnFamily = passiveSpawnFamily;
                PredatorSpawnFamily = predatorSpawnFamily;
                PrimaryPattern = primaryPattern;
                SecondaryPattern = secondaryPattern;
                BiomeAffinityWeight = biomeAffinityWeight;
                ZoneAffinityWeight = zoneAffinityWeight;
                AcceptedFamilyAffinityBonus = acceptedFamilyAffinityBonus;
                PatternAffinityWeight = patternAffinityWeight;
                PatternMismatchScale = patternMismatchScale;
                GeologyScoreScale = geologyScoreScale;
                DensityScaleFactor = densityScaleFactor;
                MinDepthMeters = minDepthMeters;
                MaxDepthMeters = maxDepthMeters;
                MinSlopeDegrees = minSlopeDegrees;
                MaxSlopeDegrees = maxSlopeDegrees;
                RequiredSubstrate = requiredSubstrate;
                MaxTiltAngleDegrees = maxTiltAngleDegrees;
                ClusterNoiseScale = clusterNoiseScale;
                ClusterNoiseThreshold = clusterNoiseThreshold;
                StrictEnvelopeMapping = strictEnvelopeMapping;
                PreferredBiomeFamilies = rule != null ? rule.preferredBiomeFamilies : null;
                PreferredZoneKinds = rule != null ? rule.preferredZoneKinds : null;
                PreferredSocketKinds = rule != null ? rule.preferredSocketKinds : null;
            }

            public readonly WorldProceduralPlacementRule Rule;
            public readonly WorldPrefabFamilyProfile Family;
            public readonly WorldPrefabFamilyProfile.PlacementMode PlacementMode;
            public readonly WorldPrefabFamilyProfile.ScatterLayer ScatterLayer;
            public readonly WorldPrefabFamilyProfile.ProceduralDomain ProceduralDomain;
            public readonly WorldContentSocket.ContentKind ScatterKind;
            public readonly int RuleIdHash;
            public readonly string HeatmapChannel;
            public readonly int HeatmapChannelIndex;
            public readonly float ScoreBaseBonus;
            public readonly WorldStreamingLayer StreamingLayer;
            public readonly WorldGenerativeGeologyProfile GeologyProfile;
            public readonly bool HasMacroZone;
            public readonly bool SupportsFinalVariant;
            public readonly WorldPrefabFamilyProfile.ClusterAccentRole ClusterAccentRole;
            public readonly WorldPrefabFamilyProfile.StructureAccentRole StructureAccentRole;
            public readonly bool PassiveSpawnFamily;
            public readonly bool PredatorSpawnFamily;
            public readonly WorldProceduralPattern PrimaryPattern;
            public readonly WorldProceduralPattern SecondaryPattern;
            public readonly float BiomeAffinityWeight;
            public readonly float ZoneAffinityWeight;
            public readonly float AcceptedFamilyAffinityBonus;
            public readonly float PatternAffinityWeight;
            public readonly float PatternMismatchScale;
            public readonly float GeologyScoreScale;
            public readonly float DensityScaleFactor;
            public readonly float MinDepthMeters;
            public readonly float MaxDepthMeters;
            public readonly float MinSlopeDegrees;
            public readonly float MaxSlopeDegrees;
            public readonly WorldProceduralPlacementRule.FloraSubstrateMask RequiredSubstrate;
            public readonly float MaxTiltAngleDegrees;
            public readonly float ClusterNoiseScale;
            public readonly float ClusterNoiseThreshold;
            public readonly bool StrictEnvelopeMapping;
            public readonly HectonBiomeFamilyProfile[] PreferredBiomeFamilies;
            public readonly WorldZoneAnchor.ZoneKind[] PreferredZoneKinds;
            public readonly WorldContentSocket.ContentKind[] PreferredSocketKinds;
        }

        private readonly struct ScatterCandidatePreview
        {
            public ScatterCandidatePreview(
                int stableHash,
                Vector3 position,
                int heightLayerIndex,
                int cellX,
                int cellZ)
            {
                StableHash = stableHash;
                Position = position;
                HeightLayerIndex = heightLayerIndex;
                CellX = cellX;
                CellZ = cellZ;
            }

            public readonly int StableHash;
            public readonly Vector3 Position;
            public readonly int HeightLayerIndex;
            public readonly int CellX;
            public readonly int CellZ;
        }

        /// <summary>
        /// Lightweight editor-only preview payload describing one accepted scatter placement envelope.
        /// </summary>
        public readonly struct ScatterPreviewGizmoRecord
        {
            public ScatterPreviewGizmoRecord(
                Vector3 position,
                float spacingRadius,
                float depthMeters,
                float slopeDegrees,
                WorldPrefabFamilyProfile.ScatterLayer scatterLayer,
                WorldProceduralPlacementRule.FloraSubstrateMask substrate,
                float maxTiltAngleDegrees)
            {
                Position = position;
                SpacingRadius = spacingRadius;
                DepthMeters = depthMeters;
                SlopeDegrees = slopeDegrees;
                ScatterLayer = scatterLayer;
                Substrate = substrate;
                MaxTiltAngleDegrees = maxTiltAngleDegrees;
            }

            public readonly Vector3 Position;
            public readonly float SpacingRadius;
            public readonly float DepthMeters;
            public readonly float SlopeDegrees;
            public readonly WorldPrefabFamilyProfile.ScatterLayer ScatterLayer;
            public readonly WorldProceduralPlacementRule.FloraSubstrateMask Substrate;
            public readonly float MaxTiltAngleDegrees;
        }


        internal readonly struct ScatterWindowContext

        {
            public readonly int CellX;
            public readonly int CellZ;
            public readonly int Stride;
            public readonly int HeightLayerIndex;


            public ScatterWindowContext(int cellX, int cellZ, int stride, int heightLayerIndex)

            {
                CellX = cellX;
                CellZ = cellZ;
                Stride = stride;
                HeightLayerIndex = heightLayerIndex;
            }
        }

        internal readonly struct ScatterCandidate : IComparable<ScatterCandidate>
        {
            internal ScatterCandidate(
                ScatterPlacement placement,
                WorldPrefabFamilyProfile family,
                WorldProceduralPlacementRule rule,
                string heatmapChannel,
                float heat,
                float score)
            {
                Placement = placement;
                Family = family;
                Rule = rule;
                HeatmapChannel = heatmapChannel;
                Heat = heat;
                Score = score;
            }

            internal readonly ScatterPlacement Placement;
            public readonly WorldPrefabFamilyProfile Family;
            public readonly WorldProceduralPlacementRule Rule;
            public readonly string HeatmapChannel;
            public readonly float Heat;
            public readonly float Score;

            public int CompareTo(ScatterCandidate other)
            {
                return other.Score.CompareTo(this.Score);
            }
        }

        private static string GetPatternLabel(WorldProceduralPattern pattern)
        {
            switch (pattern)
            {
                case WorldProceduralPattern.SedimentResources:
                    return ScatterPatternSedimentResourcesLabel;
                case WorldProceduralPattern.FertileShallows:
                    return ScatterPatternFertileShallowsLabel;
                case WorldProceduralPattern.ReefNavigation:
                    return ScatterPatternReefNavigationLabel;
                case WorldProceduralPattern.IndustrialService:
                    return ScatterPatternIndustrialServiceLabel;
                case WorldProceduralPattern.BrineToxic:
                    return ScatterPatternBrineToxicLabel;
                case WorldProceduralPattern.VolcanicPressure:
                    return ScatterPatternVolcanicPressureLabel;
                case WorldProceduralPattern.RiftHazard:
                    return ScatterPatternRiftHazardLabel;
                case WorldProceduralPattern.AbyssSparse:
                    return ScatterPatternAbyssSparseLabel;
                case WorldProceduralPattern.LandmarkCorridor:
                    return ScatterPatternLandmarkCorridorLabel;
                default:
                    return ScatterPatternNoneLabel;
            }
        }

        private static string GetClusterAccentRoleLabel(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            switch (role)
            {
                case WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth:
                    return "FertileGrowth";
                case WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest:
                    return "BiologicalNest";
                case WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket:
                    return "ResourcePocket";
                case WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket:
                    return "ShelterPocket";
                case WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket:
                    return "HazardPocket";
                case WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField:
                    return "DebrisField";
                case WorldPrefabFamilyProfile.ClusterAccentRole.RockCover:
                    return "RockCover";
                default:
                    return ScatterPatternNoneLabel;
            }
        }

        private static string GetStructureAccentRoleLabel(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            switch (role)
            {
                case WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark:
                    return "NaturalLandmark";
                case WorldPrefabFamilyProfile.StructureAccentRole.TechFragment:
                    return "TechFragment";
                case WorldPrefabFamilyProfile.StructureAccentRole.CaveRead:
                    return "CaveRead";
                case WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette:
                    return "BiologicalSilhouette";
                default:
                    return ScatterPatternNoneLabel;
            }
        }

        private static string GetStreamingLayerLabel(WorldStreamingLayer layer)
        {
            switch (layer)
            {
                case WorldStreamingLayer.Flora:
                    return ScatterLayerFloraLabel;
                case WorldStreamingLayer.Debris:
                    return ScatterLayerDebrisLabel;
                case WorldStreamingLayer.Resources:
                    return ScatterLayerResourcesLabel;
                case WorldStreamingLayer.Fauna:
                    return ScatterLayerFaunaLabel;
                case WorldStreamingLayer.Construction:
                    return ScatterLayerConstructionLabel;
                case WorldStreamingLayer.LargeThreats:
                    return ScatterLayerLargeThreatsLabel;
                default:
                    return ScatterLayerTerrainLodLabel;
            }
        }

    }
}
