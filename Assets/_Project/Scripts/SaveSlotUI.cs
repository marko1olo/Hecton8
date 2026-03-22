using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;

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
        private Action<string> _onClickCallback;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _button = GetComponent<Button>();
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
                    detailsText.SetText(
                        string.Concat(_timestamp, " | ", formattedPlaytime)
                    );
                }
                else
                {
                    string noData = loc != null
                        ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                        : "NO DATA";

                    detailsText.SetText(noData);
                }
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