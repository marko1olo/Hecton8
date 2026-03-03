// ============================================================================
// HECTON-8 — IInteractable.cs
// Interface contract for every interactable object in the game world.
// ============================================================================

namespace Hecton8.Interaction
{
    using UnityEngine;

    public interface IInteractable
    {
        /// <summary>
        /// Called once when the player's raycast first hits this object.
        /// Use for highlight activation, UI prompts, audio cues.
        /// </summary>
        void OnHoverStart();

        /// <summary>
        /// Called once when the player's raycast leaves this object.
        /// Use for highlight deactivation, hiding UI prompts.
        /// </summary>
        void OnHoverEnd();

        /// <summary>
        /// Called when the player presses the interact key while hovering.
        /// </summary>
        /// <param name="interactor">The Transform of the entity performing 
        /// the interaction (player root). Used for positioning, inventory 
        /// routing, etc.</param>
        void Interact(Transform interactor);

        /// <summary>
        /// Returns the UI prompt string for this interactable.
        /// CRITICAL: Return a cached string — never allocate here.
        /// Example: "Pick up Titanium" or "Open Airlock Panel"
        /// </summary>
        string GetInteractText();
    }
}