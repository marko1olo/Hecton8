using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;
using Hecton8.SaveSystem;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// UI component for a save slot button prefab.
    /// Displays slot name, metadata (date, playtime), handles click.
    /// All visible text is localized.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SaveSlotUI : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // INSPECTOR
        // ──────────────────────────────────────────────
        [Header("=== TEXT FIELDS ===")]
        [SerializeField] private TMP_Text slotNameText;
        [SerializeField] private TMP_Text detailsText;

        // ──────────────────────────────────────────────
        // RUNTIME
        // ──────────────────────────────────────────────
        private Button _button;
        private string _slotId;
        private bool _exists;
        private string _timestamp;
        private float _playtime;
        private string _sceneName;
        private string _statusLabel;
        private SaveSlotIntegrityState _integrityState;
        private Action<string> _onClickCallback;
        private Color _slotNameBaseColor;
        private Color _detailsBaseColor;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (slotNameText != null)
                _slotNameBaseColor = slotNameText.color;
            if (detailsText != null)
                _detailsBaseColor = detailsText.color;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        // ══════════════════════════════════════════════
        // LOCALIZATION
        // ══════════════════════════════════════════════

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            // Re-apply texts with new language
            if (!string.IsNullOrEmpty(_slotId))
            {
                ApplyTexts();
            }
        }

        // ══════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Initializes the slot with save data.
        /// </summary>
        /// <param name="slotId">Slot identifier (e.g., "slot_1").</param>
        /// <param name="exists">Whether a save exists in this slot.</param>
        /// <param name="timestamp">
        /// Save date/time string (ignored if exists == false).
        /// </param>
        /// <param name="playtime">
        /// Play time in seconds (ignored if exists == false).
        /// </param>
        /// <param name="onClickCallback">
        /// Click callback receiving slotId. Null = button will be non-interactable.
        /// </param>
        public void Init(
            string slotId,
            bool exists,
            string timestamp,
            float playtime,
            Action<string> onClickCallback)
        {
            _slotId          = slotId;
            _exists          = exists;
            _timestamp       = timestamp;
            _playtime        = playtime;
            _sceneName       = string.Empty;
            _statusLabel     = string.Empty;
            _integrityState  = exists ? SaveSlotIntegrityState.Healthy : SaveSlotIntegrityState.Empty;
            _onClickCallback = onClickCallback;

            ApplyTexts();

            // Button setup
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();

                if (_exists && _onClickCallback != null)
                {
                    _button.interactable = true;
                    _button.onClick.AddListener(OnButtonClicked);
                }
                else
                {
                    _button.interactable = false;
                }
            }
        }

        public void Init(SaveSlotInfo slotInfo, Action<string> onClickCallback)
        {
            if (slotInfo == null)
            {
                Init(string.Empty, false, string.Empty, 0f, onClickCallback);
                return;
            }

            SaveMetadata metadata = slotInfo.metadata;
            Init(
                slotInfo.slotName,
                slotInfo.HasAnySaveData,
                metadata != null ? metadata.GetDateTime().ToLocalTime().ToString("g") : string.Empty,
                metadata != null ? metadata.totalPlayTime : 0f,
                onClickCallback);

            _sceneName = metadata != null ? metadata.sceneName : string.Empty;
            _statusLabel = slotInfo.GetStatusLabel();
            _integrityState = slotInfo.IntegrityState;
            ApplyTexts();
        }

        // ══════════════════════════════════════════════
        // TEXT APPLICATION
        // ══════════════════════════════════════════════

        private void ApplyTexts()
        {
            LocalizationManager loc = LocalizationManager.Instance;

            // ── Slot name ──
            if (slotNameText != null)
            {
                string prefix = loc != null
                    ? loc.Get(LocalizationKeys.SLOT_PREFIX)
                    : "SLOT";

                string number = ExtractSlotNumber(_slotId);
                slotNameText.SetText(string.Concat(prefix, " ", number));
            }

            // ── Details ──
            if (detailsText != null)
            {
                if (_exists)
                {
                    string formattedPlaytime = FormatPlaytime(_playtime);
                    string sceneChunk = string.IsNullOrEmpty(_sceneName) ? string.Empty : string.Concat(" | ", _sceneName);
                    string statusChunk = string.IsNullOrEmpty(_statusLabel) ? string.Empty : string.Concat("\n", _statusLabel);
                    detailsText.SetText(string.Concat(_timestamp, " | ", formattedPlaytime, sceneChunk, statusChunk));
                    detailsText.color = GetStatusColor(_integrityState, _detailsBaseColor);
                    if (slotNameText != null)
                        slotNameText.color = GetStatusColor(_integrityState, _slotNameBaseColor);
                }
                else
                {
                    string noData = loc != null
                        ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                        : "NO DATA";

                    detailsText.SetText(noData);
                    detailsText.color = _detailsBaseColor;
                    if (slotNameText != null)
                        slotNameText.color = _slotNameBaseColor;
                }
            }
        }

        private static Color GetStatusColor(SaveSlotIntegrityState integrityState, Color fallback)
        {
            switch (integrityState)
            {
                case SaveSlotIntegrityState.Healthy:
                case SaveSlotIntegrityState.HealthyWithBackup:
                    return fallback;
                case SaveSlotIntegrityState.BackupOnly:
                case SaveSlotIntegrityState.MetadataRecoveredFromBackup:
                    return new Color(0.92f, 0.79f, 0.36f, fallback.a);
                case SaveSlotIntegrityState.MetadataSynthesized:
                case SaveSlotIntegrityState.MissingMetadata:
                    return new Color(0.98f, 0.62f, 0.36f, fallback.a);
                case SaveSlotIntegrityState.CorruptedMetadata:
                    return new Color(0.94f, 0.36f, 0.36f, fallback.a);
                default:
                    return fallback;
            }
        }

        // ══════════════════════════════════════════════
        // BUTTON HANDLER
        // ══════════════════════════════════════════════

        private void OnButtonClicked()
        {
            _onClickCallback?.Invoke(_slotId);
        }

        // ══════════════════════════════════════════════
        // FORMATTING UTILITIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Converts playtime from seconds to "HH:MM" format.
        /// </summary>
        private static string FormatPlaytime(float totalSeconds)
        {
            if (totalSeconds < 0f) totalSeconds = 0f;

            int totalMinutes = Mathf.FloorToInt(totalSeconds / 60f);
            int hours   = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return string.Format("{0:D2}:{1:D2}", hours, minutes);
        }

        /// <summary>
        /// Extracts numeric suffix from slot ID.
        /// "slot_1" → "1", "slot_12" → "12"
        /// </summary>
        private static string ExtractSlotNumber(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
                return "?";

            int underscoreIndex = slotId.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotId.Length - 1)
            {
                return slotId.Substring(underscoreIndex + 1);
            }

            return slotId;
        }
    }
}
