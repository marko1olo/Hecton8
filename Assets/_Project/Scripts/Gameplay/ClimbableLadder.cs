using Hecton.Localization;
using Hecton8.Animation.Locomotion;
using Hecton8.Core;
using Hecton8.Interaction;
using System;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Ladder interaction adapter. Runtime movement is owned by ProceduralLadderClimbRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbableLadder : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;

        public bool IsTransitioning => _isTransitioning;
        public Transform EntryPoint => entryPoint;
        public Transform ExitPoint => exitPoint;

        private void Awake()
        {
            _transform = transform;
            TryGetComponent(out _collider);

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

            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegisterHotSwap();
            CacheRegistryServicesCold();
            InteractableRegistry.RegisterTree(this);
            RebuildLocalizedTextCache();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwap();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
            }
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
            return !string.IsNullOrWhiteSpace(interactText) &&
                   !string.Equals(interactText, DefaultInteractText, System.StringComparison.Ordinal)
                ? interactText
                : DefaultInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength), destination, out length);
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
            _cachedInteractTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(
                interactText,
                DefaultInteractText,
                LocalizationKeys.INTERACT_CLIMB_LADDER,
                _localizationManager,
                _cachedInteractTextBuffer);
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

            IAudioService audio = _audioService;
            if (climbSound != null && audio != null)
            {
                audio.PlayAtPoint(climbSound, player.position, climbVolume);
            }

            _isTransitioning = false;
            return true;
        }

        private void CacheRegistryServicesCold()
        {
            _audioService = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            _localizationManager = GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
            {
                return;
            }

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
            {
                return;
            }

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
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
