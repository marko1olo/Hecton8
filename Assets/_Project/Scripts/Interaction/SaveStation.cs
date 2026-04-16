// ============================================================================
// HECTON-8 - SaveStation.cs
// Мирный терминал сохранения с defensive-поведением и интеграцией в HUD.
// ============================================================================

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Терминал в мире, который запускает сохранение в указанный слот.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SaveStation : MonoBehaviour, IInteractable
    {
        [Header("── Settings ──────────────────────────────")]
        [Tooltip("Отображаемое имя терминала в подсказке взаимодействия.")]
        [SerializeField] private string _stationName = "Save Station";
        [SerializeField] private LocalizedTextReference _localizedStationName;

        [Tooltip("Имя save-слота, в который терминал будет сохранять игру.")]
        [SerializeField] private string _saveSlot = "slot_0";

        [Tooltip("Необязательная ссылка на HUD-уведомления. Если не задана, ищется лениво.")]
        [SerializeField] private HUDNotification _hudNotification;

        [Header("── Audio ──────────────────────────────")]
        [Tooltip("Звук активации терминала.")]
        [SerializeField] private AudioClip _interactionSound;

        private string _cachedInteractText;

        private void Awake()
        {
            RefreshCachedInteractText();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RefreshCachedInteractText();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
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
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                ResolveHudNotification();
                _hudNotification?.ShowWarning(ResolveLocalized(LocalizationKeys.SAVE_STATION_OFFLINE, "SAVE SYSTEM OFFLINE"));
                Debug.LogError("[SaveStation] SaveManager instance not found.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(_saveSlot))
            {
                ResolveHudNotification();
                _hudNotification?.ShowWarning(ResolveLocalized(LocalizationKeys.SAVE_STATION_SLOT_NOT_CONFIGURED, "SAVE SLOT NOT CONFIGURED"));
                Debug.LogWarning("[SaveStation] Save slot is not configured.", this);
                return;
            }

            if (saveManager.IsBusy)
            {
                ResolveHudNotification();
                _hudNotification?.ShowInfo(ResolveLocalized(LocalizationKeys.SAVE_STATION_BUSY, "SAVE ALREADY IN PROGRESS"));
                Debug.LogWarning($"[SaveStation] Save skipped for '{_saveSlot}' because another save/load is already running.", this);
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

        private void PlayInteractionSound()
        {
            if (_interactionSound == null)
                return;

            SpatialAudioManager audioManager = SpatialAudioManager.Instance;
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
            string stationName = _localizedStationName.ResolveOrFallback(FallbackOrDefault(_stationName, "Save Station"));
            string actionLabel = ResolveLocalized(LocalizationKeys.INTERACT_SAVE_GAME, "Save Game");
            _cachedInteractText = actionLabel + " (" + stationName + ")";
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshCachedInteractText();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
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
