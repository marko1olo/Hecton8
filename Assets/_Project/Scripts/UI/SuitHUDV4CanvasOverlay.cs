using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NASAPunk.Visor;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit HUD V4 Canvas Overlay")]
    [RequireComponent(typeof(Canvas))]
    public sealed class SuitHUDV4CanvasOverlay : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, IOriginShiftListener, IUIService
    {
        private static readonly List<SuitHUDV4CanvasOverlay> s_activeOverlays = new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        private static readonly List<GameObject> s_sceneRootResolveBuffer = new List<GameObject>(16);
        private static readonly HeadingLabelCacheEntry[] s_headingLabels = BuildHeadingLabels();
        private const string PrimaryHudCanvasName = "Suit_HUD_Canvas";
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
        private const int QuickbarSlotCount = 4;
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
        private const string DefaultToolDepletedLabel = "TOOL DEPLETED";
        private const string DefaultCriticalLabel = "CRITICAL";
        private const string DepthNumberToken = "{N0:F0}";
        private const string FixedTenthsNumberToken = "{N0:F1}";
        private const string HeadingNumberTemplate = "HEADING {N0:D3} / ";
        private const int SlowCadenceFrameModulo = 30;
        private const int MediumCadenceFrameModulo = 6;
        private const int StaticCanvasSortingOrder = 10;
        private const int LowCadenceCanvasSortingOrder = 20;
        private const int HighCadenceCanvasSortingOrder = 30;
        private const float OxygenGaugeDamping = 12f;
        private const float HealthGaugeDamping = 8f;
        private const float BatteryGaugeDamping = 6f;
        private const float GaugeSmoothingEpsilon = 0.01f;
        private const float FillWriteEpsilon = 0.0005f;
        private const float OxygenStreamThreshold01 = 0.005f;
        private const float DepthStreamThresholdMeters = 0.5f;
        private const float TemperatureStreamThreshold = 0.1f;
        private const float PressureStreamThreshold = 0.1f;
        private const float HeadingStreamThresholdDegrees = 1f;
        private const float ToolDepletedWarningDurationSeconds = 2.25f;
        private const float CorruptedModeThreshold = 0.75f;
        private const float JitterAmplitudePixels = 7f;
        private const float JitterFrequencyRadians = 23f;
        private const float DiegeticHudDistanceMeters = 0.5f;
        private const float DiegeticHudWorldScale = 0.0005f;
        private const float ProjectionNearClipSafetyPaddingMeters = 0.05f;
        private const float ProjectionPosePositionTolerance = 0.0001f;
        private const float ProjectionPoseScaleTolerance = 0.000001f;
        private const string AcousticRadarShaderPath = "Assets/_Project/Art/Shaders/Hecton_HUD_AcousticRadarOverlay.shader";
        private const string ThreatChevronShaderPath = "Assets/_Project/Art/Shaders/Hecton_ScannerMarkerInstanced.shader";
        private const string WorldGeometrySortingLayer = "WorldGeometry";
        private const int MaxThreatChevronCount = 4;
        private const int HudInternalLayerIndex = 17;
        private const float ThreatChevronPlaneBiasMeters = 0.0004f;
        private static int UiLayerIndex = -1;
        private static bool s_layerCacheInitialized;
        private const int DefaultLayerIndex = 0;
        private static readonly int _ThreatChevronBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ThreatChevronFlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _ThreatChevronFlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _ThreatChevronFillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int _AcousticRadarTexId = Shader.PropertyToID("_AcousticRadarTex");
        private static readonly int _AcousticRadarPrimaryColorId = Shader.PropertyToID("_PrimaryColor");
        private static readonly int _AcousticRadarWarningColorId = Shader.PropertyToID("_WarningColor");
        private static readonly int _AcousticRadarOpacityId = Shader.PropertyToID("_OverlayOpacity");
        private static readonly int _AcousticRadarGlitchId = Shader.PropertyToID("_GlitchAmount");
        private static readonly int _AcousticRadarInnerEdgeId = Shader.PropertyToID("_InnerEdge");
        private static readonly int _AcousticRadarWaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
        private static readonly int _AcousticRadarBandThicknessId = Shader.PropertyToID("_BandThickness");
        private static readonly int _AcousticRadarPulseFrequencyId = Shader.PropertyToID("_PulseFrequency");
        private static readonly int _AcousticRadarRadarIntensityId = Shader.PropertyToID("_RadarIntensity");
#if UNITY_EDITOR
        private static readonly Color s_gizmoCanvasBoundsColor = new Color(0.12f, 0.68f, 0.92f, 0.65f);
        private static readonly Color s_gizmoElementFillColor = new Color(0.12f, 0.68f, 0.92f, 0.08f);
        private static readonly Color s_gizmoTextColor = new Color(0.92f, 0.92f, 0.92f, 0.72f);
        private static readonly Color s_gizmoProjectionPlaneColor = new Color(1f, 0.5f, 0f, 0.5f);
        private static GUIStyle s_gizmoLabelStyle;
#endif
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
        private static readonly char[] s_quickbarSlotOneChars = "1".ToCharArray();
        private static readonly char[] s_quickbarSlotTwoChars = "2".ToCharArray();
        private static readonly char[] s_quickbarSlotThreeChars = "3".ToCharArray();
        private static readonly char[] s_quickbarSlotFourChars = "4".ToCharArray();
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
        private static readonly char[] s_emptyHudChars = Array.Empty<char>();
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

        private static void EnsureLayerCache()
        {
            if (s_layerCacheInitialized)
                return;

            UiLayerIndex = LayerMask.NameToLayer("UI");
            s_layerCacheInitialized = true;
        }
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

        private readonly struct FixedCharBuffer
        {
            private readonly char[] _buffer;

            public FixedCharBuffer(char[] buffer)
            {
                _buffer = buffer;
            }

            public char[] Buffer => _buffer;

            public bool TryWriteInt(int value, out int length)
            {
                if (_buffer == null)
                {
                    length = 0;
                    return false;
                }

                return value.TryFormat(_buffer.AsSpan(), out length);
            }

            public bool TryWriteTemplateInt(ReadOnlySpan<char> template, int value, out int length)
            {
                if (_buffer == null)
                {
                    length = 0;
                    return false;
                }

                return LocNumericBuffer.TryWrite(template, _buffer.AsSpan(), LocNumericArg.Int(value), out length);
            }

            public bool TryWriteTemplateFloatTenths(ReadOnlySpan<char> template, int roundedTenths, out int length)
            {
                if (_buffer == null)
                {
                    length = 0;
                    return false;
                }

                return LocNumericBuffer.TryWrite(template, _buffer.AsSpan(), LocNumericArg.Float(roundedTenths * 0.1f), out length);
            }
        }

        private const int LayoutRevision = 12;
        private const float AutoResolveRetryInterval = 1f;
        private static readonly Vector2 DefaultUiReferenceResolution = new Vector2(1600f, 900f);
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
        [SerializeField] private float projectionPlaneDistance = DiegeticHudDistanceMeters;
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

        [Header("Quickbar Layout")]
        [SerializeField] private Vector2 quickbarOffset = new Vector2(0f, 94f);
        [SerializeField] private Vector2 quickbarSize = new Vector2(244f, 64f);
        [SerializeField] private float quickbarSlotSize = 44f;
        [SerializeField] private float quickbarSlotGap = 8f;

        [Header("Acoustic Radar")]
        [SerializeField]
        [Tooltip("UI shader used to visualize the 360-bin passive acoustic ring on the visor edges.")]
        private Shader acousticRadarShader;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base opacity of the acoustic radar overlay when the audio ring contains valid energy.")]
        private float acousticRadarOpacity = 0.18f;
        [SerializeField, Range(0.01f, 0.5f)]
        [Tooltip("Normalized inner edge radius where the acoustic radar begins to appear.")]
        private float acousticRadarInnerEdge = 0.74f;
        [SerializeField, Range(0.01f, 0.35f)]
        [Tooltip("Normalized edge band thickness occupied by the acoustic radar.")]
        private float acousticRadarBandThickness = 0.18f;
        [SerializeField, Range(0f, 8f)]
        [Tooltip("World-agnostic sine-wave amplitude used to make the passive radar feel unstable and alive.")]
        private float acousticRadarWaveAmplitude = 2.4f;
        [SerializeField, Range(0f, 16f)]
        [Tooltip("Low-amplitude pulse frequency applied to acoustic edge highlights.")]
        private float acousticRadarPulseFrequency = 3.2f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Additional glitch strength blended into the acoustic overlay during corruption or trauma spikes.")]
        private float acousticRadarGlitchStrength = 0.2f;

        [Header("Threat AR")]
        [SerializeField]
        [Tooltip("Instanced shader used by diegetic threat chevrons on the visor plane.")]
        private Shader threatChevronShader;
        [SerializeField]
        [Tooltip("Base color used by high-threat diegetic warning chevrons.")]
        private Color threatChevronColor = new Color(1f, 0.18f, 0.2f, 0.88f);
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum threat cell value required before a diegetic warning chevron is emitted.")]
        private float threatChevronThreshold = 0.62f;
        [SerializeField, Min(1f)]
        [Tooltip("Maximum radius in meters around the player scanned for high-threat ecosystem cells.")]
        private float threatChevronRadiusMeters = 50f;
        [SerializeField, Min(0f)]
        [Tooltip("Inset in pixels applied from the visor edge before the warning chevrons clamp.")]
        private float threatChevronEdgeInsetPixels = 96f;
        [SerializeField, Min(4f)]
        [Tooltip("Approximate chevron size in pixels along the visor plane.")]
        private float threatChevronSizePixels = 26f;
        [SerializeField, Range(0f, 0.4f)]
        [Tooltip("Subtle fill alpha used by the threat chevron instanced material.")]
        private float threatChevronFillAlpha = 0.08f;
        [SerializeField, Range(0f, 40f)]
        [Tooltip("Low-amplitude flicker used by threat chevrons to keep them readable without becoming UI noise.")]
        private float threatChevronFlickerFrequency = 7f;
        [SerializeField, Range(0f, 0.4f)]
        [Tooltip("Flicker amplitude used by the threat chevrons.")]
        private float threatChevronFlickerIntensity = 0.04f;

        private const string RootName = "HUD_V4_CanvasRoot";

        private RectTransform _root;
        private RectTransform _ornamentRoot;
        private CanvasGroup _rootCanvasGroup;
        private CanvasGroup _ornamentCanvasGroup;
        private RectTransform _headerRoot;
        private RectTransform _reticleRoot;
        private RectTransform _telemetryChromeRoot;
        private RectTransform _telemetrySupplementRoot;
        private RectTransform _telemetryRoot;
        private RectTransform _gaugeClusterRoot;
        private RectTransform _quickbarRoot;
        private CanvasGroup _headerCanvasGroup;
        private CanvasGroup _telemetryChromeCanvasGroup;
        private CanvasGroup _telemetrySupplementCanvasGroup;
        private CanvasGroup _statusCanvasGroup;
        private CanvasGroup _quickbarCanvasGroup;
        private Image _biosBackdrop;
        private Image _acousticRadarOverlay;
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
        private readonly QuickbarSlotRefs[] _quickbarSlots = new QuickbarSlotRefs[QuickbarSlotCount];
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
            s_sceneRootResolveBuffer.Clear();
            s_ringFillSprite = null;
            s_ringFrameSprite = null;
            s_oxygenIconSprite = null;
            s_healthIconSprite = null;
            s_energyIconSprite = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeHudCanvasBindings()
        {
            if (!Application.isPlaying)
                return;

            Canvas canvas = FindSceneCanvasByName(PrimaryHudCanvasName);
            if (canvas == null)
                return;

            if (!canvas.TryGetComponent(out HectonUIScaler _))
                canvas.gameObject.AddComponent<HectonUIScaler>();

            if (!canvas.TryGetComponent(out SuitHUDV4CanvasOverlay _))
                canvas.gameObject.AddComponent<SuitHUDV4CanvasOverlay>();
        }

        private SuitData _activeSuit;
        private SuitHUDProfile _activeProfile;
        private SuitHUDProfile _cachedPaletteProfile;
        private Color _cachedPalettePrimary;
        private Color _cachedPaletteSecondary;
        private Color _cachedPaletteDim;
        private Color _cachedPaletteWarning;
        private float _displayTemperature = 8f;
        private float _displayOxygen01 = 1f;
        private float _displayHealth01 = 1f;
        private float _displayPower01 = 1f;
        private float _lastDepth;
        private float _depthMeters;
        private int _uiCadenceFrame;
        private float _lastStreamedOxygen01 = float.NaN;
        private float _lastStreamedPower01 = float.NaN;
        private float _lastStreamedHealth01 = float.NaN;
        private float _lastStreamedDepthMeters = float.NaN;
        private float _lastStreamedTemperature = float.NaN;
        private float _lastStreamedPressure = float.NaN;
        private float _lastStreamedHeadingDegrees = float.NaN;
        private bool _quickbarVisualsInitialized;
        private bool _layoutBuilt;
        [SerializeField, HideInInspector] private int _appliedLayoutRevision;
        private float _nextAutoResolveAt;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _pendingRuntimeCanvasRefresh = true;
        private bool _forceResolveOnSlowTick = true;
        private bool _pendingDepthSignalRefresh = true;
        private SuitData _cachedSuitLabelSuit;
        private string _cachedSuitLabelOverride;
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
        private float _traumaGlitchIntensity;
        private float _traumaRecoilScalar;
        private float _traumaTransportPower01 = 1f;
        private float _traumaHullIntegrity01 = 1f;
        private float _toolDepletedWarningTimer;
        private float _threatChevronPulseTime;
        private float _jitterTime;
        private int _toolDepletedVersion;
        private int _toolDepletedHashId;
        private int _corruptionFrameVersion;
        private bool _biosRecoveryMode;
        private Transform _defaultCanvasParent;
        private int _defaultCanvasSiblingIndex = -1;
        private bool _rootBaseAnchoredPositionCaptured;
        private Vector2 _rootBaseAnchoredPosition;
        private GameLanguage _localizedMeasurementLanguage = GameLanguage.English;
        // COLD ALLOC: char[64] â€” cached suit label staging buffer â€” owner: SuitHUDV4CanvasOverlay
        private char[] _cachedSuitLabelBuffer = new char[64];
        private int _cachedSuitLabelLength;
        // COLD ALLOC: char[64] â€” localized depth metric template buffer â€” owner: SuitHUDV4CanvasOverlay
        private char[] _depthTemplateBuffer = new char[64];
        private int _depthTemplateLength;
        // COLD ALLOC: char[64] â€” localized temperature metric template buffer â€” owner: SuitHUDV4CanvasOverlay
        private char[] _temperatureTemplateBuffer = new char[64];
        private int _temperatureTemplateLength;
        // COLD ALLOC: char[64] â€” localized pressure metric template buffer â€” owner: SuitHUDV4CanvasOverlay
        private char[] _pressureTemplateBuffer = new char[64];
        private int _pressureTemplateLength;
        private char[] _depthDisplayBuffer = new char[64];
        private char[] _temperatureDisplayBuffer = new char[64];
        private char[] _pressureDisplayBuffer = new char[64];
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
        private GraphicRaycaster _cachedGraphicRaycaster;
        private HectonUIScaler _cachedUiScaler;
        private HectonSurvivalSystem _depthSignalSource;
        private IPlayerInventoryService _inventoryService;
        private PlayerInventory _playerInventory;
        private ItemCatalog _itemCatalog;
        private PlayerToolManager _toolManager;
        private SpatialAudioManager _spatialAudioManager;
        private int _lastInventoryVersion = -1;
        private readonly int[] _quickbarSlotHashCache = new int[QuickbarSlotCount];
        private readonly bool[] _quickbarSlotHashResolved = new bool[QuickbarSlotCount];
        private readonly GameObject[] _quickbarSlotPrefabCache = new GameObject[QuickbarSlotCount];
        private int _cachedHullStressWhisperBucket = int.MinValue;
        private string _cachedHullStressWhisperText;
        private bool _cachedHullStressWhisperRtl;
        // COLD ALLOC: char[96] â€” cached hull-stress whisper text buffer â€” owner: SuitHUDV4CanvasOverlay
        private char[] _cachedHullStressWhisperBuffer = new char[96];
        private int _cachedHullStressWhisperLength;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private Material _acousticRadarMaterial;
        private Texture2D _acousticRadarTexture;
        private int _acousticRadarResolution;
        private float _acousticRadarPeakIntensity;
        private Mesh _threatChevronMesh;
        private Material _threatChevronMaterial;
        private NativeArray<Matrix4x4> _threatChevronMatrices;
        // COLD ALLOC: Matrix4x4[4] â€” instanced threat-chevron draw mirror â€” owner: SuitHUDV4CanvasOverlay
        private readonly Matrix4x4[] _threatChevronMatrixMirror = new Matrix4x4[MaxThreatChevronCount];
        // COLD ALLOC: Matrix4x4[1] â€” single-chevron instanced draw bridge for per-chevron alpha fades â€” owner: SuitHUDV4CanvasOverlay
        private readonly Matrix4x4[] _threatChevronSingleDrawMirror = new Matrix4x4[1];
        // COLD ALLOC: float[4] â€” per-chevron alpha cache for alpha-faded threat warnings â€” owner: SuitHUDV4CanvasOverlay
        private readonly float[] _threatChevronAlphaMirror = new float[MaxThreatChevronCount];
        // COLD ALLOC: ThreatChevronState[4] â€” cached top threat-grid chevron slots â€” owner: SuitHUDV4CanvasOverlay
        private readonly ThreatChevronState[] _threatChevronStates = new ThreatChevronState[MaxThreatChevronCount];
        private int _threatChevronVisibleCount;

        public Canvas TargetCanvas => ResolveTargetCanvas();
        public Camera ProjectionCamera => projectionCamera;
        internal static SuitHUDV4CanvasOverlay ActiveRuntimeInstance { get; private set; }

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
            public CanvasGroup CanvasGroup;
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
            public float LastTargetFillAmount;
            public bool HasLastTargetFillAmount;
            public Color CachedIconColor;
            public Color CachedRingBackColor;
            public Color CachedRingFillColor;
            public Color CachedRingFrameColor;
            public Color CachedLabelColor;
            public Color CachedValueColor;
        }

        private struct QuickbarSlotRefs
        {
            public RectTransform Root;
            public Image Backdrop;
            public Image Icon;
            public Image Accent;
            public TextMeshProUGUI Key;
            public Sprite CachedIconSprite;
            public bool CachedIconVisible;
            public bool CachedAvailable;
            public bool CachedActive;
            public Color CachedBackdropColor;
            public Color CachedAccentColor;
            public Color CachedKeyColor;
            public Color CachedIconColor;
        }

        private struct ThreatChevronState
        {
            public Vector3 WorldPosition;
            public float Threat01;
            public bool Active;
        }

        private enum DynamicCanvasCadenceBucket : byte
        {
            Static = 0,
            LowCadence = 1,
            HighCadence = 2
        }

        public bool IsInitialized => isActiveAndEnabled && targetCanvas != null && _root != null && _layoutBuilt;

        private bool _ownsGlobalUiSlot;

        private void OnEnable()
        {
            EnsureLayerCache();
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            LocalizationManager.OnCorruptionVisualStateChanged += HandleCorruptionVisualStateChanged;
            SceneBootstrap.OnGameReady += HandleSceneBootstrapReady;
            PlayerSignalEvents.OnTraumaHudSignal += HandleTraumaHudSignal;
            PlayerSignalEvents.OnToolDepletedSignal += HandleToolDepletedSignal;
            RegisterActiveOverlay();
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();

            if (!Application.isPlaying)
            {
                if (keepVisibleInEditMode)
                {
                    NormalizeCanvas();
                    EnsureHierarchy();
                }

                return;
            }

            HectonFloatingOrigin.RegisterListener(this);
            TryRegisterUiService();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
            TryRegisterRuntimeTick();
            EnsureAcousticRadarRuntimeResources();
            EnsureThreatChevronRuntimeResources();
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
            PlayerSignalEvents.OnTraumaHudSignal -= HandleTraumaHudSignal;
            PlayerSignalEvents.OnToolDepletedSignal -= HandleToolDepletedSignal;
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterUiService();
            UnregisterActiveOverlay();
            UnregisterRuntimeTick();
            ClearDepthSignalSubscription();
            _stressPulseIntensity = 0f;
            _stressPulsePhase = 0f;
            _appliedStressPulseStrength = -1f;
            _toolDepletedWarningTimer = 0f;
            _traumaGlitchIntensity = 0f;
            _traumaRecoilScalar = 0f;
            _traumaTransportPower01 = 1f;
            _traumaHullIntegrity01 = 1f;
            _threatChevronPulseTime = 0f;
            _biosRecoveryMode = false;
            if (_root != null && _rootBaseAnchoredPositionCaptured)
                _root.anchoredPosition = _rootBaseAnchoredPosition;
            _rootBaseAnchoredPositionCaptured = false;
            _threatChevronVisibleCount = 0;
            DisposeAcousticRadarRuntimeResources();
            DisposeThreatChevronRuntimeResources();

            SetRootVisible(false);
        }

        private void OnDestroy()
        {
            UnregisterUiService();
            DisposeAcousticRadarRuntimeResources();
            DisposeThreatChevronRuntimeResources();
        }

        private void TryRegisterUiService()
        {
            if (!Application.isPlaying || _ownsGlobalUiSlot)
                return;

            IUIService current = GlobalRegistry.UI;
            if (current != null && !ReferenceEquals(current, this))
                return;

            GlobalRegistry.RegisterUIService(this);
            _ownsGlobalUiSlot = true;
        }

        private void UnregisterUiService()
        {
            if (!_ownsGlobalUiSlot)
                return;

            GlobalRegistry.UnregisterUIService(this);
            _ownsGlobalUiSlot = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();

            if (!Application.isPlaying && isActiveAndEnabled && keepVisibleInEditMode)
            {
                EditorApplication.delayCall -= HandleDelayedEditModeRefresh;
                EditorApplication.delayCall += HandleDelayedEditModeRefresh;
            }
        }

        private void HandleDelayedEditModeRefresh()
        {
            EditorApplication.delayCall -= HandleDelayedEditModeRefresh;
            if (this == null || Application.isPlaying || !isActiveAndEnabled || !keepVisibleInEditMode)
                return;

            NormalizeCanvas();
        }

        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            Camera previewCamera = projectionCamera;
            if (previewCamera == null)
                previewCamera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;

            if (previewCamera == null)
                return;

            Vector2 referenceResolution = ResolveUiReferenceResolution();
            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
                return;

            float planeDistance = projectionCamera != null
                ? ResolveProjectionPlaneDistance()
                : Mathf.Max(previewCamera.nearClipPlane + ProjectionNearClipSafetyPaddingMeters, ProjectionNearClipSafetyPaddingMeters);
            float halfFovRadians = Mathf.Max(0.001f, previewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float frustumHalfHeight = Mathf.Tan(halfFovRadians) * planeDistance;
            float frustumHalfWidth = frustumHalfHeight * Mathf.Max(0.0001f, previewCamera.aspect);
            float worldScale = Mathf.Max(0.000001f, (frustumHalfHeight * 2f) / referenceResolution.y);

            Transform cameraTransform = previewCamera.transform;
            Vector3 planeCenter = cameraTransform.position + cameraTransform.forward * planeDistance;
            Vector3 frustumRight = cameraTransform.right * frustumHalfWidth;
            Vector3 frustumUp = cameraTransform.up * frustumHalfHeight;
            DrawGizmoQuad(planeCenter, frustumRight, frustumUp, s_gizmoProjectionPlaneColor, false);

            float canvasHalfWidth = referenceResolution.x * worldScale * 0.5f;
            float canvasHalfHeight = referenceResolution.y * worldScale * 0.5f;
            Vector3 canvasRight = cameraTransform.right * canvasHalfWidth;
            Vector3 canvasUp = cameraTransform.up * canvasHalfHeight;
            DrawGizmoQuad(planeCenter, canvasRight, canvasUp, s_gizmoCanvasBoundsColor, false);

            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, headerOffset, new Vector2(620f, 84f), "HEADER");
            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, telemetryOffset, telemetrySize, "TELEMETRY");
            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, gaugeClusterOffset, gaugeClusterSize, "GAUGES");
            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, statusOffset, new Vector2(420f, 24f), "STATUS");
            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, quickbarOffset, quickbarSize, "QUICKBAR");
            DrawGizmoHudElement(planeCenter, cameraTransform.right, cameraTransform.up, worldScale, reticleOffset, new Vector2(22f, 22f), "RETICLE");
        }

        private static void DrawGizmoHudElement(
            Vector3 planeCenter,
            Vector3 localRight,
            Vector3 localUp,
            float worldScale,
            Vector2 pixelOffset,
            Vector2 pixelSize,
            string label)
        {
            Vector3 centerWorld =
                planeCenter +
                (localRight * (pixelOffset.x * worldScale)) +
                (localUp * (pixelOffset.y * worldScale));
            Vector3 halfRight = localRight * (pixelSize.x * worldScale * 0.5f);
            Vector3 halfUp = localUp * (pixelSize.y * worldScale * 0.5f);

            DrawGizmoQuad(centerWorld, halfRight, halfUp, s_gizmoCanvasBoundsColor, true);
            Handles.color = s_gizmoTextColor;
            Handles.Label(centerWorld, label, ResolveGizmoLabelStyle());
        }

        private static void DrawGizmoQuad(Vector3 center, Vector3 halfRight, Vector3 halfUp, Color lineColor, bool drawFillCross)
        {
            Vector3 bottomLeft = center - halfRight - halfUp;
            Vector3 bottomRight = center + halfRight - halfUp;
            Vector3 topRight = center + halfRight + halfUp;
            Vector3 topLeft = center - halfRight + halfUp;

            Gizmos.color = lineColor;
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            if (!drawFillCross)
                return;

            Gizmos.color = s_gizmoElementFillColor;
            Gizmos.DrawLine(bottomLeft, topRight);
            Gizmos.DrawLine(bottomRight, topLeft);
        }

        private static GUIStyle ResolveGizmoLabelStyle()
        {
            if (s_gizmoLabelStyle != null)
                return s_gizmoLabelStyle;

            s_gizmoLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
            s_gizmoLabelStyle.normal.textColor = s_gizmoTextColor;
            return s_gizmoLabelStyle;
        }

        [ContextMenu("Rebuild UI")]
        private void RebuildUiInEditor()
        {
            if (Application.isPlaying)
                return;

            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();
            RefreshAll(0.016f, forceResolve: true);
        }
