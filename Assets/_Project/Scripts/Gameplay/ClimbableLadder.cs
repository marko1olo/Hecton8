// ============================================================================
// HECTON-8 — ClimbableLadder.cs
// Ladder for moving between vertical base modules.
//
// ARCHITECTURE:
//   • Standalone prop — implements IInteractable.
//   • Simple teleport with optional screen fade.
//   • Configurable entry and exit points.
//   • UnityEvents for custom behavior.
//
// ZERO GC:
//   • No Update() — event-driven via IInteractable.
//   • Cached Transform.
//   • Pre-cached interaction text.
//
// USAGE:
//   1. Place on ladder GameObject with collider.
//   2. Assign entry and exit transforms.
//   3. Configure screen fade (optional).
//   4. Player interacts to teleport to exit point.
// ============================================================================

using Hecton8.Animation.Locomotion;
using Hecton8.Audio;
using Hecton8.Interaction;
using Hecton.Localization;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Ladder for moving between vertical base modules.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbableLadder : MonoBehaviour, IInteractable, ILocalizationLanguageChangedListener
    {
        private const string DefaultInteractText = "Climb Ladder";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSFORMS
        // ══════════════════════════════════════════════════════════

        [Header("── Transforms ──────────────────────────────────")]
        [Tooltip("Transform where player enters the ladder.")]
        [SerializeField] private Transform entryPoint;

        [Tooltip("Transform where player exits the ladder.")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("Should player rotation be matched to exit point?")]
        [SerializeField] private bool matchRotation = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Transition ───────────────────────────────────")]
        [Tooltip("Use screen fade during transition.")]
        [SerializeField] private bool useScreenFade = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when player climbs.")]
        [SerializeField] private AudioClip climbSound;

        [Tooltip("Volume for climb sound.")]
        [SerializeField, Range(0f, 1f)] private float climbVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ──────────────────────────────────")]
        [Tooltip("Interaction text shown in HUD.")]
        [SerializeField] private string interactText = DefaultInteractText;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when player starts climbing.")]
        [SerializeField] private UnityEvent OnClimbStart;

        [Tooltip("Invoked when player finishes climbing.")]
        [SerializeField] private UnityEvent OnClimbEnd;

        [Tooltip("Invoked with the player transform for custom positioning.")]
        [SerializeField] private UnityEvent<Transform> OnPlayerTeleported;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private bool _isTransitioning;

        // Pre-cached interaction text
        private string _cachedInteractText;

        // Pre-cached player tag
        private const string PlayerTag = "Player";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Is a transition in progress?</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Entry point transform.</summary>
        public Transform EntryPoint => entryPoint;

        /// <summary>Exit point transform.</summary>
        public Transform ExitPoint => exitPoint;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _collider = GetComponent<Collider>();

            // Ensure collider is a trigger
            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            // Use self as entry/exit if not assigned
            if (entryPoint == null)
            {
                entryPoint = _transform;
            }

            if (exitPoint == null)
            {
                exitPoint = _transform;
            }

            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizedTextCache();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        void IInteractable.OnHoverStart()
        {
            // Could trigger highlight effect here
        }

        void IInteractable.OnHoverEnd()
        {
            // Could disable highlight effect here
        }

        void IInteractable.Interact(Transform interactor)
        {
            if (_isTransitioning) return;

            // Determine direction based on player position
            Vector3 interactorPosition = interactor.position;
            bool goingUp = (interactorPosition - entryPoint.position).sqrMagnitude <
                           (interactorPosition - exitPoint.position).sqrMagnitude;

            RequestProceduralClimb(interactor, goingUp);
        }

        string IInteractable.GetInteractText()
        {
            return _cachedInteractText;
        }

        private void RebuildLocalizedTextCache()
        {
            if (!string.IsNullOrWhiteSpace(interactText) &&
                !string.Equals(interactText, DefaultInteractText, System.StringComparison.Ordinal))
            {
                _cachedInteractText = interactText;
                return;
            }

            _cachedInteractText = ResolveLocalized(LocalizationKeys.INTERACT_CLIMB_LADDER, DefaultInteractText);
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
            if (manager == null)
                return fallback;

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

        // ══════════════════════════════════════════════════════════
        //  TELEPORTATION
        // ══════════════════════════════════════════════════════════

        private bool RequestProceduralClimb(Transform player, bool goingUp)
        {
            if (player == null)
                return false;

            _isTransitioning = true;
            bool accepted = ProceduralLadderClimbRuntime.TryBeginClimb(
                _transform,
                entryPoint,
                exitPoint,
                player,
                goingUp,
                matchRotation);

            if (!accepted)
            {
                _isTransitioning = false;
                return false;
            }

            // Fire start event
            OnClimbStart?.Invoke();

            // Play climb sound
            if (climbSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(climbSound, player.position, climbVolume);
            }

            _isTransitioning = false;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Teleports the player to the exit point.
        /// </summary>
        /// <param name="player">Player transform.</param>
        public void TeleportToExit(Transform player)
        {
            RequestProceduralClimb(player, true);
        }

        /// <summary>
        /// Teleports the player to the entry point.
        /// </summary>
        /// <param name="player">Player transform.</param>
        public void TeleportToEntry(Transform player)
        {
            RequestProceduralClimb(player, false);
        }

        /// <summary>
        /// Sets the exit point.
        /// </summary>
        /// <param name="point">New exit point transform.</param>
        public void SetExitPoint(Transform point)
        {
            exitPoint = point;
        }

        /// <summary>
        /// Sets the entry point.
        /// </summary>
        /// <param name="point">New entry point transform.</param>
        public void SetEntryPoint(Transform point)
        {
            entryPoint = point;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw entry point
            if (entryPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
                Gizmos.DrawWireSphere(entryPoint.position, 0.3f);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(entryPoint.position, entryPoint.forward * 0.5f);

                // Label
                UnityEditor.Handles.Label(
                    entryPoint.position + Vector3.up * 0.5f,
                    "Entry",
                    new GUIStyle { normal = { textColor = Color.green } }
                );
            }

            // Draw exit point
            if (exitPoint != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Gizmos.DrawWireSphere(exitPoint.position, 0.3f);
                Gizmos.color = new Color(1f, 0.7f, 0f);
                Gizmos.DrawRay(exitPoint.position, exitPoint.forward * 0.5f);

                // Label
                UnityEditor.Handles.Label(
                    exitPoint.position + Vector3.up * 0.5f,
                    "Exit",
                    new GUIStyle { normal = { textColor = new Color(1f, 0.7f, 0f) } }
                );
            }

            // Draw connection line
            if (entryPoint != null && exitPoint != null)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
                Gizmos.DrawLine(entryPoint.position, exitPoint.position);
            }
        }
#endif
    }
}

