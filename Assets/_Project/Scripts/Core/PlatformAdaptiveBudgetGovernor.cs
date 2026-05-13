using System;
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
        private const int DeckRenderScaleMilli = 780;
        private const int VramPressureRenderScaleMilli = 720;
        private const int FramePressureRenderScaleMilli = 700;
        private const int CriticalRenderScaleMilli = 620;
        private const float TargetFrameTimeMs = 16.67f;
        private const float CriticalFrameTimeMs = 25f;
        private const float FrameTrendAlpha = 0.125f;
        private const int SustainedFramePressureSamples = 3;

        // COLD ALLOC: AdaptiveBudgetTickable[1] - dispatcher-owned platform pressure sampler - owner: PlatformAdaptiveBudgetGovernor
        private static readonly AdaptiveBudgetTickable s_tickable = new AdaptiveBudgetTickable();

        private static bool _registered;
        private static bool _lowTierApplied;
        private static bool _platformRenderScaleApplied;
        private static uint _pressureFlags;
        private static int _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
        private static int _frostTickIntervalFrames = StableFrostTickFrames;
        private static bool _secondaryHudEffectsAllowed = true;
        private static float _frameTimeTrendMs = TargetFrameTimeMs;
        private static int _sustainedFramePressureSamples;

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
            _registered = false;
            _lowTierApplied = false;
            _platformRenderScaleApplied = false;
            _pressureFlags = 0u;
            _recommendedRenderScaleMilli = DefaultRenderScaleMilli;
            _frostTickIntervalFrames = StableFrostTickFrames;
            _secondaryHudEffectsAllowed = true;
            _frameTimeTrendMs = TargetFrameTimeMs;
            _sustainedFramePressureSamples = 0;
            s_tickable.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
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
            if (IsFrameOverBudget(deltaTime))
                flags |= (uint)PlatformAdaptivePressureFlags.FrameOverBudget;

            bool pressured = flags != 0u;
            bool critical = (flags & (uint)(PlatformAdaptivePressureFlags.CriticalBattery |
                                            PlatformAdaptivePressureFlags.VramNearBudget |
                                            PlatformAdaptivePressureFlags.FrameOverBudget)) != 0u;

            _pressureFlags = flags;
            _recommendedRenderScaleMilli = ResolveRenderScaleMilli(flags);
            _frostTickIntervalFrames = critical ? CriticalFrostTickFrames : pressured ? PressuredFrostTickFrames : StableFrostTickFrames;
            _secondaryHudEffectsAllowed = !critical;

            if (pressured && !_lowTierApplied)
            {
                GlobalRegistry.RegisterScalabilityTierOverride(ScalabilityTierProfiles.LowMx350);
                _lowTierApplied = true;
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
            float level = SystemInfo.batteryLevel;
            if (float.IsNaN(level) || float.IsInfinity(level) || level < 0f)
                return false;

            return level < 0.15f &&
                   SystemInfo.batteryStatus == BatteryStatus.Discharging;
        }

        private static bool IsFrameOverBudget(float deltaTime)
        {
            float frameTimeMs = deltaTime > 0f ? deltaTime * 1000f : TargetFrameTimeMs;
            if (float.IsNaN(frameTimeMs) || float.IsInfinity(frameTimeMs))
                frameTimeMs = TargetFrameTimeMs;

            _frameTimeTrendMs += (frameTimeMs - _frameTimeTrendMs) * FrameTrendAlpha;
            if (_frameTimeTrendMs >= CriticalFrameTimeMs)
            {
                _sustainedFramePressureSamples = SustainedFramePressureSamples;
                return true;
            }

            if (_frameTimeTrendMs > TargetFrameTimeMs)
            {
                _sustainedFramePressureSamples++;
                return _sustainedFramePressureSamples >= SustainedFramePressureSamples;
            }

            _sustainedFramePressureSamples = 0;
            return false;
        }

        private static int ResolveRenderScaleMilli(uint flags)
        {
            if ((flags & (uint)PlatformAdaptivePressureFlags.CriticalBattery) != 0u)
                return CriticalRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.VramNearBudget) != 0u)
                return VramPressureRenderScaleMilli;
            if ((flags & (uint)PlatformAdaptivePressureFlags.FrameOverBudget) != 0u)
                return FramePressureRenderScaleMilli;
            if ((flags & (uint)(PlatformAdaptivePressureFlags.SharedMemory | PlatformAdaptivePressureFlags.SteamDeckLike)) != 0u)
                return DeckRenderScaleMilli;

            return DefaultRenderScaleMilli;
        }

        private static void ApplyDynamicResolutionPressure(bool pressured)
        {
            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
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

        private sealed class AdaptiveBudgetTickable : IUpdatable
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
        FrameOverBudget = 1u << 4
    }
}
