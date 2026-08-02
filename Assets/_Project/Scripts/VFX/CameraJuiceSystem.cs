// ============================================================================
// HECTON-8 вЂ” CameraJuiceSystem.cs
// Camera shake, FOV effects, and post-processing modulation system.
// Zero-GC hot paths, 1.0ms frame budget, native tick-driven transitions.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using CameraJuiceImpactSignal = Hecton8.Core.Contracts.Signals.CameraJuiceImpactSignal;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Optimization;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.VFX
{
    /// <summary>
    /// Camera presentation runtime managing shake, FOV effects, and post-processing modulation.
    /// Integrates with HectonSurvivalSystem, PlayerMovement, InteractionEvents, GameTickManager, SaveManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CameraJuiceSystem : MonoBehaviour, ICameraJuiceSystem, ISlowTickable, ILateFrameTickable, ISaveable, IInteractionEventListener, IPhysicsImpactEventListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        // в•ђв•ђв•ђ CACHED REFERENCES в•ђв•ђв•ђ
        [StructLayout(LayoutKind.Explicit, Size = CameraJuiceTelemetryEntrySizeBytes)]
        private struct CameraJuiceTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public float TraumaScalar;
            [FieldOffset(12)] public float MaxTranslationalOffsetMagnitude;
            [FieldOffset(16)] public float3 Offset;
            [FieldOffset(28)] public float3 RotationDegrees;
            [FieldOffset(40)] public int IncomingSignalCount;
            [FieldOffset(44)] public float BurstExecutionMicroseconds;
            [FieldOffset(48)] public float GlobalQualityWeight01;
            [FieldOffset(52)] public float DirectionalImpulseMagnitude;
            [FieldOffset(56)] public uint StateHash;
            [FieldOffset(60)] public uint Sequence;
        }

        private Camera _mainCamera;
        private Volume _urpVolume;
        private Transform _cameraTransform;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private Rigidbody _submarineHullRigidbody;
        private IPhysicsStateEventService _physicsStateEvents;
        private DynamicResolutionScaler _dynamicResolutionScaler;
        private IVramBudgetReadModel _vramMonitor;
        private ITickDispatcher _dispatcher;
        private Vector3 _cameraLocalRestPosition;
        private ParticleSystem _speedLineParticles;
        private ParticleSystemRenderer _speedLineRenderer;
        private Transform _speedLineRoot;
        private float _speedLineIntensity;
        private float _pendingSpeedLineDeltaTime;
        private float _cachedSpeedLineEmissionRate = -1f;
        private float _cachedSpeedLineVelocityZ = float.MinValue;
        private float _cachedSpeedLineStretch = -1f;
        private bool _speedLineVisualDirty;
        private bool _physicsImpactRegistered;
        private bool _projectionFovDirty;
        private float _queuedProjectionFov;
        private bool _projectionFovApplied;
        private float _lastAppliedProjectionFov;
        private bool _biomeBlendDirty;
        private BiomeProfile _queuedBiomeBlendFrom;
        private BiomeProfile _queuedBiomeBlendTo;
        private float _queuedBiomeBlendT;
        private bool _healthVignetteDirty;
        private float _queuedHealthVignetteIntensity;
        private bool _queuedHealthVignetteActive;
        private bool _interactionDofDirty;
        private bool _queuedInteractionDofActive;
        private float _queuedInteractionFocusDistance;
        private bool _pauseDofDirty;
        private bool _pauseDofRestoreDirty;
        private float _queuedPauseDofFocusDistance;
        private float _queuedPauseDofFocalLength;
        private float _queuedPauseDofAperture;
        private float _queuedPauseDofGaussianEnd;
        private float _queuedPauseDofGaussianMaxRadius;

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
        [SerializeField, Tooltip("Authored camera-local speed-line particles. Shipping builds require this object and do not synthesize particle systems.")]
        private ParticleSystem _authoredSpeedLineParticles;

        [SerializeField, Tooltip("Optional renderer paired with authored speed-line particles. Resolved from the authored particle object if omitted.")]
        private ParticleSystemRenderer _authoredSpeedLineRenderer;

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

        // в•ђв•ђв•ђ SHAKE STATE в•ђв•ђв•ђ
        private Vector3 _shakeOffset;
        private const int MAX_ACTIVE_SHAKES = 8;
        private const float MAX_SHAKE_DISPLACEMENT = 0.5f;
        private const float DEFAULT_SHAKE_CLIP_SAFE_DISPLACEMENT = 0.05f;
        private const float PROCEDURAL_TRAUMA_DECAY_RATE = 1.65f;
        private const float PROCEDURAL_SHAKE_FREQUENCY = 18f;
        private const float PROCEDURAL_TRANSLATION_AMPLITUDE_METERS = 0.07f;
        private const float PROCEDURAL_ROTATION_AMPLITUDE_DEGREES = 2.4f;
        private const float PROCEDURAL_ROLL_AMPLITUDE_DEGREES = 5.5f;
        private const float PROCEDURAL_DIRECTIONAL_BIAS_SECONDS = 0.06f;
        private const float PROCEDURAL_HIT_STOP_THRESHOLD = 0.8f;
        private const float PROCEDURAL_IMPACT_FOV_MIN_SEVERITY = 0.52f;
        private const float PROCEDURAL_IMPACT_FOV_MAX_DEGREES = 4.0f;
        private const float PROCEDURAL_IMPACT_FOV_DURATION = 0.16f;
        private const int PROCEDURAL_MAX_IMPACTS_PER_FRAME = 32;
        private const int CAMERA_JUICE_TELEMETRY_CAPACITY = 300;
        private const int CameraJuiceTelemetryEntrySizeBytes = 64;
        private const int CameraJuiceTelemetryDumpHeaderSizeBytes = 32;
        private const SystemID CameraJuiceOwnerSystemId = SystemID.Vfx;
        private const uint CameraJuiceTelemetryDumpMagic = 0x354A4353u; // SCJ5
        private const uint CameraJuiceTelemetryDumpVersion = 4u;
        private const uint CAMERA_JUICE_HIT_STOP_REASON_HASH = 0xC45A1CEu;
        private const uint KccVelocityCameraJuiceMaxAgeFrames = 12u;
        private float _trauma;
        private float3 _proceduralShakeTranslation;
        private float3 _proceduralShakeRotationDegrees;
        private IDataVault _dataVault;
        private VaultGenerationHandle<CameraJuiceTelemetryEntry> _cameraJuiceTelemetryHandle;
        private bool _ownsCameraJuiceTelemetryBuffer;
        private uint _cameraJuiceTelemetryCursor;
        private bool _cameraJuiceTelemetryDumped;
        private bool _cameraJuiceTelemetryDumpRequested;
        private bool _cameraJuiceTelemetryDumpDeferred;
#pragma warning disable CS0414
        private bool _cameraJuiceTelemetryReady;
