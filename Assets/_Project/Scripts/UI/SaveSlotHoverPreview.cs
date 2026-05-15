using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Hecton.UI.MainMenu;
using Hecton8.Core;
using Hecton.Localization;
using Hecton8.SaveSystem;
using System.Collections.Generic;
using System;

namespace Hecton8.UI
{
    /// <summary>
    /// Hover preview for save slots â€” shows enlarged thumbnail + metadata on hover.
    /// EXCEEDS SUBNAUTICA: Subnautica has no hover preview, only click-to-load.
    /// Zero-GC: ILateFrameTickable state machine, cached delegates, CanvasGroup alpha.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Slot Hover Preview")]
    public sealed class SaveSlotHoverPreview : MonoBehaviour, ILateFrameTickable, IPointerEnterHandler, IPointerExitHandler, ILocalizationLanguageChangedListener
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("=== PREVIEW PANEL ===")]
        [SerializeField] private CanvasGroup previewPanel;
        [SerializeField] private RectTransform previewContainer;
        [SerializeField] private SaveSlotThumbnail previewThumbnail;
        [SerializeField] private TMP_Text previewTitleText;
        [SerializeField] private TMP_Text previewDetailsText;
        [SerializeField] private TMP_Text previewStatusText;

        [Header("=== SETTINGS ===")]
        [SerializeField] private float hoverDelay = 0.3f;
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.1f;
        [SerializeField] private Vector2 previewOffset = new Vector2(20f, 0f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // FIELDS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private enum State { Idle, WaitingForDelay, FadingIn, Visible, FadingOut }

        private static readonly char[] UnknownTimestampChars = "UNKNOWN".ToCharArray();
        private static readonly char[] ZeroPlaytimeChars = "00:00:00".ToCharArray();

        private State _state;
        private float _timer;
        private float _fadeStartAlpha;
        private string _currentSlotId;
        private bool _registered;
        private SaveSlotUI _slotUI;
        private RectTransform _slotRect;
        private RectTransform _previewParentRect;
        private RectTransform _previewPanelRect;
        private Canvas _rootCanvas;
        private Camera _uiCamera;
        // COLD ALLOC: char[256] - save hover title staging for TMP SetCharArray - owner: SaveSlotHoverPreview
        private readonly char[] _previewTitleBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[256] - save hover preview metadata staging for TMP SetCharArray - owner: SaveSlotHoverPreview
        private readonly char[] _previewDetailsBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[256] - save hover preview integrity status staging for TMP SetCharArray - owner: SaveSlotHoverPreview
        private readonly char[] _previewStatusBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: List<TMP_Text>(8) â€” preview text auto-wire buffer â€” owner: SaveSlotHoverPreview
        private readonly List<TMP_Text> _previewTextResolveBuffer = new List<TMP_Text>(8);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            TryGetComponent(out _slotUI);
            _slotRect = transform as RectTransform;
            _previewPanelRect = previewPanel != null ? previewPanel.transform as RectTransform : null;
            _previewParentRect = previewContainer != null
                ? previewContainer.parent as RectTransform
                : (previewPanel != null ? previewPanel.transform.parent as RectTransform : null);
            _rootCanvas = ResolveNearestParentCanvas(transform);
            _uiCamera = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;
            AutoWirePreviewTextReferences();
            ConfigurePreviewText(previewTitleText, 18f, 32f);
            ConfigurePreviewText(previewDetailsText, 14f, 24f);
            ConfigurePreviewText(previewStatusText, 14f, 24f);
            HideImmediate();
        }

        private static Canvas ResolveNearestParentCanvas(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Canvas canvas))
                    return canvas;
            }

            return null;
        }

        private void OnEnable()
        {
            TryRegister();
            LocalizationEvents.RegisterLanguageListener(this);
            HideImmediate();
        }

        private void OnDisable()
        {
            Unregister();
            LocalizationEvents.UnregisterLanguageListener(this);
            HideImmediate();
        }

        private void OnDestroy()
        {
            Unregister();
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ILATEFRAMETICKABLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void LateFrameTick()
        {
            float dt = Mathf.Max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            switch (_state)
            {
                case State.WaitingForDelay:
                    _timer += dt;
                    if (_timer >= hoverDelay)
                    {
                        _state = State.FadingIn;
                        _timer = 0f;
                        ShowPreview();
                    }
                    break;

                case State.FadingIn:
                    _timer += dt;
                    float fadeInT = Mathf.Clamp01(_timer / fadeInDuration);
                    if (previewPanel != null)
                        previewPanel.alpha = fadeInT;
                    if (fadeInT >= 1f)
                        _state = State.Visible;
                    break;

                case State.FadingOut:
                    _timer += dt;
                    float fadeOutT = Mathf.Clamp01(_timer / fadeOutDuration);
                    if (previewPanel != null)
                        previewPanel.alpha = _fadeStartAlpha * (1f - fadeOutT);
                    if (fadeOutT >= 1f)
                    {
                        HideImmediate();
                        _state = State.Idle;
                    }
                    break;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // POINTER EVENTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_state != State.Idle)
                return;

            if (_slotUI == null || !_slotUI.IsInteractable || !_slotUI.HasSaveData)
                return;

            _currentSlotId = _slotUI.SlotId;
            if (string.IsNullOrEmpty(_currentSlotId))
                return;

            _state = State.WaitingForDelay;
            _timer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_state == State.Idle || _state == State.FadingOut)
                return;

            if (_state == State.WaitingForDelay)
            {
                HideImmediate();
                _state = State.Idle;
                return;
            }

            _state = State.FadingOut;
            _timer = 0f;
            _fadeStartAlpha = previewPanel != null ? previewPanel.alpha : 0f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ShowPreview()
        {
            if (previewPanel == null)
                return;

            PopulatePreviewMetadata();
            if (previewThumbnail != null)
                previewThumbnail.LoadThumbnail(_currentSlotId);

            PositionPreview();

            previewPanel.alpha = 0f;
            previewPanel.interactable = false;
            previewPanel.blocksRaycasts = false;
        }

        private void HideImmediate()
        {
            if (previewPanel == null)
                return;

            previewPanel.alpha = 0f;
            previewPanel.interactable = false;
            previewPanel.blocksRaycasts = false;

            if (previewThumbnail != null)
                previewThumbnail.ClearThumbnail();

            ClearPreviewMetadata();
            _currentSlotId = string.Empty;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage newLanguage)
        {
            if (!string.IsNullOrEmpty(_currentSlotId) &&
                (_state == State.Visible || _state == State.FadingIn))
            {
                PopulatePreviewMetadata();
            }
        }

        private void PopulatePreviewMetadata()
        {
            if (string.IsNullOrEmpty(_currentSlotId))
            {
                ClearPreviewMetadata();
                return;
            }

            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null || !saveManager.TryGetSaveSlotInfo(_currentSlotId, out SaveSlotInfo slotInfo) || slotInfo == null)
            {
                ApplyPreviewTexts(
                    string.Empty,
                    ResolveLocalized(localization, LocalizationKeys.SLOT_NO_DATA, "NO DATA"),
                    string.Empty,
                    Color.white);
                ApplySlotTitle(localization, _currentSlotId);
                return;
            }

            SaveMetadata metadata = slotInfo.Metadata;
            string scene = ResolveSceneLabel(localization, metadata != null ? metadata.SceneName : string.Empty);
            int detailsLength = BuildPreviewDetails(metadata, scene, _previewDetailsBuffer);
            string status = ResolveStatusLabel(localization, slotInfo.IntegrityState, slotInfo.GetStatusLabel());
            Color statusColor = ResolveStatusColor(slotInfo.IntegrityState);

            ApplyPreviewTexts(string.Empty, string.Empty, status, statusColor);
            ApplySlotTitle(localization, _currentSlotId);
            if (previewDetailsText != null)
                previewDetailsText.SetCharArray(_previewDetailsBuffer, 0, detailsLength);
        }

        private void ApplyPreviewTexts(string title, string details, string status, Color statusColor)
        {
            ApplyPreviewText(previewTitleText, title, _previewTitleBuffer);
            ApplyPreviewText(previewDetailsText, details, _previewDetailsBuffer);

            if (previewStatusText != null)
            {
                ApplyPreviewText(previewStatusText, status, _previewStatusBuffer);
                previewStatusText.color = statusColor;
            }
        }

        private static void ApplyPreviewText(TMP_Text text, string value, char[] buffer)
        {
            if (text == null || buffer == null)
                return;

            int length = AppendString(buffer, 0, value);
            text.SetCharArray(buffer, 0, length);
        }

        private void ApplySlotTitle(LocalizationManager localization, string slotId)
        {
            if (previewTitleText == null)
                return;

            int titleLength = BuildSlotTitle(localization, slotId, _previewTitleBuffer);
            previewTitleText.SetCharArray(_previewTitleBuffer, 0, titleLength);
        }

        private void ClearPreviewMetadata()
        {
            ApplyPreviewTexts(string.Empty, string.Empty, string.Empty, Color.white);
        }

        private void AutoWirePreviewTextReferences()
        {
            if (previewPanel == null ||
                (previewTitleText != null && previewDetailsText != null && previewStatusText != null))
                return;

            _previewTextResolveBuffer.Clear();
            previewPanel.GetComponentsInChildren(true, _previewTextResolveBuffer);
            for (int i = 0; i < _previewTextResolveBuffer.Count; i++)
            {
                TMP_Text candidate = _previewTextResolveBuffer[i];
                if (candidate == null)
                    continue;

                string candidateName = candidate.name;
                if (previewTitleText == null && IsPreviewTextMatch(candidateName, "title", "header", "slot"))
                {
                    previewTitleText = candidate;
                    continue;
                }

                if (previewDetailsText == null && IsPreviewTextMatch(candidateName, "detail", "meta", "info", "body"))
                {
                    previewDetailsText = candidate;
                    continue;
                }

                if (previewStatusText == null && IsPreviewTextMatch(candidateName, "status", "integrity", "warning"))
                {
                    previewStatusText = candidate;
                }
            }

            for (int i = 0; i < _previewTextResolveBuffer.Count; i++)
            {
                TMP_Text candidate = _previewTextResolveBuffer[i];
                if (candidate == null)
                    continue;

                if (previewTitleText == null)
                {
                    previewTitleText = candidate;
                    continue;
                }

                if (previewDetailsText == null && candidate != previewTitleText)
                {
                    previewDetailsText = candidate;
                    continue;
                }

                if (previewStatusText == null &&
                    candidate != previewTitleText &&
                    candidate != previewDetailsText)
                {
                    previewStatusText = candidate;
                    break;
                }
            }

            _previewTextResolveBuffer.Clear();
        }

        private static bool IsPreviewTextMatch(string candidateName, string tokenA, string tokenB, string tokenC)
        {
            if (string.IsNullOrEmpty(candidateName))
                return false;

            return candidateName.IndexOf(tokenA, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidateName.IndexOf(tokenB, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidateName.IndexOf(tokenC, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPreviewTextMatch(
            string candidateName,
            string tokenA,
            string tokenB,
            string tokenC,
            string tokenD)
        {
            return IsPreviewTextMatch(candidateName, tokenA, tokenB, tokenC) ||
                   (!string.IsNullOrEmpty(candidateName) &&
                    candidateName.IndexOf(tokenD, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void ConfigurePreviewText(TMP_Text text, float minSize, float maxSize)
        {
            if (text == null)
                return;

            LocalizedTMPAutoSizer.Configure(
                text,
                minSize,
                maxSize,
                TextOverflowModes.Ellipsis,
                TextWrappingModes.Normal);
        }

        private static int BuildSlotTitle(LocalizationManager localization, string slotId, char[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0;

            string prefix = ResolveLocalized(localization, LocalizationKeys.SLOT_PREFIX, "SLOT");
            int cursor = AppendString(buffer, 0, prefix);
            cursor = AppendChar(buffer, cursor, ' ');
            return AppendSlotNumber(buffer, cursor, slotId);
        }

        private static string ResolveSceneLabel(LocalizationManager localization, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return string.Empty;

            if (string.Equals(sceneName, "02_HECTON_WORLD", System.StringComparison.Ordinal))
                return ResolveLocalized(localization, LocalizationKeys.SLOT_SCENE_WORLD, "WORLD");

            return sceneName;
        }

        private static string ResolveStatusLabel(LocalizationManager localization, SaveSlotIntegrityState integrityState, string fallbackStatus)
        {
            switch (integrityState)
            {
                case SaveSlotIntegrityState.Healthy:
                    return string.Empty;
                case SaveSlotIntegrityState.HealthyWithBackup:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_BACKUP, "BACKUP");
                case SaveSlotIntegrityState.BackupOnly:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_BACKUP_ONLY, "BACKUP ONLY");
                case SaveSlotIntegrityState.MissingMetadata:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_NO_META, "NO META");
                case SaveSlotIntegrityState.MetadataRecoveredFromBackup:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_META_RESTORED, "META RESTORED");
                case SaveSlotIntegrityState.MetadataSynthesized:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_META_SYNTH, "META SYNTH");
                case SaveSlotIntegrityState.CorruptedMetadata:
                    return ResolveLocalized(localization, LocalizationKeys.SLOT_STATUS_CORRUPT, "CORRUPT");
                default:
                    return string.IsNullOrEmpty(fallbackStatus) ? string.Empty : fallbackStatus;
            }
        }

        private static Color ResolveStatusColor(SaveSlotIntegrityState integrityState)
        {
            switch (integrityState)
            {
                case SaveSlotIntegrityState.BackupOnly:
                case SaveSlotIntegrityState.MetadataRecoveredFromBackup:
                    return new Color(0.92f, 0.79f, 0.36f, 1f);
                case SaveSlotIntegrityState.MetadataSynthesized:
                case SaveSlotIntegrityState.MissingMetadata:
                    return new Color(0.98f, 0.62f, 0.36f, 1f);
                case SaveSlotIntegrityState.CorruptedMetadata:
                    return new Color(0.94f, 0.36f, 0.36f, 1f);
                default:
                    return Color.white;
            }
        }

        private static int BuildPreviewDetails(SaveMetadata metadata, string scene, char[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0;

            int cursor = 0;
            if (metadata != null)
            {
                DateTime localTime = metadata.GetDateTime().ToLocalTime();
                cursor = AppendFourDigits(buffer, cursor, localTime.Year);
                cursor = AppendChar(buffer, cursor, '-');
                cursor = AppendTwoDigits(buffer, cursor, localTime.Month);
                cursor = AppendChar(buffer, cursor, '-');
                cursor = AppendTwoDigits(buffer, cursor, localTime.Day);
                cursor = AppendChar(buffer, cursor, ' ');
                cursor = AppendTwoDigits(buffer, cursor, localTime.Hour);
                cursor = AppendChar(buffer, cursor, ':');
                cursor = AppendTwoDigits(buffer, cursor, localTime.Minute);
            }
            else
            {
                cursor = AppendChars(buffer, cursor, UnknownTimestampChars, UnknownTimestampChars.Length);
            }

            cursor = AppendChar(buffer, cursor, '\n');
            cursor = metadata != null
                ? AppendPlaytime(buffer, cursor, metadata.PlayTimeSeconds)
                : AppendChars(buffer, cursor, ZeroPlaytimeChars, ZeroPlaytimeChars.Length);

            if (!string.IsNullOrEmpty(scene))
            {
                cursor = AppendChar(buffer, cursor, '\n');
                cursor = AppendString(buffer, cursor, scene);
            }

            return cursor;
        }

        private static int AppendPlaytime(char[] buffer, int cursor, float playTimeSeconds)
        {
            int totalSeconds = playTimeSeconds > 0f ? (int)playTimeSeconds : 0;
            int hours = Mathf.Min(totalSeconds / 3600, 99);
            int minutes = (totalSeconds / 60) % 60;
            int seconds = totalSeconds % 60;
            cursor = AppendTwoDigits(buffer, cursor, hours);
            cursor = AppendChar(buffer, cursor, ':');
            cursor = AppendTwoDigits(buffer, cursor, minutes);
            cursor = AppendChar(buffer, cursor, ':');
            return AppendTwoDigits(buffer, cursor, seconds);
        }

        private static int AppendString(char[] buffer, int cursor, string value)
        {
            if (string.IsNullOrEmpty(value))
                return cursor;

            int limit = Mathf.Min(value.Length, buffer.Length - cursor);
            for (int i = 0; i < limit; i++)
                buffer[cursor++] = value[i];

            return cursor;
        }

        private static int AppendChars(char[] buffer, int cursor, char[] value, int length)
        {
            int limit = Mathf.Min(length, buffer.Length - cursor);
            for (int i = 0; i < limit; i++)
                buffer[cursor++] = value[i];

            return cursor;
        }

        private static int AppendFourDigits(char[] buffer, int cursor, int value)
        {
            value = Mathf.Clamp(value, 0, 9999);
            cursor = AppendDigit(buffer, cursor, value / 1000);
            cursor = AppendDigit(buffer, cursor, (value / 100) % 10);
            cursor = AppendDigit(buffer, cursor, (value / 10) % 10);
            return AppendDigit(buffer, cursor, value % 10);
        }

        private static int AppendTwoDigits(char[] buffer, int cursor, int value)
        {
            value = Mathf.Clamp(value, 0, 99);
            cursor = AppendDigit(buffer, cursor, value / 10);
            return AppendDigit(buffer, cursor, value % 10);
        }

        private static int AppendDigit(char[] buffer, int cursor, int digit)
        {
            return AppendChar(buffer, cursor, (char)('0' + Mathf.Clamp(digit, 0, 9)));
        }

        private static int AppendChar(char[] buffer, int cursor, char value)
        {
            if (cursor >= buffer.Length)
                return cursor;

            buffer[cursor] = value;
            return cursor + 1;
        }

        private static int AppendSlotNumber(char[] buffer, int cursor, string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
                return AppendChar(buffer, cursor, '?');

            int underscoreIndex = slotId.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotId.Length - 1)
            {
                int limit = Mathf.Min(slotId.Length - underscoreIndex - 1, buffer.Length - cursor);
                for (int i = 0; i < limit; i++)
                    buffer[cursor++] = slotId[underscoreIndex + 1 + i];

                return cursor;
            }

            return AppendString(buffer, cursor, slotId);
        }

        private static string ResolveLocalized(LocalizationManager localization, string key, string fallback)
        {
            return localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void PositionPreview()
        {
            if (previewContainer == null || _slotRect == null || _previewParentRect == null)
                return;

            Vector3 worldCenter = _slotRect.TransformPoint(_slotRect.rect.center);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _previewParentRect,
                    RectTransformUtility.WorldToScreenPoint(_uiCamera, worldCenter),
                    _uiCamera,
                    out Vector2 localPoint))
            {
                previewContainer.anchoredPosition = previewOffset;
                return;
            }

            Vector2 anchoredPosition = localPoint + previewOffset;
            RectTransform targetRect = _previewPanelRect != null ? _previewPanelRect : previewContainer;
            Rect parentRect = _previewParentRect.rect;
            Vector2 size = targetRect.rect.size;
            Vector2 pivot = targetRect.pivot;

            float minX = parentRect.xMin + size.x * pivot.x;
            float maxX = parentRect.xMax - size.x * (1f - pivot.x);
            float minY = parentRect.yMin + size.y * pivot.y;
            float maxY = parentRect.yMax - size.y * (1f - pivot.y);

            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
            previewContainer.anchoredPosition = anchoredPosition;
        }
    }
}
