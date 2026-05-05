// ============================================================================
// HECTON-8 — CameraJuiceSystem.cs
// Camera shake, FOV effects, and post-processing modulation system.
// Zero-GC hot paths, 1.0ms frame budget, native tick-driven transitions.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.VFX
{
    /// <summary>
    /// Camera presentation runtime managing shake, FOV effects, and post-processing modulation.
    /// Integrates with HectonSurvivalSystem, PlayerMovement, InteractionEvents, GameTickManager, SaveManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraJuiceSystem : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, ISaveable, IInteractionEventListener
    {
        // ═══ CACHED REFERENCES ═══
        private struct ShakeJobInput
        {
            public float NoiseTime;
            public float NoiseSeed;
            public float MaxDisplacement;
            public float Falloff;
            public float IntensityScale;
            public float3 AxisWeights;
        }

        private struct ShakeJobResult
        {
            public float3 Offset;
        }

        private Camera _mainCamera;
        private Volume _urpVolume;
        private Transform _cameraTransform;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private Vector3 _cameraLocalRestPosition;
        private ParticleSystem _speedLineParticles;
        private ParticleSystemRenderer _speedLineRenderer;
        private Transform _speedLineRoot;
        private float _speedLineIntensity;
        private float _cachedSpeedLineEmissionRate = -1f;
        private float _cachedSpeedLineVelocityZ = float.MinValue;
        private float _cachedSpeedLineStretch = -1f;

        [Header("References")]
        [SerializeField, Tooltip("Optional explicit camera reference. Falls back to local hierarchy lookup.")]
        private Camera _cameraReference;

        [SerializeField, Tooltip("Optional explicit URP volume reference. Falls back to local hierarchy lookup.")]
        private Volume _volumeReference;

        [SerializeField, Tooltip("Optional explicit survival system reference. Falls back to cold-path scene resolve.")]
        private HectonSurvivalSystem _survivalSystemReference;

        [SerializeField, Tooltip("Optional explicit player movement reference. Falls back to cold-path scene resolve.")]
        private HectonPlayerMovement _playerMovementReference;

        [Header("Cinematic Speed Lines")]
        [SerializeField, Tooltip("Optional shared material for camera-local GPU-instanced speed-line particles.")]
        private Material _speedLineMaterial;

        [SerializeField, Min(0f), Tooltip("Speed threshold where camera-local speed lines start emitting.")]
        private float _speedLineStartMetersPerSecond = 10f;

        [SerializeField, Min(0.1f), Tooltip("Speed where speed-line emission reaches full density.")]
        private float _speedLineFullMetersPerSecond = 22f;

        [SerializeField, Min(1f), Tooltip("Maximum camera-local speed-line emission rate.")]
        private float _speedLineMaxEmissionRate = 180f;

        [SerializeField, Min(1f), Tooltip("Maximum camera-local speed-line particle budget.")]
        private int _speedLineMaxParticles = 256;

        [SerializeField, Min(0.1f), Tooltip("Maximum renderer length scale for stretched speed-line particles.")]
        private float _speedLineMaxStretch = 5f;

        // ═══ SHAKE STATE ═══
        private Vector3 _shakeOffset;
        // COLD ALLOC: List<ActiveShake>[8] — max simultaneous shakes — owner: CameraJuiceSystem
        private readonly List<ActiveShake> _activeShakes = new List<ActiveShake>(8);
        private const int MAX_ACTIVE_SHAKES = 8;
        private const float MAX_SHAKE_DISPLACEMENT = 0.5f;
        private const float DEFAULT_SHAKE_CLIP_SAFE_DISPLACEMENT = 0.05f;
        private const float LOW_FREQUENCY_HEAVE_NOISE_SCALE = 0.28f;
        private const float LOW_FREQUENCY_HEAVE_DISPLACEMENT_SCALE = 0.35f;
        private float _shakeNoiseTime;
        private NativeArray<ShakeJobInput> _shakeJobInputs;
        private NativeArray<ShakeJobResult> _shakeJobResults;
        private JobHandle _shakeJobHandle;
        private bool _shakeJobScheduled;
        private bool _shakeJobApplyResult;

        // ═══ FOV STATE ═══
        public enum FOVState { Idle, SprintKick, DamageRecoil }
        private FOVState _fovState = FOVState.Idle;
        private float _baseFOV;
        private float _currentFOVOffset;
        private float _fovBlendStart;
        private float _fovBlendTarget;
        private float _fovBlendDuration;
        private float _fovBlendElapsed;
        private bool _fovBlendActive;
        private float _inputReclaimFovStart;
        private float _inputReclaimFovTarget;
        private float _inputReclaimFovDuration;
        private float _inputReclaimFovElapsed;
        private bool _inputReclaimFovActive;
        private const float MIN_FOV = 40f;
        private const float MAX_FOV = 90f;

        // ═══ POST-PROCESSING STATE ═══
        private Vignette _healthVignette;
        private ChromaticAberration _o2ChromaticAberration;
        private DepthOfField _interactionDoF;
        private bool _postProcessingEnabled = true;
        private bool _healthO2EffectsEnabled = true;

        // ═══ BIOME PROFILE ═══
        private BiomeProfile _currentBiome;
        private BiomeProfile _targetBiome;
        private BiomeProfile _biomeBlendFrom;
        private float _biomeBlendDuration;
        private float _biomeBlendElapsed;
        private bool _biomeBlendActive;

        // ═══ INTERACTION FOCUS ═══
        private IInteractable _focusTarget;
        private Transform _focusTargetTransform;
        private float _focusDistance;
        private NativeArray<RaycastCommand> _focusRaycastCommands;
        private NativeArray<RaycastHit> _focusRaycastHits;
        private JobHandle _focusRaycastHandle;
        private bool _focusRaycastScheduled;
        private float _resolvedFocusDistance = 0.06f;
        private float _focusDistanceVelocity;
        private float _pauseDepthOfFieldWeight;
        private float _pauseDofBaseFocalLength;
        private float _pauseDofBaseAperture;
        private float _pauseDofBaseGaussianEnd;
        private float _pauseDofBaseGaussianMaxRadius;
        private bool _pauseDofDefaultsCaptured;
        private bool _pauseDofOverrideEngaged;
        private const float PauseDofFocusDistance = 0.1f;
        private const float PauseDofFocalLength = 85f;
        private const float PauseDofAperture = 2.8f;
        private const float PauseDofGaussianEnd = 4f;
        private const float PauseDofGaussianMaxRadius = 1f;
        private const int TransparentFxLayerIndex = 1;
        private const int WaterLayerIndex = 4;
        private const int UiLayerIndex = 5;
        private const int HudInternalLayerIndex = 17;
        private static readonly int _TransparentFxLayer = TransparentFxLayerIndex;
        private static readonly int _WaterLayer = WaterLayerIndex;
        private static readonly int _UiLayer = UiLayerIndex;
        private static readonly int _HudInternalLayer = HudInternalLayerIndex;

        // ═══ SETTINGS ═══
        [Header("── Settings ──────────────────")]
        [SerializeField, Range(0f, 2f), Tooltip("Camera shake intensity multiplier (0 = off, 1 = default, 2 = double)")]
        private float _shakeIntensityMultiplier = 1.0f;

        [SerializeField, Range(0.005f, 0.2f), Tooltip("Maximum presentation-space shake offset allowed inside tight submarine interiors.")]
        private float _cameraShakeClipSafeDisplacement = DEFAULT_SHAKE_CLIP_SAFE_DISPLACEMENT;

        [SerializeField, Range(0f, 2f), Tooltip("FOV effects intensity multiplier (0 = off, 1 = default, 2 = double)")]
        private float _fovIntensityMultiplier = 1.0f;

        [SerializeField, Tooltip("Enable motion blur post-processing effect")]
        private bool _motionBlurEnabled = false;

        [SerializeField, Tooltip("Enable chromatic aberration post-processing effect")]
        private bool _chromaticAberrationEnabled = true;

        [SerializeField, Tooltip("Enable depth-of-field post-processing effect")]
        private bool _depthOfFieldEnabled = true;

        [SerializeField, Tooltip("Physics layers sampled by the center-eye focus ray.")]
        private LayerMask _interactionFocusMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [SerializeField, Tooltip("Maximum range used by the center-eye focus ray.")]
        private float _interactionFocusRayDistance = 120f;

        [SerializeField, Tooltip("Fallback near-field focus distance used when the diegetic visor HUD is the active focus plane.")]
        private float _hudFocusDistance = 0.06f;

        [SerializeField, Tooltip("Hard near-field focus distance applied while the center-eye ray is locked onto the PDA plane.")]
        private float _pdaFocusDistance = 0.4f;

        [SerializeField, Tooltip("Far-field focus distance restored when the player is no longer center-looking at the PDA plane.")]
        private float _worldFocusDistance = 20f;

        [SerializeField, Tooltip("Maximum center-ray hit distance treated as a PDA focus lock.")]
        private float _pdaFocusThreshold = 0.2f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Smoothing duration for center-eye focus-distance convergence between near-field UI focus and far-field ocean focus.")]
        private float _focusTransitionDuration = 0.2f;

        [SerializeField, Range(0f, 0.8f), Tooltip("Additional chromatic-aberration intensity injected by active submarine structural fatigue.")]
        private float _structuralFatigueChromaticAberrationMax = 0.26f;

        [Header("Adaptive Budget Response")]
        [SerializeField, Tooltip("Allow camera juice to degrade under render-scale and VRAM pressure instead of paying full effect cost on weak hardware.")]
        private bool _enableAdaptiveBudgetResponse = true;

        [SerializeField, Range(0.5f, 1f), Tooltip("Render scale at which adaptive camera-juice degradation reaches its floor.")]
        private float _adaptiveBudgetFloorRenderScale = 0.72f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum shake intensity scale when adaptive budget response is fully engaged.")]
        private float _adaptiveShakeFloor = 0.45f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum FOV response scale when adaptive budget response is fully engaged.")]
        private float _adaptiveFOVFloor = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum post-processing intensity scale when adaptive budget response is fully engaged.")]
        private float _adaptivePostFxFloor = 0.35f;

        [SerializeField, Range(0.4f, 1f), Tooltip("Effective post-processing scale below which interaction depth-of-field is disabled.")]
        private float _adaptiveDoFDisableThreshold = 0.7f;

        [SerializeField, Range(0f, 1f), Tooltip("Additional shake attenuation applied while VRAM monitor reports warning pressure.")]
        private float _adaptiveVRAMWarningShakeScale = 0.85f;

        [SerializeField, Range(0f, 1f), Tooltip("Additional shake attenuation applied while VRAM monitor reports critical pressure.")]
        private float _adaptiveVRAMCriticalShakeScale = 0.65f;

        [SerializeField, Range(0f, 1f), Tooltip("Additional post-processing attenuation applied while VRAM monitor reports warning pressure.")]
        private float _adaptiveVRAMWarningPostFxScale = 0.82f;

        [SerializeField, Range(0f, 1f), Tooltip("Additional post-processing attenuation applied while VRAM monitor reports critical pressure.")]
        private float _adaptiveVRAMCriticalPostFxScale = 0.55f;

        [SerializeField, Range(1, MAX_ACTIVE_SHAKES), Tooltip("Maximum simultaneous shakes retained while VRAM pressure is warning.")]
        private int _adaptiveWarningMaxActiveShakes = 5;

        [SerializeField, Range(1, MAX_ACTIVE_SHAKES), Tooltip("Maximum simultaneous shakes retained while VRAM pressure is critical.")]
        private int _adaptiveCriticalMaxActiveShakes = 3;

        // ═══ PUBLIC SETTINGS PROPERTIES ═══

        /// <summary>
        /// Camera shake intensity multiplier (0 = off, 1 = default, 2 = double).
        /// Applied immediately without scene reload.
        /// </summary>
        public float ShakeIntensityMultiplier
        {
            get => _shakeIntensityMultiplier;
            set => _shakeIntensityMultiplier = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>
        /// FOV effects intensity multiplier (0 = off, 1 = default, 2 = double).
        /// Applied immediately without scene reload.
        /// </summary>
        public float FOVIntensityMultiplier
        {
            get => _fovIntensityMultiplier;
            set => _fovIntensityMultiplier = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>
        /// Enable motion blur post-processing effect.
        /// Applied immediately without scene reload.
        /// </summary>
        public bool MotionBlurEnabled
        {
            get => _motionBlurEnabled;
            set => _motionBlurEnabled = value;
        }

        /// <summary>
        /// Enable chromatic aberration post-processing effect.
        /// Applied immediately without scene reload.
        /// </summary>
        public bool ChromaticAberrationEnabled
        {
            get => _chromaticAberrationEnabled;
            set => _chromaticAberrationEnabled = value;
        }

        /// <summary>
        /// Enable depth-of-field post-processing effect.
        /// Applied immediately without scene reload.
        /// </summary>
        public bool DepthOfFieldEnabled
        {
            get => _depthOfFieldEnabled;
            set => _depthOfFieldEnabled = value;
        }

        // ═══ TICK REGISTRATION ═══
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _survivalEventsHooked;
        private bool _movementEventsHooked;
        private float _nextDependencyResolveTime;

        // ═══ EFFECT ENABLE FLAGS ═══
        private bool _shakeEnabled = true;
        private bool _fovEnabled = true;
        private bool _sprintFOVEnabled = true;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveShakeScale = 1f;
        private float _adaptiveFOVScale = 1f;
        private float _adaptivePostFxScale = 1f;
        private float _adaptiveRenderScale = 1f;
        private int _adaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;
        private VRAMMonitor.VRAMPressureState _adaptiveVRAMPressureState = VRAMMonitor.VRAMPressureState.Stable;
        private bool _adaptiveDisableInteractionDoF;

        // ═══ SHADER PROPERTY IDS ═══
        // Note: Volume overrides use direct .value assignment, not MaterialPropertyBlock

        // ═══ DEBUG ═══
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
        private const string DebugPressureStable = "Stable";
        private const string DebugPressureWarning = "Warning";
        private const string DebugPressureCritical = "Critical";
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveShakeScale = 1f;
        [SerializeField] private float _debugAdaptiveFOVScale = 1f;
        [SerializeField] private float _debugAdaptivePostFxScale = 1f;
        [SerializeField] private string _debugAdaptiveVRAMPressure = "Stable";
        [SerializeField] private int _debugAdaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;
#endif

        // ═══ LIFECYCLE ═══

        private void Awake()
        {
            CameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                LogDuplicateInstanceDetected();
                Destroy(gameObject);
                return;
            }

            if (!TryResolveCamera())
            {
                LogMainCameraMissing();
                enabled = false;
                return;
            }

            // Cache URPVolume
            TryResolveVolume();
            if (_urpVolume == null)
            {
                LogVolumeMissing();
                _postProcessingEnabled = false;
            }
            else
            {
                // Cache Volume overrides
                if (_urpVolume.profile.TryGet(out _healthVignette) == false)
                {
                    LogVignetteMissing();
                }
                if (_urpVolume.profile.TryGet(out _o2ChromaticAberration) == false)
                {
                    LogChromaticAberrationMissing();
                }
                if (_urpVolume.profile.TryGet(out _interactionDoF) == false)
                {
                    LogDepthOfFieldMissing();
                }
            }

            TryResolveGameplayDependencies();
            SyncDependencyFlags();
            EnsureFocusRaycastBuffers();
            EnsureShakeJobBuffers();
            SanitizeInteractionFocusMask();
            EnsureCameraSpeedLineParticles();

            // Performance mode degradation
            if (QualitySettings.GetQualityLevel() == 0)
            {
                _depthOfFieldEnabled = false;
                _motionBlurEnabled = false;
            }
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();

            if (!_registered && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
                _registered = GlobalRegistry.Updatables.Contains(this) ||
                              GlobalRegistry.SlowTickables.Contains(this);
            }
            TryRegisterLateFrame();

            TryResolveGameplayDependencies();
            SyncDependencySubscriptions();
            EnsureShakeJobBuffers();
            EnsureCameraSpeedLineParticles();

            InteractionEvents.Register(this);
        }

        private void OnDisable()
        {
            // Unregister from GameTickManager
            TryUnregister();
            TryUnregisterFromGlobalRegistry();

            UnhookDependencyEvents();

            InteractionEvents.Unregister(this);

            _fovBlendActive = false;
            _inputReclaimFovActive = false;
            _biomeBlendActive = false;

            _focusTarget = null;
            _focusTargetTransform = null;
            _focusDistanceVelocity = 0f;
            _pauseDepthOfFieldWeight = 0f;
            _pauseDofOverrideEngaged = false;
            _pauseDofDefaultsCaptured = false;
            _shakeOffset = Vector3.zero;
            _shakeJobApplyResult = false;
            ReleaseFocusRaycastBuffers();
            ReleaseShakeJobBuffers();
            StopCameraSpeedLineParticles();

            if (_cameraTransform != null)
            {
                _cameraTransform.localPosition = _cameraLocalRestPosition;
            }

            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView = _baseFOV;
            }
        }

        private void SanitizeInteractionFocusMask()
        {
            int layerMask = _interactionFocusMask.value;
            layerMask = ExcludeLayer(layerMask, _TransparentFxLayer);
            layerMask = ExcludeLayer(layerMask, _WaterLayer);
            layerMask = ExcludeLayer(layerMask, _UiLayer);
            layerMask = IncludeLayer(layerMask, _HudInternalLayer);
            _interactionFocusMask = layerMask;
        }

        private static int ExcludeLayer(int mask, int layer)
        {
            return layer >= 0 ? mask & ~(1 << layer) : mask;
        }

        private static int IncludeLayer(int mask, int layer)
        {
            return layer >= 0 ? mask | (1 << layer) : mask;
        }

        private void TryUnregister()
        {
            TryUnregisterLateFrame();

            if (_registered)
            {
                if (GlobalRegistry.SlowTickables.Contains(this))
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);

                if (GlobalRegistry.Updatables.Contains(this))
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

                _registered = false;
            }
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            CameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                LogDuplicateInstanceDetected();
                enabled = false;
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterCameraJuiceRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.CameraJuice, this);
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterCameraJuiceRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterFromGlobalRegistry();

            ReleaseFocusRaycastBuffers();
            ReleaseShakeJobBuffers();

        }

        // ═══ ITICKABLE ═══

        /// <summary>
        /// Per-frame update for camera shake, FOV, and depth-of-field focus distance.
        /// </summary>
        public void Tick(float dt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float startTime = Time.realtimeSinceStartup;
#endif

            try
            {
                RefreshAdaptiveBudgetResponse();
                UpdateShake(dt);
            }
            catch (Exception)
            {
                LogShakeCalculationFailed();
                _shakeEnabled = false;
            }

            try
            {
                UpdateFOV(dt);
                UpdateCameraSpeedLines(dt);
            }
            catch (Exception)
            {
                LogFovCalculationFailed();
                _fovEnabled = false;
            }

            try
            {
                UpdateBiomeBlend(dt);
            }
            catch (Exception)
            {
                LogBiomeBlendFailed();
                _biomeBlendActive = false;
            }

            try
            {
                UpdateInteractionFocus(dt);
                UpdatePauseDepthOfField(dt);
            }
            catch (Exception)
            {
                LogInteractionFocusFailed();
                _depthOfFieldEnabled = false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float frameTime = (Time.realtimeSinceStartup - startTime) * 1000f;
            if (frameTime > 1.0f && Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + 5f;
                LogFrameBudgetExceeded();
            }
#endif
        }

        public void LateFrameTick()
        {
            ResolveScheduledShakeJob(false);
            ResolveScheduledFocusRaycast();
            ApplyPostAupShakeOffset();
        }

        // ═══ ISLOWTICKABLE ═══

        /// <summary>
        /// 2Hz update for health and O2 post-processing effects.
        /// </summary>
        public void SlowTick()
        {
            if ((_survivalSystem == null || _playerMovement == null) && Time.time >= _nextDependencyResolveTime)
            {
                _nextDependencyResolveTime = Time.time + 2f;
                TryResolveGameplayDependencies();
                SyncDependencySubscriptions();
            }

            if (!_healthO2EffectsEnabled || !_postProcessingEnabled) return;

            try
            {
                UpdateHealthPostProcessing();
            }
            catch (Exception)
            {
                LogHealthPostProcessingFailed();
                _healthO2EffectsEnabled = false;
            }

            try
            {
                UpdateO2PostProcessing();
            }
            catch (Exception)
            {
                LogO2PostProcessingFailed();
                _healthO2EffectsEnabled = false;
            }
        }

        // ═══ ISAVEABLE ═══

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDuplicateInstanceDetected()
        {
            Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMainCameraMissing()
        {
            Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVolumeMissing()
        {
            Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVignetteMissing()
        {
            Debug.LogWarning("[CameraJuiceSystem] Vignette override not found in Volume profile.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogChromaticAberrationMissing()
        {
            Debug.LogWarning("[CameraJuiceSystem] ChromaticAberration override not found in Volume profile.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthOfFieldMissing()
        {
            Debug.LogWarning("[CameraJuiceSystem] DepthOfField override not found in Volume profile.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeCalculationFailed()
        {
            Debug.LogError("[CameraJuiceSystem] Shake calculation failed.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullShakeProfile()
        {
            Debug.LogError("[CameraJuiceSystem] TriggerShake called with null profile.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeMaxDisplacementClamped(float maxDisplacement)
        {
            Debug.LogWarning($"[CameraJuiceSystem] ShakeProfile MaxDisplacement out of range [0, 1]: {maxDisplacement}. Clamping.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeDurationDefaulted(float duration)
        {
            Debug.LogWarning($"[CameraJuiceSystem] ShakeProfile Duration invalid: {duration}. Using default 0.5s.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullBiomeProfile()
        {
            Debug.LogWarning("[CameraJuiceSystem] TransitionToBiome called with null biome. Using default fallback.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFovCalculationFailed()
        {
            Debug.LogError("[CameraJuiceSystem] FOV calculation failed.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBiomeBlendFailed()
        {
            Debug.LogError("[CameraJuiceSystem] Biome blend failed.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInteractionFocusFailed()
        {
            Debug.LogError("[CameraJuiceSystem] Interaction focus calculation failed.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFrameBudgetExceeded()
        {
            Debug.LogWarning("[CameraJuiceSystem] Frame time exceeded budget.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHealthPostProcessingFailed()
        {
            Debug.LogError("[CameraJuiceSystem] Health post-processing failed.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogO2PostProcessingFailed()
        {
            Debug.LogError("[CameraJuiceSystem] O2 post-processing failed.");
        }

        public int SavePriority => 75;
        public int LoadPriority => 75;

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            // CameraJuiceSystem settings stored as public fields in SaveData
            // Note: SaveData uses public fields, not Set/Get methods
            // Add fields to SaveData.cs if not present:
            // public float cameraJuiceShakeIntensity = 1.0f;
            // public float cameraJuiceFOVIntensity = 1.0f;
            // public bool cameraJuiceMotionBlur = false;
            // public bool cameraJuiceChromaticAberration = true;
            // public bool cameraJuiceDepthOfField = true;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;

            // Load settings from SaveData public fields
            // Implementation pending SaveData field additions
        }

        // ═══ PUBLIC API ═══

        /// <summary>
        /// Applies the dispatcher-owned pause menu depth-of-field isolation weight.
        /// </summary>
        /// <param name="weight">Normalized pause menu focus weight.</param>
        public void ApplyPauseDepthOfFieldWeight(float weight)
        {
            _pauseDepthOfFieldWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Reclaims gameplay control from a cinematic wide FOV without a camera snap.
        /// </summary>
        public void BeginInputReclaimFov(float startFov, float durationSeconds)
        {
            if (!_fovEnabled || _mainCamera == null)
                return;

            _inputReclaimFovStart = Mathf.Clamp(startFov, MIN_FOV, MAX_FOV);
            _inputReclaimFovTarget = Mathf.Clamp(_baseFOV, MIN_FOV, MAX_FOV);
            _inputReclaimFovDuration = Mathf.Max(0.0001f, durationSeconds);
            _inputReclaimFovElapsed = 0f;
            _inputReclaimFovActive = true;
            _mainCamera.fieldOfView = _inputReclaimFovStart;
        }

        /// <summary>
        /// Trigger camera shake with specified profile and intensity scale.
        /// </summary>
        /// <param name="profile">Shake configuration profile</param>
        /// <param name="intensityScale">Intensity multiplier (default 1.0)</param>
        public void TriggerShake(ShakeProfile profile, float intensityScale = 1.0f)
        {
            if (profile == null)
            {
                LogNullShakeProfile();
                return;
            }

            if (!_shakeEnabled) return;

            // Validate profile parameters
            float maxDisplacement = profile.MaxDisplacement;
            if (maxDisplacement < 0f || maxDisplacement > 1f)
            {
                LogShakeMaxDisplacementClamped(maxDisplacement);
                maxDisplacement = Mathf.Clamp(maxDisplacement, 0f, 1f);
            }

            float duration = profile.Duration;
            if (duration <= 0f)
            {
                LogShakeDurationDefaulted(duration);
                duration = 0.5f;
            }

            // Check capacity
            if (_activeShakes.Count >= MAX_ACTIVE_SHAKES)
            {
                _activeShakes.RemoveAt(0);
            }

            // Create new shake
            ActiveShake shake = new ActiveShake
            {
                Profile = profile,
                Elapsed = 0f,
                IntensityScale = intensityScale,
                Offset = Vector3.zero
            };

            _activeShakes.Add(shake);
        }

        /// <summary>
        /// Trigger FOV kick effect.
        /// </summary>
        /// <param name="amount">FOV offset amount in degrees</param>
        /// <param name="duration">Transition duration in seconds</param>
        public void TriggerFOVKick(float amount, float duration)
        {
            if (!_fovEnabled) return;

            _fovBlendStart = _currentFOVOffset;
            _fovBlendTarget = amount;
            _fovBlendDuration = Mathf.Max(0.0001f, duration);
            _fovBlendElapsed = 0f;
            _fovBlendActive = true;
        }

        /// <summary>
        /// Transition to new biome post-processing profile.
        /// </summary>
        /// <param name="biome">Target biome profile</param>
        /// <param name="blendDuration">Blend duration in seconds</param>
        public void TransitionToBiome(BiomeProfile biome, float blendDuration)
        {
            if (biome == null)
            {
                LogNullBiomeProfile();
                // TODO: Use default fallback biome
                return;
            }

            if (!_postProcessingEnabled || _urpVolume == null) return;

            _targetBiome = biome;
            _biomeBlendFrom = _currentBiome ?? biome;
            _biomeBlendDuration = Mathf.Max(0.0001f, blendDuration);
            _biomeBlendElapsed = 0f;
            _biomeBlendActive = true;
            ApplyBiomeBlend(_biomeBlendFrom, _targetBiome, 0f);
        }

        private static float EvaluateEaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse;
        }

        private static float EvaluateEaseInOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        }

        private void ApplyBiomeBlend(BiomeProfile from, BiomeProfile to, float t)
        {
            if (_urpVolume == null || _urpVolume.profile == null) return;

            // Blend color grading
            if (_urpVolume.profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments.colorFilter.value = Color.Lerp(from.ColorFilter, to.ColorFilter, t);
                // Note: Temperature and Tint require WhiteBalance component
            }

            // Blend bloom
            if (_urpVolume.profile.TryGet(out Bloom bloom))
            {
                bloom.intensity.value = Mathf.Lerp(from.BloomIntensity, to.BloomIntensity, t);
                bloom.threshold.value = Mathf.Lerp(from.BloomThreshold, to.BloomThreshold, t);
            }

            // Note: AO and Fog require additional URP components
        }

        // ═══ READ-ONLY PROPERTIES ═══

        public int ActiveShakeCount => _activeShakes.Count;
        public float CurrentFOVOffset => _currentFOVOffset;
        public FOVState CurrentFOVState => _fovState;
        public bool IsPostProcessingEnabled => _postProcessingEnabled;
        internal float DebugAdaptiveShakeScale => _adaptiveShakeScale;
        internal float DebugAdaptiveFOVScale => _adaptiveFOVScale;
        internal float DebugAdaptivePostFxScale => _adaptivePostFxScale;
        internal int DebugAdaptiveMaxActiveShakes => _adaptiveMaxActiveShakes;
        internal bool DebugAdaptiveDisableInteractionDoF => _adaptiveDisableInteractionDoF;

        // ═══ PRIVATE METHODS ═══

        private void HandleSprintStarted()
        {
            if (!_sprintFOVEnabled) return;
            TriggerFOVKick(10f, 0.3f);
        }

        private void HandleSprintEnded()
        {
            if (!_sprintFOVEnabled) return;
            TriggerFOVKick(0f, 0.3f);
        }

        private void HandleIntegrityChanged(float integrity)
        {
            // Calculate damage amount (assuming integrity is 0-1 normalized)
            // Trigger FOV recoil proportional to damage
            float damageAmount = Mathf.Max(0f, 1f - integrity);
            if (damageAmount > 0.1f)
            {
                float fovReduction = -5f * damageAmount;
                TriggerFOVKick(fovReduction, 0.2f);
            }
        }

        private void HandleOxygenCritical(float o2Normalized)
        {
            // Handled in UpdateO2PostProcessing via SlowTick
        }

        private void HandleHoverChanged(IInteractable target)
        {
            _focusTarget = target;
            _focusTargetTransform = null;

            if (target is Component component)
            {
                _focusTargetTransform = component.transform;
            }
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if ((InteractionEventType)payload.EventType != InteractionEventType.HoverChanged)
                return;

            InteractionEvents.TryResolveTarget(in payload, out IInteractable target);
            HandleHoverChanged(target);
        }

        private void UpdateShake(float dt)
        {
            if (!_shakeEnabled) return;

            float effectiveShakeScale = _shakeIntensityMultiplier * _adaptiveShakeScale;
            if (effectiveShakeScale <= 0f)
            {
                _shakeOffset = Vector3.zero;
                _shakeJobApplyResult = false;
                return;
            }

            _shakeNoiseTime += Mathf.Max(0f, dt);

            int count = _activeShakes.Count;
            if (count <= 0)
            {
                _shakeOffset = Vector3.zero;
                _shakeJobApplyResult = false;
                return;
            }

            if (_shakeJobScheduled || !EnsureShakeJobBuffers())
                return;

            int liveCount = 0;
            for (int i = count - 1; i >= 0; i--)
            {
                ActiveShake shake = _activeShakes[i];
                if (shake.Profile == null)
                {
                    _activeShakes.RemoveAt(i);
                    continue;
                }

                shake.Elapsed += dt;

                if (shake.Elapsed >= shake.Profile.Duration)
                {
                    _activeShakes.RemoveAt(i);
                    continue;
                }

                float t = shake.Elapsed / shake.Profile.Duration;
                _shakeJobInputs[liveCount++] = new ShakeJobInput
                {
                    NoiseTime = _shakeNoiseTime * shake.Profile.Frequency,
                    NoiseSeed = i * 17.31f + 3.17f,
                    MaxDisplacement = shake.Profile.MaxDisplacement,
                    Falloff = shake.Profile.FalloffCurve.Evaluate(t),
                    IntensityScale = shake.IntensityScale,
                    AxisWeights = new float3(
                        shake.Profile.AxisWeights.x,
                        shake.Profile.AxisWeights.y,
                        shake.Profile.AxisWeights.z)
                };

                _activeShakes[i] = shake;
            }

            if (liveCount <= 0)
            {
                _shakeOffset = Vector3.zero;
                _shakeJobApplyResult = false;
                return;
            }

            ScheduleShakeJob(
                liveCount,
                effectiveShakeScale,
                Mathf.Min(MAX_SHAKE_DISPLACEMENT, Mathf.Max(0.005f, _cameraShakeClipSafeDisplacement)));
        }

        private bool EnsureShakeJobBuffers()
        {
            if (!_shakeJobInputs.IsCreated)
            {
                _shakeJobInputs = new NativeArray<ShakeJobInput>(
                    MAX_ACTIVE_SHAKES,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<ShakeJobInput>[8] — Burst camera shake inputs — owner: CameraJuiceSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _shakeJobInputs,
                    nameof(CameraJuiceSystem),
                    nameof(_shakeJobInputs),
                    NativeAllocationLifetime.Scene);
            }

            if (!_shakeJobResults.IsCreated)
            {
                _shakeJobResults = new NativeArray<ShakeJobResult>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ShakeJobResult>[1] — Burst camera shake aggregate output — owner: CameraJuiceSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _shakeJobResults,
                    nameof(CameraJuiceSystem),
                    nameof(_shakeJobResults),
                    NativeAllocationLifetime.Scene);
            }

            return _shakeJobInputs.IsCreated && _shakeJobResults.IsCreated;
        }

        private void ScheduleShakeJob(int count, float effectiveShakeScale, float maxShakeDisplacement)
        {
            _shakeJobResults[0] = default;
            _shakeJobHandle = new ShakeAccumulationJob
            {
                Inputs = _shakeJobInputs,
                Results = _shakeJobResults,
                Count = count,
                EffectiveShakeScale = effectiveShakeScale,
                MaxShakeDisplacement = maxShakeDisplacement
            }.Schedule();
            _shakeJobScheduled = true;
            _shakeJobApplyResult = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void ResolveScheduledShakeJob(bool forceComplete)
        {
            if (!_shakeJobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _shakeJobHandle, forceComplete))
                return;

            _shakeJobScheduled = false;
            if (!_shakeJobApplyResult || !_shakeJobResults.IsCreated)
            {
                _shakeJobApplyResult = false;
                return;
            }

            ShakeJobResult result = _shakeJobResults[0];
            _shakeOffset = new Vector3(result.Offset.x, result.Offset.y, result.Offset.z);
            _shakeJobApplyResult = false;
        }

        private void ReleaseShakeJobBuffers()
        {
            if (_shakeJobInputs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_shakeJobInputs);
                if (_shakeJobScheduled)
                    _shakeJobInputs.Dispose(_shakeJobHandle);
                else
                    _shakeJobInputs.Dispose();

                _shakeJobInputs = default;
            }

            if (_shakeJobResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_shakeJobResults);
                if (_shakeJobScheduled)
                    _shakeJobResults.Dispose(_shakeJobHandle);
                else
                    _shakeJobResults.Dispose();

                _shakeJobResults = default;
            }

            _shakeJobScheduled = false;
            _shakeJobApplyResult = false;
            _shakeJobHandle = default;
        }

        private void ApplyPostAupShakeOffset()
        {
            if (_cameraTransform == null)
                return;

            _cameraTransform.localPosition = _cameraLocalRestPosition + ResolveClipSafeShakeOffset(_shakeOffset);
        }

        private Vector3 ResolveClipSafeShakeOffset(Vector3 offset)
        {
            float maxShakeDisplacement = Mathf.Min(
                MAX_SHAKE_DISPLACEMENT,
                Mathf.Max(0.005f, _cameraShakeClipSafeDisplacement));
            return offset.sqrMagnitude > maxShakeDisplacement * maxShakeDisplacement
                ? offset.normalized * maxShakeDisplacement
                : offset;
        }

        private void EnsureCameraSpeedLineParticles()
        {
            if (_speedLineParticles != null || _cameraTransform == null)
                return;

            int maxParticles = Mathf.Max(1, _speedLineMaxParticles);
            // COLD ALLOC: GameObject[1] + ParticleSystem[1] - camera-local cinematic speed-line emitter - owner: CameraJuiceSystem
            GameObject speedLineObject = new GameObject("PFX_Camera_SpeedLines");
            speedLineObject.layer = TransparentFxLayerIndex;
            speedLineObject.transform.SetParent(_cameraTransform, false);
            speedLineObject.transform.localPosition = new Vector3(0f, 0f, 1.35f);
            speedLineObject.transform.localRotation = Quaternion.identity;
            speedLineObject.transform.localScale = Vector3.one;
            _speedLineRoot = speedLineObject.transform;
            _speedLineParticles = speedLineObject.AddComponent<ParticleSystem>();

            var main = _speedLineParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.56f, 0.82f, 1f, 0.12f),
                new Color(0.86f, 1f, 1f, 0.34f));

            var emission = _speedLineParticles.emission;
            emission.rateOverTime = 0f;

            var shape = _speedLineParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 0f, 5.5f);
            shape.scale = new Vector3(7.5f, 4.2f, 8f);

            var velocity = _speedLineParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-18f, -28f);

            _speedLineRenderer = _speedLineParticles.GetComponent<ParticleSystemRenderer>();
            if (_speedLineRenderer != null)
            {
                _speedLineRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                _speedLineRenderer.lengthScale = 0.45f;
                _speedLineRenderer.velocityScale = 0.08f;
                _speedLineRenderer.cameraVelocityScale = 0f;
                _speedLineRenderer.enableGPUInstancing = true;
                _speedLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _speedLineRenderer.receiveShadows = false;
                if (_speedLineMaterial != null)
                    _speedLineRenderer.sharedMaterial = _speedLineMaterial;
            }
        }

        private void UpdateCameraSpeedLines(float dt)
        {
            if (_speedLineParticles == null)
                EnsureCameraSpeedLineParticles();
            if (_speedLineParticles == null)
                return;

            float currentSpeed = ResolveCurrentCameraSpeed();
            float speed01 = Mathf.Clamp01(
                (currentSpeed - Mathf.Max(0f, _speedLineStartMetersPerSecond)) /
                Mathf.Max(0.01f, _speedLineFullMetersPerSecond - _speedLineStartMetersPerSecond));
            speed01 = speed01 * speed01 * (3f - 2f * speed01);
            float blend = Mathf.Clamp01(1f - Mathf.Exp(-8f * Mathf.Max(0f, dt)));
            _speedLineIntensity = Mathf.Lerp(_speedLineIntensity, speed01, blend);

            var emission = _speedLineParticles.emission;
            float emissionRate = Mathf.Lerp(0f, Mathf.Max(1f, _speedLineMaxEmissionRate), _speedLineIntensity);
            if (Mathf.Abs(_cachedSpeedLineEmissionRate - emissionRate) > 0.5f)
            {
                emission.rateOverTime = emissionRate;
                _cachedSpeedLineEmissionRate = emissionRate;
            }

            var velocity = _speedLineParticles.velocityOverLifetime;
            float velocityZ = -Mathf.Lerp(18f, 44f, _speedLineIntensity);
            if (Mathf.Abs(_cachedSpeedLineVelocityZ - velocityZ) > 0.25f)
            {
                velocity.z = new ParticleSystem.MinMaxCurve(velocityZ * 0.64f, velocityZ);
                _cachedSpeedLineVelocityZ = velocityZ;
            }

            if (_speedLineRenderer != null)
            {
                float stretch = Mathf.Lerp(0.45f, Mathf.Max(0.45f, _speedLineMaxStretch), _speedLineIntensity);
                if (Mathf.Abs(_cachedSpeedLineStretch - stretch) > 0.02f)
                {
                    _speedLineRenderer.lengthScale = stretch;
                    _cachedSpeedLineStretch = stretch;
                }
            }

            if (_speedLineIntensity > 0.01f)
            {
                if (!_speedLineParticles.isPlaying)
                    _speedLineParticles.Play(true);
            }
            else if (_speedLineParticles.isPlaying)
            {
                _speedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private float ResolveCurrentCameraSpeed()
        {
            float speed = 0f;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            if (playerBody != null)
                speed = Mathf.Max(speed, playerBody.linearVelocity.magnitude);

            ISubmarineRuntimeContext submarineContext = GlobalRegistry.Submarine;
            Rigidbody submarineBody = submarineContext != null ? submarineContext.HullRigidbody : null;
            if (submarineBody != null)
                speed = Mathf.Max(speed, submarineBody.linearVelocity.magnitude);

            return float.IsFinite(speed) ? speed : 0f;
        }

        private void StopCameraSpeedLineParticles()
        {
            _speedLineIntensity = 0f;
            _cachedSpeedLineEmissionRate = -1f;
            _cachedSpeedLineVelocityZ = float.MinValue;
            _cachedSpeedLineStretch = -1f;
            if (_speedLineParticles != null)
                _speedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void UpdateFOV(float dt)
        {
            if (!_fovEnabled) return;

            if (_fovBlendActive)
            {
                _fovBlendElapsed = Mathf.Min(_fovBlendElapsed + dt, _fovBlendDuration);
                float normalizedBlend = Mathf.Clamp01(_fovBlendElapsed / _fovBlendDuration);
                float easedBlend = EvaluateEaseOutQuad(normalizedBlend);
                _currentFOVOffset = Mathf.Lerp(_fovBlendStart, _fovBlendTarget, easedBlend);
                if (normalizedBlend >= 1f)
                {
                    _currentFOVOffset = _fovBlendTarget;
                    _fovBlendActive = false;
                }
            }

            // Calculate target FOV
            float targetFOV = _baseFOV + (_currentFOVOffset * _fovIntensityMultiplier * _adaptiveFOVScale);
            targetFOV = Mathf.Clamp(targetFOV, MIN_FOV, MAX_FOV);

            if (_inputReclaimFovActive)
            {
                _inputReclaimFovElapsed = Mathf.Min(_inputReclaimFovElapsed + Mathf.Max(0f, dt), _inputReclaimFovDuration);
                float normalizedReclaim = Mathf.Clamp01(_inputReclaimFovElapsed / _inputReclaimFovDuration);
                float easedReclaim = EvaluateEaseOutQuad(normalizedReclaim);
                targetFOV = Mathf.Lerp(_inputReclaimFovStart, _inputReclaimFovTarget, easedReclaim);
                if (normalizedReclaim >= 1f)
                    _inputReclaimFovActive = false;
            }

            // Apply to camera
            _mainCamera.fieldOfView = targetFOV;
        }

        private void UpdateBiomeBlend(float dt)
        {
            if (!_biomeBlendActive)
                return;

            _biomeBlendElapsed = Mathf.Min(_biomeBlendElapsed + dt, _biomeBlendDuration);
            float normalizedBlend = Mathf.Clamp01(_biomeBlendElapsed / _biomeBlendDuration);
            float easedBlend = EvaluateEaseInOutQuad(normalizedBlend);
            ApplyBiomeBlend(_biomeBlendFrom, _targetBiome, easedBlend);
            if (normalizedBlend >= 1f)
            {
                _currentBiome = _targetBiome;
                _biomeBlendActive = false;
            }
        }

        private void UpdateInteractionFocus(float dt)
        {
            if (!_depthOfFieldEnabled || !_postProcessingEnabled) return;
            if (_interactionDoF == null) return;

            // Performance mode check
            if (_adaptiveDisableInteractionDoF)
            {
                _interactionDoF.active = false;
                return;
            }

            EnsureFocusRaycastBuffers();

            float targetFocusDistance = ResolveTargetFocusDistance();
            _focusDistance = Mathf.SmoothDamp(
                _focusDistance > 0f ? _focusDistance : targetFocusDistance,
                targetFocusDistance,
                ref _focusDistanceVelocity,
                Mathf.Max(0.02f, _focusTransitionDuration),
                Mathf.Infinity,
                dt);

            _interactionDoF.focusDistance.value = _focusDistance;
            _interactionDoF.active = true;
            ScheduleFocusRaycast();
        }

        private void UpdatePauseDepthOfField(float dt)
        {
            if (!_depthOfFieldEnabled || !_postProcessingEnabled) return;
            if (_interactionDoF == null) return;

            if (_pauseDepthOfFieldWeight <= 0f)
            {
                if (_pauseDofOverrideEngaged)
                    RestorePauseDofOpticalDefaults();

                _pauseDofOverrideEngaged = false;
                _pauseDofDefaultsCaptured = false;
                return;
            }

            if (!_pauseDofOverrideEngaged)
            {
                CapturePauseDofOpticalDefaults();
                _pauseDofOverrideEngaged = true;
            }

            float easedBlend = EvaluateEaseInOutQuad(_pauseDepthOfFieldWeight);
            float baseFocusDistance = _focusDistance > 0f ? _focusDistance : _worldFocusDistance;
            _interactionDoF.focusDistance.value = Mathf.Lerp(
                baseFocusDistance,
                PauseDofFocusDistance,
                easedBlend);
            _interactionDoF.focalLength.value = Mathf.Lerp(_pauseDofBaseFocalLength, PauseDofFocalLength, easedBlend);
            _interactionDoF.aperture.value = Mathf.Lerp(_pauseDofBaseAperture, PauseDofAperture, easedBlend);
            _interactionDoF.gaussianEnd.value = Mathf.Lerp(_pauseDofBaseGaussianEnd, PauseDofGaussianEnd, easedBlend);
            _interactionDoF.gaussianMaxRadius.value = Mathf.Lerp(_pauseDofBaseGaussianMaxRadius, PauseDofGaussianMaxRadius, easedBlend);
            _interactionDoF.active = true;
        }

        private void CapturePauseDofOpticalDefaults()
        {
            if (_interactionDoF == null || _pauseDofDefaultsCaptured)
                return;

            _pauseDofBaseFocalLength = _interactionDoF.focalLength.value;
            _pauseDofBaseAperture = _interactionDoF.aperture.value;
            _pauseDofBaseGaussianEnd = _interactionDoF.gaussianEnd.value;
            _pauseDofBaseGaussianMaxRadius = _interactionDoF.gaussianMaxRadius.value;
            _pauseDofDefaultsCaptured = true;
        }

        private void RestorePauseDofOpticalDefaults()
        {
            if (_interactionDoF == null || !_pauseDofDefaultsCaptured)
                return;

            _interactionDoF.focalLength.value = _pauseDofBaseFocalLength;
            _interactionDoF.aperture.value = _pauseDofBaseAperture;
            _interactionDoF.gaussianEnd.value = _pauseDofBaseGaussianEnd;
            _interactionDoF.gaussianMaxRadius.value = _pauseDofBaseGaussianMaxRadius;
        }

        private void EnsureFocusRaycastBuffers()
        {
            if (_focusRaycastCommands.IsCreated && _focusRaycastHits.IsCreated)
                return;

            if (!_focusRaycastCommands.IsCreated)
            {
                _focusRaycastCommands = new NativeArray<RaycastCommand>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[1] — center-eye DoF focus ray lane — owner: CameraJuiceSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _focusRaycastCommands,
                    nameof(CameraJuiceSystem),
                    nameof(_focusRaycastCommands),
                    NativeAllocationLifetime.Scene);
            }

            if (!_focusRaycastHits.IsCreated)
            {
                _focusRaycastHits = new NativeArray<RaycastHit>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[1] — center-eye DoF focus hit lane — owner: CameraJuiceSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _focusRaycastHits,
                    nameof(CameraJuiceSystem),
                    nameof(_focusRaycastHits),
                    NativeAllocationLifetime.Scene);
            }
        }

        private void ReleaseFocusRaycastBuffers()
        {
            if (_focusRaycastCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_focusRaycastCommands);
                if (_focusRaycastScheduled)
                    _focusRaycastCommands.Dispose(_focusRaycastHandle);
                else
                    _focusRaycastCommands.Dispose();

                _focusRaycastCommands = default;
            }

            if (_focusRaycastHits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_focusRaycastHits);
                if (_focusRaycastScheduled)
                    _focusRaycastHits.Dispose(_focusRaycastHandle);
                else
                    _focusRaycastHits.Dispose();

                _focusRaycastHits = default;
            }

            _focusRaycastScheduled = false;
            _focusRaycastHandle = default;
        }

        private void ResolveScheduledFocusRaycast()
        {
            if (!_focusRaycastScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _focusRaycastHandle, false))
                return;

            _focusRaycastScheduled = false;

            RaycastHit hit = _focusRaycastHits[0];
            if (hit.collider != null && hit.distance > 0f)
            {
                _resolvedFocusDistance = hit.distance;
                return;
            }

            _resolvedFocusDistance = ResolveHudPlaneFocusDistance();
        }

        private float ResolveTargetFocusDistance()
        {
            if (PlayerPDA.IsOpen)
                return Mathf.Max(0.01f, _pdaFocusDistance);

            if (HectonFabricatorUI.IsMenuOpen)
            {
                float focusCandidate = _focusRaycastScheduled
                    ? _resolvedFocusDistance
                    : (_focusTargetTransform != null
                        ? ResolveAupRuntimeDistance(_cameraTransform.position, _focusTargetTransform.position)
                        : ResolveHudPlaneFocusDistance());

                if (focusCandidate <= Mathf.Max(0.01f, _pdaFocusThreshold))
                    return Mathf.Max(0.01f, _pdaFocusDistance);

                return Mathf.Max(0.01f, _worldFocusDistance);
            }

            if (_focusRaycastScheduled)
                return Mathf.Max(0.01f, _resolvedFocusDistance);

            if (_focusTargetTransform != null)
                return Mathf.Max(0.01f, ResolveAupRuntimeDistance(_cameraTransform.position, _focusTargetTransform.position));

            return Mathf.Max(0.01f, ResolveHudPlaneFocusDistance());
        }

        private static float ResolveAupRuntimeDistance(Vector3 fromRuntimePosition, Vector3 toRuntimePosition)
        {
            AbsoluteUniversePosition fromAup = AbsoluteUniversePosition.FromRuntimePosition(fromRuntimePosition);
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntimePosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in fromAup, in toAup);
            if (distanceSq <= 0d)
                return 0f;

            double distance = Math.Sqrt(distanceSq);
            return distance >= float.MaxValue ? float.MaxValue : (float)distance;
        }

        private float ResolveHudPlaneFocusDistance()
        {
            if (_cameraTransform == null)
                return Mathf.Max(0.01f, _hudFocusDistance);

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay == null || overlay.TargetCanvas == null)
                return Mathf.Max(0.01f, _hudFocusDistance);

            RectTransform canvasRect = overlay.TargetCanvas.transform as RectTransform;
            if (canvasRect == null)
                return Mathf.Max(0.01f, _hudFocusDistance);

            float planeDistance = Vector3.Dot(_cameraTransform.forward, canvasRect.position - _cameraTransform.position);
            return Mathf.Max(0.01f, planeDistance > 0f ? planeDistance : _hudFocusDistance);
        }

        private void ScheduleFocusRaycast()
        {
            if (_focusRaycastScheduled || _cameraTransform == null || !_focusRaycastCommands.IsCreated || !_focusRaycastHits.IsCreated)
                return;

            _focusRaycastCommands[0] = CreateFocusRaycastCommand(
                _cameraTransform.position,
                _cameraTransform.forward,
                Mathf.Max(0.1f, _interactionFocusRayDistance),
                _interactionFocusMask);
            _focusRaycastHandle = RaycastCommand.ScheduleBatch(_focusRaycastCommands, _focusRaycastHits, 1, default);
            _focusRaycastScheduled = true;
        }

        private static RaycastCommand CreateFocusRaycastCommand(
            Vector3 origin,
            Vector3 direction,
            float range,
            int layerMask)
        {
            return new RaycastCommand
            {
                from = origin,
                direction = direction,
                distance = range,
                queryParameters = new QueryParameters
                {
                    layerMask = layerMask,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };
        }

        private bool TryResolveCamera()
        {
            _mainCamera = _cameraReference;

            if (_mainCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _mainCamera = playerContext.PlayerCamera;

                if (_mainCamera == null &&
                    SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerTransform.TryGetComponent(out _mainCamera);
                }

                if (_mainCamera == null && !TryGetComponent(out _mainCamera))
                    _mainCamera = null;

                if (_mainCamera == null)
                {
                    _mainCamera = GetComponentInParent<Camera>();
                }

            }

            if (_mainCamera == null)
            {
                return false;
            }

            _cameraTransform = _mainCamera.transform;
            _cameraLocalRestPosition = _cameraTransform.localPosition;
            _baseFOV = _mainCamera.fieldOfView;
            return true;
        }

        private void TryResolveVolume()
        {
            _urpVolume = _volumeReference;

            if (_urpVolume == null)
            {
                TryGetComponent(out _urpVolume);
            }

            if (_urpVolume == null && _mainCamera != null)
            {
                _mainCamera.TryGetComponent(out _urpVolume);
            }

            if (_urpVolume == null && _cameraTransform != null)
            {
                _urpVolume = _cameraTransform.GetComponentInParent<Volume>();
            }
        }

        private void TryResolveGameplayDependencies()
        {
            Transform playerRoot = null;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                playerRoot = currentPlayerTransform;
            }

            if (_survivalSystem == null)
            {
                _survivalSystem = _survivalSystemReference;

                if (_survivalSystem == null && _cameraTransform != null)
                {
                    _survivalSystem = _cameraTransform.GetComponentInParent<HectonSurvivalSystem>();
                }

                if (_survivalSystem == null && playerRoot != null)
                {
                    playerRoot.TryGetComponent(out _survivalSystem);
                }
            }

            if (_playerMovement == null)
            {
                _playerMovement = _playerMovementReference;

                if (_playerMovement == null && _cameraTransform != null)
                {
                    _playerMovement = _cameraTransform.GetComponentInParent<HectonPlayerMovement>();
                }

                if (_playerMovement == null && playerRoot != null)
                {
                    playerRoot.TryGetComponent(out _playerMovement);
                }
            }

            SyncDependencyFlags();
        }

        private void SyncDependencyFlags()
        {
            _healthO2EffectsEnabled = _survivalSystem != null;
            _sprintFOVEnabled = _playerMovement != null;
        }

        private void SyncDependencySubscriptions()
        {
            if (_survivalSystem != null && !_survivalEventsHooked)
            {
                _survivalSystem.OnIntegrityChanged += HandleIntegrityChanged;
                _survivalSystem.OnOxygenCritical += HandleOxygenCritical;
                _survivalEventsHooked = true;
            }

            if (_playerMovement != null && !_movementEventsHooked)
            {
                _playerMovement.OnSprintStarted += HandleSprintStarted;
                _playerMovement.OnSprintEnded += HandleSprintEnded;
                _movementEventsHooked = true;
            }
        }

        private void UnhookDependencyEvents()
        {
            if (_survivalSystem != null && _survivalEventsHooked)
            {
                _survivalSystem.OnIntegrityChanged -= HandleIntegrityChanged;
                _survivalSystem.OnOxygenCritical -= HandleOxygenCritical;
                _survivalEventsHooked = false;
            }

            if (_playerMovement != null && _movementEventsHooked)
            {
                _playerMovement.OnSprintStarted -= HandleSprintStarted;
                _playerMovement.OnSprintEnded -= HandleSprintEnded;
                _movementEventsHooked = false;
            }
        }

        private void UpdateHealthPostProcessing()
        {
            if (_survivalSystem == null || _healthVignette == null) return;

            float healthNormalized = _survivalSystem.IntegrityNormalized;

            if (healthNormalized < 0.3f)
            {
                // Calculate vignette intensity
                float intensity = Mathf.Lerp(0f, 1f, (0.3f - healthNormalized) / 0.3f) * _adaptivePostFxScale;

                // Direct Volume override assignment (not renderer-based, no MaterialPropertyBlock needed)
                _healthVignette.intensity.value = intensity;
                _healthVignette.active = intensity > 0.001f;
            }
            else
            {
                _healthVignette.intensity.value = 0f;
                _healthVignette.active = false;
            }
        }

        private void UpdateO2PostProcessing()
        {
            if (_o2ChromaticAberration == null) return;
            if (!_chromaticAberrationEnabled)
            {
                _o2ChromaticAberration.intensity.value = 0f;
                _o2ChromaticAberration.active = false;
                return;
            }

            float o2Normalized = _survivalSystem != null ? _survivalSystem.OxygenNormalized : 1f;
            float oxygenIntensity = 0f;
            if (o2Normalized < 0.2f)
                oxygenIntensity = Mathf.Lerp(0f, 0.8f, (0.2f - o2Normalized) / 0.2f);

            float structuralFatigueIntensity = ResolveStructuralFatigueChromaticContribution();
            float intensity = Mathf.Max(oxygenIntensity, structuralFatigueIntensity) * _adaptivePostFxScale;

            if (intensity > 0.001f)
            {
                // Direct Volume override assignment (not renderer-based, no MaterialPropertyBlock needed)
                _o2ChromaticAberration.intensity.value = intensity;
                _o2ChromaticAberration.active = intensity > 0.001f;
            }
            else
            {
                _o2ChromaticAberration.intensity.value = 0f;
                _o2ChromaticAberration.active = false;
            }
        }

        private float ResolveStructuralFatigueChromaticContribution()
        {
            ISubmarineRuntimeContext submarineRuntimeContext = GlobalRegistry.Submarine;
            SubmarineStructuralGrid structuralGrid = submarineRuntimeContext != null ? submarineRuntimeContext.StructuralGrid : null;
            if (structuralGrid == null || !structuralGrid.isActiveAndEnabled || !structuralGrid.IsReady)
                return 0f;

            return Mathf.Clamp01(structuralGrid.FatiguePeakNormalized) * Mathf.Clamp01(_structuralFatigueChromaticAberrationMax);
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!_enableAdaptiveBudgetResponse || !Application.isPlaying)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f, VRAMMonitor.VRAMPressureState.Stable);
                return;
            }

            float renderScale = 1f;
            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
            if (scaler != null && scaler.Enabled)
            {
                renderScale = Mathf.Clamp01(scaler.CurrentRenderScale);
            }

            float normalized = Mathf.Clamp01(
                (renderScale - _adaptiveBudgetFloorRenderScale) /
                Mathf.Max(0.0001f, 1f - _adaptiveBudgetFloorRenderScale));

            VRAMMonitor vramMonitor = Hecton8.Core.GlobalRegistry.VRAMMonitor;
            VRAMMonitor.VRAMPressureState pressureState = vramMonitor != null
                ? vramMonitor.PressureState
                : VRAMMonitor.VRAMPressureState.Stable;

            ApplyAdaptiveBudgetResponse(renderScale, normalized, pressureState);
        }

        private void ApplyAdaptiveBudgetResponse(
            float renderScale,
            float normalized,
            VRAMMonitor.VRAMPressureState pressureState)
        {
            _adaptiveRenderScale = renderScale;
            _adaptiveBudgetNormalized = normalized;
            _adaptiveVRAMPressureState = pressureState;
            _adaptiveShakeScale = Mathf.Lerp(_adaptiveShakeFloor, 1f, normalized);
            _adaptiveFOVScale = Mathf.Lerp(_adaptiveFOVFloor, 1f, normalized);
            _adaptivePostFxScale = Mathf.Lerp(_adaptivePostFxFloor, 1f, normalized);
            _adaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;

            switch (pressureState)
            {
                case VRAMMonitor.VRAMPressureState.Critical:
                    _adaptiveShakeScale *= _adaptiveVRAMCriticalShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMCriticalPostFxScale;
                    _adaptiveMaxActiveShakes = Mathf.Clamp(_adaptiveCriticalMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;

                case VRAMMonitor.VRAMPressureState.Warning:
                    _adaptiveShakeScale *= _adaptiveVRAMWarningShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMWarningPostFxScale;
                    _adaptiveMaxActiveShakes = Mathf.Clamp(_adaptiveWarningMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;
            }

            _adaptiveDisableInteractionDoF =
                QualitySettings.GetQualityLevel() == 0 ||
                !_depthOfFieldEnabled ||
                _adaptivePostFxScale <= _adaptiveDoFDisableThreshold;

            while (_activeShakes.Count > _adaptiveMaxActiveShakes)
            {
                _activeShakes.RemoveAt(0);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugAdaptiveRenderScale = renderScale;
            _debugAdaptiveBudgetNormalized = normalized;
            _debugAdaptiveShakeScale = _adaptiveShakeScale;
            _debugAdaptiveFOVScale = _adaptiveFOVScale;
            _debugAdaptivePostFxScale = _adaptivePostFxScale;
            _debugAdaptiveVRAMPressure = ResolveDebugPressureLabel(pressureState);
            _debugAdaptiveMaxActiveShakes = _adaptiveMaxActiveShakes;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ResolveDebugPressureLabel(VRAMMonitor.VRAMPressureState pressureState)
        {
            switch (pressureState)
            {
                case VRAMMonitor.VRAMPressureState.Critical:
                    return DebugPressureCritical;

                case VRAMMonitor.VRAMPressureState.Warning:
                    return DebugPressureWarning;

                default:
                    return DebugPressureStable;
            }
        }
#endif

        // ═══ EDITOR GIZMOS ═══

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_mainCamera == null || _cameraTransform == null) return;

            // Shake displacement vector
            if (_shakeOffset.magnitude > 0.001f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_cameraTransform.position, _cameraTransform.position + _shakeOffset);
                Gizmos.DrawWireSphere(_cameraTransform.position + _shakeOffset, 0.05f);
            }

            // FOV cone visualization
            if (_currentFOVOffset != 0f)
            {
                Gizmos.color = Color.yellow;
                float currentFOV = _mainCamera.fieldOfView;
                float coneDistance = 5f;
                float coneRadius = Mathf.Tan(currentFOV * 0.5f * Mathf.Deg2Rad) * coneDistance;
                
                Vector3 forward = _cameraTransform.forward * coneDistance;
                Vector3 right = _cameraTransform.right * coneRadius;
                Vector3 up = _cameraTransform.up * coneRadius;
                
                Vector3 topLeft = _cameraTransform.position + forward + up - right;
                Vector3 topRight = _cameraTransform.position + forward + up + right;
                Vector3 bottomLeft = _cameraTransform.position + forward - up - right;
                Vector3 bottomRight = _cameraTransform.position + forward - up + right;
                
                Gizmos.DrawLine(_cameraTransform.position, topLeft);
                Gizmos.DrawLine(_cameraTransform.position, topRight);
                Gizmos.DrawLine(_cameraTransform.position, bottomLeft);
                Gizmos.DrawLine(_cameraTransform.position, bottomRight);
                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
            }

            // Depth-of-field focus distance
            if (_focusTargetTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_cameraTransform.position, _focusTargetTransform.position);
                Gizmos.DrawWireSphere(_focusTargetTransform.position, 0.2f);
            }
        }
#endif

        // ═══ NESTED TYPES ═══

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ShakeAccumulationJob : IJob
        {
            [ReadOnly]
            public NativeArray<ShakeJobInput> Inputs;
            public NativeArray<ShakeJobResult> Results;
            public int Count;
            public float EffectiveShakeScale;
            public float MaxShakeDisplacement;

            public void Execute()
            {
                float3 offset = float3.zero;
                int count = math.clamp(Count, 0, Inputs.Length);
                for (int i = 0; i < count; i++)
                {
                    ShakeJobInput input = Inputs[i];
                    float noiseX = noise.snoise(new float2(input.NoiseTime, input.NoiseSeed));
                    float noiseY = noise.snoise(new float2(input.NoiseSeed, input.NoiseTime));
                    float noiseZ = noise.snoise(new float2(input.NoiseTime + input.NoiseSeed, input.NoiseTime - input.NoiseSeed));
                    float heaveNoise = noise.snoise(new float2(
                        input.NoiseTime * LOW_FREQUENCY_HEAVE_NOISE_SCALE,
                        input.NoiseSeed + 17.13f));
                    float scale = input.MaxDisplacement * input.Falloff * input.IntensityScale * EffectiveShakeScale;
                    offset += new float3(
                        noiseX * input.AxisWeights.x,
                        noiseY * input.AxisWeights.y,
                        noiseZ * input.AxisWeights.z) * scale;
                    offset.y += heaveNoise * input.AxisWeights.y * scale * LOW_FREQUENCY_HEAVE_DISPLACEMENT_SCALE;
                }

                float maxShakeDisplacement = math.max(0.0001f, MaxShakeDisplacement);
                float maxShakeDisplacementSq = maxShakeDisplacement * maxShakeDisplacement;
                float offsetSq = math.lengthsq(offset);
                if (offsetSq > maxShakeDisplacementSq)
                    offset = math.normalizesafe(offset, float3.zero) * maxShakeDisplacement;

                Results[0] = new ShakeJobResult { Offset = offset };
            }
        }

        private struct ActiveShake
        {
            public ShakeProfile Profile;
            public float Elapsed;
            public float IntensityScale;
            public Vector3 Offset;
        }
    }
}
