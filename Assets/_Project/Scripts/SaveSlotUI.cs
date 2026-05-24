using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// UI component for a save slot button.
    /// Supports both authored two-text layouts and compact single-text slots.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SaveSlotUI : MonoBehaviour, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        private UnityAction _buttonClickAction;
        private LocalizationManager _localization;
        private bool _hotSwapRegistered;
        private readonly char[] _slotLineBuffer = new char[128]; // COLD ALLOC: char[128] - save slot title staging buffer - owner: SaveSlotUI
        private readonly char[] _detailsLineBuffer = new char[512]; // COLD ALLOC: char[512] - save slot metadata staging buffer - owner: SaveSlotUI
        private readonly char[] _compactLineBuffer = new char[768]; // COLD ALLOC: char[768] - compact save slot combined label staging buffer - owner: SaveSlotUI
        private readonly char[] _timestampBuffer = new char[32]; // COLD ALLOC: char[32] - timestamp staging buffer, yyyy-MM-dd HH:mm - owner: SaveSlotUI
        private int _timestampLength;

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
            CacheRegistryServicesCold();
            AutoWireTextReferences();
            TryGetComponent(out _button);
            _buttonClickAction = OnButtonClicked; // COLD ALLOC: UnityAction[1] - cached save slot click listener - owner: SaveSlotUI
            if (_button != null)
            {
                _button.onClick.RemoveListener(_buttonClickAction);
                _button.onClick.AddListener(_buttonClickAction);
            }

            if (slotNameText != null)
                _slotNameBaseColor = slotNameText.color;

            if (detailsText != null)
                _detailsBaseColor = detailsText.color;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (_button != null)
                _button.onClick.RemoveListener(_buttonClickAction);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            OnLanguageChanged((GameLanguage)payload.Language);

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
            _timestampLength = CopyStringToBuffer(timestamp, _timestampBuffer);
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
                string.Empty,
                metadata != null ? metadata.totalPlayTime : 0f,
                onClickCallback);

            _timestampLength = metadata != null
                ? WriteTimestamp(metadata.GetDateTime().ToLocalTime(), _timestampBuffer)
                : 0;
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

            TMP_Text namedSlotNameText = null;
            TMP_Text namedDetailsText = null;
            FindNamedTextReferences(transform, ref namedSlotNameText, ref namedDetailsText);

            TMP_Text firstText = null;
            TMP_Text secondText = null;
            if (namedSlotNameText == null || namedDetailsText == null)
                FindTextReferences(transform, ref firstText, ref secondText);

            if (slotNameText == null)
                slotNameText = namedSlotNameText != null ? namedSlotNameText : firstText;

            if (detailsText == null)
            {
                TMP_Text detailsCandidate = namedDetailsText != null ? namedDetailsText : secondText;
                if (detailsCandidate != slotNameText)
                    detailsText = detailsCandidate;
            }

            _useCompactSingleTextLayout = slotNameText != null && detailsText == null;
            if (_useCompactSingleTextLayout)
                ConfigureCompactSingleTextLayout(slotNameText);
        }

        private static void FindNamedTextReferences(Transform parent, ref TMP_Text slotText, ref TMP_Text detailsText)
        {
            if (parent == null || (slotText != null && detailsText != null))
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                    continue;

                if (child.TryGetComponent(out TMP_Text text))
                {
                    string candidateName = child.name;
                    if (slotText == null && IsTextNameMatch(candidateName, "slot", "title", "header", "name"))
                        slotText = text;
                    else if (detailsText == null && IsTextNameMatch(candidateName, "detail", "meta", "info", "status", "body"))
                        detailsText = text;
                }

                FindNamedTextReferences(child, ref slotText, ref detailsText);
                if (slotText != null && detailsText != null)
                    return;
            }
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

        private static bool IsTextNameMatch(string candidateName, params string[] tokens)
        {
            if (string.IsNullOrEmpty(candidateName) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) &&
                    candidateName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureCompactSingleTextLayout(TMP_Text text)
        {
            if (text == null)
                return;

            Hecton8.UI.LocalizedTMPAutoSizer.Configure(
                text,
                text.fontSize * 0.68f,
                text.fontSize,
                TextOverflowModes.Ellipsis,
                TextWrappingModes.Normal);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = Mathf.Min(text.fontSize, 52f);
            text.lineSpacing = -10f;
            text.maxVisibleLines = 2;
        }

        private void ApplyPresentation()
        {
            LocalizationManager loc = _localization;
            string prefix = loc != null
                ? loc.Get(LocalizationKeys.SLOT_PREFIX)
                : "SLOT";
            int slotLineLength = BuildSlotLine(prefix.AsSpan(), ExtractSlotNumberSpan(_slotId), _slotLineBuffer);
            int detailsLineLength = _useCompactSingleTextLayout
                ? BuildCompactDetailsLine(loc, _detailsLineBuffer)
                : BuildDetailsLine(loc, _detailsLineBuffer);

            if (slotNameText != null)
            {
                Hecton8.UI.LocalizedTMPAutoSizer.Configure(
                    slotNameText,
                    slotNameText.fontSize * 0.68f,
                    slotNameText.fontSize,
                    TextOverflowModes.Ellipsis,
                    _useCompactSingleTextLayout ? TextWrappingModes.Normal : TextWrappingModes.NoWrap);
            }

            if (detailsText != null)
            {
                Hecton8.UI.LocalizedTMPAutoSizer.Configure(
                    detailsText,
                    detailsText.fontSize * 0.72f,
                    detailsText.fontSize,
                    TextOverflowModes.Ellipsis,
                    TextWrappingModes.NoWrap);
            }

            if (_useCompactSingleTextLayout && slotNameText != null)
            {
                int compactLength = 0;
                Append(_slotLineBuffer, slotLineLength, _compactLineBuffer, ref compactLength);
                Append("\n".AsSpan(), _compactLineBuffer, ref compactLength);
                Append(_detailsLineBuffer, detailsLineLength, _compactLineBuffer, ref compactLength);
                slotNameText.SetCharArray(_compactLineBuffer, 0, compactLength);
                slotNameText.color = _exists
                    ? GetStatusColor(_integrityState, _slotNameBaseColor)
                    : _slotNameBaseColor;
                return;
            }

            if (slotNameText != null)
            {
                slotNameText.SetCharArray(_slotLineBuffer, 0, slotLineLength);
                slotNameText.color = _exists
                    ? GetStatusColor(_integrityState, _slotNameBaseColor)
                    : _slotNameBaseColor;
            }

            if (detailsText != null)
            {
                detailsText.SetCharArray(_detailsLineBuffer, 0, detailsLineLength);
                detailsText.color = _exists
                    ? GetStatusColor(_integrityState, _detailsBaseColor)
                    : _detailsBaseColor;
            }
        }

        private static int BuildSlotLine(ReadOnlySpan<char> prefix, ReadOnlySpan<char> number, char[] destination)
        {
            int cursor = 0;
            Append(prefix, destination, ref cursor);
            Append(" ".AsSpan(), destination, ref cursor);
            Append(number, destination, ref cursor);
            return cursor;
        }

        private int BuildDetailsLine(LocalizationManager loc, char[] destination)
        {
            if (_useCompactSingleTextLayout)
                return BuildCompactDetailsLine(loc, destination);

            int cursor = 0;

            if (_exists)
            {
                string sceneLabel = ResolveSceneLabel(loc, _sceneName);
                string statusLabel = ResolveStatusLabel(loc, _integrityState, _statusLabel);
                if (_timestampLength > 0)
                    Append(_timestampBuffer, _timestampLength, destination, ref cursor);
                else
                    Append(string.IsNullOrEmpty(_timestamp) ? ReadOnlySpan<char>.Empty : _timestamp.AsSpan(), destination, ref cursor);
                Append(" | ".AsSpan(), destination, ref cursor);
                AppendPlaytime(_playtime, destination, ref cursor);
                if (!string.IsNullOrEmpty(sceneLabel))
                {
                    Append(" | ".AsSpan(), destination, ref cursor);
                    Append(sceneLabel.AsSpan(), destination, ref cursor);
                }

                if (!string.IsNullOrEmpty(statusLabel))
                {
                    Append("\n".AsSpan(), destination, ref cursor);
                    Append(statusLabel.AsSpan(), destination, ref cursor);
                }

                return cursor;
            }

            string noData = loc != null
                ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                : "NO DATA";
            Append(noData.AsSpan(), destination, ref cursor);
            return cursor;
        }

        private int BuildCompactDetailsLine(LocalizationManager loc, char[] destination)
        {
            int cursor = 0;
            if (!_exists)
            {
                string noData = loc != null
                    ? loc.Get(LocalizationKeys.SLOT_NO_DATA)
                    : "NO DATA";
                Append("<size=58%>".AsSpan(), destination, ref cursor);
                Append(noData.AsSpan(), destination, ref cursor);
                Append("</size>".AsSpan(), destination, ref cursor);
                return cursor;
            }

            Append("<size=52%>".AsSpan(), destination, ref cursor);
            AppendPlaytime(_playtime, destination, ref cursor);
            string sceneLabel = ResolveSceneLabel(loc, _sceneName);
            string compactStatus = GetCompactStatusLabel(loc, _integrityState, _statusLabel);

            if (!string.IsNullOrEmpty(sceneLabel))
            {
                Append(" | ".AsSpan(), destination, ref cursor);
                AppendCompactSceneName(sceneLabel.AsSpan(), destination, ref cursor);
            }

            if (!string.IsNullOrEmpty(compactStatus))
            {
                Append(" | ".AsSpan(), destination, ref cursor);
                Append(compactStatus.AsSpan(), destination, ref cursor);
            }

            Append("</size>".AsSpan(), destination, ref cursor);
            return cursor;
        }

        private static void AppendCompactSceneName(ReadOnlySpan<char> sceneLabel, char[] destination, ref int cursor)
        {
            const int CompactSceneNameLimit = 16;
            if (sceneLabel.Length <= CompactSceneNameLimit)
            {
                Append(sceneLabel, destination, ref cursor);
                return;
            }

            Append(sceneLabel.Slice(0, CompactSceneNameLimit - 1), destination, ref cursor);
            Append("...".AsSpan(), destination, ref cursor);
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

        private static string ResolveSceneLabel(LocalizationManager loc, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return string.Empty;

            if (string.Equals(sceneName, "02_HECTON_WORLD", StringComparison.Ordinal))
            {
                return loc != null
                    ? loc.Get(LocalizationKeys.SLOT_SCENE_WORLD)
                    : "WORLD";
            }

            return sceneName;
        }

        private static string ResolveStatusLabel(
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
                return;

            _localization = currentService as LocalizationManager;
            if (!string.IsNullOrEmpty(_slotId))
                ApplyPresentation();
        }

        private void CacheRegistryServicesCold()
        {
            _localization = GlobalRegistry.Localization;
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

        private static void AppendPlaytime(float totalSeconds, char[] destination, ref int cursor)
        {
            if (totalSeconds < 0f)
                totalSeconds = 0f;

            int totalMinutes = Mathf.FloorToInt(totalSeconds / 60f);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            int hourDigits = CountTwoDigitMinimumDecimalDigits(hours);
            if (destination == null || cursor + hourDigits + 3 > destination.Length)
                return;

            WritePaddedPositiveDecimal(hours, destination.AsSpan(cursor, hourDigits));
            cursor += hourDigits;
            destination[cursor++] = ':';
            WritePaddedPositiveDecimal(minutes, destination.AsSpan(cursor, 2));
            cursor += 2;
        }

        private static int WriteTimestamp(DateTime localTime, char[] destination)
        {
            if (destination == null || destination.Length < 16)
                return 0;

            int cursor = 0;
            WritePaddedPositiveDecimal(localTime.Year, destination.AsSpan(cursor, 4));
            cursor += 4;
            destination[cursor++] = '-';
            WritePaddedPositiveDecimal(localTime.Month, destination.AsSpan(cursor, 2));
            cursor += 2;
            destination[cursor++] = '-';
            WritePaddedPositiveDecimal(localTime.Day, destination.AsSpan(cursor, 2));
            cursor += 2;
            destination[cursor++] = ' ';
            WritePaddedPositiveDecimal(localTime.Hour, destination.AsSpan(cursor, 2));
            cursor += 2;
            destination[cursor++] = ':';
            WritePaddedPositiveDecimal(localTime.Minute, destination.AsSpan(cursor, 2));
            cursor += 2;
            return cursor;
        }

        private static int CopyStringToBuffer(string source, char[] destination)
        {
            if (string.IsNullOrEmpty(source) || destination == null || destination.Length == 0)
                return 0;

            int length = Mathf.Min(source.Length, destination.Length);
            source.AsSpan(0, length).CopyTo(destination.AsSpan(0, length));
            return length;
        }

        private static int CountTwoDigitMinimumDecimalDigits(int value)
        {
            int safeValue = value < 0 ? -value : value;
            int digits = 1;
            while (safeValue >= 10)
            {
                safeValue /= 10;
                digits++;
            }

            return digits < 2 ? 2 : digits;
        }

        private static void WritePaddedPositiveDecimal(int value, Span<char> destination)
        {
            int safeValue = value < 0 ? -value : value;
            for (int i = destination.Length - 1; i >= 0; i--)
            {
                destination[i] = (char)('0' + safeValue % 10);
                safeValue /= 10;
            }
        }

        private static ReadOnlySpan<char> ExtractSlotNumberSpan(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
                return "?".AsSpan();

            int underscoreIndex = slotId.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotId.Length - 1)
                return slotId.AsSpan(underscoreIndex + 1);

            return slotId.AsSpan();
        }

        private static bool Append(ReadOnlySpan<char> source, char[] destination, ref int cursor)
        {
            if (destination == null || source.Length == 0 || cursor >= destination.Length)
                return source.Length == 0;

            int writable = Mathf.Min(source.Length, destination.Length - cursor);
            source.Slice(0, writable).CopyTo(destination.AsSpan(cursor, writable));
            cursor += writable;
            return writable == source.Length;
        }

        private static bool Append(char[] source, int sourceLength, char[] destination, ref int cursor)
        {
            if (source == null || sourceLength <= 0)
                return true;

            int safeLength = Mathf.Clamp(sourceLength, 0, source.Length);
            return Append(source.AsSpan(0, safeLength), destination, ref cursor);
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
