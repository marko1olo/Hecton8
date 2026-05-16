namespace Hecton8.Interaction
{
    using Hecton8.Inventory;
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

    public static class InventoryPickupSignalConstants
    {
        public const byte ItemSourceManualPickup = 9;
        public const byte SignalFlagManualPickup = 1 << 1;
    }
}
