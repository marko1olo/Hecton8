using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NASAPunk.Visor;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/UI/Suit HUD V4 Canvas Overlay")]
    [RequireComponent(typeof(Canvas))]
    public sealed class SuitHUDV4CanvasOverlay : MonoBehaviour, ITickable
    {
        private static readonly List<SuitHUDV4CanvasOverlay> s_activeOverlays = new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        private static readonly string[] s_headingLabels = BuildHeadingLabels();
        private const string DefaultSuitLabel = "EXPEDITION SUIT";
        private const string DefaultDepthPattern = "DEPTH: -{0:0} m";
        private const string DefaultTemperaturePattern = "TEMPERATURE: {0:0.0} C";
        private const string DefaultPressurePattern = "PRESSURE: {0:0.0} atm";
        private const string DefaultGaugeO2Label = "O2";
        private const string DefaultGaugePowerLabel = "PWR";
        private const string DefaultGaugeHullLabel = "HULL";
        private const string DefaultPressureLabel = "PRESSURE";
        private const string DefaultTemperatureLabel = "TEMP";
        private const string StatusPressureLimitExceeded = "PRESSURE LIMIT EXCEEDED";
        private const string StatusApproachingSafeDepth = "APPROACHING SAFE DEPTH LIMIT";
        private const string StatusSuitDamageCritical = "SUIT DAMAGE CRITICAL";
        private const string StatusOxygenReserveLow = "OXYGEN RESERVE LOW";
        private const string StatusPowerCellsLow = "POWER CELLS LOW";
        private const string StatusLampThermalLimit = "LAMP THERMAL LIMIT";
        private const string StatusSuitLinkRoutingPda = "SUIT LINK ROUTING PDA";
        private const string StatusLifeSupportNominalStable = "LIFE SUPPORT NOMINAL / STABLE";
        private const string StatusLifeSupportNominalAscending = "LIFE SUPPORT NOMINAL / ASCENDING";
        private const string StatusLifeSupportNominalDescending = "LIFE SUPPORT NOMINAL / DESCENDING";

        public enum RenderPath
        {
            ScreenOverlay,
            ProjectionSource
        }

        private static readonly string[] _cachedUpperStrings = new string[16];

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int hash = input.GetHashCode() & 0xF;
            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

        private const int LayoutRevision = 12;
        private const float AutoResolveRetryInterval = 1f;
        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera projectionCamera;
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;
        [SerializeField] private Texture2D oxygenIconTexture;
        [SerializeField] private Texture2D healthIconTexture;
        [SerializeField] private Texture2D energyIconTexture;
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

        [Header("Stress Pulse")]
        [SerializeField, Range(0f, 1f)] private float stressPulseStartThreshold = 0.24f;
        [SerializeField, Range(0.25f, 4f)] private float stressPulseFrequencyMin = 0.9f;
        [SerializeField, Range(0.25f, 6f)] private float stressPulseFrequencyMax = 2.3f;
        [SerializeField, Range(0f, 0.25f)] private float stressPulseChromeAlphaBoost = 0.09f;
        [SerializeField, Range(0f, 0.35f)] private float stressPulseBrightnessBoost = 0.12f;
        [SerializeField, Range(0f, 0.4f)] private float stressPulseWarningBlend = 0.18f;
        [SerializeField, Range(0.25f, 12f)] private float stressPulseBlendSpeed = 3.4f;

        [Header("Layout Controls")]
        [SerializeField] private Vector2 headerOffset = new Vector2(0f, -34f);
        [SerializeField] private Vector2 telemetryOffset = new Vector2(-226f, 126f);
        [SerializeField] private Vector2 telemetrySize = new Vector2(184f, 124f);
        [SerializeField] private Vector2 gaugeClusterOffset = new Vector2(116f, 110f);
        [SerializeField] private Vector2 gaugeClusterSize = new Vector2(300f, 128f);
        [SerializeField] private Vector2 statusOffset = new Vector2(0f, 50f);
        [SerializeField] private Vector2 reticleOffset = Vector2.zero;

        [Header("Gauge Ring Controls")]
        [SerializeField] private float gaugeColumnSpacing = 82f;
        [SerializeField] private float gaugeRingSize = 54f;
        [SerializeField] private float gaugeRingThickness = 6f;
        [SerializeField] private Vector2 gaugeIconSize = new Vector2(16f, 16f);
        [SerializeField] private float gaugeValueOffsetY = 0f;
        [SerializeField] private float gaugeLabelOffsetY = -34f;

        private const string RootName = "HUD_V4_CanvasRoot";

        private RectTransform _root;
        private CanvasGroup _rootCanvasGroup;
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
        private static Sprite s_ringFillSprite;
        private static Sprite s_ringFrameSprite;
        private static Sprite s_oxygenIconSprite;
        private static Sprite s_healthIconSprite;
        private static Sprite s_energyIconSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeOverlays.Clear();
            s_controllerResolveBuffer.Clear();
            s_ringFillSprite = null;
            s_ringFrameSprite = null;
            s_oxygenIconSprite = null;
            s_healthIconSprite = null;
            s_energyIconSprite = null;
        }

        private SuitData _activeSuit;
        private SuitHUDProfile _activeProfile;
        private SuitHUDProfile _cachedPaletteProfile;
        private Color _cachedPalettePrimary;
        private Color _cachedPaletteSecondary;
        private Color _cachedPaletteDim;
        private Color _cachedPaletteWarning;
        private float _displayTemperature = 8f;
        private float _lastDepth;
        private float _depthMeters;
        private bool _layoutBuilt;
        [SerializeField, HideInInspector] private int _appliedLayoutRevision;
        private float _nextAutoResolveAt;
        private bool _tickRegistered;
        private SuitData _cachedSuitLabelSuit;
        private string _cachedSuitLabelOverride;
        private string _cachedSuitLabelText = DefaultSuitLabel;
        private string _appliedSuitLabelText;
        private Color _appliedSuitLabelColor;
        private string _appliedHeadingLabelText;
        private Color _appliedHeadingLabelColor;
        private string _appliedStatusLabelText;
        private Color _appliedStatusLabelColor;
        private int _appliedDepthValue;
        private bool _hasAppliedDepthValue;
        private Color _appliedDepthColor;
        private int _appliedTemperatureTenths;
        private bool _hasAppliedTemperatureTenths;
        private Color _appliedTemperatureColor;
        private int _appliedPressureTenths;
        private bool _hasAppliedPressureTenths;
        private Color _appliedPressureColor;
        private bool _styleApplied;
        private bool _canvasStateApplied;
        private bool _hasAppliedRootVisibility;
        private bool _appliedRootVisible;
        private float _stressPulseIntensity;
        private float _stressPulsePhase;
        private float _appliedStressPulseStrength = -1f;
        private string _localizedDepthPattern = DefaultDepthPattern;
        private string _localizedTemperaturePattern = DefaultTemperaturePattern;
        private string _localizedPressurePattern = DefaultPressurePattern;
        private GameLanguage _localizedMeasurementLanguage = GameLanguage.English;
        private string _localizedGaugeO2Label = DefaultGaugeO2Label;
        private string _localizedGaugePowerLabel = DefaultGaugePowerLabel;
        private string _localizedGaugeHullLabel = DefaultGaugeHullLabel;
        private string _localizedStatusPressureLimitExceeded = StatusPressureLimitExceeded;
        private string _localizedStatusApproachingSafeDepth = StatusApproachingSafeDepth;
        private string _localizedStatusSuitDamageCritical = StatusSuitDamageCritical;
        private string _localizedStatusOxygenReserveLow = StatusOxygenReserveLow;
        private string _localizedStatusPowerCellsLow = StatusPowerCellsLow;
        private string _localizedStatusLampThermalLimit = StatusLampThermalLimit;
        private string _localizedStatusSuitLinkRoutingPda = StatusSuitLinkRoutingPda;
        private string _localizedStatusLifeSupportNominalStable = StatusLifeSupportNominalStable;
        private string _localizedStatusLifeSupportNominalAscending = StatusLifeSupportNominalAscending;
        private string _localizedStatusLifeSupportNominalDescending = StatusLifeSupportNominalDescending;
        private Canvas _appliedCanvasTarget;
        private Camera _appliedProjectionCamera;
        private RenderPath _appliedRenderPath;
        private int _appliedOverlaySortingOrder;
        private float _appliedOverallScale;
        private float _appliedChromeAlpha;
        private Color _appliedPrimary;
        private Color _appliedSecondary;
        private Color _appliedDim;
        private Color _appliedWarning;
        private CanvasScaler _cachedCanvasScaler;
        private HectonSurvivalSystem _depthSignalSource;

        public Canvas TargetCanvas => ResolveTargetCanvas();
        public Camera ProjectionCamera => projectionCamera;

        public static void CopyActiveOverlaysTo(List<SuitHUDV4CanvasOverlay> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < s_activeOverlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_activeOverlays[i];
                if (overlay != null && overlay.isActiveAndEnabled)
                    results.Add(overlay);
            }
        }

        private struct GaugeRefs
        {
            public RectTransform Root;
            public Image Icon;
            public Image RingBack;
            public Image RingFill;
            public Image RingFrame;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public TextMeshProUGUI Sub;
            public string CachedLabel;
            public string CachedSubLabel;
            public int CachedRoundedValue;
            public bool HasCachedRoundedValue;
            public float CachedFillAmount;
            public bool HasCachedFillAmount;
            public Color CachedIconColor;
            public Color CachedRingBackColor;
            public Color CachedRingFillColor;
            public Color CachedRingFrameColor;
            public Color CachedLabelColor;
            public Color CachedValueColor;
            public Color CachedSubColor;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            SceneBootstrap.OnGameReady += HandleSceneBootstrapReady;
            RegisterActiveOverlay();
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();
            RefreshAll(0.016f, forceResolve: true);
            RefreshDepthSignalSubscription();
            TryRegisterRuntimeTick();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EvaluateEditorTickRegistration();
#endif
        }

        private void Start()
        {
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SceneBootstrap.OnGameReady -= HandleSceneBootstrapReady;
            UnregisterActiveOverlay();
            UnregisterRuntimeTick();
            ClearDepthSignalSubscription();
            _stressPulseIntensity = 0f;
            _stressPulsePhase = 0f;
            _appliedStressPulseStrength = -1f;
#if UNITY_EDITOR
            UnregisterEditorTick();
#endif

            SetRootVisible(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();
            if (!Application.isPlaying)
                EvaluateEditorTickRegistration();
        }
#endif

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !keepVisibleInEditMode)
            {
                UnregisterEditorTick();
                return;
            }

            RefreshAll(0.016f, forceResolve: false);
            if (!ShouldTickInEditMode())
                UnregisterEditorTick();
        }
