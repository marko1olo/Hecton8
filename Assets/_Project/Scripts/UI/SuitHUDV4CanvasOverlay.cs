using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.UI;
using NASAPunk.Visor;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit HUD V4 Canvas Overlay")]
    [RequireComponent(typeof(Canvas))]
    public sealed class SuitHUDV4CanvasOverlay : MonoBehaviour, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IUIService, IScannerInterferenceUiSink, IGameBootstrapperEventListener, IPlayerSignalEventListener, ILocalizationLanguageChangedListener, ILocalizationCorruptionVisualStateListener, ISaveEventListener, IGlobalRegistryHotSwapListener
    {
        // Fixed-slot active HUD overlay registry. Overflow fails closed; no managed registry container.
        private const int MaxActiveOverlays = 4;
        private static SuitHUDV4CanvasOverlay s_activeOverlay0;
        private static SuitHUDV4CanvasOverlay s_activeOverlay1;
        private static SuitHUDV4CanvasOverlay s_activeOverlay2;
        private static SuitHUDV4CanvasOverlay s_activeOverlay3;
        private static int s_activeOverlayCount;
        private const int MaxHudDynamicTextBufferChars = 4096;
        private static readonly char[] s_sharedOversizedHudTextBuffer = new char[MaxHudDynamicTextBufferChars]; // COLD ALLOC: char[4096] - editor/no-GC fallback for oversized HUD text staging - owner: SuitHUDV4CanvasOverlay
        private static bool s_stencilRenderGraphRuntimeActive;
        // One save-failure bridge for the whole process, NOT one per overlay: this component is deliberately
        // multi-instance (s_activeOverlay0..3) and one bridge each would raise one notification per active HUD
        // canvas for a single failed write.
        private static HUDSaveNotificationLink s_saveFailureNotificationBridge;
        // Probe hashes for every SaveEvents status bucket. SaveEvents.ResolveStatusSlotIndex maps only the three
        // manual slot hashes to buckets 0..2 and everything else to the final bucket, so these four hashes address
        // all StatusSlotCapacity buckets. Polling all of them keeps the read correct whichever bucket the write
        // landed in - which matters, because SaveManager.ComputeSlotHash and SaveEvents.ComputeSlotHash are NOT the
        // same function today, so manual-slot statuses currently land in the trailing bucket rather than 0..2.
        // static readonly, not const: a const cannot hold a method call (CS0133).
        private static readonly uint s_saveStatusProbeSlot0Hash = SaveEvents.ComputeSlotHash(SaveEvents.ResolveManualSlotName(0));
        private static readonly uint s_saveStatusProbeSlot1Hash = SaveEvents.ComputeSlotHash(SaveEvents.ResolveManualSlotName(1));
        private static readonly uint s_saveStatusProbeSlot2Hash = SaveEvents.ComputeSlotHash(SaveEvents.ResolveManualSlotName(2));
        private const uint SaveStatusProbeUnknownSlotHash = 0u;
        private const int ThreatChevronRollRight = 0;
        private const int ThreatChevronRollUp = 1;
        private const int ThreatChevronRollLeft = 2;
        private const int ThreatChevronRollDown = 3;
        private const AdditionalCanvasShaderChannels AcousticRadarCanvasShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        // COLD ALLOC: Quaternion[4] - threat chevron cardinal roll LUT - owner: SuitHUDV4CanvasOverlay
        private static readonly Quaternion[] s_threatChevronRollByDominantAxis =
        {
            Quaternion.identity,
            new Quaternion(0f, 0f, 0.70710678f, 0.70710678f),
            new Quaternion(0f, 0f, 1f, 0f),
            new Quaternion(0f, 0f, -0.70710678f, 0.70710678f)
        };
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
        private const string DefaultHullStressWhisper = "THE SEA IS INSIDE THE GLASS";
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
        private const string DefaultStatusOptimizingCoreSystems = "OPTIMIZING CORE SYSTEMS";
        private const string DefaultToolDepletedLabel = "TOOL DEPLETED";
        private const string DefaultCriticalLabel = "CRITICAL";
        private const string DepthNumberToken = "{N0:F0}";
        private const string FixedTenthsNumberToken = "{N0:F1}";
        private const float DegreesToHalfRadians = 0.00872664626f;
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
        private const float AtmosphereRoomOxygenFreshSeconds = 0.35f;
        private const float DepthStreamThresholdMeters = 0.5f;
        private const float TemperatureStreamThreshold = 0.1f;
        private const float PressureStreamThreshold = 0.1f;
        private const float CadenceDeltaEpsilon = 0.000001f;
        private const float HeadingStreamThresholdDegrees = 1f;
        private const float OxygenCriticalHapticThreshold = 0.15f;
        private const float PowerCriticalHapticThreshold = 0.12f;
        private const float HealthCriticalHapticThreshold = 0.18f;
        private const float CriticalHapticCooldownSeconds = 0.65f;
        private const float CriticalHapticDurationSeconds = 0.5f;
        private const float CriticalHapticFrequencyHz = 7.5f;
        private const byte CriticalHapticPriority = ToolHapticsRuntime.PriorityCritical;
        private const byte BothMotorMask = 0b0011;
        private const byte CriticalMaskOxygen = 1 << 0;
        private const byte CriticalMaskPower = 1 << 1;
        private const byte CriticalMaskHealth = 1 << 2;
        private const float ToolDepletedWarningDurationSeconds = 2.25f;
        private const float MemorySubsystemBreachHoldSeconds = 4f;
        private const int LegacyGlitchReadabilityPrefixChars = 5;
        private const float SavingProgressFadeSpeed = 6.5f;
        private const float SavingProgressVisibleEpsilon = 0.001f;
        private const float SavingProgressMinimumVisibleSeconds = 0.35f;
        private const float SavingProgressHapticCooldownSeconds = 0.5f;
        private const float SavingProgressHapticDurationSeconds = 0.08f;
        private const float SavingProgressHapticFrequencyHz = 18f;
        private const byte SavingProgressHapticPriority = 1;
        // A failed write is held on screen well past SavingProgressMinimumVisibleSeconds so it cannot be mistaken
        // for the prompt fade-out of a completed one.
        private const float SavingProgressFailureHoldSeconds = 3.25f;
        // Radians per second, NOT Hz: IsBlinkVisible feeds this straight into EvaluateCheapSignedWave, whose
        // argument is a phase in radians (26 rad/s is ~4.1 Hz). Matches the homeostasis blink call convention.
        private const float SavingProgressFailureBlinkRadiansPerSecond = 26f;
        private const float CorruptedModeThreshold = 0.75f;
        private const float JitterAmplitudePixels = 7f;
        private const float JitterFrequencyRadians = 23f;
        private const int AnalogUiJitterQuantizationSteps = 32;
        private const float InvAnalogUiJitterQuantizationSteps = 1f / AnalogUiJitterQuantizationSteps;
        private const float InvTwoPi = 1f / (Mathf.PI * 2f);
        private const float DiegeticHudDistanceMeters = 0.5f;
        private const float DiegeticHudWorldScale = 0.0005f;
        private const float HelmetScissorInsetPixelsX = 48f;
        private const float HelmetScissorInsetPixelsY = 36f;
        private const float ProjectionNearClipSafetyPaddingMeters = 0.05f;
        private const float ProjectionPosePositionTolerance = 0.0001f;
        private const float ProjectionPoseRotationSinHalfSqTolerance = 0.00000000761543549f;
        private const float ProjectionPoseScaleTolerance = 0.000001f;
        private const float MaterialFloatWriteEpsilon = 0.0005f;
        private const float ScannerEvidenceEpsilon = 0.0001f;
        private const float ScannerTraceEvidenceThreshold01 = 0.1f;
        private const float ScannerHologramFragmentSignalFloor = 0.18f;
        private const float ScannerHologramMaterialSignalFloor = 0.24f;
        private const float ScannerHologramTraceSignalFloor = 0.32f;
        private const float ScannerHologramFaunaSignalFloor = 0.85f;
        private const int HudPerformanceWarningCooldownFrames = 300;
        private const int SystemHealthWarningStaleFrames = 120;
        private const string WorldGeometrySortingLayer = "WorldGeometry";
        private const int MaxThreatChevronCount = 4;
        private const int HudInternalLayerIndex = 17;
        private const int MaxReactiveUiCadenceStride = 4;
        private const float ThreatChevronPlaneBiasMeters = 0.0004f;
        private const float VrLazyFollowPositionSharpness = 18f;
        private const float VrLazyFollowRotationSharpness = 22f;
        private static int UiLayerIndex = -1;
        private static bool s_layerCacheInitialized;
        private const int DefaultLayerIndex = 0;
        private static readonly int _ThreatChevronBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _ThreatChevronFlickerFrequencyId = Shader.PropertyToID("_FlickerFrequency");
        private static readonly int _ThreatChevronFlickerIntensityId = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int _ThreatChevronFillAlphaId = Shader.PropertyToID("_FillAlpha");
        private static readonly int _ThreatChevronInstanceDataId = Shader.PropertyToID("_InstanceData");
        private static readonly uint _HudSolveBudgetWarningHash = unchecked((uint)LocHash.Compute("HUD_CANVAS_SOLVE_OVER_BUDGET"));
        private static readonly uint _HudSolveBudgetContextHash = unchecked((uint)LocHash.Compute("SuitHUDV4CanvasOverlay.Tick"));
        private static readonly long _HudSolveBudgetWarningTicks = Math.Max(1L, Stopwatch.Frequency / 5000L);
#if UNITY_EDITOR
        private static readonly Color s_gizmoCanvasBoundsColor = new Color(0.12f, 0.68f, 0.92f, 0.65f);
        private static readonly Color s_gizmoElementFillColor = new Color(0.12f, 0.68f, 0.92f, 0.08f);
        private static readonly Color s_gizmoTextColor = new Color(0.92f, 0.92f, 0.92f, 0.72f);
        private static readonly Color s_gizmoProjectionPlaneColor = new Color(1f, 0.5f, 0f, 0.5f);
        private static GUIStyle s_gizmoLabelStyle;
#endif
        private const int MemorySubsystemBreachTextLength = 34;
        private static ReadOnlySpan<char> AtmLabelChars => DefaultAtmLabel.AsSpan();
        private static ReadOnlySpan<char> DepthLabelChars => "DEPTH".AsSpan();
        private static ReadOnlySpan<char> TemperatureLabelChars => DefaultTemperatureLabel.AsSpan();
        private static ReadOnlySpan<char> PressureLabelChars => DefaultPressureLabel.AsSpan();
        private static ReadOnlySpan<char> MetersLabelChars => DefaultMetersLabel.AsSpan();
        private static ReadOnlySpan<char> FeetLabelChars => DefaultFeetLabel.AsSpan();
        private static ReadOnlySpan<char> CelsiusLabelChars => DefaultCelsiusLabel.AsSpan();
        private static ReadOnlySpan<char> FahrenheitLabelChars => DefaultFahrenheitLabel.AsSpan();
        private static ReadOnlySpan<char> GaugeO2LabelChars => DefaultGaugeO2Label.AsSpan();
        private static ReadOnlySpan<char> GaugePowerLabelChars => DefaultGaugePowerLabel.AsSpan();
        private static ReadOnlySpan<char> GaugeHullLabelChars => DefaultGaugeHullLabel.AsSpan();
        private static ReadOnlySpan<char> QuickbarSlotOneChars => "1".AsSpan();
        private static ReadOnlySpan<char> QuickbarSlotTwoChars => "2".AsSpan();
        private static ReadOnlySpan<char> QuickbarSlotThreeChars => "3".AsSpan();
        private static ReadOnlySpan<char> QuickbarSlotFourChars => "4".AsSpan();
        private static ReadOnlySpan<char> StatusPressureLimitExceededChars => DefaultStatusPressureLimitExceeded.AsSpan();
        private static ReadOnlySpan<char> StatusApproachingSafeDepthChars => DefaultStatusApproachingSafeDepth.AsSpan();
        private static ReadOnlySpan<char> StatusSuitDamageCriticalChars => DefaultStatusSuitDamageCritical.AsSpan();
        private static ReadOnlySpan<char> StatusOxygenReserveLowChars => DefaultStatusOxygenReserveLow.AsSpan();
        private static ReadOnlySpan<char> StatusPowerCellsLowChars => DefaultStatusPowerCellsLow.AsSpan();
        private static ReadOnlySpan<char> StatusLampThermalLimitChars => DefaultStatusLampThermalLimit.AsSpan();
        private static ReadOnlySpan<char> StatusSuitLinkRoutingPdaChars => DefaultStatusSuitLinkRoutingPda.AsSpan();
        private static ReadOnlySpan<char> StatusLifeSupportNominalStableChars => DefaultStatusLifeSupportNominalStable.AsSpan();
        private static ReadOnlySpan<char> StatusLifeSupportNominalAscendingChars => DefaultStatusLifeSupportNominalAscending.AsSpan();
        private static ReadOnlySpan<char> StatusLifeSupportNominalDescendingChars => DefaultStatusLifeSupportNominalDescending.AsSpan();
        private static ReadOnlySpan<char> StatusOptimizingCoreSystemsChars => DefaultStatusOptimizingCoreSystems.AsSpan();
        private static ReadOnlySpan<char> LoadPrefixChars => "MASS: ".AsSpan();
        private static ReadOnlySpan<char> LoadKgSuffixChars => " KG".AsSpan();
        private static ReadOnlySpan<char> MemorySubsystemBreachTemplateChars => "MEMORY SUBSYSTEM BREACH 0x00000000".AsSpan();
        private static readonly char[] s_memorySubsystemBreachBuffer = new char[MemorySubsystemBreachTextLength]; // COLD ALLOC: char[34] - mutable memory breach warning buffer - owner: SuitHUDV4CanvasOverlay
        private static double s_memorySubsystemBreachUntilTime;
        private static uint s_memorySubsystemBreachCode;
        private static int s_memorySubsystemBreachVersion;
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

            UiLayerIndex = HectonLayerMasks.UI;
            s_layerCacheInitialized = true;
        }

        public static void TriggerMemorySubsystemBreach(uint errorCode)
        {
            uint resolvedCode = errorCode != 0u ? errorCode : 0x4D454D30u;
            s_memorySubsystemBreachCode = resolvedCode;
            WriteMemorySubsystemBreachHex(resolvedCode);
            s_memorySubsystemBreachUntilTime = SystemDispatcher.CurrentUnscaledTimeSeconds + MemorySubsystemBreachHoldSeconds;
            unchecked
            {
                s_memorySubsystemBreachVersion++;
            }
        }

        public static void SetStencilRenderGraphRuntimeActive(bool active)
        {
            if (s_stencilRenderGraphRuntimeActive == active)
                return;

            s_stencilRenderGraphRuntimeActive = active;
            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = GetActiveOverlay(i);
                if (overlay == null)
                    continue;

                if (active)
                    overlay.ApplyStencilRenderGraphSuppressionIfNeeded();
                else
                    overlay.ReleaseStencilRenderGraphSuppressionIfNeeded();
            }
        }

        public static bool IsStencilRenderGraphRuntimeActive()
        {
            return s_stencilRenderGraphRuntimeActive;
        }

        private static bool TryResolveMemorySubsystemBreach(out char[] buffer, out int length, out int version)
        {
            version = s_memorySubsystemBreachVersion;
            if (s_memorySubsystemBreachCode == 0u || SystemDispatcher.CurrentUnscaledTimeSeconds > s_memorySubsystemBreachUntilTime)
            {
                buffer = null;
                length = 0;
                return false;
            }

            buffer = s_memorySubsystemBreachBuffer;
            length = s_memorySubsystemBreachBuffer.Length;
            return true;
        }

        private static void WriteMemorySubsystemBreachHex(uint value)
        {
            const string HexDigits = "0123456789ABCDEF";
            CopySpanToBuffer(MemorySubsystemBreachTemplateChars, s_memorySubsystemBreachBuffer);
            int cursor = s_memorySubsystemBreachBuffer.Length - 8;
            for (int digit = 0; digit < 8; digit++)
            {
                int shift = 28 - (digit * 4);
                s_memorySubsystemBreachBuffer[cursor + digit] = HexDigits[(int)((value >> shift) & 0xFu)];
            }
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

        private static bool IsScreenOverlayAllowed()
        {
            return false;
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

                return ZeroGCFormatter.TryWriteInt(value, _buffer.AsSpan(), out length);
            }

            public bool TryWriteTemplateInt(ReadOnlySpan<char> template, int value, out int length)
            {
                if (_buffer == null)
                {
                    length = 0;
                    return false;
                }

                return ZeroGCFormatter.TryWriteMetricTemplateInt(template, value, _buffer.AsSpan(), out length);
            }

            public bool TryWriteTemplateFloatTenths(ReadOnlySpan<char> template, int roundedTenths, out int length)
            {
                if (_buffer == null)
                {
                    length = 0;
                    return false;
                }

                return ZeroGCFormatter.TryWriteMetricTemplateFloatTenths(template, roundedTenths, _buffer.AsSpan(), out length);
            }
        }

        private const int LayoutRevision = 13;
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
        [SerializeField] private RenderPath renderPath = RenderPath.ProjectionSource;
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

        [Header("Proxy Light")]
        [SerializeField]
        [Tooltip("Registers the diegetic HUD projection as a deferred proxy light when the canvas is world-space.")]
        private bool enableHudProxyLight = true;
        [SerializeField, Min(0.01f)]
        [Tooltip("Proxy-light radius emitted by the projected HUD in meters.")]
        private float hudProxyLightRangeMeters = 1.05f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base proxy-light intensity before power flicker and oxygen stress modulation.")]
        private float hudProxyLightIntensity = 0.18f;
        [SerializeField, Range(0f, 0.75f)]
        [Tooltip("Maximum intensity boost applied as oxygen approaches zero.")]
        private float hudProxyLightOxygenStressBoost = 0.28f;
        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Maximum power-light reduction applied by the stress pulse flicker.")]
        private float hudProxyLightStressFlicker = 0.22f;
        [SerializeField, Min(0f)]
        [Tooltip("Forward offset from the projection canvas used to place the proxy light above the glass.")]
        private float hudProxyLightForwardOffsetMeters = 0.03f;
        [SerializeField]
        [Tooltip("Normal HUD proxy-light color before oxygen stress warning tint is blended in.")]
        private Color hudProxyLightColor = new Color(0.08f, 0.88f, 1f, 1f);
        [SerializeField]
        [Tooltip("Warning HUD proxy-light color reached as oxygen stress approaches one.")]
        private Color hudProxyLightStressColor = new Color(1f, 0.18f, 0.12f, 1f);

        [Header("Layout Controls")]
        [SerializeField] private Vector2 headerOffset = new Vector2(0f, -34f);
        [SerializeField] private Vector2 telemetryOffset = new Vector2(-226f, 126f);
        [SerializeField] private Vector2 telemetrySize = new Vector2(184f, 124f);
        [SerializeField] private Vector2 gaugeClusterOffset = new Vector2(116f, 110f);
        [SerializeField] private Vector2 gaugeClusterSize = new Vector2(300f, 128f);
        [SerializeField] private Vector2 statusOffset = new Vector2(0f, 50f);
        [SerializeField] private Vector2 reticleOffset = Vector2.zero;

        [Header("Reticle Dynamics")]
        [SerializeField, Range(0f, 24f)] private float reticleBaseSpread = 16f;
        [SerializeField, Range(0f, 8f)] private float reticleVelocityFactor = 0.1f;
        [SerializeField, Range(0f, 24f)] private float reticleHeatSpread = 2f;
        [SerializeField, Range(0.25f, 24f)] private float reticleSpreadBlendSpeed = 8f;
        [SerializeField, Range(8f, 36f)] private float reticleLineLength = 22f;
        [SerializeField, Range(1f, 6f)] private float reticleLineThickness = 2f;
        [SerializeField, Range(4f, 24f)] private float reticleBracketLength = 10f;
        private const float ReticleSquaredSpeedSpreadScale = 0.125f;

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
        [Tooltip("Authored UI material used to visualize the 360-bin passive acoustic ring on the visor edges.")]
        private Material acousticRadarMaterial;
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

        [Header("Dithered UI Backgrounds")]
        [SerializeField]
        [Tooltip("Authored alpha-clip IGN material used for HUD background panels that must avoid alpha blending.")]
        private Material ditheredUiBackgroundMaterial;
        [SerializeField]
        [Tooltip("Authored pulse material used by the diegetic save/data-recording lamp.")]
        private Material dataRecPulseMaterial;

        [Header("Threat AR")]
        [SerializeField]
        [Tooltip("Authored instanced material used by diegetic threat chevrons on the visor plane.")]
        private Material threatChevronMaterial;
        [SerializeField]
        [Tooltip("Offline-baked chevron mesh authored in the editor. Runtime mesh generation is forbidden for this HUD path.")]
        private Mesh _threatChevronStaticMesh;
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

        [Header("Scanner Hologram")]
        [SerializeField]
        [Tooltip("Canvas offset for the scanner hologram fake.")]
        private Vector2 scannerHologramOffsetPixels = new Vector2(0f, -56f);
        [SerializeField]
        [Tooltip("Canvas size for the flat scanner hologram fake.")]
        private Vector2 scannerHologramSizePixels = new Vector2(92f, 56f);
        [SerializeField, Min(0f)]
        [Tooltip("Pulse size added to the flat scanner hologram fake.")]
        private float scannerHologramPulsePixels = 5f;
        [SerializeField, Min(0f)]
        [Tooltip("Low-amplitude screen-space signal wobble for the flat scanner hologram fake.")]
        private float scannerHologramJitterPixels = 2.5f;
        [SerializeField]
        [Tooltip("Scan-start hologram tint.")]
        private Color scannerHologramScanStartColor = new Color(0.08f, 0.88f, 1f, 0.44f);
        [SerializeField]
        [Tooltip("Scan-complete hologram tint.")]
        private Color scannerHologramScanCompleteColor = new Color(0.14f, 1f, 0.46f, 0.52f);

        private const string RootName = "HUD_V4_CanvasRoot";
        private const string ActionProgressHudName = "ActionProgressHUD";

        private RectTransform _root;
        private RectTransform _ornamentRoot;
        private RectTransform _actionProgressHudRoot;
        private CanvasGroup _rootCanvasGroup;
        private RectMask2D _rootScissorMask;
        private CanvasGroup _ornamentCanvasGroup;
        private RectTransform _headerRoot;
        private RectTransform _reticleRoot;
        private RectTransform _telemetryChromeRoot;
        private RectTransform _telemetrySupplementRoot;
        private RectTransform _telemetryRoot;
        private RectTransform _gaugeClusterRoot;
        private RectTransform _quickbarRoot;
        private RectTransform _savingProgressRoot;
        private RectTransform _savingProgressIconRoot;
        private RectTransform _scannerFlatHologramRoot;
        private CanvasGroup _headerCanvasGroup;
        private CanvasGroup _telemetryChromeCanvasGroup;
        private CanvasGroup _telemetrySupplementCanvasGroup;
        private CanvasGroup _statusCanvasGroup;
        private CanvasGroup _quickbarCanvasGroup;
        private CanvasGroup _savingProgressCanvasGroup;
        private CanvasGroup _scannerFlatHologramCanvasGroup;
        private ActionProgressHUD _actionProgressHud;
        private Image _biosBackdrop;
        private AcousticRadarRawImage _acousticRadarOverlay;
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
        private Image _savingProgressDiskBody;
        private Image _savingProgressDiskNotch;
        private Image _savingProgressDiskLabel;
        private Image _savingProgressDataNeedle;
        private Image _savingProgressDataLamp;
        private Image _scannerFlatHologramBody;
        private Image _scannerFlatHologramScanline;
        private Image _scannerFlatHologramCore;

        private TextMeshProUGUI _suitLabel;
        private TextMeshProUGUI _headingLabel;
        private TextMeshProUGUI _depthLabel;
        private TextMeshProUGUI _temperatureLabel;
        private TextMeshProUGUI _pressureLabel;
        private TextMeshProUGUI _loadLabel;
        private TextMeshProUGUI _statusLabel;

        private GaugeRefs _oxygenGauge;
        private GaugeRefs _powerGauge;
        private GaugeRefs _healthGauge;
        private readonly QuickbarSlotRefs[] _quickbarSlots = new QuickbarSlotRefs[QuickbarSlotCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearActiveOverlays();
            s_memorySubsystemBreachUntilTime = 0d;
            s_memorySubsystemBreachCode = 0u;
            s_memorySubsystemBreachVersion = 0;
            s_stencilRenderGraphRuntimeActive = false;
            s_saveFailureNotificationBridge = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeHudCanvasBindings()
        {
            if (!Application.isPlaying || s_stencilRenderGraphRuntimeActive)
                return;

            Canvas canvas = FindSceneCanvasByName(PrimaryHudCanvasName);
            if (canvas == null)
                return;

// Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            if (!canvas.TryGetComponent(out HectonUIScaler _))
                // COLD ALLOC: HectonUIScaler[1] - primary HUD canvas bootstrap binding - owner: SuitHUDV4CanvasOverlay
                canvas.gameObject.AddComponent<HectonUIScaler>();

            if (!canvas.TryGetComponent(out SuitHUDV4CanvasOverlay _))
                // COLD ALLOC: SuitHUDV4CanvasOverlay[1] - primary HUD canvas bootstrap binding - owner: SuitHUDV4CanvasOverlay
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
        private float _targetPower01 = 1f;
        private float _reticleSpreadPixels;
        private float _appliedReticleSpreadPixels = float.NaN;
        private float _appliedReticleLineLength = float.NaN;
        private float _appliedReticleLineThickness = float.NaN;
        private float _appliedReticleBracketLength = float.NaN;
        private float _lastDepth;
        private float _depthMeters;
        private float _lastStreamedOxygen01 = float.NaN;
        private float _lastStreamedPower01 = float.NaN;
        private float _lastStreamedHealth01 = float.NaN;
        private float _lastStreamedDepthMeters = float.NaN;
        private float _lastStreamedTemperature = float.NaN;
        private float _lastStreamedPressure = float.NaN;
        private float _lastStreamedHeadingDegrees = float.NaN;
        private uint _lastHapticOxygenVersion;
        private uint _lastHapticPowerVersion;
        private uint _lastHapticHealthVersion;
        private float _nextCriticalHapticTime;
        private float _nextSavingProgressHapticTime;
        private byte _activeCriticalHapticMask;
        private bool _quickbarVisualsInitialized;
        private bool _layoutBuilt;
        [SerializeField, HideInInspector] private int _appliedLayoutRevision;
        private float _nextAutoResolveAt;
        private bool _slowTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _pendingRuntimeCanvasRefresh = true;
        private bool _forceResolveOnSlowTick = true;
        private bool _pendingDepthSignalRefresh = true;
        private bool _pendingStencilSuppressionApply;
        private bool _reactiveLateFrameSolveRequested;
        private float _queuedReactiveDeltaTime;
        private readonly char[] _oxygenGaugeValueBuffer = new char[ZeroGCFormatter.HudMetricBufferCapacity]; // COLD ALLOC: char[64] - O2 gauge numeric buffer - owner: SuitHUDV4CanvasOverlay
        private readonly char[] _healthGaugeValueBuffer = new char[ZeroGCFormatter.HudMetricBufferCapacity]; // COLD ALLOC: char[64] - health gauge numeric buffer - owner: SuitHUDV4CanvasOverlay
        private readonly char[] _powerGaugeValueBuffer = new char[ZeroGCFormatter.HudMetricBufferCapacity]; // COLD ALLOC: char[64] - power gauge numeric buffer - owner: SuitHUDV4CanvasOverlay
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
        private int _systemHealthWarningFrame = int.MinValue;
        private byte _systemHealthPressureLevel;
        private bool _systemHealthWarningActive;
        private bool _systemHealthSignalDirty;
        private bool _inventorySignalDirty = true;
        private bool _playerStateSignalDirty;
        private bool _inputStateSignalDirty;
        private bool _localizedPresentationDirty;
        private int _reactiveUiCadenceStride = 1;
        private float _qualityWeight01 = 1f;
        private uint _lastInventorySignalRevision;
        private uint _lastInputSchemeHash;
        private byte _playerStateFlags;
        private byte _playerStateState;
        private float _playerStateIntensity01;
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
        private int _appliedLoadVersion = int.MinValue;
        private Color _appliedLoadColor;
        private int _loadMassCharStart = -1;
        private int _loadMassCharLength;
        private float _loadMassPulsePhase;
        private bool _hasAppliedLoadMassVertexColor;
        private bool _loadMassVertexRefreshDeferred;
        private Color32 _appliedLoadMassVertexColor;
        private bool _styleApplied;
        private bool _canvasStateApplied;
        private bool _hasAppliedRootVisibility;
        private bool _appliedRootVisible;
        private float _stressPulseIntensity;
        private float _stressPulsePhase;
        private float _appliedStressPulseStrength = -1f;
        private float _appliedAnalogUiJitterStrength = -1f;
        private int _appliedAnalogUiJitterBucket = int.MinValue;
        private float _traumaGlitchIntensity;
        private float _traumaRecoilScalar;
        private float _traumaTransportPower01 = 1f;
        private float _traumaHullIntegrity01 = 1f;
        private float _toolDepletedWarningTimer;
        private float _savingProgressAlpha;
        private float _savingProgressTargetAlpha;
        private float _savingProgressHideNotBeforeTime;
        // One read cursor per SaveEvents status bucket. Fixed scalars rather than a uint[4] to match this class's
        // fixed-slot convention and keep the per-tick poll free of an array bounds check.
        private uint _savingProgressStatusSequenceSlot0;
        private uint _savingProgressStatusSequenceSlot1;
        private uint _savingProgressStatusSequenceSlot2;
        private uint _savingProgressStatusSequenceUnknownSlot;
        private float _threatChevronPulseTime;
        private float _scannerInterferencePhase;
        private float _jitterTime;
        private int _nextHudPerformanceWarningFrame;
        private int _toolDepletedVersion;
        private int _toolDepletedHashId;
        private int _corruptionFrameVersion;
        private bool _savingProgressHidePending;
        private bool _savingProgressFailureLatched;
        private bool _savingProgressFailureBlinkOn;
        private bool _appliedSavingProgressFailureLatched;
        private bool _appliedSavingProgressFailureBlinkOn;
        private bool _hasAppliedSavingProgressFailureStyle;
        private bool _scannerInterferenceActive;
        private bool _biosRecoveryMode;
        private Transform _defaultCanvasParent;
        private int _defaultCanvasSiblingIndex = -1;
        private bool _rootBaseAnchoredPositionCaptured;
        private Vector2 _rootBaseAnchoredPosition;
        private GameLanguage _localizedMeasurementLanguage = GameLanguage.English;
        // COLD ALLOC: char[256] - cached suit label staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _cachedSuitLabelBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private int _cachedSuitLabelLength;
        // COLD ALLOC: char[256] - localized depth metric template buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _depthTemplateBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private int _depthTemplateLength;
        // COLD ALLOC: char[256] - localized temperature metric template buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _temperatureTemplateBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private int _temperatureTemplateLength;
        // COLD ALLOC: char[256] - localized pressure metric template buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _pressureTemplateBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private int _pressureTemplateLength;
        // COLD ALLOC: char[64] - depth meter display staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _depthDisplayBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[64] - temperature meter display staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _temperatureDisplayBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[64] - pressure meter display staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _pressureDisplayBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[64] - compass ribbon display staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _headingDisplayBuffer = new char[ZeroGCFormatter.HudMetricBufferCapacity];
        // COLD ALLOC: char[256] - LOAD telemetry fallback staging buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _loadDisplayFallbackBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        // COLD ALLOC: char[256] - caller-owned HUD glitch scratch buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _glitchScratchBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private IDataVault _glitchVault;
        private VaultGenerationHandle<byte> _glitchTableHandle;
        private bool _glitchTableHandleReady;
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
        private Color _appliedBiosBackdropColor;
        private bool _hasAppliedBiosBackdropColor;
        private CanvasScaler _cachedCanvasScaler;
        private GraphicRaycaster _cachedGraphicRaycaster;
        private Canvas _cachedGraphicRaycasterCanvas;
        private HectonUIScaler _cachedUiScaler;
        private HectonSurvivalSystem _depthSignalSource;
        private uint _depthSignalSourceId;
        private uint _lastDepthSignalSequence;
        private IPlayerInventoryService _inventoryService;
        private PlayerInventory _playerInventory;
        private ItemCatalog _itemCatalog;
        private PlayerToolManager _toolManager;
        private Hecton8.Core.IAudioService _spatialAudioManager;
        private ILocalizationStressPresentationReadModel _localizationRuntime;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private int _lastInventoryVersion = -1;
        private readonly int[] _quickbarSlotHashCache = new int[QuickbarSlotCount];
        private readonly bool[] _quickbarSlotHashResolved = new bool[QuickbarSlotCount];
        private readonly GameObject[] _quickbarSlotPrefabCache = new GameObject[QuickbarSlotCount];
        private int _cachedHullStressWhisperBucket = int.MinValue;
        private bool _cachedHullStressWhisperRtl;
        // COLD ALLOC: char[256] - cached hull-stress whisper text buffer - owner: SuitHUDV4CanvasOverlay
        private char[] _cachedHullStressWhisperBuffer = new char[CharBufferPool.RequiredVrTextCapacity];
        private int _cachedHullStressWhisperLength;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private TMP_FontAsset _cachedFontMaterialAsset0;
        private TMP_FontAsset _cachedFontMaterialAsset1;
        private Material _cachedFontSharedMaterial0;
        private Material _cachedFontSharedMaterial1;
        private bool _acousticRadarOverlayMaterialBound;
        private bool _savingProgressDataLampPulseMaterialBound;
        private bool _savingProgressDataNeedlePulseMaterialBound;
        private Texture2D _acousticRadarTexture;
        private int _acousticRadarResolution;
        private int _pendingAcousticRadarResolution;
        private float _acousticRadarPeakIntensity;
        private bool _pendingAcousticRadarTextureRefresh;
        private bool _acousticRadarTextureBindingDirty = true;
        private bool _hasAppliedAcousticRadarOverlayColor;
        private Color _appliedAcousticRadarOverlayColor;
        private Mesh _threatChevronMesh;
        // COLD ALLOC: Matrix4x4[4] - instanced threat-chevron draw mirror - owner: SuitHUDV4CanvasOverlay
        private readonly Matrix4x4[] _threatChevronMatrixMirror = new Matrix4x4[MaxThreatChevronCount];
        // COLD ALLOC: float[4] - per-chevron alpha cache for alpha-faded threat warnings - owner: SuitHUDV4CanvasOverlay
        private readonly float[] _threatChevronAlphaMirror = new float[MaxThreatChevronCount];
        // COLD ALLOC: Vector4[4] - per-chevron instanced alpha payload - owner: SuitHUDV4CanvasOverlay
        private readonly Vector4[] _threatChevronInstanceDataMirror = new Vector4[MaxThreatChevronCount];
        // COLD ALLOC: ThreatChevronState[4] - cached top threat-grid chevron slots - owner: SuitHUDV4CanvasOverlay
        private readonly ThreatChevronState[] _threatChevronStates = new ThreatChevronState[MaxThreatChevronCount];
        private uint _threatChevronActiveMask;
        private MaterialPropertyBlock _threatChevronPropertyBlock;
        private bool _hasAppliedThreatChevronColor;
        private bool _hasAppliedThreatChevronFlickerFrequency;
        private bool _hasAppliedThreatChevronFlickerIntensity;
        private bool _hasAppliedThreatChevronFillAlpha;
        private Color _appliedThreatChevronColor;
        private float _appliedThreatChevronFlickerFrequency;
        private float _appliedThreatChevronFlickerIntensity;
        private float _appliedThreatChevronFillAlpha;
        private int _threatChevronVisibleCount;
        private float _scannerHologramAnimationTime;

        public Canvas TargetCanvas => targetCanvas;
        public Camera ProjectionCamera => projectionCamera;
        internal static SuitHUDV4CanvasOverlay ActiveRuntimeInstance { get; private set; }

        internal static int ActiveOverlayCount => s_activeOverlayCount;

        internal static bool TryResolveActiveRuntime(ref SuitHUDV4CanvasOverlay target)
        {
            SuitHUDV4CanvasOverlay active = ActiveRuntimeInstance;
            if (active == null || !active.isActiveAndEnabled)
            {
                target = null;
                return false;
            }

            if (!ReferenceEquals(target, active))
                target = active;

            return true;
        }

        public static void CopyActiveOverlaysTo(System.Collections.Generic.List<SuitHUDV4CanvasOverlay> results)
        {
            if (results == null)
                return;

            results.Clear();
            int resultCapacity = results.Capacity;
            if (resultCapacity <= 0)
                return;

            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = GetActiveOverlay(i);
                if (overlay == null || !overlay.isActiveAndEnabled)
                    continue;

                if (results.Count >= resultCapacity)
                    return;

                results.Add(overlay);
            }
        }

        private struct GaugeRefs
        {
            public RectTransform Root;
            public CanvasGroup CanvasGroup;
            public CanvasGroup SubCanvasGroup;
            public RawImage Icon;
            public GaugeRingGraphic RingBack;
            public GaugeRingGraphic RingFill;
            public GaugeRingGraphic RingFrame;
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

        private sealed class GaugeRingGraphic : MaskableGraphic
        {
            private const int SegmentCapacity = 48;
            private const float TwoPi = Mathf.PI * 2f;
            private const float OctantDiagonal = 0.70710677f;
            private const float RingThicknessScale = 0.18f;
            private const float FrameThicknessScale = 0.055f;
            private float _fillAmount = 1f;
            private float _ringThicknessScale = RingThicknessScale;
            private float _frameThicknessScale = FrameThicknessScale;
            private bool _outlineOnly;

            public void Configure(bool outlineOnly)
            {
                Configure(outlineOnly, RingThicknessScale, FrameThicknessScale);
            }

            public void Configure(bool outlineOnly, float ringThicknessScale, float frameThicknessScale)
            {
                float resolvedRingThicknessScale = math.clamp(ringThicknessScale, 0.02f, 0.45f);
                float resolvedFrameThicknessScale = math.clamp(frameThicknessScale, 0.01f, 0.24f);
                if (_outlineOnly == outlineOnly &&
                    math.abs(_ringThicknessScale - resolvedRingThicknessScale) <= 0.0001f &&
                    math.abs(_frameThicknessScale - resolvedFrameThicknessScale) <= 0.0001f)
                {
                    return;
                }

                _outlineOnly = outlineOnly;
                _ringThicknessScale = resolvedRingThicknessScale;
                _frameThicknessScale = resolvedFrameThicknessScale;
                SetVerticesDirty();
            }

            public void SetFillAmount(float value)
            {
                float clamped = math.saturate(value);
                if (math.abs(_fillAmount - clamped) <= FillWriteEpsilon)
                    return;

                _fillAmount = clamped;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vertexHelper)
            {
                vertexHelper.Clear();

                Rect rect = rectTransform.rect;
                float outerRadius = math.min(math.abs(rect.width), math.abs(rect.height)) * 0.5f;
                if (outerRadius <= 0.001f)
                    return;

                float thickness = outerRadius * (_outlineOnly ? _frameThicknessScale : _ringThicknessScale);
                float innerRadius = math.max(0.001f, outerRadius - thickness);
                float fillAmount = _outlineOnly ? 1f : _fillAmount;
                int segmentCount = math.clamp(CeilPositiveToInt(SegmentCapacity * fillAmount), 1, SegmentCapacity);
                float startAngle = Mathf.PI * 0.5f;
                float radiansPerSegment = TwoPi / SegmentCapacity;
                Color32 vertexColor = color;

                for (int i = 0; i < segmentCount; i++)
                {
                    float angleA = startAngle - radiansPerSegment * i;
                    float angleB = startAngle - radiansPerSegment * math.min(i + 1, SegmentCapacity * fillAmount);
                    AddRingSegment(vertexHelper, angleA, angleB, outerRadius, innerRadius, vertexColor);
                }
            }

            private static void AddRingSegment(
                VertexHelper vertexHelper,
                float angleA,
                float angleB,
                float outerRadius,
                float innerRadius,
                Color32 vertexColor)
            {
                int startIndex = vertexHelper.currentVertCount;
                Vector2 outerA = ResolveOctantRingPoint(angleA, outerRadius);
                Vector2 outerB = ResolveOctantRingPoint(angleB, outerRadius);
                Vector2 innerA = ResolveOctantRingPoint(angleA, innerRadius);
                Vector2 innerB = ResolveOctantRingPoint(angleB, innerRadius);

                UIVertex vertex = UIVertex.simpleVert;
                vertex.color = vertexColor;
                vertex.uv0 = Vector2.zero;
                vertex.position = innerA;
                vertexHelper.AddVert(vertex);
                vertex.position = outerA;
                vertexHelper.AddVert(vertex);
                vertex.position = outerB;
                vertexHelper.AddVert(vertex);
                vertex.position = innerB;
                vertexHelper.AddVert(vertex);

                vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
                vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
            }

            private static Vector2 ResolveOctantRingPoint(float radians, float radius)
            {
                float octantFloat = math.frac(radians * InvTwoPi) * 8f;
                int octant = (int)math.floor(octantFloat);
                float t = octantFloat - octant;
                float2 a;
                float2 b;

                switch (octant & 7)
                {
                    case 0:
                        a = new float2(1f, 0f);
                        b = new float2(OctantDiagonal, OctantDiagonal);
                        break;
                    case 1:
                        a = new float2(OctantDiagonal, OctantDiagonal);
                        b = new float2(0f, 1f);
                        break;
                    case 2:
                        a = new float2(0f, 1f);
                        b = new float2(-OctantDiagonal, OctantDiagonal);
                        break;
                    case 3:
                        a = new float2(-OctantDiagonal, OctantDiagonal);
                        b = new float2(-1f, 0f);
                        break;
                    case 4:
                        a = new float2(-1f, 0f);
                        b = new float2(-OctantDiagonal, -OctantDiagonal);
                        break;
                    case 5:
                        a = new float2(-OctantDiagonal, -OctantDiagonal);
                        b = new float2(0f, -1f);
                        break;
                    case 6:
                        a = new float2(0f, -1f);
                        b = new float2(OctantDiagonal, -OctantDiagonal);
                        break;
                    default:
                        a = new float2(OctantDiagonal, -OctantDiagonal);
                        b = new float2(1f, 0f);
                        break;
                }

                float2 point = math.lerp(a, b, t);
                return new Vector2(point.x * radius, point.y * radius);
            }
        }

        private sealed class AcousticRadarRawImage : RawImage
        {
            private Vector4 _tuning0;
            private Vector4 _tuning1;
            private bool _hasTuning;

            public void SetTuning(Vector4 tuning0, Vector4 tuning1)
            {
                if (_hasTuning &&
                    _tuning0 == tuning0 &&
                    _tuning1 == tuning1)
                {
                    return;
                }

                _tuning0 = tuning0;
                _tuning1 = tuning1;
                _hasTuning = true;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vertexHelper)
            {
                base.OnPopulateMesh(vertexHelper);
                int vertexCount = vertexHelper.currentVertCount;
                if (vertexCount <= 0)
                    return;

                UIVertex vertex = default;
                for (int i = 0; i < vertexCount; i++)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    vertex.uv1 = _tuning0;
                    vertex.uv2 = _tuning1;
                    vertexHelper.SetUIVertex(vertex, i);
                }
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ThreatChevronState
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)]
            public float Threat01;
        }

        private enum DynamicCanvasCadenceBucket : byte
        {
            Static = 0,
            LowCadence = 1,
            HighCadence = 2
        }

        public bool IsInitialized => !IsStencilRenderGraphSuppressedRuntime() && isActiveAndEnabled && targetCanvas != null && _root != null && _layoutBuilt;

        private bool _ownsGlobalUiSlot;
        private bool _hotSwapRegistered;
        private bool _floatingOriginRegistered;
        private int _hudProxyLightKey;
        private bool _hudProxyLightRegistered;
        private bool _runtimeHudCallbacksActive;

        private static bool IsStencilRenderGraphSuppressedRuntime()
        {
            return SystemDispatcher.ActiveRuntimeInstance != null && s_stencilRenderGraphRuntimeActive;
        }

        private void ApplyStencilRenderGraphSuppressionIfNeeded()
        {
            if (!IsStencilRenderGraphSuppressedRuntime())
                return;

            _pendingRuntimeCanvasRefresh = false;
            _forceResolveOnSlowTick = false;
            _pendingDepthSignalRefresh = false;
            _layoutBuilt = false;
            _canvasStateApplied = false;
            UnregisterRuntimeTick();
            UnregisterUiService();
            UnregisterHudProxyLight();
            ClearDepthSignalSubscription();

            Canvas canvas = targetCanvas != null ? targetCanvas : ResolveTargetCanvas();
            if (canvas != null && canvas.enabled)
                canvas.enabled = false;

            ResolveGraphicRaycasterCold();
            if (_cachedGraphicRaycaster != null && _cachedGraphicRaycaster.enabled)
                _cachedGraphicRaycaster.enabled = false;

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
                _appliedRootVisible = false;
                _hasAppliedRootVisibility = true;
            }
        }

        private void ReleaseStencilRenderGraphSuppressionIfNeeded()
        {
            if (IsStencilRenderGraphSuppressedRuntime() || !Application.isPlaying || !isActiveAndEnabled)
                return;

            Canvas canvas = targetCanvas != null ? targetCanvas : ResolveTargetCanvas();
            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;

            ResolveGraphicRaycasterCold();
            if (_cachedGraphicRaycaster != null && !_cachedGraphicRaycaster.enabled)
                _cachedGraphicRaycaster.enabled = true;

            CacheRuntimeDependenciesCold();
            CacheGlitchTableVaultCold();
            TryRegisterUiService();
            TryRegisterHotSwapListener();
            TryRegisterFloatingOriginListener();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
            TryRegisterRuntimeTick();
        }

        private void Awake()
        {
            _runtimeHudCallbacksActive = Application.isPlaying;
            CharBufferPool.BindDataVaultCold(GlobalRegistry.DataVault);
            CharBufferPool.Prewarm();
            _hudProxyLightKey = unchecked((int)(EntityId.ToULong(gameObject.GetEntityId()) ^ 0x48445544u));
            if (_hudProxyLightKey == 0)
                _hudProxyLightKey = 0x48445544;

            ResolveGraphicRaycasterCold();
        }

        private void OnEnable()
        {
            _runtimeHudCallbacksActive = Application.isPlaying;
            EnsureLayerCache();
            ResolveGraphicRaycasterCold();
            RegisterActiveOverlay();
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            CacheRuntimeDependenciesCold();
            CacheGlitchTableVaultCold();
            LocalizationEvents.RegisterLanguageListener(this);
            LocalizationEvents.RegisterCorruptionVisualStateListener(this);
            GameBootstrapper.Register(this);
            PlayerSignalEvents.Register(this);
            if (Application.isPlaying)
            {
                SaveEvents.Register(this);
                EnsureSaveFailureNotificationBridgeCold();
            }

            _layoutBuilt = false;
            InvalidateVisualCaches();
            RebuildLocalizationCache();
            HideIncompleteRootImmediately();

            if (!Application.isPlaying)
            {
                if (keepVisibleInEditMode)
                {
                    NormalizeCanvas();
                    EnsureHierarchy();
                    RefreshVisuals(0f, refreshMediumCadence: true, refreshSlowCadence: true);
                }

                return;
            }

            TryRegisterFloatingOriginListener();
            TryRegisterUiService();
            TryRegisterHotSwapListener();
            ToolHapticsRuntime.EnsureRuntimeInstance();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
            TryRegisterRuntimeTick();
            EnsureAcousticRadarRuntimeResources();
            EnsureSavingProgressPulseRuntimeResources();
            EnsureThreatChevronRuntimeResources();
            ProcessPendingRuntimeCanvasRefresh();
            RefreshQuickbarSlotHashCacheCold();
        }

        private void Start()
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            TryRegisterRuntimeTick();
            ProcessPendingRuntimeCanvasRefresh();
            RefreshQuickbarSlotHashCacheCold();
        }

        private void OnDisable()
        {
            _runtimeHudCallbacksActive = false;
            LocalizationEvents.UnregisterLanguageListener(this);
            LocalizationEvents.UnregisterCorruptionVisualStateListener(this);
            GameBootstrapper.Unregister(this);
            PlayerSignalEvents.Unregister(this);
            SaveEvents.Unregister(this);
            TryUnregisterFloatingOriginListener();
            TryUnregisterHotSwapListener();
            UnregisterUiService();
            UnregisterActiveOverlay();
            UnregisterRuntimeTick();
            UnregisterHudProxyLight();
            ClearDepthSignalSubscription();
            _stressPulseIntensity = 0f;
            _stressPulsePhase = 0f;
            _appliedStressPulseStrength = -1f;
            _appliedAnalogUiJitterStrength = -1f;
            _appliedAnalogUiJitterBucket = int.MinValue;
            _toolDepletedWarningTimer = 0f;
            _savingProgressTargetAlpha = 0f;
            _savingProgressAlpha = 0f;
            _savingProgressHideNotBeforeTime = 0f;
            _savingProgressHidePending = false;
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
            DisposeSavingProgressPulseRuntimeResources();
            DisposeThreatChevronRuntimeResources();
            DisposeScannerHologramRuntimeResources();
            ClearGlitchTableBinding();

            SetRootVisible(false);
        }

        private void OnDestroy()
        {
            _runtimeHudCallbacksActive = false;
            SaveEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            UnregisterUiService();
            UnregisterHudProxyLight();
            DisposeAcousticRadarRuntimeResources();
            DisposeSavingProgressPulseRuntimeResources();
            DisposeThreatChevronRuntimeResources();
            DisposeScannerHologramRuntimeResources();
            ClearGlitchTableBinding();
        }

        private void HideIncompleteRootImmediately()
        {
            RectTransform root = _root;
            if (root == null)
            {
                Canvas resolvedCanvas = targetCanvas != null ? targetCanvas : ResolveTargetCanvas();
                RectTransform parentRoot = ResolveUiParent(resolvedCanvas);
                if (parentRoot != null)
                    root = FindChildRect(parentRoot, RootName);

                if (root == null && resolvedCanvas != null)
                    root = FindChildRect(resolvedCanvas.transform, RootName);
            }

            if (root == null)
                return;

            _root = root;
            EnsureRootCanvasGroup();
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
                _appliedRootVisible = false;
                _hasAppliedRootVisibility = true;
            }

            DisableAcousticRadarOverlayImage(root);
        }

        private void TryRegisterUiService()
        {
            if (!Application.isPlaying || _ownsGlobalUiSlot)
                return;

            IUIService current = GlobalRegistry.UI;
            if (current != null && !ReferenceEquals(current, this))
                return;

            GlobalRegistry.RegisterUIService(this);
            _ownsGlobalUiSlot = ReferenceEquals(GlobalRegistry.UI, this);
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
            EnsureHierarchy();
            HideIncompleteRootImmediately();
            RefreshVisuals(0f, refreshMediumCadence: true, refreshSlowCadence: true);
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
                : math.max(previewCamera.nearClipPlane + ProjectionNearClipSafetyPaddingMeters, ProjectionNearClipSafetyPaddingMeters);
            float halfFovRadians = math.max(0.001f, previewCamera.fieldOfView * DegreesToHalfRadians);
            float frustumHalfHeight = ExactPrimaryHudTanPositive(halfFovRadians) * planeDistance;
            float frustumHalfWidth = frustumHalfHeight * math.max(0.0001f, previewCamera.aspect);
            float invReferenceHeight = math.rcp(referenceResolution.y);
            float worldScale = math.max(0.000001f, frustumHalfHeight * 2f * invReferenceHeight);

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

        private void QueueSuitHudReactiveTick(float deltaTime)
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                _pendingStencilSuppressionApply = true;
                return;
            }

            QueueReactiveLateFrameSolve(deltaTime);
        }

        private void RunReactiveLateFrameSolve(float deltaTime)
        {
            if (_toolDepletedWarningTimer > 0f)
            {
                _toolDepletedWarningTimer = math.max(0f, _toolDepletedWarningTimer - math.max(0f, deltaTime));
                _corruptionFrameVersion++;
            }

            if (!_layoutBuilt || _root == null || targetCanvas == null)
                return;

            long hudSolveStartTimestamp = Stopwatch.GetTimestamp();
            UpdateSavingProgressHud(deltaTime);
            int cadenceFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            bool reactiveDirty = ConsumeReactiveSignals();
            int cadenceStride = math.max(1, _reactiveUiCadenceStride);
            bool cadenceGateOpen = cadenceStride <= 1 ||
                reactiveDirty ||
                cadenceFrame % cadenceStride == 0;
            bool refreshMediumCadence = reactiveDirty ||
                (cadenceGateOpen && cadenceFrame % MediumCadenceFrameModulo == 0);
            bool refreshSlowCadence = reactiveDirty ||
                (cadenceGateOpen && cadenceFrame % SlowCadenceFrameModulo == 0);
            RefreshAcousticRadarPayload();
            if (_threatChevronActiveMask != 0u)
                _threatChevronPulseTime += math.max(0f, deltaTime);
            if (cadenceGateOpen)
            {
                RefreshVisuals(deltaTime, refreshMediumCadence, refreshSlowCadence);
                HphiReactiveUiTelemetry.RecordActiveUiUpdate();
            }

            PublishHudSolveWarningIfNeeded(hudSolveStartTimestamp);
            RuntimeWatchdog.MarkHudCanvasUpdated(targetCanvas);
        }

        private void PublishHudSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= _HudSolveBudgetWarningTicks || Hecton8.Core.SystemDispatcher.CurrentFrameIndex < _nextHudPerformanceWarningFrame)
                return;

            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _HudSolveBudgetWarningHash,
                _HudSolveBudgetContextHash,
                (float)elapsedMilliseconds);
            _nextHudPerformanceWarningFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + HudPerformanceWarningCooldownFrames;
        }

        public void SlowTick()
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                _pendingStencilSuppressionApply = true;
                return;
            }

            if (!_runtimeHudCallbacksActive)
                return;

            RefreshQualityPolicy();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
            RefreshThreatChevronTargets();
            FlushPendingAcousticRadarTextureRefresh();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            _canvasStateApplied = false;
            _rootBaseAnchoredPositionCaptured = false;
            _rootBaseAnchoredPosition = Vector2.zero;

            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying)
            {
                QueueRuntimeCanvasRefresh(forceResolve: false, refreshDepthSignal: false);
                NormalizeCanvas(allowUiScalerCreation: false);
                EnsureHierarchy();
                return;
            }

            if (keepVisibleInEditMode)
            {
                NormalizeCanvas();
                EnsureHierarchy();
                RefreshVisuals(0f, refreshMediumCadence: true, refreshSlowCadence: true);
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                _pendingStencilSuppressionApply = false;
                return;
            }

            if (_pendingStencilSuppressionApply)
            {
                _pendingStencilSuppressionApply = false;
                return;
            }

            if (!_runtimeHudCallbacksActive || _pendingRuntimeCanvasRefresh || _forceResolveOnSlowTick || !IsRuntimeHierarchyReady())
                return;

            if (!_reactiveLateFrameSolveRequested)
                QueueSuitHudReactiveTick(SystemDispatcher.CurrentFrameUnscaledDeltaTime);

            ConsumeDepthChangedSignals();
            float reactiveDeltaTime = _reactiveLateFrameSolveRequested
                ? _queuedReactiveDeltaTime
                : SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            _reactiveLateFrameSolveRequested = false;
            _queuedReactiveDeltaTime = 0f;
            RunReactiveLateFrameSolve(reactiveDeltaTime);

            if (renderPath == RenderPath.ProjectionSource && targetCanvas != null)
                UpdateProjectionCanvasPose(targetCanvas.transform as RectTransform, ResolveUiReferenceResolution());

            float unscaledDeltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            _displayPower01 = DampHudValue(_displayPower01, _targetPower01, BatteryGaugeDamping, unscaledDeltaTime);
            RenderScannerHologram(unscaledDeltaTime);
            RenderThreatChevrons();

            if (!_rootBaseAnchoredPositionCaptured)
            {
                _rootBaseAnchoredPosition = _root.anchoredPosition;
                _rootBaseAnchoredPositionCaptured = true;
            }

            float jitterStrength = math.saturate(math.max(_traumaRecoilScalar, _traumaGlitchIntensity * 0.35f));
            if (jitterStrength <= 0.0001f)
            {
                if (_root.anchoredPosition != _rootBaseAnchoredPosition)
                    _root.anchoredPosition = _rootBaseAnchoredPosition;
                return;
            }

            _jitterTime += unscaledDeltaTime;
            float amplitude = JitterAmplitudePixels * jitterStrength;
            Vector2 jitterOffset = new Vector2(
                EvaluateCheapSignedWave(_jitterTime * JitterFrequencyRadians) * amplitude,
                EvaluateCheapSignedWave(_jitterTime * (JitterFrequencyRadians * 0.73f)) * amplitude * 0.58f);
            _root.anchoredPosition = _rootBaseAnchoredPosition + jitterOffset;
        }

        public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
        {
            if ((GameBootstrapperEventType)payload.EventType != GameBootstrapperEventType.GameReady)
                return;

            // Ahead of HandleGameBootstrapperReady, whose isActiveAndEnabled/stencil-suppression early returns
            // must not decide whether a failed write can reach the player. GameBootstrapper.Register does not
            // replay GameReady, so this is the only pass that covers an overlay enabled before the bootstrapper.
            EnsureSaveFailureNotificationBridgeCold();
            HandleGameBootstrapperReady();
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            SaveEventType eventType = (SaveEventType)payload.Type;
            if (eventType == SaveEventType.MappedWriteStarted)
            {
                BeginSavingProgressMappedWrite();
                return;
            }

            if (eventType == SaveEventType.SaveCompleted || eventType == SaveEventType.SaveFailed)
                RequestSavingProgressHide();
        }

        /// <summary>
        /// Mounts the authored save-failure notification bridge onto the live notification surface, so a failed
        /// write becomes visible in the gameplay HUD instead of reading as a completed one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="OnSaveEvent"/> above retires the "saving" pulse for <see cref="SaveEventType.SaveFailed"/>
        /// on exactly the same branch as <see cref="SaveEventType.SaveCompleted"/>, which is correct for the pulse
        /// — the write is over either way — and is the entire gameplay response. Rendered alone it reports a lost
        /// write as a saved one.
        /// </para>
        /// <para>
        /// <see cref="HUDSaveNotificationLink"/> is the only component in the tree that renders a save failure to
        /// the HUD, and its script guid <c>473b7a7cc5029354e85995ce5c763e8f</c> occurred in exactly one file in
        /// the whole tree — its own <c>.cs.meta</c>. Not a scene, not a prefab, not a runtime construction:
        /// authored complete, then never instantiated. The runtime <see cref="ISaveEventListener"/> registrations
        /// were <c>MainMenuController</c> (menu scene), <c>PauseMenuController</c> (only while paused),
        /// <c>HarvestableOutcrop</c> and <c>ModLoader</c> (neither a surface), so with no listener holding a HUD,
        /// <c>SaveEvents.FlushPending</c> took its <c>_listeners.Count &lt;= 0</c> branch and discarded the
        /// failure through <c>DrainWithoutDispatch</c>. Its presentation is already authored, so activating it
        /// invents nothing and adds no second notification system.
        /// </para>
        /// <para>
        /// The bridge is mounted on the notification's OWN GameObject, and that placement is load-bearing rather
        /// than convenient: <see cref="HUDSaveNotificationLink"/> resolves its surface with a same-object
        /// <c>TryGetComponent</c>, so hosting it anywhere else leaves that field null and the component
        /// early-returns out of every event it receives — the same silent nothing it is being activated to fix.
        /// </para>
        /// <para>
        /// Resolution uses <see cref="HUDNotification.TryGetActive"/> with the same
        /// <c>GameBootstrapper.EnsureHudNotificationRegistered</c> fallback <c>PlayerRuntimeContextService</c>
        /// already uses, and that surface is a persisted <c>ScreenSpaceOverlay</c> canvas of its own. Being
        /// independent of this overlay's <c>renderPath</c> is the requirement: wiring the message into the visor
        /// projection-source canvas instead would make it visible only in projected mode.
        /// </para>
        /// <para>
        /// Gated on <see cref="GameBootstrapper.HasActiveInstance"/> so no notification surface is constructed for
        /// the main menu, which raises no gameplay notification and owns its own save-failure modal.
        /// </para>
        /// </remarks>
        private static void EnsureSaveFailureNotificationBridgeCold()
        {
            // Destroyed components compare equal to null, so a stale bridge from a previous scene re-mounts.
            if (!Application.isPlaying || s_saveFailureNotificationBridge != null)
                return;

            if (!GameBootstrapper.HasActiveInstance)
                return;

            if (!HUDNotification.TryGetActive(out HUDNotification notification))
                notification = GameBootstrapper.EnsureHudNotificationRegistered();

            if (notification == null)
                return;

            GameObject notificationHost = notification.gameObject;
            if (!notificationHost.TryGetComponent(out HUDSaveNotificationLink bridge))
                // COLD ALLOC: HUDSaveNotificationLink[1] - save-failure HUD bridge, mounted on the notification surface so its same-object surface lookup binds - owner: SuitHUDV4CanvasOverlay
                bridge = notificationHost.AddComponent<HUDSaveNotificationLink>();

            s_saveFailureNotificationBridge = bridge;
        }

        public void SetScannerInterferenceActive(bool active)
        {
            if (_scannerInterferenceActive == active)
                return;

            _scannerInterferenceActive = active;
            if (active)
            {
                _scannerInterferencePhase = 0f;
                SetScannerFlatHologramVisible(true);
                return;
            }

            if (!HasActiveScannerHologramSnapshot())
                SetScannerFlatHologramVisible(false);
        }

        private void BeginSavingProgressMappedWrite()
        {
            _savingProgressTargetAlpha = 1f;
            _savingProgressHidePending = false;
            _savingProgressHideNotBeforeTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds + SavingProgressMinimumVisibleSeconds;
            ClearSavingProgressFailureLatch();
            EmitSavingProgressHapticPulse();
        }

        private void EmitSavingProgressHapticPulse()
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextSavingProgressHapticTime)
                return;

            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                0.08f,
                0.16f,
                SavingProgressHapticDurationSeconds,
                SavingProgressHapticFrequencyHz,
                SavingProgressHapticPriority,
                BothMotorMask);
            _nextSavingProgressHapticTime = now + SavingProgressHapticCooldownSeconds;
        }

        private void RequestSavingProgressHide()
        {
            if ((float)SystemDispatcher.CurrentUnscaledTimeSeconds < _savingProgressHideNotBeforeTime)
            {
                _savingProgressHidePending = true;
                return;
            }

            _savingProgressHidePending = false;
            _savingProgressTargetAlpha = 0f;
        }

        /// <summary>
        /// Polls the authoritative save-status latch so the indicator reports the write's real terminal state
        /// instead of a fade timed off <see cref="SaveEventType.MappedWriteStarted"/> alone.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Route chosen: <see cref="SaveEvents.TryGetCurrentStatus"/>, not
        /// <c>SignalBus&lt;SaveStatusSignal&gt;.GetFrameSnapshot()</c>. Both carry the identical payload - SaveManager
        /// writes them on the same two lines (<c>SaveManager.PublishSaveStatus</c>, at the managed publish plus the
        /// best-effort lane push) - so the choice is purely about whether the terminal state can be missed from THIS
        /// tick lane. The SignalBus read is edge-triggered and frame-scoped, and <see cref="LateFrameTick"/> has five
        /// early returns above <see cref="RunReactiveLateFrameSolve"/> (stencil suppression, pending suppression
        /// apply, inactive runtime callbacks, pending canvas refresh, forced slow-tick resolve, hierarchy not ready)
        /// plus a sixth on <c>_layoutBuilt</c>. A <c>Failed</c> signal that flushes on a frame gated by any of those
        /// is gone for good, and the lane push is best-effort anyway - <c>SignalBus.TryPush</c> can shed on stress or
        /// on ring overflow. The latch is level-triggered: <c>SaveEvents.PublishCurrentStatus</c> stores the status
        /// against a monotonic, never-zero sequence, and this method compares it against a caller-owned cursor, so a
        /// skipped frame costs latency and nothing else. It is also unconditional - no lane initialization, no load
        /// shedding, no capacity - and cheaper per tick than the span read plus its filter loop.
        /// </para>
        /// <para>
        /// This is the SAVE indicator, so load traffic is filtered out by
        /// <see cref="SaveStatusSignal.LoadOperationFlag"/>: SaveManager latches load statuses into the same
        /// per-slot bucket, and an unfiltered read would light the disk during a load.
        /// </para>
        /// <para>
        /// No numeric percentage is rendered, and that is a truth constraint rather than a budget one.
        /// <see cref="SaveStatusSignal.Progress01"/> is not a ramp in this codebase - SaveManager publishes exactly
        /// <c>0f</c> for Queued/Rejected, <c>0.05f</c> once for a save in progress, <c>0.08f</c> once for a load in
        /// progress, and <c>1f</c> for Completed/Failed. Nothing increments it between those stops, so a percentage
        /// readout would sit on "5%" for the entire write and then jump to 100%: a second false animation. The
        /// honest signal here is the discrete state, which is what this drives. The saving-progress hierarchy also
        /// contains no TMP_Text at all - only Images - so no tick-path text formatting is introduced.
        /// </para>
        /// </remarks>
        private void PollSavingProgressStatus()
        {
            TryApplySavingProgressStatusBucket(s_saveStatusProbeSlot0Hash, ref _savingProgressStatusSequenceSlot0);
            TryApplySavingProgressStatusBucket(s_saveStatusProbeSlot1Hash, ref _savingProgressStatusSequenceSlot1);
            TryApplySavingProgressStatusBucket(s_saveStatusProbeSlot2Hash, ref _savingProgressStatusSequenceSlot2);
            TryApplySavingProgressStatusBucket(SaveStatusProbeUnknownSlotHash, ref _savingProgressStatusSequenceUnknownSlot);
        }

        private void TryApplySavingProgressStatusBucket(uint slotHash, ref uint lastSeenSequence)
        {
            if (!SaveEvents.TryGetCurrentStatus(slotHash, ref lastSeenSequence, out SaveStatusSignal status))
                return;

            if ((status.Flags & SaveStatusSignal.LoadOperationFlag) != 0)
                return;

            ApplySavingProgressStatus(in status);
        }

        private void ApplySavingProgressStatus(in SaveStatusSignal status)
        {
            bool failed = (status.Flags & SaveStatusSignal.FailureFlag) != 0 ||
                status.State == SaveStatusSignal.Failed ||
                status.State == SaveStatusSignal.Rejected;
            if (failed)
            {
                LatchSavingProgressFailure();
                return;
            }

            if (status.State == SaveStatusSignal.Completed)
            {
                ClearSavingProgressFailureLatch();
                RequestSavingProgressHide();
                return;
            }

            // Queued/InProgress: the operation is genuinely live, so the icon is raised here too and not only on
            // MappedWriteStarted. InProgress is published before the mapped write begins, which is what gives a save
            // that dies during snapshot or compression an icon to turn red instead of showing the player nothing.
            ClearSavingProgressFailureLatch();
            _savingProgressHidePending = false;
            _savingProgressTargetAlpha = 1f;
        }

        /// <summary>
        /// Holds the indicator up in the warning palette for a failed write.
        /// </summary>
        /// <remarks>
        /// The hold is applied by pushing the EXISTING <c>_savingProgressHideNotBeforeTime</c> gate forward rather
        /// than by adding a second timer, because the ordering between this latch and
        /// <see cref="RequestSavingProgressHide"/> is not fixed: <c>HandleSaveFailure</c> publishes the Failed status
        /// before it raises <see cref="SaveEventType.SaveFailed"/>, but the status is polled on this tick lane while
        /// the event arrives through the SaveEvents flush, so either can land first. Reusing the one gate makes both
        /// orders end in the same held, red state.
        /// </remarks>
        private void LatchSavingProgressFailure()
        {
            _savingProgressTargetAlpha = 1f;
            _savingProgressHidePending = false;

            float failureHoldUntilTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds + SavingProgressFailureHoldSeconds;
            if (failureHoldUntilTime > _savingProgressHideNotBeforeTime)
                _savingProgressHideNotBeforeTime = failureHoldUntilTime;

            _savingProgressFailureLatched = true;
        }

        private void ClearSavingProgressFailureLatch()
        {
            _savingProgressFailureLatched = false;
        }

        /// <summary>
        /// Re-applies the saving-progress colours from the cached palette after a status change.
        /// </summary>
        /// <remarks>
        /// <see cref="ApplySavingProgressStyle"/> stays the single writer of those Image colours. Writing them
        /// directly from the tick would lose the fight with <see cref="ApplyStressPulseStyle"/>, which re-applies the
        /// whole chrome style whenever the stress pulse moves and would repaint a failed write back to its normal
        /// colour. When no palette has been resolved yet there is nothing to re-apply from; the next
        /// <see cref="ApplyStaticStyleIfNeeded"/> pass picks the state up.
        /// </remarks>
        private void RefreshSavingProgressStyleForStatus()
        {
            if (!_styleApplied)
                return;

            ApplySavingProgressStyle(_appliedPrimary, _appliedDim, _appliedWarning);
        }

        private void HandleGameBootstrapperReady()
        {
            if (!isActiveAndEnabled)
                return;

            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            if (CacheRuntimeDependenciesCold())
                RebuildLocalizationCache();
            _layoutBuilt = false;
            InvalidateVisualCaches();
            TryRegisterRuntimeTick();
            QueueRuntimeCanvasRefresh(forceResolve: true, refreshDepthSignal: true);
            ProcessPendingRuntimeCanvasRefresh();
            RefreshQuickbarSlotHashCacheCold();
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            QueueLocalizedPresentationRefresh(forceResolve: false, refreshDepthSignal: false);
        }

        void IPlayerSignalEventListener.OnTraumaHudSignal(in TraumaHudSignal signal)
        {
            HandleTraumaHudSignal(in signal);
        }

        void IPlayerSignalEventListener.OnInteractionSignal(in PlayerInteractionStressSignal signal)
        {
        }

        void IPlayerSignalEventListener.OnToolDepletedSignal(in PlayerToolDepletedSignal signal)
        {
            HandleToolDepletedSignal(in signal);
        }

        private void HandleTraumaHudSignal(in TraumaHudSignal signal)
        {
            _traumaGlitchIntensity = math.saturate(signal.GlitchIntensity);
            _traumaRecoilScalar = math.saturate(signal.RecoilScalar);
            _traumaTransportPower01 = math.saturate(signal.TransportPower01);
            _traumaHullIntegrity01 = math.saturate(signal.HullIntegrity01);
            _biosRecoveryMode = signal.BiosRecoveryMode != 0;
            _corruptionFrameVersion++;
            InvalidateVisualCaches();
        }

        private void HandleToolDepletedSignal(in PlayerToolDepletedSignal signal)
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
            RefreshVisuals(dt, refreshMediumCadence: true, refreshSlowCadence: true);
        }

        private void QueueRuntimeCanvasRefresh(bool forceResolve, bool refreshDepthSignal)
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                _pendingRuntimeCanvasRefresh = false;
                _forceResolveOnSlowTick = false;
                _pendingDepthSignalRefresh = false;
                return;
            }

            _pendingRuntimeCanvasRefresh = true;
            if (forceResolve)
                _forceResolveOnSlowTick = true;
            if (refreshDepthSignal)
                _pendingDepthSignalRefresh = true;
        }

        private void QueueLocalizedPresentationRefresh(bool forceResolve, bool refreshDepthSignal)
        {
            _localizedPresentationDirty = true;
            if (isActiveAndEnabled)
            {
                QueueRuntimeCanvasRefresh(forceResolve, refreshDepthSignal);
                TryRegisterRuntimeTick();
            }
        }

        private void ProcessPendingRuntimeCanvasRefresh()
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            bool runtimeHierarchyReady = IsRuntimeHierarchyReady();
            bool needsRuntimeCanvasRefresh =
                _pendingRuntimeCanvasRefresh ||
                _forceResolveOnSlowTick ||
                _localizedPresentationDirty ||
                !runtimeHierarchyReady ||
                NeedsAutoResolve();
            if (!needsRuntimeCanvasRefresh && !_pendingDepthSignalRefresh)
                return;

            AutoResolve(_forceResolveOnSlowTick);
            NormalizeCanvas(_pendingRuntimeCanvasRefresh || _forceResolveOnSlowTick || !runtimeHierarchyReady);
            EnsureHierarchy();

            if (_pendingDepthSignalRefresh || survival != _depthSignalSource)
                RefreshDepthSignalSubscription();

            if (_localizedPresentationDirty)
            {
                _localizedPresentationDirty = false;
                RebuildLocalizationCache();
                InvalidateVisualCaches();
            }

            if (_layoutBuilt && _root != null && targetCanvas != null)
                RefreshVisuals(0.016f, refreshMediumCadence: true, refreshSlowCadence: true);

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
            RefreshRuntimeDependenciesFromCachedServices();

            if (targetCanvas == null)
                targetCanvas = ResolveTargetCanvas();

            if (_inventoryService != null)
            {
                _playerInventory = _inventoryService.Inventory;
                _itemCatalog = _playerInventory != null ? _playerInventory.ItemCatalog : null;
                if (_toolManager == null)
                    _toolManager = _inventoryService.ToolManager;
            }

            if (projectionCamera == null)
            {
                Transform root = transform.root;
                for (int i = 0; i < VisorHUDController.ActiveControllerCount; i++)
                {
                    VisorHUDController controller = VisorHUDController.GetActiveController(i);
                    if (controller == null || controller.HudCamera == null)
                        continue;

                    if (controller.transform.root != root)
                        continue;

                    projectionCamera = controller.HudCamera;
                    break;
                }

                if (projectionCamera == null)
                {
                    for (int i = 0; i < VisorHUDController.ActiveControllerCount; i++)
                    {
                        VisorHUDController controller = VisorHUDController.GetActiveController(i);
                        if (controller != null && controller.HudCamera != null)
                        {
                            projectionCamera = controller.HudCamera;
                            break;
                        }
                    }
                }

            }

            TryResolveProjectionCameraFromPlayer();

            if (uiFont == null)
                uiFont = TMP_Settings.defaultFontAsset;

            if (numericFont == null)
                numericFont = uiFont;

            if (labelFont == null)
                labelFont = uiFont != null && !IsNumericOnlyFont(uiFont) ? uiFont : TMP_Settings.defaultFontAsset;

            TryResolveDefaultIconTextures();

            RefreshRuntimeDependenciesFromCachedServices();

            RefreshDepthSignalSubscription();
        }

        private void TryResolveProjectionCameraFromPlayer()
        {
            if (projectionCamera != null)
                return;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera != null)
            {
                projectionCamera = playerCamera;
                return;
            }

        }

        private bool CacheRuntimeDependenciesCold()
        {
            ILocalizationStressPresentationReadModel localizationRuntime = GlobalRegistry.LocalizationStressPresentation;
            bool localizationChanged = !ReferenceEquals(_localizationRuntime, localizationRuntime);
            _localizationRuntime = localizationRuntime;
            CacheAudioService(GlobalRegistry.Audio);
            _playerRuntimeContext = GlobalRegistry.Player;
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
            RebindInventoryService(GlobalRegistry.PlayerInventory);
            RefreshRuntimeDependenciesFromCachedServices();
            RefreshQualityPolicy();
            return localizationChanged;
        }

        private void CacheAudioService(Hecton8.Core.IAudioService audioService)
        {
            _spatialAudioManager = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private Hecton8.Core.IAudioService ResolveAudioService()
        {
            Hecton8.Core.IAudioService audioService = _spatialAudioManager;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _spatialAudioManager = null;
            return null;
        }

        private static bool IsAudioServiceUsable(Hecton8.Core.IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void RefreshRuntimeDependenciesFromCachedServices()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (projectionCamera == null)
                    projectionCamera = playerContext.PlayerCamera;
                if (survival == null)
                    survival = playerContext.SurvivalSystem;
                if (playerMovement == null)
                    playerMovement = playerContext.PlayerMovement;
                if (flashlight == null)
                    flashlight = playerContext.Flashlight;
                if (underwaterVisuals == null)
                    underwaterVisuals = playerContext.UnderwaterVisuals;
                if (_toolManager == null)
                    _toolManager = playerContext.ToolManager;
            }

            if (_inventoryService != null)
            {
                _playerInventory = _inventoryService.Inventory;
                _itemCatalog = _playerInventory != null ? _playerInventory.ItemCatalog : null;
                if (_toolManager == null)
                    _toolManager = _inventoryService.ToolManager;
            }

            RefreshQualityPolicy();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            bool needsRefresh = false;
            bool forceResolve = false;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    if (ReferenceEquals(_localizationRuntime, currentService))
                        return;

                    _localizationRuntime = currentService as ILocalizationStressPresentationReadModel;
                    QueueLocalizedPresentationRefresh(forceResolve: false, refreshDepthSignal: false);
                    break;

                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;

                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    ClearPlayerRuntimeBindings(previousService as IPlayerRuntimeContext);
                    forceResolve = true;
                    needsRefresh = true;
                    break;

                case GlobalRegistryServiceSlot.PlayerInventory:
                    RebindInventoryService(currentService as IPlayerInventoryService);
                    _toolManager = null;
                    forceResolve = true;
                    needsRefresh = true;
                    break;

                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
                    CharBufferPool.BindDataVaultCold(nextVault);
                    BindGlitchTableVault(nextVault);
                    return;

                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _vegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
                    return;

                default:
                    return;
            }

            if (!needsRefresh || !isActiveAndEnabled)
                return;

            QueueRuntimeCanvasRefresh(forceResolve, refreshDepthSignal: forceResolve);
            TryRegisterRuntimeTick();
        }

        private void RefreshQualityPolicy()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            _qualityWeight01 = math.saturate(math.select(_qualityWeight01, qualityWeight, math.isfinite(qualityWeight)));
            float curve = SmoothStep01(_qualityWeight01);
            _reactiveUiCadenceStride = math.clamp(
                (int)math.round(math.lerp(MaxReactiveUiCadenceStride, 1f, curve)),
                1,
                MaxReactiveUiCadenceStride);
        }

        private void ClearPlayerRuntimeBindings(IPlayerRuntimeContext previousContext)
        {
            if (previousContext == null)
            {
                flashlight = null;
                underwaterVisuals = null;
                _toolManager = null;
                return;
            }

            if (ReferenceEquals(flashlight, previousContext.Flashlight))
                flashlight = null;
            if (ReferenceEquals(underwaterVisuals, previousContext.UnderwaterVisuals))
                underwaterVisuals = null;
            if (ReferenceEquals(_toolManager, previousContext.ToolManager))
                _toolManager = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterFloatingOriginListener()
        {
            if (_floatingOriginRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _floatingOriginRegistered = true;
        }

        private void TryUnregisterFloatingOriginListener()
        {
            if (!_floatingOriginRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _floatingOriginRegistered = false;
        }

        private void CacheGlitchTableVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            BindGlitchTableVault(vault);
        }

        private void BindGlitchTableVault(IDataVault vault)
        {
            if (!ReferenceEquals(_glitchVault, vault))
                ClearGlitchTableBinding();

            _glitchVault = vault;
            _glitchTableHandleReady = false;
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle(
                    (BufferID)DiegeticGlitchSurgeonRuntime.GlitchTableBufferIdRaw,
                    out VaultGenerationHandle<byte> borrowedHandle) ||
                !IsGlitchTableHandle(in borrowedHandle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in borrowedHandle, out NativeArray<byte>.ReadOnly glitchTable) ||
                vault.IsCompactionFenceActive ||
                glitchTable.Length < DiegeticGlitchSurgeonRuntime.GlitchTableCapacity)
            {
                return;
            }

            _glitchTableHandle = borrowedHandle;
            _glitchTableHandleReady = true;

            unsafe
            {
                byte* table = (byte*)glitchTable.GetUnsafeReadOnlyPtr();
                if (table == null || !GlitchTable.IsValidGlyphTable(table, DiegeticGlitchSurgeonRuntime.GlitchTableCapacity))
                    ClearGlitchTableBinding();
            }
        }

        private unsafe bool TryResolveGlitchTablePointer(out byte* table, out int tableLength)
        {
            table = null;
            tableLength = 0;
            if (!_glitchTableHandleReady ||
                _glitchVault == null ||
                _glitchVault.IsCompactionFenceActive ||
                !IsGlitchTableHandle(in _glitchTableHandle))
                return false;

            if (!_glitchVault.TryReadOnlyHandle(in _glitchTableHandle, out NativeArray<byte>.ReadOnly glitchTable) ||
                _glitchVault.IsCompactionFenceActive ||
                glitchTable.Length < DiegeticGlitchSurgeonRuntime.GlitchTableCapacity)
            {
                ClearGlitchTableBinding();
                return false;
            }

            table = (byte*)glitchTable.GetUnsafeReadOnlyPtr();
            if (table == null)
                return false;

            tableLength = DiegeticGlitchSurgeonRuntime.GlitchTableCapacity;
            return true;
        }

        private void ClearGlitchTableBinding()
        {
            _glitchVault = null;
            _glitchTableHandle = default;
            _glitchTableHandleReady = false;
        }

        private static bool IsGlitchTableHandle<T>(in VaultGenerationHandle<T> handle) where T : unmanaged
        {
            return handle.BufferID == DiegeticGlitchSurgeonRuntime.GlitchTableBufferIdRaw &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private void RebindInventoryService(IPlayerInventoryService inventoryService)
        {
            if (ReferenceEquals(_inventoryService, inventoryService))
                return;

            _inventoryService = inventoryService;
            _playerInventory = null;
            _itemCatalog = null;
            _lastInventoryVersion = -1;
            _inventorySignalDirty = true;
            InvalidateQuickbarSlotHashCache();
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
            return Application.isPlaying
                ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds
                : ResolveEditorPreviewClockSeconds();
        }

        private static float ResolveEditorPreviewClockSeconds()
        {
            return (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        }

        private void NormalizeCanvas(bool allowUiScalerCreation = true)
        {
            if (IsStencilRenderGraphSuppressedRuntime())
            {
                ApplyStencilRenderGraphSuppressionIfNeeded();
                return;
            }

            if (targetCanvas == null)
                return;

            if (!IsScreenOverlayAllowed() && renderPath != RenderPath.ProjectionSource)
            {
                renderPath = RenderPath.ProjectionSource;
                _layoutBuilt = false;
                InvalidateVisualCaches();
            }

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            CacheDefaultCanvasHierarchy(canvasRect);
            Vector2 referenceResolution = ResolveUiReferenceResolution();
            bool useProjectionCanvas = renderPath == RenderPath.ProjectionSource || !IsScreenOverlayAllowed();
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

            HectonUIScaler uiScaler = ResolveUiScaler(allowUiScalerCreation);
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
                if (targetCanvas.additionalShaderChannels != AcousticRadarCanvasShaderChannels)
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
#if !UNITY_EDITOR
                return false;
#else
                if (Application.isPlaying)
                    return false;
                if (targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    return false;
                if (targetCanvas.worldCamera != null)
                    return false;
                if (targetCanvas.sortingOrder != overlaySortingOrder)
                    return false;
#endif
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
            float halfFovRadians = math.max(0.001f, targetCamera.fieldOfView * DegreesToHalfRadians);
            float frustumHalfHeight = ExactPrimaryHudTanPositive(halfFovRadians) * safeDistance;
            float frustumHalfWidth = frustumHalfHeight * math.max(0.0001f, targetCamera.aspect);
            float invReferenceWidth = math.rcp(referenceResolution.x);
            float invReferenceHeight = math.rcp(referenceResolution.y);
            float scaleX = frustumHalfWidth * 2f * invReferenceWidth;
            float scaleY = frustumHalfHeight * 2f * invReferenceHeight;
            return math.max(0.000001f, math.min(scaleX, scaleY));
        }

        private void ApplyOverlayCanvasState(Canvas canvas, RectTransform canvasRect)
        {
#if !UNITY_EDITOR
            renderPath = RenderPath.ProjectionSource;
            ApplyProjectionCanvasState(canvas, canvasRect, ResolveUiReferenceResolution());
            return;
#else
            if (Application.isPlaying)
            {
                renderPath = RenderPath.ProjectionSource;
                ApplyProjectionCanvasState(canvas, canvasRect, ResolveUiReferenceResolution());
                return;
            }

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
#endif
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
            canvas.additionalShaderChannels = AcousticRadarCanvasShaderChannels;
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
            Vector3 poseDelta = canvasRect.position - expectedPosition;
            float3 poseDelta3 = new float3(poseDelta.x, poseDelta.y, poseDelta.z);
            if (math.lengthsq(poseDelta3) > ProjectionPosePositionTolerance)
                return false;

            float rotationDot = math.abs(Quaternion.Dot(canvasRect.rotation, cameraTransform.rotation));
            float rotationSinHalfSq = math.max(0f, 1f - math.min(rotationDot * rotationDot, 1f));
            if (rotationSinHalfSq > ProjectionPoseRotationSinHalfSqTolerance)
                return false;

            if (!IsProjectionCanvasLayerValid(canvasRect.gameObject, projectionCamera))
                return false;

            float expectedScale = ResolveProjectionCanvasWorldScale(projectionCamera, referenceResolution);
            Vector3 scale = canvasRect.localScale;
            return math.abs(scale.x - expectedScale) <= ProjectionPoseScaleTolerance &&
                   math.abs(scale.y - expectedScale) <= ProjectionPoseScaleTolerance &&
                   math.abs(scale.z - expectedScale) <= ProjectionPoseScaleTolerance;
        }

        private void UpdateProjectionCanvasPose(RectTransform canvasRect, Vector2 referenceResolution)
        {
            if (canvasRect == null || projectionCamera == null)
                return;

            Transform cameraTransform = projectionCamera.transform;
            float projectionDistance = ResolveProjectionPlaneDistance();
            float expectedScale = ResolveProjectionCanvasWorldScale(projectionCamera, referenceResolution);
            Vector3 targetPosition = cameraTransform.position + cameraTransform.forward * projectionDistance;
            if (HectonXRRuntimeState.IsXRActive)
            {
                float dt = math.max(0f, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
                float positionBlend = ApproximateOneMinusExpNeg(VrLazyFollowPositionSharpness * dt);
                float rotationBlend = ApproximateOneMinusExpNeg(VrLazyFollowRotationSharpness * dt);
                Vector3 smoothedPosition = Vector3.LerpUnclamped(canvasRect.position, targetPosition, positionBlend);
                Quaternion smoothedRotation = CinematicMath.FastNlerp(canvasRect.rotation, cameraTransform.rotation, rotationBlend);
                canvasRect.SetPositionAndRotation(smoothedPosition, smoothedRotation);
            }
            else
            {
                canvasRect.SetPositionAndRotation(targetPosition, cameraTransform.rotation);
            }

            canvasRect.localScale = new Vector3(expectedScale, expectedScale, expectedScale);
        }

        private float ResolveProjectionPlaneDistance()
        {
            float authoredDistance = math.max(ProjectionNearClipSafetyPaddingMeters, projectionPlaneDistance);
            if (projectionCamera == null)
                return authoredDistance;

            return math.max(
                projectionCamera.nearClipPlane + ProjectionNearClipSafetyPaddingMeters,
                authoredDistance);
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
            if ((targetCamera.cullingMask & hudInternalMask) == 0)
                targetCamera.cullingMask |= hudInternalMask;
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

            _threatChevronMesh = _threatChevronStaticMesh;
            EnsureThreatChevronPropertyBlockCold();
        }

        private void EnsureThreatChevronPropertyBlockCold()
        {
            _threatChevronPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - threat chevron instanced alpha payload - owner: SuitHUDV4CanvasOverlay
        }

        private void EnsureScannerHologramRuntimeResources()
        {
            // Flat canvas scanner fake has no runtime mesh, material, or TRS buffer.
        }

        private void ApplyDitheredBackgroundMaterial(Graphic image)
        {
            if (image == null)
                return;

            if (image is MaskableGraphic maskableGraphic)
                maskableGraphic.maskable = true;
            if (ditheredUiBackgroundMaterial != null)
                image.material = ditheredUiBackgroundMaterial;
        }

        private void EnsureSavingProgressPulseRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

            BindSavingProgressPulseMaterials();
        }

        private void DisposeSavingProgressPulseRuntimeResources()
        {
            if (_savingProgressDataLamp != null && _savingProgressDataLampPulseMaterialBound)
                _savingProgressDataLamp.material = null;
            _savingProgressDataLampPulseMaterialBound = false;

            if (_savingProgressDataNeedle != null && _savingProgressDataNeedlePulseMaterialBound)
                _savingProgressDataNeedle.material = null;
            _savingProgressDataNeedlePulseMaterialBound = false;

        }

        private void BindSavingProgressPulseMaterials()
        {
            if (dataRecPulseMaterial == null)
                return;

            if (_savingProgressDataLamp != null && !_savingProgressDataLampPulseMaterialBound)
            {
                _savingProgressDataLamp.material = dataRecPulseMaterial;
                _savingProgressDataLampPulseMaterialBound = true;
            }

            if (_savingProgressDataNeedle != null && !_savingProgressDataNeedlePulseMaterialBound)
            {
                _savingProgressDataNeedle.material = dataRecPulseMaterial;
                _savingProgressDataNeedlePulseMaterialBound = true;
            }
        }

        private void EnsureAcousticRadarRuntimeResources()
        {
            if (!Application.isPlaying)
                return;


            BindAcousticRadarOverlayMaterial();
        }

        private void DisposeAcousticRadarRuntimeResources()
        {
            if (_acousticRadarOverlay != null && _acousticRadarOverlayMaterialBound)
            {
                _acousticRadarOverlay.material = null;
                _acousticRadarOverlay.texture = null;
            }
            _acousticRadarOverlayMaterialBound = false;

            InvalidateAcousticRadarOverlayState();

            if (_acousticRadarTexture != null)
            {
                Destroy(_acousticRadarTexture);
                _acousticRadarTexture = null;
                _acousticRadarTextureBindingDirty = true;
            }

            _acousticRadarResolution = 0;
            _pendingAcousticRadarResolution = 0;
            _pendingAcousticRadarTextureRefresh = false;
            _acousticRadarPeakIntensity = 0f;
        }

        private void RefreshAcousticRadarPayload()
        {
            if (!Application.isPlaying)
                return;

            if (IsStencilRenderGraphSuppressedRuntime())
                return;

            if (acousticRadarMaterial == null)
                return;

            NativeArray<float>.ReadOnly radarSamples = default;
            int radarResolution = 0;
            Hecton8.Core.IAudioService audioManager = ResolveAudioService();
            bool hasRadarPayload = audioManager != null &&
                                   audioManager.TryGetAcousticRadarPayload(out radarSamples, out radarResolution) &&
                                   radarSamples.Length > 0 &&
                                   radarResolution > 0;

            if (!hasRadarPayload)
            {
                _acousticRadarPeakIntensity = 0f;
                return;
            }

            if (!IsAcousticRadarTextureReady(radarResolution))
            {
                QueueAcousticRadarTextureRefresh(radarResolution);
                _acousticRadarPeakIntensity = 0f;
                return;
            }

            audioManager = ResolveAudioService();
            if (audioManager == null ||
                !audioManager.TryUploadAcousticRadarPayload(
                    _acousticRadarTexture,
                    out int uploadedSampleCount,
                    out float uploadedPeakIntensity) ||
                uploadedSampleCount <= 0)
            {
                _acousticRadarPeakIntensity = 0f;
                return;
            }

            _acousticRadarTexture.Apply(false, false);
            ApplyAcousticRadarTextureBindingIfNeeded();
            _acousticRadarPeakIntensity = uploadedPeakIntensity;
        }

        private bool IsAcousticRadarTextureReady(int radialResolution)
        {
            return radialResolution > 0 &&
                   _acousticRadarTexture != null &&
                   _acousticRadarResolution == radialResolution;
        }

        private void QueueAcousticRadarTextureRefresh(int radialResolution)
        {
            if (radialResolution <= 0)
                return;

            _pendingAcousticRadarResolution = radialResolution;
            _pendingAcousticRadarTextureRefresh = true;
        }

        private void FlushPendingAcousticRadarTextureRefresh()
        {
            if (!_pendingAcousticRadarTextureRefresh)
                return;

            int resolution = _pendingAcousticRadarResolution;
            _pendingAcousticRadarTextureRefresh = false;
            _pendingAcousticRadarResolution = 0;
            EnsureAcousticRadarTexture(resolution);
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
                _acousticRadarTextureBindingDirty = true;
            }

            _acousticRadarTexture = new Texture2D(radialResolution, 1, TextureFormat.RFloat, false, true)
            {
                name = "HUD_AcousticRadar_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            }; // COLD ALLOC: Texture2D[radialResolution x 1] — passive acoustic visor ring upload target — owner: SuitHUDV4CanvasOverlay
            _acousticRadarTexture.Apply(false, false);
            _acousticRadarResolution = radialResolution;
            _acousticRadarTextureBindingDirty = true;
            return true;
        }

        private void DisposeThreatChevronRuntimeResources()
        {
            _threatChevronMesh = null;
            InvalidateThreatChevronMaterialState();
        }

        private void DisposeScannerHologramRuntimeResources()
        {
            SetScannerFlatHologramVisible(false);
            _scannerHologramAnimationTime = 0f;
        }

        private void RefreshThreatChevronTargets()
        {
            _threatChevronVisibleCount = 0;
            _threatChevronActiveMask = 0u;

            if (!Application.isPlaying ||
                renderPath != RenderPath.ProjectionSource ||
                projectionCamera == null ||
                threatChevronRadiusMeters <= 0f)
            {
                return;
            }

            if (IsStencilRenderGraphSuppressedRuntime())
                return;

            if (_vegetationBridge == null ||
                !_vegetationBridge.TryGetEcosystemThreatGridPayload(
                    out NativeArray<float>.ReadOnly threatLevels,
                    out int gridResolution,
                    out Vector3 gridCenter,
                    out float cellSize))
            {
                return;
            }

            Transform cameraTransform = projectionCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            if (!TryResolveRuntimeAup(cameraPosition, out AbsoluteUniversePosition cameraAup))
                return;

            double3 cameraAbsolute = cameraAup.ToAbsoluteDouble3();
            if (!TryResolveRuntimeAup(gridCenter, out AbsoluteUniversePosition gridCenterAup))
                return;

            float3 gridCenterRelativeToCamera = AupPrecisionMath.LocalDeltaFloat3Clamped(
                gridCenterAup.ToAbsoluteDouble3(),
                cameraAup.ToAbsoluteDouble3(),
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                float3.zero);
            float radius = math.max(1f, threatChevronRadiusMeters);
            float radiusSq = radius * radius;
            float safeCellSize = math.max(0.001f, cellSize);
            float invSafeCellSize = math.rcp(safeCellSize);
            int halfResolution = gridResolution >> 1;
            int radiusCells = CeilPositiveToInt(radius * invSafeCellSize);
            int centerCellX = math.clamp(RoundToIntFast(-gridCenterRelativeToCamera.x * invSafeCellSize) + halfResolution, 0, gridResolution - 1);
            int centerCellZ = math.clamp(RoundToIntFast(-gridCenterRelativeToCamera.z * invSafeCellSize) + halfResolution, 0, gridResolution - 1);
            float halfExtent = (gridResolution - 1) * 0.5f * safeCellSize;
            float originRelativeX = gridCenterRelativeToCamera.x - halfExtent;
            float originRelativeZ = gridCenterRelativeToCamera.z - halfExtent;

            for (int cellZ = math.max(0, centerCellZ - radiusCells); cellZ <= math.min(gridResolution - 1, centerCellZ + radiusCells); cellZ++)
            {
                float relativeZ = originRelativeZ + (cellZ * safeCellSize);
                for (int cellX = math.max(0, centerCellX - radiusCells); cellX <= math.min(gridResolution - 1, centerCellX + radiusCells); cellX++)
                {
                    int cellIndex = (cellZ * gridResolution) + cellX;
                    float threat01 = threatLevels[cellIndex];
                    if (threat01 < threatChevronThreshold)
                        continue;

                    float relativeX = originRelativeX + (cellX * safeCellSize);
                    float horizontalDistanceSq = (relativeX * relativeX) + (relativeZ * relativeZ);
                    if (horizontalDistanceSq > radiusSq)
                        continue;

                    double3 threatAbsolute = cameraAbsolute + math.double3(relativeX, 0d, relativeZ);
                    InsertThreatChevronCandidate(in threatAbsolute, threat01);
                }
            }
        }

        private void InsertThreatChevronCandidate(in double3 absolutePosition, float threat01)
        {
            int insertIndex = -1;
            float weakestThreat = threat01;
            for (int i = 0; i < MaxThreatChevronCount; i++)
            {
                uint bit = 1u << i;
                if ((_threatChevronActiveMask & bit) == 0u)
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

            ThreatChevronState threatState = _threatChevronStates[insertIndex];
            threatState.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
            threatState.Threat01 = threat01;
            _threatChevronStates[insertIndex] = threatState;
            _threatChevronActiveMask |= 1u << insertIndex;
        }

        private void RenderScannerHologram(float deltaTime)
        {
            if (_scannerFlatHologramRoot == null ||
                _scannerFlatHologramBody == null ||
                _scannerFlatHologramCore == null ||
                _scannerFlatHologramScanline == null)
            {
                return;
            }

            if (!TryGetActiveScannerHologramSnapshot(out ScannerTool.ScientificScanSnapshot snapshot))
            {
                RenderScannerInterference(deltaTime);
                return;
            }

            _scannerHologramAnimationTime += math.max(0f, deltaTime);

            float signal01 = ResolveScannerHologramSignal01(snapshot);
            Vector2 size = ResolveScannerFlatHologramSize();
            float pulsePixels = EvaluateCheapSignedWave(_scannerHologramAnimationTime * 5.2f) * math.max(0f, scannerHologramPulsePixels);
            float jitterPixels = EvaluateCheapSignedWave((_scannerHologramAnimationTime * 23.0f) + (signal01 * 5.0f)) * math.max(0f, scannerHologramJitterPixels);
            _scannerFlatHologramRoot.anchoredPosition = scannerHologramOffsetPixels + new Vector2(jitterPixels, pulsePixels * 0.18f);
            _scannerFlatHologramRoot.sizeDelta = size + (Vector2.one * pulsePixels);
            _scannerFlatHologramRoot.localEulerAngles = Vector3.zero;

            Color hologramColor = new Color(
                math.lerp(scannerHologramScanStartColor.r, scannerHologramScanCompleteColor.r, signal01),
                math.lerp(scannerHologramScanStartColor.g, scannerHologramScanCompleteColor.g, signal01),
                math.lerp(scannerHologramScanStartColor.b, scannerHologramScanCompleteColor.b, signal01),
                math.lerp(scannerHologramScanStartColor.a, scannerHologramScanCompleteColor.a, signal01));
            float glitchAmount = math.saturate((_stressPulseIntensity * 0.45f) + (_traumaGlitchIntensity * 0.55f));
            Color bodyColor = hologramColor;
            bodyColor.a *= 0.44f + glitchAmount * 0.18f;
            _scannerFlatHologramBody.color = bodyColor;

            Color coreColor = hologramColor;
            coreColor.a = math.saturate(hologramColor.a + 0.22f + glitchAmount * 0.18f);
            _scannerFlatHologramCore.color = coreColor;

            Color lineColor = scannerHologramScanCompleteColor;
            lineColor.a = math.saturate(0.35f + signal01 * 0.5f + glitchAmount * 0.15f);
            _scannerFlatHologramScanline.color = lineColor;
            _scannerFlatHologramScanline.rectTransform.anchoredPosition = new Vector2(
                math.lerp(size.x * -0.42f, size.x * 0.42f, signal01),
                0f);

            SetScannerFlatHologramVisible(true);
        }

        private bool HasActiveScannerHologramSnapshot()
        {
            return TryGetActiveScannerHologramSnapshot(out _);
        }

        private bool TryGetActiveScannerHologramSnapshot(out ScannerTool.ScientificScanSnapshot snapshot)
        {
            snapshot = default;
            if (!Application.isPlaying ||
                _toolManager == null ||
                !(_toolManager.CurrentTool is ScannerTool scanner) ||
                !scanner.TryGetScientificScanSnapshot(out snapshot))
            {
                return false;
            }

            return HasScannerHologramPayload(snapshot);
        }

        private static bool HasScannerHologramPayload(ScannerTool.ScientificScanSnapshot snapshot)
        {
            return snapshot.IsActive != 0 &&
                (snapshot.Fragment != null ||
                 snapshot.ProxyMeshIndex >= 0 ||
                 snapshot.MaterialClass != ScannerTool.ScientificMaterialClass.None ||
                 snapshot.HasFaunaContact != 0 ||
                 snapshot.ChemicalLoad01 > ScannerEvidenceEpsilon ||
                 snapshot.Toxicity01 > ScannerEvidenceEpsilon ||
                 snapshot.OrganicBlood01 > ScannerTraceEvidenceThreshold01 ||
                 snapshot.HasAttractantTrace != 0);
        }

        private static float ResolveScannerHologramSignal01(ScannerTool.ScientificScanSnapshot snapshot)
        {
            float signal01 = math.saturate(snapshot.Progress01);
            signal01 = math.max(signal01, math.saturate(snapshot.Density01));
            signal01 = math.max(signal01, math.saturate(snapshot.ChemicalLoad01));
            signal01 = math.max(signal01, math.saturate(snapshot.Toxicity01));
            signal01 = math.max(signal01, math.saturate(snapshot.OrganicBlood01));
            signal01 = math.max(signal01, math.saturate(snapshot.AttractantScent01));
            if (snapshot.Fragment != null || snapshot.ProxyMeshIndex >= 0)
                signal01 = math.max(signal01, ScannerHologramFragmentSignalFloor);

            if (snapshot.MaterialClass != ScannerTool.ScientificMaterialClass.None)
                signal01 = math.max(signal01, ScannerHologramMaterialSignalFloor);

            if (snapshot.ChemicalLoad01 > ScannerEvidenceEpsilon ||
                snapshot.Toxicity01 > ScannerEvidenceEpsilon ||
                snapshot.OrganicBlood01 > ScannerTraceEvidenceThreshold01 ||
                snapshot.HasAttractantTrace != 0)
            {
                signal01 = math.max(signal01, ScannerHologramTraceSignalFloor);
            }

            if (snapshot.HasFaunaContact != 0)
                signal01 = math.max(signal01, ScannerHologramFaunaSignalFloor);

            return signal01;
        }

        private void RenderScannerInterference(float deltaTime)
        {
            if (!_scannerInterferenceActive)
            {
                SetScannerFlatHologramVisible(false);
                return;
            }

            _scannerInterferencePhase += math.max(0f, deltaTime);
            Vector2 size = ResolveScannerFlatHologramSize();
            float pulsePixels = EvaluateCheapSignedWave(_scannerInterferencePhase * 31.0f) * math.max(1f, scannerHologramPulsePixels);
            float jitterPixels = EvaluateCheapSignedWave(_scannerInterferencePhase * 71.0f) * math.max(1f, scannerHologramJitterPixels);
            _scannerFlatHologramRoot.anchoredPosition = scannerHologramOffsetPixels + new Vector2(jitterPixels, pulsePixels * 0.35f);
            _scannerFlatHologramRoot.sizeDelta = size + (Vector2.one * math.abs(pulsePixels));
            _scannerFlatHologramRoot.localEulerAngles = Vector3.zero;

            float band01 = EvaluateCheapPulse01(_scannerInterferencePhase * 43.0f);
            Color bodyColor = scannerHologramScanStartColor;
            bodyColor.a = math.saturate(0.34f + band01 * 0.24f);
            _scannerFlatHologramBody.color = bodyColor;

            Color coreColor = scannerHologramScanCompleteColor;
            coreColor.a = math.saturate(0.54f + band01 * 0.28f);
            _scannerFlatHologramCore.color = coreColor;

            Color lineColor = scannerHologramScanCompleteColor;
            lineColor.a = math.saturate(0.64f + band01 * 0.28f);
            _scannerFlatHologramScanline.color = lineColor;
            _scannerFlatHologramScanline.rectTransform.anchoredPosition = new Vector2(
                math.lerp(size.x * -0.45f, size.x * 0.45f, band01),
                0f);

            SetScannerFlatHologramVisible(true);
        }

        private Vector2 ResolveScannerFlatHologramSize()
        {
            return new Vector2(
                math.max(24f, scannerHologramSizePixels.x),
                math.max(18f, scannerHologramSizePixels.y));
        }

        private void SetScannerFlatHologramVisible(bool visible)
        {
            if (_scannerFlatHologramCanvasGroup == null)
                return;

            float targetAlpha = visible ? 1f : 0f;
            if (math.abs(_scannerFlatHologramCanvasGroup.alpha - targetAlpha) <= 0.001f)
                return;

            _scannerFlatHologramCanvasGroup.alpha = targetAlpha;
            _scannerFlatHologramCanvasGroup.blocksRaycasts = false;
            _scannerFlatHologramCanvasGroup.interactable = false;
        }

        private void RenderThreatChevrons()
        {
            if (IsStencilRenderGraphSuppressedRuntime())
                return;

            if (!Application.isPlaying ||
                renderPath != RenderPath.ProjectionSource ||
                projectionCamera == null)
            {
                return;
            }

            if (_threatChevronActiveMask == 0u)
            {
                _threatChevronVisibleCount = 0;
                return;
            }

            MaterialPropertyBlock threatChevronPropertyBlock = _threatChevronPropertyBlock;
            if (threatChevronMaterial == null || _threatChevronMesh == null || threatChevronPropertyBlock == null)
                return;

            int visibleCount = BuildThreatChevronMatrices();
            _threatChevronVisibleCount = visibleCount;
            if (visibleCount <= 0)
                return;

            for (int visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                float alpha01 = math.saturate(_threatChevronAlphaMirror[visibleIndex]);
                float pulse01 = 0.74f + (0.26f * EvaluateCheapPulse01((_threatChevronPulseTime * threatChevronFlickerFrequency * 0.75f) + (visibleIndex * 0.91f)));
                alpha01 *= pulse01;
                Vector4 instanceData = _threatChevronInstanceDataMirror[visibleIndex];
                instanceData.x = alpha01;
                instanceData.y = 1f;
                instanceData.z = 0f;
                instanceData.w = 0f;
                _threatChevronInstanceDataMirror[visibleIndex] = instanceData;
            }

            ApplyThreatChevronMaterialProperties();
            threatChevronPropertyBlock.SetVectorArray(_ThreatChevronInstanceDataId, _threatChevronInstanceDataMirror);

            UnityEngine.Graphics.DrawMeshInstanced(
                _threatChevronMesh,
                0,
                threatChevronMaterial,
                _threatChevronMatrixMirror,
                visibleCount,
                threatChevronPropertyBlock,
                ShadowCastingMode.Off,
                false,
                HudInternalLayerIndex,
                projectionCamera,
                LightProbeUsage.Off);
        }

        private void ApplyThreatChevronMaterialProperties()
        {
            if (threatChevronMaterial == null)
                return;

            ApplyThreatChevronColor(
                _ThreatChevronBaseColorId,
                threatChevronColor,
                ref _appliedThreatChevronColor,
                ref _hasAppliedThreatChevronColor);
            ApplyThreatChevronFloat(
                _ThreatChevronFlickerFrequencyId,
                math.max(0f, threatChevronFlickerFrequency),
                ref _appliedThreatChevronFlickerFrequency,
                ref _hasAppliedThreatChevronFlickerFrequency);
            ApplyThreatChevronFloat(
                _ThreatChevronFlickerIntensityId,
                math.saturate(threatChevronFlickerIntensity),
                ref _appliedThreatChevronFlickerIntensity,
                ref _hasAppliedThreatChevronFlickerIntensity);
            ApplyThreatChevronFloat(
                _ThreatChevronFillAlphaId,
                math.saturate(threatChevronFillAlpha),
                ref _appliedThreatChevronFillAlpha,
                ref _hasAppliedThreatChevronFillAlpha);
        }

        private void ApplyThreatChevronColor(int propertyId, Color value, ref Color cachedValue, ref bool hasCachedValue)
        {
            if (hasCachedValue && cachedValue == value)
                return;

            MaterialPropertyBlock threatChevronPropertyBlock = _threatChevronPropertyBlock;
            if (threatChevronPropertyBlock == null)
                return;

            threatChevronPropertyBlock.SetColor(propertyId, value);
            cachedValue = value;
            hasCachedValue = true;
        }

        private void ApplyThreatChevronFloat(int propertyId, float value, ref float cachedValue, ref bool hasCachedValue)
        {
            if (hasCachedValue && math.abs(cachedValue - value) <= MaterialFloatWriteEpsilon)
                return;

            MaterialPropertyBlock threatChevronPropertyBlock = _threatChevronPropertyBlock;
            if (threatChevronPropertyBlock == null)
                return;

            threatChevronPropertyBlock.SetFloat(propertyId, value);
            cachedValue = value;
            hasCachedValue = true;
        }

        private void InvalidateThreatChevronMaterialState()
        {
            _hasAppliedThreatChevronColor = false;
            _hasAppliedThreatChevronFlickerFrequency = false;
            _hasAppliedThreatChevronFlickerIntensity = false;
            _hasAppliedThreatChevronFillAlpha = false;
        }

        private int BuildThreatChevronMatrices()
        {
            uint activeMask = _threatChevronActiveMask;
            if (activeMask == 0u || projectionCamera == null)
                return 0;

            Transform cameraTransform = projectionCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            if (!TryResolveRuntimeAup(cameraPosition, out AbsoluteUniversePosition cameraAup))
                return 0;

            float projectionDistance = ResolveProjectionPlaneDistance() + ThreatChevronPlaneBiasMeters;
            float halfFovRadians = math.max(0.001f, projectionCamera.fieldOfView * DegreesToHalfRadians);
            float frustumHalfHeight = ApproximateTanPositive(halfFovRadians) * projectionDistance;
            float frustumHalfWidth = frustumHalfHeight * math.max(0.0001f, projectionCamera.aspect);
            float invPixelHeight = math.rcp(math.max(1f, projectionCamera.pixelHeight));
            float worldPerPixel = frustumHalfHeight * 2f * invPixelHeight;
            float insetWorld = math.max(0f, threatChevronEdgeInsetPixels) * worldPerPixel;
            float safeHalfWidth = math.max(worldPerPixel, frustumHalfWidth - insetWorld);
            float safeHalfHeight = math.max(worldPerPixel, frustumHalfHeight - insetWorld);
            float chevronScaleWorld = math.max(0.0001f, math.max(4f, threatChevronSizePixels) * worldPerPixel);
            int visibleCount = 0;

            while (activeMask != 0u)
            {
                int i = (int)math.tzcnt(activeMask);
                activeMask &= activeMask - 1u;

                if (!TryBuildThreatChevronMatrix(
                    cameraTransform,
                    cameraPosition,
                    in cameraAup,
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

                _threatChevronMatrixMirror[visibleCount] = matrix;
                _threatChevronAlphaMirror[visibleCount] = alpha01;
                visibleCount++;
            }

            return visibleCount;
        }

        private bool TryBuildThreatChevronMatrix(
            Transform cameraTransform,
            Vector3 cameraPosition,
            in AbsoluteUniversePosition cameraAup,
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
            float3 threatRelative = AupPrecisionMath.LocalDeltaFloat3Clamped(
                threatState.PositionAup.ToAbsoluteDouble3(),
                cameraAup.ToAbsoluteDouble3(),
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                float3.zero);
            float3 cameraForwardF3 = math.float3(cameraTransform.forward.x, cameraTransform.forward.y, cameraTransform.forward.z);
            float3 cameraRightF3 = math.float3(cameraTransform.right.x, cameraTransform.right.y, cameraTransform.right.z);
            float3 cameraUpF3 = math.float3(cameraTransform.up.x, cameraTransform.up.y, cameraTransform.up.z);
            Vector3 localThreatPosition = new Vector3(
                math.dot(threatRelative, cameraRightF3),
                math.dot(threatRelative, cameraUpF3),
                math.dot(threatRelative, cameraForwardF3));
            bool behind = localThreatPosition.z <= 0.001f;
            if (behind)
            {
                localThreatPosition.x = -localThreatPosition.x;
                localThreatPosition.y = -localThreatPosition.y;
                localThreatPosition.z = math.abs(localThreatPosition.z) + 0.001f;
            }

            if (math.abs(localThreatPosition.x) <= 0.0001f && math.abs(localThreatPosition.y) <= 0.0001f)
                return false;

            float projectionScale = projectionDistance * math.rcp(math.max(0.001f, localThreatPosition.z));
            Vector2 projectedPlanePosition = new Vector2(localThreatPosition.x * projectionScale, localThreatPosition.y * projectionScale);
            Vector2 clampedPlanePosition = ClampToThreatBounds(projectedPlanePosition, safeHalfWidth, safeHalfHeight);
            float2 direction2DF3 = new float2(clampedPlanePosition.x, clampedPlanePosition.y);
            float direction2DLengthSq = math.lengthsq(direction2DF3);
            if (direction2DLengthSq <= 0.000001f)
                return false;

            Vector3 worldPosition =
                cameraPosition +
                (cameraTransform.forward * projectionDistance) +
                (cameraTransform.right * clampedPlanePosition.x) +
                (cameraTransform.up * clampedPlanePosition.y);

            float threatDirectionLengthSq = math.lengthsq(threatRelative);
            float forwardComponent = behind ? 0f : math.max(0f, localThreatPosition.z);
            float forwardDot01 = threatDirectionLengthSq > 0.000001f
                ? math.saturate((forwardComponent * forwardComponent) * math.rcp(threatDirectionLengthSq))
                : 1f;
            Quaternion worldRotation = cameraTransform.rotation * ResolveApproxThreatChevronRoll(clampedPlanePosition);
            float behindFade = behind ? 0.35f : 1f;
            float threatFade = FastInverseLerp01(threatChevronThreshold, 1f, threatState.Threat01);
            float invSafeHalfWidth = math.rcp(math.max(0.0001f, safeHalfWidth));
            float invSafeHalfHeight = math.rcp(math.max(0.0001f, safeHalfHeight));
            float edgeDistance01 = math.saturate(math.max(
                math.abs(clampedPlanePosition.x) * invSafeHalfWidth,
                math.abs(clampedPlanePosition.y) * invSafeHalfHeight));
            float edgeFade = math.lerp(0.72f, 1f, edgeDistance01);
            float threatScale = math.lerp(0.72f, 1.15f, math.saturate(threatState.Threat01)) * behindFade;
            float rotationScaleBias = 1f + (1f - forwardDot01) * 0.04f;
            Vector3 worldScale = new Vector3(
                chevronScaleWorld * threatScale * rotationScaleBias,
                chevronScaleWorld * threatScale,
                chevronScaleWorld * threatScale);
            alpha01 = math.saturate(math.max(0.16f, threatFade * edgeFade * behindFade));
            matrix = Matrix4x4.TRS(worldPosition, worldRotation, worldScale);
            return true;
        }

        private static Quaternion ResolveApproxThreatChevronRoll(Vector2 direction)
        {
            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            if (absX <= 0.000001f && absY <= 0.000001f)
                return s_threatChevronRollByDominantAxis[ThreatChevronRollRight];

            bool horizontal = absX >= absY;
            float signedAxis = math.select(direction.y, direction.x, horizontal);
            int horizontalBit = math.select(0, 1, horizontal);
            int negativeBit = math.select(0, 1, signedAxis < 0f);
            int rollIndex = (horizontalBit ^ 1) | (negativeBit << 1);
            return s_threatChevronRollByDominantAxis[rollIndex];
        }

        private void ApplyAcousticRadarVisuals(Color primary, Color warning, float corruptionIntensity)
        {
            if (IsStencilRenderGraphSuppressedRuntime())
                return;

            if (_acousticRadarOverlay == null)
                return;

            if (acousticRadarMaterial == null)
            {
                _acousticRadarOverlay.enabled = false;
                return;
            }

            BindAcousticRadarOverlayMaterial();

            float overlayOpacity = math.saturate(acousticRadarOpacity * math.lerp(0.2f, 1f, _acousticRadarPeakIntensity));
            bool visible = overlayOpacity > 0.001f && _acousticRadarPeakIntensity > 0.001f;
            _acousticRadarOverlay.enabled = visible;
            if (!visible)
                return;

            Color overlayColor = Alpha(primary, overlayOpacity);
            if (!_hasAppliedAcousticRadarOverlayColor || _appliedAcousticRadarOverlayColor != overlayColor)
            {
                _acousticRadarOverlay.color = overlayColor;
                _appliedAcousticRadarOverlayColor = overlayColor;
                _hasAppliedAcousticRadarOverlayColor = true;
            }

            Vector4 tuning0 = new Vector4(
                math.clamp(FiniteOr(acousticRadarInnerEdge, 0.74f), 0.05f, 0.95f),
                math.clamp(FiniteOr(acousticRadarBandThickness, 0.18f), 0.01f, 0.49f),
                math.max(0f, FiniteOr(acousticRadarWaveAmplitude, 2.4f)),
                math.max(0f, FiniteOr(acousticRadarPulseFrequency, 3.2f)));
            Vector4 tuning1 = new Vector4(
                math.saturate(FiniteOr(acousticRadarGlitchStrength, 0.2f) + FiniteOr(corruptionIntensity, 0f) * 0.42f),
                math.saturate(_acousticRadarPeakIntensity),
                math.saturate(warning.r),
                math.saturate(warning.g));
            _acousticRadarOverlay.SetTuning(tuning0, tuning1);
        }

        private void ApplyAcousticRadarTextureBindingIfNeeded()
        {
            if (!_acousticRadarTextureBindingDirty || _acousticRadarOverlay == null)
                return;

            _acousticRadarOverlay.texture = _acousticRadarTexture;
            _acousticRadarTextureBindingDirty = false;
        }

        private void BindAcousticRadarOverlayMaterial()
        {
            if (_acousticRadarOverlay == null || acousticRadarMaterial == null || _acousticRadarOverlayMaterialBound)
                return;

            _acousticRadarOverlay.material = acousticRadarMaterial;
            _acousticRadarOverlayMaterialBound = true;
        }

        private void InvalidateAcousticRadarOverlayState()
        {
            _acousticRadarTextureBindingDirty = true;
            _hasAppliedAcousticRadarOverlayColor = false;
        }

        private static Vector2 ClampToThreatBounds(Vector2 projectedPlanePosition, float safeHalfWidth, float safeHalfHeight)
        {
            if (math.abs(projectedPlanePosition.x) <= safeHalfWidth &&
                math.abs(projectedPlanePosition.y) <= safeHalfHeight)
            {
                return projectedPlanePosition;
            }

            float tx = safeHalfWidth * math.rcp(math.max(0.0001f, math.abs(projectedPlanePosition.x)));
            float ty = safeHalfHeight * math.rcp(math.max(0.0001f, math.abs(projectedPlanePosition.y)));
            return projectedPlanePosition * math.min(tx, ty);
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

            if (_appliedLayoutRevision != LayoutRevision)
            {
                _layoutBuilt = false;
                _appliedLayoutRevision = LayoutRevision;
                InvalidateVisualCaches();
            }

            EnsureRootCanvasGroup();
            if (_layoutBuilt)
            {
                EnsureActionProgressHudBinding(_root);
                SetRootVisible(true);
                return;
            }

            // Keep the projection root hidden until the first full visual pass
            // has restyled the default white UI primitives.
            SetRootVisible(false);

            ClearChildren(_root);

            _biosBackdrop = CreateImage("BiosBackdrop", _root, Color.black);
            Stretch(_biosBackdrop.rectTransform, 0f, 0f, 0f, 0f);
            _biosBackdrop.raycastTarget = false;
            _appliedBiosBackdropColor = new Color(0f, 0f, 0f, 0f);
            _biosBackdrop.color = _appliedBiosBackdropColor;
            _hasAppliedBiosBackdropColor = true;
            ApplyDitheredBackgroundMaterial(_biosBackdrop);

            _acousticRadarOverlay = CreateAcousticRadarImage("AcousticRadarOverlay", _root, Color.white);
            Stretch(_acousticRadarOverlay.rectTransform, 0f, 0f, 0f, 0f);
            _acousticRadarOverlay.raycastTarget = false;
            _acousticRadarOverlay.enabled = false;

            _ornamentRoot = CreateRect("OrnamentRoot", _root);
            Stretch(_ornamentRoot, 0f, 0f, 0f, 0f);

            _topVeil = CreateImage("TopVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.08f));
            _topVeil.rectTransform.anchorMin = new Vector2(0f, 1f);
            _topVeil.rectTransform.anchorMax = new Vector2(1f, 1f);
            _topVeil.rectTransform.pivot = new Vector2(0.5f, 1f);
            _topVeil.rectTransform.sizeDelta = new Vector2(0f, 92f);
            _topVeil.rectTransform.anchoredPosition = Vector2.zero;
            ApplyDitheredBackgroundMaterial(_topVeil);

            _bottomVeil = CreateImage("BottomVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.1f));
            _bottomVeil.rectTransform.anchorMin = new Vector2(0f, 0f);
            _bottomVeil.rectTransform.anchorMax = new Vector2(1f, 0f);
            _bottomVeil.rectTransform.pivot = new Vector2(0.5f, 0f);
            _bottomVeil.rectTransform.sizeDelta = new Vector2(0f, 144f);
            _bottomVeil.rectTransform.anchoredPosition = Vector2.zero;
            ApplyDitheredBackgroundMaterial(_bottomVeil);

            _leftVeil = CreateImage("LeftVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.05f));
            Anchor(_leftVeil.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(72f, 0f));
            ApplyDitheredBackgroundMaterial(_leftVeil);

            _rightVeil = CreateImage("RightVeil", _ornamentRoot, new Color(0f, 0f, 0f, 0.05f));
            Anchor(_rightVeil.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(72f, 0f));
            ApplyDitheredBackgroundMaterial(_rightVeil);

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

            // VISION_LOCKS: visor glass owns only Oxygen + Depth/Pressure + hazard warnings.
            // Depth and Pressure are first-class critical metrics on telemetryRoot (not secondary chrome).
            _depthLabel = CreateText("DepthLabel", _telemetryRoot, 28f, FontStyles.Bold, TextAlignmentOptions.Right, 0.96f);
            Anchor(_depthLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -24f), new Vector2(152f, 34f));

            _pressureLabel = CreateText("PressureLabel", _telemetryRoot, 16f, FontStyles.Bold, TextAlignmentOptions.Right, 0.78f);
            Anchor(_pressureLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -52f), new Vector2(152f, 22f));

            _telemetrySupplementRoot = CreateRect("TelemetrySupplementRoot", _telemetryRoot);
            Stretch(_telemetrySupplementRoot, 0f, 0f, 0f, 0f);

            _temperatureLabel = CreateText("TemperatureLabel", _telemetrySupplementRoot, 14f, FontStyles.Normal, TextAlignmentOptions.Right, 0.82f);
            Anchor(_temperatureLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(152f, 20f));

            _loadLabel = CreateText("LoadLabel", _telemetrySupplementRoot, 12f, FontStyles.Bold, TextAlignmentOptions.Right, 0.72f);
            Anchor(_loadLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, -18f), new Vector2(152f, 18f));

            _statusLabel = CreateText("StatusLabel", _root, 16f, FontStyles.Bold, TextAlignmentOptions.Center, 0.84f);
            Anchor(_statusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), statusOffset, new Vector2(420f, 24f));

            Vector2 resolvedGaugeClusterOffset = ResolveGaugeClusterOffset();
            Vector2 resolvedGaugeClusterSize = ResolveGaugeClusterSize();
            float resolvedGaugeColumnSpacing = ResolveGaugeColumnSpacing();

            _gaugeClusterRoot = CreateRect("GaugeClusterRoot", _root);
            Anchor(_gaugeClusterRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), resolvedGaugeClusterOffset, resolvedGaugeClusterSize);
            _gaugeClusterRoot.localEulerAngles = Vector3.zero;

            _oxygenGauge = CreateGauge("Gauge_O2", _gaugeClusterRoot, new Vector2(-resolvedGaugeColumnSpacing, 0f), oxygenIconTexture, _HudOxygenKeyHash, _oxygenGaugeValueBuffer);
            _healthGauge = CreateGauge("Gauge_HLT", _gaugeClusterRoot, Vector2.zero, healthIconTexture, _HudHullKeyHash, _healthGaugeValueBuffer);
            _powerGauge = CreateGauge("Gauge_PWR", _gaugeClusterRoot, new Vector2(resolvedGaugeColumnSpacing, 0f), energyIconTexture, _HudPowerKeyHash, _powerGaugeValueBuffer);

            _quickbarRoot = CreateRect("QuickbarRoot", _root);
            Anchor(_quickbarRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), quickbarOffset, quickbarSize);
            _quickbarCanvasGroup = EnsureCanvasGroup(_quickbarRoot);
            BuildQuickbarHierarchy(_quickbarRoot);

            BuildSavingProgressHierarchy(_root);
            BuildScannerFlatHologramHierarchy(_root);
            EnsureActionProgressHudBinding(_root);

            EnsureIsolatedDynamicCanvas(_reticleRoot, DynamicCanvasCadenceBucket.HighCadence);
            // Depth + Pressure are first-class critical metrics: isolate each so canvas rebuilds stay local.
            EnsureIsolatedDynamicCanvas(_depthLabel.rectTransform, DynamicCanvasCadenceBucket.LowCadence);
            EnsureIsolatedDynamicCanvas(_pressureLabel.rectTransform, DynamicCanvasCadenceBucket.LowCadence);
            EnsureIsolatedDynamicCanvas(_telemetrySupplementRoot, DynamicCanvasCadenceBucket.LowCadence);
            EnsureIsolatedDynamicCanvas(_statusLabel.rectTransform, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_oxygenGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_healthGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_powerGauge.Root, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_quickbarRoot, DynamicCanvasCadenceBucket.LowCadence);
            EnsureIsolatedDynamicCanvas(_savingProgressRoot, DynamicCanvasCadenceBucket.HighCadence);
            EnsureIsolatedDynamicCanvas(_scannerFlatHologramRoot, DynamicCanvasCadenceBucket.HighCadence);

            _ornamentCanvasGroup = EnsureCanvasGroup(_ornamentRoot);
            _headerCanvasGroup = EnsureCanvasGroup(_headerRoot);
            _telemetryChromeCanvasGroup = EnsureCanvasGroup(_telemetryChromeRoot);
            _telemetrySupplementCanvasGroup = EnsureCanvasGroup(_telemetrySupplementRoot);
            _statusCanvasGroup = EnsureCanvasGroup(_statusLabel.rectTransform);
            _quickbarCanvasGroup = EnsureCanvasGroup(_quickbarRoot);
            _savingProgressCanvasGroup = EnsureCanvasGroup(_savingProgressRoot);
            _scannerFlatHologramCanvasGroup = EnsureCanvasGroup(_scannerFlatHologramRoot);
            ApplySavingProgressCanvasState(0f);
            SetScannerFlatHologramVisible(false);

            _layoutBuilt = true;
        }

        private void EnsureActionProgressHudBinding(RectTransform parent)
        {
            if (parent == null)
                return;

            if (_actionProgressHudRoot == null)
                _actionProgressHudRoot = FindChildRect(parent, ActionProgressHudName);

            if (_actionProgressHudRoot == null)
            {
                _actionProgressHudRoot = CreateRect(ActionProgressHudName, parent);
                Anchor(_actionProgressHudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(118f, 78f));
            }
            else if (_actionProgressHudRoot.parent != parent)
            {
                _actionProgressHudRoot.SetParent(parent, false);
                Anchor(_actionProgressHudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(118f, 78f));
            }

            _actionProgressHudRoot.SetAsLastSibling();
            if (!_actionProgressHudRoot.TryGetComponent(out _actionProgressHud))
                // COLD ALLOC: ActionProgressHUD[1] - delayed player action progress binding in the live suit HUD - owner: SuitHUDV4CanvasOverlay
                _actionProgressHud = _actionProgressHudRoot.gameObject.AddComponent<ActionProgressHUD>();

            CanvasGroup canvasGroup = EnsureCanvasGroup(_actionProgressHudRoot);
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureRootCanvasGroup()
        {
            if (_root == null)
                return;

            if (_rootCanvasGroup == null)
            {
                _root.TryGetComponent(out _rootCanvasGroup);
                if (_rootCanvasGroup == null)
                    // COLD ALLOC: CanvasGroup[1] - HUD root visibility latch bootstrap - owner: SuitHUDV4CanvasOverlay
                    _rootCanvasGroup = _root.gameObject.AddComponent<CanvasGroup>();

                _hasAppliedRootVisibility = false;
            }

            EnsureRootScissorMask();
        }

        private void EnsureRootScissorMask()
        {
            if (_root == null)
                return;

            EnsureGraphicCanvasRenderers(_root);

            if (_rootScissorMask == null)
                _root.TryGetComponent(out _rootScissorMask);

            if (_rootScissorMask == null)
                // COLD ALLOC: RectMask2D[1] - helmet-frame scissor mask bootstrap - owner: SuitHUDV4CanvasOverlay
                _rootScissorMask = _root.gameObject.AddComponent<RectMask2D>();

            _rootScissorMask.padding = new Vector4(
                HelmetScissorInsetPixelsX,
                HelmetScissorInsetPixelsY,
                HelmetScissorInsetPixelsX,
                HelmetScissorInsetPixelsY);
            _rootScissorMask.softness = Vector2Int.zero;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
                return null;

            target.TryGetComponent(out CanvasGroup canvasGroup);
            if (canvasGroup == null)
                // COLD ALLOC: CanvasGroup[1] - isolated HUD group visibility latch bootstrap - owner: SuitHUDV4CanvasOverlay
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();

            return canvasGroup;
        }

        private static void EnsureGraphicCanvasRenderers(RectTransform root)
        {
            if (root == null)
                return;

            EnsureGraphicCanvasRenderer(root);

            int childCount = root.childCount;
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                if (root.GetChild(childIndex) is RectTransform childRect)
                    EnsureGraphicCanvasRenderers(childRect);
            }
        }

        private static void EnsureGraphicCanvasRenderer(RectTransform target)
        {
            if (target == null)
                return;

            if (!target.TryGetComponent(out Graphic graphic) || graphic == null)
                return;

            if (!target.TryGetComponent(out CanvasRenderer canvasRenderer) || canvasRenderer == null)
                // COLD ALLOC: CanvasRenderer[1] - repairs authored/generated HUD Graphics before masks and isolated canvases touch them - owner: SuitHUDV4CanvasOverlay
                target.gameObject.AddComponent<CanvasRenderer>();
        }

        private static Canvas EnsureIsolatedDynamicCanvas(RectTransform target, DynamicCanvasCadenceBucket cadenceBucket)
        {
            if (target == null)
                return null;

            EnsureGraphicCanvasRenderers(target);

            target.TryGetComponent(out Canvas canvas);
            if (canvas == null)
                // COLD ALLOC: Canvas[1] - isolated dynamic HUD rebuild bucket bootstrap - owner: SuitHUDV4CanvasOverlay
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

            float targetAlpha = visible ? 1f : 0f;
            if (canvasGroup.alpha == targetAlpha &&
                canvasGroup.interactable == visible &&
                canvasGroup.blocksRaycasts == visible)
            {
                return;
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void ApplySavingProgressCanvasState(float alpha)
        {
            if (_savingProgressCanvasGroup == null)
                return;

            float targetAlpha = math.saturate(alpha);
            if (math.abs(_savingProgressCanvasGroup.alpha - targetAlpha) <= SavingProgressVisibleEpsilon &&
                !_savingProgressCanvasGroup.interactable &&
                !_savingProgressCanvasGroup.blocksRaycasts)
            {
                return;
            }

            _savingProgressCanvasGroup.alpha = targetAlpha;
            _savingProgressCanvasGroup.interactable = false;
            _savingProgressCanvasGroup.blocksRaycasts = false;
        }

        private void SetRootVisible(bool visible)
        {
            if (_root == null)
                return;

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

            return new Vector2(math.min(telemetryOffset.x, -226f), math.min(telemetryOffset.y, 126f));
        }

        private Vector2 ResolveTelemetrySize()
        {
            if (renderPath != RenderPath.ProjectionSource)
                return telemetrySize;

            return new Vector2(math.min(telemetrySize.x, 184f), math.min(telemetrySize.y, 124f));
        }

        private Vector2 ResolveGaugeClusterOffset()
        {
            return new Vector2(math.max(gaugeClusterOffset.x, 132f), math.max(gaugeClusterOffset.y, 104f));
        }

        private Vector2 ResolveGaugeClusterSize()
        {
            return new Vector2(math.max(gaugeClusterSize.x, 290f), math.max(gaugeClusterSize.y, 116f));
        }

        private float ResolveGaugeColumnSpacing()
        {
            return math.max(gaugeColumnSpacing, 78f);
        }

        private void ApplySectionVisibility(bool biosRecoveryMode)
        {
            // VISION_LOCKS L189: visor glass owns only Oxygen + Depth/Pressure + hazard warnings.
            // Non-critical chrome stays off the glass (header/compass, ornament veils, temp/load,
            // health/power rings, quickbar). Bios recovery keeps the same critical set.
            _ = biosRecoveryMode;
            SetCanvasGroupVisible(_ornamentCanvasGroup, false);
            SetCanvasGroupVisible(_headerCanvasGroup, false);
            SetCanvasGroupVisible(_telemetryChromeCanvasGroup, false);
            SetCanvasGroupVisible(_telemetrySupplementCanvasGroup, false);
            SetCanvasGroupVisible(_statusCanvasGroup, true);
            SetCanvasGroupVisible(_quickbarCanvasGroup, false);

            if (_oxygenGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_oxygenGauge.CanvasGroup, true);
            if (_healthGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_healthGauge.CanvasGroup, false);
            if (_powerGauge.CanvasGroup != null)
                SetCanvasGroupVisible(_powerGauge.CanvasGroup, false);
        }

        private void ApplyBiosBackdrop(bool biosRecoveryMode)
        {
            if (_biosBackdrop == null)
                return;

            Color targetColor = biosRecoveryMode
                ? new Color(0f, 0f, 0f, 0.84f)
                : new Color(0f, 0f, 0f, 0f);
            if (_hasAppliedBiosBackdropColor && _appliedBiosBackdropColor == targetColor)
                return;

            _biosBackdrop.color = targetColor;
            _appliedBiosBackdropColor = targetColor;
            _hasAppliedBiosBackdropColor = true;
        }

        private void UpdateSavingProgressHud(float deltaTime)
        {
            if (_savingProgressCanvasGroup == null)
                return;

            PollSavingProgressStatus();

            if (_savingProgressHidePending && (float)SystemDispatcher.CurrentUnscaledTimeSeconds >= _savingProgressHideNotBeforeTime)
                RequestSavingProgressHide();

            float safeDeltaTime = math.max(0f, deltaTime);
            float nextAlpha = MoveTowardsFast(
                _savingProgressAlpha,
                _savingProgressTargetAlpha,
                safeDeltaTime * SavingProgressFadeSpeed);

            if (math.abs(nextAlpha - _savingProgressAlpha) > SavingProgressVisibleEpsilon)
            {
                _savingProgressAlpha = nextAlpha;
                ApplySavingProgressCanvasState(nextAlpha);
            }

            if (nextAlpha <= SavingProgressVisibleEpsilon)
                return;

            // The tick is the only writer of the failure colours, so both the latch and its release repaint from
            // here. Doing it inside OnSaveEvent instead would write Image.color from the SaveEvents flush, which is
            // not this HUD's frame phase.
            bool failureLatched = _savingProgressFailureLatched;
            bool failureBlinkOn = failureLatched &&
                IsBlinkVisible(
                    (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                    SavingProgressFailureBlinkRadiansPerSecond);
            if (_hasAppliedSavingProgressFailureStyle &&
                _appliedSavingProgressFailureLatched == failureLatched &&
                _appliedSavingProgressFailureBlinkOn == failureBlinkOn)
            {
                return;
            }

            _savingProgressFailureBlinkOn = failureBlinkOn;
            RefreshSavingProgressStyleForStatus();
        }

        private static bool IsBlinkVisible(float elapsedTime, float frequency)
        {
            return EvaluateCheapSignedWave(elapsedTime * frequency) >= 0f;
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
            float oxygenFallback = hasSurvivalStats ? FiniteSaturate01(survival.OxygenNormalized, 1f) : 1f;
            float realOxygen = FiniteSaturate01(ReadHeadlessUIValue(UIValueSlotId.Oxygen01, oxygenFallback), oxygenFallback);
            realOxygen = math.min(realOxygen, ResolveAtmosphereRoomOxygen01(realOxygen));
            float oxygen = ResolveDearLieOxygenDisplay01(realOxygen);
            float powerFallback = hasSurvivalStats ? FiniteSaturate01(survival.EnergyNormalized, 1f) : 1f;
            float power = _biosRecoveryMode
                ? FiniteSaturate01(_traumaTransportPower01, powerFallback)
                : FiniteSaturate01(ReadHeadlessUIValue(UIValueSlotId.Power01, powerFallback), powerFallback);
            float healthFallback = hasSurvivalStats ? FiniteSaturate01(survival.IntegrityNormalized, 1f) : 1f;
            float health = FiniteSaturate01(ReadHeadlessUIValue(UIValueSlotId.Health01, healthFallback), healthFallback);
            health = math.min(health, FiniteSaturate01(_traumaHullIntegrity01, health));
            float depth = FiniteNonNegative(ReadHeadlessUIValue(UIValueSlotId.DepthMeters, _depthMeters), _depthMeters);
            float pressureFallback = survival != null ? FiniteAtLeast(survival.Pressure, 1f, 1f) : 1f + depth * 0.1f;
            float pressure = FiniteAtLeast(ReadHeadlessUIValue(UIValueSlotId.PressureAtm, pressureFallback), pressureFallback, 1f);
            float rawHeading = playerMovement != null ? playerMovement.CameraYaw : 0f;
            rawHeading = math.isfinite(rawHeading) ? rawHeading : 0f;
            float heading = rawHeading - (math.floor(rawHeading * 0.00277777778f) * 360f);
            float safeDepthFallback = hasSurvivalStats ? FiniteAtLeast(survival.Stats.SafeDepth, 50f, 1f) : 50f;
            float safeDepth = FiniteAtLeast(ReadHeadlessUIValue(UIValueSlotId.SafeDepthMeters, safeDepthFallback), safeDepthFallback, 1f);
            float safeDepthNormalized = ResolveSafeDepthNormalized(depth, safeDepth);
            float oxygenCurrentFallback = survival != null ? FiniteNonNegative(survival.Oxygen, oxygen * 100f) : oxygen * 100f;
            float energyCurrentFallback = survival != null ? FiniteNonNegative(survival.Energy, power * 100f) : power * 100f;
            float healthCurrentFallback = survival != null ? FiniteNonNegative(survival.Integrity, health * 100f) : health * 100f;
            float oxygenCurrent = FiniteNonNegative(ReadHeadlessUIValue(UIValueSlotId.OxygenCurrent, oxygenCurrentFallback), oxygenCurrentFallback);
            float energyCurrent = FiniteNonNegative(ReadHeadlessUIValue(UIValueSlotId.EnergyCurrent, energyCurrentFallback), energyCurrentFallback);
            float healthCurrent = FiniteNonNegative(ReadHeadlessUIValue(UIValueSlotId.IntegrityCurrent, healthCurrentFallback), healthCurrentFallback);
            float oxygenCurrentDisplay = ResolveDearLieOxygenCurrentDisplay(oxygenCurrent, realOxygen, oxygen);
            uint survivalStatusMask = ReadHeadlessSurvivalStatusMask(survival);
            float stressPulse = _biosRecoveryMode ? 0f : UpdateStressPulse(dt);
            if (_playerStateSignalDirty &&
                _playerStateState == PlayerStateSignal.StateSqueezing &&
                (_playerStateFlags & PlayerStateSignal.FlagSqueezing) != 0)
            {
                stressPulse = math.max(stressPulse, _playerStateIntensity01 * 0.35f);
            }

            Color pulsedPrimary = ResolveStressPulseColor(primary, warning, stressPulse, stressPulseBrightnessBoost, stressPulseWarningBlend);
            Color pulsedDim = ResolveStressPulseColor(dim, warning, stressPulse, stressPulseBrightnessBoost * 0.45f, stressPulseWarningBlend * 0.38f);
            Color pulsedWarning = ResolveStressPulseColor(warning, primary, stressPulse, stressPulseBrightnessBoost * 0.22f, 0f);
            ILocalizationStressPresentationReadModel manager = _localizationRuntime;
            float hullStressCorruptionIntensity = manager != null ? manager.GetHullStressCorruptionIntensity() : 0f;
            bool hullStressWhisperMode = !_biosRecoveryMode && ShouldUseHullStressWhisperMode(manager);
            float traumaCorruptionIntensity = _traumaGlitchIntensity > CorruptedModeThreshold ? _traumaGlitchIntensity : 0f;
            float displayCorruptionIntensity = math.max(hullStressCorruptionIntensity, traumaCorruptionIntensity);
            bool memorySubsystemBreachActive = TryResolveMemorySubsystemBreach(
                out char[] memorySubsystemBreachBuffer,
                out int memorySubsystemBreachLength,
                out int memorySubsystemBreachVersion);
            if (memorySubsystemBreachActive)
                displayCorruptionIntensity = math.max(displayCorruptionIntensity, 0.32f);

            bool corruptedMode = !_biosRecoveryMode && displayCorruptionIntensity > 0f;
            bool toolDepletedWarningActive = !_biosRecoveryMode && _toolDepletedWarningTimer > 0f;
            EvaluateCriticalHapticCoupling(realOxygen, power, health);
            HectonUnderwaterVisuals.TryPublishHudAverageLuminance(ResolveHudFogLuminance(oxygen, power, health, toolDepletedWarningActive, displayCorruptionIntensity));
            if (corruptedMode)
                _corruptionFrameVersion++;

            float targetTemp = EstimateTemperature(depth);
            _displayTemperature = math.lerp(_displayTemperature, targetTemp, ApproximateOneMinusExpNeg(4f * dt));
            _displayOxygen01 = DampHudValue(_displayOxygen01, oxygen, OxygenGaugeDamping, dt);
            _displayHealth01 = DampHudValue(_displayHealth01, health, HealthGaugeDamping, dt);
            _targetPower01 = power;
            UpdateHudProxyLightRegistration(_displayPower01, _displayOxygen01, stressPulse);
            float depthDelta = depth - _lastDepth;
            _lastDepth = depth;
            ApplySectionVisibility(_biosRecoveryMode);
            ApplyBiosBackdrop(_biosRecoveryMode);
            ApplyAcousticRadarVisuals(pulsedPrimary, pulsedWarning, displayCorruptionIntensity);
            ApplyStaticStyleIfNeeded(primary, secondary, dim, warning);
            ApplyStressPulseStyle(primary, warning, _biosRecoveryMode ? 0f : stressPulse);
            ApplyAnalogUiJitter(safeDepthNormalized, displayCorruptionIntensity, _biosRecoveryMode ? 0f : stressPulse);
            UpdateReticleSpread(dt);

            float localizedDepth = LocalizedMeasurementFormatter.ConvertDistanceMeters(depth, _localizedMeasurementLanguage);
            float localizedTemperature = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(_displayTemperature, _localizedMeasurementLanguage);
            bool localizedRtl = LocalizedMeasurementFormatter.IsRightToLeft(_localizedMeasurementLanguage);
            bool specialCadenceBypass = _biosRecoveryMode || hullStressWhisperMode || corruptedMode || memorySubsystemBreachActive;
            bool shouldRefreshSuitLabel = specialCadenceBypass || refreshSlowCadence || _appliedSuitLabelVersion == int.MinValue;
            bool shouldRefreshHeadingLabel = specialCadenceBypass || NeedsHeadingCadenceRefresh(refreshMediumCadence, heading);
            bool shouldRefreshTelemetryText = specialCadenceBypass || NeedsTelemetryCadenceRefresh(refreshMediumCadence, oxygen, depth, localizedTemperature, pressure);
            bool shouldRefreshGaugeText = specialCadenceBypass || NeedsGaugeCadenceRefresh(refreshMediumCadence, oxygen, power, health);

            SetRootVisible(true);
            bool shouldRefreshQuickbar = !_quickbarVisualsInitialized || _inventorySignalDirty || _inputStateSignalDirty || specialCadenceBypass || refreshMediumCadence;
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
                        int roundedHeading = RoundToIntFast(heading);
                        int headingVersion = roundedHeading % 360;
                        if (headingVersion < 0)
                            headingVersion += 360;

                        if (!ZeroGCFormatter.TryWriteCompassHeading(headingVersion, ResolveCardinal(headingVersion).AsSpan(), _headingDisplayBuffer.AsSpan(), out int headingLength))
                            headingLength = 0;

                        SetDisplayBufferIfChanged(_headingLabel, _headingDisplayBuffer, headingLength, false, Alpha(dim, 0.58f), headingVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 107, ref _appliedHeadingLabelVersion, ref _appliedHeadingLabelColor);
                        _lastStreamedHeadingDegrees = heading;
                    }
                }
            }

            if (shouldRefreshTelemetryText)
            {
                if (_biosRecoveryMode)
                {
                    // VISION_LOCKS: bios recovery keeps the critical set (O2 + Depth/Pressure + hazard status).
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    int roundedDepth = RoundToIntFast(localizedDepth);
                    int pressureTenths = RoundToIntFast(pressure * 10f);
                    ResolveMetricIntBuffer(_depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, roundedDepth, out char[] depthBuffer, out int depthLength);
                    ResolveMetricFloatTenthsBuffer(_pressureTemplateBuffer, _pressureTemplateLength, ref _pressureDisplayBuffer, pressure, out char[] pressureBuffer, out int pressureLength);
                    SetDisplayBufferIfChanged(_depthLabel, depthBuffer, depthLength, localizedRtl, Alpha(primary, 0.98f), roundedDepth, false, 0f, 0, 0, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetDisplayBufferIfChanged(_pressureLabel, pressureBuffer, pressureLength, localizedRtl, Alpha(primary, 0.78f), pressureTenths, false, 0f, 0, 0, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                    SetCanvasGroupVisible(_telemetrySupplementCanvasGroup, false);
                }
                else if (hullStressWhisperMode)
                {
                    SetDisplayBufferIfChanged(_depthLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedPrimary, 0.96f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 211, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetDisplayBufferIfChanged(_temperatureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedDim, 0.84f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 223, ref _appliedTemperatureWhisperVersion, ref _appliedTemperatureColor);
                    // Pressure is first-class (with Depth); keep it brighter than secondary chrome.
                    SetDisplayBufferIfChanged(_pressureLabel, hullStressWhisperBuffer, hullStressWhisperLength, localizedRtl, Alpha(pulsedPrimary, 0.78f), hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 227, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                    _hasAppliedDepthValue = false;
                    _hasAppliedTemperatureTenths = false;
                    _hasAppliedPressureTenths = false;
                }
                else if (corruptedMode)
                {
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    int roundedDepth = RoundToIntFast(localizedDepth);
                    int temperatureTenths = RoundToIntFast(localizedTemperature * 10f);
                    int pressureTenths = RoundToIntFast(pressure * 10f);
                    ResolveMetricIntBuffer(_depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, roundedDepth, out char[] depthBuffer, out int depthLength);
                    ResolveMetricFloatTenthsBuffer(_temperatureTemplateBuffer, _temperatureTemplateLength, ref _temperatureDisplayBuffer, localizedTemperature, out char[] temperatureBuffer, out int temperatureLength);
                    ResolveMetricFloatTenthsBuffer(_pressureTemplateBuffer, _pressureTemplateLength, ref _pressureDisplayBuffer, pressure, out char[] pressureBuffer, out int pressureLength);
                    SetDisplayBufferIfChanged(_depthLabel, depthBuffer, depthLength, localizedRtl, Alpha(pulsedPrimary, 0.96f), roundedDepth, true, displayCorruptionIntensity, _corruptionFrameVersion, 211, ref _appliedDepthWhisperVersion, ref _appliedDepthColor);
                    SetDisplayBufferIfChanged(_temperatureLabel, temperatureBuffer, temperatureLength, localizedRtl, Alpha(pulsedDim, 0.84f), temperatureTenths, true, displayCorruptionIntensity, _corruptionFrameVersion, 223, ref _appliedTemperatureWhisperVersion, ref _appliedTemperatureColor);
                    SetDisplayBufferIfChanged(_pressureLabel, pressureBuffer, pressureLength, localizedRtl, Alpha(pulsedPrimary, 0.78f), pressureTenths, true, displayCorruptionIntensity, _corruptionFrameVersion, 227, ref _appliedPressureWhisperVersion, ref _appliedPressureColor);
                    _hasAppliedDepthValue = false;
                    _hasAppliedTemperatureTenths = false;
                    _hasAppliedPressureTenths = false;
                }
                else
                {
                    _appliedDepthWhisperVersion = int.MinValue;
                    _appliedTemperatureWhisperVersion = int.MinValue;
                    _appliedPressureWhisperVersion = int.MinValue;
                    SetMetricIntTemplateIfChanged(_depthLabel, _depthTemplateBuffer, _depthTemplateLength, ref _depthDisplayBuffer, RoundToIntFast(localizedDepth), localizedRtl, Alpha(pulsedPrimary, 0.96f), ref _appliedDepthValue, ref _hasAppliedDepthValue, ref _appliedDepthColor);
                    SetMetricFloatTenthsTemplateIfChanged(_temperatureLabel, _temperatureTemplateBuffer, _temperatureTemplateLength, ref _temperatureDisplayBuffer, localizedTemperature, localizedRtl, Alpha(pulsedDim, 0.84f), ref _appliedTemperatureTenths, ref _hasAppliedTemperatureTenths, ref _appliedTemperatureColor);
                    SetMetricFloatTenthsTemplateIfChanged(_pressureLabel, _pressureTemplateBuffer, _pressureTemplateLength, ref _pressureDisplayBuffer, pressure, localizedRtl, Alpha(pulsedPrimary, 0.78f), ref _appliedPressureTenths, ref _hasAppliedPressureTenths, ref _appliedPressureColor);
                }

                _lastStreamedOxygen01 = oxygen;
                _lastStreamedDepthMeters = depth;
                _lastStreamedTemperature = localizedTemperature;
                _lastStreamedPressure = pressure;
            }

            UpdateEncumbranceReadout(dt, pulsedPrimary, pulsedDim, pulsedWarning, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion);

            Color statusColor = PickAccent(oxygen, power, health, safeDepthNormalized, pulsedPrimary, pulsedWarning);
            if (_biosRecoveryMode)
            {
                float criticalAlpha = IsBlinkVisible((float)SystemDispatcher.CurrentUnscaledTimeSeconds, 12f) ? 1f : 0.2f;
                LocNumericBuffer.Write(DefaultCriticalLabel.AsSpan(), out char[] criticalBuffer, out int criticalLength);
                SetDisplayBufferIfChanged(_statusLabel, criticalBuffer, criticalLength, false, Alpha(primary, criticalAlpha), 1, false, 0f, 0, 0, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                _hasAppliedStatusKeyHash = false;
            }
            else if (memorySubsystemBreachActive)
            {
                float breachAlpha = IsBlinkVisible((float)SystemDispatcher.CurrentUnscaledTimeSeconds, 18f) ? 1f : 0.28f;
                SetDisplayBufferIfChanged(
                    _statusLabel,
                    memorySubsystemBreachBuffer,
                    memorySubsystemBreachLength,
                    false,
                    new Color(1f, 0.08f, 0.06f, breachAlpha),
                    memorySubsystemBreachVersion,
                    false,
                    0f,
                    0,
                    0,
                    ref _appliedStatusWhisperVersion,
                    ref _appliedStatusLabelColor);
                _hasAppliedStatusKeyHash = false;
            }
            else if (toolDepletedWarningActive)
            {
                float depletedAlpha = IsBlinkVisible((float)SystemDispatcher.CurrentUnscaledTimeSeconds, 16f) ? 1f : 0.22f;
                LocNumericBuffer.Write(DefaultToolDepletedLabel.AsSpan(), out char[] depletedBuffer, out int depletedLength);
                SetDisplayBufferIfChanged(_statusLabel, depletedBuffer, depletedLength, false, Alpha(pulsedWarning, depletedAlpha), _toolDepletedVersion ^ _toolDepletedHashId, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 307, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                _hasAppliedStatusKeyHash = false;
            }
            else if (TryApplyHomeostasisStatusLabelOverride(pulsedWarning, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion))
            {
                _hasAppliedStatusKeyHash = false;
            }
            else if (TryApplyScannerStatusLabelOverride(statusColor, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion))
            {
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
                int statusKeyHash = ResolveStatusKeyHash(realOxygen, power, health, safeDepthNormalized, depth, safeDepth, depthDelta, survivalStatusMask);
                if (statusKeyHash == _StatusOxygenReserveLowKeyHash)
                    statusColor = Alpha(statusColor, statusColor.a * ResolveOxygenWarningBlinkAlpha());

                if (corruptedMode)
                {
                    ResolveLocalizedKeyBuffer(statusKeyHash, _localizationRuntime != null, out char[] statusBuffer, out int statusLength);
                    SetDisplayBufferIfChanged(_statusLabel, statusBuffer, statusLength, localizedRtl, statusColor, statusKeyHash, true, displayCorruptionIntensity, _corruptionFrameVersion, 307, ref _appliedStatusWhisperVersion, ref _appliedStatusLabelColor);
                    _hasAppliedStatusKeyHash = false;
                }
                else
                {
                    SetLocalizedKeyIfChanged(_statusLabel, statusKeyHash, _localizationRuntime != null, localizedRtl, statusColor, ref _appliedStatusKeyHash, ref _hasAppliedStatusKeyHash, ref _appliedStatusLabelColor);
                }
            }

            Color oxygenAccent = pulsedPrimary;
            Color healthAccent = LerpColor(pulsedPrimary, pulsedDim, 0.24f);
            Color energyAccent = LerpColor(pulsedPrimary, pulsedWarning, 0.28f);

            bool hasLocalizationRuntime = manager != null;
            UpdateGauge(ref _oxygenGauge, _displayOxygen01, oxygenCurrentDisplay, oxygenAccent, pulsedDim, pulsedWarning, localizedRtl, hasLocalizationRuntime, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 401, dt, OxygenGaugeDamping, shouldRefreshGaugeText);
            UpdateGauge(ref _healthGauge, _displayHealth01, healthCurrent, healthAccent, pulsedDim, pulsedWarning, localizedRtl, hasLocalizationRuntime, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 503, dt, HealthGaugeDamping, shouldRefreshGaugeText);
            UpdateGauge(ref _powerGauge, _displayPower01, energyCurrent, energyAccent, pulsedDim, pulsedWarning, localizedRtl, hasLocalizationRuntime, hullStressWhisperBuffer, hullStressWhisperLength, hullStressWhisperVersion, corruptedMode, displayCorruptionIntensity, _corruptionFrameVersion, 607, dt, BatteryGaugeDamping, shouldRefreshGaugeText);
            if (shouldRefreshGaugeText)
            {
                _lastStreamedPower01 = power;
                _lastStreamedHealth01 = health;
            }

            if (shouldRefreshQuickbar)
            {
                RefreshQuickbarVisuals(pulsedPrimary, pulsedDim, pulsedWarning);
                _quickbarVisualsInitialized = true;
                _inventorySignalDirty = false;
                _inputStateSignalDirty = false;
            }

            _playerStateSignalDirty = false;
            _systemHealthSignalDirty = false;
        }

        private void UpdateEncumbranceReadout(
            float deltaTime,
            Color primary,
            Color dim,
            Color warning,
            bool corruptedMode,
            float corruptionIntensity,
            int corruptionVersion)
        {
            if (_loadLabel == null)
                return;

            ResolveInventoryLoadValues(out float totalMassKg, out float carryCapacityKg, out float load01);
            Color loadColor = ResolveLoadBaseColor(primary, dim, warning, load01);

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                if (_appliedLoadColor != loadColor)
                {
                    _loadLabel.color = loadColor;
                    _appliedLoadColor = loadColor;
                    _hasAppliedLoadMassVertexColor = false;
                }

                UpdateLoadMassVertexWarning(deltaTime, load01, loadColor, warning);
                return;
            }

            try
            {
                if (!TryBuildLoadBuffer(lease.Buffer, totalMassKg, carryCapacityKg, out int length, out int version))
                {
                    if (!TryBuildLoadBuffer(_loadDisplayFallbackBuffer, totalMassKg, carryCapacityKg, out length, out version))
                        return;

                    _loadMassCharStart = 0;
                    _loadMassCharLength = length;
                    bool forceMeshUpdate = PrepareLoadMassVertexRefresh(version, loadColor, corruptedMode, corruptionVersion, 229);
                    SetDisplayBufferIfChanged(
                        _loadLabel,
                        _loadDisplayFallbackBuffer,
                        length,
                        false,
                        loadColor,
                        version,
                        corruptedMode,
                        corruptionIntensity,
                        corruptionVersion,
                        229,
                        ref _appliedLoadVersion,
                        ref _appliedLoadColor);
                    if (forceMeshUpdate)
                        _loadMassVertexRefreshDeferred = true;

                    UpdateLoadMassVertexWarning(deltaTime, load01, loadColor, warning);
                    return;
                }

                _loadMassCharStart = 0;
                _loadMassCharLength = length;
                bool forceLeaseMeshUpdate = PrepareLoadMassVertexRefresh(version, loadColor, corruptedMode, corruptionVersion, 229);
                SetDisplayBufferIfChanged(
                    _loadLabel,
                    lease.Buffer,
                    length,
                    false,
                    loadColor,
                    version,
                    corruptedMode,
                    corruptionIntensity,
                    corruptionVersion,
                    229,
                    ref _appliedLoadVersion,
                    ref _appliedLoadColor);
                if (forceLeaseMeshUpdate)
                    _loadMassVertexRefreshDeferred = true;

                UpdateLoadMassVertexWarning(deltaTime, load01, loadColor, warning);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void ResolveInventoryLoadValues(out float totalMassKg, out float carryCapacityKg, out float load01)
        {
            float fallbackMassKg = _playerInventory != null
                ? FiniteNonNegative(_playerInventory.TotalMassKg, 0f)
                : (survival != null ? FiniteNonNegative(survival.Weight, 0f) : 0f);
            float fallbackCapacityKg = survival != null && survival.Stats != null
                ? FiniteAtLeast(survival.Stats.CarryCapacityKg, 200f, 0.01f)
                : 200f;
            float fallbackLoad01 = playerMovement != null
                ? FiniteSaturate01(playerMovement.InventoryLoad01, 0f)
                : FiniteSaturate01(fallbackMassKg * math.rcp(fallbackCapacityKg), 0f);

            totalMassKg = FiniteNonNegative(ReadHeadlessUIValue(UIValueSlotId.InventoryMassKg, fallbackMassKg), fallbackMassKg);
            carryCapacityKg = FiniteAtLeast(ReadHeadlessUIValue(UIValueSlotId.CarryCapacityKg, fallbackCapacityKg), fallbackCapacityKg, 0.01f);
            load01 = FiniteSaturate01(ReadHeadlessUIValue(UIValueSlotId.InventoryLoad01, fallbackLoad01), fallbackLoad01);
        }

        private static float ReadHeadlessUIValue(UIValueSlotId slotId, float fallback)
        {
            fallback = FiniteOr(fallback, 0f);
            if (!UIStateStore.TryReadValue(slotId, out UIValueSlot valueSlot))
                return fallback;

            return math.isfinite(valueSlot.Value) ? valueSlot.Value : fallback;
        }

        private static float ResolveDearLieOxygenDisplay01(float realOxygen01)
        {
            float real = math.saturate(math.select(0f, realOxygen01, math.isfinite(realOxygen01)));
            float lowBandPhase = math.smoothstep(0f, 0.15f, real);
            float lowBand = real * (2f - lowBandPhase);
            return math.select(lowBand, real, real >= 0.15f);
        }

        private static float ResolveDearLieOxygenCurrentDisplay(float realOxygenCurrent, float realOxygen01, float displayOxygen01)
        {
            float current = FiniteNonNegative(realOxygenCurrent, 0f);
            float real = FiniteSaturate01(realOxygen01, 0f);
            float display = FiniteSaturate01(displayOxygen01, real);
            float scale = math.select(0f, display * math.rcp(math.max(real, 0.0001f)), real > 0.0001f);
            return current * scale;
        }

        private static uint ReadHeadlessSurvivalStatusMask(HectonSurvivalSystem survival)
        {
            uint fallback = survival != null ? survival.StatusMask : 0u;
            if (!UIStateStore.TryReadValue(UIValueSlotId.SurvivalStatusMask, out UIValueSlot valueSlot) ||
                !math.isfinite(valueSlot.Value) ||
                valueSlot.Value <= 0f)
            {
                return fallback;
            }

            uint mask = (uint)valueSlot.Value;
            return mask > 0xFFFFu ? 0xFFFFu : mask;
        }

        private static float ResolveAtmosphereRoomOxygen01(float fallback)
        {
            if (!UIStateStore.TryReadValue(UIValueSlotId.RoomOxygen01, out UIValueSlot valueSlot))
                return fallback;

            float age = (float)SystemDispatcher.CurrentUnscaledTimeSeconds - valueSlot.LastWriteUnscaledTime;
            if (!float.IsFinite(age) || age < 0f || age > AtmosphereRoomOxygenFreshSeconds)
                return fallback;

            return FiniteSaturate01(valueSlot.Value, fallback);
        }

        private void EvaluateCriticalHapticCoupling(float oxygen01, float power01, float health01)
        {
            TryConsumeHapticSlotVersion(UIValueSlotId.Oxygen01, ref _lastHapticOxygenVersion);
            TryConsumeHapticSlotVersion(UIValueSlotId.Power01, ref _lastHapticPowerVersion);
            TryConsumeHapticSlotVersion(UIValueSlotId.Health01, ref _lastHapticHealthVersion);

            byte criticalMask = ResolveCriticalHapticMask(oxygen01, power01, health01);
            byte enteredCriticalMask = (byte)(criticalMask & ~_activeCriticalHapticMask);
            _activeCriticalHapticMask = criticalMask;
            byte hapticMask = enteredCriticalMask;
            if ((criticalMask & CriticalMaskOxygen) != 0)
                hapticMask |= CriticalMaskOxygen;

            if (hapticMask == 0)
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextCriticalHapticTime)
                return;

            float oxygenCritical = math.select(0f, 1f, (hapticMask & CriticalMaskOxygen) != 0);
            float powerCritical = math.select(0f, 1f, (hapticMask & CriticalMaskPower) != 0);
            float healthCritical = math.select(0f, 1f, (hapticMask & CriticalMaskHealth) != 0);
            float lowMotor = math.saturate(math.max(oxygenCritical, healthCritical) * 0.78f + powerCritical * 0.18f);
            float highMotor = math.saturate(math.max(powerCritical, healthCritical) * 0.86f + oxygenCritical * 0.34f);

            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                lowMotor,
                highMotor,
                CriticalHapticDurationSeconds,
                CriticalHapticFrequencyHz,
                CriticalHapticPriority,
                BothMotorMask);
            _nextCriticalHapticTime = now + CriticalHapticCooldownSeconds;
        }

        internal static byte ResolveCriticalHapticMask(float oxygen01, float power01, float health01)
        {
            oxygen01 = FiniteSaturate01(oxygen01, 0f);
            power01 = FiniteSaturate01(power01, 0f);
            health01 = FiniteSaturate01(health01, 0f);

            byte criticalMask = 0;
            criticalMask |= (byte)math.select(0, CriticalMaskOxygen, oxygen01 < OxygenCriticalHapticThreshold);
            criticalMask |= (byte)math.select(0, CriticalMaskPower, power01 < PowerCriticalHapticThreshold);
            criticalMask |= (byte)math.select(0, CriticalMaskHealth, health01 < HealthCriticalHapticThreshold);
            return criticalMask;
        }

        private static bool TryConsumeHapticSlotVersion(UIValueSlotId slotId, ref uint lastVersion)
        {
            if (!UIStateStore.TryReadValue(slotId, out UIValueSlot slot) || slot.Version == lastVersion)
                return false;

            lastVersion = slot.Version;
            return true;
        }

        private static float ResolveHudFogLuminance(
            float oxygen01,
            float power01,
            float health01,
            bool toolDepletedWarningActive,
            float corruptionIntensity)
        {
            oxygen01 = FiniteSaturate01(oxygen01, 1f);
            power01 = FiniteSaturate01(power01, 1f);
            health01 = FiniteSaturate01(health01, 1f);
            float oxygenWarning = 1f - math.smoothstep(0.12f, 0.34f, oxygen01);
            float powerWarning = 1f - math.smoothstep(0.10f, 0.30f, power01);
            float healthWarning = 1f - math.smoothstep(0.14f, 0.36f, health01);
            float toolWarning = math.select(0f, 1f, toolDepletedWarningActive);
            float warningLuminance = math.max(math.max(oxygenWarning, powerWarning), math.max(healthWarning, toolWarning));
            return math.saturate(0.08f + warningLuminance * 0.72f + FiniteSaturate01(corruptionIntensity, 0f) * 0.2f);
        }

        private static float FiniteSaturate01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(FiniteOr(fallback, 0f));
        }

        private static float FiniteNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : math.max(0f, FiniteOr(fallback, 0f));
        }

        private static float FiniteAtLeast(float value, float fallback, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : math.max(minimum, FiniteOr(fallback, minimum));
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private bool PrepareLoadMassVertexRefresh(int version, Color loadColor, bool corruptedMode, int corruptionVersion, int corruptionSalt)
        {
            int displayVersion = corruptedMode
                ? unchecked((version * 397) ^ corruptionVersion ^ corruptionSalt)
                : version;
            bool textWillChange = _appliedLoadVersion != displayVersion;
            bool colorWillChange = _appliedLoadColor != loadColor;
            if (!textWillChange && !colorWillChange)
                return false;

            _hasAppliedLoadMassVertexColor = false;
            return textWillChange;
        }

        private void UpdateLoadMassVertexWarning(float deltaTime, float load01, Color loadBaseColor, Color warning)
        {
            if (_loadMassCharStart < 0 || _loadMassCharLength <= 0)
                return;

            if (_loadMassVertexRefreshDeferred)
            {
                _loadMassVertexRefreshDeferred = false;
                return;
            }

            if (load01 > 0.8f)
            {
                _loadMassPulsePhase = (float)SystemDispatcher.CurrentUnscaledTimeSeconds * 10f;
                ApplyLoadMassVertexColor(ResolveLoadMassPulseColor(warning, _loadMassPulsePhase));
                return;
            }

            _loadMassPulsePhase = 0f;
            ApplyLoadMassVertexColor((Color32)loadBaseColor);
        }

        private void ApplyLoadMassVertexColor(Color32 color)
        {
            if (_loadLabel == null || _loadMassCharStart < 0 || _loadMassCharLength <= 0)
                return;

            if (_hasAppliedLoadMassVertexColor && _appliedLoadMassVertexColor.Equals(color))
                return;

            TMP_TextInfo textInfo = _loadLabel.textInfo;
            if (textInfo == null || textInfo.characterCount <= 0 || _loadMassCharStart >= textInfo.characterCount)
                return;

            int end = math.min(textInfo.characterCount, _loadMassCharStart + _loadMassCharLength);
            for (int i = _loadMassCharStart; i < end; i++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
                if (!characterInfo.isVisible)
                    continue;

                int meshIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                Color32[] colors = textInfo.meshInfo[meshIndex].colors32;
                colors[vertexIndex] = color;
                colors[vertexIndex + 1] = color;
                colors[vertexIndex + 2] = color;
                colors[vertexIndex + 3] = color;
            }

            _loadLabel.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _appliedLoadMassVertexColor = color;
            _hasAppliedLoadMassVertexColor = true;
        }

        private static Color ResolveLoadBaseColor(Color primary, Color dim, Color warning, float load01)
        {
            if (load01 <= 0.8f)
                return Alpha(LerpColor(dim, primary, math.saturate(load01 * 0.65f)), 0.72f);

            return Alpha(warning, 0.95f);
        }

        private static Color32 ResolveLoadMassPulseColor(Color warning, float phase)
        {
            float pulse = EvaluateCheapPulse01(phase);
            Color32 warning32 = warning;
            byte greenBase = ToByteSaturate(math.max(96f, warning32.g * 0.55f));
            byte green = ToByteSaturate(math.lerp(greenBase, 168f, pulse));
            byte blue = ToByteSaturate(math.lerp(12f, 28f, pulse * 0.35f));
            byte alpha = ToByteSaturate(math.lerp(224f, 255f, pulse));
            return new Color32(255, green, blue, alpha);
        }

        private static bool TryBuildLoadBuffer(char[] buffer, float totalMassKg, float carryCapacityKg, out int length, out int version)
        {
            length = 0;
            version = 0;
            if (buffer == null || buffer.Length < 18)
                return false;

            int massTenthKg = RoundNonNegativeToInt(math.max(0f, totalMassKg) * 10f);
            float displayMassKg = massTenthKg * 0.1f;
            int cursor = 0;
            cursor = AppendChars(LoadPrefixChars, buffer, cursor);
            if (!displayMassKg.TryFormat(buffer.AsSpan(cursor), out int written, "F1"))
                return false;
            cursor += written;
            cursor = AppendChars(LoadKgSuffixChars, buffer, cursor);
            length = math.clamp(cursor, 0, buffer.Length);
            version = massTenthKg;
            return cursor <= buffer.Length;
        }

        private void RefreshDepthSignalSubscription()
        {
            if (ReferenceEquals(_depthSignalSource, survival))
                return;

            _depthSignalSource = survival;
            _depthSignalSourceId = ResolveSurvivalSignalSourceId(_depthSignalSource);
            _lastDepthSignalSequence = 0u;
            if (_depthSignalSource != null)
            {
                HandleDepthChanged(_depthSignalSource.Depth);
                return;
            }

            RefreshDepthFromMovementFallback();
        }

        private void ClearDepthSignalSubscription()
        {
            _depthSignalSource = null;
            _depthSignalSourceId = 0u;
            _lastDepthSignalSequence = 0u;
        }

        private void ConsumeDepthChangedSignals()
        {
            uint sourceId = _depthSignalSourceId;
            if (sourceId == 0u)
                return;

            ReadOnlySpan<SurvivalVitalsChangedSignal> signals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly SurvivalVitalsChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId)
                    continue;

                if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Depth) == 0u)
                    continue;

                if (signal.Sequence == 0u || signal.Sequence == _lastDepthSignalSequence)
                    continue;

                _lastDepthSignalSequence = signal.Sequence;
                if (_depthSignalSource != null)
                    HandleDepthChanged(_depthSignalSource.Depth);
            }
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private static uint FoldEntityIdToSourceId(ulong entityId)
        {
            uint hash = unchecked((uint)entityId ^ (uint)(entityId >> 32));
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash == 0u ? 1u : hash;
        }

        private void RefreshDepthFromMovementFallback()
        {
            if (_depthSignalSource == null)
                _depthMeters = ResolveHudDepthMeters(_depthMeters);
        }

        private void HandleDepthChanged(float depth)
        {
            _depthMeters = ResolveHudDepthMeters(depth);
        }

        private float ResolveHudDepthMeters(float fallbackDepth)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonPlayerMovement movement = playerMovement;
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

            return math.isfinite(fallbackDepth) ? math.max(0f, fallbackDepth) : 0f;
        }

        private bool NeedsHeadingCadenceRefresh(bool cadenceGateOpen, float headingDegrees)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedHeadingDegrees) ||
                   math.abs(Mathf.DeltaAngle(_lastStreamedHeadingDegrees, headingDegrees)) >= HeadingStreamThresholdDegrees;
        }

        private bool NeedsTelemetryCadenceRefresh(bool cadenceGateOpen, float oxygen01, float depthMeters, float localizedTemperature, float pressureAtm)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedOxygen01) ||
                   ExceedsCadenceDelta(oxygen01, _lastStreamedOxygen01, OxygenStreamThreshold01) ||
                   !float.IsFinite(_lastStreamedDepthMeters) ||
                   ExceedsCadenceDelta(depthMeters, _lastStreamedDepthMeters, DepthStreamThresholdMeters) ||
                   !float.IsFinite(_lastStreamedTemperature) ||
                   ExceedsCadenceDelta(localizedTemperature, _lastStreamedTemperature, TemperatureStreamThreshold) ||
                   !float.IsFinite(_lastStreamedPressure) ||
                   ExceedsCadenceDelta(pressureAtm, _lastStreamedPressure, PressureStreamThreshold);
        }

        private bool NeedsGaugeCadenceRefresh(bool cadenceGateOpen, float oxygen01, float power01, float health01)
        {
            if (!cadenceGateOpen)
                return false;

            return !float.IsFinite(_lastStreamedOxygen01) ||
                   ExceedsCadenceDelta(oxygen01, _lastStreamedOxygen01, OxygenStreamThreshold01) ||
                   !float.IsFinite(_lastStreamedPower01) ||
                   ExceedsCadenceDelta(power01, _lastStreamedPower01, OxygenStreamThreshold01) ||
                   !float.IsFinite(_lastStreamedHealth01) ||
                   ExceedsCadenceDelta(health01, _lastStreamedHealth01, OxygenStreamThreshold01);
        }

        private static bool ExceedsCadenceDelta(float current, float previous, float threshold)
        {
            if (!float.IsFinite(current) || !float.IsFinite(previous))
                return false;

            return math.abs(current - previous) > threshold + CadenceDeltaEpsilon;
        }

        private static float DampHudValue(float displayValue, float targetValue, float dampFactor, float dt)
        {
            if (math.abs(displayValue - targetValue) <= GaugeSmoothingEpsilon)
                return targetValue;

            float interpolation = math.saturate(math.max(0f, dampFactor) * math.max(0f, dt));
            return math.lerp(displayValue, targetValue, interpolation);
        }

        private static float ResolveOxygenWarningBlinkAlpha()
        {
            float blink01 = EvaluateCheapPulse01((float)SystemDispatcher.CurrentUnscaledTimeSeconds * 5f);
            return math.lerp(0.18f, 1f, blink01);
        }

        public void OnLocalizationCorruptionVisualStateChanged(in LocalizationEventPayload payload)

        {

            HandleCorruptionVisualStateChanged();

        }


        private void HandleCorruptionVisualStateChanged()
        {
            _cachedHullStressWhisperBucket = int.MinValue;
            _cachedHullStressWhisperLength = 0;
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
            float depth01 = math.saturate(depth * 0.000689655172f);
            float estimated = math.lerp(13.5f, 2.4f, depth01 * depth01 * (3f - 2f * depth01));
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

        private int ResolveStatusKeyHash(float oxygen, float power, float health, float safeDepthNormalized, float depth, float safeDepth, float depthDelta, uint survivalStatusMask)
        {
            if (safeDepthNormalized <= 0.08f || depth >= safeDepth)
                return _StatusPressureLimitExceededKeyHash;
            if (safeDepthNormalized <= 0.22f)
                return _StatusApproachingSafeDepthKeyHash;
            int physiologyStatusHash = ResolvePhysiologyStatusKeyHash(survivalStatusMask);
            if (physiologyStatusHash != 0)
                return physiologyStatusHash;
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

        private int ResolvePhysiologyStatusKeyHash(uint survivalStatusMask)
        {
            if (survivalStatusMask == 0u)
                return 0;

            int bitIndex = math.tzcnt(survivalStatusMask);
            switch (bitIndex)
            {
                case 0:
                case 6:
                    return _StatusPressureLimitExceededKeyHash;

                case 1:
                    return _StatusLampThermalLimitKeyHash;

                case 2:
                case 3:
                case 5:
                    return _StatusSuitDamageCriticalKeyHash;

                case 4:
                    return _StatusApproachingSafeDepthKeyHash;

                default:
                    return 0;
            }
        }

        private void RebuildLocalizationCache()
        {
            ILocalizationStressPresentationReadModel manager = _localizationRuntime;
            bool hasLocalizationRuntime = manager != null;
            _localizedMeasurementLanguage = manager != null ? (GameLanguage)manager.ActiveLanguageId : GameLanguage.English;
            // Depth is a non-negative magnitude (meters/feet below surface). No decorative leading minus —
            // a fixed "-" made the reading look like altitude/negative depth and confused players.
            BuildMetricTemplate(ref _depthTemplateBuffer, out _depthTemplateLength, _HudDepthKeyHash, ResolveDistanceUnitKeyHash(_localizedMeasurementLanguage), DepthNumberToken.AsSpan(), prependNegativeSign: false, hasLocalizationRuntime: hasLocalizationRuntime);
            BuildMetricTemplate(ref _temperatureTemplateBuffer, out _temperatureTemplateLength, _HudTemperatureKeyHash, ResolveTemperatureUnitKeyHash(_localizedMeasurementLanguage), FixedTenthsNumberToken.AsSpan(), prependNegativeSign: false, hasLocalizationRuntime: hasLocalizationRuntime);
            BuildMetricTemplate(ref _pressureTemplateBuffer, out _pressureTemplateLength, _HudPressureKeyHash, _HudAtmKeyHash, FixedTenthsNumberToken.AsSpan(), prependNegativeSign: false, hasLocalizationRuntime: hasLocalizationRuntime);
        }

        private static bool ShouldUseHullStressWhisperMode(ILocalizationStressPresentationReadModel manager)
        {
            return manager != null && manager.GetHullStressCorruptionIntensity() > 0.9f;
        }

        private void ResolveHullStressWhisperText(ILocalizationStressPresentationReadModel manager)
        {
            if (manager == null)
            {
                _cachedHullStressWhisperBucket = 0;
                CopyTextToBuffer(DefaultHullStressWhisper.AsSpan(), ref _cachedHullStressWhisperBuffer, out _cachedHullStressWhisperLength);
                return;
            }

            int bucket = manager.GetHullStressCorruptionBucket();
            if (_cachedHullStressWhisperBucket == bucket && _cachedHullStressWhisperLength > 0)
                return;

            _cachedHullStressWhisperBucket = bucket;
            if (!manager.TryGetHullStressHudWhisperBuffer(DefaultHullStressWhisper.AsSpan(), _cachedHullStressWhisperBuffer, out _cachedHullStressWhisperLength))
                CopyTextToBuffer(DefaultHullStressWhisper.AsSpan(), ref _cachedHullStressWhisperBuffer, out _cachedHullStressWhisperLength);
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

            return 1f - math.saturate(depth * math.rcp(safeDepth));
        }

        private void UpdateGauge(
            ref GaugeRefs gauge,
            float normalized,
            float currentValue,
            Color primary,
            Color dim,
            Color warning,
            bool localizedRtl,
            bool hasLocalizationRuntime,
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
            float clamped = FiniteSaturate01(normalized, 0f);
            currentValue = FiniteNonNegative(currentValue, 0f);
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
                if (math.abs(displayFill - clamped) <= GaugeSmoothingEpsilon)
                    displayFill = clamped;

                if (!gauge.HasCachedFillAmount || math.abs(gauge.CachedFillAmount - displayFill) > FillWriteEpsilon)
                {
                    gauge.RingFill.SetFillAmount(displayFill);
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
                        ResolveLocalizedKeyBuffer(gauge.LabelKeyHash, hasLocalizationRuntime, out char[] labelBuffer, out int labelLength);
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
                        SetLocalizedKeyIfChanged(gauge.Label, gauge.LabelKeyHash, hasLocalizationRuntime, localizedRtl, labelColor, ref gauge.CachedLabelKeyHash, ref gauge.HasCachedLabelKeyHash, ref gauge.CachedLabelColor);
                    }
                }
            }

            Color valueColor = Alpha(accent, 0.98f);
            int roundedValue = RoundToIntFast(currentValue);
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

            if (gauge.Sub != null)
                SetLocalizedRtlState(gauge.Sub, localizedRtl);
        }

        private GaugeRefs CreateGauge(string name, RectTransform parent, Vector2 anchoredPosition, Texture2D iconTexture, int labelKeyHash, char[] valueBuffer)
        {
            GaugeRefs refs = new GaugeRefs();
            refs.LabelKeyHash = labelKeyHash;
            refs.CachedLabelWhisperVersion = int.MinValue;
            refs.CachedValueWhisperVersion = int.MinValue;

            refs.Root = CreateRect(name, parent);
            Anchor(refs.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(86f, 92f));
            refs.CanvasGroup = EnsureCanvasGroup(refs.Root);

            RectTransform iconRect = CreateRect("GaugeIcon", refs.Root);
            Anchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 28f), gaugeIconSize);
            // COLD ALLOC: RawImage[1] - gauge icon hierarchy bootstrap - owner: SuitHUDV4CanvasOverlay
            refs.Icon = iconRect.gameObject.AddComponent<RawImage>();
            refs.Icon.texture = iconTexture;
            refs.Icon.raycastTarget = false;

            float resolvedRingSize = math.max(gaugeRingSize, 50f);
            float resolvedRingThicknessScale = math.clamp(gaugeRingThickness * math.rcp(resolvedRingSize), 0.02f, 0.45f);
            float resolvedFrameThicknessScale = math.clamp(resolvedRingThicknessScale * 0.35f, 0.01f, 0.24f);
            float resolvedValueOffsetY = math.clamp(gaugeValueOffsetY, -4f, 4f);
            float resolvedLabelOffsetY = math.clamp(gaugeLabelOffsetY, -42f, -24f);

            RectTransform backRect = CreateRect("GaugeRingBack", refs.Root);
            Anchor(backRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            // COLD ALLOC: GaugeRingGraphic[1] - gauge backdrop ring hierarchy bootstrap - owner: SuitHUDV4CanvasOverlay
            refs.RingBack = backRect.gameObject.AddComponent<GaugeRingGraphic>();
            refs.RingBack.Configure(false, resolvedRingThicknessScale, resolvedFrameThicknessScale);
            refs.RingBack.SetFillAmount(1f);
            refs.RingBack.raycastTarget = false;
            ApplyDitheredBackgroundMaterial(refs.RingBack);

            RectTransform fillRect = CreateRect("GaugeRingFill", refs.Root);
            Anchor(fillRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            // COLD ALLOC: GaugeRingGraphic[1] - gauge fill ring hierarchy bootstrap - owner: SuitHUDV4CanvasOverlay
            refs.RingFill = fillRect.gameObject.AddComponent<GaugeRingGraphic>();
            refs.RingFill.Configure(false, resolvedRingThicknessScale, resolvedFrameThicknessScale);
            refs.RingFill.SetFillAmount(1f);
            refs.RingFill.raycastTarget = false;

            RectTransform frameRect = CreateRect("GaugeRingFrame", refs.Root);
            Anchor(frameRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(resolvedRingSize, resolvedRingSize));
            // COLD ALLOC: GaugeRingGraphic[1] - gauge frame ring hierarchy bootstrap - owner: SuitHUDV4CanvasOverlay
            refs.RingFrame = frameRect.gameObject.AddComponent<GaugeRingGraphic>();
            refs.RingFrame.Configure(true, resolvedRingThicknessScale, resolvedFrameThicknessScale);
            refs.RingFrame.SetFillAmount(1f);
            refs.RingFrame.raycastTarget = false;

            refs.Label = CreateText("GaugeLabel", refs.Root, 10f, FontStyles.Bold, TextAlignmentOptions.Center, 0.82f, ResolveLabelFontAsset());
            Anchor(refs.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, resolvedLabelOffsetY), new Vector2(86f, 16f));
            TMP_TextRegistry.SetMetadata(refs.Label, labelKeyHash, LocLayer.Core);

            refs.Value = CreateText("GaugeValue", refs.Root, 15f, FontStyles.Bold, TextAlignmentOptions.Center, 0.98f, ResolveNumericFontAsset());
            Anchor(refs.Value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f + resolvedValueOffsetY), new Vector2(44f, 22f));
            refs.ValueBuffer = valueBuffer;

            refs.Sub = CreateText("GaugeSub", refs.Root, 10f, FontStyles.Normal, TextAlignmentOptions.Center, 0.52f, ResolveLabelFontAsset());
            Anchor(refs.Sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(86f, 12f));
            refs.SubCanvasGroup = EnsureCanvasGroup(refs.Sub.rectTransform);
            SetCanvasGroupVisible(refs.SubCanvasGroup, false);

            return refs;
        }

        private void BuildQuickbarHierarchy(RectTransform parent)
        {
            if (parent == null)
                return;

            float resolvedSlotSize = math.max(36f, quickbarSlotSize);
            float resolvedSlotGap = math.max(2f, quickbarSlotGap);
            float totalWidth = (QuickbarSlotCount * resolvedSlotSize) + ((QuickbarSlotCount - 1) * resolvedSlotGap);
            float startX = (-totalWidth * 0.5f) + (resolvedSlotSize * 0.5f);

            for (int slotIndex = 0; slotIndex < QuickbarSlotCount; slotIndex++)
            {
                QuickbarSlotRefs refs = new QuickbarSlotRefs();
                refs.Root = CreateRect("QuickbarSlot", parent);
                Anchor(
                    refs.Root,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + (slotIndex * (resolvedSlotSize + resolvedSlotGap)), 0f),
                    new Vector2(resolvedSlotSize, resolvedSlotSize));

                refs.Backdrop = CreateImage("Backdrop", refs.Root, new Color(0.04f, 0.1f, 0.12f, 0.55f));
                Stretch(refs.Backdrop.rectTransform, 0f, 0f, 0f, 0f);
                refs.Backdrop.raycastTarget = false;
                ApplyDitheredBackgroundMaterial(refs.Backdrop);

                refs.Accent = CreateImage("Accent", refs.Root, new Color(0.46f, 0.98f, 0.94f, 0f));
                Anchor(refs.Accent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(resolvedSlotSize - 8f, 2f));
                refs.Accent.raycastTarget = false;

                RectTransform iconRect = CreateRect("Icon", refs.Root);
                Stretch(iconRect, 7f, 7f, 7f, 7f);
                // COLD ALLOC: Image[1] - quickbar slot icon hierarchy bootstrap - owner: SuitHUDV4CanvasOverlay
                refs.Icon = iconRect.gameObject.AddComponent<Image>();
                refs.Icon.preserveAspect = true;
                refs.Icon.raycastTarget = false;
                refs.Icon.material = null;
                refs.Icon.maskable = false;
                refs.Icon.color = new Color(1f, 1f, 1f, 0f);

                refs.Key = CreateText("Key", refs.Root, 10f, FontStyles.Bold, TextAlignmentOptions.TopLeft, 0.45f, ResolveLabelFontAsset());
                Anchor(refs.Key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(9f, -7f), new Vector2(14f, 12f));
                ApplyHudCharArray(refs.Key, ResolveQuickbarKeyBuffer(slotIndex));

                _quickbarSlots[slotIndex] = refs;
            }
        }

        private void BuildSavingProgressHierarchy(RectTransform parent)
        {
            if (parent == null)
                return;

            _savingProgressDataLampPulseMaterialBound = false;
            _savingProgressDataNeedlePulseMaterialBound = false;

            _savingProgressRoot = CreateRect("SavingProgressRoot", parent);
            Anchor(
                _savingProgressRoot,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-48f, -48f),
                new Vector2(42f, 42f));

            _savingProgressIconRoot = CreateRect("SavingProgressIcon", _savingProgressRoot);
            Anchor(
                _savingProgressIconRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(34f, 34f));

            _savingProgressDiskBody = CreateImage("DiskBody", _savingProgressIconRoot, new Color(0.08f, 0.95f, 1f, 0.52f));
            Anchor(_savingProgressDiskBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 26f));
            _savingProgressDiskBody.raycastTarget = false;

            _savingProgressDiskNotch = CreateImage("DiskNotch", _savingProgressIconRoot, new Color(0f, 0.04f, 0.05f, 0.82f));
            Anchor(_savingProgressDiskNotch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3f, 6f), new Vector2(10f, 6f));
            _savingProgressDiskNotch.raycastTarget = false;

            _savingProgressDiskLabel = CreateImage("DiskLabel", _savingProgressIconRoot, new Color(1f, 1f, 1f, 0.68f));
            Anchor(_savingProgressDiskLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(14f, 6f));
            _savingProgressDiskLabel.raycastTarget = false;

            _savingProgressDataNeedle = CreateImage("DataNeedle", _savingProgressIconRoot, new Color(0.98f, 0.22f, 0.1f, 0.9f));
            Anchor(_savingProgressDataNeedle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(3f, 8f));
            _savingProgressDataNeedle.raycastTarget = false;

            _savingProgressDataLamp = CreateImage("DataRecLamp", _savingProgressIconRoot, new Color(1f, 0.08f, 0.02f, 0.95f));
            Anchor(_savingProgressDataLamp.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(6f, 6f));
            _savingProgressDataLamp.raycastTarget = false;
            EnsureSavingProgressPulseRuntimeResources();

            _savingProgressCanvasGroup = EnsureCanvasGroup(_savingProgressRoot);
            ApplySavingProgressCanvasState(0f);
        }

        private void BuildScannerFlatHologramHierarchy(RectTransform parent)
        {
            if (parent == null)
                return;

            _scannerFlatHologramRoot = CreateRect("ScannerFlatHologram", parent);
            Anchor(
                _scannerFlatHologramRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                scannerHologramOffsetPixels,
                ResolveScannerFlatHologramSize());

            _scannerFlatHologramBody = CreateImage("ScannerFlatHologramBody", _scannerFlatHologramRoot, scannerHologramScanStartColor);
            Stretch(_scannerFlatHologramBody.rectTransform, 0f, 0f, 0f, 0f);
            _scannerFlatHologramBody.raycastTarget = false;

            _scannerFlatHologramCore = CreateImage("ScannerFlatHologramCore", _scannerFlatHologramRoot, scannerHologramScanStartColor);
            Anchor(_scannerFlatHologramCore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            _scannerFlatHologramCore.raycastTarget = false;

            _scannerFlatHologramScanline = CreateImage("ScannerFlatHologramScanline", _scannerFlatHologramRoot, scannerHologramScanCompleteColor);
            Anchor(_scannerFlatHologramScanline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 46f));
            _scannerFlatHologramScanline.raycastTarget = false;

            _scannerFlatHologramCanvasGroup = EnsureCanvasGroup(_scannerFlatHologramRoot);
            _scannerFlatHologramCanvasGroup.interactable = false;
            _scannerFlatHologramCanvasGroup.blocksRaycasts = false;
            _scannerFlatHologramCanvasGroup.alpha = 0f;
        }

        private static ReadOnlySpan<char> ResolveQuickbarKeyBuffer(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return QuickbarSlotOneChars;
                case 1:
                    return QuickbarSlotTwoChars;
                case 2:
                    return QuickbarSlotThreeChars;
                case 3:
                    return QuickbarSlotFourChars;
                default:
                    return ReadOnlySpan<char>.Empty;
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

        private void RefreshQuickbarSlotHashCacheCold()
        {
            if (_toolManager == null)
                return;

            for (int slotIndex = 0; slotIndex < QuickbarSlotCount; slotIndex++)
            {
                GameObject prefab = _toolManager.GetAssignedToolPrefab(slotIndex);
                _quickbarSlotPrefabCache[slotIndex] = prefab;
                _quickbarSlotHashCache[slotIndex] = ResolveQuickbarPrefabHashCold(prefab);
                _quickbarSlotHashResolved[slotIndex] = true;
            }
        }

        private static int ResolveQuickbarPrefabHashCold(GameObject prefab)
        {
            if (prefab == null ||
                !prefab.TryGetComponent(out IPlayerToolDataReadModel tool) ||
                tool.ToolData == null)
            {
                return 0;
            }

            return tool.ToolData.PersistentHashId;
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

            return _quickbarSlotHashResolved[slotIndex] ? _quickbarSlotHashCache[slotIndex] : 0;
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

            int anchorCount = math.min(anchorHashIds.Length, stackCounts.Length);
            for (int anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
            {
                if (anchorHashIds[anchorIndex] == itemHashId && stackCounts[anchorIndex] > 0)
                    return true;
            }

            return false;
        }

        private void TryResolveDefaultIconTextures()
        {
            // Runtime assembly must not depend on editor-only APIs here.
            // Icon textures are serialized authoring data; missing assignments stay explicit.
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
            // COLD ALLOC: GameObject[1] - HUD RectTransform hierarchy bootstrap factory - owner: SuitHUDV4CanvasOverlay
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            if (parent != null)
                go.layer = parent.gameObject.layer;
            rect.localScale = Vector3.one;
            return rect;
        }

        private Image CreateImage(string name, RectTransform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            // COLD ALLOC: Image[1] - HUD image hierarchy bootstrap factory - owner: SuitHUDV4CanvasOverlay
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.material = null;
            image.maskable = false;
            return image;
        }

        private AcousticRadarRawImage CreateAcousticRadarImage(string name, RectTransform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            // COLD ALLOC: AcousticRadarRawImage[1] - HUD texture hierarchy bootstrap factory - owner: SuitHUDV4CanvasOverlay
            AcousticRadarRawImage image = rect.gameObject.AddComponent<AcousticRadarRawImage>();
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
            // COLD ALLOC: TextMeshProUGUI[1] - HUD text hierarchy bootstrap factory - owner: SuitHUDV4CanvasOverlay
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset resolvedFont = fontAsset != null ? fontAsset : ResolveLabelFontAsset();
            label.font = resolvedFont;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterSpacing = size >= 36f ? 4f : 1.5f;
            label.color = Alpha(Color.white, alpha);
            label.fontSharedMaterial = ResolveFontSharedMaterial(resolvedFont);
            label.maskable = false;
            ApplyHudCharArray(label, ReadOnlySpan<char>.Empty);
            TMP_TextRegistry.EnsureRegistered(label);
            return label;
        }

        private Material ResolveFontSharedMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;

            if (ReferenceEquals(fontAsset, _cachedFontMaterialAsset0))
                return _cachedFontSharedMaterial0;

            if (ReferenceEquals(fontAsset, _cachedFontMaterialAsset1))
                return _cachedFontSharedMaterial1;

            Material material = fontAsset.material;
            _cachedFontMaterialAsset1 = _cachedFontMaterialAsset0;
            _cachedFontSharedMaterial1 = _cachedFontSharedMaterial0;
            _cachedFontMaterialAsset0 = fontAsset;
            _cachedFontSharedMaterial0 = material;
            return material;
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

        private static Color LerpColor(Color a, Color b, float t)
        {
            return new Color(
                math.lerp(a.r, b.r, t),
                math.lerp(a.g, b.g, t),
                math.lerp(a.b, b.b, t),
                math.lerp(a.a, b.a, t));
        }

        private static float ApproximateOneMinusExpNeg(float x)
        {
            x = math.max(0f, x);
            float numerator = x * (6f + x);
            float denominator = 6f + (4f * x) + (x * x);
            return math.saturate(numerator * math.rcp(denominator));
        }

        private static float MoveTowardsFast(float current, float target, float maxDelta)
        {
            if (maxDelta <= 0f)
                return current;

            float delta = target - current;
            if (math.abs(delta) <= maxDelta)
                return target;

            return current + math.sign(delta) * maxDelta;
        }

        private static float FastInverseLerp01(float from, float to, float value)
        {
            float range = to - from;
            if (math.abs(range) <= 0.00001f)
                return 0f;

            return math.saturate((value - from) * math.rcp(range));
        }

        private static float EvaluateCheapPulse01(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return triangle * triangle;
        }

        private static float EvaluateCheapSignedWave(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return (triangle * 2f) - 1f;
        }

        private static float ApproximateTanPositive(float radians)
        {
            float x = math.clamp(radians, 0f, 1.4f);
            float x2 = x * x;
            float numerator = 15f - x2;
            float denominator = math.max(0.0001f, 15f - 6f * x2);
            return x * numerator * math.rcp(denominator);
        }

        private static float ExactPrimaryHudTanPositive(float radians)
        {
            float x = math.clamp(radians, 0.001f, 1.55334306f);
            return MathLodApproximation.ApproxTanClamped(x, 4096f);
        }

        private static int CeilPositiveToInt(float value)
        {
            int whole = (int)math.max(0f, value);
            return value > whole ? whole + 1 : whole;
        }

        private static int RoundToIntFast(float value)
        {
            return value >= 0f
                ? (int)(value + 0.5f)
                : (int)(value - 0.5f);
        }

        private static int RoundNonNegativeToInt(float value)
        {
            return (int)(math.max(0f, value) + 0.5f);
        }

        private static byte ToByteSaturate(float value)
        {
            return (byte)math.clamp((int)(value + 0.5f), 0, 255);
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

            int roundedTenths = RoundToIntFast(value * 10f);
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
                if (!ZeroGCFormatter.TryWriteInt(value, stagingBuffer.AsSpan(), out int length))
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
            bool hasLocalizationRuntime,
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
                if (!hasLocalizationRuntime)
                {
                    TryGetFallbackSpan(keyHash, out ReadOnlySpan<char> fallback);
                    ApplyHudCharArray(label, fallback);
                }
                else
                {
                    char[] buffer;
                    int length;
                    LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length);
                    ApplyHudCharArray(label, buffer, length);
                }

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

        private bool ConsumeReactiveSignals()
        {
            bool dirty = false;
            dirty |= ConsumePlayerStateSignals();
            dirty |= ConsumeInventoryChangedSignals();
            dirty |= ConsumeInputStateSignals();
            dirty |= ConsumeSystemHealthSignals();
            return dirty || _inventorySignalDirty || _inputStateSignalDirty;
        }

        private bool ConsumePlayerStateSignals()
        {
            bool dirty = false;
            ReadOnlySpan<PlayerStateSignal> signals = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerStateSignal signal = signals[i];
                _playerStateFlags = signal.Flags;
                _playerStateState = signal.State;
                _playerStateIntensity01 = math.saturate(signal.Intensity01);
                dirty = true;
            }

            _playerStateSignalDirty = dirty;
            return dirty;
        }

        private bool ConsumeInventoryChangedSignals()
        {
            bool dirty = false;
            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                InventoryChangedSignal signal = signals[i];
                if (signal.Revision == _lastInventorySignalRevision && _lastInventorySignalRevision != 0u)
                    continue;

                _lastInventorySignalRevision = signal.Revision;
                dirty = true;
            }

            if (!dirty)
                return false;

            _inventorySignalDirty = true;
            InvalidateQuickbarSlotHashCache();
            return true;
        }

        private bool ConsumeInputStateSignals()
        {
            bool dirty = false;
            ReadOnlySpan<InputStateSignal> signals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                uint schemeHash = signals[i].CurrentInputSchemeHash;
                if (schemeHash == 0u || schemeHash == _lastInputSchemeHash)
                    continue;

                _lastInputSchemeHash = schemeHash;
                dirty = true;
            }

            _inputStateSignalDirty = dirty;
            return dirty;
        }

        private bool ConsumeSystemHealthSignals()
        {
            bool dirty = false;
            ReadOnlySpan<SystemHealthSignal> signals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SystemHealthSignal signal = signals[i];
                bool warning = signal.PressureLevel > 1 ||
                    (signal.Flags & (ushort)HomeostasisSignalFlags.HudWarning) != 0;
                _systemHealthWarningActive = warning;
                _systemHealthPressureLevel = signal.PressureLevel;
                _systemHealthWarningFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                dirty = true;
            }

            if (_systemHealthWarningActive &&
                Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _systemHealthWarningFrame > SystemHealthWarningStaleFrames)
            {
                _systemHealthWarningActive = false;
                _systemHealthPressureLevel = 0;
                dirty = true;
            }

            _systemHealthSignalDirty = dirty;
            return dirty;
        }

        private bool TryApplyHomeostasisStatusLabelOverride(
            Color warningColor,
            bool corruptedMode,
            float corruptionIntensity,
            int corruptionVersion)
        {
            if (!_systemHealthWarningActive || _statusLabel == null)
                return false;

            float flickerFrequency = 14f + _systemHealthPressureLevel * 2f;
            float flickerAlpha = IsBlinkVisible((float)SystemDispatcher.CurrentUnscaledTimeSeconds, flickerFrequency) ? 1f : 0.18f;
            Color color = Alpha(warningColor, math.max(0.18f, warningColor.a * flickerAlpha));
            int version = unchecked((int)(0x484F0000u |
                ((uint)_systemHealthPressureLevel << 8) |
                (uint)((Hecton8.Core.SystemDispatcher.CurrentFrameIndex >> 2) & 0xFF)));

            int optimizingCoreLength = CopySpanToBuffer(StatusOptimizingCoreSystemsChars, s_sharedOversizedHudTextBuffer);
            SetDisplayBufferIfChanged(
                _statusLabel,
                s_sharedOversizedHudTextBuffer,
                optimizingCoreLength,
                false,
                color,
                version,
                corruptedMode,
                corruptionIntensity,
                corruptionVersion,
                307,
                ref _appliedStatusWhisperVersion,
                ref _appliedStatusLabelColor);
            return true;
        }

        private bool TryApplyScannerStatusLabelOverride(
            Color statusColor,
            bool corruptedMode,
            float corruptionIntensity,
            int corruptionVersion)
        {
            if (_statusLabel == null ||
                _toolManager == null ||
                !(_toolManager.CurrentTool is ScannerTool scanner) ||
                !scanner.TryGetScientificScanSnapshot(out ScannerTool.ScientificScanSnapshot snapshot) ||
                !HasScannerHologramPayload(snapshot) ||
                !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                return false;
            }

            try
            {
                int length;
                int version;
                if (CharBufferPool.TryAcquireArenaSpan(CharBufferPool.RequiredVrTextCapacity, out Span<char> arenaSpan))
                {
                    if (!TryBuildScannerStatusBuffer(snapshot, arenaSpan, out length, out version))
                        return false;

                    arenaSpan.Slice(0, length).CopyTo(lease.Buffer.AsSpan(0, length));
                }
                else
                {
                    if (!TryBuildScannerStatusBuffer(snapshot, lease.Buffer, out length, out version))
                        return false;
                }

                SetDisplayBufferIfChanged(
                    _statusLabel,
                    lease.Buffer,
                    length,
                    false,
                    statusColor,
                    version,
                    corruptedMode,
                    corruptionIntensity,
                    corruptionVersion,
                    307,
                    ref _appliedStatusWhisperVersion,
                    ref _appliedStatusLabelColor);
                return true;
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static bool TryBuildScannerStatusBuffer(
            ScannerTool.ScientificScanSnapshot snapshot,
            Span<char> buffer,
            out int length,
            out int version)
        {
            length = 0;
            version = 0;
            if (buffer.Length == 0)
                return false;

            int cursor = 0;
            int progressPercent = math.clamp(RoundToIntFast(snapshot.Progress01 * 100f), 0, 100);
            int purityPercent = math.clamp(RoundToIntFast(snapshot.Purity01 * 100f), 0, 100);
            int temperatureRounded = RoundToIntFast(snapshot.TemperatureC);
            int toxicityPercent = math.clamp(RoundToIntFast(snapshot.Toxicity01 * 100f), 0, 100);
            int scentX = 0;
            int scentY = 0;
            int scentZ = 0;
            if (snapshot.HasAttractantTrace != 0)
            {
                scentX = RoundToIntFast(snapshot.ScentDirection.x * 100f);
                scentY = RoundToIntFast(snapshot.ScentDirection.y * 100f);
                scentZ = RoundToIntFast(snapshot.ScentDirection.z * 100f);
            }

            cursor = AppendLiteral("SCAN ", buffer, cursor);
            cursor = AppendInt(progressPercent, buffer, cursor);
            cursor = AppendLiteral("% // ", buffer, cursor);
            cursor = AppendScientificTarget(snapshot, buffer, cursor);
            cursor = AppendLiteral(" // P ", buffer, cursor);
            cursor = AppendInt(purityPercent, buffer, cursor);
            cursor = AppendLiteral(" // T ", buffer, cursor);
            cursor = AppendInt(temperatureRounded, buffer, cursor);
            cursor = AppendLiteral("C // X ", buffer, cursor);
            cursor = AppendInt(toxicityPercent, buffer, cursor);
            if (snapshot.HasAttractantTrace != 0)
            {
                cursor = AppendLiteral(" // ", buffer, cursor);
                cursor = AppendLiteral(DescribeScientificAttractantChannel(snapshot.AttractantChannel), buffer, cursor);
                cursor = AppendLiteral(" V ", buffer, cursor);
                cursor = AppendSignedInt(scentX, buffer, cursor);
                cursor = AppendLiteral(",", buffer, cursor);
                cursor = AppendSignedInt(scentY, buffer, cursor);
                cursor = AppendLiteral(",", buffer, cursor);
                cursor = AppendSignedInt(scentZ, buffer, cursor);
            }
            else if (snapshot.OrganicBlood01 > ScannerTraceEvidenceThreshold01)
            {
                cursor = AppendLiteral(" // BLOOD TRACE", buffer, cursor);
            }
            if (snapshot.ThreatPredictionUnlocked != 0 && snapshot.FlankingManeuverDetected != 0)
                cursor = AppendLiteral(" // FLANKING MANEUVER DETECTED", buffer, cursor);

            length = math.clamp(cursor, 0, buffer.Length);
            version = unchecked(
                (progressPercent * 397) ^
                (purityPercent * 17) ^
                ((int)snapshot.MaterialClass * 13) ^
                (temperatureRounded * 31) ^
                (toxicityPercent * 19) ^
                (snapshot.OrganicBlood01 > ScannerTraceEvidenceThreshold01 ? 251 : 0) ^
                ((int)snapshot.AttractantChannel * 67) ^
                (scentX * 23) ^
                (scentY * 29) ^
                (scentZ * 31) ^
                (snapshot.HasFaunaContact != 0 ? 911 : 0) ^
                (snapshot.ThreatPredictionUnlocked != 0 ? 577 : 0) ^
                (snapshot.FlankingManeuverDetected != 0 ? 839 : 0));
            return true;
        }

        private static int AppendScientificMaterial(
            ScannerTool.ScientificMaterialClass materialClass,
            Span<char> buffer,
            int cursor)
        {
            switch (materialClass)
            {
                case ScannerTool.ScientificMaterialClass.Basalt:
                    return AppendLiteral("BASALT", buffer, cursor);
                case ScannerTool.ScientificMaterialClass.MetallicSilt:
                    return AppendLiteral("METALLIC SILT", buffer, cursor);
                case ScannerTool.ScientificMaterialClass.Sediment:
                    return AppendLiteral("SEDIMENT", buffer, cursor);
                default:
                    return AppendLiteral("UNKNOWN", buffer, cursor);
            }
        }

        private static string DescribeScientificAttractantChannel(ScannerTool.ScientificAttractantChannel attractantChannel)
        {
            switch (attractantChannel)
            {
                case ScannerTool.ScientificAttractantChannel.Blood:
                    return "BLOOD";
                case ScannerTool.ScientificAttractantChannel.Exhaust:
                    return "EXHAUST";
                default:
                    return "TRACE";
            }
        }

        private static int AppendScientificTarget(
            ScannerTool.ScientificScanSnapshot snapshot,
            Span<char> buffer,
            int cursor)
        {
            if (snapshot.HasFaunaContact != 0)
                return AppendLiteral("BIOFORM", buffer, cursor);

            return snapshot.MaterialClass != ScannerTool.ScientificMaterialClass.None
                ? AppendScientificMaterial(snapshot.MaterialClass, buffer, cursor)
                : AppendLiteral("WATER", buffer, cursor);
        }

        private static int AppendLiteral(string value, Span<char> buffer, int cursor)
        {
            if (string.IsNullOrEmpty(value) || cursor >= buffer.Length)
                return cursor;

            ReadOnlySpan<char> span = value.AsSpan();
            int writable = math.min(span.Length, buffer.Length - cursor);
            span.Slice(0, writable).CopyTo(buffer.Slice(cursor, writable));
            return cursor + writable;
        }

        private static int AppendChars(ReadOnlySpan<char> value, Span<char> buffer, int cursor)
        {
            if (value.Length == 0 || cursor >= buffer.Length)
                return cursor;

            int writable = math.min(value.Length, buffer.Length - cursor);
            value.Slice(0, writable).CopyTo(buffer.Slice(cursor, writable));
            return cursor + writable;
        }

        private static int AppendInt(int value, Span<char> buffer, int cursor)
        {
            if (cursor >= buffer.Length)
                return cursor;

            if (!value.TryFormat(buffer.Slice(cursor), out int written))
                return cursor;

            return cursor + written;
        }

        private static int AppendSignedInt(int value, Span<char> buffer, int cursor)
        {
            if (value >= 0)
                cursor = AppendLiteral("+", buffer, cursor);

            return AppendInt(value, buffer, cursor);
        }

        private void SetDisplayBufferIfChanged(
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

            if (sourceLength > _glitchScratchBuffer.Length)
            {
                SetCharBufferIfChanged(label, sourceBuffer, sourceLength, rtl, color, version, ref cachedVersion, ref cachedColor);
                return;
            }

            int seed = unchecked((corruptionVersion * 397) ^ corruptionSalt);
            unsafe
            {
                if (TryResolveGlitchTablePointer(out byte* table, out int tableLength))
                {
                    GlitchEncoder.ApplyDecayToBuffer(
                        sourceBuffer,
                        sourceLength,
                        _glitchScratchBuffer,
                        corruptionIntensity,
                        seed,
                        table,
                        tableLength,
                        LegacyGlitchReadabilityPrefixChars,
                        out int corruptedLength);
                    int corruptedDisplayVersion = unchecked((version * 397) ^ corruptionVersion ^ corruptionSalt);
                    SetCharBufferIfChanged(label, _glitchScratchBuffer, corruptedLength, rtl, color, corruptedDisplayVersion, ref cachedVersion, ref cachedColor);
                }
                else
                {
                    GlitchEncoder.ApplyDecayToBuffer(
                        sourceBuffer,
                        sourceLength,
                        _glitchScratchBuffer,
                        corruptionIntensity,
                        seed,
                        out int corruptedLength);
                    int corruptedDisplayVersion = unchecked((version * 397) ^ corruptionVersion ^ corruptionSalt);
                    SetCharBufferIfChanged(label, _glitchScratchBuffer, corruptedLength, rtl, color, corruptedDisplayVersion, ref cachedVersion, ref cachedColor);
                }
            }
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
            int roundedTenths = RoundToIntFast(value * 10f);
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

        private static void ResolveLocalizedKeyBuffer(int keyHash, bool hasLocalizationRuntime, out char[] buffer, out int length)
        {
            if (!hasLocalizationRuntime)
            {
                TryGetFallbackSpan(keyHash, out ReadOnlySpan<char> fallback);
                buffer = s_sharedOversizedHudTextBuffer;
                length = CopySpanToBuffer(fallback, buffer);
                return;
            }

            LocRegistry.TryGetRawBuffer(keyHash, out buffer, out length);
        }

        private static void SetLocalizedRtlState(TMP_Text label, bool rtl)
        {
            if (label == null)
                return;

            bool richText = BabelRichTextLodPolicy.ShouldEnableTmpRichTextParsing();
            if (label.richText != richText)
                label.richText = richText;

            if (label.isRightToLeftText != rtl)
                label.isRightToLeftText = rtl;
        }

        private static void ApplyHudCharArray(TMP_Text label, char[] buffer, int length)
        {
            if (label == null || buffer == null)
                return;

            int safeLength = math.clamp(length, 0, buffer.Length);
            label.SetCharArray(buffer, 0, safeLength);
        }

        private static void ApplyHudCharArray(TMP_Text label, ReadOnlySpan<char> source)
        {
            if (label == null)
                return;

            int safeLength = CopySpanToBuffer(source, s_sharedOversizedHudTextBuffer);
            label.SetCharArray(s_sharedOversizedHudTextBuffer, 0, safeLength);
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

        private static bool TryGetFallbackSpan(int keyHash, out ReadOnlySpan<char> buffer)
        {
            switch (keyHash)
            {
                case var _ when keyHash == _HudDepthKeyHash:
                    buffer = DepthLabelChars;
                    return true;

                case var _ when keyHash == _HudTemperatureKeyHash:
                    buffer = TemperatureLabelChars;
                    return true;

                case var _ when keyHash == _HudPressureKeyHash:
                    buffer = PressureLabelChars;
                    return true;

                case var _ when keyHash == _HudAtmKeyHash:
                    buffer = AtmLabelChars;
                    return true;

                case var _ when keyHash == _HudOxygenKeyHash:
                    buffer = GaugeO2LabelChars;
                    return true;

                case var _ when keyHash == _HudPowerKeyHash:
                    buffer = GaugePowerLabelChars;
                    return true;

                case var _ when keyHash == _HudHullKeyHash:
                    buffer = GaugeHullLabelChars;
                    return true;

                case var _ when keyHash == _HudMetersKeyHash:
                    buffer = MetersLabelChars;
                    return true;

                case var _ when keyHash == _HudFeetKeyHash:
                    buffer = FeetLabelChars;
                    return true;

                case var _ when keyHash == _HudCelsiusKeyHash:
                    buffer = CelsiusLabelChars;
                    return true;

                case var _ when keyHash == _HudFahrenheitKeyHash:
                    buffer = FahrenheitLabelChars;
                    return true;

                case var _ when keyHash == _StatusPressureLimitExceededKeyHash:
                    buffer = StatusPressureLimitExceededChars;
                    return true;

                case var _ when keyHash == _StatusApproachingSafeDepthKeyHash:
                    buffer = StatusApproachingSafeDepthChars;
                    return true;

                case var _ when keyHash == _StatusSuitDamageCriticalKeyHash:
                    buffer = StatusSuitDamageCriticalChars;
                    return true;

                case var _ when keyHash == _StatusOxygenReserveLowKeyHash:
                    buffer = StatusOxygenReserveLowChars;
                    return true;

                case var _ when keyHash == _StatusPowerCellsLowKeyHash:
                    buffer = StatusPowerCellsLowChars;
                    return true;

                case var _ when keyHash == _StatusLampThermalLimitKeyHash:
                    buffer = StatusLampThermalLimitChars;
                    return true;

                case var _ when keyHash == _StatusSuitLinkRoutingPdaKeyHash:
                    buffer = StatusSuitLinkRoutingPdaChars;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalStableKeyHash:
                    buffer = StatusLifeSupportNominalStableChars;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalAscendingKeyHash:
                    buffer = StatusLifeSupportNominalAscendingChars;
                    return true;

                case var _ when keyHash == _StatusLifeSupportNominalDescendingKeyHash:
                    buffer = StatusLifeSupportNominalDescendingChars;
                    return true;
            }

            buffer = ReadOnlySpan<char>.Empty;
            return false;
        }

        private static void BuildMetricTemplate(
            ref char[] buffer,
            out int length,
            int labelKeyHash,
            int unitKeyHash,
            ReadOnlySpan<char> numberToken,
            bool prependNegativeSign,
            bool hasLocalizationRuntime)
        {
            ReadOnlySpan<char> labelSpan;
            ReadOnlySpan<char> unitSpan;

            if (!hasLocalizationRuntime)
            {
                TryGetFallbackSpan(labelKeyHash, out labelSpan);
                TryGetFallbackSpan(unitKeyHash, out unitSpan);
            }
            else
            {
                char[] labelBuffer;
                int labelLength;
                char[] unitBuffer;
                int unitLength;
                LocRegistry.TryGetRawBuffer(labelKeyHash, out labelBuffer, out labelLength);
                LocRegistry.TryGetRawBuffer(unitKeyHash, out unitBuffer, out unitLength);
                labelSpan = labelBuffer != null ? labelBuffer.AsSpan(0, math.clamp(labelLength, 0, labelBuffer.Length)) : ReadOnlySpan<char>.Empty;
                unitSpan = unitBuffer != null ? unitBuffer.AsSpan(0, math.clamp(unitLength, 0, unitBuffer.Length)) : ReadOnlySpan<char>.Empty;
            }

            int totalLength = labelSpan.Length + 2 + (prependNegativeSign ? 1 : 0) + numberToken.Length + 1 + unitSpan.Length;
            EnsureCharCapacity(ref buffer, totalLength);
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                return;
            }

            int writeIndex = 0;
            CopySpan(labelSpan, buffer, ref writeIndex);
            if (writeIndex < buffer.Length)
                buffer[writeIndex++] = ':';
            if (writeIndex < buffer.Length)
                buffer[writeIndex++] = ' ';
            if (prependNegativeSign && writeIndex < buffer.Length)
                buffer[writeIndex++] = '-';

            for (int i = 0; i < numberToken.Length && writeIndex < buffer.Length; i++)
                buffer[writeIndex++] = numberToken[i];

            if (writeIndex < buffer.Length)
                buffer[writeIndex++] = ' ';
            CopySpan(unitSpan, buffer, ref writeIndex);
            length = writeIndex;
        }

        private void GetHullStressWhisperBuffer(ILocalizationStressPresentationReadModel manager, out char[] buffer, out int length, out int version)
        {
            ResolveHullStressWhisperText(manager);
            _cachedHullStressWhisperRtl = manager != null &&
                                          LocalizedMeasurementFormatter.IsRightToLeft((GameLanguage)manager.ActiveLanguageId);
            buffer = _cachedHullStressWhisperBuffer;
            length = _cachedHullStressWhisperLength;
            version = _cachedHullStressWhisperBucket;
        }

        private static void CopyTextToBuffer(ReadOnlySpan<char> source, ref char[] buffer, out int length)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                return;
            }

            int copyLength = math.min(source.Length, buffer.Length);
            for (int i = 0; i < copyLength; i++)
                buffer[i] = source[i];
            if (source.Length > buffer.Length && buffer.Length > 0)
                buffer[buffer.Length - 1] = '!';

            length = copyLength;
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int copyLength = math.min(source.Length, destination.Length);
            for (int i = 0; i < copyLength; i++)
                destination[i] = source[i];
            if (source.Length > destination.Length && destination.Length > 0)
                destination[destination.Length - 1] = '!';

            return copyLength;
        }

        private static void WriteUppercaseTextToBuffer(ReadOnlySpan<char> source, bool replaceUnderscores, ref char[] buffer, out int length, out int version)
        {
            EnsureCharCapacity(ref buffer, source.Length);
            if (buffer == null || buffer.Length == 0)
            {
                length = 0;
                version = 0;
                return;
            }

            int hash = 17;
            int copyLength = math.min(source.Length, buffer.Length);
            for (int i = 0; i < copyLength; i++)
            {
                char character = source[i];
                if (replaceUnderscores && character == '_')
                    character = ' ';

                character = ConvertUppercaseInvariantChar(character);
                buffer[i] = character;
                hash = unchecked((hash * 397) ^ character);
            }

            if (source.Length > buffer.Length && buffer.Length > 0)
            {
                buffer[buffer.Length - 1] = '!';
                hash = unchecked((hash * 397) ^ '!');
            }

            length = copyLength;
            version = copyLength == 0 ? 0 : hash;
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
            int safeRequired = math.max(0, requiredLength);
            if (buffer == null || buffer.Length == 0)
            {
                buffer = s_sharedOversizedHudTextBuffer;
                return;
            }

            if (safeRequired <= buffer.Length)
                return;

            int markerIndex = buffer.Length - 1;
            if (markerIndex >= 0)
                buffer[markerIndex] = '!';
        }

        private static void CopySpan(ReadOnlySpan<char> source, char[] destination, ref int destinationIndex)
        {
            if (source.Length == 0 || destination == null || destinationIndex >= destination.Length)
                return;

            int available = destination.Length - destinationIndex;
            int count = math.min(source.Length, available);
            for (int i = 0; i < count; i++)
                destination[destinationIndex++] = source[i];
            if (source.Length > available && destination.Length > 0)
            {
                destination[destination.Length - 1] = '!';
                destinationIndex = destination.Length;
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

        private void UpdateHudProxyLightRegistration(float power01, float oxygen01, float stressPulse01)
        {
            if (!enableHudProxyLight ||
                !Application.isPlaying ||
                targetCanvas == null ||
                _hudProxyLightKey == 0 ||
                (renderPath != RenderPath.ProjectionSource && targetCanvas.renderMode != RenderMode.WorldSpace))
            {
                UnregisterHudProxyLight();
                return;
            }

            Transform canvasTransform = targetCanvas.transform;
            Vector3 canvasPosition = canvasTransform.position;
            Vector3 canvasForward = canvasTransform.forward;
            float3 forward = new float3(canvasForward.x, canvasForward.y, canvasForward.z);
            if (!math.all(math.isfinite(forward)) || math.lengthsq(forward) <= 0.000001f)
                forward = new float3(0f, 0f, 1f);
            float3 runtimePosition = new float3(canvasPosition.x, canvasPosition.y, canvasPosition.z) +
                                      forward * math.max(0f, hudProxyLightForwardOffsetMeters);
            if (!math.all(math.isfinite(runtimePosition)) || !math.all(math.isfinite(forward)))
            {
                UnregisterHudProxyLight();
                return;
            }

            power01 = FiniteSaturate01(power01, 0f);
            oxygen01 = FiniteSaturate01(oxygen01, 1f);
            stressPulse01 = FiniteSaturate01(stressPulse01, 0f);
            float oxygenStress01 = math.saturate(1f - oxygen01);
            float powerFlicker01 = math.saturate(power01 * (1f - (math.saturate(stressPulse01) * hudProxyLightStressFlicker)));
            float intensity = math.saturate(hudProxyLightIntensity * powerFlicker01 * (1f + (oxygenStress01 * hudProxyLightOxygenStressBoost)));
            if (intensity <= 0.0001f)
            {
                UnregisterHudProxyLight();
                return;
            }

            Color proxyColor = LerpColor(hudProxyLightColor, hudProxyLightStressColor, oxygenStress01).linear;
            if (!TryResolveRuntimeAup((Vector3)runtimePosition, out AbsoluteUniversePosition aup))
            {
                UnregisterHudProxyLight();
                return;
            }

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            ProxyLightData lightData = ProxyLightData.CreateUiPanel(
                in aup,
                runtimePosition,
                forward,
                proxyColor,
                hudProxyLightRangeMeters,
                intensity,
                stressPulse01,
                powerFlicker01,
                oxygenStress01,
                now);

            if (ProxyLightRegistry.RegisterOrUpdate(_hudProxyLightKey, in lightData))
                _hudProxyLightRegistered = true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (IsStencilRenderGraphSuppressedRuntime())
                return false;

            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(originAup)))
                return false;

            positionAup = AbsoluteUniversePosition.FromAbsolutePosition(originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void UnregisterHudProxyLight()
        {
            if (!_hudProxyLightRegistered)
                return;

            ProxyLightRegistry.Unregister(_hudProxyLightKey);
            _hudProxyLightRegistered = false;
        }

        private float UpdateStressPulse(float dt)
        {
            float rawStress = playerMovement != null
                ? math.saturate(playerMovement.CurrentUnderwaterStressIntensity01)
                : 0f;
            float targetStress = rawStress <= stressPulseStartThreshold
                ? 0f
                : FastInverseLerp01(stressPulseStartThreshold, 1f, rawStress);
            float blendT = ApproximateOneMinusExpNeg(math.max(0.01f, stressPulseBlendSpeed) * dt);
            _stressPulseIntensity = math.lerp(_stressPulseIntensity, targetStress, blendT);
            if (_stressPulseIntensity <= 0.001f)
            {
                _stressPulseIntensity = 0f;
                return 0f;
            }

            float frequency = math.lerp(stressPulseFrequencyMin, stressPulseFrequencyMax, _stressPulseIntensity);
            _stressPulsePhase += dt * frequency * Mathf.PI * 2f;
            if (_stressPulsePhase >= Mathf.PI * 2f)
                _stressPulsePhase -= Mathf.PI * 2f;

            float wave = EvaluateCheapPulse01(_stressPulsePhase);
            return _stressPulseIntensity * wave;
        }

        private static Color ResolveStressPulseColor(Color baseColor, Color warningColor, float pulseStrength, float brightnessBoost, float warningBlend)
        {
            if (pulseStrength <= 0.0001f)
                return baseColor;

            Color pulsedColor = LerpColor(baseColor, warningColor, pulseStrength * warningBlend);
            return LerpColor(pulsedColor, Color.white, pulseStrength * brightnessBoost);
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
            _reticleH.color = Alpha(primary, 0.96f);
            _reticleV.color = Alpha(primary, 0.82f);
            _reticleBracketLeft.color = Alpha(primary, 0.68f);
            _reticleBracketRight.color = Alpha(primary, 0.68f);
            ApplySavingProgressStyle(primary, dim, warning);

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
            if (!_styleApplied || math.abs(_appliedStressPulseStrength - stressPulse) <= 0.002f)
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
            _reticleH.color = Alpha(pulsedPrimary, 0.96f);
            _reticleV.color = Alpha(pulsedPrimary, 0.82f + stressPulse * 0.08f);
            _reticleBracketLeft.color = Alpha(pulsedPrimary, 0.68f + stressPulse * 0.08f);
            _reticleBracketRight.color = Alpha(pulsedPrimary, 0.68f + stressPulse * 0.08f);
            ApplySavingProgressStyle(pulsedPrimary, primary, warning);
            _appliedStressPulseStrength = stressPulse;
        }

        private void ApplyAnalogUiJitter(float safeDepthNormalized, float corruptionIntensity, float stressPulse)
        {
            float pressureStress = 1f - math.saturate(safeDepthNormalized);
            float targetStrength = math.saturate(math.max(
                pressureStress * 0.78f,
                math.max(corruptionIntensity, stressPulse * 0.55f)));

            int targetBucket = math.clamp(
                (int)math.round(targetStrength * AnalogUiJitterQuantizationSteps),
                0,
                AnalogUiJitterQuantizationSteps);
            if (_appliedAnalogUiJitterBucket == targetBucket)
                return;

            float quantizedStrength = targetBucket * InvAnalogUiJitterQuantizationSteps;
            _appliedAnalogUiJitterBucket = targetBucket;
            _appliedAnalogUiJitterStrength = quantizedStrength;
        }

        /// <summary>
        /// Single writer of the saving-indicator colours, for both the normal and the failed write.
        /// </summary>
        /// <remarks>
        /// The failure read is deliberately non-numeric and non-textual: the whole disk goes to the warning colour
        /// and the record lamp blinks, which separates a lost write from a completed one without formatting a
        /// percentage that the save system never publishes. See <see cref="PollSavingProgressStatus"/>.
        /// </remarks>
        private void ApplySavingProgressStyle(Color primary, Color dim, Color warning)
        {
            bool failureLatched = _savingProgressFailureLatched;
            Color bodyColor = failureLatched ? warning : primary;
            Color labelColor = failureLatched ? warning : dim;
            float bodyAlpha = failureLatched ? 0.88f : 0.58f;
            float lampAlpha = !failureLatched || _savingProgressFailureBlinkOn ? 0.95f : 0.1f;

            if (_savingProgressDiskBody != null)
                _savingProgressDiskBody.color = Alpha(bodyColor, bodyAlpha);
            if (_savingProgressDiskNotch != null)
                _savingProgressDiskNotch.color = Alpha(Color.black, 0.78f);
            if (_savingProgressDiskLabel != null)
                _savingProgressDiskLabel.color = Alpha(labelColor, 0.72f);
            if (_savingProgressDataNeedle != null)
                _savingProgressDataNeedle.color = Alpha(warning, 0.92f);
            if (_savingProgressDataLamp != null)
                _savingProgressDataLamp.color = Alpha(warning, lampAlpha);

            _appliedSavingProgressFailureLatched = failureLatched;
            _appliedSavingProgressFailureBlinkOn = _savingProgressFailureBlinkOn;
            _hasAppliedSavingProgressFailureStyle = true;
        }

        private void UpdateReticleSpread(float dt)
        {
            if (_reticleRoot == null || _reticleH == null || _reticleV == null || _reticleBracketLeft == null || _reticleBracketRight == null)
                return;

            float movementSpeedSq = 0f;
            if (playerMovement != null)
            {
                Vector3 movementVelocity = playerMovement.InterpolatedLinearVelocity;
                movementSpeedSq = math.lengthsq(new float3(movementVelocity.x, movementVelocity.y, movementVelocity.z));
            }

            float headlessMovementSpeed = ReadHeadlessUIValue(UIValueSlotId.MovementSpeed, -1f);
            if (headlessMovementSpeed >= 0f)
            {
                headlessMovementSpeed = FiniteNonNegative(headlessMovementSpeed, 0f);
                movementSpeedSq = headlessMovementSpeed * headlessMovementSpeed;
            }

            movementSpeedSq = FiniteNonNegative(movementSpeedSq, 0f);
            float velocityContribution = math.min(movementSpeedSq * reticleVelocityFactor * ReticleSquaredSpeedSpreadScale, 36f);
            float heatContribution = FiniteSaturate01(ReadHeadlessUIValue(UIValueSlotId.ToolHeat01, ResolveReticleHeat01()), 0f) * reticleHeatSpread;
            float targetSpread = math.max(0f, reticleBaseSpread + velocityContribution + heatContribution);
            float blendT = ApproximateOneMinusExpNeg(math.max(0.01f, reticleSpreadBlendSpeed) * math.max(dt, 0.016f));
            _reticleSpreadPixels = math.lerp(_reticleSpreadPixels, targetSpread, blendT);

            float safeLineLength = math.clamp(reticleLineLength, 8f, 36f);
            float safeLineThickness = math.clamp(reticleLineThickness, 1f, 6f);
            float safeBracketLength = math.clamp(reticleBracketLength, 4f, 24f);
            bool dimensionsDirty =
                math.abs(_appliedReticleLineLength - safeLineLength) > 0.01f ||
                math.abs(_appliedReticleLineThickness - safeLineThickness) > 0.01f ||
                math.abs(_appliedReticleBracketLength - safeBracketLength) > 0.01f;

            if (!dimensionsDirty && math.abs(_appliedReticleSpreadPixels - _reticleSpreadPixels) <= 0.05f)
                return;

            float horizontalHalfSpan = math.max(0f, _reticleSpreadPixels);
            _reticleRoot.sizeDelta = new Vector2(64f + horizontalHalfSpan * 2f, 64f + horizontalHalfSpan * 2f);
            if (dimensionsDirty)
            {
                _reticleH.rectTransform.sizeDelta = new Vector2(safeLineLength, safeLineThickness);
                _reticleV.rectTransform.sizeDelta = new Vector2(safeLineThickness, safeLineLength);
                _reticleBracketLeft.rectTransform.sizeDelta = new Vector2(safeBracketLength, safeLineThickness);
                _reticleBracketRight.rectTransform.sizeDelta = new Vector2(safeBracketLength, safeLineThickness);
                _appliedReticleLineLength = safeLineLength;
                _appliedReticleLineThickness = safeLineThickness;
                _appliedReticleBracketLength = safeBracketLength;
            }

            _reticleBracketLeft.rectTransform.anchoredPosition = new Vector2(-horizontalHalfSpan, 0f);
            _reticleBracketRight.rectTransform.anchoredPosition = new Vector2(horizontalHalfSpan, 0f);
            _appliedReticleSpreadPixels = _reticleSpreadPixels;
        }

        private float ResolveReticleHeat01()
        {
            if (_toolManager != null && _toolManager.CurrentTool is LaserCutter cutter)
                return math.saturate(cutter.ReadHeatLevel());

            if (flashlight != null && flashlight.IsOverheated)
                return 1f;

            return 0f;
        }

        private void InvalidateVisualCaches()
        {
            _quickbarVisualsInitialized = false;
            _lastStreamedOxygen01 = float.NaN;
            _lastStreamedPower01 = float.NaN;
            _lastStreamedHealth01 = float.NaN;
            _lastStreamedDepthMeters = float.NaN;
            _lastStreamedTemperature = float.NaN;
            _lastStreamedPressure = float.NaN;
            _lastStreamedHeadingDegrees = float.NaN;
            _lastHapticOxygenVersion = 0u;
            _lastHapticPowerVersion = 0u;
            _lastHapticHealthVersion = 0u;
            _nextCriticalHapticTime = 0f;
            _nextSavingProgressHapticTime = 0f;
            // The hierarchy is about to be rebuilt, so the previously applied disk colours are gone; drop the
            // change-gate cache or a live failure latch would never repaint itself onto the new Images.
            _appliedSavingProgressFailureLatched = false;
            _appliedSavingProgressFailureBlinkOn = false;
            _hasAppliedSavingProgressFailureStyle = false;
            _activeCriticalHapticMask = 0;
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
            _reticleSpreadPixels = reticleBaseSpread;
            _appliedReticleSpreadPixels = float.NaN;
            _appliedReticleLineLength = float.NaN;
            _appliedReticleLineThickness = float.NaN;
            _appliedReticleBracketLength = float.NaN;
            _appliedStressPulseStrength = -1f;
            _appliedAnalogUiJitterStrength = -1f;
            _appliedAnalogUiJitterBucket = int.MinValue;
            _styleApplied = false;
            _appliedOverallScale = 0f;
            _appliedChromeAlpha = 0f;
            _appliedPrimary = default;
            _appliedSecondary = default;
            _appliedDim = default;
            _appliedWarning = default;
            _appliedBiosBackdropColor = default;
            _hasAppliedBiosBackdropColor = false;
            _canvasStateApplied = false;
            _appliedCanvasTarget = null;
            _appliedProjectionCamera = null;
            _appliedRenderPath = default;
            _appliedOverlaySortingOrder = 0;
            _cachedCanvasScaler = null;
            _cachedGraphicRaycaster = null;
            _cachedGraphicRaycasterCanvas = null;
            _cachedUiScaler = null;
            _cachedHullStressWhisperBucket = int.MinValue;
            _cachedHullStressWhisperLength = 0;
            _rootBaseAnchoredPositionCaptured = false;
            _savingProgressDataLampPulseMaterialBound = false;
            _savingProgressDataNeedlePulseMaterialBound = false;
            _cachedFontMaterialAsset0 = null;
            _cachedFontMaterialAsset1 = null;
            _cachedFontSharedMaterial0 = null;
            _cachedFontSharedMaterial1 = null;
        }

        private Canvas ResolveTargetCanvas()
        {
            if (targetCanvas == null)
                TryGetComponent(out targetCanvas);

            return targetCanvas;
        }

        private void ResolveGraphicRaycasterCold()
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null || ReferenceEquals(_cachedGraphicRaycasterCanvas, canvas))
                return;

            canvas.TryGetComponent(out _cachedGraphicRaycaster);
            _cachedGraphicRaycasterCanvas = canvas;
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
                canvas.TryGetComponent(out _cachedCanvasScaler);

            return _cachedCanvasScaler;
        }

        private HectonUIScaler ResolveUiScaler(bool allowCreation)
        {
            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
            {
                _cachedUiScaler = null;
                return null;
            }

            if (_cachedUiScaler == null || _cachedUiScaler.gameObject != canvas.gameObject)
                canvas.TryGetComponent(out _cachedUiScaler);

            if (_cachedUiScaler == null)
            {
                if (!allowCreation)
                    return null;

                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                // COLD ALLOC: HectonUIScaler[1] - canvas matrix scaler bootstrap - owner: SuitHUDV4CanvasOverlay
                _cachedUiScaler = canvas.gameObject.AddComponent<HectonUIScaler>();
            }

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

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y);
        }

        private void DisableAcousticRadarOverlayImage(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out RawImage image) && image.name == "AcousticRadarOverlay")
            {
                image.enabled = false;
                image.material = null;
                image.texture = null;
                if (ReferenceEquals(image, _acousticRadarOverlay))
                    _acousticRadarOverlayMaterialBound = false;
                return;
            }

            for (int i = 0; i < root.childCount; i++)
                DisableAcousticRadarOverlayImage(root.GetChild(i));
        }

        private static Canvas FindSceneCanvasByName(string canvasName)
        {
            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = GetActiveOverlay(i);
                if (overlay == null)
                    continue;

                Canvas canvas = overlay.TargetCanvas;
                if (canvas != null && canvas.name == canvasName)
                    return canvas;
            }

            return null;
        }

        public void SetRenderPathProjectionSource(bool projectionSource)
        {
            RenderPath nextPath = projectionSource || !IsScreenOverlayAllowed()
                ? RenderPath.ProjectionSource
                : RenderPath.ScreenOverlay;
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

        public void ConfigureRuntimeProjection(Camera camera)
        {
            SetProjectionCamera(camera);
            SetRenderPathProjectionSource(true);
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

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                       canvas.worldCamera == null;
            }
#endif

            return canvas.renderMode == RenderMode.WorldSpace &&
                   ReferenceEquals(canvas.worldCamera, requestedProjectionCamera);
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
            ditheredUiBackgroundMaterial = source.ditheredUiBackgroundMaterial;
            dataRecPulseMaterial = source.dataRecPulseMaterial;
            acousticRadarMaterial = source.acousticRadarMaterial;
            acousticRadarOpacity = source.acousticRadarOpacity;
            acousticRadarInnerEdge = source.acousticRadarInnerEdge;
            acousticRadarBandThickness = source.acousticRadarBandThickness;
            acousticRadarWaveAmplitude = source.acousticRadarWaveAmplitude;
            acousticRadarPulseFrequency = source.acousticRadarPulseFrequency;
            acousticRadarGlitchStrength = source.acousticRadarGlitchStrength;
            threatChevronMaterial = source.threatChevronMaterial;
            _threatChevronStaticMesh = source._threatChevronStaticMesh;
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
                if (IsStencilRenderGraphSuppressedRuntime())
                {
                    ApplyStencilRenderGraphSuppressionIfNeeded();
                    return;
                }

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
            if (!_runtimeHudCallbacksActive)
                return;

            if (IsStencilRenderGraphSuppressedRuntime())
            {
                QueueStencilSuppressionApply();
                return;
            }

            if (_lateFrameTickRegistered && _slowTickRegistered)
                return;

            if (!_lateFrameTickRegistered)
            {
                _lateFrameTickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
            }

            if (_slowTickRegistered)
                return;

            _slowTickRegistered = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void QueueStencilSuppressionApply()
        {
            _pendingStencilSuppressionApply = true;
            if (!_lateFrameTickRegistered && Application.isPlaying)
                _lateFrameTickRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void QueueReactiveLateFrameSolve(float deltaTime)
        {
            _reactiveLateFrameSolveRequested = true;
            _queuedReactiveDeltaTime = math.max(_queuedReactiveDeltaTime, math.max(0f, deltaTime));
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private void UnregisterRuntimeTick()
        {
            if (_lateFrameTickRegistered)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _lateFrameTickRegistered = false;
            }

            if (!_slowTickRegistered)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
            _slowTickRegistered = false;
        }

        private void RegisterActiveOverlay()
        {
            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                if (ReferenceEquals(GetActiveOverlay(i), this))
                    return;
            }

            if (s_activeOverlayCount >= MaxActiveOverlays)
                return;

            SetActiveOverlaySlot(s_activeOverlayCount, this);
            s_activeOverlayCount++;
            RefreshActiveRuntimeInstance();
        }

        private void UnregisterActiveOverlay()
        {
            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                if (!ReferenceEquals(GetActiveOverlay(i), this))
                    continue;

                int lastIndex = s_activeOverlayCount - 1;
                SetActiveOverlaySlot(i, GetActiveOverlay(lastIndex));
                SetActiveOverlaySlot(lastIndex, null);
                s_activeOverlayCount = lastIndex;
                break;
            }

            RefreshActiveRuntimeInstance();
        }

        private static void RefreshActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;

