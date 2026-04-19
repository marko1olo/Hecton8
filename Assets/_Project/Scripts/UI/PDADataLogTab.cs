// ============================================================================
// HECTON-8 â€” PDADataLogTab.cs
// Ð’ÐºÐ»Ð°Ð´ÐºÐ° PDA: Ð°Ñ€Ñ…Ð¸Ð² Ð°ÑƒÐ´Ð¸Ð¾Ð´Ð½ÐµÐ²Ð½Ð¸ÐºÐ¾Ð² ÐºÐ¾Ð»Ð¾Ð½Ð¸Ð¸.
//
// Ð ÐžÐ›Ð¬:
//   â€¢ ÐžÑ‚Ð¾Ð±Ñ€Ð°Ð¶Ð°ÐµÑ‚ ÑÐ¿Ð¸ÑÐ¾Ðº Ð¾Ð±Ð½Ð°Ñ€ÑƒÐ¶ÐµÐ½Ð½Ñ‹Ñ… AudioLogData.
//   â€¢ ÐŸÐ¾Ð·Ð²Ð¾Ð»ÑÐµÑ‚ Ð¿ÐµÑ€ÐµÑÐ»ÑƒÑˆÐ°Ñ‚ÑŒ Ð»ÑŽÐ±ÑƒÑŽ Ð·Ð°Ð¿Ð¸ÑÑŒ.
//   â€¢ ÐŸÐ¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÑ‚ ÑÑƒÐ±Ñ‚Ð¸Ñ‚Ñ€Ñ‹ Ñ‚ÐµÐºÑƒÑ‰ÐµÐ¹ Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ð¼Ð¾Ð¹ Ð·Ð°Ð¿Ð¸ÑÐ¸.
//   â€¢ ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¸Ð¸ PDA (Ð½Ðµ Ð² Ñ€ÐµÐ°Ð»ÑŒÐ½Ð¾Ð¼ Ð²Ñ€ÐµÐ¼ÐµÐ½Ð¸ â€” zero GC).
//
// ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð:
//   â€¢ ÐŸÑ€Ð¾Ñ†ÐµÐ´ÑƒÑ€Ð½Ñ‹Ð¹ UI (Ð±ÐµÐ· UXML/USS) â€” Ð² ÑÑ‚Ð¸Ð»Ðµ PDAInventoryTab.
//   â€¢ ITickable â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð´Ð»Ñ Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ñ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð° Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ.
//   â€¢ Ð¡Ð»ÑƒÑˆÐ°ÐµÑ‚ AudioLogEvents Ð´Ð»Ñ Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ñ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ñ.
//   â€¢ ÐšÐ°Ñ‚Ð°Ð»Ð¾Ð³ Ð»Ð¾Ð³Ð¾Ð²: Ð½Ð°Ð·Ð½Ð°Ñ‡Ð°ÐµÑ‚ÑÑ Ð² Ð¸Ð½ÑÐ¿ÐµÐºÑ‚Ð¾Ñ€Ðµ (AudioLogData[]).
//
// ZERO GC:
//   â€¢ Pre-allocated ÑÐ¿Ð¸ÑÐ¾Ðº ÑÑ‚Ñ€Ð¾Ðº Ð´Ð»Ñ Ð¾Ñ‚Ð¾Ð±Ñ€Ð°Ð¶ÐµÐ½Ð¸Ñ.
//   â€¢ Dirty-flag Ð¾Ð±Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ‚ÐµÐºÑÑ‚Ð°.
//   â€¢ ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… new/LINQ Ð² Tick.
// ============================================================================

