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
        private const byte BatteryStatusDischargingAndroid = 3;
        private static readonly HardwareServiceHotSwapBridge s_hotSwapBridge = new HardwareServiceHotSwapBridge();
        private static IHardwareThermalService _hardwareThermalService;
        private static bool _criticalQualityApplied;
        private static bool _hotSwapRegistered;

        /// <summary>
        /// True after the watchdog has forced the minimum quality level for critical battery.
        /// </summary>
        public static bool CriticalQualityApplied => _criticalQualityApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TryUnregisterHotSwap();
            GlobalRegistry.SetTransientLowScalabilityOverride(
                GlobalRegistry.TransientScalabilityBatteryPressureMask,
                false);
            _hardwareThermalService = null;
            _criticalQualityApplied = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            RebindHardwareServiceCold();
            TryRegisterHotSwap();
            SampleAndApply();
        }

        /// <summary>
        /// Applies the critical quality clamp from cached hardware telemetry only.
        /// </summary>
        public static void SampleAndApply()
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
            {
                if (_criticalQualityApplied)
                {
                    GlobalRegistry.SetTransientLowScalabilityOverride(
                        GlobalRegistry.TransientScalabilityBatteryPressureMask,
                        false);
                    _criticalQualityApplied = false;
                }

                return;
            }

            byte batteryPercent = hardware.BatteryPercent;
            bool criticalBattery = IsCriticalBattery(hardware, batteryPercent);
            if (criticalBattery == _criticalQualityApplied)
                return;

            GlobalRegistry.SetTransientLowScalabilityOverride(
                GlobalRegistry.TransientScalabilityBatteryPressureMask,
                criticalBattery);
            _criticalQualityApplied = criticalBattery;
        }

        public static void SampleAndApply(IHardwareThermalService hardware)
        {
            if (hardware != null)
                _hardwareThermalService = hardware;

            SampleAndApply();
        }

        internal static bool IsCriticalBattery(IHardwareThermalService hardware)
        {
            if (hardware == null)
                return false;

            return IsCriticalBattery(hardware, hardware.BatteryPercent);
        }

        private static bool IsCriticalBattery(IHardwareThermalService hardware, byte fallbackBatteryPercent)
        {
            if (hardware != null && hardware.TryGetSnapshot(out HardwareThermalSnapshot snapshot))
            {
                return snapshot.BatteryPercent > 0 &&
                       snapshot.BatteryPercent < CriticalBatteryPercent &&
                       IsDischarging(snapshot.BatteryStatus);
            }

            return fallbackBatteryPercent > 0 && fallbackBatteryPercent < CriticalBatteryPercent;
        }

        private static bool IsDischarging(byte batteryStatus)
        {
            return batteryStatus == BatteryStatusDischargingAndroid ||
                   batteryStatus == (byte)BatteryStatus.Discharging;
        }

        private static void RebindHardwareServiceCold()
        {
            _hardwareThermalService = GlobalRegistry.HardwareThermal;
        }

        private static void RebindHardwareService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.HardwareThermalService)
                return;

            _hardwareThermalService = currentService as IHardwareThermalService;
            SampleAndApply();
        }

        private static void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_hotSwapBridge);
        }

        private static void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(s_hotSwapBridge);
            _hotSwapRegistered = false;
        }

        private sealed class HardwareServiceHotSwapBridge : IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
        {
            public void OnGlobalRegistryServiceRebound(
                GlobalRegistryServiceSlot serviceSlot,
                ref object currentService)
            {
                RebindHardwareService(serviceSlot, currentService);
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                RebindHardwareService(serviceSlot, currentService);
            }
        }
    }
}
