using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic environmental-envelope spawner for harvestable resource nodes.
    /// Uses AUP sector quantization, MapMagic seabed queries, cached thermal/slope envelopes,
    /// and voxel-density rejection so resources are placed by conditions instead of biome labels.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4042)]
    public sealed partial class ResourceDistributionDirector : MonoBehaviour, ISlowTickable, IRandomEventListener, IBrineFluidDensityReadModel, IGlobalRegistryHotSwapListener
    {
        private const int DefaultSectorSizeMeters = 128;
        private const int DefaultMaxPendingSpawnRequests = 1024;
        private const int DefaultPoolWarmupFloor = 64;
        private const int InitialMetamorphismCapacity = 128;
        private const int GhostProxySnapBatchCapacity = 32;
        private const int TransientRuntimeSectorReserve = 16;
        private const SystemID MetamorphismJobOwnerSystemId = SystemID.WorldResourceSpawnerRuntime;
        private const float GameSecondsPerDay = 86400f;
        private const float DefaultSlopeSampleDistanceMeters = 4f;
        private const float DefaultVoxelSolidThreshold = 0.08f;
        private const float DefaultSectorMarginMeters = 2f;
        private const float DefaultBrinePoolRadiusMinMeters = 12f;
        private const float DefaultBrinePoolRadiusMaxMeters = 28f;
        private const float DefaultBrinePoolThicknessMinMeters = 4f;
        private const float DefaultBrinePoolThicknessMaxMeters = 18f;
        private const float DefaultBrinePoolMinimumDepthMeters = 2500f;
        private const float DefaultBrinePoolMinimumLipMeters = 2.5f;
        private const float DefaultBrinePoolToxicityIntensity = 0.92f;
        private const float DefaultBrinePoolHazardVisorBias = 0.8f;
        private const float DefaultBrinePoolFluidDensityKgPerCubicMeter = 1250f;
        private const float DefaultUpwellingRespawnRate = 0.05f;
        private const float DefaultMagmaVentLifetimeSeconds = 6f;
        private const float DefaultMeteorImpactIntervalSeconds = 600f;
        private const float DefaultMeteorImpactSearchRadiusMeters = 96f;
        private const float DefaultMeteorImpactCraterRadiusMeters = 5f;
        private const float DefaultMeteoriteRadiationIntensity = 0.85f;
        private const float DefaultMeteoriteRadiationRadiusMeters = 30f;
        private const float DefaultMeteoriteRadiationVisorBias = 1.35f;
        private const float DefaultWaterSurfaceLevelY = 14.02f;
        private const float DefaultPressureMetamorphismDepthMeters = 3500f;
        private const float DefaultPressureMetamorphismDays = 5f;
        private const string CarbonMetamorphismStableId = "resource.node.carbon_graphite_nodule";
        private const string PressureDiamondStableId = "resource.node.pressure_diamond";
        private const string ThermalDiamondStableId = "resource.node.thermal_diamond";
        private const string VoidGlassMeteoriteStableId = "resource.node.void_glass_meteorite";
        private const string DeepMantleGeodeStableId = "resource.node.deep_mantle_geode";
        private static readonly int CarbonMetamorphismStableHashId = LocHash.Compute(CarbonMetamorphismStableId);
        private static readonly int PressureDiamondStableHashId = LocHash.Compute(PressureDiamondStableId);
        private static readonly int ThermalDiamondStableHashId = LocHash.Compute(ThermalDiamondStableId);
        private static readonly int VoidGlassMeteoriteStableHashId = LocHash.Compute(VoidGlassMeteoriteStableId);
        private static readonly int DeepMantleGeodeStableHashId = LocHash.Compute(DeepMantleGeodeStableId);
        private const int BrinePoolSeedSalt = unchecked((int)0x4252494E);
        private const int BrinePoolHazardIdSalt = unchecked((int)0x52494E45);
        private const int MagmaVentSeedSalt = unchecked((int)0x56454E54);
        private const int MeteorImpactSeedSalt = unchecked((int)0x4D45544F);
        private const int MeteorRadiationHazardIdSalt = unchecked((int)0x524144);

        private static ResourceDistributionDirector s_activeRuntimeInstance;

        internal static ResourceDistributionDirector ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_activeRuntimeInstance = null;
        }

        private struct BrinePoolState
        {
            public byte IsValid;
            public byte HazardRegistered;
            public int HazardZoneId;
            public uint StableSeed;
            public Vector3 Center;
            public float RadiusMeters;
            public float BottomHeight;
            public float SurfaceHeight;
            public float ToxicityIntensity;
            public float FluidDensityKgPerCubicMeter;
        }

        private sealed class SectorState
        {
            public readonly int PoolIndex;
            public int2 Coordinates;
            public readonly List<ResourceNode> ActiveNodes;
            public bool SpawnEnvelopeQueued;
            public BrinePoolState BrinePool;
            public bool IsLeased;

            public SectorState(int poolIndex, int initialCapacity)
            {
                PoolIndex = poolIndex;
                // COLD ALLOC: List<ResourceNode>[initialCapacity] — live sector resource node registry — owner: ResourceDistributionDirector
                ActiveNodes = new List<ResourceNode>(initialCapacity);
                Coordinates = default;
                SpawnEnvelopeQueued = false;
                BrinePool = default;
                IsLeased = false;
            }

            public void Lease(int2 coordinates)
            {
                Coordinates = coordinates;
                ActiveNodes.Clear();
                SpawnEnvelopeQueued = false;
                BrinePool = default;
                IsLeased = true;
            }

            public void Release()
            {
                Coordinates = default;
                ActiveNodes.Clear();
                SpawnEnvelopeQueued = false;
                BrinePool = default;
                IsLeased = false;
            }
        }

        private struct SpawnRequest
        {
            public long SectorKey;
            public int TemplateIndex;
            public Vector3 RuntimePosition;
            public Quaternion Rotation;
            public float YawDegrees;
            public float SurfaceOffsetMeters;
            public uint StableSeed;
            public ulong TombstoneId;
            public byte RequiresGhostProxySnap;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PressureMetamorphismSample
        {
            [FieldOffset(0)]
            public float DepthMeters;
            [FieldOffset(4)]
            public float ProgressSeconds;
            [FieldOffset(8)]
            public int TemplateHashId;
            [FieldOffset(12)]
            public byte Active;
            [FieldOffset(13)]
            public byte _pad0;
            [FieldOffset(14)]
            public ushort _pad1;
            [FieldOffset(16)]
            public float ResultProgressSeconds;
            [FieldOffset(20)]
            public byte TransformToDiamond;
            [FieldOffset(21)]
            public byte _pad2;
            [FieldOffset(22)]
            public ushort _pad3;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct PressureMetamorphismJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<PressureMetamorphismSample> Workspace;
            public float DeltaSeconds;
            public float DepthThresholdMeters;
            public float RequiredSeconds;
            public int CarbonTemplateHashId;

            public void Execute(int index)
            {
                PressureMetamorphismSample sample = Workspace[index];
                sample.ResultProgressSeconds = math.max(0f, sample.ProgressSeconds);
                sample.TransformToDiamond = 0;

                bool eligible = sample.Active != 0 &&
                                sample.TemplateHashId == CarbonTemplateHashId &&
                                sample.DepthMeters > DepthThresholdMeters &&
                                RequiredSeconds > 0f;
                if (!eligible)
                {
                    Workspace[index] = sample;
                    return;
                }

                sample.ResultProgressSeconds = math.min(RequiredSeconds, sample.ResultProgressSeconds + math.max(0f, DeltaSeconds));
                sample.TransformToDiamond = sample.ResultProgressSeconds >= RequiredSeconds ? (byte)1 : (byte)0;
                Workspace[index] = sample;
            }
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("Authored resource-node templates consumed by the environmental-envelope spawner.")]
        private ResourceNodeTemplate[] resourceTemplates;

        [SerializeField]
        [Tooltip("Optional explicit template spawned by flash-freeze crystallization. If empty, the director resolves resource.node.thermal_diamond from resourceTemplates.")]
        private ResourceNodeTemplate thermalDiamondTemplate;

        [SerializeField]
        [Tooltip("Optional explicit template spawned at extraterrestrial impact epicenters. If empty, the director resolves resource.node.void_glass_meteorite from resourceTemplates.")]
        private ResourceNodeTemplate voidGlassMeteoriteTemplate;

        [SerializeField]
        [Tooltip("Optional explicit carbon source template for deep-pressure metamorphism. If empty, resolves resource.node.carbon_graphite_nodule.")]
        private ResourceNodeTemplate pressureCarbonTemplate;

        [SerializeField]
        [Tooltip("Optional explicit diamond output template for deep-pressure metamorphism. If empty, resolves resource.node.pressure_diamond then thermal diamond.")]
        private ResourceNodeTemplate pressureDiamondTemplate;

        [SerializeField, FormerlySerializedAs("authoredOrePrefab")]
        [Tooltip("Author-authored fallback pooled ResourceNode prefab. Used when a ResourceNodeTemplate has no RuntimeNodePrefab.")]
        private GameObject _authoredOrePrefab;

        [SerializeField, FormerlySerializedAs("authoredMagmaVentPrefab")]
        [Tooltip("Author-authored pooled temporary magma vent marker prefab. Leave empty to disable visual markers without runtime factories.")]
        private GameObject _authoredMagmaVentPrefab;

        [SerializeField]
        [Tooltip("Optional explicit player transform. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private Transform playerTransform;

        [SerializeField]
        [Tooltip("Optional explicit MapMagic bridge. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private MapMagicBridge mapMagicBridge;

        [SerializeField]
        [Tooltip("Optional explicit vegetation bridge. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional explicit voxel engine. Runtime falls back to WorldRuntimeReferenceUtility when empty.")]
        private HectonVoxelEngine voxelEngine;

        [Header("Streaming")]
        [SerializeField, Min(32)]
        [Tooltip("AUP sector edge length used by the deterministic resource-node envelope pass.")]
        private int sectorSizeMeters = DefaultSectorSizeMeters;

        [SerializeField, Range(0, 3)]
        [Tooltip("How many sector rings around the player stay resident.")]
        private int sectorRadius = 1;

        [SerializeField, Range(1, 64)]
        [Tooltip("Maximum queued node spawns resolved during one SlowTick.")]
        private int maxSpawnsPerSlowTick = 12;

        [SerializeField, Min(8)]
        [Tooltip("One-time generic node pool warmup floor. Final warmup is the max of this value and computed envelope demand.")]
        private int poolWarmupFloor = DefaultPoolWarmupFloor;

        [Header("Envelope Sampling")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Probe distance used when resolving fallback terrain slope samples.")]
        private float slopeSampleDistanceMeters = DefaultSlopeSampleDistanceMeters;

        [SerializeField, Min(0f)]
        [Tooltip("Rejects samples this close to sector edges to avoid visible seam packing.")]
        private float sectorEdgeMarginMeters = DefaultSectorMarginMeters;

        [SerializeField, Range(0.001f, 1f)]
        [Tooltip("Positive voxel density above this threshold blocks surface placement.")]
        private float voxelSolidThreshold = DefaultVoxelSolidThreshold;

        [Header("Brine Pools")]
        [SerializeField, Min(4f)]
        [Tooltip("Minimum deterministic brine-pool radius allowed inside deep hadal sectors.")]
        private float brinePoolRadiusMinMeters = DefaultBrinePoolRadiusMinMeters;

        [SerializeField, Min(6f)]
        [Tooltip("Maximum deterministic brine-pool radius allowed inside deep hadal sectors.")]
        private float brinePoolRadiusMaxMeters = DefaultBrinePoolRadiusMaxMeters;

        [SerializeField, Min(1f)]
        [Tooltip("Minimum vertical thickness of the deterministic brine fluid lens.")]
        private float brinePoolThicknessMinMeters = DefaultBrinePoolThicknessMinMeters;

        [SerializeField, Min(2f)]
        [Tooltip("Maximum vertical thickness of the deterministic brine fluid lens.")]
        private float brinePoolThicknessMaxMeters = DefaultBrinePoolThicknessMaxMeters;

        [SerializeField, Min(1000f)]
        [Tooltip("Minimum seabed depth before a sector becomes eligible for brine-pool generation.")]
        private float brinePoolMinimumDepthMeters = DefaultBrinePoolMinimumDepthMeters;

        [SerializeField, Min(0.5f)]
        [Tooltip("Minimum lip delta between the bowl floor and sampled rim heights for a valid brine pool.")]
        private float brinePoolMinimumLipMeters = DefaultBrinePoolMinimumLipMeters;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized toxicity intensity registered into HazardZoneManager for brine pools.")]
        private float brinePoolToxicityIntensity = DefaultBrinePoolToxicityIntensity;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Visor glitch multiplier registered alongside brine toxicity hazards.")]
        private float brinePoolHazardVisorBias = DefaultBrinePoolHazardVisorBias;

        [SerializeField, Min(HectonPhysicsContract.WaterDensityKgPerCubicMeterConst)]
        [Tooltip("Fluid density in kg/m3 used by buoyancy overrides inside deterministic brine pools.")]
        private float brinePoolFluidDensityKgPerCubicMeter = DefaultBrinePoolFluidDensityKgPerCubicMeter;

        [Header("Tectonic Upwelling")]
        [SerializeField, Range(0f, 0.25f)]
        [Tooltip("Fraction of tombstoned nodes in an affected chunk that are eligible for seismic reinstatement.")]
        private float tectonicUpwellingRespawnRate = DefaultUpwellingRespawnRate;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Lifetime in seconds of the temporary magma-vent marker spawned during upwelling reinstatement.")]
        private float magmaVentLifetimeSeconds = DefaultMagmaVentLifetimeSeconds;

        [Header("Extraterrestrial Impacts")]
        [SerializeField]
        [Tooltip("Allows rare deterministic meteor impacts to carve the seabed and spawn Void-Glass Meteorite nodes.")]
        private bool enableMeteorImpacts = true;

        [SerializeField, Range(60f, 1800f)]
        [Tooltip("Base seconds between meteor-impact eligibility windows. Actual windows use deterministic jitter.")]
        private float meteorImpactIntervalSeconds = DefaultMeteorImpactIntervalSeconds;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance evaluated when a meteor-impact window opens near the resident player sector.")]
        private float meteorImpactChancePerWindow = 0.35f;

        [SerializeField, Range(24f, 256f)]
        [Tooltip("Horizontal radius around the player used to resolve an impactable seabed epicenter.")]
        private float meteorImpactSearchRadiusMeters = DefaultMeteorImpactSearchRadiusMeters;

        [SerializeField, Range(1f, 16f)]
        [Tooltip("Subtractive SDF sphere radius applied to the voxel volume at the meteor epicenter.")]
        private float meteorImpactCraterRadiusMeters = DefaultMeteorImpactCraterRadiusMeters;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Radiation hazard intensity registered at the Void-Glass Meteorite epicenter.")]
        private float meteoriteRadiationIntensity = DefaultMeteoriteRadiationIntensity;

        [SerializeField, Range(4f, 40f)]
        [Tooltip("Radiation hazard radius registered around the Void-Glass Meteorite epicenter.")]
        private float meteoriteRadiationRadiusMeters = DefaultMeteoriteRadiationRadiusMeters;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Visor glitch bias forwarded with the meteorite radiation hazard.")]
        private float meteoriteRadiationVisorBias = DefaultMeteoriteRadiationVisorBias;

        [Header("Pressure Metamorphism")]
        [SerializeField]
        [Tooltip("Enables slow deep-pressure carbon-to-diamond metamorphism on resident resource nodes.")]
        private bool enablePressureMetamorphism = true;

        [SerializeField, Min(0f)]
        [Tooltip("Depth in meters below which carbon nodes start metamorphic compression.")]
        private float pressureMetamorphismDepthMeters = DefaultPressureMetamorphismDepthMeters;

        [SerializeField, Min(0.01f)]
        [Tooltip("In-game days required for eligible carbon nodes to become pressure diamonds.")]
        private float pressureMetamorphismDays = DefaultPressureMetamorphismDays;

        [Header("Diagnostics")]
        [SerializeField] private int _debugResidentSectorCount;
        [SerializeField] private int _debugActiveNodeCount;
        [SerializeField] private int _debugQueuedSpawnCount;
        [SerializeField] private int _debugLastAcceptedTemplateHash;
        [SerializeField] private Vector2Int _debugPlayerSector;
        [SerializeField] private int _debugActiveBrinePoolCount;
        [SerializeField] private float _debugMeteorImpactTimerSeconds;
        [SerializeField] private int _debugMeteorImpactCount;
        [SerializeField] private int _debugLastMeteorHazardZoneId;
        [SerializeField] private int _debugMetamorphosedNodeCount;

        // COLD ALLOC: Dictionary<long,SectorState>[32] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
        private Dictionary<long, SectorState> _residentSectors;
        // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
        private Queue<SpawnRequest> _pendingSpawns;
        // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] - meshless resource proxy surface snap queue - owner: ResourceDistributionDirector
        private Queue<SpawnRequest> _pendingGhostProxySnaps;
        // COLD ALLOC: List<long>[32] — sector eviction scratch list — owner: ResourceDistributionDirector
        private List<long> _sectorEvictionScratch;
        private readonly List<GameObject> _pendingNodeDeactivations = new List<GameObject>(64);
        private SectorState[] _sectorStatePool;
        private int[] _freeSectorStateIndices;
        private int _freeSectorStateCount;

        private bool _runtimePoolReady;
        private bool _reportedMissingAuthoredOrePrefab;
        private GameObject _validatedAuthoredOrePrefab;
        private bool _slowTickRegistered;
        private bool _seismicHookRegistered;
        private int _computedPoolWarmupCount;
        private float _meteorImpactTimerSeconds;
        private uint _meteorImpactSequence;
        private struct MetamorphismWorkspaceOwner
        {
            public NativeArray<PressureMetamorphismSample> Workspace;

            public bool HasCapacity(int requiredCount)
            {
                return Workspace.IsCreated && Workspace.Length >= requiredCount;
            }

            public void Ensure(int capacity)
            {
                if (HasCapacity(capacity))
                    return;

                Dispose();
                Workspace = H8Memory.Allocate<PressureMetamorphismSample>(
                    capacity,
                    MetamorphismJobOwnerSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!Workspace.IsCreated)
                    throw new InvalidOperationException("H8Memory allocation failed for pressure metamorphism workspace.");
            }

            public void Dispose()
            {
                if (!Workspace.IsCreated)
                    return;

                H8Memory.Release(ref Workspace, MetamorphismJobOwnerSystemId);
            }
        }

        private MetamorphismWorkspaceOwner _metamorphismWorkspace;
        private HazardZoneManager _hazardZoneManager;
        private JobHandle _metamorphismJobHandle;
        private bool _metamorphismJobActive;
        private bool _metamorphismWorkspaceInUse;
        private int _scheduledMetamorphismCount;
        private int _metamorphismCapacity;
        private SpawnRequest[] _ghostProxySnapRequests;
        // COLD ALLOC: List<ResourceNodeTombstoneRecord>[64] — tectonic-upwelling scratch tombstone staging — owner: ResourceDistributionDirector
        private List<ResourceNodeTombstoneRecord> _resourceTombstoneScratch;
        // COLD ALLOC: List<ResourceNode>[InitialMetamorphismCapacity] — pressure-metamorphism job node mapping — owner: ResourceDistributionDirector
        private List<ResourceNode> _metamorphismNodeScratch;
        private IObjectPoolService _objectPool;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private ITickDispatcher _dispatcher;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private bool _registeredHotSwapListener;
        private ResourceNodeTemplate _cachedPressureCarbonTemplate;
        private ResourceNodeTemplate _cachedPressureDiamondTemplate;
        private ResourceNodeTemplate _cachedThermalDiamondTemplate;
        private ResourceNodeTemplate _cachedVoidGlassMeteoriteTemplate;
        private ResourceNodeTemplate _cachedDeepMantleGeodeTemplate;

        private void Awake()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            sectorRadius = math.clamp(sectorRadius, 0, 3);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            sectorEdgeMarginMeters = math.clamp(sectorEdgeMarginMeters, 0f, sectorSizeMeters * 0.25f);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);
            SanitizeBrinePoolSettings();
            tectonicUpwellingRespawnRate = math.clamp(tectonicUpwellingRespawnRate, 0f, 0.25f);
            magmaVentLifetimeSeconds = math.max(1f, magmaVentLifetimeSeconds);
            meteorImpactIntervalSeconds = math.clamp(meteorImpactIntervalSeconds, 60f, 1800f);
            meteorImpactChancePerWindow = math.saturate(meteorImpactChancePerWindow);
            meteorImpactSearchRadiusMeters = math.clamp(meteorImpactSearchRadiusMeters, 24f, 256f);
            meteorImpactCraterRadiusMeters = math.clamp(meteorImpactCraterRadiusMeters, 1f, 16f);
            meteoriteRadiationIntensity = ResolveFiniteSaturate(meteoriteRadiationIntensity, DefaultMeteoriteRadiationIntensity);
            meteoriteRadiationRadiusMeters = math.clamp(
                ResolveFiniteAtLeast(meteoriteRadiationRadiusMeters, DefaultMeteoriteRadiationRadiusMeters, 4f),
                4f,
                40f);
            meteoriteRadiationVisorBias = math.clamp(
                ResolveFiniteAtLeast(meteoriteRadiationVisorBias, DefaultMeteoriteRadiationVisorBias, 0f),
                0f,
                2f);
            pressureMetamorphismDepthMeters = math.max(0f, pressureMetamorphismDepthMeters);
            pressureMetamorphismDays = math.max(0.01f, pressureMetamorphismDays);
            uint meteorSeed = Mix(unchecked((uint)EntityId.ToULong(GetEntityId())), (uint)MeteorImpactSeedSalt);
            _meteorImpactTimerSeconds = ResolveNextMeteorImpactDelay(ref meteorSeed);
            _meteorImpactSequence = meteorSeed;

            int runtimeSectorCapacity = ComputeRuntimeSectorPoolCapacity();
            int perSectorNodeCapacity = ComputePerSectorInitialCapacity();

            // COLD ALLOC: Dictionary<long,SectorState>[runtimeSectorCapacity] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
            _residentSectors = new Dictionary<long, SectorState>(runtimeSectorCapacity);
            // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
            _pendingSpawns = new Queue<SpawnRequest>(DefaultMaxPendingSpawnRequests);
            // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] - pending meshless proxy down-snap requests - owner: ResourceDistributionDirector
            _pendingGhostProxySnaps = new Queue<SpawnRequest>(DefaultMaxPendingSpawnRequests);
            // COLD ALLOC: List<long>[runtimeSectorCapacity] — sector eviction scratch list — owner: ResourceDistributionDirector
            _sectorEvictionScratch = new List<long>(runtimeSectorCapacity);
            // COLD ALLOC: List<ResourceNodeTombstoneRecord>[64] — tectonic-upwelling scratch tombstone staging — owner: ResourceDistributionDirector
            _resourceTombstoneScratch = new List<ResourceNodeTombstoneRecord>(64);
            // COLD ALLOC: List<ResourceNode>[InitialMetamorphismCapacity] — pressure-metamorphism node mapping for Burst result commit — owner: ResourceDistributionDirector
            _metamorphismNodeScratch = new List<ResourceNode>(InitialMetamorphismCapacity);
            InitializeSectorStatePoolCold(runtimeSectorCapacity, perSectorNodeCapacity);
            EnsurePendingNodeDeactivationCapacityCold();
            EnsureGhostProxySnapStaging();

            CacheRegistryServicesCold();
            CacheWorldgenRuntimeReferencesCold();
            CacheSpawnSdfValidationServicesCold();
            CacheSpecialResourceTemplatesCold();
            EnsureMetamorphismCapacityCold(ResolveMetamorphismColdCapacity());
            ValidateAuthoredRuntimePrefabsCold();
            UpdateDiagnostics(default);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            PublishActiveRuntimeInstance();
            CacheRegistryServicesCold();
            CacheWorldgenRuntimeReferencesCold();
            CacheSpawnSdfValidationServicesCold();
            CacheSpecialResourceTemplatesCold();
            EnsureMetamorphismCapacityCold(ResolveMetamorphismColdCapacity());
            ValidateAuthoredRuntimePrefabsCold();
            EnsureRuntimePool();
            TryRegisterHotSwapListener();
            EnsureGhostProxySnapStaging();

            TryRegisterSlowTick();

            if (!_seismicHookRegistered)
            {
                RandomEventEvents.Register(this);
                _seismicHookRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_seismicHookRegistered)
            {
                RandomEventEvents.Unregister(this);
                _seismicHookRegistered = false;
            }

            ClearActiveRuntimeInstance();

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            CancelMetamorphismJobForTeardown();
            ReleaseMetamorphismBuffers();
            DespawnAllResidentNodes();
            _residentSectors?.Clear();
            _pendingSpawns?.Clear();
            _pendingGhostProxySnaps?.Clear();
            _runtimePoolReady = false;
            TryUnregisterHotSwapListener();
            ClearSpawnSdfValidationServicesCold();
            ClearCachedRegistryServices();
            UpdateDiagnostics(default);
        }

        private void OnDestroy()
        {
            ClearActiveRuntimeInstance();
            CancelMetamorphismJobForTeardown();
            DisposeMetamorphismBuffers();
            TryUnregisterHotSwapListener();
            ClearSpawnSdfValidationServicesCold();
            ClearCachedRegistryServices();
        }

        private void PublishActiveRuntimeInstance()
        {
            GlobalRegistry.RegisterResourceDistribution(this);
            s_activeRuntimeInstance = this;
        }

        private void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            if (ReferenceEquals(GlobalRegistry.ResourceDistribution, this))
                GlobalRegistry.UnregisterResourceDistribution(this);
        }

        private void CacheRegistryServicesCold()
        {
            CacheObjectPoolService(null);

            if (_persistentWorldRegistry == null)
                _persistentWorldRegistry = PersistentWorldRegistry.Instance;

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_hazardZoneManager == null)
                _hazardZoneManager = GlobalRegistry.HazardZones;

            if (_oceanKinematicsService == null)
                _oceanKinematicsService = GlobalRegistry.OceanKinematics;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private void CacheWorldgenRuntimeReferencesCold()
        {
            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (vegetationBridge == null)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);

            if (voxelEngine == null)
                WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
        }

        private void RebindPlayerRuntimeContext(object previousService, object currentService)
        {
            IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
            if (previousContext != null &&
                previousContext.PlayerTransform != null &&
                ReferenceEquals(playerTransform, previousContext.PlayerTransform))
            {
                playerTransform = null;
            }

            _playerRuntimeContext = currentService as IPlayerRuntimeContext;
            if (_playerRuntimeContext != null && _playerRuntimeContext.PlayerTransform != null)
                playerTransform = _playerRuntimeContext.PlayerTransform;
        }

        private void RebindMapMagicRuntime(object previousService, object currentService)
        {
            MapMagicBridge currentMapMagic = currentService as MapMagicBridge;
            if (currentMapMagic != null)
            {
                mapMagicBridge = currentMapMagic;
                return;
            }

            MapMagicBridge previousMapMagic = previousService as MapMagicBridge;
            if (previousMapMagic != null && ReferenceEquals(mapMagicBridge, previousMapMagic))
                mapMagicBridge = null;
        }

        private void RebindVegetationRuntime(object previousService, object currentService)
        {
            HectonMapMagicVegetationBridge currentVegetation = currentService as HectonMapMagicVegetationBridge;
            if (WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref currentVegetation))
            {
                vegetationBridge = currentVegetation;
                return;
            }

            HectonMapMagicVegetationBridge previousVegetation = previousService as HectonMapMagicVegetationBridge;
            if (previousVegetation != null && ReferenceEquals(vegetationBridge, previousVegetation))
                vegetationBridge = null;
        }

        private void RebindVoxelEngineRuntime(object previousService, object currentService)
        {
            HectonVoxelEngine currentVoxelEngine = currentService as HectonVoxelEngine;
            if (currentVoxelEngine != null)
            {
                voxelEngine = currentVoxelEngine;
                return;
            }

            HectonVoxelEngine previousVoxelEngine = previousService as HectonVoxelEngine;
            if (previousVoxelEngine != null && ReferenceEquals(voxelEngine, previousVoxelEngine))
                voxelEngine = null;
        }

        private void ClearCachedRegistryServices()
        {
            _objectPool = null;
            _persistentWorldRegistry = null;
            _dispatcher = null;
            _playerRuntimeContext = null;
            _oceanKinematicsService = null;
            _hazardZoneManager = null;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    _runtimePoolReady = false;
                    ValidateAuthoredRuntimePrefabsCold();
                    EnsureRuntimePool();
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (_slowTickRegistered)
                    {
                        GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                        _slowTickRegistered = false;
                    }

                    _dispatcher = currentService as ITickDispatcher;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterSlowTick();
                    }
                    break;
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerRuntimeContext(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    RebindMapMagicRuntime(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    RebindVegetationRuntime(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    RebindVoxelEngineRuntime(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    HazardZoneManager previousHazards = previousService as HazardZoneManager;
                    HazardZoneManager currentHazards = currentService as HazardZoneManager;
                    if (!ReferenceEquals(previousHazards, currentHazards))
                        UnregisterAllBrineHazards(previousHazards);
                    _hazardZoneManager = currentHazards;
                    if (currentHazards != null)
                        RefreshBrineHazardRegistrations();
                    break;
            }
        }

        private void TryRegisterSlowTick()
        {
            if (_slowTickRegistered || !Application.isPlaying || _dispatcher == null)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        /// <summary>
        /// Slow-tick residency pass. Maintains deterministic resource sectors around the player.
        /// </summary>
        public void SlowTick()
        {
            if (!HasRuntimeDependencies())
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            int2 playerSector = QuantizeSector(in playerAup);
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);

            if (_metamorphismJobActive && _metamorphismJobHandle.IsCompleted)
                CompleteAndApplyMetamorphismJob();

            RefreshResidentSectors(playerSector);
            ProcessGhostProxySurfaceSnaps();
            TickMeteorImpacts(0.5f, in playerAup, playerSector);
            FlushPendingNodeDeactivations();
            ProcessPendingSpawns();
            SchedulePressureMetamorphismJob();
            UpdateDiagnostics(playerSector);
        }

        /// <summary>
        /// Reserved for visual-only resource presentation once simulation ownership has settled.
        /// </summary>
        /// <summary>
        /// Spawns one Thermal Diamond node at a flash-freeze crystallization boundary.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space crystallization center.</param>
        /// <param name="crystallizationRadiusMeters">Thermal boundary radius that produced the crystal.</param>
        /// <param name="deltaTemperatureCelsius">Signed current-minus-previous temperature delta that passed the flash-freeze threshold.</param>
        /// <param name="sourceId">Stable caller-provided source id used for deterministic yaw jitter.</param>
        /// <returns>True when a pooled ResourceNode was spawned and spatially registered.</returns>
        public bool TrySpawnThermalDiamondCrystallization(
            Vector3 runtimePosition,
            float crystallizationRadiusMeters,
            float deltaTemperatureCelsius,
            uint sourceId)
        {
            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                !TryResolveThermalDiamondTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;
            GameObject prefab = ResolveAuthoredOrePrefab(template);
            if (prefab == null)
                return false;
            if (!HasAuthoredPoolReserve(pool, prefab))
                return false;

            float safeRadius = math.max(0.25f, crystallizationRadiusMeters);
            Vector3 spawnPosition = ResolveThermalDiamondVoxelFacePosition(runtimePosition, safeRadius, template);
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(spawnPosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(spawnPosition, out AbsoluteUniversePosition spawnAup))
                return false;

            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = EnsureRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;
            if (!HasActiveNodeCapacity(sectorState))
                return false;

            uint yawSeed = SeedSectorCandidate(sector, template.StableHashId, (int)(sourceId & 0x7FFFFFFFu));
            yawSeed = Mix(yawSeed, (uint)math.asint(deltaTemperatureCelsius));
            float yawDegrees = Next01(ref yawSeed) * 360f;
            Quaternion rotation = ResolveSurfaceRotation(Vector3.up, yawDegrees);
            return TrySpawnAuthoredResourceNodeNow(pool, prefab, template, sectorState, spawnPosition, rotation);
        }

        internal bool TrySpawnDeepMantleGeodeAtAup(
            AbsoluteUniversePosition positionAup,
            float sourceRadiusMeters,
            uint sourceId)
        {
            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                !TryResolveDeepMantleGeodeTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;
            GameObject prefab = ResolveAuthoredOrePrefab(template);
            if (!HasAuthoredPoolReserve(pool, prefab))
                return false;

            Vector3 runtimePosition = positionAup.ToRuntimeFloat3();
            Vector3 spawnPosition = ResolveThermalDiamondVoxelFacePosition(
                runtimePosition,
                math.max(0.25f, sourceRadiusMeters),
                template);
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(spawnPosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(spawnPosition, out AbsoluteUniversePosition spawnAup))
                return false;

            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = EnsureRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;
            if (!HasActiveNodeCapacity(sectorState))
                return false;

            uint yawSeed = SeedSectorCandidate(sector, template.StableHashId, (int)(sourceId & 0x7FFFFFFFu));
            float yawDegrees = Next01(ref yawSeed) * 360f;
            Quaternion rotation = ResolveSurfaceRotation(Vector3.up, yawDegrees);
            return TrySpawnAuthoredResourceNodeNow(pool, prefab, template, sectorState, spawnPosition, rotation);
        }

        /// <summary>
        /// Binds deterministic high-value resource nodes to a chthonic pillar surface.
        /// Spawns a Deep Mantle Geode plus one rare pressure/thermal ore fallback when runtime systems are ready.
        /// </summary>
        public int TryBindChthonicPillarResourcesAtAup(
            AbsoluteUniversePosition pillarBaseAup,
            float pillarRadiusMeters,
            float pillarHeightMeters,
            uint pillarId)
        {
            if (!IsFiniteAup(in pillarBaseAup))
                return 0;

            float safeRadius = ResolveFiniteAtLeast(pillarRadiusMeters, 1f, 1f);
            float safeHeight = ResolveFiniteAtLeast(pillarHeightMeters, 1f, 1f);
            double3 baseAbsolute = pillarBaseAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(baseAbsolute)))
                return 0;

            uint state = Mix(0x43504854u, pillarId);
            int sectorA = (int)(Next01(ref state) * 7.999f);
            float2 directionA = ResolveOctantDirection(sectorA);
            float2 directionB = ResolveOctantDirection(sectorA + 3);
            float3 normalA = new float3(directionA.x, 0f, directionA.y);
            float3 normalB = new float3(directionB.x, 0f, directionB.y);

            AbsoluteUniversePosition geodeAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                baseAbsolute.x + normalA.x * safeRadius,
                baseAbsolute.y + safeHeight * 0.22f,
                baseAbsolute.z + normalA.z * safeRadius));
            AbsoluteUniversePosition rareOreAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                baseAbsolute.x + normalB.x * safeRadius,
                baseAbsolute.y + safeHeight * 0.62f,
                baseAbsolute.z + normalB.z * safeRadius));

            int spawned = 0;
            if (TrySpawnDeepMantleGeodeAtPillarSurfaceAup(geodeAup, safeRadius, pillarId, normalA))
                spawned++;

            if (TrySpawnRarePillarOreAtPillarSurfaceAup(rareOreAup, safeRadius, pillarId ^ 0x524F5245u, normalB))
                spawned++;

            return spawned;
        }

        /// <summary>
        /// Binds deterministic high-value resource nodes to a chthonic pillar surface from absolute AUP meters.
        /// </summary>
        public int TryBindChthonicPillarResourcesAtAup(
            double3 pillarBaseAup,
            float pillarRadiusMeters,
            float pillarHeightMeters,
            uint pillarId)
        {
            if (!math.all(math.isfinite(pillarBaseAup)))
                return 0;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAbsolutePosition(pillarBaseAup);
            return TryBindChthonicPillarResourcesAtAup(position, pillarRadiusMeters, pillarHeightMeters, pillarId);
        }

        private bool TrySpawnRarePillarOreAtAup(
            AbsoluteUniversePosition positionAup,
            float sourceRadiusMeters,
            uint sourceId)
        {
            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                !TryResolvePressureDiamondTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;
            GameObject prefab = ResolveAuthoredOrePrefab(template);
            if (!HasAuthoredPoolReserve(pool, prefab))
                return false;

            Vector3 runtimePosition = positionAup.ToRuntimeFloat3();
            Vector3 spawnPosition = ResolveThermalDiamondVoxelFacePosition(
                runtimePosition,
                math.max(0.25f, sourceRadiusMeters),
                template);
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(spawnPosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(spawnPosition, out AbsoluteUniversePosition spawnAup))
                return false;

            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = EnsureRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;
            if (!HasActiveNodeCapacity(sectorState))
                return false;

            uint yawSeed = SeedSectorCandidate(sector, template.StableHashId, (int)(sourceId & 0x7FFFFFFFu));
            float yawDegrees = Next01(ref yawSeed) * 360f;
            Vector3 surfaceNormal = spawnPosition - runtimePosition;
            if (surfaceNormal.sqrMagnitude <= 0.000001f)
                surfaceNormal = Vector3.up;
            Quaternion rotation = ResolveSurfaceRotation(surfaceNormal, yawDegrees);
            return TrySpawnAuthoredResourceNodeNow(pool, prefab, template, sectorState, spawnPosition, rotation);
        }

        private bool TrySpawnDeepMantleGeodeAtPillarSurfaceAup(
            AbsoluteUniversePosition positionAup,
            float sourceRadiusMeters,
            uint sourceId,
            float3 surfaceNormalAup)
        {
            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                !TryResolveDeepMantleGeodeTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            return TrySpawnPillarSurfaceResourceAtAup(positionAup, sourceRadiusMeters, sourceId, surfaceNormalAup, template);
        }

        private bool TrySpawnRarePillarOreAtPillarSurfaceAup(
            AbsoluteUniversePosition positionAup,
            float sourceRadiusMeters,
            uint sourceId,
            float3 surfaceNormalAup)
        {
            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                !TryResolvePressureDiamondTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            return TrySpawnPillarSurfaceResourceAtAup(positionAup, sourceRadiusMeters, sourceId, surfaceNormalAup, template);
        }

        private bool TrySpawnPillarSurfaceResourceAtAup(
            AbsoluteUniversePosition positionAup,
            float sourceRadiusMeters,
            uint sourceId,
            float3 surfaceNormalAup,
            ResourceNodeTemplate template)
        {
            if (template == null || !TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;
            GameObject prefab = ResolveAuthoredOrePrefab(template);
            if (!HasAuthoredPoolReserve(pool, prefab))
                return false;

            float3 safeNormal = ResolveDominantSurfaceNormal(surfaceNormalAup);
            Vector3 surfaceNormal = new Vector3(safeNormal.x, safeNormal.y, safeNormal.z);
            Vector3 runtimePosition = positionAup.ToRuntimeFloat3();
            float spawnOffset = math.max(template.SpawnOffsetMeters, math.max(0.25f, sourceRadiusMeters) * 0.08f);
            Vector3 spawnPosition = runtimePosition + surfaceNormal * spawnOffset;
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(spawnPosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            AbsoluteUniversePosition spawnAup = AbsoluteUniversePosition.OffsetMeters(
                in positionAup,
                new double3(surfaceNormal.x * spawnOffset, surfaceNormal.y * spawnOffset, surfaceNormal.z * spawnOffset));
            if (!IsFiniteAup(in spawnAup))
                return false;

            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = EnsureRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;
            if (!HasActiveNodeCapacity(sectorState))
                return false;

            uint yawSeed = SeedSectorCandidate(sector, template.StableHashId, (int)(sourceId & 0x7FFFFFFFu));
            float yawDegrees = Next01(ref yawSeed) * 360f;
            Quaternion rotation = ResolveSurfaceRotation(surfaceNormal, yawDegrees);

            return TrySpawnAuthoredResourceNodeNow(pool, prefab, template, sectorState, spawnPosition, rotation);
        }

        private Vector3 ResolveThermalDiamondVoxelFacePosition(
            Vector3 runtimePosition,
            float safeRadius,
            ResourceNodeTemplate template)
        {
            float spawnOffset = math.max(template.SpawnOffsetMeters, safeRadius * 0.08f);
            if (voxelEngine != null &&
                voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) &&
                volume != null &&
                volume.TrySampleSurfaceNormal(runtimePosition, math.max(0.25f, safeRadius * 0.25f), out Vector3 surfaceNormal))
            {
                return runtimePosition + surfaceNormal * spawnOffset;
            }

            return runtimePosition + Vector3.up * spawnOffset;
        }

        private void TickMeteorImpacts(float deltaSeconds, in AbsoluteUniversePosition playerAup, int2 playerSector)
        {
            _debugMeteorImpactTimerSeconds = _meteorImpactTimerSeconds;
            if (!enableMeteorImpacts ||
                meteorImpactChancePerWindow <= 0f ||
                !_runtimePoolReady)
            {
                return;
            }

            _meteorImpactTimerSeconds = math.max(0f, _meteorImpactTimerSeconds - math.max(0f, deltaSeconds));
            _debugMeteorImpactTimerSeconds = _meteorImpactTimerSeconds;
            if (_meteorImpactTimerSeconds > 0f)
                return;

            uint sequence = _meteorImpactSequence + 1u;
            _meteorImpactSequence = sequence;
            uint state = SeedSectorCandidate(playerSector, MeteorImpactSeedSalt, (int)(sequence & 0x7FFFFFFFu));
            state = Mix(state, sequence);
            if (TryResolveVoidGlassMeteoriteTemplate(out ResourceNodeTemplate template) &&
                Next01(ref state) <= meteorImpactChancePerWindow)
            {
                TryExecuteMeteorImpact(in playerAup, template, ref state);
            }

            _meteorImpactTimerSeconds = ResolveNextMeteorImpactDelay(ref state);
            _debugMeteorImpactTimerSeconds = _meteorImpactTimerSeconds;
        }

        private bool TryExecuteMeteorImpact(
            in AbsoluteUniversePosition playerAup,
            ResourceNodeTemplate template,
            ref uint state)
        {
            if (template == null || mapMagicBridge == null)
                return false;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return false;
            GameObject prefab = ResolveAuthoredOrePrefab(template);
            if (!HasAuthoredPoolReserve(pool, prefab))
                return false;

            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            float2 impactDirection = ResolveOctantDirection((int)(Next01(ref state) * 7.999f));
            float radialDistance = ResolveCinematicRadialDistance(ref state, math.max(1f, meteorImpactSearchRadiusMeters));
            double absoluteX = playerAbsolute.x + (impactDirection.x * radialDistance);
            double absoluteZ = playerAbsolute.z + (impactDirection.y * radialDistance);
            Vector3 runtimeProbe = AbsoluteToRuntime(absoluteX, playerAbsolute.y, absoluteZ);
            if (!mapMagicBridge.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out float seabedHeight))
                return false;

            Vector3 surfaceAnchorPosition = new Vector3(runtimeProbe.x, seabedHeight, runtimeProbe.z);
            float yawDegrees = Next01(ref state) * 360f;
            if (!TryResolveSurfacePlacement(surfaceAnchorPosition, template.SpawnOffsetMeters, yawDegrees, out Vector3 runtimePosition, out Quaternion rotation))
                return false;

            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(runtimePosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition spawnAup))
                return false;

            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = EnsureRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;
            if (!HasActiveNodeCapacity(sectorState))
                return false;

            int templateIndex = FindTemplateIndex(template);
            if (templateIndex < 0)
                return false;

            SpawnRequest request = new SpawnRequest
            {
                SectorKey = sectorKey,
                TemplateIndex = templateIndex,
                RuntimePosition = runtimePosition,
                Rotation = rotation,
                YawDegrees = yawDegrees,
                SurfaceOffsetMeters = template.SpawnOffsetMeters,
                StableSeed = state,
                TombstoneId = tombstoneId,
                RequiresGhostProxySnap = 0
            };

            if (!HasSpawnQueueCapacity(in request) ||
                !TryApplyMeteorImpactCrater(surfaceAnchorPosition, meteorImpactCraterRadiusMeters))
            {
                return false;
            }

            if (!QueueSpawnRequest(in request))
                return false;

            _debugLastAcceptedTemplateHash = template.StableHashId;
            _debugMeteorImpactCount++;
            RegisterMeteoriteRadiationHazard(runtimePosition, state);
            return true;
        }

        private bool TryApplyMeteorImpactCrater(Vector3 runtimeEpicenter, float radiusMeters)
        {
            if (voxelEngine == null ||
                !voxelEngine.TryGetNearestActiveVolume(runtimeEpicenter, out HectonVoxelVolume volume) ||
                volume == null)
            {
                return false;
            }

            return volume.TryApplyExtraterrestrialImpactCrater(runtimeEpicenter, radiusMeters);
        }

        private void RegisterMeteoriteRadiationHazard(Vector3 runtimePosition, uint stableSeed)
        {
            int zoneId = ResolveMeteorRadiationHazardZoneId(stableSeed);
            if (zoneId == 0)
                return;

            if (!math.isfinite(meteoriteRadiationIntensity) ||
                meteoriteRadiationIntensity <= 0f ||
                !math.isfinite(meteoriteRadiationRadiusMeters) ||
                meteoriteRadiationRadiusMeters <= 0f ||
                !TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition radiationAup))
            {
                RadiationHazardGrid.UnregisterSource(zoneId);
                _debugLastMeteorHazardZoneId = 0;
                return;
            }

            float safeIntensity = ResolveFiniteSaturate(meteoriteRadiationIntensity, DefaultMeteoriteRadiationIntensity);
            float safeRadius = math.clamp(
                ResolveFiniteAtLeast(meteoriteRadiationRadiusMeters, DefaultMeteoriteRadiationRadiusMeters, 4f),
                4f,
                40f);
            RadiationHazardGrid.RegisterSource(
                zoneId,
                in radiationAup,
                safeIntensity,
                safeRadius);
            _debugLastMeteorHazardZoneId = zoneId;
        }

        private float ResolveNextMeteorImpactDelay(ref uint state)
        {
            return meteorImpactIntervalSeconds * math.lerp(0.75f, 1.25f, Next01(ref state));
        }

        private bool HasRuntimeDependencies()
        {
            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return false;

            if (_playerRuntimeContext == null || playerTransform == null)
                return false;

            return mapMagicBridge != null;
        }

        private void CacheSpecialResourceTemplatesCold()
        {
            if (pressureCarbonTemplate != null)
                pressureCarbonTemplate.ResolveStableHashIdCold();
            if (pressureDiamondTemplate != null)
                pressureDiamondTemplate.ResolveStableHashIdCold();
            if (thermalDiamondTemplate != null)
                thermalDiamondTemplate.ResolveStableHashIdCold();
            if (voidGlassMeteoriteTemplate != null)
                voidGlassMeteoriteTemplate.ResolveStableHashIdCold();

            _cachedPressureCarbonTemplate = ResolveTemplateByStableHashCold(CarbonMetamorphismStableHashId);
            _cachedPressureDiamondTemplate = ResolveTemplateByStableHashCold(PressureDiamondStableHashId);
            _cachedThermalDiamondTemplate = ResolveTemplateByStableHashCold(ThermalDiamondStableHashId);
            _cachedVoidGlassMeteoriteTemplate = ResolveTemplateByStableHashCold(VoidGlassMeteoriteStableHashId);
            _cachedDeepMantleGeodeTemplate = ResolveTemplateByStableHashCold(DeepMantleGeodeStableHashId);
        }

        private ResourceNodeTemplate ResolveTemplateByStableHashCold(int stableHashId)
        {
            if (stableHashId == 0 || resourceTemplates == null)
                return null;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate candidate = resourceTemplates[i];
                if (candidate != null && candidate.ResolveStableHashIdCold() == stableHashId)
                    return candidate;
            }

            return null;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return playerAup.IsFinite();
            }

            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            return false;
        }

        private void ValidateAuthoredRuntimePrefabsCold()
        {
            _validatedAuthoredOrePrefab = HasResourceNodeComponentCold(_authoredOrePrefab) ? _authoredOrePrefab : null;

            if (!HasAnyAuthoredOrePrefabCold())
            {
                if (!_reportedMissingAuthoredOrePrefab)
                {
                    _reportedMissingAuthoredOrePrefab = true;
                    Hecton8.Core.H8Debug.LogError(
                        "[ResourceDistributionDirector] No authored ore ResourceNode prefab assigned. Runtime ore spawning will fail closed; run HECTON-8/World/Install Resource Distribution Director or assign PFB_Ore prefabs.",
                        this);
                }

                return;
            }

            _reportedMissingAuthoredOrePrefab = false;

#if UNITY_EDITOR
            if (_authoredOrePrefab != null && _validatedAuthoredOrePrefab == null)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[ResourceDistributionDirector] _authoredOrePrefab has no ResourceNode component. Runtime ore spawning will fail closed.",
                    this);
            }

            ValidateTemplateRuntimePrefabsCold();
