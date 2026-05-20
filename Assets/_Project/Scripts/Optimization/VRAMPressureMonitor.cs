using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Applies graduated VRAM pressure responses against the MX350 hard ceiling.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8007)]
    public sealed class VRAMPressureMonitor : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float DefaultWarningVramFraction = 1600f / 1800f;
        private const float DefaultRestoreFraction = 1.40f / 1.80f;
        private const int DefaultSampleIntervalFrames = 90;
        private const float RamWarningFraction = 0.75f;
        private const float RamEmergencyFraction = 0.90f;
        private const float ForcedHalfResolutionVramFraction = 1400f / 1800f;
        private const float VRForcedHalfResolutionVramFraction = 1200f / 1800f;
        private const float VRRestoreFullResolutionVramFraction = 900f / 1800f;
        private const float LODAggressionVramFraction = 1700f / 1800f;
        private const float LODAggressionMultiplier = 0.5f;
        private const long MinimumHardwareHeadroomBytes = 200L * 1024L * 1024L;
        private const int WorldPrefabEvictionIdleFrames = 180;
        private const int SoftPressurePendingReleaseDrain = 4;
        private const uint ResolutionChangeSourceHash = 0x5652414Du; // VRAM

        [Header("VRAM Pressure Thresholds")]
        [Tooltip("Preventive mip downgrade fraction of the active runtime graphics budget. Default matches 1.6 GB against the 1.8 GB MX350 ceiling.")]
        [SerializeField, Range(0.5f, 1f)] private float warningVramFraction = DefaultWarningVramFraction;

        [Tooltip("Emergency eviction fraction of the active runtime graphics budget.")]
        [SerializeField, Range(0.5f, 1f)] private float emergencyVramFraction = 0.95f;

        [Tooltip("Recovery fraction that restores the baseline mip residency. XR uses a lower fixed fraction to prevent mip thrash.")]
        [SerializeField, Range(0.25f, 1f)] private float restoreVramFraction = DefaultRestoreFraction;

        [Tooltip("Frames between pressure samples. 90 frames is the streaming mandate cadence.")]
        [SerializeField, Range(1, 180)] private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [Tooltip("Maximum number of forced evictions performed in a single emergency pass.")]
        [SerializeField, Range(1, 8)] private int maxEmergencyEvictionsPerPass = 4;

        private bool _registeredTick;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private int _framesUntilSample;
        private int _baselineMipLimit;
        private int _activeMipLimit;
        private float _baselineLodBias;
        private bool _lodAggressionActive;
        private VRAMBudgetThresholds _runtimeBudgetThresholds;
        private long _runtimeTotalVramBudgetBytes;
        private VRAMMonitor _vramMonitor;
        private AssetLifecycleGovernor _assetLifecycle;
        private IPlayerInventoryService _playerInventory;
        private RenderTexturePool _renderTexturePool;

        internal static float BrgLodDistanceScalar { get; private set; } = 1f;

        internal bool HasSample { get; private set; }
        internal float VramPressureFactor { get; private set; }
        internal float RamPressureFactor { get; private set; }
        internal float PressureFactor { get; private set; }
        internal float LastStreamingMipBudgetMb { get; private set; }
        internal long LastUsedVramBytes { get; private set; }
        internal int EmergencyEvictionCount { get; private set; }

        private void Awake()
        {
            _runtimeBudgetThresholds = VRAMBudgetThresholds.RuntimeDefault;
            _runtimeTotalVramBudgetBytes = _runtimeBudgetThresholds.TotalVRAMBudgetBytes > 0L
                ? _runtimeBudgetThresholds.TotalVRAMBudgetBytes
                : VRAMBudgetThresholds.Default.TotalVRAMBudgetBytes;
            BrgLodDistanceScalar = 1f;
            _baselineMipLimit = QualitySettings.globalTextureMipmapLimit;
            _activeMipLimit = _baselineMipLimit;
            _baselineLodBias = QualitySettings.lodBias;
            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
        }

        private void OnEnable()
        {
            CacheDependencies();
            TryRegisterHotSwap();
            if (TryRegisterService())
                TryRegister();
        }

        private void Start()
        {
            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_lodAggressionActive)
                BrgLodDistanceScalar = 1f;

            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearCachedDependencies();
        }

        private void OnDestroy()
        {
            if (_lodAggressionActive)
                BrgLodDistanceScalar = 1f;

            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearCachedDependencies();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _framesUntilSample--;
            if (_framesUntilSample > 0)
                return;

            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
            SampleAndRespond();
        }

        internal void ForceImmediateSampleAndResponse()
        {
            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
            SampleAndRespond();
        }

        private void TryRegister()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.VRAMPressure, this))
                return;

            CacheDependencies();
            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            VRAMPressureMonitor registered = GlobalRegistry.VRAMPressure;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterVRAMPressureRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.VRAMPressure, this);
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterVRAMPressureRuntime(this);
            _registeredService = false;
        }

        private void SampleAndRespond()
        {
            VRAMMonitor monitor = _vramMonitor;
            if (monitor != null)
                monitor.SlowTick();

            AssetLifecycleGovernor governor = _assetLifecycle;
            long maxSystemRamBytes = (long)SystemInfo.systemMemorySize * 1024L * 1024L;
            long currentReservedBytes = Profiler.GetTotalReservedMemoryLong();
            if (governor != null)
                currentReservedBytes += governor.NativeHeapEstimateBytes;

            VRAMBudgetThresholds thresholds = _runtimeBudgetThresholds;
            long vramBudgetBytes = thresholds.TotalVRAMBudgetBytes;
            long usedVramBytes = monitor != null ? monitor.TotalVRAMBytes : 0L;
            LastUsedVramBytes = usedVramBytes;

            VramPressureFactor = vramBudgetBytes > 0L ? usedVramBytes / (float)vramBudgetBytes : 0f;
            RamPressureFactor = maxSystemRamBytes > 0L ? currentReservedBytes / (float)maxSystemRamBytes : 0f;
            PressureFactor = math.max(VramPressureFactor, RamPressureFactor);
            HasSample = true;

            ApplyStreamingMipBudget(monitor, thresholds);
            ApplyMipBias();
            ApplyLodAggression();
            RunPressureEviction(governor, monitor);
        }

        private void CacheDependencies()
        {
            if (_vramMonitor == null)
                _vramMonitor = GlobalRegistry.VRAMMonitor;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecycle;
            if (_playerInventory == null)
                _playerInventory = GlobalRegistry.PlayerInventory;
            if (_renderTexturePool == null)
                _renderTexturePool = GlobalRegistry.RenderTexturePool;
        }

        private void ClearCachedDependencies()
        {
            _vramMonitor = null;
            _assetLifecycle = null;
            _playerInventory = null;
            _renderTexturePool = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as VRAMMonitor;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as AssetLifecycleGovernor;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventory = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime:
                    _renderTexturePool = currentService as RenderTexturePool;
                    break;
            }
        }

        private void ApplyStreamingMipBudget(VRAMMonitor monitor, VRAMBudgetThresholds thresholds)
        {
            long nonTextureBytes = 0L;
            if (monitor != null)
            {
                nonTextureBytes = monitor.TotalVRAMBytes - monitor.TextureMemoryBytes;
                if (nonTextureBytes < 0L)
                    nonTextureBytes = 0L;
            }

            long streamingBudgetBytes = thresholds.TotalVRAMBudgetBytes - nonTextureBytes;
            if (streamingBudgetBytes < 128L * 1024L * 1024L)
                streamingBudgetBytes = 128L * 1024L * 1024L;

            LastStreamingMipBudgetMb = streamingBudgetBytes / BytesPerMegabyte;
            QualitySettings.streamingMipmapsMemoryBudget = LastStreamingMipBudgetMb;
        }

        private void ApplyMipBias()
        {
            int targetMipLimit = _activeMipLimit;
            bool softVramPressure = IsSoftVramPressureActive();
            long forcedMipThresholdBytes = ResolveForcedMipDropThresholdBytes();
            long restoreMipThresholdBytes = ResolveFullResolutionRestoreThresholdBytes();
            bool allowFractionRestore = !HectonXRRuntimeState.IsXRActive;

            if (LastUsedVramBytes >= ResolveRedZoneVramPressureThresholdBytes())
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (LastUsedVramBytes >= forcedMipThresholdBytes)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (LastUsedVramBytes <= restoreMipThresholdBytes)
                targetMipLimit = _baselineMipLimit;
            else if (softVramPressure)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (allowFractionRestore && VramPressureFactor <= ResolveRestoreVramFraction())
                targetMipLimit = _baselineMipLimit;

            if (targetMipLimit == _activeMipLimit)
                return;

            ApplyTextureMipLimit(targetMipLimit);
        }

        private void ApplyTextureMipLimit(int targetMipLimit)
        {
            int oldMipLimit = _activeMipLimit;
            QualitySettings.globalTextureMipmapLimit = targetMipLimit;
            _activeMipLimit = targetMipLimit;
            GlobalSignals.Publish(new ResolutionChangedSignal
            {
                Frame = unchecked((uint)Time.frameCount),
                SourceHash = ResolutionChangeSourceHash,
                OldMipLimit = oldMipLimit,
                NewMipLimit = targetMipLimit,
                VramUsedMb = LastUsedVramBytes / BytesPerMegabyte,
                Reason = targetMipLimit > oldMipLimit
                    ? ResolutionChangedSignal.ReasonVramRedline
                    : ResolutionChangedSignal.ReasonVramRecovered,
                Flags = ResolutionChangedSignal.FlagTextureMipLimit
            });
        }

        private void ApplyLodAggression()
        {
            float lodPressureResponse = ResolveLodAggressionResponse();
            bool redZonePressure = LastUsedVramBytes >= ResolveRedZoneVramPressureThresholdBytes();
            bool shouldCollapseLods = redZonePressure || lodPressureResponse > 0f;
            bool shouldRestoreLods = LastUsedVramBytes <= ResolveFullResolutionRestoreThresholdBytes() ||
                                     (!HectonXRRuntimeState.IsXRActive && VramPressureFactor <= ResolveRestoreVramFraction());

            if (shouldCollapseLods)
            {
                float lodScalar = math.lerp(1f, LODAggressionMultiplier, redZonePressure ? 1f : math.saturate(lodPressureResponse));
                float targetLodBias = Mathf.Max(0.05f, _baselineLodBias * lodScalar);
                if (!_lodAggressionActive || !Mathf.Approximately(QualitySettings.lodBias, targetLodBias))
                    QualitySettings.lodBias = targetLodBias;

                BrgLodDistanceScalar = lodScalar;
                _lodAggressionActive = true;
                return;
            }

            if (!_lodAggressionActive || !shouldRestoreLods)
                return;

            QualitySettings.lodBias = _baselineLodBias;
            BrgLodDistanceScalar = 1f;
            _lodAggressionActive = false;
        }

        private bool IsSoftVramPressureActive()
        {
            return ResolveSoftPressureResponse() > 0f;
        }

        private long ResolveForcedMipDropThresholdBytes()
        {
            return ResolveBudgetFractionBytes(HectonXRRuntimeState.IsXRActive
                ? ResolveQualityAdjustedFraction(
                    math.max(0.25f, VRForcedHalfResolutionVramFraction - 0.12f),
                    VRForcedHalfResolutionVramFraction)
                : ResolveQualityAdjustedFraction(
                    math.max(0.25f, ForcedHalfResolutionVramFraction - 0.12f),
                    ForcedHalfResolutionVramFraction));
        }

        private long ResolveFullResolutionRestoreThresholdBytes()
        {
            return ResolveBudgetFractionBytes(HectonXRRuntimeState.IsXRActive
                ? ResolveQualityAdjustedFraction(
                    math.max(0.25f, VRRestoreFullResolutionVramFraction - 0.10f),
                    VRRestoreFullResolutionVramFraction)
                : ResolveRestoreVramFraction());
        }

        private long ResolveRedZoneVramPressureThresholdBytes()
        {
            return _runtimeTotalVramBudgetBytes;
        }

        private long ResolveBudgetFractionBytes(float fraction)
        {
            float clampedFraction = Mathf.Clamp01(fraction);
            return (long)(_runtimeTotalVramBudgetBytes * clampedFraction);
        }

        private float ResolveSoftPressureResponse()
        {
            float vramResponse = ResolvePressureResponse(ResolveWarningVramFraction(), VramPressureFactor);
            float ramResponse = ResolvePressureResponse(ResolveRamWarningFraction(), RamPressureFactor);
            return math.saturate(math.max(vramResponse, ramResponse));
        }

        private float ResolveEmergencyPressureResponse()
        {
            float vramResponse = ResolvePressureResponse(ResolveEmergencyVramFraction(), VramPressureFactor);
            float ramResponse = ResolvePressureResponse(ResolveRamEmergencyFraction(), RamPressureFactor);
            return math.saturate(math.max(vramResponse, ramResponse));
        }

        private float ResolveLodAggressionResponse()
        {
            float fraction = ResolveQualityAdjustedFraction(0.75f, LODAggressionVramFraction);
            return ResolvePressureResponse(fraction, VramPressureFactor);
        }

        private float ResolveWarningVramFraction()
        {
            return ResolveQualityAdjustedFraction(math.max(0.5f, warningVramFraction - 0.18f), warningVramFraction);
        }

        private float ResolveEmergencyVramFraction()
        {
            return ResolveQualityAdjustedFraction(math.max(0.5f, emergencyVramFraction - 0.09f), emergencyVramFraction);
        }

        private float ResolveRestoreVramFraction()
        {
            return ResolveQualityAdjustedFraction(math.max(0.25f, restoreVramFraction - 0.12f), restoreVramFraction);
        }

        private static float ResolveRamWarningFraction()
        {
            return ResolveQualityAdjustedFraction(0.60f, RamWarningFraction);
        }

        private static float ResolveRamEmergencyFraction()
        {
            return ResolveQualityAdjustedFraction(0.82f, RamEmergencyFraction);
        }

        private static float ResolvePressureResponse(float startFraction, float pressureFactor)
        {
            float start = math.saturate(startFraction);
            float end = 1f;
            if (start >= end)
                start = end - 0.0001f;

            return math.smoothstep(start, end, math.saturate(pressureFactor));
        }

        private static float ResolveQualityAdjustedFraction(float lowQualityFraction, float highQualityFraction)
        {
            float quality = ResolveGlobalQualityWeight();
            float qualityCurve = math.smoothstep(0.15f, 0.85f, quality);
            return math.saturate(math.lerp(lowQualityFraction, highQualityFraction, qualityCurve));
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static int ResolveBudgetedPressureCount(int maxCount, float response)
        {
            int safeMax = math.max(1, maxCount);
            return math.max(1, (int)math.ceil(math.lerp(1f, safeMax, math.saturate(response))));
        }

        private void RunPressureEviction(AssetLifecycleGovernor governor, VRAMMonitor monitor)
        {
            EmergencyEvictionCount = 0;
            IPlayerInventoryService playerInventory = _playerInventory;
            ItemCatalog itemCatalog = playerInventory != null && playerInventory.Inventory != null
                ? playerInventory.Inventory.ItemCatalog
                : null;

            long hardwareHeadroomBytes = long.MaxValue;
            if (monitor != null)
            {
                hardwareHeadroomBytes = ((long)SystemInfo.graphicsMemorySize * 1024L * 1024L) - monitor.TotalVRAMBytes;
                if (hardwareHeadroomBytes < 0L)
                    hardwareHeadroomBytes = 0L;
            }

            bool redZoneVramPressure = LastUsedVramBytes >= ResolveRedZoneVramPressureThresholdBytes() || VramPressureFactor >= 1f;
            float emergencyPressureResponse = ResolveEmergencyPressureResponse();
            if (redZoneVramPressure || emergencyPressureResponse > 0f)
            {
                if (redZoneVramPressure)
                    SystemDispatcher.RequestVisualStaticGlitch();

                int emergencyEvictionBudget = redZoneVramPressure
                    ? maxEmergencyEvictionsPerPass
                    : ResolveBudgetedPressureCount(maxEmergencyEvictionsPerPass, emergencyPressureResponse);

                if (governor != null)
                {
                    governor.ForceDrainPendingReleaseQueue();
                    EmergencyEvictionCount = governor.EvictLowestPriorityUnusedAssets(
                        emergencyEvictionBudget,
                        AssetPriorityTier.Tier4MidRange);
                }

                if (redZoneVramPressure || (monitor != null && monitor.RenderTextureBudgetUtilization >= 1f))
                {
                    RenderTexturePool pool = _renderTexturePool;
                    if (pool != null)
                        pool.ClearAllPools();
                }

                if (itemCatalog != null && hardwareHeadroomBytes < MinimumHardwareHeadroomBytes)
                {
                    EmergencyEvictionCount += itemCatalog.EvictLeastRecentlyUsedWorldPrefabs(
                        emergencyEvictionBudget,
                        WorldPrefabEvictionIdleFrames);
                }

                return;
            }

            float softPressureResponse = ResolveSoftPressureResponse();
            if (softPressureResponse > 0f)
            {
                int softReleaseDrain = ResolveBudgetedPressureCount(SoftPressurePendingReleaseDrain, softPressureResponse);
                int softEvictionBudget = ResolveBudgetedPressureCount(2, softPressureResponse);
                if (governor != null)
                {
                    governor.DrainPendingReleaseQueueBudgeted(softReleaseDrain);
                    governor.EvictLowestPriorityUnusedAssets(softEvictionBudget, AssetPriorityTier.Tier5DistantHlod);
                }

                if (itemCatalog != null && hardwareHeadroomBytes < MinimumHardwareHeadroomBytes)
                    itemCatalog.EvictLeastRecentlyUsedWorldPrefabs(softEvictionBudget, WorldPrefabEvictionIdleFrames);
            }
        }
    }
}
