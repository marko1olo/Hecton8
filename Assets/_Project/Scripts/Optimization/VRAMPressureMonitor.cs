using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Applies graduated VRAM pressure responses against the active runtime graphics budget.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8007)]
    public sealed class VRAMPressureMonitor : MonoBehaviour, ISlowTickable, IVramPressureReadModel, IVramPressureSampleSink, IVramPressureMipBiasSink, IGlobalRegistryHotSwapListener
    {
        private static int s_x001VRAMPressureMonitorSignalPushDropCount;
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
        [Tooltip("Preventive mip downgrade fraction of the active runtime graphics budget. Default is the compact-budget warning fraction.")]
        [SerializeField, Range(0.5f, 1f)] private float warningVramFraction = DefaultWarningVramFraction;

        [Tooltip("Emergency eviction fraction of the active runtime graphics budget.")]
        [SerializeField, Range(0.5f, 1f)] private float emergencyVramFraction = 0.95f;

        [Tooltip("Recovery fraction that restores the baseline mip residency. XR uses a lower fixed fraction to prevent mip thrash.")]
        [SerializeField, Range(0.25f, 1f)] private float restoreVramFraction = DefaultRestoreFraction;

        [Tooltip("Frames between pressure samples. 90 frames is the streaming mandate cadence.")]
        [SerializeField, Range(1, 180)] private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [Tooltip("Maximum number of forced evictions performed in a single emergency pass.")]
        [SerializeField, Range(1, 8)] private int maxEmergencyEvictionsPerPass = 4;

        private bool _registeredSlowTick;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _runtimeOwnerAborted;
        private bool _forceSampleQueued;
        private int _nextSampleFrame;
        private int _baselineMipLimit;
        private int _activeMipLimit;
        private float _externalMipPressureResponse;
        private float _baselineLodBias;
        private bool _lodAggressionActive;
        private VRAMBudgetThresholds _runtimeBudgetThresholds;
        private long _runtimeTotalVramBudgetBytes;
        private long _runtimeSystemRamBytes;
        private IVramBudgetReadModel _vramMonitor;
        private IVramBudgetSampleSink _vramBudgetSample;
        private IAssetLifecyclePressureSink _assetLifecycle;
        private IPlayerInventoryService _playerInventory;
        private IRenderTexturePoolService _renderTexturePool;

        internal static float BrgLodDistanceScalar { get; private set; } = 1f;

        internal bool HasSample { get; private set; }
        internal float VramPressureFactor { get; private set; }
        internal float RamPressureFactor { get; private set; }
        internal float PressureFactor { get; private set; }
        internal float LastStreamingMipBudgetMb { get; private set; }
        internal long LastUsedVramBytes { get; private set; }
        internal int EmergencyEvictionCount { get; private set; }

        bool IVramPressureReadModel.HasSample => HasSample;
        float IVramPressureReadModel.VramPressureFactor => VramPressureFactor;
        float IVramPressureReadModel.RamPressureFactor => RamPressureFactor;
        float IVramPressureReadModel.PressureFactor => PressureFactor;
        float IVramPressureReadModel.BrgLodDistanceScalar => BrgLodDistanceScalar;

        void IVramPressureSampleSink.ForceImmediateSampleAndResponse()
        {
            ForceImmediateSampleAndResponse();
        }

        void IVramPressureMipBiasSink.SetExternalMipPressureResponse(float pressureResponse, long observedVramBytes)
        {
            SetExternalMipPressureResponse(pressureResponse, observedVramBytes);
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            _runtimeBudgetThresholds = VRAMBudgetThresholds.RuntimeDefault;
            _runtimeTotalVramBudgetBytes = _runtimeBudgetThresholds.TotalVRAMBudgetBytes > 0L
                ? _runtimeBudgetThresholds.TotalVRAMBudgetBytes
                : VRAMBudgetThresholds.Default.TotalVRAMBudgetBytes;
            BrgLodDistanceScalar = 1f;
            _baselineMipLimit = QualitySettings.globalTextureMipmapLimit;
            _activeMipLimit = _baselineMipLimit;
            _baselineLodBias = QualitySettings.lodBias;
            _nextSampleFrame = ResolveSampleFrame(Hecton8.Core.SystemDispatcher.CurrentFrameId, sampleIntervalFrames);
            _runtimeSystemRamBytes = ResolveSystemMemoryBytesCold();
        }

        private static long ResolveSystemMemoryBytesCold()
        {
            long memoryMb = SystemInfo.systemMemorySize;
            return memoryMb > 0L ? memoryMb * 1024L * 1024L : 0L;
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!TryRegisterService())
                return;

            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void Start()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_registeredService && !TryRegisterService())
                return;

            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
            {
                ClearCachedDependencies();
                return;
            }

            RestoreGlobalQualityOverrides();
            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearCachedDependencies();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                ClearCachedDependencies();
                return;
            }

            RestoreGlobalQualityOverrides();
            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearCachedDependencies();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            int currentFrame = ResolveCurrentFrame();
            if (!_forceSampleQueued && currentFrame < _nextSampleFrame)
                return;

            _forceSampleQueued = false;
            _nextSampleFrame = ResolveSampleFrame(currentFrame, sampleIntervalFrames);
            SampleAndRespond();
        }

        internal void ForceImmediateSampleAndResponse()
        {
            if (!Application.isPlaying)
            {
                _forceSampleQueued = true;
                return;
            }

            CacheDependencies();
            _forceSampleQueued = false;
            _nextSampleFrame = ResolveSampleFrame(ResolveCurrentFrame(), sampleIntervalFrames);
            SampleAndRespond();
        }

        private static int ResolveCurrentFrame()
        {
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            return frame <= int.MaxValue ? (int)frame : int.MaxValue;
        }

        private static int ResolveSampleFrame(uint currentFrame, int intervalFrames)
        {
            int clampedFrame = currentFrame <= int.MaxValue ? (int)currentFrame : int.MaxValue;
            return ResolveSampleFrame(clampedFrame, intervalFrames);
        }

        private static int ResolveSampleFrame(int currentFrame, int intervalFrames)
        {
            int interval = Mathf.Max(1, intervalFrames);
            return currentFrame <= int.MaxValue - interval ? currentFrame + interval : int.MaxValue;
        }

        private void TryRegister()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.VRAMPressure, this))
                return;

            CacheDependencies();
            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterVRAMPressureRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.VRAMPressure, this);
            return _registeredService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            VRAMPressureMonitor registered = GlobalRegistry.VRAMPressure;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsVRAMPressureRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterVRAMPressureRuntime(registered);
            return false;
        }

        private static bool IsVRAMPressureRuntimeUsable(VRAMPressureMonitor monitor)
        {
            return monitor != null &&
                   monitor._registeredService &&
                   monitor.isActiveAndEnabled;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
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
            IVramBudgetReadModel monitor = _vramMonitor;
            IVramBudgetSampleSink budgetSample = _vramBudgetSample;
            if (budgetSample != null)
                budgetSample.SampleVramCounters();

            IAssetLifecyclePressureSink governor = _assetLifecycle;
            long maxSystemRamBytes = _runtimeSystemRamBytes;
            long currentReservedBytes = Profiler.GetTotalReservedMemoryLong();
            if (governor != null)
                currentReservedBytes += governor.NativeHeapEstimateBytes;

            VRAMBudgetThresholds thresholds = _runtimeBudgetThresholds;
            long vramBudgetBytes = thresholds.TotalVRAMBudgetBytes;
            long usedVramBytes = monitor != null ? monitor.TotalVRAMBytes : LastUsedVramBytes;
            LastUsedVramBytes = usedVramBytes;

            float vramDenominator = math.max((float)vramBudgetBytes, 1f);
            float ramDenominator = math.max((float)maxSystemRamBytes, 1f);
            VramPressureFactor = math.select(0f, math.saturate(usedVramBytes / vramDenominator), vramBudgetBytes > 0L);
            RamPressureFactor = math.select(0f, math.saturate(currentReservedBytes / ramDenominator), maxSystemRamBytes > 0L);
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
                _vramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            if (_vramBudgetSample == null)
                _vramBudgetSample = GlobalRegistry.VRAMBudgetSampleSink;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecyclePressureSink;
            if (_playerInventory == null)
                _playerInventory = GlobalRegistry.PlayerInventory;
            if (_renderTexturePool == null)
                _renderTexturePool = GlobalRegistry.RenderTexturePoolService;
        }

        private void ClearCachedDependencies()
        {
            _vramMonitor = null;
            _vramBudgetSample = null;
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
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled && !_runtimeOwnerAborted)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    _vramBudgetSample = currentService as IVramBudgetSampleSink;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as IAssetLifecyclePressureSink;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventory = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime:
                    _renderTexturePool = currentService as IRenderTexturePoolService;
                    break;
            }
        }

        private void ApplyStreamingMipBudget(IVramBudgetReadModel monitor, VRAMBudgetThresholds thresholds)
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
            int effectiveBaselineMipLimit = ResolveEffectiveBaselineMipLimit();
            int targetMipLimit = Mathf.Max(_activeMipLimit, effectiveBaselineMipLimit);
            bool redZonePressure = LastUsedVramBytes >= ResolveRedZoneVramPressureThresholdBytes();
            float softPressureResponse = ResolveSoftPressureResponse();
            float forcedMipResponse = ResolveForcedMipResponse();
            float mipPressureResponse = math.select(
                math.saturate(math.max(math.max(softPressureResponse, forcedMipResponse), _externalMipPressureResponse)),
                1f,
                redZonePressure);
            long restoreMipThresholdBytes = ResolveFullResolutionRestoreThresholdBytes();
            bool allowFractionRestore = !HectonXRRuntimeState.IsXRActive;

            if (mipPressureResponse > 0f)
            {
                int mipDelta = ResolveMipLimitDelta(mipPressureResponse, redZonePressure);
                if (mipDelta > 0)
                    targetMipLimit = Mathf.Max(effectiveBaselineMipLimit, effectiveBaselineMipLimit + mipDelta);
            }
            else if (LastUsedVramBytes <= restoreMipThresholdBytes)
                targetMipLimit = effectiveBaselineMipLimit;
            else if (allowFractionRestore && VramPressureFactor <= ResolveRestoreVramFraction())
                targetMipLimit = effectiveBaselineMipLimit;

            if (targetMipLimit == _activeMipLimit)
                return;

            ApplyTextureMipLimit(targetMipLimit);
        }

        internal void SetExternalMipPressureResponse(float pressureResponse, long observedVramBytes)
        {
            _externalMipPressureResponse = math.saturate(math.select(0f, pressureResponse, math.isfinite(pressureResponse)));
            if (observedVramBytes >= 0L)
            {
                LastUsedVramBytes = observedVramBytes;
                float vramDenominator = math.max((float)_runtimeTotalVramBudgetBytes, 1f);
                float externalVramPressure = math.saturate(observedVramBytes / vramDenominator);
                VramPressureFactor = math.select(VramPressureFactor, externalVramPressure, _runtimeTotalVramBudgetBytes > 0L);
                PressureFactor = math.saturate(math.max(VramPressureFactor, RamPressureFactor));
            }

            _forceSampleQueued = true;
        }

        private void ApplyTextureMipLimit(int targetMipLimit)
        {
            targetMipLimit = Mathf.Max(targetMipLimit, ResolveEffectiveBaselineMipLimit());
            int oldMipLimit = _activeMipLimit;
            QualitySettings.globalTextureMipmapLimit = targetMipLimit;
            _activeMipLimit = targetMipLimit;
            SignalBus<ResolutionChangedSignal>.TryPushTracked(new ResolutionChangedSignal
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SourceHash = ResolutionChangeSourceHash,
                OldMipLimit = oldMipLimit,
                NewMipLimit = targetMipLimit,
                VramUsedMb = LastUsedVramBytes / BytesPerMegabyte,
                Reason = targetMipLimit > oldMipLimit
                    ? ResolutionChangedSignal.ReasonVramRedline
                    : ResolutionChangedSignal.ReasonVramRecovered,
                Flags = ResolutionChangedSignal.FlagTextureMipLimit
            }, ref s_x001VRAMPressureMonitorSignalPushDropCount);
        }

        private void RestoreGlobalQualityOverrides()
        {
            int effectiveBaselineMipLimit = ResolveEffectiveBaselineMipLimit();
            if (_activeMipLimit != effectiveBaselineMipLimit)
            {
                QualitySettings.globalTextureMipmapLimit = effectiveBaselineMipLimit;
                _activeMipLimit = effectiveBaselineMipLimit;
            }

            if (_lodAggressionActive)
            {
                QualitySettings.lodBias = _baselineLodBias;
                _lodAggressionActive = false;
            }

            BrgLodDistanceScalar = 1f;
        }

        private int ResolveEffectiveBaselineMipLimit()
        {
            return Mathf.Max(_baselineMipLimit, VRAMEnforcer.RuntimeTextureMipLimitFloor);
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
                float lodScalar = math.lerp(1f, LODAggressionMultiplier, math.select(math.saturate(lodPressureResponse), 1f, redZonePressure));
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

        private float ResolveForcedMipResponse()
        {
            float forcedFraction = HectonXRRuntimeState.IsXRActive
                ? ResolveQualityAdjustedFraction(
                    math.max(0.25f, VRForcedHalfResolutionVramFraction - 0.12f),
                    VRForcedHalfResolutionVramFraction)
                : ResolveQualityAdjustedFraction(
                    math.max(0.25f, ForcedHalfResolutionVramFraction - 0.12f),
                    ForcedHalfResolutionVramFraction);
            return ResolvePressureResponse(forcedFraction, VramPressureFactor);
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

        private static int ResolveMipLimitDelta(float mipPressureResponse, bool redZonePressure)
        {
            int pressureDelta = math.clamp((int)math.round(math.lerp(0f, 2f, math.saturate(mipPressureResponse))), 0, 2);
            return math.select(pressureDelta, math.max(pressureDelta, 2), redZonePressure);
        }

        public static int ResolveMipLimitDeltaForAudit(long usedVramBytes, long budgetBytes, float globalQualityWeight, bool xrActive)
        {
            float pressure = budgetBytes > 0L
                ? math.saturate(usedVramBytes / math.max((float)budgetBytes, 1f))
                : 0f;
            float quality = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float softWarningFraction = ResolveQualityAdjustedFractionExplicit(
                math.max(0.5f, DefaultWarningVramFraction - 0.18f),
                DefaultWarningVramFraction,
                quality);
            float forcedFraction = xrActive
                ? ResolveQualityAdjustedFractionExplicit(
                    math.max(0.25f, VRForcedHalfResolutionVramFraction - 0.12f),
                    VRForcedHalfResolutionVramFraction,
                    quality)
                : ResolveQualityAdjustedFractionExplicit(
                    math.max(0.25f, ForcedHalfResolutionVramFraction - 0.12f),
                    ForcedHalfResolutionVramFraction,
                    quality);
            float response = math.saturate(math.max(
                ResolvePressureResponse(softWarningFraction, pressure),
                ResolvePressureResponse(forcedFraction, pressure)));
            bool redZonePressure = budgetBytes > 0L && usedVramBytes >= budgetBytes;
            return ResolveMipLimitDelta(response, redZonePressure);
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
            float start = math.min(math.saturate(startFraction), 0.9999f);
            float end = 1f;
            return math.smoothstep(start, end, math.saturate(pressureFactor));
        }

        private static float ResolveQualityAdjustedFraction(float lowQualityFraction, float highQualityFraction)
        {
            float quality = ResolveGlobalQualityWeight();
            return ResolveQualityAdjustedFractionExplicit(lowQualityFraction, highQualityFraction, quality);
        }

        private static float ResolveQualityAdjustedFractionExplicit(float lowQualityFraction, float highQualityFraction, float quality)
        {
            float qualityCurve = math.smoothstep(0.15f, 0.85f, math.saturate(quality));
            return math.saturate(math.lerp(lowQualityFraction, highQualityFraction, qualityCurve));
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static int ResolveBudgetedPressureCount(int maxCount, float response)
        {
            int safeMax = math.max(1, maxCount);
            return math.max(1, (int)math.ceil(math.lerp(1f, safeMax, math.saturate(response))));
        }

        private void RunPressureEviction(IAssetLifecyclePressureSink governor, IVramBudgetReadModel monitor)
        {
            EmergencyEvictionCount = 0;
            IPlayerInventoryService playerInventory = _playerInventory;
            ItemCatalog itemCatalog = playerInventory != null && playerInventory.Inventory != null
                ? playerInventory.Inventory.ItemCatalog
                : null;

            long hardwareHeadroomBytes = long.MaxValue;
            if (monitor != null)
            {
                hardwareHeadroomBytes = _runtimeTotalVramBudgetBytes - monitor.TotalVRAMBytes;
                if (hardwareHeadroomBytes < 0L)
                    hardwareHeadroomBytes = 0L;
            }

            bool redZoneVramPressure = LastUsedVramBytes >= ResolveRedZoneVramPressureThresholdBytes() || VramPressureFactor >= 1f;
            float emergencyPressureResponse = ResolveEmergencyPressureResponse();
            if (redZoneVramPressure || emergencyPressureResponse > 0f)
            {
                if (redZoneVramPressure)
                    SystemDispatcher.RequestVisualStaticGlitch();

                int emergencyEvictionBudget = math.select(
                    ResolveBudgetedPressureCount(maxEmergencyEvictionsPerPass, emergencyPressureResponse),
                    maxEmergencyEvictionsPerPass,
                    redZoneVramPressure);

                if (governor != null)
                {
                    governor.DrainPendingReleaseQueueBudgeted(emergencyEvictionBudget);
                    EmergencyEvictionCount = governor.EvictLowestPriorityUnusedAssets(
                        emergencyEvictionBudget,
                        AssetPriorityTierCodes.Tier4MidRange);
                }

                if (redZoneVramPressure || (monitor != null && monitor.RenderTextureBudgetUtilization >= 1f))
                {
                    IRenderTexturePoolService pool = _renderTexturePool;
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
                    governor.EvictLowestPriorityUnusedAssets(softEvictionBudget, AssetPriorityTierCodes.Tier5DistantHlod);
                }

                if (itemCatalog != null && hardwareHeadroomBytes < MinimumHardwareHeadroomBytes)
                    itemCatalog.EvictLeastRecentlyUsedWorldPrefabs(softEvictionBudget, WorldPrefabEvictionIdleFrames);
            }
        }
    }
}
