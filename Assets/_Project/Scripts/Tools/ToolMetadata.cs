// ============================================================================
// HECTON-8 — ToolMetadata.cs  v1.0 ENTERPRISE
// Расширенные метаданные инструментов: durability, upgrades, stats.
// ScriptableObject — назначается на ItemData инструмента.
//
// v1.0 ENTERPRISE FEATURES:
//   [ADD] Durability system — износ инструмента при использовании
//   [ADD] Upgrade slots — до 3 слотов для модулей улучшения
//   [ADD] Tool stats — эффективность, скорость, энергопотребление
//   [ADD] Repair cost — стоимость ремонта в ресурсах
//   [ADD] Tool tier — уровень инструмента (Basic/Advanced/Master)
//   [ADD] Localization keys — для названий и описаний
//
// ZERO GC:
//   • Все данные — value types или cached references
//   • Upgrade slots — fixed array (max 3)
//   • Stats — struct-based calculations
//
// АРХИТЕКТУРА:
//   • Один ToolMetadata на один ItemData
//   • Читается PlayerTool при OnEquip()
//   • Обновляется через ToolDurabilitySystem
//   • Отображается в HUD и PDA
// ============================================================================

using UnityEngine;
using System;

namespace Hecton8.Tools
{
    [CreateAssetMenu(fileName = "ToolMetadata_", menuName = "Hecton8/Tools/Tool Metadata")]
    public sealed class ToolMetadata : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — IDENTITY
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ────────────────────────────────")]
        [Tooltip("Уникальный ID инструмента (для сохранений).")]
        public string toolID = "tool_laser_cutter";

        [Tooltip("Уровень инструмента (Basic/Advanced/Master).")]
        public ToolTier tier = ToolTier.Basic;

        [Tooltip("Категория инструмента (для фильтрации в PDA).")]
        public ToolCategory category = ToolCategory.Utility;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DURABILITY
        // ══════════════════════════════════════════════════════════

        [Header("── Durability ──────────────────────────────")]
        [Tooltip("Максимальная прочность инструмента.")]
        [Range(100f, 10000f)]
        public float maxDurability = 1000f;

        [Tooltip("Износ за секунду использования (Primary action).")]
        [Range(0.1f, 50f)]
        public float durabilityDrainRate = 1f;

        [Tooltip("Износ за секунду использования (Secondary action).")]
        [Range(0.1f, 50f)]
        public float durabilityDrainRateSecondary = 0.5f;

        [Tooltip("Критический уровень прочности (%). Ниже — warning.")]
        [Range(0f, 50f)]
        public float criticalDurabilityThreshold = 20f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — STATS
        // ══════════════════════════════════════════════════════════

        [Header("── Stats ───────────────────────────────────")]
        [Tooltip("Эффективность инструмента (1.0 = 100%).")]
        [Range(0.5f, 2f)]
        public float efficiency = 1f;

        [Tooltip("Скорость работы инструмента (1.0 = 100%).")]
        [Range(0.5f, 2f)]
        public float speed = 1f;

        [Tooltip("Энергопотребление за секунду использования.")]
        [Range(0f, 10f)]
        public float energyConsumptionRate = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — UPGRADES
        // ══════════════════════════════════════════════════════════

        [Header("── Upgrades ────────────────────────────────")]
        [Tooltip("Максимальное количество слотов для улучшений.")]
        [Range(0, 3)]
        public int maxUpgradeSlots = 2;

        [Tooltip("Текущие установленные улучшения (max 3).")]
        public ToolUpgradeData[] installedUpgrades = new ToolUpgradeData[3];

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REPAIR
        // ══════════════════════════════════════════════════════════

        [Header("── Repair ──────────────────────────────────")]
        [Tooltip("Стоимость полного ремонта (в единицах ресурса).")]
        [Range(1, 100)]
        public int repairCostFull = 10;

        [Tooltip("ID ресурса для ремонта (например, 'titanium').")]
        public string repairResourceID = "titanium";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LOCALIZATION
        // ══════════════════════════════════════════════════════════

        [Header("── Localization ────────────────────────────")]
        [Tooltip("Ключ локализации для названия инструмента.")]
        public string nameLocKey = "TOOL_LASER_CUTTER_NAME";

        [Tooltip("Ключ локализации для описания инструмента.")]
        public string descriptionLocKey = "TOOL_LASER_CUTTER_DESC";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CALCULATED STATS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает итоговую эффективность с учётом улучшений.
        /// Zero GC — struct-based calculation.
        /// </summary>
        public float GetTotalEfficiency()
        {
            float total = efficiency;

            for (int i = 0; i < maxUpgradeSlots && i < installedUpgrades.Length; i++)
            {
                ToolUpgradeData upgrade = installedUpgrades[i];
                if (upgrade != null)
                    total += upgrade.efficiencyBonus;
            }

            return total;
        }

        /// <summary>
        /// Возвращает итоговую скорость с учётом улучшений.
        /// </summary>
        public float GetTotalSpeed()
        {
            float total = speed;

            for (int i = 0; i < maxUpgradeSlots && i < installedUpgrades.Length; i++)
            {
                ToolUpgradeData upgrade = installedUpgrades[i];
                if (upgrade != null)
                    total += upgrade.speedBonus;
            }

            return total;
        }

        /// <summary>
        /// Возвращает итоговое энергопотребление с учётом улучшений.
        /// </summary>
        public float GetTotalEnergyConsumption()
        {
            float total = energyConsumptionRate;

            for (int i = 0; i < maxUpgradeSlots && i < installedUpgrades.Length; i++)
            {
                ToolUpgradeData upgrade = installedUpgrades[i];
                if (upgrade != null)
                    total += upgrade.energyConsumptionModifier;
            }

            return Mathf.Max(0.1f, total); // минимум 0.1
        }

        /// <summary>
        /// Проверяет, есть ли свободный слот для улучшения.
        /// </summary>
        public bool HasFreeUpgradeSlot()
        {
            int usedSlots = 0;
            for (int i = 0; i < maxUpgradeSlots && i < installedUpgrades.Length; i++)
            {
                if (installedUpgrades[i] != null)
                    usedSlots++;
            }
            return usedSlots < maxUpgradeSlots;
        }

        /// <summary>
        /// Устанавливает улучшение в первый свободный слот.
        /// Возвращает true если успешно.
        /// </summary>
        public bool InstallUpgrade(ToolUpgradeData upgrade)
        {
            if (upgrade == null) return false;

            for (int i = 0; i < maxUpgradeSlots && i < installedUpgrades.Length; i++)
            {
                if (installedUpgrades[i] == null)
                {
                    installedUpgrades[i] = upgrade;
                    return true;
                }
            }

            return false; // нет свободных слотов
        }

        /// <summary>
        /// Удаляет улучшение из слота.
        /// </summary>
        public bool RemoveUpgrade(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxUpgradeSlots || slotIndex >= installedUpgrades.Length)
                return false;

            if (installedUpgrades[slotIndex] == null)
                return false;

            installedUpgrades[slotIndex] = null;
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════════════════════

    public enum ToolTier
    {
        Basic = 0,
        Advanced = 1,
        Master = 2
    }

    public enum ToolCategory
    {
        Utility = 0,      // Laser Cutter, Scanner
        Construction = 1, // Builder
        Combat = 2,       // Weapons
        Survival = 3,     // Repair Tool, Flashlight
        Science = 4       // Analysis tools
    }
}
