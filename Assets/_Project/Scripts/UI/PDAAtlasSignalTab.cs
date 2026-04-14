// ============================================================================
// HECTON-8 — PDAAtlasSignalTab.cs
// Вкладка PDA: ATLAS SIGNAL — мониторинг сигнала Атлас-6.
//
// ЛОР (лор3 Блок З):
//   Сигнал повторяется каждые 11:23 (683 сек).
//   Чем ближе к ядру — тем яснее содержание.
//   Фазы: 0=нет сигнала, 1=ритм, 2=эмоции, 3=содержание, 4=полная расшифровка.
//
// АРХИТЕКТУРА:
//   • Процедурный UI — сила сигнала, фаза декодирования, направление.
//   • ITickable — обновление таймера до следующего пульса.
//   • Слушает AtlasSignalEvents.
//
// ZERO GC:
//   • Pre-cached strings.
//   • Dirty-flag обновление.
// ============================================================================

using Hecton8.AtlasSignal;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Atlas Signal Tab")]
    public sealed class PDAAtlasSignalTab : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Font ─────────────────────────────────────")]
        [Tooltip("Шрифт с кириллицей. Если null — используется TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset _labelFont;

        [Header("── Colors ───────────────────────────────────")]
        [SerializeField] private Color colorBackground = new Color(0.03f, 0.05f, 0.08f, 0.95f);
        [SerializeField] private Color colorAccent    = new Color(0.20f, 0.80f, 0.60f, 1f);
        [SerializeField] private Color colorWarning   = new Color(0.90f, 0.60f, 0.20f, 1f);
        [SerializeField] private Color colorText      = new Color(0.85f, 0.90f, 0.85f, 1f);
        [SerializeField] private Color colorDim       = new Color(0.45f, 0.50f, 0.45f, 1f);
        [SerializeField] private Color colorPhase0    = new Color(0.30f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color colorPhase1    = new Color(0.40f, 0.40f, 0.50f, 1f);
        [SerializeField] private Color colorPhase2    = new Color(0.50f, 0.50f, 0.60f, 1f);
        [SerializeField] private Color colorPhase3    = new Color(0.60f, 0.70f, 0.80f, 1f);
        [SerializeField] private Color colorPhase4    = new Color(0.20f, 0.80f, 0.60f, 1f);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private RectTransform _root;
        private bool _built;
        private bool _registered;

        // UI elements
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _strengthLabel;
        private TextMeshProUGUI _strengthValue;
        private Image _strengthBar;
        private TextMeshProUGUI _phaseLabel;
        private TextMeshProUGUI _phaseValue;
        private Image[] _phaseIndicators = new Image[5];
        private TextMeshProUGUI _messageLabel;
        private TextMeshProUGUI _directionLabel;
        private TextMeshProUGUI _countdownLabel;
        private TextMeshProUGUI _pulseTimerLabel;

        // Cached state
        private float _currentStrength;
        private int _currentPhase = -1;
        private float _pulseCountdown;
        private bool _signalDetected;
        private bool _dirty;
        private int _lastCountdownSeconds = int.MinValue;
        private UnityEngine.Camera _mainCamera;

        // Pre-cached strings — zero GC
        private static readonly string[] PhaseNames =
        {
            "НЕТ СИГНАЛА",
            "РИТМИЧНЫЙ ПАТТЕРН",
            "ЭМОЦИОНАЛЬНЫЙ ПАТТЕРН",
            "СОДЕРЖАНИЕ СИГНАЛА",
            "РАСШИФРОВКА ЗАВЕРШЕНА"
        };

        private static readonly string[] PhaseShortNames =
        {
            "—",
            "РИТМ",
            "ЭМОЦИИ",
            "СОДЕРЖАНИЕ",
            "ГОТОВО"
        };

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null)
                _root = gameObject.AddComponent<RectTransform>();
                
            _mainCamera = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            if (!_built) EnsureBuilt();

            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            AtlasSignalEvents.OnSignalStrengthChanged += HandleStrengthChanged;
            AtlasSignalEvents.OnSignalPulse          += HandleSignalPulse;
            AtlasSignalEvents.OnSignalDetected       += HandleSignalDetected;
            AtlasSignalEvents.OnSignalDecoded        += HandleSignalDecoded;
            PDAEvents.OnOpened                       += HandlePDAOpened;

            _dirty = true;
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            AtlasSignalEvents.OnSignalStrengthChanged -= HandleStrengthChanged;
            AtlasSignalEvents.OnSignalPulse          -= HandleSignalPulse;
            AtlasSignalEvents.OnSignalDetected       -= HandleSignalDetected;
            AtlasSignalEvents.OnSignalDecoded        -= HandleSignalDecoded;
            PDAEvents.OnOpened                       -= HandlePDAOpened;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_dirty)
            {
                RefreshAll();
                _dirty = false;
            }

            // Update pulse countdown
            if (_signalDetected && _pulseCountdown > 0f)
            {
                _pulseCountdown -= deltaTime;
                if (_pulseCountdown < 0f) _pulseCountdown = 0f;
                UpdateCountdownDisplay();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        private void HandleStrengthChanged(float strength)
        {
            _currentStrength = strength;
            _dirty = true;
        }

        private void HandleSignalPulse(float intensity)
        {
            // Reset countdown to 683 seconds (11:23)
            _pulseCountdown = 683f;
            _lastCountdownSeconds = int.MinValue;
            _dirty = true;
        }

        private void HandleSignalDetected(Vector3 sourcePos)
        {
            _signalDetected = true;
            _pulseCountdown = 683f;
            _lastCountdownSeconds = int.MinValue;
            _dirty = true;
        }

        private void HandleSignalDecoded(string messageId)
        {
            _currentPhase = 4;
            _dirty = true;
        }

        private void HandlePDAOpened(int tab) => _dirty = true;

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
                _labelFont = UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/текст SDF");
                if (_labelFont == null)
                    _labelFont = TMPro.TMP_Settings.defaultFontAsset;
            }

            // Background
            Image bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = colorBackground;

            BuildHeader();
            BuildStrengthSection();
            BuildPhaseSection();
            BuildMessageSection();
            BuildDirectionSection();
            BuildCountdownSection();
        }

        private void BuildHeader()
        {
            RectTransform header = CreateRect("Header", _root);
            Anchor(header, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -48), new Vector2(0, 0));

            Image hBg = header.gameObject.AddComponent<Image>();
            hBg.color = new Color(0.04f, 0.08f, 0.06f, 1f);

            _titleLabel = CreateText("Title", header, 13f, colorAccent, TextAlignmentOptions.MidlineLeft);
            _titleLabel.text = "ATLAS SIGNAL — МОНИТОРИНГ";
            _titleLabel.fontStyle = FontStyles.Bold;
            Anchor(_titleLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 0), new Vector2(-12, 0));
        }

        private void BuildStrengthSection()
        {
            // Section: Signal Strength
            RectTransform section = CreateRect("StrengthSection", _root);
            Anchor(section, new Vector2(0, 0.75f), new Vector2(1, 1),
                new Vector2(0, -48), new Vector2(0, -8));

            _strengthLabel = CreateText("Label", section, 10f, colorDim, TextAlignmentOptions.TopLeft);
            _strengthLabel.text = "СИЛА СИГНАЛА";
            Anchor(_strengthLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -8), new Vector2(0, 0));

            // Bar background
            RectTransform barBg = CreateRect("BarBg", section);
            Anchor(barBg, new Vector2(0, 0), new Vector2(1, 0.5f),
                new Vector2(12, 0), new Vector2(-12, 0));

            Image barBgImg = barBg.gameObject.AddComponent<Image>();
            barBgImg.color = new Color(0.1f, 0.12f, 0.15f, 1f);

            // Bar fill
            RectTransform barFill = CreateRect("BarFill", barBg);
            _strengthBar = barFill.gameObject.AddComponent<Image>();
            _strengthBar.color = colorAccent;
            Anchor(barFill, new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 0));

            // Value label
            _strengthValue = CreateText("Value", section, 11f, colorText, TextAlignmentOptions.TopRight);
            Anchor(_strengthValue.rectTransform, new Vector2(0.5f, 1), new Vector2(1, 1),
                new Vector2(0, -24), new Vector2(-12, -8));
        }

        private void BuildPhaseSection()
        {
            RectTransform section = CreateRect("PhaseSection", _root);
            Anchor(section, new Vector2(0, 0.55f), new Vector2(1, 0.75f),
                new Vector2(0, 0), new Vector2(0, 0));

            _phaseLabel = CreateText("Label", section, 10f, colorDim, TextAlignmentOptions.TopLeft);
            _phaseLabel.text = "ФАЗА ДЕКОДИРОВАНИЯ";
            Anchor(_phaseLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -8), new Vector2(0, 0));

            // Phase indicators (5 circles)
            RectTransform indicators = CreateRect("Indicators", section);
            Anchor(indicators, new Vector2(0, 0), new Vector2(1, 0.6f),
                new Vector2(12, 0), new Vector2(-12, 0));

            float spacing = 1f / 6f;
            for (int i = 0; i < 5; i++)
            {
                RectTransform ind = CreateRect($"Phase_{i}", indicators);
                float xMin = spacing + i * spacing * 1.1f;
                float xMax = xMin + spacing * 0.8f;
                Anchor(ind, new Vector2(xMin, 0.1f), new Vector2(xMax, 0.9f),
                    new Vector2(0, 0), new Vector2(0, 0));

                Image img = ind.gameObject.AddComponent<Image>();
                img.color = colorPhase0;
                _phaseIndicators[i] = img;
            }

            _phaseValue = CreateText("PhaseValue", section, 9f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_phaseValue.rectTransform, new Vector2(0, 0), new Vector2(1, 0.4f),
                new Vector2(12, 0), new Vector2(-12, 0));
        }

        private void BuildMessageSection()
        {
            RectTransform section = CreateRect("MessageSection", _root);
            Anchor(section, new Vector2(0, 0.30f), new Vector2(1, 0.55f),
                new Vector2(0, 0), new Vector2(0, 0));

            Image sBg = section.gameObject.AddComponent<Image>();
            sBg.color = new Color(0.02f, 0.04f, 0.03f, 1f);

            _messageLabel = CreateText("Message", section, 10f, colorText, TextAlignmentOptions.TopLeft);
            _messageLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_messageLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 8), new Vector2(-12, -8));
        }

        private void BuildDirectionSection()
        {
            RectTransform section = CreateRect("DirectionSection", _root);
            Anchor(section, new Vector2(0, 0.15f), new Vector2(1, 0.30f),
                new Vector2(0, 0), new Vector2(0, 0));

            _directionLabel = CreateText("Direction", section, 10f, colorDim, TextAlignmentOptions.MidlineLeft);
            Anchor(_directionLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 0), new Vector2(-12, 0));
        }

        private void BuildCountdownSection()
        {
            RectTransform section = CreateRect("CountdownSection", _root);
            Anchor(section, new Vector2(0, 0), new Vector2(1, 0.15f),
                new Vector2(0, 0), new Vector2(0, 0));

            Image cBg = section.gameObject.AddComponent<Image>();
            cBg.color = new Color(0.04f, 0.06f, 0.05f, 1f);

            _pulseTimerLabel = CreateText("PulseTimer", section, 11f, colorWarning, TextAlignmentOptions.MidlineRight);
            Anchor(_pulseTimerLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(-12, 0));

            TextMeshProUGUI label = CreateText("Label", section, 9f, colorDim, TextAlignmentOptions.MidlineLeft);
            label.text = "СЛЕДУЮЩИЙ ПУЛЬС:";
            Anchor(label.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
        }

        // ═════════════════════��════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            AtlasSignalDecoder decoder = AtlasSignalDecoder.Instance;

            // Get current state
            _currentStrength = sys != null ? sys.CurrentStrength : 0f;
            _signalDetected = sys != null && sys.IsDetected;
            _currentPhase = decoder != null ? decoder.CurrentPhase : 0;

            RefreshStrength();
            RefreshPhase();
            RefreshMessage();
            RefreshDirection();
            UpdateCountdownDisplay();
        }

        private void RefreshStrength()
        {
            // Update bar
            if (_strengthBar != null)
            {
                RectTransform rt = _strengthBar.rectTransform;
                float w = Mathf.Clamp01(_currentStrength);
                rt.anchorMax = new Vector2(w, 1f);
                rt.offsetMax = new Vector2(0, 0);

                // Color based on strength
                _strengthBar.color = _currentPhase >= 4 ? colorPhase4 :
                    _currentStrength > 0.7f ? colorAccent :
                    _currentStrength > 0.3f ? colorWarning : colorDim;
            }

            // Update value
            if (_strengthValue != null)
                _strengthValue.text = $"{(_currentStrength * 100f):F0}%";
        }

        private void RefreshPhase()
        {
            // Update phase indicators
            Color[] phaseColors = { colorPhase0, colorPhase1, colorPhase2, colorPhase3, colorPhase4 };
            for (int i = 0; i < _phaseIndicators.Length; i++)
            {
                if (_phaseIndicators[i] == null) continue;
                _phaseIndicators[i].color = i <= _currentPhase ? phaseColors[Mathf.Min(i, 4)] : colorPhase0;
            }

            // Update phase label
            if (_phaseValue != null)
            {
                int phase = Mathf.Clamp(_currentPhase, 0, 4);
                _phaseValue.text = PhaseNames[phase];
            }
        }

        private void RefreshMessage()
        {
            if (_messageLabel == null) return;

            if (_currentPhase >= 4)
            {
                _messageLabel.text = "АТЛАС-6 — РАСШИФРОВКА ЗАВЕРШЕНА\n\nИсточник: глубина -5000м\nЯдро активно. Программа посева активна.\n\n847 дней поиска решения. Колония мертва.";
                _messageLabel.color = colorPhase4;
            }
            else if (_currentPhase == 3)
            {
                _messageLabel.text = "АТЛАС-6 — ПОИСК РЕШЕНИЯ\n\n847 дней. Колония мертва.\nПрограмма посева активна.\n\nСигнал содержит... что-то.";
                _messageLabel.color = colorText;
            }
            else if (_currentPhase == 2)
            {
                _messageLabel.text = "СИГНАЛ АТЛАС-6\n\nЭмоциональный паттерн:\nОтчаяние → Надежда → Безумие\n\nПриблизиться для расшифровки.";
                _messageLabel.color = colorText;
            }
            else if (_currentPhase == 1)
            {
                _messageLabel.text = "НЕИЗВЕСТНЫЙ СИГНАЛ\n\nРитмичный паттерн.\nПериод: 11:23\n\nИсточник неизвестен.";
                _messageLabel.color = colorDim;
            }
            else
            {
                _messageLabel.text = "СИГНАЛ НЕ ОБНАРУЖЕН\n\nПриблизьтесь к источнику\nили используйте сканер.";
                _messageLabel.color = colorDim;
            }
        }

        private void RefreshDirection()
        {
            if (_directionLabel == null) return;

            if (!_signalDetected)
            {

                _directionLabel.text = "НАПРАВЛЕНИЕ: —";
                _directionLabel.color = colorDim;
                return;
            }

            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            if (sys == null)
            {
                _directionLabel.text = "НАПРАВЛЕНИЕ: ОШИБКА ДАННЫХ";
                return;
            }

            Vector3 dir = sys.DirectionToCore;
            float dist = Vector3.Distance(sys.AtlasCorePosition, 
                _mainCamera != null ? _mainCamera.transform.position : Vector3.zero);

            // Convert direction to compass
            string compass = GetCompassDirection(dir);
            _directionLabel.text = $"НАПРАВЛЕНИЕ: {compass}  |  РАССТОЯНИЕ: {dist:F0}М";
            _directionLabel.color = colorAccent;
        }

        private void UpdateCountdownDisplay()
        {
            if (_pulseTimerLabel == null) return;

            if (!_signalDetected)
            {
                if (_lastCountdownSeconds == -1)
                    return;

                _lastCountdownSeconds = -1;
                _pulseTimerLabel.text = "—:—";
                return;
            }

            int totalSecs = Mathf.CeilToInt(_pulseCountdown);
            if (totalSecs == _lastCountdownSeconds)
                return;

            _lastCountdownSeconds = totalSecs;
            int mins = totalSecs / 60;
            int secs = totalSecs % 60;
            _pulseTimerLabel.SetText("{0:D2}:{1:D2}", mins, secs);
        }

        private static string GetCompassDirection(Vector3 dir)
        {
            // Project to horizontal plane
            Vector2 horizontal = new Vector2(dir.x, dir.z);
            if (horizontal.sqrMagnitude < 0.001f)
                return dir.y > 0 ? "ВВЕРХ" : "ВНИЗ";

            float angle = Mathf.Atan2(horizontal.x, horizontal.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle < 22.5f || angle >= 337.5f) return "СЕВЕР";
            if (angle < 67.5f) return "СЕВЕРО-ВОСТОК";
            if (angle < 112.5f) return "ВОСТОК";
            if (angle < 157.5f) return "ЮГО-ВОСТОК";
            if (angle < 202.5f) return "ЮГ";
            if (angle < 247.5f) return "ЮГО-ЗАПАД";
            if (angle < 292.5f) return "ЗАПАД";
            return "СЕВЕРО-ЗАПАД";
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
