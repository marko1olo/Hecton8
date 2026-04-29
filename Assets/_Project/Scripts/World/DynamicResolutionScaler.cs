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
//   • Singleton via DynamicResolutionScaler.Instance
//   • ITickable — registers with GameTickManager
//   • Zero-GC — no allocations in hot paths
//
// PERFORMANCE:
//   • Target: < 0.1ms per frame
//   • Zero GC allocations
//   • Smooth scale adjustments
//
// INTEGRATION:
//   • GameTickManager — ITickable registration
//   • LODSystemManager — quality preset integration
//   • UniversalRenderPipeline.asset — render scale application
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Hecton8.Core;
using Hecton8.SaveSystem;

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
    public sealed class DynamicResolutionScaler : MonoBehaviour, ITickable, ISaveable
    {
        private static readonly string[] RenderPressureStateLabels = Enum.GetNames(typeof(RenderPressureState));

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static DynamicResolutionScaler _instance;

        /// <summary>
        /// Singleton instance. Null if not initialized.
        /// </summary>
        public static DynamicResolutionScaler Instance => _instance;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Dynamic Resolution ──────────────────")]
        [SerializeField, Tooltip("Target frame time (milliseconds)")]
        private float _targetFrameTime = 16.67f; // 60 FPS

        [SerializeField, Tooltip("Emergency frame time threshold. Crossing it forces a more aggressive render-scale drop.")]
        private float _criticalFrameTime = 25f;

        [SerializeField, Tooltip("Min render scale")]
        private float _minRenderScale = 0.5f;

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
        private float _frameTimeSmoothing = 0.18f;

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
        private bool _registered;
        private LODQualityPreset _qualityPreset = LODQualityPreset.Medium;
        private float _smoothedFrameTimeMs;
        private float _peakFrameTimeMs;
        private RenderPressureState _pressureState = RenderPressureState.Stable;

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

        /// <summary>
        /// Whether dynamic resolution scaling is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        internal string DebugPressureStateLabel => _debugPressureState;

        internal float DebugSmoothedFrameTimeMs => _debugSmoothedFrameTimeMs;

        internal bool DebugFrameTimeOverrideActive => _debugFrameTimeOverrideActive;

        internal float DebugFrameTimeOverrideMs => _debugFrameTimeOverrideMs;

        internal bool DebugRenderScaleOverrideActive => _debugRenderScaleOverrideActive;

        internal float DebugRenderScaleOverrideValue => _debugRenderScaleOverrideValue;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[DynamicResolutionScaler] Duplicate instance detected. Destroying duplicate.");
                #endif
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Cache URP asset
            _urpAsset = UniversalRenderPipeline.asset;
            if (_urpAsset == null)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[DynamicResolutionScaler] UniversalRenderPipeline.asset is null. Dynamic resolution disabled.");
                #endif
                _enabled = false;
            }

            // Initialize render scale
            if (_urpAsset != null)
            {
                _defaultRenderScale = _urpAsset.renderScale;
            }

            _minRenderScale = GetMinimumRenderScaleForPreset(_qualityPreset);
            _currentRenderScale = Mathf.Clamp(_defaultRenderScale, _minRenderScale, _maxRenderScale);
            _startupGraceRemainingSeconds = Mathf.Max(0f, _startupGraceSeconds);
            _smoothedFrameTimeMs = _targetFrameTime;
            _peakFrameTimeMs = _targetFrameTime;
            UpdatePressureDiagnostics();
            if (_urpAsset != null)
            {
                ApplyRenderScale();
            }

            // Register with the authoritative save service.
            GlobalRegistry.Save?.Register(this);

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[DynamicResolutionScaler] Initialized. Target frame time: " + _targetFrameTime + " ms");
            #endif
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            // Unregister from the authoritative save service.
            GlobalRegistry.Save?.Unregister(this);

            RestoreDefaultRenderScale();
            TryUnregister();

            // Clear singleton
            if (_instance == this)
                _instance = null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
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
                _currentRenderScale = _defaultRenderScale;
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
        public void Tick(float dt)
        {
            if (_urpAsset == null)
                return;

            if (!_enabled && !_debugRenderScaleOverrideActive)
                return;

            // Convert dt to milliseconds
            float observedFrameTime = ResolveObservedFrameTime(dt);
            UpdateFrameTrend(observedFrameTime);

            if (_debugRenderScaleOverrideActive)
            {
                float overrideScale = Mathf.Clamp(_debugRenderScaleOverrideValue, 0.1f, _maxRenderScale);
                if (!Mathf.Approximately(_currentRenderScale, overrideScale))
                {
                    _currentRenderScale = overrideScale;
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
            bool criticalPressure = effectiveFrameTime >= _criticalFrameTime;
            bool pressured = effectiveFrameTime > _targetFrameTime;
            bool fastEnoughForRecovery = effectiveFrameTime < _targetFrameTime * 0.9f;

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
                    float pressureRange = Mathf.Max(0.01f, _criticalFrameTime - _targetFrameTime);
                    float pressureLerp = Mathf.Clamp01((effectiveFrameTime - _targetFrameTime) / pressureRange);
                    float adaptiveReductionPercent = Mathf.Lerp(_scaleReductionPercent, _criticalScaleReductionPercent, pressureLerp);
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
                _startupGraceRemainingSeconds = Mathf.Max(0f, _startupGraceSeconds);

            if (!_enabled && _urpAsset != null)
            {
                _currentRenderScale = _defaultRenderScale;
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
            _minRenderScale = GetMinimumRenderScaleForPreset(preset);

            // Clamp current scale to new minimum
            if (_currentRenderScale < _minRenderScale)
            {
                _currentRenderScale = _minRenderScale;
                ApplyRenderScale();
            }

            UpdatePressureDiagnostics();
        }

        internal void SetDebugFrameTimeOverride(float frameTimeMs)
        {
            _debugFrameTimeOverrideActive = frameTimeMs > 0f;
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
            _debugRenderScaleOverrideActive = renderScale > 0f;
            _debugRenderScaleOverrideValue = _debugRenderScaleOverrideActive
                ? Mathf.Clamp(renderScale, 0.1f, _maxRenderScale)
                : 0f;
        }

        internal void ClearDebugRenderScaleOverride()
        {
            _debugRenderScaleOverrideActive = false;
            _debugRenderScaleOverrideValue = 0f;
        }

        private bool HandleStartupGrace(float dt, float observedFrameTime)
        {
            if (_startupGraceRemainingSeconds <= 0f)
                return false;

            _startupGraceRemainingSeconds = Mathf.Max(
                0f,
                _startupGraceRemainingSeconds - Mathf.Max(0f, dt));

            float startupScale = Mathf.Clamp(_defaultRenderScale, _minRenderScale, _maxRenderScale);
            if (Mathf.Abs(_currentRenderScale - startupScale) > 0.0001f)
            {
                _currentRenderScale = startupScale;
                ApplyRenderScale();
            }

            _consecutiveSlowFrames = 0;
            _consecutiveFastFrames = 0;
            _recoveryHoldFramesRemaining = 0;
            _peakFrameTimeMs = Mathf.Max(_peakFrameTimeMs, observedFrameTime);
            _pressureState = RenderPressureState.Stable;
            UpdatePressureDiagnostics();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void ApplyRenderScale()
        {
            if (_urpAsset == null) return;

            _urpAsset.renderScale = _currentRenderScale;
        }

        private float ResolveObservedFrameTime(float dt)
        {
            float frameTime = dt * 1000f;
            if (_debugFrameTimeOverrideActive)
                return Mathf.Max(frameTime, _debugFrameTimeOverrideMs);

            return frameTime;
        }

        private void UpdateFrameTrend(float frameTimeMs)
        {
            float smoothing = Mathf.Clamp01(_frameTimeSmoothing);
            _smoothedFrameTimeMs = Mathf.Lerp(_smoothedFrameTimeMs, frameTimeMs, smoothing);
            if (frameTimeMs > _peakFrameTimeMs)
                _peakFrameTimeMs = frameTimeMs;
        }

        private void ApplyScaleReduction(float reductionPercent)
        {
            float safeReductionPercent = Mathf.Max(0f, reductionPercent);
            float reductionFactor = 1f - (safeReductionPercent / 100f);
            float targetScale = _currentRenderScale * reductionFactor;
            float nextScale = Mathf.Max(targetScale, _minRenderScale);
            if (Mathf.Abs(nextScale - _currentRenderScale) <= 0.0001f)
                return;

            _currentRenderScale = nextScale;
            _recoveryHoldFramesRemaining = Mathf.Max(0, _recoveryHoldFrames);
            _peakFrameTimeMs = _smoothedFrameTimeMs;
            ApplyRenderScale();
        }

        private void ApplyScaleIncrease(float increasePercent)
        {
            float safeIncreasePercent = Mathf.Max(0f, increasePercent);
            float increaseFactor = 1f + (safeIncreasePercent / 100f);
            float targetScale = _currentRenderScale * increaseFactor;
            float nextScale = Mathf.Min(targetScale, _maxRenderScale);
            if (Mathf.Abs(nextScale - _currentRenderScale) <= 0.0001f)
                return;

            _currentRenderScale = nextScale;
            ApplyRenderScale();
        }

        private void UpdatePressureState(float effectiveFrameTime)
        {
            if (_recoveryHoldFramesRemaining > 0)
            {
                _pressureState = RenderPressureState.Recovering;
                return;
            }

            if (effectiveFrameTime >= _criticalFrameTime)
            {
                _pressureState = RenderPressureState.Critical;
                return;
            }

            if (effectiveFrameTime > _targetFrameTime)
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
        }

        private static string ResolvePressureStateLabel(RenderPressureState state)
        {
            int index = (int)state;
            return (uint)index < (uint)RenderPressureStateLabels.Length ? RenderPressureStateLabels[index] : RenderPressureStateLabels[0];
        }

        private float GetMinimumRenderScaleForPreset(LODQualityPreset preset)
        {
            switch (preset)
            {
                case LODQualityPreset.Low:
                    return 0.7f;
                case LODQualityPreset.Medium:
                    return 0.8f;
                case LODQualityPreset.High:
                    return 0.9f;
                default:
                    return 0.8f;
            }
        }

        private void RestoreDefaultRenderScale()
        {
            if (_urpAsset == null) return;

            _urpAsset.renderScale = _defaultRenderScale;
        }
    }
}
