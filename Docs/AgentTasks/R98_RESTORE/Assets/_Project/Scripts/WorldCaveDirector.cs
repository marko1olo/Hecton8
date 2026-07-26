// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  WorldCaveDirector.cs — Project HECTON-8 Cave Generation Director         ║
// ║  Unity 6 | Zero GC in Hot Paths | Integrated with Biome/Zone Logic        ║
// ║  v1.0 — Initial cave integration with world-fill pipeline                 ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Integrates cave generation into the world-fill pipeline. Determines      ║
// ║  cave spawn locations based on biome/zone rules, generates cave topology  ║
// ║  via CaveGraphGenerator, and triggers voxel mesh generation via           ║
// ║  HectonVoxelEngine. Ensures caves are meaningful exploration layers.      ║
// ║                                                                             ║
// ║  INTEGRATION:                                                              ║
// ║  ────────────                                                              ║
// ║  - Reads biome/zone from BiomeMatrixDirector/WorldZoneDirector            ║
// ║  - Uses CavePreset from biome profile for generation parameters           ║
// ║  - Spawns caves at strategic locations (terrain seams, biome edges)       ║
// ║  - Registers with streaming system for LOD/distance culling               ║
// ║  - Provides cave entrance hints for scatter system                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Caves;
using Hecton8.Environment;
using UnityEngine;
using Unity.Mathematics;

