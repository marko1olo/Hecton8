// ============================================================================
// HECTON-8 — InteractionEvents.cs
// Global static event bus. Zero-instance, zero-GC dispatch.
// All gameplay systems subscribe here instead of polling.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using Hecton8.Items;
    using UnityEngine;

    // ========================================================================
    // Static Event Bus — No MonoBehaviour, no instance, no GC.
    // ========================================================================
    public static class InteractionEvents
    {
        // ====================================================================
        // EVENT: OnItemCollected
        // Fired when any item in the world is picked up by any interactor.
        // Params: ItemData (what), int quantity (how many), Transform (who)
        // ====================================================================
        public static event Action<ItemData, int, Transform> OnItemCollected;

        /// <summary>
        /// Thread-safe raise pattern. Fires OnItemCollected to all subscribers.
        /// Call this from any IInteractable.Interact() implementation.
        /// </summary>
        /// <param name="item">The ScriptableObject data for the collected item.</param>
        /// <param name="quantity">Number of items in this pickup.</param>
        /// <param name="interactor">The Transform that performed the pickup.</param>
        public static void RaiseItemCollected(ItemData item, int quantity, Transform interactor)
        {
            // Cache delegate to avoid race condition in multithreaded edge cases.
            var handler = OnItemCollected;
            handler?.Invoke(item, quantity, interactor);
        }

        // ====================================================================
        // EVENT: OnInteractionStarted
        // Generic event for any interaction beginning (doors, terminals, etc.)
        // ====================================================================
        public static event Action<IInteractable, Transform> OnInteractionStarted;

        public static void RaiseInteractionStarted(IInteractable target, Transform interactor)
        {
            var handler = OnInteractionStarted;
            handler?.Invoke(target, interactor);
        }

        // ====================================================================
        // EVENT: OnHoverChanged
        // Fired when the player starts or stops looking at an interactable.
        // Null target means "looking at nothing."
        // UI system subscribes here to show/hide prompts.
        // ====================================================================
        public static event Action<IInteractable> OnHoverChanged;

        public static void RaiseHoverChanged(IInteractable target)
        {
            var handler = OnHoverChanged;
            handler?.Invoke(target);
        }
    }
}