#if UNITY_EDITOR
            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = GetActiveOverlay(i);
                if (overlay != null &&
                    overlay.isActiveAndEnabled &&
                    overlay.renderPath == RenderPath.ScreenOverlay)
                {
                    ActiveRuntimeInstance = overlay;
                    return;
                }
            }
#endif

            for (int i = 0; i < s_activeOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = GetActiveOverlay(i);
                if (overlay != null && overlay.isActiveAndEnabled)
                {
                    ActiveRuntimeInstance = overlay;
                    return;
                }
            }
        }

        internal static SuitHUDV4CanvasOverlay GetActiveOverlay(int index)
        {
            switch (index)
            {
                case 0: return s_activeOverlay0;
                case 1: return s_activeOverlay1;
                case 2: return s_activeOverlay2;
                case 3: return s_activeOverlay3;
                default: return null;
            }
        }

        private static void SetActiveOverlaySlot(int index, SuitHUDV4CanvasOverlay overlay)
        {
            switch (index)
            {
                case 0:
                    s_activeOverlay0 = overlay;
                    break;
                case 1:
                    s_activeOverlay1 = overlay;
                    break;
                case 2:
                    s_activeOverlay2 = overlay;
                    break;
                case 3:
                    s_activeOverlay3 = overlay;
                    break;
            }
        }

        private static void ClearActiveOverlays()
        {
            s_activeOverlay0 = null;
            s_activeOverlay1 = null;
            s_activeOverlay2 = null;
            s_activeOverlay3 = null;
            s_activeOverlayCount = 0;
            ActiveRuntimeInstance = null;
        }
    }