using CurrentVolume = global::Hecton8.Physics.CurrentVolume;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4035)]
    public sealed class WorldCaveDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        internal readonly struct CaveEntranceHint
        {
            public CaveEntranceHint(Vector3 surfacePosition, Vector3 interiorPosition, float entranceRadius, float influenceRadius)
            {
                SurfacePosition = surfacePosition;
                InteriorPosition = interiorPosition;
                EntranceRadius = entranceRadius;
                InfluenceRadius = influenceRadius;
            }

            public readonly Vector3 SurfacePosition;
            public readonly Vector3 InteriorPosition;
            public readonly float EntranceRadius;
            public readonly float InfluenceRadius;
        }

        private enum CaveBiomePresetKind : byte
        {
            Generic = 0,
            Cliff = 1,
            Canyon = 2,
            Abyss = 3
        }

        private const int ActiveCaveKeyCapacity = 32;
        private const int CandidateBufferCapacity = 8;
        private const int PendingCaveSpawnCapacity = 16;
        private const int PendingCaveKeyBufferCapacity = 16;
        private const int RuntimePresetSlotCapacity = ActiveCaveKeyCapacity + PendingCaveSpawnCapacity;
        private const int EntranceMarkerNameCapacity = 32;
        private const int ThermalGeyserNameCapacity = 32;
        private const float CaveEvaluationIntervalSeconds = 2f;
        private const float CaveEvaluationClockMaxSeconds = 16777215f;
        private const float ThermalGeyserFloorOffset = 0.35f;
        private const float EntranceQualityFallbackRadius = 4f;
        private const float EntranceQualityMinRadius = 0.5f;
        private const float EntranceQualityMaxRadius = 24f;
        private const float EntranceHintMinFunnelLength = 0.5f;
        private const float EntranceHintMaxFunnelLength = 80f;
        private const float EntranceHintMaxInfluenceRadius = 128f;
        private static readonly string[] _EntranceMarkerNames = CreateIndexedNameCache("Marker_", EntranceMarkerNameCapacity); // COLD ALLOC: string[32] — bounded entrance marker names — owner: WorldCaveDirector
        private static readonly string[] _ThermalGeyserNames = CreateTwoDigitNameCache("_ThermalGeyser_", ThermalGeyserNameCapacity); // COLD ALLOC: string[32] — bounded thermal geyser names — owner: WorldCaveDirector
        private static readonly Color _EntranceHazardColor = new Color(0.9f, 0.3f, 0.2f);
        private static readonly Color _EntranceLifeColor = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color _EntranceNeutralColor = new Color(0.8f, 0.6f, 0.2f);
        private static readonly Color _EntranceDeepColor = new Color(0.2f, 0.8f, 1f);
        private static readonly Gradient _EntranceHazardGradient = CreateStaticGradient(_EntranceHazardColor, 0.3f); // COLD ALLOC: Gradient[1] — reused hazard entrance marker gradient — owner: WorldCaveDirector
        private static readonly Gradient _EntranceLifeGradient = CreateStaticGradient(_EntranceLifeColor, 0.3f); // COLD ALLOC: Gradient[1] — reused life entrance marker gradient — owner: WorldCaveDirector
        private static readonly Gradient _EntranceNeutralGradient = CreateStaticGradient(_EntranceNeutralColor, 0.3f); // COLD ALLOC: Gradient[1] — reused neutral entrance marker gradient — owner: WorldCaveDirector
        private static readonly Gradient _EntranceDeepGradient = CreateStaticGradient(_EntranceDeepColor, 0.5f); // COLD ALLOC: Gradient[1] — reused deep entrance marker gradient — owner: WorldCaveDirector

        private sealed class DeepFungiParticleCache : MonoBehaviour
        {
            private readonly GradientColorKey[] _colorKeys = new GradientColorKey[2]; // COLD ALLOC: GradientColorKey[2] — reusable deep-fungi gradient color keys — owner: DeepFungiParticleCache
            private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[2]; // COLD ALLOC: GradientAlphaKey[2] — reusable deep-fungi gradient alpha keys — owner: DeepFungiParticleCache
            private Gradient _gradient; // COLD ALLOC: Gradient[1] — per-fungi particle color-over-life gradient — owner: DeepFungiParticleCache
            private Color _cachedGlowColor;
            private bool _hasGradient;

            internal void Prewarm()
            {
                if (_gradient == null)
                    _gradient = new Gradient();
            }

            internal bool TryResolveGradient(Color glowColor, out Gradient gradient)
            {
                gradient = _gradient;
                if (gradient == null)
                    return false;

                if (_hasGradient && _cachedGlowColor == glowColor)
                    return true;

                _colorKeys[0] = new GradientColorKey(glowColor, 0f);
                _colorKeys[1] = new GradientColorKey(Color.clear, 1f);
                _alphaKeys[0] = new GradientAlphaKey(0.6f, 0f);
                _alphaKeys[1] = new GradientAlphaKey(0f, 1f);
                gradient.SetKeys(_colorKeys, _alphaKeys);
                _cachedGlowColor = glowColor;
                _hasGradient = true;
                return true;
            }
        }

        private readonly struct PendingCaveSpawnState
        {
            public PendingCaveSpawnState(int version)
            {
                Version = version;
            }

            public readonly int Version;
        }

        private readonly struct PendingCaveVisualSync
        {
            public PendingCaveVisualSync(long caveKey, CavePreset preset)
            {
                CaveKey = caveKey;
                Preset = preset;
            }

            public readonly long CaveKey;
            public readonly CavePreset Preset;
        }

        private struct CaveEntranceMarkerRuntimeState
        {
            public GameObject GameObject;
            public Transform Transform;
            public Light Light;
            public ParticleSystem Particles;
        }

        private struct ThermalGeyserRuntimeState
        {
            public GameObject GameObject;
            public Transform Transform;
            public CurrentVolume CurrentVolume;
            public ThermalGeyser Geyser;
        }

        private struct CavePrimitiveVisualRuntimeCache
        {
            public GameObject[] Objects;
            public MeshFilter[] Filters;
            public MeshRenderer[] Renderers;
        }

        private sealed class CaveVisualRuntimeState
        {
            public CaveEntranceMarkerRuntimeState[] EntranceMarkers;
            public Transform EntranceMarkerRoot;
            public Transform EntranceQualityRoot;
            public Transform DressingRoot;
            public Transform WallGrowthRoot;
            public Transform GlowingTissueRoot;
            public Transform ServiceRemnantRoot;
            public Transform SedimentShelfRoot;
            public CavePrimitiveVisualRuntimeCache WallGrowthPrimitives;
            public CavePrimitiveVisualRuntimeCache GlowingTissuePrimitives;
            public CavePrimitiveVisualRuntimeCache ServiceRemnantPrimitives;
            public CavePrimitiveVisualRuntimeCache SedimentShelfPrimitives;
            public Transform BioRootsRoot;
            public Transform ThermalGeyserRoot;
            public GameObject EntranceQualityObject;
            public SphereCollider EntranceQualityCollider;
            public Light EntranceQualityLight;
            public CaveBioRootsGenerator BioRootsGenerator;
            public ThermalGeyserRuntimeState[] ThermalGeysers;
            public GameObject FungiObject;
            public Transform FungiTransform;
            public ParticleSystem FungiParticles;
            public DeepFungiParticleCache FungiCache;
        }

        private struct CachedBiomeRuntimeContext
        {
            public HectonBiomeFamilyProfile Family;
            public string FamilyId;
            public string FamilyLabel;
            public int FamilyHash;
            public bool SupportsCaves;
            public CaveBiomePresetKind PresetKind;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private WorldProceduralFieldSampler fieldSampler;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Cave Generation")]
        [SerializeField] private float caveSearchRadius = 200f;
        [SerializeField] private int maxCavesPerBiome = 3;
        [SerializeField] private float minCaveSpacing = 150f;
        [SerializeField] private float caveSpawnProbability = 0.4f; // Per biome evaluation

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveCaves;
        [SerializeField] private int _debugPendingCaves;
        [SerializeField] private string _debugCurrentBiome = "None";
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private bool _debugReady;

        private bool _registeredToTickManager;
        private bool _registeredLateFrame;
        private readonly HashSet<long> _activeCaveKeys = new HashSet<long>(ActiveCaveKeyCapacity);
        private readonly Dictionary<long, CaveInstance> _caveInstances = new Dictionary<long, CaveInstance>(32);
        private readonly Dictionary<long, PendingCaveSpawnState> _pendingCaveSpawns = new Dictionary<long, PendingCaveSpawnState>(PendingCaveSpawnCapacity);
        private readonly Dictionary<long, CaveEntranceHint[]> _caveEntranceHints = new Dictionary<long, CaveEntranceHint[]>(32); // COLD ALLOC: cached entrance hints for field sampling, capped by active caves.
        private readonly Dictionary<long, CaveVisualRuntimeState> _caveVisualRuntimeStates = new Dictionary<long, CaveVisualRuntimeState>(32); // COLD ALLOC: per-cave visual component cache keyed by cave id.
        private readonly List<Vector3> _candidateBuffer = new List<Vector3>(CandidateBufferCapacity); // COLD ALLOC: buffered cave candidates, capped by CandidateBufferCapacity.
        private readonly List<long> _staleCaveKeyBuffer = new List<long>(ActiveCaveKeyCapacity); // COLD ALLOC: stale cave cleanup buffer, capped by active cave count around player.
        private readonly List<long> _pendingCaveKeyBuffer = new List<long>(PendingCaveKeyBufferCapacity); // COLD ALLOC: buffered pending cave keys for deterministic cancel/cleanup without mutating dictionaries during enumeration.
        private readonly List<PendingCaveVisualSync> _pendingCaveVisualSyncs = new List<PendingCaveVisualSync>(16); // COLD ALLOC: visual-sync cave dressing queue.
        private readonly CavePreset[] _runtimePresetPool = new CavePreset[RuntimePresetSlotCapacity]; // COLD ALLOC: per cave preset slots, no shared mutable template references.
        private readonly CaveStructureType[][] _runtimePresetStructureTypes3 = new CaveStructureType[RuntimePresetSlotCapacity][]; // COLD ALLOC: exact-length structure type storage for 3-type biome presets.
        private readonly CaveStructureType[][] _runtimePresetStructureTypes5 = new CaveStructureType[RuntimePresetSlotCapacity][]; // COLD ALLOC: exact-length structure type storage for 5-type biome presets.
        private readonly long[] _runtimePresetKeys = new long[RuntimePresetSlotCapacity]; // COLD ALLOC: cave key bound to each preset slot.
        private readonly bool[] _runtimePresetSlotUsed = new bool[RuntimePresetSlotCapacity]; // COLD ALLOC: slot occupancy flags.
        private CachedBiomeRuntimeContext _cachedBiomeRuntimeContext;
        private float _lastEvaluationTime = float.NegativeInfinity;
        private CancellationTokenSource _lifetimeCancellation;
        private int _pendingSpawnVersion;
        private int _entranceHintVersion;
        private bool _runtimePresetPoolReady;
        private static readonly int _CrustIntensityId = Shader.PropertyToID("_CrustIntensity");
        private static readonly int _CrustColorId = Shader.PropertyToID("_CrustColor");
        private static readonly int _CrustRoughnessId = Shader.PropertyToID("_CrustRoughness");
        private static MaterialPropertyBlock _CaveSurfacePropertyBlock;
        private static readonly CaveStructureType[] _CliffStructureTypes =
        {
            CaveStructureType.Stalactite,
            CaveStructureType.Column,
            CaveStructureType.Stalagmite
        };
        private static readonly CaveStructureType[] _CanyonStructureTypes =
        {
            CaveStructureType.Boulder,
            CaveStructureType.Arch,
            CaveStructureType.Bridge,
            CaveStructureType.Block,
            CaveStructureType.Wall
        };
        private static readonly CaveStructureType[] _AbyssStructureTypes =
        {
            CaveStructureType.Column,
            CaveStructureType.Arch,
            CaveStructureType.Stalactite,
            CaveStructureType.Stalagmite,
            CaveStructureType.Wall
        };
        private static readonly CaveStructureType[] _GenericStructureTypes =
        {
            CaveStructureType.Stalactite,
            CaveStructureType.Boulder,
            CaveStructureType.Column
        };
        private static readonly CavePreset _CliffPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Cliff);
        private static readonly CavePreset _CanyonPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Canyon);
        private static readonly CavePreset _AbyssPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Abyss);
        private static readonly CavePreset _GenericPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Generic);

        internal static WorldCaveDirector ActiveRuntimeInstance { get; private set; }
        internal static event Action<WorldCaveDirector> ActiveRuntimeInstanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged = null;
        }

        /// <summary>Represents an active cave instance in the world.</summary>
        public struct CaveInstance
        {
            public long key;
            public Vector3 position;
            public CavePreset preset;
            public HectonVoxelVolume volume; // Reference to generated volume
            public byte isActive;
        }

        private void Awake()
        {
            PublishActiveRuntimeInstance();

            if (_CaveSurfacePropertyBlock == null)
                _CaveSurfacePropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: shared cave-surface block for dressing overlays.

            WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources();
            CaveWallGrowthRuntimeBuilder.PrewarmSharedResources();
            CaveGlowingTissueRuntimeBuilder.PrewarmSharedResources();
            CaveServiceRemnantRuntimeBuilder.PrewarmSharedResources();
            CaveSedimentShelfRuntimeBuilder.PrewarmSharedResources();
            EnsureRuntimePresetPool();
            RefreshColdReferences();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            EnsureLifetimeCancellation();
            EnsureRuntimePresetPool();
            RefreshColdReferences();
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegister();
        }

        private void Start()
        {
            EnsureRuntimePresetPool();
            RefreshColdReferences();
            TryRegister();

            EvaluateCaveSpawns();
        }

        private void OnDisable()
        {
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            CancelLifetimeCancellation();
            CancelAllPendingSpawns();
        }

        private void OnDestroy()
        {
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ClearActiveRuntimeInstance();
        }

        private void PublishActiveRuntimeInstance()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return;

            ActiveRuntimeInstance = this;
            ActiveRuntimeInstanceChanged?.Invoke(this);
        }

        private void ClearActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged?.Invoke(null);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerTransform(previousService as IPlayerRuntimeContext, currentService as IPlayerRuntimeContext);
                    return;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    biomeMatrixDirector = currentService as BiomeMatrixDirector;
                    UpdateDiagnostics();
                    return;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    voxelEngine = currentService as HectonVoxelEngine;
                    UpdateDiagnostics();
                    return;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    mapMagicBridge = currentService as MapMagicBridge;
                    UpdateDiagnostics();
                    return;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (isActiveAndEnabled)
                    {
                        if (currentService != null)
                            TryRegister();
                    }
                    return;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToTickManager = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            _pendingCaveVisualSyncs.Clear();
        }

        public void SlowTick()
        {
            RefreshCaveLifecycleState();
            EvaluateCaveSpawns();
        }

        public void LateFrameTick()
        {
            FlushPendingCaveVisualSyncs();
        }

        private void EvaluateCaveSpawns()
        {
            if (!HasRequiredReferences())
                return;

            float evaluationTime = ResolveCaveEvaluationTimeSeconds();
            if (evaluationTime - _lastEvaluationTime < CaveEvaluationIntervalSeconds)
                return;

            _lastEvaluationTime = evaluationTime;

            HectonBiomeFamilyProfile biomeFamily = biomeMatrixDirector.CurrentFamilyProfile;
            WorldZoneAnchor currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;

            if (biomeFamily == null)
                return;

            RefreshBiomeRuntimeContext(biomeFamily);

            if (!_cachedBiomeRuntimeContext.SupportsCaves)
            {
                // Clean up caves from unsupported biomes
                CleanupUnsupportedCaves();
                return;
            }

            // Generate cave spawn candidates
            List<Vector3> candidates = GenerateCaveCandidates(currentZone);

            // Spawn caves at candidates
            for (int i = 0; i < candidates.Count; i++)
                TryQueueCaveSpawn(candidates[i], biomeFamily);

            UpdateDiagnostics();
        }

        private static float ResolveCaveEvaluationTimeSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(CaveEvaluationClockMaxSeconds, timeSeconds);
        }

        private static bool EvaluateBiomeCaveSupport(string biomeId)
        {
            return biomeId.Contains("cliff") || biomeId.Contains("canyon") || biomeId.Contains("deep") || biomeId.Contains("abyss");
        }

        private List<Vector3> GenerateCaveCandidates(WorldZoneAnchor zone)
        {
            _candidateBuffer.Clear();

            if (playerTransform == null)
                return _candidateBuffer;

            Vector3 playerPos = playerTransform.position;
            Vector3 routeAnchor = ResolveCaveRouteAnchor(zone, playerPos);
            float routeQuality = ResolveCaveRouteQuality(zone);
            float searchRadius = Mathf.Max(60f, caveSearchRadius);
            float spawnChance = math.saturate(caveSpawnProbability * routeQuality);

            // Generate candidates around player within search radius
            // Use deterministic seeding based on biome and position.
            // R95 FIX: the old additive seed (biomeSeed + floor(x/100) + floor(z/100)) was
            // diagonal-degenerate — every cell along (x+1, z-1) shared one seed, repeating the same
            // candidate pattern down whole diagonals. Proper 2D cell hash decorrelates the axes.
            int biomeSeed = _cachedBiomeRuntimeContext.FamilyHash;
            uint seed = HashCandidateCellSeed(
                Mathf.FloorToInt(routeAnchor.x / 100f),
                Mathf.FloorToInt(routeAnchor.z / 100f),
                biomeSeed);
            if (seed == 0u)
                seed = 1u;

            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);

            if (rng.NextFloat() > spawnChance)
                return _candidateBuffer;

            int requestedCount = Mathf.Clamp(maxCavesPerBiome, 1, _candidateBuffer.Capacity);
            int candidateCount = rng.NextInt(1, requestedCount + 1);
            float minDistance = Mathf.Clamp(Mathf.Max(24f, minCaveSpacing), 12f, searchRadius - 1f);
            if (zone != null && zone.RouteCritical)
                candidateCount = Mathf.Min(candidateCount + 1, requestedCount);

            for (int i = 0; i < candidateCount; i++)
            {
                // Random position within radius, biased toward terrain features
                float angle = rng.NextFloat(0f, 2f * Mathf.PI);
                float distance = rng.NextFloat(minDistance, searchRadius);

                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                Vector3 offset = new Vector3(cos * distance, 0f, sin * distance);
                Vector3 candidatePos = routeAnchor + offset;

                // Sample terrain height
                if (mapMagicBridge != null && mapMagicBridge.TryGetHeight(candidatePos.x, candidatePos.z, out float terrainHeight))
                {
                    candidatePos.y = terrainHeight - 5f; // Slightly below surface for cave entrance
                }

                if (!PassesTerrainDetailCaveCandidate(candidatePos))
                    continue;

                if (!IsCandidateTooClose(candidatePos, minDistance) && _candidateBuffer.Count < _candidateBuffer.Capacity)
                    _candidateBuffer.Add(candidatePos);
            }

            return _candidateBuffer;
        }

        private bool PassesTerrainDetailCaveCandidate(Vector3 candidatePosition)
        {
            if (fieldSampler == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler(ref fieldSampler);

            if (fieldSampler == null ||
                !fieldSampler.TrySampleTerrainDetail(candidatePosition, out WorldTerrainDetailRuntimeSample sample) ||
                !sample.IsValid)
            {
                return true;
            }

            WorldTerrainDetailEligibilityFlags eligibility = sample.EligibilityFlags;
            bool hasCaveMouth =
                (eligibility & WorldTerrainDetailEligibilityFlags.CaveMouthCandidate) != 0;
            bool hasVoxelRockAnchor =
                (eligibility & (WorldTerrainDetailEligibilityFlags.VoxelAnchor | WorldTerrainDetailEligibilityFlags.RockScatter)) ==
                (WorldTerrainDetailEligibilityFlags.VoxelAnchor | WorldTerrainDetailEligibilityFlags.RockScatter);

            return hasCaveMouth || hasVoxelRockAnchor;
        }

        private void TryQueueCaveSpawn(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return;

            long caveKey = GenerateCaveKey(position, biomeFamily);

            if (_activeCaveKeys.Contains(caveKey) || _pendingCaveSpawns.ContainsKey(caveKey))
                return;

            if (_activeCaveKeys.Count >= ActiveCaveKeyCapacity ||
                _pendingCaveSpawns.Count >= PendingCaveSpawnCapacity)
            {
                return;
            }

            HectonVoxelEngine activeVoxelEngine = voxelEngine;
            if (activeVoxelEngine == null)
            {
                LogMissingVoxelEngine();
                return;
            }

            if (!TryAcquireRuntimePreset(caveKey, biomeFamily, out CavePreset preset))
                return;

            PendingCaveSpawnState pendingState = CreatePendingSpawnState();
            _pendingCaveSpawns[caveKey] = pendingState;
            _debugPendingCaves = _pendingCaveSpawns.Count;

            uint seed = MixCaveKeyToSeed(caveKey);
            _ = SpawnCaveAsync(activeVoxelEngine, caveKey, position, preset, seed, pendingState);
        }

        private async Awaitable SpawnCaveAsync(
            HectonVoxelEngine activeVoxelEngine,
            long caveKey,
            Vector3 position,
            CavePreset preset,
            uint seed,
            PendingCaveSpawnState pendingState)
        {
            GameObject caveVolume = null;
            bool activated = false;
            CancellationToken token = _lifetimeCancellation != null
                ? _lifetimeCancellation.Token
                : default;

            try
            {
                if (activeVoxelEngine == null)
                    return;

                caveVolume = await activeVoxelEngine.GenerateVolumeAsync(position, seed, preset, lodLevel: 0, ct: token);
                if (caveVolume == null)
                {
                    LogNoGeometry(position);
                    return;
                }

                if (!activeVoxelEngine.TryGetRegisteredVolumeComponent(caveVolume, out HectonVoxelVolume voxelVolume))
                {
                    CleanupSpawnedVolume(activeVoxelEngine, caveVolume);
                    LogCaveSpawnFailure(position, "Generated cave volume did not include HectonVoxelVolume.");
                    return;
                }

                if (!isActiveAndEnabled ||
                    !_pendingCaveSpawns.TryGetValue(caveKey, out PendingCaveSpawnState currentState) ||
                    currentState.Version != pendingState.Version)
                {
                    CleanupSpawnedVolume(activeVoxelEngine, caveVolume);
                    return;
                }

                CaveInstance instance = new CaveInstance
                {
                    key = caveKey,
                    position = position,
                    preset = preset,
                    volume = voxelVolume,
                    isActive = 1
                };

                instance.volume.caveKey = caveKey;
                instance.volume.generationPosition = position;
                instance.volume.preset = preset;

                CacheEntranceHints(caveKey, instance.volume.Entrances);
                PrepareCaveVisualRuntimeState(caveKey, instance.volume, preset, instance.volume.Entrances);
                _caveInstances[caveKey] = instance;
                _activeCaveKeys.Add(caveKey);
                activated = true;
                QueueCaveVisualSync(caveKey, preset);
                LogCaveGenerated(position);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (caveVolume != null)
                    CleanupSpawnedVolume(activeVoxelEngine, caveVolume);

                LogCaveSpawnFailure(position, exception.Message);
            }
            finally
            {
                CompletePendingSpawn(caveKey, pendingState);
                if (!activated)
                    ReleaseRuntimePreset(caveKey);
                RefreshCaveLifecycleState();
                UpdateDiagnostics();
            }
        }

        private bool TryAcquireRuntimePreset(long caveKey, HectonBiomeFamilyProfile biomeFamily, out CavePreset preset)
        {
            preset = null;
            if (!_runtimePresetPoolReady)
                return false;

            RefreshBiomeRuntimeContext(biomeFamily);
            CavePreset template = ResolveBiomePresetTemplate(_cachedBiomeRuntimeContext.PresetKind);
            if (template == null)
                return false;

            for (int i = 0; i < _runtimePresetPool.Length; i++)
            {
                if (!_runtimePresetSlotUsed[i] || _runtimePresetKeys[i] != caveKey)
                    continue;

                preset = _runtimePresetPool[i];
                return preset != null;
            }

            for (int i = 0; i < _runtimePresetPool.Length; i++)
            {
                if (_runtimePresetSlotUsed[i])
                    continue;

                CavePreset slot = _runtimePresetPool[i];
                if (slot == null || !TryCopyRuntimePreset(template, slot, i))
                    return false;

                _runtimePresetKeys[i] = caveKey;
                _runtimePresetSlotUsed[i] = true;
                preset = slot;
                return true;
            }

            return false;
        }

        private static CaveBiomePresetKind ResolveBiomePresetKind(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
                return CaveBiomePresetKind.Generic;

            if (biomeId.Contains("cliff") || biomeId.Contains("escarpment"))
                return CaveBiomePresetKind.Cliff;

            if (biomeId.Contains("canyon") || biomeId.Contains("rift"))
                return CaveBiomePresetKind.Canyon;

            if (biomeId.Contains("deep") || biomeId.Contains("abyss") || biomeId.Contains("hadal"))
                return CaveBiomePresetKind.Abyss;

            return CaveBiomePresetKind.Generic;
        }

        private static CavePreset ResolveBiomePresetTemplate(CaveBiomePresetKind presetKind)
        {
            return presetKind switch
            {
                CaveBiomePresetKind.Cliff => _CliffPresetTemplate,
                CaveBiomePresetKind.Canyon => _CanyonPresetTemplate,
                CaveBiomePresetKind.Abyss => _AbyssPresetTemplate,
                _ => _GenericPresetTemplate
            };
        }

        private static CavePreset CreateBiomePresetTemplate(CaveBiomePresetKind presetKind)
        {
            CavePreset preset = new CavePreset
            {
                gridDimension = 64,
                voxelSize = 1.5f,
                minEntrances = 1,
                maxEntrances = 2,
                tallTunnelChance = 0.15f,
                tunnelWarpAmount = 2f,
                extraConnectionChance = 0.2f,
                enableStructures = true
            };

            switch (presetKind)
            {
                case CaveBiomePresetKind.Cliff:
                    preset.presetName = "Cliff Cave";
                    preset.presetType = CavePresetType.System;
                    preset.minRooms = 4;
                    preset.maxRooms = 10;
                    preset.minRoomRadius = 5f;
                    preset.maxRoomRadius = 12f;
                    preset.verticalShaftChance = 0.3f;
                    preset.maxDepth = 80f;
                    preset.verticalSpread = 0.6f;
                    preset.minTunnelRadius = 2.5f;
                    preset.maxTunnelRadius = 4f;
                    preset.entranceRadius = 4f;
                    preset.entranceFunnelLength = 15f;
                    preset.spawnContext = SpawnContext.CaveShallow;
                    preset.hazardLevel = 0.2f;
                    preset.moodLevel = 0.4f;
                    preset.maxStructures = 6;
                    preset.structureDensity = 1.2f;
                    preset.allowedStructureTypes = _CliffStructureTypes;
                    break;

                case CaveBiomePresetKind.Canyon:
                    preset.presetName = "Canyon Cave";
                    preset.presetType = CavePresetType.Labyrinth;
                    preset.minRooms = 6;
                    preset.maxRooms = 15;
                    preset.minRoomRadius = 6f;
                    preset.maxRoomRadius = 18f;
                    preset.flatHallChance = 0.4f;
                    preset.maxDepth = 60f;
                    preset.verticalSpread = 0.4f;
                    preset.minTunnelRadius = 3f;
                    preset.maxTunnelRadius = 6f;
                    preset.wideTunnelChance = 0.3f;
                    preset.entranceRadius = 5f;
                    preset.entranceFunnelLength = 20f;
                    preset.spawnContext = SpawnContext.CaveMid;
                    preset.hazardLevel = 0.5f;
                    preset.moodLevel = 0.6f;
                    preset.maxStructures = 8;
                    preset.structureDensity = 1.0f;
                    preset.isRuinLinked = true;
                    preset.allowedStructureTypes = _CanyonStructureTypes;
                    break;

                case CaveBiomePresetKind.Abyss:
                    preset.presetName = "Abyss Cave";
                    preset.presetType = CavePresetType.Abyss;
                    preset.minRooms = 8;
                    preset.maxRooms = 20;
                    preset.minRoomRadius = 8f;
                    preset.maxRoomRadius = 25f;
                    preset.verticalShaftChance = 0.4f;
                    preset.creviceChance = 0.2f;
                    preset.maxDepth = 150f;
                    preset.verticalSpread = 0.8f;
                    preset.minTunnelRadius = 3f;
                    preset.maxTunnelRadius = 7f;
                    preset.tunnelWarpAmount = 4.5f;
                    preset.extraConnectionChance = 0.3f;
                    preset.entranceRadius = 3f;
                    preset.entranceFunnelLength = 25f;
                    preset.warpFrequency = 0.03f;
                    preset.warpAmplitude = 6.5f;
                    preset.warpOctaves = 3;
                    preset.wallNoiseFrequency = 0.1f;
                    preset.wallNoiseAmplitude = 2.8f;
                    preset.wallNoiseOctaves = 4;
                    preset.terraceFrequency = 0.28f;
                    preset.terraceAmplitude = 0.85f;
                    preset.terraceSharpness = 3.5f;
                    preset.globalBlendK = 18f;
                    preset.floorFlatness = 0.35f;
                    preset.spawnContext = SpawnContext.CaveDeep;
                    preset.hazardLevel = 0.8f;
                    preset.moodLevel = 0.2f;
                    preset.maxStructures = 12;
                    preset.structureDensity = 0.8f;
                    preset.allowedStructureTypes = _AbyssStructureTypes;
                    break;

                default:
                    preset.presetName = "Generic Cave";
                    preset.presetType = CavePresetType.System;
                    preset.minRooms = 3;
                    preset.maxRooms = 8;
                    preset.minRoomRadius = 4f;
                    preset.maxRoomRadius = 12f;
                    preset.maxDepth = 50f;
                    preset.verticalSpread = 0.3f;
                    preset.minTunnelRadius = 2f;
                    preset.maxTunnelRadius = 4f;
                    preset.entranceRadius = 3f;
                    preset.entranceFunnelLength = 12f;
                    preset.spawnContext = SpawnContext.CaveShallow;
                    preset.hazardLevel = 0.3f;
                    preset.moodLevel = 0.3f;
                    preset.maxStructures = 4;
                    preset.structureDensity = 0.7f;
                    preset.allowedStructureTypes = _GenericStructureTypes;
                    break;
            }

            return preset;
        }

        private void EnsureRuntimePresetPool()
        {
            if (_runtimePresetPoolReady)
                return;

            for (int i = 0; i < _runtimePresetPool.Length; i++)
            {
                if (_runtimePresetPool[i] == null)
                    _runtimePresetPool[i] = new CavePreset();

                if (_runtimePresetStructureTypes3[i] == null)
                    _runtimePresetStructureTypes3[i] = new CaveStructureType[3];

                if (_runtimePresetStructureTypes5[i] == null)
                    _runtimePresetStructureTypes5[i] = new CaveStructureType[5];
            }

            _runtimePresetPoolReady = true;
        }

        private bool TryCopyRuntimePreset(CavePreset source, CavePreset target, int slotIndex)
        {
            if (source == null ||
                target == null ||
                slotIndex < 0 ||
                slotIndex >= _runtimePresetPool.Length)
            {
                return false;
            }

            target.presetName = source.presetName;
            target.presetType = source.presetType;
            target.gridDimension = source.gridDimension;
            target.voxelSize = source.voxelSize;
            target.minRooms = source.minRooms;
            target.maxRooms = source.maxRooms;
            target.minRoomRadius = source.minRoomRadius;
            target.maxRoomRadius = source.maxRoomRadius;
            target.verticalShaftChance = source.verticalShaftChance;
            target.flatHallChance = source.flatHallChance;
            target.creviceChance = source.creviceChance;
            target.verticalSpread = source.verticalSpread;
            target.maxDepth = source.maxDepth;
            target.minTunnelRadius = source.minTunnelRadius;
            target.maxTunnelRadius = source.maxTunnelRadius;
            target.tallTunnelChance = source.tallTunnelChance;
            target.wideTunnelChance = source.wideTunnelChance;
            target.tunnelWarpAmount = source.tunnelWarpAmount;
            target.extraConnectionChance = source.extraConnectionChance;
            target.minEntrances = source.minEntrances;
            target.maxEntrances = source.maxEntrances;
            target.entranceRadius = source.entranceRadius;
            target.entranceFunnelLength = source.entranceFunnelLength;
            target.warpFrequency = source.warpFrequency;
            target.warpAmplitude = source.warpAmplitude;
            target.warpOctaves = source.warpOctaves;
            target.wallNoiseFrequency = source.wallNoiseFrequency;
            target.wallNoiseAmplitude = source.wallNoiseAmplitude;
            target.wallNoiseOctaves = source.wallNoiseOctaves;
            target.wallNoiseLacunarity = source.wallNoiseLacunarity;
            target.wallNoisePersistence = source.wallNoisePersistence;
            target.terraceFrequency = source.terraceFrequency;
            target.terraceAmplitude = source.terraceAmplitude;
            target.terraceSharpness = source.terraceSharpness;
            target.globalBlendK = source.globalBlendK;
            target.sealMargin = source.sealMargin;
            target.floorFlatness = source.floorFlatness;
            target.spawnContext = source.spawnContext;
            target.enableStructures = source.enableStructures;
            target.maxStructures = source.maxStructures;
            target.structureDensity = source.structureDensity;
            target.hazardLevel = source.hazardLevel;
            target.moodLevel = source.moodLevel;
            target.isRuinLinked = source.isRuinLinked;

            CaveStructureType[] sourceTypes = source.allowedStructureTypes;
            if (sourceTypes == null || sourceTypes.Length == 0)
            {
                target.allowedStructureTypes = Array.Empty<CaveStructureType>();
                return true;
            }

            CaveStructureType[] targetTypes;
            if (sourceTypes.Length == 3)
            {
                targetTypes = _runtimePresetStructureTypes3[slotIndex];
            }
            else if (sourceTypes.Length == 5)
            {
                targetTypes = _runtimePresetStructureTypes5[slotIndex];
            }
            else
            {
                return false;
            }

            for (int i = 0; i < sourceTypes.Length; i++)
                targetTypes[i] = sourceTypes[i];

            target.allowedStructureTypes = targetTypes;
            return true;
        }

        private void ReleaseRuntimePreset(long caveKey)
        {
            for (int i = 0; i < _runtimePresetSlotUsed.Length; i++)
            {
                if (!_runtimePresetSlotUsed[i] || _runtimePresetKeys[i] != caveKey)
                    continue;

                _runtimePresetSlotUsed[i] = false;
                _runtimePresetKeys[i] = 0L;
                return;
            }
        }

        private long GenerateCaveKey(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            RefreshBiomeRuntimeContext(biomeFamily);
            return ComposeCaveKey(position, _cachedBiomeRuntimeContext.FamilyHash);
        }

        private static long GenerateCaveKeyPure(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return 0L;

            string familyId = biomeFamily.familyId ?? string.Empty;
            return ComposeCaveKey(position, Hecton.Localization.LocHash.Compute(familyId));
        }

        private static long ComposeCaveKey(Vector3 position, int biomeHash)
        {
            int x = Mathf.FloorToInt(position.x / 100f);
            int z = Mathf.FloorToInt(position.z / 100f);

            // R95 FIX: the old packing ((long)x << 32) | ((long)z << 16) | (uint)biomeHash ORed a
            // sign-extended z over both the x field and the biome hash, colliding distinct cells
            // (wrong dedupe/lookup) and truncating the derived generation seed to a value that did
            // not depend on x at all (identical cave topology repeated along every 100 m z-band).
            // Disjoint 21-bit lanes: +/-1,048,575 cells (~+/-104,857 km) per axis, hash sign-folded.
            long xBits = ((long)x & 0x1FFFFF) << 42;
            long zBits = ((long)z & 0x1FFFFF) << 21;
            long bBits = (((uint)biomeHash ^ ((uint)biomeHash >> 21)) & 0x1FFFFF);
            return xBits | zBits | bBits;
        }

        /// <summary>Decorrelated 2D cell-hash seed (x and z mixed independently, not summed).</summary>
        private static uint HashCandidateCellSeed(int cellX, int cellZ, int biomeSeed)
        {
            unchecked
            {
                uint hash = (uint)cellX * 0x8DA6B343u;
                hash ^= (uint)cellZ * 0xD8163841u;
                hash ^= (uint)biomeSeed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        /// <summary>
        /// Derives the cave generation seed from the FULL 64-bit cave key via a splitmix64-style
        /// finalizer, so the seed depends on x, z, and biome (the old truncation dropped x entirely).
        /// </summary>
        private static uint MixCaveKeyToSeed(long caveKey)
        {
            unchecked
            {
                ulong v = (ulong)caveKey;
                v ^= v >> 30;
                v *= 0xBF58476D1CE4E5B9ul;
                v ^= v >> 27;
                v *= 0x94D049BB133111EBul;
                v ^= v >> 31;
                uint seed = (uint)(v ^ (v >> 32));
                return seed == 0u ? 1u : seed;
            }
        }

        private void CleanupUnsupportedCaves()
        {
            CancelAllPendingSpawns();

            _staleCaveKeyBuffer.Clear();
            Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
            while (caveEnumerator.MoveNext())
            {
                if (_staleCaveKeyBuffer.Count >= _staleCaveKeyBuffer.Capacity)
                    break;

                _staleCaveKeyBuffer.Add(caveEnumerator.Current.Key);
            }

            for (int i = 0; i < _staleCaveKeyBuffer.Count; i++)
                RemoveTrackedCave(_staleCaveKeyBuffer[i], despawnOwnedVolume: true);

            UpdateDiagnostics();
        }

        private void RefreshColdReferences()
        {
            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (biomeMatrixDirector == null)
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);

            if (worldZoneDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);

            if (voxelEngine == null)
                WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);

            if (mapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);

            if (fieldSampler == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler(ref fieldSampler);
        }

        private bool HasRequiredReferences()
        {
            return playerTransform != null &&
                biomeMatrixDirector != null &&
                worldZoneDirector != null &&
                voxelEngine != null &&
                mapMagicBridge != null;
        }

        private void RebindPlayerTransform(IPlayerRuntimeContext previousContext, IPlayerRuntimeContext currentContext)
        {
            Transform previousTransform = previousContext != null ? previousContext.PlayerTransform : null;
            if (playerTransform != null && previousTransform != null && ReferenceEquals(playerTransform, previousTransform))
                playerTransform = null;

            Transform currentTransform = currentContext != null ? currentContext.PlayerTransform : null;
            if (currentTransform != null)
                playerTransform = currentTransform;

            UpdateDiagnostics();
        }

        private void UpdateDiagnostics()
        {
            _debugActiveCaves = _activeCaveKeys.Count;
            _debugPendingCaves = _pendingCaveSpawns.Count;
            if (biomeMatrixDirector != null && biomeMatrixDirector.CurrentFamilyProfile != null)
            {
                RefreshBiomeRuntimeContext(biomeMatrixDirector.CurrentFamilyProfile);
                _debugCurrentBiome = _cachedBiomeRuntimeContext.FamilyLabel;
            }
            else
            {
                _debugCurrentBiome = "None";
            }

            _debugCurrentZone = worldZoneDirector != null && worldZoneDirector.CurrentZone != null
                ? worldZoneDirector.CurrentZone.ZoneLabel : "None";
            _debugReady = HasRequiredReferences();
        }

        // Public API for other systems
        public bool TryGetCaveAt(Vector3 position, out CaveInstance cave)
        {
            cave = default;
            HectonBiomeFamilyProfile biomeFamily = biomeMatrixDirector != null
                ? biomeMatrixDirector.CurrentFamilyProfile
                : null;
            if (biomeFamily == null)
                return false;

            long key = GenerateCaveKeyPure(position, biomeFamily);
            if (!_caveInstances.TryGetValue(key, out cave))
                return false;

            if (IsTrackedVolumeAlive(key, cave.volume))
                return true;

            cave = default;
            return false;
        }

        [Obsolete("Use CopyActiveCavesTo with caller-owned storage. IEnumerable access is cold/API-only.")]
        public IEnumerable<CaveInstance> GetActiveCaves()
        {
            return _caveInstances.Values;
        }

        public int CopyActiveCavesTo(CaveInstance[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
                return 0;

            int writeCount = 0;
            Dictionary<long, CaveInstance>.Enumerator enumerator = _caveInstances.GetEnumerator();
            while (enumerator.MoveNext() && writeCount < buffer.Length)
            {
                KeyValuePair<long, CaveInstance> pair = enumerator.Current;
                CaveInstance instance = pair.Value;
                if (instance.isActive == 0 || !IsTrackedVolumeAlive(pair.Key, instance.volume))
                    continue;

                buffer[writeCount] = instance;
                writeCount++;
            }

            for (int i = writeCount; i < buffer.Length; i++)
                buffer[i] = default;

            return writeCount;
        }

        internal int EntranceHintVersion => _entranceHintVersion;

        internal void CollectEntranceHints(List<CaveEntranceHint> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            Dictionary<long, CaveEntranceHint[]>.Enumerator enumerator = _caveEntranceHints.GetEnumerator();
            while (enumerator.MoveNext())
            {
                CaveEntranceHint[] hints = enumerator.Current.Value;
                if (hints == null || hints.Length == 0)
                    continue;

                for (int i = 0; i < hints.Length; i++)
                    buffer.Add(hints[i]);
            }
        }

        internal int CopyEntranceHintsTo(CaveEntranceHint[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
                return 0;

            int writeCount = 0;
            Dictionary<long, CaveEntranceHint[]>.Enumerator enumerator = _caveEntranceHints.GetEnumerator();
            while (enumerator.MoveNext() && writeCount < buffer.Length)
            {
                CaveEntranceHint[] hints = enumerator.Current.Value;
                if (hints == null || hints.Length == 0)
                    continue;

                for (int i = 0; i < hints.Length && writeCount < buffer.Length; i++)
                {
                    buffer[writeCount] = hints[i];
                    writeCount++;
                }
            }

            for (int i = writeCount; i < buffer.Length; i++)
                buffer[i] = default;

            return writeCount;
        }

        internal void CollectActiveVolumes(List<HectonVoxelVolume> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();

            var enumerator = _caveInstances.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<long, CaveInstance> pair = enumerator.Current;
                CaveInstance instance = pair.Value;
                if (instance.isActive == 0 || !IsTrackedVolumeAlive(pair.Key, instance.volume))
                    continue;

                buffer.Add(instance.volume);
            }
        }

        internal int CopyActiveVolumesTo(HectonVoxelVolume[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
                return 0;

            int writeCount = 0;
            var enumerator = _caveInstances.GetEnumerator();
            while (enumerator.MoveNext() && writeCount < buffer.Length)
            {
                KeyValuePair<long, CaveInstance> pair = enumerator.Current;
                CaveInstance instance = pair.Value;
                if (instance.isActive == 0 || !IsTrackedVolumeAlive(pair.Key, instance.volume))
                    continue;

                buffer[writeCount] = instance.volume;
                writeCount++;
            }

            for (int i = writeCount; i < buffer.Length; i++)
                buffer[i] = null;

            return writeCount;
        }

        private void SpawnEntranceVisualCues(CaveInstance instance)
        {
            if (instance.volume == null)
                return;

            CaveEntrance[] entrances = instance.volume.Entrances;
            if (entrances == null || entrances.Length <= 0)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null ||
                visualState.EntranceMarkers == null)
            {
                return;
            }

            Transform markerRoot = ActivateCachedRoot(visualState.EntranceMarkerRoot);
            if (markerRoot == null)
                return;

            int usedMarkerCount = 0;

            for (int i = 0; i < entrances.Length; i++)
            {
                CaveEntrance entrance = entrances[i];
                SpawnEntranceMarker(visualState, markerRoot, usedMarkerCount, entrance.surfacePosition, entrance.inwardDirection, instance);
                usedMarkerCount++;
            }

            DisableUnusedEntranceMarkers(visualState.EntranceMarkers, usedMarkerCount);
        }

        private void SpawnEntranceMarker(
            CaveVisualRuntimeState visualState,
            Transform markerRoot,
            int markerIndex,
            Vector3 position,
            Vector3 inwardDirection,
            CaveInstance instance)
        {
            if (markerRoot == null || visualState == null || visualState.EntranceMarkers == null)
                return;

            CaveEntranceMarkerRuntimeState[] markerStates = visualState.EntranceMarkers;
            if ((uint)markerIndex >= (uint)markerStates.Length)
                return;

            // Spawn a simple visual marker (light or particle system) at entrance
            ref CaveEntranceMarkerRuntimeState markerState = ref markerStates[markerIndex];
            Transform markerTransform = markerState.Transform;
            if (markerTransform == null)
                return;

            GameObject marker = markerState.GameObject;
            if (marker == null)
                return;

            if (instance.preset == null ||
                !CaveDressingRuntimeSanitizer.IsFinite(position) ||
                !CaveDressingRuntimeSanitizer.IsFinite(inwardDirection))
            {
                if (marker.activeSelf)
                    marker.SetActive(false);
                return;
            }

            // Adjust effects based on cave mood and hazard
            float mood = CaveDressingRuntimeSanitizer.SaturateFinite(instance.preset.moodLevel);
            float hazard = CaveDressingRuntimeSanitizer.SaturateFinite(instance.preset.hazardLevel);
            markerTransform.position = position + Vector3.up * 0.5f; // Slightly above ground
            float inwardDirectionSq = inwardDirection.sqrMagnitude;
            markerTransform.rotation = inwardDirectionSq > 0.001f && math.isfinite(inwardDirectionSq)
                ? Quaternion.LookRotation(inwardDirection * math.rsqrt(inwardDirectionSq), Vector3.up)
                : Quaternion.identity;
            if (!marker.activeSelf)
                marker.SetActive(true);

            // Light color based on mood/hazard
            Color lightColor;
            if (hazard > 0.7f)
            {
                lightColor = _EntranceHazardColor; // Red for danger
            }
            else if (mood > 0.6f)
            {
                lightColor = _EntranceLifeColor; // Green for life
            }
            else
            {
                lightColor = _EntranceNeutralColor; // Warm for neutral
            }

            // Add a light for visibility
            Light entranceLight = markerState.Light;
            if (entranceLight == null)
            {
                if (marker.activeSelf)
                    marker.SetActive(false);
                return;
            }

            entranceLight.type = LightType.Point;
            entranceLight.color = lightColor;
            entranceLight.intensity = 1f + mood * 2f; // Brighter for active caves
            entranceLight.range = 4f + hazard * 2f; // Wider for dangerous caves

            // Add particle system for atmospheric effect
            ParticleSystem ps = markerState.Particles;
            if (ps == null)
            {
                if (marker.activeSelf)
                    marker.SetActive(false);
                return;
            }

            var main = ps.main;
            main.startSize = 0.05f + mood * 0.15f;
            main.startSpeed = 0.2f + mood * 0.8f;
            main.startLifetime = 2f + mood * 2f;
            main.maxParticles = 10 + (int)(mood * 30);

            var emission = ps.emission;
            emission.rateOverTime = 3f + mood * 10f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f + hazard * 0.4f;

            // Particle color based on context
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = ResolveEntranceMarkerGradient(instance.preset.spawnContext, lightColor);
        }

        private void QueueCaveVisualSync(long caveKey, CavePreset preset)
        {
            for (int i = 0; i < _pendingCaveVisualSyncs.Count; i++)
            {
                if (_pendingCaveVisualSyncs[i].CaveKey != caveKey)
                    continue;

                _pendingCaveVisualSyncs[i] = new PendingCaveVisualSync(caveKey, preset);
                if (Application.isPlaying && !_registeredLateFrame)
                    _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                return;
            }

            if (_pendingCaveVisualSyncs.Count >= _pendingCaveVisualSyncs.Capacity)
                return;

            _pendingCaveVisualSyncs.Add(new PendingCaveVisualSync(caveKey, preset));
            if (Application.isPlaying && !_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void FlushPendingCaveVisualSyncs()
        {
            int count = _pendingCaveVisualSyncs.Count;
            if (count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                PendingCaveVisualSync pending = _pendingCaveVisualSyncs[i];
                if (!_caveInstances.TryGetValue(pending.CaveKey, out CaveInstance instance) ||
                    instance.isActive == 0 ||
                    instance.volume == null)
                {
                    continue;
                }

                if (!HasCaveVisualRuntimeState(pending.CaveKey))
                    continue;

                SpawnEntranceVisualCues(instance);
                ApplyEntranceQualityPass(instance, pending.Preset);
                InitializeCaveDressingLayer(instance, pending.Preset);
            }

            _pendingCaveVisualSyncs.Clear();
        }

        private bool HasCaveVisualRuntimeState(long caveKey)
        {
            return _caveVisualRuntimeStates.TryGetValue(caveKey, out CaveVisualRuntimeState visualState) &&
                visualState != null;
        }

        private void ApplyEntranceQualityPass(CaveInstance instance, CavePreset preset)
        {
            // Entrance quality improvements:
            // 1. Mark entrance zone as "safe" for debris placement
            // 2. Add subtle entrance glow aura
            // 3. Ensure entrance seams are clean (no floating geometry)

            if (instance.volume == null) return;
            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            // Create an entrance quality marker for in-game logic
            Transform entranceQualityRoot = ActivateCachedRoot(visualState.EntranceQualityRoot);
            if (entranceQualityRoot == null)
                return;

            GameObject entranceQualityGO = visualState.EntranceQualityObject;
            if (entranceQualityGO == null)
                return;
            if (preset == null)
            {
                DisableEntranceQualityObject(entranceQualityGO);
                return;
            }

            entranceQualityRoot.localPosition = Vector3.zero;
            entranceQualityRoot.localRotation = Quaternion.identity;
            entranceQualityRoot.localScale = Vector3.one;
            float safeEntranceRadius = CaveDressingRuntimeSanitizer.ClampFinite(
                preset.entranceRadius,
                EntranceQualityFallbackRadius,
                EntranceQualityMinRadius,
                EntranceQualityMaxRadius);

            // Add collider as "quality zone" marker
            SphereCollider sphereCollider = visualState.EntranceQualityCollider;
            if (sphereCollider == null)
            {
                DisableEntranceQualityObject(entranceQualityGO);
                return;
            }
            sphereCollider.radius = safeEntranceRadius * 2f;
            sphereCollider.isTrigger = true;

            // Add light glow aura at entrance for safe zone feel
            Light entranceGlow = visualState.EntranceQualityLight;
            if (entranceGlow == null)
            {
                DisableEntranceQualityObject(entranceQualityGO);
                return;
            }
            entranceGlow.type = LightType.Point;
            entranceGlow.color = new Color(0.8f, 0.7f, 0.5f); // warm safety glow
            entranceGlow.intensity = 0.5f;
            entranceGlow.range = safeEntranceRadius * 3f;
            entranceGlow.renderingLayerMask = HectonLayerMasks.AllDefinedProjectLayersMask;

            if (!entranceQualityGO.activeSelf)
                entranceQualityGO.SetActive(true);
        }

        private void InitializeCaveDressingLayer(CaveInstance instance, CavePreset preset)
        {
            // Initialize cheap dressing layer for cave interiors:
            // 1. Get dressing config based on spawn context + hazard
            // 2. Apply shader overlays (mineral crust, wall growth)
            // 3. Place simple sediment shelf meshes
            // 4. Spawn fungi particle systems

            if (instance.volume == null || preset == null) return;

            // Get dressing config for this cave type
            CaveDressingConfig dressingConfig = CaveDressingConfig.GetConfigForContext(preset.spawnContext);
            if (dressingConfig == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            Transform dressingRoot = ActivateCachedRoot(visualState.DressingRoot);
            if (dressingRoot == null)
                return;

            // Apply mineral crust if enabled
            if (dressingConfig.mineralCrust != null && dressingConfig.mineralCrust.enabled)
            {
                ApplyMineralCrustToVolume(instance.volume, dressingConfig.mineralCrust);
            }

            if (dressingConfig.wallGrowth != null && dressingConfig.wallGrowth.enabled)
            {
                ApplyWallGrowth(instance, dressingConfig);
            }

            if (dressingConfig.glowingTissue != null && dressingConfig.glowingTissue.enabled)
            {
                ApplyGlowingTissue(instance, dressingConfig);
            }

            // Spawn sediment shelves if enabled
            if (dressingConfig.sedimentShelves != null && dressingConfig.sedimentShelves.enabled)
            {
                SpawnSedimentShelves(instance, dressingConfig);
            }

            if (dressingConfig.serviceRemnants != null && dressingConfig.serviceRemnants.enabled)
            {
                ApplyServiceRemnants(instance, dressingConfig);
            }

            if (dressingConfig.bioRoots != null && dressingConfig.bioRoots.enabled)
            {
                ApplyBioRoots(instance, dressingConfig);
            }

            if (dressingConfig.thermalGeysers != null && dressingConfig.thermalGeysers.enabled)
            {
                ApplyThermalGeysers(instance, dressingConfig);
            }

            // Spawn fungi particles if enabled
            if (dressingConfig.deepFungi != null && dressingConfig.deepFungi.enabled)
            {
                SpawnDeepFungiParticles(dressingRoot.gameObject, instance, dressingConfig.deepFungi);
            }
        }

        private void ApplyMineralCrustToVolume(HectonVoxelVolume volume, MineralCrustConfig config)
        {
            // Apply mineral crust as material property block to the cave mesh
            MeshRenderer meshRenderer = volume != null ? volume.CachedMeshRenderer : null;
            if (meshRenderer == null || config == null) return;

            _CaveSurfacePropertyBlock.Clear();
            meshRenderer.GetPropertyBlock(_CaveSurfacePropertyBlock);
            float crustIntensity = CaveDressingRuntimeSanitizer.SaturateFinite(config.intensity, 0f) *
                CaveDressingRuntimeSanitizer.ClampFinite(config.scale, 1f, 0.1f, 2f);
            float roughnessBoost = CaveDressingRuntimeSanitizer.SaturateFinite(config.roughnessBoost);
            Color crustTint = CaveDressingRuntimeSanitizer.SanitizeColor(config.tint, new Color(0.9f, 0.85f, 0.7f, 1f));

            // Set crust parameters (assuming shader has these properties)
            _CaveSurfacePropertyBlock.SetFloat(_CrustIntensityId, crustIntensity);
            _CaveSurfacePropertyBlock.SetColor(_CrustColorId, crustTint);
            _CaveSurfacePropertyBlock.SetFloat(_CrustRoughnessId, roughnessBoost);

            meshRenderer.SetPropertyBlock(_CaveSurfacePropertyBlock);
        }

        private void ApplyWallGrowth(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            CaveWallGrowthRuntimeBuilder.BuildPreparedCachedHot(
                visualState.WallGrowthRoot,
                visualState.WallGrowthPrimitives.Objects,
                visualState.WallGrowthPrimitives.Filters,
                visualState.WallGrowthPrimitives.Renderers,
                instance.volume,
                instance.preset,
                dressingConfig.wallGrowth,
                dressingConfig.globalIntensity);
        }

        private void ApplyGlowingTissue(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            CaveGlowingTissueRuntimeBuilder.BuildPreparedCachedHot(
                visualState.GlowingTissueRoot,
                visualState.GlowingTissuePrimitives.Objects,
                visualState.GlowingTissuePrimitives.Filters,
                visualState.GlowingTissuePrimitives.Renderers,
                instance.volume,
                instance.preset,
                dressingConfig.glowingTissue,
                dressingConfig.globalIntensity);
        }

        private void ApplyServiceRemnants(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            CaveServiceRemnantRuntimeBuilder.BuildPreparedCachedHot(
                visualState.ServiceRemnantRoot,
                visualState.ServiceRemnantPrimitives.Objects,
                visualState.ServiceRemnantPrimitives.Filters,
                visualState.ServiceRemnantPrimitives.Renderers,
                instance.volume,
                instance.preset,
                dressingConfig.serviceRemnants,
                dressingConfig.globalIntensity);
        }

        private void ApplyBioRoots(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null || dressingConfig.bioRoots == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            Transform rootsTransform = ActivateCachedRoot(visualState.BioRootsRoot);
            if (rootsTransform == null)
                return;

            CaveBioRootsGenerator generator = visualState.BioRootsGenerator;
            if (generator == null)
                return;
        }

        private void ApplyThermalGeysers(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null || dressingConfig.thermalGeysers == null)
                return;

            ThermalGeyserConfig geyserConfig = dressingConfig.thermalGeysers;
            int maxGeyserCount = Mathf.Clamp(geyserConfig.maxCount, 0, ThermalGeyserNameCapacity);
            float safeGeyserIntensity = ResolveFiniteClamp(dressingConfig.globalIntensity, 1f, 0f, 1.25f);
            int geyserCount = Mathf.Clamp(Mathf.RoundToInt(maxGeyserCount * safeGeyserIntensity), 0, maxGeyserCount);
            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null ||
                visualState.ThermalGeysers == null)
            {
                return;
            }

            Transform geyserRoot = ActivateCachedRoot(visualState.ThermalGeyserRoot);
            if (geyserRoot == null)
                return;

            for (int geyserIndex = 0; geyserIndex < geyserCount; geyserIndex++)
            {
                if ((uint)geyserIndex >= (uint)visualState.ThermalGeysers.Length)
                    break;

                ref ThermalGeyserRuntimeState geyserState = ref visualState.ThermalGeysers[geyserIndex];
                Transform geyserTransform = geyserState.Transform;
                if (geyserTransform == null)
                    continue;

                GameObject geyserObject = geyserState.GameObject;
                if (geyserObject == null)
                    continue;

                ThermalGeyser geyser = geyserState.Geyser;
                if (geyser == null)
                    continue;

                geyserTransform.localPosition = ResolveThermalGeyserLocalPosition(instance.volume, instance.preset, geyserIndex);
                geyserTransform.localRotation = Quaternion.identity;
                geyser.Configure(geyserConfig, safeGeyserIntensity);

                if (!geyserObject.activeSelf)
                    geyserObject.SetActive(true);
            }

            DisableUnusedThermalGeysers(visualState.ThermalGeysers, geyserCount);
        }

        private Vector3 ResolveThermalGeyserLocalPosition(HectonVoxelVolume volume, CavePreset preset, int geyserIndex)
        {
            if (volume == null ||
                !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, preset, out Bounds bounds) ||
                !IsFiniteBounds(bounds))
            {
                return Vector3.zero;
            }

            float margin = 1.25f;
            float minX = bounds.min.x + margin;
            float maxX = bounds.max.x - margin;
            float minZ = bounds.min.z + margin;
            float maxZ = bounds.max.z - margin;
            if (maxX < minX)
                minX = maxX = bounds.center.x;
            if (maxZ < minZ)
                minZ = maxZ = bounds.center.z;

            float sampleX = math.lerp(minX, maxX, Hash01(geyserIndex + 1, 41));
            float sampleZ = math.lerp(minZ, maxZ, Hash01(geyserIndex + 1, 83));
            return new Vector3(sampleX, bounds.min.y + ThermalGeyserFloorOffset, sampleZ);
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector3(bounds.min) &&
                   IsFiniteVector3(bounds.max) &&
                   IsFiniteVector3(bounds.center);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static float ResolveFiniteClamp(float value, float fallback, float minimum, float maximum)
        {
            float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
            float safeValue = math.select(safeFallback, value, math.isfinite(value));
            return math.clamp(safeValue, minimum, maximum);
        }

        private static float Hash01(int index, int salt)
        {
            uint hash = unchecked((uint)index * 0x9E3779B9u) ^ unchecked((uint)salt * 0x85EBCA6Bu);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void SpawnSedimentShelves(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;
            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            CaveSedimentShelfRuntimeBuilder.BuildPreparedCachedHot(
                visualState.SedimentShelfRoot,
                visualState.SedimentShelfPrimitives.Objects,
                visualState.SedimentShelfPrimitives.Filters,
                visualState.SedimentShelfPrimitives.Renderers,
                instance.volume,
                instance.preset,
                dressingConfig.sedimentShelves,
                dressingConfig.globalIntensity);
        }

        private void SpawnDeepFungiParticles(GameObject parent, CaveInstance instance, DeepFungiConfig config)
        {
            if (parent == null || instance.volume == null || config == null)
                return;

            if (!_caveVisualRuntimeStates.TryGetValue(instance.key, out CaveVisualRuntimeState visualState) ||
                visualState == null)
            {
                return;
            }

            Transform fungiTransform = visualState.FungiTransform;
            GameObject fungiGO = visualState.FungiObject;
            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(instance.volume, instance.preset, out Bounds volumeBounds) ||
                !CaveDressingRuntimeSanitizer.IsFinite(volumeBounds))
            {
                DisableFungiObject(fungiGO);
                return;
            }

            if (fungiTransform == null)
                return;
            else if (fungiGO != null && !fungiGO.activeSelf)
            {
                fungiGO.SetActive(true);
            }

            float verticalBias = CaveDressingRuntimeSanitizer.SaturateFinite(config.verticalBias, 0.3f);
            float verticalMin = math.lerp(volumeBounds.min.y, volumeBounds.center.y, 0.2f);
            float verticalMax = math.lerp(volumeBounds.center.y, volumeBounds.max.y, 0.85f);
            Vector3 emissionCenter = new Vector3(
                volumeBounds.center.x,
                math.lerp(verticalMin, verticalMax, verticalBias),
                volumeBounds.center.z);
            Vector3 emissionSize = new Vector3(
                Mathf.Max(2f, volumeBounds.size.x * 0.72f),
                Mathf.Max(1.5f, volumeBounds.size.y * 0.28f),
                Mathf.Max(2f, volumeBounds.size.z * 0.72f));
            if (!CaveDressingRuntimeSanitizer.IsFinite(emissionCenter) ||
                !CaveDressingRuntimeSanitizer.IsFinite(emissionSize))
            {
                DisableFungiObject(fungiGO);
                return;
            }

            float volumeFactor = CaveDressingRuntimeSanitizer.SaturateFinite(
                (volumeBounds.size.x * volumeBounds.size.y * volumeBounds.size.z) / 6000f,
                0f);

            fungiTransform.localPosition = emissionCenter;

            ParticleSystem ps = visualState.FungiParticles;
            if (ps == null)
            {
                DisableFungiObject(fungiGO);
                return;
            }

            float particleSize = CaveDressingRuntimeSanitizer.ClampFinite(config.particleSize, 0.1f, 0.01f, 0.5f);
            float lifetime = CaveDressingRuntimeSanitizer.ClampFinite(config.lifetime, 2f, 0.5f, 5f);
            float density = CaveDressingRuntimeSanitizer.SaturateFinite(config.density, 0.5f);
            float emissionRate = CaveDressingRuntimeSanitizer.ClampFinite(config.emissionRate, 10f, 0f, 50f);
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize * 1.5f);
            main.startLifetime = lifetime;
            main.maxParticles = Mathf.Clamp(
                Mathf.RoundToInt(math.lerp(18f, 84f, volumeFactor) * density),
                8,
                96);

            var emission = ps.emission;
            emission.rateOverTime = emissionRate * math.lerp(0.7f, 1.2f, volumeFactor);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.BoxShell;
            shape.scale = emissionSize;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            DeepFungiParticleCache fungiCache = visualState.FungiCache;
            if (fungiCache == null)
            {
                DisableFungiObject(fungiGO);
                return;
            }

            Color glowColor = CaveDressingRuntimeSanitizer.SanitizeColor(config.glowColor, new Color(0.22f, 0.72f, 0.56f, 1f));
            if (!fungiCache.TryResolveGradient(glowColor, out Gradient fungiGradient))
            {
                DisableFungiObject(fungiGO);
                return;
            }
            colorOverLifetime.color = fungiGradient;
        }

        private static void DisableFungiObject(GameObject fungiObject)
        {
            if (fungiObject != null && fungiObject.activeSelf)
                fungiObject.SetActive(false);
        }

        private static void DisableEntranceQualityObject(GameObject entranceQualityObject)
        {
            if (entranceQualityObject != null && entranceQualityObject.activeSelf)
                entranceQualityObject.SetActive(false);
        }

        private static Transform ActivateCachedRoot(Transform root)
        {
            if (root == null)
                return null;

            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);

            return root;
        }

        private static void DisableUnusedEntranceMarkers(CaveEntranceMarkerRuntimeState[] markerStates, int usedMarkerCount)
        {
            if (markerStates == null)
                return;

            for (int i = usedMarkerCount; i < markerStates.Length; i++)
            {
                GameObject markerObject = markerStates[i].GameObject;
                if (markerObject != null && markerObject.activeSelf)
                    markerObject.SetActive(false);
            }
        }

        private static void DisableUnusedThermalGeysers(ThermalGeyserRuntimeState[] geyserStates, int usedGeyserCount)
        {
            if (geyserStates == null)
                return;

            for (int i = usedGeyserCount; i < geyserStates.Length; i++)
            {
                GameObject geyserObject = geyserStates[i].GameObject;
                if (geyserObject != null && geyserObject.activeSelf)
                    geyserObject.SetActive(false);
            }
        }

        private static string GetCachedEntranceMarkerName(int index)
        {
            if ((uint)index < (uint)_EntranceMarkerNames.Length)
                return _EntranceMarkerNames[index];

            return "_EntranceMarker";
        }

        private static string GetCachedThermalGeyserName(int index)
        {
            if ((uint)index < (uint)_ThermalGeyserNames.Length)
                return _ThermalGeyserNames[index];

            return "_ThermalGeyser";
        }

        private static string[] CreateIndexedNameCache(string prefix, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = prefix + i;

            return names;
        }

        private static string[] CreateTwoDigitNameCache(string prefix, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = i < 10 ? prefix + "0" + i : prefix + i;

            return names;
        }

        private static Gradient ResolveEntranceMarkerGradient(SpawnContext spawnContext, Color lightColor)
        {
            if (spawnContext == SpawnContext.CaveDeep)
                return _EntranceDeepGradient;

            if (lightColor == _EntranceHazardColor)
                return _EntranceHazardGradient;

            if (lightColor == _EntranceLifeColor)
                return _EntranceLifeGradient;

            return _EntranceNeutralGradient;
        }

        private static Gradient CreateStaticGradient(Color color, float alpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.clear, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private void RefreshCaveLifecycleState()
        {
            _staleCaveKeyBuffer.Clear();
            Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
            while (caveEnumerator.MoveNext())
            {
                KeyValuePair<long, CaveInstance> pair = caveEnumerator.Current;
                CaveInstance instance = pair.Value;
                if (IsTrackedVolumeAlive(pair.Key, instance.volume))
                    continue;

                if (_staleCaveKeyBuffer.Count >= _staleCaveKeyBuffer.Capacity)
                    break;

                _staleCaveKeyBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _staleCaveKeyBuffer.Count; i++)
                RemoveTrackedCave(_staleCaveKeyBuffer[i], despawnOwnedVolume: false);
        }

        private bool IsTrackedVolumeAlive(long caveKey, HectonVoxelVolume volume)
        {
            if (volume == null)
                return false;

            GameObject volumeObject = volume.gameObject;
            if (volumeObject == null || !volumeObject.activeInHierarchy)
                return false;

            return volume.caveKey == caveKey;
        }

        private void RemoveTrackedCave(long caveKey)
        {
            RemoveTrackedCave(caveKey, despawnOwnedVolume: false);
        }

        private void RemoveTrackedCave(long caveKey, bool despawnOwnedVolume)
        {
            if (despawnOwnedVolume &&
                _caveInstances.TryGetValue(caveKey, out CaveInstance instance) &&
                IsTrackedVolumeAlive(caveKey, instance.volume))
            {
                CleanupSpawnedVolume(instance.volume.gameObject);
            }

            _caveInstances.Remove(caveKey);
            _activeCaveKeys.Remove(caveKey);
            _caveVisualRuntimeStates.Remove(caveKey);
            ReleaseRuntimePreset(caveKey);
            if (_caveEntranceHints.Remove(caveKey))
                _entranceHintVersion = _entranceHintVersion == int.MaxValue ? 1 : _entranceHintVersion + 1;
        }

        private void PrepareCaveVisualRuntimeState(long caveKey, HectonVoxelVolume volume, CavePreset preset, CaveEntrance[] entrances)
        {
            int entranceCount = entrances != null ? entrances.Length : 0;
            Transform entranceMarkerRoot = CreateOrActivateRuntimeRoot(volume, "_EntranceMarkers");
            Transform entranceQualityRoot = CreateOrActivateRuntimeRoot(volume, "_EntranceQualityZone");
            Transform dressingRoot = CreateOrActivateRuntimeRoot(volume, "_CaveDressing");
            CaveDressingConfig dressingConfig = ResolveDressingConfig(preset);
            bool wallGrowthEnabled = dressingConfig != null && dressingConfig.wallGrowth != null && dressingConfig.wallGrowth.enabled;
            bool glowingTissueEnabled = dressingConfig != null && dressingConfig.glowingTissue != null && dressingConfig.glowingTissue.enabled;
            bool serviceRemnantEnabled = dressingConfig != null && dressingConfig.serviceRemnants != null && dressingConfig.serviceRemnants.enabled;
            bool sedimentShelfEnabled = dressingConfig != null && dressingConfig.sedimentShelves != null && dressingConfig.sedimentShelves.enabled;
            CavePrimitiveVisualRuntimeCache wallGrowthPrimitives = wallGrowthEnabled
                ? CreatePrimitiveVisualRuntimeCache(CaveWallGrowthRuntimeBuilder.RuntimeCapacity)
                : default;
            CavePrimitiveVisualRuntimeCache glowingTissuePrimitives = glowingTissueEnabled
                ? CreatePrimitiveVisualRuntimeCache(CaveGlowingTissueRuntimeBuilder.RuntimeCapacity)
                : default;
            CavePrimitiveVisualRuntimeCache serviceRemnantPrimitives = serviceRemnantEnabled
                ? CreatePrimitiveVisualRuntimeCache(CaveServiceRemnantRuntimeBuilder.RuntimeCapacity)
                : default;
            CavePrimitiveVisualRuntimeCache sedimentShelfPrimitives = sedimentShelfEnabled
                ? CreatePrimitiveVisualRuntimeCache(CaveSedimentShelfRuntimeBuilder.RuntimeCapacity)
                : default;
            CaveVisualRuntimeState visualState = new CaveVisualRuntimeState
            {
                EntranceMarkerRoot = entranceMarkerRoot,
                EntranceQualityRoot = entranceQualityRoot,
                DressingRoot = dressingRoot,
                BioRootsRoot = CreateOrActivateChildRoot(dressingRoot, "_CaveBioRoots"),
                ThermalGeyserRoot = CreateOrActivateChildRoot(dressingRoot, "_ThermalGeysers"),
                WallGrowthRoot = wallGrowthEnabled
                    ? CaveWallGrowthRuntimeBuilder.Prewarm(dressingRoot, wallGrowthPrimitives.Objects, wallGrowthPrimitives.Filters, wallGrowthPrimitives.Renderers)
                    : null,
                GlowingTissueRoot = glowingTissueEnabled
                    ? CaveGlowingTissueRuntimeBuilder.Prewarm(dressingRoot, glowingTissuePrimitives.Objects, glowingTissuePrimitives.Filters, glowingTissuePrimitives.Renderers)
                    : null,
                ServiceRemnantRoot = serviceRemnantEnabled
                    ? CaveServiceRemnantRuntimeBuilder.Prewarm(dressingRoot, serviceRemnantPrimitives.Objects, serviceRemnantPrimitives.Filters, serviceRemnantPrimitives.Renderers)
                    : null,
                SedimentShelfRoot = sedimentShelfEnabled
                    ? CaveSedimentShelfRuntimeBuilder.Prewarm(dressingRoot, sedimentShelfPrimitives.Objects, sedimentShelfPrimitives.Filters, sedimentShelfPrimitives.Renderers)
                    : null,
                WallGrowthPrimitives = wallGrowthPrimitives,
                GlowingTissuePrimitives = glowingTissuePrimitives,
                ServiceRemnantPrimitives = serviceRemnantPrimitives,
                SedimentShelfPrimitives = sedimentShelfPrimitives,
                EntranceMarkers = entranceCount > 0
                    ? new CaveEntranceMarkerRuntimeState[entranceCount]
                    : Array.Empty<CaveEntranceMarkerRuntimeState>(),
                ThermalGeysers = new ThermalGeyserRuntimeState[ThermalGeyserNameCapacity]
            };

            PrepareEntranceMarkerRuntimeState(visualState, entranceCount);
            PrepareEntranceQualityRuntimeState(visualState);
            PrepareOptionalDressingRuntimeState(visualState, volume, preset, dressingConfig);
            _caveVisualRuntimeStates[caveKey] = visualState;
        }

        private static CaveDressingConfig ResolveDressingConfig(CavePreset preset)
        {
            SpawnContext spawnContext = preset != null ? preset.spawnContext : SpawnContext.CaveShallow;
            return CaveDressingConfig.GetConfigForContext(spawnContext);
        }

        private static CavePrimitiveVisualRuntimeCache CreatePrimitiveVisualRuntimeCache(int capacity)
        {
            if (capacity <= 0)
                return default;

            return new CavePrimitiveVisualRuntimeCache
            {
                Objects = new GameObject[capacity],
                Filters = new MeshFilter[capacity],
                Renderers = new MeshRenderer[capacity]
            };
        }

        private static Transform CreateOrActivateRuntimeRoot(HectonVoxelVolume volume, string childName)
        {
            if (volume == null)
                return null;

            return volume.GetOrCreateRuntimeRoot(childName);
        }

        private static Transform CreateOrActivateChildRoot(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            Transform child = parent.Find(childName);
            if (child != null)
                return ActivateCachedRoot(child);

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void PrepareEntranceMarkerRuntimeState(CaveVisualRuntimeState visualState, int entranceCount)
        {
            if (visualState == null || visualState.EntranceMarkerRoot == null || visualState.EntranceMarkers == null)
                return;

            for (int i = 0; i < entranceCount; i++)
            {
                ref CaveEntranceMarkerRuntimeState markerState = ref visualState.EntranceMarkers[i];
                string markerName = GetCachedEntranceMarkerName(i);
                Transform markerTransform = i < visualState.EntranceMarkerRoot.childCount
                    ? visualState.EntranceMarkerRoot.GetChild(i)
                    : null;
                GameObject markerObject;
                if (markerTransform != null)
                {
                    markerObject = markerTransform.gameObject;
                    markerObject.name = markerName;
                }
                else
                {
                    markerObject = new GameObject(markerName);
                    markerTransform = markerObject.transform;
                    markerTransform.SetParent(visualState.EntranceMarkerRoot, false);
                }

                markerState.GameObject = markerObject;
                markerState.Transform = markerTransform;
                markerState.Light = markerObject.TryGetComponent(out Light markerLight)
                    ? markerLight
                    : markerObject.AddComponent<Light>();
                markerState.Particles = markerObject.TryGetComponent(out ParticleSystem particles)
                    ? particles
                    : markerObject.AddComponent<ParticleSystem>();
                markerObject.SetActive(false);
            }

            for (int childIndex = entranceCount; childIndex < visualState.EntranceMarkerRoot.childCount; childIndex++)
            {
                Transform child = visualState.EntranceMarkerRoot.GetChild(childIndex);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private static void PrepareEntranceQualityRuntimeState(CaveVisualRuntimeState visualState)
        {
            if (visualState == null || visualState.EntranceQualityRoot == null)
                return;

            GameObject entranceQualityObject = visualState.EntranceQualityRoot.gameObject;
            visualState.EntranceQualityObject = entranceQualityObject;
            visualState.EntranceQualityCollider = entranceQualityObject.TryGetComponent(out SphereCollider entranceCollider)
                ? entranceCollider
                : entranceQualityObject.AddComponent<SphereCollider>();
            visualState.EntranceQualityLight = entranceQualityObject.TryGetComponent(out Light entranceLight)
                ? entranceLight
                : entranceQualityObject.AddComponent<Light>();
        }

        private static void PrepareOptionalDressingRuntimeState(
            CaveVisualRuntimeState visualState,
            HectonVoxelVolume volume,
            CavePreset preset,
            CaveDressingConfig dressingConfig)
        {
            if (visualState == null)
                return;

            if (dressingConfig == null)
                return;

            CaveWallGrowthRuntimeBuilder.PrewarmSharedResources();
            CaveGlowingTissueRuntimeBuilder.PrewarmSharedResources();
            CaveServiceRemnantRuntimeBuilder.PrewarmSharedResources();
            CaveSedimentShelfRuntimeBuilder.PrewarmSharedResources();

            if (dressingConfig.bioRoots != null && dressingConfig.bioRoots.enabled && visualState.BioRootsRoot != null)
            {
                GameObject rootsObject = visualState.BioRootsRoot.gameObject;
                visualState.BioRootsGenerator = rootsObject.TryGetComponent(out CaveBioRootsGenerator generator)
                    ? generator
                    : rootsObject.AddComponent<CaveBioRootsGenerator>();
                visualState.BioRootsGenerator.ConfigureCold(volume, preset, dressingConfig.bioRoots, dressingConfig.globalIntensity);
            }

            if (dressingConfig.thermalGeysers != null &&
                dressingConfig.thermalGeysers.enabled &&
                visualState.ThermalGeyserRoot != null &&
                visualState.ThermalGeysers != null)
            {
                ThermalGeyserConfig geyserConfig = dressingConfig.thermalGeysers;
                int maxGeyserCount = Mathf.Clamp(geyserConfig.maxCount, 0, ThermalGeyserNameCapacity);
                for (int geyserIndex = 0; geyserIndex < maxGeyserCount; geyserIndex++)
                {
                    ref ThermalGeyserRuntimeState geyserState = ref visualState.ThermalGeysers[geyserIndex];
                    string geyserName = GetCachedThermalGeyserName(geyserIndex);
                    Transform geyserTransform = geyserIndex < visualState.ThermalGeyserRoot.childCount
                        ? visualState.ThermalGeyserRoot.GetChild(geyserIndex)
                        : null;
                    GameObject geyserObject;
                    if (geyserTransform != null)
                    {
                        geyserObject = geyserTransform.gameObject;
                        geyserObject.name = geyserName;
                    }
                    else
                    {
                        geyserObject = new GameObject(geyserName);
                        geyserTransform = geyserObject.transform;
                        geyserTransform.SetParent(visualState.ThermalGeyserRoot, false);
                    }

                    geyserState.GameObject = geyserObject;
                    geyserState.Transform = geyserTransform;
                    geyserState.CurrentVolume = geyserObject.TryGetComponent(out CurrentVolume currentVolume)
                        ? currentVolume
                        : geyserObject.AddComponent<CurrentVolume>();
                    geyserState.Geyser = geyserObject.TryGetComponent(out ThermalGeyser geyser)
                        ? geyser
                        : geyserObject.AddComponent<ThermalGeyser>();
                    geyserState.Geyser.CacheRuntimeWiringCold(geyserState.CurrentVolume);
                    geyserObject.SetActive(false);
                }

                for (int childIndex = maxGeyserCount; childIndex < visualState.ThermalGeyserRoot.childCount; childIndex++)
                {
                    Transform child = visualState.ThermalGeyserRoot.GetChild(childIndex);
                    if (child != null && child.gameObject.activeSelf)
                        child.gameObject.SetActive(false);
                }
            }

            if (dressingConfig.deepFungi != null && dressingConfig.deepFungi.enabled && visualState.DressingRoot != null)
            {
                Transform fungiTransform = visualState.DressingRoot.Find("_DeepFungi");
                GameObject fungiObject;
                if (fungiTransform != null)
                {
                    fungiObject = fungiTransform.gameObject;
                }
                else
                {
                    fungiObject = new GameObject("_DeepFungi");
                    fungiTransform = fungiObject.transform;
                    fungiTransform.SetParent(visualState.DressingRoot, false);
                }

                visualState.FungiObject = fungiObject;
                visualState.FungiTransform = fungiTransform;
                visualState.FungiParticles = fungiObject.TryGetComponent(out ParticleSystem fungiParticles)
                    ? fungiParticles
                    : fungiObject.AddComponent<ParticleSystem>();
                visualState.FungiCache = fungiObject.TryGetComponent(out DeepFungiParticleCache fungiCache)
                    ? fungiCache
                    : fungiObject.AddComponent<DeepFungiParticleCache>();
                visualState.FungiCache.Prewarm();
                fungiObject.SetActive(false);
            }
        }

        private void CacheEntranceHints(long caveKey, CaveEntrance[] entrances)
        {
            if (entrances == null || entrances.Length <= 0)
            {
                if (_caveEntranceHints.Remove(caveKey))
                    _entranceHintVersion = _entranceHintVersion == int.MaxValue ? 1 : _entranceHintVersion + 1;
                return;
            }

            int validHintCount = 0;
            for (int i = 0; i < entrances.Length; i++)
            {
                if (TryBuildEntranceHint(in entrances[i], out _))
                    validHintCount++;
            }

            if (validHintCount <= 0)
            {
                if (_caveEntranceHints.Remove(caveKey))
                    _entranceHintVersion = _entranceHintVersion == int.MaxValue ? 1 : _entranceHintVersion + 1;
                return;
            }

            CaveEntranceHint[] hints = new CaveEntranceHint[validHintCount]; // COLD ALLOC: one hint array per cave, owner: WorldCaveDirector
            int writeIndex = 0;
            for (int i = 0; i < entrances.Length && writeIndex < hints.Length; i++)
            {
                if (!TryBuildEntranceHint(in entrances[i], out CaveEntranceHint hint))
                    continue;

                hints[writeIndex++] = hint;
            }

            _caveEntranceHints[caveKey] = hints;
            _entranceHintVersion = _entranceHintVersion == int.MaxValue ? 1 : _entranceHintVersion + 1;
        }

        private static bool TryBuildEntranceHint(in CaveEntrance entrance, out CaveEntranceHint hint)
        {
            hint = default;
            Vector3 surfacePosition = entrance.surfacePosition;
            Vector3 inwardDirection = (Vector3)entrance.inwardDirection;
            if (!CaveDressingRuntimeSanitizer.IsFinite(surfacePosition) ||
                !CaveDressingRuntimeSanitizer.IsFinite(inwardDirection) ||
                !math.isfinite(entrance.radius) ||
                !math.isfinite(entrance.funnelLength) ||
                !math.isfinite(entrance.innerRadius) ||
                entrance.radius <= 0f ||
                entrance.funnelLength <= 0f ||
                entrance.innerRadius < 0f)
            {
                return false;
            }

            float directionSq = inwardDirection.sqrMagnitude;
            if (!math.isfinite(directionSq) || directionSq <= 0.0001f)
                return false;

            Vector3 safeDirection = inwardDirection * math.rsqrt(directionSq);
            float entranceRadius = CaveDressingRuntimeSanitizer.ClampFinite(
                entrance.radius,
                EntranceQualityFallbackRadius,
                EntranceQualityMinRadius,
                EntranceQualityMaxRadius);
            float funnelLength = CaveDressingRuntimeSanitizer.ClampFinite(
                entrance.funnelLength,
                entranceRadius * 2f,
                EntranceHintMinFunnelLength,
                EntranceHintMaxFunnelLength);
            float innerRadius = CaveDressingRuntimeSanitizer.ClampFinite(
                entrance.innerRadius,
                entranceRadius * 0.5f,
                0f,
                entranceRadius);
            Vector3 interiorPosition = surfacePosition + (safeDirection * funnelLength);
            if (!CaveDressingRuntimeSanitizer.IsFinite(interiorPosition))
                return false;

            float influenceRadius = CaveDressingRuntimeSanitizer.ClampFinite(
                Mathf.Max(entranceRadius * 2.5f, funnelLength + innerRadius),
                entranceRadius * 2.5f,
                entranceRadius,
                EntranceHintMaxInfluenceRadius);
            hint = new CaveEntranceHint(
                surfacePosition,
                interiorPosition,
                entranceRadius,
                influenceRadius);
            return true;
        }

        private void RefreshBiomeRuntimeContext(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
            {
                _cachedBiomeRuntimeContext = default;
                return;
            }

            string familyId = biomeFamily.familyId ?? string.Empty;
            if (ReferenceEquals(_cachedBiomeRuntimeContext.Family, biomeFamily) &&
                string.Equals(_cachedBiomeRuntimeContext.FamilyId, familyId, StringComparison.Ordinal))
            {
                return;
            }

            _cachedBiomeRuntimeContext.Family = biomeFamily;
            _cachedBiomeRuntimeContext.FamilyId = familyId;
            _cachedBiomeRuntimeContext.FamilyLabel = string.IsNullOrEmpty(biomeFamily.familyLabel) ? "None" : biomeFamily.familyLabel;
            _cachedBiomeRuntimeContext.FamilyHash = Hecton.Localization.LocHash.Compute(familyId);
            _cachedBiomeRuntimeContext.SupportsCaves = EvaluateBiomeCaveSupport(familyId);
            _cachedBiomeRuntimeContext.PresetKind = ResolveBiomePresetKind(familyId);
        }

        private static Vector3 ResolveCaveRouteAnchor(WorldZoneAnchor zone, Vector3 playerPosition)
        {
            if (zone == null)
                return playerPosition;

            Vector3 zonePosition = zone.transform.position;
            if (zone.RouteCritical || zone.Kind == WorldZoneAnchor.ZoneKind.Navigation || zone.Kind == WorldZoneAnchor.ZoneKind.Progression)
                return zonePosition;

            return Vector3.Lerp(playerPosition, zonePosition, 0.35f);
        }

        private static float ResolveCaveRouteQuality(WorldZoneAnchor zone)
        {
            if (zone == null)
                return 1f;

            float quality = zone.Kind switch
            {
                WorldZoneAnchor.ZoneKind.Navigation => 1.18f,
                WorldZoneAnchor.ZoneKind.Progression => 1.14f,
                WorldZoneAnchor.ZoneKind.Resources => 1.08f,
                WorldZoneAnchor.ZoneKind.Service => 1.05f,
                WorldZoneAnchor.ZoneKind.Power => 1.05f,
                _ => 1f
            };

            if (zone.RouteCritical)
                quality += 0.08f;

            return Mathf.Clamp(quality, 0.9f, 1.3f);
        }

        private bool IsCandidateTooClose(Vector3 candidatePosition, float minSpacing)
        {
            float minSpacingSqr = Mathf.Max(0f, minSpacing) * Mathf.Max(0f, minSpacing);

            Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
            while (caveEnumerator.MoveNext())
            {
                CaveInstance existing = caveEnumerator.Current.Value;
                if (Vector3.SqrMagnitude(existing.position - candidatePosition) < minSpacingSqr)
                    return true;
            }

            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                if (Vector3.SqrMagnitude(_candidateBuffer[i] - candidatePosition) < minSpacingSqr)
                    return true;
            }

            return false;
        }

        private PendingCaveSpawnState CreatePendingSpawnState()
        {
            _pendingSpawnVersion = _pendingSpawnVersion == int.MaxValue ? 1 : _pendingSpawnVersion + 1;
            return new PendingCaveSpawnState(_pendingSpawnVersion);
        }

        private CancellationTokenSource EnsureLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                _lifetimeCancellation = new CancellationTokenSource();

            return _lifetimeCancellation;
        }

        private void CancelLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                return;

            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }

        private void CancelAllPendingSpawns()
        {
            _pendingCaveKeyBuffer.Clear();
            Dictionary<long, PendingCaveSpawnState>.Enumerator pendingEnumerator = _pendingCaveSpawns.GetEnumerator();
            while (pendingEnumerator.MoveNext())
            {
                if (_pendingCaveKeyBuffer.Count >= _pendingCaveKeyBuffer.Capacity)
                    break;

                _pendingCaveKeyBuffer.Add(pendingEnumerator.Current.Key);
            }

            for (int i = 0; i < _pendingCaveKeyBuffer.Count; i++)
            {
                long caveKey = _pendingCaveKeyBuffer[i];
                _pendingCaveSpawns.Remove(caveKey);
            }

            _debugPendingCaves = _pendingCaveSpawns.Count;
        }

        private void CompletePendingSpawn(long caveKey, PendingCaveSpawnState pendingState)
        {
            if (_pendingCaveSpawns.TryGetValue(caveKey, out PendingCaveSpawnState currentState) &&
                currentState.Version == pendingState.Version)
            {
                _pendingCaveSpawns.Remove(caveKey);
            }
        }

        private void CleanupSpawnedVolume(GameObject caveVolume)
        {
            CleanupSpawnedVolume(voxelEngine, caveVolume);
        }

        private void CleanupSpawnedVolume(HectonVoxelEngine ownerVoxelEngine, GameObject caveVolume)
        {
            if (caveVolume == null)
                return;

            if (ownerVoxelEngine != null)
            {
                ownerVoxelEngine.DespawnVolume(caveVolume);
                return;
            }

            Destroy(caveVolume);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCaveGenerated(Vector3 position)
        {
            Hecton8.Core.H8Debug.Log("[WorldCaveDirector] Cave generated.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNoGeometry(Vector3 position)
        {
            Hecton8.Core.H8Debug.LogWarning("[WorldCaveDirector] Cave generation produced no geometry.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCaveSpawnFailure(Vector3 position, string message)
        {
            Hecton8.Core.H8Debug.LogError(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingVoxelEngine()
        {
            Hecton8.Core.H8Debug.LogWarning("[WorldCaveDirector] No voxel engine available for cave generation");
        }
    }
}
