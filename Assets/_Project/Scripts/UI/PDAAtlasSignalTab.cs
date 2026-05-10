// ============================================================================
// HECTON-8 — PDAAtlasSignalTab.cs
// Vkladka PDA: ATLAS SIGNAL — monitoring signala Atlas-6.
//
// LOR (lor3 Blok Z):
//   Signal povtoryaetsya kazhdye 11:23 (683 sek).
//   Chem blizhe k yadru — tem yasnee soderzhanie.
//   Fazy: 0=net signala, 1=ritm, 2=emotsii, 3=soderzhanie, 4=polnaya rasshifrovka.
//
// ARHITEKTURA:
//   • Protsedurnyy UI — sila signala, faza dekodirovaniya, napravlenie.
//   • ITickable — obnovlenie taymera do sleduyuschego pulsa.
//   • Slushaet AtlasSignalEvents.
//
// ZERO GC:
//   • Pre-cached strings.
//   • Dirty-flag obnovlenie.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Atlas Signal Tab")]
    public sealed class PDAAtlasSignalTab : MonoBehaviour, ITickable, IUpdatable, IAtlasSignalEventListener, IPDAEventListener
    {
        private const int DirectionDistanceNearStepMeters = 5;
        private const int DirectionDistanceMidStepMeters = 25;
        private const int DirectionDistanceFarStepMeters = 100;
        private const int DirectionDistanceNearThresholdMeters = 100;
        private const int DirectionDistanceMidThresholdMeters = 1000;
        private const int DirectionDistanceMaxDisplayMeters = 99999;
        private const float CompassOctantAxisRatio = 0.41421356f;
        private const float BeaconTelemetryEpsilon = 0.01f;
        private const float BeaconTelemetryPollInterval = 0.1f;
        private const string StrengthPercentTemplate = "{0}%";
        private const string PulseTimerTemplate = "{0:D2}:{1:D2}";
        private static readonly char[] StrengthPercentTemplateChars = StrengthPercentTemplate.ToCharArray();
        private static readonly char[] PulseTimerTemplateChars = PulseTimerTemplate.ToCharArray();
        private static readonly char[] PulseTimerEmptyChars = "—:—".ToCharArray();

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Font ─────────────────────────────────────")]
        [Tooltip("Shrift s kirillitsey. Esli null — ispolzuetsya TMP default.")]
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
        private PDADecryptionSpectrogramPanel _spectrogramPanel;

        // Cached state
        private float _currentStrength;
        private int _currentPhase = -1;
        private float _pulseCountdown;
        private bool _signalDetected;
        private bool _atlasTelemetryVisible;
        private bool _signalBeaconContact;
        private float _beaconStrength01;
        private float _beaconStatic01;
        private bool _dirty;
        private float _beaconTelemetryPollTimer;
        private int _lastCountdownSeconds = int.MinValue;
        private int _lastStrengthDisplayMode = int.MinValue;
        private int _lastStrengthPercent = int.MinValue;
        // COLD ALLOC: char[192] — atlas direction label formatting buffer — owner: PDAAtlasSignalTab
        private readonly char[] _directionBuffer = new char[192];
        // COLD ALLOC: char[16] — atlas strength percent formatting buffer — owner: PDAAtlasSignalTab
        private readonly char[] _strengthNumericBuffer = new char[16];
        // COLD ALLOC: char[16] — atlas pulse timer formatting buffer — owner: PDAAtlasSignalTab
        private readonly char[] _pulseTimerBuffer = new char[16];
        // COLD ALLOC: char[1024] — atlas cached label copy buffer for runtime TMP SetCharArray paths — owner: PDAAtlasSignalTab
        private readonly char[] _labelTextBuffer = new char[1024];

        // Pre-cached strings — zero GC
        private static readonly string[] PhaseNames =
        {
            "NET SIGNALA",
            "RITMIChNYY PATTERN",
            "EMOTsIONALNYY PATTERN",
            "SODERZhANIE SIGNALA",
            "RASShIFROVKA ZAVERShENA"
        };

        private static readonly string[] PhaseShortNames =
        {
            "—",
            "RITM",
            "EMOTsII",
            "SODERZhANIE",
            "GOTOVO"
        };

        private static readonly string[] PhaseIndicatorNames =
        {
            "Phase_0",
            "Phase_1",
            "Phase_2",
            "Phase_3",
            "Phase_4"
        };

        private static readonly string[] MessageTexts =
        {
            "SIGNAL NE OBNARUZhEN\n\nPribliztes k istochniku\nili ispolzuyte skaner.",
            "NEIZVESTNYY SIGNAL\n\nRitmichnyy pattern.\nPeriod: 11:23\n\nIstochnik neizvesten.",
            "NESTABILNYY PATTERN\n\nEmotsionalnyy otpechatok:\nOtchayanie → Nadezhda → Bezumie\n\nIstochnik esche ne uderzhivaetsya.",
            "ATLAS-6 — POISK REShENIYa\n\n847 dney. Koloniya mertva.\nProgramma poseva aktivna.\n\nSignal soderzhit... chto-to.",
            "ATLAS-6 — RASShIFROVKA ZAVERShENA\n\nIstochnik: glubina -5000m\nYadro aktivno. Programma poseva aktivna.\n\n847 dney poiska resheniya. Koloniya mertva."
        };

        private static readonly string[] DirectionDistancePrefixes =
        {
            "NAPRAVLENIE: VVERH  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: VNIZ  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: SEVER  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: SEVERO-VOSTOK  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: VOSTOK  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: YuGO-VOSTOK  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: YuG  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: YuGO-ZAPAD  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: ZAPAD  |  RASSTOYaNIE: ",
            "NAPRAVLENIE: SEVERO-ZAPAD  |  RASSTOYaNIE: "
        };

        private const string StrengthNoiseLabel = "ShUM";
        private const string StrengthPatternLabel = "PATTERN";
        private const string BackgroundNoiseMessage = "FONOVYY ShUM\n\nStabilnoy telemetrii net.\nSet ne derzhit reshenie napravleniya.\n\nProdolzhayte marshrut i sbor.";
        private const string DirectionUnavailableLabel = "NAPRAVLENIE: —";
        private const string DirectionDataErrorLabel = "NAPRAVLENIE: OShIBKA DANNYH";
        private const string DirectionUnstableLabel = "NAPRAVLENIE: PELENG ESchE NE UDERZhIVAETSYa";
        private const string SignalBeaconContactMessage = "AUP SIGNAL CONTACT\n\nTriangulated carrier strength is active.\nUse sonar breadcrumbs to locate the source.";
        private const string SignalBeaconStaticMessage = "AUP SIGNAL CONTACT\n\nCave interference is corrupting the carrier.\nStatic shader gain is elevated.";

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
            _beaconTelemetryPollTimer = 0f;
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
            AtlasSignalEvents.Unregister(this);
            PDAEvents.Unregister(this);
            PDAEvents.AssertUnregistered(this, nameof(PDAAtlasSignalTab));
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            _beaconTelemetryPollTimer -= math.max(0f, deltaTime);
            if (_beaconTelemetryPollTimer <= 0f)
            {
                _beaconTelemetryPollTimer = BeaconTelemetryPollInterval;
                PollSignalBeaconDirtyState();
            }

            if (_dirty)
            {
                RefreshAll();
                _dirty = false;
            }

            // Update pulse countdown
            if (_signalDetected && _pulseCountdown > 0f)
            {
                _pulseCountdown = math.max(0f, _pulseCountdown - math.max(0f, deltaTime));
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

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
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
            BuildSpectrogramSection();
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
            SetLabelText(_titleLabel, "ATLAS SIGNAL — MONITORING");
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
            SetLabelText(_strengthLabel, "SILA SIGNALA");
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
            Anchor(section, new Vector2(0, 0.62f), new Vector2(1, 0.75f),
                new Vector2(0, 0), new Vector2(0, 0));

            _phaseLabel = CreateText("Label", section, 10f, colorDim, TextAlignmentOptions.TopLeft);
            SetLabelText(_phaseLabel, "FAZA DEKODIROVANIYa");
            Anchor(_phaseLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -8), new Vector2(0, 0));

            // Phase indicators (5 circles)
            RectTransform indicators = CreateRect("Indicators", section);
            Anchor(indicators, new Vector2(0, 0), new Vector2(1, 0.6f),
                new Vector2(12, 0), new Vector2(-12, 0));

            float spacing = 1f / 6f;
            for (int i = 0; i < 5; i++)
            {
                RectTransform ind = CreateRect(PhaseIndicatorNames[i], indicators);
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

        private void BuildSpectrogramSection()
        {
            RectTransform section = CreateRect("SpectrogramSection", _root);
            Anchor(section, new Vector2(0, 0.34f), new Vector2(1, 0.62f),
                new Vector2(0, 0), new Vector2(0, 0));

            _spectrogramPanel = section.gameObject.AddComponent<PDADecryptionSpectrogramPanel>();
        }

        private void BuildMessageSection()
        {
            RectTransform section = CreateRect("MessageSection", _root);
            Anchor(section, new Vector2(0, 0.18f), new Vector2(1, 0.34f),
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
            Anchor(section, new Vector2(0, 0.08f), new Vector2(1, 0.18f),
                new Vector2(0, 0), new Vector2(0, 0));

            _directionLabel = CreateText("Direction", section, 10f, colorDim, TextAlignmentOptions.MidlineLeft);
            Anchor(_directionLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(12, 0), new Vector2(-12, 0));
        }

        private void BuildCountdownSection()
        {
            RectTransform section = CreateRect("CountdownSection", _root);
            Anchor(section, new Vector2(0, 0), new Vector2(1, 0.08f),
                new Vector2(0, 0), new Vector2(0, 0));

            Image cBg = section.gameObject.AddComponent<Image>();
            cBg.color = new Color(0.04f, 0.06f, 0.05f, 1f);

            _pulseTimerLabel = CreateText("PulseTimer", section, 11f, colorWarning, TextAlignmentOptions.MidlineRight);
            Anchor(_pulseTimerLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(-12, 0));

            TextMeshProUGUI label = CreateText("Label", section, 9f, colorDim, TextAlignmentOptions.MidlineLeft);
            SetLabelText(label, "SLEDUYuSchIY PULS:");
            Anchor(label.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(12, 0), new Vector2(0, 0));
        }

        // ═════════════════════��════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            AtlasSignalSystem sys = Hecton8.Core.GlobalRegistry.AtlasSignal;
            AtlasSignalDecoder decoder = Hecton8.Core.GlobalRegistry.AtlasSignalDecoder;
            _signalBeaconContact = SignalBeaconRegistry.TryGetDominantTelemetry(out _beaconStrength01, out _beaconStatic01) &&
                                   _beaconStrength01 > 0f;
            _atlasTelemetryVisible = CanRevealAtlasTelemetry(sys);
            bool hasReadableContact = HasReadableAtlasContact(sys);

            if (_signalBeaconContact)
            {
                _atlasTelemetryVisible = true;
                _currentStrength = _beaconStrength01;
                _signalDetected = true;
                _currentPhase = decoder != null
                    ? decoder.CurrentPhase
                    : math.clamp(SignalStrengthSystem.StrengthToBand(_currentStrength), 0, 3);
            }
            else if (_atlasTelemetryVisible)
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

        private void PollSignalBeaconDirtyState()
        {
            bool hasBeaconContact = SignalBeaconRegistry.TryGetDominantTelemetry(out float strength01, out float static01) &&
                                    strength01 > 0f;
            float safeStrength01 = hasBeaconContact ? math.saturate(strength01) : 0f;
            float safeStatic01 = hasBeaconContact ? math.saturate(static01) : 0f;
            if (hasBeaconContact == _signalBeaconContact &&
                math.abs(safeStrength01 - _beaconStrength01) <= BeaconTelemetryEpsilon &&
                math.abs(safeStatic01 - _beaconStatic01) <= BeaconTelemetryEpsilon)
            {
                return;
            }

            _dirty = true;
        }

        private void RefreshStrength()
        {
            // Update bar
            if (_strengthBar != null)
            {
                RectTransform rt = _strengthBar.rectTransform;
                float w = math.saturate(_currentStrength);
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
                    int roundedPercent = math.clamp((int)math.round(_currentStrength * 100f), 0, 100);
                    if (_lastStrengthDisplayMode != displayMode || _lastStrengthPercent != roundedPercent)
                    {
                        _lastStrengthDisplayMode = displayMode;
                        _lastStrengthPercent = roundedPercent;
                        SetNumericText(_strengthValue, _strengthNumericBuffer, StrengthPercentTemplateChars, LocNumericArg.Int(roundedPercent));
                    }
                }
            }
        }

        private void RefreshPhase()
        {
            // Update phase indicators
            for (int i = 0; i < _phaseIndicators.Length; i++)
            {
                if (_phaseIndicators[i] == null) continue;
                _phaseIndicators[i].color = i <= _currentPhase ? ResolvePhaseColor(math.min(i, 4)) : colorPhase0;
            }

            // Update phase label
            if (_phaseValue != null)
            {
                int phase = math.clamp(_currentPhase, 0, 4);
                SetLabelText(_phaseValue, PhaseNames[phase]);
            }
        }

        private Color ResolvePhaseColor(int phase)
        {
            return phase switch
            {
                1 => colorPhase1,
                2 => colorPhase2,
                3 => colorPhase3,
                4 => colorPhase4,
                _ => colorPhase0
            };
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

            if (_signalBeaconContact)
            {
                SetLabelText(_messageLabel, _beaconStatic01 > 0.5f ? SignalBeaconStaticMessage : SignalBeaconContactMessage);
                SetLabelColor(_messageLabel, _beaconStatic01 > 0.5f ? colorWarning : colorText);
                return;
            }

            int messageIndex = math.clamp(_currentPhase, 0, MessageTexts.Length - 1);
            SetLabelText(_messageLabel, MessageTexts[messageIndex]);
            SetLabelColor(_messageLabel, _currentPhase >= 4 ? colorPhase4 : _currentPhase >= 2 ? colorText : colorDim);
        }

        private void RefreshDirection()
        {
            if (_directionLabel == null) return;

            AtlasSignalSystem sys = Hecton8.Core.GlobalRegistry.AtlasSignal;
            int revealStage = sys != null ? sys.CurrentRevealStage : 0;

            if (_signalBeaconContact)
            {
                SetLabelText(_directionLabel, DirectionUnstableLabel);
                SetLabelColor(_directionLabel, colorDim);
                return;
            }

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
            if (!TryResolveAtlasCoreDistanceMeters(sys, out int distanceMeters))
            {
                SetLabelText(_directionLabel, DirectionDataErrorLabel);
                SetLabelColor(_directionLabel, colorDim);
                return;
            }

            // Convert direction to compass
            int directionIndex = GetCompassDirectionIndex(dir);
            int directionLength = 0;
            directionLength = Append(_directionBuffer, directionLength, DirectionDistancePrefixes[directionIndex]);
            directionLength = AppendInt(_directionBuffer, directionLength, distanceMeters);
            directionLength = Append(_directionBuffer, directionLength, 'M');
            SetBufferText(_directionLabel, _directionBuffer, directionLength);
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
                _pulseTimerLabel.SetCharArray(PulseTimerEmptyChars, 0, PulseTimerEmptyChars.Length);
                return;
            }

            int totalSecs = (int)math.ceil(_pulseCountdown);
            if (totalSecs == _lastCountdownSeconds)
                return;

            _lastCountdownSeconds = totalSecs;
            int mins = totalSecs / 60;
            int secs = totalSecs % 60;
            SetNumericText(_pulseTimerLabel, _pulseTimerBuffer, PulseTimerTemplateChars, LocNumericArg.Int(mins), LocNumericArg.Int(secs));
        }

        private bool CanRevealAtlasTelemetry(AtlasSignalSystem sys)
        {
            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
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

        private static bool TryResolveAtlasCoreDistanceMeters(AtlasSignalSystem sys, out int distanceMeters)
        {
            distanceMeters = 0;
            if (sys == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement == null)
                return false;

            AbsoluteUniversePosition playerAup = playerMovement.CurrentAup;
            AbsoluteUniversePosition coreAup = AbsoluteUniversePosition.FromRuntimePosition(sys.AtlasCorePosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            distanceMeters = EstimateCinematicDistanceMeters(in playerAup, in coreAup, distanceSq);
            return true;
        }

        private static int EstimateCinematicDistanceMeters(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition coreAup,
            double distanceSq)
        {
            if (distanceSq <= 0d || double.IsNaN(distanceSq))
                return 0;
            if (double.IsInfinity(distanceSq))
                return DirectionDistanceMaxDisplayMeters;

            Unity.Mathematics.double3 delta = coreAup.ToAbsoluteDouble3() - playerAup.ToAbsoluteDouble3();
            double ax = Math.Abs(delta.x);
            double ay = Math.Abs(delta.y);
            double az = Math.Abs(delta.z);
            double max = Math.Max(ax, Math.Max(ay, az));
            double min = Math.Min(ax, Math.Min(ay, az));
            double mid = ax + ay + az - max - min;
            double estimatedMeters = max + (mid * 0.375d) + (min * 0.25d);
            if (estimatedMeters >= DirectionDistanceMaxDisplayMeters)
                return DirectionDistanceMaxDisplayMeters;

            int roundedMeters = (int)(estimatedMeters + 0.5d);
            int step = roundedMeters < DirectionDistanceNearThresholdMeters
                ? DirectionDistanceNearStepMeters
                : roundedMeters < DirectionDistanceMidThresholdMeters
                    ? DirectionDistanceMidStepMeters
                    : DirectionDistanceFarStepMeters;

            int quantizedMeters = ((roundedMeters + (step >> 1)) / step) * step;
            return quantizedMeters > DirectionDistanceMaxDisplayMeters
                ? DirectionDistanceMaxDisplayMeters
                : quantizedMeters;
        }

        private static int GetCompassDirectionIndex(Vector3 dir)
        {
            float x = dir.x;
            float z = dir.z;
            float horizontalSq = (x * x) + (z * z);
            if (horizontalSq < 0.001f)
                return dir.y > 0 ? 0 : 1;

            float absX = math.abs(x);
            float absZ = math.abs(z);
            bool east = x >= 0f;
            bool north = z >= 0f;

            if (absX <= absZ * CompassOctantAxisRatio)
                return north ? 2 : 6;

            if (absZ <= absX * CompassOctantAxisRatio)
                return east ? 4 : 8;

            return north
                ? east ? 3 : 9
                : east ? 5 : 7;
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

        private void SetLabelText(TextMeshProUGUI label, string value)
        {
            if (label == null)
                return;

            int length = CopyStringToBuffer(value, _labelTextBuffer);
            SetBufferText(label, _labelTextBuffer, length);
        }

        private static void SetNumericText(TextMeshProUGUI label, char[] destination, char[] template, LocNumericArg value0)
        {
            if (label == null || destination == null || template == null)
                return;

            if (!LocNumericBuffer.TryWrite(new ReadOnlySpan<char>(template), destination.AsSpan(), value0, out int length))
                length = 0;

            SetBufferText(label, destination, length);
        }

        private static void SetNumericText(TextMeshProUGUI label, char[] destination, char[] template, LocNumericArg value0, LocNumericArg value1)
        {
            if (label == null || destination == null || template == null)
                return;

            if (!LocNumericBuffer.TryWrite(new ReadOnlySpan<char>(template), destination.AsSpan(), value0, value1, out int length))
                length = 0;

            SetBufferText(label, destination, length);
        }

        private static void SetBufferText(TextMeshProUGUI label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = math.clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static int Append(char[] buffer, int index, string value)
        {
            if (buffer == null || string.IsNullOrEmpty(value) || index >= buffer.Length)
                return math.clamp(index, 0, buffer != null ? buffer.Length : 0);

            if (index < 0)
                index = 0;

            int length = math.min(value.Length, buffer.Length - index);
            value.AsSpan(0, length).CopyTo(buffer.AsSpan(index));
            return index + length;
        }

        private static int CopyStringToBuffer(string value, char[] buffer)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return 0;

            int length = math.min(value.Length, buffer.Length);
            value.AsSpan(0, length).CopyTo(buffer.AsSpan());
            return length;
        }

        private static int Append(char[] buffer, int index, char value)
        {
            if (buffer == null)
                return 0;

            if (index < 0)
                index = 0;
            if (index >= buffer.Length)
                return buffer.Length;

            buffer[index] = value;
            return index + 1;
        }

        private static int AppendInt(char[] buffer, int index, int value)
        {
            if (buffer == null)
                return 0;

            if (index < 0)
                index = 0;
            if (index >= buffer.Length)
                return buffer.Length;

            if (!value.TryFormat(buffer.AsSpan(index), out int written))
                return index;

            return index + written;
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
