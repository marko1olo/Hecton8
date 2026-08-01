// ============================================================================
// HECTON-8 - SaveStation.cs
// Mirnyy terminal sohraneniya s defensive-povedeniem i integratsiey v HUD.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Terminal v mire, kotoryy zapuskaet sohranenie v ukazannyy slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SaveStation : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener, IInteractionStartedEventOwner
    {
        [Header("── Settings ──────────────────────────────")]
        [Tooltip("Otobrazhaemoe imya terminala v podskazke vzaimodeystviya.")]
        [SerializeField] private string _stationName = "Save Station";
        [SerializeField] private LocalizedTextReference _localizedStationName;

        [Tooltip("Imya save-slota, v kotoryy terminal budet sohranyat igru.")]
        [SerializeField] private string _saveSlot = "slot_0";

        [Tooltip("Neobyazatelnaya ssylka na HUD-uvedomleniya. Esli ne zadana, ischetsya lenivo.")]
        [SerializeField] private HUDNotification _hudNotification;

        [Header("── Audio ──────────────────────────────")]
        [Tooltip("Zvuk aktivatsii terminala.")]
        [SerializeField] private AudioClip _interactionSound;

        private const string SaveGameFallbackLabel = "Save Game";
        private const string SaveStationFallbackName = "Save Station";
        private const int InteractTextCapacity = 128;
        private const uint SaveStationSourceHash = 0x53535645u; // SSVE

        private string _cachedInteractText = SaveGameFallbackLabel;
        private readonly char[] _interactTextBuffer = new char[InteractTextCapacity];
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - save-station HUD notification staging buffer - owner: SaveStation
        private int _interactTextLength;
        private ISaveService _saveService;
        private Hecton8.Core.IAudioService _audioService;
        private ILocalizationTextReadModel _localization;
        private bool _hotSwapRegistered;

        private void Awake()
        {
            CacheRegistryServicesCold();
            RefreshCachedInteractText();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshCachedInteractText();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
        }

        /// <inheritdoc />
        public void OnHoverStart()
        {
        }

        /// <inheritdoc />
        public void OnHoverEnd()
        {
        }

        /// <inheritdoc />
        public void Interact(Transform interactor)
        {
            ISaveService saveService = _saveService;
            if (saveService == null || !saveService.IsInitialized)
            {
                ShowHudWarning(LocalizationKeys.SAVE_STATION_OFFLINE, "SAVE SYSTEM OFFLINE");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[SaveStation] Save service is unavailable.", this);
#endif
                return;
            }

            if (string.IsNullOrWhiteSpace(_saveSlot))
            {
                ShowHudWarning(LocalizationKeys.SAVE_STATION_SLOT_NOT_CONFIGURED, "SAVE SLOT NOT CONFIGURED");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[SaveStation] Save slot is not configured.", this);
#endif
                return;
            }

            if (!SaveManager.IsSafeSlotName(_saveSlot))
            {
                ShowHudWarning(LocalizationKeys.SAVE_STATION_SLOT_NOT_CONFIGURED, "SAVE SLOT NOT CONFIGURED");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[SaveStation] Save slot rejected by SaveManager slot-name guard.", this);
#endif
                return;
            }

            if (saveService.IsBusy)
            {
                ShowHudInfo(LocalizationKeys.SAVE_STATION_BUSY, "SAVE ALREADY IN PROGRESS");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[SaveStation] Save skipped because another save/load is already running.", this);
#endif
                return;
            }

            if (TryRequestManualSlotSave(saveService, interactor))
                return;

            PlayInteractionSound();
            ShowHudInfo(LocalizationKeys.SAVE_STATION_REQUESTED, "SAVE REQUESTED");
            _ = saveService.SaveGameAsync(_saveSlot);
            InteractionEvents.TryRaiseInteractionStarted(this, interactor);
        }

        /// <inheritdoc />
        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            length = 0;
            if (_interactTextLength <= 0)
                return InteractableTextCopy.TryCopy(_cachedInteractText, destination, out length);

            int copyLength = math.min(_interactTextLength, destination.Length);
            _interactTextBuffer.AsSpan(0, copyLength).CopyTo(destination);
            length = copyLength;
            return copyLength == _interactTextLength;
        }

        private void PlayInteractionSound()
        {
            if (_interactionSound == null)
                return;

            Hecton8.Core.IAudioService audioManager = ResolveAudioService();
            if (audioManager == null)
                return;

            audioManager.PlayAtPoint(_interactionSound, transform.position);
        }

        private bool TryRequestManualSlotSave(ISaveService saveService, Transform interactor)
        {
            IAsyncPersistenceService asyncPersistence = saveService as IAsyncPersistenceService;
            if (asyncPersistence == null)
                return false;

            int slotIndex = SaveEvents.ResolveKnownSlotIndex(_saveSlot);
            if (slotIndex < 0 || slotIndex >= SaveEvents.ManualSlotCount)
                return false;

            bool accepted = asyncPersistence.TryRequestSave((byte)slotIndex, SaveStationSourceHash);
            if (!accepted)
            {
                // Rejection is silent at the persistence layer (no SaveFailed event). Surface it
                // on the station HUD so an explicit player save request never looks like success.
                if (saveService.IsBusy)
                    ShowHudInfo(LocalizationKeys.SAVE_STATION_BUSY, "SAVE ALREADY IN PROGRESS");
                else
                    ShowHudWarning(LocalizationKeys.SAVE_STATION_OFFLINE, "SAVE SYSTEM OFFLINE");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[SaveStation] TryRequestSave rejected; player was notified on HUD.",
                    this);
#endif
                return true;
            }

            PlayInteractionSound();
            ShowHudInfo(LocalizationKeys.SAVE_STATION_REQUESTED, "SAVE REQUESTED");
            InteractionEvents.TryRaiseInteractionStarted(this, interactor);
            return true;
        }

        private void ResolveHudNotification()
        {
            if (_hudNotification == null)
                HUDNotification.TryGetActive(out _hudNotification);
        }

        private void RefreshCachedInteractText()
        {
            _cachedInteractText = SaveGameFallbackLabel;
            _interactTextLength = 0;

            ReadOnlySpan<char> actionLabel = ResolveLocalizedSpan(LocalizationKeys.INTERACT_SAVE_GAME, SaveGameFallbackLabel);
            ReadOnlySpan<char> stationName = _localizedStationName.ResolveSpanOrFallback(
                _localization,
                FallbackOrDefault(_stationName, SaveStationFallbackName));

            int length = 0;
            TryAppendSpan(actionLabel, _interactTextBuffer, ref length);
            TryAppendSpan(" (".AsSpan(), _interactTextBuffer, ref length);
            TryAppendSpan(stationName, _interactTextBuffer, ref length);
            TryAppendSpan(")".AsSpan(), _interactTextBuffer, ref length);
            _interactTextLength = length;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    _saveService = currentService as ISaveService;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as Hecton8.Core.IAudioService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    RefreshCachedInteractText();
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _saveService = GlobalRegistry.Save;
            CacheAudioService(GlobalRegistry.Audio);
            _localization = GlobalRegistry.LocalizationText;
        }

        private void CacheAudioService(Hecton8.Core.IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private Hecton8.Core.IAudioService ResolveAudioService()
        {
            Hecton8.Core.IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(Hecton8.Core.IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshCachedInteractText();
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
                return fallback.AsSpan();

            ILocalizationTextReadModel manager = _localization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private void ShowHudWarning(string key, string fallback)
        {
            if (!TryBuildNotification(key, fallback))
                return;

            ResolveHudNotification();
            _hudNotification?.ShowWarning(in _notificationBuffer);
        }

        private void ShowHudInfo(string key, string fallback)
        {
            if (!TryBuildNotification(key, fallback))
                return;

            ResolveHudNotification();
            _hudNotification?.ShowInfo(in _notificationBuffer);
        }

        private bool TryBuildNotification(string key, string fallback)
        {
            _notificationBuffer.Clear();
            return _notificationBuffer.Append(ResolveLocalizedSpan(key, fallback));
        }

        private static bool TryAppendSpan(ReadOnlySpan<char> source, char[] destination, ref int length)
        {
            if (destination == null || length < 0 || length > destination.Length)
                return false;

            int copyLength = math.min(source.Length, destination.Length - length);
            if (copyLength <= 0)
                return source.Length == 0;

            source.Slice(0, copyLength).CopyTo(destination.AsSpan(length, copyLength));
            length += copyLength;
            return copyLength == source.Length;
        }

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshCachedInteractText();
        }
#endif
    }
}
