using Hecton.Localization;
using Hecton8.Animation.Locomotion;
using Hecton8.Interaction;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Ladder interaction adapter. Runtime movement is owned by ProceduralLadderClimbRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbableLadder : MonoBehaviour, IInteractable, ILocalizationLanguageChangedListener
    {
        private const string DefaultInteractText = "Climb Ladder";

        [Header("Transforms")]
        [Tooltip("Transform where player enters the ladder.")]
        [SerializeField] private Transform entryPoint;

        [Tooltip("Transform where player exits the ladder.")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("Should player rotation be matched to exit point?")]
        [SerializeField] private bool matchRotation = true;

        [Header("Audio")]
        [Tooltip("Sound played when player climbs.")]
        [SerializeField] private AudioClip climbSound;

        [Tooltip("Volume for climb sound.")]
        [SerializeField, Range(0f, 1f)] private float climbVolume = 0.6f;

        [Header("Interaction")]
        [Tooltip("Interaction text shown in HUD.")]
        [SerializeField] private string interactText = DefaultInteractText;

        private Transform _transform;
        private Collider _collider;
        private bool _isTransitioning;
        private string _cachedInteractText;

        public bool IsTransitioning => _isTransitioning;
        public Transform EntryPoint => entryPoint;
        public Transform ExitPoint => exitPoint;

        private void Awake()
        {
            _transform = transform;
            _collider = GetComponent<Collider>();

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

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

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
            if (_isTransitioning || interactor == null)
            {
                return;
            }

            Vector3 interactorPosition = interactor.position;
            Vector3 entryVisualDelta = interactorPosition - entryPoint.position;
            Vector3 exitVisualDelta = interactorPosition - exitPoint.position;
            bool goingUp = entryVisualDelta.sqrMagnitude < exitVisualDelta.sqrMagnitude;

            RequestProceduralClimb(interactor, goingUp);
        }

        string IInteractable.GetInteractText()
        {
            return _cachedInteractText;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            RebuildLocalizedTextCache();
        }

        public void RequestClimbToExit(Transform player)
        {
            RequestProceduralClimb(player, true);
        }

        public void RequestClimbToEntry(Transform player)
        {
            RequestProceduralClimb(player, false);
        }

        public void SetExitPoint(Transform point)
        {
            exitPoint = point;
        }

        public void SetEntryPoint(Transform point)
        {
            entryPoint = point;
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

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            if (manager == null)
            {
                return fallback;
            }

            string localized = manager.Get(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

        private bool RequestProceduralClimb(Transform player, bool goingUp)
        {
            if (player == null)
            {
                return false;
            }

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

            if (climbSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(climbSound, player.position, climbVolume);
            }

            _isTransitioning = false;
            return true;
        }

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
            if (entryPoint != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
                Gizmos.DrawWireSphere(entryPoint.position, 0.3f);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(entryPoint.position, entryPoint.forward * 0.5f);
                UnityEditor.Handles.Label(
                    entryPoint.position + Vector3.up * 0.5f,
                    "Entry",
                    new GUIStyle { normal = { textColor = Color.green } });
            }

            if (exitPoint != null)
            {
                Color exitColor = new Color(1f, 0.7f, 0f);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                Gizmos.DrawWireSphere(exitPoint.position, 0.3f);
                Gizmos.color = exitColor;
                Gizmos.DrawRay(exitPoint.position, exitPoint.forward * 0.5f);
                UnityEditor.Handles.Label(
                    exitPoint.position + Vector3.up * 0.5f,
                    "Exit",
                    new GUIStyle { normal = { textColor = exitColor } });
            }

            if (entryPoint != null && exitPoint != null)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
                Gizmos.DrawLine(entryPoint.position, exitPoint.position);
            }
        }
#endif
    }
}
