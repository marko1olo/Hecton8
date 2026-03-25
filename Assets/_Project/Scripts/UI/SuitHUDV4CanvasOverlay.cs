using Hecton8.Environment;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/UI/Suit HUD V4 Canvas Overlay")]
    [RequireComponent(typeof(Canvas))]
    public sealed class SuitHUDV4CanvasOverlay : MonoBehaviour
    {
        public enum RenderPath
        {
            ScreenOverlay,
            ProjectionSource
        }

        private const int LayoutRevision = 5;
        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera projectionCamera;
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private HectonSurvivalSystem survival;
        [SerializeField] private HectonPlayerMovement playerMovement;
        [SerializeField] private PlayerFlashlight flashlight;
        [SerializeField] private HectonUnderwaterVisuals underwaterVisuals;
        [SerializeField] private SuitHUDProfile defaultHudProfile;

        [Header("Presentation")]
        [SerializeField] private bool keepVisibleInEditMode = true;
        [SerializeField] private RenderPath renderPath = RenderPath.ScreenOverlay;
        [SerializeField] [Range(0.85f, 1.2f)] private float overallScale = 0.98f;
        [SerializeField] [Range(0f, 1f)] private float chromeAlpha = 0.14f;
        [SerializeField] private float projectionPlaneDistance = 1f;
        [SerializeField] private int overlaySortingOrder = 140;

        [Header("Layout Controls")]
        [SerializeField] private Vector2 headerOffset = new Vector2(0f, -34f);
        [SerializeField] private Vector2 telemetryOffset = new Vector2(-226f, 126f);
        [SerializeField] private Vector2 telemetrySize = new Vector2(184f, 124f);
        [SerializeField] private Vector2 gaugeClusterOffset = new Vector2(116f, 110f);
        [SerializeField] private Vector2 gaugeClusterSize = new Vector2(300f, 128f);
        [SerializeField] private Vector2 statusOffset = new Vector2(0f, 50f);
        [SerializeField] private Vector2 reticleOffset = Vector2.zero;

        private const string RootName = "HUD_V4_CanvasRoot";

        private RectTransform _root;
        private RectTransform _headerRoot;
        private RectTransform _telemetryRoot;
        private RectTransform _gaugeClusterRoot;
        private Image _topVeil;
        private Image _bottomVeil;
        private Image _leftVeil;
        private Image _rightVeil;
        private Image _headerLine;
        private Image _headerWingLeft;
        private Image _headerWingRight;
        private Image _footerLine;
        private Image _statusRuleLeft;
        private Image _statusRuleRight;
        private Image _reticleH;
        private Image _reticleV;
        private Image _reticleBracketLeft;
        private Image _reticleBracketRight;
        private Image _telemetryRule;
        private Image _telemetryBraceUpper;
        private Image _telemetryBraceLower;

        private TextMeshProUGUI _suitLabel;
        private TextMeshProUGUI _headingLabel;
        private TextMeshProUGUI _depthLabel;
        private TextMeshProUGUI _temperatureLabel;
        private TextMeshProUGUI _pressureLabel;
        private TextMeshProUGUI _statusLabel;

        private GaugeRefs _oxygenGauge;
        private GaugeRefs _powerGauge;
        private GaugeRefs _healthGauge;

        private SuitData _activeSuit;
        private SuitHUDProfile _activeProfile;
        private float _displayTemperature = 8f;
        private float _lastDepth;
        private bool _layoutBuilt;
        [SerializeField, HideInInspector] private int _appliedLayoutRevision;

        private struct GaugeRefs
        {
            public RectTransform Root;
            public TextMeshProUGUI Ring;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Sub;
        }

        private void OnEnable()
        {
            _layoutBuilt = false;
            RefreshAll(0.016f);
        }

        private void OnDisable()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying && !keepVisibleInEditMode)
                return;

            RefreshAll(Application.isPlaying ? Time.deltaTime : 0.016f);
        }

        private void RefreshAll(float dt)
        {
            AutoResolve();
            NormalizeCanvas();
            EnsureHierarchy();
            RefreshVisuals(dt);
        }

        private void AutoResolve()
        {
            if (targetCanvas == null)
                targetCanvas = GetComponent<Canvas>();

            if (projectionCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera candidate = cameras[i];
                    if (candidate != null && candidate.name == "HUD_Render_Camera")
                    {
                        projectionCamera = candidate;
                        break;
                    }
                }
            }

            if (uiFont == null || uiFont.name.Contains("циф") || uiFont.name.Contains("Digit"))
                uiFont = TMP_Settings.defaultFontAsset;

            if (survival == null)
                survival = FindFirstObjectByType<HectonSurvivalSystem>();

            if (playerMovement == null)
                playerMovement = FindFirstObjectByType<HectonPlayerMovement>();

            if (flashlight == null)
                flashlight = FindFirstObjectByType<PlayerFlashlight>();

            if (underwaterVisuals == null)
                underwaterVisuals = FindFirstObjectByType<HectonUnderwaterVisuals>();
        }

        private void NormalizeCanvas()
        {
            if (targetCanvas == null)
                return;

            if (renderPath == RenderPath.ProjectionSource && projectionCamera != null)
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                targetCanvas.worldCamera = projectionCamera;
                targetCanvas.planeDistance = projectionPlaneDistance;
                targetCanvas.overrideSorting = false;
                targetCanvas.sortingOrder = 0;
            }
            else
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.worldCamera = null;
                targetCanvas.overrideSorting = true;
                targetCanvas.sortingOrder = overlaySortingOrder;
            }

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.localScale = Vector3.one;
            }

            CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            HectonSuitHUD legacyHud = targetCanvas.GetComponent<HectonSuitHUD>();
            if (legacyHud != null)
                legacyHud.enabled = false;

        }

        private void EnsureHierarchy()
        {
            if (targetCanvas == null)
                return;

            if (_root == null)
                _root = targetCanvas.transform.Find(RootName) as RectTransform;

            if (_root == null)
            {
                _root = CreateRect(RootName, targetCanvas.transform as RectTransform);
                Stretch(_root, 0f, 0f, 0f, 0f);
                _root.SetAsLastSibling();
                _layoutBuilt = false;
            }

            if (_root != null && !_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);

            if (_appliedLayoutRevision != LayoutRevision)
            {
                _layoutBuilt = false;
                _appliedLayoutRevision = LayoutRevision;
            }

            if (_layoutBuilt)
                return;

            ClearChildren(_root);

            _topVeil = CreateImage("TopVeil", _root, new Color(0f, 0f, 0f, 0.08f));
            _topVeil.rectTransform.anchorMin = new Vector2(0f, 1f);
            _topVeil.rectTransform.anchorMax = new Vector2(1f, 1f);
            _topVeil.rectTransform.pivot = new Vector2(0.5f, 1f);
            _topVeil.rectTransform.sizeDelta = new Vector2(0f, 92f);
            _topVeil.rectTransform.anchoredPosition = Vector2.zero;

            _bottomVeil = CreateImage("BottomVeil", _root, new Color(0f, 0f, 0f, 0.1f));
            _bottomVeil.rectTransform.anchorMin = new Vector2(0f, 0f);
            _bottomVeil.rectTransform.anchorMax = new Vector2(1f, 0f);
            _bottomVeil.rectTransform.pivot = new Vector2(0.5f, 0f);
            _bottomVeil.rectTransform.sizeDelta = new Vector2(0f, 144f);
            _bottomVeil.rectTransform.anchoredPosition = Vector2.zero;

            _leftVeil = CreateImage("LeftVeil", _root, new Color(0f, 0f, 0f, 0.05f));
            Anchor(_leftVeil.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(72f, 0f));

            _rightVeil = CreateImage("RightVeil", _root, new Color(0f, 0f, 0f, 0.05f));
            Anchor(_rightVeil.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(72f, 0f));

            _headerRoot = CreateRect("HeaderRoot", _root);
            Anchor(_headerRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), headerOffset, new Vector2(620f, 84f));

            _headerLine = CreateImage("HeaderLine", _headerRoot, Color.white);
            Anchor(_headerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(260f, 2f));

            _headerWingLeft = CreateImage("HeaderWingLeft", _headerRoot, Color.white);
            Anchor(_headerWingLeft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-204f, 6f), new Vector2(120f, 2f));
            _headerWingLeft.rectTransform.localEulerAngles = Vector3.zero;

            _headerWingRight = CreateImage("HeaderWingRight", _headerRoot, Color.white);
            Anchor(_headerWingRight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(204f, 6f), new Vector2(120f, 2f));
            _headerWingRight.rectTransform.localEulerAngles = Vector3.zero;

            _suitLabel = CreateText("SuitLabel", _headerRoot, 28f, FontStyles.Bold, TextAlignmentOptions.Center, 0.9f);
            Anchor(_suitLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(480f, 36f));

            _headingLabel = CreateText("HeadingLabel", _headerRoot, 15f, FontStyles.Normal, TextAlignmentOptions.Center, 0.62f);
            Anchor(_headingLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(420f, 22f));

            _footerLine = CreateImage("FooterLine", _root, Color.white);
            Anchor(_footerLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(180f, 2f));

            _statusRuleLeft = CreateImage("StatusRuleLeft", _root, Color.white);
            Anchor(_statusRuleLeft.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-148f, 76f), new Vector2(104f, 2f));
            _statusRuleLeft.rectTransform.localEulerAngles = Vector3.zero;

            _statusRuleRight = CreateImage("StatusRuleRight", _root, Color.white);
            Anchor(_statusRuleRight.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(148f, 76f), new Vector2(104f, 2f));
            _statusRuleRight.rectTransform.localEulerAngles = Vector3.zero;

            _reticleH = CreateImage("ReticleH", _root, Color.white);
            Anchor(_reticleH.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), reticleOffset, new Vector2(22f, 2f));

            _reticleV = CreateImage("ReticleV", _root, Color.white);
            Anchor(_reticleV.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), reticleOffset, new Vector2(2f, 22f));

            _reticleBracketLeft = CreateImage("ReticleBracketLeft", _root, Color.white);
            Anchor(_reticleBracketLeft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), reticleOffset + new Vector2(-26f, 0f), new Vector2(10f, 2f));

            _reticleBracketRight = CreateImage("ReticleBracketRight", _root, Color.white);
            Anchor(_reticleBracketRight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), reticleOffset + new Vector2(26f, 0f), new Vector2(10f, 2f));

            Vector2 resolvedTelemetryOffset = ResolveTelemetryOffset();
            Vector2 resolvedTelemetrySize = ResolveTelemetrySize();

            _telemetryRoot = CreateRect("TelemetryRoot", _root);
            Anchor(_telemetryRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), resolvedTelemetryOffset, resolvedTelemetrySize);
            _telemetryRoot.localEulerAngles = Vector3.zero;

            _telemetryRule = CreateImage("TelemetryRule", _telemetryRoot, Color.white);
            Anchor(_telemetryRule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(120f, 2f));

            _telemetryBraceUpper = CreateImage("TelemetryBraceUpper", _telemetryRoot, Color.white);
            Anchor(_telemetryBraceUpper.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 26f), new Vector2(22f, 2f));
            _telemetryBraceUpper.rectTransform.localEulerAngles = Vector3.zero;

            _telemetryBraceLower = CreateImage("TelemetryBraceLower", _telemetryRoot, Color.white);
            Anchor(_telemetryBraceLower.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(18f, 2f));
            _telemetryBraceLower.rectTransform.localEulerAngles = Vector3.zero;

            _depthLabel = CreateText("DepthLabel", _telemetryRoot, 28f, FontStyles.Bold, TextAlignmentOptions.Right, 0.96f);
            Anchor(_depthLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -24f), new Vector2(152f, 34f));

            _temperatureLabel = CreateText("TemperatureLabel", _telemetryRoot, 14f, FontStyles.Normal, TextAlignmentOptions.Right, 0.82f);
            Anchor(_temperatureLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(152f, 20f));

            _pressureLabel = CreateText("PressureLabel", _telemetryRoot, 13f, FontStyles.Normal, TextAlignmentOptions.Right, 0.62f);
            Anchor(_pressureLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 0f), new Vector2(152f, 18f));

            _statusLabel = CreateText("StatusLabel", _root, 16f, FontStyles.Bold, TextAlignmentOptions.Center, 0.84f);
            Anchor(_statusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), statusOffset, new Vector2(420f, 24f));

            _gaugeClusterRoot = CreateRect("GaugeClusterRoot", _root);
            Anchor(_gaugeClusterRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), gaugeClusterOffset, gaugeClusterSize);
            _gaugeClusterRoot.localEulerAngles = Vector3.zero;

            _oxygenGauge = CreateGauge("Gauge_O2", _gaugeClusterRoot, new Vector2(-92f, 0f));
            _powerGauge = CreateGauge("Gauge_PWR", _gaugeClusterRoot, new Vector2(0f, 0f));
            _healthGauge = CreateGauge("Gauge_HLT", _gaugeClusterRoot, new Vector2(92f, 0f));

            _layoutBuilt = true;
        }

        private Vector2 ResolveTelemetryOffset()
        {
            if (renderPath != RenderPath.ProjectionSource)
                return telemetryOffset;

            return new Vector2(Mathf.Min(telemetryOffset.x, -226f), Mathf.Min(telemetryOffset.y, 126f));
        }

        private Vector2 ResolveTelemetrySize()
        {
            if (renderPath != RenderPath.ProjectionSource)
                return telemetrySize;

            return new Vector2(Mathf.Min(telemetrySize.x, 184f), Mathf.Min(telemetrySize.y, 124f));
        }

        private void RefreshVisuals(float dt)
        {
            if (_root == null)
                return;

            ResolveProfile();
            ResolvePalette(out Color primary, out Color secondary, out Color dim, out Color warning);

            float oxygen = survival != null ? Mathf.Clamp01(survival.OxygenNormalized) : 1f;
            float power = survival != null ? Mathf.Clamp01(survival.EnergyNormalized) : 1f;
            float health = survival != null ? Mathf.Clamp01(survival.IntegrityNormalized) : 1f;
            float depth = survival != null ? Mathf.Max(0f, survival.Depth) : (playerMovement != null ? playerMovement.CurrentDepth : 0f);
            float pressure = survival != null ? Mathf.Max(1f, survival.Pressure) : 1f + depth / 10f;
            float heading = playerMovement != null ? Mathf.Repeat(playerMovement.CameraYaw, 360f) : 0f;

            float targetTemp = EstimateTemperature(depth);
            _displayTemperature = Mathf.Lerp(_displayTemperature, targetTemp, 1f - Mathf.Exp(-4f * dt));
            float depthDelta = depth - _lastDepth;
            _lastDepth = depth;

            _root.localScale = Vector3.one * overallScale;

            _topVeil.color = new Color(0.01f, 0.04f, 0.06f, chromeAlpha * 0.45f);
            _bottomVeil.color = new Color(0.01f, 0.04f, 0.06f, chromeAlpha * 0.55f);
            _leftVeil.color = new Color(0.01f, 0.03f, 0.05f, chromeAlpha * 0.16f);
            _rightVeil.color = new Color(0.01f, 0.03f, 0.05f, chromeAlpha * 0.16f);
            _headerLine.color = Alpha(primary, 0.22f);
            _headerWingLeft.color = Alpha(primary, 0.16f);
            _headerWingRight.color = Alpha(primary, 0.16f);
            _footerLine.color = Alpha(primary, 0.18f);
            _statusRuleLeft.color = Alpha(primary, 0.14f);
            _statusRuleRight.color = Alpha(primary, 0.14f);
            _telemetryRule.color = Alpha(primary, 0.18f);
            _telemetryBraceUpper.color = Alpha(primary, 0.18f);
            _telemetryBraceLower.color = Alpha(primary, 0.12f);
            _reticleH.color = Alpha(primary, 0.76f);
            _reticleV.color = Alpha(primary, 0.5f);
            _reticleBracketLeft.color = Alpha(primary, 0.42f);
            _reticleBracketRight.color = Alpha(primary, 0.42f);

            SetText(_suitLabel, ResolveSuitLabel(), Alpha(primary, 0.95f));
            SetText(_headingLabel, "HEADING " + Mathf.RoundToInt(heading).ToString("000") + " / " + ResolveCardinal(heading), Alpha(dim, 0.58f));
            SetText(_depthLabel, "DEPTH: " + Mathf.RoundToInt(depth).ToString("N0") + " m", Alpha(primary, 0.96f));
            SetText(_temperatureLabel, "TEMPERATURE: " + _displayTemperature.ToString("0.0") + " C", Alpha(dim, 0.84f));
            SetText(_pressureLabel, "PRESSURE: " + pressure.ToString("0.0") + " atm", Alpha(dim, 0.64f));
            SetText(_statusLabel, ResolveStatus(oxygen, power, health, depthDelta), PickAccent(oxygen, power, health, primary, warning));

            UpdateGauge(_oxygenGauge, "O2", "LIFE SUPPORT", oxygen, primary, secondary, dim, warning);
            UpdateGauge(_powerGauge, "PWR", "CELL ARRAY", power, primary, secondary, dim, warning);
            UpdateGauge(_healthGauge, "HLT", "HULL", health, primary, secondary, dim, warning);
        }

        private void ResolveProfile()
        {
            _activeSuit = playerMovement != null ? playerMovement.CurrentSuit : null;
            _activeProfile = _activeSuit != null && _activeSuit.HudProfile != null ? _activeSuit.HudProfile : defaultHudProfile;
        }

        private void ResolvePalette(out Color primary, out Color secondary, out Color dim, out Color warning)
        {
            primary = new Color(0.46f, 0.98f, 0.94f, 1f);
            secondary = new Color(0.13f, 0.6f, 0.58f, 1f);
            dim = new Color(0.78f, 0.98f, 0.95f, 1f);
            warning = new Color(1f, 0.74f, 0.22f, 1f);

            if (_activeProfile == null)
                return;

            if (_activeProfile.OverridePalette)
            {
                primary = _activeProfile.PrimaryColor;
                secondary = _activeProfile.SecondaryColor;
                dim = _activeProfile.DimColor;
                warning = _activeProfile.WarningColor;
                return;
            }

            switch (_activeProfile.Style)
            {
                case SuitHUDProfile.VisualStyle.CyanVisor:
                    primary = new Color(0.26f, 0.98f, 1f, 1f);
                    secondary = new Color(0.14f, 0.58f, 0.74f, 1f);
                    dim = new Color(0.7f, 0.95f, 1f, 1f);
                    warning = new Color(1f, 0.76f, 0.24f, 1f);
                    break;

                case SuitHUDProfile.VisualStyle.AtlasIndustrial:
                    primary = new Color(0.62f, 0.94f, 1f, 1f);
                    secondary = new Color(0.28f, 0.56f, 0.7f, 1f);
                    dim = new Color(0.86f, 0.95f, 1f, 1f);
                    warning = new Color(1f, 0.68f, 0.22f, 1f);
                    break;
            }
        }

        private float EstimateTemperature(float depth)
        {
            float depth01 = Mathf.Clamp01(depth / 1450f);
            float estimated = Mathf.Lerp(13.5f, 2.4f, depth01 * depth01 * (3f - 2f * depth01));
            if (underwaterVisuals != null)
                estimated -= (1f - underwaterVisuals.CurrentLightFactor) * 0.8f;
            return estimated;
        }

        private string ResolveSuitLabel()
        {
            if (_activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.DisplayNameOverride))
                return _activeProfile.DisplayNameOverride.ToUpperInvariant();

            if (_activeSuit != null)
                return _activeSuit.name.Replace('_', ' ').ToUpperInvariant();

            return "EXPEDITION SUIT";
        }

        private static string ResolveCardinal(float heading)
        {
            if (heading >= 315f || heading < 45f) return "N";
            if (heading < 135f) return "E";
            if (heading < 225f) return "S";
            return "W";
        }

        private static string ResolveTrend(float delta)
        {
            if (delta > 0.04f) return "DESCENDING";
            if (delta < -0.04f) return "ASCENDING";
            return "STABLE";
        }

        private string ResolveStatus(float oxygen, float power, float health, float depthDelta)
        {
            if (health <= 0.18f)
                return "HULL INTEGRITY CRITICAL";
            if (oxygen <= 0.2f)
                return "OXYGEN RESERVE LOW";
            if (power <= 0.2f)
                return "POWER CELLS LOW";
            if (flashlight != null && flashlight.IsOverheated)
                return "LAMP THERMAL LIMIT";
            if (PlayerPDA.IsOpen)
                return "SUIT LINK ROUTING PDA";
            return ResolveTrend(depthDelta);
        }

        private static Color PickAccent(float oxygen, float power, float health, Color primary, Color warning)
        {
            if (oxygen <= 0.2f || power <= 0.2f || health <= 0.18f)
                return Alpha(warning, 0.94f);

            return Alpha(primary, 0.88f);
        }

        private static void UpdateGauge(GaugeRefs gauge, string label, string subLabel, float normalized, Color primary, Color secondary, Color dim, Color warning)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f);
            Color accent = percent <= 20 ? warning : primary;

            gauge.Root.localScale = Vector3.one;
                        gauge.Ring.text = "O";
            gauge.Ring.color = Alpha(accent, 0.78f);
            gauge.Label.text = label;
            gauge.Label.color = Alpha(dim, 0.84f);
            gauge.Value.text = percent + "%";
            gauge.Value.color = Alpha(accent, 0.96f);
            gauge.Sub.text = subLabel;
            gauge.Sub.color = Alpha(secondary, 0.82f);
        }

        private GaugeRefs CreateGauge(string name, RectTransform parent, Vector2 anchoredPosition)
        {
            GaugeRefs refs = new GaugeRefs();

            refs.Root = CreateRect(name, parent);
            Anchor(refs.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(92f, 112f));

            refs.Ring = CreateText(name + "_Ring", refs.Root, 108f, FontStyles.Normal, TextAlignmentOptions.Center, 0.65f);
            Stretch(refs.Ring.rectTransform, 0f, 0f, 0f, 0f);

            refs.Label = CreateText(name + "_Label", refs.Root, 18f, FontStyles.Bold, TextAlignmentOptions.Top, 0.82f);
            Anchor(refs.Label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(84f, 22f));

            refs.Value = CreateText(name + "_Value", refs.Root, 30f, FontStyles.Bold, TextAlignmentOptions.Center, 0.96f);
            Anchor(refs.Value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(84f, 34f));

            refs.Sub = CreateText(name + "_Sub", refs.Root, 11f, FontStyles.Normal, TextAlignmentOptions.Bottom, 0.52f);
            Anchor(refs.Sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(84f, 18f));

            return refs;
        }

        private void ClearChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (parent != null)
                go.layer = parent.gameObject.layer;
            rect.localScale = Vector3.one;
            return rect;
        }

        private Image CreateImage(string name, RectTransform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment, float alpha)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = uiFont;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterSpacing = size >= 36f ? 4f : 1.5f;
            label.color = Alpha(Color.white, alpha);
            label.text = name;
            return label;
        }

        private static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void SetText(TextMeshProUGUI label, string text, Color color)
        {
            if (label == null)
                return;

            label.text = text;
            label.color = color;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        public void SetRenderPathProjectionSource(bool projectionSource)
        {
            RenderPath nextPath = projectionSource ? RenderPath.ProjectionSource : RenderPath.ScreenOverlay;
            if (renderPath == nextPath)
                return;

            renderPath = nextPath;
            _layoutBuilt = false;
        }

        public void SetProjectionCamera(Camera camera)
        {
            if (projectionCamera == camera)
                return;

            projectionCamera = camera;
            _layoutBuilt = false;
        }

        public void CopyConfigurationFrom(SuitHUDV4CanvasOverlay source)
        {
            if (source == null || ReferenceEquals(source, this))
                return;

            uiFont = source.uiFont;
            survival = source.survival;
            playerMovement = source.playerMovement;
            flashlight = source.flashlight;
            underwaterVisuals = source.underwaterVisuals;
            defaultHudProfile = source.defaultHudProfile;
            keepVisibleInEditMode = source.keepVisibleInEditMode;
            overallScale = source.overallScale;
            chromeAlpha = source.chromeAlpha;
            projectionPlaneDistance = source.projectionPlaneDistance;
            overlaySortingOrder = source.overlaySortingOrder;
            headerOffset = source.headerOffset;
            telemetryOffset = source.telemetryOffset;
            telemetrySize = source.telemetrySize;
            gaugeClusterOffset = source.gaugeClusterOffset;
            gaugeClusterSize = source.gaugeClusterSize;
            statusOffset = source.statusOffset;
            reticleOffset = source.reticleOffset;
            _layoutBuilt = false;
        }
    }
}
