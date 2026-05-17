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
            GlobalRegistry.SetTransientLowScalabilityOverride(
                GlobalRegistry.TransientScalabilityBatteryPressureMask,
                false);
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
            IHardwareThermalService hardware = GlobalRegistry.HardwareThermal;
            if (hardware == null)
                return;

            byte batteryPercent = hardware.BatteryPercent;
            bool criticalBattery = batteryPercent > 0 && batteryPercent < CriticalBatteryPercent;
            if (criticalBattery == _criticalQualityApplied)
                return;

            GlobalRegistry.SetTransientLowScalabilityOverride(
                GlobalRegistry.TransientScalabilityBatteryPressureMask,
                criticalBattery);
            _criticalQualityApplied = criticalBattery;
        }
    }
}
