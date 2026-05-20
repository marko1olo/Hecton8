// ============================================================================
// HECTON-8 — CameraJuiceSystem.cs
// Camera shake, FOV effects, and post-processing modulation system.
// Zero-GC hot paths, 1.0ms frame budget, native tick-driven transitions.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using CameraJuiceImpactSignal = Hecton8.Core.Contracts.Signals.CameraJuiceImpactSignal;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
    public sealed class CameraJuiceSystem : MonoBehaviour, ICameraJuiceSystem, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, ISaveable, IInteractionEventListener, IPhysicsImpactEventListener, ICombatDamageEventListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IScalabilityChangedEventListener
    {
        // ═══ CACHED REFERENCES ═══
        [StructLayout(LayoutKind.Explicit, Size = CameraJuiceTelemetryEntrySizeBytes)]
        private struct CameraJuiceTelemetryEntry
        {
            [FieldOffset(0)] public int Frame;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public float Trauma;
            [FieldOffset(12)] public float FovKick;
            [FieldOffset(16)] public float3 Offset;
            [FieldOffset(28)] public float3 RotationDegrees;
            [FieldOffset(40)] public float RollDegrees;
            [FieldOffset(44)] public float DirectionalBiasTimer;
            [FieldOffset(48)] public float AdaptiveShakeScale;
            [FieldOffset(52)] public float Reserved0;
            [FieldOffset(56)] public float Reserved1;
            [FieldOffset(60)] public float Reserved2;
        }

        private Camera _mainCamera;
        private Volume _urpVolume;
        private Transform _cameraTransform;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private Rigidbody _playerRigidbody;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private Rigidbody _submarineHullRigidbody;
        private SubmarineStructuralGrid _submarineStructuralGrid;
        private DynamicResolutionScaler _dynamicResolutionScaler;
        private VRAMMonitor _vramMonitor;
        private ITickDispatcher _dispatcher;
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
        private const int MAX_ACTIVE_SHAKES = 8;
        private const float MAX_SHAKE_DISPLACEMENT = 0.5f;
        private const float DEFAULT_SHAKE_CLIP_SAFE_DISPLACEMENT = 0.05f;
        private const float PROCEDURAL_TRAUMA_DECAY_RATE = 1.65f;
        private const float PROCEDURAL_SHAKE_FREQUENCY = 18f;
        private const float PROCEDURAL_LOW_TIER_SAMPLE_INTERVAL = 0.033333334f;
        private const float PROCEDURAL_TRANSLATION_AMPLITUDE_METERS = 0.07f;
        private const float PROCEDURAL_ROTATION_AMPLITUDE_DEGREES = 2.4f;
        private const float PROCEDURAL_ROLL_AMPLITUDE_DEGREES = 5.5f;
        private const float PROCEDURAL_DIRECTIONAL_BIAS_SECONDS = 0.06f;
        private const float PROCEDURAL_ROLL_SPRING = 44f;
        private const float PROCEDURAL_ROLL_DAMPING = 10f;
        private const float PROCEDURAL_HIT_STOP_THRESHOLD = 0.8f;
        private const int PROCEDURAL_MAX_IMPACTS_PER_FRAME = 32;
        private const int CAMERA_JUICE_TELEMETRY_CAPACITY = 300;
        private const int CameraJuiceTelemetryEntrySizeBytes = 64;
        private const uint CAMERA_JUICE_HIT_STOP_REASON_HASH = 0xC45A1CEu;
        private const float PROCEDURAL_NOISE_SEED_X = 11.137f;
        private const float PROCEDURAL_NOISE_SEED_Y = 23.719f;
        private const float PROCEDURAL_NOISE_SEED_Z = 37.031f;
        private const float PROCEDURAL_NOISE_SEED_PITCH = 43.661f;
        private const float PROCEDURAL_NOISE_SEED_YAW = 59.173f;
        private const float PROCEDURAL_NOISE_SEED_ROLL = 71.411f;
        private const float SEISMIC_CAMERA_DECAY_RATE = 3.4f;
        private const float SEISMIC_CAMERA_FREQUENCY = 21f;
        private const float SEISMIC_TRANSLATION_AMPLITUDE_METERS = 0.035f;
        private const float SEISMIC_ROTATION_AMPLITUDE_DEGREES = 0.85f;
        private float _shakeNoiseTime;
        private float _trauma;
        private float _proceduralShakeTime;
        private float _proceduralLowTierSampleTimer;
        private float3 _proceduralLowTierTranslationFrom;
        private float3 _proceduralLowTierTranslationTo;
        private float3 _proceduralLowTierRotationFrom;
        private float3 _proceduralLowTierRotationTo;
        private bool _proceduralLowTierNoisePrimed;
        private float3 _proceduralShakeTranslation;
        private float3 _proceduralShakeRotationDegrees;
        private float3 _directionalBiasLocal;
        private float _directionalBiasTimer;
        private float _proceduralRollDegrees;
        private float _proceduralRollVelocity;
        private bool _shakeRotationApplied;
        private Quaternion _lastShakeLocalRotation = Quaternion.identity;
        private Quaternion _lastShakeCompositeLocalRotation = Quaternion.identity;
        private IDataVault _dataVault;
        private VaultBufferHandle<CameraJuiceTelemetryEntry> _cameraJuiceTelemetryHandle;
        private int _cameraJuiceTelemetryCursor;
        private bool _cameraJuiceTelemetryDumped;
        private bool _cameraJuiceTelemetryReady;
        private float _submarineImpactShakeSign = 1f;
        private int _lastSeismicSignalSequence;
        private float _seismicJitterIntensity;
        private float _seismicJitterTime;
        private float3 _seismicDirectionLocal;
        private float3 _seismicShakeOffset;
        private float3 _seismicShakeRotationDegrees;

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
        private float _swimmingVelocityFovOffset;
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

        // ═══ SETTINGS ═══
        [Header("── Settings ──────────────────")]
        [SerializeField, Range(0f, 2f), Tooltip("Camera shake intensity multiplier (0 = off, 1 = default, 2 = double)")]
        private float _shakeIntensityMultiplier = 1.0f;

        [SerializeField, Range(0.005f, 0.2f), Tooltip("Maximum presentation-space shake offset allowed inside tight submarine interiors.")]
        private float _cameraShakeClipSafeDisplacement = DEFAULT_SHAKE_CLIP_SAFE_DISPLACEMENT;

        [SerializeField, Range(0f, 2f), Tooltip("FOV effects intensity multiplier (0 = off, 1 = default, 2 = double)")]
        private float _fovIntensityMultiplier = 1.0f;

        [SerializeField, Range(0f, 20f), Tooltip("Player water speed where delayed swimming FOV warp starts.")]
        private float _swimmingFovWarpStartSpeed = 5f;

        [SerializeField, Range(0.1f, 35f), Tooltip("Player water speed where delayed swimming FOV warp reaches full offset.")]
        private float _swimmingFovWarpFullSpeed = 18f;

        [SerializeField, Range(0f, 8f), Tooltip("Maximum FOV degrees added by fast underwater locomotion.")]
        private float _swimmingFovWarpMaxOffset = 4.5f;

        [SerializeField, Range(0.1f, 12f), Tooltip("Delayed lerp sharpness for swimming velocity FOV warp.")]
        private float _swimmingFovWarpSharpness = 3.5f;

        [SerializeField, Tooltip("Enable motion blur post-processing effect")]
        private bool _motionBlurEnabled = false;

        [SerializeField, Tooltip("Enable chromatic aberration post-processing effect")]
        private bool _chromaticAberrationEnabled = true;

        [SerializeField, Tooltip("Enable depth-of-field post-processing effect")]
        private bool _depthOfFieldEnabled = true;

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
            set => _shakeIntensityMultiplier = math.clamp(value, 0f, 2f);
        }

        /// <summary>
        /// FOV effects intensity multiplier (0 = off, 1 = default, 2 = double).
        /// Applied immediately without scene reload.
        /// </summary>
        public float FOVIntensityMultiplier
        {
            get => _fovIntensityMultiplier;
            set => _fovIntensityMultiplier = math.clamp(value, 0f, 2f);
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
        private bool _hotSwapRegistered;
        private bool _scalabilityEventsRegistered;
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
        private byte _cachedScalabilityTier;
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
            ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                LogDuplicateInstanceDetected();
                Destroy(gameObject);
                return;
            }

            RefreshCachedRegistryServices();
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

            TryRegisterScalabilityEvents();
            TryResolveGameplayDependencies();
            SyncDependencyFlags();
            EnsureCameraJuiceTelemetry();
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
            TryRegisterHotSwapListener();
            TryRegisterScalabilityEvents();
            if (Application.isPlaying)
                CameraJuiceSignals.EnsurePrewarmed();

            TryRegisterDispatcherTicks();
            TryRegisterLateFrame();

            TryResolveGameplayDependencies();
            SyncDependencySubscriptions();
            EnsureCameraSpeedLineParticles();

            InteractionEvents.Register(this);
            PhysicsEvents.Register(this);
            CombatDamageRuntime.Register(this);
        }

        private void OnDisable()
        {
            // Unregister from GameTickManager
            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterScalabilityEvents();
            TryUnregisterHotSwapListener();

            UnhookDependencyEvents();

            InteractionEvents.Unregister(this);
            PhysicsEvents.Unregister(this);
            CombatDamageRuntime.Unregister(this);

            _fovBlendActive = false;
            _inputReclaimFovActive = false;
            _biomeBlendActive = false;

            _focusTarget = null;
            _focusTargetTransform = null;
            _pauseDepthOfFieldWeight = 0f;
            _pauseDofOverrideEngaged = false;
            _pauseDofDefaultsCaptured = false;
            _shakeOffset = Vector3.zero;
            _trauma = 0f;
            _proceduralShakeTranslation = float3.zero;
            _proceduralShakeRotationDegrees = float3.zero;
            _directionalBiasLocal = float3.zero;
            _directionalBiasTimer = 0f;
            _proceduralRollDegrees = 0f;
            _proceduralRollVelocity = 0f;
            _playerRuntimeContext = null;
            _playerRigidbody = null;
            _submarineRuntimeContext = null;
            _submarineHullRigidbody = null;
            _submarineStructuralGrid = null;
            _dynamicResolutionScaler = null;
            _vramMonitor = null;
            _dispatcher = null;
            _cachedScalabilityTier = 0;
            StopCameraSpeedLineParticles();

            if (_cameraTransform != null)
            {
                RemoveLastShakeRotation();
                _cameraTransform.localPosition = _cameraLocalRestPosition;
            }

            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView = _baseFOV;
                _mainCamera.ResetProjectionMatrix();
            }

            ReleaseCameraJuiceTelemetry();
        }

        private void TryUnregister()
        {
            TryUnregisterLateFrame();

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registered = false;
            }
        }

        private void TryRegisterDispatcherTicks()
        {
            if (_registered || !Application.isPlaying || _dispatcher == null)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            bool slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            _registered = updateRegistered || slowTickRegistered;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
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
            if (_registeredLateFrame || !Application.isPlaying || _dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
            {
                ScalabilityEvents.Register(this);
                _scalabilityEventsRegistered = true;
            }

            _cachedScalabilityTier = (byte)GlobalRegistry.ScalabilityTier;
        }

        private void TryUnregisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityEventsRegistered = false;
        }

        void IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedScalabilityTier = (byte)payload.CurrentQualityTier;
            _proceduralLowTierNoisePrimed = false;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Player, GlobalRegistry.Player);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Submarine, GlobalRegistry.Submarine);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DynamicResolutionRuntime, GlobalRegistry.DynamicResolution);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.VRAMMonitorRuntime, GlobalRegistry.VRAMMonitor);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    ITickDispatcher dispatcher = currentService as ITickDispatcher;
                    if (!ReferenceEquals(_dispatcher, dispatcher))
                    {
                        TryUnregister();
                        _dispatcher = dispatcher;
                    }

                    if (_dispatcher != null)
                    {
                        TryRegisterDispatcherTicks();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.Player:
                    BindPlayerRuntime(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    BindSubmarineRuntime(currentService as ISubmarineRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionScaler = currentService as DynamicResolutionScaler;
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as VRAMMonitor;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void BindPlayerRuntime(IPlayerRuntimeContext playerRuntimeContext)
        {
            if (ReferenceEquals(_playerRuntimeContext, playerRuntimeContext))
                return;

            UnhookDependencyEvents();
            _playerRuntimeContext = playerRuntimeContext;
            _playerRigidbody = playerRuntimeContext != null ? playerRuntimeContext.PlayerRigidbody : null;
            if (_survivalSystemReference == null)
                _survivalSystem = playerRuntimeContext != null ? playerRuntimeContext.SurvivalSystem : null;
            if (_playerMovementReference == null)
                _playerMovement = playerRuntimeContext != null ? playerRuntimeContext.PlayerMovement : null;

            SyncDependencyFlags();
            if (isActiveAndEnabled)
                SyncDependencySubscriptions();
        }

        private void BindSubmarineRuntime(ISubmarineRuntimeContext submarineRuntimeContext)
        {
            _submarineRuntimeContext = submarineRuntimeContext;
            _submarineHullRigidbody = submarineRuntimeContext != null ? submarineRuntimeContext.HullRigidbody : null;
            _submarineStructuralGrid = submarineRuntimeContext != null ? submarineRuntimeContext.StructuralGrid : null;
        }

        private void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            _dataVault = vault;
            _cameraJuiceTelemetryHandle = default;
            _cameraJuiceTelemetryReady = false;
            _cameraJuiceTelemetryCursor = 0;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterScalabilityEvents();
            TryUnregisterHotSwapListener();
            InteractionEvents.Unregister(this);
            PhysicsEvents.Unregister(this);
            CombatDamageRuntime.Unregister(this);

            ReleaseCameraJuiceTelemetry();

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
            ConsumePlayerSprintSignals();

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
            ApplyPostAupShakeOffset();
            RecordCameraJuiceTelemetry();
            DecayProceduralTrauma(SystemDispatcher.CurrentFrameDeltaTime);
        }

        // ═══ ISLOWTICKABLE ═══

        /// <summary>
        /// 2Hz update for health and O2 post-processing effects.
        /// </summary>
        public void SlowTick()
        {
            if (Time.time >= _nextDependencyResolveTime)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMainCameraMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVolumeMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVignetteMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] Vignette override not found in Volume profile.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogChromaticAberrationMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] ChromaticAberration override not found in Volume profile.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthOfFieldMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] DepthOfField override not found in Volume profile.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeCalculationFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] Shake calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullShakeProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] TriggerShake called with null profile.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeMaxDisplacementClamped(float maxDisplacement)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] ShakeProfile MaxDisplacement out of range [0, 1]. Clamping.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeDurationDefaulted(float duration)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] ShakeProfile Duration invalid. Using default 0.5s.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullBiomeProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] TransitionToBiome called with null biome. Using default fallback.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFovCalculationFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] FOV calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBiomeBlendFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] Biome blend failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInteractionFocusFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] Interaction focus calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFrameBudgetExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CameraJuiceSystem] Frame time exceeded budget.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHealthPostProcessingFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] Health post-processing failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogO2PostProcessingFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[CameraJuiceSystem] O2 post-processing failed.");
