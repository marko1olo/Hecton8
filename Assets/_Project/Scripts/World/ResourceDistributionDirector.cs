using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Scavenging;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic environmental-envelope spawner for harvestable resource nodes.
    /// Uses AUP sector quantization, MapMagic seabed queries, cached thermal/slope envelopes,
    /// and voxel-density rejection so resources are placed by conditions instead of biome labels.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4042)]
    public sealed class ResourceDistributionDirector : MonoBehaviour, ISlowTickable
    {
        private const int DefaultSectorSizeMeters = 128;
        private const int DefaultMaxPendingSpawnRequests = 1024;
        private const int DefaultPoolWarmupFloor = 64;
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
        private const float GhostAlpha = 0.24f;
        private const string RuntimePrefabName = "PFB_RuntimeResourceNode_Generic";
        private const string RuntimeMagmaVentPrefabName = "PFB_RuntimeMagmaVent_Generic";
        private const string ThermalDiamondStableId = "resource.node.thermal_diamond";
        private const int BrinePoolSeedSalt = unchecked((int)0x4252494E);
        private const int BrinePoolHazardIdSalt = unchecked((int)0x52494E45);
        private const int MagmaVentSeedSalt = unchecked((int)0x56454E54);

        internal static ResourceDistributionDirector ActiveRuntimeInstance { get; private set; }

        private struct BrinePoolState
        {
            public bool IsValid;
            public bool HazardRegistered;
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
            public readonly int2 Coordinates;
            public readonly List<ResourceNode> ActiveNodes;
            public bool SpawnEnvelopeQueued;
            public BrinePoolState BrinePool;

            public SectorState(int2 coordinates, int initialCapacity)
            {
                Coordinates = coordinates;
                // COLD ALLOC: List<ResourceNode>[initialCapacity] — live sector resource node registry — owner: ResourceDistributionDirector
                ActiveNodes = new List<ResourceNode>(initialCapacity);
                SpawnEnvelopeQueued = false;
                BrinePool = default;
            }
        }

        private struct SpawnRequest
        {
            public long SectorKey;
            public int TemplateIndex;
            public Vector3 RuntimePosition;
            public Quaternion Rotation;
            public float YawDegrees;
            public uint StableSeed;
            public ulong TombstoneId;
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("Authored resource-node templates consumed by the environmental-envelope spawner.")]
        private ResourceNodeTemplate[] resourceTemplates;

        [SerializeField]
        [Tooltip("Optional explicit template spawned by flash-freeze crystallization. If empty, the director resolves resource.node.thermal_diamond from resourceTemplates.")]
        private ResourceNodeTemplate thermalDiamondTemplate;

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

        [SerializeField, Min(1025f)]
        [Tooltip("Fluid density in kg/m3 used by buoyancy overrides inside deterministic brine pools.")]
        private float brinePoolFluidDensityKgPerCubicMeter = DefaultBrinePoolFluidDensityKgPerCubicMeter;

        [Header("Tectonic Upwelling")]
        [SerializeField, Range(0f, 0.25f)]
        [Tooltip("Fraction of tombstoned nodes in an affected chunk that are eligible for seismic reinstatement.")]
        private float tectonicUpwellingRespawnRate = DefaultUpwellingRespawnRate;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Lifetime in seconds of the temporary magma-vent marker spawned during upwelling reinstatement.")]
        private float magmaVentLifetimeSeconds = DefaultMagmaVentLifetimeSeconds;

        [Header("Diagnostics")]
        [SerializeField] private int _debugResidentSectorCount;
        [SerializeField] private int _debugActiveNodeCount;
        [SerializeField] private int _debugQueuedSpawnCount;
        [SerializeField] private int _debugLastAcceptedTemplateHash;
        [SerializeField] private Vector2Int _debugPlayerSector;
        [SerializeField] private int _debugActiveBrinePoolCount;

        // COLD ALLOC: Dictionary<long,SectorState>[32] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
        private Dictionary<long, SectorState> _residentSectors;
        // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
        private Queue<SpawnRequest> _pendingSpawns;
        // COLD ALLOC: List<long>[32] — sector eviction scratch list — owner: ResourceDistributionDirector
        private List<long> _sectorEvictionScratch;

        private GameObject _runtimePrefab;
        private GameObject _magmaVentPrefab;
        private Mesh _ghostCubeMesh;
        private Mesh _ghostCylinderMesh;
        private Material _ghostMaterial;
        private Material _magmaVentMaterial;
        private bool _runtimePoolReady;
        private bool _slowTickRegistered;
        private bool _seismicHookRegistered;
        private int _computedPoolWarmupCount;
        // COLD ALLOC: List<ResourceNodeTombstoneRecord>[64] — tectonic-upwelling scratch tombstone staging — owner: ResourceDistributionDirector
        private List<ResourceNodeTombstoneRecord> _resourceTombstoneScratch;

        private void Awake()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            sectorEdgeMarginMeters = math.clamp(sectorEdgeMarginMeters, 0f, sectorSizeMeters * 0.25f);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);
            brinePoolRadiusMinMeters = math.max(4f, brinePoolRadiusMinMeters);
            brinePoolRadiusMaxMeters = math.max(brinePoolRadiusMinMeters, brinePoolRadiusMaxMeters);
            brinePoolThicknessMinMeters = math.max(1f, brinePoolThicknessMinMeters);
            brinePoolThicknessMaxMeters = math.max(brinePoolThicknessMinMeters, brinePoolThicknessMaxMeters);
            brinePoolMinimumDepthMeters = math.max(1000f, brinePoolMinimumDepthMeters);
            brinePoolMinimumLipMeters = math.max(0.5f, brinePoolMinimumLipMeters);
            brinePoolToxicityIntensity = math.saturate(brinePoolToxicityIntensity);
            brinePoolHazardVisorBias = math.max(0f, brinePoolHazardVisorBias);
            brinePoolFluidDensityKgPerCubicMeter = math.max(1025f, brinePoolFluidDensityKgPerCubicMeter);
            tectonicUpwellingRespawnRate = math.clamp(tectonicUpwellingRespawnRate, 0f, 0.25f);
            magmaVentLifetimeSeconds = math.max(1f, magmaVentLifetimeSeconds);

            // COLD ALLOC: Dictionary<long,SectorState>[32] — resident sector registry keyed by AUP sector hash — owner: ResourceDistributionDirector
            _residentSectors = new Dictionary<long, SectorState>(32);
            // COLD ALLOC: Queue<SpawnRequest>[DefaultMaxPendingSpawnRequests] — deterministic deferred resource spawn queue — owner: ResourceDistributionDirector
            _pendingSpawns = new Queue<SpawnRequest>(DefaultMaxPendingSpawnRequests);
            // COLD ALLOC: List<long>[32] — sector eviction scratch list — owner: ResourceDistributionDirector
            _sectorEvictionScratch = new List<long>(32);
            // COLD ALLOC: List<ResourceNodeTombstoneRecord>[64] — tectonic-upwelling scratch tombstone staging — owner: ResourceDistributionDirector
            _resourceTombstoneScratch = new List<ResourceNodeTombstoneRecord>(64);

            EnsureRuntimePrefab();
            UpdateDiagnostics(default);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ActiveRuntimeInstance = this;

            if (!_slowTickRegistered && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = true;
            }

            if (!_seismicHookRegistered)
            {
                RandomEventEvents.OnSeismicShockwave += HandleSeismicShockwave;
                _seismicHookRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_seismicHookRegistered)
            {
                RandomEventEvents.OnSeismicShockwave -= HandleSeismicShockwave;
                _seismicHookRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            DespawnAllResidentNodes();
            _residentSectors?.Clear();
            _pendingSpawns?.Clear();
            _runtimePoolReady = false;
            ActiveRuntimeInstance = ReferenceEquals(ActiveRuntimeInstance, this) ? null : ActiveRuntimeInstance;
            UpdateDiagnostics(default);
        }

        /// <summary>
        /// Slow-tick residency pass. Maintains deterministic resource sectors around the player.
        /// </summary>
        public void SlowTick()
        {
            if (!TryResolveRuntimeDependencies())
                return;

            EnsureRuntimePool();

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            int2 playerSector = QuantizeSector(in playerAup);
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);

            RefreshResidentSectors(playerSector);
            ProcessPendingSpawns();
            UpdateDiagnostics(playerSector);
        }

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
            EnsureRuntimePool();

            if (!Application.isPlaying ||
                _residentSectors == null ||
                !_runtimePoolReady ||
                _runtimePrefab == null ||
                !TryResolveThermalDiamondTemplate(out ResourceNodeTemplate template))
            {
                return false;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return false;

            float safeRadius = math.max(0.25f, crystallizationRadiusMeters);
            Vector3 spawnPosition = ResolveThermalDiamondVoxelFacePosition(runtimePosition, safeRadius, template);
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(spawnPosition);
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            AbsoluteUniversePosition spawnAup = AbsoluteUniversePosition.FromRuntimePosition(spawnPosition);
            int2 sector = QuantizeSector(in spawnAup);
            long sectorKey = ComposeSectorKey(sector);
            SectorState sectorState = ResolveOrCreateRuntimeSectorState(sector, sectorKey);
            if (sectorState == null || ContainsActiveNodeWithTombstone(sectorState, tombstoneId))
                return false;

            uint yawSeed = SeedSectorCandidate(sector, template.StableHashId, (int)(sourceId & 0x7FFFFFFFu));
            yawSeed = Mix(yawSeed, (uint)math.asint(deltaTemperatureCelsius));
            float yawDegrees = Next01(ref yawSeed) * 360f;
            Quaternion rotation = ResolveSurfaceRotation(Vector3.up, yawDegrees);
            GameObject instance = pool.Spawn(_runtimePrefab, spawnPosition, rotation);
            if (instance == null)
                return false;

            if (!instance.TryGetComponent(out ResourceNode node))
            {
                pool.Despawn(instance);
                return false;
            }

            node.ApplyRuntimeTemplate(template, _ghostCubeMesh, _ghostMaterial);
            node.RefreshRuntimeSpatialRegistration();
            sectorState.ActiveNodes.Add(node);
            _debugLastAcceptedTemplateHash = template.StableHashId;
            return true;
        }

        private Vector3 ResolveThermalDiamondVoxelFacePosition(
            Vector3 runtimePosition,
            float safeRadius,
            ResourceNodeTemplate template)
        {
            float spawnOffset = math.max(template.SpawnOffsetMeters, safeRadius * 0.08f);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            if (voxelEngine != null &&
                voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) &&
                volume != null &&
                volume.TrySampleSurfaceNormal(runtimePosition, math.max(0.25f, safeRadius * 0.25f), out Vector3 surfaceNormal))
            {
                return runtimePosition + surfaceNormal * spawnOffset;
            }

            return runtimePosition + Vector3.up * spawnOffset;
        }

        private bool TryResolveRuntimeDependencies()
        {
            if (resourceTemplates == null || resourceTemplates.Length == 0)
                return false;

            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform) || playerTransform == null)
                return false;

            if (!WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge) || mapMagicBridge == null)
                return false;

            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            return true;
        }

        private void EnsureRuntimePrefab()
        {
            if (_runtimePrefab != null)
                return;

            _ghostCubeMesh = CaptureCubeMesh();
            _ghostCylinderMesh = CaptureCylinderMesh();
            _ghostMaterial = CreateGhostMaterial();
            _magmaVentMaterial = CreateMagmaVentMaterial();

            // COLD ALLOC: GameObject[1] — generic pooled runtime resource-node prefab template — owner: ResourceDistributionDirector
            _runtimePrefab = new GameObject(RuntimePrefabName);
            _runtimePrefab.transform.SetParent(transform, false);
            _runtimePrefab.SetActive(false);

            MeshFilter meshFilter = _runtimePrefab.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _ghostCubeMesh;

            MeshRenderer meshRenderer = _runtimePrefab.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _ghostMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            BoxCollider boxCollider = _runtimePrefab.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one;

            SphereCollider sphereCollider = _runtimePrefab.AddComponent<SphereCollider>();
            sphereCollider.enabled = false;
            sphereCollider.radius = 0.5f;

            _runtimePrefab.AddComponent<ResourceNode>();

            if (_ghostCylinderMesh == null || _magmaVentMaterial == null)
                return;

            // COLD ALLOC: GameObject[1] — temporary tectonic-upwelling marker prefab template — owner: ResourceDistributionDirector
            _magmaVentPrefab = new GameObject(RuntimeMagmaVentPrefabName);
            _magmaVentPrefab.transform.SetParent(transform, false);
            _magmaVentPrefab.SetActive(false);

            MeshFilter magmaMeshFilter = _magmaVentPrefab.AddComponent<MeshFilter>();
            magmaMeshFilter.sharedMesh = _ghostCylinderMesh;

            MeshRenderer magmaMeshRenderer = _magmaVentPrefab.AddComponent<MeshRenderer>();
            magmaMeshRenderer.sharedMaterial = _magmaVentMaterial;
            magmaMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            magmaMeshRenderer.receiveShadows = false;
        }

        private void EnsureRuntimePool()
        {
            if (_runtimePoolReady || _runtimePrefab == null)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            _computedPoolWarmupCount = ComputeRequiredPoolWarmupCount();
            int warmupCount = math.max(poolWarmupFloor, _computedPoolWarmupCount);
            if (!pool.HasPool(_runtimePrefab))
                pool.Warmup(_runtimePrefab, warmupCount);

            if (_magmaVentPrefab != null && !pool.HasPool(_magmaVentPrefab))
                pool.Warmup(_magmaVentPrefab, math.max(4, ((sectorRadius * 2) + 1) * ((sectorRadius * 2) + 1)));

            _runtimePoolReady = pool.HasPool(_runtimePrefab);
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
            Dictionary<long, SectorState>.Enumerator residentEnumerator = _residentSectors.GetEnumerator();
            while (residentEnumerator.MoveNext())
            {
                SectorState state = residentEnumerator.Current.Value;
                int deltaX = math.abs(state.Coordinates.x - playerSector.x);
                int deltaY = math.abs(state.Coordinates.y - playerSector.y);
                if (deltaX > sectorRadius || deltaY > sectorRadius)
                    _sectorEvictionScratch.Add(residentEnumerator.Current.Key);
                else
                {
                    CompactSectorNodes(state);
                    SyncBrineHazardRegistration(state);
                }
            }

            residentEnumerator.Dispose();

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

                    SectorState state = new SectorState(sector, ComputePerSectorInitialCapacity());
                    _residentSectors.Add(sectorKey, state);
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

            state.BrinePool = ResolveBrinePoolState(state.Coordinates);
            SyncBrineHazardRegistration(state);
            state.SpawnEnvelopeQueued = true;
            for (int templateIndex = 0; templateIndex < resourceTemplates.Length; templateIndex++)
            {
                ResourceNodeTemplate template = resourceTemplates[templateIndex];
                if (template == null)
                    continue;

                int acceptedForTemplate = 0;
                int candidateBudget = template.CandidateBudgetPerSector;
                for (int candidateIndex = 0; candidateIndex < candidateBudget; candidateIndex++)
                {
                    if (_pendingSpawns.Count >= DefaultMaxPendingSpawnRequests ||
                        acceptedForTemplate >= template.MaxInstancesPerSector)
                    {
                        return;
                    }

                    if (!TryBuildSpawnRequest(state.Coordinates, sectorKey, template, templateIndex, candidateIndex, in state.BrinePool, out SpawnRequest request) ||
                        IsSpawnAlreadyQueuedOrActive(state, in request))
                    {
                        continue;
                    }

                    _pendingSpawns.Enqueue(request);
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
                if (!brinePool.IsValid)
                    return false;

                float angleRadians = Next01(ref state) * math.PI * 2f;
                float radialDistance = math.sqrt(Next01(ref state)) * math.max(1f, brinePool.RadiusMeters * 0.82f);
                float sampleX = brinePool.Center.x + (math.cos(angleRadians) * radialDistance);
                float sampleZ = brinePool.Center.z + (math.sin(angleRadians) * radialDistance);
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
                if (brinePool.IsValid && IsInsideBrinePool(in brinePool, surfaceAnchorPosition))
                    return false;
            }

            float yawDegrees = Next01(ref state) * 360f;
            if (!TryResolveSurfacePlacement(surfaceAnchorPosition, template.SpawnOffsetMeters, yawDegrees, out Vector3 runtimePosition, out Quaternion rotation))
                return false;

            float waterSurface = mapMagicBridge.WaterSurfaceLevel;
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

            if (IsBlockedByVoxelSolid(runtimePosition))
                return false;

            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(runtimePosition);
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry != null && registry.IsResourceNodeTombstoned(tombstoneId))
                return false;

            request = new SpawnRequest
            {
                SectorKey = sectorKey,
                TemplateIndex = templateIndex,
                RuntimePosition = runtimePosition,
                Rotation = rotation,
                YawDegrees = yawDegrees,
                StableSeed = seed,
                TombstoneId = tombstoneId
            };
            return true;
        }

        private void ProcessPendingSpawns()
        {
            if (!_runtimePoolReady || _runtimePrefab == null || _pendingSpawns.Count == 0)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            int processedCount = 0;
            while (processedCount < maxSpawnsPerSlowTick && _pendingSpawns.Count > 0)
            {
                SpawnRequest request = _pendingSpawns.Peek();
                if (!_residentSectors.TryGetValue(request.SectorKey, out SectorState sectorState))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if ((uint)request.TemplateIndex >= (uint)resourceTemplates.Length)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                if (ContainsActiveNodeWithTombstone(sectorState, request.TombstoneId))
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                ResourceNodeTemplate template = resourceTemplates[request.TemplateIndex];
                if (template == null)
                {
                    _pendingSpawns.Dequeue();
                    continue;
                }

                GameObject instance = pool.Spawn(_runtimePrefab, request.RuntimePosition, request.Rotation);
                if (instance == null)
                    break;

                _pendingSpawns.Dequeue();
                processedCount++;

                if (!instance.TryGetComponent(out ResourceNode node))
                {
                    pool.Despawn(instance);
                    continue;
                }

                node.ApplyRuntimeTemplate(template, _ghostCubeMesh, _ghostMaterial);
                TryApplyEmbeddedVein(node, template, in request);
                node.RefreshRuntimeSpatialRegistration();
                sectorState.ActiveNodes.Add(node);
                _debugLastAcceptedTemplateHash = template.StableHashId;
            }
        }

        private bool TryResolveThermalDiamondTemplate(out ResourceNodeTemplate template)
        {
            template = thermalDiamondTemplate;
            if (template != null)
                return true;

            if (resourceTemplates == null)
                return false;

            for (int i = 0; i < resourceTemplates.Length; i++)
            {
                ResourceNodeTemplate candidate = resourceTemplates[i];
                if (candidate == null || !string.Equals(candidate.StableId, ThermalDiamondStableId, System.StringComparison.Ordinal))
                    continue;

                template = candidate;
                return true;
            }

            return false;
        }

        private SectorState ResolveOrCreateRuntimeSectorState(int2 sector, long sectorKey)
        {
            if (_residentSectors == null)
                return null;

            if (_residentSectors.TryGetValue(sectorKey, out SectorState state))
                return state;

            state = new SectorState(sector, ComputePerSectorInitialCapacity());
            if (mapMagicBridge != null)
            {
                state.BrinePool = ResolveBrinePoolState(sector);
                SyncBrineHazardRegistration(state);
            }
            _residentSectors.Add(sectorKey, state);
            return state;
        }

        internal bool TrySampleBrineFluidDensity(Vector3 runtimePosition, out float fluidDensityKgPerCubicMeter)
        {
            fluidDensityKgPerCubicMeter = 0f;
            if (_residentSectors == null || _residentSectors.Count == 0)
                return false;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            int2 sector = QuantizeSector(in aup);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int2 candidateSector = new int2(sector.x + offsetX, sector.y + offsetY);
                    if (!_residentSectors.TryGetValue(ComposeSectorKey(candidateSector), out SectorState state))
                        continue;

                    BrinePoolState brinePool = state.BrinePool;
                    if (!brinePool.IsValid || !IsInsideBrinePool(in brinePool, runtimePosition))
                        continue;

                    fluidDensityKgPerCubicMeter = brinePool.FluidDensityKgPerCubicMeter;
                    return fluidDensityKgPerCubicMeter > 0f;
                }
            }

            return false;
        }

        private void HandleSeismicShockwave(SeismicShockwaveEvent payload)
        {
            if (!Application.isPlaying || tectonicUpwellingRespawnRate <= 0f)
                return;

            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null || _resourceTombstoneScratch == null)
                return;

            int3 affectedChunkId = registry.ResolveRuntimeChunkId(payload.EpicenterWS);
            if (registry.CopyResourceNodeTombstonesInChunk(affectedChunkId, _resourceTombstoneScratch) <= 0)
                return;

            uint shockSeed = ResolveShockwaveSeed(in payload);
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
            if (!mapMagicBridge.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out float bowlFloorHeight))
                return brinePool;

            float waterSurface = mapMagicBridge.WaterSurfaceLevel;
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
                float angle = sampleIndex * (math.PI * 0.5f);
                float sampleX = runtimeProbe.x + (math.cos(angle) * rimSampleRadius);
                float sampleZ = runtimeProbe.z + (math.sin(angle) * rimSampleRadius);
                if (!mapMagicBridge.TryGetHeight(sampleX, sampleZ, out float rimHeight))
                    return default;

                rimMinHeight = math.min(rimMinHeight, rimHeight);
            }

            if ((rimMinHeight - bowlFloorHeight) < brinePoolMinimumLipMeters)
                return brinePool;

            float surfaceHeight = math.min(waterSurface - 0.25f, bowlFloorHeight + thicknessMeters);
            if (surfaceHeight <= bowlFloorHeight + 0.1f)
                return brinePool;

            brinePool.IsValid = true;
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

            if (!state.BrinePool.IsValid)
            {
                UnregisterBrineHazard(ref state.BrinePool);
                return;
            }

            HazardZoneManager hazardManager = HazardZoneManager.EnsureRuntimeInstance();
            if (hazardManager == null)
                return;

            int zoneId = ResolveBrineHazardZoneId(state.BrinePool.StableSeed);
            float radius = math.max(state.BrinePool.RadiusMeters, (state.BrinePool.SurfaceHeight - state.BrinePool.BottomHeight) * 0.75f);
            if (!hazardManager.RegisterZone(
                    zoneId,
                    state.BrinePool.Center,
                    state.BrinePool.ToxicityIntensity,
                    radius,
                    HazardType.Toxicity,
                    brinePoolHazardVisorBias))
            {
                return;
            }

            state.BrinePool.HazardZoneId = zoneId;
            state.BrinePool.HazardRegistered = true;
        }

        private void UnregisterBrineHazard(ref BrinePoolState brinePool)
        {
            if (!brinePool.HazardRegistered)
                return;

            HazardZoneManager manager = HazardZoneManager.Instance;
            if (manager != null)
                manager.UnregisterZone(brinePool.HazardZoneId);

            brinePool.HazardRegistered = false;
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
            if (_pendingSpawns == null || _pendingSpawns.Count == 0 || tombstoneId == 0UL)
                return false;

            Queue<SpawnRequest>.Enumerator enumerator = _pendingSpawns.GetEnumerator();
            while (enumerator.MoveNext())
            {
                SpawnRequest queuedRequest = enumerator.Current;
                if (queuedRequest.SectorKey == sectorKey && queuedRequest.TombstoneId == tombstoneId)
                    return true;
            }

            return false;
        }

        private bool IsInsideBrinePool(in BrinePoolState brinePool, Vector3 runtimePosition)
        {
            if (!brinePool.IsValid ||
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

            double3 absolutePosition = AbsoluteUniversePosition.FromRuntimePosition(request.RuntimePosition).ToAbsoluteDouble3();
            Vector3 absoluteStart = new Vector3((float)absolutePosition.x, (float)absolutePosition.y, (float)absolutePosition.z);
            float yawRadians = request.YawDegrees * Mathf.Deg2Rad;
            Vector3 veinDirection = new Vector3(math.cos(yawRadians), -0.35f, math.sin(yawRadians)).normalized;
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
            if (_magmaVentPrefab == null)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            if (!pool.HasPool(_magmaVentPrefab))
                pool.Warmup(_magmaVentPrefab, 4);

            GameObject marker = pool.Spawn(_magmaVentPrefab, runtimePosition, Quaternion.identity);
            if (marker == null)
                return;

            float magnitude01 = math.saturate(impulseMagnitude / 40f);
            float height = math.lerp(2.5f, 6.5f, magnitude01);
            float radius = math.lerp(0.75f, 1.65f, Next01(ref stableSeed));
            marker.transform.localScale = new Vector3(radius, height, radius);
            pool.Despawn(marker, magmaVentLifetimeSeconds);
        }

        private uint ResolveShockwaveSeed(in SeismicShockwaveEvent payload)
        {
            AbsoluteUniversePosition epicenter = AbsoluteUniversePosition.FromRuntimePosition(payload.EpicenterWS);
            int2 sector = QuantizeSector(in epicenter);
            uint seed = SeedSectorCandidate(sector, MagmaVentSeedSalt, payload.AppliedStampCount);
            seed = Mix(seed, (uint)math.asint(payload.ImpulseRadiusMeters));
            seed = Mix(seed, (uint)math.asint(payload.ImpulseMagnitude));
            return seed;
        }

        private static int ResolveBrineHazardZoneId(uint stableSeed)
        {
            return (int)(stableSeed ^ BrinePoolHazardIdSalt);
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
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            List<ResourceNode> nodes = state.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null)
                    continue;

                if (pool != null)
                    pool.Despawn(node.gameObject);
                else
                    node.gameObject.SetActive(false);
            }

            nodes.Clear();
            _residentSectors.Remove(sectorKey);
        }

        private void DespawnAllResidentNodes()
        {
            if (_residentSectors == null || _residentSectors.Count == 0)
                return;

            _sectorEvictionScratch.Clear();
            Dictionary<long, SectorState>.Enumerator enumerator = _residentSectors.GetEnumerator();
            while (enumerator.MoveNext())
                _sectorEvictionScratch.Add(enumerator.Current.Key);
            enumerator.Dispose();

            for (int i = 0; i < _sectorEvictionScratch.Count; i++)
                EvictSector(_sectorEvictionScratch[i]);

            _sectorEvictionScratch.Clear();
        }

        private bool IsBlockedByVoxelSolid(Vector3 runtimePosition)
        {
            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) || volume == null)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                return false;

            Vector3 localPoint = volume.transform.InverseTransformPoint(runtimePosition);
            if (!localBounds.Contains(localPoint))
                return false;

            return volume.TrySampleDensity(runtimePosition, out float density, out float density01) &&
                   (density > 0f || density01 >= voxelSolidThreshold);
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
            return terrainNormal.sqrMagnitude > 0.000001f ? terrainNormal.normalized : Vector3.up;
        }

        private static Quaternion ResolveSurfaceRotation(Vector3 surfaceNormal, float yawDegrees)
        {
            Vector3 up = surfaceNormal.sqrMagnitude > 0.000001f ? surfaceNormal.normalized : Vector3.up;
            float yawRadians = yawDegrees * Mathf.Deg2Rad;
            Vector3 authoredForward = new Vector3(math.sin(yawRadians), 0f, math.cos(yawRadians));
            Vector3 tangentForward = Vector3.ProjectOnPlane(authoredForward, up);
            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.ProjectOnPlane(Vector3.forward, up);

            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.Cross(up, Vector3.right);

            if (tangentForward.sqrMagnitude <= 0.000001f)
                tangentForward = Vector3.forward;

            return Quaternion.LookRotation(tangentForward.normalized, up);
        }

        private float ResolveSlope(Vector3 runtimePosition)
        {
            if (vegetationBridge != null &&
                vegetationBridge.TrySampleTerrainSlopeDegrees(runtimePosition, slopeSampleDistanceMeters, out float vegetationSlope))
            {
                return vegetationSlope;
            }

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
            float gradientMagnitude = math.sqrt((gradientX * gradientX) + (gradientZ * gradientZ));
            return math.degrees(math.atan(gradientMagnitude));
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

        private Mesh CaptureCubeMesh()
        {
            // COLD ALLOC: GameObject[1] — temporary primitive source used to capture the built-in cube mesh — owner: ResourceDistributionDirector
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            MeshFilter filter = temp.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);

            return mesh;
        }

        private Mesh CaptureCylinderMesh()
        {
            // COLD ALLOC: GameObject[1] — temporary primitive source used to capture the built-in cylinder mesh — owner: ResourceDistributionDirector
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MeshFilter filter = temp.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);

            return mesh;
        }

        private Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] — shared ghost placeholder material for meshless resource nodes — owner: ResourceDistributionDirector
            Material material = new Material(shader)
            {
                name = "MAT_Runtime_ResourceGhost"
            };

            Color ghostColor = new Color(1f, 0.15f, 0.1f, GhostAlpha);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", ghostColor);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", ghostColor);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private Material CreateMagmaVentMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] — shared tectonic-upwelling marker material — owner: ResourceDistributionDirector
            Material material = new Material(shader)
            {
                name = "MAT_Runtime_MagmaVentGhost"
            };

            Color ventColor = new Color(1f, 0.42f, 0.12f, 0.72f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", ventColor);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", ventColor);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private void UpdateDiagnostics(int2 playerSector)
        {
            _debugResidentSectorCount = _residentSectors != null ? _residentSectors.Count : 0;
            _debugQueuedSpawnCount = _pendingSpawns != null ? _pendingSpawns.Count : 0;

            int activeNodeCount = 0;
            int activeBrinePoolCount = 0;
            if (_residentSectors != null)
            {
                Dictionary<long, SectorState>.Enumerator enumerator = _residentSectors.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    activeNodeCount += enumerator.Current.Value.ActiveNodes.Count;
                    if (enumerator.Current.Value.BrinePool.IsValid)
                        activeBrinePoolCount++;
                }
                enumerator.Dispose();
            }

            _debugActiveNodeCount = activeNodeCount;
            _debugActiveBrinePoolCount = activeBrinePoolCount;
            _debugPlayerSector = new Vector2Int(playerSector.x, playerSector.y);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sectorSizeMeters = math.max(32, sectorSizeMeters);
            sectorRadius = math.clamp(sectorRadius, 0, 3);
            maxSpawnsPerSlowTick = math.max(1, maxSpawnsPerSlowTick);
            poolWarmupFloor = math.max(8, poolWarmupFloor);
            slopeSampleDistanceMeters = math.max(0.5f, slopeSampleDistanceMeters);
            voxelSolidThreshold = math.clamp(voxelSolidThreshold, 0.001f, 1f);
            sectorEdgeMarginMeters = math.max(0f, sectorEdgeMarginMeters);
            brinePoolRadiusMinMeters = math.max(4f, brinePoolRadiusMinMeters);
            brinePoolRadiusMaxMeters = math.max(brinePoolRadiusMinMeters, brinePoolRadiusMaxMeters);
            brinePoolThicknessMinMeters = math.max(1f, brinePoolThicknessMinMeters);
            brinePoolThicknessMaxMeters = math.max(brinePoolThicknessMinMeters, brinePoolThicknessMaxMeters);
            brinePoolMinimumDepthMeters = math.max(1000f, brinePoolMinimumDepthMeters);
            brinePoolMinimumLipMeters = math.max(0.5f, brinePoolMinimumLipMeters);
            brinePoolToxicityIntensity = math.saturate(brinePoolToxicityIntensity);
            brinePoolHazardVisorBias = math.max(0f, brinePoolHazardVisorBias);
            brinePoolFluidDensityKgPerCubicMeter = math.max(1025f, brinePoolFluidDensityKgPerCubicMeter);
            tectonicUpwellingRespawnRate = math.clamp(tectonicUpwellingRespawnRate, 0f, 0.25f);
            magmaVentLifetimeSeconds = math.max(1f, magmaVentLifetimeSeconds);
        }
#endif
    }
}