#endif

        public void Tick(float deltaTime)
        {
            RefreshAll(deltaTime, forceResolve: false);
        }

        private void HandleSceneBootstrapReady()
        {
            if (!isActiveAndEnabled)
                return;

            _layoutBuilt = false;
            InvalidateVisualCaches();
            TryRegisterRuntimeTick();
            RefreshAll(0.016f, forceResolve: true);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizationCache();
            InvalidateVisualCaches();

            if (isActiveAndEnabled)
                RefreshAll(0.016f, forceResolve: false);
        }

        private void RefreshAll(float dt, bool forceResolve)
        {
            AutoResolve(forceResolve);
            NormalizeCanvas();
            EnsureHierarchy();
            RefreshVisuals(dt);
        }

        private void AutoResolve(bool force)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = GetAutoResolveNow();
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;

            if (targetCanvas == null)
                targetCanvas = ResolveTargetCanvas();

            Transform playerRoot = null;
            bool hasPlayerRoot = false;
            if (survival == null || playerMovement == null || flashlight == null || underwaterVisuals == null)
                hasPlayerRoot = SceneBootstrap.TryGetCurrentPlayerTransform(out playerRoot);

            if (projectionCamera == null)
            {
                VisorHUDController.CopyActiveControllersTo(s_controllerResolveBuffer);
                Transform root = transform.root;
                for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
                {
                    VisorHUDController controller = s_controllerResolveBuffer[i];
                    if (controller == null || controller.HudCamera == null)
                        continue;

                    if (controller.transform.root != root)
                        continue;

                    projectionCamera = controller.HudCamera;
                    break;
                }

                if (projectionCamera == null)
                {
                    for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
                    {
                        VisorHUDController controller = s_controllerResolveBuffer[i];
                        if (controller != null && controller.HudCamera != null)
                        {
                            projectionCamera = controller.HudCamera;
                            break;
                        }
                    }
                }

                s_controllerResolveBuffer.Clear();
            }

            if (uiFont == null)
                uiFont = TMP_Settings.defaultFontAsset;

            if (numericFont == null)
                numericFont = uiFont;

            if (labelFont == null)
                labelFont = uiFont != null && !IsNumericOnlyFont(uiFont) ? uiFont : TMP_Settings.defaultFontAsset;

            TryResolveDefaultIconTextures();

            if (survival == null)
            {
                if (hasPlayerRoot)
                    survival = playerRoot.GetComponent<HectonSurvivalSystem>();
            }

            RefreshDepthSignalSubscription();

            if (playerMovement == null)
            {
                if (hasPlayerRoot)
                    playerMovement = playerRoot.GetComponent<HectonPlayerMovement>();
            }

            if (flashlight == null)
            {
                if (hasPlayerRoot)
                    flashlight = playerRoot.GetComponentInChildren<PlayerFlashlight>(true);
            }

            if (underwaterVisuals == null)
            {
                if (hasPlayerRoot)
                    underwaterVisuals = playerRoot.GetComponentInChildren<HectonUnderwaterVisuals>(true);
            }
        }

        private bool NeedsAutoResolve()
        {
            bool missingProjectionCamera = renderPath == RenderPath.ProjectionSource && projectionCamera == null;
            return targetCanvas == null
                || missingProjectionCamera
                || survival == null
                || playerMovement == null
                || flashlight == null
                || underwaterVisuals == null;
        }

        private static float GetAutoResolveNow()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }

        private bool ShouldTickInEditMode()
        {
            return isActiveAndEnabled &&
                   keepVisibleInEditMode &&
                   (NeedsAutoResolve() ||
                    !_layoutBuilt ||
                    !_styleApplied ||
                    !_canvasStateApplied ||
                    _root == null);
        }

        private void NormalizeCanvas()
        {
            if (targetCanvas == null)
                return;

            bool useProjectionCanvas =
                renderPath == RenderPath.ProjectionSource &&
                projectionCamera != null;
            bool canvasStateMatches =
                _canvasStateApplied &&
                ReferenceEquals(_appliedCanvasTarget, targetCanvas) &&
                ReferenceEquals(_appliedProjectionCamera, projectionCamera) &&
                _appliedRenderPath == renderPath &&
                _appliedOverlaySortingOrder == overlaySortingOrder &&
                IsCanvasStateCurrent(useProjectionCanvas);

            if (canvasStateMatches)
                return;

            targetCanvas.enabled = true;

            if (useProjectionCanvas)
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

            CanvasScaler scaler = ResolveCanvasScaler();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            _canvasStateApplied = true;
            _appliedCanvasTarget = targetCanvas;
            _appliedProjectionCamera = projectionCamera;
            _appliedRenderPath = renderPath;
            _appliedOverlaySortingOrder = overlaySortingOrder;
        }

        private bool IsCanvasStateCurrent(bool useProjectionCanvas)
        {
            if (targetCanvas == null || !targetCanvas.enabled)
                return false;

            if (useProjectionCanvas)
            {
                if (targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                    return false;
                if (!ReferenceEquals(targetCanvas.worldCamera, projectionCamera))
                    return false;
                if (!Mathf.Approximately(targetCanvas.planeDistance, projectionPlaneDistance))
                    return false;
                if (targetCanvas.sortingOrder != 0)
                    return false;
            }
            else
            {
                if (targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    return false;
                if (targetCanvas.worldCamera != null)
                    return false;
                if (targetCanvas.sortingOrder != overlaySortingOrder)
                    return false;
            }

            CanvasScaler scaler = ResolveCanvasScaler();
            if (scaler != null)
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    return false;
                if (!Approximately(scaler.referenceResolution, new Vector2(1600f, 900f)))
                    return false;
                if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                    return false;
                if (!Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
                    return false;
            }

            return true;
        }

        private void EnsureHierarchy()
        {
            targetCanvas = ResolveTargetCanvas();
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
                InvalidateVisualCaches();
            }

            EnsureRootCanvasGroup();
            SetRootVisible(true);

            if (_appliedLayoutRevision != LayoutRevision)
            {
                _layoutBuilt = false;
                _appliedLayoutRevision = LayoutRevision;
                InvalidateVisualCaches();
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

            Vector2 resolvedGaugeClusterOffset = ResolveGaugeClusterOffset();
            Vector2 resolvedGaugeClusterSize = ResolveGaugeClusterSize();
            float resolvedGaugeColumnSpacing = ResolveGaugeColumnSpacing();

            _gaugeClusterRoot = CreateRect("GaugeClusterRoot", _root);
            Anchor(_gaugeClusterRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), resolvedGaugeClusterOffset, resolvedGaugeClusterSize);
            _gaugeClusterRoot.localEulerAngles = Vector3.zero;

            _oxygenGauge = CreateGauge("Gauge_O2", _gaugeClusterRoot, new Vector2(-resolvedGaugeColumnSpacing, 0f), GetOxygenIconSprite());
            _healthGauge = CreateGauge("Gauge_HLT", _gaugeClusterRoot, Vector2.zero, GetHealthIconSprite());
            _powerGauge = CreateGauge("Gauge_PWR", _gaugeClusterRoot, new Vector2(resolvedGaugeColumnSpacing, 0f), GetEnergyIconSprite());

            _layoutBuilt = true;
        }

        private void EnsureRootCanvasGroup()
        {
            if (_root == null)
                return;

            if (!_root.gameObject.activeSelf)
                _root.gameObject.SetActive(true);

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = _root.GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null)
                    _rootCanvasGroup = _root.gameObject.AddComponent<CanvasGroup>();

                _hasAppliedRootVisibility = false;
            }
        }

        private void SetRootVisible(bool visible)
        {
            if (_root == null)
                return;

            EnsureRootCanvasGroup();
            if (_rootCanvasGroup == null)
                return;

            if (_hasAppliedRootVisibility &&
                _appliedRootVisible == visible &&
                IsRootVisibilityCurrent(visible))
            {
                return;
            }

            _rootCanvasGroup.alpha = visible ? 1f : 0f;
            _rootCanvasGroup.interactable = visible;
            _rootCanvasGroup.blocksRaycasts = visible;
            _appliedRootVisible = visible;
            _hasAppliedRootVisibility = true;
        }

        private bool IsRootVisibilityCurrent(bool visible)
        {
            if (_rootCanvasGroup == null)
                return false;

            float expectedAlpha = visible ? 1f : 0f;
            return Mathf.Approximately(_rootCanvasGroup.alpha, expectedAlpha) &&
                   _rootCanvasGroup.interactable == visible &&
                   _rootCanvasGroup.blocksRaycasts == visible;
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

        private Vector2 ResolveGaugeClusterOffset()
        {
            return new Vector2(Mathf.Max(gaugeClusterOffset.x, 132f), Mathf.Max(gaugeClusterOffset.y, 104f));
        }

        private Vector2 ResolveGaugeClusterSize()
        {
            return new Vector2(Mathf.Max(gaugeClusterSize.x, 290f), Mathf.Max(gaugeClusterSize.y, 116f));
        }

        private float ResolveGaugeColumnSpacing()
        {
            return Mathf.Max(gaugeColumnSpacing, 78f);
        }

        private void RefreshVisuals(float dt)
        {
            if (_root == null)
                return;

            RefreshDepthFromMovementFallback();
            ResolveProfile();
            ResolvePalette(out Color primary, out Color secondary, out Color dim, out Color warning);

            bool hasSurvivalStats = survival != null && survival.Stats != null;
            float oxygen = hasSurvivalStats ? Mathf.Clamp01(survival.OxygenNormalized) : 1f;
            float power = hasSurvivalStats ? Mathf.Clamp01(survival.EnergyNormalized) : 1f;
            float health = hasSurvivalStats ? Mathf.Clamp01(survival.IntegrityNormalized) : 1f;
            float depth = _depthMeters;
            float pressure = survival != null ? Mathf.Max(1f, survival.Pressure) : 1f + depth / 10f;
            float heading = playerMovement != null ? Mathf.Repeat(playerMovement.CameraYaw, 360f) : 0f;
            float safeDepth = hasSurvivalStats ? Mathf.Max(1f, survival.Stats.SafeDepth) : 50f;
            float safeDepthNormalized = ResolveSafeDepthNormalized(depth, safeDepth);
            float oxygenCurrent = survival != null ? survival.Oxygen : oxygen * 100f;
            float oxygenMax = hasSurvivalStats ? survival.Stats.MaxOxygen : 100f;
            float energyCurrent = survival != null ? survival.Energy : power * 100f;
            float energyMax = hasSurvivalStats ? survival.Stats.MaxEnergy : 100f;
            float healthCurrent = survival != null ? survival.Integrity : health * 100f;
            float healthMax = hasSurvivalStats ? survival.Stats.MaxIntegrity : 100f;
            float stressPulse = UpdateStressPulse(dt);
            Color pulsedPrimary = ResolveStressPulseColor(primary, warning, stressPulse, stressPulseBrightnessBoost, stressPulseWarningBlend);
            Color pulsedDim = ResolveStressPulseColor(dim, warning, stressPulse, stressPulseBrightnessBoost * 0.45f, stressPulseWarningBlend * 0.38f);
            Color pulsedWarning = ResolveStressPulseColor(warning, primary, stressPulse, stressPulseBrightnessBoost * 0.22f, 0f);

            float targetTemp = EstimateTemperature(depth);
            _displayTemperature = Mathf.Lerp(_displayTemperature, targetTemp, 1f - Mathf.Exp(-4f * dt));
            float depthDelta = depth - _lastDepth;
            _lastDepth = depth;

            ApplyStaticStyleIfNeeded(primary, secondary, dim, warning);
            ApplyStressPulseStyle(primary, warning, stressPulse);

            SetTextIfChanged(_suitLabel, ResolveSuitLabel(), Alpha(primary, 0.95f), ref _appliedSuitLabelText, ref _appliedSuitLabelColor);
            SetTextIfChanged(_headingLabel, ResolveHeadingLabel(Mathf.RoundToInt(heading)), Alpha(dim, 0.58f), ref _appliedHeadingLabelText, ref _appliedHeadingLabelColor);
            float localizedDepth = LocalizedMeasurementFormatter.ConvertDistanceMeters(depth, _localizedMeasurementLanguage);
            float localizedTemperature = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(_displayTemperature, _localizedMeasurementLanguage);
            SetMetricIntIfChanged(_depthLabel, _localizedDepthPattern, Mathf.RoundToInt(localizedDepth), Alpha(pulsedPrimary, 0.96f), ref _appliedDepthValue, ref _hasAppliedDepthValue, ref _appliedDepthColor);
            SetMetricFloatTenthsIfChanged(_temperatureLabel, _localizedTemperaturePattern, localizedTemperature, Alpha(pulsedDim, 0.84f), ref _appliedTemperatureTenths, ref _hasAppliedTemperatureTenths, ref _appliedTemperatureColor);
            SetMetricFloatTenthsIfChanged(_pressureLabel, _localizedPressurePattern, pressure, Alpha(pulsedDim, 0.64f), ref _appliedPressureTenths, ref _hasAppliedPressureTenths, ref _appliedPressureColor);
            SetTextIfChanged(_statusLabel, ResolveStatus(oxygen, power, health, safeDepthNormalized, depth, safeDepth, depthDelta), PickAccent(oxygen, power, health, safeDepthNormalized, pulsedPrimary, pulsedWarning), ref _appliedStatusLabelText, ref _appliedStatusLabelColor);

            Color oxygenAccent = pulsedPrimary;
            Color healthAccent = Color.Lerp(pulsedPrimary, pulsedDim, 0.24f);
            Color energyAccent = Color.Lerp(pulsedPrimary, pulsedWarning, 0.28f);

            UpdateGauge(ref _oxygenGauge, _localizedGaugeO2Label, string.Empty, oxygen, oxygenCurrent, oxygenAccent, secondary, pulsedDim, pulsedWarning);
            UpdateGauge(ref _healthGauge, _localizedGaugeHullLabel, string.Empty, health, healthCurrent, healthAccent, secondary, pulsedDim, pulsedWarning);
            UpdateGauge(ref _powerGauge, _localizedGaugePowerLabel, string.Empty, power, energyCurrent, energyAccent, secondary, pulsedDim, pulsedWarning);
        }

        private void RefreshDepthSignalSubscription()
        {
            if (ReferenceEquals(_depthSignalSource, survival))
                return;

            if (_depthSignalSource != null)
                _depthSignalSource.OnDepthChanged -= HandleDepthChanged;

            _depthSignalSource = survival;
            if (_depthSignalSource != null)
            {
                _depthSignalSource.OnDepthChanged += HandleDepthChanged;
                HandleDepthChanged(_depthSignalSource.Depth);
                return;
            }

            RefreshDepthFromMovementFallback();
        }

        private void ClearDepthSignalSubscription()
        {
            if (_depthSignalSource != null)
                _depthSignalSource.OnDepthChanged -= HandleDepthChanged;

            _depthSignalSource = null;
        }

        private void RefreshDepthFromMovementFallback()
        {
            if (_depthSignalSource == null && playerMovement != null)
                _depthMeters = Mathf.Max(0f, playerMovement.CurrentDepth);
        }

        private void HandleDepthChanged(float depth)
        {
            _depthMeters = Mathf.Max(0f, depth);
        }

        private void ResolveProfile()
        {
            _activeSuit = playerMovement != null ? playerMovement.CurrentSuit : null;
            SuitHUDProfile runtimeOverride = PlayerExpressionManager.ActiveHudProfileOverride;
            _activeProfile = runtimeOverride != null
                ? runtimeOverride
                : (_activeSuit != null && _activeSuit.HudProfile != null ? _activeSuit.HudProfile : defaultHudProfile);
        }

        private void ResolvePalette(out Color primary, out Color secondary, out Color dim, out Color warning)
        {
            if (ReferenceEquals(_cachedPaletteProfile, _activeProfile))
            {
                primary = _cachedPalettePrimary;
                secondary = _cachedPaletteSecondary;
                dim = _cachedPaletteDim;
                warning = _cachedPaletteWarning;
                return;
            }

            primary = new Color(0.46f, 0.98f, 0.94f, 1f);
            secondary = new Color(0.13f, 0.6f, 0.58f, 1f);
            dim = new Color(0.78f, 0.98f, 0.95f, 1f);
            warning = new Color(1f, 0.74f, 0.22f, 1f);

            if (_activeProfile == null)
            {
                CacheResolvedPalette(null, primary, secondary, dim, warning);
                return;
            }

            if (_activeProfile.OverridePalette)
            {
                primary = _activeProfile.PrimaryColor;
                secondary = _activeProfile.SecondaryColor;
                dim = _activeProfile.DimColor;
                warning = _activeProfile.WarningColor;
                CacheResolvedPalette(_activeProfile, primary, secondary, dim, warning);
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

            CacheResolvedPalette(_activeProfile, primary, secondary, dim, warning);
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
            string runtimeOverrideLabel = PlayerExpressionManager.ActiveSuitLabelOverride;
            string overrideLabel = !string.IsNullOrWhiteSpace(runtimeOverrideLabel)
                ? runtimeOverrideLabel
                : (_activeProfile != null ? _activeProfile.DisplayNameOverride : null);
            if (ReferenceEquals(_cachedSuitLabelSuit, _activeSuit) &&
                string.Equals(_cachedSuitLabelOverride, overrideLabel, System.StringComparison.Ordinal))
            {
                return _cachedSuitLabelText;
            }

            _cachedSuitLabelSuit = _activeSuit;
            _cachedSuitLabelOverride = overrideLabel;

            if (!string.IsNullOrWhiteSpace(overrideLabel))
            {
                _cachedSuitLabelText = CachedToUpperInvariant(overrideLabel);
                return _cachedSuitLabelText;
            }

            if (_activeSuit != null)
            {
                _cachedSuitLabelText = CachedToUpperInvariant(_activeSuit.name.Replace('_', ' '));
                return _cachedSuitLabelText;
            }

            _cachedSuitLabelText = DefaultSuitLabel;
            return _cachedSuitLabelText;
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

        private string ResolveStatus(float oxygen, float power, float health, float safeDepthNormalized, float depth, float safeDepth, float depthDelta)
        {
            if (safeDepthNormalized <= 0.08f || depth >= safeDepth)
                return _localizedStatusPressureLimitExceeded;
            if (safeDepthNormalized <= 0.22f)
                return _localizedStatusApproachingSafeDepth;
            if (health <= 0.2f)
                return _localizedStatusSuitDamageCritical;
            if (oxygen <= 0.2f)
                return _localizedStatusOxygenReserveLow;
            if (power <= 0.2f)
                return _localizedStatusPowerCellsLow;
            if (flashlight != null && flashlight.IsOverheated)
                return _localizedStatusLampThermalLimit;
            if (PlayerPDA.IsOpen)
                return _localizedStatusSuitLinkRoutingPda;

            if (depthDelta > 0.04f)
                return _localizedStatusLifeSupportNominalDescending;
            if (depthDelta < -0.04f)
                return _localizedStatusLifeSupportNominalAscending;
            return _localizedStatusLifeSupportNominalStable;
        }

        private void RebuildLocalizationCache()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            _localizedMeasurementLanguage = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            string depthLabel = ResolveLocalized(LocalizationKeys.HUD_DEPTH, "DEPTH");
            string temperatureLabel = ResolveLocalized(LocalizationKeys.HUD_TEMP, DefaultTemperatureLabel);
            string pressureLabel = ResolveLocalized(LocalizationKeys.HUD_PRESSURE, DefaultPressureLabel);
            string atmLabel = ResolveLocalized(LocalizationKeys.HUD_ATM, "atm");
            string depthUnitLabel = LocalizedMeasurementFormatter.ResolveDistanceUnitLabel(_localizedMeasurementLanguage);
            string temperatureUnitLabel = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabel(_localizedMeasurementLanguage);

            _localizedDepthPattern = string.Concat(depthLabel, ": -{0:0} ", depthUnitLabel);
            _localizedTemperaturePattern = string.Concat(temperatureLabel, ": {0:0.0} ", temperatureUnitLabel);
            _localizedPressurePattern = string.Concat(pressureLabel, ": {0:0.0} ", atmLabel);
            _localizedGaugeO2Label = ResolveLocalized(LocalizationKeys.HUD_O2, DefaultGaugeO2Label);
            _localizedGaugePowerLabel = ResolveLocalized(LocalizationKeys.HUD_PWR, DefaultGaugePowerLabel);
            _localizedGaugeHullLabel = ResolveLocalized(LocalizationKeys.HUD_HULL, DefaultGaugeHullLabel);
            _localizedStatusPressureLimitExceeded = ResolveLocalized(LocalizationKeys.HUD_STATUS_PRESSURE_LIMIT_EXCEEDED, StatusPressureLimitExceeded);
            _localizedStatusApproachingSafeDepth = ResolveLocalized(LocalizationKeys.HUD_STATUS_APPROACHING_SAFE_DEPTH_LIMIT, StatusApproachingSafeDepth);
            _localizedStatusSuitDamageCritical = ResolveLocalized(LocalizationKeys.HUD_STATUS_SUIT_DAMAGE_CRITICAL, StatusSuitDamageCritical);
            _localizedStatusOxygenReserveLow = ResolveLocalized(LocalizationKeys.HUD_STATUS_OXYGEN_RESERVE_LOW, StatusOxygenReserveLow);
            _localizedStatusPowerCellsLow = ResolveLocalized(LocalizationKeys.HUD_STATUS_POWER_CELLS_LOW, StatusPowerCellsLow);
            _localizedStatusLampThermalLimit = ResolveLocalized(LocalizationKeys.HUD_STATUS_LAMP_THERMAL_LIMIT, StatusLampThermalLimit);
            _localizedStatusSuitLinkRoutingPda = ResolveLocalized(LocalizationKeys.HUD_STATUS_SUIT_LINK_ROUTING_PDA, StatusSuitLinkRoutingPda);
            _localizedStatusLifeSupportNominalStable = ResolveLocalized(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_STABLE, StatusLifeSupportNominalStable);
            _localizedStatusLifeSupportNominalAscending = ResolveLocalized(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_ASCENDING, StatusLifeSupportNominalAscending);
            _localizedStatusLifeSupportNominalDescending = ResolveLocalized(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_DESCENDING, StatusLifeSupportNominalDescending);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static Color PickAccent(float oxygen, float power, float health, float safeDepthNormalized, Color primary, Color warning)
        {
            if (oxygen <= 0.2f || power <= 0.2f || health <= 0.2f || safeDepthNormalized <= 0.18f)
                return Alpha(warning, 0.94f);

            return Alpha(primary, 0.88f);
        }

        private static float ResolveSafeDepthNormalized(float depth, float safeDepth)
        {
            if (safeDepth <= 0.01f)
                return 1f;

            return 1f - Mathf.Clamp01(depth / safeDepth);
        }

        private static void UpdateGauge(ref GaugeRefs gauge, string label, string subLabel, float normalized, float currentValue, Color primary, Color secondary, Color dim, Color warning)
        {
            float clamped = Mathf.Clamp01(normalized);
            Color accent = clamped <= 0.2f ? warning : primary;
            if (gauge.Icon != null)
            {
                Color iconColor = Alpha(accent, 0.94f);
                if (gauge.CachedIconColor != iconColor)
                {
                    gauge.Icon.color = iconColor;
                    gauge.CachedIconColor = iconColor;
                }
            }

            if (gauge.RingBack != null)
            {
                Color ringBackColor = Alpha(primary, 0.08f);
                if (gauge.CachedRingBackColor != ringBackColor)
                {
                    gauge.RingBack.color = ringBackColor;
                    gauge.CachedRingBackColor = ringBackColor;
                }
            }

            if (gauge.RingFill != null)
            {
                Color ringFillColor = Alpha(accent, 0.94f);
                if (gauge.CachedRingFillColor != ringFillColor)
                {
                    gauge.RingFill.color = ringFillColor;
                    gauge.CachedRingFillColor = ringFillColor;
                }

                if (!gauge.HasCachedFillAmount || !Mathf.Approximately(gauge.CachedFillAmount, clamped))
                {
                    gauge.RingFill.fillAmount = clamped;
                    gauge.CachedFillAmount = clamped;
                    gauge.HasCachedFillAmount = true;
                }
            }

            if (gauge.RingFrame != null)
            {
                Color ringFrameColor = Alpha(dim, 0.28f);
                if (gauge.CachedRingFrameColor != ringFrameColor)
                {
                    gauge.RingFrame.color = ringFrameColor;
                    gauge.CachedRingFrameColor = ringFrameColor;
                }
            }

            Color labelColor = Alpha(dim, 0.84f);
            if (gauge.Label != null)
            {
                if (!string.Equals(gauge.CachedLabel, label, System.StringComparison.Ordinal))
                {
                    gauge.Label.text = label;
                    gauge.CachedLabel = label;
                }

                if (gauge.CachedLabelColor != labelColor)
                {
                    gauge.Label.color = labelColor;
                    gauge.CachedLabelColor = labelColor;
                }
            }

            Color valueColor = Alpha(accent, 0.98f);
            int roundedValue = Mathf.RoundToInt(currentValue);
            if (gauge.Value != null)
            {
                if (!gauge.HasCachedRoundedValue || gauge.CachedRoundedValue != roundedValue)
                {
                    gauge.Value.SetText("{0:0}", roundedValue);
                    gauge.CachedRoundedValue = roundedValue;
                    gauge.HasCachedRoundedValue = true;
                }

                if (gauge.CachedValueColor != valueColor)
                {
                    gauge.Value.color = valueColor;
                    gauge.CachedValueColor = valueColor;
                }
            }

            if (gauge.Sub != null)
            {
                if (!string.Equals(gauge.CachedSubLabel, subLabel, System.StringComparison.Ordinal))
                {
                    gauge.Sub.text = subLabel;
                    gauge.CachedSubLabel = subLabel;
                }

                Color subColor = Alpha(secondary, 0.55f);
                if (gauge.CachedSubColor != subColor)
                {
                    gauge.Sub.color = subColor;
                    gauge.CachedSubColor = subColor;
                }
            }
        }

        private GaugeRefs CreateGauge(string name, RectTransform parent, Vector2 anchoredPosition, Sprite iconSprite)
        {
            GaugeRefs refs = new GaugeRefs();

            refs.Root = CreateRect(name, parent);
            Anchor(refs.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(86f, 92f));

            RectTransform iconRect = CreateRect(name + "_Icon", refs.Root);
            Anchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 28f), gaugeIconSize);
            refs.Icon = iconRect.gameObject.AddComponent<Image>();
            refs.Icon.sprite = iconSprite;
            refs.Icon.preserveAspect = true;
            refs.Icon.raycastTarget = false;

            Sprite fillSprite = GetRingFillSprite();
            Sprite frameSprite = GetRingFrameSprite();

            float resolvedRingSize = Mathf.Max(gaugeRingSize, 50f);
            float resolvedRingThickness = Mathf.Clamp(gaugeRingThickness, 4f, 10f);
            float resolvedValueOffsetY = Mathf.Clamp(gaugeValueOffsetY, -4f, 4f);
            float resolvedLabelOffsetY = Mathf.Clamp(gaugeLabelOffsetY, -42f, -24f);

            RectTransform backRect = CreateRect(name + "_RingBack", refs.Root);
            Anchor(backRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            refs.RingBack = backRect.gameObject.AddComponent<Image>();
            refs.RingBack.sprite = fillSprite;
            refs.RingBack.type = Image.Type.Filled;
            refs.RingBack.fillMethod = Image.FillMethod.Radial360;
            refs.RingBack.fillOrigin = (int)Image.Origin360.Top;
            refs.RingBack.fillClockwise = true;
            refs.RingBack.fillAmount = 1f;
            refs.RingBack.raycastTarget = false;

            RectTransform fillRect = CreateRect(name + "_RingFill", refs.Root);
            Anchor(fillRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            refs.RingFill = fillRect.gameObject.AddComponent<Image>();
            refs.RingFill.sprite = fillSprite;
            refs.RingFill.type = Image.Type.Filled;
            refs.RingFill.fillMethod = Image.FillMethod.Radial360;
            refs.RingFill.fillOrigin = (int)Image.Origin360.Top;
            refs.RingFill.fillClockwise = true;
            refs.RingFill.fillAmount = 1f;
            refs.RingFill.raycastTarget = false;

            RectTransform frameRect = CreateRect(name + "_RingFrame", refs.Root);
            Anchor(frameRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            refs.RingFrame = frameRect.gameObject.AddComponent<Image>();
            refs.RingFrame.sprite = frameSprite;
            refs.RingFrame.type = Image.Type.Simple;
            refs.RingFrame.raycastTarget = false;

            refs.Label = CreateText(name + "_Label", refs.Root, 10f, FontStyles.Bold, TextAlignmentOptions.Center, 0.82f, ResolveLabelFontAsset());
            Anchor(refs.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, resolvedLabelOffsetY), new Vector2(86f, 16f));

            refs.Value = CreateText(name + "_Value", refs.Root, 15f, FontStyles.Bold, TextAlignmentOptions.Center, 0.98f, ResolveNumericFontAsset());
            Anchor(refs.Value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f + resolvedValueOffsetY), new Vector2(44f, 22f));

            refs.Sub = CreateText(name + "_Sub", refs.Root, 10f, FontStyles.Normal, TextAlignmentOptions.Center, 0.52f, ResolveLabelFontAsset());
            Anchor(refs.Sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(86f, 12f));
            refs.Sub.gameObject.SetActive(false);

            return refs;
        }

        private static Sprite GetRingFillSprite()
        {
            if (s_ringFillSprite != null)
                return s_ringFillSprite;

            s_ringFillSprite = CreateRingSprite("HUDRingFillRuntime", 128, 14, false);
            return s_ringFillSprite;
        }

        private static Sprite GetRingFrameSprite()
        {
            if (s_ringFrameSprite != null)
                return s_ringFrameSprite;

            s_ringFrameSprite = CreateRingSprite("HUDRingFrameRuntime", 128, 14, true);
            return s_ringFrameSprite;
        }

        private static Sprite CreateRingSprite(string name, int size, int thickness, bool outlineOnly)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            float center = (size - 1) * 0.5f;
            float outerRadius = center - 1f;
            float innerRadius = Mathf.Max(outerRadius - thickness, 0f);
            float frameInnerRadius = Mathf.Max(innerRadius - 1.5f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    bool insideRing = distance <= outerRadius && distance >= innerRadius;
                    if (!insideRing)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    if (!outlineOnly)
                    {
                        texture.SetPixel(x, y, solid);
                        continue;
                    }

                    bool edge = distance >= outerRadius - 2f || distance <= frameInnerRadius;

                    texture.SetPixel(x, y, edge ? solid : clear);
                }
            }

            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private void TryResolveDefaultIconTextures()
        {
#if UNITY_EDITOR
            bool changed = false;

            if (oxygenIconTexture == null)
            {
                oxygenIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/oxygen-tank.png");
                changed |= oxygenIconTexture != null;
            }

            if (healthIconTexture == null)
            {
                healthIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/cardiogram.png");
                changed |= healthIconTexture != null;
            }

            if (energyIconTexture == null)
            {
                energyIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/thunder.png");
                changed |= energyIconTexture != null;
            }

            if (changed)
                EditorUtility.SetDirty(this);
#endif
        }

        private Sprite GetOxygenIconSprite()
        {
            if (s_oxygenIconSprite == null)
                s_oxygenIconSprite = CreateIconSprite(oxygenIconTexture, "HUD_OxygenIcon");

            return s_oxygenIconSprite;
        }

        private Sprite GetHealthIconSprite()
        {
            if (s_healthIconSprite == null)
                s_healthIconSprite = CreateIconSprite(healthIconTexture, "HUD_HealthIcon");

            return s_healthIconSprite;
        }

        private Sprite GetEnergyIconSprite()
        {
            if (s_energyIconSprite == null)
                s_energyIconSprite = CreateIconSprite(energyIconTexture, "HUD_EnergyIcon");

            return s_energyIconSprite;
        }

        private static Sprite CreateIconSprite(Texture2D texture, string name)
        {
            if (texture == null)
                return null;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
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
            return CreateText(name, parent, size, style, alignment, alpha, ResolveLabelFontAsset());
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, float size, FontStyles style, TextAlignmentOptions alignment, float alpha, TMP_FontAsset fontAsset)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset != null ? fontAsset : ResolveLabelFontAsset();
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterSpacing = size >= 36f ? 4f : 1.5f;
            label.color = Alpha(Color.white, alpha);
            label.text = name;
            return label;
        }

        private TMP_FontAsset ResolveLabelFontAsset()
        {
            return labelFont != null ? labelFont : (uiFont != null ? uiFont : TMP_Settings.defaultFontAsset);
        }

        private TMP_FontAsset ResolveNumericFontAsset()
        {
            return numericFont != null ? numericFont : ResolveLabelFontAsset();
        }

        private static bool IsNumericOnlyFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            string fontName = fontAsset.name;
            return fontName.Contains("Digit") || fontName.Contains("циф");
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

        private static void SetTextIfChanged(
            TextMeshProUGUI label,
            string text,
            Color color,
            ref string cachedText,
            ref Color cachedColor)
        {
            if (label == null)
                return;

            if (!string.Equals(cachedText, text, System.StringComparison.Ordinal))
            {
                label.text = text;
                cachedText = text;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetMetricInt(TextMeshProUGUI label, string pattern, int value, Color color)
        {
            if (label == null)
                return;

            label.color = color;
            label.SetText(pattern, value);
        }

        private static void SetMetricFloat(TextMeshProUGUI label, string pattern, float value, Color color)
        {
            if (label == null)
                return;

            label.color = color;
            label.SetText(pattern, value);
        }

        private static void SetMetricIntIfChanged(
            TextMeshProUGUI label,
            string pattern,
            int value,
            Color color,
            ref int cachedValue,
            ref bool hasCachedValue,
            ref Color cachedColor)
        {
            if (label == null)
                return;

            if (!hasCachedValue || cachedValue != value)
            {
                label.SetText(pattern, value);
                cachedValue = value;
                hasCachedValue = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetMetricFloatTenthsIfChanged(
            TextMeshProUGUI label,
            string pattern,
            float value,
            Color color,
            ref int cachedTenths,
            ref bool hasCachedTenths,
            ref Color cachedColor)
        {
            if (label == null)
                return;

            int roundedTenths = Mathf.RoundToInt(value * 10f);
            if (!hasCachedTenths || cachedTenths != roundedTenths)
            {
                label.SetText(pattern, roundedTenths * 0.1f);
                cachedTenths = roundedTenths;
                hasCachedTenths = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
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

        // ZERO-GC: Cached colors for veil (reused with alpha modulation)
        private static readonly Color VeilBaseTop = new Color(0.01f, 0.04f, 0.06f, 1f);
        private static readonly Color VeilBaseBottom = new Color(0.01f, 0.04f, 0.06f, 1f);
        private static readonly Color VeilBaseSide = new Color(0.01f, 0.03f, 0.05f, 1f);

        private float UpdateStressPulse(float dt)
        {
            float rawStress = playerMovement != null
                ? Mathf.Clamp01(playerMovement.CurrentUnderwaterStressIntensity01)
                : 0f;
            float targetStress = rawStress <= stressPulseStartThreshold
                ? 0f
                : Mathf.InverseLerp(stressPulseStartThreshold, 1f, rawStress);
            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.01f, stressPulseBlendSpeed) * dt);
            _stressPulseIntensity = Mathf.Lerp(_stressPulseIntensity, targetStress, blendT);
            if (_stressPulseIntensity <= 0.001f)
            {
                _stressPulseIntensity = 0f;
                return 0f;
            }

            float frequency = Mathf.Lerp(stressPulseFrequencyMin, stressPulseFrequencyMax, _stressPulseIntensity);
            _stressPulsePhase += dt * frequency * Mathf.PI * 2f;
            if (_stressPulsePhase >= Mathf.PI * 2f)
                _stressPulsePhase -= Mathf.PI * 2f;

            float wave = 0.5f + 0.5f * Mathf.Sin(_stressPulsePhase);
            return _stressPulseIntensity * wave;
        }

        private static Color ResolveStressPulseColor(Color baseColor, Color warningColor, float pulseStrength, float brightnessBoost, float warningBlend)
        {
            if (pulseStrength <= 0.0001f)
                return baseColor;

            Color pulsedColor = Color.Lerp(baseColor, warningColor, pulseStrength * warningBlend);
            return Color.Lerp(pulsedColor, Color.white, pulseStrength * brightnessBoost);
        }

        private void ApplyStaticStyleIfNeeded(Color primary, Color secondary, Color dim, Color warning)
        {
            if (_styleApplied &&
                Mathf.Approximately(_appliedOverallScale, overallScale) &&
                Mathf.Approximately(_appliedChromeAlpha, chromeAlpha) &&
                _appliedPrimary == primary &&
                _appliedSecondary == secondary &&
                _appliedDim == dim &&
                _appliedWarning == warning)
            {
                return;
            }

            _root.localScale = Vector3.one * overallScale;

            // ZERO-GC: Modulate alpha on cached colors
            _topVeil.color = Alpha(VeilBaseTop, chromeAlpha * 0.45f);
            _bottomVeil.color = Alpha(VeilBaseBottom, chromeAlpha * 0.55f);
            _leftVeil.color = Alpha(VeilBaseSide, chromeAlpha * 0.16f);
            _rightVeil.color = Alpha(VeilBaseSide, chromeAlpha * 0.16f);
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

            _appliedOverallScale = overallScale;
            _appliedChromeAlpha = chromeAlpha;
            _appliedPrimary = primary;
            _appliedSecondary = secondary;
            _appliedDim = dim;
            _appliedWarning = warning;
            _styleApplied = true;
        }

        private void ApplyStressPulseStyle(Color primary, Color warning, float stressPulse)
        {
            if (!_styleApplied || Mathf.Abs(_appliedStressPulseStrength - stressPulse) <= 0.002f)
                return;

            Color pulsedPrimary = ResolveStressPulseColor(primary, warning, stressPulse, stressPulseBrightnessBoost, stressPulseWarningBlend);
            float chromePulse = stressPulse * stressPulseChromeAlphaBoost;
            _topVeil.color = Alpha(VeilBaseTop, chromeAlpha * (0.45f + chromePulse * 0.6f));
            _bottomVeil.color = Alpha(VeilBaseBottom, chromeAlpha * (0.55f + chromePulse));
            _leftVeil.color = Alpha(VeilBaseSide, chromeAlpha * (0.16f + chromePulse * 0.28f));
            _rightVeil.color = Alpha(VeilBaseSide, chromeAlpha * (0.16f + chromePulse * 0.28f));
            _headerLine.color = Alpha(pulsedPrimary, 0.22f + stressPulse * 0.04f);
            _headerWingLeft.color = Alpha(pulsedPrimary, 0.16f + stressPulse * 0.03f);
            _headerWingRight.color = Alpha(pulsedPrimary, 0.16f + stressPulse * 0.03f);
            _footerLine.color = Alpha(pulsedPrimary, 0.18f + stressPulse * 0.04f);
            _statusRuleLeft.color = Alpha(pulsedPrimary, 0.14f + stressPulse * 0.05f);
            _statusRuleRight.color = Alpha(pulsedPrimary, 0.14f + stressPulse * 0.05f);
            _telemetryRule.color = Alpha(pulsedPrimary, 0.18f + stressPulse * 0.03f);
            _telemetryBraceUpper.color = Alpha(pulsedPrimary, 0.18f + stressPulse * 0.03f);
            _telemetryBraceLower.color = Alpha(pulsedPrimary, 0.12f + stressPulse * 0.03f);
            _reticleH.color = Alpha(pulsedPrimary, 0.76f + stressPulse * 0.1f);
            _reticleV.color = Alpha(pulsedPrimary, 0.5f + stressPulse * 0.08f);
            _reticleBracketLeft.color = Alpha(pulsedPrimary, 0.42f + stressPulse * 0.08f);
            _reticleBracketRight.color = Alpha(pulsedPrimary, 0.42f + stressPulse * 0.08f);
            _appliedStressPulseStrength = stressPulse;
        }

        private void InvalidateVisualCaches()
        {
            _appliedSuitLabelText = null;
            _appliedSuitLabelColor = default;
            _appliedHeadingLabelText = null;
            _appliedHeadingLabelColor = default;
            _appliedStatusLabelText = null;
            _appliedStatusLabelColor = default;
            _appliedDepthValue = 0;
            _hasAppliedDepthValue = false;
            _appliedDepthColor = default;
            _appliedTemperatureTenths = 0;
            _hasAppliedTemperatureTenths = false;
            _appliedTemperatureColor = default;
            _appliedPressureTenths = 0;
            _hasAppliedPressureTenths = false;
            _appliedPressureColor = default;
            _appliedStressPulseStrength = -1f;
            _styleApplied = false;
            _appliedOverallScale = 0f;
            _appliedChromeAlpha = 0f;
            _appliedPrimary = default;
            _appliedSecondary = default;
            _appliedDim = default;
            _appliedWarning = default;
            _canvasStateApplied = false;
            _appliedCanvasTarget = null;
            _appliedProjectionCamera = null;
            _appliedRenderPath = default;
            _appliedOverlaySortingOrder = 0;
            _cachedCanvasScaler = null;
        }

        private Canvas ResolveTargetCanvas()
        {
            if (targetCanvas == null)
                targetCanvas = GetComponent<Canvas>();

            return targetCanvas;
        }

        private CanvasScaler ResolveCanvasScaler()
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
            {
                _cachedCanvasScaler = null;
                return null;
            }

            if (_cachedCanvasScaler == null || _cachedCanvasScaler.gameObject != canvas.gameObject)
                _cachedCanvasScaler = canvas.GetComponent<CanvasScaler>();

            return _cachedCanvasScaler;
        }

        private void CacheResolvedPalette(SuitHUDProfile profile, Color primary, Color secondary, Color dim, Color warning)
        {
            _cachedPaletteProfile = profile;
            _cachedPalettePrimary = primary;
            _cachedPaletteSecondary = secondary;
            _cachedPaletteDim = dim;
            _cachedPaletteWarning = warning;
        }

        private static string ResolveHeadingLabel(int roundedHeading)
        {
            int normalizedHeading = roundedHeading % 360;
            if (normalizedHeading < 0)
                normalizedHeading += 360;

            return s_headingLabels[normalizedHeading];
        }

        private static string[] BuildHeadingLabels()
        {
            string[] labels = new string[360];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = "HEADING " + i.ToString("000") + " / " + ResolveCardinal(i);

            return labels;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y);
        }

        public void SetRenderPathProjectionSource(bool projectionSource)
        {
            RenderPath nextPath = projectionSource ? RenderPath.ProjectionSource : RenderPath.ScreenOverlay;
            if (renderPath == nextPath)
                return;

            renderPath = nextPath;
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RequestRefresh();
        }

        public void SetProjectionCamera(Camera camera)
        {
            if (projectionCamera == camera)
                return;

            projectionCamera = camera;
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RequestRefresh();
        }

        public void CopyConfigurationFrom(SuitHUDV4CanvasOverlay source)
        {
            if (source == null || ReferenceEquals(source, this))
                return;

            uiFont = source.uiFont;
            labelFont = source.labelFont;
            numericFont = source.numericFont;
            oxygenIconTexture = source.oxygenIconTexture;
            healthIconTexture = source.healthIconTexture;
            energyIconTexture = source.energyIconTexture;
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
            InvalidateVisualCaches();
            RequestRefresh();
        }

        private void RequestRefresh()
        {
            if (Application.isPlaying)
            {
                TryRegisterRuntimeTick();
            }
#if UNITY_EDITOR
            else
            {
                EvaluateEditorTickRegistration();
            }
#endif
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying || _tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _tickRegistered = true;
        }

        private void UnregisterRuntimeTick()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _tickRegistered = false;
        }

#if UNITY_EDITOR
        private void EvaluateEditorTickRegistration()
        {
            if (Application.isPlaying)
            {
                UnregisterEditorTick();
                return;
            }

            if (ShouldTickInEditMode())
                RegisterEditorTick();
            else
                UnregisterEditorTick();
        }

        private void RegisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        private void UnregisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
        }
#endif

        private void RegisterActiveOverlay()
        {
            if (s_activeOverlays.Contains(this))
                return;

            s_activeOverlays.Add(this);
        }

        private void UnregisterActiveOverlay()
        {
            s_activeOverlays.Remove(this);
        }
    }
}
