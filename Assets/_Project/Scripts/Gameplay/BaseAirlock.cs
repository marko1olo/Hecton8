// ============================================================================
// HECTON-8 — BaseAirlock.cs
// Entry point for underwater modules.
//
// ARCHITECTURE:
//   • IInteractable for player interaction
//   • State machine via ITickable (no coroutines)
//   • MaterialPropertyBlock for status light (zero GC)
//   • BaseAirlockEvents NativeQueue for runtime listeners
//   • Legacy UnityEvent hooks for existing scene/prefab designer wiring
//
// STATES:
//   Ready → Cycling (enter/exit) → Ready
//   Red light = Cycling, Green light = Ready
//
// INTEGRATION:
//   • OnEnvironmentChanged(bool isDry) — fires when player transitions
// ============================================================================

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay.AirlockPressurization;
using Hecton8.Interaction;
using Hecton8.World;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Airlock state machine states.
    /// </summary>
    public enum AirlockState
    {
        Ready,      // Green light, can interact
        Cycling     // Red light, animation playing
    }

    /// <summary>
    /// Entry point for underwater base modules.
    /// Implements IInteractable for player interaction.
    /// Uses ITickable state machine for airlock cycle animation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Hecton/Gameplay/Base Airlock")]
    public sealed class BaseAirlock : MonoBehaviour, IInteractable, IInteractableTextProvider, ITickable, IUpdatable, ILateFrameTickable, IOriginShiftListener, global::Hecton8.Interaction.IInteractionSignalConsumer, global::Hecton8.Interaction.IInteractionVulnerabilitySource, ILocalizationLanguageChangedListener, global::Hecton8.Interaction.IKinematicRepairTarget, IGlobalRegistryHotSwapListener
    {
        private const float DefaultWeldOverrideDurationSeconds = 5f;
        private const float MaxSignalWeldDeltaSeconds = 0.25f;
        private const float MinOverrideSignalDirectionSqr = 0.000001f;
        private const float OverrideWeldRangeSlackMeters = 0.35f;
        private const float MinimumEnvironmentSnapshotTransitionSeconds = 1.5f;
        private const float DryOceanRoarLowPassHz = 650f;
        private const float WetOceanRoarLowPassHz = 22000f;
        private const float InteriorPressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float PressureWhistleStartDeltaKPa = 450f;
        private const uint FastSqrtApproximationBias = 0x1FC00000u;
        private const float PressureWhistleFullDeltaKPa = 2200f;
        private const int PressureWhistleFrameMask = 15;
        private const float RepairHandHalfSpanMeters = 0.14f;
        private const float RepairHandVerticalBiasMeters = 0.04f;
        private const int ParentComponentResolveDepth = 32;
        private const string MissingInteriorSpawnPointMessage = "[BaseAirlock] Interior spawn point not set.";
        private const string MissingExteriorSpawnPointMessage = "[BaseAirlock] Exterior spawn point not set.";
        private const string InvalidInteriorSpawnPointPoseMessage = "[BaseAirlock] Interior spawn point pose is invalid.";
        private const string InvalidExteriorSpawnPointPoseMessage = "[BaseAirlock] Exterior spawn point pose is invalid.";
        private const float PlayerDockingSnapDurationSeconds = 0.5f;
        private const float PlayerDockingSnapInverseDuration = 1f / PlayerDockingSnapDurationSeconds;
        private const float PlayerDockingSnapCompletionSeconds = PlayerDockingSnapDurationSeconds - 0.0001f;
        private const float FallbackEqualizationSeconds = 5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Airlock Settings ───────────────────────────")]
        [Tooltip("Legacy lower-bound cycle budget. Mathematical pressure equalization resolves the active duration.")]
        [SerializeField, Range(1f, 10f)] private float cycleDuration = FallbackEqualizationSeconds;

        [Tooltip("Internal airlock chamber volume used to calculate pressure equalization time.")]
        [SerializeField, Min(0.1f)] private float airlockVolumeM3 = 18f;

        [Tooltip("Equalization flow coefficient in m3 per sqrt(kPa) per second.")]
        [SerializeField, Min(0.01f)] private float equalizationFlowM3PerSqrtKPaSecond = 1.35f;

        [Tooltip("Maximum mathematical pressure equalization time. Gas particles are not simulated.")]
        [SerializeField, Min(1f)] private float maximumEqualizationSeconds = 18f;

        [Tooltip("Transform where the player spawns when entering the base.")]
        [SerializeField] private Transform interiorSpawnPoint;

        [Tooltip("Transform where the player spawns when exiting the base.")]
        [SerializeField] private Transform exteriorSpawnPoint;

        [Header("── Status Light ───────────────────────────────")]
        [Tooltip("Renderer with the status light material.")]
        [SerializeField] private Renderer statusLightRenderer;

        [Tooltip("Material property name for emission color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Green color for Ready state.")]
        [SerializeField] private Color readyColor = new Color(0f, 1f, 0.3f);

        [Tooltip("Red color for Cycling state.")]
        [SerializeField] private Color cyclingColor = new Color(1f, 0.2f, 0.1f);

        [Tooltip("Amber color shown while emergency bulkhead lockdown overrides player control.")]
        [SerializeField] private Color lockedDownColor = new Color(1f, 0.6f, 0.08f);

        [Header("Emergency Override")]
        [Tooltip("Owning base module intentionally flooded when a lockdown override opens this quarantined airlock.")]
        [SerializeField] private BaseModule owningModule;

        [Tooltip("Continuous weld time required before a quarantined airlock unlocks.")]
        [SerializeField, Min(0.1f)] private float weldOverrideDurationSeconds = DefaultWeldOverrideDurationSeconds;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Sound played when airlock cycle starts.")]
        [SerializeField] private AudioClip cycleStartSound;

        [Tooltip("Sound played when airlock cycle completes.")]
        [SerializeField] private AudioClip cycleEndSound;

        [Header("Mathematical Bulkhead")]
        [Tooltip("Optional preauthored CSR edge hash. Zero derives a stable hash from this airlock entity id.")]
        [SerializeField] private uint emergencyBulkheadEdgeHash;

        [Tooltip("Mathematical blocking plane width in meters. Visual closure is GPU shader deformation, not Transform motion.")]
        [SerializeField, Min(0.25f)] private float emergencyBulkheadWidthMeters = 2.6f;

        [Tooltip("Mathematical blocking plane height in meters. KCC reads the data plane instead of colliders.")]
        [SerializeField, Min(0.25f)] private float emergencyBulkheadHeightMeters = 3.2f;

        [Header("Airlock Audio Snapshots")]
        [Tooltip("Audio mixer snapshot used while the player is inside dry base volume.")]
        [SerializeField] private AudioMixerSnapshot dryInteriorSnapshot;

        [Tooltip("Audio mixer snapshot used while the player is outside in flooded ocean volume.")]
        [SerializeField] private AudioMixerSnapshot wetExteriorSnapshot;

        [Tooltip("Snapshot transition duration for wet/dry airlock transitions.")]
        [SerializeField, Min(MinimumEnvironmentSnapshotTransitionSeconds)] private float environmentSnapshotTransitionSeconds = MinimumEnvironmentSnapshotTransitionSeconds;

        [Tooltip("Optional mixer containing the exposed ocean-roar low-pass cutoff parameter.")]
        [SerializeField] private AudioMixer environmentMixer;

        [Tooltip("Exposed AudioMixer float parameter controlling ocean roar low-pass cutoff in Hz.")]
        [SerializeField] private string oceanRoarLowPassCutoffParameter = "OceanRoarLowPassHz";

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when player environment changes. True = Dry (inside base), False = Wet (outside).")]
        [SerializeField] private UnityEvent<bool> OnEnvironmentChanged;

        [Tooltip("Fired when airlock cycle starts.")]
        [SerializeField] private UnityEvent OnCycleStarted;

        [Tooltip("Fired when airlock cycle completes.")]
        [SerializeField] private UnityEvent OnCycleCompleted;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private AirlockState _state = AirlockState.Ready;
        private float _cycleTimer;
        private bool _isPlayerInside; // True if player is currently inside the base
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _statusLightDirty;
        private Color _pendingStatusLightColor;
        private bool _audioPresentationDirty;
        private bool _pendingEnvironmentSnapshot;
        private bool _pendingEnvironmentInsideDryVolume;
        private bool _pendingCycleEndSound;
        private Vector3 _pendingCycleEndAudioPosition;
        private bool _pendingPressureWhistle;
        private Vector3 _pendingPressureWhistlePosition;
        private float _pendingPressureWhistleIntensity01;
        private float _pendingPressureWhistleAttackSeconds;
        private float _pendingPressureWhistleReleaseSeconds;
        private float _pendingPressureWhistleFrequencyHz;
        private bool _emergencyLockedDown;
        private bool _lockdownOverrideBlockedByFloodedNeighbor;
        private float _weldOverrideProgressSeconds;
        private int _emissionPropertyId;
        private Transform _cycleInteractor;
        private Vector3 _pendingDestinationPosition;
        private Quaternion _pendingDestinationRotation = Quaternion.identity;
        private bool _hasPendingDestination;
        private bool _inputWasEnabledBeforeCycle;
        private INativeInputManagerRuntime _cycleInputManager;
        private Transform _cachedInteractorTransform;
        private Rigidbody _cachedInteractorBody;
        private bool _cachedInteractorComponentCacheValid;
        private bool _playerDockingSnapActive;
        private Transform _snapInteractor;
        private Rigidbody _snapBody;
        private HectonPlayerMotor _snapMotor;
        private Vector3 _snapStartLocalPosition;
        private Vector3 _snapTargetLocalPosition;
        private Quaternion _snapStartLocalRotation = Quaternion.identity;
        private Quaternion _snapTargetLocalRotation = Quaternion.identity;
        private float _snapElapsedSeconds;
        private bool _snapBodyStateCached;
        private bool _snapBodyWasKinematic;
        private bool _snapBodyUseGravity;
        private float _snapBodyLinearDamping;
        private float _snapBodyAngularDamping;
        private int _pressureWhistleFrameOffset;
        private bool _bulkheadContainmentPublishPending;
        private byte _bulkheadContainmentRetryTicks;
        private AbsoluteUniversePosition _bulkheadPoseCenterAup;
        private float3 _bulkheadPoseNormal;
        private float3 _bulkheadPoseUp;
        private uint _bulkheadPoseShiftSequence;
        private bool _bulkheadPoseSnapshotValid;
        private bool _originShiftRegistered;
        private bool _hotSwapListenerRegistered;
        private IAudioService _cachedAudioService;
        private INativeInputManagerRuntime _cachedNativeInputManager;
        private IPhysicsService _cachedPhysicsService;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly uint _OverrideVulnerabilityMask = ToolCapabilityMasks.ResolveCapabilityMask(InteractionEffectType.Weld) |
                                                                  ToolCapabilityMasks.ResolveCapabilityMask(InteractionEffectType.PlasmaCut);
        // Pre-cached interaction text
        private const string DefaultEnterText = "Enter Base";
        private const string DefaultExitText = "Exit Base";
        private const string DefaultCyclingText = "Cycling...";
        private const string DefaultLockedText = "Bulkhead Lockdown";
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedEnterTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedExitTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedCyclingTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedLockedTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedEnterTextLength;
        private int _cachedExitTextLength;
        private int _cachedCyclingTextLength;
        private int _cachedLockedTextLength;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Current airlock state.</summary>
        public AirlockState State => _state;

        /// <summary>True if player is currently inside the base.</summary>
        public bool IsPlayerInside => _isPlayerInside;

        /// <summary>True while emergency lockdown overrides player interaction.</summary>
        public bool IsEmergencyLockedDown => _emergencyLockedDown;
        /// <summary>True when the habitat graph forbids manual lockdown override because the sealed neighbor is still flooded.</summary>
        public bool IsManualOverrideBlocked => _lockdownOverrideBlockedByFloodedNeighbor;

        /// <summary>Normalized welding progress toward a manual emergency override.</summary>
        public float WeldOverrideProgress01
        {
            get
            {
                float requiredSeconds = ResolveWeldOverrideDurationSeconds();
                float progressSeconds = SanitizeNonNegative(_weldOverrideProgressSeconds, 0f);
                return requiredSeconds > 0f ? Sanitize01(progressSeconds / requiredSeconds, 0f) : 0f;
            }
        }

        /// <inheritdoc />
        public uint VulnerabilityMask => _OverrideVulnerabilityMask;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: BaseAirlock

            if (statusLightRenderer == null && TryGetComponent(out Renderer cachedRenderer))
                statusLightRenderer = cachedRenderer;

            CacheOwningModule();
            RefreshBulkheadPoseSnapshot();
            _pressureWhistleFrameOffset = unchecked((int)EntityId.ToULong(GetEntityId())) & PressureWhistleFrameMask;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            TryRegisterOriginShiftListener();
            RefreshBulkheadPoseSnapshot();
            RebuildLocalizedTextCache();
            // Set initial state
            _state = AirlockState.Ready;
            _weldOverrideProgressSeconds = 0f;
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);
            PublishBulkheadContainmentState(_emergencyLockedDown);
        }

        private void Start()
        {
            CacheOwningModule();
            TryRegister();
            TryRegisterOriginShiftListener();
            RefreshBulkheadPoseSnapshot();
            PublishBulkheadContainmentState(_emergencyLockedDown);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            CancelPlayerDockingSnap();
            ReleaseCycleInputLock();
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
            ClearInteractorComponentCache();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            CancelPlayerDockingSnap();
            ReleaseCycleInputLock();
            TryUnregister();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _cachedAudioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    _cachedNativeInputManager = currentService as INativeInputManagerRuntime;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _cachedPhysicsService = currentService as IPhysicsService;
                    break;
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _bulkheadPoseSnapshotValid = false;
            _bulkheadPoseShiftSequence = shiftData.Sequence;
            _bulkheadContainmentPublishPending = true;
            _bulkheadContainmentRetryTicks = 0;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedAudioService = GlobalRegistry.Audio;
            _cachedNativeInputManager = GlobalRegistry.NativeInputRuntime;
            _cachedPhysicsService = GlobalRegistry.Physics;
        }

        private void ClearCachedRegistryServices()
        {
            _cachedAudioService = null;
            _cachedNativeInputManager = null;
            _cachedPhysicsService = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — STATE MACHINE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles airlock cycle animation.
        /// Zero GC: no allocations, uses cached values.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bulkheadContainmentPublishPending)
                RetryBulkheadContainmentPublish();

            QueuePressureDifferentialWhistle();

            if (_playerDockingSnapActive)
            {
                AdvancePlayerDockingSnap(deltaTime);
                return;
            }

            if (_state != AirlockState.Cycling)
                return;

            _cycleTimer -= deltaTime;

            if (_cycleTimer <= 0f)
            {
                CompleteCycle();
            }
        }

        // ══════════════════════════════════════════════════════════
        public void LateFrameTick()
        {
            FlushStatusLight();
            FlushAirlockAudioPresentation();
        }

        //  IInteractable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called when player's raycast first hits this object.
        /// </summary>
        public void OnHoverStart()
        {
            // Future: highlight effect, UI prompt
        }

        /// <summary>
        /// Called when player's raycast leaves this object.
        /// </summary>
        public void OnHoverEnd()
        {
            // Future: remove highlight, hide UI prompt
        }

        /// <summary>
        /// Called when player presses interact key while hovering.
        /// Starts airlock cycle if ready.
        /// </summary>
        public void Interact(Transform interactor)
        {
            if (_emergencyLockedDown || _state != AirlockState.Ready)
                return;

            StartCycle(interactor);
        }

        /// <summary>
        /// Returns the UI prompt string. Zero GC: returns cached string.
        /// </summary>
        public string GetInteractText()
        {
            return ResolveInteractTextLegacy();
        }

        private string ResolveInteractTextLegacy()
        {
            switch (_state)
            {
                case AirlockState.Ready:
                    if (_emergencyLockedDown)
                        return DefaultLockedText;
                    return _isPlayerInside ? DefaultExitText : DefaultEnterText;
                case AirlockState.Cycling:
                    return DefaultCyclingText;
                default:
                    return string.Empty;
            }
        }

        private ReadOnlySpan<char> ResolveInteractTextSpan()
        {
            switch (_state)
            {
                case AirlockState.Ready:
                    if (_emergencyLockedDown)
                        return _cachedLockedTextBuffer.AsSpan(0, _cachedLockedTextLength);
                    return _isPlayerInside
                        ? _cachedExitTextBuffer.AsSpan(0, _cachedExitTextLength)
                        : _cachedEnterTextBuffer.AsSpan(0, _cachedEnterTextLength);
                case AirlockState.Cycling:
                    return _cachedCyclingTextBuffer.AsSpan(0, _cachedCyclingTextLength);
                default:
                    return ReadOnlySpan<char>.Empty;
            }
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(ResolveInteractTextSpan(), destination, out length);
        }

        /// <summary>
        /// Consumes welding time against an emergency lockdown. Completion unlocks the door and floods the protected module.
        /// </summary>
        /// <param name="deltaTime">Continuous weld duration in seconds for this tool sample.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point used by future VFX hooks.</param>
        /// <returns>True when the weld was accepted by a quarantined door.</returns>
        public bool TryApplyWeldOverride(float deltaTime, Vector3 runtimeHitPoint)
        {
            if (!_emergencyLockedDown || _lockdownOverrideBlockedByFloodedNeighbor || _state != AirlockState.Ready)
                return false;

            if (deltaTime <= 0f || !float.IsFinite(deltaTime))
                return true;

            float requiredSeconds = ResolveWeldOverrideDurationSeconds();
            float progressSeconds = SanitizeNonNegative(_weldOverrideProgressSeconds, 0f);
            _weldOverrideProgressSeconds = math.min(requiredSeconds, progressSeconds + deltaTime);
            if (_weldOverrideProgressSeconds >= requiredSeconds)
                ForceEmergencyOverride();

            return true;
        }

        public bool TryResolveRepairSnapPoints(
            Vector3 runtimeHitPoint,
            out AbsoluteUniversePosition leftHandAup,
            out AbsoluteUniversePosition rightHandAup,
            out Quaternion toolRotation)
        {
            leftHandAup = default;
            rightHandAup = default;
            toolRotation = Quaternion.identity;
            if (!TryResolveRepairSnapRuntimePoints(
                    runtimeHitPoint,
                    out Vector3 leftRuntime,
                    out Vector3 rightRuntime,
                    out toolRotation))
            {
                return false;
            }

            return IsFinite(toolRotation) &&
                   TryResolveAupFromBulkheadPose(leftRuntime, out leftHandAup) &&
                   TryResolveAupFromBulkheadPose(rightRuntime, out rightHandAup);
        }

        private bool TryResolveRepairSnapRuntimePoints(
            Vector3 runtimeHitPoint,
            out Vector3 leftRuntime,
            out Vector3 rightRuntime,
            out Quaternion toolRotation)
        {
            leftRuntime = default;
            rightRuntime = default;
            toolRotation = Quaternion.identity;
            if (!IsFinite(runtimeHitPoint))
                return false;

            if (!TryReadBulkheadRuntimeBasis(out Vector3 forward, out Vector3 up, out Vector3 right))
                return false;

            Vector3 handCenter = runtimeHitPoint + up * RepairHandVerticalBiasMeters;
            leftRuntime = handCenter - right * RepairHandHalfSpanMeters;
            rightRuntime = handCenter + right * RepairHandHalfSpanMeters;
            toolRotation = ResolveBasisRotationNoTrig(forward, up);
            return IsFinite(leftRuntime) && IsFinite(rightRuntime) && IsFinite(toolRotation);
        }

        public bool TryResolveKinematicRepairSnap(
            in global::Hecton8.Interaction.KinematicRepairTargetProbe probe,
            out global::Hecton8.Interaction.KinematicRepairSnapPoint snapPoint)
        {
            snapPoint = default;
            if (!TryConvertAupToRuntimePosition(in probe.HitAup, out Vector3 runtimeHitPoint))
                return false;

            if (!TryResolveRepairSnapRuntimePoints(
                    runtimeHitPoint,
                    out Vector3 leftRuntimePoint,
                    out Vector3 rightRuntimePoint,
                    out Quaternion toolRotation))
            {
                return false;
            }

            if (!TryOffsetAupByRuntimeDelta(in probe.HitAup, runtimeHitPoint, leftRuntimePoint, out AbsoluteUniversePosition leftHandAup) ||
                !TryOffsetAupByRuntimeDelta(in probe.HitAup, runtimeHitPoint, rightRuntimePoint, out AbsoluteUniversePosition rightHandAup))
            {
                return false;
            }

            Vector3 runtimeAnchor = (leftRuntimePoint + rightRuntimePoint) * 0.5f;
            if (!IsFinite(runtimeAnchor))
                runtimeAnchor = runtimeHitPoint;

            if (!TryOffsetAupByRuntimeDelta(in probe.HitAup, runtimeHitPoint, runtimeAnchor, out AbsoluteUniversePosition anchorAup))
                return false;

            Vector3 surfaceNormal = TryNormalizeFinite(probe.HitNormal, out Vector3 normalizedHitNormal)
                ? normalizedHitNormal
                : toolRotation * Vector3.forward;
            snapPoint = new global::Hecton8.Interaction.KinematicRepairSnapPoint
            {
                AnchorAup = anchorAup,
                LeftHandAup = leftHandAup,
                RightHandAup = rightHandAup,
                RuntimePosition = runtimeAnchor,
                SurfaceNormal = surfaceNormal,
                ToolRotation = toolRotation,
                HitDistance = SanitizeNonNegative(probe.HitDistance, 0f),
                Blend = 1f,
                ColliderInstanceId = probe.ColliderInstanceId
            };
            return true;
        }

        private static bool TryOffsetAupByRuntimeDelta(
            in AbsoluteUniversePosition referenceAup,
            Vector3 referenceRuntimePosition,
            Vector3 targetRuntimePosition,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!referenceAup.IsFinite() ||
                !IsFinite(referenceRuntimePosition) ||
                !IsFinite(targetRuntimePosition))
            {
                return false;
            }

            double3 localDelta = new double3(
                (double)targetRuntimePosition.x - referenceRuntimePosition.x,
                (double)targetRuntimePosition.y - referenceRuntimePosition.y,
                (double)targetRuntimePosition.z - referenceRuntimePosition.z);
            targetAup = AbsoluteUniversePosition.OffsetMeters(in referenceAup, localDelta);
            return targetAup.IsFinite();
        }

        private static bool TryConvertAupToRuntimePosition(
            in AbsoluteUniversePosition positionAup,
            out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (HectonFloatingOrigin.IsShiftInProgress || !positionAup.IsFinite())
                return false;

            double3 absolutePosition = ToBulkheadAbsoluteDouble3(in positionAup);
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            Vector3 candidate = HectonFloatingOrigin.ToRuntimePosition(absolutePosition);
            if (!IsFinite(candidate))
                return false;

            runtimePosition = candidate;
            return true;
        }

        private static bool TryConvertRuntimePositionToAup(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (HectonFloatingOrigin.IsShiftInProgress || !IsFinite(runtimePosition))
                return false;

            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            positionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
            return positionAup.IsFinite();
        }

        /// <inheritdoc />
        public void ApplyInteractionSignal(in global::Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            InteractionEffectType effectType = (InteractionEffectType)signal.EffectType;
            if (effectType != InteractionEffectType.Weld && effectType != InteractionEffectType.PlasmaCut)
                return;

            if (_lockdownOverrideBlockedByFloodedNeighbor)
                return;

            if (!IsOverrideWeldSignalValid(in signal, runtimeHitPoint))
                return;

            TryApplyWeldOverride(ResolveSignalWeldDeltaSeconds(in signal), runtimeHitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  AIRLOCK LOGIC
        // ══════════════════════════════════════════════════════════

        void global::Hecton8.Interaction.IInteractionSignalConsumer.ApplyInteractionSignal(
            in global::Hecton8.Interaction.InteractionSignal signal,
            global::UnityEngine.Vector3 runtimeHitPoint)
        {
            ApplyInteractionSignal(in signal, runtimeHitPoint);
        }

        private void StartCycle(Transform player)
        {
            if (player == null)
                return;

            if (!TryResolveTeleportDestination(out Vector3 destinationPosition, out Quaternion destinationRotation))
                return;

            _state = AirlockState.Cycling;
            _cycleTimer = ResolveEqualizationDurationSeconds();
            _cycleInteractor = player;
            _pendingDestinationPosition = destinationPosition;
            _pendingDestinationRotation = destinationRotation;
            _hasPendingDestination = true;
            CaptureCycleInputLock();

            // Update status light to red
            UpdateStatusLight(cyclingColor);

            BaseAirlockEvents.TryRaiseCycleStarted(this, player);

            // Play cycle start sound
            IAudioService audio = _cachedAudioService;
            if (cycleStartSound != null &&
                audio != null &&
                TryResolveBulkheadAudioRuntimePosition(out Vector3 audioPosition))
            {
                audio.PlayAtPoint(cycleStartSound, audioPosition);
            }

            // Fire event
            OnCycleStarted?.Invoke();
        }

        private void CompleteCycle()
        {
            Transform completedInteractor = _cycleInteractor;
            if (completedInteractor != null &&
                _hasPendingDestination &&
                BeginPlayerDockingSnap(completedInteractor, _pendingDestinationPosition, _pendingDestinationRotation))
            {
                return;
            }

            if (completedInteractor != null && _hasPendingDestination)
                TeleportPlayer(completedInteractor, _pendingDestinationPosition, _pendingDestinationRotation);

            FinalizeCompletedCycle(completedInteractor);
        }

        private void FinalizeCompletedCycle(Transform completedInteractor)
        {
            _state = AirlockState.Ready;

            // Restore state light after the cycle ends.
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);

            // Play cycle end sound
            QueueCycleEndSound();

            BaseAirlockEvents.TryRaiseCycleCompleted(this, completedInteractor);
            _cycleInteractor = null;
            _hasPendingDestination = false;
            ReleaseCycleInputLock();

            // Fire event
            OnCycleCompleted?.Invoke();
        }

        private bool TryResolveTeleportDestination(out Vector3 destinationPosition, out Quaternion destinationRotation)
        {
            destinationPosition = default;
            destinationRotation = Quaternion.identity;

            // Determine destination based on current state
            Transform destination = _isPlayerInside ? exteriorSpawnPoint : interiorSpawnPoint;

            if (destination == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    _isPlayerInside ? MissingExteriorSpawnPointMessage : MissingInteriorSpawnPointMessage,
                    this);
#endif
                return false;
            }

            destinationPosition = destination.position;
            destinationRotation = destination.rotation;
            if (!IsFinite(destinationPosition) || !IsFinite(destinationRotation))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    _isPlayerInside ? InvalidExteriorSpawnPointPoseMessage : InvalidInteriorSpawnPointPoseMessage,
                    this);
