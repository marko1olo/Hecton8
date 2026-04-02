// ============================================================================
// HECTON-8 — InventoryEvents.cs
// Zero-GC Event bus for Inventory actions.
// ============================================================================

using System;
using Hecton8.Items;

namespace Hecton8.Inventory
{
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

        public static void NotifyInventoryFull(ItemData item) => OnInventoryFull?.Invoke(item);
        public static void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();
    }
}