#endif
        }

        private bool HasAnyAuthoredOrePrefabCold()
        {
            if (_validatedAuthoredOrePrefab != null)
                return true;

            if (resourceTemplates == null)
                return false;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template != null && template.ValidateRuntimeNodePrefabCold())
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void ValidateTemplateRuntimePrefabsCold()
        {
            if (resourceTemplates == null)
                return;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template == null)
                    continue;

                if (template.HasRuntimeNodePrefabAssignment && !template.ValidateRuntimeNodePrefabCold())
                {
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ResourceDistributionDirector] ResourceNodeTemplate RuntimeNodePrefab has no ResourceNode component. Template will not be warmed for runtime ore spawning.",
                        template);
                }
            }
        }
#endif

        private static bool HasResourceNodeComponentCold(GameObject prefab)
        {
            return prefab != null && prefab.GetComponent<ResourceNode>() != null;
        }

        private void EnsureRuntimePool()
        {
            if (_runtimePoolReady)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            _computedPoolWarmupCount = ComputeRequiredPoolWarmupCount();
            int warmupCount = math.max(poolWarmupFloor, _computedPoolWarmupCount);
            bool hasOrePool = TryWarmAuthoredPrefab(pool, _validatedAuthoredOrePrefab, warmupCount);

            if (resourceTemplates != null)
            {
                for (int i = 0; i < resourceTemplates.Length; i++)
                {
                    ResourceNodeTemplate template = resourceTemplates[i];
                    if (template == null)
                        continue;

                    hasOrePool |= TryWarmAuthoredPrefab(pool, template.RuntimeNodePrefab, warmupCount);
                }
            }

            TryWarmAuthoredPrefab(
                pool,
                _authoredMagmaVentPrefab,
                math.max(4, ((sectorRadius * 2) + 1) * ((sectorRadius * 2) + 1)));

            _runtimePoolReady = hasOrePool;
        }

        private static bool TryWarmAuthoredPrefab(IObjectPoolService pool, GameObject prefab, int warmupCount)
        {
            if (pool == null || prefab == null)
                return false;

            int requiredWarmup = math.max(1, warmupCount);
            int availableCount = pool.HasPool(prefab) ? pool.GetAvailableCount(prefab) : 0;
            if (availableCount < requiredWarmup)
                pool.Warmup(prefab, requiredWarmup - availableCount);

            return pool.HasPool(prefab);
        }

        private int ComputeRequiredPoolWarmupCount()
        {
            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return poolWarmupFloor;

            int sectorsInWindow = (sectorRadius * 2) + 1;
            sectorsInWindow *= sectorsInWindow;

            int perSectorDemand = 0;
            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template == null)
                    continue;

                perSectorDemand += math.max(1, template.MaxInstancesPerSector);
            }

            return math.max(poolWarmupFloor, perSectorDemand * sectorsInWindow);
        }

        private void RefreshResidentSectors(int2 playerSector)
        {
            _sectorEvictionScratch.Clear();
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates != null)
            {
                for (int i = 0; i < sectorStates.Length; i++)
                {
                    SectorState state = sectorStates[i];
                    if (state == null || !state.IsLeased)
                        continue;

                    int deltaX = math.abs(state.Coordinates.x - playerSector.x);
                    int deltaY = math.abs(state.Coordinates.y - playerSector.y);
                    if (deltaX > sectorRadius || deltaY > sectorRadius)
                        TryQueueSectorEvictionNoGrowth(ComposeSectorKey(state.Coordinates));
                    else
                    {
                        CompactSectorNodes(state);
                        SyncBrineHazardRegistration(state);
                    }
                }
            }

            for (int i = 0; i < _sectorEvictionScratch.Count; i++)
                EvictSector(_sectorEvictionScratch[i]);

            for (int z = -sectorRadius; z <= sectorRadius; z++)
            {
                for (int x = -sectorRadius; x <= sectorRadius; x++)
                {
                    int2 sector = new int2(playerSector.x + x, playerSector.y + z);
                    long sectorKey = ComposeSectorKey(sector);
                    if (_residentSectors.TryGetValue(sectorKey, out SectorState existingState))
                    {
                        CompactSectorNodes(existingState);
                        SyncBrineHazardRegistration(existingState);
                        continue;
                    }

                    SectorState state = AcquireSectorStateNoGrowth(sector);
                    if (state == null)
                        continue;

                    if (!TryRegisterResidentSectorNoGrowth(sectorKey, state))
                    {
                        ReleaseSectorStateNoGrowth(state);
                        continue;
                    }

                    EnqueueSectorEnvelope(state, sectorKey);
                }
            }
        }

        private int ComputePerSectorInitialCapacity()
        {
            int capacity = 4;
            if (resourceTemplates == null)
                return capacity;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate template = resourceTemplates[i];
                if (template == null)
                    continue;

                capacity += math.max(1, template.MaxInstancesPerSector);
            }

            return capacity;
        }

        private void EnqueueSectorEnvelope(SectorState state, long sectorKey)
        {
            if (state == null || state.SpawnEnvelopeQueued)
                return;

            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return;

            state.BrinePool = ResolveBrinePoolState(state.Coordinates);
            SyncBrineHazardRegistration(state);
            state.SpawnEnvelopeQueued = true;
            int templateCount = resourceTemplates.Length;
            for (int templateIndex = 0; templateIndex < templateCount; templateIndex++)
            {
                ResourceNodeTemplate template = resourceTemplates[templateIndex];
                if (template == null)
                    continue;

                int acceptedForTemplate = 0;
                int candidateBudget = template.CandidateBudgetPerSector;
                for (int candidateIndex = 0; candidateIndex < candidateBudget; candidateIndex++)
                {
                    if (!HasAnySpawnQueueCapacity() ||
                        acceptedForTemplate >= template.MaxInstancesPerSector)
                    {
                        return;
                    }

                    if (!TryBuildSpawnRequest(state.Coordinates, sectorKey, template, templateIndex, candidateIndex, in state.BrinePool, out SpawnRequest request) ||
                        IsSpawnAlreadyQueuedOrActive(state, in request))
                    {
                        continue;
                    }

                    if (!HasSpawnQueueCapacity(in request))
                        continue;

                    if (QueueSpawnRequest(in request))
                        acceptedForTemplate++;
                }
            }
        }

        private bool TryBuildSpawnRequest(
            int2 sector,
            long sectorKey,
            ResourceNodeTemplate template,
            int templateIndex,
            int candidateIndex,
            in BrinePoolState brinePool,
            out SpawnRequest request)
        {
            request = default;
            uint seed = SeedSectorCandidate(sector, template.StableHashId, candidateIndex);
            uint state = seed;

            float seabedHeight;
            Vector3 surfaceAnchorPosition;
            if (template.RequiresBrinePool)
            {
                if (brinePool.IsValid == 0)
                    return false;

                float2 brineDirection = ResolveOctantDirection((int)(Next01(ref state) * 7.999f));
                float radialDistance = ResolveCinematicRadialDistance(ref state, math.max(1f, brinePool.RadiusMeters * 0.82f));
                float sampleX = brinePool.Center.x + (brineDirection.x * radialDistance);
                float sampleZ = brinePool.Center.z + (brineDirection.y * radialDistance);
                if (!mapMagicBridge.TryGetHeight(sampleX, sampleZ, out seabedHeight))
                    return false;

                surfaceAnchorPosition = new Vector3(sampleX, seabedHeight, sampleZ);
                if (!IsInsideBrinePool(in brinePool, surfaceAnchorPosition))
                    return false;
            }
            else
            {
                double absoluteX = (sector.x * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
                double absoluteZ = (sector.y * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
                Vector3 runtimeProbe = AbsoluteToRuntime(absoluteX, 0d, absoluteZ);
                if (!mapMagicBridge.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out seabedHeight))
                    return false;

                surfaceAnchorPosition = new Vector3(runtimeProbe.x, seabedHeight, runtimeProbe.z);
                if (brinePool.IsValid != 0 && IsInsideBrinePool(in brinePool, surfaceAnchorPosition))
                    return false;
            }

            float yawDegrees = Next01(ref state) * 360f;
            if (!TryResolveSurfacePlacement(surfaceAnchorPosition, template.SpawnOffsetMeters, yawDegrees, out Vector3 runtimePosition, out Quaternion rotation))
                return false;

            float waterSurface = ResolveWaterSurfaceLevel();
            float depthMeters = math.max(0f, waterSurface - seabedHeight);
            float temperatureCelsius = ResolveTemperature(runtimePosition);
            float slopeDegrees = ResolveSlope(runtimePosition);
            if (!template.MatchesEnvelope(depthMeters, temperatureCelsius, slopeDegrees))
                return false;

            if (template.RequiresHydrothermalVent &&
                temperatureCelsius < template.HydrothermalVentTemperatureThresholdCelsius)
            {
                return false;
            }

            if (Next01(ref state) > template.PlacementProbability)
                return false;

            if (!TryValidateSpawnRuntimePositionViaSdf(runtimePosition, ResolveSpawnSdfRequiredClearanceRadius(template), out runtimePosition))
                return false;

            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(runtimePosition);
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            request = new SpawnRequest
            {
                SectorKey = sectorKey,
                TemplateIndex = templateIndex,
                RuntimePosition = runtimePosition,
                Rotation = rotation,
                YawDegrees = yawDegrees,
                SurfaceOffsetMeters = template.SpawnOffsetMeters,
                StableSeed = seed,
                TombstoneId = tombstoneId,
                RequiresGhostProxySnap = template.HasPresentationMesh ? (byte)0 : (byte)1
            };
            return true;
        }

        private bool QueueSpawnRequest(in SpawnRequest request)
        {
            if (!HasSpawnQueueCapacity(in request))
                return false;

            if (request.RequiresGhostProxySnap != 0)
            {
                _pendingGhostProxySnaps.Enqueue(request);
                return true;
            }

            _pendingSpawns.Enqueue(request);
            return true;
        }

        private bool HasSpawnQueueCapacity(in SpawnRequest request)
        {
            if (request.RequiresGhostProxySnap != 0)
            {
                return _pendingGhostProxySnaps != null &&
                       _pendingGhostProxySnaps.Count < DefaultMaxPendingSpawnRequests;
            }

            return _pendingSpawns != null &&
                   _pendingSpawns.Count < DefaultMaxPendingSpawnRequests;
        }

        private bool HasAnySpawnQueueCapacity()
        {
            return (_pendingSpawns != null && _pendingSpawns.Count < DefaultMaxPendingSpawnRequests) ||
                   (_pendingGhostProxySnaps != null && _pendingGhostProxySnaps.Count < DefaultMaxPendingSpawnRequests);
        }

        private void ProcessGhostProxySurfaceSnaps()
        {
            if (!_runtimePoolReady ||
                _pendingGhostProxySnaps == null ||
                _pendingGhostProxySnaps.Count == 0 ||
                _ghostProxySnapRequests == null ||
                _pendingSpawns == null ||
                mapMagicBridge == null)
            {
                return;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            int scheduledCount = math.min(_pendingGhostProxySnaps.Count, GhostProxySnapBatchCapacity);
            if (scheduledCount <= 0)
                return;

            for (int i = 0; i < scheduledCount; i++)
            {
                if (_pendingSpawns.Count >= DefaultMaxPendingSpawnRequests)
                    break;

                SpawnRequest request = _pendingGhostProxySnaps.Dequeue();
                _ghostProxySnapRequests[i] = request;

                ResourceNodeTemplate template = ResolveTemplateOrNull(request.TemplateIndex);
                GameObject prefab = ResolveAuthoredOrePrefab(template);
                if (prefab == null || !pool.HasPool(prefab))
                {
                    _ghostProxySnapRequests[i] = default;
                    continue;
                }

                if (pool.GetAvailableCount(prefab) <= 0)
                {
                    DeferGhostProxySnapRequestNoGrowth(in request);
                    _ghostProxySnapRequests[i] = default;
                    continue;
                }

                if (!TryResolveGhostProxySurfaceSnap(ref request))
                {
                    _ghostProxySnapRequests[i] = default;
                    continue;
                }

                request.TombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(request.RuntimePosition);

                request.RequiresGhostProxySnap = 0;
                PersistentWorldRegistry registry = _persistentWorldRegistry;
                if (registry != null && registry.IsResourceNodeTombstoned(request.TombstoneId))
                {
                    _ghostProxySnapRequests[i] = default;
                    continue;
                }

                _pendingSpawns.Enqueue(request);
                _ghostProxySnapRequests[i] = default;
            }
        }

        private void DeferGhostProxySnapRequestNoGrowth(in SpawnRequest request)
        {
            if (_pendingGhostProxySnaps.Count < DefaultMaxPendingSpawnRequests)
                _pendingGhostProxySnaps.Enqueue(request);
        }

        private bool TryResolveGhostProxySurfaceSnap(ref SpawnRequest request)
        {
            if (!IsFiniteRuntimePosition(request.RuntimePosition) || mapMagicBridge == null)
                return false;

            if (!mapMagicBridge.TryGetHeight(request.RuntimePosition.x, request.RuntimePosition.z, out float seabedHeight) ||
                !math.isfinite(seabedHeight))
            {
                return false;
            }

            Vector3 anchor = new Vector3(request.RuntimePosition.x, seabedHeight, request.RuntimePosition.z);
            if (!TryResolveSurfacePlacement(anchor, request.SurfaceOffsetMeters, request.YawDegrees, out Vector3 snappedPosition, out Quaternion snappedRotation))
                return false;

            ResourceNodeTemplate template = ResolveTemplateOrNull(request.TemplateIndex);
            if (!TryValidateSpawnRuntimePositionViaSdf(snappedPosition, ResolveSpawnSdfRequiredClearanceRadius(template), out snappedPosition))
                return false;

            request.RuntimePosition = snappedPosition;
            request.Rotation = snappedRotation;
            return true;
        }

        private ResourceNodeTemplate ResolveTemplateOrNull(int templateIndex)
        {
            return resourceTemplates != null && (uint)templateIndex < (uint)resourceTemplates.Length
                ? resourceTemplates[templateIndex]
                : null;
        }

        private GameObject ResolveAuthoredOrePrefab(ResourceNodeTemplate template)
        {
            if (template == null)
                return null;

            GameObject templatePrefab = template.RuntimeNodePrefab;
            if (templatePrefab != null)
                return templatePrefab;

            return _validatedAuthoredOrePrefab;
        }

        /// <summary>
        /// Resolves an authored pooled ResourceNode prefab — and, when the runtime environment matches an
        /// authored envelope, the ResourceNodeTemplate to stamp onto it — for producers that own their own
        /// spawn queue and therefore never reach this director's private sector-planning path.
        /// ScavengePopulator.ProcessSpawnQueue is the only such producer: it receives scatter points from
        /// HectonScatterOutput / HectonVoxelEngine and has no template concept at all, so without this entry
        /// point its spawns die on a null prefab whenever its authored loot tables are empty.
        ///
        /// Only authored data is used:
        ///   - the prefab comes from <see cref="ResolveAuthoredOrePrefab"/>, i.e. the template's own
        ///     RuntimeNodePrefab first and then the author-authored _authoredOrePrefab fallback that
        ///     EnsureRuntimePool already warms, so the pool is guaranteed to have a reserve for it;
        ///   - the template is chosen with <see cref="ResourceNodeTemplate.MatchesEnvelope"/>, the same
        ///     depth/temperature/slope gate TryEvaluateSectorCandidate applies, against this director's own
        ///     environment resolvers. PlacementProbability is deliberately not applied: it is a per-sector
        ///     density knob for candidates this director invents, and the caller's point was chosen by the
        ///     scatter producer, not by a density roll.
        ///
        /// The template is best-effort by design. A prefab is still returned when no authored envelope
        /// matches the caller's position, so the caller spawns a real authored node instead of nothing;
        /// callers must treat a null template as "no authored yield for this position".
        /// Returns false only when this director has no usable authored prefab or no warm pool at all.
        /// </summary>
        internal bool TryResolveExternalProducerNodeSpawn(
            Vector3 runtimePosition,
            int deterministicSelector,
            out GameObject prefab,
            out ResourceNodeTemplate template)
        {
            prefab = null;
            template = null;

            if (!_runtimePoolReady)
                return false;

            template = ResolveEnvelopeMatchedTemplateOrNull(runtimePosition, deterministicSelector);

            GameObject resolved = ResolveAuthoredOrePrefab(template);
            if (resolved == null)
                resolved = _validatedAuthoredOrePrefab;

            if (resolved == null)
            {
                template = null;
                return false;
            }

            prefab = resolved;
            return true;
        }

        /// <summary>
        /// Deterministic authored-template lookup for an externally supplied runtime position.
        /// Scans resourceTemplates from a caller-supplied rotation offset so repeat calls with the same
        /// selector always land on the same template, and prefers a match that actually carries a
        /// LootPickupPrefab — a node whose template yields no pickup is harvestable but drops nothing,
        /// which would silently starve every downstream consumer of the harvest.
        /// ZERO GC: array indexing and struct math only.
        /// </summary>
        private ResourceNodeTemplate ResolveEnvelopeMatchedTemplateOrNull(
            Vector3 runtimePosition,
            int deterministicSelector)
        {
            ResourceNodeTemplate[] templates = resourceTemplates;
            if (templates == null || templates.Length == 0)
                return null;

            if (!TryResolveExternalProducerEnvelope(
                    runtimePosition,
                    out float depthMeters,
                    out float temperatureCelsius,
                    out float slopeDegrees))
            {
                return null;
            }

            int length = templates.Length;
            int start = deterministicSelector % length;
            if (start < 0)
                start += length;

            ResourceNodeTemplate yieldlessMatch = null;

            for (int offset = 0; offset < length; offset++)
            {
                int index = start + offset;
                if (index >= length)
                    index -= length;

                ResourceNodeTemplate candidate = templates[index];
                if (candidate == null)
                    continue;

                // Brine-pool templates are only valid inside a director-planned brine pool, which an
                // external producer's point is not; excluding them is the same decision
                // TryEvaluateSectorCandidate makes when brinePool.IsValid == 0.
                if (candidate.RequiresBrinePool)
                    continue;

                if (!candidate.MatchesEnvelope(depthMeters, temperatureCelsius, slopeDegrees))
                    continue;

                if (candidate.RequiresHydrothermalVent &&
                    temperatureCelsius < candidate.HydrothermalVentTemperatureThresholdCelsius)
                {
                    continue;
                }

                if (candidate.LootPickupPrefab != null)
                    return candidate;

                if (yieldlessMatch == null)
                    yieldlessMatch = candidate;
            }

            return yieldlessMatch;
        }

        /// <summary>
        /// Samples the authored environment at an externally supplied runtime position using the same
        /// resolvers TryEvaluateSectorCandidate uses. Fails closed when the MapMagic bridge is absent or
        /// the seabed sample is unavailable, because ResolveSlope dereferences that bridge unguarded.
        /// </summary>
        private bool TryResolveExternalProducerEnvelope(
            Vector3 runtimePosition,
            out float depthMeters,
            out float temperatureCelsius,
            out float slopeDegrees)
        {
            depthMeters = 0f;
            temperatureCelsius = 0f;
            slopeDegrees = 0f;

            MapMagicBridge bridge = mapMagicBridge;
            if (bridge == null)
                return false;

            if (!bridge.TryGetHeight(runtimePosition.x, runtimePosition.z, out float seabedHeight) ||
                !math.isfinite(seabedHeight))
            {
                return false;
            }

            depthMeters = math.max(0f, ResolveWaterSurfaceLevel() - seabedHeight);
            temperatureCelsius = ResolveTemperature(runtimePosition);
            slopeDegrees = ResolveSlope(runtimePosition);
            return true;
        }

        private void ProcessPendingSpawns()
        {
            if (!_runtimePoolReady || _pendingSpawns.Count == 0)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            int processedCount = 0;
            int inspectedCount = 0;
            int maxInspectionCount = _pendingSpawns.Count;
            while (processedCount < maxSpawnsPerSlowTick &&
                   inspectedCount < maxInspectionCount &&
                   _pendingSpawns.Count > 0)
            {
                inspectedCount++;
                SpawnRequest request = _pendingSpawns.Peek();
                ResourceNodeTemplate template = ResolveTemplateOrNull(request.TemplateIndex);
                if (template == null)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                template = ResolveMetamorphosedTemplateOverride(request.TombstoneId, template);
                GameObject prefab = ResolveAuthoredOrePrefab(template);
                if (prefab == null)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if (!pool.HasPool(prefab))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if (!_residentSectors.TryGetValue(request.SectorKey, out SectorState sectorState))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if (pool.GetAvailableCount(prefab) <= 0)
                {
                    DeferPendingSpawnRequestNoGrowth(in request);
                    continue;
                }

                if (ContainsActiveNodeWithTombstone(sectorState, request.TombstoneId))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if (!HasActiveNodeCapacity(sectorState))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                GameObject instance = pool.Spawn(prefab, request.RuntimePosition, request.Rotation, false);
                if (instance == null)
                {
                    DeferPendingSpawnRequestNoGrowth(in request);
                    continue;
                }

                _pendingSpawns.Dequeue();
                processedCount++;

                if (!TryResolvePooledResourceNode(pool, instance, out ResourceNode node))
                {
                    pool.Despawn(instance);
                    continue;
                }

                node.ApplyRuntimeTemplate(template, null, null);
                TryApplyEmbeddedVein(node, template, in request);
                node.RefreshRuntimeSpatialRegistration();
                if (!TryAttachActiveNodeNoGrowth(sectorState, node))
                {
                    pool.Despawn(instance);
                    continue;
                }

                _debugLastAcceptedTemplateHash = template.StableHashId;
            }
        }

        private void DeferPendingSpawnRequestNoGrowth(in SpawnRequest request)
        {
            _pendingSpawns.Dequeue();
            if (_pendingSpawns.Count < DefaultMaxPendingSpawnRequests)
                _pendingSpawns.Enqueue(request);
        }

        private bool TrySpawnAuthoredResourceNodeNow(
            IObjectPoolService pool,
            GameObject prefab,
            ResourceNodeTemplate template,
            SectorState sectorState,
            Vector3 spawnPosition,
            Quaternion rotation)
        {
            if (pool == null ||
                prefab == null ||
                template == null ||
                sectorState == null ||
                !HasAuthoredPoolReserve(pool, prefab))
            {
                return false;
            }

            GameObject instance = pool.Spawn(prefab, spawnPosition, rotation, false);
            if (instance == null)
                return false;

            if (!TryResolvePooledResourceNode(pool, instance, out ResourceNode node))
            {
                pool.Despawn(instance);
                return false;
            }

            node.ApplyRuntimeTemplate(template, null, null);
            node.RefreshRuntimeSpatialRegistration();
            if (!TryAttachActiveNodeNoGrowth(sectorState, node))
            {
                pool.Despawn(instance);
                return false;
            }

            _debugLastAcceptedTemplateHash = template.StableHashId;
            return true;
        }

        private static bool HasAuthoredPoolReserve(IObjectPoolService pool, GameObject prefab)
        {
            return pool != null &&
                   prefab != null &&
                   pool.HasPool(prefab) &&
                   pool.GetAvailableCount(prefab) > 0;
        }

        private static bool TryResolvePooledResourceNode(
            IObjectPoolService pool,
            GameObject instance,
            out ResourceNode node)
        {
            node = null;
            return pool != null &&
                   instance != null &&
                   pool.TryGetPooledComponent(instance, out node);
        }

        private static void DespawnKnownPooledResourceOrDisable(IObjectPoolService pool, GameObject target)
        {
            if (target == null)
                return;

            if (TryResolvePooledResourceNode(pool, target, out _))
            {
                pool.Despawn(target);
                return;
            }

            target.SetActive(false);
        }

        private void SchedulePressureMetamorphismJob()
        {
            if (!enablePressureMetamorphism ||
                _metamorphismJobActive ||
                _residentSectors == null ||
                _residentSectors.Count == 0 ||
                mapMagicBridge == null ||
                !TryResolvePressureCarbonTemplate(out ResourceNodeTemplate carbonTemplate) ||
                !TryResolvePressureDiamondTemplate(out _))
            {
                return;
            }

            try
            {
                int nodeCount = BuildPressureMetamorphismInputs(
                    carbonTemplate.StableHashId,
                    out NativeArray<PressureMetamorphismSample> workspace);
                if (nodeCount <= 0)
                    return;

                PressureMetamorphismJob job = new PressureMetamorphismJob
                {
                    Workspace = workspace,
                    DeltaSeconds = 0.5f,
                    DepthThresholdMeters = pressureMetamorphismDepthMeters,
                    RequiredSeconds = pressureMetamorphismDays * GameSecondsPerDay,
                    CarbonTemplateHashId = carbonTemplate.StableHashId
                };

                _scheduledMetamorphismCount = nodeCount;
                _metamorphismJobHandle = job.Schedule(nodeCount, 16);
                _metamorphismJobActive = true;
                H8Memory.RegisterActiveJob(MetamorphismJobOwnerSystemId, _metamorphismJobHandle);
            }
            finally
            {
                ReleaseMetamorphismWorkspaceLease();
            }
        }

        private int BuildPressureMetamorphismInputs(
            int carbonTemplateHashId,
            out NativeArray<PressureMetamorphismSample> workspace)
        {
            workspace = default;
            _metamorphismNodeScratch.Clear();
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates == null || sectorStates.Length == 0)
                return 0;

            int estimatedCount = 0;
            for (int sectorIndex = 0; sectorIndex < sectorStates.Length; sectorIndex++)
            {
                SectorState state = sectorStates[sectorIndex];
                if (state == null || !state.IsLeased || state.ActiveNodes == null)
                    continue;

                List<ResourceNode> activeNodes = state.ActiveNodes;
                for (int i = 0; i < activeNodes.Count; i++)
                {
                    if (TryResolvePressureMetamorphismCandidate(activeNodes[i], carbonTemplateHashId, out _))
                        estimatedCount++;
                }
            }

            if (estimatedCount <= 0 || !TryAcquireMetamorphismJobBuffer(estimatedCount, out workspace))
                return 0;

            int writeIndex = 0;
            float waterSurface = ResolveWaterSurfaceLevel();
            for (int sectorIndex = 0; sectorIndex < sectorStates.Length; sectorIndex++)
            {
                SectorState state = sectorStates[sectorIndex];
                if (state == null || !state.IsLeased || state.ActiveNodes == null)
                    continue;

                for (int i = 0; i < state.ActiveNodes.Count && writeIndex < workspace.Length; i++)
                {
                    ResourceNode node = state.ActiveNodes[i];
                    if (!TryResolvePressureMetamorphismCandidate(node, carbonTemplateHashId, out ResourceNodeTemplate template))
                        continue;

                    if (!node.TryGetPersistentAup(out AbsoluteUniversePosition nodeAup))
                        continue;

                    double3 nodeAbsolute = nodeAup.ToAbsoluteDouble3();
                    if (!math.all(math.isfinite(nodeAbsolute)))
                        continue;

                    float depthMeters = math.max(0f, waterSurface - (float)nodeAbsolute.y);
                    workspace[writeIndex] = new PressureMetamorphismSample
                    {
                        DepthMeters = depthMeters,
                        ProgressSeconds = node.PressureMetamorphismProgressSeconds,
                        TemplateHashId = template.StableHashId,
                        Active = (byte)(depthMeters > pressureMetamorphismDepthMeters ? 1 : 0),
                        ResultProgressSeconds = 0f,
                        TransformToDiamond = 0
                    };
                    _metamorphismNodeScratch.Add(node);
                    writeIndex++;
                }
            }

            if (writeIndex <= 0)
            {
                ReleaseMetamorphismWorkspaceLease();
                workspace = default;
            }

            return writeIndex;
        }

        private static bool TryResolvePressureMetamorphismCandidate(
            ResourceNode node,
            int carbonTemplateHashId,
            out ResourceNodeTemplate template)
        {
            template = null;
            if (node == null || node.IsDepleted || !node.gameObject.activeInHierarchy)
                return false;

            template = node.ResourceTemplate;
            return template != null && template.StableHashId == carbonTemplateHashId;
        }

        private void CompleteAndApplyMetamorphismJob()
        {
            if (!DispatcherJobSwap.TryComplete(ref _metamorphismJobHandle, forceComplete: false))
                return;

            _metamorphismJobActive = false;

            try
            {
                if (!TryResolvePressureDiamondTemplate(out ResourceNodeTemplate diamondTemplate) ||
                    !TryReadMetamorphismWorkspace(_scheduledMetamorphismCount, out NativeArray<PressureMetamorphismSample>.ReadOnly metamorphismResults))
                {
                    _scheduledMetamorphismCount = 0;
                    _metamorphismNodeScratch.Clear();
                    return;
                }

                PersistentWorldRegistry registry = _persistentWorldRegistry;
                int count = math.min(_scheduledMetamorphismCount, _metamorphismNodeScratch.Count);
                for (int i = 0; i < count; i++)
                {
                    ResourceNode node = _metamorphismNodeScratch[i];
                    if (node == null || node.IsDepleted || !node.gameObject.activeInHierarchy)
                        continue;

                    PressureMetamorphismSample result = metamorphismResults[i];
                    if (result.TransformToDiamond == 0)
                    {
                        node.SetPressureMetamorphismProgressSeconds(result.ResultProgressSeconds);
                        continue;
                    }

                    node.SetPressureMetamorphismProgressSeconds(0f);
                    node.ApplyRuntimeTemplate(diamondTemplate, null, null);
                    node.RefreshRuntimeSpatialRegistration();
                    if (registry != null && node.TryGetPersistentAup(out AbsoluteUniversePosition nodeAup))
                        registry.TryRegisterResourceNodeMetamorphosis(node.PersistentTombstoneId, in nodeAup);
                    _debugMetamorphosedNodeCount++;
                }
            }
            finally
            {
                ReleaseMetamorphismWorkspaceLease();
                _scheduledMetamorphismCount = 0;
                _metamorphismNodeScratch.Clear();
            }
        }

        private void CancelMetamorphismJobForTeardown()
        {
            if (!_metamorphismJobActive)
            {
                ReleaseMetamorphismWorkspaceLease();
                return;
            }

            DispatcherJobSwap.TryComplete(ref _metamorphismJobHandle, forceComplete: true);
            _metamorphismJobHandle = default;
            _metamorphismJobActive = false;
            _scheduledMetamorphismCount = 0;
            _metamorphismNodeScratch?.Clear();
            ReleaseMetamorphismWorkspaceLease();
        }

        private bool EnsureMetamorphismCapacity(int requiredCount)
        {
            if (requiredCount <= 0 || _metamorphismJobActive || _metamorphismWorkspaceInUse)
                return false;

            return _metamorphismWorkspace.HasCapacity(requiredCount);
        }

        private int ResolveMetamorphismColdCapacity()
        {
            return math.max(InitialMetamorphismCapacity, ComputeRequiredPoolWarmupCount());
        }

        private int ComputeResidentSectorWindowCount()
        {
            int radius = math.clamp(sectorRadius, 0, 3);
            int diameter = (radius * 2) + 1;
            return math.max(1, diameter * diameter);
        }

        private int ComputeRuntimeSectorPoolCapacity()
        {
            return ComputeResidentSectorWindowCount() + TransientRuntimeSectorReserve;
        }

        private void InitializeSectorStatePoolCold(int runtimeSectorCapacity, int perSectorNodeCapacity)
        {
            int safeSectorCapacity = math.max(1, runtimeSectorCapacity);
            int safeNodeCapacity = math.max(1, perSectorNodeCapacity);

            // COLD ALLOC: SectorState[safeSectorCapacity] + int freelist - no-growth runtime sector residency pool - owner: ResourceDistributionDirector
            _sectorStatePool = new SectorState[safeSectorCapacity];
            _freeSectorStateIndices = new int[safeSectorCapacity];
            for (int i = 0; i < safeSectorCapacity; i++)
            {
                _sectorStatePool[i] = new SectorState(i, safeNodeCapacity);
                _freeSectorStateIndices[i] = i;
            }

            _freeSectorStateCount = safeSectorCapacity;
        }

        private void EnsurePendingNodeDeactivationCapacityCold()
        {
            int requiredCapacity = math.max(
                _pendingNodeDeactivations.Capacity,
                ComputePerSectorInitialCapacity() * ComputeRuntimeSectorPoolCapacity());
            if (_pendingNodeDeactivations.Capacity < requiredCapacity)
                _pendingNodeDeactivations.Capacity = requiredCapacity;
        }

        private SectorState AcquireSectorStateNoGrowth(int2 sector)
        {
            if (_sectorStatePool == null ||
                _freeSectorStateIndices == null ||
                _freeSectorStateCount <= 0)
            {
                return null;
            }

            int index = _freeSectorStateIndices[_freeSectorStateCount - 1];
            if ((uint)index >= (uint)_sectorStatePool.Length)
                return null;

            SectorState state = _sectorStatePool[index];
            if (state == null || state.IsLeased)
                return null;

            _freeSectorStateCount--;
            state.Lease(sector);
            return state;
        }

        private void ReleaseSectorStateNoGrowth(SectorState state)
        {
            if (state == null ||
                _freeSectorStateIndices == null ||
                !state.IsLeased ||
                (uint)state.PoolIndex >= (uint)_freeSectorStateIndices.Length)
            {
                return;
            }

            state.Release();
            if (_freeSectorStateCount < _freeSectorStateIndices.Length)
                _freeSectorStateIndices[_freeSectorStateCount++] = state.PoolIndex;
        }

        private bool TryRegisterResidentSectorNoGrowth(long sectorKey, SectorState state)
        {
            if (_residentSectors == null || state == null)
                return false;

            if (_residentSectors.ContainsKey(sectorKey))
                return false;

            int residentCapacity = _sectorStatePool != null ? _sectorStatePool.Length : 0;
            if (residentCapacity <= 0 || _residentSectors.Count >= residentCapacity)
                return false;

            _residentSectors.Add(sectorKey, state);
            return true;
        }

        private bool TryQueueSectorEvictionNoGrowth(long sectorKey)
        {
            if (_sectorEvictionScratch == null ||
                _sectorEvictionScratch.Count >= _sectorEvictionScratch.Capacity)
            {
                return false;
            }

            _sectorEvictionScratch.Add(sectorKey);
            return true;
        }

        private static bool HasActiveNodeCapacity(SectorState state)
        {
            List<ResourceNode> nodes = state != null ? state.ActiveNodes : null;
            return nodes != null && nodes.Count < nodes.Capacity;
        }

        private static bool TryAttachActiveNodeNoGrowth(SectorState state, ResourceNode node)
        {
            if (node == null || !HasActiveNodeCapacity(state))
                return false;

            state.ActiveNodes.Add(node);
            return true;
        }

        private bool TryQueueNodeDeactivationNoGrowth(GameObject target)
        {
            if (target == null)
                return true;

            if (_pendingNodeDeactivations.Count < _pendingNodeDeactivations.Capacity)
            {
                _pendingNodeDeactivations.Add(target);
                return true;
            }

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            DespawnKnownPooledResourceOrDisable(pool, target);

            return false;
        }

        private void EnsureMetamorphismCapacityCold(int requiredCount)
        {
            if (!Application.isPlaying || requiredCount <= 0 || _metamorphismJobActive || _metamorphismWorkspaceInUse)
                return;

            int nextCapacity = math.max(requiredCount, InitialMetamorphismCapacity);
            if (_metamorphismWorkspace.HasCapacity(nextCapacity))
            {
                _metamorphismCapacity = _metamorphismWorkspace.Workspace.Length;
                if (_metamorphismNodeScratch != null && _metamorphismNodeScratch.Capacity < nextCapacity)
                    _metamorphismNodeScratch.Capacity = nextCapacity;
                return;
            }

            _metamorphismWorkspace.Ensure(nextCapacity);
            _metamorphismCapacity = _metamorphismWorkspace.Workspace.IsCreated ? _metamorphismWorkspace.Workspace.Length : 0;
            if (_metamorphismNodeScratch != null && _metamorphismNodeScratch.Capacity < nextCapacity)
                _metamorphismNodeScratch.Capacity = nextCapacity;
        }

        private void DisposeMetamorphismBuffers()
        {
            ReleaseMetamorphismBuffers();
        }

        private bool TryAcquireMetamorphismJobBuffer(
            int requiredCount,
            out NativeArray<PressureMetamorphismSample> workspace)
        {
            workspace = default;
            if (!EnsureMetamorphismCapacity(requiredCount) || _metamorphismWorkspaceInUse)
                return false;

            if (_metamorphismNodeScratch == null || _metamorphismNodeScratch.Capacity < requiredCount)
                return false;

            workspace = _metamorphismWorkspace.Workspace;
            if (!workspace.IsCreated || workspace.Length < requiredCount)
            {
                workspace = default;
                return false;
            }

            _metamorphismWorkspaceInUse = true;
            return true;
        }

        private bool TryReadMetamorphismWorkspace(
            int requiredCount,
            out NativeArray<PressureMetamorphismSample>.ReadOnly workspace)
        {
            workspace = default;
            if (requiredCount <= 0 ||
                !_metamorphismWorkspace.Workspace.IsCreated ||
                _metamorphismWorkspace.Workspace.Length < requiredCount)
            {
                return false;
            }

            workspace = _metamorphismWorkspace.Workspace.AsReadOnly();
            return true;
        }

        private void ReleaseMetamorphismWorkspaceLease()
        {
            if (!_metamorphismWorkspaceInUse)
                return;

            _metamorphismWorkspaceInUse = false;
        }

        private void ReleaseMetamorphismBuffers()
        {
            if (_metamorphismJobActive)
                CancelMetamorphismJobForTeardown();

            ReleaseMetamorphismWorkspaceLease();
            _metamorphismWorkspace.Dispose();
            _metamorphismCapacity = 0;
        }

        private void EnsureGhostProxySnapStaging()
        {
            if (_ghostProxySnapRequests == null || _ghostProxySnapRequests.Length != GhostProxySnapBatchCapacity)
            {
                // COLD ALLOC: SpawnRequest[GhostProxySnapBatchCapacity] - fixed staging for meshless proxy surface snaps - owner: ResourceDistributionDirector
                _ghostProxySnapRequests = new SpawnRequest[GhostProxySnapBatchCapacity];
            }
        }

        private ResourceNodeTemplate ResolveMetamorphosedTemplateOverride(ulong tombstoneId, ResourceNodeTemplate fallback)
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || !registry.IsResourceNodeMetamorphosed(tombstoneId))
                return fallback;

            return TryResolvePressureDiamondTemplate(out ResourceNodeTemplate diamondTemplate)
                ? diamondTemplate
                : fallback;
        }

        private bool TryResolvePressureCarbonTemplate(out ResourceNodeTemplate template)
        {
            template = pressureCarbonTemplate;
            if (template == null)
                template = _cachedPressureCarbonTemplate;

            return template != null;
        }

        private bool TryResolvePressureDiamondTemplate(out ResourceNodeTemplate template)
        {
            template = pressureDiamondTemplate;
            if (template == null)
                template = _cachedPressureDiamondTemplate;

            if (template != null)
                return true;

            return TryResolveThermalDiamondTemplate(out template);
        }

        private bool TryResolveThermalDiamondTemplate(out ResourceNodeTemplate template)
        {
            template = thermalDiamondTemplate;
            if (template == null)
                template = _cachedThermalDiamondTemplate;

            return template != null;
        }

        private bool TryResolveVoidGlassMeteoriteTemplate(out ResourceNodeTemplate template)
        {
            template = voidGlassMeteoriteTemplate;
            if (template == null)
                template = _cachedVoidGlassMeteoriteTemplate;

            return template != null;
        }

        private bool TryResolveDeepMantleGeodeTemplate(out ResourceNodeTemplate template)
        {
            template = _cachedDeepMantleGeodeTemplate;
            return template != null;
        }

        private int FindTemplateIndex(ResourceNodeTemplate template)
        {
            if (template == null || resourceTemplates == null)
                return -1;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate candidate = resourceTemplates[i];
                if (ReferenceEquals(candidate, template) ||
                    (candidate != null && candidate.StableHashId == template.StableHashId))
                {
                    return i;
                }
            }

            return -1;
        }

        private SectorState EnsureRuntimeSectorState(int2 sector, long sectorKey)
        {
            if (_residentSectors == null)
                return null;

            if (_residentSectors.TryGetValue(sectorKey, out SectorState state))
                return state;

            state = AcquireSectorStateNoGrowth(sector);
            if (state == null)
                return null;

            if (mapMagicBridge != null)
            {
                state.BrinePool = ResolveBrinePoolState(sector);
                SyncBrineHazardRegistration(state);
            }

            if (!TryRegisterResidentSectorNoGrowth(sectorKey, state))
            {
                UnregisterBrineHazard(ref state.BrinePool);
                ReleaseSectorStateNoGrowth(state);
                return null;
            }

            return state;
        }

        public bool TrySampleBrineFluidDensity(Vector3 runtimePosition, out float fluidDensityKgPerCubicMeter)
        {
            fluidDensityKgPerCubicMeter = 0f;
            if (_residentSectors == null || _residentSectors.Count == 0 || !IsFiniteRuntimePosition(runtimePosition))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                return false;

            int2 sector = QuantizeSector(in aup);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int2 candidateSector = new int2(sector.x + offsetX, sector.y + offsetY);
                    if (!_residentSectors.TryGetValue(ComposeSectorKey(candidateSector), out SectorState state))
                        continue;

                    BrinePoolState brinePool = state.BrinePool;
                    if (brinePool.IsValid == 0 || !IsInsideBrinePool(in brinePool, runtimePosition))
                        continue;

                    fluidDensityKgPerCubicMeter = brinePool.FluidDensityKgPerCubicMeter;
                    return fluidDensityKgPerCubicMeter > 0f;
                }
            }

            return false;
        }

        public bool TrySampleBrineLayer(Vector3 runtimePosition, out BrineLayerSample sample)
        {
            sample = default;
            if (_residentSectors == null || _residentSectors.Count == 0 || !IsFiniteRuntimePosition(runtimePosition))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                return false;

            int2 sector = QuantizeSector(in aup);
            double3 absoluteRuntime = aup.ToAbsoluteDouble3();

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int2 candidateSector = new int2(sector.x + offsetX, sector.y + offsetY);
                    if (!_residentSectors.TryGetValue(ComposeSectorKey(candidateSector), out SectorState state))
                        continue;

                    BrinePoolState brinePool = state.BrinePool;
                    if (brinePool.IsValid == 0 || !IsInsideBrinePool(in brinePool, runtimePosition))
                        continue;

                    float absoluteHeightY = (float)(absoluteRuntime.y + ((double)brinePool.SurfaceHeight - runtimePosition.y));
                    if (!math.isfinite(absoluteHeightY))
                        continue;

                    double invCartographySectorSize = math.rcp((double)BrineLayerConstants.CartographySectorSizeMeters);
                    int2 cartographySector = new int2(
                        (int)math.floor(absoluteRuntime.x * invCartographySectorSize),
                        (int)math.floor(absoluteRuntime.z * invCartographySectorSize));
                    sample = new BrineLayerSample
                    {
                        CartographySector = cartographySector,
                        AbsoluteHeightY = absoluteHeightY,
                        RuntimeHeightY = brinePool.SurfaceHeight,
                        DensityMultiplier = BrineLayerConstants.DensityMultiplier,
                        Toxicity01 = brinePool.ToxicityIntensity,
                        Flags = BrineLayerConstants.SampleValidFlag,
                        Reserved0 = 0,
                        SectorHash = BrineLayerMath.ResolveSectorHash(cartographySector)
                    };
                    return true;
                }
            }

            return false;
        }

        void IRandomEventListener.OnSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            HandleSeismicShockwave(in payload);
        }

        private void HandleSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            if (!Application.isPlaying || tectonicUpwellingRespawnRate <= 0f)
                return;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || _resourceTombstoneScratch == null)
                return;

            int3 affectedChunkId = registry.ResolveRuntimeChunkId(payload.EpicenterWS);
            if (registry.CopyResourceNodeTombstonesInChunk(affectedChunkId, _resourceTombstoneScratch) <= 0)
                return;

            if (!TryResolveShockwaveSeed(in payload, out uint shockSeed))
                return;

            float maxDistanceSqr = math.max(1f, payload.ImpulseRadiusMeters * payload.ImpulseRadiusMeters);
            for (int i = 0; i < _resourceTombstoneScratch.Count; i++)
            {
                ResourceNodeTombstoneRecord tombstone = _resourceTombstoneScratch[i];
                Vector3 runtimePosition = tombstone.Position.ToRuntimeFloat3();
                if ((runtimePosition - payload.EpicenterWS).sqrMagnitude > maxDistanceSqr)
                    continue;

                uint selectionState = Mix(shockSeed, (uint)tombstone.TombstoneId);
                selectionState = Mix(selectionState, (uint)(tombstone.TombstoneId >> 32));
                if (Next01(ref selectionState) > tectonicUpwellingRespawnRate ||
                    !registry.TryReinstateDestroyedResourceNode(tombstone.TombstoneId))
                {
                    continue;
                }

                int2 sector = QuantizeSector(in tombstone.Position);
                long sectorKey = ComposeSectorKey(sector);
                if (_residentSectors != null && _residentSectors.TryGetValue(sectorKey, out SectorState residentState))
                {
                    residentState.SpawnEnvelopeQueued = false;
                    EnqueueSectorEnvelope(residentState, sectorKey);
                }

                SpawnMagmaVentMarker(runtimePosition, payload.ImpulseMagnitude, selectionState);
            }
        }

        private BrinePoolState ResolveBrinePoolState(int2 sector)
        {
            BrinePoolState brinePool = default;
            uint state = SeedSectorCandidate(sector, BrinePoolSeedSalt, 0);
            uint stableSeed = state;

            double absoluteX = (sector.x * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
            double absoluteZ = (sector.y * (double)sectorSizeMeters) + ResolveSectorOffsetMeters(ref state);
            Vector3 runtimeProbe = AbsoluteToRuntime(absoluteX, 0d, absoluteZ);
            if (!mapMagicBridge.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out float bowlFloorHeight) ||
                !math.isfinite(bowlFloorHeight))
            {
                return brinePool;
            }

            float waterSurface = ResolveWaterSurfaceLevel();

            float depthMeters = math.max(0f, waterSurface - bowlFloorHeight);
            if (depthMeters < brinePoolMinimumDepthMeters)
                return brinePool;

            float radiusMeters = math.lerp(brinePoolRadiusMinMeters, brinePoolRadiusMaxMeters, Next01(ref state));
            float thicknessMeters = math.lerp(brinePoolThicknessMinMeters, brinePoolThicknessMaxMeters, Next01(ref state));
            thicknessMeters = math.min(thicknessMeters, math.max(1f, depthMeters * 0.12f));
            float rimSampleRadius = math.max(1f, radiusMeters * 0.94f);
            float rimMinHeight = float.MaxValue;
            for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
            {
                float2 rimDirection = ResolveCardinalDirection(sampleIndex);
                float sampleX = runtimeProbe.x + (rimDirection.x * rimSampleRadius);
                float sampleZ = runtimeProbe.z + (rimDirection.y * rimSampleRadius);
                if (!mapMagicBridge.TryGetHeight(sampleX, sampleZ, out float rimHeight) ||
                    !math.isfinite(rimHeight))
                {
                    return default;
                }

                rimMinHeight = math.min(rimMinHeight, rimHeight);
            }

            if ((rimMinHeight - bowlFloorHeight) < brinePoolMinimumLipMeters)
                return brinePool;

            float maximumContainedSurfaceHeight = rimMinHeight - math.max(0.1f, brinePoolMinimumLipMeters);
            float surfaceHeight = math.min(
                math.min(waterSurface - 0.25f, bowlFloorHeight + thicknessMeters),
                maximumContainedSurfaceHeight);
            if (!math.isfinite(surfaceHeight) || surfaceHeight <= bowlFloorHeight + 0.1f)
                return brinePool;

            brinePool.IsValid = 1;
            brinePool.StableSeed = stableSeed;
            brinePool.Center = new Vector3(runtimeProbe.x, (bowlFloorHeight + surfaceHeight) * 0.5f, runtimeProbe.z);
            brinePool.RadiusMeters = radiusMeters;
            brinePool.BottomHeight = bowlFloorHeight;
            brinePool.SurfaceHeight = surfaceHeight;
            brinePool.ToxicityIntensity = brinePoolToxicityIntensity;
            brinePool.FluidDensityKgPerCubicMeter = brinePoolFluidDensityKgPerCubicMeter;
            return brinePool;
        }

        private void SyncBrineHazardRegistration(SectorState state)
        {
            if (state == null)
                return;

            if (!IsValidBrinePoolState(in state.BrinePool))
            {
                UnregisterBrineHazard(ref state.BrinePool);
                return;
            }

            HazardZoneManager hazardManager = _hazardZoneManager;
            if (hazardManager == null)
            {
                UnregisterBrineHazard(ref state.BrinePool);
                return;
            }

            int zoneId = ResolveBrineHazardZoneId(state.BrinePool.StableSeed);
            bool isSameRegisteredZone =
                state.BrinePool.HazardRegistered != 0 &&
                state.BrinePool.HazardZoneId == zoneId;
            bool isToxicMudCellRegistered =
                isSameRegisteredZone &&
                HectonBrineToxicMudGrid.IsRegisteredCell(zoneId);

            if (state.BrinePool.HazardRegistered != 0 &&
                state.BrinePool.HazardZoneId != zoneId)
            {
                UnregisterBrineHazard(ref state.BrinePool);
                isToxicMudCellRegistered = false;
            }

            float depthMeters = state.BrinePool.SurfaceHeight - state.BrinePool.BottomHeight;
            float radius = math.max(state.BrinePool.RadiusMeters, depthMeters * 0.75f);
            if (!isToxicMudCellRegistered)
            {
                Vector3 brineSurfaceCenter = new Vector3(
                    state.BrinePool.Center.x,
                    state.BrinePool.SurfaceHeight,
                    state.BrinePool.Center.z);
                HectonBrineToxicMudGrid.RegisterCell(
                    zoneId,
                    brineSurfaceCenter,
                    state.BrinePool.RadiusMeters * 2f,
                    state.BrinePool.RadiusMeters * 2f,
                    depthMeters);
                if (!HectonBrineToxicMudGrid.IsRegisteredCell(zoneId))
                {
                    if (isSameRegisteredZone)
                    {
                        hazardManager.UnregisterZone(zoneId);
                        state.BrinePool.HazardRegistered = 0;
                        state.BrinePool.HazardZoneId = 0;
                    }

                    return;
                }
            }

            if (!hazardManager.RegisterZone(
                    zoneId,
                    state.BrinePool.Center,
                    state.BrinePool.ToxicityIntensity,
                    radius,
                    HazardType.Toxicity,
                    brinePoolHazardVisorBias))
            {
                HectonBrineToxicMudGrid.UnregisterCell(zoneId);
                if (state.BrinePool.HazardRegistered != 0 &&
                    state.BrinePool.HazardZoneId == zoneId)
                {
                    hazardManager.UnregisterZone(zoneId);
                    state.BrinePool.HazardRegistered = 0;
                    state.BrinePool.HazardZoneId = 0;
                }

                return;
            }

            state.BrinePool.HazardZoneId = zoneId;
            state.BrinePool.HazardRegistered = 1;
        }

        private void RefreshBrineHazardRegistrations()
        {
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates == null || sectorStates.Length == 0)
                return;

            for (int i = 0; i < sectorStates.Length; i++)
            {
                SectorState state = sectorStates[i];
                if (state == null || !state.IsLeased)
                    continue;

                SyncBrineHazardRegistration(state);
            }
        }

        private void UnregisterAllBrineHazards(HazardZoneManager managerFallback)
        {
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates == null || sectorStates.Length == 0)
                return;

            for (int i = 0; i < sectorStates.Length; i++)
            {
                SectorState state = sectorStates[i];
                if (state == null || !state.IsLeased)
                    continue;

                UnregisterBrineHazard(ref state.BrinePool, managerFallback);
            }
        }

        private void UnregisterBrineHazard(ref BrinePoolState brinePool, HazardZoneManager managerFallback = null)
        {
            if (brinePool.HazardRegistered == 0)
                return;

            HazardZoneManager manager = _hazardZoneManager != null ? _hazardZoneManager : managerFallback;
            if (manager != null)
                manager.UnregisterZone(brinePool.HazardZoneId);
            HectonBrineToxicMudGrid.UnregisterCell(brinePool.HazardZoneId);

            brinePool.HazardRegistered = 0;
            brinePool.HazardZoneId = 0;
        }

        private bool IsSpawnAlreadyQueuedOrActive(SectorState state, in SpawnRequest request)
        {
            return ContainsActiveNodeWithTombstone(state, request.TombstoneId) ||
                   ContainsPendingSpawn(request.SectorKey, request.TombstoneId);
        }

        private bool ContainsActiveNodeWithTombstone(SectorState state, ulong tombstoneId)
        {
            if (state == null || tombstoneId == 0UL)
                return false;

            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node != null && node.PersistentTombstoneId == tombstoneId && node.gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }

        private bool ContainsPendingSpawn(long sectorKey, ulong tombstoneId)
        {
            if (tombstoneId == 0UL)
                return false;

            if (ContainsQueuedSpawn(_pendingSpawns, sectorKey, tombstoneId))
                return true;

            return ContainsQueuedSpawn(_pendingGhostProxySnaps, sectorKey, tombstoneId);
        }

        private static bool ContainsQueuedSpawn(Queue<SpawnRequest> queue, long sectorKey, ulong tombstoneId)
        {
            if (queue == null || queue.Count == 0)
                return false;

            int count = queue.Count;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                SpawnRequest queuedRequest = queue.Dequeue();
                if (!found &&
                    queuedRequest.SectorKey == sectorKey &&
                    queuedRequest.TombstoneId == tombstoneId)
                {
                    found = true;
                }

                queue.Enqueue(queuedRequest);
            }

            return found;
        }

        private static bool IsValidBrinePoolState(in BrinePoolState brinePool)
        {
            return brinePool.IsValid != 0 &&
                   IsFiniteRuntimePosition(brinePool.Center) &&
                   math.isfinite(brinePool.RadiusMeters) &&
                   math.isfinite(brinePool.BottomHeight) &&
                   math.isfinite(brinePool.SurfaceHeight) &&
                   math.isfinite(brinePool.ToxicityIntensity) &&
                   math.isfinite(brinePool.FluidDensityKgPerCubicMeter) &&
                   brinePool.RadiusMeters > 0f &&
                   brinePool.SurfaceHeight > brinePool.BottomHeight &&
                   brinePool.ToxicityIntensity > 0f &&
                   brinePool.FluidDensityKgPerCubicMeter > 0f;
        }

        private bool IsInsideBrinePool(in BrinePoolState brinePool, Vector3 runtimePosition)
        {
            if (!IsValidBrinePoolState(in brinePool) ||
                !IsFiniteRuntimePosition(runtimePosition) ||
                runtimePosition.y < brinePool.BottomHeight ||
                runtimePosition.y > brinePool.SurfaceHeight)
            {
                return false;
            }

            float deltaX = runtimePosition.x - brinePool.Center.x;
            float deltaZ = runtimePosition.z - brinePool.Center.z;
            float radius = math.max(0.01f, brinePool.RadiusMeters);
            return ((deltaX * deltaX) + (deltaZ * deltaZ)) <= (radius * radius);
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private void SanitizeBrinePoolSettings()
        {
            brinePoolRadiusMinMeters = ResolveFiniteAtLeast(brinePoolRadiusMinMeters, DefaultBrinePoolRadiusMinMeters, 4f);
            brinePoolRadiusMaxMeters = ResolveFiniteAtLeast(brinePoolRadiusMaxMeters, DefaultBrinePoolRadiusMaxMeters, brinePoolRadiusMinMeters);
            brinePoolThicknessMinMeters = ResolveFiniteAtLeast(brinePoolThicknessMinMeters, DefaultBrinePoolThicknessMinMeters, 1f);
            brinePoolThicknessMaxMeters = ResolveFiniteAtLeast(brinePoolThicknessMaxMeters, DefaultBrinePoolThicknessMaxMeters, brinePoolThicknessMinMeters);
            brinePoolMinimumDepthMeters = ResolveFiniteAtLeast(brinePoolMinimumDepthMeters, DefaultBrinePoolMinimumDepthMeters, 1000f);
            brinePoolMinimumLipMeters = ResolveFiniteAtLeast(brinePoolMinimumLipMeters, DefaultBrinePoolMinimumLipMeters, 0.5f);
            brinePoolToxicityIntensity = ResolveFiniteSaturate(brinePoolToxicityIntensity, DefaultBrinePoolToxicityIntensity);
            brinePoolHazardVisorBias = ResolveFiniteAtLeast(brinePoolHazardVisorBias, DefaultBrinePoolHazardVisorBias, 0f);
            brinePoolFluidDensityKgPerCubicMeter = ResolveFiniteAtLeast(
                brinePoolFluidDensityKgPerCubicMeter,
                DefaultBrinePoolFluidDensityKgPerCubicMeter,
                HectonPhysicsContract.WaterDensityKgPerCubicMeterConst);
        }

        private static float ResolveFiniteAtLeast(float value, float fallback, float minimum)
        {
            float safeFallback = math.isfinite(fallback) ? fallback : minimum;
            return math.isfinite(value) ? math.max(minimum, value) : math.max(minimum, safeFallback);
        }

        private static float ResolveFiniteSaturate(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        private void TryApplyEmbeddedVein(ResourceNode node, ResourceNodeTemplate template, in SpawnRequest request)
        {
            if (node == null ||
                template == null ||
                !template.EmbedInVoxelRock ||
                voxelEngine == null ||
                !voxelEngine.TryGetNearestActiveVolume(request.RuntimePosition, out HectonVoxelVolume volume) ||
                volume == null)
            {
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(request.RuntimePosition, out AbsoluteUniversePosition requestAup))
                return;

            double3 absolutePosition = requestAup.ToAbsoluteDouble3();
            Vector3 absoluteStart = new Vector3((float)absolutePosition.x, (float)absolutePosition.y, (float)absolutePosition.z);
            float2 veinPlanarDirection = ResolveOctantDirection(QuantizeYawDegreesToOctant(request.YawDegrees));
            Vector3 veinDirection = new Vector3(veinPlanarDirection.x, -0.35f, veinPlanarDirection.y);
            volume.TryApplyEmbeddedOreVein(
                absoluteStart,
                veinDirection,
                template.EmbeddedVeinLengthMeters,
                template.EmbeddedVeinRadiusMeters,
                template.EmbeddedVeinNoiseAmplitudeMeters,
                template.EmbeddedVeinStampCount,
                request.StableSeed);
        }

        private void SpawnMagmaVentMarker(Vector3 runtimePosition, float impulseMagnitude, uint stableSeed)
        {
            if (_authoredMagmaVentPrefab == null)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            if (!HasAuthoredPoolReserve(pool, _authoredMagmaVentPrefab))
            {
                return;
            }

            GameObject marker = pool.Spawn(_authoredMagmaVentPrefab, runtimePosition, Quaternion.identity, false);
            if (marker == null)
                return;

            float magnitude01 = math.saturate(impulseMagnitude / 40f);
            float height = math.lerp(2.5f, 6.5f, magnitude01);
            float radius = math.lerp(0.75f, 1.65f, Next01(ref stableSeed));
            marker.transform.localScale = new Vector3(radius, height, radius);
            pool.Despawn(marker, magmaVentLifetimeSeconds);
        }

        private bool TryResolveShockwaveSeed(in SeismicShockwaveEvent payload, out uint seed)
        {
            seed = 0u;
            if (!TryResolveAupFromRuntimeOrigin(payload.EpicenterWS, out AbsoluteUniversePosition epicenter))
                return false;

            int2 sector = QuantizeSector(in epicenter);
            seed = SeedSectorCandidate(sector, MagmaVentSeedSalt, payload.AppliedStampCount);
            seed = Mix(seed, (uint)math.asint(payload.ImpulseRadiusMeters));
            seed = Mix(seed, (uint)math.asint(payload.ImpulseMagnitude));
            return true;
        }

        private static int ResolveBrineHazardZoneId(uint stableSeed)
        {
            return (int)(stableSeed ^ BrinePoolHazardIdSalt);
        }

        private static int ResolveMeteorRadiationHazardZoneId(uint stableSeed)
        {
            return (int)(stableSeed ^ MeteorRadiationHazardIdSalt);
        }

        private void CompactSectorNodes(SectorState state)
        {
            if (state == null)
                return;

            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                ResourceNode node = nodes[i];
                if (node == null || !node.gameObject.activeInHierarchy)
                    nodes.RemoveAt(i);
            }
        }

        private void EvictSector(long sectorKey)
        {
            if (!_residentSectors.TryGetValue(sectorKey, out SectorState state))
                return;

            UnregisterBrineHazard(ref state.BrinePool);
            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null)
                    continue;

                TryQueueNodeDeactivationNoGrowth(node.gameObject);
            }

            _residentSectors.Remove(sectorKey);
            ReleaseSectorStateNoGrowth(state);
        }

        private void FlushPendingNodeDeactivations()
        {
            TryResolveCachedObjectPool(out IObjectPoolService pool);
            for (int i = 0; i < _pendingNodeDeactivations.Count; i++)
            {
                GameObject target = _pendingNodeDeactivations[i];
                if (target == null)
                    continue;

                DespawnKnownPooledResourceOrDisable(pool, target);
            }

            if (_pendingNodeDeactivations.Count > 0)
                _pendingNodeDeactivations.Clear();
        }

        private void DespawnAllResidentNodes()
        {
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates == null || sectorStates.Length == 0)
                return;

            _sectorEvictionScratch.Clear();
            for (int i = 0; i < sectorStates.Length; i++)
            {
                SectorState state = sectorStates[i];
                if (state == null || !state.IsLeased)
                    continue;

                TryQueueSectorEvictionNoGrowth(ComposeSectorKey(state.Coordinates));
            }

            for (int i = 0; i < _sectorEvictionScratch.Count; i++)
                EvictSector(_sectorEvictionScratch[i]);

            _sectorEvictionScratch.Clear();
        }

        private bool IsBlockedByVoxelSolid(Vector3 runtimePosition)
        {
            return !TryValidateSpawnRuntimePositionViaSdf(runtimePosition, math.max(0.05f, voxelSolidThreshold), out _);
        }

        private float ResolveWaterSurfaceLevel()
        {
            if (TryResolveOceanWaterSurfaceLevel(out float oceanWaterSurfaceLevel))
                return oceanWaterSurfaceLevel;

            MapMagicBridge bridge = mapMagicBridge;
            if (bridge != null && TryResolveWaterSurfaceLevel(bridge.WaterSurfaceLevel, out float waterSurfaceLevel))
                return waterSurfaceLevel;

            return DefaultWaterSurfaceLevelY;
        }

        private bool TryResolveOceanWaterSurfaceLevel(out float waterSurfaceLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceLevel(oceanKinematics.SeaLevel, out waterSurfaceLevel))
            {
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) > 0.0001f &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private float ResolveTemperature(Vector3 runtimePosition)
        {
            return vegetationBridge != null
                ? vegetationBridge.GetWaterTemperature(runtimePosition)
                : 0f;
        }

        private bool TryResolveSurfacePlacement(
            Vector3 surfaceAnchorPosition,
            float surfaceOffsetMeters,
            float yawDegrees,
            out Vector3 runtimePosition,
            out Quaternion rotation)
        {
            Vector3 surfaceNormal = ResolveSurfaceNormal(surfaceAnchorPosition);
            runtimePosition = surfaceAnchorPosition + (surfaceNormal * math.max(0f, surfaceOffsetMeters));
            rotation = ResolveSurfaceRotation(surfaceNormal, yawDegrees);
            return true;
        }

        private Vector3 ResolveSurfaceNormal(Vector3 runtimePosition)
        {
            if (voxelEngine != null &&
                voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) &&
                volume != null &&
                CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
            {
                Vector3 localPoint = volume.transform.InverseTransformPoint(runtimePosition);
                localBounds.Expand(math.max(1f, slopeSampleDistanceMeters));
                if (localBounds.Contains(localPoint) &&
                    volume.TrySampleSurfaceNormal(runtimePosition, math.max(0.2f, slopeSampleDistanceMeters * 0.25f), out Vector3 voxelSurfaceNormal))
                {
                    return voxelSurfaceNormal;
                }
            }

            return ResolveTerrainNormal(runtimePosition);
        }

        private Vector3 ResolveTerrainNormal(Vector3 runtimePosition)
        {
            float probe = math.max(0.5f, slopeSampleDistanceMeters);
            if (!mapMagicBridge.TryGetHeight(runtimePosition.x + probe, runtimePosition.z, out float heightPosX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x - probe, runtimePosition.z, out float heightNegX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z + probe, out float heightPosZ) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z - probe, out float heightNegZ))
            {
                return Vector3.up;
            }

            float gradientX = (heightPosX - heightNegX) / (probe * 2f);
            float gradientZ = (heightPosZ - heightNegZ) / (probe * 2f);
            Vector3 terrainNormal = new Vector3(-gradientX, 1f, -gradientZ);
            return terrainNormal.sqrMagnitude > 0.000001f ? terrainNormal : Vector3.up;
        }

        private static Quaternion ResolveSurfaceRotation(Vector3 surfaceNormal, float yawDegrees)
        {
            Vector3 up = surfaceNormal.sqrMagnitude > 0.000001f ? surfaceNormal : Vector3.up;
            float2 yawDirection = ResolveOctantDirection(QuantizeYawDegreesToOctant(yawDegrees));
            Vector3 authoredForward = new Vector3(yawDirection.x, 0f, yawDirection.y);
            Vector3 tangentForward = authoredForward - (up * Vector3.Dot(authoredForward, up));
            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.forward - (up * Vector3.Dot(Vector3.forward, up));

            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.Cross(up, Vector3.right);

            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.forward;

            return Quaternion.LookRotation(tangentForward, up);
        }

        private static int QuantizeYawDegreesToOctant(float yawDegrees)
        {
            float wrapped = yawDegrees - (math.floor(yawDegrees / 360f) * 360f);
            return (int)math.floor((wrapped + 22.5f) * 0.0222222228f) & 7;
        }

        private static float2 ResolveOctantDirection(int sector)
        {
            switch (sector & 7)
            {
                case 0:
                    return new float2(1f, 0f);
                case 1:
                    return new float2(0.70710677f, 0.70710677f);
                case 2:
                    return new float2(0f, 1f);
                case 3:
                    return new float2(-0.70710677f, 0.70710677f);
                case 4:
                    return new float2(-1f, 0f);
                case 5:
                    return new float2(-0.70710677f, -0.70710677f);
                case 6:
                    return new float2(0f, -1f);
                default:
                    return new float2(0.70710677f, -0.70710677f);
            }
        }

        private static float2 ResolveCardinalDirection(int sector)
        {
            switch (sector & 3)
            {
                case 0:
                    return new float2(1f, 0f);
                case 1:
                    return new float2(0f, 1f);
                case 2:
                    return new float2(-1f, 0f);
                default:
                    return new float2(0f, -1f);
            }
        }

        private static float3 ResolveDominantSurfaceNormal(float3 value)
        {
            if (!math.all(math.isfinite(value)))
                return new float3(0f, 1f, 0f);

            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            if (math.max(math.max(ax, ay), az) <= 0.000001f)
                return new float3(0f, 1f, 0f);

            if (ay >= ax && ay >= az)
                return new float3(0f, value.y < 0f ? -1f : 1f, 0f);

            return ax >= az
                ? new float3(value.x < 0f ? -1f : 1f, 0f, 0f)
                : new float3(0f, 0f, value.z < 0f ? -1f : 1f);
        }

        private float ResolveSlope(Vector3 runtimePosition)
        {
            float probe = math.max(0.5f, slopeSampleDistanceMeters);
            if (!mapMagicBridge.TryGetHeight(runtimePosition.x + probe, runtimePosition.z, out float heightPosX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x - probe, runtimePosition.z, out float heightNegX) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z + probe, out float heightPosZ) ||
                !mapMagicBridge.TryGetHeight(runtimePosition.x, runtimePosition.z - probe, out float heightNegZ))
            {
                return 0f;
            }

            float gradientX = (heightPosX - heightNegX) / (probe * 2f);
            float gradientZ = (heightPosZ - heightNegZ) / (probe * 2f);
            float gradientMagnitude = FastMagnitudeApprox(new float2(gradientX, gradientZ));
            return FastAtanDegreesPositive(gradientMagnitude);
        }

        private static float ResolveCinematicRadialDistance(ref uint state, float maxRadius)
        {
            float t = Next01(ref state);
            return math.max(0f, maxRadius) * t;
        }

        private static float FastMagnitudeApprox(float2 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float max = math.max(ax, ay);
            float min = math.min(ax, ay);
            return max + (min * 0.41421356f);
        }

        private static float FastAtanDegreesPositive(float value)
        {
            float x = math.max(0f, value);
            float reciprocal = 1f / math.max(x, 0.000001f);
            bool useReciprocal = x > 1f;
            float y = math.select(x, reciprocal, useReciprocal);
            float radians = y / (1f + 0.280872f * y * y);
            radians = math.select(radians, 1.5707964f - radians, useReciprocal);
            return radians * 57.29578f;
        }

        private int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            double sectorSize = math.max(1d, sectorSizeMeters);
            return new int2(
                (int)math.floor(absolute.x / sectorSize),
                (int)math.floor(absolute.z / sectorSize));
        }

        private Vector3 AbsoluteToRuntime(double absoluteX, double absoluteY, double absoluteZ)
        {
            AbsoluteUniversePosition candidate = AbsoluteUniversePosition.FromAbsolutePosition(new double3(absoluteX, absoluteY, absoluteZ));
            float3 runtime = candidate.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private float ResolveSectorOffsetMeters(ref uint state)
        {
            float margin = math.clamp(sectorEdgeMarginMeters, 0f, sectorSizeMeters * 0.45f);
            float usableSpan = math.max(1f, sectorSizeMeters - (margin * 2f));
            return margin + (Next01(ref state) * usableSpan);
        }

        private static long ComposeSectorKey(int2 sector)
        {
            return ((long)sector.x << 32) ^ (uint)sector.y;
        }

        private static uint SeedSectorCandidate(int2 sector, int templateHash, int candidateIndex)
        {
            uint seed = 2166136261u;
            seed = Mix(seed, (uint)sector.x);
            seed = Mix(seed, (uint)sector.y);
            seed = Mix(seed, (uint)templateHash);
            seed = Mix(seed, (uint)candidateIndex);
            return seed != 0u ? seed : 0xA341316Cu;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash;
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void UpdateDiagnostics(int2 playerSector)
        {
            _debugResidentSectorCount = _residentSectors != null ? _residentSectors.Count : 0;
            _debugQueuedSpawnCount = _pendingSpawns != null ? _pendingSpawns.Count : 0;

            int activeNodeCount = 0;
            int activeBrinePoolCount = 0;
            SectorState[] sectorStates = _sectorStatePool;
            if (sectorStates != null)
            {
                for (int i = 0; i < sectorStates.Length; i++)
                {
                    SectorState state = sectorStates[i];
                    if (state == null || !state.IsLeased)
                        continue;

                    activeNodeCount += state.ActiveNodes.Count;
                    if (state.BrinePool.IsValid != 0)
                        activeBrinePoolCount++;
                }
            }

            _debugActiveNodeCount = activeNodeCount;
            _debugActiveBrinePoolCount = activeBrinePoolCount;
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);
        }

#if UNITY_EDITOR
        private const string EditorVoidGlassMeteoriteTemplatePath =
            "Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_VoidGlassMeteorite.asset";

        private void OnValidate()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            sectorRadius = math.clamp(sectorRadius, 0, 3);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);
            sectorEdgeMarginMeters = math.max(0f, sectorEdgeMarginMeters);
            SanitizeBrinePoolSettings();
            tectonicUpwellingRespawnRate = math.clamp(tectonicUpwellingRespawnRate, 0f, 0.25f);
            magmaVentLifetimeSeconds = math.max(1f, magmaVentLifetimeSeconds);
            meteorImpactIntervalSeconds = math.clamp(meteorImpactIntervalSeconds, 60f, 1800f);
            meteorImpactChancePerWindow = math.saturate(meteorImpactChancePerWindow);
            meteorImpactSearchRadiusMeters = math.clamp(meteorImpactSearchRadiusMeters, 24f, 256f);
            meteorImpactCraterRadiusMeters = math.clamp(meteorImpactCraterRadiusMeters, 1f, 16f);
            meteoriteRadiationIntensity = ResolveFiniteSaturate(meteoriteRadiationIntensity, DefaultMeteoriteRadiationIntensity);
            meteoriteRadiationRadiusMeters = math.clamp(
                ResolveFiniteAtLeast(meteoriteRadiationRadiusMeters, DefaultMeteoriteRadiationRadiusMeters, 4f),
                4f,
                40f);
            meteoriteRadiationVisorBias = math.clamp(
                ResolveFiniteAtLeast(meteoriteRadiationVisorBias, DefaultMeteoriteRadiationVisorBias, 0f),
                0f,
                2f);

            if (voidGlassMeteoriteTemplate == null)
                voidGlassMeteoriteTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceNodeTemplate>(EditorVoidGlassMeteoriteTemplatePath);

            CacheSpecialResourceTemplatesCold();
        }
#endif
    }
}
