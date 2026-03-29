// ============================================================================
// HECTON-8 — PickupItem.cs
// Example IInteractable implementation showing all systems working together.
// ============================================================================

using Hecton8.Items;

namespace Hecton8.Interaction
{
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        // Cached references — resolved once in Awake, never again.
        private InteractionHighlighter _highlighter;
        private string _cachedInteractText;

        public ItemData ItemData => itemData;
        public int Quantity => quantity;

        public void Configure(ItemData data, int itemQuantity)
        {
            itemData = data;
            quantity = Mathf.Max(1, itemQuantity);

            _cachedInteractText = itemData != null
                ? itemData.GetInteractText()
                : "Pick up Unknown";
        }

        private void Awake()
        {
            _highlighter = GetComponent<InteractionHighlighter>();

            // Cache the string ONCE. GetInteractText() returns this forever.
            _cachedInteractText = itemData != null
                ? itemData.GetInteractText()
                : "Pick up Unknown";
        }

        // ================================================================
        // IInteractable Implementation
        // ================================================================

        public void OnHoverStart()
        {
            _highlighter.SetHighlight(true);
        }

        public void OnHoverEnd()
        {
            _highlighter.SetHighlight(false);
        }

        public void Interact(Transform interactor)
        {
            // Fire the global event — inventory system, audio, VFX all react.
            InteractionEvents.RaiseItemCollected(itemData, quantity, interactor);

            // Return to pool instead of Destroy in production.
            // ObjectPool.Return(gameObject);
            gameObject.SetActive(false);
        }

        public string GetInteractText()
        {
            // Zero allocation — returns pre-cached string from Awake().
            return _cachedInteractText;
        }
    }
}
