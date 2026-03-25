// ============================================================================
// HECTON-8 — ToolUpgradeData.cs  v1.0 ENTERPRISE
// Данные модуля улучшения инструмента.
// ScriptableObject — устанавливается в ToolMetadata.installedUpgrades[].
//
// v1.0 ENTERPRISE FEATURES:
//   [ADD] Stat modifiers — efficiency, speed, energy consumption
//   [ADD] Special effects — durability multiplier, repair cost reduction
//   [ADD] Tier requirements — какой уровень инструмента требуется
//   [ADD] Crafting cost — стоимость создания улучшения
//   [ADD] Localization keys
//
// АРХИТЕКТУРА:
//   • Один ToolUpgradeData = один модуль улучшения
//   • Может быть установлен в любой инструмент (если tier подходит)
//   • Бонусы суммируются с базовыми статами инструмента
// ============================================================================

using UnityEngine;

namespace Hecton8.Tools
{
    [CreateAssetMenu(fileName = "ToolUpgrade_", menuName = "Hecton8/Tools/Tool Upgrade")]
    public sealed class ToolUpgradeData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — IDENTITY
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID улучшения.")]
        public string upgradeID = "upgrade_efficiency_mk1";

        [Tooltip("Название улучшения (ключ локализации).")]
        public string nameLocKey = "UPGRADE_EFFICIENCY_MK1_NAME";

        [Tooltip("Описание улучшения (ключ локализации).")]
        public string descriptionLocKey = "UPGRADE_EFFICIENCY_MK1_DESC";

        [Tooltip("Иконка улучшения (для UI).")]
        public Sprite icon;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REQUIREMENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Requirements ────────────────────────────")]
        [Tooltip("Минимальный tier инструмента для установки.")]
        public ToolTier requiredTier = ToolTier.Basic;

        [Tooltip("Категории инструментов, на которые можно установить.")]
        public ToolCategory[] compatibleCategories = new ToolCategory[]
        {
            ToolCategory.Utility,
            ToolCategory.Construction
        };

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — STAT MODIFIERS
        // ══════════════════════════════════════════════════════════

        [Header("── Stat Modifiers ──────────────────────────")]
        [Tooltip("Бонус к эффективности (+0.2 = +20%).")]
        [Range(-0.5f, 1f)]
        public float efficiencyBonus = 0.2f;

        [Tooltip("Бонус к скорости (+0.1 = +10%).")]
        [Range(-0.5f, 1f)]
        public float speedBonus = 0f;

        [Tooltip("Модификатор энергопотребления (-0.2 = -20%).")]
        [Range(-0.5f, 0.5f)]
        public float energyConsumptionModifier = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SPECIAL EFFECTS
        // ══════════════════════════════════════════════════════════

        [Header("── Special Effects ─────────────────────────")]
        [Tooltip("Множитель износа (0.8 = -20% износа).")]
        [Range(0.5f, 1.5f)]
        public float durabilityDrainMultiplier = 1f;

        [Tooltip("Снижение стоимости ремонта (%).")]
        [Range(0f, 50f)]
        public float repairCostReduction = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CRAFTING
        // ══════════════════════════════════════════════════════════

        [Header("── Crafting ────────────────────────────────")]
        [Tooltip("Стоимость создания улучшения (в единицах ресурса).")]
        [Range(1, 50)]
        public int craftingCost = 5;

        [Tooltip("ID ресурса для создания (например, 'copper').")]
        public string craftingResourceID = "copper";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет совместимость с инструментом.
        /// </summary>
        public bool IsCompatibleWith(ToolMetadata tool)
        {
            if (tool == null) return false;

            // Проверка tier
            if (tool.tier < requiredTier)
                return false;

            // Проверка категории
            if (compatibleCategories == null || compatibleCategories.Length == 0)
                return true; // универсальное улучшение

            foreach (ToolCategory cat in compatibleCategories)
            {
                if (tool.category == cat)
                    return true;
            }

            return false;
        }
    }
}