#endif

        public void Tick(float deltaTime)
        {
            if (_toolDepletedWarningTimer > 0f)
            {
                _toolDepletedWarningTimer = Mathf.Max(0f, _toolDepletedWarningTimer - Mathf.Max(0f, deltaTime));
                _corruptionFrameVersion++;
            }

            if (!_layoutBuilt || _root == null || targetCanvas == null)
                return;

            int cadenceFrame = unchecked(++_uiCadenceFrame);
            bool refreshMediumCadence = cadenceFrame % MediumCadenceFrameModulo == 0;
            bool refreshSlowCadence = cadenceFrame % SlowCadenceFrameModulo == 0;
            RefreshAcousticRadarPayload();
            _threatChevronPulseTime += Mathf.Max(0f, Time.unscaledDeltaTime);
            RefreshVisuals(deltaTime, refreshMediumCadence, refreshSlowCadence);
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterRuntimeTick();
            ProcessPendingRuntimeCanvasRefresh();
            RefreshThreatChevronTargets();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _canvasStateApplied = false;
            _rootBaseAnchoredPositionCaptured = false;
            _rootBaseAnchoredPosition = Vector2.zero;

            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying)
            {
                QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);
                NormalizeCanvas();
                EnsureHierarchy();
                return;
            }

            if (keepVisibleInEditMode)
            {
                NormalizeCanvas();
                EnsureHierarchy();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || _root == null)
                return;

            if (renderPath == RenderPath.ProjectionSource && targetCanvas != null)
                UpdateProjectionCanvasPose(targetCanvas.transform as RectTransform, ResolveUiReferenceResolution());

            RenderThreatChevrons();

            if (!_rootBaseAnchoredPositionCaptured)
            {
                _rootBaseAnchoredPosition = _root.anchoredPosition;
                _rootBaseAnchoredPositionCaptured = true;
            }

            float jitterStrength = Mathf.Clamp01(Mathf.Max(_traumaRecoilScalar, _traumaGlitchIntensity * 0.35f));
            if (jitterStrength <= 0.0001f)
            {
                if (_root.anchoredPosition != _rootBaseAnchoredPosition)
                    _root.anchoredPosition = _rootBaseAnchoredPosition;
                return;
            }

            _jitterTime += Time.unscaledDeltaTime;
            float amplitude = JitterAmplitudePixels * jitterStrength;
            Vector2 jitterOffset = new Vector2(
                Mathf.Sin(_jitterTime * JitterFrequencyRadians) * amplitude,
                Mathf.Sin(_jitterTime * (JitterFrequencyRadians * 0.73f)) * amplitude * 0.58f);
            _root.anchoredPosition = _rootBaseAnchoredPosition + jitterOffset;
        }

        private void HandleSceneBootstrapReady()
        {
            if (!isActiveAndEnabled)
                return;

            _layoutBuilt = false;
            InvalidateVisualCaches();
            TryRegisterRuntimeTick();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizationCache();
            InvalidateVisualCaches();

            if (isActiveAndEnabled)
                QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);
        }

        private void HandleTraumaHudSignal(TraumaHudSignal signal)
        {
            _traumaGlitchIntensity = Mathf.Clamp01(signal.GlitchIntensity);
            _traumaRecoilScalar = Mathf.Clamp01(signal.RecoilScalar);
            _traumaTransportPower01 = Mathf.Clamp01(signal.TransportPower01);
            _traumaHullIntegrity01 = Mathf.Clamp01(signal.HullIntegrity01);
            _biosRecoveryMode = signal.BiosRecoveryMode;
            _corruptionFrameVersion++;
            InvalidateVisualCaches();
        }

        private void HandleToolDepletedSignal(ToolDepletedSignal signal)
        {
            _toolDepletedHashId = signal.ToolHashId;
            _toolDepletedWarningTimer = ToolDepletedWarningDurationSeconds;
            _toolDepletedVersion++;
            InvalidateVisualCaches();
        }

        private void RefreshAll(float dt, bool forceResolve)
        {
            AutoResolve(forceResolve);
            NormalizeCanvas();
            EnsureHierarchy();
            RefreshVisuals(dt);
        }

        private void QueueRuntimeCanvasRefresh(bool forceResolve, bool refreshDepthSignal)
        {
            _pendingRuntimeCanvasRefresh = true;
            if (forceResolve)
                _forceResolveOnSlowTick = true;
            if (refreshDepthSignal)
                _pendingDepthSignalRefresh = true;
        }

        private void ProcessPendingRuntimeCanvasRefresh()
        {
            bool needsRuntimeCanvasRefresh =
                _pendingRuntimeCanvasRefresh ||
                _forceResolveOnSlowTick ||
                !IsRuntimeHierarchyReady() ||
                NeedsAutoResolve();
            if (!needsRuntimeCanvasRefresh && !_pendingDepthSignalRefresh)
                return;

            AutoResolve(_forceResolveOnSlowTick);
            NormalizeCanvas();
            EnsureHierarchy();

            if (_pendingDepthSignalRefresh || survival != _depthSignalSource)
                RefreshDepthSignalSubscription();

            if (_layoutBuilt && _root != null && targetCanvas != null)
                RefreshVisuals(0.016f);

            bool ready = IsRuntimeHierarchyReady();
            _pendingRuntimeCanvasRefresh = !ready;
            _forceResolveOnSlowTick = false;
            _pendingDepthSignalRefresh = false;
        }

        private bool IsRuntimeHierarchyReady()
        {
            return targetCanvas != null && _root != null && _layoutBuilt;
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

            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            if (!ReferenceEquals(_inventoryService, inventoryService))
            {
                _inventoryService = inventoryService;
                _playerInventory = null;
                _itemCatalog = null;
                _lastInventoryVersion = -1;
                InvalidateQuickbarSlotHashCache();
            }

            if (_inventoryService != null)
            {
                _playerInventory = _inventoryService.Inventory;
                _itemCatalog = _playerInventory != null ? _playerInventory.ItemCatalog : null;
                if (_toolManager == null)
                    _toolManager = _inventoryService.ToolManager;
            }

            Transform playerRoot = null;
            bool hasPlayerRoot = false;
            if (survival == null || playerMovement == null || flashlight == null || underwaterVisuals == null || _toolManager == null)
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
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    flashlight = playerContext.Flashlight;
            }

            if (underwaterVisuals == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    underwaterVisuals = playerContext.UnderwaterVisuals;
            }

            if (_toolManager == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _toolManager = playerContext.ToolManager;
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
                || underwaterVisuals == null
                || _toolManager == null
                || _playerInventory == null
                || _itemCatalog == null;
        }

        private static float GetAutoResolveNow()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }

        private void NormalizeCanvas()
        {
            if (targetCanvas == null)
                return;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            CacheDefaultCanvasHierarchy(canvasRect);
            Vector2 referenceResolution = ResolveUiReferenceResolution();
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
                ApplyProjectionCanvasState(targetCanvas, canvasRect, referenceResolution);
            }
            else
            {
                ApplyOverlayCanvasState(targetCanvas, canvasRect);
            }

            if (!Mathf.Approximately(targetCanvas.scaleFactor, 1f))
                targetCanvas.scaleFactor = 1f;

            HectonUIScaler uiScaler = ResolveUiScaler();
            if (uiScaler != null)
                uiScaler.Configure(referenceResolution, 0.5f);

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

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            Vector2 referenceResolution = ResolveUiReferenceResolution();
            if (useProjectionCanvas)
            {
                if (targetCanvas.renderMode != RenderMode.WorldSpace)
                    return false;
                if (!ReferenceEquals(targetCanvas.worldCamera, projectionCamera))
                    return false;
                if (!Mathf.Approximately(targetCanvas.planeDistance, ResolveProjectionPlaneDistance()))
                    return false;
                if (targetCanvas.pixelPerfect)
                    return false;
                if (targetCanvas.overrideSorting)
                    return false;
                if (targetCanvas.additionalShaderChannels != AdditionalCanvasShaderChannels.None)
                    return false;
                if (targetCanvas.sortingLayerName != WorldGeometrySortingLayer)
                    return false;
                if (targetCanvas.sortingOrder != 0)
                    return false;
                if (_cachedGraphicRaycaster != null && _cachedGraphicRaycaster.enabled)
                    return false;
                if (!IsProjectionCanvasPoseCurrent(canvasRect, referenceResolution))
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

            if (!Mathf.Approximately(targetCanvas.scaleFactor, 1f))
                return false;

            if (canvasRect == null)
                return false;

            if (!useProjectionCanvas &&
                (!Approximately(canvasRect.anchorMin, Vector2.zero) ||
                 !Approximately(canvasRect.anchorMax, Vector2.one) ||
                 !Approximately(canvasRect.offsetMin, Vector2.zero) ||
                 !Approximately(canvasRect.offsetMax, Vector2.zero) ||
                 !Approximately(canvasRect.anchoredPosition, Vector2.zero) ||
                 canvasRect.localScale != Vector3.one))
            {
                return false;
            }

            return true;
        }

        private void CacheDefaultCanvasHierarchy(RectTransform canvasRect)
        {
            if (canvasRect == null || _defaultCanvasParent != null)
                return;

            _defaultCanvasParent = canvasRect.parent;
            _defaultCanvasSiblingIndex = canvasRect.GetSiblingIndex();
        }

        private Vector2 ResolveUiReferenceResolution()
        {
            if (_cachedUiScaler != null)
                return _cachedUiScaler.ReferenceResolution;

            return DefaultUiReferenceResolution;
        }

        private float ResolveProjectionCanvasWorldScale(Camera targetCamera, Vector2 referenceResolution)
        {
            if (targetCamera == null || referenceResolution.x <= 0f || referenceResolution.y <= 0f)
                return DiegeticHudWorldScale;

            float safeDistance = ResolveProjectionPlaneDistance();
            float halfFovRadians = Mathf.Max(0.001f, targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float frustumHalfHeight = Mathf.Tan(halfFovRadians) * safeDistance;
            float frustumHalfWidth = frustumHalfHeight * Mathf.Max(0.0001f, targetCamera.aspect);
            float scaleX = (frustumHalfWidth * 2f) / referenceResolution.x;
            float scaleY = (frustumHalfHeight * 2f) / referenceResolution.y;
            return Mathf.Max(0.000001f, Mathf.Min(scaleX, scaleY));
        }

        private void ApplyOverlayCanvasState(Canvas canvas, RectTransform canvasRect)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = overlaySortingOrder;

            if (canvasRect == null)
                return;

            if (_defaultCanvasParent != null && canvasRect.parent != _defaultCanvasParent)
            {
                canvasRect.SetParent(_defaultCanvasParent, false);
                if (_defaultCanvasSiblingIndex >= 0 && _defaultCanvasSiblingIndex < canvasRect.parent.childCount)
                    canvasRect.SetSiblingIndex(_defaultCanvasSiblingIndex);
            }

            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
        }

        private void ApplyProjectionCanvasState(Canvas canvas, RectTransform canvasRect, Vector2 referenceResolution)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = projectionCamera;
            canvas.planeDistance = ResolveProjectionPlaneDistance();
            canvas.overrideSorting = false;
            canvas.sortingLayerName = WorldGeometrySortingLayer;
            canvas.sortingOrder = 0;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            EnforceProjectionCameraVisibilityContract(projectionCamera);
            ApplyProjectionCanvasLayer(canvas.gameObject, projectionCamera);

            if (_cachedGraphicRaycaster != null)
                _cachedGraphicRaycaster.enabled = false;

            if (canvasRect == null || projectionCamera == null)
                return;

            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = referenceResolution;
            UpdateProjectionCanvasPose(canvasRect, referenceResolution);
        }

        private bool IsProjectionCanvasPoseCurrent(RectTransform canvasRect, Vector2 referenceResolution)
        {
            if (canvasRect == null || projectionCamera == null)
                return false;

            if (!Approximately(canvasRect.anchorMin, new Vector2(0.5f, 0.5f)) ||
                !Approximately(canvasRect.anchorMax, new Vector2(0.5f, 0.5f)) ||
                !Approximately(canvasRect.pivot, new Vector2(0.5f, 0.5f)) ||
                !Approximately(canvasRect.sizeDelta, referenceResolution))
            {
                return false;
            }

            Transform cameraTransform = projectionCamera.transform;
            float projectionDistance = ResolveProjectionPlaneDistance();
            Vector3 expectedPosition = cameraTransform.position + cameraTransform.forward * projectionDistance;
            if ((canvasRect.position - expectedPosition).sqrMagnitude > ProjectionPosePositionTolerance)
                return false;

            if (Quaternion.Angle(canvasRect.rotation, cameraTransform.rotation) > 0.01f)
                return false;

            if (!IsProjectionCanvasLayerValid(canvasRect.gameObject, projectionCamera))
                return false;

            float expectedScale = ResolveProjectionCanvasWorldScale(projectionCamera, referenceResolution);
            Vector3 scale = canvasRect.localScale;
            return Mathf.Abs(scale.x - expectedScale) <= ProjectionPoseScaleTolerance &&
                   Mathf.Abs(scale.y - expectedScale) <= ProjectionPoseScaleTolerance &&
                   Mathf.Abs(scale.z - expectedScale) <= ProjectionPoseScaleTolerance;
        }

        private void UpdateProjectionCanvasPose(RectTransform canvasRect, Vector2 referenceResolution)
        {
            if (canvasRect == null || projectionCamera == null)
                return;

            Transform cameraTransform = projectionCamera.transform;
            float projectionDistance = ResolveProjectionPlaneDistance();
            float expectedScale = ResolveProjectionCanvasWorldScale(projectionCamera, referenceResolution);
            Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * projectionDistance;
            canvasRect.SetPositionAndRotation(targetPosition, cameraTransform.rotation);
            canvasRect.localScale = new Vector3(expectedScale, expectedScale, expectedScale);
        }

        private float ResolveProjectionPlaneDistance()
        {
            if (projectionCamera == null)
                return Mathf.Max(ProjectionNearClipSafetyPaddingMeters, projectionPlaneDistance);

            return Mathf.Max(
                projectionCamera.nearClipPlane + ProjectionNearClipSafetyPaddingMeters,
                ProjectionNearClipSafetyPaddingMeters);
        }

        private static bool IsProjectionCanvasLayerValid(GameObject canvasObject, Camera targetCamera)
        {
            if (canvasObject == null || targetCamera == null)
                return false;

            int layer = canvasObject.layer;
            if (layer == UiLayerIndex)
                return false;

            return IsLayerVisibleToCamera(targetCamera, layer);
        }

        private static void ApplyProjectionCanvasLayer(GameObject canvasObject, Camera targetCamera)
        {
            if (canvasObject == null || targetCamera == null)
                return;

            int resolvedLayer = ResolveProjectionCanvasLayer(targetCamera, canvasObject.layer);
            if (resolvedLayer < 0 || canvasObject.layer == resolvedLayer)
                return;

            SetLayerRecursively(canvasObject.transform, resolvedLayer);
        }

        private static int ResolveProjectionCanvasLayer(Camera targetCamera, int currentLayer)
        {
            return HudInternalLayerIndex;
        }

        private static bool IsLayerVisibleToCamera(Camera targetCamera, int layer)
        {
            if (targetCamera == null || layer < 0 || layer > 31)
                return false;

            return (targetCamera.cullingMask & (1 << layer)) != 0;
        }

        private static void EnforceProjectionCameraVisibilityContract(Camera targetCamera)
        {
            if (targetCamera == null)
                return;

            int hudInternalMask = 1 << HudInternalLayerIndex;
            if (targetCamera.cullingMask != hudInternalMask)
                targetCamera.cullingMask = hudInternalMask;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
                SetLayerRecursively(root.GetChild(childIndex), layer);
        }

        private void EnsureThreatChevronRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (threatChevronShader == null)
                threatChevronShader = AssetDatabase.LoadAssetAtPath<Shader>(ThreatChevronShaderPath);
