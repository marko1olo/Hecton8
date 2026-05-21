using System.Threading;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public interface IBatteryChargerLogisticsService : ISystem
    {
        bool TryRegisterChargerLink(
            uint inventorySlotIndex,
            uint powerGraphNodeIndex,
            float chargeRate,
            float efficiencyScalar,
            double3 chargerAup,
            out int linkIndex);

        int TryUnregisterChargerLinks(
            uint inventorySlotStartIndex,
            int slotCount,
            uint powerGraphNodeIndex);

        bool TryWriteInventorySlotState(uint inventorySlotIndex, uint itemHash, float charge01);
        bool TryReadCharge01(uint inventorySlotIndex, out float charge01);
    }

    /// <summary>
    /// Cold cached facade for the battery charger logistics runtime service.
    /// Keeps Gameplay/Core facade code from directly referencing the Power runtime assembly.
    /// </summary>
    public static class BatteryChargerLogisticsBridge
    {
        private static IBatteryChargerLogisticsService s_service;

        public static bool IsRegistered
        {
            get
            {
                return Volatile.Read(ref s_service) != null;
            }
        }

        internal static void BindService(IBatteryChargerLogisticsService service)
        {
            Volatile.Write(ref s_service, service);
        }

        public static bool TryRegisterChargerLink(
            uint inventorySlotIndex,
            uint powerGraphNodeIndex,
            float chargeRate,
            float efficiencyScalar,
            double3 chargerAup,
            out int linkIndex)
        {
            IBatteryChargerLogisticsService service = Volatile.Read(ref s_service);
            if (service == null)
            {
                linkIndex = -1;
                return false;
            }

            return service.TryRegisterChargerLink(inventorySlotIndex, powerGraphNodeIndex, chargeRate, efficiencyScalar, chargerAup, out linkIndex);
        }

        public static int TryUnregisterChargerLinks(uint inventorySlotStartIndex, int slotCount, uint powerGraphNodeIndex)
        {
            IBatteryChargerLogisticsService service = Volatile.Read(ref s_service);
            return service != null ? service.TryUnregisterChargerLinks(inventorySlotStartIndex, slotCount, powerGraphNodeIndex) : 0;
        }

        public static bool TryWriteInventorySlotState(uint inventorySlotIndex, uint itemHash, float charge01)
        {
            IBatteryChargerLogisticsService service = Volatile.Read(ref s_service);
            return service != null && service.TryWriteInventorySlotState(inventorySlotIndex, itemHash, charge01);
        }

        public static bool TryReadCharge01(uint inventorySlotIndex, out float charge01)
        {
            IBatteryChargerLogisticsService service = Volatile.Read(ref s_service);
            if (service != null)
                return service.TryReadCharge01(inventorySlotIndex, out charge01);

            charge01 = 0f;
            return false;
        }
    }
}
