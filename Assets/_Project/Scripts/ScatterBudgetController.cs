using System.Globalization;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4200)]
    public sealed class ScatterBudgetController : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const string SurfaceBudgetBandLabel = "Surface";
        private const string MidDepthBudgetBandLabel = "MidDepth";
        private const string DeepBudgetBandLabel = "Deep";
        private const float MinimumDepthBandHysteresisMeters = 3f;
        private const float DefaultDepthBandHysteresisMeters = 5f;
        private const float MinimumBudgetRefreshDepthDelta = 1f;
        private const float MinimumBudgetRefreshQualityDelta = 0.025f;

        internal static ScatterBudgetController ActiveRuntimeInstance { get; private set; }
        internal static event System.Action<ScatterBudgetController> ActiveRuntimeInstanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged = null;
        }

        private enum BudgetBand
        {
            Surface,
            MidDepth,
            Deep
        }

        [System.Serializable]
        private struct BudgetProfile
        {
            public float scavengeUnloadDistance;
            public float scavengePriorityRadius;
            public int scavengeSpawnsPerTick;
            public float colliderActivateRadius;
            public float colliderDeactivateRadius;
            public int colliderOpsPerTick;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private ScavengePopulator scavengePopulator;
        [SerializeField] private ProximityColliderSystem proximityColliderSystem;
        [SerializeField] private BiomeSamplerCache biomeSamplerCache;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Depth Thresholds")]
        [SerializeField] private float midDepthStart = 60f;
        [SerializeField] private float deepDepthStart = 180f;
        [SerializeField, Range(MinimumDepthBandHysteresisMeters, 12f)] private float depthBandHysteresisMeters = DefaultDepthBandHysteresisMeters;

        [Header("Profiles")]
        [SerializeField] private BudgetProfile surfaceProfile;
        [SerializeField] private BudgetProfile midDepthProfile;
        [SerializeField] private BudgetProfile deepProfile;

        // Inspector-only live diagnostics for depth-band budget tuning.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private string _debugCurrentBand = "Surface";
        [SerializeField] private string _debugLastBlocker = "None";
        [SerializeField] private float _debugCurrentDepth;
        [SerializeField] private bool _debugApplied;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugScavengeReady;
        [SerializeField] private bool _debugColliderReady;
        [SerializeField] private float _debugDirectorScavengeRadiusScale = 1f;
        [SerializeField] private float _debugDirectorSpawnScale = 1f;
        [SerializeField] private float _debugDirectorColliderRadiusScale = 1f;
        [SerializeField] private float _debugDirectorColliderOpsScale = 1f;
        [SerializeField] private float _debugInterestScavengeRadiusScale = 1f;
        [SerializeField] private float _debugInterestSpawnScale = 1f;
        [SerializeField] private float _debugInterestColliderRadiusScale = 1f;
        [SerializeField] private float _debugInterestColliderOpsScale = 1f;
        [SerializeField] private float _debugZoneScavengeRadiusScale = 1f;
        [SerializeField] private float _debugZoneSpawnScale = 1f;
        [SerializeField] private float _debugZoneColliderRadiusScale = 1f;
        [SerializeField] private float _debugZoneColliderOpsScale = 1f;
        [SerializeField] private float _debugProfileResourcesNearScale = 1f;
        [SerializeField] private float _debugProfileDebrisNearScale = 1f;
#pragma warning restore CS0414

        private bool _registeredToTickManager;
        private BudgetBand _lastAppliedBand = (BudgetBand)(-1);
        private float _directorScavengeRadiusScale = 1f;
        private float _directorSpawnScale = 1f;
        private float _directorColliderRadiusScale = 1f;
        private float _directorColliderOpsScale = 1f;
        private float _interestScavengeRadiusScale = 1f;
        private float _interestSpawnScale = 1f;
        private float _interestColliderRadiusScale = 1f;
        private float _interestColliderOpsScale = 1f;
        private float _zoneScavengeRadiusScale = 1f;
        private float _zoneSpawnScale = 1f;
        private float _zoneColliderRadiusScale = 1f;
        private float _zoneColliderOpsScale = 1f;
        private float _lastAppliedDepth = float.NaN;
        private float _lastAppliedQualityWeight = -1f;
        private float _cachedGlobalQualityWeight = 1f;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private bool _proximityColliderListenerRegistered;
        private bool _biomeSamplerListenerRegistered;

        private void Reset()
        {
            surfaceProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 320f,
                scavengePriorityRadius = 150f,
                scavengeSpawnsPerTick = 24,
                colliderActivateRadius = 42f,
                colliderDeactivateRadius = 48f,
                colliderOpsPerTick = 64
            };

            midDepthProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 260f,
                scavengePriorityRadius = 120f,
                scavengeSpawnsPerTick = 18,
                colliderActivateRadius = 34f,
                colliderDeactivateRadius = 40f,
                colliderOpsPerTick = 48
            };

            deepProfile = new BudgetProfile
            {
                scavengeUnloadDistance = 220f,
                scavengePriorityRadius = 90f,
                scavengeSpawnsPerTick = 12,
                colliderActivateRadius = 26f,
                colliderDeactivateRadius = 32f,
                colliderOpsPerTick = 32
            };
        }

        private void Awake()
        {
            PublishActiveRuntimeInstance();
            RefreshColdReferences(force: true);
            ApplyChunkProfileDefaults();
            ClampProfiles();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            RegisterRuntimeDependencyListeners();
            RefreshColdReferences(force: false);
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            ApplyCurrentBudget(force: true);
        }

        private void OnDisable()
        {
            TryUnregister();
            UnregisterRuntimeDependencyListeners();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnregisterRuntimeDependencyListeners();
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            if (ActiveRuntimeInstance == this)
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
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
                if (previousContext != null && ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                    playerTransform = null;

                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                if (_playerRuntimeContext == null)
                {
                    playerTransform = null;
                    _playerMovement = null;
                }
                else
                {
                    RefreshPlayerFromCachedContext();
                }
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.MapMagicRuntime)
            {
                if (previousService != null && ReferenceEquals(mapMagicBridge, previousService))
                    mapMagicBridge = null;

                if (currentService is MapMagicBridge currentMapMagic)
                    mapMagicBridge = currentMapMagic;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.ScavengePopulatorRuntime)
            {
                if (previousService != null && ReferenceEquals(scavengePopulator, previousService))
                    scavengePopulator = null;

                if (currentService is ScavengePopulator currentScavenge)
                    scavengePopulator = currentScavenge;
            }
        }

        private void TryRegister()
        {
            if (_registeredToTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;


            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        public void SlowTick()
        {
            ApplyCurrentBudget(force: false);
        }

        /// <summary>
        /// Returns a compact summary for diagnostics and log output.
        /// </summary>
        public string DescribeStatus()
        {
            return
                "band=" + _debugCurrentBand + " depth=" + _debugCurrentDepth.ToString("F1", CultureInfo.InvariantCulture) + " applied=" + _debugApplied + " " +
                "blocker=" + _debugLastBlocker + " player=" + _debugPlayerReady + " bridge=" + _debugBridgeReady + " " +
                "scavenge=" + _debugScavengeReady + " collider=" + _debugColliderReady;
        }

        public void SetDirectorScales(
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            float clampedScavengeRadius = Mathf.Clamp(scavengeRadiusScale, 0.4f, 1.5f);
            float clampedSpawn = Mathf.Clamp(spawnScale, 0.4f, 1.5f);
            float clampedColliderRadius = Mathf.Clamp(colliderRadiusScale, 0.4f, 1.5f);
            float clampedColliderOps = Mathf.Clamp(colliderOpsScale, 0.4f, 1.5f);

            bool changed =
                !Mathf.Approximately(_directorScavengeRadiusScale, clampedScavengeRadius) ||
                !Mathf.Approximately(_directorSpawnScale, clampedSpawn) ||
                !Mathf.Approximately(_directorColliderRadiusScale, clampedColliderRadius) ||
                !Mathf.Approximately(_directorColliderOpsScale, clampedColliderOps);

            _directorScavengeRadiusScale = clampedScavengeRadius;
            _directorSpawnScale = clampedSpawn;
            _directorColliderRadiusScale = clampedColliderRadius;
            _directorColliderOpsScale = clampedColliderOps;

            if (changed)
                ApplyCurrentBudget(force: true);
        }

        public void SetInterestScales(
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            float clampedScavengeRadius = Mathf.Clamp(scavengeRadiusScale, 0.75f, 1.4f);
            float clampedSpawn = Mathf.Clamp(spawnScale, 0.75f, 1.4f);
            float clampedColliderRadius = Mathf.Clamp(colliderRadiusScale, 0.75f, 1.4f);
            float clampedColliderOps = Mathf.Clamp(colliderOpsScale, 0.75f, 1.4f);

            bool changed =
                !Mathf.Approximately(_interestScavengeRadiusScale, clampedScavengeRadius) ||
                !Mathf.Approximately(_interestSpawnScale, clampedSpawn) ||
                !Mathf.Approximately(_interestColliderRadiusScale, clampedColliderRadius) ||
                !Mathf.Approximately(_interestColliderOpsScale, clampedColliderOps);

            _interestScavengeRadiusScale = clampedScavengeRadius;
            _interestSpawnScale = clampedSpawn;
            _interestColliderRadiusScale = clampedColliderRadius;
            _interestColliderOpsScale = clampedColliderOps;

            if (changed)
                ApplyCurrentBudget(force: true);
        }

        public void SetZoneScales(
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale)
        {
            float clampedScavengeRadius = Mathf.Clamp(scavengeRadiusScale, 0.75f, 1.4f);
            float clampedSpawn = Mathf.Clamp(spawnScale, 0.75f, 1.4f);
            float clampedColliderRadius = Mathf.Clamp(colliderRadiusScale, 0.75f, 1.4f);
            float clampedColliderOps = Mathf.Clamp(colliderOpsScale, 0.75f, 1.4f);

            bool changed =
                !Mathf.Approximately(_zoneScavengeRadiusScale, clampedScavengeRadius) ||
                !Mathf.Approximately(_zoneSpawnScale, clampedSpawn) ||
                !Mathf.Approximately(_zoneColliderRadiusScale, clampedColliderRadius) ||
                !Mathf.Approximately(_zoneColliderOpsScale, clampedColliderOps);

            _zoneScavengeRadiusScale = clampedScavengeRadius;
            _zoneSpawnScale = clampedSpawn;
            _zoneColliderRadiusScale = clampedColliderRadius;
            _zoneColliderOpsScale = clampedColliderOps;

            if (changed)
                ApplyCurrentBudget(force: true);
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            ApplyChunkProfileDefaults();
            ClampProfiles();
            ApplyCurrentBudget(force: true);
        }

        private void ApplyCurrentBudget(bool force)
        {
            RefreshPlayerFromCachedContext();
            ApplyChunkProfileDefaults();
            ClampProfiles();

            if (playerTransform == null || !TryResolveCurrentDepth(out float depth))
            {
                _debugApplied = false;
                _debugCurrentDepth = 0f;
                _debugCurrentBand = "Unresolved";
                _debugLastBlocker = GetBlockerReason();
                UpdateDiagnostics();
                return;
            }

            float globalQualityWeight = ResolveGlobalQualityWeight();
            BudgetBand stableBand = ResolveStableBudgetBand(depth);

            _debugCurrentDepth = depth;
            _debugCurrentBand = ResolveBudgetBandLabel(stableBand);

            if (!force &&
                stableBand == _lastAppliedBand &&
                Mathf.Abs(depth - _lastAppliedDepth) < MinimumBudgetRefreshDepthDelta &&
                Mathf.Abs(globalQualityWeight - _lastAppliedQualityWeight) < MinimumBudgetRefreshQualityDelta)
            {
                UpdateDiagnostics();
                return;
            }

            BudgetProfile profile = ResolveContinuousDepthProfile(depth);
            profile = ApplyGlobalQualityWeight(profile, globalQualityWeight);

            if (scavengePopulator != null)
            {
                float scavengeRadiusScale = Mathf.Clamp(_directorScavengeRadiusScale * _interestScavengeRadiusScale * _zoneScavengeRadiusScale, 0.35f, 1.75f);
                float spawnScale = Mathf.Clamp(_directorSpawnScale * _interestSpawnScale * _zoneSpawnScale, 0.35f, 1.75f);
                scavengePopulator.SetRuntimeBudget(
                    profile.scavengeUnloadDistance * scavengeRadiusScale,
                    profile.scavengePriorityRadius * scavengeRadiusScale,
                    Mathf.Max(1, Mathf.RoundToInt(profile.scavengeSpawnsPerTick * spawnScale)));
            }

            if (proximityColliderSystem != null)
            {
                float colliderRadiusScale = Mathf.Clamp(_directorColliderRadiusScale * _interestColliderRadiusScale * _zoneColliderRadiusScale, 0.35f, 1.75f);
                float colliderOpsScale = Mathf.Clamp(_directorColliderOpsScale * _interestColliderOpsScale * _zoneColliderOpsScale, 0.35f, 1.75f);
                proximityColliderSystem.SetRuntimeBudget(
                    profile.colliderActivateRadius * colliderRadiusScale,
                    profile.colliderDeactivateRadius * colliderRadiusScale,
                    Mathf.Max(4, Mathf.RoundToInt(profile.colliderOpsPerTick * colliderOpsScale)));
            }

            _lastAppliedBand = stableBand;
            _lastAppliedDepth = depth;
            _lastAppliedQualityWeight = globalQualityWeight;
            _debugApplied = true;
            _debugLastBlocker = "None";
            UpdateDiagnostics();
        }

        private BudgetBand GetBandForDepth(float depth)
        {
            if (depth >= deepDepthStart)
                return BudgetBand.Deep;

            if (depth >= midDepthStart)
                return BudgetBand.MidDepth;

            return BudgetBand.Surface;
        }

        private BudgetBand ResolveStableBudgetBand(float depth)
        {
            BudgetBand current = _lastAppliedBand == (BudgetBand)(-1) ? GetBandForDepth(depth) : _lastAppliedBand;
            float hysteresis = ResolveDepthBandHysteresisMeters();

            switch (current)
            {
                case BudgetBand.Deep:
                    return depth < deepDepthStart - hysteresis ? BudgetBand.MidDepth : BudgetBand.Deep;
                case BudgetBand.MidDepth:
                    if (depth < midDepthStart - hysteresis)
                        return BudgetBand.Surface;
                    return depth >= deepDepthStart + hysteresis ? BudgetBand.Deep : BudgetBand.MidDepth;
                default:
                    return depth >= midDepthStart + hysteresis ? BudgetBand.MidDepth : BudgetBand.Surface;
            }
        }

        private BudgetProfile ResolveContinuousDepthProfile(float depth)
        {
            float hysteresis = ResolveDepthBandHysteresisMeters();
            float midBlend = Smooth01(InvLerp(midDepthStart - hysteresis, midDepthStart + hysteresis, depth));
            float deepBlend = Smooth01(InvLerp(deepDepthStart - hysteresis, deepDepthStart + hysteresis, depth));
            BudgetProfile midProfile = LerpProfile(surfaceProfile, midDepthProfile, midBlend);
            return LerpProfile(midProfile, deepProfile, deepBlend);
        }

        private BudgetProfile ApplyGlobalQualityWeight(BudgetProfile profile, float globalQualityWeight)
        {
            float q = Smooth01(globalQualityWeight);
            float scavengeRadiusScale = Mathf.Lerp(0.78f, 1.12f, q);
            float scavengeSpawnScale = Mathf.Lerp(0.55f, 1.25f, q);
            float colliderRadiusScale = Mathf.Lerp(0.85f, 1.08f, q);
            float colliderOpsScale = Mathf.Lerp(0.60f, 1.20f, q);

            profile.scavengeUnloadDistance *= scavengeRadiusScale;
            profile.scavengePriorityRadius *= scavengeRadiusScale;
            profile.scavengeSpawnsPerTick = Mathf.Max(1, Mathf.RoundToInt(profile.scavengeSpawnsPerTick * scavengeSpawnScale));
            profile.colliderActivateRadius *= colliderRadiusScale;
            profile.colliderDeactivateRadius *= colliderRadiusScale;
            profile.colliderOpsPerTick = Mathf.Max(4, Mathf.RoundToInt(profile.colliderOpsPerTick * colliderOpsScale));
            return ClampProfile(profile);
        }

        private static BudgetProfile LerpProfile(BudgetProfile a, BudgetProfile b, float t)
        {
            return new BudgetProfile
            {
                scavengeUnloadDistance = Mathf.Lerp(a.scavengeUnloadDistance, b.scavengeUnloadDistance, t),
                scavengePriorityRadius = Mathf.Lerp(a.scavengePriorityRadius, b.scavengePriorityRadius, t),
                scavengeSpawnsPerTick = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(a.scavengeSpawnsPerTick, b.scavengeSpawnsPerTick, t))),
                colliderActivateRadius = Mathf.Lerp(a.colliderActivateRadius, b.colliderActivateRadius, t),
                colliderDeactivateRadius = Mathf.Lerp(a.colliderDeactivateRadius, b.colliderDeactivateRadius, t),
                colliderOpsPerTick = Mathf.Max(4, Mathf.RoundToInt(Mathf.Lerp(a.colliderOpsPerTick, b.colliderOpsPerTick, t)))
            };
        }

        private static string ResolveBudgetBandLabel(BudgetBand band)
        {
            switch (band)
            {
                case BudgetBand.MidDepth:
                    return MidDepthBudgetBandLabel;
                case BudgetBand.Deep:
                    return DeepBudgetBandLabel;
                default:
                    return SurfaceBudgetBandLabel;
            }
        }

        private void RefreshColdReferences(bool force)
        {
            if (force || _playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            RefreshPlayerFromCachedContext();

            if (force || mapMagicBridge == null)
                mapMagicBridge = GlobalRegistry.MapMagic;

            if (force || scavengePopulator == null)
                scavengePopulator = GlobalRegistry.ScavengePopulator;

            if (force || proximityColliderSystem == null)
                proximityColliderSystem = ProximityColliderSystem.ActiveRuntimeInstance;

            if (force || biomeSamplerCache == null)
                biomeSamplerCache = BiomeSamplerCache.ActiveRuntimeInstance;
        }

        private void RefreshPlayerFromCachedContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
            {
                playerTransform = null;
                _playerMovement = null;
                return;
            }

            if (playerTransform == null && playerContext.PlayerTransform != null)
                playerTransform = playerContext.PlayerTransform;

            if (_playerMovement == null && playerContext.PlayerMovement != null)
                _playerMovement = playerContext.PlayerMovement;

            if (playerTransform == null)
                _playerMovement = null;
        }

        private void RegisterRuntimeDependencyListeners()
        {
            if (!_proximityColliderListenerRegistered)
            {
                ProximityColliderSystem.ActiveRuntimeInstanceChanged += HandleProximityColliderSystemChanged;
                _proximityColliderListenerRegistered = true;
            }

            if (!_biomeSamplerListenerRegistered)
            {
                BiomeSamplerCache.ActiveRuntimeInstanceChanged += HandleBiomeSamplerCacheChanged;
                _biomeSamplerListenerRegistered = true;
            }
        }

        private void UnregisterRuntimeDependencyListeners()
        {
            if (_proximityColliderListenerRegistered)
            {
                ProximityColliderSystem.ActiveRuntimeInstanceChanged -= HandleProximityColliderSystemChanged;
                _proximityColliderListenerRegistered = false;
            }

            if (_biomeSamplerListenerRegistered)
            {
                BiomeSamplerCache.ActiveRuntimeInstanceChanged -= HandleBiomeSamplerCacheChanged;
                _biomeSamplerListenerRegistered = false;
            }
        }

        private void HandleProximityColliderSystemChanged(ProximityColliderSystem current)
        {
            proximityColliderSystem = current;
        }

        private void HandleBiomeSamplerCacheChanged(BiomeSamplerCache current)
        {
            biomeSamplerCache = current;
        }

        private bool TryResolveCurrentDepth(out float depth)
        {
            depth = 0f;

            if (_playerMovement != null)
            {
                depth = Mathf.Max(0f, _playerMovement.CurrentDepth);
                return true;
            }

            if (playerTransform == null || mapMagicBridge == null || !mapMagicBridge.IsAvailable)
                return false;

            depth = Mathf.Max(0f, mapMagicBridge.WaterSurfaceLevel - playerTransform.position.y);
            return true;
        }

        private void ApplyChunkProfileDefaults()
        {
            if (chunkStreamingProfile == null)
            {
                _debugProfileResourcesNearScale = 1f;
                _debugProfileDebrisNearScale = 1f;
                return;
            }

            WorldChunkStreamingProfile.LayerProfile resourcesLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Resources);
            WorldChunkStreamingProfile.LayerProfile debrisLayer =
                chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Debris);

            float basePriority = Mathf.Max(48f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.5f, resourcesLayer.nearRadiusScale));
            float baseUnload = Mathf.Max(basePriority + 48f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.5f, resourcesLayer.midRadiusScale));
            int baseSpawns = Mathf.Max(8, resourcesLayer.maxActivationsPerTick);

            float baseColliderActivate = Mathf.Max(20f, chunkStreamingProfile.fullSimulationRadius * 0.24f * Mathf.Max(0.5f, debrisLayer.nearRadiusScale));
            float baseColliderDeactivate = Mathf.Max(baseColliderActivate + 6f, chunkStreamingProfile.fullSimulationRadius * 0.28f * Mathf.Max(0.5f, debrisLayer.midRadiusScale));
            int baseColliderOps = Mathf.Max(16, debrisLayer.maxActivationsPerTick);

            surfaceProfile = BuildDepthProfile(baseUnload, basePriority, baseSpawns, baseColliderActivate, baseColliderDeactivate, baseColliderOps, 1f);
            midDepthProfile = BuildDepthProfile(baseUnload, basePriority, baseSpawns, baseColliderActivate, baseColliderDeactivate, baseColliderOps, 0.84f);
            deepProfile = BuildDepthProfile(baseUnload, basePriority, baseSpawns, baseColliderActivate, baseColliderDeactivate, baseColliderOps, 0.7f);

            _debugProfileResourcesNearScale = resourcesLayer.nearRadiusScale;
            _debugProfileDebrisNearScale = debrisLayer.nearRadiusScale;
        }

        private static BudgetProfile BuildDepthProfile(
            float baseUnload,
            float basePriority,
            int baseSpawns,
            float baseColliderActivate,
            float baseColliderDeactivate,
            int baseColliderOps,
            float depthScale)
        {
            return new BudgetProfile
            {
                scavengeUnloadDistance = baseUnload * depthScale,
                scavengePriorityRadius = basePriority * depthScale,
                scavengeSpawnsPerTick = Mathf.Max(6, Mathf.RoundToInt(baseSpawns * depthScale)),
                colliderActivateRadius = baseColliderActivate * depthScale,
                colliderDeactivateRadius = Mathf.Max(baseColliderActivate * depthScale + 4f, baseColliderDeactivate * depthScale),
                colliderOpsPerTick = Mathf.Max(12, Mathf.RoundToInt(baseColliderOps * depthScale))
            };
        }

        private void ClampProfiles()
        {
            midDepthStart = Mathf.Max(10f, midDepthStart);
            deepDepthStart = Mathf.Max(midDepthStart + 20f, deepDepthStart);

            surfaceProfile = ClampProfile(surfaceProfile);
            midDepthProfile = ClampProfile(midDepthProfile);
            deepProfile = ClampProfile(deepProfile);
        }

        private static BudgetProfile ClampProfile(BudgetProfile profile)
        {
            profile.scavengeUnloadDistance = Mathf.Max(50f, profile.scavengeUnloadDistance);
            profile.scavengePriorityRadius = Mathf.Max(10f, profile.scavengePriorityRadius);
            profile.scavengeSpawnsPerTick = Mathf.Max(1, profile.scavengeSpawnsPerTick);
            profile.colliderActivateRadius = Mathf.Max(4f, profile.colliderActivateRadius);
            profile.colliderDeactivateRadius = Mathf.Max(profile.colliderActivateRadius + 2f, profile.colliderDeactivateRadius);
            profile.colliderOpsPerTick = Mathf.Max(4, profile.colliderOpsPerTick);
            return profile;
        }

        private void UpdateDiagnostics()
        {
            _debugPlayerReady = playerTransform != null;
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugScavengeReady = scavengePopulator != null;
            _debugColliderReady = proximityColliderSystem != null;
            _debugDirectorScavengeRadiusScale = _directorScavengeRadiusScale;
            _debugDirectorSpawnScale = _directorSpawnScale;
            _debugDirectorColliderRadiusScale = _directorColliderRadiusScale;
            _debugDirectorColliderOpsScale = _directorColliderOpsScale;
            _debugInterestScavengeRadiusScale = _interestScavengeRadiusScale;
            _debugInterestSpawnScale = _interestSpawnScale;
            _debugInterestColliderRadiusScale = _interestColliderRadiusScale;
            _debugInterestColliderOpsScale = _interestColliderOpsScale;
            _debugZoneScavengeRadiusScale = _zoneScavengeRadiusScale;
            _debugZoneSpawnScale = _zoneSpawnScale;
            _debugZoneColliderRadiusScale = _zoneColliderRadiusScale;
            _debugZoneColliderOpsScale = _zoneColliderOpsScale;
            if (chunkStreamingProfile == null)
            {
                _debugProfileResourcesNearScale = 1f;
                _debugProfileDebrisNearScale = 1f;
            }
        }

        private string GetBlockerReason()
        {
            if (playerTransform == null)
                return "player-missing";

            if (mapMagicBridge == null)
                return "mapmagic-missing";

            if (!mapMagicBridge.IsAvailable)
                return "bridge-unavailable";

            if (scavengePopulator == null)
                return "scavenge-missing";

            if (proximityColliderSystem == null)
                return "collider-missing";

            return "depth-unresolved";
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            _cachedGlobalQualityWeight = Sanitize01(weight, _cachedGlobalQualityWeight);
            return _cachedGlobalQualityWeight;
        }

        private float ResolveDepthBandHysteresisMeters()
        {
            depthBandHysteresisMeters = Mathf.Max(MinimumDepthBandHysteresisMeters, depthBandHysteresisMeters);
            return depthBandHysteresisMeters;
        }

        private static float InvLerp(float min, float max, float value)
        {
            float span = max - min;
            if (span <= 0.0001f)
                return value >= max ? 1f : 0f;
            return Mathf.Clamp01((value - min) / span);
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - (2f * t));
        }

        private static float Sanitize01(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Mathf.Clamp01(fallback);

            return Mathf.Clamp01(value);
        }
    }
}
