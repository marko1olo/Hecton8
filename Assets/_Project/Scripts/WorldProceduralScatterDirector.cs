using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4036)]
    public sealed class WorldProceduralScatterDirector : MonoBehaviour, ISlowTickable
    {
        private const string ScatterRootName = "__PROCEDURAL_SCATTER_WORLD";
        private const int ScatterLayerCount = 4;
        private static readonly int _ClusterAccentRoleCount = Enum.GetValues(typeof(WorldPrefabFamilyProfile.ClusterAccentRole)).Length;
        private static readonly int _StructureAccentRoleCount = Enum.GetValues(typeof(WorldPrefabFamilyProfile.StructureAccentRole)).Length;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldProceduralFieldSampler fieldSampler;
        [SerializeField] private WorldProceduralFillDirector proceduralFillDirector;
        [SerializeField] private WorldProceduralPatternCatalog patternCatalog;
        [SerializeField] private WorldProceduralBiomeFamilyContextCatalog biomeContextCatalog;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;
        [SerializeField] private WorldFaunaSpawnRegistry faunaSpawnRegistry;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;
        [SerializeField] private WorldGenerativeGeologyService generativeGeologyService;

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
        [SerializeField] private float missingPlacementGraceSeconds = 8f;
        [SerializeField] private bool waitForSceneBootstrap = true;
        [Tooltip("Максимум ожидания появления SceneBootstrap в сцене. Если bootstrap отсутствует дольше этого времени, scatter продолжит работу без него. 0 = не ждать.")]
        [SerializeField] private float bootstrapWaitTimeoutSeconds = 12f;
        [SerializeField] private float scatterRefreshDistanceThreshold = 8f;
        [SerializeField] private bool enableForcedScatterRefresh = false;
        [SerializeField] private float scatterForcedRefreshInterval = 0f;
        [SerializeField] private bool enableScatterRebuildProfiling = true;
        [SerializeField] private float scatterRebuildSpikeThresholdMs = 40f;
        [Tooltip("Включает подробную строковую диагностику sampling/rebuild. Держи выключенной в обычном runtime, чтобы не тратить CPU на hot path.")]
        [SerializeField] private bool enableScatterDetailedDiagnostics = false;

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
        [SerializeField] private int _debugRaycastSamples;
        [SerializeField] private int _debugFallbackSamples;
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
        [SerializeField] private string _debugLastScatterRefreshReason = "None";
        [SerializeField] private string _debugLastScatterInvalidationReason = "None";

        private readonly Dictionary<long, WorldProceduralProxyInstance> _activeInstances = new Dictionary<long, WorldProceduralProxyInstance>(1024);
        private readonly Dictionary<long, ScatterPlacement> _desiredPlacements = new Dictionary<long, ScatterPlacement>(1024);
        private readonly Dictionary<long, ScatterPlacement> _retainedPlacements = new Dictionary<long, ScatterPlacement>(1024);
        private readonly Dictionary<long, float> _placementLastSeenTimes = new Dictionary<long, float>(1024);
        private readonly Dictionary<long, int> _structureWindowCounts = new Dictionary<long, int>(256);
        private readonly Dictionary<long, int> _spawnWindowCounts = new Dictionary<long, int>(256);
        private readonly List<long> _removalBuffer = new List<long>(256);
        private readonly List<ScatterCandidate> _candidateBuffer = new List<ScatterCandidate>(32);
        private readonly List<WorldFaunaSpawnRegistry.Anchor> _faunaAnchorBuffer = new List<WorldFaunaSpawnRegistry.Anchor>(128);
        private readonly List<ScatterCandidate> _clusterAccentOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _clusterOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _exactClusterOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _groundOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _windowOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _patternStructureOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _structureAccentOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _patternSpawnOrderedCandidates = new List<ScatterCandidate>(128);
        private readonly List<ScatterCandidate> _patternSpawnPassiveOrderedCandidates = new List<ScatterCandidate>(96);
        private readonly List<ScatterCandidate> _patternSpawnPredatorOrderedCandidates = new List<ScatterCandidate>(64);
        private readonly HashSet<long> _occupiedCellBuffer = new HashSet<long>();
        private readonly ScatterCandidate[] _layerTopCandidatesBuffer = new ScatterCandidate[ScatterLayerCount];
        private readonly bool[] _layerTopValidBuffer = new bool[ScatterLayerCount];
        private readonly int[] _layerPlacementCountsBuffer = new int[ScatterLayerCount];
        private readonly int[] _clusterAccentCountsBuffer = new int[_ClusterAccentRoleCount];
        private readonly int[] _structureAccentCountsBuffer = new int[_StructureAccentRoleCount];
        private readonly Dictionary<string, int>[] _layerFamilyCountsBuffer = CreateLayerFamilyCounters();
        private readonly Dictionary<string, int>[] _layerBiomeCountsBuffer = CreateLayerFamilyCounters();
        private readonly Dictionary<long, ScatterCandidate> _groundRescueCandidates = new Dictionary<long, ScatterCandidate>(128);
        private readonly Dictionary<long, ScatterCandidate> _clusterRescueCandidates = new Dictionary<long, ScatterCandidate>(128);
        private readonly Dictionary<long, ScatterCandidate> _structureRescueCandidates = new Dictionary<long, ScatterCandidate>(64);
        private readonly Dictionary<long, ScatterCandidate> _spawnRescueCandidates = new Dictionary<long, ScatterCandidate>(64);
        private readonly Dictionary<long, ScatterCandidate> _clusterFertileCandidates = new Dictionary<long, ScatterCandidate>(48);
        private readonly Dictionary<long, ScatterCandidate> _clusterNestCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _clusterResourceCandidates = new Dictionary<long, ScatterCandidate>(48);
        private readonly Dictionary<long, ScatterCandidate> _clusterShelterCandidates = new Dictionary<long, ScatterCandidate>(48);
        private readonly Dictionary<long, ScatterCandidate> _clusterHazardCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _clusterDebrisCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _clusterRockCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _structureNaturalCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _structureTechCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _structureCaveCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _structureBioCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _passiveSpawnCandidates = new Dictionary<long, ScatterCandidate>(32);
        private readonly Dictionary<long, ScatterCandidate> _predatorSpawnCandidates = new Dictionary<long, ScatterCandidate>(16);
        private readonly Dictionary<HectonBiomeMatrixProfile, int> _sampledMatrixProfileCounts = new Dictionary<HectonBiomeMatrixProfile, int>(16);
        private readonly Dictionary<string, int> _sampledMatrixBiomeCounts = new Dictionary<string, int>(16);
        private readonly Dictionary<string, int> _sampledBiomeCounts = new Dictionary<string, int>(16);
        private readonly Dictionary<string, int> _sampledPatternCounts = new Dictionary<string, int>(8);
        private readonly Dictionary<string, int> _sampledZoneCounts = new Dictionary<string, int>(8);
        private static WorldProceduralPatternProfile _emergencyPatternProfile;
        private static WorldGenerativeGeologyProfile _emergencyArchGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyCanopyGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyLandmarkGeologyProfile;
        private static WorldGenerativeGeologyProfile _emergencyCaveGeologyProfile;
        private bool _registeredToTickManager;
        private bool _subscribedToBootstrap;
        private bool _sceneBootstrapPresenceResolved;
        private bool _sceneBootstrapPresent;
        private bool _bootstrapFailed;
        private float _bootstrapWaitStartTime = -1f;
        private bool _bootstrapTimeoutReported;
        private bool _hasScatterRefreshSample;
        private Vector3 _lastScatterRefreshPosition;
        private int _lastScatterRefreshCenterCellX;
        private int _lastScatterRefreshCenterCellZ;
        private float _lastScatterRefreshTime = float.NegativeInfinity;
        private float _runtimeCellSize = 22f;
        private int _runtimeRadiusCells = 7;
        private float _runtimeChunkSize = 192f;
        private float _runtimeMacroZoneSize = 768f;

        public int ActivePlacementCount => _activeInstances.Count;

        private void Awake()
        {
            ResolveReferences();
            RegisterProceduralStateRegistryCallbacks();
            SubscribeToBootstrap();
            RefreshRuntimeStreamingSettings();
            if (!ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private void OnEnable()
        {
            BeginBootstrapWaitWindow();
            ResolveReferences();
            RegisterProceduralStateRegistryCallbacks();
            SubscribeToBootstrap();
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            if (!ShouldDeferUntilBootstrapReady())
                RebuildScatterPreview();
        }

        private void OnDisable()
        {
            UnsubscribeFromBootstrap();
            UnregisterProceduralStateRegistryCallbacks();
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            if (ShouldDeferUntilBootstrapReady())
                return;

            RefreshRuntimeStreamingSettings();
            if (ShouldSkipScatterRefresh())
                return;

            RebuildScatterPreview();
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            RefreshRuntimeStreamingSettings();
            InvalidateScatterRefreshSample("chunk-profile");
        }

        public void SetFaunaSpawnRegistry(WorldFaunaSpawnRegistry registry)
        {
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

            InvalidateScatterRefreshSample("state-registry");
            RebuildScatterPreview();
        }

        public void RebuildScatterPreview()
        {
            if (ShouldDeferUntilBootstrapReady())
            {
                ResetDiagnostics();
                return;
            }

            ResolveReferences();
            RefreshRuntimeStreamingSettings();

            IReadOnlyList<WorldProceduralPlacementRule> rules = proceduralFillDirector != null ? proceduralFillDirector.Rules : null;
            if (playerTransform == null || fieldSampler == null || rules == null || rules.Count == 0)
            {
                PublishFaunaRegistrySnapshot();
                ResetDiagnostics();
                return;
            }

            _desiredPlacements.Clear();
            _structureWindowCounts.Clear();
            _spawnWindowCounts.Clear();
            _candidateBuffer.Clear();
            ClearScatterWorkingBuffers();

            float size = Mathf.Max(6f, _runtimeCellSize);
            int radius = Mathf.Max(2, _runtimeRadiusCells);
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            int groundBudget = ResolveRuntimeBudget(groundPlacementsPerCell, WorldStreamingLayer.Flora, 0, 4);
            int clusterBudget = ResolveRuntimeBudget(clusterPlacementsPerCell, WorldStreamingLayer.Debris, 0, 3);
            int structureStride = Mathf.Max(2, structureCellStride);
            int structureBudget = ResolveRuntimeBudget(structurePlacementsPerWindow, WorldStreamingLayer.Construction, 0, 2);
            int spawnStride = Mathf.Max(2, spawnCellStride);
            int spawnBudget = ResolveRuntimeBudget(spawnPlacementsPerWindow, WorldStreamingLayer.Fauna, 0, 2);
            Vector3 center = playerTransform.position;
            int centerX = Mathf.RoundToInt(center.x / size);
            int centerZ = Mathf.RoundToInt(center.z / size);
            long rebuildStartTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;
            int evaluatedCells = 0;
            ScatterCandidate topCandidate = default;
            bool hasTopCandidate = false;
            ScatterCandidate[] layerTopCandidates = _layerTopCandidatesBuffer;
            bool[] layerTopValid = _layerTopValidBuffer;
            int[] layerPlacementCounts = _layerPlacementCountsBuffer;
            int[] clusterAccentCounts = _clusterAccentCountsBuffer;
            int[] structureAccentCounts = _structureAccentCountsBuffer;
            Dictionary<string, int>[] layerFamilyCounts = _layerFamilyCountsBuffer;
            Dictionary<string, int>[] layerBiomeCounts = _layerBiomeCountsBuffer;
            Dictionary<long, ScatterCandidate> groundRescueCandidates = _groundRescueCandidates;
            Dictionary<long, ScatterCandidate> clusterRescueCandidates = _clusterRescueCandidates;
            Dictionary<long, ScatterCandidate> structureRescueCandidates = _structureRescueCandidates;
            Dictionary<long, ScatterCandidate> spawnRescueCandidates = _spawnRescueCandidates;
            Dictionary<long, ScatterCandidate> clusterFertileCandidates = _clusterFertileCandidates;
            Dictionary<long, ScatterCandidate> clusterNestCandidates = _clusterNestCandidates;
            Dictionary<long, ScatterCandidate> clusterResourceCandidates = _clusterResourceCandidates;
            Dictionary<long, ScatterCandidate> clusterShelterCandidates = _clusterShelterCandidates;
            Dictionary<long, ScatterCandidate> clusterHazardCandidates = _clusterHazardCandidates;
            Dictionary<long, ScatterCandidate> clusterDebrisCandidates = _clusterDebrisCandidates;
            Dictionary<long, ScatterCandidate> clusterRockCandidates = _clusterRockCandidates;
            Dictionary<long, ScatterCandidate> structureNaturalCandidates = _structureNaturalCandidates;
            Dictionary<long, ScatterCandidate> structureTechCandidates = _structureTechCandidates;
            Dictionary<long, ScatterCandidate> structureCaveCandidates = _structureCaveCandidates;
            Dictionary<long, ScatterCandidate> structureBioCandidates = _structureBioCandidates;
            Dictionary<long, ScatterCandidate> passiveSpawnCandidates = _passiveSpawnCandidates;
            Dictionary<long, ScatterCandidate> predatorSpawnCandidates = _predatorSpawnCandidates;
            Dictionary<HectonBiomeMatrixProfile, int> sampledMatrixProfileCounts = _sampledMatrixProfileCounts;
            Dictionary<string, int> sampledMatrixBiomeCounts = _sampledMatrixBiomeCounts;
            Dictionary<string, int> sampledBiomeCounts = _sampledBiomeCounts;
            Dictionary<string, int> sampledPatternCounts = _sampledPatternCounts;
            Dictionary<string, int> sampledZoneCounts = _sampledZoneCounts;
            int passiveSpawnCount = 0;
            int predatorSpawnCount = 0;
            int mapMagicSamples = 0;
            int raycastSamples = 0;
            int fallbackSamples = 0;
            bool collectDetailedDiagnostics = enableScatterDetailedDiagnostics;
            WorldZoneAnchor debugZone = null;
            WorldZoneAnchor.ZoneKind debugResolvedZoneKind = default;
            WorldProceduralPattern debugPattern = WorldProceduralPattern.SedimentResources;
            float debugGroundBudgetScale = 1f;
            float debugClusterBudgetScale = 1f;
            float debugStructureBudgetScale = 1f;
            float debugSpawnBudgetScale = 1f;
            HectonBiomeMatrixProfile debugBiomeProfile = null;
            Hecton8.Environment.HectonBiomeFamilyProfile debugBiomeFamily = null;

            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int cellXIndex = centerX + x;
                    int cellZIndex = centerZ + z;
                    Vector3 sampleOrigin = new Vector3(
                        (cellXIndex + 0.5f) * size,
                        center.y,
                        (cellZIndex + 0.5f) * size);

                    if (!fieldSampler.TrySampleSeafloor(sampleOrigin, out WorldProceduralFieldSampler.FieldSample fieldSample))
                        continue;

                    evaluatedCells++;
                    CountSeafloorSource(fieldSample.seafloorSource, ref mapMagicSamples, ref raycastSamples, ref fallbackSamples);
                    debugZone = fieldSample.zone;
                    debugResolvedZoneKind = fieldSample.resolvedZoneKind;
                    debugPattern = fieldSample.resolvedPattern;
                    debugBiomeProfile = fieldSample.biomeProfile;
                    debugBiomeFamily = fieldSample.biomeFamily;
                    RegisterProfileCount(sampledMatrixProfileCounts, fieldSample.biomeProfile);
                    if (collectDetailedDiagnostics)
                    {
                        RegisterStringCount(sampledMatrixBiomeCounts, ResolveBiomeMatrixLabel(fieldSample.biomeProfile));
                        RegisterStringCount(sampledBiomeCounts, ResolveBiomeLabel(fieldSample.biomeFamily));
                        RegisterStringCount(sampledPatternCounts, fieldSample.resolvedPattern.ToString());
                        RegisterStringCount(sampledZoneCounts, fieldSample.zone != null ? fieldSample.zone.ZoneLabel : $"Synthetic:{fieldSample.resolvedZoneKind}");
                    }
                    int localGroundBudget = ResolveScaledBudget(groundBudget, fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Ground, 4);
                    int localClusterBudget = ResolveScaledBudget(clusterBudget, fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Cluster, 3);
                    int localStructureBudget = ResolveScaledBudget(structureBudget, fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Structure, 2);
                    int localSpawnBudget = ResolveScaledBudget(spawnBudget, fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Spawn, 2);
                    debugGroundBudgetScale = GetCombinedBudgetScale(fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Ground);
                    debugClusterBudgetScale = GetCombinedBudgetScale(fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
                    debugStructureBudgetScale = GetCombinedBudgetScale(fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Structure);
                    debugSpawnBudgetScale = GetCombinedBudgetScale(fieldSample.resolvedPattern, fieldSample.biomeFamily, WorldPrefabFamilyProfile.ScatterLayer.Spawn);

                    _candidateBuffer.Clear();
                    for (int i = 0; i < rules.Count; i++)
                    {
                        WorldProceduralPlacementRule rule = rules[i];
                        if (rule == null || rule.familyProfile == null || !rule.familyProfile.allowRuntimeScatter)
                            continue;

                        WorldContentSocket.ContentKind scatterKind = rule.GetScatterContentKind();
                        if (!rule.MatchesScatter(fieldSample.biomeFamily, fieldSample.zone, fieldSample.resolvedZoneKind, scatterKind, fieldSample.depthMeters, fieldSample.slopeDegrees))
                            continue;

                        WorldPrefabFamilyProfile family = rule.familyProfile;
                        string heatmapChannel = !string.IsNullOrWhiteSpace(rule.requiredHeatmapChannel)
                            ? rule.requiredHeatmapChannel
                            : family.heatmapChannel;
                        float heat = fieldSampler.EvaluateHeatmap(heatmapChannel, fieldSample, family, rule);
                        heat = Mathf.Clamp01(
                            heat
                            * GetPatternHeatScale(fieldSample.resolvedPattern, family)
                            * GetDepthDomainScale(fieldSample.depthMeters, family));
                        float effectiveMinHeat = ResolveEffectiveMinHeat(rule, family, fieldSample);
                        float effectiveDensityScale = ResolveEffectiveDensityScale(rule, family, fieldSample);
                        if (heat < effectiveMinHeat)
                            continue;

                        float normalizedHeat = Mathf.InverseLerp(effectiveMinHeat, 1f, heat);
                        float spawnProbability = Mathf.Clamp01(normalizedHeat * (0.45f + Mathf.Clamp(effectiveDensityScale, 0.1f, 4f) * 0.18f));
                        float score = spawnProbability
                            + heat
                            + GetPlacementModeBonus(family.placementMode)
                            + GetScatterLayerBonus(family.scatterLayer)
                            + GetFamilyAffinityBonus(fieldSample, family)
                            + GetGenerativeGeologyContextBonus(fieldSample, family)
                            + GetPatternAffinityBonus(fieldSample, family)
                            + GetClusterAccentPatternBonus(fieldSample.resolvedPattern, family)
                            + GetSpawnFamilyPatternBonus(fieldSample.resolvedPattern, family)
                            + GetPatternContextBonus(fieldSample.resolvedPattern, family)
                            + GetBiomeContextBonus(fieldSample.biomeFamily, family)
                            + GetBiomeMatrixBonus(fieldSample.resolvedPattern, fieldSample.biomeProfile, family)
                            + GetBiomeSignatureScoreBonus(fieldSample.resolvedPattern, fieldSample.biomeProfile, family);
                        ScatterCandidate candidate = BuildCandidate(cellXIndex, cellZIndex, fieldSample, family, rule, heatmapChannel, heat, score, size);
                        if (!IsPlacementWithinResidency(candidate.Placement, center))
                            continue;

                        TrackRescueCandidate(
                            candidate,
                            fieldSample,
                            structureStride,
                            spawnStride,
                        groundRescueCandidates,
                        clusterRescueCandidates,
                        structureRescueCandidates,
                        spawnRescueCandidates,
                        clusterFertileCandidates,
                        clusterNestCandidates,
                        clusterResourceCandidates,
                        clusterShelterCandidates,
                        clusterHazardCandidates,
                        clusterDebrisCandidates,
                        clusterRockCandidates,
                        structureNaturalCandidates,
                        structureTechCandidates,
                        structureCaveCandidates,
                            structureBioCandidates,
                            passiveSpawnCandidates,
                            predatorSpawnCandidates);
                        float gate = StableRandom01(cellXIndex, cellZIndex, rule.ruleId);
                        if (gate > spawnProbability)
                            continue;

                        _candidateBuffer.Add(candidate);

                        if (!hasTopCandidate || candidate.Score > topCandidate.Score)
                        {
                            topCandidate = candidate;
                            hasTopCandidate = true;
                        }
                    }

                    if (_candidateBuffer.Count == 0)
                        continue;

                    _candidateBuffer.Sort(ScatterCandidateComparer.Instance);

                    int cellGroundCount = 0;
                    int cellClusterCount = 0;
                    int cellStructureCount = 0;
                    int cellSpawnCount = 0;
                    for (int i = 0; i < _candidateBuffer.Count; i++)
                    {
                        ScatterCandidate candidate = _candidateBuffer[i];
                        WorldPrefabFamilyProfile.ScatterLayer layer = candidate.Family.scatterLayer;
                        int layerIndex = (int)layer;
                        if (!HasPatternLayerGlobalBudget(fieldSample.resolvedPattern, fieldSample.biomeProfile, layer, layerPlacementCounts[layerIndex]))
                            continue;

                        if (!HasLayerBudget(
                                candidate,
                                localGroundBudget,
                                localClusterBudget,
                                structureStride,
                                localStructureBudget,
                                spawnStride,
                                localSpawnBudget,
                                cellGroundCount,
                                cellClusterCount,
                                cellStructureCount,
                                cellSpawnCount))
                            continue;

                        if (!CanAcceptPatternAccentBudget(
                                fieldSample.resolvedPattern,
                                fieldSample.biomeProfile,
                                candidate,
                                clusterAccentCounts,
                                structureAccentCounts,
                                passiveSpawnCount,
                                predatorSpawnCount,
                                layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster],
                                layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure],
                                layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn]))
                            continue;

                        if (!CanAcceptCandidate(candidate))
                            continue;

                        ScatterPlacement placement = candidate.Placement;
                        if (!TryRegisterDesiredPlacement(placement, now))
                            continue;
                        layerPlacementCounts[layerIndex]++;
                        if (collectDetailedDiagnostics)
                        {
                            RegisterLayerFamilyCount(layerFamilyCounts, layer, candidate.Family);
                            RegisterLayerBiomeCount(layerBiomeCounts, layer, candidate.Placement.BiomeFamily);
                        }
                        if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
                        {
                            layerTopCandidates[layerIndex] = candidate;
                            layerTopValid[layerIndex] = true;
                        }
                        RegisterAccentAndSpawnCounts(candidate.Family, clusterAccentCounts, structureAccentCounts, ref passiveSpawnCount, ref predatorSpawnCount);

                        switch (layer)
                        {
                            case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                                cellGroundCount++;
                                break;
                            case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                                cellClusterCount++;
                                break;
                            case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                                cellStructureCount++;
                                RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, structureStride, _structureWindowCounts);
                                break;
                            case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                                cellSpawnCount++;
                                RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, spawnStride, _spawnWindowCounts);
                                break;
                        }
                    }
                }
            }

            long samplingEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            HectonBiomeMatrixProfile dominantBiomeProfile = ResolveDominantBiomeMatrixProfile(sampledMatrixProfileCounts, debugBiomeProfile);

            InjectRescuePlacementsIfNeeded(
                debugPattern,
                dominantBiomeProfile,
                clusterBudget,
                structureStride,
                spawnStride,
                structureBudget,
                spawnBudget,
                layerPlacementCounts,
                clusterAccentCounts,
                structureAccentCounts,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts,
                groundRescueCandidates,
                clusterRescueCandidates,
                structureRescueCandidates,
                spawnRescueCandidates,
                clusterFertileCandidates,
                clusterNestCandidates,
                clusterResourceCandidates,
                clusterShelterCandidates,
                clusterHazardCandidates,
                clusterDebrisCandidates,
                clusterRockCandidates,
                structureNaturalCandidates,
                structureTechCandidates,
                structureCaveCandidates,
                structureBioCandidates,
                passiveSpawnCandidates,
                predatorSpawnCandidates);
            long rescueEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            RestoreRecentDesiredPlacements(center, now);
            long restoreEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            ScatterReconcileMetrics reconcileMetrics = ReconcileInstances(enableScatterRebuildProfiling);
            long reconcileEndTimestamp = reconcileMetrics.EndTimestamp;

            _debugReady = true;
            _debugEvaluatedCells = evaluatedCells;
            _debugDesiredPlacements = _desiredPlacements.Count;
            _debugActivePlacements = _activeInstances.Count;
            _debugGroundPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Ground];
            _debugClusterPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            _debugStructurePlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            _debugSpawnPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            _debugMapMagicSamples = mapMagicSamples;
            _debugRaycastSamples = raycastSamples;
            _debugFallbackSamples = fallbackSamples;
            _debugZone = debugZone != null ? debugZone.ZoneLabel : $"Synthetic:{debugResolvedZoneKind}";
            _debugBiomeMatrixProfile = dominantBiomeProfile != null ? dominantBiomeProfile.biomeName : ResolveBiomeMatrixLabel(debugBiomeProfile);
            _debugBiomeFamily = debugBiomeFamily != null ? debugBiomeFamily.familyLabel : "None";
            _debugPattern = debugPattern.ToString();
            WorldProceduralPatternProfile debugPatternProfile = ResolvePatternProfile(debugPattern, out bool usedFallbackPatternProfile);
            WorldProceduralBiomeFamilyContextProfile debugBiomeContextProfile = ResolveBiomeContextProfile(debugBiomeFamily, out bool usedFallbackBiomeContextProfile);
            _debugResolvedPatternProfile = debugPatternProfile != null ? debugPatternProfile.label : "None";
            _debugUsedFallbackPatternProfile = usedFallbackPatternProfile;
            _debugResolvedBiomeContextProfile = debugBiomeContextProfile != null ? debugBiomeContextProfile.label : "None";
            _debugUsedFallbackBiomeContextProfile = usedFallbackBiomeContextProfile;
            _debugTopRule = hasTopCandidate && topCandidate.Rule != null ? topCandidate.Rule.ruleLabel : "None";
            _debugTopFamily = hasTopCandidate && topCandidate.Family != null ? topCandidate.Family.familyLabel : "None";
            _debugTopHeatmap = hasTopCandidate ? topCandidate.HeatmapChannel : "None";
            _debugTargetGroundMin = ResolveMinimumGroundPlacements(debugPattern, dominantBiomeProfile);
            _debugTargetGroundMax = ResolvePatternLayerTargetMax(debugPattern, dominantBiomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Ground);
            _debugTargetClusterMin = ResolveMinimumClusterPlacements(debugPattern, dominantBiomeProfile);
            _debugTargetClusterMax = ResolvePatternLayerTargetMax(debugPattern, dominantBiomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            _debugTargetStructureMin = ResolvePatternStructureTargetMin(debugPattern, dominantBiomeProfile);
            _debugTargetStructureMax = ResolvePatternStructureTargetMax(debugPattern, dominantBiomeProfile);
            _debugTargetSpawnMin = ResolvePatternSpawnTargetMin(debugPattern, dominantBiomeProfile);
            _debugTargetSpawnMax = ResolvePatternSpawnTargetMax(debugPattern, dominantBiomeProfile);
            _debugPatternGroundBudgetScale = debugGroundBudgetScale;
            _debugPatternClusterBudgetScale = debugClusterBudgetScale;
            _debugPatternStructureBudgetScale = debugStructureBudgetScale;
            _debugPatternSpawnBudgetScale = debugSpawnBudgetScale;
            _debugTopHeat = hasTopCandidate ? topCandidate.Heat : 0f;
            _debugTopScore = hasTopCandidate ? topCandidate.Score : 0f;
            _debugGroundTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Ground);
            _debugClusterTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            _debugStructureTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Structure);
            _debugSpawnTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Spawn);
            _debugClusterDominantAccentRole = ResolveDominantClusterAccentRole(clusterAccentCounts, out _debugClusterDominantAccentCount);
            _debugStructureDominantAccentRole = ResolveDominantStructureAccentRole(structureAccentCounts, out _debugStructureDominantAccentCount);
            _debugClusterFertileGrowthCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth);
            _debugClusterBiologicalNestCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest);
            _debugClusterResourcePocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket);
            _debugClusterShelterPocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket);
            _debugClusterHazardPocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket);
            _debugClusterDebrisFieldCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField);
            _debugClusterRockCoverCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.RockCover);
            _debugStructureNaturalLandmarkCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark);
            _debugStructureTechFragmentCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.TechFragment);
            _debugStructureCaveReadCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.CaveRead);
            _debugStructureBiologicalSilhouetteCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette);
            _debugSpawnPassiveCount = passiveSpawnCount;
            _debugSpawnPredatorCount = predatorSpawnCount;
            if (collectDetailedDiagnostics)
            {
                _debugSampleDominantMatrixBiome = ResolveDominantCounter(sampledMatrixBiomeCounts, out _debugSampleDominantMatrixCount);
                _debugGroundDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, out _debugGroundDominantCount);
                _debugClusterDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, out _debugClusterDominantCount);
                _debugStructureDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Structure, out _debugStructureDominantCount);
                _debugSpawnDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Spawn, out _debugSpawnDominantCount);
                _debugGroundDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, out _);
                _debugClusterDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, out _);
                _debugStructureDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Structure, out _);
                _debugSpawnDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Spawn, out _);
                _debugSampleDominantBiomeFamily = ResolveDominantCounter(sampledBiomeCounts, out _debugSampleDominantBiomeCount);
                _debugSampleDominantPattern = ResolveDominantCounter(sampledPatternCounts, out _debugSampleDominantPatternCount);
                _debugSampleDominantZone = ResolveDominantCounter(sampledZoneCounts, out _debugSampleDominantZoneCount);
            }
            else
            {
                _debugSampleDominantMatrixBiome = "Disabled";
                _debugGroundDominantFamily = "Disabled";
                _debugClusterDominantFamily = "Disabled";
                _debugStructureDominantFamily = "Disabled";
                _debugSpawnDominantFamily = "Disabled";
                _debugGroundDominantBiomeFamily = "Disabled";
                _debugClusterDominantBiomeFamily = "Disabled";
                _debugStructureDominantBiomeFamily = "Disabled";
                _debugSpawnDominantBiomeFamily = "Disabled";
                _debugSampleDominantBiomeFamily = "Disabled";
                _debugSampleDominantPattern = "Disabled";
                _debugSampleDominantZone = "Disabled";
                _debugGroundDominantCount = 0;
                _debugClusterDominantCount = 0;
                _debugStructureDominantCount = 0;
                _debugSpawnDominantCount = 0;
                _debugSampleDominantMatrixCount = 0;
                _debugSampleDominantBiomeCount = 0;
                _debugSampleDominantPatternCount = 0;
                _debugSampleDominantZoneCount = 0;
            }
            RecordScatterRefreshSample();
            if (enableScatterRebuildProfiling)
            {
                long diagnosticsEndTimestamp = Stopwatch.GetTimestamp();
                CommitScatterRebuildProfile(
                    rebuildStartTimestamp,
                    samplingEndTimestamp,
                rescueEndTimestamp,
                restoreEndTimestamp,
                reconcileMetrics,
                diagnosticsEndTimestamp,
                evaluatedCells);
            }
        }

        private bool ShouldDeferUntilBootstrapReady()
        {
            if (!Application.isPlaying || !waitForSceneBootstrap || SceneBootstrap.IsGameReady)
                return false;

            if (_bootstrapFailed)
                return false;

            return ResolveSceneBootstrapPresence();
        }

        private void HandleSceneBootstrapReady()
        {
            _sceneBootstrapPresenceResolved = true;
            _sceneBootstrapPresent = true;
            _bootstrapFailed = false;
            _bootstrapWaitStartTime = -1f;
            _bootstrapTimeoutReported = false;
            InvalidateScatterRefreshSample("scene-bootstrap");
            RefreshRuntimeStreamingSettings();
            RebuildScatterPreview();
        }

        private void HandleSceneBootstrapFailed(string reason)
        {
            _sceneBootstrapPresenceResolved = true;
            _sceneBootstrapPresent = true;
            _bootstrapFailed = true;
            _bootstrapWaitStartTime = -1f;
            _bootstrapTimeoutReported = false;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                UnityEngine.Debug.LogWarning(
                    $"[WorldScatter] Scene bootstrap failed and scatter fallback was enabled. Reason: {reason}",
                    this);
            }

            InvalidateScatterRefreshSample("scene-bootstrap-failed");
            RefreshRuntimeStreamingSettings();
            RebuildScatterPreview();
        }

        private void BeginBootstrapWaitWindow()
        {
            if (!Application.isPlaying)
                return;

            if (_bootstrapWaitStartTime >= 0f)
                return;

            _bootstrapWaitStartTime = Time.unscaledTime;
            _bootstrapTimeoutReported = false;
        }

        private bool ResolveSceneBootstrapPresence()
        {
            if (_sceneBootstrapPresenceResolved)
                return _sceneBootstrapPresent;

            SceneBootstrap bootstrap = FindFirstObjectByType<SceneBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
            {
                _sceneBootstrapPresenceResolved = true;
                _sceneBootstrapPresent = true;
                _bootstrapWaitStartTime = -1f;
                _bootstrapTimeoutReported = false;
                return true;
            }

            BeginBootstrapWaitWindow();

            if (bootstrapWaitTimeoutSeconds > 0f)
            {
                float elapsed = Time.unscaledTime - _bootstrapWaitStartTime;
                if (elapsed < bootstrapWaitTimeoutSeconds)
                    return true;
            }

            if (!_bootstrapTimeoutReported)
            {
                _bootstrapTimeoutReported = true;
                UnityEngine.Debug.LogWarning(
                    $"[WorldScatter] SceneBootstrap was not found after {bootstrapWaitTimeoutSeconds:0.0}s. " +
                    "Continuing procedural scatter warmup without bootstrap gating.",
                    this);
            }

            _sceneBootstrapPresenceResolved = true;
            _sceneBootstrapPresent = false;
            _bootstrapWaitStartTime = -1f;
            return false;
        }

        private bool ShouldSkipScatterRefresh()
        {
            if (!_hasScatterRefreshSample || playerTransform == null)
            {
                _debugLastScatterRefreshReason = $"dirty:{_debugLastScatterInvalidationReason}";
                return false;
            }

            if (enableForcedScatterRefresh && scatterForcedRefreshInterval > 0f)
            {
                float forcedInterval = Mathf.Max(0.5f, scatterForcedRefreshInterval);
                if (Application.isPlaying && Time.unscaledTime - _lastScatterRefreshTime >= forcedInterval)
                {
                    _debugLastScatterRefreshReason = "forced-interval";
                    return false;
                }
            }

            if (TryGetScatterCenterCell(out int centerCellX, out int centerCellZ))
            {
                if (centerCellX != _lastScatterRefreshCenterCellX || centerCellZ != _lastScatterRefreshCenterCellZ)
                {
                    _debugLastScatterRefreshReason = $"cell:{_lastScatterRefreshCenterCellX},{_lastScatterRefreshCenterCellZ}->{centerCellX},{centerCellZ}";
                    return false;
                }

                _debugLastScatterRefreshReason = "same-cell";
                return true;
            }

            float threshold = Mathf.Max(0.5f, scatterRefreshDistanceThreshold);
            bool sameDistanceBucket = (playerTransform.position - _lastScatterRefreshPosition).sqrMagnitude < threshold * threshold;
            _debugLastScatterRefreshReason = sameDistanceBucket ? "same-distance" : "distance-threshold";
            return sameDistanceBucket;
        }

        private void RecordScatterRefreshSample()
        {
            if (playerTransform == null)
                return;

            _hasScatterRefreshSample = true;
            _lastScatterRefreshPosition = playerTransform.position;
            _lastScatterRefreshTime = Application.isPlaying ? Time.unscaledTime : 0f;
            if (TryGetScatterCenterCell(out int centerCellX, out int centerCellZ))
            {
                _lastScatterRefreshCenterCellX = centerCellX;
                _lastScatterRefreshCenterCellZ = centerCellZ;
            }

            _debugLastScatterInvalidationReason = "None";
        }

        private void InvalidateScatterRefreshSample(string reason = "manual")
        {
            _hasScatterRefreshSample = false;
            _lastScatterRefreshTime = float.NegativeInfinity;
            _debugLastScatterInvalidationReason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason;
        }

        private bool TryGetScatterCenterCell(out int centerCellX, out int centerCellZ)
        {
            centerCellX = 0;
            centerCellZ = 0;
            if (playerTransform == null)
                return false;

            float size = Mathf.Max(6f, _runtimeCellSize);
            Vector3 center = playerTransform.position;
            centerCellX = Mathf.RoundToInt(center.x / size);
            centerCellZ = Mathf.RoundToInt(center.z / size);
            return true;
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
            _groundRescueCandidates.Clear();
            _clusterRescueCandidates.Clear();
            _structureRescueCandidates.Clear();
            _spawnRescueCandidates.Clear();
            _clusterFertileCandidates.Clear();
            _clusterNestCandidates.Clear();
            _clusterResourceCandidates.Clear();
            _clusterShelterCandidates.Clear();
            _clusterHazardCandidates.Clear();
            _clusterDebrisCandidates.Clear();
            _clusterRockCandidates.Clear();
            _structureNaturalCandidates.Clear();
            _structureTechCandidates.Clear();
            _structureCaveCandidates.Clear();
            _structureBioCandidates.Clear();
            _passiveSpawnCandidates.Clear();
            _predatorSpawnCandidates.Clear();
            _sampledMatrixProfileCounts.Clear();
            _sampledMatrixBiomeCounts.Clear();
            _sampledBiomeCounts.Clear();
            _sampledPatternCounts.Clear();
            _sampledZoneCounts.Clear();
        }

        private static void ClearDictionaryArray(IReadOnlyList<Dictionary<string, int>> dictionaries)
        {
            if (dictionaries == null)
                return;

            for (int i = 0; i < dictionaries.Count; i++)
                dictionaries[i]?.Clear();
        }

        private void CommitScatterRebuildProfile(
            long rebuildStartTimestamp,
            long samplingEndTimestamp,
            long rescueEndTimestamp,
            long restoreEndTimestamp,
            in ScatterReconcileMetrics reconcileMetrics,
            long diagnosticsEndTimestamp,
            int evaluatedCells)
        {
            long reconcileEndTimestamp = reconcileMetrics.EndTimestamp;
            float samplingMs = GetElapsedMilliseconds(rebuildStartTimestamp, samplingEndTimestamp);
            float rescueMs = GetElapsedMilliseconds(samplingEndTimestamp, rescueEndTimestamp);
            float restoreMs = GetElapsedMilliseconds(rescueEndTimestamp, restoreEndTimestamp);
            float reconcileMs = GetElapsedMilliseconds(restoreEndTimestamp, reconcileEndTimestamp);
            float diagnosticsMs = GetElapsedMilliseconds(reconcileEndTimestamp, diagnosticsEndTimestamp);
            float totalMs = GetElapsedMilliseconds(rebuildStartTimestamp, diagnosticsEndTimestamp);
            float reconcileCleanupMs = GetElapsedMilliseconds(restoreEndTimestamp, reconcileMetrics.CleanupEndTimestamp);
            float reconcileSpawnMs = GetElapsedMilliseconds(reconcileMetrics.CleanupEndTimestamp, reconcileMetrics.SpawnEndTimestamp);
            float reconcileFaunaMs = GetElapsedMilliseconds(reconcileMetrics.SpawnEndTimestamp, reconcileMetrics.EndTimestamp);

            _debugLastScatterRebuildMs = totalMs;
            _debugSamplingStageMs = samplingMs;
            _debugRescueStageMs = rescueMs;
            _debugRestoreStageMs = restoreMs;
            _debugReconcileStageMs = reconcileMs;
            _debugDiagnosticsStageMs = diagnosticsMs;
            _debugReconcileCleanupStageMs = reconcileCleanupMs;
            _debugReconcileSpawnStageMs = reconcileSpawnMs;
            _debugReconcileFaunaStageMs = reconcileFaunaMs;
            _debugReconcileRemovedCount = reconcileMetrics.RemovedCount;
            _debugReconcileRebuiltCount = reconcileMetrics.RebuiltCount;
            _debugReconcileCreatedCount = reconcileMetrics.CreatedCount;
            _debugReconcileReusedCount = reconcileMetrics.ReusedCount;

            if (totalMs < Mathf.Max(1f, scatterRebuildSpikeThresholdMs))
                return;

            UnityEngine.Debug.Log(
                $"[WorldScatterProfiler] rebuild={totalMs:0.00}ms sample={samplingMs:0.00}ms rescue={rescueMs:0.00}ms " +
                $"restore={restoreMs:0.00}ms reconcile={reconcileMs:0.00}ms " +
                $"cleanup={reconcileCleanupMs:0.00}ms spawn={reconcileSpawnMs:0.00}ms fauna={reconcileFaunaMs:0.00}ms " +
                $"diag={diagnosticsMs:0.00}ms removed={reconcileMetrics.RemovedCount} rebuilt={reconcileMetrics.RebuiltCount} " +
                $"created={reconcileMetrics.CreatedCount} reused={reconcileMetrics.ReusedCount} " +
                $"cells={evaluatedCells} desired={_desiredPlacements.Count} active={_activeInstances.Count} reason={_debugLastScatterRefreshReason}",
                this);
        }

        private static float GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (endTimestamp <= startTimestamp)
                return 0f;

            return (float)((endTimestamp - startTimestamp) * 1000.0d / Stopwatch.Frequency);
        }

        private readonly struct ScatterReconcileMetrics
        {
            public readonly int RemovedCount;
            public readonly int RebuiltCount;
            public readonly int CreatedCount;
            public readonly int ReusedCount;
            public readonly long CleanupEndTimestamp;
            public readonly long SpawnEndTimestamp;
            public readonly long EndTimestamp;

            public ScatterReconcileMetrics(
                int removedCount,
                int rebuiltCount,
                int createdCount,
                int reusedCount,
                long cleanupEndTimestamp,
                long spawnEndTimestamp,
                long endTimestamp)
            {
                RemovedCount = removedCount;
                RebuiltCount = rebuiltCount;
                CreatedCount = createdCount;
                ReusedCount = reusedCount;
                CleanupEndTimestamp = cleanupEndTimestamp;
                SpawnEndTimestamp = spawnEndTimestamp;
                EndTimestamp = endTimestamp;
            }
        }

        private void SubscribeToBootstrap()
        {
            if (_subscribedToBootstrap)
                return;

            SceneBootstrap.OnGameReady += HandleSceneBootstrapReady;
            SceneBootstrap.OnBootstrapFailed += HandleSceneBootstrapFailed;
            _subscribedToBootstrap = true;
        }

        private void UnsubscribeFromBootstrap()
        {
            if (!_subscribedToBootstrap)
                return;

            SceneBootstrap.OnGameReady -= HandleSceneBootstrapReady;
            SceneBootstrap.OnBootstrapFailed -= HandleSceneBootstrapFailed;
            _subscribedToBootstrap = false;
        }

        public void ClearScatterPreview()
        {
            GameObject root = GameObject.Find(ScatterRootName);
            if (root != null && _activeInstances.Count == 0)
                ClearRootChildren(root.transform);

            _removalBuffer.Clear();
            foreach (KeyValuePair<long, WorldProceduralProxyInstance> pair in _activeInstances)
                _removalBuffer.Add(pair.Key);

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                long key = _removalBuffer[i];
                if (!_activeInstances.TryGetValue(key, out WorldProceduralProxyInstance instance) || instance == null)
                    continue;

                DestroyProxyInstance(instance.gameObject);
            }

            _activeInstances.Clear();
            _removalBuffer.Clear();
            faunaSpawnRegistry?.Clear();
            ResetDiagnostics();
        }

        private void ResetDiagnostics()
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
            _debugRaycastSamples = 0;
            _debugFallbackSamples = 0;
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
            _debugRuntimeCellSize = _runtimeCellSize;
            _debugRuntimeRadiusCells = _runtimeRadiusCells;
            _debugRuntimeChunkSize = _runtimeChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeMacroZoneSize;
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

        private ScatterCandidate BuildCandidate(
            int cellXIndex,
            int cellZIndex,
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            string heatmapChannel,
            float heat,
            float score,
            float size)
        {
            int stableHash = ComputeStableHash(rule.ruleId, cellXIndex, cellZIndex);
            WorldPrefabFamilyProfile.VariantEntry variant = ResolveRuntimeVariant(family, stableHash, preferFinalVariant: false);
            Vector3 position = ResolvePlacementPosition(fieldSample.position, family, rule, stableHash, size);
            position.y = fieldSample.seafloorHeight + surfaceYOffset;
            Quaternion rotation = Quaternion.Euler(0f, Mathf.Abs(stableHash % 360), 0f);
            float scale = ResolveScaleMultiplier(variant, stableHash);
            WorldStreamingLayer streamingLayer = family != null ? family.ResolveStreamingLayer() : WorldStreamingLayer.Flora;
            WorldGenerativeGeologyProfile geologyProfile = ResolveEffectiveGenerativeGeologyProfile(family);
            WorldChunkCoordinate chunkCoord = WorldChunkCoordinate.FromWorldPosition(position, _runtimeChunkSize);
            bool hasMacroZone = streamingLayer == WorldStreamingLayer.LargeThreats || (family != null && family.ResolveContributesLargeThreatZone());
            WorldMacroZoneCoordinate macroZoneCoord = hasMacroZone
                ? WorldMacroZoneCoordinate.FromWorldPosition(position, _runtimeMacroZoneSize)
                : default;
            bool supportsFinalVariant = FamilySupportsFinalVariant(family);

            ScatterPlacement placement = new ScatterPlacement(
                ComposeKey(cellXIndex, cellZIndex, rule.ruleId),
                stableHash,
                family,
                rule,
                fieldSample.zone,
                fieldSample.biomeFamily,
                fieldSample.biomeProfile,
                fieldSample.resolvedPattern,
                ResolveBiomeContextProfile(fieldSample.biomeFamily, out _)?.label ?? "None",
                streamingLayer,
                geologyProfile,
                variant,
                supportsFinalVariant,
                heatmapChannel,
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
                cellXIndex,
                cellZIndex,
                chunkCoord,
                hasMacroZone,
                macroZoneCoord,
                position,
                rotation,
                scale);

            return new ScatterCandidate(placement, family, rule, heatmapChannel, heat, score);
        }

        private ScatterReconcileMetrics ReconcileInstances(bool captureProfiling)
        {
            long reconcileStartTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : 0L;
            Transform root = GetOrCreateRoot().transform;
            if (_activeInstances.Count == 0)
                ClearRootChildren(root);

            _removalBuffer.Clear();
            int removedCount = 0;
            int rebuiltCount = 0;
            int createdCount = 0;
            int reusedCount = 0;

            foreach (KeyValuePair<long, WorldProceduralProxyInstance> pair in _activeInstances)
            {
                if (_desiredPlacements.ContainsKey(pair.Key))
                    continue;

                _removalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                long key = _removalBuffer[i];
                if (!_activeInstances.TryGetValue(key, out WorldProceduralProxyInstance instance))
                    continue;

                if (instance != null)
                    DestroyProxyInstance(instance.gameObject);

                _activeInstances.Remove(key);
                removedCount++;
            }

            long cleanupEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : reconcileStartTimestamp;

            foreach (KeyValuePair<long, ScatterPlacement> pair in _desiredPlacements)
            {
                ScatterPlacement placement = pair.Value;
                WorldPrefabFamilyProfile.VariantEntry runtimeVariant = ResolvePlacementVariant(placement);
                bool finalVariantActive = ShouldUseFinalVariant(placement);
                if (_activeInstances.TryGetValue(pair.Key, out WorldProceduralProxyInstance instance) && instance != null)
                {
                    if (RequiresInstanceRebuild(instance, placement, runtimeVariant, finalVariantActive))
                    {
                        DestroyProxyInstance(instance.gameObject);
                        GameObject rebuilt = CreateScatterInstance(root, placement, runtimeVariant, finalVariantActive);
                        WorldProceduralProxyInstance rebuiltMetadata = rebuilt.GetComponent<WorldProceduralProxyInstance>();
                        if (rebuiltMetadata == null)
                            rebuiltMetadata = rebuilt.AddComponent<WorldProceduralProxyInstance>();

                        ApplyPlacement(rebuiltMetadata, placement, runtimeVariant, finalVariantActive);
                        ApplyGeneratedGeology(rebuiltMetadata, placement, finalVariantActive);
                        _activeInstances[pair.Key] = rebuiltMetadata;
                        rebuiltCount++;
                        continue;
                    }

                    ApplyPlacement(instance, placement, runtimeVariant, finalVariantActive);
                    ApplyGeneratedGeology(instance, placement, finalVariantActive);
                    reusedCount++;
                    continue;
                }

                GameObject go = CreateScatterInstance(root, placement, runtimeVariant, finalVariantActive);
                WorldProceduralProxyInstance metadata = go.GetComponent<WorldProceduralProxyInstance>();
                if (metadata == null)
                    metadata = go.AddComponent<WorldProceduralProxyInstance>();

                ApplyPlacement(metadata, placement, runtimeVariant, finalVariantActive);
                ApplyGeneratedGeology(metadata, placement, finalVariantActive);
                _activeInstances[pair.Key] = metadata;
                createdCount++;
            }

            long spawnEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : cleanupEndTimestamp;
            PublishFaunaRegistrySnapshot();
            long faunaEndTimestamp = captureProfiling ? Stopwatch.GetTimestamp() : spawnEndTimestamp;

            _debugReconcileRemovedCount = removedCount;
            _debugReconcileRebuiltCount = rebuiltCount;
            _debugReconcileCreatedCount = createdCount;
            _debugReconcileReusedCount = reusedCount;

            return new ScatterReconcileMetrics(
                removedCount,
                rebuiltCount,
                createdCount,
                reusedCount,
                cleanupEndTimestamp,
                spawnEndTimestamp,
                faunaEndTimestamp);
        }

        private void PublishFaunaRegistrySnapshot()
        {
            _faunaAnchorBuffer.Clear();

            if (faunaSpawnRegistry == null)
            {
                _debugPublishedFaunaAnchors = 0;
                _debugPublishedLargeThreatZones = 0;
                return;
            }

            int faunaAnchorCount = 0;
            int largeThreatZoneCount = 0;
            foreach (KeyValuePair<long, ScatterPlacement> pair in _desiredPlacements)
            {
                ScatterPlacement placement = pair.Value;
                bool largeThreatZone = IsLargeThreatZonePlacement(placement);
                bool faunaAnchor = largeThreatZone || IsFaunaAnchorPlacement(placement);
                if (!faunaAnchor)
                    continue;

                WorldFaunaSpawnRegistry.Anchor anchor = new WorldFaunaSpawnRegistry.Anchor
                {
                    runtimeKey = placement.Key,
                    position = placement.Position,
                    radius = ResolveFaunaAnchorRadius(placement),
                    chunkCoord = placement.ChunkCoord,
                    macroZoneCoord = placement.HasMacroZone
                        ? placement.MacroZoneCoord
                        : WorldMacroZoneCoordinate.FromWorldPosition(placement.Position, _runtimeMacroZoneSize),
                    streamingLayer = placement.StreamingLayer,
                    familyId = placement.Family != null ? placement.Family.familyId : "world.family.generic",
                    isLargeThreatZone = largeThreatZone
                };

                _faunaAnchorBuffer.Add(anchor);
                if (largeThreatZone)
                    largeThreatZoneCount++;
                else
                    faunaAnchorCount++;
            }

            faunaSpawnRegistry.ReplaceProceduralAnchors(_faunaAnchorBuffer);
            _debugPublishedFaunaAnchors = faunaAnchorCount;
            _debugPublishedLargeThreatZones = largeThreatZoneCount;
        }

        private bool TryRegisterDesiredPlacement(in ScatterPlacement placement, float now)
        {
            if (placement.Key == 0L)
                return false;

            if (proceduralStateRegistry != null && proceduralStateRegistry.IsPlacementSuppressed(placement.Key))
                return false;

            _desiredPlacements[placement.Key] = placement;
            _retainedPlacements[placement.Key] = placement;
            _placementLastSeenTimes[placement.Key] = now;
            return true;
        }

        private bool TryRegisterDesiredPlacement(in ScatterPlacement placement)
        {
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            return TryRegisterDesiredPlacement(placement, now);
        }

        private void RestoreRecentDesiredPlacements(Vector3 observerPosition, float now)
        {
            if (_activeInstances.Count == 0)
                return;

            float graceSeconds = Mathf.Max(0.25f, missingPlacementGraceSeconds);
            foreach (KeyValuePair<long, WorldProceduralProxyInstance> pair in _activeInstances)
            {
                long runtimeKey = pair.Key;
                if (_desiredPlacements.ContainsKey(runtimeKey))
                    continue;

                if (!_placementLastSeenTimes.TryGetValue(runtimeKey, out float lastSeenTime) ||
                    now - lastSeenTime > graceSeconds)
                {
                    continue;
                }

                if (!_retainedPlacements.TryGetValue(runtimeKey, out ScatterPlacement placement))
                    continue;

                if (!IsPlacementWithinResidency(placement, observerPosition))
                    continue;

                _desiredPlacements[runtimeKey] = placement;
            }
        }

        private bool IsPlacementWithinResidency(in ScatterPlacement placement, Vector3 observerPosition)
        {
            ResolveLayerRadii(placement.StreamingLayer, out _, out _, out float farRadius);
            if (farRadius <= 0f)
                return false;

            return (placement.Position - observerPosition).sqrMagnitude <= farRadius * farRadius;
        }

        private void ResolveLayerRadii(
            WorldStreamingLayer layer,
            out float nearRadius,
            out float midRadius,
            out float farRadius)
        {
            nearRadius = Mathf.Max(24f, cellSize * 2f);
            midRadius = Mathf.Max(nearRadius + cellSize, nearRadius * 1.8f);
            farRadius = Mathf.Max(midRadius + cellSize, midRadius * 1.5f);

            if (chunkStreamingProfile == null)
                return;

            WorldChunkStreamingProfile.LayerProfile layerProfile = chunkStreamingProfile.GetLayerProfileOrDefault(layer);
            nearRadius = Mathf.Max(24f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.35f, layerProfile.nearRadiusScale));
            midRadius = Mathf.Max(nearRadius + _runtimeCellSize, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.35f, layerProfile.midRadiusScale));
            farRadius = Mathf.Max(midRadius + _runtimeCellSize, chunkStreamingProfile.visualResidencyRadius * Mathf.Max(0.35f, layerProfile.farRadiusScale));
        }

        private int ResolveRuntimeBudget(int authoredBudget, WorldStreamingLayer layer, int minValue, int maxValue)
        {
            int clampedBudget = Mathf.Clamp(authoredBudget, minValue, maxValue);
            if (chunkStreamingProfile == null)
                return clampedBudget;

            WorldChunkStreamingProfile.LayerProfile layerProfile = chunkStreamingProfile.GetLayerProfileOrDefault(layer);
            float densityScale = Mathf.Lerp(0.7f, 1.45f, Mathf.Clamp01(layerProfile.maxActivationsPerTick / 24f));
            int scaledBudget = Mathf.RoundToInt(clampedBudget * densityScale);
            return Mathf.Clamp(scaledBudget, minValue, maxValue);
        }

        private void RefreshRuntimeStreamingSettings()
        {
            _runtimeCellSize = Mathf.Max(6f, cellSize);
            _runtimeRadiusCells = Mathf.Max(2, radiusCells);
            _runtimeChunkSize = 192f;
            _runtimeMacroZoneSize = 768f;

            if (chunkStreamingProfile != null)
            {
                _runtimeChunkSize = Mathf.Max(_runtimeCellSize, chunkStreamingProfile.chunkSizeMeters);
                _runtimeMacroZoneSize = Mathf.Max(_runtimeChunkSize, chunkStreamingProfile.macroZoneSizeMeters);
            }

            _debugRuntimeCellSize = _runtimeCellSize;
            _debugRuntimeRadiusCells = _runtimeRadiusCells;
            _debugRuntimeChunkSize = _runtimeChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeMacroZoneSize;
        }

        private WorldPrefabFamilyProfile.VariantEntry ResolvePlacementVariant(in ScatterPlacement placement)
        {
            return ResolveRuntimeVariant(placement.Family, placement.StableHash, ShouldUseFinalVariant(placement));
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

            return fit * Mathf.Max(0.15f, geologyProfile.compositionWeight);
        }

        private void ApplyGeneratedGeology(
            WorldProceduralProxyInstance metadata,
            in ScatterPlacement placement,
            bool finalVariantActive)
        {
            if (metadata == null)
                return;

            WorldGenerativeGeologyProfile geologyProfile = placement.GeologyProfile ?? ResolveEffectiveGenerativeGeologyProfile(placement.Family);
            if (placement.Family == null || !placement.Family.UsesGenerativeGeology() || geologyProfile == null)
            {
                WorldGenerativeGeologyService existingService = ResolveGenerativeGeologyService(false);
                if (existingService != null)
                    existingService.ClearGeneratedGeology(metadata.gameObject);
                return;
            }

            WorldGenerativeGeologyService service = ResolveGenerativeGeologyService(true);
            if (service == null)
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

            if (service.TryApplyGeneratedGeology(metadata.gameObject, request))
                _debugGeneratedGeologyCount++;
        }

        private WorldGenerativeGeologyService ResolveGenerativeGeologyService(bool createIfMissing)
        {
            if (generativeGeologyService != null)
                return generativeGeologyService;

            generativeGeologyService = FindAnyObjectByType<WorldGenerativeGeologyService>();
            if (generativeGeologyService != null || !createIfMissing)
                return generativeGeologyService;

            GameObject root = GetOrCreateRoot();
            Transform serviceTransform = root.transform.Find("__GENERATIVE_GEOLOGY_SERVICE");
            if (serviceTransform == null)
            {
                serviceTransform = new GameObject("__GENERATIVE_GEOLOGY_SERVICE").transform;
                serviceTransform.SetParent(root.transform, false);
            }

            generativeGeologyService = serviceTransform.GetComponent<WorldGenerativeGeologyService>();
            if (generativeGeologyService == null)
                generativeGeologyService = serviceTransform.gameObject.AddComponent<WorldGenerativeGeologyService>();

            return generativeGeologyService;
        }

        private static WorldGenerativeGeologyProfile ResolveEffectiveGenerativeGeologyProfile(WorldPrefabFamilyProfile family)
        {
            if (family == null || !family.UsesGenerativeGeology())
                return null;

            if (family.generativeGeologyProfile != null && family.generativeGeologyProfile.IsEnabled)
                return family.generativeGeologyProfile;

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
        }

        private static WorldGenerativeGeologyProfile GetOrCreateEmergencyGeologyProfile(
            ref WorldGenerativeGeologyProfile cachedProfile,
            string profileId,
            string label,
            WorldGenerativeGeologyProfile.ShapeArchetype archetype,
            WorldGenerativeGeologyProfile.CompositionMode compositionMode)
        {
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
        }

        private bool ShouldUseFinalVariant(in ScatterPlacement placement)
        {
            if (!placement.SupportsFinalVariant || playerTransform == null)
                return false;

            ResolveLayerRadii(placement.StreamingLayer, out float nearRadius, out _, out _);
            return (placement.Position - playerTransform.position).sqrMagnitude <= nearRadius * nearRadius;
        }

        private static bool RequiresInstanceRebuild(
            WorldProceduralProxyInstance instance,
            in ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            if (instance == null)
                return true;

            if (placement.Family != null && placement.Family.UsesGenerativeGeology())
            {
                return instance.ActiveStreamingLayer != placement.StreamingLayer;
            }

            string runtimeVariantId = runtimeVariant != null ? runtimeVariant.variantId : $"{placement.Family.familyId}.generated";
            return !string.Equals(instance.ActiveVariantId, runtimeVariantId, StringComparison.Ordinal)
                || instance.IsFinalVariantActive != finalVariantActive
                || instance.SupportsFinalVariant != placement.SupportsFinalVariant
                || instance.ActiveStreamingLayer != placement.StreamingLayer;
        }

        private static bool IsFaunaAnchorPlacement(in ScatterPlacement placement)
        {
            if (placement.Family == null)
                return false;

            return placement.Family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn
                || placement.Family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn
                || placement.StreamingLayer == WorldStreamingLayer.Fauna;
        }

        private static bool IsLargeThreatZonePlacement(in ScatterPlacement placement)
        {
            return placement.StreamingLayer == WorldStreamingLayer.LargeThreats
                || (placement.Family != null && placement.Family.ResolveContributesLargeThreatZone());
        }

        private static float ResolveFaunaAnchorRadius(in ScatterPlacement placement)
        {
            float familyRadius = placement.Family != null ? placement.Family.clusterRadiusMeters : 0f;
            float spacing = placement.Family != null && placement.Rule != null
                ? GetEffectiveSpacing(placement.Family, placement.Rule)
                : Mathf.Max(8f, familyRadius);
            return Mathf.Max(12f, Mathf.Max(familyRadius, spacing));
        }

        private bool CanAcceptCandidate(in ScatterCandidate candidate)
        {
            foreach (KeyValuePair<long, ScatterPlacement> pair in _desiredPlacements)
            {
                ScatterPlacement existing = pair.Value;
                float minDistance = ResolveRequiredDistance(candidate.Placement, existing);
                if (minDistance <= 0f)
                    continue;

                if ((candidate.Placement.Position - existing.Position).sqrMagnitude < minDistance * minDistance)
                    return false;
            }

            return true;
        }

        private static float ResolveRequiredDistance(in ScatterPlacement candidate, in ScatterPlacement existing)
        {
            WorldPrefabFamilyProfile.ScatterLayer candidateLayer = candidate.Family.scatterLayer;
            WorldPrefabFamilyProfile.ScatterLayer existingLayer = existing.Family.scatterLayer;
            float candidateSpacing = GetEffectiveSpacing(candidate.Family, candidate.Rule);
            float existingSpacing = GetEffectiveSpacing(existing.Family, existing.Rule);
            float maxSpacing = Mathf.Max(candidateSpacing, existingSpacing);

            if (candidateLayer == existingLayer)
            {
                return candidateLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(1.25f, maxSpacing * 0.52f),
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(3f, maxSpacing * 0.92f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Max(12f, maxSpacing),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Max(14f, maxSpacing * 1.08f),
                    _ => maxSpacing
                };
            }

            bool candidatePocket = IsPocket(candidate.Family.proceduralDomain);
            bool existingPocket = IsPocket(existing.Family.proceduralDomain);
            if (candidatePocket && existingPocket)
                return Mathf.Max(10f, maxSpacing * 1.35f);

            bool candidateStructure = IsStructure(candidate.Family.scatterLayer);
            bool existingStructure = IsStructure(existing.Family.scatterLayer);
            if (candidateStructure && existingStructure)
                return Mathf.Max(14f, maxSpacing * 0.88f);

            bool candidateSpawn = candidateLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            bool existingSpawn = existingLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            if ((candidateSpawn && existingStructure) || (candidateStructure && existingSpawn))
                return Mathf.Max(12f, Mathf.Max(candidateSpacing, existingSpacing) * 0.9f);

            return 0f;
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
            if (rule != null && rule.minSpacingOverrideMeters > 0f)
                return Mathf.Max(0.5f, rule.minSpacingOverrideMeters);

            return family != null ? Mathf.Max(0.5f, family.minSpacingMeters) : 1f;
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
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => groundCount < groundBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => clusterCount < clusterBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Structure => structureCount < structureBudget && GetWindowPlacementCount(candidate.Placement.CellX, candidate.Placement.CellZ, structureStride, _structureWindowCounts) < structureBudget,
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => spawnCount < spawnBudget && GetWindowPlacementCount(candidate.Placement.CellX, candidate.Placement.CellZ, spawnStride, _spawnWindowCounts) < spawnBudget,
                _ => false
            };
        }

        private bool TryInjectCandidate(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            in ScatterCandidate candidate,
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
            int currentWindowCount = GetWindowPlacementCount(
                candidate.Placement.CellX,
                candidate.Placement.CellZ,
                stride,
                layer == WorldPrefabFamilyProfile.ScatterLayer.Structure ? _structureWindowCounts : _spawnWindowCounts);
            if (currentWindowCount >= perWindowBudget)
                return false;

            int currentLayerCount = layer == WorldPrefabFamilyProfile.ScatterLayer.Structure ? structureCount : spawnCount;
            if (!HasPatternLayerGlobalBudget(pattern, biomeProfile, layer, currentLayerCount))
                return false;

            if (!CanAcceptPatternAccentBudget(
                    pattern,
                    biomeProfile,
                    candidate,
                    null,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    0,
                    structureCount,
                    spawnCount))
            {
                return false;
            }

            if (!CanAcceptCandidate(candidate))
                return false;

            if (!TryRegisterDesiredPlacement(candidate.Placement))
                return false;
            RegisterWindowPlacement(
                candidate.Placement.CellX,
                candidate.Placement.CellZ,
                stride,
                layer == WorldPrefabFamilyProfile.ScatterLayer.Structure ? _structureWindowCounts : _spawnWindowCounts);

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
            else if (layer == WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                spawnCount++;

            return true;
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
            if (!UsesPatternAccentQuotas(pattern) || candidate.Family == null)
                return true;

            if (candidate.Family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
            {
                if (clusterCount >= ResolvePatternLayerTargetMax(pattern, biomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster))
                    return false;

                WorldPrefabFamilyProfile.ClusterAccentRole role = GetClusterAccentRole(candidate.Family);
                if (role == WorldPrefabFamilyProfile.ClusterAccentRole.None)
                    return true;

                if (clusterCount < ResolvePatternClusterRatioStart(pattern))
                    return true;

                float maxRatio = ResolvePatternClusterAccentRoleMaxRatio(pattern, biomeProfile, role);
                if (maxRatio <= 0f)
                    return false;

                int roleCount = GetClusterAccentCount(clusterAccentCounts, role);
                int totalAfterPlacement = Mathf.Max(1, clusterCount + 1);
                int allowed = Mathf.Max(1, Mathf.CeilToInt(maxRatio * totalAfterPlacement));
                return roleCount < allowed;
            }

            if (candidate.Family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure)
            {
                if (structureCount >= ResolvePatternStructureTargetMax(pattern, biomeProfile))
                    return false;

                WorldPrefabFamilyProfile.StructureAccentRole role = GetStructureAccentRole(candidate.Family);
                int roleCount = GetStructureAccentCount(structureAccentCounts, role);
                int roleMax = ResolvePatternAccentRoleMax(pattern, biomeProfile, role);
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

            if (candidate.Family.scatterLayer != WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                return true;

            if (spawnCount >= ResolvePatternSpawnTargetMax(pattern, biomeProfile))
                return false;

            if (IsPredatorSpawnFamily(candidate.Family))
                return predatorSpawnCount < Mathf.Max(0, ResolvePatternPredatorSpawnMax(pattern, biomeProfile));

            if (IsPassiveSpawnFamily(candidate.Family))
                return passiveSpawnCount < Mathf.Max(ResolvePatternPassiveSpawnMin(pattern, biomeProfile), ResolvePatternSpawnTargetMax(pattern, biomeProfile));

            return true;
        }

        private static int GetWindowPlacementCount(int cellX, int cellZ, int stride, IReadOnlyDictionary<long, int> counts)
        {
            long key = ComposeWindowKey(cellX, cellZ, stride);
            return counts.TryGetValue(key, out int count) ? count : 0;
        }

        private static void RegisterWindowPlacement(int cellX, int cellZ, int stride, IDictionary<long, int> counts)
        {
            long key = ComposeWindowKey(cellX, cellZ, stride);
            counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        private static long ComposeWindowKey(int cellX, int cellZ, int stride)
        {
            int safeStride = Mathf.Max(1, stride);
            int windowX = Mathf.FloorToInt(cellX / (float)safeStride);
            int windowZ = Mathf.FloorToInt(cellZ / (float)safeStride);
            return ((long)windowX << 32) ^ (uint)windowZ ^ ((long)safeStride << 24);
        }

        private static void CountSeafloorSource(
            WorldProceduralFieldSampler.SeafloorSource source,
            ref int mapMagicCount,
            ref int raycastCount,
            ref int fallbackCount)
        {
            switch (source)
            {
                case WorldProceduralFieldSampler.SeafloorSource.MapMagicHeight:
                    mapMagicCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.SceneRaycast:
                    raycastCount++;
                    break;
                case WorldProceduralFieldSampler.SeafloorSource.FallbackSynthetic:
                    fallbackCount++;
                    break;
            }
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

        private static Dictionary<string, int>[] CreateLayerFamilyCounters()
        {
            return new[]
            {
                new Dictionary<string, int>(8),
                new Dictionary<string, int>(8),
                new Dictionary<string, int>(8),
                new Dictionary<string, int>(8)
            };
        }

        private static void FillOrderedCandidateBuffer(
            IDictionary<long, ScatterCandidate> source,
            List<ScatterCandidate> buffer)
        {
            buffer.Clear();
            if (source == null || source.Count == 0)
                return;

            if (source is Dictionary<long, ScatterCandidate> concreteDictionary)
            {
                foreach (KeyValuePair<long, ScatterCandidate> pair in concreteDictionary)
                    buffer.Add(pair.Value);
            }
            else
            {
                foreach (KeyValuePair<long, ScatterCandidate> pair in source)
                    buffer.Add(pair.Value);
            }

            buffer.Sort(ScatterCandidateComparer.Instance);
        }

        private void RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            _occupiedCellBuffer.Clear();
            foreach (KeyValuePair<long, ScatterPlacement> pair in _desiredPlacements)
            {
                if (pair.Value.Family == null || pair.Value.Family.scatterLayer != layer)
                    continue;

                _occupiedCellBuffer.Add(ComposeWindowKey(pair.Value.CellX, pair.Value.CellZ, 1));
            }
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
            return family != null && string.Equals(family.familyId, "family.creature.spawn.passive", StringComparison.Ordinal);
        }

        private static bool IsPredatorSpawnFamily(WorldPrefabFamilyProfile family)
        {
            return family != null && string.Equals(family.familyId, "family.creature.spawn.predator", StringComparison.Ordinal);
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
                : dominantRole.ToString();
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
                : dominantRole.ToString();
        }

        private static void RegisterStringCount(IDictionary<string, int> counters, string label)
        {
            if (counters == null)
                return;

            string safeLabel = string.IsNullOrWhiteSpace(label) ? "None" : label;
            counters.TryGetValue(safeLabel, out int count);
            counters[safeLabel] = count + 1;
        }

        private static void RegisterProfileCount(
            IDictionary<HectonBiomeMatrixProfile, int> counters,
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
            foreach (KeyValuePair<string, int> pair in counters[index])
            {
                if (pair.Value <= count)
                    continue;

                count = pair.Value;
                bestFamily = pair.Key;
            }

            return bestFamily;
        }

        private static string ResolveDominantCounter(
            IReadOnlyDictionary<string, int> counters,
            out int count)
        {
            count = 0;
            if (counters == null || counters.Count == 0)
                return "None";

            string bestLabel = "None";
            foreach (KeyValuePair<string, int> pair in counters)
            {
                if (pair.Value <= count)
                    continue;

                count = pair.Value;
                bestLabel = pair.Key;
            }

            return bestLabel;
        }

        private static HectonBiomeMatrixProfile ResolveDominantBiomeMatrixProfile(
            IReadOnlyDictionary<HectonBiomeMatrixProfile, int> counters,
            HectonBiomeMatrixProfile fallbackProfile)
        {
            if (counters == null || counters.Count == 0)
                return fallbackProfile;

            HectonBiomeMatrixProfile bestProfile = fallbackProfile;
            int bestCount = -1;
            int bestTieBreaker = int.MinValue;
            foreach (KeyValuePair<HectonBiomeMatrixProfile, int> pair in counters)
            {
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
            in WorldProceduralFieldSampler.FieldSample sample,
            int structureStride,
            int spawnStride,
            IDictionary<long, ScatterCandidate> groundCandidates,
            IDictionary<long, ScatterCandidate> clusterCandidates,
            IDictionary<long, ScatterCandidate> structureCandidates,
            IDictionary<long, ScatterCandidate> spawnCandidates,
            IDictionary<long, ScatterCandidate> clusterFertileCandidates,
            IDictionary<long, ScatterCandidate> clusterNestCandidates,
            IDictionary<long, ScatterCandidate> clusterResourceCandidates,
            IDictionary<long, ScatterCandidate> clusterShelterCandidates,
            IDictionary<long, ScatterCandidate> clusterHazardCandidates,
            IDictionary<long, ScatterCandidate> clusterDebrisCandidates,
            IDictionary<long, ScatterCandidate> clusterRockCandidates,
            IDictionary<long, ScatterCandidate> structureNaturalCandidates,
            IDictionary<long, ScatterCandidate> structureTechCandidates,
            IDictionary<long, ScatterCandidate> structureCaveCandidates,
            IDictionary<long, ScatterCandidate> structureBioCandidates,
            IDictionary<long, ScatterCandidate> passiveSpawnCandidates,
            IDictionary<long, ScatterCandidate> predatorSpawnCandidates)
        {
            if (!NeedsPreviewRescue(sample, candidate.Family))
                return;

            switch (candidate.Family.scatterLayer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    TrackWindowCandidate(candidate, 1, groundCandidates);
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    TrackWindowCandidate(candidate, 1, clusterCandidates);
                    switch (GetClusterAccentRole(candidate.Family))
                    {
                        case WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth:
                            TrackWindowCandidate(candidate, 1, clusterFertileCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest:
                            TrackWindowCandidate(candidate, 1, clusterNestCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket:
                            TrackWindowCandidate(candidate, 1, clusterResourceCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket:
                            TrackWindowCandidate(candidate, 1, clusterShelterCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket:
                            TrackWindowCandidate(candidate, 1, clusterHazardCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField:
                            TrackWindowCandidate(candidate, 1, clusterDebrisCandidates);
                            break;
                        case WorldPrefabFamilyProfile.ClusterAccentRole.RockCover:
                            TrackWindowCandidate(candidate, 1, clusterRockCandidates);
                            break;
                    }
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    TrackWindowCandidate(candidate, structureStride, structureCandidates);
                    switch (GetStructureAccentRole(candidate.Family))
                    {
                        case WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark:
                            TrackWindowCandidate(candidate, structureStride, structureNaturalCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.TechFragment:
                            TrackWindowCandidate(candidate, structureStride, structureTechCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.CaveRead:
                            TrackWindowCandidate(candidate, structureStride, structureCaveCandidates);
                            break;
                        case WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette:
                            TrackWindowCandidate(candidate, structureStride, structureBioCandidates);
                            break;
                    }
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    TrackWindowCandidate(candidate, spawnStride, spawnCandidates);
                    if (IsPassiveSpawnFamily(candidate.Family))
                        TrackWindowCandidate(candidate, spawnStride, passiveSpawnCandidates);
                    else if (IsPredatorSpawnFamily(candidate.Family))
                        TrackWindowCandidate(candidate, spawnStride, predatorSpawnCandidates);
                    break;
            }
        }

        private static void TrackWindowCandidate(
            in ScatterCandidate candidate,
            int stride,
            IDictionary<long, ScatterCandidate> windowCandidates)
        {
            long windowKey = ComposeWindowKey(candidate.Placement.CellX, candidate.Placement.CellZ, stride);
            if (!windowCandidates.TryGetValue(windowKey, out ScatterCandidate existing) || candidate.Score > existing.Score)
                windowCandidates[windowKey] = candidate;
        }

        private void InjectRescuePlacementsIfNeeded(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int clusterBudget,
            int structureStride,
            int spawnStride,
            int structureBudget,
            int spawnBudget,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            IDictionary<long, ScatterCandidate> groundCandidates,
            IDictionary<long, ScatterCandidate> clusterCandidates,
            IDictionary<long, ScatterCandidate> structureCandidates,
            IDictionary<long, ScatterCandidate> spawnCandidates,
            IDictionary<long, ScatterCandidate> clusterFertileCandidates,
            IDictionary<long, ScatterCandidate> clusterNestCandidates,
            IDictionary<long, ScatterCandidate> clusterResourceCandidates,
            IDictionary<long, ScatterCandidate> clusterShelterCandidates,
            IDictionary<long, ScatterCandidate> clusterHazardCandidates,
            IDictionary<long, ScatterCandidate> clusterDebrisCandidates,
            IDictionary<long, ScatterCandidate> clusterRockCandidates,
            IDictionary<long, ScatterCandidate> structureNaturalCandidates,
            IDictionary<long, ScatterCandidate> structureTechCandidates,
            IDictionary<long, ScatterCandidate> structureCaveCandidates,
            IDictionary<long, ScatterCandidate> structureBioCandidates,
            IDictionary<long, ScatterCandidate> passiveSpawnCandidates,
            IDictionary<long, ScatterCandidate> predatorSpawnCandidates)
        {
            int groundLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Ground;
            int minimumGroundCount = ResolveMinimumGroundPlacements(pattern, biomeProfile);
            if (layerPlacementCounts[groundLayerIndex] < minimumGroundCount)
            {
                int added = InjectGroundCandidates(
                    minimumGroundCount - layerPlacementCounts[groundLayerIndex],
                    groundCandidates,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                layerPlacementCounts[groundLayerIndex] += added;
            }

            int clusterLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster;
            if (biomeProfile != null && layerPlacementCounts[clusterLayerIndex] > 0)
            {
                int added = InjectPreferredClusterFamilyCandidates(
                    pattern,
                    biomeProfile,
                    Mathf.Max(1, clusterBudget),
                    clusterCandidates,
                    layerPlacementCounts,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                layerPlacementCounts[clusterLayerIndex] += added;
            }

            int minimumClusterCount = ResolveMinimumClusterPlacements(pattern, biomeProfile);
            if (layerPlacementCounts[clusterLayerIndex] < minimumClusterCount)
            {
                int added = UsesPatternAccentQuotas(pattern)
                    ? InjectPatternClusterAccentCandidates(
                        pattern,
                        biomeProfile,
                        Mathf.Max(1, clusterBudget),
                        minimumClusterCount,
                        clusterCandidates,
                        clusterFertileCandidates,
                        clusterNestCandidates,
                        clusterResourceCandidates,
                        clusterShelterCandidates,
                        clusterHazardCandidates,
                        clusterDebrisCandidates,
                        clusterRockCandidates,
                        layerPlacementCounts,
                        clusterAccentCounts,
                        structureAccentCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts)
                    : InjectClusterCandidates(
                        pattern,
                        biomeProfile,
                        minimumClusterCount - layerPlacementCounts[clusterLayerIndex],
                        Mathf.Max(1, clusterBudget),
                        clusterCandidates,
                        clusterAccentCounts,
                        structureAccentCounts,
                        layerPlacementCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts);
                layerPlacementCounts[clusterLayerIndex] += added;
            }

            int structureLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Structure;
            if (biomeProfile != null)
            {
                int added = InjectPreferredStructureFamilyCandidates(
                    pattern,
                    biomeProfile,
                    structureStride,
                    Mathf.Max(1, structureBudget),
                    structureCandidates,
                    layerPlacementCounts,
                    structureAccentCounts,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                layerPlacementCounts[structureLayerIndex] += added;
            }

            int minimumStructureCount = ResolveMinimumStructurePlacements(pattern, biomeProfile);
            if (layerPlacementCounts[structureLayerIndex] < minimumStructureCount)
            {
                int added = UsesPatternAccentQuotas(pattern)
                    ? InjectPatternStructureAccentCandidates(
                        pattern,
                        biomeProfile,
                        structureStride,
                        Mathf.Max(1, structureBudget),
                        minimumStructureCount,
                        structureCandidates,
                        structureNaturalCandidates,
                        structureTechCandidates,
                        structureCaveCandidates,
                        structureBioCandidates,
                        layerPlacementCounts,
                        structureAccentCounts,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts)
                    : InjectWindowCandidates(
                        minimumStructureCount - layerPlacementCounts[structureLayerIndex],
                        structureStride,
                        Mathf.Max(1, structureBudget),
                        structureCandidates,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts,
                        layerPlacementCounts,
                        structureAccentCounts,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        pattern,
                        biomeProfile,
                        WorldPrefabFamilyProfile.ScatterLayer.Structure);
                layerPlacementCounts[structureLayerIndex] += added;
            }

            int spawnLayerIndex = (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            if (biomeProfile != null)
            {
                int added = InjectPreferredSpawnFamilyCandidates(
                    pattern,
                    biomeProfile,
                    spawnStride,
                    Mathf.Max(1, spawnBudget),
                    spawnCandidates,
                    layerPlacementCounts,
                    structureAccentCounts,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
                layerPlacementCounts[spawnLayerIndex] += added;
            }

            int minimumSpawnCount = ResolveMinimumSpawnPlacements(pattern, biomeProfile);
            if (layerPlacementCounts[spawnLayerIndex] < minimumSpawnCount)
            {
                int added = UsesPatternAccentQuotas(pattern)
                    ? InjectPatternSpawnCandidates(
                        pattern,
                        biomeProfile,
                        spawnStride,
                        Mathf.Max(1, spawnBudget),
                        minimumSpawnCount,
                        spawnCandidates,
                        passiveSpawnCandidates,
                        predatorSpawnCandidates,
                        layerPlacementCounts,
                        structureAccentCounts,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts)
                    : InjectWindowCandidates(
                        minimumSpawnCount - layerPlacementCounts[spawnLayerIndex],
                        spawnStride,
                        Mathf.Max(1, spawnBudget),
                        spawnCandidates,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts,
                        layerPlacementCounts,
                        structureAccentCounts,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        pattern,
                        biomeProfile,
                        WorldPrefabFamilyProfile.ScatterLayer.Spawn);
                layerPlacementCounts[spawnLayerIndex] += added;
            }
        }

        private int InjectPatternClusterAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int perCellBudget,
            int targetClusterCount,
            IDictionary<long, ScatterCandidate> rescueCandidates,
            IDictionary<long, ScatterCandidate> fertileCandidates,
            IDictionary<long, ScatterCandidate> nestCandidates,
            IDictionary<long, ScatterCandidate> resourceCandidates,
            IDictionary<long, ScatterCandidate> shelterCandidates,
            IDictionary<long, ScatterCandidate> hazardCandidates,
            IDictionary<long, ScatterCandidate> debrisCandidates,
            IDictionary<long, ScatterCandidate> rockCandidates,
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
            if (rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];

            foreach (WorldPrefabFamilyProfile.ClusterAccentRole accentRole in GetPatternClusterAccentPriority(pattern))
            {
                int requiredCount = ResolvePatternClusterAccentMin(pattern, biomeProfile, accentRole);
                if (requiredCount <= 0)
                    continue;

                IDictionary<long, ScatterCandidate> sourceCandidates = GetClusterAccentCandidatePool(
                    accentRole,
                    fertileCandidates,
                    nestCandidates,
                    resourceCandidates,
                    shelterCandidates,
                    hazardCandidates,
                    debrisCandidates,
                    rockCandidates,
                    rescueCandidates);
                if (sourceCandidates == null || sourceCandidates.Count == 0)
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
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
            }

            int remaining = Mathf.Max(0, targetClusterCount - clusterCount);
            if (remaining > 0)
            {
                int[] updatedLayerCounts = (int[])layerPlacementCounts.Clone();
                updatedLayerCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster] = clusterCount;
                added += InjectClusterCandidates(
                    pattern,
                    biomeProfile,
                    remaining,
                    perCellBudget,
                    rescueCandidates,
                    clusterAccentCounts,
                    structureAccentCounts,
                    updatedLayerCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts);
            }

            return added;
        }

        private int InjectClusterAccentRoleCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            IDictionary<long, ScatterCandidate> rescueCandidates,
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
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (rescueCandidates == null || rescueCandidates.Count == 0 || requiredCount <= 0)
                return 0;

            List<ScatterCandidate> ordered = _clusterAccentOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int currentCount = GetClusterAccentCount(clusterAccentCounts, accentRole);
            int needed = Mathf.Max(0, requiredCount - currentCount);
            if (needed <= 0)
                return 0;

            int added = 0;
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            for (int i = 0; i < ordered.Count && needed > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (GetClusterAccentRole(candidate.Family) != accentRole)
                    continue;

                long cellKey = ComposeWindowKey(candidate.Placement.CellX, candidate.Placement.CellZ, 1);
                if (_occupiedCellBuffer.Contains(cellKey) || perCellBudget <= 0)
                    continue;

                if (!CanAcceptPatternAccentBudget(
                        pattern,
                        biomeProfile,
                        candidate,
                        clusterAccentCounts,
                        structureAccentCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        clusterCount,
                        structureCount,
                        spawnCount))
                {
                    continue;
                }

                if (!CanAcceptCandidate(candidate))
                    continue;

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
            IDictionary<long, ScatterCandidate> rescueCandidates,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int[] layerPlacementCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (targetCount <= 0 || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            List<ScatterCandidate> ordered = _clusterOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            int clusterCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            for (int i = 0; i < ordered.Count && added < targetCount; i++)
            {
                ScatterCandidate candidate = ordered[i];
                long cellKey = ComposeWindowKey(candidate.Placement.CellX, candidate.Placement.CellZ, 1);
                if (_occupiedCellBuffer.Contains(cellKey))
                    continue;

                if (perCellBudget <= 0)
                    continue;

                if (!CanAcceptPatternAccentBudget(
                        pattern,
                        biomeProfile,
                        candidate,
                        clusterAccentCounts,
                        structureAccentCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        clusterCount,
                        structureCount,
                        spawnCount))
                {
                    continue;
                }

                if (!CanAcceptCandidate(candidate))
                    continue;

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
            IDictionary<long, ScatterCandidate> rescueCandidates,
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
            if (biomeProfile?.preferredClusterFamilies == null || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

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

                added += InjectExactClusterFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    perCellBudget,
                    rescueCandidates,
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
            }

            return added;
        }

        private int InjectExactClusterFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile preferredFamily,
            int perCellBudget,
            IDictionary<long, ScatterCandidate> rescueCandidates,
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
            if (preferredFamily == null || rescueCandidates == null || rescueCandidates.Count == 0 || targetCount <= 0)
                return 0;

            int currentFamilyCount = CountPlacedFamily(preferredFamily, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            int needed = Mathf.Max(0, targetCount - currentFamilyCount);
            if (needed <= 0)
                return 0;

            List<ScatterCandidate> ordered = _exactClusterOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Cluster);

            for (int i = 0; i < ordered.Count && needed > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (!IsSameFamily(candidate.Family, preferredFamily))
                    continue;

                long cellKey = ComposeWindowKey(candidate.Placement.CellX, candidate.Placement.CellZ, 1);
                if (_occupiedCellBuffer.Contains(cellKey) || perCellBudget <= 0)
                    continue;

                if (!CanAcceptPatternAccentBudget(
                        pattern,
                        biomeProfile,
                        candidate,
                        clusterAccentCounts,
                        structureAccentCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        clusterCount,
                        structureCount,
                        spawnCount))
                {
                    continue;
                }

                if (!CanAcceptCandidate(candidate))
                    continue;

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
            IDictionary<long, ScatterCandidate> rescueCandidates,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (targetCount <= 0 || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            List<ScatterCandidate> ordered = _groundOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            RebuildOccupiedCellBuffer(WorldPrefabFamilyProfile.ScatterLayer.Ground);

            for (int i = 0; i < ordered.Count && added < targetCount; i++)
            {
                ScatterCandidate candidate = ordered[i];
                long cellKey = ComposeWindowKey(candidate.Placement.CellX, candidate.Placement.CellZ, 1);
                if (_occupiedCellBuffer.Contains(cellKey))
                    continue;

                if (!CanAcceptCandidate(candidate))
                    continue;

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
            IDictionary<long, ScatterCandidate> rescueCandidates,
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

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            for (int i = 0; i < ordered.Count && added < targetCount; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                    continue;

                added++;
            }

            return added;
        }

        private int InjectPreferredStructureFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            IDictionary<long, ScatterCandidate> rescueCandidates,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (biomeProfile?.preferredStructureFamilies == null || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

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

                added += InjectExactWindowFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure,
                    rescueCandidates,
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
            }

            return added;
        }

        private int InjectPreferredSpawnFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            IDictionary<long, ScatterCandidate> rescueCandidates,
            int[] layerPlacementCounts,
            int[] structureAccentCounts,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts)
        {
            if (biomeProfile?.preferredSpawnFamilies == null || rescueCandidates == null || rescueCandidates.Count == 0)
                return 0;

            int added = 0;
            int structureCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int spawnCount = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            for (int i = 0; i < biomeProfile.preferredSpawnFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferredFamily = biomeProfile.preferredSpawnFamilies[i];
                int targetCount = ResolvePreferredSpawnFamilyTarget(pattern, biomeProfile, preferredFamily, i);
                if (preferredFamily == null || targetCount <= 0)
                    continue;

                added += InjectExactWindowFamilyCandidates(
                    pattern,
                    biomeProfile,
                    preferredFamily,
                    stride,
                    perWindowBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn,
                    rescueCandidates,
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
            }

            return added;
        }

        private int InjectExactWindowFamilyCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile preferredFamily,
            int stride,
            int perWindowBudget,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            IDictionary<long, ScatterCandidate> rescueCandidates,
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
            if (preferredFamily == null || rescueCandidates == null || rescueCandidates.Count == 0 || targetCount <= 0)
                return 0;

            int currentFamilyCount = CountPlacedFamily(preferredFamily, layer);
            int needed = Mathf.Max(0, targetCount - currentFamilyCount);
            if (needed <= 0)
                return 0;

            List<ScatterCandidate> ordered = _windowOrderedCandidates;
            FillOrderedCandidateBuffer(rescueCandidates, ordered);

            int added = 0;
            for (int i = 0; i < ordered.Count && needed > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (!IsSameFamily(candidate.Family, preferredFamily))
                    continue;

                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                added++;
                needed--;
            }

            return added;
        }

        private int InjectPatternStructureAccentCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            int targetStructureCount,
            IDictionary<long, ScatterCandidate> rescueCandidates,
            IDictionary<long, ScatterCandidate> naturalCandidates,
            IDictionary<long, ScatterCandidate> techCandidates,
            IDictionary<long, ScatterCandidate> caveCandidates,
            IDictionary<long, ScatterCandidate> bioCandidates,
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

                IDictionary<long, ScatterCandidate> sourceCandidates = GetAccentRoleCandidatePool(
                    role,
                    naturalCandidates,
                    techCandidates,
                    caveCandidates,
                    bioCandidates,
                    rescueCandidates);
                added += InjectStructureAccentRoleCandidates(
                    pattern,
                    biomeProfile,
                    sourceCandidates,
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
            }

            added = structureCount - layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            int remaining = Mathf.Max(0, targetStructureCount - structureCount);
            if (remaining <= 0)
                return added;

            for (int i = 0; i < ordered.Count && remaining > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                int passiveUnused = 0;
                int predatorUnused = 0;
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                added++;
                remaining--;
            }

            return added;
        }

        private int InjectStructureAccentRoleCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            IDictionary<long, ScatterCandidate> rescueCandidates,
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
            for (int i = 0; i < ordered.Count && needed > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (GetStructureAccentRole(candidate.Family) != accentRole)
                    continue;

                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                added++;
                needed--;
            }

            return added;
        }

        private int InjectPatternSpawnCandidates(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            int stride,
            int perWindowBudget,
            int targetSpawnCount,
            IDictionary<long, ScatterCandidate> rescueCandidates,
            IDictionary<long, ScatterCandidate> passiveCandidates,
            IDictionary<long, ScatterCandidate> predatorCandidates,
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
            for (int i = 0; i < orderedPredator.Count && neededPredators > 0; i++)
            {
                ScatterCandidate candidate = orderedPredator[i];
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                neededPredators--;
            }

            int neededPassive = Mathf.Max(0, ResolvePatternPassiveSpawnMin(pattern, biomeProfile) - passiveSpawnCount);
            for (int i = 0; i < orderedPassive.Count && neededPassive > 0; i++)
            {
                ScatterCandidate candidate = orderedPassive[i];
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                neededPassive--;
            }

            added = spawnCount - layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            int remaining = Mathf.Max(0, targetSpawnCount - spawnCount);
            neededPredators = Mathf.Max(0, Mathf.Min(
                ResolvePatternPredatorSpawnMax(pattern, biomeProfile),
                targetSpawnCount / 3) - predatorSpawnCount);
            for (int i = 0; i < orderedPredator.Count && remaining > 0 && neededPredators > 0; i++)
            {
                ScatterCandidate candidate = orderedPredator[i];
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                added++;
                remaining--;
                neededPredators--;
            }

            for (int i = 0; i < ordered.Count && remaining > 0; i++)
            {
                ScatterCandidate candidate = ordered[i];
                if (!TryInjectCandidate(
                        pattern,
                        biomeProfile,
                        candidate,
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
                        layerBiomeCounts))
                {
                    continue;
                }

                added++;
                remaining--;
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

        private IDictionary<long, ScatterCandidate> GetAccentRoleCandidatePool(
            WorldPrefabFamilyProfile.StructureAccentRole role,
            IDictionary<long, ScatterCandidate> naturalCandidates,
            IDictionary<long, ScatterCandidate> techCandidates,
            IDictionary<long, ScatterCandidate> caveCandidates,
            IDictionary<long, ScatterCandidate> bioCandidates,
            IDictionary<long, ScatterCandidate> fallbackCandidates)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => naturalCandidates != null && naturalCandidates.Count > 0 ? naturalCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => techCandidates != null && techCandidates.Count > 0 ? techCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => caveCandidates != null && caveCandidates.Count > 0 ? caveCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => bioCandidates != null && bioCandidates.Count > 0 ? bioCandidates : fallbackCandidates,
                _ => fallbackCandidates
            };
        }

        private IDictionary<long, ScatterCandidate> GetClusterAccentCandidatePool(
            WorldPrefabFamilyProfile.ClusterAccentRole role,
            IDictionary<long, ScatterCandidate> fertileCandidates,
            IDictionary<long, ScatterCandidate> nestCandidates,
            IDictionary<long, ScatterCandidate> resourceCandidates,
            IDictionary<long, ScatterCandidate> shelterCandidates,
            IDictionary<long, ScatterCandidate> hazardCandidates,
            IDictionary<long, ScatterCandidate> debrisCandidates,
            IDictionary<long, ScatterCandidate> rockCandidates,
            IDictionary<long, ScatterCandidate> fallbackCandidates)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => fertileCandidates != null && fertileCandidates.Count > 0 ? fertileCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => nestCandidates != null && nestCandidates.Count > 0 ? nestCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => resourceCandidates != null && resourceCandidates.Count > 0 ? resourceCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => shelterCandidates != null && shelterCandidates.Count > 0 ? shelterCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => hazardCandidates != null && hazardCandidates.Count > 0 ? hazardCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => debrisCandidates != null && debrisCandidates.Count > 0 ? debrisCandidates : fallbackCandidates,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => rockCandidates != null && rockCandidates.Count > 0 ? rockCandidates : fallbackCandidates,
                _ => fallbackCandidates
            };
        }

        private static WorldPrefabFamilyProfile.StructureAccentRole[] GetPatternAccentPriority(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment
                },
                WorldProceduralPattern.ReefNavigation => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment
                },
                WorldProceduralPattern.IndustrialService => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                WorldProceduralPattern.BrineToxic => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                WorldProceduralPattern.VolcanicPressure => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                WorldProceduralPattern.RiftHazard => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                WorldProceduralPattern.AbyssSparse => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                WorldProceduralPattern.LandmarkCorridor => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                },
                _ => new[]
                {
                    WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark,
                    WorldPrefabFamilyProfile.StructureAccentRole.TechFragment,
                    WorldPrefabFamilyProfile.StructureAccentRole.CaveRead,
                    WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette
                }
            };
        }

        private static WorldPrefabFamilyProfile.ClusterAccentRole[] GetPatternClusterAccentPriority(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
                },
                WorldProceduralPattern.ReefNavigation => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest
                },
                WorldProceduralPattern.SedimentResources => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField
                },
                WorldProceduralPattern.IndustrialService => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
                },
                WorldProceduralPattern.BrineToxic => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                    WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
                },
                WorldProceduralPattern.VolcanicPressure => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                    WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
                },
                WorldProceduralPattern.RiftHazard => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
                },
                WorldProceduralPattern.AbyssSparse => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
                },
                WorldProceduralPattern.LandmarkCorridor => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket
                },
                _ => new[]
                {
                    WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket,
                    WorldPrefabFamilyProfile.ClusterAccentRole.RockCover
                }
            };
        }

        private float ResolvePatternClusterAccentRoleMaxRatio(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            float baseValue = profile != null ? profile.GetClusterAccentMaxRatio(role) : 1f;
            return Mathf.Clamp01(baseValue + GetMatrixBiomeClusterAccentRatioDelta(biomeProfile, role));
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
            return Mathf.Max(0, value + GetMatrixBiomeClusterAccentMinDelta(biomeProfile, role));
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
            return Mathf.Max(0, value + GetMatrixBiomeStructureAccentMinDelta(biomeProfile, role) + GetServiceWaterStructureRoleMinDelta(pattern, biomeProfile, role));
        }

        private int ResolvePatternAccentRoleMax(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            int minValue = ResolvePatternAccentRoleMin(pattern, biomeProfile, role);
            int value = profile != null ? profile.GetStructureAccentMax(role) : 0;
            return Mathf.Max(minValue, value + GetMatrixBiomeStructureAccentMaxDelta(biomeProfile, role) + GetServiceWaterStructureRoleMaxDelta(pattern, biomeProfile, role));
        }

        private static float ResolveEffectiveMinHeat(
            WorldProceduralPlacementRule rule,
            WorldPrefabFamilyProfile family,
            in WorldProceduralFieldSampler.FieldSample sample)
        {
            float value = rule != null ? Mathf.Clamp01(rule.minHeatmapValue) : 0f;
            if (!NeedsPreviewRescue(sample, family))
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
            float value = rule != null ? Mathf.Max(0.1f, rule.densityScale) : 1f;
            if (!NeedsPreviewRescue(sample, family))
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
            in ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            Transform transform = metadata.transform;
            transform.position = placement.Position;
            transform.rotation = placement.Rotation;
            transform.localScale = Vector3.one * placement.Scale;
            metadata.ConfigureScatter(
                placement.Family,
                placement.Rule,
                placement.Zone,
                runtimeVariant != null ? runtimeVariant.variantId : $"{placement.Family.familyId}.generated",
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
                placement.BiomeProfileLabel,
                placement.BiomeFamilyLabel,
                placement.PatternLabel,
                placement.BiomeContextLabel,
                placement.CellX,
                placement.CellZ,
                placement.Key,
                placement.StreamingLayer,
                placement.ChunkCoord,
                placement.HasMacroZone,
                placement.MacroZoneCoord,
                placement.SupportsFinalVariant,
                finalVariantActive);
        }

        private static GameObject CreateScatterInstance(
            Transform parent,
            in ScatterPlacement placement,
            WorldPrefabFamilyProfile.VariantEntry runtimeVariant,
            bool finalVariantActive)
        {
            GameObject prefab = runtimeVariant != null ? runtimeVariant.prefab : null;
            GameObject instance = prefab != null
                ? Instantiate(prefab, placement.Position, placement.Rotation, parent)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            if (prefab == null)
            {
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                        Destroy(collider);
                    else
                        DestroyImmediate(collider);
                }

                instance.transform.SetParent(parent, false);
            }

            string layerLabel = placement.StreamingLayer.ToString();
            string finalLabel = finalVariantActive ? "FINAL" : "PROXY";
            instance.name = $"SCATTER_{layerLabel}_{finalLabel}_{placement.Family.familyId}_{placement.CellX}_{placement.CellZ}";
            return instance;
        }

        private static void DestroyProxyInstance(GameObject instance)
        {
            if (instance == null)
                return;

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
            float radiusT = StableRandom01(stableHash, stableHash >> 4, family.familyId);
            float radius = baseRadius * Mathf.Lerp(0.18f, 1f, radiusT);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return origin + offset;
        }

        private static float ResolveScaleMultiplier(WorldPrefabFamilyProfile.VariantEntry variant, int stableHash)
        {
            Vector2 range = variant != null ? variant.uniformScaleRange : new Vector2(0.9f, 1.1f);
            float min = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            if (Mathf.Approximately(min, max))
                return min;

            float t = Mathf.Abs((stableHash % 1000) / 999f);
            return Mathf.Lerp(min, max, t);
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

        private static WorldPrefabFamilyProfile.VariantEntry ResolveRuntimeVariant(
            WorldPrefabFamilyProfile family,
            int stableHash,
            bool preferFinalVariant)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return null;

            if (preferFinalVariant)
            {
                WorldPrefabFamilyProfile.VariantEntry finalVariant =
                    ResolveVariantByPredicate(family, stableHash, variant => variant != null && variant.finalReady && !variant.proxyOnly);
                if (finalVariant != null)
                    return finalVariant;
            }

            WorldPrefabFamilyProfile.VariantEntry proxyVariant =
                ResolveVariantByPredicate(family, stableHash, variant => variant != null && (variant.proxyOnly || !variant.finalReady));
            if (proxyVariant != null)
                return proxyVariant;

            return ResolveVariantByPredicate(family, stableHash, variant => variant != null);
        }

        private static WorldPrefabFamilyProfile.VariantEntry ResolveVariantByPredicate(
            WorldPrefabFamilyProfile family,
            int stableHash,
            Predicate<WorldPrefabFamilyProfile.VariantEntry> predicate)
        {
            int totalWeight = 0;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant == null)
                    continue;

                if (predicate == null || predicate(variant))
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

                if (predicate != null && !predicate(variant))
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
            return GetPatternBudgetScale(pattern, layer) * GetBiomeContextBudgetScale(biomeFamily, layer);
        }

        private float GetPatternBudgetScale(
            WorldProceduralPattern pattern,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralPatternProfile profile = ResolvePatternProfile(pattern, out _);
            return profile != null ? profile.GetBudgetScale(layer) : 1f;
        }

        private float GetBiomeContextBudgetScale(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            WorldProceduralBiomeFamilyContextProfile context = ResolveBiomeContextProfile(biomeFamily, out _);
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

        private float GetBiomeContextBonus(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0f;

            WorldProceduralBiomeFamilyContextProfile context = ResolveBiomeContextProfile(biomeFamily, out _);
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
            return bonus;
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

        private static int GetPreferredFamilyIndex(
            WorldPrefabFamilyProfile[] preferredFamilies,
            WorldPrefabFamilyProfile family)
        {
            if (preferredFamilies == null || family == null || string.IsNullOrWhiteSpace(family.familyId))
                return -1;

            for (int i = 0; i < preferredFamilies.Length; i++)
            {
                WorldPrefabFamilyProfile preferred = preferredFamilies[i];
                if (preferred == null || string.IsNullOrWhiteSpace(preferred.familyId))
                    continue;

                if (string.Equals(preferred.familyId, family.familyId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool IsSameFamily(WorldPrefabFamilyProfile a, WorldPrefabFamilyProfile b)
        {
            if (a == null || b == null || string.IsNullOrWhiteSpace(a.familyId) || string.IsNullOrWhiteSpace(b.familyId))
                return false;

            return string.Equals(a.familyId, b.familyId, StringComparison.OrdinalIgnoreCase);
        }

        private int CountPlacedFamily(
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                return 0;

            int count = 0;
            foreach (KeyValuePair<long, ScatterPlacement> pair in _desiredPlacements)
            {
                ScatterPlacement placement = pair.Value;
                if (placement.Family == null || placement.Family.scatterLayer != layer)
                    continue;

                if (IsSameFamily(placement.Family, family))
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

        private static float GetDepthDomainScale(
            float depthMeters,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            WorldPrefabFamilyProfile.ProceduralDomain domain = family.proceduralDomain;
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => EvaluateDepthBand(depthMeters, 25f, 90f, 180f, 1.14f, 0.82f, 0.34f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => EvaluateDepthBand(depthMeters, 35f, 110f, 220f, 1.08f, 0.78f, 0.36f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => EvaluateDepthBand(depthMeters, 50f, 180f, 420f, 1.06f, 0.94f, 0.68f, 0.42f),
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => EvaluateDepthBand(depthMeters, 60f, 180f, 420f, 1.02f, 0.92f, 0.72f, 0.48f),
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => EvaluateDepthBand(depthMeters, 40f, 180f, 600f, 0.92f, 1.02f, 1.14f, 1.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => EvaluateDepthBand(depthMeters, 40f, 180f, 700f, 0.94f, 1.04f, 1.16f, 1.22f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => EvaluateDepthBand(depthMeters, 60f, 220f, 800f, 0.96f, 1.02f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => EvaluateDepthBand(depthMeters, 50f, 220f, 800f, 0.98f, 1.04f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => EvaluateDepthBand(depthMeters, 60f, 220f, 900f, 0.96f, 1.02f, 1.10f, 1.14f),
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn
                    ? EvaluateSpawnDepthScale(depthMeters, family.primaryPattern)
                    : 1f,
                _ => 1f
            };
        }

        private static float EvaluateSpawnDepthScale(float depthMeters, WorldProceduralPattern primaryPattern)
        {
            return primaryPattern switch
            {
                WorldProceduralPattern.FertileShallows or WorldProceduralPattern.ReefNavigation
                    => EvaluateDepthBand(depthMeters, 35f, 120f, 280f, 1.08f, 0.94f, 0.66f, 0.4f),
                WorldProceduralPattern.BrineToxic
                    => EvaluateDepthBand(depthMeters, 50f, 180f, 650f, 0.78f, 0.92f, 1.04f, 1.08f),
                WorldProceduralPattern.VolcanicPressure
                    => EvaluateDepthBand(depthMeters, 60f, 200f, 700f, 0.74f, 0.94f, 1.08f, 1.14f),
                WorldProceduralPattern.RiftHazard or WorldProceduralPattern.IndustrialService
                    => EvaluateDepthBand(depthMeters, 60f, 180f, 600f, 0.86f, 1.0f, 1.12f, 1.18f),
                _ => EvaluateDepthBand(depthMeters, 40f, 160f, 420f, 1.0f, 0.96f, 0.88f, 0.72f)
            };
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
            if (depthMeters <= nearEnd)
                return shallowScale;

            if (depthMeters <= midEnd)
                return Mathf.Lerp(shallowScale, midScale, Mathf.InverseLerp(nearEnd, midEnd, depthMeters));

            if (depthMeters <= deepEnd)
                return Mathf.Lerp(midScale, deepScale, Mathf.InverseLerp(midEnd, deepEnd, depthMeters));

            return abyssScale;
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

        private static long ComposeKey(int cellX, int cellZ, string ruleId)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < ruleId.Length; i++)
                    hash = (hash * 31) + ruleId[i];

                return ((long)cellX << 32) ^ (uint)cellZ ^ ((long)hash << 1);
            }
        }

        private static int ComputeStableHash(string ruleId, int cellX, int cellZ)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < ruleId.Length; i++)
                    hash = (hash * 31) + ruleId[i];

                hash = (hash * 31) + cellX;
                hash = (hash * 31) + cellZ;
                return hash;
            }
        }

        private static float StableRandom01(int cellX, int cellZ, string salt)
        {
            int hash = ComputeStableHash(salt, cellX, cellZ);
            uint normalized = (uint)(hash & 0x7fffffff);
            return normalized / (float)int.MaxValue;
        }

        private static GameObject GetOrCreateRoot()
        {
            GameObject root = GameObject.Find(ScatterRootName);
            if (root == null)
                root = new GameObject(ScatterRootName);

            return root;
        }

        private static void ClearRootChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private WorldProceduralPatternProfile ResolvePatternProfile(WorldProceduralPattern pattern, out bool usedFallbackPatternProfile)
        {
            if (patternCatalog != null)
            {
                WorldProceduralPatternProfile profile = patternCatalog.GetProfile(pattern, out usedFallbackPatternProfile);
                if (profile != null)
                    return profile;
            }

            usedFallbackPatternProfile = true;
            return GetEmergencyPatternProfile();
        }

        private WorldProceduralBiomeFamilyContextProfile ResolveBiomeContextProfile(
            Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
            out bool usedFallbackBiomeContextProfile)
        {
            if (biomeContextCatalog != null)
            {
                WorldProceduralBiomeFamilyContextProfile profile = biomeContextCatalog.GetProfile(biomeFamily, out usedFallbackBiomeContextProfile);
                if (profile != null)
                    return profile;
            }

            usedFallbackBiomeContextProfile = true;
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
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                    player = GameObject.Find("Player");

                if (player != null)
                    playerTransform = player.transform;
            }

            if (fieldSampler == null)
                fieldSampler = FindAnyObjectByType<WorldProceduralFieldSampler>();

            if (proceduralFillDirector == null)
                proceduralFillDirector = FindAnyObjectByType<WorldProceduralFillDirector>();

            if (faunaSpawnRegistry == null)
                faunaSpawnRegistry = FindAnyObjectByType<WorldFaunaSpawnRegistry>();

            if (proceduralStateRegistry == null)
                proceduralStateRegistry = FindAnyObjectByType<WorldProceduralStateRegistry>();

            if (generativeGeologyService == null)
                generativeGeologyService = FindAnyObjectByType<WorldGenerativeGeologyService>();

            if (faunaSpawnRegistry != null)
                faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
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

        private readonly struct ScatterPlacement
        {
            public ScatterPlacement(
                long key,
                int stableHash,
                WorldPrefabFamilyProfile family,
                WorldProceduralPlacementRule rule,
                WorldZoneAnchor zone,
                Hecton8.Environment.HectonBiomeFamilyProfile biomeFamily,
                HectonBiomeMatrixProfile biomeProfile,
                WorldProceduralPattern pattern,
                string biomeContextLabel,
                WorldStreamingLayer streamingLayer,
                WorldGenerativeGeologyProfile geologyProfile,
                WorldPrefabFamilyProfile.VariantEntry variant,
                bool supportsFinalVariant,
                string heatmapChannel,
                float heat,
                WorldProceduralFieldSampler.SeafloorSource fieldSource,
                float seafloorHeight,
                float depthMeters,
                float slopeDegrees,
                float curvature,
                float caveProximity,
                float ridgeSignal,
                float canyonSignal,
                float compositionPotential,
                int cellX,
                int cellZ,
                WorldChunkCoordinate chunkCoord,
                bool hasMacroZone,
                WorldMacroZoneCoordinate macroZoneCoord,
                Vector3 position,
                Quaternion rotation,
                float scale)
            {
                Key = key;
                StableHash = stableHash;
                Family = family;
                Rule = rule;
                Zone = zone;
                BiomeFamily = biomeFamily;
                BiomeProfile = biomeProfile;
                Pattern = pattern;
                BiomeContextLabel = biomeContextLabel;
                StreamingLayer = streamingLayer;
                GeologyProfile = geologyProfile;
                Variant = variant;
                SupportsFinalVariant = supportsFinalVariant;
                HeatmapChannel = heatmapChannel;
                Heat = heat;
                FieldSource = fieldSource;
                SeafloorHeight = seafloorHeight;
                DepthMeters = depthMeters;
                SlopeDegrees = slopeDegrees;
                Curvature = curvature;
                CaveProximity = caveProximity;
                RidgeSignal = ridgeSignal;
                CanyonSignal = canyonSignal;
                CompositionPotential = compositionPotential;
                CellX = cellX;
                CellZ = cellZ;
                ChunkCoord = chunkCoord;
                HasMacroZone = hasMacroZone;
                MacroZoneCoord = macroZoneCoord;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public long Key { get; }
            public int StableHash { get; }
            public WorldPrefabFamilyProfile Family { get; }
            public WorldProceduralPlacementRule Rule { get; }
            public WorldZoneAnchor Zone { get; }
            public Hecton8.Environment.HectonBiomeFamilyProfile BiomeFamily { get; }
            public HectonBiomeMatrixProfile BiomeProfile { get; }
            public WorldProceduralPattern Pattern { get; }
            public string BiomeContextLabel { get; }
            public WorldStreamingLayer StreamingLayer { get; }
            public WorldGenerativeGeologyProfile GeologyProfile { get; }
            public WorldPrefabFamilyProfile.VariantEntry Variant { get; }
            public bool SupportsFinalVariant { get; }
            public string HeatmapChannel { get; }
            public float Heat { get; }
            public WorldProceduralFieldSampler.SeafloorSource FieldSource { get; }
            public float SeafloorHeight { get; }
            public float DepthMeters { get; }
            public float SlopeDegrees { get; }
            public float Curvature { get; }
            public float CaveProximity { get; }
            public float RidgeSignal { get; }
            public float CanyonSignal { get; }
            public float CompositionPotential { get; }
            public int CellX { get; }
            public int CellZ { get; }
            public WorldChunkCoordinate ChunkCoord { get; }
            public bool HasMacroZone { get; }
            public WorldMacroZoneCoordinate MacroZoneCoord { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public float Scale { get; }
            public string BiomeProfileLabel => BiomeProfile != null ? BiomeProfile.biomeName : "None";
            public string BiomeFamilyLabel => BiomeFamily != null ? BiomeFamily.familyLabel : "None";
            public string PatternLabel => Pattern.ToString();
        }

        private readonly struct ScatterCandidate
        {
            public ScatterCandidate(
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

            public ScatterPlacement Placement { get; }
            public WorldPrefabFamilyProfile Family { get; }
            public WorldProceduralPlacementRule Rule { get; }
            public string HeatmapChannel { get; }
            public float Heat { get; }
            public float Score { get; }
        }

        private sealed class ScatterCandidateComparer : IComparer<ScatterCandidate>
        {
            public static readonly ScatterCandidateComparer Instance = new ScatterCandidateComparer();

            public int Compare(ScatterCandidate x, ScatterCandidate y)
            {
                return y.Score.CompareTo(x.Score);
            }
        }
    }
}
