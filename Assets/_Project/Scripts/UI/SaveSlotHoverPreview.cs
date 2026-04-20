using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Hecton.UI.MainMenu;
using Hecton8.Core;
using Hecton.Localization;
using Hecton8.SaveSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Hover preview for save slots — shows enlarged thumbnail + metadata on hover.
    /// EXCEEDS SUBNAUTICA: Subnautica has no hover preview, only click-to-load.
    /// Zero-GC: ITickable state machine, cached delegates, CanvasGroup alpha.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Slot Hover Preview")]
    public sealed class SaveSlotHoverPreview : MonoBehaviour, ITickable, IPointerEnterHandler, IPointerExitHandler
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private enum State { Idle, WaitingForDelay, FadingIn, Visible, FadingOut }

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

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _slotUI = GetComponent<SaveSlotUI>();
            _slotRect = transform as RectTransform;
            _previewPanelRect = previewPanel != null ? previewPanel.transform as RectTransform : null;
            _previewParentRect = previewContainer != null
                ? previewContainer.parent as RectTransform
                : (previewPanel != null ? previewPanel.transform.parent as RectTransform : null);
            _rootCanvas = GetComponentInParent<Canvas>();
            _uiCamera = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;
            AutoWirePreviewTextReferences();
            ConfigurePreviewText(previewTitleText, 18f, 32f);
            ConfigurePreviewText(previewDetailsText, 14f, 24f);
            ConfigurePreviewText(previewStatusText, 14f, 24f);
            HideImmediate();
        }

        private void OnEnable()
        {
            TryRegister();
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            HideImmediate();
        }

        private void OnDisable()
        {
            Unregister();
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            HideImmediate();
        }

        private void OnDestroy()
        {
            Unregister();
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
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
                        previewPanel.alpha = Mathf.Lerp(_fadeStartAlpha, 0f, fadeOutT);
                    if (fadeOutT >= 1f)
                    {
                        HideImmediate();
                        _state = State.Idle;
                    }
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // POINTER EVENTS
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

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

            LocalizationManager localization = LocalizationManager.Instance;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || !saveManager.TryGetSaveSlotInfo(_currentSlotId, out SaveSlotInfo slotInfo) || slotInfo == null)
            {
                ApplyPreviewTexts(
                    BuildSlotTitle(localization, _currentSlotId),
                    ResolveLocalized(localization, LocalizationKeys.SLOT_NO_DATA, "NO DATA"),
                    string.Empty,
                    Color.white);
                return;
            }

            SaveMetadata metadata = slotInfo.Metadata;
            string timestamp = metadata != null ? metadata.GetDateTime().ToLocalTime().ToString("g") : "UNKNOWN";
            string playtime = metadata != null ? metadata.GetFormattedPlayTime() : "00:00:00";
            string scene = ResolveSceneLabel(localization, metadata != null ? metadata.SceneName : string.Empty);
            string details = string.IsNullOrEmpty(scene)
                ? string.Concat(timestamp, "\n", playtime)
                : string.Concat(timestamp, "\n", playtime, "\n", scene);
            string status = ResolveStatusLabel(localization, slotInfo.IntegrityState, slotInfo.GetStatusLabel());
            Color statusColor = ResolveStatusColor(slotInfo.IntegrityState);

            ApplyPreviewTexts(
                BuildSlotTitle(localization, _currentSlotId),
                details,
                status,
                statusColor);
        }

        private void ApplyPreviewTexts(string title, string details, string status, Color statusColor)
        {
            if (previewTitleText != null)
                previewTitleText.SetText(title ?? string.Empty);

            if (previewDetailsText != null)
                previewDetailsText.SetText(details ?? string.Empty);

            if (previewStatusText != null)
            {
                previewStatusText.SetText(status ?? string.Empty);
                previewStatusText.color = statusColor;
            }
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

            TMP_Text[] previewTexts = previewPanel.GetComponentsInChildren<TMP_Text>(true); // COLD ALLOC: TMP_Text[] - one-shot preview metadata auto-wire scan - owner: SaveSlotHoverPreview
            for (int i = 0; i < previewTexts.Length; i++)
            {
                TMP_Text candidate = previewTexts[i];
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

            for (int i = 0; i < previewTexts.Length; i++)
            {
                TMP_Text candidate = previewTexts[i];
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

        private static string BuildSlotTitle(LocalizationManager localization, string slotId)
        {
            string prefix = ResolveLocalized(localization, LocalizationKeys.SLOT_PREFIX, "SLOT");
            return string.Concat(prefix, " ", ExtractSlotNumber(slotId));
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

        private static string ExtractSlotNumber(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
                return "?";

            int underscoreIndex = slotId.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotId.Length - 1)
                return slotId.Substring(underscoreIndex + 1);

            return slotId;
        }

        private static string ResolveLocalized(LocalizationManager localization, string key, string fallback)
        {
            return localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister(this);
            }

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
