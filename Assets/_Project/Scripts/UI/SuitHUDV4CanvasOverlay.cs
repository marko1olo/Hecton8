using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using Unity.Mathematics;
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
        private static readonly HeadingLabelCacheEntry[] s_headingLabels = BuildHeadingLabels();
        private const string DefaultSuitLabel = "EXPEDITION SUIT";
        private const string DefaultTemperatureLabel = "TEMP";
        private const string DefaultPressureLabel = "PRESSURE";
        private const string DefaultAtmLabel = "atm";
        private const string DefaultMetersLabel = "m";
        private const string DefaultFeetLabel = "ft";
        private const string DefaultCelsiusLabel = "C";
        private const string DefaultFahrenheitLabel = "F";
        private const string DefaultGaugeO2Label = "O2";
        private const string DefaultGaugePowerLabel = "PWR";
        private const string DefaultGaugeHullLabel = "HULL";
        private const string DefaultStatusPressureLimitExceeded = "PRESSURE LIMIT EXCEEDED";
        private const string DefaultStatusApproachingSafeDepth = "APPROACHING SAFE DEPTH LIMIT";
        private const string DefaultStatusSuitDamageCritical = "SUIT DAMAGE CRITICAL";
        private const string DefaultStatusOxygenReserveLow = "OXYGEN RESERVE LOW";
        private const string DefaultStatusPowerCellsLow = "POWER CELLS LOW";
        private const string DefaultStatusLampThermalLimit = "LAMP THERMAL LIMIT";
        private const string DefaultStatusSuitLinkRoutingPda = "SUIT LINK ROUTING PDA";
        private const string DefaultStatusLifeSupportNominalStable = "LIFE SUPPORT NOMINAL / STABLE";
        private const string DefaultStatusLifeSupportNominalAscending = "LIFE SUPPORT NOMINAL / ASCENDING";
        private const string DefaultStatusLifeSupportNominalDescending = "LIFE SUPPORT NOMINAL / DESCENDING";
        private const string DepthNumberToken = "{N0:F0}";
        private const string FixedTenthsNumberToken = "{N0:F1}";
        private const string HeadingNumberTemplate = "HEADING {N0:D3} / ";
        private const float OxygenGaugeDamping = 12f;
        private const float BatteryGaugeDamping = 6f;
        private const float GaugeSmoothingEpsilon = 0.01f;
        private static readonly char[] s_atmLabelChars = DefaultAtmLabel.ToCharArray();
        private static readonly char[] s_depthLabelChars = "DEPTH".ToCharArray();
        private static readonly char[] s_temperatureLabelChars = DefaultTemperatureLabel.ToCharArray();
        private static readonly char[] s_pressureLabelChars = DefaultPressureLabel.ToCharArray();
        private static readonly char[] s_metersLabelChars = DefaultMetersLabel.ToCharArray();
        private static readonly char[] s_feetLabelChars = DefaultFeetLabel.ToCharArray();
        private static readonly char[] s_celsiusLabelChars = DefaultCelsiusLabel.ToCharArray();
        private static readonly char[] s_fahrenheitLabelChars = DefaultFahrenheitLabel.ToCharArray();
        private static readonly char[] s_gaugeO2LabelChars = DefaultGaugeO2Label.ToCharArray();
        private static readonly char[] s_gaugePowerLabelChars = DefaultGaugePowerLabel.ToCharArray();
        private static readonly char[] s_gaugeHullLabelChars = DefaultGaugeHullLabel.ToCharArray();
        private static readonly char[] s_statusPressureLimitExceededChars = DefaultStatusPressureLimitExceeded.ToCharArray();
        private static readonly char[] s_statusApproachingSafeDepthChars = DefaultStatusApproachingSafeDepth.ToCharArray();
        private static readonly char[] s_statusSuitDamageCriticalChars = DefaultStatusSuitDamageCritical.ToCharArray();
        private static readonly char[] s_statusOxygenReserveLowChars = DefaultStatusOxygenReserveLow.ToCharArray();
        private static readonly char[] s_statusPowerCellsLowChars = DefaultStatusPowerCellsLow.ToCharArray();
        private static readonly char[] s_statusLampThermalLimitChars = DefaultStatusLampThermalLimit.ToCharArray();
        private static readonly char[] s_statusSuitLinkRoutingPdaChars = DefaultStatusSuitLinkRoutingPda.ToCharArray();
        private static readonly char[] s_statusLifeSupportNominalStableChars = DefaultStatusLifeSupportNominalStable.ToCharArray();
        private static readonly char[] s_statusLifeSupportNominalAscendingChars = DefaultStatusLifeSupportNominalAscending.ToCharArray();
        private static readonly char[] s_statusLifeSupportNominalDescendingChars = DefaultStatusLifeSupportNominalDescending.ToCharArray();
        private static readonly int _HudDepthKeyHash = LocHash.Compute(LocalizationKeys.HUD_DEPTH);
        private static readonly int _HudTemperatureKeyHash = LocHash.Compute(LocalizationKeys.HUD_TEMP);
        private static readonly int _HudPressureKeyHash = LocHash.Compute(LocalizationKeys.HUD_PRESSURE);
        private static readonly int _HudAtmKeyHash = LocHash.Compute(LocalizationKeys.HUD_ATM);
        private static readonly int _HudOxygenKeyHash = LocHash.Compute(LocalizationKeys.HUD_O2);
        private static readonly int _HudPowerKeyHash = LocHash.Compute(LocalizationKeys.HUD_PWR);
        private static readonly int _HudHullKeyHash = LocHash.Compute(LocalizationKeys.HUD_HULL);
        private static readonly int _HudMetersKeyHash = LocHash.Compute(LocalizationKeys.HUD_UNIT_METERS);
        private static readonly int _HudFeetKeyHash = LocHash.Compute(LocalizationKeys.HUD_UNIT_FEET);
        private static readonly int _HudCelsiusKeyHash = LocHash.Compute(LocalizationKeys.HUD_UNIT_CELSIUS);
        private static readonly int _HudFahrenheitKeyHash = LocHash.Compute(LocalizationKeys.HUD_UNIT_FAHRENHEIT);
        private static readonly int _StatusPressureLimitExceededKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_PRESSURE_LIMIT_EXCEEDED);
        private static readonly int _StatusApproachingSafeDepthKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_APPROACHING_SAFE_DEPTH_LIMIT);
        private static readonly int _StatusSuitDamageCriticalKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_SUIT_DAMAGE_CRITICAL);
        private static readonly int _StatusOxygenReserveLowKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_OXYGEN_RESERVE_LOW);
        private static readonly int _StatusPowerCellsLowKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_POWER_CELLS_LOW);
        private static readonly int _StatusLampThermalLimitKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_LAMP_THERMAL_LIMIT);
        private static readonly int _StatusSuitLinkRoutingPdaKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_SUIT_LINK_ROUTING_PDA);
        private static readonly int _StatusLifeSupportNominalStableKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_STABLE);
        private static readonly int _StatusLifeSupportNominalAscendingKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_ASCENDING);
        private static readonly int _StatusLifeSupportNominalDescendingKeyHash = LocHash.Compute(LocalizationKeys.HUD_STATUS_LIFE_SUPPORT_NOMINAL_DESCENDING);

        public enum RenderPath
        {
            ScreenOverlay,
            ProjectionSource
        }

        private readonly struct HeadingLabelCacheEntry
        {
            public HeadingLabelCacheEntry(char[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }

            public char[] Buffer { get; }
            public int Length { get; }
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
        private bool _cachedSuitLabelRtl;
        private int _cachedSuitLabelVersion;
        private int _appliedSuitLabelVersion = int.MinValue;
        private Color _appliedSuitLabelColor;
        private int _appliedHeadingLabelVersion = int.MinValue;
        private Color _appliedHeadingLabelColor;
        private int _appliedStatusKeyHash;
        private bool _hasAppliedStatusKeyHash;
        private int _appliedStatusWhisperVersion = int.MinValue;
        private Color _appliedStatusLabelColor;
        private int _appliedDepthValue;
        private bool _hasAppliedDepthValue;
        private int _appliedDepthWhisperVersion = int.MinValue;
        private Color _appliedDepthColor;
        private int _appliedTemperatureTenths;
        private bool _hasAppliedTemperatureTenths;
        private int _appliedTemperatureWhisperVersion = int.MinValue;
        private Color _appliedTemperatureColor;
        private int _appliedPressureTenths;
        private bool _hasAppliedPressureTenths;
        private int _appliedPressureWhisperVersion = int.MinValue;
        private Color _appliedPressureColor;
        private bool _styleApplied;
        private bool _canvasStateApplied;
        private bool _hasAppliedRootVisibility;
        private bool _appliedRootVisible;
        private float _stressPulseIntensity;
        private float _stressPulsePhase;
        private float _appliedStressPulseStrength = -1f;
        private GameLanguage _localizedMeasurementLanguage = GameLanguage.English;
        // COLD ALLOC: char[64] — cached suit label staging buffer — owner: SuitHUDV4CanvasOverlay
        private char[] _cachedSuitLabelBuffer = new char[64];
        private int _cachedSuitLabelLength;
        // COLD ALLOC: char[64] — localized depth metric template buffer — owner: SuitHUDV4CanvasOverlay
        private char[] _depthTemplateBuffer = new char[64];
        private int _depthTemplateLength;
        // COLD ALLOC: char[64] — localized temperature metric template buffer — owner: SuitHUDV4CanvasOverlay
        private char[] _temperatureTemplateBuffer = new char[64];
        private int _temperatureTemplateLength;
        // COLD ALLOC: char[64] — localized pressure metric template buffer — owner: SuitHUDV4CanvasOverlay
        private char[] _pressureTemplateBuffer = new char[64];
        private int _pressureTemplateLength;
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
        private HectonUIScaler _cachedUiScaler;
        private HectonSurvivalSystem _depthSignalSource;
        private int _cachedHullStressWhisperBucket = int.MinValue;
        private string _cachedHullStressWhisperText;
        private bool _cachedHullStressWhisperRtl;
        // COLD ALLOC: char[96] — cached hull-stress whisper text buffer — owner: SuitHUDV4CanvasOverlay
        private char[] _cachedHullStressWhisperBuffer = new char[96];
        private int _cachedHullStressWhisperLength;

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
            public int LabelKeyHash;
            public char[] ValueBuffer;
            public int CachedLabelKeyHash;
            public bool HasCachedLabelKeyHash;
            public int CachedRoundedValue;
            public bool HasCachedRoundedValue;
            public int CachedLabelWhisperVersion;
            public int CachedValueWhisperVersion;
            public float CachedFillAmount;
            public bool HasCachedFillAmount;
            public Color CachedIconColor;
            public Color CachedRingBackColor;
            public Color CachedRingFillColor;
            public Color CachedRingFrameColor;
            public Color CachedLabelColor;
            public Color CachedValueColor;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            LocalizationManager.OnCorruptionVisualStateChanged += HandleCorruptionVisualStateChanged;
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
            LocalizationManager.OnCorruptionVisualStateChanged -= HandleCorruptionVisualStateChanged;
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

            HectonUIScaler uiScaler = ResolveUiScaler();
            if (uiScaler != null)
                uiScaler.Configure(new Vector2(1600f, 900f), 0.5f);

            CanvasScaler scaler = ResolveCanvasScaler();
            if (scaler != null && scaler.enabled)
                scaler.enabled = false;
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
            if (scaler != null && scaler.enabled)
                return false;

            return true;
        }

        private void EnsureHierarchy()
        {
            targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            RectTransform parentRoot = ResolveUiParent(targetCanvas);
            if (_root == null)
                _root = FindChildRect(parentRoot, RootName);

            if (_root == null && parentRoot != null && parentRoot != targetCanvas.transform)
                _root = FindChildRect(targetCanvas.transform, RootName);

            if (_root == null)
            {
                _root = CreateRect(RootName, parentRoot);
                Stretch(_root, 0f, 0f, 0f, 0f);
                _root.SetAsLastSibling();
                _layoutBuilt = false;
                InvalidateVisualCaches();
            }
            else if (parentRoot != null && _root.parent != parentRoot)
            {
                _root.SetParent(parentRoot, false);
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

            _oxygenGauge = CreateGauge("Gauge_O2", _gaugeClusterRoot, new Vector2(-resolvedGaugeColumnSpacing, 0f), GetOxygenIconSprite(), _HudOxygenKeyHash);
            _healthGauge = CreateGauge("Gauge_HLT", _gaugeClusterRoot, Vector2.zero, GetHealthIconSprite(), _HudHullKeyHash);
            _powerGauge = CreateGauge("Gauge_PWR", _gaugeClusterRoot, new Vector2(resolvedGaugeColumnSpacing, 0f), GetEnergyIconSprite(), _HudPowerKeyHash);

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
            float energyCurrent = survival != null ? survival.Energy : power * 100f;
            float healthCurrent = survival != null ? survival.Integrity : health * 100f;
            float stressPulse = UpdateStressPulse(dt);
            Color pulsedPrimary = ResolveStressPulseColor(primary, warning, stressPulse, stressPulseBrightnessBoost, stressPulseWarningBlend);
            Color pulsedDim = ResolveStressPulseColor(dim, warning, stressPulse, stressPulseBrightnessBoost * 0.45f, stressPulseWarningBlend * 0.38f);
            Color pulsedWarning = ResolveStressPulseColor(warning, primary, stressPulse, stressPulseBrightnessBoost * 0.22f, 0f);
            LocalizationManager manager = LocalizationManager.Instance;
            bool hullStressWhisperMode = ShouldUseHullStressWhisperMode(manager);
            string hullStressWhisperText = hullStressWhisperMode ? ResolveHullStressWhisperText(manager) : null;

            float targetTemp = EstimateTemperature(depth);
            _displayTemperature = Mathf.Lerp(_displayTemperature, targetTemp, 1f - Mathf.Exp(-4f * dt));
            float depthDelta = depth - _lastDepth;
            _lastDepth = depth;
            ApplyStaticStyleIfNeeded(primary, secondary, dim, warning);
            ApplyStressPulseStyle(primary, warning, stressPulse);

            float localizedDepth = LocalizedMeasurementFormatter.ConvertDistanceMeters(depth, _localizedMeasurementLanguage);
            float localizedTemperature = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(_displayTemperature, _localizedMeasurementLanguage);
            bool localizedRtl = LocalizedMeasurementFormatter.IsRightToLeft(_localizedMeasurementLanguage);
            char[] hullStressWhisperBuffer = null;
            int hullStressWhisperLength = 0;
            int hullStressWhisperVersion = int.MinValue;
            if (hullStressWhisperMode)
                GetHullStressWhisperBuffer(manager, out hullStressWhisperBuffer, out hullStressWhisperLength, out hullStressWhisperVersion);

            if (hullStressWhisperMode)
            {
                SetCharBufferIfChanged(_suitLabel, hullStressWhisperBuffer, hullStressWhisperLength, _cachedHullStressWhisperRtl, Alpha(primary, 0.95f), hullStressWhisperVersion, ref _appliedSuitLabelVersion, ref _appliedSuitLabelColor);
                SetCharBufferIfChanged(_headingLabel, hullStressWhisperBuffer, hullStressWhisperLength, _cachedHullStressWhisperRtl, Alpha(dim, 0.58f), hullStressWhisperVersion, ref _appliedHeadingLabelVersion, ref _appliedHeadingLabelColor);
            }
            else
            {
                ResolveSuitLabelBuffer(out char[] suitLabelBuffer, out int suitLabelLength, out int suitLabelVersion, out bool suitLabelRtl);
                SetCharBufferIfChanged(_suitLabel, suitLabelBuffer, suitLabelLength, suitLabelRtl, Alpha(primary, 0.95f), suitLabelVersion, ref _appliedSuitLabelVersion, ref _appliedSuitLabelColor);
                HeadingLabelCacheEntry headingEntry = ResolveHeadingLabelEntry(Mathf.RoundToInt(heading));
                int headingVersion = Mathf.RoundToInt(heading) % 360;
                if (headingVersion < 0)
                    headingVersion += 360;
                SetCharBufferIfChanged(_headingLabel, headingEntry.Buffer, headingEntry.Length, false, Alpha(dim, 0.58f), headingVersion, ref _appliedHeadingLabelVersion, ref _appliedHeadingLabelColor);
            }

            if (hullStressWhisperMode)
            {
                SetCharBufferIfChanged(_depthLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedPrimary, 0.96f), hullStressWhisperVersion, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                SetCharBufferIfChanged(_temperatureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedDim, 0.84f), hullStressWhisperVersion, ref _appliedTemperatureWhisperVersion, ref _appliedTemperatureColor);
                SetCharBufferIfChanged(_pressureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedDim, 0.64f), hullStressWhisperVersion, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                _hasAppliedDepthValue = false;
                _hasAppliedTemperatureTenths = false;
                _hasAppliedPressureTenths = false;
            }
            else
            {
                _appliedDepthWhisperVersion = int.MinValue;
                _appliedTemperatureWhisperVersion = int.MinValue;
                _appliedPressureWhisperVersion = int.MinValue;
                SetMetricIntTemplateIfChanged(_depthLabel, _depthTemplateBuffer, _depthTemplateLength, Mathf.RoundToInt(localizedDepth), localizedRtl, Alpha(pulsedPrimary, 0.96f), ref _appliedDepthValue, ref _hasAppliedDepthValue, ref _appliedDepthColor);
                SetMetricFloatTenthsTemplateIfChanged(_temperatureLabel, _temperatureTemplateBuffer, _temperatureTemplateLength, localizedTemperature, localizedRtl, Alpha(pulsedDim, 0.84f), ref _appliedTemperatureTenths, ref _hasAppliedTemperatureTenths, ref _appliedTemperatureColor);
                SetMetricFloatTenthsTemplateIfChanged(_pressureLabel, _pressureTemplateBuffer, _pressureTemplateLength, pressure, localizedRtl, Alpha(pulsedDim, 0.64f), ref _appliedPressureTenths, ref _hasAppliedPressureTenths, ref _appliedPressureColor);
            }

            Color statusColor = PickAccent(oxygen, power, health, safeDepthNormalized, pulsedPrimary, pulsedWarning);
            if (hullStressWhisperMode)
            {
                _hasAppliedStatusKeyHash = false;
                SetCharBufferIfChanged(_statusLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, statusColor, hullStressWhisperVersion, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
            }
            else
            {
                _appliedStatusWhisperVersion = int.MinValue;
                int statusKeyHash = ResolveStatusKeyHash(oxygen, power, health, safeDepthNormalized, depth, safeDepth, depthDelta);
                SetLocalizedKeyIfChanged(_statusLabel, statusKeyHash, localizedRtl, statusColor, ref _appliedStatusKeyHash, ref _hasAppliedStatusKeyHash, ref _appliedStatusLabelColor);
            }

            Color oxygenAccent = pulsedPrimary;
            Color healthAccent = Color.Lerp(pulsedPrimary, pulsedDim, 0.24f);
            Color energyAccent = Color.Lerp(pulsedPrimary, pulsedWarning, 0.28f);

            UpdateGauge(ref _oxygenGauge, oxygen, oxygenCurrent, oxygenAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion);
            UpdateGauge(ref _healthGauge, health, healthCurrent, healthAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion);
            UpdateGauge(ref _powerGauge, power, energyCurrent, energyAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion);
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

        private void HandleCorruptionVisualStateChanged()
        {
            _cachedHullStressWhisperBucket = int.MinValue;
            _cachedHullStressWhisperText = null;
            InvalidateVisualCaches();
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

        private void ResolveSuitLabelBuffer(out char[] buffer, out int length, out int version, out bool rtl)
        {
            string label = ResolveSuitLabel();
            _cachedSuitLabelRtl = LocalizedMeasurementFormatter.IsRightToLeft(LocRegistry.ActiveLanguage);
            CopyTextToBuffer(label.AsSpan(), ref _cachedSuitLabelBuffer, out _cachedSuitLabelLength);
            _cachedSuitLabelVersion = string.IsNullOrEmpty(label) ? 0 : label.GetHashCode();

            buffer = _cachedSuitLabelBuffer;
            length = _cachedSuitLabelLength;
            version = _cachedSuitLabelVersion;
            rtl = _cachedSuitLabelRtl;
        }

        private static string ResolveCardinal(float heading)
        {
            if (heading >= 315f || heading < 45f) return "N";
            if (heading < 135f) return "E";
            if (heading < 225f) return "S";
            return "W";
        }

        private int ResolveStatusKeyHash(float oxygen, float power, float health, float safeDepthNormalized, float depth, float safeDepth, float depthDelta)
        {
            if (safeDepthNormalized <= 0.08f || depth >= safeDepth)
                return _StatusPressureLimitExceededKeyHash;
            if (safeDepthNormalized <= 0.22f)
                return _StatusApproachingSafeDepthKeyHash;
            if (health <= 0.2f)
                return _StatusSuitDamageCriticalKeyHash;
            if (oxygen <= 0.2f)
                return _StatusOxygenReserveLowKeyHash;
            if (power <= 0.2f)
                return _StatusPowerCellsLowKeyHash;
            if (flashlight != null && flashlight.IsOverheated)
                return _StatusLampThermalLimitKeyHash;
            if (PlayerPDA.IsOpen)
                return _StatusSuitLinkRoutingPdaKeyHash;

            if (depthDelta > 0.04f)
                return _StatusLifeSupportNominalDescendingKeyHash;
            if (depthDelta < -0.04f)
                return _StatusLifeSupportNominalAscendingKeyHash;
            return _StatusLifeSupportNominalStableKeyHash;
        }

        private void RebuildLocalizationCache()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            _localizedMeasurementLanguage = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            BuildMetricTemplate(ref _depthTemplateBuffer, out _depthTemplateLength, _HudDepthKeyHash, ResolveDistanceUnitKeyHash(_localizedMeasurementLanguage), DepthNumberToken.AsSpan(), prependNegativeSign: true);
            BuildMetricTemplate(ref _temperatureTemplateBuffer, out _temperatureTemplateLength, _HudTemperatureKeyHash, ResolveTemperatureUnitKeyHash(_localizedMeasurementLanguage), FixedTenthsNumberToken.AsSpan(), prependNegativeSign: false);
            BuildMetricTemplate(ref _pressureTemplateBuffer, out _pressureTemplateLength, _HudPressureKeyHash, _HudAtmKeyHash, FixedTenthsNumberToken.AsSpan(), prependNegativeSign: false);
        }

        private static bool ShouldUseHullStressWhisperMode(LocalizationManager manager)
        {
            return manager != null && manager.GetHullStressCorruptionIntensity() > 0.9f;
        }

        private string ResolveHullStressWhisperText(LocalizationManager manager)
        {
            if (manager == null)
            {
                const string defaultWhisper = "THE SEA IS INSIDE THE GLASS";
                _cachedHullStressWhisperBucket = 0;
                _cachedHullStressWhisperText = defaultWhisper;
                CopyTextToBuffer(defaultWhisper.AsSpan(), ref _cachedHullStressWhisperBuffer, out _cachedHullStressWhisperLength);
                return defaultWhisper;
            }

            int bucket = manager.GetHullStressCorruptionBucket();
            if (_cachedHullStressWhisperBucket == bucket && !string.IsNullOrEmpty(_cachedHullStressWhisperText))
                return _cachedHullStressWhisperText;

            _cachedHullStressWhisperBucket = bucket;
            _cachedHullStressWhisperText = manager.ApplyHullStressCorruptionIfNeeded(
                manager.GetHullStressHudWhisper("THE SEA IS INSIDE THE GLASS"));
            CopyTextToBuffer(_cachedHullStressWhisperText.AsSpan(), ref _cachedHullStressWhisperBuffer, out _cachedHullStressWhisperLength);
            return _cachedHullStressWhisperText;
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

        private static void UpdateGauge(ref GaugeRefs gauge, float normalized, float currentValue, Color primary, Color dim, Color warning, bool localizedRtl, char[] hullStressWhisperBuffer, int hullStressWhisperLength, int hullStressWhisperVersion)
        {
            float clamped = Mathf.Clamp01(normalized);
            Color accent = clamped <= 0.2f ? warning : primary;
            bool hullStressWhisperMode = hullStressWhisperBuffer != null && hullStressWhisperLength > 0;
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
                if (hullStressWhisperMode)
                {
                    gauge.HasCachedLabelKeyHash = false;
                    SetCharBufferIfChanged(gauge.Label, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, labelColor, hullStressWhisperVersion, ref gauge.CachedLabelWhisperVersion, ref gauge.CachedLabelColor);
                }
                else
                {
                    gauge.CachedLabelWhisperVersion = int.MinValue;
                    SetLocalizedKeyIfChanged(gauge.Label, gauge.LabelKeyHash, localizedRtl, labelColor, ref gauge.CachedLabelKeyHash, ref gauge.HasCachedLabelKeyHash, ref gauge.CachedLabelColor);
                }
            }

            Color valueColor = Alpha(accent, 0.98f);
            int roundedValue = Mathf.RoundToInt(currentValue);
            if (gauge.Value != null)
            {
                if (hullStressWhisperMode)
                {
                    SetCharBufferIfChanged(gauge.Value, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, valueColor, hullStressWhisperVersion, ref gauge.CachedValueWhisperVersion, ref gauge.CachedValueColor);
                    gauge.HasCachedRoundedValue = false;
                }
                else
                {
                    gauge.CachedValueWhisperVersion = int.MinValue;
                    SetNumericIntIfChanged(gauge.Value, gauge.ValueBuffer, roundedValue, valueColor, ref gauge.CachedRoundedValue, ref gauge.HasCachedRoundedValue, ref gauge.CachedValueColor);
                }
            }

            if (gauge.Sub != null && gauge.Sub.gameObject.activeSelf)
                SetLocalizedRtlState(gauge.Sub, localizedRtl);
        }

        private GaugeRefs CreateGauge(string name, RectTransform parent, Vector2 anchoredPosition, Sprite iconSprite, int labelKeyHash)
        {
            GaugeRefs refs = new GaugeRefs();
            refs.LabelKeyHash = labelKeyHash;
            refs.CachedLabelWhisperVersion = int.MinValue;
            refs.CachedValueWhisperVersion = int.MinValue;

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
            TMP_TextRegistry.SetMetadata(refs.Label, labelKeyHash, LocLayer.Core);

            refs.Value = CreateText(name + "_Value", refs.Root, 15f, FontStyles.Bold, TextAlignmentOptions.Center, 0.98f, ResolveNumericFontAsset());
            Anchor(refs.Value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f + resolvedValueOffsetY), new Vector2(44f, 22f));
            refs.ValueBuffer = new char[12]; // COLD ALLOC: char[12] - gauge numeric buffer - owner: SuitHUDV4CanvasOverlay

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
            label.text = string.Empty;
            TMP_TextRegistry.EnsureRegistered(label);
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

        private static void SetMetricIntTemplateIfChanged(
            TextMeshProUGUI label,
            char[] templateBuffer,
            int templateLength,
            int value,
            bool rtl,
            Color color,
            ref int cachedValue,
            ref bool hasCachedValue,
            ref Color cachedColor)
        {
            if (label == null)
                return;

            if (!hasCachedValue || cachedValue != value)
            {
                SetLocalizedRtlState(label, rtl);
                LocNumericBuffer.Write(templateBuffer.AsSpan(0, templateLength), LocNumericArg.Int(value), out char[] buffer, out int length);
                label.SetCharArray(buffer, 0, length);
                cachedValue = value;
                hasCachedValue = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetMetricFloatTenthsTemplateIfChanged(
            TextMeshProUGUI label,
            char[] templateBuffer,
            int templateLength,
            float value,
            bool rtl,
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
                SetLocalizedRtlState(label, rtl);
                LocNumericBuffer.Write(templateBuffer.AsSpan(0, templateLength), LocNumericArg.Float(roundedTenths * 0.1f), out char[] buffer, out int length);
                label.SetCharArray(buffer, 0, length);
                cachedTenths = roundedTenths;
                hasCachedTenths = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetNumericIntIfChanged(
            TextMeshProUGUI label,
            char[] stagingBuffer,
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
                SetLocalizedRtlState(label, false);
                if (!value.TryFormat(stagingBuffer, out int length))
                    length = 0;

                label.SetCharArray(stagingBuffer, 0, length);
                cachedValue = value;
                hasCachedValue = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetLocalizedKeyIfChanged(
            TextMeshProUGUI label,
            int keyHash,
            bool rtl,
            Color color,
            ref int cachedKeyHash,
            ref bool hasCachedKeyHash,
            ref Color cachedColor)
        {
            if (label == null)
                return;

            if (!hasCachedKeyHash || cachedKeyHash != keyHash)
            {
                SetLocalizedRtlState(label, rtl);
                char[] buffer;
                int length;
                if (LocalizationManager.Instance == null)
                {
                    TryGetFallbackBuffer(keyHash, out buffer, out length);
                }
                else
                {
                    LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length);
                }

                label.SetCharArray(buffer, 0, length);
                cachedKeyHash = keyHash;
                hasCachedKeyHash = true;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetCharBufferIfChanged(
            TextMeshProUGUI label,
            char[] buffer,
            int length,
            bool rtl,
            Color color,
            int version,
            ref int cachedVersion,
            ref Color cachedColor)
        {
            if (label == null || buffer == null)
                return;

            if (cachedVersion != version)
            {
                SetLocalizedRtlState(label, rtl);
                label.SetCharArray(buffer, 0, length);
                cachedVersion = version;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetLocalizedRtlState(TMP_Text label, bool rtl)
        {
            if (label != null && label.isRightToLeftText != rtl)
                label.isRightToLeftText = rtl;
        }

        private static int ResolveDistanceUnitKeyHash(GameLanguage language)
        {
            return language == GameLanguage.English
                ? _HudFeetKeyHash
                : _HudMetersKeyHash;
        }

        private static int ResolveTemperatureUnitKeyHash(GameLanguage language)
        {
            return language == GameLanguage.English
                ? _HudFahrenheitKeyHash
                : _HudCelsiusKeyHash;
        }

        private static bool TryGetFallbackBuffer(int keyHash, out char[] buffer, out int length)
        {
            switch (keyHash)
            {
                case var _ when keyHash == _HudDepthKeyHash:
                    buffer = s_depthLabelChars;
                    length = s_depthLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudTemperatureKeyHash:
                    buffer = s_temperatureLabelChars;
                    length = s_temperatureLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudPressureKeyHash:
                    buffer = s_pressureLabelChars;
                    length = s_pressureLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudAtmKeyHash:
                    buffer = s_atmLabelChars;
                    length = s_atmLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudOxygenKeyHash:
                    buffer = s_gaugeO2LabelChars;
                    length = s_gaugeO2LabelChars.Length;
                    return true;

                case var _ when keyHash == _HudPowerKeyHash:
                    buffer = s_gaugePowerLabelChars;
                    length = s_gaugePowerLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudHullKeyHash:
                    buffer = s_gaugeHullLabelChars;
                    length = s_gaugeHullLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudMetersKeyHash:
                    buffer = s_metersLabelChars;
                    length = s_metersLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudFeetKeyHash:
                    buffer = s_feetLabelChars;
                    length = s_feetLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudCelsiusKeyHash:
                    buffer = s_celsiusLabelChars;
                    length = s_celsiusLabelChars.Length;
                    return true;

                case var _ when keyHash == _HudFahrenheitKeyHash:
                    buffer = s_fahrenheitLabelChars;
                    length = s_fahrenheitLabelChars.Length;
                    return true;

                case var _ when keyHash == _StatusPressureLimitExceededKeyHash:
                    buffer = s_statusPressureLimitExceededChars;
                    length = s_statusPressureLimitExceededChars.Length;
                    return true;

                case var _ when keyHash == _StatusApproachingSafeDepthKeyHash:
                    buffer = s_statusApproachingSafeDepthChars;
                    length = s_statusApproachingSafeDepthChars.Length;
                    return true;

                case var _ when keyHash == _StatusSuitDamageCriticalKeyHash:
                    buffer = s_statusSuitDamageCriticalChars;
                    length = s_statusSuitDamageCriticalChars.Length;
                    return true;

                case var _ when keyHash == _StatusOxygenReserveLowKeyHash:
                    buffer = s_statusOxygenReserveLowChars;
                    length = s_statusOxygenReserveLowChars.Length;
                    return true;

                case var _ when keyHash == _StatusPowerCellsLowKeyHash:
                    buffer = s_statusPowerCellsLowChars;
                    length = s_statusPowerCellsLowChars.Length;
                    return true;

                case var _ when keyHash == _StatusLampThermalLimitKeyHash:
                    buffer = s_statusLampThermalLimitChars;
                    length = s_statusLampThermalLimitChars.Length;
                    return true;

                case var _ when keyHash == _StatusSuitLinkRoutingPdaKeyHash:
                    buffer = s_statusSuitLinkRoutingPdaChars;
                    length = s_statusSuitLinkRoutingPdaChars.Length;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalStableKeyHash:
                    buffer = s_statusLifeSupportNominalStableChars;
                    length = s_statusLifeSupportNominalStableChars.Length;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalAscendingKeyHash:
                    buffer = s_statusLifeSupportNominalAscendingChars;
                    length = s_statusLifeSupportNominalAscendingChars.Length;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalDescendingKeyHash:
                    buffer = s_statusLifeSupportNominalDescendingChars;
                    length = s_statusLifeSupportNominalDescendingChars.Length;
                    return true;
            }

            buffer = Array.Empty<char>();
            length = 0;
            return false;
        }

        private static void BuildMetricTemplate(
            ref char[] buffer,
            out int length,
            int labelKeyHash,
            int unitKeyHash,
            ReadOnlySpan<char> numberToken,
            bool prependNegativeSign)
        {
            char[] labelBuffer;
            int labelLength;
            char[] unitBuffer;
            int unitLength;

            if (LocalizationManager.Instance == null)
            {
                TryGetFallbackBuffer(labelKeyHash, out labelBuffer, out labelLength);
                TryGetFallbackBuffer(unitKeyHash, out unitBuffer, out unitLength);
            }
            else
            {
                LocRegistry.TryGetRawBuffer(labelKeyHash, out labelBuffer, out labelLength);
                LocRegistry.TryGetRawBuffer(unitKeyHash, out unitBuffer, out unitLength);
            }

            int totalLength = labelLength + 2 + (prependNegativeSign ? 1 : 0) + numberToken.Length + 1 + unitLength;
            EnsureCharCapacity(ref buffer, totalLength);

            int writeIndex = 0;
            CopyChars(labelBuffer, labelLength, buffer, ref writeIndex);
            buffer[writeIndex++] = ':';
            buffer[writeIndex++] = ' ';
            if (prependNegativeSign)
                buffer[writeIndex++] = '-';

            for (int i = 0; i < numberToken.Length; i++)
                buffer[writeIndex++] = numberToken[i];

            buffer[writeIndex++] = ' ';
            CopyChars(unitBuffer, unitLength, buffer, ref writeIndex);
            length = writeIndex;
        }

        private void GetHullStressWhisperBuffer(LocalizationManager manager, out char[] buffer, out int length, out int version)
        {
            ResolveHullStressWhisperText(manager);
            _cachedHullStressWhisperRtl = manager != null && LocalizedMeasurementFormatter.IsRightToLeft(manager.CurrentLanguage);
            buffer = _cachedHullStressWhisperBuffer;
            length = _cachedHullStressWhisperLength;
            version = _cachedHullStressWhisperBucket;
        }

        private static void CopyTextToBuffer(ReadOnlySpan<char> source, ref char[] buffer, out int length)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            for (int i = 0; i < source.Length; i++)
                buffer[i] = source[i];

            length = source.Length;
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;

            int capacity = buffer == null ? 32 : buffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] - expanded HUD text staging buffer - owner: SuitHUDV4CanvasOverlay
        }

        private static void CopyChars(char[] source, int sourceLength, char[] destination, ref int destinationIndex)
        {
            for (int i = 0; i < sourceLength; i++)
                destination[destinationIndex++] = source[i];
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
            _appliedSuitLabelVersion = int.MinValue;
            _appliedSuitLabelColor = default;
            _appliedHeadingLabelVersion = int.MinValue;
            _appliedHeadingLabelColor = default;
            _appliedStatusKeyHash = 0;
            _hasAppliedStatusKeyHash = false;
            _appliedStatusWhisperVersion = int.MinValue;
            _appliedStatusLabelColor = default;
            _appliedDepthValue = 0;
            _hasAppliedDepthValue = false;
            _appliedDepthWhisperVersion = int.MinValue;
            _appliedDepthColor = default;
            _appliedTemperatureTenths = 0;
            _hasAppliedTemperatureTenths = false;
            _appliedTemperatureWhisperVersion = int.MinValue;
            _appliedTemperatureColor = default;
            _appliedPressureTenths = 0;
            _hasAppliedPressureTenths = false;
            _appliedPressureWhisperVersion = int.MinValue;
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
            _cachedUiScaler = null;
            _cachedHullStressWhisperBucket = int.MinValue;
            _cachedHullStressWhisperText = null;
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

        private HectonUIScaler ResolveUiScaler()
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
            {
                _cachedUiScaler = null;
                return null;
            }

            if (_cachedUiScaler == null || _cachedUiScaler.gameObject != canvas.gameObject)
                _cachedUiScaler = canvas.GetComponent<HectonUIScaler>();

            if (_cachedUiScaler == null)
                _cachedUiScaler = canvas.gameObject.AddComponent<HectonUIScaler>();

            return _cachedUiScaler;
        }

        private static RectTransform ResolveUiParent(Canvas canvas)
        {
            RectTransform resolved = HectonUIScaler.ResolveContentRoot(canvas);
            return resolved != null
                ? resolved
                : canvas != null ? canvas.transform as RectTransform : null;
        }

        private static RectTransform FindChildRect(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private void CacheResolvedPalette(SuitHUDProfile profile, Color primary, Color secondary, Color dim, Color warning)
        {
            _cachedPaletteProfile = profile;
            _cachedPalettePrimary = primary;
            _cachedPaletteSecondary = secondary;
            _cachedPaletteDim = dim;
            _cachedPaletteWarning = warning;
        }

        private static HeadingLabelCacheEntry ResolveHeadingLabelEntry(int roundedHeading)
        {
            int normalizedHeading = roundedHeading % 360;
            if (normalizedHeading < 0)
                normalizedHeading += 360;

            return s_headingLabels[normalizedHeading];
        }

        private static HeadingLabelCacheEntry[] BuildHeadingLabels()
        {
            HeadingLabelCacheEntry[] labels = new HeadingLabelCacheEntry[360];
            for (int i = 0; i < labels.Length; i++)
            {
                LocNumericBuffer.Write(HeadingNumberTemplate.AsSpan(), LocNumericArg.Int(i), out char[] prefixBuffer, out int prefixLength);
                string cardinal = ResolveCardinal(i);
                char[] labelBuffer = new char[prefixLength + cardinal.Length];
                for (int j = 0; j < prefixLength; j++)
                    labelBuffer[j] = prefixBuffer[j];

                for (int j = 0; j < cardinal.Length; j++)
                    labelBuffer[prefixLength + j] = cardinal[j];

                labels[i] = new HeadingLabelCacheEntry(labelBuffer, labelBuffer.Length);
            }

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

    /// <summary>
    /// Canvas-root scaler that applies a single matrix-driven transform to a dedicated content root instead of using CanvasScaler relayout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton UI Scaler")]
    [RequireComponent(typeof(Canvas))]
    public sealed class HectonUIScaler : MonoBehaviour, ITickable
    {
        private const string ContentRootName = "HectonUI_ScaledRoot";

        [Header("── Scale Policy ──────────────────")]
        [Tooltip("Reference UI resolution used by the root transform matrix.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1600f, 900f);
        [Tooltip("CanvasScaler-compatible logarithmic width/height blend. 0 = width, 1 = height.")]
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
        [Tooltip("Lower clamp for the matrix scale to keep the HUD readable on 720p displays.")]
        [SerializeField, Range(0.5f, 1.5f)] private float minimumScale = 0.72f;
        [Tooltip("Upper clamp for the matrix scale so HUD chrome does not bloat on larger displays.")]
        [SerializeField, Range(0.75f, 2f)] private float maximumScale = 1.35f;

        private Canvas _targetCanvas;
        private RectTransform _contentRoot;
        private bool _registeredToTickManager;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private float _lastAppliedScale = -1f;
        private Vector2 _lastAppliedReferenceResolution = Vector2.zero;
        private float _lastAppliedMatch = -1f;
        private Matrix4x4 _uiMatrix = Matrix4x4.identity;

        /// <summary>Current matrix applied to the scaled content root.</summary>
        public Matrix4x4 CurrentMatrix => _uiMatrix;

        /// <summary>Scaled content parent used by first-party HUD overlays.</summary>
        public RectTransform ContentRoot => EnsureContentRoot();

        private void OnEnable()
        {
            ResolveCanvas();
            EnsureContentRoot();
            ApplyScale(force: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ApplyScale(force: false);
        }

        /// <summary>
        /// Configures the scaler from an owning canvas system.
        /// </summary>
        public void Configure(Vector2 nextReferenceResolution, float nextMatchWidthOrHeight)
        {
            Vector2 sanitizedResolution = new Vector2(
                Mathf.Max(1f, nextReferenceResolution.x),
                Mathf.Max(1f, nextReferenceResolution.y));
            float sanitizedMatch = Mathf.Clamp01(nextMatchWidthOrHeight);
            if (Approximately(referenceResolution, sanitizedResolution) &&
                Mathf.Approximately(matchWidthOrHeight, sanitizedMatch))
            {
                return;
            }

            referenceResolution = sanitizedResolution;
            matchWidthOrHeight = sanitizedMatch;
            ApplyScale(force: true);
        }

        /// <summary>
        /// Resolves the scaled content parent for a canvas, or falls back to the canvas RectTransform when no scaler is present.
        /// </summary>
        public static RectTransform ResolveContentRoot(Canvas canvas)
        {
            if (canvas == null)
                return null;

            if (canvas.TryGetComponent(out HectonUIScaler scaler))
            {
                RectTransform contentRoot = scaler.ContentRoot;
                if (contentRoot != null)
                    return contentRoot;
            }

            return canvas.transform as RectTransform;
        }

        private void ResolveCanvas()
        {
            if (_targetCanvas == null)
                _targetCanvas = GetComponent<Canvas>();
        }

        private RectTransform EnsureContentRoot()
        {
            ResolveCanvas();
            if (_targetCanvas == null)
                return null;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return null;

            if (_contentRoot == null || _contentRoot.gameObject == null)
                _contentRoot = FindExistingChild(canvasRoot, ContentRootName);

            if (_contentRoot == null)
            {
                // COLD ALLOC: GameObject[1] — matrix-scaled HUD content root — owner: HectonUIScaler
                GameObject rootObject = new GameObject(ContentRootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _contentRoot = rootObject.GetComponent<RectTransform>();
                _contentRoot.SetParent(canvasRoot, false);
            }

            _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.localRotation = Quaternion.identity;
            return _contentRoot;
        }

        private void ApplyScale(bool force)
        {
            RectTransform contentRoot = EnsureContentRoot();
            if (contentRoot == null)
                return;

            int screenWidth = Mathf.Max(1, Screen.width);
            int screenHeight = Mathf.Max(1, Screen.height);
            if (!force &&
                screenWidth == _lastScreenWidth &&
                screenHeight == _lastScreenHeight &&
                Approximately(referenceResolution, _lastAppliedReferenceResolution) &&
                Mathf.Approximately(matchWidthOrHeight, _lastAppliedMatch))
            {
                return;
            }

            float scale = ComputeScale(screenWidth, screenHeight);
            if (!force &&
                Mathf.Approximately(scale, _lastAppliedScale) &&
                contentRoot.sizeDelta == referenceResolution)
            {
                _lastScreenWidth = screenWidth;
                _lastScreenHeight = screenHeight;
                return;
            }

            _uiMatrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            contentRoot.sizeDelta = referenceResolution;
            contentRoot.localScale = new Vector3(_uiMatrix.m00, _uiMatrix.m11, 1f);

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastAppliedScale = scale;
            _lastAppliedReferenceResolution = referenceResolution;
            _lastAppliedMatch = matchWidthOrHeight;
        }

        private float ComputeScale(int screenWidth, int screenHeight)
        {
            float scaleX = screenWidth / Mathf.Max(1f, referenceResolution.x);
            float scaleY = screenHeight / Mathf.Max(1f, referenceResolution.y);
            float logWidth = Mathf.Log(Mathf.Max(0.0001f, scaleX), 2f);
            float logHeight = Mathf.Log(Mathf.Max(0.0001f, scaleY), 2f);
            float blendedScale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight));
            return Mathf.Clamp(blendedScale, minimumScale, maximumScale);
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTickManager = false;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y);
        }
    }
}