#endif

            if (_threatChevronMesh == null)
                _threatChevronMesh = BuildThreatChevronMesh();

            if (_threatChevronMaterial == null && threatChevronShader != null)
            {
                _threatChevronMaterial = new Material(threatChevronShader)
                {
                    name = "HUD_ThreatChevron_Runtime"
                }; // COLD ALLOC: Material[1] â€” instanced HUD threat-chevron material â€” owner: SuitHUDV4CanvasOverlay
            }

            if (!_threatChevronMatrices.IsCreated)
                _threatChevronMatrices = new NativeArray<Matrix4x4>(MaxThreatChevronCount, Allocator.Persistent);
        }

        private void EnsureAcousticRadarRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (acousticRadarShader == null)
                acousticRadarShader = AssetDatabase.LoadAssetAtPath<Shader>(AcousticRadarShaderPath);
#endif

            if (_acousticRadarMaterial == null && acousticRadarShader != null)
            {
                _acousticRadarMaterial = new Material(acousticRadarShader)
                {
                    name = "HUD_AcousticRadar_Runtime"
                }; // COLD ALLOC: Material[1] â€” passive acoustic visor overlay material â€” owner: SuitHUDV4CanvasOverlay
            }

            if (_acousticRadarOverlay != null && _acousticRadarMaterial != null && _acousticRadarOverlay.material != _acousticRadarMaterial)
                _acousticRadarOverlay.material = _acousticRadarMaterial;
        }

        private void DisposeAcousticRadarRuntimeResources()
        {
            if (_acousticRadarOverlay != null && _acousticRadarOverlay.material == _acousticRadarMaterial)
                _acousticRadarOverlay.material = null;

            if (_acousticRadarMaterial != null)
            {
                Destroy(_acousticRadarMaterial);
                _acousticRadarMaterial = null;
            }

            if (_acousticRadarTexture != null)
            {
                Destroy(_acousticRadarTexture);
                _acousticRadarTexture = null;
            }

            _acousticRadarResolution = 0;
            _acousticRadarPeakIntensity = 0f;
        }

        private void RefreshAcousticRadarPayload()
        {
            if (!Application.isPlaying)
                return;

            EnsureAcousticRadarRuntimeResources();
            if (_acousticRadarMaterial == null)
                return;

            if (_spatialAudioManager == null && !SpatialAudioManager.TryGetInstance(out _spatialAudioManager))
                return;

            if (_spatialAudioManager == null ||
                !_spatialAudioManager.TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution) ||
                !radialIntensityBins.IsCreated ||
                radialResolution <= 0)
            {
                _acousticRadarPeakIntensity = 0f;
                return;
            }

            if (!EnsureAcousticRadarTexture(radialResolution))
                return;

            _acousticRadarTexture.SetPixelData(radialIntensityBins, 0);
            _acousticRadarTexture.Apply(false, false);
            _acousticRadarMaterial.SetTexture(_AcousticRadarTexId, _acousticRadarTexture);

            float peakIntensity = 0f;
            int sampleCount = math.min(radialIntensityBins.Length, radialResolution);
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = radialIntensityBins[i];
                if (sample > peakIntensity)
                    peakIntensity = sample;
            }

            _acousticRadarPeakIntensity = Mathf.Clamp01(peakIntensity);
        }

        private bool EnsureAcousticRadarTexture(int radialResolution)
        {
            if (radialResolution <= 0)
                return false;

            if (_acousticRadarTexture != null && _acousticRadarResolution == radialResolution)
                return true;

            if (_acousticRadarTexture != null)
            {
                Destroy(_acousticRadarTexture);
                _acousticRadarTexture = null;
            }

            _acousticRadarTexture = new Texture2D(radialResolution, 1, TextureFormat.RFloat, false, true)
            {
                name = "HUD_AcousticRadar_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            }; // COLD ALLOC: Texture2D[radialResolution x 1] â€” passive acoustic visor ring upload target â€” owner: SuitHUDV4CanvasOverlay
            _acousticRadarTexture.Apply(false, false);
            _acousticRadarResolution = radialResolution;
            return true;
        }

        private void DisposeThreatChevronRuntimeResources()
        {
            if (_threatChevronMatrices.IsCreated)
            {
                _threatChevronMatrices.Dispose();
                _threatChevronMatrices = default;
            }

            if (_threatChevronMaterial != null)
            {
                Destroy(_threatChevronMaterial);
                _threatChevronMaterial = null;
            }

            if (_threatChevronMesh != null)
            {
                Destroy(_threatChevronMesh);
                _threatChevronMesh = null;
            }
        }

        private void RefreshThreatChevronTargets()
        {
            _threatChevronVisibleCount = 0;
            Array.Clear(_threatChevronStates, 0, _threatChevronStates.Length);

            if (!Application.isPlaying ||
                renderPath != RenderPath.ProjectionSource ||
                projectionCamera == null ||
                threatChevronRadiusMeters <= 0f)
            {
                return;
            }

            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_vegetationBridge == null ||
                !_vegetationBridge.TryGetEcosystemThreatGridPayload(
                    out NativeArray<float> threatLevels,
                    out int gridResolution,
                    out Vector3 gridCenter,
                    out float cellSize))
            {
                return;
            }

            Transform cameraTransform = projectionCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            float radius = Mathf.Max(1f, threatChevronRadiusMeters);
            float radiusSq = radius * radius;
            float safeCellSize = Mathf.Max(0.001f, cellSize);
            int halfResolution = gridResolution >> 1;
            int radiusCells = Mathf.CeilToInt(radius / safeCellSize);
            int centerCellX = Mathf.Clamp(Mathf.RoundToInt((cameraPosition.x - gridCenter.x) / safeCellSize) + halfResolution, 0, gridResolution - 1);
            int centerCellZ = Mathf.Clamp(Mathf.RoundToInt((cameraPosition.z - gridCenter.z) / safeCellSize) + halfResolution, 0, gridResolution - 1);
            float halfExtent = (gridResolution - 1) * 0.5f * safeCellSize;
            float originX = gridCenter.x - halfExtent;
            float originZ = gridCenter.z - halfExtent;

            for (int cellZ = Mathf.Max(0, centerCellZ - radiusCells); cellZ <= Mathf.Min(gridResolution - 1, centerCellZ + radiusCells); cellZ++)
            {
                float worldZ = originZ + (cellZ * safeCellSize);
                for (int cellX = Mathf.Max(0, centerCellX - radiusCells); cellX <= Mathf.Min(gridResolution - 1, centerCellX + radiusCells); cellX++)
                {
                    int cellIndex = (cellZ * gridResolution) + cellX;
                    float threat01 = threatLevels[cellIndex];
                    if (threat01 < threatChevronThreshold)
                        continue;

                    Vector3 worldPosition = new Vector3(originX + (cellX * safeCellSize), cameraPosition.y, worldZ);
                    Vector3 toThreat = worldPosition - cameraPosition;
                    toThreat.y = 0f;
                    if (toThreat.sqrMagnitude > radiusSq)
                        continue;

                    InsertThreatChevronCandidate(worldPosition, threat01);
                }
            }
        }

        private void InsertThreatChevronCandidate(Vector3 worldPosition, float threat01)
        {
            int insertIndex = -1;
            float weakestThreat = threat01;
            for (int i = 0; i < MaxThreatChevronCount; i++)
            {
                if (!_threatChevronStates[i].Active)
                {
                    insertIndex = i;
                    break;
                }

                if (_threatChevronStates[i].Threat01 < weakestThreat)
                {
                    weakestThreat = _threatChevronStates[i].Threat01;
                    insertIndex = i;
                }
            }

            if (insertIndex < 0)
                return;

            _threatChevronStates[insertIndex] = new ThreatChevronState
            {
                WorldPosition = worldPosition,
                Threat01 = threat01,
                Active = true
            };
        }

        private void RenderThreatChevrons()
        {
            if (!Application.isPlaying ||
                renderPath != RenderPath.ProjectionSource ||
                projectionCamera == null)
            {
                return;
            }

            EnsureThreatChevronRuntimeResources();
            if (_threatChevronMaterial == null || _threatChevronMesh == null || !_threatChevronMatrices.IsCreated)
                return;

            int visibleCount = BuildThreatChevronMatrices();
            _threatChevronVisibleCount = visibleCount;
            if (visibleCount <= 0)
                return;

            _threatChevronMaterial.SetFloat(_ThreatChevronFlickerFrequencyId, Mathf.Max(0f, threatChevronFlickerFrequency));
            _threatChevronMaterial.SetFloat(_ThreatChevronFlickerIntensityId, Mathf.Clamp01(threatChevronFlickerIntensity));

            for (int visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                _threatChevronSingleDrawMirror[0] = _threatChevronMatrixMirror[visibleIndex];
                float alpha01 = Mathf.Clamp01(_threatChevronAlphaMirror[visibleIndex]);
                float pulse01 = 0.74f + (0.26f * (0.5f + (0.5f * Mathf.Sin((_threatChevronPulseTime * threatChevronFlickerFrequency * 0.75f) + (visibleIndex * 0.91f)))));
                alpha01 *= pulse01;
                Color chevronColor = threatChevronColor;
                chevronColor.a *= alpha01;
                _threatChevronMaterial.SetColor(_ThreatChevronBaseColorId, chevronColor);
                _threatChevronMaterial.SetFloat(_ThreatChevronFillAlphaId, Mathf.Clamp01(threatChevronFillAlpha) * alpha01);

                Graphics.DrawMeshInstanced(
                    _threatChevronMesh,
                    0,
                    _threatChevronMaterial,
                    _threatChevronSingleDrawMirror,
                    1,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    HudInternalLayerIndex,
                    projectionCamera,
                    LightProbeUsage.Off,
                    null);
            }
        }

        private int BuildThreatChevronMatrices()
        {
            if (projectionCamera == null)
                return 0;

            Transform cameraTransform = projectionCamera.transform;
            float projectionDistance = ResolveProjectionPlaneDistance() + ThreatChevronPlaneBiasMeters;
            float halfFovRadians = Mathf.Max(0.001f, projectionCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float frustumHalfHeight = Mathf.Tan(halfFovRadians) * projectionDistance;
            float frustumHalfWidth = frustumHalfHeight * Mathf.Max(0.0001f, projectionCamera.aspect);
            float worldPerPixel = (frustumHalfHeight * 2f) / Mathf.Max(1f, projectionCamera.pixelHeight);
            float insetWorld = Mathf.Max(0f, threatChevronEdgeInsetPixels) * worldPerPixel;
            float safeHalfWidth = Mathf.Max(worldPerPixel, frustumHalfWidth - insetWorld);
            float safeHalfHeight = Mathf.Max(worldPerPixel, frustumHalfHeight - insetWorld);
            float chevronScaleWorld = Mathf.Max(0.0001f, Mathf.Max(4f, threatChevronSizePixels) * worldPerPixel);
            int visibleCount = 0;

            for (int i = 0; i < MaxThreatChevronCount; i++)
            {
                if (!_threatChevronStates[i].Active)
                    continue;

                if (!TryBuildThreatChevronMatrix(
                    cameraTransform,
                    projectionDistance,
                    safeHalfWidth,
                    safeHalfHeight,
                    chevronScaleWorld,
                    _threatChevronStates[i],
                    out Matrix4x4 matrix,
                    out float alpha01))
                {
                    continue;
                }

                _threatChevronMatrices[visibleCount] = matrix;
                _threatChevronMatrixMirror[visibleCount] = matrix;
                _threatChevronAlphaMirror[visibleCount] = alpha01;
                visibleCount++;
            }

            return visibleCount;
        }

        private bool TryBuildThreatChevronMatrix(
            Transform cameraTransform,
            float projectionDistance,
            float safeHalfWidth,
            float safeHalfHeight,
            float chevronScaleWorld,
            ThreatChevronState threatState,
            out Matrix4x4 matrix,
            out float alpha01)
        {
            matrix = default;
            alpha01 = 0f;
            Vector3 localThreatPosition = cameraTransform.InverseTransformPoint(threatState.WorldPosition);
            bool behind = localThreatPosition.z <= 0.001f;
            if (behind)
            {
                localThreatPosition.x = -localThreatPosition.x;
                localThreatPosition.y = -localThreatPosition.y;
                localThreatPosition.z = Mathf.Abs(localThreatPosition.z) + 0.001f;
            }

            if (Mathf.Abs(localThreatPosition.x) <= 0.0001f && Mathf.Abs(localThreatPosition.y) <= 0.0001f)
                return false;

            float projectionScale = projectionDistance / Mathf.Max(0.001f, localThreatPosition.z);
            Vector2 projectedPlanePosition = new Vector2(localThreatPosition.x * projectionScale, localThreatPosition.y * projectionScale);
            Vector2 clampedPlanePosition = ClampToThreatBounds(projectedPlanePosition, safeHalfWidth, safeHalfHeight);
            Vector2 direction2D = clampedPlanePosition.normalized;
            if (direction2D.sqrMagnitude <= 0.000001f)
                return false;

            Vector3 worldPosition =
                cameraTransform.position +
                (cameraTransform.forward * projectionDistance) +
                (cameraTransform.right * clampedPlanePosition.x) +
                (cameraTransform.up * clampedPlanePosition.y);

            Vector3 threatDirection = (threatState.WorldPosition - cameraTransform.position).normalized;
            float signedAngleDegrees = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(cameraTransform.forward, threatDirection), cameraTransform.up),
                Vector3.Dot(cameraTransform.forward, threatDirection)) * Mathf.Rad2Deg;
            float rollDegrees = Mathf.Atan2(direction2D.y, direction2D.x) * Mathf.Rad2Deg;
            Quaternion worldRotation = cameraTransform.rotation * Quaternion.Euler(0f, 0f, rollDegrees);
            float behindFade = behind ? 0.35f : 1f;
            float threatFade = Mathf.Clamp01(Mathf.InverseLerp(threatChevronThreshold, 1f, threatState.Threat01));
            float edgeDistance01 = Mathf.Clamp01(Mathf.Max(
                Mathf.Abs(clampedPlanePosition.x) / Mathf.Max(0.0001f, safeHalfWidth),
                Mathf.Abs(clampedPlanePosition.y) / Mathf.Max(0.0001f, safeHalfHeight)));
            float edgeFade = Mathf.Lerp(0.72f, 1f, edgeDistance01);
            float threatScale = Mathf.Lerp(0.72f, 1.15f, Mathf.Clamp01(threatState.Threat01)) * behindFade;
            float rotationScaleBias = 1f + (Mathf.Abs(signedAngleDegrees) / 180f) * 0.04f;
            Vector3 worldScale = new Vector3(
                chevronScaleWorld * threatScale * rotationScaleBias,
                chevronScaleWorld * threatScale,
                chevronScaleWorld * threatScale);
            alpha01 = Mathf.Clamp01(Mathf.Max(0.16f, threatFade * edgeFade * behindFade));
            matrix = Matrix4x4.TRS(worldPosition, worldRotation, worldScale);
            return true;
        }

        private void ApplyAcousticRadarVisuals(Color primary, Color warning, float corruptionIntensity)
        {
            if (_acousticRadarOverlay == null)
                return;

            EnsureAcousticRadarRuntimeResources();
            if (_acousticRadarMaterial == null)
            {
                _acousticRadarOverlay.enabled = false;
                return;
            }

            if (_acousticRadarOverlay.material != _acousticRadarMaterial)
                _acousticRadarOverlay.material = _acousticRadarMaterial;

            float overlayOpacity = Mathf.Clamp01(acousticRadarOpacity * Mathf.Lerp(0.2f, 1f, _acousticRadarPeakIntensity));
            bool visible = overlayOpacity > 0.001f && _acousticRadarPeakIntensity > 0.001f;
            _acousticRadarOverlay.enabled = visible;
            if (!visible)
                return;

            _acousticRadarMaterial.SetColor(_AcousticRadarPrimaryColorId, Alpha(primary, 0.92f));
            _acousticRadarMaterial.SetColor(_AcousticRadarWarningColorId, Alpha(warning, 0.98f));
            _acousticRadarMaterial.SetFloat(_AcousticRadarOpacityId, overlayOpacity);
            _acousticRadarMaterial.SetFloat(_AcousticRadarGlitchId, Mathf.Clamp01(acousticRadarGlitchStrength + corruptionIntensity * 0.42f));
            _acousticRadarMaterial.SetFloat(_AcousticRadarInnerEdgeId, Mathf.Clamp(acousticRadarInnerEdge, 0.05f, 0.95f));
            _acousticRadarMaterial.SetFloat(_AcousticRadarWaveAmplitudeId, Mathf.Max(0f, acousticRadarWaveAmplitude));
            _acousticRadarMaterial.SetFloat(_AcousticRadarBandThicknessId, Mathf.Clamp(acousticRadarBandThickness, 0.01f, 0.49f));
            _acousticRadarMaterial.SetFloat(_AcousticRadarPulseFrequencyId, Mathf.Max(0f, acousticRadarPulseFrequency));
            _acousticRadarMaterial.SetFloat(_AcousticRadarRadarIntensityId, _acousticRadarPeakIntensity);
        }

        private static Vector2 ClampToThreatBounds(Vector2 projectedPlanePosition, float safeHalfWidth, float safeHalfHeight)
        {
            if (Mathf.Abs(projectedPlanePosition.x) <= safeHalfWidth &&
                Mathf.Abs(projectedPlanePosition.y) <= safeHalfHeight)
            {
                return projectedPlanePosition;
            }

            float tx = safeHalfWidth / Mathf.Max(0.0001f, Mathf.Abs(projectedPlanePosition.x));
            float ty = safeHalfHeight / Mathf.Max(0.0001f, Mathf.Abs(projectedPlanePosition.y));
            return projectedPlanePosition * Mathf.Min(tx, ty);
        }

        private static Mesh BuildThreatChevronMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "HUD_ThreatChevron_Mesh"
            };

            Vector3[] vertices = new Vector3[8];
            Vector2[] uvs = new Vector2[8];
            int[] triangles = new int[12];

            AppendThreatChevronBar(new Vector2(-0.48f, 0.44f), new Vector2(0.36f, 0f), 0.14f, vertices, uvs, 0, triangles, 0);
            AppendThreatChevronBar(new Vector2(-0.48f, -0.44f), new Vector2(0.36f, 0f), 0.14f, vertices, uvs, 4, triangles, 6);

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendThreatChevronBar(
            Vector2 start,
            Vector2 end,
            float thickness,
            Vector3[] vertices,
            Vector2[] uvs,
            int vertexOffset,
            int[] triangles,
            int triangleOffset)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

            vertices[vertexOffset + 0] = start + normal;
            vertices[vertexOffset + 1] = start - normal;
            vertices[vertexOffset + 2] = end + normal;
            vertices[vertexOffset + 3] = end - normal;

            uvs[vertexOffset + 0] = new Vector2(0f, 1f);
            uvs[vertexOffset + 1] = new Vector2(0f, 0f);
            uvs[vertexOffset + 2] = new Vector2(1f, 1f);
            uvs[vertexOffset + 3] = new Vector2(1f, 0f);

            triangles[triangleOffset + 0] = vertexOffset + 0;
            triangles[triangleOffset + 1] = vertexOffset + 2;
            triangles[triangleOffset + 2] = vertexOffset + 1;
            triangles[triangleOffset + 3] = vertexOffset + 2;
            triangles[triangleOffset + 4] = vertexOffset + 3;
            triangles[triangleOffset + 5] = vertexOffset + 1;
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

            _biosBackdrop = CreateImage("BiosBackdrop", _root, Color.black);
            Stretch(_biosBackdrop.rectTransform, 0f, 0f, 0f, 0f);
            _biosBackdrop.raycastTarget = false;
            _biosBackdrop.color = new Color(0f, 0f, 0f, 0f);

            _acousticRadarOverlay = CreateImage("AcousticRadarOverlay", _root, Color.white);
            Stretch(_acousticRadarOverlay.rectTransform, 0f, 0f, 0f, 0f);
            _acousticRadarOverlay.raycastTarget = false;

            _ornamentRoot = CreateRect("OrnamentRoot", _root);
            Stretch(_ornamentRoot, 0f, 0f, 0f, 0f);

            _topVeil = CreateImage("TopVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.08f));
            _topVeil.rectTransform.anchorMin = new Vector2(0f, 1f);
            _topVeil.rectTransform.anchorMax = new Vector2(1f, 1f);
            _topVeil.rectTransform.pivot = new Vector2(0.5f, 1f);
            _topVeil.rectTransform.sizeDelta = new Vector2(0f, 92f);
            _topVeil.rectTransform.anchoredPosition = Vector2.zero;

            _bottomVeil = CreateImage("BottomVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.1f));
            _bottomVeil.rectTransform.anchorMin = new Vector2(0f, 0f);
            _bottomVeil.rectTransform.anchorMax = new Vector2(1f, 0f);
            _bottomVeil.rectTransform.pivot = new Vector2(0.5f, 0f);
            _bottomVeil.rectTransform.sizeDelta = new Vector2(0f, 144f);
            _bottomVeil.rectTransform.anchoredPosition = Vector2.zero;

            _leftVeil = CreateImage("LeftVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.05f));
            Anchor(_leftVeil.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(72f, 0f));

            _rightVeil = CreateImage("RightVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.05f));
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

            _footerLine = CreateImage("FooterLine", _ornamentRoot, Color.white);
            Anchor(_footerLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(180f, 2f));

            _statusRuleLeft = CreateImage("StatusRuleLeft", _ornamentRoot, Color.white);
            Anchor(_statusRuleLeft.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-148f, 76f), new Vector2(104f, 2f));
            _statusRuleLeft.rectTransform.localEulerAngles = Vector3.zero;

            _statusRuleRight = CreateImage("StatusRuleRight", _ornamentRoot, Color.white);
            Anchor(_statusRuleRight.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(148f, 76f), new Vector2(104f, 2f));
            _statusRuleRight.rectTransform.localEulerAngles = Vector3.zero;

            _reticleRoot = CreateRect("ReticleRoot", _root);
            Anchor(_reticleRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), reticleOffset, new Vector2(64f, 64f));

            _reticleH = CreateImage("ReticleH", _reticleRoot, Color.white);
            Anchor(_reticleH.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 2f));

            _reticleV = CreateImage("ReticleV", _reticleRoot, Color.white);
            Anchor(_reticleV.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 22f));

            _reticleBracketLeft = CreateImage("ReticleBracketLeft", _reticleRoot, Color.white);
            Anchor(_reticleBracketLeft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(10f, 2f));

            _reticleBracketRight = CreateImage("ReticleBracketRight", _reticleRoot, Color.white);
            Anchor(_reticleBracketRight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(10f, 2f));

            Vector2 resolvedTelemetryOffset = ResolveTelemetryOffset();
            Vector2 resolvedTelemetrySize = ResolveTelemetrySize();

            _telemetryRoot = CreateRect("TelemetryRoot", _root);
            Anchor(_telemetryRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), resolvedTelemetryOffset, resolvedTelemetrySize);
            _telemetryRoot.localEulerAngles = Vector3.zero;

            _telemetryChromeRoot = CreateRect("TelemetryChromeRoot", _telemetryRoot);
            Stretch(_telemetryChromeRoot, 0f, 0f, 0f, 0f);

            _telemetryRule = CreateImage("TelemetryRule", _telemetryChromeRoot, Color.white);
            Anchor(_telemetryRule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(120f, 2f));

            _telemetryBraceUpper = CreateImage("TelemetryBraceUpper", _telemetryChromeRoot, Color.white);
            Anchor(_telemetryBraceUpper.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 26f), new Vector2(22f, 2f));
            _telemetryBraceUpper.rectTransform.localEulerAngles = Vector3.zero;

            _telemetryBraceLower = CreateImage("TelemetryBraceLower", _telemetryChromeRoot, Color.white);
            Anchor(_telemetryBraceLower.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(18f, 2f));
            _telemetryBraceLower.rectTransform.localEulerAngles = Vector3.zero;

            _depthLabel = CreateText("DepthLabel", _telemetryRoot, 28f, FontStyles.Bold, TextAlignmentOptions.Right, 0.96f);
            Anchor(_depthLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -24f), new Vector2(152f, 34f));

            _telemetrySupplementRoot = CreateRect("TelemetrySupplementRoot", _telemetryRoot);
            Stretch(_telemetrySupplementRoot, 0f, 0f, 0f, 0f);

            _temperatureLabel = CreateText("TemperatureLabel", _telemetrySupplementRoot, 14f, FontStyles.Normal, TextAlignmentOptions.Right, 0.82f);
            Anchor(_temperatureLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(152f, 20f));

            _pressureLabel = CreateText("PressureLabel", _telemetrySupplementRoot, 13f, FontStyles.Normal, TextAlignmentOptions.Right, 0.62f);
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

            _quickbarRoot = CreateRect("QuickbarRoot", _root);
            Anchor(_quickbarRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), quickbarOffset, quickbarSize);
            _quickbarCanvasGroup = EnsureCanvasGroup(_quickbarRoot);
            BuildQuickbarHierarchy(_quickbarRoot);

            EnsureIsolatedDynamicCanvas(_reticleRoot, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_depthLabel.rectTransform, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_telemetrySupplementRoot, DynamicCanvasCadenceBucket.LowCadence);
            EnsureIsolatedDynamicCanvas(_statusLabel.rectTransform, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_oxygenGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_healthGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_powerGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_quickbarRoot, DynamicCanvasCadenceBucket.LowCadence);

            _ornamentCanvasGroup = EnsureCanvasGroup(_ornamentRoot);
            _headerCanvasGroup = EnsureCanvasGroup(_headerRoot);
            _telemetryChromeCanvasGroup = EnsureCanvasGroup(_telemetryChromeRoot);
            _telemetrySupplementCanvasGroup = EnsureCanvasGroup(_telemetrySupplementRoot);
            _statusCanvasGroup = EnsureCanvasGroup(_statusLabel.rectTransform);
            _quickbarCanvasGroup = EnsureCanvasGroup(_quickbarRoot);

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

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
                return null;

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();

            return canvasGroup;
        }

        private static Canvas EnsureIsolatedDynamicCanvas(RectTransform target, DynamicCanvasCadenceBucket cadenceBucket)
        {
            if (target == null)
                return null;

            Canvas canvas = target.GetComponent<Canvas>();
            if (canvas == null)
                canvas = target.gameObject.AddComponent<Canvas>();

            canvas.overrideSorting = true;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = ResolveCanvasSortingOrder(cadenceBucket);

            if (target.TryGetComponent(out GraphicRaycaster raycaster) && raycaster.enabled)
                raycaster.enabled = false;

            return canvas;
        }

        private static int ResolveCanvasSortingOrder(DynamicCanvasCadenceBucket cadenceBucket)
        {
            switch (cadenceBucket)
            {
                case DynamicCanvasCadenceBucket.Static:
                    return StaticCanvasSortingOrder;
                case DynamicCanvasCadenceBucket.LowCadence:
                    return LowCadenceCanvasSortingOrder;
                default:
                    return HighCadenceCanvasSortingOrder;
            }
        }

        private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
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

        private void ApplySectionVisibility(bool biosRecoveryMode)
        {
            SetCanvasGroupVisible(_ornamentCanvasGroup, !biosRecoveryMode);
            SetCanvasGroupVisible(_headerCanvasGroup, !biosRecoveryMode);
            SetCanvasGroupVisible(_telemetryChromeCanvasGroup, !biosRecoveryMode);
            SetCanvasGroupVisible(_telemetrySupplementCanvasGroup, !biosRecoveryMode);
            SetCanvasGroupVisible(_statusCanvasGroup, true);
            SetCanvasGroupVisible(_quickbarCanvasGroup, true);

            if (_oxygenGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_oxygenGauge.CanvasGroup, true);
            if (_healthGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_healthGauge.CanvasGroup, !biosRecoveryMode);
            if (_powerGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_powerGauge.CanvasGroup, !biosRecoveryMode);
        }

        private void ApplyBiosBackdrop(bool biosRecoveryMode)
        {
            if (_biosBackdrop == null)
                return;

            _biosBackdrop.color = biosRecoveryMode
                ? new Color(0f, 0f, 0f, 0.84f)
                : new Color(0f, 0f, 0f, 0f);
        }

        private static bool IsBlinkVisible(float elapsedTime, float frequency)
        {
            return Mathf.Sin(elapsedTime * frequency) >= 0f;
        }

        private void RefreshVisuals(float dt, bool refreshMediumCadence, bool refreshSlowCadence)
        {
            if (_root == null)
                return;

            RefreshDepthFromMovementFallback();
            ResolveProfile();
            ResolvePalette(out Color primary, out Color secondary, out Color dim, out Color warning);

            if (_biosRecoveryMode)
            {
                primary = new Color(0.32f, 1f, 0.34f, 1f);
                secondary = primary;
                dim = primary;
                warning = primary;
            }

            bool hasSurvivalStats = survival != null && survival.Stats != null;
            float oxygen = hasSurvivalStats ? Mathf.Clamp01(survival.OxygenNormalized) : 1f;
            float power = _biosRecoveryMode
                ? _traumaTransportPower01
                : (hasSurvivalStats ? Mathf.Clamp01(survival.EnergyNormalized) : 1f);
            float health = hasSurvivalStats ? Mathf.Clamp01(survival.IntegrityNormalized) : 1f;
            health = Mathf.Min(health, _traumaHullIntegrity01);
            float depth = _depthMeters;
            float pressure = survival != null ? Mathf.Max(1f, survival.Pressure) : 1f + depth / 10f;
            float heading = playerMovement != null ? Mathf.Repeat(playerMovement.CameraYaw, 360f) : 0f;
            float safeDepth = hasSurvivalStats ? Mathf.Max(1f, survival.Stats.SafeDepth) : 50f;
            float safeDepthNormalized = ResolveSafeDepthNormalized(depth, safeDepth);
            float oxygenCurrent = survival != null ? survival.Oxygen : oxygen * 100f;
            float energyCurrent = survival != null ? survival.Energy : power * 100f;
            float healthCurrent = survival != null ? survival.Integrity : health * 100f;
            float stressPulse = _biosRecoveryMode ? 0f : UpdateStressPulse(dt);
            Color pulsedPrimary = ResolveStressPulseColor(primary, warning, stressPulse, stressPulseBrightnessBoost, stressPulseWarningBlend);
            Color pulsedDim = ResolveStressPulseColor(dim, warning, stressPulse, stressPulseBrightnessBoost * 0.45f, stressPulseWarningBlend * 0.38f);
            Color pulsedWarning = ResolveStressPulseColor(warning, primary, stressPulse, stressPulseBrightnessBoost * 0.22f, 0f);
            LocalizationManager manager = LocalizationManager.Instance;
            float hullStressCorruptionIntensity = manager != null ? manager.GetHullStressCorruptionIntensity() : 0f;
            bool hullStressWhisperMode = !_biosRecoveryMode && ShouldUseHullStressWhisperMode(manager);
            float traumaCorruptionIntensity = _traumaGlitchIntensity > CorruptedModeThreshold ? _traumaGlitchIntensity : 0f;
            float displayCorruptionIntensity = math.max(hullStressCorruptionIntensity, traumaCorruptionIntensity);
            bool corruptedMode = !_biosRecoveryMode && displayCorruptionIntensity > 0f;
            bool toolDepletedWarningActive = !_biosRecoveryMode && _toolDepletedWarningTimer > 0f;
            if (corruptedMode)
                _corruptionFrameVersion++;

            float targetTemp = EstimateTemperature(depth);
            _displayTemperature = Mathf.Lerp(_displayTemperature, targetTemp, 1f - Mathf.Exp(-4f * dt));
            _displayOxygen01 = DampHudValue(_displayOxygen01, oxygen, OxygenGaugeDamping, dt);
            _displayHealth01 = DampHudValue(_displayHealth01, health, HealthGaugeDamping, dt);
            _displayPower01 = DampHudValue(_displayPower01, power, BatteryGaugeDamping, dt);
            float depthDelta = depth - _lastDepth;
            _lastDepth = depth;
            ApplySectionVisibility(_biosRecoveryMode);
            ApplyBiosBackdrop(_biosRecoveryMode);
            ApplyAcousticRadarVisuals(pulsedPrimary, pulsedWarning, displayCorruptionIntensity);
            ApplyStaticStyleIfNeeded(primary, secondary, dim, warning);
            ApplyStressPulseStyle(primary, warning, _biosRecoveryMode ? 0f : stressPulse);

            float localizedDepth = LocalizedMeasurementFormatter.ConvertDistanceMeters(depth, _localizedMeasurementLanguage);
            float localizedTemperature = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(_displayTemperature, _localizedMeasurementLanguage);
            bool localizedRtl = LocalizedMeasurementFormatter.IsRightToLeft(_localizedMeasurementLanguage);
            bool specialCadenceBypass = _biosRecoveryMode || hullStressWhisperMode || corruptedMode;
            bool shouldRefreshSuitLabel = specialCadenceBypass || refreshSlowCadence || _appliedSuitLabelVersion == int.MinValue;
            bool shouldRefreshHeadingLabel = specialCadenceBypass || NeedsHeadingCadenceRefresh(refreshMediumCadence, heading);
            bool shouldRefreshTelemetryText = specialCadenceBypass || NeedsTelemetryCadenceRefresh(refreshMediumCadence, oxygen, depth, localizedTemperature, pressure);
            bool shouldRefreshGaugeText = specialCadenceBypass || NeedsGaugeCadenceRefresh(refreshMediumCadence, oxygen, power, health);
            bool shouldRefreshQuickbar = !_quickbarVisualsInitialized || specialCadenceBypass || refreshMediumCadence;
            char[] hullStressWhisperBuffer = null;
            int hullStressWhisperLength = 0;
            int hullStressWhisperVersion = int.MinValue;
            if (hullStressWhisperMode)
                GetHullStressWhisperBuffer(manager, out hullStressWhisperBuffer, out hullStressWhisperLength, out hullStressWhisperVersion);

            if (shouldRefreshSuitLabel || shouldRefreshHeadingLabel)
            {
                if (hullStressWhisperMode)
                {
                    if (shouldRefreshSuitLabel)
                        SetDisplayBufferIfChanged(_suitLabel, hullStressWhisperBuffer, hullStressWhisperLength, _cachedHullStressWhisperRtl, Alpha(primary, 0.95f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 101, ref _appliedSuitLabelVersion, ref _appliedSuitLabelColor);

                    if (shouldRefreshHeadingLabel)
                        SetDisplayBufferIfChanged(_headingLabel, hullStressWhisperBuffer, hullStressWhisperLength, _cachedHullStressWhisperRtl, Alpha(dim, 0.58f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 107, ref _appliedHeadingLabelVersion, ref _appliedHeadingLabelColor);
                }
                else
                {
                    if (shouldRefreshSuitLabel)
                    {
                        ResolveSuitLabelBuffer(out char[] suitLabelBuffer, out int suitLabelLength, out int suitLabelVersion, out bool suitLabelRtl);
                        SetDisplayBufferIfChanged(_suitLabel, suitLabelBuffer, suitLabelLength, suitLabelRtl, Alpha(primary, 0.95f), suitLabelVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 101, ref _appliedSuitLabelVersion, ref _appliedSuitLabelColor);
                    }

                    if (shouldRefreshHeadingLabel)
                    {
                        HeadingLabelCacheEntry headingEntry = ResolveHeadingLabelEntry(Mathf.RoundToInt(heading));
                        int headingVersion = Mathf.RoundToInt(heading) % 360;
                        if (headingVersion < 0)
                            headingVersion += 360;

                        SetDisplayBufferIfChanged(_headingLabel, headingEntry.Buffer, headingEntry.Length, false, Alpha(dim, 0.58f), headingVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 107, ref _appliedHeadingLabelVersion, ref _appliedHeadingLabelColor);
                        _lastStreamedHeadingDegrees = heading;
                    }
                }
            }

            if (shouldRefreshTelemetryText)
            {
                if (_biosRecoveryMode)
                {
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    ResolveMetricIntBuffer(_depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, Mathf.RoundToInt(localizedDepth), out char[] depthBuffer, out int depthLength);
                    SetDisplayBufferIfChanged(_depthLabel, depthBuffer, depthLength, localizedRtl, Alpha(primary, 0.98f), Mathf.RoundToInt(localizedDepth), false, 0f, 0, 0, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetCanvasGroupVisible(_telemetrySupplementCanvasGroup, false);
                }
                else if (hullStressWhisperMode)
                {
                    SetDisplayBufferIfChanged(_depthLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedPrimary, 0.96f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 211, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetDisplayBufferIfChanged(_temperatureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedDim, 0.84f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 223, ref _appliedTemperatureWhisperVersion, ref _appliedTemperatureColor);
                    SetDisplayBufferIfChanged(_pressureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedDim, 0.64f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 227, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                    _hasAppliedDepthValue = false;
                    _hasAppliedTemperatureTenths = false;
                    _hasAppliedPressureTenths = false;
                }
                else if (corruptedMode)
                {
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    ResolveMetricIntBuffer(_depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, Mathf.RoundToInt(localizedDepth), out char[] depthBuffer, out int depthLength);
                    ResolveMetricFloatTenthsBuffer(_temperatureTemplateBuffer, _temperatureTemplateLength, ref _temperatureDisplayBuffer, localizedTemperature, out char[] temperatureBuffer, out int temperatureLength);
                    ResolveMetricFloatTenthsBuffer(_pressureTemplateBuffer, _pressureTemplateLength, ref _pressureDisplayBuffer, pressure, out char[] pressureBuffer, out int pressureLength);
                    SetDisplayBufferIfChanged(_depthLabel, depthBuffer, depthLength, localizedRtl, Alpha(pulsedPrimary, 0.96f), Mathf.RoundToInt(localizedDepth), true, displayCorruptionIntensity, _corruptionFrameVersion, 211, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetDisplayBufferIfChanged(_temperatureLabel, temperatureBuffer, temperatureLength, localizedRtl, Alpha(pulsedDim, 0.84f), Mathf.RoundToInt(localizedTemperature * 10f), true, displayCorruptionIntensity, _corruptionFrameVersion, 223, ref _appliedTemperatureWhisperVersion, ref _appliedTemperatureColor);
                    SetDisplayBufferIfChanged(_pressureLabel, pressureBuffer, pressureLength, localizedRtl, Alpha(pulsedDim, 0.64f), Mathf.RoundToInt(pressure * 10f), true, displayCorruptionIntensity, _corruptionFrameVersion, 227, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                    _hasAppliedDepthValue = false;
                    _hasAppliedTemperatureTenths = false;
                    _hasAppliedPressureTenths = false;
                }
                else
                {
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    SetMetricIntTemplateIfChanged(_depthLabel, _depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, Mathf.RoundToInt(localizedDepth), localizedRtl, Alpha(pulsedPrimary, 0.96f), ref _appliedDepthValue, ref _hasAppliedDepthValue, ref _appliedDepthColor);
                    SetMetricFloatTenthsTemplateIfChanged(_temperatureLabel, _temperatureTemplateBuffer, _temperatureTemplateLength, ref _temperatureDisplayBuffer, localizedTemperature, localizedRtl, Alpha(pulsedDim, 0.84f), ref _appliedTemperatureTenths, ref _hasAppliedTemperatureTenths, ref _appliedTemperatureColor);
                    SetMetricFloatTenthsTemplateIfChanged(_pressureLabel, _pressureTemplateBuffer, _pressureTemplateLength, ref _pressureDisplayBuffer, pressure, localizedRtl, Alpha(pulsedDim, 0.64f), ref _appliedPressureTenths, ref _hasAppliedPressureTenths, ref _appliedPressureColor);
                }

                _lastStreamedOxygen01 = oxygen;
                _lastStreamedDepthMeters = depth;
                _lastStreamedTemperature = localizedTemperature;
                _lastStreamedPressure = pressure;
            }

            Color statusColor = PickAccent(oxygen, power, health, safeDepthNormalized, pulsedPrimary, pulsedWarning);
            if (_biosRecoveryMode)
            {
                float criticalAlpha = IsBlinkVisible(Time.unscaledTime, 12f) ? 1f : 0.2f;
                LocNumericBuffer.Write(DefaultCriticalLabel.AsSpan(), out char[] criticalBuffer, out int criticalLength);
                SetDisplayBufferIfChanged(_statusLabel, criticalBuffer, criticalLength, false, Alpha(primary, criticalAlpha), 1, false, 0f, 0, 0, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                _hasAppliedStatusKeyHash = false;
            }
            else if (toolDepletedWarningActive)
            {
                float depletedAlpha = IsBlinkVisible(Time.unscaledTime, 16f) ? 1f : 0.22f;
                LocNumericBuffer.Write(DefaultToolDepletedLabel.AsSpan(), out char[] depletedBuffer, out int depletedLength);
                SetDisplayBufferIfChanged(_statusLabel, depletedBuffer, depletedLength, false, Alpha(pulsedWarning, depletedAlpha), _toolDepletedVersion ^ _toolDepletedHashId, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 307, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                _hasAppliedStatusKeyHash = false;
            }
            else if (hullStressWhisperMode)
            {
                _hasAppliedStatusKeyHash = false;
                SetDisplayBufferIfChanged(_statusLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, statusColor, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 307, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
            }
            else
            {
                _appliedStatusWhisperVersion = int.MinValue;
                int statusKeyHash = ResolveStatusKeyHash(oxygen, power, health, safeDepthNormalized, depth, safeDepth, depthDelta);
                if (corruptedMode)
                {
                    ResolveLocalizedKeyBuffer(statusKeyHash, out char[] statusBuffer, out int statusLength);
                    SetDisplayBufferIfChanged(_statusLabel, statusBuffer, statusLength, localizedRtl, statusColor, statusKeyHash, true, displayCorruptionIntensity, _corruptionFrameVersion, 307, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                    _hasAppliedStatusKeyHash = false;
                }
                else
                {
                    SetLocalizedKeyIfChanged(_statusLabel, statusKeyHash, localizedRtl, statusColor, ref _appliedStatusKeyHash, ref _hasAppliedStatusKeyHash, ref _appliedStatusLabelColor);
                }
            }

            Color oxygenAccent = pulsedPrimary;
            Color healthAccent = Color.Lerp(pulsedPrimary, pulsedDim, 0.24f);
            Color energyAccent = Color.Lerp(pulsedPrimary, pulsedWarning, 0.28f);

            UpdateGauge(ref _oxygenGauge, _displayOxygen01, oxygenCurrent, oxygenAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 401, dt, OxygenGaugeDamping, shouldRefreshGaugeText);
            UpdateGauge(ref _healthGauge, _displayHealth01, healthCurrent, healthAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 503, dt, HealthGaugeDamping, shouldRefreshGaugeText);
            UpdateGauge(ref _powerGauge, _displayPower01, energyCurrent, energyAccent, pulsedDim, pulsedWarning, localizedRtl, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 607, dt, BatteryGaugeDamping, shouldRefreshGaugeText);
            if (shouldRefreshGaugeText)
            {
                _lastStreamedPower01 = power;
                _lastStreamedHealth01 = health;
            }

            if (shouldRefreshQuickbar)
            {
                RefreshQuickbarVisuals(pulsedPrimary, pulsedDim, pulsedWarning);
                _quickbarVisualsInitialized = true;
            }
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

        private bool NeedsHeadingCadenceRefresh(bool cadenceGateOpen, float headingDegrees)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedHeadingDegrees) ||
                   Mathf.Abs(Mathf.DeltaAngle(_lastStreamedHeadingDegrees, headingDegrees)) >= HeadingStreamThresholdDegrees;
        }

        private bool NeedsTelemetryCadenceRefresh(bool cadenceGateOpen, float oxygen01, float depthMeters, float localizedTemperature, float pressureAtm)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedOxygen01) ||
                   Mathf.Abs(oxygen01 - _lastStreamedOxygen01) >= OxygenStreamThreshold01 ||
                   !float.IsFinite(_lastStreamedDepthMeters) ||
                   Mathf.Abs(depthMeters - _lastStreamedDepthMeters) >= DepthStreamThresholdMeters ||
                   !float.IsFinite(_lastStreamedTemperature) ||
                   Mathf.Abs(localizedTemperature - _lastStreamedTemperature) >= TemperatureStreamThreshold ||
                   !float.IsFinite(_lastStreamedPressure) ||
                   Mathf.Abs(pressureAtm - _lastStreamedPressure) >= PressureStreamThreshold;
        }

        private bool NeedsGaugeCadenceRefresh(bool cadenceGateOpen, float oxygen01, float power01, float health01)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedOxygen01) ||
                   Mathf.Abs(oxygen01 - _lastStreamedOxygen01) >= OxygenStreamThreshold01 ||
                   !float.IsFinite(_lastStreamedPower01) ||
                   Mathf.Abs(power01 - _lastStreamedPower01) >= OxygenStreamThreshold01 ||
                   !float.IsFinite(_lastStreamedHealth01) ||
                   Mathf.Abs(health01 - _lastStreamedHealth01) >= OxygenStreamThreshold01;
        }

        private static float DampHudValue(float displayValue, float targetValue, float dampFactor, float dt)
        {
            if (Mathf.Abs(displayValue - targetValue) <= GaugeSmoothingEpsilon)
                return targetValue;

            float interpolation = 1f - Mathf.Exp(-Mathf.Max(0f, dampFactor) * Mathf.Max(0f, dt));
            return Mathf.Lerp(displayValue, targetValue, interpolation);
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

        private void ResolveSuitLabelBuffer(out char[] buffer, out int length, out int version, out bool rtl)
        {
            string runtimeOverrideLabel = PlayerExpressionManager.ActiveSuitLabelOverride;
            string overrideLabel = !string.IsNullOrWhiteSpace(runtimeOverrideLabel)
                ? runtimeOverrideLabel
                : (_activeProfile != null ? _activeProfile.DisplayNameOverride : null);
            bool rtlChanged = _cachedSuitLabelRtl != LocalizedMeasurementFormatter.IsRightToLeft(LocRegistry.ActiveLanguage);
            if (!ReferenceEquals(_cachedSuitLabelSuit, _activeSuit) ||
                !string.Equals(_cachedSuitLabelOverride, overrideLabel, System.StringComparison.Ordinal) ||
                rtlChanged)
            {
                _cachedSuitLabelSuit = _activeSuit;
                _cachedSuitLabelOverride = overrideLabel;
                _cachedSuitLabelRtl = LocalizedMeasurementFormatter.IsRightToLeft(LocRegistry.ActiveLanguage);

                if (!string.IsNullOrWhiteSpace(overrideLabel))
                {
                    WriteUppercaseTextToBuffer(overrideLabel.AsSpan(), replaceUnderscores: false, ref _cachedSuitLabelBuffer, out _cachedSuitLabelLength, out _cachedSuitLabelVersion);
                }
                else if (_activeSuit != null)
                {
                    WriteUppercaseTextToBuffer(_activeSuit.name.AsSpan(), replaceUnderscores: true, ref _cachedSuitLabelBuffer, out _cachedSuitLabelLength, out _cachedSuitLabelVersion);
                }
                else
                {
                    WriteUppercaseTextToBuffer(DefaultSuitLabel.AsSpan(), replaceUnderscores: false, ref _cachedSuitLabelBuffer, out _cachedSuitLabelLength, out _cachedSuitLabelVersion);
                }
            }

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

        private static void UpdateGauge(
            ref GaugeRefs gauge,
            float normalized,
            float currentValue,
            Color primary,
            Color dim,
            Color warning,
            bool localizedRtl,
            char[] hullStressWhisperBuffer,
            int hullStressWhisperLength,
            int hullStressWhisperVersion,
            bool corruptedMode,
            float corruptionIntensity,
            int corruptionVersion,
            int corruptionSalt,
            float dt,
            float dampFactor,
            bool updateValueText)
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

                float displayFill = gauge.HasCachedFillAmount
                    ? DampHudValue(gauge.CachedFillAmount, clamped, dampFactor, dt)
                    : clamped;
                if (Mathf.Abs(displayFill - clamped) <= GaugeSmoothingEpsilon)
                    displayFill = clamped;

                if (!gauge.HasCachedFillAmount || Mathf.Abs(gauge.CachedFillAmount - displayFill) > FillWriteEpsilon)
                {
                    gauge.RingFill.fillAmount = displayFill;
                    gauge.CachedFillAmount = displayFill;
                    gauge.HasCachedFillAmount = true;
                }

                gauge.LastTargetFillAmount = clamped;
                gauge.HasLastTargetFillAmount = true;
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
                    SetDisplayBufferIfChanged(
                        gauge.Label,
                        hullStressWhisperBuffer,
                        hullStressWhisperLength,
                        localizedRtl,
                        labelColor,
                        hullStressWhisperVersion,
                        corruptedMode,
                        corruptionIntensity,
                        corruptionVersion,
                        corruptionSalt + 11,
                        ref gauge.CachedLabelWhisperVersion,
                        ref gauge.CachedLabelColor);
                }
                else
                {
                    gauge.CachedLabelWhisperVersion = int.MinValue;
                    if (corruptedMode)
                    {
                        ResolveLocalizedKeyBuffer(gauge.LabelKeyHash, out char[] labelBuffer, out int labelLength);
                        SetDisplayBufferIfChanged(
                            gauge.Label,
                            labelBuffer,
                            labelLength,
                            localizedRtl,
                            labelColor,
                            gauge.LabelKeyHash,
                            true,
                            corruptionIntensity,
                            corruptionVersion,
                            corruptionSalt + 11,
                            ref gauge.CachedLabelWhisperVersion,
                            ref gauge.CachedLabelColor);
                        gauge.HasCachedLabelKeyHash = false;
                    }
                    else
                    {
                        SetLocalizedKeyIfChanged(gauge.Label, gauge.LabelKeyHash, localizedRtl, labelColor, ref gauge.CachedLabelKeyHash, ref gauge.HasCachedLabelKeyHash, ref gauge.CachedLabelColor);
                    }
                }
            }

            Color valueColor = Alpha(accent, 0.98f);
            int roundedValue = Mathf.RoundToInt(currentValue);
            if (gauge.Value != null && updateValueText)
            {
                if (hullStressWhisperMode)
                {
                    SetDisplayBufferIfChanged(
                        gauge.Value,
                        hullStressWhisperBuffer,
                        hullStressWhisperLength,
                        localizedRtl,
                        valueColor,
                        hullStressWhisperVersion,
                        corruptedMode,
                        corruptionIntensity,
                        corruptionVersion,
                        corruptionSalt + 23,
                        ref gauge.CachedValueWhisperVersion,
                        ref gauge.CachedValueColor);
                    gauge.HasCachedRoundedValue = false;
                }
                else
                {
                    gauge.CachedValueWhisperVersion = int.MinValue;
                    if (corruptedMode)
                    {
                        ResolveNumericIntBuffer(gauge.ValueBuffer, roundedValue, out char[] valueBuffer, out int valueLength);
                        SetDisplayBufferIfChanged(
                            gauge.Value,
                            valueBuffer,
                            valueLength,
                            false,
                            valueColor,
                            roundedValue,
                            true,
                            corruptionIntensity,
                            corruptionVersion,
                            corruptionSalt + 23,
                            ref gauge.CachedValueWhisperVersion,
                            ref gauge.CachedValueColor);
                        gauge.HasCachedRoundedValue = false;
                    }
                    else
                    {
                        SetNumericIntIfChanged(gauge.Value, gauge.ValueBuffer, roundedValue, valueColor, ref gauge.CachedRoundedValue, ref gauge.HasCachedRoundedValue, ref gauge.CachedValueColor);
                    }
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
            refs.CanvasGroup = EnsureCanvasGroup(refs.Root);

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

        private void BuildQuickbarHierarchy(RectTransform parent)
        {
            if (parent == null)
                return;

            float resolvedSlotSize = Mathf.Max(36f, quickbarSlotSize);
            float resolvedSlotGap = Mathf.Max(2f, quickbarSlotGap);
            float totalWidth = (QuickbarSlotCount * resolvedSlotSize) + ((QuickbarSlotCount - 1) * resolvedSlotGap);
            float startX = (-totalWidth * 0.5f) + (resolvedSlotSize * 0.5f);

            for (int slotIndex = 0; slotIndex < QuickbarSlotCount; slotIndex++)
            {
                QuickbarSlotRefs refs = new QuickbarSlotRefs();
                refs.Root = CreateRect("QuickbarSlot_" + slotIndex, parent);
                Anchor(
                    refs.Root,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + (slotIndex * (resolvedSlotSize + resolvedSlotGap)), 0f),
                    new Vector2(resolvedSlotSize, resolvedSlotSize));

                refs.Backdrop = CreateImage("Backdrop", refs.Root, new Color(0.04f, 0.1f, 0.12f, 0.55f));
                Stretch(refs.Backdrop.rectTransform, 0f, 0f, 0f, 0f);
                refs.Backdrop.raycastTarget = false;

                refs.Accent = CreateImage("Accent", refs.Root, new Color(0.46f, 0.98f, 0.94f, 0f));
                Anchor(refs.Accent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(resolvedSlotSize - 8f, 2f));
                refs.Accent.raycastTarget = false;

                RectTransform iconRect = CreateRect("Icon", refs.Root);
                Stretch(iconRect, 7f, 7f, 7f, 7f);
                refs.Icon = iconRect.gameObject.AddComponent<Image>();
                refs.Icon.preserveAspect = true;
                refs.Icon.raycastTarget = false;
                refs.Icon.material = null;
                refs.Icon.maskable = false;
                refs.Icon.color = new Color(1f, 1f, 1f, 0f);

                refs.Key = CreateText("Key", refs.Root, 10f, FontStyles.Bold, TextAlignmentOptions.TopLeft, 0.45f, ResolveLabelFontAsset());
                Anchor(refs.Key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(9f, -7f), new Vector2(14f, 12f));
                ApplyHudCharArray(refs.Key, ResolveQuickbarKeyBuffer(slotIndex), 1);

                _quickbarSlots[slotIndex] = refs;
            }
        }

        private static char[] ResolveQuickbarKeyBuffer(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return s_quickbarSlotOneChars;
                case 1:
                    return s_quickbarSlotTwoChars;
                case 2:
                    return s_quickbarSlotThreeChars;
                case 3:
                    return s_quickbarSlotFourChars;
                default:
                    return s_emptyHudChars;
            }
        }

        private void RefreshQuickbarVisuals(Color primary, Color dim, Color warning)
        {
            if (_toolManager == null || _quickbarSlots == null)
                return;

            if (_playerInventory != null && _lastInventoryVersion != _playerInventory.InventoryVersion)
            {
                _lastInventoryVersion = _playerInventory.InventoryVersion;
                InvalidateQuickbarSlotHashCache();
            }

            int activeSlot = _toolManager.CurrentSlotIndex;
            for (int slotIndex = 0; slotIndex < QuickbarSlotCount; slotIndex++)
                RefreshQuickbarSlotVisual(slotIndex, activeSlot, primary, dim, warning);
        }

        private void RefreshQuickbarSlotVisual(int slotIndex, int activeSlot, Color primary, Color dim, Color warning)
        {
            if ((uint)slotIndex >= (uint)_quickbarSlots.Length)
                return;

            QuickbarSlotRefs refs = _quickbarSlots[slotIndex];
            if (refs.Root == null)
                return;

            GameObject prefab = _toolManager.GetAssignedToolPrefab(slotIndex);
            int itemHashId = ResolveQuickbarSlotHash(slotIndex, prefab);
            bool hasRuntimeDescriptor = TryResolveQuickbarRuntimeDescriptor(itemHashId, out _);
            bool available = itemHashId != 0 && IsInventoryHashAvailable(itemHashId);
            if (!available)
                available = _toolManager.IsToolAvailableInSlot(slotIndex);

            Sprite desiredSprite = ResolveQuickbarSlotSprite(itemHashId, hasRuntimeDescriptor);
            bool active = slotIndex == activeSlot;
            Color backdropColor = active
                ? new Color(primary.r, primary.g, primary.b, 0.24f)
                : new Color(0.04f, 0.1f, 0.12f, 0.55f);
            Color accentColor = active
                ? new Color(primary.r, primary.g, primary.b, 0.95f)
                : new Color(primary.r, primary.g, primary.b, 0.18f);
            Color keyColor = active ? Alpha(primary, 0.9f) : Alpha(dim, 0.48f);
            Color iconColor = desiredSprite == null
                ? new Color(1f, 1f, 1f, 0f)
                : (available ? Color.white : new Color(1f, 1f, 1f, 0.22f));
            if (!available && active)
                accentColor = Alpha(warning, 0.68f);

            if (refs.CachedBackdropColor != backdropColor && refs.Backdrop != null)
            {
                refs.Backdrop.color = backdropColor;
                refs.CachedBackdropColor = backdropColor;
            }

            if (refs.CachedAccentColor != accentColor && refs.Accent != null)
            {
                refs.Accent.color = accentColor;
                refs.CachedAccentColor = accentColor;
            }

            if (refs.CachedKeyColor != keyColor && refs.Key != null)
            {
                refs.Key.color = keyColor;
                refs.CachedKeyColor = keyColor;
            }

            if (!ReferenceEquals(refs.CachedIconSprite, desiredSprite) && refs.Icon != null)
            {
                refs.Icon.sprite = desiredSprite;
                refs.CachedIconSprite = desiredSprite;
            }

            bool iconVisible = desiredSprite != null;
            if (refs.CachedIconVisible != iconVisible || refs.CachedIconColor != iconColor)
            {
                refs.Icon.color = iconColor;
                refs.CachedIconVisible = iconVisible;
                refs.CachedIconColor = iconColor;
            }

            refs.CachedAvailable = available;
            refs.CachedActive = active;
            _quickbarSlots[slotIndex] = refs;
        }

        private void InvalidateQuickbarSlotHashCache()
        {
            for (int slotIndex = 0; slotIndex < QuickbarSlotCount; slotIndex++)
            {
                _quickbarSlotHashCache[slotIndex] = 0;
                _quickbarSlotHashResolved[slotIndex] = false;
                _quickbarSlotPrefabCache[slotIndex] = null;
            }
        }

        private int ResolveQuickbarSlotHash(int slotIndex, GameObject prefab)
        {
            if ((uint)slotIndex >= (uint)QuickbarSlotCount)
                return 0;

            if (!ReferenceEquals(_quickbarSlotPrefabCache[slotIndex], prefab))
            {
                _quickbarSlotHashResolved[slotIndex] = false;
                _quickbarSlotHashCache[slotIndex] = 0;
                _quickbarSlotPrefabCache[slotIndex] = prefab;
            }

            if (_quickbarSlotHashResolved[slotIndex])
                return _quickbarSlotHashCache[slotIndex];

            int itemHashId = 0;
            if (prefab != null &&
                prefab.TryGetComponent(out PlayerTool tool) &&
                tool.ToolData != null)
            {
                string persistentId = tool.ToolData.PersistentId;
                if (!string.IsNullOrWhiteSpace(persistentId))
                    itemHashId = LocHash.Compute(persistentId);
            }

            _quickbarSlotHashCache[slotIndex] = itemHashId;
            _quickbarSlotHashResolved[slotIndex] = true;
            return itemHashId;
        }

        private bool TryResolveQuickbarRuntimeDescriptor(int itemHashId, out ItemCatalog.ItemRuntimeDescriptor descriptor)
        {
            descriptor = default;
            return itemHashId != 0 &&
                   _itemCatalog != null &&
                   _itemCatalog.TryGetRuntimeDescriptor(itemHashId, out descriptor);
        }

        private Sprite ResolveQuickbarSlotSprite(int itemHashId, bool hasRuntimeDescriptor)
        {
            if (!hasRuntimeDescriptor || _itemCatalog == null)
                return null;

            ItemData item = _itemCatalog.FindByHash(itemHashId);
            return item != null ? item.icon : null;
        }

        private bool IsInventoryHashAvailable(int itemHashId)
        {
            if (itemHashId == 0 || _playerInventory == null)
                return false;

            InventoryGrid grid = _playerInventory.Grid;
            if (grid == null)
                return false;

            NativeArray<int>.ReadOnly anchorHashIds = grid.AnchorHashIds;
            NativeArray<ushort>.ReadOnly stackCounts = _playerInventory.GetStackCountsReadOnly();
            if (!anchorHashIds.IsCreated || !stackCounts.IsCreated)
                return false;

            int anchorCount = Mathf.Min(anchorHashIds.Length, stackCounts.Length);
            for (int anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
            {
                if (anchorHashIds[anchorIndex] == itemHashId && stackCounts[anchorIndex] > 0)
                    return true;
            }

            return false;
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
            // Runtime assembly must not depend on editor-only APIs here.
            // Icon textures are serialized authoring data; missing assignments stay explicit.
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
            image.material = null;
            image.maskable = false;
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
            label.fontSharedMaterial = label.font != null ? label.font.material : null;
            label.maskable = false;
            ApplyHudCharArray(label, s_emptyHudChars, 0);
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
            return fontName.Contains("Digit") || fontName.Contains("Ñ†Ð¸Ñ„");
        }

        private static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static void SetMetricIntTemplateIfChanged(
            TextMeshProUGUI label,
            char[] templateBuffer,
            int templateLength,
            ref char[] displayBuffer,
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
                EnsureCharCapacity(ref displayBuffer, templateLength + 24);
                FixedCharBuffer fixedBuffer = new FixedCharBuffer(displayBuffer);
                if (!fixedBuffer.TryWriteTemplateInt(templateBuffer.AsSpan(0, templateLength), value, out int length))
                    length = 0;

                ApplyHudCharArray(label, fixedBuffer.Buffer, length);
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
            ref char[] displayBuffer,
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
                EnsureCharCapacity(ref displayBuffer, templateLength + 24);
                FixedCharBuffer fixedBuffer = new FixedCharBuffer(displayBuffer);
                if (!fixedBuffer.TryWriteTemplateFloatTenths(templateBuffer.AsSpan(0, templateLength), roundedTenths, out int length))
                    length = 0;

                ApplyHudCharArray(label, fixedBuffer.Buffer, length);
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

                ApplyHudCharArray(label, stagingBuffer, length);
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

                ApplyHudCharArray(label, buffer, length);
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
                ApplyHudCharArray(label, buffer, length);
                cachedVersion = version;
            }

            if (cachedColor != color)
            {
                label.color = color;
                cachedColor = color;
            }
        }

        private static void SetDisplayBufferIfChanged(
            TextMeshProUGUI label,
            char[] sourceBuffer,
            int sourceLength,
            bool rtl,
            Color color,
            int version,
            bool corruptedMode,
            float corruptionIntensity,
            int corruptionVersion,
            int corruptionSalt,
            ref int cachedVersion,
            ref Color cachedColor)
        {
            if (!corruptedMode)
            {
                SetCharBufferIfChanged(label, sourceBuffer, sourceLength, rtl, color, version, ref cachedVersion, ref cachedColor);
                return;
            }

            GlitchEncoder.ApplyDecay(
                sourceBuffer,
                sourceLength,
                corruptionIntensity,
                unchecked((corruptionVersion * 397) ^ corruptionSalt),
                out char[] corruptedBuffer,
                out int corruptedLength);
            int corruptedDisplayVersion = unchecked((version * 397) ^ corruptionVersion ^ corruptionSalt);
            SetCharBufferIfChanged(label, corruptedBuffer, corruptedLength, rtl, color, corruptedDisplayVersion, ref cachedVersion, ref cachedColor);
        }

        private static void ResolveMetricIntBuffer(char[] templateBuffer, int templateLength, ref char[] displayBuffer, int value, out char[] buffer, out int length)
        {
            EnsureCharCapacity(ref displayBuffer, templateLength + 24);
            FixedCharBuffer fixedBuffer = new FixedCharBuffer(displayBuffer);
            if (!fixedBuffer.TryWriteTemplateInt(templateBuffer.AsSpan(0, templateLength), value, out length))
                length = 0;

            buffer = fixedBuffer.Buffer;
        }

        private static void ResolveMetricFloatTenthsBuffer(char[] templateBuffer, int templateLength, ref char[] displayBuffer, float value, out char[] buffer, out int length)
        {
            int roundedTenths = Mathf.RoundToInt(value * 10f);
            EnsureCharCapacity(ref displayBuffer, templateLength + 24);
            FixedCharBuffer fixedBuffer = new FixedCharBuffer(displayBuffer);
            if (!fixedBuffer.TryWriteTemplateFloatTenths(templateBuffer.AsSpan(0, templateLength), roundedTenths, out length))
                length = 0;

            buffer = fixedBuffer.Buffer;
        }

        private static void ResolveNumericIntBuffer(char[] stagingBuffer, int value, out char[] buffer, out int length)
        {
            FixedCharBuffer fixedBuffer = new FixedCharBuffer(stagingBuffer);
            buffer = fixedBuffer.Buffer;
            if (!fixedBuffer.TryWriteInt(value, out length))
                length = 0;
        }

        private static void ResolveLocalizedKeyBuffer(int keyHash, out char[] buffer, out int length)
        {
            if (LocalizationManager.Instance == null)
            {
                TryGetFallbackBuffer(keyHash, out buffer, out length);
                return;
            }

            LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length);
        }

        private static void SetLocalizedRtlState(TMP_Text label, bool rtl)
        {
            if (label != null && label.isRightToLeftText != rtl)
                label.isRightToLeftText = rtl;
        }

        private static void ApplyHudCharArray(TMP_Text label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
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

        private static void WriteUppercaseTextToBuffer(ReadOnlySpan<char> source, bool replaceUnderscores, ref char[] buffer, out int length, out int version)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            int hash = 17;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (replaceUnderscores && character == '_')
                    character = ' ';

                character = ConvertUppercaseInvariantChar(character);
                buffer[i] = character;
                hash = unchecked((hash * 397) ^ character);
            }

            length = source.Length;
            version = source.Length == 0 ? 0 : hash;
        }

        private static char ConvertUppercaseInvariantChar(char value)
        {
            if ((uint)(value - 'a') <= 25u)
                return (char)(value - 32);

            if ((uint)(value - '\u0430') <= 31u)
                return (char)(value - 32);

            return value == '\u0451' ? '\u0401' : value;
        }

        private static void EnsureCharCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer != null && buffer.Length >= requiredLength)
                return;

            int capacity = buffer == null ? 32 : buffer.Length;
            int growthWatchdog = 31;
            while (capacity < requiredLength && growthWatchdog-- > 0)
            {
                if (capacity > (int.MaxValue >> 1))
                {
                    capacity = requiredLength;
                    break;
                }

                capacity <<= 1;
            }

            if (capacity < requiredLength)
                capacity = requiredLength;

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
            _cachedGraphicRaycaster = null;
            _cachedUiScaler = null;
            _cachedHullStressWhisperBucket = int.MinValue;
            _cachedHullStressWhisperText = null;
            _rootBaseAnchoredPositionCaptured = false;
        }

        private Canvas ResolveTargetCanvas()
        {
            if (targetCanvas == null)
                targetCanvas = GetComponent<Canvas>();

            if (targetCanvas != null &&
                (_cachedGraphicRaycaster == null || _cachedGraphicRaycaster.gameObject != targetCanvas.gameObject))
            {
                targetCanvas.TryGetComponent(out _cachedGraphicRaycaster);
            }

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

        private static Canvas FindSceneCanvasByName(string canvasName)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                s_sceneRootResolveBuffer.Clear();
                scene.GetRootGameObjects(s_sceneRootResolveBuffer);
                for (int i = 0; i < s_sceneRootResolveBuffer.Count; i++)
                {
                    Canvas canvas = FindCanvasInHierarchy(s_sceneRootResolveBuffer[i].transform, canvasName);
                    if (canvas != null)
                        return canvas;
                }
            }

            return null;
        }

        private static Canvas FindCanvasInHierarchy(Transform root, string canvasName)
        {
            if (root == null)
                return null;

            if (root.name == canvasName && root.TryGetComponent(out Canvas canvas))
                return canvas;

            for (int i = 0; i < root.childCount; i++)
            {
                Canvas nested = FindCanvasInHierarchy(root.GetChild(i), canvasName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        public void SetRenderPathProjectionSource(bool projectionSource)
        {
            RenderPath nextPath = projectionSource ? RenderPath.ProjectionSource : RenderPath.ScreenOverlay;
            if (renderPath == nextPath && CanvasStateMatchesRequestedRenderPath(nextPath, projectionCamera))
                return;

            renderPath = nextPath;
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RequestRefresh();
        }

        public void SetProjectionCamera(Camera camera)
        {
            if (projectionCamera == camera && CanvasStateMatchesRequestedRenderPath(renderPath, camera))
                return;

            projectionCamera = camera;
            _layoutBuilt = false;
            InvalidateVisualCaches();
            RequestRefresh();
        }

        private bool CanvasStateMatchesRequestedRenderPath(RenderPath requestedRenderPath, Camera requestedProjectionCamera)
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
                return false;

            if (requestedRenderPath == RenderPath.ProjectionSource)
            {
                return canvas.renderMode == RenderMode.WorldSpace &&
                       ReferenceEquals(canvas.worldCamera, requestedProjectionCamera);
            }

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                   canvas.worldCamera == null;
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
                QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);
                TryRegisterRuntimeTick();
                return;
            }

            if (!isActiveAndEnabled || !keepVisibleInEditMode)
                return;

            NormalizeCanvas();
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying)
                return;

            if (!_tickRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _tickRegistered = true;
            }

            if (_slowTickRegistered)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = true;
        }

        private void UnregisterRuntimeTick()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _tickRegistered = false;
            }

            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _slowTickRegistered = false;
        }

        private void RegisterActiveOverlay()
        {
            if (s_activeOverlays.Contains(this))
                return;

            s_activeOverlays.Add(this);
            RefreshActiveRuntimeInstance();
        }

        private void UnregisterActiveOverlay()
        {
            s_activeOverlays.Remove(this);
            RefreshActiveRuntimeInstance();
        }

        private static void RefreshActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;

            for (int i = 0; i < s_activeOverlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_activeOverlays[i];
                if (overlay != null &&
                    overlay.isActiveAndEnabled &&
                    overlay.renderPath == RenderPath.ScreenOverlay)
                {
                    ActiveRuntimeInstance = overlay;
                    return;
                }
            }

            for (int i = 0; i < s_activeOverlays.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_activeOverlays[i];
                if (overlay != null && overlay.isActiveAndEnabled)
                {
                    ActiveRuntimeInstance = overlay;
                    return;
                }
            }
        }
    }