#endif
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
            _pauseDepthOfFieldWeight = math.saturate(weight);
        }

        /// <summary>
        /// Reclaims gameplay control from a cinematic wide FOV without a camera snap.
        /// </summary>
        public void BeginInputReclaimFov(float startFov, float durationSeconds)
        {
            if (!_fovEnabled || _mainCamera == null || HectonXRRuntimeState.IsXRActive)
                return;

            _inputReclaimFovStart = math.clamp(startFov, MIN_FOV, MAX_FOV);
            _inputReclaimFovTarget = math.clamp(_baseFOV, MIN_FOV, MAX_FOV);
            _inputReclaimFovDuration = math.max(0.0001f, durationSeconds);
            _inputReclaimFovElapsed = 0f;
            _inputReclaimFovActive = true;
            ApplyProjectionFov(_inputReclaimFovStart);
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

            float maxDisplacement = profile.MaxDisplacement;
            if (maxDisplacement < 0f || maxDisplacement > 1f)
            {
                LogShakeMaxDisplacementClamped(maxDisplacement);
                maxDisplacement = math.saturate(maxDisplacement);
            }

            float duration = profile.Duration;
            if (duration <= 0f)
            {
                LogShakeDurationDefaulted(duration);
            }

            float severity = math.saturate((maxDisplacement * 2.5f) * math.max(0f, intensityScale));
            AddProceduralTrauma(severity, float3.zero);
        }

        /// <summary>
        /// Triggers the fixed 0.5s submarine impact shake without requiring a ShakeProfile asset.
        /// </summary>
        public void TriggerSubmarineImpactShake(float severity01)
        {
            if (!_shakeEnabled)
                return;

            float safeSeverity = math.saturate(severity01);
            if (safeSeverity <= 0f)
                return;

            _submarineImpactShakeSign = _submarineImpactShakeSign >= 0f ? -1f : 1f;
            AddProceduralTrauma(
                safeSeverity,
                new float3(_submarineImpactShakeSign, -0.55f, -0.25f));
        }

        /// <summary>
        /// Trigger FOV kick effect.
        /// </summary>
        /// <param name="amount">FOV offset amount in degrees</param>
        /// <param name="duration">Transition duration in seconds</param>
        public void TriggerFOVKick(float amount, float duration)
        {
            if (!_fovEnabled || HectonXRRuntimeState.IsXRActive)
                return;

            _fovBlendStart = _currentFOVOffset;
            _fovBlendTarget = amount;
            _fovBlendDuration = math.max(0.0001f, duration);
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
            _biomeBlendDuration = math.max(0.0001f, blendDuration);
            _biomeBlendElapsed = 0f;
            _biomeBlendActive = true;
            ApplyBiomeBlend(_biomeBlendFrom, _targetBiome, 0f);
        }

        private static float EvaluateEaseOutQuad(float t)
        {
            t = math.saturate(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse;
        }

        private static float EvaluateEaseInOutQuad(float t)
        {
            t = math.saturate(t);
            if (t < 0.5f)
                return 2f * t * t;

            float inverse = -2f * t + 2f;
            return 1f - inverse * inverse * 0.5f;
        }

        private static float EvaluateSmoothStep01(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - (2f * t));
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
                bloom.intensity.value = math.lerp(from.BloomIntensity, to.BloomIntensity, t);
                bloom.threshold.value = math.lerp(from.BloomThreshold, to.BloomThreshold, t);
            }

            // Note: AO and Fog require additional URP components
        }

        // ═══ READ-ONLY PROPERTIES ═══

        public int ActiveShakeCount =>
            _trauma > 0.001f || _directionalBiasTimer > 0f || math.abs(_proceduralRollDegrees) > 0.001f ? 1 : 0;
        public float CurrentFOVOffset => _currentFOVOffset;
        public FOVState CurrentFOVState => _fovState;
        public bool IsPostProcessingEnabled => _postProcessingEnabled;
        public float DebugAdaptiveShakeScale => _adaptiveShakeScale;
        public float DebugAdaptiveFOVScale => _adaptiveFOVScale;
        public float DebugAdaptivePostFxScale => _adaptivePostFxScale;
        public int DebugAdaptiveMaxActiveShakes => _adaptiveMaxActiveShakes;
        public bool DebugAdaptiveDisableInteractionDoF => _adaptiveDisableInteractionDoF;

        // ═══ PRIVATE METHODS ═══

        private void ConsumePlayerSprintSignals()
        {
            ReadOnlySpan<PlayerSprintStateSignal> signals = SignalBus<PlayerSprintStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].IsSprinting != 0)
                    HandleSprintStarted();
                else
                    HandleSprintEnded();
            }
        }

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

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            if (!_shakeEnabled)
                return;

            float severity = ResolvePhysicsImpactSeverity(in impactSignal);
            if (severity <= 0f)
                return;

            AddProceduralTrauma(severity, ResolvePhysicsImpactDirection(in impactSignal));
        }

        void ICombatDamageEventListener.OnCombatDamageResolved(in CombatDamageResult result)
        {
            if (!_shakeEnabled)
                return;

            float severity = ResolveCombatDamageSeverity(in result);
            if (severity <= 0f)
                return;

            AddProceduralTrauma(severity, result.Direction);
        }

        private void UpdateShake(float dt)
        {
            if (!_shakeEnabled)
                return;

            float effectiveShakeScale = _shakeIntensityMultiplier * _adaptiveShakeScale;
            if (effectiveShakeScale <= 0f)
            {
                _shakeOffset = Vector3.zero;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                _seismicJitterIntensity = 0f;
                _seismicShakeOffset = float3.zero;
                _seismicShakeRotationDegrees = float3.zero;
                return;
            }

            DrainCameraImpactSignals();
            UpdateProceduralTraumaShake(dt, effectiveShakeScale);
            UpdateSeismicCameraJitter(dt, effectiveShakeScale);
            _shakeNoiseTime += math.max(0f, dt);
        }

        private void DrainCameraImpactSignals()
        {
            int processed = 0;
            while (processed < PROCEDURAL_MAX_IMPACTS_PER_FRAME &&
                   CameraJuiceSignals.TryDequeueImpact(out CameraJuiceImpactSignal signal))
            {
                float severity = math.saturate(math.max(signal.Severity, signal.Impact.Intensity));
                AddProceduralTrauma(severity, signal.Direction);
                processed++;
            }
        }

        private void AddProceduralTrauma(float severity01, float3 worldDirection)
        {
            float severity = math.saturate(severity01);
            if (severity <= 0f || !math.isfinite(severity))
                return;

            _trauma = math.saturate(_trauma + ResolveTraumaAddition(severity));

            float3 localDirection = ResolveLocalShakeDirection(worldDirection);
            if (math.lengthsq(localDirection) > 0.000001f)
            {
                _directionalBiasLocal = localDirection * severity;
                _directionalBiasTimer = PROCEDURAL_DIRECTIONAL_BIAS_SECONDS;
                _proceduralRollVelocity += -localDirection.x * severity * PROCEDURAL_ROLL_AMPLITUDE_DEGREES;
            }

            if (severity > PROCEDURAL_HIT_STOP_THRESHOLD)
                RequestProceduralHitStop();
        }

        private void UpdateProceduralTraumaShake(float dt, float effectiveShakeScale)
        {
            float safeDt = math.max(0f, dt);
            _proceduralShakeTime += safeDt * PROCEDURAL_SHAKE_FREQUENCY;

            float trauma = math.saturate(_trauma);
            float intensity = trauma * trauma * math.max(0f, effectiveShakeScale);
            bool xrActive = HectonXRRuntimeState.IsXRActive;
            if (xrActive)
            {
                _directionalBiasTimer = 0f;
                _proceduralRollDegrees = 0f;
                _proceduralRollVelocity = 0f;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                _shakeOffset = Vector3.zero;
                return;
            }

            float3 noiseTranslation;
            float3 noiseRotation;
            ResolveProceduralNoise(safeDt, out noiseTranslation, out noiseRotation);

            float translationAmplitude = intensity * PROCEDURAL_TRANSLATION_AMPLITUDE_METERS;
            float rotationAmplitude = intensity * PROCEDURAL_ROTATION_AMPLITUDE_DEGREES;

            float3 translation = noiseTranslation * translationAmplitude;
            float3 rotation = noiseRotation * rotationAmplitude;

            float biasT = 0f;
            if (_directionalBiasTimer > 0f)
            {
                biasT = math.saturate(_directionalBiasTimer / PROCEDURAL_DIRECTIONAL_BIAS_SECONDS);
                _directionalBiasTimer = math.max(0f, _directionalBiasTimer - safeDt);
            }

            if (biasT > 0f)
                translation += _directionalBiasLocal * (PROCEDURAL_TRANSLATION_AMPLITUDE_METERS * biasT);

            float targetRoll = biasT > 0f
                ? -_directionalBiasLocal.x * PROCEDURAL_ROLL_AMPLITUDE_DEGREES * intensity
                : 0f;
            float rollAcceleration = ((targetRoll - _proceduralRollDegrees) * PROCEDURAL_ROLL_SPRING) -
                                     (_proceduralRollVelocity * PROCEDURAL_ROLL_DAMPING);
            _proceduralRollVelocity += rollAcceleration * safeDt;
            _proceduralRollDegrees += _proceduralRollVelocity * safeDt;
            rotation.z += _proceduralRollDegrees;

            bool invalidShakeMath = false;
            if (!math.all(math.isfinite(translation)))
            {
                invalidShakeMath = true;
                translation = float3.zero;
            }
            if (!math.all(math.isfinite(rotation)))
            {
                invalidShakeMath = true;
                rotation = float3.zero;
            }
            if (!math.isfinite(_proceduralRollDegrees) || !math.isfinite(_proceduralRollVelocity))
            {
                invalidShakeMath = true;
                _proceduralRollDegrees = 0f;
                _proceduralRollVelocity = 0f;
            }
            if (invalidShakeMath)
                DumpCameraJuiceTelemetry();

            _proceduralShakeTranslation = translation;
            _proceduralShakeRotationDegrees = rotation;
            _shakeOffset = new Vector3(translation.x, translation.y, translation.z);
        }

        private void DecayProceduralTrauma(float dt)
        {
            float safeDt = math.max(0f, dt);
            _trauma = math.max(0f, _trauma - (PROCEDURAL_TRAUMA_DECAY_RATE * safeDt));

            if (_trauma <= 0.0001f &&
                _directionalBiasTimer <= 0f &&
                math.abs(_proceduralRollDegrees) <= 0.001f &&
                math.abs(_proceduralRollVelocity) <= 0.001f)
            {
                _trauma = 0f;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                _shakeOffset = Vector3.zero;
            }
        }

        private void ResolveProceduralNoise(float dt, out float3 translation, out float3 rotation)
        {
            bool lowTier =
                QualitySettings.GetQualityLevel() == 0 ||
                _cachedScalabilityTier <= (byte)HectonQualityTier.Mx350;

            if (!lowTier)
            {
                EvaluateProceduralNoise(_proceduralShakeTime, out translation, out rotation);
                return;
            }

            if (!_proceduralLowTierNoisePrimed)
            {
                EvaluateProceduralNoise(_proceduralShakeTime, out _proceduralLowTierTranslationTo, out _proceduralLowTierRotationTo);
                _proceduralLowTierTranslationFrom = _proceduralLowTierTranslationTo;
                _proceduralLowTierRotationFrom = _proceduralLowTierRotationTo;
                _proceduralLowTierSampleTimer = 0f;
                _proceduralLowTierNoisePrimed = true;
            }

            _proceduralLowTierSampleTimer += math.max(0f, dt);
            if (_proceduralLowTierSampleTimer >= PROCEDURAL_LOW_TIER_SAMPLE_INTERVAL)
            {
                _proceduralLowTierSampleTimer -= PROCEDURAL_LOW_TIER_SAMPLE_INTERVAL;
                _proceduralLowTierTranslationFrom = _proceduralLowTierTranslationTo;
                _proceduralLowTierRotationFrom = _proceduralLowTierRotationTo;
                EvaluateProceduralNoise(_proceduralShakeTime, out _proceduralLowTierTranslationTo, out _proceduralLowTierRotationTo);
            }

            float t = EvaluateSmoothStep01(_proceduralLowTierSampleTimer / PROCEDURAL_LOW_TIER_SAMPLE_INTERVAL);
            translation = math.lerp(_proceduralLowTierTranslationFrom, _proceduralLowTierTranslationTo, t);
            rotation = math.lerp(_proceduralLowTierRotationFrom, _proceduralLowTierRotationTo, t);
        }

        private static void EvaluateProceduralNoise(float time, out float3 translation, out float3 rotation)
        {
            translation = new float3(
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_X)),
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_Y)),
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_Z)));

            rotation = new float3(
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_PITCH)),
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_YAW)),
                noise.cnoise(new float2(time, PROCEDURAL_NOISE_SEED_ROLL)));
        }

        private float3 ResolveLocalShakeDirection(float3 worldDirection)
        {
            if (!math.all(math.isfinite(worldDirection)))
                return float3.zero;

            float lengthSq = math.lengthsq(worldDirection);
            if (lengthSq <= 0.000001f)
                return float3.zero;

            float3 normalized = worldDirection * math.rsqrt(math.max(lengthSq, 0.000001f));
            if (_cameraTransform == null)
                return normalized;

            Vector3 local = _cameraTransform.InverseTransformDirection(new Vector3(normalized.x, normalized.y, normalized.z));
            float3 local3 = new float3(local.x, local.y, local.z);
            float localLengthSq = math.lengthsq(local3);
            return localLengthSq > 0.000001f ? local3 * math.rsqrt(math.max(localLengthSq, 0.000001f)) : float3.zero;
        }

        private float3 ResolvePhysicsImpactDirection(in PhysicsImpactSignal impactSignal)
        {
            float3 direction = new float3(
                impactSignal.Normal.x,
                impactSignal.Normal.y,
                impactSignal.Normal.z);
            if (math.lengthsq(direction) > 0.000001f && math.all(math.isfinite(direction)))
                return direction;

            if (_cameraTransform == null)
                return float3.zero;

            Vector3 cameraPosition = _cameraTransform.position;
            Vector3 fromImpact = cameraPosition - impactSignal.Point;
            return new float3(fromImpact.x, fromImpact.y, fromImpact.z);
        }

        private static float ResolvePhysicsImpactSeverity(in PhysicsImpactSignal impactSignal)
        {
            float intensity = math.isfinite(impactSignal.Intensity) ? math.saturate(impactSignal.Intensity) : 0f;
            float massVelocity = math.isfinite(impactSignal.MassVelocity) ? math.max(0f, impactSignal.MassVelocity) : 0f;
            float massVelocity01 = math.saturate(math.log10(1f + (massVelocity * 0.015f)));
            float weightBonus = impactSignal.WeightClass == PhysicsImpactWeightClass.Heavy
                ? 0.18f
                : impactSignal.WeightClass == PhysicsImpactWeightClass.Medium ? 0.08f : 0f;
            return math.saturate(intensity + (massVelocity01 * 0.35f) + weightBonus);
        }

        private static float ResolveCombatDamageSeverity(in CombatDamageResult result)
        {
            float healthSeverity = 0f;
            if (math.isfinite(result.AppliedDamage) && math.isfinite(result.MaxHealth) && result.MaxHealth > 0.0001f)
                healthSeverity = math.saturate(result.AppliedDamage / result.MaxHealth);

            float traumaSeverity = math.saturate(result.TraumaLevel * 0.25f);
            return math.saturate(math.max(healthSeverity, traumaSeverity));
        }

        private static float ResolveTraumaAddition(float severity)
        {
            float safeSeverity = math.saturate(severity);
            return math.saturate(0.12f + (safeSeverity * 0.68f));
        }

        private void RequestProceduralHitStop()
        {
            ITickDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
                dispatcher.RequestCoreTickDilation(0.05f, 3, CAMERA_JUICE_HIT_STOP_REASON_HASH);
        }

        private void UpdateSeismicCameraJitter(float dt, float effectiveShakeScale)
        {
            float safeDt = math.max(0f, dt);
            ReadOnlySpan<SeismicSignal> seismicSignals = SignalBus<SeismicSignal>.GetFrameSnapshot();
            for (int i = 0; i < seismicSignals.Length; i++)
            {
                SeismicSignal signal = seismicSignals[i];
                int sequence = signal.Sequence;
                if (sequence == _lastSeismicSignalSequence)
                    continue;

                _lastSeismicSignalSequence = sequence;
                float signalIntensity = math.saturate(signal.CameraJitter01 * math.max(0f, effectiveShakeScale));
                if (signalIntensity > _seismicJitterIntensity)
                    _seismicJitterIntensity = signalIntensity;

                float3 localDirection = ResolveLocalShakeDirection(signal.Direction);
                if (math.lengthsq(localDirection) > 0.000001f)
                    _seismicDirectionLocal = localDirection;
            }

            if (HectonXRRuntimeState.IsXRActive)
            {
                _seismicJitterIntensity = 0f;
                _seismicShakeOffset = float3.zero;
                _seismicShakeRotationDegrees = float3.zero;
                return;
            }

            _seismicJitterIntensity = math.max(0f, _seismicJitterIntensity - SEISMIC_CAMERA_DECAY_RATE * safeDt);
            _seismicJitterTime += safeDt * SEISMIC_CAMERA_FREQUENCY;
            if (_seismicJitterIntensity <= 0.0001f)
            {
                _seismicJitterIntensity = 0f;
                _seismicShakeOffset = float3.zero;
                _seismicShakeRotationDegrees = float3.zero;
                return;
            }

            float pulseA = ResolveTrianglePulseSigned(_seismicJitterTime + 0.17f);
            float pulseB = ResolveTrianglePulseSigned(_seismicJitterTime * 1.37f + 0.53f);
            float pulseC = ResolveTrianglePulseSigned(_seismicJitterTime * 0.73f + 0.91f);
            float3 direction = math.lengthsq(_seismicDirectionLocal) > 0.000001f
                ? _seismicDirectionLocal
                : new float3(1f, 0f, 0f);

            _seismicShakeOffset = new float3(
                (direction.x * pulseA + pulseC * 0.21f) * SEISMIC_TRANSLATION_AMPLITUDE_METERS,
                pulseB * SEISMIC_TRANSLATION_AMPLITUDE_METERS * 0.35f,
                (direction.z * pulseC + pulseA * 0.13f) * SEISMIC_TRANSLATION_AMPLITUDE_METERS) * _seismicJitterIntensity;
            _seismicShakeRotationDegrees = new float3(
                pulseB * SEISMIC_ROTATION_AMPLITUDE_DEGREES * 0.35f,
                pulseA * SEISMIC_ROTATION_AMPLITUDE_DEGREES,
                -direction.x * pulseC * SEISMIC_ROTATION_AMPLITUDE_DEGREES * 0.25f) * _seismicJitterIntensity;
        }

        private bool EnsureCameraJuiceTelemetry()
        {
            if (!ValidateCameraJuiceTelemetryLayout())
            {
                ReleaseCameraJuiceTelemetry();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ReleaseCameraJuiceTelemetry();
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuiceTelemetryHandle = default;
                _cameraJuiceTelemetryReady = false;
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.CameraJuiceTelemetryRing, out _cameraJuiceTelemetryHandle) ||
                !_cameraJuiceTelemetryHandle.IsCreated)
            {
                _cameraJuiceTelemetryHandle = vault.GetBufferHandle<CameraJuiceTelemetryEntry>(
                    BufferID.CameraJuiceTelemetryRing,
                    CAMERA_JUICE_TELEMETRY_CAPACITY,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            _cameraJuiceTelemetryReady =
                _cameraJuiceTelemetryHandle.IsCreated &&
                _cameraJuiceTelemetryHandle.Length >= CAMERA_JUICE_TELEMETRY_CAPACITY;
            return _cameraJuiceTelemetryReady;
        }

        private bool TryResolveCameraJuiceTelemetry(out NativeArray<CameraJuiceTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (!EnsureCameraJuiceTelemetry())
                return false;

            if (!_dataVault.TryGetBufferHandle(BufferID.CameraJuiceTelemetryRing, out _cameraJuiceTelemetryHandle) ||
                !_cameraJuiceTelemetryHandle.IsCreated)
            {
                ReleaseCameraJuiceTelemetry();
                return false;
            }

            telemetry = _cameraJuiceTelemetryHandle.Resolve(_dataVault);
            return telemetry.IsCreated && telemetry.Length >= CAMERA_JUICE_TELEMETRY_CAPACITY;
        }

        private void ReleaseCameraJuiceTelemetry()
        {
            _dataVault = null;
            _cameraJuiceTelemetryHandle = default;
            _cameraJuiceTelemetryReady = false;
            _cameraJuiceTelemetryCursor = 0;
        }

        private static bool ValidateCameraJuiceTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<CameraJuiceTelemetryEntry>() == CameraJuiceTelemetryEntrySizeBytes;
        }

        private void RecordCameraJuiceTelemetry()
        {
            if (!TryResolveCameraJuiceTelemetry(out var telemetry))
                return;

            int index = _cameraJuiceTelemetryCursor % CAMERA_JUICE_TELEMETRY_CAPACITY;
            telemetry[index] = new CameraJuiceTelemetryEntry
            {
                Frame = Time.frameCount,
                Flags = HectonXRRuntimeState.IsXRActive ? 1u : 0u,
                Trauma = _trauma,
                FovKick = 0f,
                Offset = _proceduralShakeTranslation,
                RotationDegrees = _proceduralShakeRotationDegrees,
                RollDegrees = _proceduralRollDegrees,
                DirectionalBiasTimer = _directionalBiasTimer,
                AdaptiveShakeScale = _adaptiveShakeScale
            };
            _cameraJuiceTelemetryCursor++;
        }

        private void DumpCameraJuiceTelemetry()
        {
            if (_cameraJuiceTelemetryDumped || !TryResolveCameraJuiceTelemetry(out var telemetry))
                return;

            _cameraJuiceTelemetryDumped = true;
            string dumpPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Docs",
                "AgentLogs",
                "Dump_CAMERA_JUICE_SYSTEM.bin"));

            try
            {
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int count = math.min(_cameraJuiceTelemetryCursor, CAMERA_JUICE_TELEMETRY_CAPACITY);
                    int start = _cameraJuiceTelemetryCursor - count;
                    writer.Write(CAMERA_JUICE_TELEMETRY_CAPACITY);
                    writer.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        CameraJuiceTelemetryEntry entry = telemetry[(start + i) % CAMERA_JUICE_TELEMETRY_CAPACITY];
                        writer.Write(entry.Frame);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Trauma);
                        writer.Write(entry.FovKick);
                        writer.Write(entry.Offset.x);
                        writer.Write(entry.Offset.y);
                        writer.Write(entry.Offset.z);
                        writer.Write(entry.RotationDegrees.x);
                        writer.Write(entry.RotationDegrees.y);
                        writer.Write(entry.RotationDegrees.z);
                        writer.Write(entry.RollDegrees);
                        writer.Write(entry.DirectionalBiasTimer);
                        writer.Write(entry.AdaptiveShakeScale);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void ApplyPostAupShakeOffset()
        {
            if (_cameraTransform == null)
                return;

            RemoveLastShakeRotation();
            if (HectonXRRuntimeState.IsXRActive)
            {
                _shakeOffset = Vector3.zero;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                _seismicShakeOffset = float3.zero;
                _seismicShakeRotationDegrees = float3.zero;
                return;
            }

            Vector3 combinedOffset = _shakeOffset + new Vector3(
                _seismicShakeOffset.x,
                _seismicShakeOffset.y,
                _seismicShakeOffset.z);
            _cameraTransform.localPosition = _cameraLocalRestPosition + ResolveClipSafeShakeOffset(combinedOffset);

            float3 combinedRotationDegrees = _proceduralShakeRotationDegrees + _seismicShakeRotationDegrees;
            if (math.lengthsq(combinedRotationDegrees) <= 0.000001f)
                return;

            Quaternion shakeRotation = Quaternion.Euler(
                combinedRotationDegrees.x,
                combinedRotationDegrees.y,
                combinedRotationDegrees.z);
            _cameraTransform.localRotation = _cameraTransform.localRotation * shakeRotation;
            _lastShakeLocalRotation = shakeRotation;
            _lastShakeCompositeLocalRotation = _cameraTransform.localRotation;
            _shakeRotationApplied = true;
        }

        private void RemoveLastShakeRotation()
        {
            if (!_shakeRotationApplied || _cameraTransform == null)
                return;

            if (math.abs(Quaternion.Dot(_cameraTransform.localRotation, _lastShakeCompositeLocalRotation)) > 0.9995f)
                _cameraTransform.localRotation = _cameraTransform.localRotation * Quaternion.Inverse(_lastShakeLocalRotation);
            _lastShakeLocalRotation = Quaternion.identity;
            _lastShakeCompositeLocalRotation = Quaternion.identity;
            _shakeRotationApplied = false;
        }

        private Vector3 ResolveClipSafeShakeOffset(Vector3 offset)
        {
            float maxShakeDisplacement = math.min(
                MAX_SHAKE_DISPLACEMENT,
                math.max(0.005f, _cameraShakeClipSafeDisplacement));
            float distanceSq = offset.sqrMagnitude;
            float maxShakeDisplacementSq = maxShakeDisplacement * maxShakeDisplacement;
            if (distanceSq <= maxShakeDisplacementSq)
                return offset;

            if (distanceSq <= 0.000001f)
                return Vector3.zero;

            return offset * (maxShakeDisplacement * math.rsqrt(math.max(distanceSq, 0.000001f)));
        }

        private void EnsureCameraSpeedLineParticles()
        {
            if (_speedLineParticles != null || _cameraTransform == null)
                return;

            int maxParticles = math.max(1, _speedLineMaxParticles);
            // COLD ALLOC: GameObject[1] + ParticleSystem[1] — camera-local cinematic speed-line emitter — owner: CameraJuiceSystem
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

            _speedLineParticles.TryGetComponent(out _speedLineRenderer);
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
                return;

            float currentSpeed = ResolveCurrentCameraSpeed();
            float speed01 = math.saturate(
                (currentSpeed - math.max(0f, _speedLineStartMetersPerSecond)) /
                math.max(0.01f, _speedLineFullMetersPerSecond - _speedLineStartMetersPerSecond));
            speed01 = speed01 * speed01 * (3f - 2f * speed01);
            float blend = ResolvePadeApproach01(8f, dt);
            _speedLineIntensity = math.lerp(_speedLineIntensity, speed01, blend);

            var emission = _speedLineParticles.emission;
            float emissionRate = math.lerp(0f, math.max(1f, _speedLineMaxEmissionRate), _speedLineIntensity) *
                                 FrameTimeWatchdog.ParticleEmissionScale;
            if (math.abs(_cachedSpeedLineEmissionRate - emissionRate) > 0.5f)
            {
                emission.rateOverTime = emissionRate;
                _cachedSpeedLineEmissionRate = emissionRate;
            }

            var velocity = _speedLineParticles.velocityOverLifetime;
            float velocityZ = -math.lerp(18f, 44f, _speedLineIntensity);
            if (math.abs(_cachedSpeedLineVelocityZ - velocityZ) > 0.25f)
            {
                velocity.z = new ParticleSystem.MinMaxCurve(velocityZ * 0.64f, velocityZ);
                _cachedSpeedLineVelocityZ = velocityZ;
            }

            if (_speedLineRenderer != null)
            {
                float stretch = math.lerp(0.45f, math.max(0.45f, _speedLineMaxStretch), _speedLineIntensity);
                if (math.abs(_cachedSpeedLineStretch - stretch) > 0.02f)
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
            Rigidbody playerBody = _playerRigidbody;
            if (playerBody != null)
                speed = math.max(speed, ApproximateVectorMagnitude(playerBody.linearVelocity));

            Rigidbody submarineBody = _submarineHullRigidbody;
            if (submarineBody != null)
                speed = math.max(speed, ApproximateVectorMagnitude(submarineBody.linearVelocity));

            return float.IsFinite(speed) ? speed : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (0.375f * mid) + (0.125f * min);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DotVector(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        private bool HasSubmarineVelocityFovSource()
        {
            Rigidbody submarineBody = _submarineHullRigidbody;
            if (submarineBody == null)
                return false;

            float startSpeed = math.max(0f, _swimmingFovWarpStartSpeed);
            return submarineBody.linearVelocity.sqrMagnitude > startSpeed * startSpeed;
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
            if (!_fovEnabled)
                return;

            if (HectonXRRuntimeState.IsXRActive)
            {
                _currentFOVOffset = 0f;
                _fovBlendActive = false;
                _inputReclaimFovActive = false;
                _swimmingVelocityFovOffset = 0f;
                return;
            }

            if (_fovBlendActive)
            {
                _fovBlendElapsed = math.min(_fovBlendElapsed + dt, _fovBlendDuration);
                float normalizedBlend = math.saturate(_fovBlendElapsed / _fovBlendDuration);
                float easedBlend = EvaluateEaseOutQuad(normalizedBlend);
                _currentFOVOffset = math.lerp(_fovBlendStart, _fovBlendTarget, easedBlend);
                if (normalizedBlend >= 1f)
                {
                    _currentFOVOffset = _fovBlendTarget;
                    _fovBlendActive = false;
                }
            }

            // Calculate target FOV
            float targetFOV = _baseFOV +
                (_currentFOVOffset * _fovIntensityMultiplier * _adaptiveFOVScale);
            float swimmingWarpTarget = 0f;
            bool velocityWarpEligible =
                (_playerMovement != null && _playerMovement.IsPlayerSubmerged) ||
                HasSubmarineVelocityFovSource();
            if (velocityWarpEligible && _swimmingFovWarpMaxOffset > 0f)
            {
                float currentSpeed = ResolveCurrentCameraSpeed();
                float speedRange = math.max(0.01f, _swimmingFovWarpFullSpeed - _swimmingFovWarpStartSpeed);
                float speedT = math.saturate((currentSpeed - _swimmingFovWarpStartSpeed) / speedRange);
                float smoothSpeedT = EvaluateSmoothStep01(speedT);
                swimmingWarpTarget = smoothSpeedT * _swimmingFovWarpMaxOffset * _adaptiveFOVScale;
            }

            float warpBlendT = ResolvePadeApproach01(math.max(0.01f, _swimmingFovWarpSharpness), dt);
            warpBlendT = EvaluateSmoothStep01(warpBlendT);
            _swimmingVelocityFovOffset = math.lerp(_swimmingVelocityFovOffset, swimmingWarpTarget, warpBlendT);
            targetFOV = math.clamp(targetFOV + _swimmingVelocityFovOffset, MIN_FOV, MAX_FOV);

            if (_inputReclaimFovActive)
            {
                _inputReclaimFovElapsed = math.min(_inputReclaimFovElapsed + math.max(0f, dt), _inputReclaimFovDuration);
                float normalizedReclaim = math.saturate(_inputReclaimFovElapsed / _inputReclaimFovDuration);
                float easedReclaim = EvaluateEaseOutQuad(normalizedReclaim);
                targetFOV = math.lerp(_inputReclaimFovStart, _inputReclaimFovTarget, easedReclaim);
                if (normalizedReclaim >= 1f)
                    _inputReclaimFovActive = false;
            }

            ApplyProjectionFov(targetFOV);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveTrianglePulseSigned(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return (1f - math.abs(wrapped * 2f - 1f)) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePadeApproach01(float sharpness, float dt)
        {
            float x = math.min(math.max(0f, sharpness) * math.max(0f, dt), 8f);
            float x2 = x * x;
            float expNegApprox = 1f / (1f + x + (0.48f * x2) + (0.235f * x2 * x));
            return math.saturate(1f - expNegApprox);
        }

        private void ApplyProjectionFov(float targetFOV)
        {
            if (_mainCamera == null)
                return;

            _mainCamera.fieldOfView = targetFOV;
            if (_mainCamera.orthographic)
                return;

            _mainCamera.projectionMatrix = Matrix4x4.Perspective(
                targetFOV,
                _mainCamera.aspect,
                _mainCamera.nearClipPlane,
                _mainCamera.farClipPlane);
        }

        private void UpdateBiomeBlend(float dt)
        {
            if (!_biomeBlendActive)
                return;

            _biomeBlendElapsed = math.min(_biomeBlendElapsed + dt, _biomeBlendDuration);
            float normalizedBlend = math.saturate(_biomeBlendElapsed / _biomeBlendDuration);
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

            float targetFocusDistance = ResolveTargetFocusDistance();
            float currentFocusDistance = _focusDistance > 0f ? _focusDistance : targetFocusDistance;
            float focusBlendT = ResolvePadeApproach01(2f / math.max(0.02f, _focusTransitionDuration), dt);
            _focusDistance = math.lerp(currentFocusDistance, targetFocusDistance, focusBlendT);

            _interactionDoF.focusDistance.value = _focusDistance;
            _interactionDoF.active = true;
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

            float easedBlend = EvaluateSmoothStep01(_pauseDepthOfFieldWeight);
            float baseFocusDistance = _focusDistance > 0f ? _focusDistance : _worldFocusDistance;
            _interactionDoF.focusDistance.value = math.lerp(
                baseFocusDistance,
                PauseDofFocusDistance,
                easedBlend);
            _interactionDoF.focalLength.value = math.lerp(_pauseDofBaseFocalLength, PauseDofFocalLength, easedBlend);
            _interactionDoF.aperture.value = math.lerp(_pauseDofBaseAperture, PauseDofAperture, easedBlend);
            _interactionDoF.gaussianEnd.value = math.lerp(_pauseDofBaseGaussianEnd, PauseDofGaussianEnd, easedBlend);
            _interactionDoF.gaussianMaxRadius.value = math.lerp(_pauseDofBaseGaussianMaxRadius, PauseDofGaussianMaxRadius, easedBlend);
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

        private float ResolveTargetFocusDistance()
        {
            if (PlayerPDA.IsOpen)
                return math.max(0.01f, _pdaFocusDistance);

            if (HectonFabricatorUI.IsMenuOpen)
            {
                if (_focusTargetTransform == null || IsFocusTargetInsidePdaThreshold())
                    return math.max(0.01f, _pdaFocusDistance);

                return math.max(0.01f, _worldFocusDistance);
            }

            if (_focusTargetTransform != null)
                return ResolveCinematicTargetFocusDistance();

            return math.max(0.01f, ResolveHudPlaneFocusDistance());
        }

        private bool IsFocusTargetInsidePdaThreshold()
        {
            if (_cameraTransform == null || _focusTargetTransform == null)
                return false;

            float threshold = math.max(0.01f, _pdaFocusThreshold);
            double distanceSq = ResolveAupRuntimeDistanceSq(_cameraTransform.position, _focusTargetTransform.position);
            return distanceSq <= threshold * threshold;
        }

        private float ResolveCinematicTargetFocusDistance()
        {
            if (_cameraTransform == null || _focusTargetTransform == null)
                return math.max(0.01f, _hudFocusDistance);

            float nearFocus = math.max(0.01f, _pdaFocusDistance);
            float farFocus = math.max(nearFocus, _worldFocusDistance);
            float nearThreshold = math.max(0.01f, _pdaFocusThreshold);
            double nearSq = nearThreshold * nearThreshold;
            double farSq = farFocus * farFocus;
            double distanceSq = ResolveAupRuntimeDistanceSq(_cameraTransform.position, _focusTargetTransform.position);
            if (distanceSq <= nearSq)
                return nearFocus;
            if (distanceSq >= farSq)
                return farFocus;

            double focusSpanSq = farSq - nearSq;
            if (focusSpanSq < 0.0001d)
                focusSpanSq = 0.0001d;
            float t = math.saturate((float)((distanceSq - nearSq) / focusSpanSq));
            return math.lerp(nearFocus, farFocus, t);
        }

        private static double ResolveAupRuntimeDistanceSq(Vector3 fromRuntimePosition, Vector3 toRuntimePosition)
        {
            AbsoluteUniversePosition fromAup = AbsoluteUniversePosition.FromRuntimePosition(fromRuntimePosition);
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntimePosition);
            return AbsoluteUniversePosition.DistanceSq(in fromAup, in toAup);
        }

        private float ResolveHudPlaneFocusDistance()
        {
            if (_cameraTransform == null)
                return math.max(0.01f, _hudFocusDistance);

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay == null || overlay.TargetCanvas == null)
                return math.max(0.01f, _hudFocusDistance);

            RectTransform canvasRect = overlay.TargetCanvas.transform as RectTransform;
            if (canvasRect == null)
                return math.max(0.01f, _hudFocusDistance);

            Vector3 canvasPosition = canvasRect.position;
            Vector3 cameraPosition = _cameraTransform.position;
            Vector3 visualPlaneDelta = canvasPosition - cameraPosition;
            float planeDistance = DotVector(_cameraTransform.forward, visualPlaneDelta);
            return math.max(0.01f, planeDistance > 0f ? planeDistance : _hudFocusDistance);
        }

        private bool TryResolveCamera()
        {
            _mainCamera = _cameraReference;

            if (_mainCamera == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                    _mainCamera = playerContext.PlayerCamera;

                if (_mainCamera == null &&
                    GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerTransform.TryGetComponent(out _mainCamera);
                }

                if (_mainCamera == null && !TryGetComponent(out _mainCamera))
                    _mainCamera = null;

                if (_mainCamera == null)
                {
                    _mainCamera = ResolveComponentInParents<Camera>(transform);
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
                _urpVolume = ResolveComponentInParents<Volume>(_cameraTransform);
            }
        }

        private void TryResolveGameplayDependencies()
        {
            _playerRigidbody = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerRigidbody : null;
            _submarineHullRigidbody = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;
            _submarineStructuralGrid = _submarineRuntimeContext != null ? _submarineRuntimeContext.StructuralGrid : null;

            Transform playerRoot = null;
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                playerRoot = currentPlayerTransform;
            }

            if (_survivalSystem == null)
            {
                _survivalSystem = _survivalSystemReference;
                if (_survivalSystem == null && _playerRuntimeContext != null)
                    _survivalSystem = _playerRuntimeContext.SurvivalSystem;

                if (_survivalSystem == null && _cameraTransform != null)
                {
                    _survivalSystem = ResolveComponentInParents<HectonSurvivalSystem>(_cameraTransform);
                }

                if (_survivalSystem == null && playerRoot != null)
                {
                    playerRoot.TryGetComponent(out _survivalSystem);
                }
            }

            if (_playerMovement == null)
            {
                _playerMovement = _playerMovementReference;
                if (_playerMovement == null && _playerRuntimeContext != null)
                    _playerMovement = _playerRuntimeContext.PlayerMovement;

                if (_playerMovement == null && _cameraTransform != null)
                {
                    _playerMovement = ResolveComponentInParents<HectonPlayerMovement>(_cameraTransform);
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
        }

        private void UnhookDependencyEvents()
        {
        }

        private void UpdateHealthPostProcessing()
        {
            if (_survivalSystem == null || _healthVignette == null) return;

            float healthNormalized = _survivalSystem.IntegrityNormalized;

            if (healthNormalized < 0.3f)
            {
                // Calculate vignette intensity
                float intensity = math.lerp(0f, 1f, (0.3f - healthNormalized) / 0.3f) * _adaptivePostFxScale;

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
                oxygenIntensity = math.lerp(0f, 0.8f, (0.2f - o2Normalized) / 0.2f);

            float structuralFatigueIntensity = ResolveStructuralFatigueChromaticContribution();
            float intensity = math.max(oxygenIntensity, structuralFatigueIntensity) * _adaptivePostFxScale;

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
            SubmarineStructuralGrid structuralGrid = _submarineStructuralGrid;
            if (structuralGrid == null || !structuralGrid.isActiveAndEnabled || !structuralGrid.IsReady)
                return 0f;

            return math.saturate(structuralGrid.FatiguePeakNormalized) * math.saturate(_structuralFatigueChromaticAberrationMax);
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!_enableAdaptiveBudgetResponse || !Application.isPlaying)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f, VRAMMonitor.VRAMPressureState.Stable);
                return;
            }

            float renderScale = 1f;
            DynamicResolutionScaler scaler = _dynamicResolutionScaler;
            if (scaler != null && scaler.Enabled)
            {
                renderScale = math.saturate(scaler.CurrentRenderScale);
            }

            float normalized = math.saturate(
                (renderScale - _adaptiveBudgetFloorRenderScale) /
                math.max(0.0001f, 1f - _adaptiveBudgetFloorRenderScale));

            VRAMMonitor vramMonitor = _vramMonitor;
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
            _adaptiveShakeScale = math.lerp(_adaptiveShakeFloor, 1f, normalized);
            _adaptiveFOVScale = math.lerp(_adaptiveFOVFloor, 1f, normalized);
            _adaptivePostFxScale = math.lerp(_adaptivePostFxFloor, 1f, normalized);
            _adaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;

            switch (pressureState)
            {
                case VRAMMonitor.VRAMPressureState.Critical:
                    _adaptiveShakeScale *= _adaptiveVRAMCriticalShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMCriticalPostFxScale;
                    _adaptiveMaxActiveShakes = math.clamp(_adaptiveCriticalMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;

                case VRAMMonitor.VRAMPressureState.Warning:
                    _adaptiveShakeScale *= _adaptiveVRAMWarningShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMWarningPostFxScale;
                    _adaptiveMaxActiveShakes = math.clamp(_adaptiveWarningMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;
            }

            _adaptiveDisableInteractionDoF =
                QualitySettings.GetQualityLevel() == 0 ||
                !_depthOfFieldEnabled ||
                _adaptivePostFxScale <= _adaptiveDoFDisableThreshold;

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

        private static T ResolveComponentInParents<T>(Transform start) where T : Component
        {
            Transform current = start;
            while (current != null)
            {
                if (current.TryGetComponent(out T component))
                    return component;

                current = current.parent;
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_mainCamera == null || _cameraTransform == null) return;

            // Shake displacement vector
            if (_shakeOffset.sqrMagnitude > 0.000001f)
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
                float coneRadius = math.tan(math.radians(currentFOV * 0.5f)) * coneDistance;

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

    }
}
