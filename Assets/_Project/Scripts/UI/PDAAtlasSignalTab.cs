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

using System.Text;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Atlas Signal Tab")]
    public sealed class PDAAtlasSignalTab : MonoBehaviour, ITickable, IUpdatable, IAtlasSignalEventListener, IPDAEventListener
    {
        private const float MainCameraResolveRetryInterval = 1f;
        private const string StrengthPercentTemplate = "{0}%";
        private const string PulseTimerTemplate = "{0:D2}:{1:D2}";
        private static readonly char[] StrengthPercentTemplateChars = StrengthPercentTemplate.ToCharArray();
        private static readonly char[] PulseTimerTemplateChars = PulseTimerTemplate.ToCharArray();

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

        [Header("── First-Hour Gate ─────────────────────")]
        [Tooltip("Do not expose stable Atlas telemetry in the PDA before the first-hour spine reaches module-route play.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToReveal = FirstHourMilestone.FirstModule;

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
        private bool _atlasTelemetryVisible;
        private bool _dirty;
        private int _lastCountdownSeconds = int.MinValue;
        private int _lastStrengthDisplayMode = int.MinValue;
        private int _lastStrengthPercent = int.MinValue;
        private UnityEngine.Camera _mainCamera;
        private float _mainCameraResolveRetryTimer;
        private readonly StringBuilder _directionBuilder = new StringBuilder(64); // COLD ALLOC: StringBuilder[64] — atlas direction label formatting — owner: PDAAtlasSignalTab

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

        private static readonly string[] MessageTexts =
        {
            "СИГНАЛ НЕ ОБНАРУЖЕН\n\nПриблизьтесь к источнику\nили используйте сканер.",
            "НЕИЗВЕСТНЫЙ СИГНАЛ\n\nРитмичный паттерн.\nПериод: 11:23\n\nИсточник неизвестен.",
            "НЕСТАБИЛЬНЫЙ ПАТТЕРН\n\nЭмоциональный отпечаток:\nОтчаяние → Надежда → Безумие\n\nИсточник ещё не удерживается.",
            "АТЛАС-6 — ПОИСК РЕШЕНИЯ\n\n847 дней. Колония мертва.\nПрограмма посева активна.\n\nСигнал содержит... что-то.",
            "АТЛАС-6 — РАСШИФРОВКА ЗАВЕРШЕНА\n\nИсточник: глубина -5000м\nЯдро активно. Программа посева активна.\n\n847 дней поиска решения. Колония мертва."
        };

        private static readonly string[] DirectionDistancePrefixes =
        {
            "НАПРАВЛЕНИЕ: ВВЕРХ  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ВНИЗ  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: СЕВЕР  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: СЕВЕРО-ВОСТОК  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ВОСТОК  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ЮГО-ВОСТОК  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ЮГ  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ЮГО-ЗАПАД  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: ЗАПАД  |  РАССТОЯНИЕ: ",
            "НАПРАВЛЕНИЕ: СЕВЕРО-ЗАПАД  |  РАССТОЯНИЕ: "
        };

        private const string StrengthNoiseLabel = "ШУМ";
        private const string StrengthPatternLabel = "ПАТТЕРН";
        private const string BackgroundNoiseMessage = "ФОНОВЫЙ ШУМ\n\nСтабильной телеметрии нет.\nСеть не держит решение направления.\n\nПродолжайте маршрут и сбор.";
        private const string DirectionUnavailableLabel = "НАПРАВЛЕНИЕ: —";
        private const string DirectionDataErrorLabel = "НАПРАВЛЕНИЕ: ОШИБКА ДАННЫХ";
        private const string DirectionUnstableLabel = "НАПРАВЛЕНИЕ: ПЕЛЕНГ ЕЩЁ НЕ УДЕРЖИВАЕТСЯ";

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

            TryRegister();

            AtlasSignalEvents.Register(this);
            PDAEvents.Register(this);

            _dirty = true;
        }

        private void OnDisable()
        {
            TryUnregister();

            AtlasSignalEvents.Unregister(this);
            PDAEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_mainCamera == null && _mainCameraResolveRetryTimer > 0f)
                _mainCameraResolveRetryTimer -= deltaTime;

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

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            switch ((AtlasSignalEventType)payload.EventType)
            {
                case AtlasSignalEventType.StrengthChanged:
                    HandleStrengthChanged(payload.SignalStrength);
                    break;
                case AtlasSignalEventType.Pulse:
                    HandleSignalPulse(payload.SignalStrength);
                    break;
                case AtlasSignalEventType.Decoded:
                    HandleSignalDecoded(string.Empty);
                    break;
            }
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType == PDAEventType.Opened)
                HandlePDAOpened(payload.CurrentTab);
        }

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

        private void HandleSignalDecoded(string messageId)
        {
            _currentPhase = 4;
            _dirty = true;
        }

        private void HandlePDAOpened(int tab) => _dirty = true;

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _labelFont = LocalizedFontResolver.ResolveReadableFont(_labelFont);

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
            _atlasTelemetryVisible = CanRevealAtlasTelemetry(sys);
            bool hasReadableContact = HasReadableAtlasContact(sys);

            if (_atlasTelemetryVisible)
            {
                _currentStrength = sys != null ? sys.CurrentStrength : 0f;
                _signalDetected = hasReadableContact;
                _currentPhase = decoder != null ? decoder.CurrentPhase : 0;
            }
            else
            {
                _currentStrength = 0f;
                _signalDetected = false;
                _currentPhase = 0;
                _pulseCountdown = 0f;
            }

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
            {
                int displayMode = _currentPhase switch
                {
                    1 => 1,
                    2 => 2,
                    _ => 0
                };

                if (displayMode == 1)
                {
                    _lastStrengthDisplayMode = displayMode;
                    _lastStrengthPercent = int.MinValue;
                    SetLabelText(_strengthValue, StrengthNoiseLabel);
                }
                else if (displayMode == 2)
                {
                    _lastStrengthDisplayMode = displayMode;
                    _lastStrengthPercent = int.MinValue;
                    SetLabelText(_strengthValue, StrengthPatternLabel);
                }
                else
                {
                    int roundedPercent = Mathf.Clamp(Mathf.RoundToInt(_currentStrength * 100f), 0, 100);
                    if (_lastStrengthDisplayMode != displayMode || _lastStrengthPercent != roundedPercent)
                    {
                        _lastStrengthDisplayMode = displayMode;
                        _lastStrengthPercent = roundedPercent;
                        SetNumericText(_strengthValue, StrengthPercentTemplate, LocNumericArg.Int(roundedPercent));
                    }
                }
            }
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
                SetLabelText(_phaseValue, PhaseNames[phase]);
            }
        }

        private void RefreshMessage()
        {
            if (_messageLabel == null) return;

            if (!_atlasTelemetryVisible)
            {
                SetLabelText(_messageLabel, BackgroundNoiseMessage);
                SetLabelColor(_messageLabel, colorDim);
                return;
            }

            int messageIndex = Mathf.Clamp(_currentPhase, 0, MessageTexts.Length - 1);
            SetLabelText(_messageLabel, MessageTexts[messageIndex]);
            SetLabelColor(_messageLabel, _currentPhase >= 4 ? colorPhase4 : _currentPhase >= 2 ? colorText : colorDim);
        }

        private void RefreshDirection()
        {
            if (_directionLabel == null) return;

            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            int revealStage = sys != null ? sys.CurrentRevealStage : 0;

            if (!_signalDetected)
            {
                SetLabelText(_directionLabel, DirectionUnavailableLabel);
                SetLabelColor(_directionLabel, colorDim);
                return;
            }

            if (sys == null)
            {
                SetLabelText(_directionLabel, DirectionDataErrorLabel);
                return;
            }

            if (revealStage < 3)
            {
                SetLabelText(_directionLabel, DirectionUnstableLabel);
                SetLabelColor(_directionLabel, colorDim);
                return;
            }

            Vector3 dir = sys.DirectionToCore;
            TryResolveMainCamera();
            float dist = Vector3.Distance(sys.AtlasCorePosition, 
                _mainCamera != null ? _mainCamera.transform.position : Vector3.zero);

            // Convert direction to compass
            int directionIndex = GetCompassDirectionIndex(dir);
            _directionBuilder.Clear();
            _directionBuilder.Append(DirectionDistancePrefixes[directionIndex]);
            _directionBuilder.Append(Mathf.RoundToInt(dist));
            _directionBuilder.Append('М');
            _directionLabel.SetText(_directionBuilder);
            SetLabelColor(_directionLabel, colorAccent);
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
            SetNumericText(_pulseTimerLabel, PulseTimerTemplate, LocNumericArg.Int(mins), LocNumericArg.Int(secs));
        }

        private bool CanRevealAtlasTelemetry(AtlasSignalSystem sys)
        {
            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null)
            {
                return firstHourDirector.IsMilestoneComplete(minimumMilestoneToReveal) &&
                    HasReadableAtlasContact(sys);
            }

            return HasReadableAtlasContact(sys);
        }

        private static bool HasReadableAtlasContact(AtlasSignalSystem sys)
        {
            return sys != null &&
                sys.CurrentRevealStage >= 2 &&
                sys.IsDetected;
        }

        private void TryResolveMainCamera()
        {
            if (_mainCamera != null)
                return;

            if (_mainCameraResolveRetryTimer > 0f)
                return;

            _mainCameraResolveRetryTimer = MainCameraResolveRetryInterval;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
            {
                _mainCamera = playerContext.PlayerCamera;
                return;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerTransform.TryGetComponent(out _mainCamera);
            }

            if (_mainCamera == null && TryGetComponent(out Camera localCamera))
            {
                _mainCamera = localCamera;
                return;
            }

            if (_mainCamera == null)
                _mainCamera = GetComponentInParent<Camera>();
        }

        private static int GetCompassDirectionIndex(Vector3 dir)
        {
            // Project to horizontal plane
            Vector2 horizontal = new Vector2(dir.x, dir.z);
            if (horizontal.sqrMagnitude < 0.001f)
                return dir.y > 0 ? 0 : 1;

            float angle = Mathf.Atan2(horizontal.x, horizontal.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle < 22.5f || angle >= 337.5f) return 2;
            if (angle < 67.5f) return 3;
            if (angle < 112.5f) return 4;
            if (angle < 157.5f) return 5;
            if (angle < 202.5f) return 6;
            if (angle < 247.5f) return 7;
            if (angle < 292.5f) return 8;
            return 9;
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

        private static void SetNumericText(TextMeshProUGUI label, string template, LocNumericArg value0)
        {
            if (label == null)
                return;

            LocNumericBuffer.Write(new System.ReadOnlySpan<char>(StrengthPercentTemplateChars), value0, out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void SetNumericText(TextMeshProUGUI label, string template, LocNumericArg value0, LocNumericArg value1)
        {
            if (label == null)
                return;

            LocNumericBuffer.Write(new System.ReadOnlySpan<char>(PulseTimerTemplateChars), value0, value1, out char[] buffer, out int length);
            int safeLength = Mathf.Clamp(length, 0, buffer != null ? buffer.Length : 0);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private static void SetLabelColor(TextMeshProUGUI label, Color value)
        {
            if (label != null && label.color != value)
            {
                label.color = value;
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
