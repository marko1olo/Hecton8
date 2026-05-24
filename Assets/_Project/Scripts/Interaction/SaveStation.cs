// ============================================================================
// HECTON-8 - SaveStation.cs
// Mirnyy terminal sohraneniya s defensive-povedeniem i integratsiey v HUD.
// ============================================================================

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Terminal v mire, kotoryy zapuskaet sohranenie v ukazannyy slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SaveStation : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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

        private string _cachedInteractText;
        private SaveManager _saveManager;
        private Hecton8.Core.IAudioService _audioService;
        private LocalizationManager _localization;
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
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshCachedInteractText();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
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
            SaveManager saveManager = _saveManager;
            if (saveManager == null)
            {
                ResolveHudNotification();
                _hudNotification?.ShowWarning(ResolveLocalized(LocalizationKeys.SAVE_STATION_OFFLINE, "SAVE SYSTEM OFFLINE"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SaveStation] SaveManager instance not found.", this);
#endif
                return;
            }

            if (string.IsNullOrWhiteSpace(_saveSlot))
            {
                ResolveHudNotification();
                _hudNotification?.ShowWarning(ResolveLocalized(LocalizationKeys.SAVE_STATION_SLOT_NOT_CONFIGURED, "SAVE SLOT NOT CONFIGURED"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveStation] Save slot is not configured.", this);
#endif
                return;
            }

            if (!SaveManager.IsSafeSlotName(_saveSlot))
            {
                ResolveHudNotification();
                _hudNotification?.ShowWarning(ResolveLocalized(LocalizationKeys.SAVE_STATION_SLOT_NOT_CONFIGURED, "SAVE SLOT NOT CONFIGURED"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SaveStation] Save slot rejected by SaveManager slot-name guard.", this);
#endif
                return;
            }

            if (saveManager.IsBusy)
            {
                ResolveHudNotification();
                _hudNotification?.ShowInfo(ResolveLocalized(LocalizationKeys.SAVE_STATION_BUSY, "SAVE ALREADY IN PROGRESS"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SaveStation] Save skipped for '{_saveSlot}' because another save/load is already running.", this);
#endif
                return;
            }

            PlayInteractionSound();

            ResolveHudNotification();
            _hudNotification?.ShowInfo(ResolveLocalized(LocalizationKeys.SAVE_STATION_REQUESTED, "SAVE REQUESTED"));

            _ = saveManager.SaveGameAsync(_saveSlot);
            InteractionEvents.RaiseInteractionStarted(this, interactor);
        }

        /// <inheritdoc />
        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(_cachedInteractText, destination, out length);
        }

        private void PlayInteractionSound()
        {
            if (_interactionSound == null)
                return;

            Hecton8.Core.IAudioService audioManager = _audioService;
            if (audioManager == null)
                return;

            audioManager.PlayAtPoint(_interactionSound, transform.position);
        }

        private void ResolveHudNotification()
        {
            if (_hudNotification == null)
                HUDNotification.TryGetActive(out _hudNotification);
        }

        private void RefreshCachedInteractText()
        {
            string stationName = _localizedStationName.ResolveOrFallback(_localization, FallbackOrDefault(_stationName, "Save Station"));
            string actionLabel = ResolveLocalized(LocalizationKeys.INTERACT_SAVE_GAME, "Save Game");
            _cachedInteractText = actionLabel + " (" + stationName + ")";
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    _saveManager = currentService as SaveManager;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as Hecton8.Core.IAudioService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as LocalizationManager;
                    RefreshCachedInteractText();
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _saveManager = GlobalRegistry.SaveRuntime;
            _audioService = GlobalRegistry.Audio;
            _localization = LocalizationManager.ActiveRuntimeInstance;
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

        private string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = _localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
