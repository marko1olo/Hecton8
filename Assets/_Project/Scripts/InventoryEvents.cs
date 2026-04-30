// ============================================================================
// HECTON-8 — InventoryEvents.cs
// Zero-GC Event bus for Inventory actions.
// ============================================================================

using System;
using Hecton8.Items;

namespace Hecton8.Inventory
{
    /// <summary>
    /// Carry-load change payload consumed by movement penalties without inventory polling.
    /// </summary>
    public readonly struct EncumbranceChangedEvent
    {
        public EncumbranceChangedEvent(
            PlayerInventory inventory,
            float totalMassKg,
            float carryCapacityKg,
            float load01)
        {
            Inventory = inventory;
            TotalMassKg = totalMassKg;
            CarryCapacityKg = carryCapacityKg;
            Load01 = load01;
        }

        public readonly PlayerInventory Inventory;
        public readonly float TotalMassKg;
        public readonly float CarryCapacityKg;
        public readonly float Load01;
    }

    public static class InventoryEvents
    {
        /// <summary>
        /// Fired when an item pickup fails due to full inventory.
        /// </summary>
        public static event Action<ItemData> OnInventoryFull;

        /// <summary>
        /// Fired when the inventory contents change.
        /// </summary>
        public static event Action OnInventoryChanged;

        /// <summary>
        /// Fired when derived carry load changes and locomotion penalties must refresh.
        /// </summary>
        public static event Action<EncumbranceChangedEvent> OnEncumbranceChanged;

        public static void NotifyInventoryFull(ItemData item) => OnInventoryFull?.Invoke(item);
        public static void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();
        public static void NotifyEncumbranceChanged(EncumbranceChangedEvent payload) => OnEncumbranceChanged?.Invoke(payload);
    }
}