#if false
    /// <summary>
    /// Canvas-root scaler that applies a single matrix-driven transform to a dedicated content root instead of using CanvasScaler relayout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Hecton UI Scaler")]
    [RequireComponent(typeof(Canvas))]
    public sealed class HectonUIScaler : MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const string ContentRootName = "HectonUI_ScaledRoot";

        [Header("-- Scale Policy ------------------")]
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
        private bool _hotSwapRegistered;
        private bool _runtimeScalerCallbacksActive;
        private bool _pendingContentRootBootstrap = true;
        private int _cachedRenderWidth = 1;
        private int _cachedRenderHeight = 1;
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
        public RectTransform ContentRoot => TryRefreshExistingContentRootHot() ? _contentRoot : null;

        private void OnEnable()
        {
            _runtimeScalerCallbacksActive = Application.isPlaying;
            ResolveCanvas();
            RefreshRenderDimensionsSlowSample();
            EnsureContentRoot();
            ApplyScale(force: true, allowContentRootCreation: false);
            _pendingContentRootBootstrap = ResolveContentRootInternal(createIfMissing: false) == null;
            if (!_runtimeScalerCallbacksActive)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            _runtimeScalerCallbacksActive = false;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            _runtimeScalerCallbacksActive = false;
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void DisabledVisualSync()
        {
            if (_pendingContentRootBootstrap)
                return;

            if (!TryRefreshExistingContentRootHot())
                return;

            ApplyScaleToResolvedContentRoot(_contentRoot, force: false);
        }

        public void SlowTick()
        {
            if (!_runtimeScalerCallbacksActive)
                return;

            bool dimensionsChanged = RefreshRenderDimensionsSlowSample();
            if (!_pendingContentRootBootstrap)
            {
                if (!TryRefreshExistingContentRootHot())
                {
                    _pendingContentRootBootstrap = true;
                }
                else if (dimensionsChanged)
                {
                    ApplyScaleToResolvedContentRoot(_contentRoot, force: true);
                }

                return;
            }

            if (!TryResolveExistingContentRootCold())
                return;

            ApplyScaleToResolvedContentRoot(_contentRoot, force: true);
            _pendingContentRootBootstrap = false;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
            _lastAppliedScale = -1f;
            _lastAppliedReferenceResolution = Vector2.zero;
            _lastAppliedMatch = -1f;
            _pendingContentRootBootstrap = !TryRefreshExistingContentRootHot();

            if (_pendingContentRootBootstrap)
                return;

            ApplyScaleToResolvedContentRoot(_contentRoot, force: true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            referenceResolution = new Vector2(
                math.max(1f, referenceResolution.x),
                math.max(1f, referenceResolution.y));
            matchWidthOrHeight = math.saturate(matchWidthOrHeight);
            minimumScale = math.max(0.1f, minimumScale);
            maximumScale = math.max(minimumScale, maximumScale);
        }

        [ContextMenu("Rebuild UI")]
        private void RebuildUiInEditor()
        {
            if (Application.isPlaying)
                return;

            ResolveCanvas();
            RefreshRenderDimensionsSlowSample();
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
                math.max(1f, nextReferenceResolution.x),
                math.max(1f, nextReferenceResolution.y));
            float sanitizedMatch = math.saturate(nextMatchWidthOrHeight);
            if (Approximately(referenceResolution, sanitizedResolution) &&
                Mathf.Approximately(matchWidthOrHeight, sanitizedMatch))
            {
                return;
            }

            referenceResolution = sanitizedResolution;
            matchWidthOrHeight = sanitizedMatch;
            if (_runtimeScalerCallbacksActive)
            {
                _pendingContentRootBootstrap = _pendingContentRootBootstrap || !TryRefreshExistingContentRootHot();
                if (!_pendingContentRootBootstrap)
                    ApplyScaleToResolvedContentRoot(_contentRoot, force: true);
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

        private bool TryRefreshExistingContentRootHot()
        {
            if (_targetCanvas == null)
                return false;

            return _contentRoot != null && _contentRoot.gameObject != null;
        }

        private bool TryResolveExistingContentRootCold()
        {
            if (_targetCanvas == null)
                return false;

            if (TryRefreshExistingContentRootHot())
                return true;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return false;

            RectTransform existingRoot = FindExistingChild(canvasRoot, ContentRootName);
            if (existingRoot == null)
                return false;

            _contentRoot = SanitizeContentRoot(existingRoot);
            return _contentRoot != null;
        }

        private void ResolveCanvas()
        {
            if (_targetCanvas == null)
                TryGetComponent(out _targetCanvas);
        }

        private RectTransform EnsureContentRoot()
        {
            ResolveCanvas();
            return EnsureContentRootFromCachedCanvas();
        }

        private RectTransform EnsureContentRootFromCachedCanvas()
        {
            if (_targetCanvas == null)
                return null;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return null;

            if (_contentRoot == null || _contentRoot.gameObject == null)
                _contentRoot = FindExistingChild(canvasRoot, ContentRootName);

            if (_contentRoot == null)
            {
                // COLD ALLOC: GameObject[1] - matrix-scaled HUD content root - owner: HectonUIScaler
                GameObject rootObject = new GameObject(ContentRootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _contentRoot = rootObject.transform as RectTransform;
                if (_contentRoot == null)
                    return null;

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

        private void ApplyScale(bool force, bool allowContentRootCreation = true)
        {
            RectTransform contentRoot;
            if (allowContentRootCreation)
            {
                contentRoot = EnsureContentRoot();
            }
            else
            {
                if (!TryRefreshExistingContentRootHot())
                {
                    _pendingContentRootBootstrap = true;
                    return;
                }

                contentRoot = _contentRoot;
            }

            if (contentRoot == null)
                return;

            ApplyScaleToResolvedContentRoot(contentRoot, force);
        }

        private void ApplyScaleToExistingContentRoot(bool force)
        {
            if (!TryRefreshExistingContentRootHot())
                return;

            RectTransform contentRoot = _contentRoot;
            if (contentRoot == null)
                return;

            ApplyScaleToResolvedContentRoot(contentRoot, force);
        }

        private void ApplyScaleToResolvedContentRoot(RectTransform contentRoot, bool force)
        {
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
            if (UsesReferenceRenderDimensions())
            {
                width = RoundToIntFast(math.max(1f, referenceResolution.x));
                height = RoundToIntFast(math.max(1f, referenceResolution.y));
                return;
            }

            width = math.max(1, _cachedRenderWidth);
            height = math.max(1, _cachedRenderHeight);
        }

        private bool RefreshRenderDimensionsSlowSample()
        {
            int width;
            int height;
            if (UsesReferenceRenderDimensions())
            {
                // World-space HUD layout is already projected onto the visor frustum by the canvas rect itself.
                // Scaling again from RT resolution collapses the authored layout toward the center as the RT downsamples.
                width = RoundToIntFast(math.max(1f, referenceResolution.x));
                height = RoundToIntFast(math.max(1f, referenceResolution.y));
            }
            else
            {
                width = math.max(1, Screen.width);
                height = math.max(1, Screen.height);
            }

            bool changed = width != _cachedRenderWidth || height != _cachedRenderHeight;
            _cachedRenderWidth = width;
            _cachedRenderHeight = height;
            return changed;
        }

        private bool UsesReferenceRenderDimensions()
        {
            return _targetCanvas != null &&
                   _targetCanvas.renderMode == RenderMode.WorldSpace &&
                   _targetCanvas.worldCamera != null;
        }

        private float ComputeScale(int screenWidth, int screenHeight)
        {
            float scaleX = screenWidth * math.rcp(math.max(1f, referenceResolution.x));
            float scaleY = screenHeight * math.rcp(math.max(1f, referenceResolution.y));
            float blendedScale = math.lerp(scaleX, scaleY, matchWidthOrHeight);
            return math.clamp(blendedScale, minimumScale, maximumScale);
        }

        private void RegisterToTickManager()
        {
            if (!_runtimeScalerCallbacksActive || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTickManager)
            {
                _registeredToTickManager = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
            }

            if (_registeredToSlowTickManager)
                return;

            _registeredToSlowTickManager = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterFromTickManager();
            if (currentService != null && isActiveAndEnabled)
                RegisterToTickManager();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !_runtimeScalerCallbacksActive)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTickManager)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredToTickManager = false;
            }

            if (!_registeredToSlowTickManager)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
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
