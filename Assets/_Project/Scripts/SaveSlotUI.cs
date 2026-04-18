using System;
using Hecton.Localization;
using Hecton8.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// UI component for a save slot button.
    /// Supports both authored two-text layouts and compact single-text slots.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SaveSlotUI : MonoBehaviour
    {
        [Header("=== Text Fields ===")]
        [SerializeField] private TMP_Text slotNameText;
        [SerializeField] private TMP_Text detailsText;

        [Header("=== Thumbnail ===")]
        [SerializeField] private Hecton8.UI.SaveSlotThumbnail thumbnail;

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
        private bool _useCompactSingleTextLayout;

        /// <summary>
        /// True when the slot button can currently be selected by menu navigation.
        /// </summary>
        public bool IsInteractable => _button != null && _button.interactable;

        /// <summary>
        /// Exposes the authored button for menu focus routing.
        /// </summary>
        public Button ButtonComponent => _button;

        /// <summary>
        /// Slot id owned by this authored slot view.
        /// </summary>
        public string SlotId => _slotId;

        /// <summary>
        /// True when this slot currently represents existing save data.
        /// </summary>
        public bool HasSaveData => _exists;

        private void Awake()
        {
            AutoWireTextReferences();
            _button = GetComponent<Button>();
            _button.onClick.RemoveListener(OnButtonClicked);
            _button.onClick.AddListener(OnButtonClicked);

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

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            if (!string.IsNullOrEmpty(_slotId))
                ApplyPresentation();
        }

        /// <summary>
        /// Initializes the slot with raw metadata.
        /// </summary>
        public void Init(
            string slotId,
            bool exists,
            string timestamp,
            float playtime,
            Action<string> onClickCallback)
        {
            _slotId = slotId;
            _exists = exists;
            _timestamp = timestamp;
            _playtime = playtime;
            _sceneName = string.Empty;
            _statusLabel = string.Empty;
            _integrityState = exists ? SaveSlotIntegrityState.Healthy : SaveSlotIntegrityState.Empty;
            _onClickCallback = onClickCallback;

            ApplyPresentation();
            UpdateThumbnail();

            if (_button != null)
                _button.interactable = _exists && _onClickCallback != null;
        }

        /// <summary>
        /// Initializes the slot from validated slot info.
        /// </summary>
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
            ApplyPresentation();
            UpdateThumbnail();
        }

        private void AutoWireTextReferences()
        {
            if (slotNameText != null && detailsText != null)
                return;

            TMP_Text firstText = null;
            TMP_Text secondText = null;
            FindTextReferences(transform, ref firstText, ref secondText);

            if (slotNameText == null)
                slotNameText = firstText;

            if (detailsText == null)
                detailsText = secondText;

            _useCompactSingleTextLayout = slotNameText != null && detailsText == null;
            if (_useCompactSingleTextLayout)
                ConfigureCompactSingleTextLayout(slotNameText);
        }

        private static void FindTextReferences(Transform parent, ref TMP_Text firstText, ref TMP_Text secondText)
        {
            if (parent == null || secondText != null)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                    continue;

                if (child.TryGetComponent(out TMP_Text text))
                {
                    if (firstText == null)
                        firstText = text;
                    else if (secondText == null)
                    {
                        secondText = text;
                        return;
                    }
                }

                FindTextReferences(child, ref firstText, ref secondText);
                if (secondText != null)
                    return;
            }
        }

        private static void ConfigureCompactSingleTextLayout(TMP_Text text)
        {
            if (text == null)
                return;

            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = Mathf.Min(text.fontSize, 52f);
            text.lineSpacing = -10f;
            text.maxVisibleLines = 2;
        }

        private void ApplyPresentation()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string prefix = loc != null
                ? loc.Get(LocalizationKeys.SLOT_PREFIX)
                : "SLOT";
            string number = ExtractSlotNumber(_slotId);
            string slotLine = string.Concat(prefix, " ", number);
            string detailsLine = BuildDetailsLine(loc);

            if (_useCompactSingleTextLayout && slotNameText != null)
            {
                slotNameText.SetText(string.Concat(slotLine, "\n", detailsLine));
                slotNameText.color = _exists
                    ? GetStatusColor(_integrityState, _slotNameBaseColor)
                    : _slotNameBaseColor;
                return;
            }

            if (slotNameText != null)
            {
                slotNameText.SetText(slotLine);
                slotNameText.color = _exists
                    ? GetStatusColor(_integrityState, _slotNameBaseColor)
                    : _slotNameBaseColor;
            }

            if (detailsText != null)
            {
                detailsText.SetText(detailsLine);
                detailsText.color = _exists
                    ? GetStatusColor(_integrityState, _detailsBaseColor)
                    : _detailsBaseColor;
            }
        }

        private string BuildDetailsLine(LocalizationManager loc)
        {
            if (_useCompactSingleTextLayout)
                return BuildCompactDetailsLine(loc);

            if (_exists)
            {
                string formattedPlaytime = FormatPlaytime(_playtime);
                string sceneChunk = string.IsNullOrEmpty(_sceneName) ? string.Empty : string.Concat(" | ", _sceneName);
                string statusChunk = string.IsNullOrEmpty(_statusLabel) ? string.Empty : string.Concat("\n", _statusLabel);
                return string.Concat(_timestamp, " | ", formattedPlaytime, sceneChunk, statusChunk);
            }

            return loc != null
                ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                : "NO DATA";
        }

        private string BuildCompactDetailsLine(LocalizationManager loc)
        {
            if (!_exists)
            {
                string noData = loc != null
                    ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                    : "NO DATA";
                return string.Concat("<size=58%>", noData, "</size>");
            }

            string formattedPlaytime = FormatPlaytime(_playtime);
            string compactSceneName = GetCompactSceneName(loc, _sceneName);
            string compactStatus = GetCompactStatusLabel(loc, _integrityState, _statusLabel);

            string details = string.IsNullOrEmpty(compactSceneName)
                ? formattedPlaytime
                : string.Concat(formattedPlaytime, " | ", compactSceneName);

            if (!string.IsNullOrEmpty(compactStatus))
                details = string.Concat(details, " | ", compactStatus);

            return string.Concat("<size=52%>", details, "</size>");
        }

        private static string GetCompactSceneName(LocalizationManager loc, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return string.Empty;

            if (string.Equals(sceneName, "02_HECTON_WORLD", StringComparison.Ordinal))
            {
                return loc != null
                    ? loc.Get(LocalizationKeys.SLOT_SCENE_WORLD)
                    : "WORLD";
            }

            const int CompactSceneNameLimit = 16;
            if (sceneName.Length <= CompactSceneNameLimit)
                return sceneName;

            return string.Concat(sceneName.Substring(0, CompactSceneNameLimit - 1), "...");
        }

        private static string GetCompactStatusLabel(
            LocalizationManager loc,
            SaveSlotIntegrityState integrityState,
            string fallbackStatusLabel)
        {
            switch (integrityState)
            {
                case SaveSlotIntegrityState.Healthy:
                    return string.Empty;
                case SaveSlotIntegrityState.HealthyWithBackup:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_BACKUP, "BACKUP");
                case SaveSlotIntegrityState.BackupOnly:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_BACKUP_ONLY, "BACKUP ONLY");
                case SaveSlotIntegrityState.MissingMetadata:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_NO_META, "NO META");
                case SaveSlotIntegrityState.MetadataRecoveredFromBackup:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_META_RESTORED, "META RESTORED");
                case SaveSlotIntegrityState.MetadataSynthesized:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_META_SYNTH, "META SYNTH");
                case SaveSlotIntegrityState.CorruptedMetadata:
                    return ResolveCompactLabel(loc, LocalizationKeys.SLOT_STATUS_CORRUPT, "CORRUPT");
                default:
                    return string.IsNullOrEmpty(fallbackStatusLabel) ? string.Empty : fallbackStatusLabel;
            }
        }

        private static string ResolveCompactLabel(LocalizationManager loc, string key, string fallback)
        {
            return loc != null
                ? loc.GetOrFallback(loc.CurrentLanguage, key, fallback)
                : fallback;
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

        private void OnButtonClicked()
        {
            _onClickCallback?.Invoke(_slotId);
        }

        private static string FormatPlaytime(float totalSeconds)
        {
            if (totalSeconds < 0f)
                totalSeconds = 0f;

            int totalMinutes = Mathf.FloorToInt(totalSeconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            return string.Format("{0:D2}:{1:D2}", hours, minutes);
        }

        private static string ExtractSlotNumber(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
                return "?";

            int underscoreIndex = slotId.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotId.Length - 1)
                return slotId.Substring(underscoreIndex + 1);

            return slotId;
        }

        /// <summary>
        /// Updates thumbnail display based on slot state.
        /// </summary>
        private void UpdateThumbnail()
        {
            if (thumbnail == null)
                return;

            if (_exists && !string.IsNullOrEmpty(_slotId))
                thumbnail.LoadThumbnail(_slotId);
            else
                thumbnail.ClearThumbnail();
        }
    }
}
