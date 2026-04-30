// ============================================================================
// HECTON-8 — BaseAirlock.cs
// Entry point for underwater modules.
//
// ARCHITECTURE:
//   • IInteractable for player interaction
//   • State machine via ITickable (no coroutines)
//   • MaterialPropertyBlock for status light (zero GC)
//   • UnityEvent for environment handoff (designer-configurable)
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
    public sealed class BaseAirlock : MonoBehaviour, IInteractable, ITickable, IUpdatable, global::Hecton8.Interaction.IInteractionSignalConsumer, global::Hecton8.Interaction.IInteractionVulnerabilitySource
    {
        private const float DefaultWeldOverrideDurationSeconds = 5f;
        private const float MaxSignalWeldDeltaSeconds = 0.25f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Airlock Settings ───────────────────────────")]
        [Tooltip("Duration of the airlock cycle animation (seconds).")]
        [SerializeField, Range(1f, 10f)] private float cycleDuration = 3f;

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
        private float _weldOverrideProgressSeconds;
        private int _emissionPropertyId;

        // Cached references
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

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

        /// <summary>Normalized welding progress toward a manual emergency override.</summary>
        public float WeldOverrideProgress01 => ResolveWeldOverrideDurationSeconds() > 0f
            ? Mathf.Clamp01(_weldOverrideProgressSeconds / ResolveWeldOverrideDurationSeconds())
            : 0f;

        /// <inheritdoc />
        public uint VulnerabilityMask => ToolCapabilityMasks.ResolveCapabilityMask(InteractionEffectType.Weld);

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: BaseAirlock

            if (statusLightRenderer == null)
                statusLightRenderer = GetComponent<Renderer>();

            CacheOwningModule();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            TryRegister();
            RebuildLocalizedTextCache();
            // Set initial state
            _state = AirlockState.Ready;
            _weldOverrideProgressSeconds = 0f;
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
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
            if (!_emergencyLockedDown || _state != AirlockState.Ready)
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
            if ((InteractionEffectType)signal.EffectType != InteractionEffectType.Weld)
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
            _state = AirlockState.Cycling;
            _cycleTimer = cycleDuration;

            // Update status light to red
            UpdateStatusLight(cyclingColor);

            // Play cycle start sound
            if (cycleStartSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(cycleStartSound, _cachedTransform.position);
            }

            // Fire event
            OnCycleStarted?.Invoke();

            // Teleport player to spawn point immediately
            // (The "cycle" is the animation/sound, teleport happens at start)
            TeleportPlayer(player);
        }

        private void CompleteCycle()
        {
            _state = AirlockState.Ready;

            // Restore state light after the cycle ends.
            UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);

            // Play cycle end sound
            if (cycleEndSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(cycleEndSound, _cachedTransform.position);
            }

            // Fire event
            OnCycleCompleted?.Invoke();
        }

        private void TeleportPlayer(Transform player)
        {
            // Determine destination based on current state
            Transform destination = _isPlayerInside ? exteriorSpawnPoint : interiorSpawnPoint;

            if (destination == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogError($"[BaseAirlock] {( _isPlayerInside ? "Exterior" : "Interior" )} spawn point not set on {gameObject.name}");
#endif
                return;
            }

            // Teleport player
            player.position = destination.position;
            player.rotation = destination.rotation;

            // Toggle environment state
            _isPlayerInside = !_isPlayerInside;

            // Notify environment change
            // True = Dry (inside base), False = Wet (outside)
            OnEnvironmentChanged?.Invoke(_isPlayerInside);

            // Update player's BuoyancyObject if present
            var buoyancy = player.GetComponent<BuoyancyObject>();
            if (buoyancy != null)
            {
                if (_isPlayerInside)
                    buoyancy.EnterDryZone();
                else
                    buoyancy.ExitDryZone();
            }

            // Update player's HectonSurvivalSystem if present
            // When inside a dry zone, oxygen consumption should be handled differently
            var survival = player.GetComponent<HectonSurvivalSystem>();
            if (survival != null)
            {
                // The survival system already handles oxygen based on the surface contract
                // (underwater vs surface swim). When in a dry zone, the BuoyancyObject
                // will report IsInAir = true, which affects fluid simulation.
                // 
                // For explicit dry zone oxygen handling, we could add a method to
                // HectonSurvivalSystem, but the current implementation already
                // handles this via the surface contract check.
                //
                // Future: If we need explicit "in base" oxygen refill, add:
                // survival.SetInDryZone(_isPlayerInside);
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

        private static float ResolveSignalWeldDeltaSeconds(in global::Hecton8.Interaction.InteractionSignal signal)
        {
            if (signal.PowerDelivered <= 0f || !float.IsFinite(signal.PowerDelivered))
                return 0f;

            float sourcePower = Mathf.Max(0.001f, signal.Source.Power);
            float deltaSeconds = signal.PowerDelivered / sourcePower;
            return Mathf.Clamp(deltaSeconds, 0f, MaxSignalWeldDeltaSeconds);
        }

        private void ForceEmergencyOverride()
        {
            _weldOverrideProgressSeconds = 0f;
            CacheOwningModule();

            if (owningModule != null)
            {
                owningModule.SetEmergencyBulkheadLockdown(false);
                if (!owningModule.IsFlooded)
                    owningModule.ForceFlood();
                return;
            }

            SetEmergencyLockdown(false);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cycleDuration < 0.5f) cycleDuration = 0.5f;
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

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
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
            _weldOverrideProgressSeconds = 0f;
            if (_state == AirlockState.Ready)
                UpdateStatusLight(_emergencyLockedDown ? lockedDownColor : readyColor);
        }
    }
}