#pragma warning restore CS0414
        private float _submarineImpactShakeSign = 1f;
        private int _lastSeismicSignalSequence;

        // в•ђв•ђв•ђ FOV STATE в•ђв•ђв•ђ
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
        private const float ProjectionFovDirtyEpsilon = 0.001f;
        private const float FovPresentationCalmEpsilon = 0.01f;

        // в•ђв•ђв•ђ POST-PROCESSING STATE в•ђв•ђв•ђ
        private Vignette _healthVignette;
        private DepthOfField _interactionDoF;
        private bool _postProcessingEnabled = true;
        private bool _healthO2EffectsEnabled = true;

        // в•ђв•ђв•ђ BIOME PROFILE в•ђв•ђв•ђ
        [Header("Biome Settings")]
        [SerializeField, Tooltip("Fallback biome profile used when no biome is specified or the target biome is invalid.")]
        private BiomeProfile _defaultFallbackBiome;
        private BiomeProfile _currentBiome;
        private BiomeProfile _targetBiome;
        private BiomeProfile _biomeBlendFrom;
        private float _biomeBlendDuration;
        private float _biomeBlendElapsed;
        private bool _biomeBlendActive;

        // в•ђв•ђв•ђ INTERACTION FOCUS в•ђв•ђв•ђ
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

        // в•ђв•ђв•ђ SETTINGS в•ђв•ђв•ђ
        [Header("в”Ђв”Ђ Settings в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
        [SerializeField, Tooltip("Fallback biome profile used when transitioning to a null biome.")]
        private BiomeProfile _fallbackBiomeProfile;

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

        // в•ђв•ђв•ђ PUBLIC SETTINGS PROPERTIES в•ђв•ђв•ђ

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
        /// Enable depth-of-field post-processing effect.
        /// Applied immediately without scene reload.
        /// </summary>
        public bool DepthOfFieldEnabled
        {
            get => _depthOfFieldEnabled;
            set => _depthOfFieldEnabled = value;
        }

        // в•ђв•ђв•ђ TICK REGISTRATION в•ђв•ђв•ђ
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
        private int _dependencyResolveSlowTickCountdown;

        // в•ђв•ђв•ђ EFFECT ENABLE FLAGS в•ђв•ђв•ђ
        private bool _shakeEnabled = true;
        private bool _fovEnabled = true;
        private bool _sprintFOVEnabled = true;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveShakeScale = 1f;
        private float _adaptiveFOVScale = 1f;
        private float _adaptivePostFxScale = 1f;
        private float _adaptiveRenderScale = 1f;
        private float _cachedPresentationMotionScale = 1f;
        private int _adaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;
        private byte _adaptiveVRAMPressureState = VramPressureStateCodes.Stable;
        private bool _adaptiveDisableInteractionDoF;

        // в•ђв•ђв•ђ SHADER PROPERTY IDS в•ђв•ђв•ђ
        // Note: Volume overrides use direct .value assignment, not MaterialPropertyBlock

        // в•ђв•ђв•ђ DEBUG в•ђв•ђв•ђ
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _frameBudgetLogCooldownSeconds;
        private const string DebugPressureStable = "Stable";
        private const string DebugPressureWarning = "Warning";
        private const string DebugPressureCritical = "Critical";
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveShakeScale = 1f;
        [SerializeField] private float _debugAdaptiveFOVScale = 1f;
        [SerializeField] private float _debugAdaptivePostFxScale = 1f;
        [SerializeField] private float _debugPresentationMotionScale = 1f;
        [SerializeField] private string _debugAdaptiveVRAMPressure = "Stable";
        [SerializeField] private int _debugAdaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;
#endif

        // в•ђв•ђв•ђ LIFECYCLE в•ђв•ђв•ђ

        /// <summary>
        /// Ensures a live <see cref="CameraJuiceSystem"/> is registered as
        /// <see cref="GlobalRegistry.CameraJuice"/> / <see cref="ICameraJuiceSystem"/>.
        /// Script GUID 394c096b405b1e745b881283ae9a05c6 has ZERO scene/prefab hits.
        /// No factory existed; Awake/OnEnable only register when already present.
        /// SceneRuntimeService BeginInputReclaimFov and SystemDispatcher consumers hit a
        /// permanent null without this path. Prefer calling after player publication so
        /// <c>TryResolveCamera</c> can bind PlayerCamera.
        /// </summary>
        public static CameraJuiceSystem EnsureRuntimeInstance()
        {
            ICameraJuiceSystem registered = GlobalRegistry.CameraJuice;
            if (IsCameraJuiceRuntimeUsable(registered))
                return registered as CameraJuiceSystem;

            CameraJuiceSystem stale = registered as CameraJuiceSystem;
            if (!ReferenceEquals(stale, null))
            {
                GlobalRegistry.UnregisterCameraJuiceRuntime(registered);
                stale._serviceRegistered = false;
            }
            else if (!ReferenceEquals(registered, null))
            {
                // Foreign ICameraJuiceSystem implementor owns the slot.
                return null;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Sole CameraJuice owner; must construct when bootstrap reorders.
            GameObject runtimeRoot = new GameObject("[CameraJuiceSystem]"); // COLD ALLOC
            return runtimeRoot.AddComponent<CameraJuiceSystem>();
        }

        private void Awake()
        {
            if (Application.isPlaying && !TryRegisterToGlobalRegistry())
                return;

            RefreshCachedRegistryServices();
            RefreshCachedPresentationMotionScale();
            if (!TryResolveCamera())
            {
                LogMainCameraMissing();
                TryUnregister();
                TryUnregisterFromGlobalRegistry();
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
                if (_urpVolume.profile.TryGet(out _interactionDoF) == false)
                {
                    LogDepthOfFieldMissing();
                }
            }

            TryResolveGameplayDependencies();
            SyncDependencyFlags();
            EnsureCameraJuiceTelemetry();
            EnsureProceduralCameraJuiceBuffers();
            EnsureCameraSpeedLineParticles();

        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())
                return;

            TryRegisterHotSwapListener();
            if (Application.isPlaying)
                CameraJuiceSignals.EnsurePrewarmed();

            TryRegisterDispatcherTicks();
            TryRegisterLateFrame();

            TryResolveGameplayDependencies();
            EnsureProceduralCameraJuiceBuffers();
            EnsureCameraSpeedLineParticles();

            InteractionEvents.Register(this);
            TryRegisterPhysicsImpactListener();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            // Unregister from GameTickManager
            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterHotSwapListener();

            InteractionEvents.Unregister(this);
            TryUnregisterPhysicsImpactListener();

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
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineHullRigidbody = null;
            _physicsStateEvents = null;
            _dynamicResolutionScaler = null;
            _vramMonitor = null;
            _dispatcher = null;
            StopCameraSpeedLineParticles();
            ReleaseProceduralCameraJuiceBuffers();

            if (_cameraTransform != null)
            {
                _cameraTransform.localPosition = _cameraLocalRestPosition;
            }

            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView = _baseFOV;
                _mainCamera.ResetProjectionMatrix();
                _projectionFovDirty = false;
                _lastAppliedProjectionFov = _baseFOV;
                _projectionFovApplied = true;
            }

            ReleaseCameraJuiceTelemetry();
        }

        private void TryUnregister()
        {
            TryUnregisterLateFrame();

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registered = false;
            }
        }

        private void TryRegisterDispatcherTicks()
        {
            if (_runtimeOwnerAborted || _registered || !Application.isPlaying || _dispatcher == null)
                return;

            bool slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            _registered = slowTickRegistered;
        }

        private bool TryRegisterToGlobalRegistry()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
            {
                if (IsCameraJuiceRuntimeUsable(registeredRuntime))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterCameraJuiceRuntime(registeredRuntime);
            }

            GlobalRegistry.RegisterCameraJuiceRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.CameraJuice, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return true;
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            GlobalRegistry.UnregisterCameraJuiceRuntime(this);
            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;
            if (ReferenceEquals(registeredRuntime, null) || ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsCameraJuiceRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterCameraJuiceRuntime(registeredRuntime);
            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            LogDuplicateInstanceDetected();
            TryUnregister();
            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterCameraJuiceRuntime(this);
                _serviceRegistered = false;
            }

            TryUnregisterHotSwapListener();
            InteractionEvents.Unregister(this);
            TryUnregisterPhysicsImpactListener();
            StopCameraSpeedLineParticles();
            ReleaseProceduralCameraJuiceBuffers();
            ReleaseCameraJuiceTelemetry();

            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineHullRigidbody = null;
            _dynamicResolutionScaler = null;
            _vramMonitor = null;
            _dispatcher = null;
            _runtimeOwnerAborted = true;
            _registered = false;
            _registeredLateFrame = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _physicsImpactRegistered = false;
            enabled = false;
            Destroy(gameObject);
        }

        private static bool IsCameraJuiceRuntimeUsable(ICameraJuiceSystem service)
        {
            CameraJuiceSystem runtime = service as CameraJuiceSystem;
            if (runtime == null)
            {
                UnityEngine.Object unityObject = service as UnityEngine.Object;
                if (!ReferenceEquals(unityObject, null))
                    return unityObject != null;

                return !ReferenceEquals(service, null);
            }

            return runtime != null &&
                   runtime._serviceRegistered &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private void TryRegisterLateFrame()
        {
            if (_runtimeOwnerAborted || _registeredLateFrame || !Application.isPlaying || _dispatcher == null)
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
            if (_runtimeOwnerAborted)
                return;

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

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            if (_runtimeOwnerAborted)
                return;

            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Player, GlobalRegistry.Player);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Submarine, GlobalRegistry.Submarine);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DynamicResolutionRuntime, GlobalRegistry.DynamicResolution);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.VRAMMonitorRuntime, GlobalRegistry.VRAMBudgetReadModel);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.PhysicsStateManager, GlobalRegistry.PhysicsStateEvents);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

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
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                    break;
            }
        }

        private void TryRegisterPhysicsImpactListener()
        {
            if (_runtimeOwnerAborted || _physicsImpactRegistered)
                return;

            RebindPhysicsStateEventService(GlobalRegistry.PhysicsStateEvents);
        }

        private void TryUnregisterPhysicsImpactListener()
        {
            if (!_physicsImpactRegistered)
            {
                _physicsStateEvents = null;
                return;
            }

            _physicsStateEvents?.UnregisterImpactListener(this);
            _physicsStateEvents = null;
            _physicsImpactRegistered = false;
        }

        private void RebindPhysicsStateEventService(IPhysicsStateEventService physicsStateEvents)
        {
            if (_runtimeOwnerAborted)
                return;

            if (ReferenceEquals(_physicsStateEvents, physicsStateEvents) && _physicsImpactRegistered)
                return;

            if (_physicsImpactRegistered)
                _physicsStateEvents?.UnregisterImpactListener(this);

            _physicsStateEvents = physicsStateEvents;
            _physicsImpactRegistered = false;

            if (_physicsStateEvents == null ||
                !isActiveAndEnabled ||
                !IsPhysicsStateEventServiceUsable(_physicsStateEvents))
                return;

            _physicsStateEvents.RegisterImpactListener(this);
            _physicsImpactRegistered = true;
        }

        private static bool IsPhysicsStateEventServiceUsable(IPhysicsStateEventService physicsStateEvents)
        {
            return physicsStateEvents != null && physicsStateEvents.IsInitialized;
        }

        private void BindPlayerRuntime(IPlayerRuntimeContext playerRuntimeContext)
        {
            if (_runtimeOwnerAborted)
                return;

            if (ReferenceEquals(_playerRuntimeContext, playerRuntimeContext))
                return;

            _playerRuntimeContext = playerRuntimeContext;
            if (_survivalSystemReference == null)
                _survivalSystem = playerRuntimeContext != null ? playerRuntimeContext.SurvivalSystem : null;
            if (_playerMovementReference == null)
                _playerMovement = playerRuntimeContext != null ? playerRuntimeContext.PlayerMovement : null;

            SyncDependencyFlags();
        }

        private void BindSubmarineRuntime(ISubmarineRuntimeContext submarineRuntimeContext)
        {
            if (_runtimeOwnerAborted)
                return;

            _submarineRuntimeContext = submarineRuntimeContext;
            _submarineHullRigidbody = submarineRuntimeContext != null ? submarineRuntimeContext.HullRigidbody : null;
        }

        private void BindDataVault(IDataVault vault)
        {
            if (_runtimeOwnerAborted)
                return;

            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseCameraJuiceTelemetryBuffer(_dataVault);
            ReleaseProceduralCameraJuiceBuffers();
            _dataVault = vault;
            ResetCameraJuiceTelemetryEpochState();
            RefreshCameraJuiceColdVaultHandles();
            EnsureCameraJuiceTelemetry();
            EnsureProceduralCameraJuiceBuffers();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterFromGlobalRegistry();
            TryUnregisterHotSwapListener();
            InteractionEvents.Unregister(this);
            TryUnregisterPhysicsImpactListener();

            ReleaseProceduralCameraJuiceBuffers();
            ReleaseCameraJuiceTelemetry();

        }

        // в•ђв•ђв•ђ ITICKABLE в•ђв•ђв•ђ

        /// <summary>
        /// Per-frame update for camera shake, FOV, and depth-of-field focus distance.
        /// </summary>
        private void AdvanceCameraJuicePresentation(float dt)
        {
            if (_runtimeOwnerAborted)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTicks = Stopwatch.GetTimestamp();
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
                FailClosedProceduralCameraJuiceFault();
                _shakeEnabled = false;
            }

            try
            {
                UpdateFOV(dt);
                QueueCameraSpeedLineUpdate(dt);
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
            float safeDt = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            _frameBudgetLogCooldownSeconds = math.max(0f, _frameBudgetLogCooldownSeconds - safeDt);
            float frameTime = (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
            if (frameTime > 1.0f && _frameBudgetLogCooldownSeconds <= 0f)
            {
                _frameBudgetLogCooldownSeconds = 5f;
                LogFrameBudgetExceeded();
            }
#endif
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            AdvanceCameraJuicePresentation(SystemDispatcher.CurrentFrameDeltaTime);

            if (_speedLineVisualDirty)
            {
                _speedLineVisualDirty = false;
                float speedLineDeltaTime = _pendingSpeedLineDeltaTime;
                _pendingSpeedLineDeltaTime = 0f;
                UpdateCameraSpeedLines(speedLineDeltaTime);
            }

            FlushQueuedProjectionFov();
            FlushQueuedBiomeBlend();
            FlushQueuedInteractionDof();
            FlushQueuedPauseDof();
            FlushQueuedHealthVignette();
            PublishProceduralCameraJuiceProjection();
            ApplyPostAupShakeOffset();
            RecordCameraJuiceTelemetry();
        }

        // в•ђв•ђв•ђ ISLOWTICKABLE в•ђв•ђв•ђ

        /// <summary>
        /// 2Hz update for health and O2 post-processing effects.
        /// </summary>
        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            RefreshCachedPresentationMotionScale();

            if (_dependencyResolveSlowTickCountdown <= 0)
            {
                _dependencyResolveSlowTickCountdown = 4;
                RefreshGameplayDependenciesFromCachedRuntime();
            }
            else
            {
                _dependencyResolveSlowTickCountdown--;
            }

            RecoverCameraJuiceVaultBindings();
            FlushDeferredCameraJuiceTelemetryDump();

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

        }

        private void RefreshCachedPresentationMotionScale()
        {
            float motionScale = 1f;
            if (SettingsManager.TryGetInstance(out SettingsManager settings) && settings != null)
                motionScale = settings.UiMotionScale;

            _cachedPresentationMotionScale = math.saturate(math.isfinite(motionScale) ? motionScale : 1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugPresentationMotionScale = _cachedPresentationMotionScale;
#endif
        }

        // в•ђв•ђв•ђ ISAVEABLE в•ђв•ђв•ђ

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDuplicateInstanceDetected()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Duplicate instance detected. Destroying duplicate.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMainCameraMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] MainCamera not found. System disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVolumeMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] URPVolume not found. Post-processing disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVignetteMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] Vignette override not found in Volume profile. Health vignette presentation disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthOfFieldMissing()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[CameraJuiceSystem] DepthOfField override not found in Volume profile. Depth-of-field presentation disabled.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeCalculationFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Shake calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullShakeProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] TriggerShake called with null profile.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeMaxDisplacementClamped(float maxDisplacement)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile MaxDisplacement out of range [0, 1]. Clamping.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogShakeDurationDefaulted(float duration)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] ShakeProfile Duration invalid. Using default 0.5s.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNullBiomeProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] TransitionToBiome called with null biome. Using default fallback.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFovCalculationFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] FOV calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBiomeBlendFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Biome blend failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInteractionFocusFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Interaction focus calculation failed.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFrameBudgetExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning("[CameraJuiceSystem] Frame time exceeded budget.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogHealthPostProcessingFailed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[CameraJuiceSystem] Health post-processing failed.");
#endif
        }

        public int SavePriority => 75;
        public int LoadPriority => 75;

        public void PopulateSaveData(SaveData data)
        {
            if (_runtimeOwnerAborted || data == null) return;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (_runtimeOwnerAborted || data == null) return;
        }

        // в•ђв•ђв•ђ PUBLIC API в•ђв•ђв•ђ

        /// <summary>
        /// Applies the dispatcher-owned pause menu depth-of-field isolation weight.
        /// </summary>
        /// <param name="weight">Normalized pause menu focus weight.</param>
        public void ApplyPauseDepthOfFieldWeight(float weight)
        {
            if (_runtimeOwnerAborted)
                return;

            _pauseDepthOfFieldWeight = math.saturate(weight);
        }

        /// <summary>
        /// Reclaims gameplay control from a cinematic wide FOV without a camera snap.
        /// </summary>
        public void BeginInputReclaimFov(float startFov, float durationSeconds)
        {
            if (_runtimeOwnerAborted || !_fovEnabled || _mainCamera == null || HectonXRRuntimeState.IsXRActive)
                return;

            _inputReclaimFovStart = math.clamp(startFov, MIN_FOV, MAX_FOV);
            _inputReclaimFovTarget = math.clamp(_baseFOV, MIN_FOV, MAX_FOV);
            _inputReclaimFovDuration = math.max(0.0001f, durationSeconds);
            _inputReclaimFovElapsed = 0f;
            _inputReclaimFovActive = true;
            QueueProjectionFov(_inputReclaimFovStart);
        }

        /// <summary>
        /// Trigger camera shake with specified profile and intensity scale.
        /// </summary>
        /// <param name="profile">Shake configuration profile</param>
        /// <param name="intensityScale">Intensity multiplier (default 1.0)</param>
        public void TriggerShake(ShakeProfile profile, float intensityScale = 1.0f)
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted || !_shakeEnabled)
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
            if (_runtimeOwnerAborted || !_fovEnabled || HectonXRRuntimeState.IsXRActive)
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
            if (_runtimeOwnerAborted)
                return;

            if (biome == null)
            {
                LogNullBiomeProfile();
                if (_defaultFallbackBiome != null)
                {
                    biome = _defaultFallbackBiome;
                }
                else if (_fallbackBiomeProfile != null)
                {
                    biome = _fallbackBiomeProfile;
                }
                else
                {
                    return;
                }
            }

            if (!_postProcessingEnabled || _urpVolume == null) return;

            _targetBiome = biome;
            _biomeBlendFrom = _currentBiome ?? biome;
            _biomeBlendDuration = math.max(0.0001f, blendDuration);
            _biomeBlendElapsed = 0f;
            _biomeBlendActive = true;
            QueueBiomeBlend(_biomeBlendFrom, _targetBiome, 0f);
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

        // в•ђв•ђв•ђ READ-ONLY PROPERTIES в•ђв•ђв•ђ

        public int ActiveShakeCount =>
            _trauma > 0.001f || _cameraJuiceLastTraumaScalar > 0.001f ? 1 : 0;
        public float CurrentFOVOffset => _currentFOVOffset;
        public FOVState CurrentFOVState => _fovState;
        public bool IsPostProcessingEnabled => _postProcessingEnabled;
        public float DebugAdaptiveShakeScale => _adaptiveShakeScale;
        public float DebugAdaptiveFOVScale => _adaptiveFOVScale;
        public float DebugAdaptivePostFxScale => _adaptivePostFxScale;
        public int DebugAdaptiveMaxActiveShakes => _adaptiveMaxActiveShakes;
        public bool DebugAdaptiveDisableInteractionDoF => _adaptiveDisableInteractionDoF;

        // в•ђв•ђв•ђ PRIVATE METHODS в•ђв•ђв•ђ

        private void ConsumePlayerSprintSignals()
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted)
                return;

            _focusTarget = target;
            _focusTargetTransform = null;

            if (target is Component component)
            {
                _focusTargetTransform = component.transform;
            }
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((InteractionEventType)payload.EventType != InteractionEventType.HoverChanged)
                return;

            InteractionEvents.TryResolveTarget(in payload, out IInteractable target);
            HandleHoverChanged(target);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            if (_runtimeOwnerAborted || !_shakeEnabled)
                return;

            float severity = ResolvePhysicsImpactSeverity(in impactSignal);
            if (severity <= 0f)
                return;

            AddProceduralTrauma(severity, ResolvePhysicsImpactDirection(in impactSignal));
        }

        private void UpdateShake(float dt)
        {
            if (!_shakeEnabled)
            {
                ClearProceduralCameraJuiceProjection();
                ClearSuppressedProceduralCameraJuiceState(CameraJuiceFlagVRSomaticWriteRejected);
                return;
            }

            float effectiveShakeScale = _shakeIntensityMultiplier * _adaptiveShakeScale * ResolvePresentationMotionScale();
            if (effectiveShakeScale <= 0f)
            {
                _shakeOffset = Vector3.zero;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                ClearProceduralCameraJuiceProjection();
                ClearSuppressedProceduralCameraJuiceState(CameraJuiceFlagVRSomaticWriteRejected);
                return;
            }

            if (CanSkipProceduralCameraJuiceFrame())
            {
                MarkProceduralCameraJuiceCalmFrame();
                return;
            }

            RunProceduralCameraJuice(dt, effectiveShakeScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolvePresentationMotionScale()
        {
            float motionScale = _cachedPresentationMotionScale;
            return math.saturate(math.isfinite(motionScale) ? motionScale : 1f);
        }

        private float ResolveCameraJuiceFluidDrag01()
        {
            HectonPlayerMovement playerMovement = _playerMovement;
            if (playerMovement == null)
                return 0f;

            float immersion = math.saturate(math.isfinite(playerMovement.WaterImmersionRatio) ? playerMovement.WaterImmersionRatio : 0f);
            return playerMovement.IsPlayerSubmerged ? math.max(immersion, 1f) : immersion;
        }

        private void RecoverCameraJuiceVaultBindings()
        {
            if (_runtimeOwnerAborted)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            if (_cameraJuicePlayerKinematicStateHandle.BufferID == 0u ||
                _cameraJuicePlayerKinematicStateHandle.Generation == 0u)
            {
                RefreshCameraJuiceColdVaultHandles();
            }

            if (!_cameraJuiceTelemetryReady)
                EnsureCameraJuiceTelemetry();

            if (!_cameraJuiceBuffersSeeded)
                EnsureProceduralCameraJuiceBuffers();
        }

        private void AddProceduralTrauma(float severity01, float3 worldDirection)
        {
            if (_runtimeOwnerAborted)
                return;

            float severity = math.saturate(severity01);
            if (severity <= 0f || !math.isfinite(severity))
                return;

            _trauma = math.saturate(_trauma + ResolveTraumaAddition(severity));
            QueueProceduralCameraJuiceManualImpulse(severity, worldDirection);

            if (severity > PROCEDURAL_HIT_STOP_THRESHOLD)
                RequestProceduralHitStop();
        }

        private static float ResolveCameraJuiceQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
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
            if (!math.all(math.isfinite(direction)))
                return float3.zero;

            if (math.lengthsq(direction) > 0.000001f)
                return direction;

            return float3.zero;
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

        private static float ResolveTraumaAddition(float severity)
        {
            float safeSeverity = math.saturate(severity);
            return math.saturate(0.12f + (safeSeverity * 0.68f));
        }

        private void RequestProceduralHitStop()
        {
            if (_runtimeOwnerAborted)
                return;

            ITickDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
                dispatcher.RequestCoreTickDilation(0.05f, 3, CAMERA_JUICE_HIT_STOP_REASON_HASH);
        }

        private bool EnsureCameraJuiceTelemetry()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (!ValidateCameraJuiceTelemetryLayout())
            {
                ReleaseCameraJuiceTelemetry();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearCameraJuiceTelemetryDescriptor();
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuiceTelemetryReady = false;
                return false;
            }

            if (IsCameraJuiceTelemetryHandle(in _cameraJuiceTelemetryHandle) &&
                vault.TryReadOnlyHandle(in _cameraJuiceTelemetryHandle, out NativeArray<CameraJuiceTelemetryEntry>.ReadOnly existingTelemetry) &&
                !vault.IsCompactionFenceActive &&
                existingTelemetry.IsCreated &&
                existingTelemetry.Length >= CAMERA_JUICE_TELEMETRY_CAPACITY)
            {
                _cameraJuiceTelemetryReady = true;
                return true;
            }

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuiceTelemetryReady = false;
                return false;
            }

            if (vault.TryGetGenerationHandle<CameraJuiceTelemetryEntry>(
                    BufferID.CameraJuiceTelemetryRing,
                    out VaultGenerationHandle<CameraJuiceTelemetryEntry> borrowedHandle) &&
                IsCameraJuiceTelemetryHandle(in borrowedHandle) &&
                vault.TryReadOnlyHandle(in borrowedHandle, out existingTelemetry) &&
                !vault.IsCompactionFenceActive &&
                existingTelemetry.IsCreated &&
                existingTelemetry.Length >= CAMERA_JUICE_TELEMETRY_CAPACITY)
            {
                _cameraJuiceTelemetryHandle = borrowedHandle;
                _ownsCameraJuiceTelemetryBuffer = false;
                _cameraJuiceTelemetryReady = true;
                return true;
            }

            if (vault.IsCompactionFenceActive)
            {
                _cameraJuiceTelemetryReady = false;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                ClearCameraJuiceTelemetryDescriptor();
                return false;
            }

            VaultGenerationHandle<CameraJuiceTelemetryEntry> acquiredHandle = vault.EnsureGenerationHandle<CameraJuiceTelemetryEntry>(
                BufferID.CameraJuiceTelemetryRing,
                CAMERA_JUICE_TELEMETRY_CAPACITY,
                CameraJuiceOwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            bool ownsAcquiredHandle = true;

            if (!IsCameraJuiceTelemetryHandle(in acquiredHandle) ||
                !TryInitializeCameraJuiceTelemetryRing(vault, in acquiredHandle))
            {
                if (IsCameraJuiceTelemetryHandle(in acquiredHandle) && ownsAcquiredHandle)
                    vault.ReleaseBuffer(in acquiredHandle);

                ClearCameraJuiceTelemetryDescriptor();
                return false;
            }

            _cameraJuiceTelemetryHandle = acquiredHandle;
            _ownsCameraJuiceTelemetryBuffer = ownsAcquiredHandle;
            _cameraJuiceTelemetryReady = true;
            return true;
        }

        private bool TryInitializeCameraJuiceTelemetryRing(
            IDataVault vault,
            in VaultGenerationHandle<CameraJuiceTelemetryEntry> handle)
        {
            NativeArray<CameraJuiceTelemetryEntry> telemetry = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsCameraJuiceTelemetryHandle(in handle))
            {
                return false;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in handle, CameraJuiceOwnerSystemId, out telemetry);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !telemetry.IsCreated ||
                    telemetry.Length < CAMERA_JUICE_TELEMETRY_CAPACITY)
                {
                    return false;
                }

                InitializeCameraJuiceTelemetryRing(telemetry);
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in handle, CameraJuiceOwnerSystemId);
            }
        }

        private bool OpenCameraJuiceTelemetryReadOnly(out NativeArray<CameraJuiceTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsCameraJuiceTelemetryHandle(in _cameraJuiceTelemetryHandle) ||
                !vault.TryReadOnlyHandle(in _cameraJuiceTelemetryHandle, out telemetry) ||
                vault.IsCompactionFenceActive ||
                !telemetry.IsCreated ||
                telemetry.Length < CAMERA_JUICE_TELEMETRY_CAPACITY)
            {
                telemetry = default;
                return false;
            }

            return true;
        }

        private void ReleaseCameraJuiceTelemetry()
        {
            ReleaseCameraJuiceTelemetryBuffer(_dataVault);
            _dataVault = null;
            ResetCameraJuiceTelemetryEpochState();
        }

        private void ReleaseCameraJuiceTelemetryBuffer(IDataVault vault)
        {
            if (_ownsCameraJuiceTelemetryBuffer &&
                vault != null &&
                IsCameraJuiceTelemetryHandle(in _cameraJuiceTelemetryHandle))
            {
                vault.ReleaseBuffer(in _cameraJuiceTelemetryHandle);
            }

            ClearCameraJuiceTelemetryDescriptor();
        }

        private void ClearCameraJuiceTelemetryDescriptor()
        {
            _cameraJuiceTelemetryHandle = default;
            _ownsCameraJuiceTelemetryBuffer = false;
            _cameraJuiceTelemetryReady = false;
            _cameraJuiceTelemetryDumpRequested = false;
            _cameraJuiceTelemetryDumpDeferred = false;
        }

        private void ResetCameraJuiceTelemetryEpochState()
        {
            _cameraJuiceTelemetryCursor = 0;
            _cameraJuiceTelemetryDumped = false;
            _cameraJuiceTelemetryDumpRequested = false;
            _cameraJuiceTelemetryDumpDeferred = false;
        }

        private static bool IsCameraJuiceTelemetryHandle(in VaultGenerationHandle<CameraJuiceTelemetryEntry> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.CameraJuiceTelemetryRing) &&
                   handle.SystemID == (uint)CameraJuiceOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool ValidateCameraJuiceTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<CameraJuiceTelemetryEntry>() == CameraJuiceTelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<CameraJuiceTelemetryDumpHeader>() == CameraJuiceTelemetryDumpHeaderSizeBytes;
        }

        private void RecordCameraJuiceTelemetry()
        {
            if (_runtimeOwnerAborted)
                return;

            IDataVault vault = _dataVault;
            NativeArray<CameraJuiceTelemetryEntry> telemetry = default;
            bool acquired = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsCameraJuiceTelemetryHandle(in _cameraJuiceTelemetryHandle))
            {
                return;
            }

            try
            {
                acquired = vault.TryAcquireWriteLock(in _cameraJuiceTelemetryHandle, CameraJuiceOwnerSystemId, out telemetry);
                if (!acquired ||
                    vault.IsCompactionFenceActive ||
                    !telemetry.IsCreated ||
                    telemetry.Length < CAMERA_JUICE_TELEMETRY_CAPACITY)
                {
                    return;
                }

                int index = (int)(_cameraJuiceTelemetryCursor % (uint)CAMERA_JUICE_TELEMETRY_CAPACITY);
                telemetry[index] = new CameraJuiceTelemetryEntry
                {
                    Frame = _cameraJuiceTelemetryCursor,
                    Flags = ResolveCameraJuiceTelemetryFlags(),
                    TraumaScalar = _cameraJuiceLastTraumaScalar,
                    MaxTranslationalOffsetMagnitude = _cameraJuiceLastMaxTranslationMagnitude,
                    Offset = _proceduralShakeTranslation,
                    RotationDegrees = _proceduralShakeRotationDegrees,
                    IncomingSignalCount = _cameraJuiceLastIncomingSignalCount,
                    BurstExecutionMicroseconds = _cameraJuiceLastBurstExecutionMicros,
                    GlobalQualityWeight01 = _cameraJuiceLastQualityWeight,
                    DirectionalImpulseMagnitude = _cameraJuiceLastDirectionalImpulseMagnitude,
                    StateHash = _cameraJuiceLastStateHash,
                    Sequence = _cameraJuiceTelemetryCursor
                };
            }
            finally
            {
                if (acquired)
                    vault.ReleaseWriteLock(in _cameraJuiceTelemetryHandle, CameraJuiceOwnerSystemId);
            }

            _cameraJuiceTelemetryCursor++;

            if (_cameraJuiceTelemetryDumpRequested)
            {
                RequestDeferredCameraJuiceTelemetryDump();
            }
        }

        private void RequestDeferredCameraJuiceTelemetryDump()
        {
            _cameraJuiceTelemetryDumpRequested = false;
            if (!_cameraJuiceTelemetryDumped)
                _cameraJuiceTelemetryDumpDeferred = true;
        }

        private void FlushDeferredCameraJuiceTelemetryDump()
        {
            if (!_cameraJuiceTelemetryDumpDeferred)
                return;

            _cameraJuiceTelemetryDumpDeferred = false;
            if (!_cameraJuiceTelemetryDumped)
                DumpCameraJuiceTelemetry();
        }

        [StructLayout(LayoutKind.Explicit, Size = CameraJuiceTelemetryDumpHeaderSizeBytes)]
        private struct CameraJuiceTelemetryDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public int EntrySizeBytes;
            [FieldOffset(12)] public int Capacity;
            [FieldOffset(16)] public uint Cursor;
            [FieldOffset(20)] public int Count;
            [FieldOffset(24)] public int StartIndex;
            [FieldOffset(28)] public uint Reserved0;
        }

        private unsafe void DumpCameraJuiceTelemetry()
        {
            if (_cameraJuiceTelemetryDumped || !OpenCameraJuiceTelemetryReadOnly(out var telemetry))
                return;

            const string dumpPath = "Docs/AgentLogs/Dump_CameraJuiceSystem.bin";

            NativeArray<byte> payload = default;
            try
            {
                int count = (int)math.min(_cameraJuiceTelemetryCursor, (uint)CAMERA_JUICE_TELEMETRY_CAPACITY);
                uint start = _cameraJuiceTelemetryCursor - (uint)count;
                int startIndex = count > 0 ? (int)(start % (uint)CAMERA_JUICE_TELEMETRY_CAPACITY) : 0;
                int headerBytes = UnsafeUtility.SizeOf<CameraJuiceTelemetryDumpHeader>();
                int stride = CameraJuiceTelemetryEntrySizeBytes;
                int byteCount = headerBytes + (count * stride);
                CameraJuiceTelemetryDumpHeader header = new CameraJuiceTelemetryDumpHeader
                {
                    Magic = CameraJuiceTelemetryDumpMagic,
                    Version = CameraJuiceTelemetryDumpVersion,
                    EntrySizeBytes = CameraJuiceTelemetryEntrySizeBytes,
                    Capacity = CAMERA_JUICE_TELEMETRY_CAPACITY,
                    Cursor = _cameraJuiceTelemetryCursor,
                    Count = count,
                    StartIndex = startIndex
                };

                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(CameraJuiceSystem),
                    "CameraJuiceTelemetryDumpPayload");

                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(payloadPtr, &header, headerBytes);
                if (count > 0)
                {
                    byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int firstCount = math.min(count, CAMERA_JUICE_TELEMETRY_CAPACITY - startIndex);
                    int firstBytes = firstCount * stride;
                    int secondCount = count - firstCount;
                    UnsafeUtility.MemCpy(payloadPtr + headerBytes, telemetryPtr + (startIndex * stride), firstBytes);
                    if (secondCount > 0)
                        UnsafeUtility.MemCpy(payloadPtr + headerBytes + firstBytes, telemetryPtr, secondCount * stride);
                }

                _cameraJuiceTelemetryDumped = NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(CameraJuiceSystem),
                    "CameraJuiceTelemetryDumpPayload");
            }
        }

        private void ApplyPostAupShakeOffset()
        {
            if (_mainCamera == null)
                return;

            if (HectonXRRuntimeState.IsXRActive)
            {
                bool needsProjectionReset = _cameraJuiceProjectionDirty || _cameraJuiceProjectionResetDirty;
                _shakeOffset = Vector3.zero;
                _proceduralShakeTranslation = float3.zero;
                _proceduralShakeRotationDegrees = float3.zero;
                _cameraJuiceProjectionTranslation = float3.zero;
                _cameraJuiceProjectionRotationDegrees = float3.zero;
                _cameraJuiceProjectionDirty = false;
                _cameraJuiceProjectionResetDirty = false;
                if (needsProjectionReset)
                    ApplyCameraJuiceProjectionToCamera();
                return;
            }
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
            if (_runtimeOwnerAborted)
                return;

            if (_speedLineParticles != null || _cameraTransform == null)
                return;

            if (TryBindAuthoredSpeedLineParticles())
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int maxParticles = math.max(1, _speedLineMaxParticles);
            // COLD ALLOC: GameObject[1] + ParticleSystem[1] вЂ” camera-local cinematic speed-line emitter вЂ” owner: CameraJuiceSystem
            GameObject speedLineObject = new GameObject("PFX_Camera_SpeedLines");
            speedLineObject.layer = TransparentFxLayerIndex;
            speedLineObject.transform.SetParent(_cameraTransform, false);
            speedLineObject.transform.localPosition = new Vector3(0f, 0f, 1.35f);
            speedLineObject.transform.localRotation = Quaternion.identity;
            speedLineObject.transform.localScale = Vector3.one;
            _speedLineRoot = speedLineObject.transform;
            _speedLineParticles = speedLineObject.AddComponent<ParticleSystem>();
            _speedLineParticles.TryGetComponent(out _speedLineRenderer);
            ConfigureSpeedLineParticles(maxParticles);
#endif
        }

        private bool TryBindAuthoredSpeedLineParticles()
        {
            if (_authoredSpeedLineParticles == null)
                return false;

            _speedLineParticles = _authoredSpeedLineParticles;
            _speedLineRoot = _authoredSpeedLineParticles.transform;
            _speedLineRenderer = _authoredSpeedLineRenderer;
            if (_speedLineRenderer == null)
                _speedLineParticles.TryGetComponent(out _speedLineRenderer);

            ConfigureSpeedLineParticles(math.max(1, _speedLineMaxParticles));
            if (_speedLineParticles.isPlaying)
                _speedLineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            return true;
        }

        private void ConfigureSpeedLineParticles(int maxParticles)
        {
            if (_speedLineParticles == null)
                return;

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
            if (_runtimeOwnerAborted)
                return;

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

        private void QueueCameraSpeedLineUpdate(float dt)
        {
            if (_runtimeOwnerAborted)
                return;

            if (_speedLineParticles == null)
                return;

            if (_speedLineIntensity <= 0.001f)
            {
                float currentSpeed = ResolveCurrentCameraSpeed();
                float startSpeed = math.max(0f, _speedLineStartMetersPerSecond);
                if (currentSpeed <= startSpeed && !_speedLineParticles.isPlaying)
                    return;
            }

            _pendingSpeedLineDeltaTime += math.max(0f, math.isfinite(dt) ? dt : 0f);
            _speedLineVisualDirty = true;
        }

        private float ResolveCurrentCameraSpeed()
        {
            if (_runtimeOwnerAborted)
                return 0f;

            float speed = 0f;
            if (CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityCameraJuiceMaxAgeFrames, out Vector3 kccVelocity))
                speed = math.max(speed, ApproximateVectorMagnitude(kccVelocity));

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

        private float ResolveSwimmingVelocityFovTarget(float presentationMotionScale)
        {
            if (_swimmingFovWarpMaxOffset <= 0f ||
                _adaptiveFOVScale <= 0f ||
                presentationMotionScale <= 0f)
            {
                return 0f;
            }

            HectonPlayerMovement playerMovement = _playerMovement;
            bool velocityWarpEligible =
                (playerMovement != null && playerMovement.IsPlayerSubmerged) ||
                HasSubmarineVelocityFovSource();
            if (!velocityWarpEligible)
                return 0f;

            float currentSpeed = ResolveCurrentCameraSpeed();
            float speedRange = math.max(0.01f, _swimmingFovWarpFullSpeed - _swimmingFovWarpStartSpeed);
            float speedT = math.saturate((currentSpeed - _swimmingFovWarpStartSpeed) / speedRange);
            float smoothSpeedT = EvaluateSmoothStep01(speedT);
            return smoothSpeedT * _swimmingFovWarpMaxOffset * _adaptiveFOVScale * presentationMotionScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsProjectionFovSettledAtBase()
        {
            if (_projectionFovDirty || !_projectionFovApplied)
                return false;

            return math.abs(_lastAppliedProjectionFov - _baseFOV) <= ProjectionFovDirtyEpsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CanSkipFovPresentationFrame(float swimmingWarpTarget)
        {
            return !_fovBlendActive &&
                   !_inputReclaimFovActive &&
                   math.abs(_currentFOVOffset) <= FovPresentationCalmEpsilon &&
                   math.abs(_swimmingVelocityFovOffset) <= FovPresentationCalmEpsilon &&
                   math.abs(swimmingWarpTarget) <= FovPresentationCalmEpsilon &&
                   IsProjectionFovSettledAtBase();
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

            float presentationMotionScale = ResolvePresentationMotionScale();
            float swimmingWarpTarget = ResolveSwimmingVelocityFovTarget(presentationMotionScale);
            if (CanSkipFovPresentationFrame(swimmingWarpTarget))
            {
                _currentFOVOffset = 0f;
                _swimmingVelocityFovOffset = 0f;
                return;
            }

            // Calculate target FOV
            float targetFOV = _baseFOV +
                (_currentFOVOffset * _fovIntensityMultiplier * _adaptiveFOVScale * presentationMotionScale);

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

            QueueProjectionFov(targetFOV);
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
            _lastAppliedProjectionFov = targetFOV;
            _projectionFovApplied = true;
            if (_mainCamera.orthographic)
                return;

            if (_cameraJuiceProjectionDirty || _cameraJuiceProjectionResetDirty)
                return;

            Matrix4x4 projection = Matrix4x4.Perspective(
                targetFOV,
                _mainCamera.aspect,
                _mainCamera.nearClipPlane,
                _mainCamera.farClipPlane);
            ApplyCameraJuiceProjectionOffset(ref projection);
            _mainCamera.projectionMatrix = projection;
        }

        private void QueueProjectionFov(float targetFOV)
        {
            float clampedFov = math.clamp(targetFOV, MIN_FOV, MAX_FOV);
            if (_projectionFovDirty)
            {
                if (math.abs(_queuedProjectionFov - clampedFov) <= ProjectionFovDirtyEpsilon)
                    return;

                _queuedProjectionFov = clampedFov;
                return;
            }

            if (_projectionFovApplied && math.abs(_lastAppliedProjectionFov - clampedFov) <= ProjectionFovDirtyEpsilon)
                return;

            _queuedProjectionFov = clampedFov;
            _projectionFovDirty = true;
        }

        private void FlushQueuedProjectionFov()
        {
            if (!_projectionFovDirty)
                return;

            _projectionFovDirty = false;
            ApplyProjectionFov(_queuedProjectionFov);
        }

        private void UpdateBiomeBlend(float dt)
        {
            if (!_biomeBlendActive)
                return;

            _biomeBlendElapsed = math.min(_biomeBlendElapsed + dt, _biomeBlendDuration);
            float normalizedBlend = math.saturate(_biomeBlendElapsed / _biomeBlendDuration);
            float easedBlend = EvaluateEaseInOutQuad(normalizedBlend);
            QueueBiomeBlend(_biomeBlendFrom, _targetBiome, easedBlend);
            if (normalizedBlend >= 1f)
            {
                _currentBiome = _targetBiome;
                _biomeBlendActive = false;
            }
        }

        private void QueueBiomeBlend(BiomeProfile from, BiomeProfile to, float t)
        {
            _queuedBiomeBlendFrom = from;
            _queuedBiomeBlendTo = to;
            _queuedBiomeBlendT = math.saturate(t);
            _biomeBlendDirty = true;
        }

        private void FlushQueuedBiomeBlend()
        {
            if (!_biomeBlendDirty)
                return;

            _biomeBlendDirty = false;
            ApplyBiomeBlend(_queuedBiomeBlendFrom, _queuedBiomeBlendTo, _queuedBiomeBlendT);
        }

        private void UpdateInteractionFocus(float dt)
        {
            if (!_depthOfFieldEnabled || !_postProcessingEnabled) return;
            if (_interactionDoF == null) return;

            // Performance mode check
            if (_adaptiveDisableInteractionDoF)
            {
                QueueInteractionDof(active: false, focusDistance: 0f);
                return;
            }

            float targetFocusDistance = ResolveTargetFocusDistance();
            float currentFocusDistance = _focusDistance > 0f ? _focusDistance : targetFocusDistance;
            float focusBlendT = ResolvePadeApproach01(2f / math.max(0.02f, _focusTransitionDuration), dt);
            _focusDistance = math.lerp(currentFocusDistance, targetFocusDistance, focusBlendT);

            QueueInteractionDof(active: true, focusDistance: _focusDistance);
        }

        private void UpdatePauseDepthOfField(float dt)
        {
            if (!_depthOfFieldEnabled || !_postProcessingEnabled) return;
            if (_interactionDoF == null) return;

            if (_pauseDepthOfFieldWeight <= 0f)
            {
                bool restoreQueued = _pauseDofOverrideEngaged;
                if (_pauseDofOverrideEngaged)
                    QueuePauseDofRestore();

                _pauseDofOverrideEngaged = false;
                if (!restoreQueued)
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
            QueuePauseDof(
                math.lerp(baseFocusDistance, PauseDofFocusDistance, easedBlend),
                math.lerp(_pauseDofBaseFocalLength, PauseDofFocalLength, easedBlend),
                math.lerp(_pauseDofBaseAperture, PauseDofAperture, easedBlend),
                math.lerp(_pauseDofBaseGaussianEnd, PauseDofGaussianEnd, easedBlend),
                math.lerp(_pauseDofBaseGaussianMaxRadius, PauseDofGaussianMaxRadius, easedBlend));
        }

        private void QueueInteractionDof(bool active, float focusDistance)
        {
            _queuedInteractionDofActive = active;
            _queuedInteractionFocusDistance = math.max(0f, focusDistance);
            _interactionDofDirty = true;
        }

        private void FlushQueuedInteractionDof()
        {
            if (!_interactionDofDirty || _interactionDoF == null)
                return;

            _interactionDofDirty = false;
            if (!_queuedInteractionDofActive)
            {
                _interactionDoF.active = false;
                return;
            }

            _interactionDoF.focusDistance.value = _queuedInteractionFocusDistance;
            _interactionDoF.active = true;
        }

        private void QueuePauseDof(float focusDistance, float focalLength, float aperture, float gaussianEnd, float gaussianMaxRadius)
        {
            _queuedPauseDofFocusDistance = math.max(0f, focusDistance);
            _queuedPauseDofFocalLength = math.max(0f, focalLength);
            _queuedPauseDofAperture = math.max(0f, aperture);
            _queuedPauseDofGaussianEnd = math.max(0f, gaussianEnd);
            _queuedPauseDofGaussianMaxRadius = math.max(0f, gaussianMaxRadius);
            _pauseDofDirty = true;
            _pauseDofRestoreDirty = false;
        }

        private void QueuePauseDofRestore()
        {
            _pauseDofRestoreDirty = true;
            _pauseDofDirty = false;
        }

        private void FlushQueuedPauseDof()
        {
            if (_interactionDoF == null)
                return;

            if (_pauseDofRestoreDirty)
            {
                _pauseDofRestoreDirty = false;
                RestorePauseDofOpticalDefaults();
                _pauseDofDefaultsCaptured = false;
            }

            if (!_pauseDofDirty)
                return;

            _pauseDofDirty = false;
            _interactionDoF.focusDistance.value = _queuedPauseDofFocusDistance;
            _interactionDoF.focalLength.value = _queuedPauseDofFocalLength;
            _interactionDoF.aperture.value = _queuedPauseDofAperture;
            _interactionDoF.gaussianEnd.value = _queuedPauseDofGaussianEnd;
            _interactionDoF.gaussianMaxRadius.value = _queuedPauseDofGaussianMaxRadius;
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
            if (!TryResolveAupFromRuntimeOrigin(fromRuntimePosition, out AbsoluteUniversePosition fromAup) ||
                !TryResolveAupFromRuntimeOrigin(toRuntimePosition, out AbsoluteUniversePosition toAup))
            {
                return ResolveLocalDistanceSq(fromRuntimePosition, toRuntimePosition);
            }

            return AbsoluteUniversePosition.DistanceSq(in fromAup, in toAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static double ResolveLocalDistanceSq(Vector3 a, Vector3 b)
        {
            if (!IsFiniteVector(a) || !IsFiniteVector(b))
                return double.MaxValue;

            Vector3 delta = a - b;
            return (double)delta.x * delta.x +
                   (double)delta.y * delta.y +
                   (double)delta.z * delta.z;
        }

        private float ResolveHudPlaneFocusDistance()
        {
            if (_cameraTransform == null)
                return math.max(0.01f, _hudFocusDistance);

            SuitHUDV4CanvasOverlay overlay = null;
            SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref overlay);
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
            _lastAppliedProjectionFov = _baseFOV;
            _projectionFovApplied = true;
            _projectionFovDirty = false;
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
            if (_runtimeOwnerAborted)
                return;

            _submarineHullRigidbody = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;
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
            RefreshCameraJuiceColdVaultHandles();
        }

        private void RefreshGameplayDependenciesFromCachedRuntime()
        {
            if (_runtimeOwnerAborted)
                return;

            _submarineHullRigidbody = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;

            if (_survivalSystemReference != null)
                _survivalSystem = _survivalSystemReference;
            else if (_survivalSystem == null && _playerRuntimeContext != null)
                _survivalSystem = _playerRuntimeContext.SurvivalSystem;

            if (_playerMovementReference != null)
                _playerMovement = _playerMovementReference;
            else if (_playerMovement == null && _playerRuntimeContext != null)
                _playerMovement = _playerRuntimeContext.PlayerMovement;

            SyncDependencyFlags();
        }

        private void SyncDependencyFlags()
        {
            if (_runtimeOwnerAborted)
                return;

            _healthO2EffectsEnabled = _survivalSystem != null;
            _sprintFOVEnabled = _playerMovement != null;
        }

        private void UpdateHealthPostProcessing()
        {
            if (_survivalSystem == null || _healthVignette == null) return;

            float healthNormalized = _survivalSystem.IntegrityNormalized;

            if (healthNormalized < 0.3f)
            {
                // Calculate vignette intensity
                float intensity = math.lerp(0f, 1f, (0.3f - healthNormalized) / 0.3f) * _adaptivePostFxScale;

                QueueHealthVignette(intensity, intensity > 0.001f);
            }
            else
            {
                QueueHealthVignette(0f, active: false);
            }
        }

        private void QueueHealthVignette(float intensity, bool active)
        {
            _queuedHealthVignetteIntensity = math.max(0f, intensity);
            _queuedHealthVignetteActive = active;
            _healthVignetteDirty = true;
        }

        private void FlushQueuedHealthVignette()
        {
            if (!_healthVignetteDirty || _healthVignette == null)
                return;

            _healthVignetteDirty = false;
            _healthVignette.intensity.value = _queuedHealthVignetteIntensity;
            _healthVignette.active = _queuedHealthVignetteActive;
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!_enableAdaptiveBudgetResponse || !Application.isPlaying)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f, VramPressureStateCodes.Stable);
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

            IVramBudgetReadModel vramMonitor = _vramMonitor;
            byte pressureState = vramMonitor != null
                ? vramMonitor.PressureStateCode
                : VramPressureStateCodes.Stable;

            ApplyAdaptiveBudgetResponse(renderScale, normalized, pressureState);
        }

        private void ApplyAdaptiveBudgetResponse(
            float renderScale,
            float normalized,
            byte pressureState)
        {
            _adaptiveRenderScale = renderScale;
            _adaptiveBudgetNormalized = normalized;
            _adaptiveVRAMPressureState = pressureState;
            _adaptiveShakeScale = math.lerp(_adaptiveShakeFloor, 1f, normalized);
            _adaptiveFOVScale = math.lerp(_adaptiveFOVFloor, 1f, normalized);
            _adaptivePostFxScale = math.lerp(_adaptivePostFxFloor, 1f, normalized);
            _adaptiveMaxActiveShakes = MAX_ACTIVE_SHAKES;
            float qualityWeight01 = ResolveCameraJuiceQualityWeight01();
            float qualityPostFxScale = math.lerp(0.35f, 1f, EvaluateSmoothStep01(qualityWeight01));
            _adaptivePostFxScale *= qualityPostFxScale;

            switch (pressureState)
            {
                case VramPressureStateCodes.Critical:
                    _adaptiveShakeScale *= _adaptiveVRAMCriticalShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMCriticalPostFxScale;
                    _adaptiveMaxActiveShakes = math.clamp(_adaptiveCriticalMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;

                case VramPressureStateCodes.Warning:
                    _adaptiveShakeScale *= _adaptiveVRAMWarningShakeScale;
                    _adaptivePostFxScale *= _adaptiveVRAMWarningPostFxScale;
                    _adaptiveMaxActiveShakes = math.clamp(_adaptiveWarningMaxActiveShakes, 1, MAX_ACTIVE_SHAKES);
                    break;
            }

            _adaptiveDisableInteractionDoF =
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
        private static string ResolveDebugPressureLabel(byte pressureState)
        {
            switch (pressureState)
            {
                case VramPressureStateCodes.Critical:
                    return DebugPressureCritical;

                case VramPressureStateCodes.Warning:
                    return DebugPressureWarning;

                default:
                    return DebugPressureStable;
            }
        }
#endif

        // в•ђв•ђв•ђ EDITOR GIZMOS в•ђв•ђв•ђ

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
                float coneRadius = MathLodApproximation.ApproxTanClamped(math.radians(currentFOV * 0.5f), 4096f) * coneDistance;

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
