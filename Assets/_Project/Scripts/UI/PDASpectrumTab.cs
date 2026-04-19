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
using Hecton8.Gameplay;
using Hecton8.World;
using System.Text;
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
        private TextMeshProUGUI _contactSummaryLabel;
        private TextMeshProUGUI _resourceSummaryLabel;
        private TextMeshProUGUI _bioformSummaryLabel;
        private TextMeshProUGUI _signalSummaryLabel;
        // COLD ALLOC: StringBuilder[96] — sonar contact line assembly — owner: PDASpectrumTab
        private readonly StringBuilder _lineBuilder = new StringBuilder(96);

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

        private static readonly string[] ActiveModeLabels =
        {
            "АКТИВНЫЙ РЕЖИМ: НОРМАЛЬНЫЙ",
            "АКТИВНЫЙ РЕЖИМ: ТЕПЛОВИЗОР",
            "АКТИВНЫЙ РЕЖИМ: СОНАР",
            "АКТИВНЫЙ РЕЖИМ: ЭХОЛОТ"
        };

        private const string SonarActiveStatus = "СОНАР АКТИВЕН — РАДИУС: 100М";

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
            SpectrumEvents.OnSonarSnapshotUpdated += HandleSonarSnapshotUpdated;
            PDAEvents.OnOpened += HandlePDAOpened;

            RefreshModeDisplay();
        }

        private void OnDisable()
        {
            SpectrumEvents.OnModeChanged -= HandleModeChanged;
            SpectrumEvents.OnSonarSnapshotUpdated -= HandleSonarSnapshotUpdated;
            PDAEvents.OnOpened -= HandlePDAOpened;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT-DRIVEN REFRESH
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void HandleModeChanged(SpectrumMode mode) => RefreshModeDisplay();
        private void HandleSonarSnapshotUpdated(SpatialSonarSnapshot snapshot) => RefreshModeDisplay();
        private void HandlePDAOpened(int tab) => RefreshModeDisplay();

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_labelFont == null)
                _labelFont = TMPro.TMP_Settings.defaultFontAsset;

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
                new Vector2(12, -88), new Vector2(-12, -44));

            _sonarStatusLabel = CreateText("SonarStatus", panel, 9f, colorDim, TextAlignmentOptions.BottomLeft);
            Anchor(_sonarStatusLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 84), new Vector2(-12, 104));

            _contactSummaryLabel = CreateText("ContactSummary", panel, 8.5f, colorAccent, TextAlignmentOptions.BottomLeft);
            Anchor(_contactSummaryLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 60), new Vector2(-12, 80));

            _resourceSummaryLabel = CreateText("ResourceSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_resourceSummaryLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 40), new Vector2(-12, 60));

            _bioformSummaryLabel = CreateText("BioformSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_bioformSummaryLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 20), new Vector2(-12, 40));

            _signalSummaryLabel = CreateText("SignalSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_signalSummaryLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(12, 0), new Vector2(-12, 20));
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
            SetLabelText(_currentModeLabel, ActiveModeLabels[idx]);
            SetLabelText(_statusLabel, ModeDescriptions[idx]);
            if (active == SpectrumMode.Sonar && sys != null && sys.HasSonarSnapshot)
            {
                RefreshSonarSnapshot(sys.LastSonarSnapshot);
            }
            else if (active == SpectrumMode.Sonar)
            {
                SetLabelText(_sonarStatusLabel, "SONAR ACTIVE // AWAITING PULSE");
                SetLabelText(_contactSummaryLabel, "CONTACTS // RES 0 | BIO 0 | SIG 0");
                SetLabelText(_resourceSummaryLabel, "NEAREST RESOURCE // NONE");
                SetLabelText(_bioformSummaryLabel, "NEAREST BIOFORM // NONE");
                SetLabelText(_signalSummaryLabel, "NEAREST SIGNAL // NONE");
            }
            else
            {
                SetLabelText(_sonarStatusLabel, string.Empty);
                SetLabelText(_contactSummaryLabel, string.Empty);
                SetLabelText(_resourceSummaryLabel, string.Empty);
                SetLabelText(_bioformSummaryLabel, string.Empty);
                SetLabelText(_signalSummaryLabel, string.Empty);
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

        private void RefreshSonarSnapshot(SpatialSonarSnapshot snapshot)
        {
            SetLabelText(_sonarStatusLabel, "SONAR ACTIVE // GRID LOCKED");

            _lineBuilder.Clear();
            _lineBuilder.Append("CONTACTS // RES ");
            AppendInt(snapshot.ResourceCount);
            _lineBuilder.Append(" | BIO ");
            AppendInt(snapshot.BioformCount);
            _lineBuilder.Append(" | SIG ");
            AppendInt(snapshot.SignalCount);
            _contactSummaryLabel.SetText(_lineBuilder);

            SetDistanceLabel(_resourceSummaryLabel, "NEAREST RESOURCE // ", snapshot.HasNearestResource, snapshot.NearestResourceDistanceMeters, "NEAREST RESOURCE // NONE");
            SetDistanceLabel(_bioformSummaryLabel, "NEAREST BIOFORM // ", snapshot.HasNearestBioform, snapshot.NearestBioformDistanceMeters, "NEAREST BIOFORM // NONE");
            SetSignalDistanceLabel(snapshot);
        }

        private void SetDistanceLabel(TextMeshProUGUI label, string prefix, bool hasDistance, int distanceMeters, string emptyValue)
        {
            if (!hasDistance)
            {
                SetLabelText(label, emptyValue);
                return;
            }

            _lineBuilder.Clear();
            _lineBuilder.Append(prefix);
            AppendDistance(distanceMeters);
            label.SetText(_lineBuilder);
        }

        private void SetSignalDistanceLabel(SpatialSonarSnapshot snapshot)
        {
            if (!snapshot.HasNearestSignal)
            {
                SetLabelText(_signalSummaryLabel, "NEAREST SIGNAL // NONE");
                return;
            }

            _lineBuilder.Clear();
            _lineBuilder.Append("NEAREST SIGNAL // ");
            _lineBuilder.Append(ResolveSignalRoleLabel(snapshot.NearestSignalRole));
            _lineBuilder.Append(' ');
            AppendDistance(snapshot.NearestSignalDistanceMeters);
            _signalSummaryLabel.SetText(_lineBuilder);
        }

        private void AppendInt(int value)
        {
            int clampedValue = Mathf.Clamp(value, 0, HudNumericStringCache.MaxIntegerValue);
            _lineBuilder.Append(HudNumericStringCache.IntStrings[clampedValue]);
        }

        private void AppendDistance(int distanceMeters)
        {
            int clampedDistance = Mathf.Clamp(distanceMeters, 0, HudNumericStringCache.MaxIntegerValue);
            _lineBuilder.Append(HudNumericStringCache.IntStrings[clampedDistance]);
            _lineBuilder.Append('M');
        }

        private static string ResolveSignalRoleLabel(FieldTargetRole role)
        {
            switch (role)
            {
                case FieldTargetRole.RouteAnchor:
                    return "ANCHOR";
                case FieldTargetRole.RouteRelay:
                    return "RELAY";
                case FieldTargetRole.RouteFrontier:
                    return "FRONTIER";
                case FieldTargetRole.HazardProbe:
                    return "HAZARD";
                case FieldTargetRole.ServiceDamaged:
                    return "SERVICE";
                case FieldTargetRole.ServiceFlooded:
                    return "FLOOD";
                case FieldTargetRole.ServiceControl:
                    return "CONTROL";
                case FieldTargetRole.StructureRelay:
                    return "STRUCT";
                case FieldTargetRole.ExpeditionCheckpoint:
                    return "CHECKPOINT";
                case FieldTargetRole.ConstructionSocket:
                    return "SOCKET";
                case FieldTargetRole.ConstructionBlocked:
                    return "BLOCKED";
                case FieldTargetRole.ConstructionClear:
                    return "CLEAR";
                case FieldTargetRole.PowerGeneration:
                    return "POWER";
                case FieldTargetRole.PowerRelay:
                    return "GRID";
                case FieldTargetRole.PowerLoad:
                    return "LOAD";
                default:
                    return "SIGNAL";
            }
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
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
            LocalizedTMPAutoSizer.Configure(tmp, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            return tmp;
        }

        private static void SetLabelText(TextMeshProUGUI label, string value)
        {
            if (label != null && label.text != value)
            {
                label.text = value;
            }
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
