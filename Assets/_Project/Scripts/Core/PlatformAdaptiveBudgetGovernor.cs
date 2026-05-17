using System;
using Hecton8.Core.Contracts;
using Hecton8.World;
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

        // COLD ALLOC: AdaptiveBudgetTickable[1] - dispatcher-owned platform pressure sampler - owner: PlatformAdaptiveBudgetGovernor
        private static readonly AdaptiveBudgetTickable s_tickable = new AdaptiveBudgetTickable();

        private static bool _registered;
        private static bool _hotSwapRegistered;
        private static bool _lowTierApplied;
        private static bool _platformRenderScaleApplied;
        private static uint _pressureFlags;
        private static int _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
        private static int _frostTickIntervalFrames = StableFrostTickFrames;
        private static bool _secondaryHudEffectsAllowed = true;
        private static float _frameTimeTrendMs = DefaultTargetFrameTimeMs;
        private static int _sustainedFramePressureSamples;
        private static bool _hasFrameTimeSample;
        private static IHardwareThermalService _hardwareThermalService;
        private static DynamicResolutionScaler _dynamicResolutionScaler;

        /// <summary>Current platform pressure flags packed for zero-allocation diagnostics.</summary>
        public static PlatformAdaptivePressureFlags PressureFlags =>
            (PlatformAdaptivePressureFlags)_pressureFlags;

        /// <summary>Recommended render scale encoded as thousandths to avoid formatting/rounding in callers.</summary>
        public static int RecommendedRenderScaleMilli => _recommendedRenderScaleMilli;

        /// <summary>Recommended render scale for cold-path callers.</summary>
        public static float RecommendedRenderScale => _recommendedRenderScaleMilli * 0.001f;

        /// <summary>Recommended cadence for non-critical FrostTick systems.</summary>
        public static int FrostTickIntervalFrames => _frostTickIntervalFrames;

        /// <summary>False while the platform is under battery/VRAM/frame pressure.</summary>
        public static bool SecondaryHudEffectsAllowed => _secondaryHudEffectsAllowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TryUnregisterHotSwap();
            _registered = false;
            _lowTierApplied = false;
            _platformRenderScaleApplied = false;
            _pressureFlags = 0u;
            _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
            _frostTickIntervalFrames = StableFrostTickFrames;
            _secondaryHudEffectsAllowed = true;
            _frameTimeTrendMs = DefaultTargetFrameTimeMs;
            _sustainedFramePressureSamples = 0;
            _hasFrameTimeSample = false;
            _hardwareThermalService = null;
            _dynamicResolutionScaler = null;
            GlobalRegistry.SetTransientLowScalabilityOverride(
                GlobalRegistry.TransientScalabilityPlatformPressureMask,
                false);
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
        /// Samples platform pressure and applies the lowest-risk global clamps.
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

            bool pressured = flags != 0u;
            bool critical = (flags & (uint)(PlatformAdaptivePressureFlags.CriticalBattery |
                                            PlatformAdaptivePressureFlags.ThermalCritical |
                                            PlatformAdaptivePressureFlags.VramNearBudget |
                                            PlatformAdaptivePressureFlags.FrameOverBudget)) != 0u;

            _pressureFlags = flags;
            _recommendedRenderScaleMilli = ResolveRenderScaleMilli(flags);
            _frostTickIntervalFrames = critical ? CriticalFrostTickFrames : pressured ? PressuredFrostTickFrames : StableFrostTickFrames;
            _secondaryHudEffectsAllowed = !critical;

            if (pressured != _lowTierApplied)
            {
                GlobalRegistry.SetTransientLowScalabilityOverride(
                    GlobalRegistry.TransientScalabilityPlatformPressureMask,
                    pressured);
                _lowTierApplied = pressured;
            }

            ApplyDynamicResolutionPressure(pressured);
        }

        private static void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(s_tickable, PriorityLayer.Core);
        }

        private static bool IsVramNearBudget()
        {
            long budgetBytes = HardwareTierDetector.RecommendedVramBudgetBytes;
            if (budgetBytes <= 0L)
                return false;

            long pressureBytes = budgetBytes * VramPressurePermille / 1000L;
            return VRAMBudgetTracker.EstimatedVRAMBytes >= pressureBytes;
        }

        private static bool IsCriticalBattery()
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
                return false;

            byte batteryPercent = hardware.BatteryPercent;
            return batteryPercent > 0 && batteryPercent < 15;
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

        private static float ResolveTargetFrameTimeMs()
        {
            if (HardwareTierDetector.IsQuest3Like)
                return MillisecondsPerSecond / HardwareProfileCatalog.Quest3TargetFps;
            if (HardwareTierDetector.IsSteamDeckLike)
                return MillisecondsPerSecond / HardwareProfileCatalog.SteamDeckLcdTargetFps;

            return DefaultTargetFrameTimeMs;
        }

        private static int ResolveRenderScaleMilli(uint flags)
        {
            if ((flags & (uint)(PlatformAdaptivePressureFlags.CriticalBattery |
                                PlatformAdaptivePressureFlags.ThermalCritical)) != 0u)
                return CriticalRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.ThermalThrottling) != 0u)
                return FramePressureRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.VramNearBudget) != 0u)
                return VramPressureRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.FrameOverBudget) != 0u)
                return FramePressureRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.SteamDeckLike) != 0u)
                return HardwareProfileCatalog.SteamDeckLcdBaselineRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.SharedMemory) != 0u)
            {
                return HardwareTierDetector.IsQuest3Like
                    ? HardwareProfileCatalog.Quest3BaselineRenderScaleMilli
                    : HardwareProfileCatalog.SteamDeckLcdBaselineRenderScaleMilli;
            }

            return DefaultRenderScaleMilli;
        }

        private static void ApplyDynamicResolutionPressure(bool pressured)
        {
            DynamicResolutionScaler scaler = _dynamicResolutionScaler;
            if (scaler == null)
                return;

            if (pressured)
            {
                float targetScale = _recommendedRenderScaleMilli * 0.001f;
                scaler.SetPlatformPressureRenderScale(true, targetScale, targetScale);
                _platformRenderScaleApplied = true;
                return;
            }

            if (!_platformRenderScaleApplied)
                return;

            scaler.SetPlatformPressureRenderScale(false, 1f, 1f);
            _platformRenderScaleApplied = false;
        }

        private static void RebindServicesCold()
        {
            _hardwareThermalService = GlobalRegistry.HardwareThermal;
            _dynamicResolutionScaler = GlobalRegistry.DynamicResolution;
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
                _dynamicResolutionScaler = currentService as DynamicResolutionScaler;
        }

        private sealed class AdaptiveBudgetTickable : IUpdatable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
        {
            private int _nextSampleFrame;

            public void Reset()
            {
                _nextSampleFrame = 0;
            }

            public void Tick(float deltaTime)
            {
                int frame = Time.frameCount;
                if (frame < _nextSampleFrame)
                    return;

                _nextSampleFrame = frame + SampleIntervalFrames;
                PlatformAdaptiveBudgetGovernor.SampleAndApply(deltaTime);
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
