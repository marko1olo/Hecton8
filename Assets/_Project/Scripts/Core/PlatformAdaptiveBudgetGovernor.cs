using System;
using Hecton8.Core.Contracts;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Low-cadence platform pressure governor for Deck/UMA, weak PCs, battery, and sustained frame pressure.
    /// </summary>
    public static class PlatformAdaptiveBudgetGovernor
    {
        private const int SampleIntervalFrames = 120;
        private const int StableFrostTickFrames = 120;
        private const int PressuredFrostTickFrames = 300;
        private const int CriticalFrostTickFrames = 600;
        private const int VramPressurePermille = 900;
        private const int DefaultRenderScaleMilli = 1000;
        private const int VramPressureRenderScaleMilli = 720;
        private const int FramePressureRenderScaleMilli = 700;
        private const int CriticalRenderScaleMilli = 620;
        private const float MillisecondsPerSecond = 1000f;
        private const float DefaultTargetFrameTimeMs = 16.67f;
        private const float CriticalFrameTimeMs = 25f;
        private const float FrameTrendAlpha = 0.125f;
        private const int SustainedFramePressureSamples = 3;
        private const int DefaultQualityWeightMilli = 1000;
        private const int MinimumQualityWeightMilli = 0;
        private const int PressureScaleMilli = 1000;
        private const int SecondaryHudEffectMinimumMilli = 350;
        private const float SharedMemoryPlatformPressure01 = 0.25f;
        private const float SteamDeckPlatformPressure01 = 0.20f;
        private const float VramPressureSoftStart01 = 0.75f;
        private const float VramPressureHard01 = 0.98f;
        private const float ThermalThrottleBasePressure01 = 0.65f;

        // COLD ALLOC: AdaptiveBudgetTickable[1] - dispatcher-owned platform pressure sampler - owner: PlatformAdaptiveBudgetGovernor
        private static readonly AdaptiveBudgetTickable s_tickable = new AdaptiveBudgetTickable();

        private static bool _registered;
        private static bool _lateFrameRegistered;
        private static bool _hotSwapRegistered;
        private static bool _platformRenderScaleApplied;
        private static bool _dynamicResolutionDirty;
        private static bool _pendingDynamicResolutionPressure;
        private static int _pendingDynamicResolutionRenderScaleMilli = DefaultRenderScaleMilli;
        private static uint _pressureFlags;
        private static int _pressureIntensityMilli;
        private static int _recommendedQualityWeightMilli = DefaultQualityWeightMilli;
        private static int _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
        private static int _frostTickIntervalFrames = StableFrostTickFrames;
        private static int _secondaryHudEffectWeightMilli = DefaultQualityWeightMilli;
        private static bool _secondaryHudEffectsAllowed = true;
        private static float _frameTimeTrendMs = DefaultTargetFrameTimeMs;
        private static int _sustainedFramePressureSamples;
        private static bool _hasFrameTimeSample;
        private static IHardwareThermalService _hardwareThermalService;
        private static IDynamicResolutionRuntime _dynamicResolutionRuntime;

        /// <summary>Current platform pressure flags packed for zero-allocation diagnostics.</summary>
        public static PlatformAdaptivePressureFlags PressureFlags =>
            (PlatformAdaptivePressureFlags)_pressureFlags;

        /// <summary>Continuous platform pressure encoded as thousandths.</summary>
        public static int PressureIntensityMilli => _pressureIntensityMilli;

        /// <summary>Continuous platform pressure for cold diagnostics.</summary>
        public static float PressureIntensity => _pressureIntensityMilli * 0.001f;

        /// <summary>Continuous quality recommendation encoded as thousandths.</summary>
        public static int RecommendedQualityWeightMilli => _recommendedQualityWeightMilli;

        /// <summary>Continuous quality recommendation for cold-path callers.</summary>
        public static float RecommendedQualityWeight => _recommendedQualityWeightMilli * 0.001f;

        /// <summary>Recommended render scale encoded as thousandths to avoid formatting/rounding in callers.</summary>
        public static int RecommendedRenderScaleMilli => _recommendedRenderScaleMilli;

        /// <summary>Recommended render scale for cold-path callers.</summary>
        public static float RecommendedRenderScale => _recommendedRenderScaleMilli * 0.001f;

        /// <summary>Recommended cadence for non-critical FrostTick systems.</summary>
        public static int FrostTickIntervalFrames => _frostTickIntervalFrames;

        /// <summary>Continuous optional HUD effect budget encoded as thousandths.</summary>
        public static int SecondaryHudEffectWeightMilli => _secondaryHudEffectWeightMilli;

        /// <summary>Continuous optional HUD effect budget for cold-path callers.</summary>
        public static float SecondaryHudEffectWeight => _secondaryHudEffectWeightMilli * 0.001f;

        /// <summary>False while the platform is under battery/VRAM/frame pressure.</summary>
        public static bool SecondaryHudEffectsAllowed => _secondaryHudEffectsAllowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TryUnregisterHotSwap();
            _registered = false;
            _lateFrameRegistered = false;
            _platformRenderScaleApplied = false;
            _dynamicResolutionDirty = false;
            _pendingDynamicResolutionPressure = false;
            _pendingDynamicResolutionRenderScaleMilli = DefaultRenderScaleMilli;
            _pressureFlags = 0u;
            _pressureIntensityMilli = 0;
            _recommendedQualityWeightMilli = DefaultQualityWeightMilli;
            _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
            _frostTickIntervalFrames = StableFrostTickFrames;
            _secondaryHudEffectWeightMilli = DefaultQualityWeightMilli;
            _secondaryHudEffectsAllowed = true;
            _frameTimeTrendMs = DefaultTargetFrameTimeMs;
            _sustainedFramePressureSamples = 0;
            _hasFrameTimeSample = false;
            _hardwareThermalService = null;
            _dynamicResolutionRuntime = null;
            s_tickable.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            RebindServicesCold();
            TryRegisterHotSwap();
            SampleAndApply(0f);
            TryRegister();
        }

        /// <summary>
        /// Samples platform pressure and applies continuous presentation recommendations.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time in seconds.</param>
        public static void SampleAndApply(float deltaTime)
        {
            HardwareTierDetector.EnsureInitialized();

            uint flags = 0u;
            if (HardwareTierDetector.SharedMemoryModeActive)
                flags |= (uint)PlatformAdaptivePressureFlags.SharedMemory;
            if (HardwareTierDetector.IsSteamDeckLike)
                flags |= (uint)PlatformAdaptivePressureFlags.SteamDeckLike;
            if (IsVramNearBudget())
                flags |= (uint)PlatformAdaptivePressureFlags.VramNearBudget;
            if (IsCriticalBattery())
                flags |= (uint)PlatformAdaptivePressureFlags.CriticalBattery;
            if (IsThermalThrottling(out bool thermalCritical))
                flags |= (uint)(thermalCritical
                    ? PlatformAdaptivePressureFlags.ThermalCritical
                    : PlatformAdaptivePressureFlags.ThermalThrottling);
            if (IsFrameOverBudget(deltaTime))
                flags |= (uint)PlatformAdaptivePressureFlags.FrameOverBudget;

            float pressure01 = ResolvePlatformPressure01(flags);
            float pressureCurve = SmoothStep01(pressure01);
            float globalQualityWeight01 = SanitizeGlobalQualityWeight01(HomeostasisBrain.GlobalQualityWeight);
            bool pressured = pressure01 > 0.001f;

            _pressureFlags = flags;
            _pressureIntensityMilli = EncodeMilli(pressure01);
            _recommendedQualityWeightMilli = ResolveQualityWeightMilli(pressureCurve, globalQualityWeight01);
            _recommendedRenderScaleMilli = ResolveRenderScaleMilli(flags, pressureCurve);
            _frostTickIntervalFrames = ResolveFrostTickIntervalFrames(pressureCurve);
            _secondaryHudEffectWeightMilli = ResolveSecondaryHudEffectWeightMilli(pressureCurve, globalQualityWeight01);
            _secondaryHudEffectsAllowed = _secondaryHudEffectWeightMilli >= SecondaryHudEffectMinimumMilli;

            QueueDynamicResolutionPressureVisualSync(pressured, _recommendedRenderScaleMilli);
        }

        private static void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(s_tickable, PriorityLayer.Core);
            if (_registered && !_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(s_tickable, PriorityLayer.Core);
        }

        private static bool IsVramNearBudget()
        {
            long budgetBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
            if (budgetBytes <= 0L)
                return false;

            long pressureBytes = budgetBytes * VramPressurePermille / 1000L;
            return VRAMBudgetTracker.EstimatedVRAMBytes >= pressureBytes;
        }

        private static float ResolveVramPressure01()
        {
            long budgetBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
            if (budgetBytes <= 0L)
                return 0f;

            float ratio = (float)VRAMBudgetTracker.EstimatedVRAMBytes / budgetBytes;
            return math.saturate(
                (ratio - VramPressureSoftStart01) *
                math.rcp(math.max(0.0001f, VramPressureHard01 - VramPressureSoftStart01)));
        }

        private static bool IsCriticalBattery()
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
                return false;

            return PlatformBatteryWatchdog.IsCriticalBattery(hardware);
        }

        private static bool IsThermalThrottling(out bool critical)
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
            {
                critical = false;
                return false;
            }

            byte severity = hardware.CurrentSeverity;
            critical = severity >= (byte)HardwareThermalSeverity.Critical;
            return severity >= (byte)HardwareThermalSeverity.Throttling;
        }

        private static float ResolveThermalPressure01()
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
                return 0f;

            byte severity = hardware.CurrentSeverity;
            byte throttle = (byte)HardwareThermalSeverity.Throttling;
            byte critical = (byte)HardwareThermalSeverity.Critical;
            if (severity < throttle)
                return 0f;
            if (severity >= critical)
                return 1f;

            float thermal01 = (severity - throttle) * math.rcp(math.max(1f, critical - throttle));
            return math.lerp(ThermalThrottleBasePressure01, 1f, math.saturate(thermal01));
        }

        private static bool IsFrameOverBudget(float deltaTime)
        {
            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            float frameTimeMs = deltaTime > 0f ? deltaTime * MillisecondsPerSecond : targetFrameTimeMs;
            if (float.IsNaN(frameTimeMs) || float.IsInfinity(frameTimeMs))
                frameTimeMs = targetFrameTimeMs;

            if (!_hasFrameTimeSample)
            {
                _frameTimeTrendMs = frameTimeMs;
                _hasFrameTimeSample = true;
            }
            else
            {
                _frameTimeTrendMs += (frameTimeMs - _frameTimeTrendMs) * FrameTrendAlpha;
            }

            if (_frameTimeTrendMs >= CriticalFrameTimeMs)
            {
                _sustainedFramePressureSamples = SustainedFramePressureSamples;
                return true;
            }

            if (_frameTimeTrendMs > targetFrameTimeMs)
            {
                _sustainedFramePressureSamples++;
                return _sustainedFramePressureSamples >= SustainedFramePressureSamples;
            }

            _sustainedFramePressureSamples = 0;
            return false;
        }

        private static float ResolveFramePressure01()
        {
            if (!_hasFrameTimeSample)
                return 0f;

            float targetFrameTimeMs = ResolveTargetFrameTimeMs();
            float pressure = (_frameTimeTrendMs - targetFrameTimeMs) *
                             math.rcp(math.max(0.0001f, CriticalFrameTimeMs - targetFrameTimeMs));
            return math.saturate(pressure);
        }

        private static float ResolveTargetFrameTimeMs()
        {
            if (HardwareTierDetector.IsQuest3Like)
                return MillisecondsPerSecond / HardwareProfileCatalog.Quest3TargetFps;
            if (HardwareTierDetector.IsSteamDeckLike)
                return MillisecondsPerSecond / HardwareProfileCatalog.SteamDeckLcdTargetFps;

            return DefaultTargetFrameTimeMs;
        }

        private static float ResolvePlatformPressure01(uint flags)
        {
            float pressure = 0f;
            if ((flags & (uint)PlatformAdaptivePressureFlags.SharedMemory) != 0u)
                pressure = math.max(pressure, SharedMemoryPlatformPressure01);
            if ((flags & (uint)PlatformAdaptivePressureFlags.SteamDeckLike) != 0u)
                pressure = math.max(pressure, SteamDeckPlatformPressure01);
            if ((flags & (uint)PlatformAdaptivePressureFlags.VramNearBudget) != 0u)
                pressure = math.max(pressure, ResolveVramPressure01());
            if ((flags & (uint)PlatformAdaptivePressureFlags.CriticalBattery) != 0u)
                pressure = math.max(pressure, PlatformBatteryWatchdog.ResolveCriticalBatteryPressure01(_hardwareThermalService));
            if ((flags & (uint)(PlatformAdaptivePressureFlags.ThermalThrottling |
                                PlatformAdaptivePressureFlags.ThermalCritical)) != 0u)
                pressure = math.max(pressure, ResolveThermalPressure01());
            if ((flags & (uint)PlatformAdaptivePressureFlags.FrameOverBudget) != 0u)
                pressure = math.max(pressure, ResolveFramePressure01());

            return math.saturate(pressure);
        }

        private static int ResolveQualityWeightMilli(float pressureCurve, float globalQualityWeight01)
        {
            float pressureLimitedWeight01 = math.lerp(1f, MinimumQualityWeightMilli * 0.001f, math.saturate(pressureCurve));
            float weight = math.min(SanitizeGlobalQualityWeight01(globalQualityWeight01), pressureLimitedWeight01) *
                           PressureScaleMilli;
            return math.clamp((int)math.round(weight), MinimumQualityWeightMilli, DefaultQualityWeightMilli);
        }

        private static int ResolveFrostTickIntervalFrames(float pressureCurve)
        {
            float safePressure = math.saturate(pressureCurve);
            float interval = safePressure <= 0.5f
                ? math.lerp(StableFrostTickFrames, PressuredFrostTickFrames, safePressure * 2f)
                : math.lerp(PressuredFrostTickFrames, CriticalFrostTickFrames, (safePressure - 0.5f) * 2f);
            return math.clamp((int)math.round(interval), StableFrostTickFrames, CriticalFrostTickFrames);
        }

        private static int ResolveRenderScaleMilli(uint flags, float pressureCurve)
        {
            int baseline = DefaultRenderScaleMilli;
            int globalTarget = EncodeMilli(HomeostasisBrain.TargetRenderScale01);
            if (globalTarget > 0)
                baseline = math.min(baseline, math.clamp(globalTarget, CriticalRenderScaleMilli, DefaultRenderScaleMilli));

            if ((flags & (uint)PlatformAdaptivePressureFlags.SteamDeckLike) != 0u)
                baseline = math.min(baseline, HardwareProfileCatalog.SteamDeckLcdBaselineRenderScaleMilli);
            if ((flags & (uint)PlatformAdaptivePressureFlags.SharedMemory) != 0u)
            {
                int sharedBaseline = HardwareTierDetector.IsQuest3Like
                    ? HardwareProfileCatalog.Quest3BaselineRenderScaleMilli
                    : HardwareProfileCatalog.SteamDeckLcdBaselineRenderScaleMilli;
                baseline = math.min(baseline, sharedBaseline);
            }

            int pressureTarget = baseline;
            if ((flags & (uint)(PlatformAdaptivePressureFlags.CriticalBattery |
                                PlatformAdaptivePressureFlags.ThermalCritical)) != 0u)
                pressureTarget = CriticalRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.ThermalThrottling) != 0u)
                pressureTarget = math.min(pressureTarget, FramePressureRenderScaleMilli);
            if ((flags & (uint)PlatformAdaptivePressureFlags.VramNearBudget) != 0u)
                pressureTarget = math.min(pressureTarget, VramPressureRenderScaleMilli);
            if ((flags & (uint)PlatformAdaptivePressureFlags.FrameOverBudget) != 0u)
                pressureTarget = math.min(pressureTarget, FramePressureRenderScaleMilli);

            float scale = math.lerp(baseline, pressureTarget, math.saturate(pressureCurve));
            return math.clamp((int)math.round(scale), CriticalRenderScaleMilli, DefaultRenderScaleMilli);
        }

        private static int ResolveSecondaryHudEffectWeightMilli(float pressureCurve, float globalQualityWeight01)
        {
            float pressureBudget01 = 1f - math.saturate(pressureCurve);
            float weight01 = math.min(SanitizeGlobalQualityWeight01(globalQualityWeight01), pressureBudget01);
            return EncodeMilli(weight01);
        }

        private static float SanitizeGlobalQualityWeight01(float qualityWeight01)
        {
            if (!math.isfinite(qualityWeight01))
                return 1f;

            return math.saturate(qualityWeight01);
        }

        private static int EncodeMilli(float value01)
        {
            return math.clamp((int)math.round(math.saturate(value01) * PressureScaleMilli), 0, PressureScaleMilli);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static void QueueDynamicResolutionPressureVisualSync(bool pressured, int renderScaleMilli)
        {
            _pendingDynamicResolutionPressure = pressured;
            _pendingDynamicResolutionRenderScaleMilli = math.clamp(
                renderScaleMilli,
                CriticalRenderScaleMilli,
                DefaultRenderScaleMilli);
            _dynamicResolutionDirty = true;
        }

        private static void FlushDynamicResolutionPressureLateFrame()
        {
            if (!_dynamicResolutionDirty)
                return;

            IDynamicResolutionRuntime runtime = _dynamicResolutionRuntime;
            if (runtime == null)
                return;

            _dynamicResolutionDirty = false;
            if (_pendingDynamicResolutionPressure)
            {
                float targetScale = _pendingDynamicResolutionRenderScaleMilli * 0.001f;
                runtime.SetPlatformPressureRenderScale(true, targetScale, targetScale);
                _platformRenderScaleApplied = true;
                return;
            }

            if (!_platformRenderScaleApplied)
                return;

            runtime.SetPlatformPressureRenderScale(false, 1f, 1f);
            _platformRenderScaleApplied = false;
        }

        private static void RebindServicesCold()
        {
            _hardwareThermalService = GlobalRegistry.HardwareThermal;
            _dynamicResolutionRuntime = GlobalRegistry.DynamicResolutionRuntime;
        }

        private static void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_tickable);
        }

        private static void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(s_tickable);
            _hotSwapRegistered = false;
        }

        private static void RebindService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.HardwareThermalService)
            {
                _hardwareThermalService = currentService as IHardwareThermalService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime)
            {
                _dynamicResolutionRuntime = currentService as IDynamicResolutionRuntime;
                _dynamicResolutionDirty = true;
            }
        }

        private sealed class AdaptiveBudgetTickable :
            IUpdatable,
            ILateFrameTickable,
            IGlobalRegistryHotSwapListener,
            IGlobalRegistryHotSwapRefListener
        {
            private int _nextSampleFrame;

            public void Reset()
            {
                _nextSampleFrame = 0;
            }

            public void Tick(float deltaTime)
            {
                int frame = SystemDispatcher.CurrentFrameIndex;
                if (frame < _nextSampleFrame)
                    return;

                _nextSampleFrame = frame + SampleIntervalFrames;
                PlatformAdaptiveBudgetGovernor.SampleAndApply(deltaTime);
            }

            public void LateFrameTick()
            {
                PlatformAdaptiveBudgetGovernor.FlushDynamicResolutionPressureLateFrame();
            }

            public void OnGlobalRegistryServiceRebound(
                GlobalRegistryServiceSlot serviceSlot,
                ref object currentService)
            {
                RebindService(serviceSlot, currentService);
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                RebindService(serviceSlot, currentService);
            }
        }
    }

    /// <summary>
    /// Platform pressure reasons packed as a bitmask to avoid per-frame strings.
    /// </summary>
    [Flags]
    public enum PlatformAdaptivePressureFlags : uint
    {
        None = 0u,
        SharedMemory = 1u << 0,
        SteamDeckLike = 1u << 1,
        VramNearBudget = 1u << 2,
        CriticalBattery = 1u << 3,
        FrameOverBudget = 1u << 4,
        ThermalThrottling = 1u << 5,
        ThermalCritical = 1u << 6
    }
}
