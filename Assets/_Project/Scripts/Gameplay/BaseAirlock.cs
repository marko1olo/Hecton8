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
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Interaction;
using Hecton8.Physics;
using UnityEngine;
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
    public sealed class BaseAirlock : MonoBehaviour, IInteractable, ITickable, IUpdatable, global::Hecton8.Interaction.IInteractionSignalConsumer, global::Hecton8.Interaction.IInteractionVulnerabilitySource, ILocalizationLanguageChangedListener
    {
        private const float DefaultWeldOverrideDurationSeconds = 5f;
        private const float MaxSignalWeldDeltaSeconds = 0.25f;
        private const int OverrideRaycastHitCapacity = 4;
        private const float MinOverrideRaycastDirectionSqr = 0.000001f;
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
        private BuoyancyObject _cachedInteractorBuoyancy;
        private bool _cachedInteractorComponentCacheValid;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly uint _OverrideVulnerabilityMask = ToolCapabilityMasks.ResolveCapabilityMask(InteractionEffectType.Weld) |
                                                                  ToolCapabilityMasks.ResolveCapabilityMask(InteractionEffectType.PlasmaCut);
        // COLD ALLOC: RaycastHit[4] — static quarantine override raycast buffer for zero-GC weld validation — owner: BaseAirlock
        private static readonly RaycastHit[] s_overrideRaycastHits = new RaycastHit[OverrideRaycastHitCapacity];

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
                return requiredSeconds > 0f ? Mathf.Clamp01(_weldOverrideProgressSeconds / requiredSeconds) : 0f;
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
        }

        private void Start()
        {
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

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
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
            _weldOverrideProgressSeconds = Mathf.Min(requiredSeconds, _weldOverrideProgressSeconds + deltaTime);
            if (_weldOverrideProgressSeconds >= requiredSeconds)
                ForceEmergencyOverride();

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

            if (!IsOverrideWeldRaycastValid(in signal))
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
            ResolveInteractorComponents(player, out Rigidbody playerBody, out BuoyancyObject buoyancy);

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

        private void ResolveInteractorComponents(Transform player, out Rigidbody body, out BuoyancyObject buoyancy)
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
            body.ResetCenterOfMass();
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
                owningModule = GetComponentInParent<BaseModule>();
        }

        private float ResolveWeldOverrideDurationSeconds()
        {
            return Mathf.Max(0.1f, weldOverrideDurationSeconds);
        }

        private float ResolveEqualizationDurationSeconds()
        {
            CacheOwningModule();
            float pressureDeltaKPa = owningModule != null
                ? owningModule.ResolveExternalPressureDeltaKPa()
                : 0f;
            float equalizationSeconds = airlockVolumeM3 *
                                        Mathf.Sqrt(Mathf.Max(0f, pressureDeltaKPa)) /
                                        Mathf.Max(0.01f, equalizationFlowM3PerSqrtKPaSecond);
            if (!float.IsFinite(equalizationSeconds))
                equalizationSeconds = cycleDuration;

            return Mathf.Clamp(
                Mathf.Max(cycleDuration, equalizationSeconds),
                cycleDuration,
                Mathf.Max(cycleDuration, maximumEqualizationSeconds));
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

            float sourcePower = Mathf.Max(0.001f, signal.Source.Power);
            float deltaSeconds = signal.PowerDelivered / sourcePower;
            return Mathf.Clamp(deltaSeconds, 0f, MaxSignalWeldDeltaSeconds);
        }

        private bool IsOverrideWeldRaycastValid(in global::Hecton8.Interaction.InteractionSignal signal)
        {
            if (!_emergencyLockedDown || _lockdownOverrideBlockedByFloodedNeighbor || _state != AirlockState.Ready)
                return false;

            Vector3 origin = new Vector3(signal.Source.Origin.x, signal.Source.Origin.y, signal.Source.Origin.z);
            Vector3 direction = new Vector3(signal.Source.Direction.x, signal.Source.Direction.y, signal.Source.Direction.z);
            if (direction.sqrMagnitude <= MinOverrideRaycastDirectionSqr)
                return false;

            float range = Mathf.Max(0f, signal.Source.Range);
            if (range <= 0f)
                return false;

            direction.Normalize();
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                direction,
                s_overrideRaycastHits,
                range,
                HectonLayerMasks.InteractableLayerMask,
                QueryTriggerInteraction.Collide);

            bool nearestHitIsOwnAirlock = false;
            float nearestDistance = float.MaxValue;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = s_overrideRaycastHits[hitIndex].collider;
                if (hitCollider == null)
                    continue;

                float hitDistance = s_overrideRaycastHits[hitIndex].distance;
                if (hitDistance >= nearestDistance)
                    continue;

                nearestDistance = hitDistance;
                nearestHitIsOwnAirlock = IsOwnAirlockCollider(hitCollider);
            }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                s_overrideRaycastHits[hitIndex] = default;

            return nearestHitIsOwnAirlock;
        }

        private bool IsOwnAirlockCollider(Collider hitCollider)
        {
            if (hitCollider == null || _cachedTransform == null)
                return false;

            Transform hitTransform = hitCollider.transform;
            return hitTransform == _cachedTransform ||
                   hitTransform.IsChildOf(_cachedTransform) ||
                   _cachedTransform.IsChildOf(hitTransform);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
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

