using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Low-cadence battery policy: drop runtime quality when portable hardware is critically low.
    /// </summary>
    public static class PlatformBatteryWatchdog
    {
        private const float CriticalBatteryLevel = 0.15f;
        private const int SampleIntervalFrames = 300;

        // COLD ALLOC: BatteryWatchdogTickable[1] - dispatcher-owned low-cadence battery sampler - owner: PlatformBatteryWatchdog
        private static readonly BatteryWatchdogTickable s_tickable = new BatteryWatchdogTickable();
        private static bool _registered;
        private static bool _criticalQualityApplied;

        /// <summary>
        /// True after the watchdog has forced the minimum quality level for critical battery.
        /// </summary>
        public static bool CriticalQualityApplied => _criticalQualityApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _registered = false;
            _criticalQualityApplied = false;
            s_tickable.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            SampleAndApply();
            TryRegister();
        }

        /// <summary>
        /// Samples the platform battery state and applies the critical quality clamp if needed.
        /// </summary>
        public static void SampleAndApply()
        {
            if (_criticalQualityApplied)
                return;

            if (!IsCriticalBattery())
                return;

            if (QualitySettings.GetQualityLevel() != 0)
                QualitySettings.SetQualityLevel(0, true);

            GlobalRegistry.RegisterScalabilityTierOverride(ScalabilityTierProfiles.LowMx350);
            _criticalQualityApplied = true;
        }

        private static void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(s_tickable, PriorityLayer.Core);
        }

        private static bool IsCriticalBattery()
        {
            float level = SystemInfo.batteryLevel;
            if (!math.isfinite(level) || level < 0f)
                return false;

            return level < CriticalBatteryLevel &&
                   SystemInfo.batteryStatus == BatteryStatus.Discharging;
        }

        private sealed class BatteryWatchdogTickable : IUpdatable
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
                PlatformBatteryWatchdog.SampleAndApply();
            }
        }
    }
}
