// ============================================================================
// HECTON-8 — PDADataLogTab.cs
// Вкладка PDA: архив аудиодневников колонии.
//
// РОЛЬ:
//   • Отображает список обнаруженных AudioLogData.
//   • Позволяет переслушать любую запись.
//   • Показывает субтитры текущей воспроизводимой записи.
//   • Обновляется при открытии PDA (не в реальном времени — zero GC).
//
// АРХИТЕКТУРА:
//   • Процедурный UI (без UXML/USS) — в стиле PDAInventoryTab.
//   • ITickable — только для обновления таймера воспроизведения.
//   • Слушает AudioLogEvents для обновления состояния.
//   • Каталог логов: назначается в инспекторе (AudioLogData[]).
//
// ZERO GC:
//   • Pre-allocated список строк для отображения.
//   • Dirty-flag обновление текста.
//   • Никаких new/LINQ в Tick.
// ============================================================================

using System.Collections.Generic;
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
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Catalog ──────────────────────────────────")]
        [Tooltip("Все AudioLogData в игре. Назначить в инспекторе.")]
        [SerializeField] private AudioLogData[] allLogs = new AudioLogData[0];

        [Header("── Font ─────────────────────────────────────")]
        [Tooltip("Шрифт с кириллицей. Если null — используется TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        [Header("── Colors ───────────────────────────────────")]
        [SerializeField] private Color colorBackground  = new Color(0.04f, 0.06f, 0.10f, 0.95f);
        [SerializeField] private Color colorAccent      = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorText        = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim         = new Color(0.45f, 0.50f, 0.45f, 1f);
        [SerializeField] private Color colorSelected    = new Color(0.10f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color colorPlaying     = new Color(0.05f, 0.35f, 0.20f, 1f);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированные строки для AudioLogCategory enum (избегает Enum.ToString() даже в COLD path)</summary>
        private static readonly string[] _cachedCategoryStrings = new string[]
        {
            "PERSONAL",   // AudioLogCategory.Personal = 0
            "TECHNICAL",  // AudioLogCategory.Technical = 1
            "EMERGENCY",  // AudioLogCategory.Emergency = 2
            "ATLAS6",     // AudioLogCategory.Atlas6 = 3
            "UNKNOWN"     // AudioLogCategory.Unknown = 4
        };

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

        // List rows — pre-allocated
        private readonly List<LogRow> _rows = new List<LogRow>(32);

        // State
        private int _selectedIndex = -1;
        private bool _built;
        private bool _registered;
        private bool _dirty;

        // Playback timer display
        private float _playbackRemaining;
        private int _prevTimerSeconds = -1;
        private string _prevSubtitleText;

        private const float TICK_DT = 1f / 60f;

        private int CatalogCount => allLogs != null ? allLogs.Length : 0;

        // ══════════════════════════════════════════════════════════
        //  NESTED TYPE
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (!_built) EnsureBuilt();

            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            AudioLogEvents.OnLogDiscovered       += HandleLogDiscovered;
            AudioLogEvents.OnLogPlaybackStarted  += HandlePlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped  += HandlePlaybackStopped;
            AudioLogEvents.OnLogPlaybackCompleted += HandlePlaybackCompleted;
            PDAEvents.OnOpened += HandlePDAOpened;

            _dirty = true;
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            AudioLogEvents.OnLogDiscovered       -= HandleLogDiscovered;
            AudioLogEvents.OnLogPlaybackStarted  -= HandlePlaybackStarted;
            AudioLogEvents.OnLogPlaybackStopped  -= HandlePlaybackStopped;
            AudioLogEvents.OnLogPlaybackCompleted -= HandlePlaybackCompleted;
            PDAEvents.OnOpened -= HandlePDAOpened;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_dirty)
            {
                RefreshList();
                _dirty = false;
            }

            // Обновляем таймер воспроизведения
            if (_playbackRemaining > 0f)
            {
                _playbackRemaining -= deltaTime;
                if (_playbackRemaining < 0f) _playbackRemaining = 0f;
                UpdatePlaybackTimer();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Выбрать запись по индексу в allLogs.</summary>
        public void SelectLog(int logIndex)
        {
            if (logIndex < 0 || logIndex >= CatalogCount) return;

            _selectedIndex = logIndex;
            RefreshDetail();
            RefreshRowHighlights();
        }

        /// <summary>Воспроизвести выбранную запись.</summary>
        public void PlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= CatalogCount) return;

            AudioLogSystem system = AudioLogSystem.Instance;
            if (system == null) return;

            AudioLogData log = GetLog(_selectedIndex);
            if (log == null) return;

            system.PlayLog(log);
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

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
            RefreshPlayButton(true);
        }

        private void HandlePlaybackStopped(string logId)
        {
            _playbackRemaining = 0f;
            ResetPlaybackTimerDisplay();
            RefreshPlayButton(false);
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
            RefreshPlayButton(false);
        }

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            // Auto-resolve font with Cyrillic support
            if (_labelFont == null)
            {
                // Try to load текст SDF which has Cyrillic
                _labelFont = UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/текст SDF");
                if (_labelFont == null)
                    _labelFont = TMPro.TMP_Settings.defaultFontAsset;
            }

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

            TextMeshProUGUI title = CreateText("Title", header, 13f, colorAccent, TextAlignmentOptions.MidlineLeft);
            title.text = "АРХИВ ДАННЫХ — КОЛОНИЯ ГЕКТОН-8";
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
        }

        private void BuildListPanel()
        {
            _listPanel = CreateRect("ListPanel", _root);
            Anchor(_listPanel, new Vector2(0, 0), new Vector2(0.42f, 1),
                new Vector2(0, 0), new Vector2(0, -48));

            Image lBg = _listPanel.gameObject.AddComponent<Image>();
            lBg.color = new Color(0.03f, 0.05f, 0.04f, 1f);

            // Scroll view would be ideal but for minimal impl – static rows
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
                int categoryIndex = (int)log.category;
                catLabel.text = categoryIndex >= 0 && categoryIndex < _cachedCategoryStrings.Length
                    ? _cachedCategoryStrings[categoryIndex]
                    : "UNKNOWN";

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
            _emptyStateLabel.text = "АРХИВ ПУСТ\nНазначь AudioLogData в allLogs.";
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
            _playButtonLabel.text = "▶  ВОСПРОИЗВЕСТИ";
            Stretch(_playButtonLabel.rectTransform);

            PlayButtonHandler pbh = playBtn.gameObject.AddComponent<PlayButtonHandler>();
            pbh.Init(this, _playButtonBg, colorAccent, new Color(colorAccent.r * 0.7f, colorAccent.g * 0.7f, colorAccent.b * 0.7f));

            // Initial state
            SetDetailVisible(false);
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshList()
        {
            AudioLogSystem system = AudioLogSystem.Instance;
            int discovered = system != null ? system.DiscoveredCount : 0;
            int logCount = CatalogCount;

            if (_countLabel != null)
                _countLabel.text = $"{discovered}/{logCount} ЗАПИСЕЙ";

            if (_emptyStateLabel != null)
                _emptyStateLabel.gameObject.SetActive(logCount == 0);

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
                if (row.CategoryLabel != null) row.CategoryLabel.color = isDiscovered ? colorDim : new Color(colorDim.r, colorDim.g, colorDim.b, 0.3f);

                // Replace title with ??? for undiscovered
                if (row.TitleLabel != null)
                    row.TitleLabel.text = isDiscovered ? log.DisplayTitleOrFallback : "??? ЗАШИФРОВАНО ???";
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
                _titleLabel.text = isDiscovered ? log.DisplayTitleOrFallback.ToUpperInvariant() : "??? ЗАШИФРОВАНО ???";

            if (_authorLabel != null)
                _authorLabel.text = isDiscovered ? $"АВТОР: {log.AuthorOrFallback}" : "АВТОР: НЕИЗВЕСТЕН";

            if (_dateLabel != null)
                _dateLabel.text = isDiscovered ? log.RecordDateOrFallback : "ДАТА: НЕИЗВЕСТНА";

            if (_summaryLabel != null)
                _summaryLabel.text = isDiscovered ? log.ArchiveSummaryOrFallback : "Запись зашифрована. Требуется взаимодействие с источником.";

            // Play button — только для обнаруженных
            if (_playButtonBg != null)
                _playButtonBg.color = isDiscovered ? colorAccent : colorDim;
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

        private void RefreshPlayButton(bool isPlaying)
        {
            if (_playButtonLabel != null)
                _playButtonLabel.text = isPlaying ? "■  СТОП" : "▶  ВОСПРОИЗВЕСТИ";

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

        // ══════════════════════════════════════════════════════════
        //  UI HELPERS
        // ══════════════════════════════════════════════════════════

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
            tmp.overflowMode = TextOverflowModes.Ellipsis;
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

        // ══════════════════════════════════════════════════════════
        //  NESTED BUTTON HANDLERS
        // ══════════════════════════════════════════════════════════

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
