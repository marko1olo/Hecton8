// ============================================================================
// HECTON-8 — PDASpectrumTab.cs
// Вкладка PDA: SPECTRUM — управление режимами визора.
//
// ЛОР (лор2 Раздел 9):
//   SPECTRUM: Тепловизор, Сонар, Эхолот.
//   Интерфейс: векторные элементы, моноширинные шрифты, HDR-цвета.
//
// АРХИТЕКТУРА:
//   • Процедурный UI — 4 кнопки режимов + статус текущего.
//   • Слушает SpectrumEvents для обновления активной кнопки.
//   • Показывает статус сонара (последний пульс, радиус).
// ============================================================================

using Hecton8.Visor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Spectrum Tab")]
    public sealed class PDASpectrumTab : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Colors ───────────────────────────────────")]
        [SerializeField] private Color colorBg       = new Color(0.03f, 0.05f, 0.08f, 0.95f);
        [SerializeField] private Color colorAccent   = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorActive   = new Color(0.10f, 0.40f, 0.25f, 1f);
        [SerializeField] private Color colorInactive = new Color(0.08f, 0.12f, 0.10f, 1f);
        [SerializeField] private Color colorText     = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim      = new Color(0.45f, 0.50f, 0.45f, 1f);

        [Header("── Font ─────────────────────────────────────")]
        [Tooltip("Шрифт с кириллицей. Если null — используется TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _built;

        // Mode buttons
        private readonly ModeButton[] _modeButtons = new ModeButton[4];

        // Status labels
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _sonarStatusLabel;
        private TextMeshProUGUI _currentModeLabel;

        private static readonly string[] ModeNames =
        {
            "НОРМАЛЬНЫЙ",
            "ТЕПЛОВИЗОР",
            "СОНАР",
            "ЭХОЛОТ"
        };

        private static readonly string[] ModeDescriptions =
        {
            "Стандартный режим визора. Без модификаций.",
            "Тепловые сигнатуры существ и оборудования.\nОбнаружение через стены и туман.",
            $"Движение в радиусе 100м.\nНе показывает что — только что есть.\nПульс каждые 3 секунды.",
            "Биомеханические сигнатуры.\nОбнаружение дронов Атлас-6.\nТребует апгрейда сенсоров."
        };

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) gameObject.AddComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (!_built) EnsureBuilt();

            SpectrumEvents.OnModeChanged += HandleModeChanged;
            PDAEvents.OnOpened += HandlePDAOpened;

            RefreshModeDisplay();
        }

        private void OnDisable()
        {
            SpectrumEvents.OnModeChanged -= HandleModeChanged;
            PDAEvents.OnOpened -= HandlePDAOpened;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT-DRIVEN REFRESH
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void HandleModeChanged(SpectrumMode mode) => RefreshModeDisplay();
        private void HandlePDAOpened(int tab) => RefreshModeDisplay();

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            // Auto-resolve Cyrillic font — same pattern as PDADataLogTab
            if (_labelFont == null)
            {
                _labelFont = UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/текст SDF");
                if (_labelFont == null)
                    _labelFont = TMPro.TMP_Settings.defaultFontAsset;
            }

            RectTransform root = GetComponent<RectTransform>();

            Image bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = colorBg;

            // Header
            BuildHeader(root);

            // Mode buttons grid
            BuildModeButtons(root);

            // Status panel
            BuildStatusPanel(root);
        }

        private void BuildHeader(RectTransform root)
        {
            RectTransform header = CreateRect("Header", root);
            Anchor(header, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -48), new Vector2(0, 0));

            Image hBg = header.gameObject.AddComponent<Image>();
            hBg.color = new Color(0.04f, 0.08f, 0.06f, 1f);

            TextMeshProUGUI title = CreateText("Title", header, 13f, colorAccent, TextAlignmentOptions.MidlineLeft);
            title.text = "SPECTRUM — УПРАВЛЕНИЕ ВИЗОРОМ";
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 0), new Vector2(-12, 0));
        }

        private void BuildModeButtons(RectTransform root)
        {
            float btnH = 80f;
            float btnW = 0.48f;
            float gap = 0.02f;

            for (int i = 0; i < 4; i++)
            {
                int row = i / 2;
                int col = i % 2;

                float xMin = col * (btnW + gap);
                float xMax = xMin + btnW;
                float yMax = 1f - 0.05f - row * (btnH / 400f + 0.02f);
                float yMin = yMax - btnH / 400f;

                RectTransform btn = CreateRect($"ModeBtn_{i}", root);
                Anchor(btn,
                    new Vector2(xMin, 0.45f + (1 - row) * 0.25f),
                    new Vector2(xMax, 0.45f + (1 - row) * 0.25f + 0.22f),
                    new Vector2(8, 0), new Vector2(-8, 0));

                Image btnBg = btn.gameObject.AddComponent<Image>();
                btnBg.color = colorInactive;

                TextMeshProUGUI modeLabel = CreateText("ModeLabel", btn, 12f, colorText, TextAlignmentOptions.Midline);
                modeLabel.fontStyle = FontStyles.Bold;
                modeLabel.text = ModeNames[i];
                Anchor(modeLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1),
                    new Vector2(8, 0), new Vector2(-8, 0));

                TextMeshProUGUI descLabel = CreateText("Desc", btn, 8.5f, colorDim, TextAlignmentOptions.TopLeft);
                descLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
                descLabel.text = ModeDescriptions[i];
                Anchor(descLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f),
                    new Vector2(8, 4), new Vector2(-8, 0));

                int capturedIndex = i;
                ModeBtnHandler handler = btn.gameObject.AddComponent<ModeBtnHandler>();
                handler.Init(this, (SpectrumMode)capturedIndex, btnBg, colorInactive, colorActive);

                _modeButtons[i] = new ModeButton
                {
                    Background = btnBg,
                    ModeLabel = modeLabel,
                    Mode = (SpectrumMode)i
                };
            }
        }

        private void BuildStatusPanel(RectTransform root)
        {
            RectTransform panel = CreateRect("StatusPanel", root);
            Anchor(panel, new Vector2(0, 0), new Vector2(1, 0.42f),
                new Vector2(0, 0), new Vector2(0, 0));

            Image pBg = panel.gameObject.AddComponent<Image>();
            pBg.color = new Color(0.02f, 0.04f, 0.03f, 1f);

            _currentModeLabel = CreateText("CurrentMode", panel, 11f, colorAccent, TextAlignmentOptions.TopLeft);
            _currentModeLabel.fontStyle = FontStyles.Bold;
            Anchor(_currentModeLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -36), new Vector2(-12, -8));

            _statusLabel = CreateText("Status", panel, 10f, colorText, TextAlignmentOptions.TopLeft);
            _statusLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_statusLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -80), new Vector2(-12, -40));

            _sonarStatusLabel = CreateText("SonarStatus", panel, 9f, colorDim, TextAlignmentOptions.BottomLeft);
            Anchor(_sonarStatusLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 8), new Vector2(-12, 28));
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshModeDisplay()
        {
            SpectrumSystem sys = SpectrumSystem.Instance;
            SpectrumMode active = sys != null ? sys.CurrentMode : SpectrumMode.Normal;

            // Обновляем кнопки
            for (int i = 0; i < _modeButtons.Length; i++)
            {
                ModeButton mb = _modeButtons[i];
                if (mb.Background == null) continue;
                mb.Background.color = mb.Mode == active ? colorActive : colorInactive;
                if (mb.ModeLabel != null)
                    mb.ModeLabel.color = mb.Mode == active ? colorAccent : colorText;
            }

            // Обновляем статус
            int idx = (int)active;
             if (_currentModeLabel != null)
             {
                 string modeText = string.Format("АКТИВНЫЙ РЕЖИМ: {0}", ModeNames[idx]);
                 if (_currentModeLabel.text != modeText)
                 {
                     _currentModeLabel.text = modeText;
                 }
             }

            if (_statusLabel != null)
                _statusLabel.text = ModeDescriptions[idx];

            if (_sonarStatusLabel != null)
            {
                _sonarStatusLabel.text = active == SpectrumMode.Sonar
                    ? $"СОНАР АКТИВЕН — РАДИУС: {(sys != null ? "100" : "—")}М"
                    : string.Empty;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void ActivateMode(SpectrumMode mode)
        {
            SpectrumSystem sys = SpectrumSystem.Instance;
            if (sys != null)
                sys.SetMode(mode);
        }

        // ══════════════════════════════════════════════════════════
        //  NESTED TYPES
        // ══════════════════════════════════════════════════════════

        private struct ModeButton
        {
            public Image Background;
            public TextMeshProUGUI ModeLabel;
            public SpectrumMode Mode;
        }

        private sealed class ModeBtnHandler : MonoBehaviour,
            UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler
        {
            private PDASpectrumTab _tab;
            private SpectrumMode _mode;
            private Image _bg;
            private Color _normal;
            private Color _hover;

            public void Init(PDASpectrumTab tab, SpectrumMode mode, Image bg, Color normal, Color hover)
            {
                _tab = tab; _mode = mode; _bg = bg; _normal = normal;
                _hover = new Color(hover.r * 1.3f, hover.g * 1.3f, hover.b * 1.3f, hover.a);
            }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
                => _tab?.ActivateMode(_mode);

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            {
                SpectrumSystem sys = SpectrumSystem.Instance;
                bool isActive = sys != null && sys.CurrentMode == _mode;
                if (_bg != null && !isActive) _bg.color = _hover;
            }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            {
                SpectrumSystem sys = SpectrumSystem.Instance;
                bool isActive = sys != null && sys.CurrentMode == _mode;
                if (_bg != null && !isActive) _bg.color = _normal;
            }
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
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Anchor(RectTransform r, Vector2 amin, Vector2 amax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = amin;
            r.anchorMax = amax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
        }
    }
}
