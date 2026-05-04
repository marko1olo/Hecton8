using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Public repair-station facade over the powered maintenance bay implementation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MaintenanceStationModule))]
    [AddComponentMenu("Hecton8/Construction/Repair Station")]
    public sealed class RepairStation : MonoBehaviour
    {
        private MaintenanceStationModule _station;

        public bool HasSlottedTool => _station != null && _station.HasSlottedTool;

        private void Awake()
        {
            _station = GetComponent<MaintenanceStationModule>();
        }

        public bool TryAcceptTool(PlayerInventory inventory, PlayerTool tool)
        {
            return _station != null &&
                   tool != null &&
                   tool.ToolData != null &&
                   _station.TryInsertFromInventory(inventory, tool.ToolData);
        }

        public bool TryInsertFromInventory(PlayerInventory inventory, ItemData item)
        {
            return _station != null && _station.TryInsertFromInventory(inventory, item);
        }

        public bool TryReturnToolToInventory(PlayerInventory inventory)
        {
            return _station != null && _station.TryReturnToolToInventory(inventory);
        }
    }
}
