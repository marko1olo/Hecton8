// ============================================================================
// HECTON-8 — BuildableData.cs
// Данные строительного модуля подводной базы.
//
// РЕФАКТОРИНГ v2 — ЭНЕРГОСИСТЕМА:
//   • Добавлены поля powerRating и powerPriority.
//   • PowerNode читает эти данные при спавне модуля.
//   • Data-Driven: потребление/генерация настраивается в ассете.
//
// ScriptableObject — один ассет на тип модуля.
// Создаётся через: Hecton → Buildable Module.
// ===========================================================================

using System;
using System.Collections.Generic;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Building
{
    // ══════════════════════════════════════════════════════════════════
    //  InventoryCost — стоимость одного ресурса
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Одна позиция в списке стоимости постройки.
    /// </summary>
    [Serializable]
    public sealed class InventoryCost
    {
        [Tooltip("Ресурс (ScriptableObject ItemData)")]
        public ItemData item;

        [Tooltip("Количество единиц этого ресурса")]
        [Min(1)]
        public int amount = 1;
    }

    // ══════════════════════════════════════════════════════════════════
    //  BuildableData — данные строительного модуля
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Чистые данные одного строительного модуля.
    /// Никакой логики — только описание.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewModule",
        menuName = "Hecton/Buildable Module",
        order    = 10)]
    public sealed class BuildableData : ScriptableObject
    {
        // ─────────────────────── Identity ────────────────────────
        [Header("Identity")]
        [Tooltip("Название модуля для UI: 'Фундамент', 'Коридор'")]
        public string moduleName = "Module";

        [Tooltip("Иконка для меню строительства (опционально)")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("Описание модуля для подсказки")]
        public string description = "";

        // ─────────────────────── Prefabs ─────────────────────────
        [Header("Prefabs")]
        [Tooltip("Полупрозрачный префаб-призрак (должен иметь PlacementGhost)")]
        public GameObject ghostPrefab;

        [Tooltip("Финальный префаб, устанавливаемый в мир")]
        public GameObject finalPrefab;

        // ─────────────────────── Cost ────────────────────────────
        [Header("Build Cost")]
        [Tooltip("Список ресурсов для постройки")]
        public List<InventoryCost> buildCost = new List<InventoryCost>();

        // ─────────────────────── Power ───────────────────────────
        [Header("Power")]
        [Tooltip("Энергетический рейтинг модуля (Ватты).\n" +
                 "• Положительное = генерация (солнечная панель: +200)\n" +
                 "• Отрицательное = потребление (жилая комната: -30)\n" +
                 "• Ноль = пассивный (коридор, стена)\n\n" +
                 "Это БАЗОВОЕ потребление модуля.\n" +
                 "Дополнительные потребители (Fabricator)\n" +
                 "добавляют своё через IPowerComponent.")]
        public float powerRating;

        [Tooltip("Приоритет отключения при дефиците энергии.\n" +
                 "0 = критический (жизнеобеспечение)\n" +
                 "50 = обычный\n" +
                 "100 = роскошь (декор)")]
        [Range(0, 100)]
        public int powerPriority = 50;

        // ─────────────────────── Cache ───────────────────────────

        /// <summary>Кэшированная строка для UI.</summary>
        private string _cachedBuildText;

        // ═════════════════════════════════════════════════════════
        //  ScriptableObject Lifecycle
        // ═════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildCache();
        }
#endif

        // ═════════════════════════════════════════════════════════
        //  Public API
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает кэшированную строку "Построить {moduleName}".
        /// Zero allocation.
        /// </summary>
        public string GetBuildText()
        {
            if (string.IsNullOrEmpty(_cachedBuildText))
                RebuildCache();
            return _cachedBuildText;
        }

        /// <summary>
        /// Суммарное количество ресурсных единиц для постройки.
        /// </summary>
        public int TotalResourceCount
        {
            get
            {
                int total = 0;
                for (int i = 0, count = buildCost.Count; i < count; i++)
                    total += buildCost[i].amount;
                return total;
            }
        }

        /// <summary>
        /// true если модуль генерирует энергию (powerRating > 0).
        /// Удобно для UI-фильтрации.
        /// </summary>
        public bool IsGenerator => powerRating > 0f;

        /// <summary>
        /// true если модуль потребляет энергию (powerRating &lt; 0).
        /// </summary>
        public bool IsConsumer => powerRating < 0f;

        // ═════════════════════════════════════════════════════════
        //  Private
        // ═════════════════════════════════════════════════════════

        private void RebuildCache()
        {
            _cachedBuildText = $"Построить {moduleName}";
        }
    }
}