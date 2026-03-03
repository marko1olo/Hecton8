namespace Hecton.Items
{
    using Hecton.Interaction;
    using UnityEngine;

    /// <summary>
    /// Подбираемый предмет в мире. Реализует IInteractable.
    /// Использует Data-Driven подход: вся информация — в <see cref="ItemData"/>.
    /// При взаимодействии:
    ///   1. Публикует событие через <see cref="InteractionEvents"/>.
    ///   2. Уничтожает свой GameObject.
    /// Будущая система инвентаря подписывается на InteractionEvents.OnItemCollected
    /// и обрабатывает добавление в рюкзак.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [DisallowMultipleComponent]
    public class HectonItem : MonoBehaviour, IInteractable
    {
        // ─────────────────────── Data ────────────────────────────
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int      quantity = 1;

        // ─────────────────────── Cached ──────────────────────────
        private InteractionHighlighter _highlighter;

        // ═════════════════════════════════════════════════════════
        private void Awake()
        {
            _highlighter = GetComponent<InteractionHighlighter>();

            if (itemData == null)
                Debug.LogError($"[HectonItem] ItemData не назначен на {gameObject.name}!", this);
        }

        // ─────────────────────── IInteractable ───────────────────
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
            if (itemData == null) return;

            // ► Оповестить все системы (инвентарь, квесты, звук и т.д.)
            InteractionEvents.RaiseItemCollected(itemData, quantity, interactor);

            // Визуальный фидбек перед уничтожением (расширяемо)
            // PlayPickupVFX();
            // PlayPickupSFX();

            Destroy(gameObject);
        }

        public string GetInteractText()
        {
            if (itemData == null) return "???";

            string qtyStr = quantity > 1 ? $" ×{quantity}" : "";
            return $"{itemData.interactVerb} {itemData.itemName}{qtyStr}";
        }

        // ─────────────────────── Editor ──────────────────────────
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (quantity < 1) quantity = 1;

            // Автоименование объекта по данным предмета
            if (itemData != null && !Application.isPlaying)
                gameObject.name = $"Item_{itemData.itemName}";
        }
        #endif
    }
}