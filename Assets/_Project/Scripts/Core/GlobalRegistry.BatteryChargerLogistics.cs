using System.Threading;
using UnityEngine.Scripting;

namespace Hecton8.Core
{
    public static partial class GlobalRegistry
    {
        private static IBatteryChargerLogisticsService _batteryChargerLogisticsRuntime;

        /// <summary>
        /// Cold service-locator route for the Power-owned battery charger logistics runtime.
        /// Facades should cache or use <see cref="BatteryChargerLogisticsBridge"/> rather than poll this per frame.
        /// </summary>
        public static IBatteryChargerLogisticsService BatteryChargerLogistics =>
            Volatile.Read(ref _batteryChargerLogisticsRuntime);

        [Preserve]
        public static void RegisterBatteryChargerLogisticsRuntime(IBatteryChargerLogisticsService instance)
        {
            if (instance == null)
                return;

            IBatteryChargerLogisticsService previous =
                Interlocked.CompareExchange(ref _batteryChargerLogisticsRuntime, instance, null);
            if (previous != null && !object.ReferenceEquals(previous, instance))
                ThrowSlotHijack(previous, instance);

            BatteryChargerLogisticsBridge.BindService(instance);
        }

        [Preserve]
        public static void UnregisterBatteryChargerLogisticsRuntime(IBatteryChargerLogisticsService instance)
        {
            if (instance == null)
                return;

            IBatteryChargerLogisticsService previous =
                Interlocked.CompareExchange(ref _batteryChargerLogisticsRuntime, null, instance);
            if (object.ReferenceEquals(previous, instance))
                BatteryChargerLogisticsBridge.BindService(null);
        }

        [Preserve]
        public static void ResetBatteryChargerLogisticsRuntimeForDomainReload()
        {
            Interlocked.Exchange(ref _batteryChargerLogisticsRuntime, null);
            BatteryChargerLogisticsBridge.BindService(null);
        }
    }
}
