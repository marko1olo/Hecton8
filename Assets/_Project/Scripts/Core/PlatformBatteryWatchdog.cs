using Hecton8.Core.Contracts;
using Unity.Mathematics;
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
        private const int BatteryPressureScale = 1000;
        private const int CriticalBatteryBasePressureMilli = 650;
        private static readonly HardwareServiceHotSwapBridge s_hotSwapBridge = new HardwareServiceHotSwapBridge();
        private static IHardwareThermalService _hardwareThermalService;
        private static bool _criticalQualityApplied;
        private static int _criticalBatteryPressureMilli;
        private static bool _hotSwapRegistered;

        /// <summary>
        /// True while critical battery pressure is active.
        /// </summary>
        public static bool CriticalQualityApplied => _criticalQualityApplied;

        /// <summary>
        /// Continuous critical-battery pressure encoded as thousandths.
        /// </summary>
        public static int CriticalBatteryPressureMilli => _criticalBatteryPressureMilli;

        /// <summary>
        /// Continuous critical-battery pressure for cold diagnostics.
        /// </summary>
        public static float CriticalBatteryPressure01 => _criticalBatteryPressureMilli * 0.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            TryUnregisterHotSwap();
            _hardwareThermalService = null;
            _criticalQualityApplied = false;
            _criticalBatteryPressureMilli = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            RebindHardwareServiceCold();
            TryRegisterHotSwap();
            SampleAndApply();
        }

        /// <summary>
        /// Samples critical battery pressure from cached hardware telemetry only.
        /// </summary>
        public static void SampleAndApply()
        {
            IHardwareThermalService hardware = _hardwareThermalService;
            if (hardware == null)
            {
                _criticalQualityApplied = false;
                _criticalBatteryPressureMilli = 0;
                return;
            }

            byte batteryPercent = hardware.BatteryPercent;
            int pressureMilli = ResolveCriticalBatteryPressureMilli(hardware, batteryPercent);
            bool criticalBattery = pressureMilli > 0;
            if (criticalBattery == _criticalQualityApplied && pressureMilli == _criticalBatteryPressureMilli)
                return;

            _criticalQualityApplied = criticalBattery;
            _criticalBatteryPressureMilli = pressureMilli;
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

        internal static float ResolveCriticalBatteryPressure01(IHardwareThermalService hardware)
        {
            return ResolveCriticalBatteryPressureMilli(hardware) * 0.001f;
        }

        internal static int ResolveCriticalBatteryPressureMilli(IHardwareThermalService hardware)
        {
            if (hardware == null)
                return 0;

            return ResolveCriticalBatteryPressureMilli(hardware, hardware.BatteryPercent);
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

        private static int ResolveCriticalBatteryPressureMilli(IHardwareThermalService hardware, byte fallbackBatteryPercent)
        {
            byte batteryPercent = fallbackBatteryPercent;
            byte batteryStatus = 0;
            HardwareThermalSnapshot snapshot = default;
            bool hasSnapshot = hardware != null && hardware.TryGetSnapshot(out snapshot);
            if (hasSnapshot)
            {
                batteryPercent = snapshot.BatteryPercent;
                batteryStatus = snapshot.BatteryStatus;
            }

            if (batteryPercent == 0 ||
                batteryPercent >= CriticalBatteryPercent ||
                (hasSnapshot && !IsDischarging(batteryStatus)))
            {
                return 0;
            }

            float depletion01 = math.saturate(
                (CriticalBatteryPercent - batteryPercent) *
                math.rcp(math.max(1f, CriticalBatteryPercent)));
            float pressure = math.lerp(CriticalBatteryBasePressureMilli, BatteryPressureScale, depletion01);
            return math.clamp((int)math.round(pressure), 1, BatteryPressureScale);
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