#endif
                return false;
            }

            return true;
        }

        private void TeleportPlayer(Transform player, Vector3 destinationPosition, Quaternion destinationRotation)
        {
            ResolveInteractorBody(player, out Rigidbody playerBody);

            bool useSafeTeleportProtocol = Application.isPlaying;
            if (useSafeTeleportProtocol)
                HectonFloatingOrigin.BeginSafeTeleportProtocol();

            try
            {
                if (TryResolveHydroPlayerMotor(player, playerBody, out HectonPlayerMotor hydroMotor))
                    TeleportHydroPlayer(player, hydroMotor, destinationPosition, destinationRotation);
                else if (playerBody != null)
                    TeleportBody(playerBody, destinationPosition, destinationRotation, _cachedPhysicsService);
                else
                    player.SetPositionAndRotation(destinationPosition, destinationRotation);
            }
            finally
            {
                if (useSafeTeleportProtocol)
                    HectonFloatingOrigin.EndSafeTeleportProtocol();
            }

            ApplyCompletedEnvironmentTransition(player);
        }

        private bool BeginPlayerDockingSnap(Transform player, Vector3 destinationPosition, Quaternion destinationRotation)
        {
            if (player == null || !IsFinite(destinationPosition) || !IsFinite(destinationRotation))
                return false;

            Transform frame = _cachedTransform != null ? _cachedTransform : transform;
            if (frame == null || !IsFinite(frame.position) || !IsFinite(frame.rotation))
                return false;

            ResolveInteractorBody(player, out Rigidbody playerBody);
            Vector3 startPosition = player.position;
            Quaternion startRotation = player.rotation;
            if (!IsFinite(startPosition) || !IsFinite(startRotation))
                return false;

            Quaternion inverseFrameRotation = Quaternion.Inverse(frame.rotation);
            _snapStartLocalPosition = frame.InverseTransformPoint(startPosition);
            _snapTargetLocalPosition = frame.InverseTransformPoint(destinationPosition);
            _snapStartLocalRotation = inverseFrameRotation * startRotation;
            _snapTargetLocalRotation = inverseFrameRotation * destinationRotation;
            _snapInteractor = player;
            _snapBody = playerBody;
            _snapMotor = TryResolveHydroPlayerMotor(player, playerBody, out HectonPlayerMotor hydroMotor)
                ? hydroMotor
                : null;
            _snapElapsedSeconds = 0f;
            _playerDockingSnapActive = true;

            if (_snapMotor == null && _snapBody != null)
            {
                _snapBodyWasKinematic = _snapBody.isKinematic;
                _snapBodyUseGravity = _snapBody.useGravity;
                _snapBodyLinearDamping = _snapBody.linearDamping;
                _snapBodyAngularDamping = _snapBody.angularDamping;
                _snapBodyStateCached = true;
                IPhysicsService physicsService = _cachedPhysicsService;
                if (physicsService != null)
                {
                    physicsService.QueueLinearVelocitySet(_snapBody, Vector3.zero, wake: false);
                    physicsService.QueueAngularVelocitySet(_snapBody, Vector3.zero, wake: false);
                }

                _snapBody.useGravity = false;
                _snapBody.linearDamping = 0f;
                _snapBody.angularDamping = 0f;
                _snapBody.isKinematic = true;
            }

            ApplyPlayerDockingSnapPose(0f);
            return true;
        }

        private void AdvancePlayerDockingSnap(float deltaTime)
        {
            float safeDeltaTime = SanitizeNonNegative(deltaTime, 0f);
            _snapElapsedSeconds = math.min(PlayerDockingSnapDurationSeconds, _snapElapsedSeconds + safeDeltaTime);
            float normalizedTime = Sanitize01(_snapElapsedSeconds * PlayerDockingSnapInverseDuration, 0f);
            ApplyPlayerDockingSnapPose(SmoothStep01(normalizedTime));
            if (_snapElapsedSeconds < PlayerDockingSnapCompletionSeconds)
                return;

            CompletePlayerDockingSnap();
        }

        private void ApplyPlayerDockingSnapPose(float easedTime)
        {
            Transform frame = _cachedTransform != null ? _cachedTransform : transform;
            if (frame == null)
                return;

            Vector3 localPosition = LerpUnclampedVector(_snapStartLocalPosition, _snapTargetLocalPosition, easedTime);
            Quaternion localRotation = NlerpQuaternion(_snapStartLocalRotation, _snapTargetLocalRotation, easedTime);
            Vector3 worldPosition = frame.TransformPoint(localPosition);
            Quaternion worldRotation = frame.rotation * localRotation;
            if (!IsFinite(worldPosition) || !IsFinite(worldRotation))
                return;

            if (_snapMotor != null)
            {
                _snapMotor.MovePosition(worldPosition);
                if (_snapInteractor != null)
                    _snapInteractor.SetPositionAndRotation(worldPosition, worldRotation);
                return;
            }

            if (_snapBody != null)
            {
                _snapBody.MovePosition(worldPosition);
                _snapBody.MoveRotation(worldRotation);
                return;
            }

            if (_snapInteractor != null)
                _snapInteractor.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private void CompletePlayerDockingSnap()
        {
            Transform completedInteractor = _snapInteractor;
            ApplyPlayerDockingSnapPose(1f);
            RestorePlayerDockingSnapBodyState();
            _playerDockingSnapActive = false;
            _snapInteractor = null;
            _snapBody = null;
            _snapMotor = null;
            ApplyCompletedEnvironmentTransition(completedInteractor);
            FinalizeCompletedCycle(completedInteractor);
        }

        private void CancelPlayerDockingSnap()
        {
            if (!_playerDockingSnapActive)
                return;

            RestorePlayerDockingSnapBodyState();
            _playerDockingSnapActive = false;
            _snapInteractor = null;
            _snapBody = null;
            _snapMotor = null;
        }

        private void RestorePlayerDockingSnapBodyState()
        {
            if (_snapBody == null || !_snapBodyStateCached)
            {
                _snapBodyStateCached = false;
                return;
            }

            IPhysicsService physicsService = _cachedPhysicsService;
            if (physicsService != null)
            {
                physicsService.QueueLinearVelocitySet(_snapBody, Vector3.zero, wake: false);
                physicsService.QueueAngularVelocitySet(_snapBody, Vector3.zero, wake: false);
            }

            _snapBody.linearDamping = _snapBodyLinearDamping;
            _snapBody.angularDamping = _snapBodyAngularDamping;
            _snapBody.useGravity = _snapBodyUseGravity;
            _snapBody.isKinematic = _snapBodyWasKinematic;
            _snapBodyStateCached = false;
        }

        private void ApplyCompletedEnvironmentTransition(Transform player)
        {
            _isPlayerInside = !_isPlayerInside;
            QueueAirlockAudioSnapshot(_isPlayerInside);
            BaseAirlockEvents.TryRaiseEnvironmentChanged(this, player);
            OnEnvironmentChanged?.Invoke(_isPlayerInside);
        }

        private void ResolveInteractorBody(Transform player, out Rigidbody body)
        {
            body = null;
            if (player == null)
                return;

            if (!ReferenceEquals(_cachedInteractorTransform, player) || !_cachedInteractorComponentCacheValid)
            {
                _cachedInteractorTransform = player;
                player.TryGetComponent(out _cachedInteractorBody);
                _cachedInteractorComponentCacheValid = true;
            }

            body = _cachedInteractorBody;
        }

        private void ClearInteractorComponentCache()
        {
            _cycleInteractor = null;
            _cachedInteractorTransform = null;
            _cachedInteractorBody = null;
            _cachedInteractorComponentCacheValid = false;
        }

        private static void TeleportBody(Rigidbody body, Vector3 position, Quaternion rotation, IPhysicsService physicsService)
        {
            if (body.TryGetComponent(out HectonPlayerMotor playerMotor) &&
                playerMotor.HydrodynamicKccOwnsCollisionAuthority)
            {
                TeleportHydroPlayer(body.transform, playerMotor, position, rotation);
                return;
            }

            bool wasKinematic = body.isKinematic;
            bool wasDetectingCollisions = body.detectCollisions;
            bool wasSleeping = body.IsSleeping();

            body.isKinematic = true;
            body.detectCollisions = false;
            body.position = position;
            body.rotation = rotation;
            body.PublishTransform();
            body.isKinematic = false;
            body.isKinematic = wasKinematic;
            body.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                if (physicsService != null)
                {
                    physicsService.QueueLinearVelocitySet(body, Vector3.zero, wake: false);
                    physicsService.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
                }

                if (wasSleeping)
                    body.Sleep();
                else
                    body.WakeUp();
            }
            else if (wasSleeping)
            {
                body.Sleep();
            }
        }

        private static bool TryResolveHydroPlayerMotor(
            Transform player,
            Rigidbody playerBody,
            out HectonPlayerMotor playerMotor)
        {
            playerMotor = null;
            if (playerBody != null && playerBody.TryGetComponent(out playerMotor) && playerMotor.HydrodynamicKccOwnsCollisionAuthority)
                return true;

            if (player != null && player.TryGetComponent(out playerMotor) && playerMotor.HydrodynamicKccOwnsCollisionAuthority)
                return true;

            playerMotor = null;
            return false;
        }

        private static void TeleportHydroPlayer(
            Transform player,
            HectonPlayerMotor playerMotor,
            Vector3 position,
            Quaternion rotation)
        {
            if (playerMotor != null)
            {
                playerMotor.MovePosition(position);
                playerMotor.SetLinearVelocity(Vector3.zero);
            }

            if (player != null)
                player.SetPositionAndRotation(position, rotation);
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void TransitionAirlockAudioSnapshot(bool insideDryVolume)
        {
            AudioMixerSnapshot targetSnapshot = insideDryVolume ? dryInteriorSnapshot : wetExteriorSnapshot;
            float transitionSeconds = math.max(
                MinimumEnvironmentSnapshotTransitionSeconds,
                SanitizePositive(environmentSnapshotTransitionSeconds, MinimumEnvironmentSnapshotTransitionSeconds));
            ApplyOceanRoarLowPass(insideDryVolume);

            if (targetSnapshot == null)
                return;

            targetSnapshot.TransitionTo(transitionSeconds);
        }

        private void QueueAirlockAudioSnapshot(bool insideDryVolume)
        {
            _pendingEnvironmentInsideDryVolume = insideDryVolume;
            _pendingEnvironmentSnapshot = true;
            _audioPresentationDirty = true;
        }

        private void QueueCycleEndSound()
        {
            if (cycleEndSound == null || !TryResolveBulkheadAudioRuntimePosition(out Vector3 audioPosition))
                return;

            _pendingCycleEndAudioPosition = audioPosition;
            _pendingCycleEndSound = true;
            _audioPresentationDirty = true;
        }

        private void ApplyOceanRoarLowPass(bool insideDryVolume)
        {
            if (environmentMixer == null || string.IsNullOrEmpty(oceanRoarLowPassCutoffParameter))
                return;

            environmentMixer.SetFloat(
                oceanRoarLowPassCutoffParameter,
                insideDryVolume ? DryOceanRoarLowPassHz : WetOceanRoarLowPassHz);
        }

        private bool PublishBulkheadContainmentState(bool lockedDown)
        {
            uint edgeHash = ResolveBulkheadEdgeHash();
            if (!IsBulkheadPoseSnapshotCurrent() && !RefreshBulkheadPoseSnapshot())
            {
                _bulkheadContainmentPublishPending = true;
                return false;
            }

            if (!TryReadBulkheadPoseSnapshot(out double3 centerAup, out float3 normal))
            {
                _bulkheadContainmentPublishPending = true;
                return false;
            }

            uint siblingHash = ResolveBulkheadSiblingHash(edgeHash);
            bool published = BulkheadContainmentIntentBus.TryWriteAirlockBulkheadIntent(
                edgeHash,
                lockedDown,
                centerAup,
                normal,
                emergencyBulkheadWidthMeters,
                emergencyBulkheadHeightMeters,
                ResolveBulkheadParentIntegrity01(),
                siblingHash,
                SystemDispatcher.CurrentFrameId);
            _bulkheadContainmentPublishPending = !published;
            if (published)
                _bulkheadContainmentRetryTicks = 0;
            return published;
        }

        private bool TryReadBulkheadPoseSnapshot(out double3 centerAup, out float3 normal)
        {
            centerAup = default;
            normal = new float3(0f, 0f, 1f);
            if (!IsBulkheadPoseSnapshotCurrent())
                return false;

            centerAup = ToBulkheadAbsoluteDouble3(in _bulkheadPoseCenterAup);
            normal = _bulkheadPoseNormal;
            float normalLengthSq = math.lengthsq(normal);
            return math.all(math.isfinite(centerAup)) &&
                   math.all(math.isfinite(normal)) &&
                   math.isfinite(normalLengthSq) &&
                   normalLengthSq > MinOverrideSignalDirectionSqr;
        }

        private static double3 ToBulkheadAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                position.GridX * cell + position.LocalX,
                position.GridY * cell + position.LocalY,
                position.GridZ * cell + position.LocalZ);
        }

        private bool TryReadBulkheadRuntimeBasis(out Vector3 forward, out Vector3 up, out Vector3 right)
        {
            forward = Vector3.forward;
            up = Vector3.up;
            right = Vector3.right;
            if (!IsBulkheadPoseSnapshotCurrent())
                return false;

            float3 forward3 = _bulkheadPoseNormal;
            float forwardLengthSq = math.lengthsq(forward3);
            if (!math.all(math.isfinite(forward3)) || forwardLengthSq <= MinOverrideSignalDirectionSqr)
                return false;

            forward3 *= math.rsqrt(forwardLengthSq);
            float3 upHint = _bulkheadPoseUp;
            if (!math.all(math.isfinite(upHint)) || math.lengthsq(upHint) <= 0.000001f)
            {
                upHint = math.abs(forward3.y) < 0.85f
                    ? new float3(0f, 1f, 0f)
                    : new float3(1f, 0f, 0f);
            }

            float3 up3 = upHint - forward3 * math.dot(upHint, forward3);
            float upLengthSq = math.lengthsq(up3);
            if (!math.all(math.isfinite(up3)) || upLengthSq <= 0.000001f)
            {
                up3 = math.abs(forward3.y) < 0.85f
                    ? new float3(0f, 1f, 0f)
                    : new float3(1f, 0f, 0f);
                up3 -= forward3 * math.dot(up3, forward3);
                upLengthSq = math.lengthsq(up3);
            }

            if (!math.all(math.isfinite(up3)) || upLengthSq <= 0.000001f)
                return false;

            up3 *= math.rsqrt(upLengthSq);
            float3 right3 = math.cross(up3, forward3);
            float rightLengthSq = math.lengthsq(right3);
            if (!math.all(math.isfinite(right3)) || rightLengthSq <= 0.000001f)
                return false;

            right3 *= math.rsqrt(rightLengthSq);
            up3 = math.cross(forward3, right3);
            upLengthSq = math.lengthsq(up3);
            if (!math.all(math.isfinite(up3)) || upLengthSq <= 0.000001f)
                return false;

            up3 *= math.rsqrt(upLengthSq);
            forward = new Vector3(forward3.x, forward3.y, forward3.z);
            up = new Vector3(up3.x, up3.y, up3.z);
            right = new Vector3(right3.x, right3.y, right3.z);
            return IsFinite(forward) && IsFinite(up) && IsFinite(right);
        }

        private bool RefreshBulkheadPoseSnapshot()
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            Transform frame = _cachedTransform != null ? _cachedTransform : transform;
            if (frame == null ||
                !IsFinite(frame.position) ||
                !TryNormalizeFinite(frame.forward, out Vector3 normalizedForward) ||
                !TryNormalizeFinite(frame.up, out Vector3 normalizedUp))
            {
                _bulkheadPoseSnapshotValid = false;
                return false;
            }

            if (!TryConvertRuntimePositionToAup(frame.position, out AbsoluteUniversePosition centerAup))
            {
                _bulkheadPoseSnapshotValid = false;
                return false;
            }

            _bulkheadPoseCenterAup = centerAup;
            _bulkheadPoseNormal = new float3(normalizedForward.x, normalizedForward.y, normalizedForward.z);
            _bulkheadPoseUp = new float3(normalizedUp.x, normalizedUp.y, normalizedUp.z);
            _bulkheadPoseShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            float normalLengthSq = math.lengthsq(_bulkheadPoseNormal);
            float upLengthSq = math.lengthsq(_bulkheadPoseUp);
            _bulkheadPoseSnapshotValid = _bulkheadPoseCenterAup.IsFinite() &&
                                         math.all(math.isfinite(_bulkheadPoseNormal)) &&
                                         math.isfinite(normalLengthSq) &&
                                         normalLengthSq > MinOverrideSignalDirectionSqr &&
                                         math.all(math.isfinite(_bulkheadPoseUp)) &&
                                         math.isfinite(upLengthSq) &&
                                         upLengthSq > 0.000001f;
            return _bulkheadPoseSnapshotValid;
        }

        private bool IsBulkheadPoseSnapshotCurrent()
        {
            return _bulkheadPoseSnapshotValid &&
                   !HectonFloatingOrigin.IsShiftInProgress &&
                   _bulkheadPoseShiftSequence == HectonFloatingOrigin.CurrentShiftSequence;
        }

        private bool TryResolveAupFromBulkheadPose(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsBulkheadPoseSnapshotCurrent() ||
                !IsFinite(runtimePosition) ||
                !TryResolveBulkheadAudioRuntimePosition(out Vector3 bulkheadRuntimePosition))
            {
                return false;
            }

            return TryOffsetAupByRuntimeDelta(
                in _bulkheadPoseCenterAup,
                bulkheadRuntimePosition,
                runtimePosition,
                out positionAup);
        }

        private void RetryBulkheadContainmentPublish()
        {
            if (_bulkheadContainmentRetryTicks > 0)
            {
                _bulkheadContainmentRetryTicks--;
                return;
            }

            if (!IsBulkheadPoseSnapshotCurrent() && !RefreshBulkheadPoseSnapshot())
            {
                _bulkheadContainmentRetryTicks = 15;
                return;
            }

            _bulkheadContainmentRetryTicks = 15;
            PublishBulkheadContainmentState(_emergencyLockedDown);
        }

        private uint ResolveBulkheadEdgeHash()
        {
            if (emergencyBulkheadEdgeHash != 0u)
                return emergencyBulkheadEdgeHash;

            ulong entity = EntityId.ToULong(GetEntityId());
            uint low = (uint)entity;
            uint high = (uint)(entity >> 32);
            uint hash = 2166136261u;
            hash = HashBulkheadLane(hash, low);
            hash = HashBulkheadLane(hash, high);
            return hash == 0u ? 1u : hash;
        }

        private uint ResolveBulkheadSiblingHash(uint edgeHash)
        {
            BaseModule module = owningModule;
            if (module == null)
                return HashBulkheadLane(edgeHash, 0xA11A0C4u);

            ulong moduleEntity = EntityId.ToULong(module.GetEntityId());
            return HashBulkheadLane(edgeHash, (uint)(moduleEntity ^ (moduleEntity >> 32)));
        }

        private float ResolveBulkheadParentIntegrity01()
        {
            BaseModule module = owningModule;
            if (module == null ||
                !float.IsFinite(module.CurrentIntegrity) ||
                !float.IsFinite(module.MaxIntegrity) ||
                module.MaxIntegrity <= 0.01f)
            {
                return 1f;
            }

            return Sanitize01(module.CurrentIntegrity / module.MaxIntegrity, 1f);
        }

        private static uint HashBulkheadLane(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            hash ^= hash >> 13;
            return hash;
        }

        private void QueuePressureDifferentialWhistle()
        {
            if (!_emergencyLockedDown)
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            if ((((int)frame + _pressureWhistleFrameOffset) & PressureWhistleFrameMask) != 0)
                return;

            BaseModule module = owningModule;
            if (module == null)
                return;

            float pressureDifferentialKPa = math.abs(module.ResolveExternalPressureDeltaKPa());
            if (!float.IsFinite(pressureDifferentialKPa) ||
                pressureDifferentialKPa < PressureWhistleStartDeltaKPa)
            {
                return;
            }

            float pressureSpan = SanitizePositive(PressureWhistleFullDeltaKPa - PressureWhistleStartDeltaKPa, 1f);
            float intensity01 = Sanitize01(
                (pressureDifferentialKPa - PressureWhistleStartDeltaKPa) / pressureSpan,
                0f);
            if (!TryResolveBulkheadAudioRuntimePosition(out Vector3 audioPosition))
                return;

            _pendingPressureWhistlePosition = audioPosition;
            _pendingPressureWhistleIntensity01 = intensity01;
            _pendingPressureWhistleAttackSeconds = math.lerp(0.035f, 0.11f, intensity01);
            _pendingPressureWhistleReleaseSeconds = math.lerp(0.18f, 0.52f, intensity01);
            _pendingPressureWhistleFrequencyHz = math.lerp(6200f, 12800f, intensity01);
            _pendingPressureWhistle = true;
            _audioPresentationDirty = true;
        }

        private bool TryResolveBulkheadAudioRuntimePosition(out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!IsBulkheadPoseSnapshotCurrent())
                return false;

            return TryConvertAupToRuntimePosition(in _bulkheadPoseCenterAup, out runtimePosition);
        }

        /// <summary>
        /// Updates the status light color using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock and Shader.PropertyToID.
        /// </summary>
        private void UpdateStatusLight(Color color)
        {
            _pendingStatusLightColor = color;
            _statusLightDirty = true;
        }

        private void FlushStatusLight()
        {
            if (!_statusLightDirty)
                return;

            _statusLightDirty = false;
            if (statusLightRenderer == null)
                return;

            statusLightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);
            statusLightRenderer.SetPropertyBlock(_mpb);
        }

        private void FlushAirlockAudioPresentation()
        {
            if (!_audioPresentationDirty)
                return;

            _audioPresentationDirty = false;

            if (_pendingEnvironmentSnapshot)
            {
                _pendingEnvironmentSnapshot = false;
                TransitionAirlockAudioSnapshot(_pendingEnvironmentInsideDryVolume);
            }

            if (_pendingCycleEndSound)
            {
                _pendingCycleEndSound = false;
                IAudioService audio = _cachedAudioService;
                if (cycleEndSound != null && audio != null)
                    audio.PlayAtPoint(cycleEndSound, _pendingCycleEndAudioPosition);
            }

            if (_pendingPressureWhistle)
            {
                _pendingPressureWhistle = false;
                ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                    _pendingPressureWhistlePosition,
                    _pendingPressureWhistleIntensity01,
                    _pendingPressureWhistleAttackSeconds,
                    _pendingPressureWhistleReleaseSeconds,
                    _pendingPressureWhistleFrequencyHz,
                    ProceduralAudioPingKind.MechanicalWhirr);
            }
        }

        private void CacheOwningModule()
        {
            if (owningModule == null)
                TryResolveParentComponent(_cachedTransform != null ? _cachedTransform : transform, out owningModule);
        }

        private float ResolveWeldOverrideDurationSeconds()
        {
            return math.max(0.1f, SanitizePositive(weldOverrideDurationSeconds, DefaultWeldOverrideDurationSeconds));
        }

        private float ResolveEqualizationDurationSeconds()
        {
            float pressureDeltaAtm = 1f;
            BaseModule module = owningModule;
            if (module != null)
            {
                float pressureDeltaKPa = math.abs(module.ResolveExternalPressureDeltaKPa());
                pressureDeltaAtm = pressureDeltaKPa * math.rcp(HectonSurvivalContract.KPaPerAtmosphere);
            }

            float fallbackMax = SanitizePositive(maximumEqualizationSeconds, FallbackEqualizationSeconds);
            return AirlockPressurizationMath.EstimateEqualizationDurationSeconds(
                SanitizePositive(airlockVolumeM3, 18f),
                SanitizePositive(equalizationFlowM3PerSqrtKPaSecond, 1.35f),
                pressureDeltaAtm,
                fallbackMax);
        }

        private void CaptureCycleInputLock()
        {
            _cycleInputManager = _cachedNativeInputManager;
            if (_cycleInputManager == null)
                return;

            _inputWasEnabledBeforeCycle = _cycleInputManager.IsPlayerInputEnabled;
            if (_inputWasEnabledBeforeCycle)
                _cycleInputManager.DisablePlayerInput();
        }

        private void ReleaseCycleInputLock()
        {
            if (_cycleInputManager != null && _inputWasEnabledBeforeCycle)
                _cycleInputManager.EnablePlayerInput();

            _cycleInputManager = null;
            _inputWasEnabledBeforeCycle = false;
        }

        private static float ResolveSignalWeldDeltaSeconds(in global::Hecton8.Interaction.InteractionSignal signal)
        {
            if (signal.PowerDelivered <= 0f || !float.IsFinite(signal.PowerDelivered))
                return 0f;

            float sourcePower = SanitizePositive(signal.Source.Power, 0.001f);
            if (sourcePower < 0.001f)
                sourcePower = 0.001f;
            float deltaSeconds = signal.PowerDelivered / sourcePower;
            return float.IsFinite(deltaSeconds) ? math.min(deltaSeconds, MaxSignalWeldDeltaSeconds) : 0f;
        }

        private bool IsOverrideWeldSignalValid(in global::Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (!_emergencyLockedDown || _lockdownOverrideBlockedByFloodedNeighbor || _state != AirlockState.Ready)
                return false;

            if (!IsFinite(runtimeHitPoint))
                return false;

            float range = SanitizePositive(signal.Source.Range, 0f);
            if (range <= 0f)
                return false;

            float3 direction = signal.Source.Direction;
            float directionLengthSq = math.lengthsq(direction);
            if (!math.isfinite(directionLengthSq) ||
                directionLengthSq <= MinOverrideSignalDirectionSqr)
            {
                return false;
            }

            Vector3 absoluteOrigin = new Vector3(signal.Source.Origin.x, signal.Source.Origin.y, signal.Source.Origin.z);
            if (!IsFinite(absoluteOrigin))
                return false;

            Vector3 runtimeOrigin = HectonFloatingOrigin.ToRuntimePosition(absoluteOrigin);
            if (!IsFinite(runtimeOrigin))
                return false;

            float3 delta = new float3(
                runtimeHitPoint.x - runtimeOrigin.x,
                runtimeHitPoint.y - runtimeOrigin.y,
                runtimeHitPoint.z - runtimeOrigin.z);
            float rangeWithSlack = range + OverrideWeldRangeSlackMeters;
            float deltaLengthSq = math.lengthsq(delta);
            if (!math.isfinite(deltaLengthSq) ||
                deltaLengthSq > rangeWithSlack * rangeWithSlack)
            {
                return false;
            }

            float forwardMeters = math.dot(delta, direction) * math.rsqrt(directionLengthSq);
            return float.IsFinite(forwardMeters) &&
                   forwardMeters >= -OverrideWeldRangeSlackMeters &&
                   forwardMeters <= rangeWithSlack;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return float.IsFinite(value) && value > 0f ? value : fallback;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return float.IsFinite(value) && value >= 0f ? value : fallback;
        }

        private static float Sanitize01(float value, float fallback)
        {
            return float.IsFinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        private static float SmoothStep01(float value)
        {
            float t = Sanitize01(value, 0f);
            return t * t * (3f - 2f * t);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;

            return current + math.sign(delta) * maxDelta;
        }

        private static bool Approximately(float lhs, float rhs)
        {
            float largest = math.max(1f, math.max(math.abs(lhs), math.abs(rhs)));
            return math.abs(lhs - rhs) <= 0.000001f * largest;
        }

        private static float ApproximatePressureRootKPa(float pressureDeltaKPa)
        {
            if (!float.IsFinite(pressureDeltaKPa) || pressureDeltaKPa <= 0f)
                return 0f;

            return math.asfloat((math.asuint(pressureDeltaKPa) >> 1) + FastSqrtApproximationBias);
        }

        private static bool TryNormalizeFinite(Vector3 value, out Vector3 normalized)
        {
            if (!IsFinite(value))
            {
                normalized = default;
                return false;
            }

            float lengthSq = value.sqrMagnitude;
            if (!float.IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                normalized = default;
                return false;
            }

            normalized = value * math.rsqrt(lengthSq);
            return true;
        }

        private static Vector3 LerpUnclampedVector(Vector3 start, Vector3 end, float t)
        {
            if (!IsFinite(start))
                start = Vector3.zero;
            if (!IsFinite(end))
                end = start;

            float safeT = Sanitize01(t, 0f);
            return new Vector3(
                math.lerp(start.x, end.x, safeT),
                math.lerp(start.y, end.y, safeT),
                math.lerp(start.z, end.z, safeT));
        }

        private static Quaternion NlerpQuaternion(Quaternion start, Quaternion end, float t)
        {
            float4 startValue = NormalizeQuaternionNoSqrt(new float4(start.x, start.y, start.z, start.w));
            float4 endValue = NormalizeQuaternionNoSqrt(new float4(end.x, end.y, end.z, end.w));
            endValue = math.dot(startValue, endValue) < 0f ? -endValue : endValue;
            float4 blended = math.lerp(startValue, endValue, Sanitize01(t, 0f));
            blended = NormalizeQuaternionNoSqrt(blended);
            return new Quaternion(blended.x, blended.y, blended.z, blended.w);
        }

        private static Quaternion ResolveBasisRotationNoTrig(Vector3 forward, Vector3 up)
        {
            float3 f = NormalizeVectorRsqrt((float3)forward, new float3(0f, 0f, 1f));
            float3 u = NormalizeVectorRsqrt((float3)up, new float3(0f, 1f, 0f));
            float3 r = NormalizeVectorRsqrt(math.cross(u, f), new float3(1f, 0f, 0f));
            u = NormalizeVectorRsqrt(math.cross(f, r), new float3(0f, 1f, 0f));

            float m00 = r.x;
            float m01 = u.x;
            float m02 = f.x;
            float m10 = r.y;
            float m11 = u.y;
            float m12 = f.y;
            float m20 = r.z;
            float m21 = u.z;
            float m22 = f.z;
            float trace = m00 + m11 + m22;

            float4 q;
            if (trace > 0f)
            {
                q = new float4(m21 - m12, m02 - m20, m10 - m01, 1f + trace);
            }
            else if (m00 >= m11 && m00 >= m22)
            {
                q = new float4(1f + m00 - m11 - m22, m01 + m10, m02 + m20, m21 - m12);
            }
            else if (m11 > m22)
            {
                q = new float4(m01 + m10, 1f + m11 - m00 - m22, m12 + m21, m02 - m20);
            }
            else
            {
                q = new float4(m02 + m20, m12 + m21, 1f + m22 - m00 - m11, m10 - m01);
            }

            q = NormalizeQuaternionNoSqrt(q);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static float3 NormalizeVectorRsqrt(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                return fallback;

            return value * math.rsqrt(lenSq);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lenSq = math.dot(value, value);
            return math.isfinite(lenSq) && lenSq > 0.000001f
                ? value * math.rsqrt(lenSq)
                : new float4(0f, 0f, 0f, 1f);
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            for (int depth = 0; current != null && depth < ParentComponentResolveDepth; depth++)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private void ForceEmergencyOverride()
        {
            _weldOverrideProgressSeconds = 0f;
            CacheOwningModule();

            if (owningModule != null)
            {
                owningModule.SetEmergencyBulkheadLockdown(false);
                if (!owningModule.IsFlooded)
                {
                    Vector3 breachAnchor = new Vector3(float.NaN, float.NaN, float.NaN);
                    if (TryResolveBulkheadAudioRuntimePosition(out Vector3 cachedBreachAnchor) ||
                        (RefreshBulkheadPoseSnapshot() &&
                         TryResolveBulkheadAudioRuntimePosition(out cachedBreachAnchor)))
                    {
                        breachAnchor = cachedBreachAnchor;
                    }

                    owningModule.ForceFloodFromBulkheadOverride(breachAnchor);
                }
                BaseAirlockEvents.TryRaiseManualOverrideCompleted(this);
                return;
            }

            SetEmergencyLockdown(false);
            BaseAirlockEvents.TryRaiseManualOverrideCompleted(this);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cycleDuration < 0.5f) cycleDuration = 0.5f;
            if (maximumEqualizationSeconds < cycleDuration) maximumEqualizationSeconds = cycleDuration;
            if (environmentSnapshotTransitionSeconds < MinimumEnvironmentSnapshotTransitionSeconds)
                environmentSnapshotTransitionSeconds = MinimumEnvironmentSnapshotTransitionSeconds;
            if (emergencyBulkheadWidthMeters < 0.25f)
                emergencyBulkheadWidthMeters = 0.25f;
            if (emergencyBulkheadHeightMeters < 0.25f)
                emergencyBulkheadHeightMeters = 0.25f;
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interior spawn point
            if (interiorSpawnPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
                Gizmos.DrawWireSphere(interiorSpawnPoint.position, 0.3f);
                Gizmos.DrawLine(transform.position, interiorSpawnPoint.position);

                // Draw forward direction
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
                Gizmos.DrawRay(interiorSpawnPoint.position, interiorSpawnPoint.forward * 0.5f);
            }

            // Draw exterior spawn point
            if (exteriorSpawnPoint != null)
            {
                Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.8f);
                Gizmos.DrawWireSphere(exteriorSpawnPoint.position, 0.3f);
                Gizmos.DrawLine(transform.position, exteriorSpawnPoint.position);

                // Draw forward direction
                Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.5f);
                Gizmos.DrawRay(exteriorSpawnPoint.position, exteriorSpawnPoint.forward * 0.5f);
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            ILocalizationTextReadModel manager = Hecton8.Core.GlobalRegistry.LocalizationText;
            _cachedEnterTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_ENTER_BASE, DefaultEnterText, _cachedEnterTextBuffer);
            _cachedExitTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_EXIT_BASE, DefaultExitText, _cachedExitTextBuffer);
            _cachedCyclingTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_CYCLING, DefaultCyclingText, _cachedCyclingTextBuffer);
            _cachedLockedTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_LOCKED, DefaultLockedText, _cachedLockedTextBuffer);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        /// <summary>
        /// Enables or clears emergency bulkhead lockdown. While active, interaction is blocked.
        /// </summary>
        public void SetEmergencyLockdown(bool lockedDown)
        {
            if (_emergencyLockedDown == lockedDown)
                return;

            _emergencyLockedDown = lockedDown;
            if (!lockedDown)
                _lockdownOverrideBlockedByFloodedNeighbor = false;
            _weldOverrideProgressSeconds = 0f;
            PublishBulkheadContainmentState(lockedDown);
            if (_state == AirlockState.Ready)
                UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);

            BaseAirlockEvents.TryRaiseEmergencyLockdownChanged(this);
        }

        /// <summary>
        /// Sets the logic-authoritative override block while a quarantined neighbor remains materially flooded.
        /// </summary>
        public void SetEmergencyLockdownOverrideBlocked(bool blocked)
        {
            if (_lockdownOverrideBlockedByFloodedNeighbor == blocked)
                return;

            _lockdownOverrideBlockedByFloodedNeighbor = blocked;
            if (blocked)
                _weldOverrideProgressSeconds = 0f;

            BaseAirlockEvents.TryRaiseManualOverrideBlockedChanged(this);
        }
    }
}

