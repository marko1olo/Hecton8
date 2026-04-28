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
    public enum BuildableFamily
    {
        Structure = 0,
        Habitat = 1,
        Utility = 2,
        Fabrication = 3,
        Logistics = 4,
        Defense = 5
    }

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
        [Tooltip("Stable module ID used by saves, scanner archives, and future content packs. Leave empty to fall back to the asset name.")]
        [SerializeField] private string stableId = string.Empty;

        [Tooltip("Иконка для меню строительства (опционально)")]
        public Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("Описание модуля для подсказки")]
        public string description = "";

        [Tooltip("Семейство модуля для browser/filter/directive logic.")]
        public BuildableFamily family = BuildableFamily.Structure;

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
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

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

        public bool IsPassive => Mathf.Approximately(powerRating, 0f);

        /// <summary>
        /// Stable content identifier used by persistence-facing systems.
        /// </summary>
        public string PersistentId => string.IsNullOrWhiteSpace(stableId) ? name : stableId;

        public string FamilyLabel => ResolveFamilyLabel(family);

        /// <summary>
        /// Returns true when the supplied ID matches the authored stable ID or the legacy asset name.
        /// </summary>
        public bool MatchesPersistentId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            string persistentId = PersistentId;
            if (string.Equals(persistentId, id, StringComparison.Ordinal))
                return true;

            return !string.Equals(name, persistentId, StringComparison.Ordinal) &&
                   string.Equals(name, id, StringComparison.Ordinal);
        }

        public string FamilyShortCode
        {
            get
            {
                switch (family)
                {
                    case BuildableFamily.Structure: return "STR";
                    case BuildableFamily.Habitat: return "HAB";
                    case BuildableFamily.Utility: return "UTL";
                    case BuildableFamily.Fabrication: return "FAB";
                    case BuildableFamily.Logistics: return "LOG";
                    case BuildableFamily.Defense: return "DEF";
                    default: return "UNK";
                }
            }
        }

        private static string ResolveFamilyLabel(BuildableFamily value)
        {
            switch (value)
            {
                case BuildableFamily.Structure: return "STRUCTURE";
                case BuildableFamily.Habitat: return "HABITAT";
                case BuildableFamily.Utility: return "UTILITY";
                case BuildableFamily.Fabrication: return "FABRICATION";
                case BuildableFamily.Logistics: return "LOGISTICS";
                case BuildableFamily.Defense: return "DEFENSE";
                default: return "UNKNOWN";
            }
        }

        // ═════════════════════════════════════════════════════════
        //  Private
        // ═════════════════════════════════════════════════════════

        private void RebuildCache()
        {
            _cachedBuildText = $"Построить {moduleName}";
        }

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _cachedUpperStrings = new string[16];

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Простой hash для кэширования (не криптографический)
            int hash = input.GetHashCode() & 0xF; // Маска для индекса 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Создаем новую строку и кэшируем
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }
    }
}