#if false
    /// <summary>
    /// Canvas-root scaler that applies a single matrix-driven transform to a dedicated content root instead of using CanvasScaler relayout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton UI Scaler")]
    [RequireComponent(typeof(Canvas))]
    public sealed class HectonUIScaler : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, IOriginShiftListener
    {
        private const string ContentRootName = "HectonUI_ScaledRoot";

        [Header("â”€â”€ Scale Policy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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
        private bool _registeredToSlowTickManager;
        private bool _pendingContentRootBootstrap = true;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private float _lastAppliedScale = -1f;
        private Vector2 _lastAppliedReferenceResolution = Vector2.zero;
        private float _lastAppliedMatch = -1f;
        private Matrix4x4 _uiMatrix = Matrix4x4.identity;

        /// <summary>Current matrix applied to the scaled content root.</summary>
        public Matrix4x4 CurrentMatrix => _uiMatrix;

        /// <summary>Reference resolution currently used by the scaler.</summary>
        public Vector2 ReferenceResolution => referenceResolution;

        /// <summary>Scaled content parent used by first-party HUD overlays.</summary>
        public RectTransform ContentRoot => ResolveContentRootInternal(createIfMissing: Application.isPlaying);

        private void OnEnable()
        {
            ResolveCanvas();
            EnsureContentRoot();
            ApplyScale(force: true);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
            if (!Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_pendingContentRootBootstrap)
                return;

            if (ResolveContentRootInternal(createIfMissing: false) == null)
                return;

            ApplyScale(force: false);
        }

        public void SlowTick()
        {
            if (!Application.isPlaying)
                return;

            RegisterToTickManager();
            if (!_pendingContentRootBootstrap && ResolveContentRootInternal(createIfMissing: false) != null)
                return;

            EnsureContentRoot();
            ApplyScale(force: true);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastAppliedScale = -1f;
            _lastAppliedReferenceResolution = Vector2.zero;
            _lastAppliedMatch = -1f;
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;

            if (_pendingContentRootBootstrap)
                return;

            ApplyScale(force: true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            referenceResolution = new Vector2(
                Mathf.Max(1f, referenceResolution.x),
                Mathf.Max(1f, referenceResolution.y));
            matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
            minimumScale = Mathf.Max(0.1f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);
        }

        [ContextMenu("Rebuild UI")]
        private void RebuildUiInEditor()
        {
            if (Application.isPlaying)
                return;

            ResolveCanvas();
            EnsureContentRoot();
            ApplyScale(force: true);
        }
#endif

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
            if (Application.isPlaying)
            {
                _pendingContentRootBootstrap = _pendingContentRootBootstrap || ResolveContentRootInternal(createIfMissing: false) == null;
                if (!_pendingContentRootBootstrap)
                    ApplyScale(force: true);
            }
            else
            {
                ApplyScale(force: true);
            }
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
                RectTransform contentRoot = scaler.ResolveContentRootInternal(createIfMissing: false);
                if (contentRoot != null)
                    return contentRoot;
            }

            return canvas.transform as RectTransform;
        }

        private RectTransform ResolveContentRootInternal(bool createIfMissing)
        {
            ResolveCanvas();
            if (_targetCanvas == null)
                return null;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return null;

            if (_contentRoot != null && _contentRoot.gameObject != null)
                return SanitizeContentRoot(_contentRoot);

            _contentRoot = FindExistingChild(canvasRoot, ContentRootName);
            if (_contentRoot != null)
                return SanitizeContentRoot(_contentRoot);

            if (!createIfMissing)
                return null;

            return EnsureContentRoot();
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
                // COLD ALLOC: GameObject[1] â€” matrix-scaled HUD content root â€” owner: HectonUIScaler
                GameObject rootObject = new GameObject(ContentRootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _contentRoot = rootObject.GetComponent<RectTransform>();
                _contentRoot.SetParent(canvasRoot, false);
            }

            return SanitizeContentRoot(_contentRoot);
        }

        private RectTransform SanitizeContentRoot(RectTransform contentRoot)
        {
            if (contentRoot == null)
                return null;

            contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            contentRoot.pivot = new Vector2(0.5f, 0.5f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localRotation = Quaternion.identity;
            // Stamp the reference rect immediately so stretched HUD children never inherit Unity's 100x100 default RectTransform.
            contentRoot.sizeDelta = referenceResolution;
            contentRoot.localScale = Vector3.one;
            return contentRoot;
        }

        private void ApplyScale(bool force)
        {
            RectTransform contentRoot = EnsureContentRoot();
            if (contentRoot == null)
                return;

            ResolveRenderDimensions(out int screenWidth, out int screenHeight);
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
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.localScale = new Vector3(scale, scale, 1f);
            if (_targetCanvas != null && !Mathf.Approximately(_targetCanvas.scaleFactor, 1f))
                _targetCanvas.scaleFactor = 1f;

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastAppliedScale = scale;
            _lastAppliedReferenceResolution = referenceResolution;
            _lastAppliedMatch = matchWidthOrHeight;
        }

        private void ResolveRenderDimensions(out int width, out int height)
        {
            width = Mathf.Max(1, Screen.width);
            height = Mathf.Max(1, Screen.height);

            if (_targetCanvas == null ||
                _targetCanvas.renderMode != RenderMode.WorldSpace ||
                _targetCanvas.worldCamera == null)
            {
                return;
            }

            // World-space HUD layout is already projected onto the visor frustum by the canvas rect itself.
            // Scaling again from RT resolution collapses the authored layout toward the center as the RT downsamples.
            width = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.x));
            height = Mathf.RoundToInt(Mathf.Max(1f, referenceResolution.y));
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
            if (!Application.isPlaying)
                return;

            if (!_registeredToTickManager)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = true;
            }

            if (_registeredToSlowTickManager)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredToSlowTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }

            if (!_registeredToSlowTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredToSlowTickManager = false;
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
#endif
}
