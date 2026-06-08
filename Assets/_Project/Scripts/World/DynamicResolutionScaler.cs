// ============================================================================
// HECTON-8 — DynamicResolutionScaler.cs
// Adjusts render resolution dynamically to maintain target frame rate.
//
// RESPONSIBILITIES:
//   • Monitor frame time (target: 16.67ms for 60 FPS)
//   • Reduce render scale after consecutive slow frames
//   • Increase render scale after consecutive fast frames
//   • Clamp render scale between min/max limits
//   • Apply quality preset-based minimum scale
//
// ARCHITECTURE:
//   • GlobalRegistry.DynamicResolution is the authoritative runtime lookup.
//   • ILateFrameTickable - applies render-scale policy in VISUAL_SYNC
//   • Zero-GC — no allocations in hot paths
//
// PERFORMANCE:
//   • Target: < 0.1ms per frame
//   • Zero GC allocations
//   • Smooth scale adjustments
//
// INTEGRATION:
//   • SystemDispatcher - late-frame presentation registration
//   • LODSystemManager — quality preset integration
//   • UniversalRenderPipeline.asset — render scale application
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.SaveSystem;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Adjusts render resolution dynamically to maintain target frame rate.
    /// Reduces scale on slow frames, increases on fast frames.
    /// </summary>
    /// <remarks>
    /// ZERO-GC ARCHITECTURE:
    ///   • No allocations in Tick
    ///   • Simple frame time monitoring
    ///   • Smooth scale adjustments
    ///
    /// PERFORMANCE TARGET:
    ///   • Processing: < 0.1ms per frame
    ///   • Target frame time: 16.67ms (60 FPS)
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)] // Run after CullingManager
    public sealed class DynamicResolutionScaler : MonoBehaviour, ILateFrameTickable, ISaveable, IDynamicResolutionRuntime, IGlobalRegistryHotSwapListener
    {
        private const string StablePressureStateLabel = "Stable";
        private const string RecoveringPressureStateLabel = "Recovering";
        private const string PressuredPressureStateLabel = "Pressured";
        private const string CriticalPressureStateLabel = "Critical";
        private const float SystemOverrideMinimumRenderScale = 0.25f;
        private const byte SystemOverrideThermalFlag = 1 << 0;
        private const float DefaultTargetFrameTimeMs = 16.67f;
        private const float DefaultCriticalFrameTimeMs = 25f;
        private const float DefaultFrameTimeSmoothing = 0.18f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Singleton instance. Null if not initialized.
        /// </summary>
        private static DynamicResolutionScaler s_activeRuntime;

        public static DynamicResolutionScaler Instance => s_activeRuntime;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Dynamic Resolution ──────────────────")]
        [SerializeField, Tooltip("Target frame time (milliseconds)")]
        private float _targetFrameTime = DefaultTargetFrameTimeMs; // 60 FPS

        [SerializeField, Tooltip("Emergency frame time threshold. Crossing it forces a more aggressive render-scale drop.")]
        private float _criticalFrameTime = DefaultCriticalFrameTimeMs;

        [SerializeField, Tooltip("Min render scale")]
        private float _minRenderScale = 0.5f;

        private float _qualityPresetMinimumRenderScale = 0.8f;

        [SerializeField, Tooltip("Max render scale")]
        private float _maxRenderScale = 1.0f;

        [SerializeField, Tooltip("Scale reduction percentage per adjustment")]
        private float _scaleReductionPercent = 5f;

        [SerializeField, Tooltip("Scale increase percentage per adjustment")]
        private float _scaleIncreasePercent = 2f;

        [SerializeField, Tooltip("Consecutive slow frames before scale reduction")]
        private int _slowFrameThreshold = 3;

        [SerializeField, Tooltip("Consecutive fast frames before scale increase")]
        private int _fastFrameThreshold = 30;

        [SerializeField, Tooltip("How many frames scale recovery stays locked after a reduction to prevent oscillation.")]
        private int _recoveryHoldFrames = 45;

        [SerializeField, Tooltip("Smoothing factor for frame-time trend tracking. Higher values react faster but oscillate more.")]
        [Range(0.01f, 1f)]
        private float _frameTimeSmoothing = DefaultFrameTimeSmoothing;

        [SerializeField, Tooltip("Extra reduction percentage applied when the frame blows past the emergency threshold.")]
        private float _criticalScaleReductionPercent = 10f;

        [SerializeField, Tooltip("Enable dynamic resolution scaling")]
        private bool _enabled = true;

        [SerializeField, Min(0f), Tooltip("Startup grace window before dynamic resolution is allowed to reduce render scale. Prevents first-frame spikes from blurring the scene immediately on Play.")]
        private float _startupGraceSeconds = 3f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _currentRenderScale = 1.0f;
        private float _defaultRenderScale = 1.0f;
        private int _consecutiveSlowFrames = 0;
        private int _consecutiveFastFrames = 0;
        private int _recoveryHoldFramesRemaining = 0;
        private float _startupGraceRemainingSeconds;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _saveRegistered;
        private bool _hotSwapRegistered;
        private bool _renderScaleApplyQueued;
        private bool _applyingRenderScaleLateFrame;
        private bool _runtimeRenderScaleQueueActive;
        private LODQualityPreset _qualityPreset = LODQualityPreset.Medium;
        private float _smoothedFrameTimeMs;
        private float _peakFrameTimeMs;
        private RenderPressureState _pressureState = RenderPressureState.Stable;
        private bool _platformPressureRenderScaleActive;
        private float _platformPressureMinimumRenderScale = 0.7f;
        private float _targetRenderScale = 1.0f;
        private float _systemOverrideFrameTimeEwmaMs = DefaultTargetFrameTimeMs;
        private byte _systemOverridePressureLevel;
        private byte _systemOverrideFlags;
        private uint _snapshotSequence;
        private bool _systemOverrideActive;
        private bool _thermalOverrideActive;
        private DynamicResolutionRuntimeSnapshot _snapshot;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;

        private UniversalRenderPipelineAsset _urpAsset;

        [Header("Diagnostics")]
        [SerializeField, Tooltip("Smoothed frame time trend used by the scaler.")]
        private float _debugSmoothedFrameTimeMs;
        [SerializeField, Tooltip("Peak frame time seen since the last scale adjustment.")]
        private float _debugPeakFrameTimeMs;
        [SerializeField, Tooltip("Current internal render-pressure state.")]
        private string _debugPressureState = "Stable";
        [SerializeField, Tooltip("Remaining recovery-lock frames before upscale is allowed again.")]
        private int _debugRecoveryHoldFramesRemaining;
        [SerializeField, Tooltip("Development-only forced frame time override used by the runtime debug harness.")]
        private bool _debugFrameTimeOverrideActive;
        [SerializeField, Tooltip("Development-only forced frame time override value in milliseconds.")]
        private float _debugFrameTimeOverrideMs;
        [SerializeField, Tooltip("Development-only direct render-scale override used by the runtime debug harness.")]
        private bool _debugRenderScaleOverrideActive;
        [SerializeField, Tooltip("Development-only direct render-scale override value.")]
        private float _debugRenderScaleOverrideValue;
        [SerializeField, Tooltip("Platform pressure has lowered the render-scale floor.")]
        private bool _debugPlatformPressureRenderScaleActive;
        [SerializeField, Tooltip("Effective render-scale floor after quality preset and platform pressure.")]
        private float _debugEffectiveMinimumRenderScale;

        private enum RenderPressureState
        {
            Stable = 0,
            Recovering = 1,
            Pressured = 2,
            Critical = 3
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current render scale (0.5 to 1.0).
        /// </summary>
        public float CurrentRenderScale => _currentRenderScale;

        public float CurrentRenderScale01 => _currentRenderScale;

        public float TargetRenderScale01 => _targetRenderScale;

        public bool IsSystemOverrideActive => _systemOverrideActive;

        public bool IsThermalOverrideActive => _thermalOverrideActive;

        /// <summary>
        /// Whether dynamic resolution scaling is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// True when platform policy has lowered the minimum render scale for pressure recovery.
        /// </summary>
        public bool PlatformPressureRenderScaleActive => _platformPressureRenderScaleActive;

        /// <summary>
        /// Effective minimum render scale after quality preset and platform pressure.
        /// </summary>
        public float EffectiveMinimumRenderScale => _minRenderScale;

        internal string DebugPressureStateLabel => _debugPressureState;

        internal float DebugSmoothedFrameTimeMs => _debugSmoothedFrameTimeMs;

        internal bool DebugFrameTimeOverrideActive => _debugFrameTimeOverrideActive;

        internal float DebugFrameTimeOverrideMs => _debugFrameTimeOverrideMs;

        internal bool DebugRenderScaleOverrideActive => _debugRenderScaleOverrideActive;

        internal float DebugRenderScaleOverrideValue => _debugRenderScaleOverrideValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            _runtimeRenderScaleQueueActive = Application.isPlaying;

            // Cache URP asset
            _urpAsset = UniversalRenderPipeline.asset;
            if (_urpAsset == null)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
                #endif
                _enabled = false;
            }

            // Initialize render scale
            if (_urpAsset != null)
            {
                _defaultRenderScale = _urpAsset.renderScale;
            }

            _qualityPresetMinimumRenderScale = ResolveMinimumRenderScaleFromPreset(_qualityPreset);
            RefreshMinimumRenderScale();
            _currentRenderScale = Mathf.Clamp(ResolveDefaultRenderScale(), _minRenderScale, ResolveMaxRenderScale());
            _targetRenderScale = _currentRenderScale;
            _startupGraceRemainingSeconds = ResolveStartupGraceSeconds();
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            _smoothedFrameTimeMs = targetFrameTimeMs;
            _peakFrameTimeMs = targetFrameTimeMs;
            UpdatePressureDiagnostics();
            if (_urpAsset != null)
            {
                ApplyRenderScale();
            }

            CacheRegistryServicesCold();
            TryRegisterSaveParticipant();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[DynamicResolutionScaler] Initialized.");
            #endif
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            _runtimeRenderScaleQueueActive = Application.isPlaying;
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegisterSaveParticipant();
            TryRegister();
        }

        private void OnDisable()
        {
            _runtimeRenderScaleQueueActive = false;
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();

            Dispose();

        }

        public void Dispose()
        {
            RestoreDefaultRenderScale();
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterDynamicResolutionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.DynamicResolution, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.DynamicResolution, this))
                GlobalRegistry.UnregisterDynamicResolutionRuntime(this);

            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            DynamicResolutionScaler registered = GlobalRegistry.DynamicResolution;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsDynamicResolutionRuntimeUsable(registered))
                {
                    s_activeRuntime = registered;
                    Destroy(gameObject);
                    return true;
                }

                GlobalRegistry.UnregisterDynamicResolutionRuntime(registered);
                if (ReferenceEquals(s_activeRuntime, registered))
                    s_activeRuntime = null;
            }

            DynamicResolutionScaler active = s_activeRuntime;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsDynamicResolutionRuntimeUsable(active))
            {
                GlobalRegistry.RegisterDynamicResolutionRuntime(active);
                s_activeRuntime = active;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterDynamicResolutionRuntime(active);
            if (ReferenceEquals(s_activeRuntime, active))
                s_activeRuntime = null;

            return false;
        }

        private static bool IsDynamicResolutionRuntimeUsable(DynamicResolutionScaler scaler)
        {
            return scaler != null && scaler._serviceRegistered && scaler.isActiveAndEnabled;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveService = null;
            _saveRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        private void CacheRegistryServicesCold()
        {
            if (!IsSaveServiceUsable(_saveService))
                _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            if (isActiveAndEnabled)
                TryRegisterSaveParticipant();
        }

        //  ISAVEABLE IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Save priority (Core system).
        /// </summary>
        public int SavePriority => 6;

        /// <summary>
        /// Load priority (Core system).
        /// </summary>
        public int LoadPriority => 6;

        /// <summary>
        /// Save dynamic resolution settings to SaveData.
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            data.DynamicResolutionEnabled = _enabled;
        }

        /// <summary>
        /// Load dynamic resolution settings from SaveData.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            _enabled = data.DynamicResolutionEnabled;

            if (!_enabled && _urpAsset != null)
            {
                _currentRenderScale = ResolveDefaultRenderScale();
                _targetRenderScale = _currentRenderScale;
                ApplyRenderScale();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITICKABLE IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Monitor frame time and adjust render scale.
        /// </summary>
        /// <param name="dt">Delta time from GameTickManager</param>
        private void AdvanceDynamicResolutionState(float dt)
        {
            if (_urpAsset == null)
                return;

            if (!_enabled && !_debugRenderScaleOverrideActive)
                return;

            if (_systemOverrideActive && !_debugRenderScaleOverrideActive)
            {
                UpdatePressureDiagnostics();
                return;
            }

            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            float criticalFrameTimeMs = ResolveCriticalFrameTimeMs(targetFrameTimeMs);
            float observedFrameTime = ResolveObservedFrameTime(dt);
            UpdateFrameTrend(observedFrameTime);

            if (_debugRenderScaleOverrideActive)
            {
                float overrideScale = Mathf.Clamp(_debugRenderScaleOverrideValue, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale());
                if (!Mathf.Approximately(_currentRenderScale, overrideScale))
                {
                    _currentRenderScale = overrideScale;
                    _targetRenderScale = overrideScale;
                    ApplyRenderScale();
                }

                float debugEffectiveFrameTime = Mathf.Max(observedFrameTime, _smoothedFrameTimeMs);
                UpdatePressureState(debugEffectiveFrameTime);
                UpdatePressureDiagnostics();
                return;
            }

            if (HandleStartupGrace(dt, observedFrameTime))
                return;

            float effectiveFrameTime = Mathf.Max(observedFrameTime, _smoothedFrameTimeMs);
            bool criticalPressure = effectiveFrameTime >= criticalFrameTimeMs;
            bool pressured = effectiveFrameTime > targetFrameTimeMs;
            bool fastEnoughForRecovery = effectiveFrameTime < targetFrameTimeMs * 0.9f;

            if (_recoveryHoldFramesRemaining > 0)
                _recoveryHoldFramesRemaining--;

            if (criticalPressure)
            {
                _consecutiveSlowFrames++;
                _consecutiveFastFrames = 0;

                if (_consecutiveSlowFrames >= 1)
                {
                    ApplyScaleReduction(_criticalScaleReductionPercent);
                    _consecutiveSlowFrames = 0;
                }
            }
            else if (pressured)
            {
                _consecutiveSlowFrames++;
                _consecutiveFastFrames = 0;

                if (_consecutiveSlowFrames >= _slowFrameThreshold)
                {
                    float pressureRange = Mathf.Max(0.01f, criticalFrameTimeMs - targetFrameTimeMs);
                    float pressureLerp = Mathf.Clamp01((effectiveFrameTime - targetFrameTimeMs) * math.rcp(pressureRange));
                    float adaptiveReductionPercent = _scaleReductionPercent +
                        (_criticalScaleReductionPercent - _scaleReductionPercent) * pressureLerp;
                    ApplyScaleReduction(adaptiveReductionPercent);
                    _consecutiveSlowFrames = 0;
                }
            }
            else if (fastEnoughForRecovery && _recoveryHoldFramesRemaining <= 0)
            {
                _consecutiveFastFrames++;
                _consecutiveSlowFrames = 0;

                if (_consecutiveFastFrames >= _fastFrameThreshold)
                {
                    ApplyScaleIncrease(_scaleIncreasePercent);
                    _consecutiveFastFrames = 0;
                }
            }
            else
            {
                _consecutiveSlowFrames = 0;
                _consecutiveFastFrames = 0;
            }

            UpdatePressureState(effectiveFrameTime);
            UpdatePressureDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Enable or disable dynamic resolution scaling.
        /// </summary>
        /// <param name="enabled">Whether to enable dynamic resolution</param>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;

            if (_enabled)
                _startupGraceRemainingSeconds = ResolveStartupGraceSeconds();

            if (!_enabled && _urpAsset != null)
            {
                _currentRenderScale = ResolveDefaultRenderScale();
                _targetRenderScale = _currentRenderScale;
                ApplyRenderScale();
                _recoveryHoldFramesRemaining = 0;
                _pressureState = RenderPressureState.Stable;
                UpdatePressureDiagnostics();
            }
        }

        /// <summary>
        /// Set minimum render scale based on quality preset.
        /// </summary>
        /// <param name="preset">Quality preset</param>
        public void SetQualityPreset(LODQualityPreset preset)
        {
            _qualityPreset = preset;
            _qualityPresetMinimumRenderScale = ResolveMinimumRenderScaleFromPreset(preset);
            RefreshMinimumRenderScale();

            // Clamp current scale to new minimum
            if (!IsFinite(_currentRenderScale) || _currentRenderScale < _minRenderScale)
            {
                _currentRenderScale = _minRenderScale;
                _targetRenderScale = _currentRenderScale;
                ApplyRenderScale();
            }

            UpdatePressureDiagnostics();
        }

        /// <summary>
        /// Applies platform-level pressure without using the debug override path.
        /// </summary>
        /// <param name="active">Whether the pressure floor is active.</param>
        /// <param name="minimumRenderScale">Lowest render scale allowed while pressured.</param>
        /// <param name="targetRenderScale">Immediate target ceiling for the current render scale.</param>
        public void SetPlatformPressureRenderScale(bool active, float minimumRenderScale, float targetRenderScale)
        {
            bool wasActive = _platformPressureRenderScaleActive;
            _platformPressureRenderScaleActive = active;
            if (active)
            {
                float safeMinimumScale = IsFinite(minimumRenderScale) && minimumRenderScale > 0f
                    ? minimumRenderScale
                    : _minRenderScale;
                _platformPressureMinimumRenderScale = Mathf.Clamp(safeMinimumScale, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale());
            }

            RefreshMinimumRenderScale();

            if (_systemOverrideActive)
            {
                UpdatePressureDiagnostics();
                return;
            }

            if (_urpAsset == null)
            {
                UpdatePressureDiagnostics();
                return;
            }

            if (active)
            {
                float safeTargetScale = IsFinite(targetRenderScale) && targetRenderScale > 0f
                    ? targetRenderScale
                    : _currentRenderScale;
                float pressuredTarget = Mathf.Clamp(safeTargetScale, _minRenderScale, ResolveMaxRenderScale());
                if (_currentRenderScale > pressuredTarget + 0.0001f)
                {
                    _currentRenderScale = pressuredTarget;
                    _targetRenderScale = pressuredTarget;
                    ApplyRenderScale();
                }

                _recoveryHoldFramesRemaining = Mathf.Max(
                    Mathf.Max(0, _recoveryHoldFramesRemaining),
                    Mathf.Max(0, _recoveryHoldFrames));
                UpdatePressureDiagnostics();
                return;
            }

            if (wasActive && _currentRenderScale < _minRenderScale)
            {
                _currentRenderScale = _minRenderScale;
                _targetRenderScale = _currentRenderScale;
                ApplyRenderScale();
            }

            UpdatePressureDiagnostics();
        }

        public void ApplySystemOverrideRenderScale(
            float currentScale01,
            float targetScale01,
            float frameTimeEwmaMs,
            byte pressureLevel,
            byte flags)
        {
            _systemOverrideActive = true;
            _thermalOverrideActive = pressureLevel >= 2 || (flags & SystemOverrideThermalFlag) != 0;
            _systemOverridePressureLevel = pressureLevel;
            _systemOverrideFlags = flags;
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            _systemOverrideFrameTimeEwmaMs = IsFinite(frameTimeEwmaMs) && frameTimeEwmaMs > 0f
                ? frameTimeEwmaMs
                : targetFrameTimeMs;

            _targetRenderScale = Mathf.Clamp(
                IsFinite(targetScale01) ? targetScale01 : _currentRenderScale,
                SystemOverrideMinimumRenderScale,
                ResolveMaxRenderScale());
            float nextScale = Mathf.Clamp(
                IsFinite(currentScale01) ? currentScale01 : _targetRenderScale,
                SystemOverrideMinimumRenderScale,
                ResolveMaxRenderScale());

            if (Mathf.Abs(_currentRenderScale - nextScale) > 0.0001f)
            {
                _currentRenderScale = nextScale;
                ApplyRenderScale();
            }
            else
            {
                UpdateSnapshot();
            }

            _smoothedFrameTimeMs = _systemOverrideFrameTimeEwmaMs;
            _peakFrameTimeMs = Mathf.Max(_peakFrameTimeMs, _systemOverrideFrameTimeEwmaMs);
            _pressureState = _thermalOverrideActive || _systemOverrideFrameTimeEwmaMs > targetFrameTimeMs
                ? RenderPressureState.Pressured
                : RenderPressureState.Stable;
            UpdatePressureDiagnostics();
        }

        public void ClearSystemOverrideRenderScale()
        {
            _systemOverrideActive = false;
            _thermalOverrideActive = false;
            _systemOverridePressureLevel = 0;
            _systemOverrideFlags = 0;
            _systemOverrideFrameTimeEwmaMs = ResolveTargetFrameTimeMs();
            float restoredScale = ResolveDefaultRenderScale();
            _currentRenderScale = restoredScale;
            _targetRenderScale = restoredScale;
            if (_urpAsset != null)
                ApplyRenderScale();
            else
                UpdateSnapshot();

            UpdatePressureDiagnostics();
        }

        public bool TryGetSnapshot(out DynamicResolutionRuntimeSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }

        internal void SetDebugFrameTimeOverride(float frameTimeMs)
        {
            _debugFrameTimeOverrideActive = IsFinite(frameTimeMs) && frameTimeMs > 0f;
            _debugFrameTimeOverrideMs = _debugFrameTimeOverrideActive
                ? Mathf.Max(0.01f, frameTimeMs)
                : 0f;
        }

        internal void ClearDebugFrameTimeOverride()
        {
            _debugFrameTimeOverrideActive = false;
            _debugFrameTimeOverrideMs = 0f;
        }

        internal void SetDebugRenderScaleOverride(float renderScale)
        {
            _debugRenderScaleOverrideActive = IsFinite(renderScale) && renderScale > 0f;
            _debugRenderScaleOverrideValue = _debugRenderScaleOverrideActive
                ? Mathf.Clamp(renderScale, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale())
                : 0f;
        }

        internal void ClearDebugRenderScaleOverride()
        {
            _debugRenderScaleOverrideActive = false;
            _debugRenderScaleOverrideValue = 0f;
        }

        public void LateFrameTick()
        {
            AdvanceDynamicResolutionState(SystemDispatcher.CurrentFrameUnscaledDeltaTime);

            if (!_renderScaleApplyQueued)
                return;

            _renderScaleApplyQueued = false;
            _applyingRenderScaleLateFrame = true;
            ApplyRenderScale();
            _applyingRenderScaleLateFrame = false;
        }

        private bool HandleStartupGrace(float dt, float observedFrameTime)
        {
            if (_startupGraceRemainingSeconds <= 0f)
                return false;

            float safeDeltaTime = IsFinite(dt) && dt > 0f ? dt : 0f;
            _startupGraceRemainingSeconds = Mathf.Max(
                0f,
                _startupGraceRemainingSeconds - safeDeltaTime);

            float startupScale = Mathf.Clamp(ResolveDefaultRenderScale(), _minRenderScale, ResolveMaxRenderScale());
            if (Mathf.Abs(_currentRenderScale - startupScale) > 0.0001f)
            {
                _currentRenderScale = startupScale;
                _targetRenderScale = startupScale;
                ApplyRenderScale();
            }

            _consecutiveSlowFrames = 0;
            _consecutiveFastFrames = 0;
            _recoveryHoldFramesRemaining = 0;
            _peakFrameTimeMs = IsFinite(_peakFrameTimeMs)
                ? Mathf.Max(_peakFrameTimeMs, observedFrameTime)
                : observedFrameTime;
            _pressureState = RenderPressureState.Stable;
            UpdatePressureDiagnostics();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void ApplyRenderScale()
        {
            if (_urpAsset == null)
                return;

            if (_runtimeRenderScaleQueueActive && !_applyingRenderScaleLateFrame)
            {
                _renderScaleApplyQueued = true;
                return;
            }

            _minRenderScale = IsFinite(_minRenderScale) && _minRenderScale > 0f
                ? Mathf.Clamp(_minRenderScale, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale())
                : SystemOverrideMinimumRenderScale;
            _currentRenderScale = IsFinite(_currentRenderScale) && _currentRenderScale > 0f
                ? Mathf.Clamp(_currentRenderScale, _minRenderScale, ResolveMaxRenderScale())
                : ResolveDefaultRenderScale();
            _targetRenderScale = IsFinite(_targetRenderScale) && _targetRenderScale > 0f
                ? Mathf.Clamp(_targetRenderScale, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale())
                : _currentRenderScale;
            if (!_systemOverrideActive)
                _urpAsset.renderScale = _currentRenderScale;
            ScalableBufferManager.ResizeBuffers(_currentRenderScale, _currentRenderScale);
            UpdateSnapshot();
        }

        private void UpdateSnapshot()
        {
            uint sequence = _snapshotSequence++;
            _snapshot.CurrentRenderScale01 = IsFinite(_currentRenderScale) && _currentRenderScale > 0f
                ? _currentRenderScale
                : ResolveDefaultRenderScale();
            _snapshot.TargetRenderScale01 = IsFinite(_targetRenderScale) && _targetRenderScale > 0f
                ? _targetRenderScale
                : ResolveDefaultRenderScale();
            _snapshot.FrameTimeEwmaMs = ResolveSnapshotFrameTimeMs();
            _snapshot.PressureLevel = _systemOverrideActive ? _systemOverridePressureLevel : (byte)_pressureState;
            _snapshot.Flags = _systemOverrideFlags;
            _snapshot.Reserved0 = 0;
            _snapshot.Reserved1 = 0;
            _snapshot.Frame = sequence;
            _snapshot.Sequence = sequence;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private float ResolveDefaultRenderScale()
        {
            return IsFinite(_defaultRenderScale) && _defaultRenderScale > 0f
                ? Mathf.Clamp(_defaultRenderScale, SystemOverrideMinimumRenderScale, ResolveMaxRenderScale())
                : 1f;
        }

        private float ResolveMaxRenderScale()
        {
            return IsFinite(_maxRenderScale) && _maxRenderScale >= SystemOverrideMinimumRenderScale
                ? _maxRenderScale
                : 1f;
        }

        private float ResolveStartupGraceSeconds()
        {
            return IsFinite(_startupGraceSeconds) && _startupGraceSeconds > 0f
                ? _startupGraceSeconds
                : 0f;
        }

        private float ResolveTargetFrameTimeMs()
        {
            return IsFinite(_targetFrameTime) && _targetFrameTime > 0f
                ? _targetFrameTime
                : DefaultTargetFrameTimeMs;
        }

        private float ResolveCriticalFrameTimeMs(float targetFrameTimeMs)
        {
            if (IsFinite(_criticalFrameTime) && _criticalFrameTime > targetFrameTimeMs)
                return _criticalFrameTime;

            return Mathf.Max(targetFrameTimeMs + 0.01f, DefaultCriticalFrameTimeMs);
        }

        private float ResolveSnapshotFrameTimeMs()
        {
            float frameTimeMs = _systemOverrideActive ? _systemOverrideFrameTimeEwmaMs : _smoothedFrameTimeMs;
            return IsFinite(frameTimeMs) && frameTimeMs > 0f
                ? frameTimeMs
                : ResolveTargetFrameTimeMs();
        }

        private float ResolveObservedFrameTime(float dt)
        {
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            if (!IsFinite(dt) || dt <= 0f)
                return targetFrameTimeMs;

            float frameTime = dt * 1000f;
            if (!IsFinite(frameTime) || frameTime <= 0f)
                return targetFrameTimeMs;

            if (_debugFrameTimeOverrideActive)
                return Mathf.Max(frameTime, _debugFrameTimeOverrideMs);

            return frameTime;
        }

        private void UpdateFrameTrend(float frameTimeMs)
        {
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            if (!IsFinite(frameTimeMs) || frameTimeMs <= 0f)
                frameTimeMs = targetFrameTimeMs;

            float smoothing = IsFinite(_frameTimeSmoothing)
                ? Mathf.Clamp01(_frameTimeSmoothing)
                : DefaultFrameTimeSmoothing;
            if (!IsFinite(_smoothedFrameTimeMs) || _smoothedFrameTimeMs <= 0f)
                _smoothedFrameTimeMs = frameTimeMs;
            else
                _smoothedFrameTimeMs += (frameTimeMs - _smoothedFrameTimeMs) * smoothing;

            if (!IsFinite(_peakFrameTimeMs) || frameTimeMs > _peakFrameTimeMs)
                _peakFrameTimeMs = frameTimeMs;
        }

        private void ApplyScaleReduction(float reductionPercent)
        {
            float safeReductionPercent = IsFinite(reductionPercent)
                ? Mathf.Max(0f, reductionPercent)
                : 0f;
            float reductionFactor = 1f - safeReductionPercent * 0.01f;
            float currentScale = IsFinite(_currentRenderScale) && _currentRenderScale > 0f
                ? _currentRenderScale
                : ResolveDefaultRenderScale();
            float targetScale = currentScale * reductionFactor;
            float nextScale = Mathf.Max(targetScale, _minRenderScale);
            if (Mathf.Abs(nextScale - currentScale) <= 0.0001f)
                return;

            _currentRenderScale = nextScale;
            _targetRenderScale = nextScale;
            _recoveryHoldFramesRemaining = Mathf.Max(0, _recoveryHoldFrames);
            _peakFrameTimeMs = IsFinite(_smoothedFrameTimeMs) && _smoothedFrameTimeMs > 0f
                ? _smoothedFrameTimeMs
                : ResolveTargetFrameTimeMs();
            ApplyRenderScale();
        }

        private void ApplyScaleIncrease(float increasePercent)
        {
            float safeIncreasePercent = IsFinite(increasePercent)
                ? Mathf.Max(0f, increasePercent)
                : 0f;
            float increaseFactor = 1f + safeIncreasePercent * 0.01f;
            float currentScale = IsFinite(_currentRenderScale) && _currentRenderScale > 0f
                ? _currentRenderScale
                : ResolveDefaultRenderScale();
            float targetScale = currentScale * increaseFactor;
            float nextScale = Mathf.Min(targetScale, ResolveMaxRenderScale());
            if (Mathf.Abs(nextScale - currentScale) <= 0.0001f)
                return;

            _currentRenderScale = nextScale;
            _targetRenderScale = nextScale;
            ApplyRenderScale();
        }

        private void UpdatePressureState(float effectiveFrameTime)
        {
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            float criticalFrameTimeMs = ResolveCriticalFrameTimeMs(targetFrameTimeMs);
            if (!IsFinite(effectiveFrameTime) || effectiveFrameTime <= 0f)
                effectiveFrameTime = targetFrameTimeMs;

            if (_recoveryHoldFramesRemaining > 0)
            {
                _pressureState = RenderPressureState.Recovering;
                return;
            }

            if (effectiveFrameTime >= criticalFrameTimeMs)
            {
                _pressureState = RenderPressureState.Critical;
                return;
            }

            if (effectiveFrameTime > targetFrameTimeMs)
            {
                _pressureState = RenderPressureState.Pressured;
                return;
            }

            _pressureState = RenderPressureState.Stable;
        }

        private void UpdatePressureDiagnostics()
        {
            _debugSmoothedFrameTimeMs = _smoothedFrameTimeMs;
            _debugPeakFrameTimeMs = _peakFrameTimeMs;
            _debugPressureState = ResolvePressureStateLabel(_pressureState);
            _debugRecoveryHoldFramesRemaining = _recoveryHoldFramesRemaining;
            _debugPlatformPressureRenderScaleActive = _platformPressureRenderScaleActive;
            _debugEffectiveMinimumRenderScale = _minRenderScale;
        }

        private static string ResolvePressureStateLabel(RenderPressureState state)
        {
            switch (state)
            {
                case RenderPressureState.Recovering:
                    return RecoveringPressureStateLabel;
                case RenderPressureState.Pressured:
                    return PressuredPressureStateLabel;
                case RenderPressureState.Critical:
                    return CriticalPressureStateLabel;
                default:
                    return StablePressureStateLabel;
            }
        }

        private static float ResolveMinimumRenderScaleFromPreset(LODQualityPreset preset)
        {
            return ResolveMinimumRenderScaleFromQualityWeight(ResolveQualityPresetWeight01(preset));
        }

        private static float ResolveQualityPresetWeight01(LODQualityPreset preset)
        {
            int rawPreset = (int)preset;
            float ordinalWeight = rawPreset * 0.5f;
            return math.select(0.5f, math.saturate(ordinalWeight), (uint)rawPreset <= 2u);
        }

        private static float ResolveMinimumRenderScaleFromQualityWeight(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 0.5f);
            float q = quality * quality * (3f - (2f * quality));
            return math.lerp(0.7f, 0.9f, q);
        }

        private void RefreshMinimumRenderScale()
        {
            float presetScale = IsFinite(_qualityPresetMinimumRenderScale) && _qualityPresetMinimumRenderScale > 0f
                ? _qualityPresetMinimumRenderScale
                : ResolveMinimumRenderScaleFromPreset(_qualityPreset);
            float maxRenderScale = ResolveMaxRenderScale();
            float presetMinimum = Mathf.Clamp(presetScale, SystemOverrideMinimumRenderScale, maxRenderScale);
            float platformMinimum = IsFinite(_platformPressureMinimumRenderScale) && _platformPressureMinimumRenderScale > 0f
                ? _platformPressureMinimumRenderScale
                : presetMinimum;
            _minRenderScale = _platformPressureRenderScaleActive
                ? Mathf.Min(presetMinimum, Mathf.Clamp(platformMinimum, SystemOverrideMinimumRenderScale, maxRenderScale))
                : presetMinimum;
        }

        private void RestoreDefaultRenderScale()
        {
            if (_urpAsset == null)
                return;

            float defaultScale = ResolveDefaultRenderScale();
            _urpAsset.renderScale = defaultScale;
            ScalableBufferManager.ResizeBuffers(defaultScale, defaultScale);
        }
    }
}
