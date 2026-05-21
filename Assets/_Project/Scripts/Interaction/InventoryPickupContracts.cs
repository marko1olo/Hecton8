namespace Hecton8.Interaction
{
    using Hecton8.Inventory;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Optional zero-GC pickup seam used by player interaction before falling back to generic interactables.
    /// </summary>
    public interface IInventoryPickupSource
    {
        /// <summary>
        /// Stable item hash carried by the hovered pickup source.
        /// </summary>
        int ItemHashId { get; }

        /// <summary>
        /// Attempts to transfer the pickup into the SOA inventory and consume the world proxy when successful.
        /// Returns true when the pickup interaction was handled.
        /// </summary>
        bool TryHandleInventoryPickup(PlayerInventory inventory, Transform interactor);
    }

    /// <summary>
    /// Optional read-only pickup preview seam for hover and zero-allocation tool collection.
    /// </summary>
    public interface IInventoryPickupPreviewSource
    {
        bool TryPeekInventoryPickup(out ItemData itemData, out int quantity);
    }

    public static class InventoryPickupSignalConstants
    {
        public const byte ItemSourceManualPickup = 9;
        public const byte SignalFlagManualPickup = 1 << 1;
    }
}
