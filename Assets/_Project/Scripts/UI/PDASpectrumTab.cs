// ============================================================================
// HECTON-8 — PDASpectrumTab.cs
// Vkladka PDA: SPECTRUM — upravlenie rezhimami vizora.
//
// LOR (lor2 Razdel 9):
//   SPECTRUM: Teplovizor, Sonar, Eholot.
//   Interfeys: vektornye elementy, monoshirinnye shrifty, HDR-tsveta.
//
// ARHITEKTURA:
//   • Protsedurnyy UI — 4 knopki rezhimov + status tekuschego.
//   • Slushaet SpectrumEvents dlya obnovleniya aktivnoy knopki.
//   • Pokazyvaet status sonara (posledniy puls, radius).
// ============================================================================

using Hecton8.Environment;
using Hecton8.Core;
using Hecton8.Visor;
using Hecton8.Gameplay;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Spectrum Tab")]
    public sealed class PDASpectrumTab : MonoBehaviour, IPDAEventListener, ISpectrumModeEventListener, ISonarSnapshotEventListener, IBiomeMatrixEventListener
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
        [Tooltip("Shrift s kirillitsey. Esli null — ispolzuetsya TMP default.")]
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
        private PDAMapTab _mapTab;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        // COLD ALLOC: char[512] — spectrum diagnostic line assembly buffer — owner: PDASpectrumTab
        private readonly char[] _lineBuffer = new char[512];
        private int _lineLength;

        private static readonly string[] ModeNames =
        {
            "NORMALNYY",
            "TEPLOVIZOR",
            "SONAR",
            "EHOLOT"
        };

        private static readonly string[] ModeDescriptions =
        {
            "Standartnyy rezhim vizora. Bez modifikatsiy.",
            "Teplovye signatury suschestv i oborudovaniya.\nObnaruzhenie cherez steny i tuman.",
            "Dvizhenie v radiuse 100m.\nNe pokazyvaet chto — tolko chto est.\nPuls kazhdye 3 sekundy.",
            "Biomehanicheskie signatury.\nObnaruzhenie dronov Atlas-6.\nTrebuet apgreyda sensorov."
        };

        private static readonly string[] ActiveModeLabels =
        {
            "AKTIVNYY REZhIM: NORMALNYY",
            "AKTIVNYY REZhIM: TEPLOVIZOR",
            "AKTIVNYY REZhIM: SONAR",
            "AKTIVNYY REZhIM: EHOLOT"
        };

        private static readonly string[] ModeButtonObjectNames =
        {
            "ModeBtn_0",
            "ModeBtn_1",
            "ModeBtn_2",
            "ModeBtn_3"
        };

        private const string SonarActiveStatus = "SONAR AKTIVEN — RADIUS: 100M";

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

            SpectrumEvents.RegisterModeListener(this);
            SpectrumEvents.RegisterSonarSnapshotListener(this);
            PDAEvents.Register(this);
            BiomeMatrixEvents.Register(this);

            RefreshModeDisplay();
        }

        private void OnDisable()
        {
            SpectrumEvents.UnregisterModeListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            PDAEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterModeListener(this);
            SpectrumEvents.UnregisterSonarSnapshotListener(this);
            PDAEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
            PDAEvents.AssertUnregistered(this, nameof(PDASpectrumTab));
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
        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile) => RefreshModeDisplay();
        private void HandleDepthTierChanged(int tier, float depthMeters) => RefreshModeDisplay();

        void ISpectrumModeEventListener.OnSpectrumModeChanged(SpectrumMode mode)
        {
            HandleModeChanged(mode);
        }

        void ISonarSnapshotEventListener.OnSonarSnapshotUpdated(in SpatialSonarSnapshot snapshot)
        {
            HandleSonarSnapshotUpdated(snapshot);
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HandleMatrixBiomeChanged(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
            HandleDepthTierChanged(depthTier, depthMeters);
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType == PDAEventType.Opened)
                HandlePDAOpened(payload.CurrentTab);
        }

        // ══════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _labelFont = LocalizedFontResolver.ResolveReadableFont(_labelFont);

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
            title.SetText("SPECTRUM — UPRAVLENIE VIZOROM");
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

                RectTransform btn = CreateRect(ModeButtonObjectNames[i], root);
                Anchor(btn,
                    new Vector2(xMin, 0.45f + (1 - row) * 0.25f),
                    new Vector2(xMax, 0.45f + (1 - row) * 0.25f + 0.22f),
                    new Vector2(8, 0), new Vector2(-8, 0));

                Image btnBg = btn.gameObject.AddComponent<Image>();
                btnBg.color = colorInactive;

                TextMeshProUGUI modeLabel = CreateText("ModeLabel", btn, 12f, colorText, TextAlignmentOptions.Midline);
                modeLabel.fontStyle = FontStyles.Bold;
                modeLabel.SetText(ModeNames[i]);
                Anchor(modeLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1),
                    new Vector2(8, 0), new Vector2(-8, 0));

                TextMeshProUGUI descLabel = CreateText("Desc", btn, 8.5f, colorDim, TextAlignmentOptions.TopLeft);
                descLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
                descLabel.SetText(ModeDescriptions[i]);
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

            RectTransform mapViewport = CreateRect("SonarMapViewport", panel);
            Anchor(mapViewport, new Vector2(0f, 0f), new Vector2(0.48f, 1f),
                new Vector2(12f, 12f), new Vector2(-8f, -12f));

            Image mapViewportFrame = mapViewport.gameObject.AddComponent<Image>();
            mapViewportFrame.color = new Color(0.03f, 0.08f, 0.08f, 0.94f);
            _mapTab = mapViewport.gameObject.GetComponent<PDAMapTab>();
            if (_mapTab == null)
                _mapTab = mapViewport.gameObject.AddComponent<PDAMapTab>();

            _currentModeLabel = CreateText("CurrentMode", panel, 11f, colorAccent, TextAlignmentOptions.TopLeft);
            _currentModeLabel.fontStyle = FontStyles.Bold;
            Anchor(_currentModeLabel.rectTransform, new Vector2(0.52f, 1), new Vector2(1, 1),
                new Vector2(12, -36), new Vector2(-12, -8));

            _statusLabel = CreateText("Status", panel, 10f, colorText, TextAlignmentOptions.TopLeft);
            _statusLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Anchor(_statusLabel.rectTransform, new Vector2(0.52f, 1), new Vector2(1, 1),
                new Vector2(12, -88), new Vector2(-12, -44));

            _sonarStatusLabel = CreateText("SonarStatus", panel, 9f, colorDim, TextAlignmentOptions.BottomLeft);
            Anchor(_sonarStatusLabel.rectTransform, new Vector2(0.52f, 0), new Vector2(1, 0),
                new Vector2(12, 84), new Vector2(-12, 104));

            _contactSummaryLabel = CreateText("ContactSummary", panel, 8.5f, colorAccent, TextAlignmentOptions.BottomLeft);
            Anchor(_contactSummaryLabel.rectTransform, new Vector2(0.52f, 0), new Vector2(1, 0),
                new Vector2(12, 60), new Vector2(-12, 80));

            _resourceSummaryLabel = CreateText("ResourceSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_resourceSummaryLabel.rectTransform, new Vector2(0.52f, 0), new Vector2(1, 0),
                new Vector2(12, 40), new Vector2(-12, 60));

            _bioformSummaryLabel = CreateText("BioformSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_bioformSummaryLabel.rectTransform, new Vector2(0.52f, 0), new Vector2(1, 0),
                new Vector2(12, 20), new Vector2(-12, 40));

            _signalSummaryLabel = CreateText("SignalSummary", panel, 8.5f, colorText, TextAlignmentOptions.BottomLeft);
            Anchor(_signalSummaryLabel.rectTransform, new Vector2(0.52f, 0), new Vector2(1, 0),
                new Vector2(12, 0), new Vector2(-12, 20));
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshModeDisplay()
        {
            SpectrumSystem sys = GlobalRegistry.Spectrum;
            SpectrumMode active = sys != null ? sys.CurrentMode : SpectrumMode.Normal;

            // Obnovlyaem knopki
            for (int i = 0; i < _modeButtons.Length; i++)
            {
                ModeButton mb = _modeButtons[i];
                if (mb.Background == null) continue;
                mb.Background.color = mb.Mode == active ? colorActive : colorInactive;
                if (mb.ModeLabel != null)
                    mb.ModeLabel.color = mb.Mode == active ? colorAccent : colorText;
            }

            // Obnovlyaem status
            int idx = (int)active;
            SetLabelText(_currentModeLabel, ActiveModeLabels[idx]);
            RefreshStatusLabel(idx);
            if (active == SpectrumMode.Sonar && IsEmpSensorBlindActive())
            {
                SetLabelText(_sonarStatusLabel, "SONAR OFFLINE // EMP BLIND");
                SetLabelText(_contactSummaryLabel, "CONTACTS // RES 0 | BIO 0 | SIG 0");
                SetLabelText(_resourceSummaryLabel, "NEAREST RESOURCE // NONE");
                SetLabelText(_bioformSummaryLabel, "NEAREST BIOFORM // NONE");
                SetLabelText(_signalSummaryLabel, "LAST LOSS // SENSOR JAM");
            }
            else if (active == SpectrumMode.Sonar && sys != null && sys.HasSonarSnapshot)
            {
                RefreshSonarSnapshot(sys.LastSonarSnapshot);
            }
            else if (active == SpectrumMode.Sonar)
            {
                SetLabelText(_sonarStatusLabel, "SONAR ACTIVE // AWAITING PULSE");
                SetLabelText(_contactSummaryLabel, "CONTACTS // RES 0 | BIO 0 | SIG 0");
                SetLabelText(_resourceSummaryLabel, "NEAREST RESOURCE // NONE");
                SetLabelText(_bioformSummaryLabel, "NEAREST BIOFORM // NONE");
                RefreshLastLossLabel();
            }
            else
            {
                RefreshBiomeDiagnostics();
            }
        }

        private void RefreshStatusLabel(int modeIndex)
        {
            if (!TryResolveBiomeData(out BiomeMatrixDirector biomeDirector, out HectonBiomeMatrixProfile matrixProfile, out HectonBiomeProfile _, out AtmosphereProfile _))
            {
                SetLabelText(_statusLabel, ModeDescriptions[modeIndex]);
                return;
            }

            ClearLine();
            Append(ModeDescriptions[modeIndex]);
            Append('\n');
            Append("BIOME // ");
            AppendBiomeName(matrixProfile);
            Append(" // DEPTH ");
            AppendDistance((int)math.round(biomeDirector.CurrentDepthMeters));
            SetLineText(_statusLabel);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void ActivateMode(SpectrumMode mode)
        {
            SpectrumSystem sys = GlobalRegistry.Spectrum;
            if (sys != null)
                sys.SetMode(mode);
        }

        private void RefreshSonarSnapshot(SpatialSonarSnapshot snapshot)
        {
            SetLabelText(_sonarStatusLabel, "SONAR ACTIVE // GRID LOCKED");

            ClearLine();
            Append("CONTACTS // RES ");
            AppendInt(snapshot.ResourceCount);
            Append(" | BIO ");
            AppendInt(snapshot.BioformCount);
            Append(" | SIG ");
            AppendInt(snapshot.SignalCount);
            SetLineText(_contactSummaryLabel);

            SetDistanceLabel(_resourceSummaryLabel, "NEAREST RESOURCE // ", snapshot.HasNearestResource, snapshot.NearestResourceDistanceMeters, "NEAREST RESOURCE // NONE");
            SetDistanceLabel(_bioformSummaryLabel, "NEAREST BIOFORM // ", snapshot.HasNearestBioform, snapshot.NearestBioformDistanceMeters, "NEAREST BIOFORM // NONE");
            SetSignalDistanceLabel(snapshot);
        }

        private void RefreshBiomeDiagnostics()
        {
            if (!TryResolveBiomeData(out BiomeMatrixDirector biomeDirector, out HectonBiomeMatrixProfile matrixProfile, out HectonBiomeProfile visualProfile, out AtmosphereProfile atmosphereProfile))
            {
                SetLabelText(_sonarStatusLabel, "BIOME // OFFLINE");
                SetLabelText(_contactSummaryLabel, "MATRIX // UNRESOLVED");
                SetLabelText(_resourceSummaryLabel, "TURBIDITY // N/A");
                SetLabelText(_bioformSummaryLabel, "ABSORPTION RGB // N/A");
                RefreshLastLossLabel();
                return;
            }

            ClearLine();
            Append("MATRIX // ");
            AppendBiomeName(matrixProfile);
            Append(" // TIER ");
            AppendInt(biomeDirector.CurrentDepthTier);
            SetLineText(_sonarStatusLabel);

            ClearLine();
            Append("DEPTH // ");
            AppendDistance((int)math.round(biomeDirector.CurrentDepthMeters));
            Append(" // MATRIX ");
            AppendInt(math.max(0, matrixProfile.matrixIndex));
            SetLineText(_contactSummaryLabel);

            if (visualProfile != null)
            {
                ClearLine();
                Append("TURBIDITY // ");
                AppendTenths(visualProfile.turbidityMultiplier);
                SetLineText(_resourceSummaryLabel);

                ClearLine();
                Append("ABSORPTION RGB // ");
                AppendTenths(visualProfile.depthFogDensity.x);
                Append(" / ");
                AppendTenths(visualProfile.depthFogDensity.y);
                Append(" / ");
                AppendTenths(visualProfile.depthFogDensity.z);
                SetLineText(_bioformSummaryLabel);
            }
            else
            {
                SetLabelText(_resourceSummaryLabel, "TURBIDITY // N/A");
                SetLabelText(_bioformSummaryLabel, "ABSORPTION RGB // N/A");
            }

            if (atmosphereProfile != null)
            {
                ClearLine();
                Append("THERMAL // ");
                AppendSignedTenths(atmosphereProfile.temperature);
                Append(" C // RAD ");
                AppendTenths(atmosphereProfile.radiation);
                AppendLastLossSuffix();
                SetLineText(_signalSummaryLabel);
            }
            else
            {
                RefreshLastLossLabel();
            }
        }

        private void SetDistanceLabel(TextMeshProUGUI label, string prefix, bool hasDistance, int distanceMeters, string emptyValue)
        {
            if (!hasDistance)
            {
                SetLabelText(label, emptyValue);
                return;
            }

            ClearLine();
            Append(prefix);
            AppendDistance(distanceMeters);
            SetLineText(label);
        }

        private void SetSignalDistanceLabel(SpatialSonarSnapshot snapshot)
        {
            if (TryAppendLastLossLabel())
                return;

            if (!snapshot.HasNearestSignal)
            {
                SetLabelText(_signalSummaryLabel, "NEAREST SIGNAL // NONE");
                return;
            }

            ClearLine();
            Append("NEAREST SIGNAL // ");
            Append(ResolveSignalRoleLabel(snapshot.NearestSignalRole));
            Append(' ');
            AppendDistance(snapshot.NearestSignalDistanceMeters);
            SetLineText(_signalSummaryLabel);
        }

        private void RefreshLastLossLabel()
        {
            if (!TryAppendLastLossLabel())
                SetLabelText(_signalSummaryLabel, "LAST LOSS // NONE");
        }

        private static bool TryResolveBiomeData(
            out BiomeMatrixDirector biomeDirector,
            out HectonBiomeMatrixProfile matrixProfile,
            out HectonBiomeProfile visualProfile,
            out AtmosphereProfile atmosphereProfile)
        {
            biomeDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            matrixProfile = biomeDirector != null ? biomeDirector.CurrentProfile : null;
            visualProfile = matrixProfile != null ? matrixProfile.runtimeVisualProfile : null;
            atmosphereProfile = matrixProfile != null && matrixProfile.familyProfile != null
                ? matrixProfile.familyProfile.atmosphereProfile
                : null;
            return biomeDirector != null && matrixProfile != null;
        }

        private bool TryResolveSurvivalSystem(out HectonSurvivalSystem survival)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerMovement != null)
                _playerMovement = playerContext.PlayerMovement;

            if (_survivalSystem != null)
            {
                survival = _survivalSystem;
                return true;
            }

            if (playerContext != null &&
                playerContext.PlayerObject != null)
            {
                playerContext.PlayerObject.TryGetComponent(out _survivalSystem);
            }

            survival = _survivalSystem;
            return survival != null;
        }

        private void AppendLastLossSuffix()
        {
            if (!TryResolveSurvivalSystem(out HectonSurvivalSystem survival) ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup) ||
                !survival.TryGetLastDeathRecord(out SurvivalDeathRecord record))
            {
                return;
            }

            Append(" // LOSS ");
            Append(ResolveDeathCauseTag(record.Cause));
            Append(' ');
            AppendDistance(ResolveRoundedApproximateAupDistanceMeters(in playerAup, record.Position));
        }

        private bool TryAppendLastLossLabel()
        {
            if (!TryResolveSurvivalSystem(out HectonSurvivalSystem survival) ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup) ||
                !survival.TryGetLastDeathRecord(out SurvivalDeathRecord record))
            {
                return false;
            }

            ClearLine();
            Append("LAST LOSS // ");
            Append(ResolveDeathCauseTag(record.Cause));
            Append(' ');
            AppendDistance(ResolveRoundedApproximateAupDistanceMeters(in playerAup, record.Position));
            SetLineText(_signalSummaryLabel);
            return true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;
            }

            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static int ResolveRoundedApproximateAupDistanceMeters(in AbsoluteUniversePosition fromAup, Vector3 toRuntimePosition)
        {
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntimePosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in fromAup, in toAup);
            float approximateMeters = ApproximateDistanceMetersFromSq(distanceSq);
            return approximateMeters >= int.MaxValue ? int.MaxValue : (int)math.round(approximateMeters);
        }

        private static float ApproximateDistanceMetersFromSq(double distanceSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return float.PositiveInfinity;
            if (distanceSq <= 0d)
                return 0f;

            float clampedSq = (float)math.min(distanceSq, (double)float.MaxValue);
            uint estimateBits = (math.asuint(clampedSq) >> 1) + 0x1FC00000u;
            float estimate = math.asfloat(estimateBits);
            return 0.5f * (estimate + (clampedSq / math.max(estimate, 0.0001f)));
        }

        private void AppendInt(int value)
        {
            int clampedValue = math.clamp(value, 0, HudNumericStringCache.MaxIntegerValue);
            Append(HudNumericStringCache.IntStrings[clampedValue]);
        }

        private void AppendDistance(int distanceMeters)
        {
            int clampedDistance = math.clamp(distanceMeters, 0, HudNumericStringCache.MaxIntegerValue);
            Append(HudNumericStringCache.IntStrings[clampedDistance]);
            Append('M');
        }

        private void AppendTenths(float value)
        {
            int roundedTenths = math.abs((int)math.round(value * 10f));
            int maxHudTenths = HudNumericStringCache.MaxIntegerValue * 10 + 9;
            int clampedTenths = math.clamp(roundedTenths, 0, maxHudTenths);
            Append(HudNumericStringCache.IntStrings[clampedTenths / 10]);
            Append('.');
            Append(HudNumericStringCache.IntStrings[clampedTenths % 10]);
        }

        private void AppendSignedTenths(float value)
        {
            int roundedTenths = (int)math.round(value * 10f);
            if (roundedTenths < 0)
            {
                Append('-');
                roundedTenths = -roundedTenths;
            }

            int maxHudTenths = HudNumericStringCache.MaxIntegerValue * 10 + 9;
            int clampedTenths = math.clamp(roundedTenths, 0, maxHudTenths);
            Append(HudNumericStringCache.IntStrings[clampedTenths / 10]);
            Append('.');
            Append(HudNumericStringCache.IntStrings[clampedTenths % 10]);
        }

        private void AppendBiomeName(HectonBiomeMatrixProfile matrixProfile)
        {
            if (matrixProfile == null || string.IsNullOrEmpty(matrixProfile.biomeName))
            {
                Append("UNRESOLVED");
                return;
            }

            Append(matrixProfile.biomeName);
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
                case FieldTargetRole.DistressBeacon:
                    return "DISTRESS";
                default:
                    return "SIGNAL";
            }
        }

        // ══════════════════════════════════════════════════════════
        //  NESTED TYPES
        // ══════════════════════════════════════════════════════════

        private static bool IsEmpSensorBlindActive()
        {
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.TraumaDispatcher == null)
            {
                return false;
            }

            return runtimeContext.TraumaDispatcher.IsEmpSensorBlindActive;
        }

        private static string ResolveDeathCauseTag(SurvivalDeathCause cause)
        {
            switch (cause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return "O2";
                case SurvivalDeathCause.PressureCollapse:
                    return "PRESS";
                case SurvivalDeathCause.ThermalFailure:
                    return "THERM";
                case SurvivalDeathCause.RadiationExposure:
                    return "RAD";
                case SurvivalDeathCause.Starvation:
                    return "HUNGER";
                case SurvivalDeathCause.Dehydration:
                    return "THIRST";
                case SurvivalDeathCause.IntegrityFailure:
                    return "HULL";
                default:
                    return "LOSS";
            }
        }

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
                SpectrumSystem sys = GlobalRegistry.Spectrum;
                bool isActive = sys != null && sys.CurrentMode == _mode;
                if (_bg != null && !isActive) _bg.color = _hover;
            }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            {
                SpectrumSystem sys = GlobalRegistry.Spectrum;
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
            if (label != null)
                label.SetText(value);
        }

        private void ClearLine()
        {
            _lineLength = 0;
        }

        private void SetLineText(TextMeshProUGUI label)
        {
            if (label == null)
                return;

            label.SetCharArray(_lineBuffer, 0, math.clamp(_lineLength, 0, _lineBuffer.Length));
        }

        private void Append(string value)
        {
            if (string.IsNullOrEmpty(value) || _lineLength >= _lineBuffer.Length)
                return;

            int copyLength = math.min(value.Length, _lineBuffer.Length - _lineLength);
            for (int i = 0; i < copyLength; i++)
                _lineBuffer[_lineLength + i] = value[i];
            _lineLength += copyLength;
        }

        private void Append(char value)
        {
            if (_lineLength >= _lineBuffer.Length)
                return;

            _lineBuffer[_lineLength] = value;
            _lineLength++;
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
