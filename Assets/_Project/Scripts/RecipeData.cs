// ============================================================================
// HECTON-8 — RecipeData.cs
// Рецепт крафта одного предмета.
//
// ScriptableObject — один ассет на рецепт.
// Создаётся через: Hecton → Recipe.
//
// Data-Driven: дизайнеры создают рецепты в редакторе,
// код Fabricator просто итерирует по ним.
//
// Zero GC: строковые кэши строятся один раз в OnEnable.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Building;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Crafting
{
    /// <summary>
    /// Данные одного рецепта крафта.
    /// Содержит входные ресурсы, выходной предмет и время изготовления.
    ///
    /// Использует InventoryCost из системы строительства —
    /// единый формат стоимости для всего проекта.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRecipe",
        menuName = "Hecton/Recipe",
        order    = 20)]
    public sealed class RecipeData : ScriptableObject
    {
        // ─────────────────────── Identity ────────────────────────
        [Header("Identity")]
        [Tooltip("Название рецепта для UI")]
        public string recipeName = "New Recipe";

        [Tooltip("Иконка для меню крафта (опционально, берётся из resultItem)")]
        public Sprite overrideIcon;

        [TextArea(2, 4)]
        [Tooltip("Описание для подсказки")]
        public string description = "";

        // ─────────────────────── Result ──────────────────────────
        [Header("Result")]
        [Tooltip("Предмет на выходе")]
        public ItemData resultItem;

        [Tooltip("Количество предметов на выходе")]
        [Min(1)]
        public int resultQuantity = 1;

        // ─────────────────────── Ingredients ─────────────────────
        [Header("Ingredients")]
        [Tooltip("Список ингредиентов (ItemData + количество)")]
        public List<InventoryCost> ingredients = new List<InventoryCost>();

        // ─────────────────────── Timing ──────────────────────────
        [Header("Timing")]
        [Tooltip("Время крафта в секундах")]
        [Min(0.1f)]
        public float craftTime = 3f;

        // ─────────────────────── Cache ───────────────────────────

        /// <summary>"Создать Кислородный баллон" — для UI промпта.</summary>
        private string _cachedCraftText;

        /// <summary>"Титан ×2, Стекло ×1" — краткое описание стоимости.</summary>
        private string _cachedCostSummary;

        // ═════════════════════════════════════════════════════════
        //  Lifecycle
        // ═════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (resultQuantity < 1) resultQuantity = 1;
            if (craftTime < 0.1f)   craftTime = 0.1f;

            RebuildCache();
        }
#endif

        // ═════════════════════════════════════════════════════════
        //  Public API — Zero GC
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// "Создать {recipeName}". Кэширована — zero alloc.
        /// </summary>
        public string GetCraftText()
        {
            if (string.IsNullOrEmpty(_cachedCraftText))
                RebuildCache();
            return _cachedCraftText;
        }

        /// <summary>
        /// "Титан ×2, Стекло ×1". Кэширована — zero alloc.
        /// Используется UI для отображения стоимости.
        /// </summary>
        public string GetCostSummary()
        {
            if (string.IsNullOrEmpty(_cachedCostSummary))
                RebuildCache();
            return _cachedCostSummary;
        }

        /// <summary>
        /// Иконка рецепта: overrideIcon, или resultItem.icon, или null.
        /// </summary>
        public Sprite Icon => overrideIcon != null
            ? overrideIcon
            : (resultItem != null ? resultItem.icon : null);

        // ═════════════════════════════════════════════════════════
        //  Private
        // ═════════════════════════════════════════════════════════

        private void RebuildCache()
        {
            _cachedCraftText = $"Создать {recipeName}";

            // ── Краткое описание стоимости ──
            var sb = new StringBuilder(64);
            for (int i = 0, count = ingredients.Count; i < count; i++)
            {
                InventoryCost cost = ingredients[i];
                if (cost == null || cost.item == null) continue;

                if (sb.Length > 0) sb.Append(", ");
                sb.Append(cost.item.itemName);
                sb.Append(" ×");
                sb.Append(cost.amount);
            }

            _cachedCostSummary = sb.Length > 0 ? sb.ToString() : "—";
        }
    }
}