using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Compatibility facade for legacy battery clamps. HardwareThermalService owns all platform polling.
    /// </summary>
    public static class PlatformBatteryWatchdog
    {
        private const byte CriticalBatteryPercent = 15;
        private static bool _criticalQualityApplied;

        /// <summary>
        /// True after the watchdog has forced the minimum quality level for critical battery.
        /// </summary>
        public static bool CriticalQualityApplied => _criticalQualityApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _criticalQualityApplied = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            SampleAndApply();
        }

        /// <summary>
        /// Applies the critical quality clamp from cached hardware telemetry only.
        /// </summary>
        public static void SampleAndApply()
        {
            if (_criticalQualityApplied)
                return;

            IHardwareThermalService hardware = GlobalRegistry.HardwareThermal;
            if (hardware == null)
                return;

            byte batteryPercent = hardware.BatteryPercent;
            if (batteryPercent == 0 || batteryPercent >= CriticalBatteryPercent)
                return;

            if (QualitySettings.GetQualityLevel() != 0)
                QualitySettings.SetQualityLevel(0, true);

            GlobalRegistry.RegisterScalabilityTierOverride(ScalabilityTierProfiles.LowMx350);
            _criticalQualityApplied = true;
        }
    }
}