using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Data Log Tab")]
    public sealed class PDADataLogTab : MonoBehaviour, ITickable
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Catalog â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð’ÑÐµ AudioLogData Ð² Ð¸Ð³Ñ€Ðµ. ÐÐ°Ð·Ð½Ð°Ñ‡Ð¸Ñ‚ÑŒ Ð² Ð¸Ð½ÑÐ¿ÐµÐºÑ‚Ð¾Ñ€Ðµ.")]
        [SerializeField] private AudioLogData[] allLogs = new AudioLogData[0];

        [Header("â”€â”€ Font â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¨Ñ€Ð¸Ñ„Ñ‚ Ñ ÐºÐ¸Ñ€Ð¸Ð»Ð»Ð¸Ñ†ÐµÐ¹. Ð•ÑÐ»Ð¸ null â€” Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        [Header("â”€â”€ Colors â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private Color colorBackground  = new Color(0.04f, 0.06f, 0.10f, 0.95f);
        [SerializeField] private Color colorAccent      = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorText        = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim         = new Color(0.45f, 0.50f, 0.45f, 1f);
        [SerializeField] private Color colorSelected    = new Color(0.10f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color colorPlaying     = new Color(0.05f, 0.35f, 0.20f, 1f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ðµ ÑÑ‚Ñ€Ð¾ÐºÐ¸ Ð´Ð»Ñ AudioLogCategory enum (Ð¸Ð·Ð±ÐµÐ³Ð°ÐµÑ‚ Enum.ToString() Ð´Ð°Ð¶Ðµ Ð² COLD path)</summary>

        // UI roots
        private RectTransform _root;
        private RectTransform _listPanel;
        private RectTransform _detailPanel;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _authorLabel;
        private TextMeshProUGUI _dateLabel;
        private TextMeshProUGUI _summaryLabel;
        private TextMeshProUGUI _subtitleLabel;
        private TextMeshProUGUI _playbackTimerLabel;
        private Image _playButtonBg;
        private TextMeshProUGUI _playButtonLabel;
        private TextMeshProUGUI _countLabel;
        private TextMeshProUGUI _emptyStateLabel;
        private TextMeshProUGUI _headerTitleLabel;

        // List rows â€” pre-allocated
        private readonly List<LogRow> _rows = new List<LogRow>(32);
        private readonly string[] _localizedCategoryLabels = new string[5]; // COLD ALLOC: string[5] — localized category labels — owner: PDADataLogTab

        // State
        private int _selectedIndex = -1;
        private bool _built;
        private bool _registered;
        private bool _dirty;
        private bool _detailVisible = true;

        // Playback timer display
        private float _playbackRemaining;
        private int _prevTimerSeconds = -1;
        private string _prevSubtitleText;

        private const float TICK_DT = 1f / 60f;
        private const string PlayAudioLabel = "PLAY AUDIO";
        private const string OpenTextLogLabel = "OPEN LOG";
        private const string StopAudioLabel = "STOP";
        private const string CloseTextLogLabel = "CLOSE LOG";
        private const string LockedLogLabel = "DISCOVERY REQUIRED";
        private const string NoPayloadLabel = "NO PLAYBACK";
        private const string TextOnlySummaryPrefix = "TEXT LOG\n";
        private const string ArchiveOnlySummaryPrefix = "ARCHIVE FRAGMENT\n";

        private string _localizedArchiveTitle = "DATA ARCHIVE - HECTON-8 COLONY";
        private string _localizedCountFormat = "{0}/{1} LOGS";
        private string _localizedCategoryUnknown = "UNKNOWN";
        private string _localizedEncryptedLabel = "??? ENCRYPTED ???";
        private string _localizedEncryptedSummary = "Entry encrypted. Discovery required before archive access.";
        private string _localizedUnknownAuthor = "UNKNOWN";
        private string _localizedUnknownDate = "DATE UNKNOWN";
        private string _localizedAuthorPrefix = "AUTHOR: ";
        private string _localizedDatePrefix = "DATE: ";
        private string _localizedPlayAudioLabel = PlayAudioLabel;
        private string _localizedOpenTextLabel = OpenTextLogLabel;
        private string _localizedStopAudioLabel = StopAudioLabel;
        private string _localizedCloseTextLabel = CloseTextLogLabel;
        private string _localizedLockedLabel = LockedLogLabel;
        private string _localizedNoPayloadLabel = NoPayloadLabel;
        private string _localizedTextOnlySummaryPrefix = TextOnlySummaryPrefix;
        private string _localizedArchiveOnlySummaryPrefix = ArchiveOnlySummaryPrefix;
        private string _localizedEmptyStateText = "ARCHIVE EMPTY\nAssign AudioLogData assets in allLogs.";

        private int CatalogCount => allLogs != null ? allLogs.Length : 0;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  NESTED TYPE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private struct LogRow
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI IndexLabel;
            public TextMeshProUGUI TitleLabel;
            public TextMeshProUGUI CategoryLabel;
            public LogRowButton Button;
            public int LogIndex; // index into allLogs
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();
        }

        private void OnEnable()
        {
            RebuildLocalizationCache();
            if (!_built) EnsureBuilt();
            ApplyLocalizedStaticText();

            TryRegister();

            AudioLogEvents.OnLogDiscovered       += HandleLogDiscovered;
            AudioLogEvents.OnLogPlaybackStarted  += HandlePlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped  += HandlePlaybackStopped;
            AudioLogEvents.OnLogPlaybackCompleted += HandlePlaybackCompleted;
            PDAEvents.OnOpened += HandlePDAOpened;
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;

            _dirty = true;
        }

        private void OnDisable()
        {
            TryUnregister();

            AudioLogEvents.OnLogDiscovered       -= HandleLogDiscovered;
            AudioLogEvents.OnLogPlaybackStarted  -= HandlePlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped  -= HandlePlaybackStopped;
            AudioLogEvents.OnLogPlaybackCompleted -= HandlePlaybackCompleted;
            PDAEvents.OnOpened -= HandlePDAOpened;
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (_dirty)
            {
                RefreshList();
                _dirty = false;
            }

            // ÐžÐ±Ð½Ð¾Ð²Ð»ÑÐµÐ¼ Ñ‚Ð°Ð¹Ð¼ÐµÑ€ Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ
            if (_playbackRemaining > 0f)
            {
                _playbackRemaining -= deltaTime;
                if (_playbackRemaining < 0f) _playbackRemaining = 0f;
                UpdatePlaybackTimer();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ Ð·Ð°Ð¿Ð¸ÑÑŒ Ð¿Ð¾ Ð¸Ð½Ð´ÐµÐºÑÑƒ Ð² allLogs.</summary>
        public void SelectLog(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount) return;

            _selectedIndex = logIndex;
            RefreshDetail();
            RefreshRowHighlights();
        }

        /// <summary>Ð’Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÑÑ‚Ð¸ Ð²Ñ‹Ð±Ñ€Ð°Ð½Ð½ÑƒÑŽ Ð·Ð°Ð¿Ð¸ÑÑŒ.</summary>
        public void PlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount) return;

            AudioLogSystem system = AudioLogSystem.Instance;
            if (system == null) return;

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) return;

            if (!system.IsDiscovered(log.logId)) return;
            if (!log.HasPlaybackPayload) return;

            system.PlayLog(log);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENT HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void HandleLogDiscovered(string logId) => _dirty = true;

        private void HandlePDAOpened(int tab) => _dirty = true;

        private void HandlePlaybackStarted(AudioLogData data)
        {
            _playbackRemaining = data != null ? data.Duration : 0f;
            if (_subtitleLabel != null && data != null)
            {
                _subtitleLabel.text = data.SubtitleOrFallback;
                _prevSubtitleText = data.SubtitleOrFallback;
            }

            RefreshPlayButton();
        }

        private void HandlePlaybackStopped(string logId)
        {
            _playbackRemaining = 0f;
            ResetPlaybackTimerDisplay();
            RefreshPlayButton();
            if (_subtitleLabel != null)
            {
                _subtitleLabel.text = string.Empty;
                _prevSubtitleText = string.Empty;
            }
        }

        private void HandlePlaybackCompleted(string logId)
        {
            _playbackRemaining = 0f;
            ResetPlaybackTimerDisplay();
            RefreshPlayButton();
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registered = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BUILD UI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_labelFont == null)
                _labelFont = TMPro.TMP_Settings.defaultFontAsset;

            // Background
            Image bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = colorBackground;

            // Header
            BuildHeader();

            // Split: list left, detail right
            BuildListPanel();
            BuildDetailPanel();
        }

        private void BuildHeader()
        {
            RectTransform header = CreateRect("Header", _root);
            Anchor(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -48), new Vector2(0, 0));

            Image hBg = header.gameObject.AddComponent<Image>();
            hBg.color = new Color(0.06f, 0.10f, 0.08f, 1f);

            _countLabel = CreateText("CountLabel", header, 11f, colorDim, TextAlignmentOptions.MidlineRight);
            Anchor(_countLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(-8, 0), new Vector2(-12, 0));

            _headerTitleLabel = CreateText("Title", header, 13f, colorAccent, TextAlignmentOptions.MidlineLeft);
            _headerTitleLabel.text = _localizedArchiveTitle;
            _headerTitleLabel.fontStyle = FontStyles.Bold;
            Anchor(_headerTitleLabel.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
        }

        private void BuildListPanel()
        {
            _listPanel = CreateRect("ListPanel", _root);
            Anchor(_listPanel, new Vector2(0, 0), new Vector2(0.42f, 1),
                new Vector2(0, 0), new Vector2(0, -48));

            Image lBg = _listPanel.gameObject.AddComponent<Image>();
            lBg.color = new Color(0.03f, 0.05f, 0.04f, 1f);

            // Scroll view would be ideal but for minimal impl â€“ static rows
            // COLD ALLOC: up to 32 rows
            BuildLogRows();

            if (_rows.Count == 0)
                BuildEmptyState();
        }

        private void BuildLogRows()
        {
            _rows.Clear();
            float rowH = 44f;
            float y = 0f;
            int logCount = CatalogCount;

            for (int i = 0; i < logCount && i < 32; i++)
            {
                AudioLogData log = GetLog(i);
                if (log == null) continue;

                RectTransform rowRoot = CreateRect($"Row_{i}", _listPanel);
                Anchor(rowRoot, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -y - rowH), new Vector2(0, -y));

                Image rowBg = rowRoot.gameObject.AddComponent<Image>();
                rowBg.color = new Color(0.04f, 0.07f, 0.05f, 0f);

                TextMeshProUGUI idxLabel = CreateText("Idx", rowRoot, 9f, colorDim, TextAlignmentOptions.MidlineLeft);
                Anchor(idxLabel.rectTransform, new Vector2(0, 0), new Vector2(0.08f, 1),
                    new Vector2(6, 0), new Vector2(0, 0));
                idxLabel.text = $"{i + 1:D2}";

                TextMeshProUGUI titleLabel = CreateText("Title", rowRoot, 10f, colorText, TextAlignmentOptions.MidlineLeft);
                Anchor(titleLabel.rectTransform, new Vector2(0.08f, 0), new Vector2(0.75f, 1),
                    new Vector2(4, 0), new Vector2(0, 0));
                titleLabel.text = log.DisplayTitleOrFallback;

                TextMeshProUGUI catLabel = CreateText("Cat", rowRoot, 8f, colorDim, TextAlignmentOptions.MidlineRight);
                Anchor(catLabel.rectTransform, new Vector2(0.75f, 0), new Vector2(1, 1),
                    new Vector2(0, 0), new Vector2(-6, 0));
                catLabel.text = GetCachedCategoryLabel(log.category);

                // Button component
                LogRowButton btn = rowRoot.gameObject.AddComponent<LogRowButton>();
                int capturedIndex = i;
                btn.Init(this, capturedIndex, rowBg, colorDim, colorSelected);

                _rows.Add(new LogRow
                {
                    Root = rowRoot,
                    Background = rowBg,
                    IndexLabel = idxLabel,
                    TitleLabel = titleLabel,
                    CategoryLabel = catLabel,
                    Button = btn,
                    LogIndex = i
                });

                y += rowH;
            }
        }

        private void BuildEmptyState()
        {
            RectTransform emptyState = CreateRect("EmptyState", _listPanel);
            Anchor(emptyState, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(16, 16), new Vector2(-16, -16));

            Image emptyBg = emptyState.gameObject.AddComponent<Image>();
            emptyBg.color = new Color(0.05f, 0.07f, 0.06f, 0.75f);

            _emptyStateLabel = CreateText("EmptyStateLabel", emptyState, 10f, colorDim, TextAlignmentOptions.Center);
            _emptyStateLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _emptyStateLabel.text = _localizedEmptyStateText;
            Stretch(_emptyStateLabel.rectTransform, 16, 16, 16, 16);
        }

        private void BuildDetailPanel()
        {
            _detailPanel = CreateRect("DetailPanel", _root);
            Anchor(_detailPanel, new Vector2(0.42f, 0), new Vector2(1, 1),
                new Vector2(1, 0), new Vector2(0, -48));

            Image dBg = _detailPanel.gameObject.AddComponent<Image>();
            dBg.color = new Color(0.03f, 0.06f, 0.05f, 1f);

            // Title
            _titleLabel = CreateText("Title", _detailPanel, 14f, colorAccent, TextAlignmentOptions.TopLeft);
            _titleLabel.fontStyle = FontStyles.Bold;
            Anchor(_titleLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -52), new Vector2(-12, -12));

            // Author
            _authorLabel = CreateText("Author", _detailPanel, 10f, colorDim, TextAlignmentOptions.TopLeft);
            Anchor(_authorLabel.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1),
                new Vector2(12, -90), new Vector2(0, -56));

            // Date
            _dateLabel = CreateText("Date", _detailPanel, 10f, colorDim, TextAlignmentOptions.TopRight);
            Anchor(_dateLabel.rectTransform, new Vector2(0.6f, 1), new Vector2(1, 1),
                new Vector2(0, -90), new Vector2(-12, -56));

            // Summary
            _summaryLabel = CreateText("Summary", _detailPanel, 10f, colorText, TextAlignmentOptions.TopLeft);
            _summaryLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_summaryLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -200), new Vector2(-12, -96));

            // Subtitle (playback)
            _subtitleLabel = CreateText("Subtitle", _detailPanel, 9f, colorAccent, TextAlignmentOptions.TopLeft);
            _subtitleLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_subtitleLabel.rectTransform, new Vector2(0, 0.3f), new Vector2(1, 0.7f),
                new Vector2(12, 0), new Vector2(-12, 0));

            // Playback timer
            _playbackTimerLabel = CreateText("Timer", _detailPanel, 9f, colorDim, TextAlignmentOptions.BottomRight);
            Anchor(_playbackTimerLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(0, 8), new Vector2(-12, 32));

            // Play button
            RectTransform playBtn = CreateRect("PlayButton", _detailPanel);
            Anchor(playBtn, new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(12, 8), new Vector2(-6, 44));

            _playButtonBg = playBtn.gameObject.AddComponent<Image>();
            _playButtonBg.color = colorAccent;

            _playButtonLabel = CreateText("PlayLabel", playBtn, 11f, colorBackground, TextAlignmentOptions.Midline);
            _playButtonLabel.fontStyle = FontStyles.Bold;
            _playButtonLabel.text = _localizedPlayAudioLabel;
            Stretch(_playButtonLabel.rectTransform);

            PlayButtonHandler pbh = playBtn.gameObject.AddComponent<PlayButtonHandler>();
            pbh.Init(this, _playButtonBg, colorAccent, new Color(colorAccent.r * 0.7f, colorAccent.g * 0.7f, colorAccent.b * 0.7f));

            // Initial state
            SetDetailVisible(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REFRESH
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void RefreshList()
        {
            AudioLogSystem system = AudioLogSystem.Instance;
            int discovered = system != null ? system.DiscoveredCount : 0;
            int logCount = CatalogCount;

            if (_countLabel != null)
                _countLabel.SetText(_localizedCountFormat, discovered, logCount);

            if (_emptyStateLabel != null)
            {
                bool shouldShowEmptyState = logCount == 0;
                if (_emptyStateLabel.gameObject.activeSelf != shouldShowEmptyState)
                    _emptyStateLabel.gameObject.SetActive(shouldShowEmptyState);
                _emptyStateLabel.text = _localizedEmptyStateText;
            }

            if (logCount == 0)
            {
                SetDetailVisible(false);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                LogRow row = _rows[i];
                AudioLogData log = GetLog(row.LogIndex);
                bool isDiscovered = system != null && log != null && system.IsDiscovered(log.logId);

                // Dim undiscovered entries
                Color textColor = isDiscovered ? colorText : colorDim;
                if (row.TitleLabel != null) row.TitleLabel.color = textColor;
                if (row.IndexLabel != null) row.IndexLabel.color = colorDim;
                if (row.CategoryLabel != null)
                    row.CategoryLabel.text = log != null
                        ? GetCachedCategoryLabel(log.category)
                        : _localizedCategoryUnknown;
                if (row.CategoryLabel != null) row.CategoryLabel.color = isDiscovered ? colorDim : new Color(colorDim.r, colorDim.g, colorDim.b, 0.3f);

                // Replace title with ??? for undiscovered
                if (row.TitleLabel != null)
                    row.TitleLabel.text = isDiscovered ? log.DisplayTitleOrFallback : _localizedEncryptedLabel;
            }

            RefreshRowHighlights();
        }

        private void RefreshDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount)
            {
                SetDetailVisible(false);
                return;
            }

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) { SetDetailVisible(false); return; }

            AudioLogSystem system = AudioLogSystem.Instance;
            bool isDiscovered = system != null && system.IsDiscovered(log.logId);

            SetDetailVisible(true);

            if (_titleLabel != null)
                _titleLabel.text = isDiscovered ? log.DisplayTitleOrFallback.ToUpperInvariant() : _localizedEncryptedLabel;

            if (_authorLabel != null)
                _authorLabel.text = isDiscovered
                    ? string.Concat(_localizedAuthorPrefix, log.AuthorOrFallback)
                    : string.Concat(_localizedAuthorPrefix, _localizedUnknownAuthor);

            if (_dateLabel != null)
                _dateLabel.text = isDiscovered
                    ? log.RecordDateOrFallback
                    : string.Concat(_localizedDatePrefix, _localizedUnknownDate);

            if (_summaryLabel != null)
                _summaryLabel.text = isDiscovered
                    ? GetCachedSummaryText(log)
                    : _localizedEncryptedSummary;

            RefreshPlayButton();
        }

        private void RefreshRowHighlights()
        {
            AudioLogSystem system = AudioLogSystem.Instance;
            string playingId = system != null && system.IsPlaying && system.CurrentLog != null
                ? system.CurrentLog.logId
                : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                LogRow row = _rows[i];
                AudioLogData log = GetLog(row.LogIndex);
                bool isSelected = row.LogIndex == _selectedIndex;
                bool isPlaying = log != null && log.logId == playingId;

                if (row.Background != null)
                {
                    if (isPlaying)
                        row.Background.color = colorPlaying;
                    else if (isSelected)
                        row.Background.color = colorSelected;
                    else
                        row.Background.color = new Color(0, 0, 0, 0);
                }
            }
        }

        private void RefreshPlayButton()
        {
            AudioLogSystem system = AudioLogSystem.Instance;
            AudioLogData selectedLog = GetSelectedLog();
            bool isDiscovered = system != null && selectedLog != null && system.IsDiscovered(selectedLog.logId);
            bool isPlaying = system != null && system.IsPlaying;
            bool canStartPlayback = isDiscovered && selectedLog != null && selectedLog.HasPlaybackPayload;
            bool buttonEnabled = isPlaying || canStartPlayback;

            if (_playButtonBg != null)
            {
                _playButtonBg.color = buttonEnabled ? colorAccent : colorDim;
                _playButtonBg.raycastTarget = buttonEnabled;
            }

            if (_playButtonLabel != null)
                _playButtonLabel.text = GetCachedPlayButtonLabel(system, selectedLog, isDiscovered);

            RefreshRowHighlights();
        }

        private void UpdatePlaybackTimer()
        {
            if (_playbackTimerLabel == null) return;

            int secs = Mathf.CeilToInt(_playbackRemaining);
            if (secs == _prevTimerSeconds)
                return;

            _prevTimerSeconds = secs;
            int m = secs / 60;
            int s = secs % 60;
            _playbackTimerLabel.SetText("{0:00}:{1:00}", m, s);
        }

        private void ResetPlaybackTimerDisplay()
        {
            _prevTimerSeconds = -1;
            if (_playbackTimerLabel != null)
                _playbackTimerLabel.SetText("{0:00}:{1:00}", 0, 0);
        }

        private void SetDetailVisible(bool visible)
        {
            if (_detailVisible == visible)
                return;

            _detailVisible = visible;
            if (_titleLabel != null)   _titleLabel.gameObject.SetActive(visible);
            if (_authorLabel != null)  _authorLabel.gameObject.SetActive(visible);
            if (_dateLabel != null)    _dateLabel.gameObject.SetActive(visible);
            if (_summaryLabel != null) _summaryLabel.gameObject.SetActive(visible);
            if (_subtitleLabel != null) _subtitleLabel.gameObject.SetActive(visible);
            if (_playbackTimerLabel != null) _playbackTimerLabel.gameObject.SetActive(visible);
            if (_playButtonBg != null) _playButtonBg.gameObject.SetActive(visible);
            if (_playButtonLabel != null) _playButtonLabel.gameObject.SetActive(visible);
        }

        private AudioLogData GetLog(int index)
        {
            if (allLogs == null || index < 0 || index >= allLogs.Length)
                return null;

            return allLogs[index];
        }

        private AudioLogData GetSelectedLog()
        {
            return GetLog(_selectedIndex);
        }

        internal bool TryGetLogById(string logId, out AudioLogData log)
        {
            if (allLogs != null && !string.IsNullOrEmpty(logId))
            {
                for (int i = 0; i < allLogs.Length; i++)
                {
                    AudioLogData candidate = allLogs[i];
                    if (candidate != null &&
                        string.Equals(candidate.logId, logId, System.StringComparison.Ordinal))
                    {
                        log = candidate;
                        return true;
                    }
                }
            }

            log = null;
            return false;
        }

        private static string GetSummaryText(AudioLogData log)
        {
            if (log == null)
                return string.Empty;

            if (log.IsTextOnlyPlayback)
                return TextOnlySummaryPrefix + log.ArchiveSummaryOrFallback;

            if (!log.HasPlaybackPayload && log.HasArchiveSummary)
                return ArchiveOnlySummaryPrefix + log.ArchiveSummaryOrFallback;

            return log.ArchiveSummaryOrFallback;
        }

        private static string ResolvePlayButtonLabel(AudioLogSystem system, AudioLogData selectedLog, bool isDiscovered)
        {
            if (system != null && system.IsPlaying)
            {
                AudioLogData playingLog = system.CurrentLog;
                return playingLog != null && playingLog.IsTextOnlyPlayback
                    ? CloseTextLogLabel
                    : StopAudioLabel;
            }

            if (!isDiscovered)
                return LockedLogLabel;

            if (selectedLog == null || !selectedLog.HasPlaybackPayload)
                return NoPayloadLabel;

            return selectedLog.HasAudioClip ? PlayAudioLabel : OpenTextLogLabel;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  UI HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizationCache();
            ApplyLocalizedStaticText();
            _dirty = true;
            RefreshList();
            RefreshDetail();
            RefreshPlayButton();
        }

        private void RebuildLocalizationCache()
        {
            _localizedArchiveTitle = ResolveLocalized(LocalizationKeys.AUDIOLOG_ARCHIVE_TITLE, "DATA ARCHIVE - HECTON-8 COLONY");
            _localizedCountFormat = ResolveLocalized(LocalizationKeys.AUDIOLOG_COUNT, "{0}/{1} LOGS");
            _localizedCategoryLabels[(int)AudioLogCategory.Personal] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_PERSONAL, "PERSONAL");
            _localizedCategoryLabels[(int)AudioLogCategory.Technical] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_TECHNICAL, "TECHNICAL");
            _localizedCategoryLabels[(int)AudioLogCategory.Emergency] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_EMERGENCY, "EMERGENCY");
            _localizedCategoryLabels[(int)AudioLogCategory.Atlas6] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_ATLAS6, "ATLAS6");
            _localizedCategoryLabels[(int)AudioLogCategory.Unknown] = ResolveLocalized(LocalizationKeys.AUDIOLOG_CATEGORY_UNKNOWN, "UNKNOWN");
            _localizedCategoryUnknown = _localizedCategoryLabels[(int)AudioLogCategory.Unknown];
            _localizedEncryptedLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_ENCRYPTED, "??? ENCRYPTED ???");
            _localizedEncryptedSummary = ResolveLocalized(LocalizationKeys.AUDIOLOG_ENCRYPTED_SUMMARY, "Entry encrypted. Discovery required before archive access.");
            _localizedUnknownAuthor = ResolveLocalized(LocalizationKeys.AUDIOLOG_UNKNOWN_AUTHOR, "UNKNOWN");
            _localizedUnknownDate = ResolveLocalized(LocalizationKeys.AUDIOLOG_UNKNOWN_DATE, "DATE UNKNOWN");
            _localizedAuthorPrefix = string.Concat(ResolveLocalized(LocalizationKeys.INTERACT_AUTHOR, "AUTHOR"), ": ");
            _localizedDatePrefix = string.Concat(ResolveLocalized(LocalizationKeys.INTERACT_DATE, "DATE"), ": ");
            _localizedPlayAudioLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_PLAY, PlayAudioLabel);
            _localizedOpenTextLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_OPEN_TEXT, OpenTextLogLabel);
            _localizedStopAudioLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_STOP, StopAudioLabel);
            _localizedCloseTextLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_CLOSE_TEXT, CloseTextLogLabel);
            _localizedLockedLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_LOCKED, LockedLogLabel);
            _localizedNoPayloadLabel = ResolveLocalized(LocalizationKeys.AUDIOLOG_NO_PAYLOAD, NoPayloadLabel);
            _localizedTextOnlySummaryPrefix = ResolveLocalized(LocalizationKeys.AUDIOLOG_TEXT_ONLY_PREFIX, TextOnlySummaryPrefix);
            _localizedArchiveOnlySummaryPrefix = ResolveLocalized(LocalizationKeys.AUDIOLOG_ARCHIVE_ONLY_PREFIX, ArchiveOnlySummaryPrefix);
            _localizedEmptyStateText = string.Concat(
                ResolveLocalized(LocalizationKeys.AUDIOLOG_EMPTY_ARCHIVE, "ARCHIVE EMPTY"),
                "\n",
                ResolveLocalized(LocalizationKeys.AUDIOLOG_EMPTY_ARCHIVE_HINT, "Assign AudioLogData assets in allLogs."));
        }

        private void ApplyLocalizedStaticText()
        {
            if (_headerTitleLabel != null)
                _headerTitleLabel.text = _localizedArchiveTitle;

            if (_emptyStateLabel != null)
                _emptyStateLabel.text = _localizedEmptyStateText;
        }

        private string GetCachedSummaryText(AudioLogData log)
        {
            if (log == null)
                return string.Empty;

            if (log.IsTextOnlyPlayback)
                return string.Concat(_localizedTextOnlySummaryPrefix, log.ArchiveSummaryOrFallback);

            if (!log.HasPlaybackPayload && log.HasArchiveSummary)
                return string.Concat(_localizedArchiveOnlySummaryPrefix, log.ArchiveSummaryOrFallback);

            return log.ArchiveSummaryOrFallback;
        }

        private string GetCachedPlayButtonLabel(AudioLogSystem system, AudioLogData selectedLog, bool isDiscovered)
        {
            if (system != null && system.IsPlaying)
            {
                AudioLogData playingLog = system.CurrentLog;
                return playingLog != null && playingLog.IsTextOnlyPlayback
                    ? _localizedCloseTextLabel
                    : _localizedStopAudioLabel;
            }

            if (!isDiscovered)
                return _localizedLockedLabel;

            if (selectedLog == null || !selectedLog.HasPlaybackPayload)
                return _localizedNoPayloadLabel;

            return selectedLog.HasAudioClip
                ? _localizedPlayAudioLabel
                : _localizedOpenTextLabel;
        }

        private string GetCachedCategoryLabel(AudioLogCategory category)
        {
            int categoryIndex = (int)category;
            if ((uint)categoryIndex < (uint)_localizedCategoryLabels.Length)
                return _localizedCategoryLabels[categoryIndex];

            return _localizedCategoryUnknown;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size,
            Color color, TextAlignmentOptions alignment)
        {
            RectTransform rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_labelFont != null) tmp.font = _labelFont;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.overflowMode = TextOverflowModes.Truncate;
            LocalizedTMPAutoSizer.Configure(tmp, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return tmp;
        }

        private static void Stretch(RectTransform r, float l = 0, float r2 = 0, float b = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(-r2, -t);
        }

        private static void Anchor(RectTransform r, Vector2 amin, Vector2 amax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  NESTED BUTTON HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private sealed class LogRowButton : MonoBehaviour,
            UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            private PDADataLogTab _tab;
            private int _index;
            private Image _bg;
            private Color _normal;
            private Color _hover;

            public void Init(PDADataLogTab tab, int index, Image bg, Color normal, Color hover)
            {
                _tab = tab; _index = index; _bg = bg; _normal = normal; _hover = hover;
            }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
                => _tab?.SelectLog(_index);

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _hover; }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _normal; }
        }

        private sealed class PlayButtonHandler : MonoBehaviour,
            UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            private PDADataLogTab _tab;
            private Image _bg;
            private Color _normal;
            private Color _hover;

            public void Init(PDADataLogTab tab, Image bg, Color normal, Color hover)
            {
                _tab = tab; _bg = bg; _normal = normal; _hover = hover;
            }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
            {
                AudioLogSystem sys = AudioLogSystem.Instance;
                if (sys != null && sys.IsPlaying)
                    sys.StopPlayback();
                else
                    _tab?.PlaySelected();
            }

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _hover; }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            { if (_bg != null) _bg.color = _normal; }
        }
    }
}

