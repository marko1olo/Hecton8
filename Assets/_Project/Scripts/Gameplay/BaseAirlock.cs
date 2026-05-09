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
//   • BuoyancyObject.EnterDryZone/ExitDryZone — called on player's buoyancy
// ============================================================================

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.World;
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
    public sealed class BaseAirlock : MonoBehaviour, IInteractable, ITickable, IUpdatable, global::Hecton8.Interaction.IInteractionSignalConsumer, global::Hecton8.Interaction.IInteractionVulnerabilitySource, ILocalizationLanguageChangedListener, global::Hecton8.Interaction.IKinematicRepairTarget
    {
        private const float DefaultWeldOverrideDurationSeconds = 5f;
        private const float MaxSignalWeldDeltaSeconds = 0.25f;
        private const float MinOverrideSignalDirectionSqr = 0.000001f;
        private const float OverrideWeldRangeSlackMeters = 0.35f;
        private const float MinimumEnvironmentSnapshotTransitionSeconds = 1.5f;
        private const float DryOceanRoarLowPassHz = 650f;
        private const float WetOceanRoarLowPassHz = 22000f;
        private const float InteriorPressureKPa = 101.325f;
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

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Airlock Settings ───────────────────────────")]
        [Tooltip("Duration of the airlock cycle animation (seconds).")]
        [SerializeField, Range(1f, 10f)] private float cycleDuration = 3f;

        [Tooltip("Internal airlock chamber volume used to calculate pressure equalization time.")]
        [SerializeField, Min(0.1f)] private float airlockVolumeM3 = 18f;

        [Tooltip("Equalization flow coefficient in m3 per sqrt(kPa) per second.")]
        [SerializeField, Min(0.01f)] private float equalizationFlowM3PerSqrtKPaSecond = 1.35f;

        [Tooltip("Maximum pressure equalization time before mechanical bypass valves saturate.")]
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

        [Header("Cinematic Bulkhead")]
        [Tooltip("Door mesh moved by emergency lockdown. No controller-driven animation is used.")]
        [SerializeField] private Transform emergencyBulkheadDoorMesh;

        [Tooltip("Local-space offset applied from the authored open position when the emergency bulkhead seals.")]
        [SerializeField] private Vector3 emergencyBulkheadClosedLocalOffset = new Vector3(0f, -2.25f, 0f);

        [Tooltip("Seconds required for the emergency bulkhead to close or reopen.")]
        [SerializeField, Min(0.01f)] private float emergencyBulkheadSlideDurationSeconds = 0.5f;

        [Tooltip("Heavy metallic impact played when the lockdown slide reaches the sealed position.")]
        [SerializeField] private AudioClip emergencyBulkheadClangSound;

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
        private bool _emergencyLockedDown;
        private bool _lockdownOverrideBlockedByFloodedNeighbor;
        private float _weldOverrideProgressSeconds;
        private int _emissionPropertyId;
        private Transform _cycleInteractor;
        private Vector3 _pendingDestinationPosition;
        private Quaternion _pendingDestinationRotation = Quaternion.identity;
        private bool _hasPendingDestination;
        private bool _inputWasEnabledBeforeCycle;
        private InputManager _cycleInputManager;
        private Transform _cachedInteractorTransform;
        private Rigidbody _cachedInteractorBody;
        private global::Hecton8.Physics.BuoyancyObject _cachedInteractorBuoyancy;
        private bool _cachedInteractorComponentCacheValid;
        private Vector3 _bulkheadOpenLocalPosition;
        private Vector3 _bulkheadClosedLocalPosition;
        private float _bulkheadSlide01;
        private float _bulkheadSlideTarget01;
        private bool _bulkheadPoseCaptured;
        private bool _bulkheadClangPlayed;
        private int _pressureWhistleFrameOffset;

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
        private string _cachedEnterText;
        private string _cachedExitText;
        private string _cachedCyclingText;
        private string _cachedLockedText;

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
                return requiredSeconds > 0f ? math.saturate(_weldOverrideProgressSeconds / requiredSeconds) : 0f;
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
            CaptureBulkheadPose();
            _pressureWhistleFrameOffset = unchecked((int)EntityId.ToULong(GetEntityId())) & PressureWhistleFrameMask;
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
            // Set initial state
            _state = AirlockState.Ready;
            _weldOverrideProgressSeconds = 0f;
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);
            SetBulkheadSlideTarget(_emergencyLockedDown ? 1f : 0f);
            ApplyBulkheadSlideImmediate(_bulkheadSlideTarget01);
        }

        private void Start()
        {
            CacheOwningModule();
            TryRegister();
        }

        private void OnDisable()
        {
            ReleaseCycleInputLock();
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
            ClearInteractorComponentCache();
        }

        private void OnDestroy()
        {
            ReleaseCycleInputLock();
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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
            AdvanceBulkheadSlide(deltaTime);
            EmitPressureDifferentialWhistle();

            if (_state != AirlockState.Cycling)
                return;

            _cycleTimer -= deltaTime;

            if (_cycleTimer <= 0f)
            {
                CompleteCycle();
            }
        }

        // ══════════════════════════════════════════════════════════
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
            switch (_state)
            {
                case AirlockState.Ready:
                    if (_emergencyLockedDown)
                        return _cachedLockedText;
                    return _isPlayerInside ? _cachedExitText : _cachedEnterText;
                case AirlockState.Cycling:
                    return _cachedCyclingText;
                default:
                    return string.Empty;
            }
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
            _weldOverrideProgressSeconds = math.min(requiredSeconds, _weldOverrideProgressSeconds + deltaTime);
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
            if (!IsFinite(runtimeHitPoint))
                return false;

            Transform airlockTransform = _cachedTransform != null ? _cachedTransform : transform;
            Vector3 right = NormalizeFiniteOrFallback(airlockTransform.right, Vector3.right);
            Vector3 up = NormalizeFiniteOrFallback(airlockTransform.up, Vector3.up);
            Vector3 forward = NormalizeFiniteOrFallback(airlockTransform.forward, Vector3.forward);

            Vector3 handCenter = runtimeHitPoint + up * RepairHandVerticalBiasMeters;
            leftHandAup = AbsoluteUniversePosition.FromRuntimePosition(handCenter - right * RepairHandHalfSpanMeters);
            rightHandAup = AbsoluteUniversePosition.FromRuntimePosition(handCenter + right * RepairHandHalfSpanMeters);
            toolRotation = ResolveBasisRotationNoTrig(forward, up);
            return IsFinite(toolRotation);
        }

        public bool TryResolveKinematicRepairSnap(
            in global::Hecton8.Interaction.KinematicRepairTargetProbe probe,
            out global::Hecton8.Interaction.KinematicRepairSnapPoint snapPoint)
        {
            snapPoint = default;
            float3 runtimeHit = probe.HitAup.ToRuntimeFloat3();
            Vector3 runtimeHitPoint = new Vector3(runtimeHit.x, runtimeHit.y, runtimeHit.z);
            if (!TryResolveRepairSnapPoints(
                    runtimeHitPoint,
                    out AbsoluteUniversePosition leftHandAup,
                    out AbsoluteUniversePosition rightHandAup,
                    out Quaternion toolRotation))
            {
                return false;
            }

            float3 leftRuntime = leftHandAup.ToRuntimeFloat3();
            float3 rightRuntime = rightHandAup.ToRuntimeFloat3();
            Vector3 runtimeAnchor = new Vector3(
                (leftRuntime.x + rightRuntime.x) * 0.5f,
                (leftRuntime.y + rightRuntime.y) * 0.5f,
                (leftRuntime.z + rightRuntime.z) * 0.5f);
            if (!IsFinite(runtimeAnchor))
                runtimeAnchor = runtimeHitPoint;

            Vector3 surfaceNormal = TryNormalizeFinite(probe.HitNormal, out Vector3 normalizedHitNormal)
                ? normalizedHitNormal
                : toolRotation * Vector3.forward;
            snapPoint = new global::Hecton8.Interaction.KinematicRepairSnapPoint
            {
                AnchorAup = AbsoluteUniversePosition.FromRuntimePosition(runtimeAnchor),
                LeftHandAup = leftHandAup,
                RightHandAup = rightHandAup,
                RuntimePosition = runtimeAnchor,
                SurfaceNormal = surfaceNormal,
                ToolRotation = toolRotation,
                HitDistance = math.max(0f, probe.HitDistance),
                Blend = 1f,
                ColliderInstanceId = probe.ColliderInstanceId
            };
            return true;
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

            BaseAirlockEvents.RaiseCycleStarted(this, player);

            // Play cycle start sound
            if (cycleStartSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(cycleStartSound, _cachedTransform.position);
            }

            // Fire event
            OnCycleStarted?.Invoke();
        }

        private void CompleteCycle()
        {
            Transform completedInteractor = _cycleInteractor;
            if (completedInteractor != null && _hasPendingDestination)
                TeleportPlayer(completedInteractor, _pendingDestinationPosition, _pendingDestinationRotation);

            _state = AirlockState.Ready;

            // Restore state light after the cycle ends.
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);

            // Play cycle end sound
            if (cycleEndSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(cycleEndSound, _cachedTransform.position);
            }

            BaseAirlockEvents.RaiseCycleCompleted(this, completedInteractor);
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
                UnityEngine.Debug.LogError(
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
                UnityEngine.Debug.LogError(
                    _isPlayerInside ? InvalidExteriorSpawnPointPoseMessage : InvalidInteriorSpawnPointPoseMessage,
                    this);
#endif
                return false;
            }

            return true;
        }

        private void TeleportPlayer(Transform player, Vector3 destinationPosition, Quaternion destinationRotation)
        {
            ResolveInteractorComponents(player, out Rigidbody playerBody, out global::Hecton8.Physics.BuoyancyObject buoyancy);

            bool useSafeTeleportProtocol = Application.isPlaying;
            if (useSafeTeleportProtocol)
                HectonFloatingOrigin.BeginSafeTeleportProtocol();

            try
            {
                if (playerBody != null)
                    TeleportBody(playerBody, destinationPosition, destinationRotation);
                else
                    player.SetPositionAndRotation(destinationPosition, destinationRotation);
            }
            finally
            {
                if (useSafeTeleportProtocol)
                    HectonFloatingOrigin.EndSafeTeleportProtocol();
            }

            // Toggle environment state
            _isPlayerInside = !_isPlayerInside;
            TransitionAirlockAudioSnapshot(_isPlayerInside);

            BaseAirlockEvents.RaiseEnvironmentChanged(this, player);

            // Notify environment change
            // True = Dry (inside base), False = Wet (outside)
            OnEnvironmentChanged?.Invoke(_isPlayerInside);

            // Update player's BuoyancyObject if present
            if (buoyancy != null)
            {
                if (_isPlayerInside)
                    buoyancy.EnterDryZone();
                else
                    buoyancy.ExitDryZone();
            }
        }

        private void ResolveInteractorComponents(Transform player, out Rigidbody body, out global::Hecton8.Physics.BuoyancyObject buoyancy)
        {
            body = null;
            buoyancy = null;
            if (player == null)
                return;

            if (!ReferenceEquals(_cachedInteractorTransform, player) || !_cachedInteractorComponentCacheValid)
            {
                _cachedInteractorTransform = player;
                player.TryGetComponent(out _cachedInteractorBody);
                player.TryGetComponent(out _cachedInteractorBuoyancy);
                _cachedInteractorComponentCacheValid = true;
            }

            body = _cachedInteractorBody;
            buoyancy = _cachedInteractorBuoyancy;
        }

        private void ClearInteractorComponentCache()
        {
            _cycleInteractor = null;
            _cachedInteractorTransform = null;
            _cachedInteractorBody = null;
            _cachedInteractorBuoyancy = null;
            _cachedInteractorComponentCacheValid = false;
        }

        private static void TeleportBody(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            bool wasKinematic = body.isKinematic;
            bool wasDetectingCollisions = body.detectCollisions;
            bool wasSleeping = body.IsSleeping();

            body.isKinematic = true;
            body.detectCollisions = false;
            body.transform.SetPositionAndRotation(position, rotation);
            body.PublishTransform();
            body.isKinematic = false;
            body.isKinematic = wasKinematic;
            body.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
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

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void TransitionAirlockAudioSnapshot(bool insideDryVolume)
        {
            AudioMixerSnapshot targetSnapshot = insideDryVolume ? dryInteriorSnapshot : wetExteriorSnapshot;
            float transitionSeconds = math.max(MinimumEnvironmentSnapshotTransitionSeconds, environmentSnapshotTransitionSeconds);
            ApplyOceanRoarLowPass(insideDryVolume);

            if (targetSnapshot == null)
                return;

            targetSnapshot.TransitionTo(transitionSeconds);
        }

        private void ApplyOceanRoarLowPass(bool insideDryVolume)
        {
            if (environmentMixer == null || string.IsNullOrEmpty(oceanRoarLowPassCutoffParameter))
                return;

            environmentMixer.SetFloat(
                oceanRoarLowPassCutoffParameter,
                insideDryVolume ? DryOceanRoarLowPassHz : WetOceanRoarLowPassHz);
        }

        private void CaptureBulkheadPose()
        {
            if (emergencyBulkheadDoorMesh == null || _bulkheadPoseCaptured)
                return;

            _bulkheadOpenLocalPosition = emergencyBulkheadDoorMesh.localPosition;
            _bulkheadClosedLocalPosition = _bulkheadOpenLocalPosition + emergencyBulkheadClosedLocalOffset;
            _bulkheadSlide01 = _emergencyLockedDown ? 1f : 0f;
            _bulkheadSlideTarget01 = _bulkheadSlide01;
            _bulkheadPoseCaptured = true;
        }

        private void SetBulkheadSlideTarget(float target01)
        {
            CaptureBulkheadPose();
            _bulkheadSlideTarget01 = math.saturate(target01);
            if (_bulkheadSlideTarget01 >= 1f && _bulkheadSlide01 < 0.999f)
                _bulkheadClangPlayed = false;
        }

        private void ApplyBulkheadSlideImmediate(float target01)
        {
            CaptureBulkheadPose();
            if (emergencyBulkheadDoorMesh == null)
                return;

            _bulkheadSlide01 = math.saturate(target01);
            float eased = SmoothStep01(_bulkheadSlide01);
            emergencyBulkheadDoorMesh.localPosition = LerpUnclampedVector(
                _bulkheadOpenLocalPosition,
                _bulkheadClosedLocalPosition,
                eased);
            _bulkheadClangPlayed = _bulkheadSlide01 >= 0.999f;
        }

        private void AdvanceBulkheadSlide(float deltaTime)
        {
            CaptureBulkheadPose();
            if (emergencyBulkheadDoorMesh == null)
                return;

            float previousSlide = _bulkheadSlide01;
            float duration = math.max(0.01f, emergencyBulkheadSlideDurationSeconds);
            float step = math.max(0f, deltaTime) / duration;
            _bulkheadSlide01 = MoveTowards(_bulkheadSlide01, _bulkheadSlideTarget01, step);
            if (Approximately(previousSlide, _bulkheadSlide01))
                return;

            float eased = SmoothStep01(_bulkheadSlide01);
            emergencyBulkheadDoorMesh.localPosition = LerpUnclampedVector(
                _bulkheadOpenLocalPosition,
                _bulkheadClosedLocalPosition,
                eased);

            if (_bulkheadSlideTarget01 < 1f || _bulkheadSlide01 < 0.999f || _bulkheadClangPlayed)
                return;

            _bulkheadClangPlayed = true;
            if (emergencyBulkheadClangSound != null && GlobalRegistry.Audio != null)
                GlobalRegistry.Audio.PlayAtPoint(emergencyBulkheadClangSound, _cachedTransform.position, 1f, 0.92f);
        }

        private void EmitPressureDifferentialWhistle()
        {
            if (!_emergencyLockedDown && _bulkheadSlide01 < 0.5f)
                return;

            if (((Time.frameCount + _pressureWhistleFrameOffset) & PressureWhistleFrameMask) != 0)
                return;

            BaseModule module = owningModule;
            if (module == null)
                return;

            float pressureDifferentialKPa = math.abs(module.ResolveExternalPressureDeltaKPa());
            if (pressureDifferentialKPa < PressureWhistleStartDeltaKPa)
                return;

            float intensity01 = math.saturate(
                (pressureDifferentialKPa - PressureWhistleStartDeltaKPa) /
                math.max(1f, PressureWhistleFullDeltaKPa - PressureWhistleStartDeltaKPa));
            ProceduralAudioEvents.RaiseAudioPingTriggered(
                _cachedTransform.position,
                intensity01,
                math.lerp(0.035f, 0.11f, intensity01),
                math.lerp(0.18f, 0.52f, intensity01),
                math.lerp(6200f, 12800f, intensity01),
                ProceduralAudioPingKind.MechanicalWhirr);
        }

        /// <summary>
        /// Updates the status light color using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock and Shader.PropertyToID.
        /// </summary>
        private void UpdateStatusLight(Color color)
        {
            if (statusLightRenderer == null)
                return;

            statusLightRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, color);
            statusLightRenderer.SetPropertyBlock(_mpb);
        }

        private void CacheOwningModule()
        {
            if (owningModule == null)
                TryResolveParentComponent(_cachedTransform != null ? _cachedTransform : transform, out owningModule);
        }

        private float ResolveWeldOverrideDurationSeconds()
        {
            return math.max(0.1f, weldOverrideDurationSeconds);
        }

        private float ResolveEqualizationDurationSeconds()
        {
            CacheOwningModule();
            float pressureDeltaKPa = owningModule != null
                ? owningModule.ResolveExternalPressureDeltaKPa()
                : 0f;
            float equalizationSeconds = airlockVolumeM3 *
                                        ApproximatePressureRootKPa(pressureDeltaKPa) /
                                        math.max(0.01f, equalizationFlowM3PerSqrtKPaSecond);
            if (!float.IsFinite(equalizationSeconds))
                equalizationSeconds = cycleDuration;

            return math.clamp(
                math.max(cycleDuration, equalizationSeconds),
                cycleDuration,
                math.max(cycleDuration, maximumEqualizationSeconds));
        }

        private void CaptureCycleInputLock()
        {
            _cycleInputManager = GlobalRegistry.NativeInputManager;
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

            float sourcePower = math.max(0.001f, signal.Source.Power);
            float deltaSeconds = signal.PowerDelivered / sourcePower;
            return math.clamp(deltaSeconds, 0f, MaxSignalWeldDeltaSeconds);
        }

        private bool IsOverrideWeldSignalValid(in global::Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (!_emergencyLockedDown || _lockdownOverrideBlockedByFloodedNeighbor || _state != AirlockState.Ready)
                return false;

            if (!IsFinite(runtimeHitPoint))
                return false;

            float range = math.max(0f, signal.Source.Range);
            if (range <= 0f)
                return false;

            float3 direction = signal.Source.Direction;
            float directionLengthSq = math.lengthsq(direction);
            if (directionLengthSq <= MinOverrideSignalDirectionSqr)
                return false;

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
            if (math.lengthsq(delta) > rangeWithSlack * rangeWithSlack)
                return false;

            float forwardMeters = math.dot(delta, direction) * math.rsqrt(directionLengthSq);
            return forwardMeters >= -OverrideWeldRangeSlackMeters && forwardMeters <= rangeWithSlack;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
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
            float lengthSq = value.sqrMagnitude;
            if (!IsFinite(value) || lengthSq <= 0.000001f)
            {
                normalized = default;
                return false;
            }

            normalized = value * math.rsqrt(lengthSq);
            return true;
        }

        private static Vector3 NormalizeFiniteOrFallback(Vector3 value, Vector3 fallback)
        {
            return TryNormalizeFinite(value, out Vector3 normalized)
                ? normalized
                : fallback;
        }

        private static Vector3 LerpUnclampedVector(Vector3 start, Vector3 end, float t)
        {
            return new Vector3(
                math.lerp(start.x, end.x, t),
                math.lerp(start.y, end.y, t),
                math.lerp(start.z, end.z, t));
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
            float lenSq = math.max(math.dot(value, value), 0.000001f);
            return value * math.rsqrt(lenSq);
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
                    owningModule.ForceFloodFromBulkheadOverride(_cachedTransform.position);
                BaseAirlockEvents.RaiseManualOverrideCompleted(this);
                return;
            }

            SetEmergencyLockdown(false);
            BaseAirlockEvents.RaiseManualOverrideCompleted(this);
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
            _cachedEnterText = ResolveLocalized(LocalizationKeys.INTERACT_ENTER_BASE, DefaultEnterText);
            _cachedExitText = ResolveLocalized(LocalizationKeys.INTERACT_EXIT_BASE, DefaultExitText);
            _cachedCyclingText = ResolveLocalized(LocalizationKeys.INTERACT_CYCLING, DefaultCyclingText);
            _cachedLockedText = ResolveLocalized(LocalizationKeys.INTERACT_LOCKED, DefaultLockedText);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
            SetBulkheadSlideTarget(lockedDown ? 1f : 0f);
            if (_state == AirlockState.Ready)
                UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);

            BaseAirlockEvents.RaiseEmergencyLockdownChanged(this);
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

            BaseAirlockEvents.RaiseManualOverrideBlockedChanged(this);
        }
    }
}

