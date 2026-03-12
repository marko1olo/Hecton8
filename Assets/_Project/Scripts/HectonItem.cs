// ============================================================================
// HECTON-8 — HectonItem.cs
// Подбираемый предмет в мире. Реализует IInteractable.
// Использует Data-Driven подход: вся информация — в ItemData.
//
// ИЗМЕНЕНИЕ v2:
//   Добавлен public метод SetItemData(ItemData, int) для программной
//   инициализации при спавне из BaseModule.Deconstruct().
//   Позволяет переиспользовать один worldItemPrefab для любых ресурсов.
// ============================================================================

using Hecton8.Core;
using Hecton8.Interaction;

namespace Hecton8.Items
{
    using UnityEngine;

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

        // ─────────────────────── Public API ──────────────────────

        /// <summary>
        /// Программная инициализация данных предмета.
        /// Вызывается при спавне из BaseModule.Deconstruct()
        /// для установки конкретного ресурса на generic worldItemPrefab.
        ///
        /// Безопасно вызывать повторно (перезаписывает данные).
        /// </summary>
        /// <param name="data">Данные предмета (ItemData ScriptableObject).</param>
        /// <param name="qty">Количество единиц.</param>
        public void SetItemData(ItemData data, int qty)
        {
            itemData = data;
            quantity = qty > 0 ? qty : 1;
        }

        /// <summary>Текущие данные предмета (read-only).</summary>
        public ItemData Data => itemData;

        /// <summary>Текущее количество (read-only).</summary>
        public int Quantity => quantity;

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

            InteractionEvents.RaiseItemCollected(itemData, quantity, interactor);

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
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

            if (itemData != null && !Application.isPlaying)
                gameObject.name = $"Item_{itemData.itemName}";
        }
#endif
    }
}