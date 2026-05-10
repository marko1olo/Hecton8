// ============================================================================
// HECTON-8 — ToolMetadata.cs  v1.0 ENTERPRISE
// Rasshirennye metadannye instrumentov: durability, upgrades, stats.
// ScriptableObject — naznachaetsya na ItemData instrumenta.
//
// v1.0 ENTERPRISE FEATURES:
//   [ADD] Durability system — iznos instrumenta pri ispolzovanii
//   [ADD] Upgrade slots — do 3 slotov dlya moduley uluchsheniya
//   [ADD] Tool stats — effektivnost, skorost, energopotreblenie
//   [ADD] Repair cost — stoimost remonta v resursah
//   [ADD] Tool tier — uroven instrumenta (Basic/Advanced/Master)
//   [ADD] Localization keys — dlya nazvaniy i opisaniy
//
// ZERO GC:
//   • Vse dannye — value types ili cached references
//   • Upgrade slots — fixed array (max 3)
//   • Stats — struct-based calculations
//
// ARHITEKTURA:
//   • Odin ToolMetadata na odin ItemData
//   • Chitaetsya PlayerTool pri OnEquip()
//   • Obnovlyaetsya cherez ToolDurabilitySystem
//   • Otobrazhaetsya v HUD i PDA
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
        [Tooltip("Unikalnyy ID instrumenta (dlya sohraneniy).")]
        public string toolID = "tool_laser_cutter";

        [Tooltip("Uroven instrumenta (Basic/Advanced/Master).")]
        public ToolTier tier = ToolTier.Basic;

        [Tooltip("Kategoriya instrumenta (dlya filtratsii v PDA).")]
        public ToolCategory category = ToolCategory.Utility;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DURABILITY
        // ══════════════════════════════════════════════════════════

        [Header("── Durability ──────────────────────────────")]
        [Tooltip("Maksimalnaya prochnost instrumenta.")]
        [Range(100f, 10000f)]
        public float maxDurability = 1000f;

        [Tooltip("Iznos za sekundu ispolzovaniya (Primary action).")]
        [Range(0.1f, 50f)]
        public float durabilityDrainRate = 1f;

        [Tooltip("Iznos za sekundu ispolzovaniya (Secondary action).")]
        [Range(0.1f, 50f)]
        public float durabilityDrainRateSecondary = 0.5f;

        [Tooltip("Kriticheskiy uroven prochnosti (%). Nizhe — warning.")]
        [Range(0f, 50f)]
        public float criticalDurabilityThreshold = 20f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — STATS
        // ══════════════════════════════════════════════════════════

        [Header("── Stats ───────────────────────────────────")]
        [Tooltip("Effektivnost instrumenta (1.0 = 100%).")]
        [Range(0.5f, 2f)]
        public float efficiency = 1f;

        [Tooltip("Skorost raboty instrumenta (1.0 = 100%).")]
        [Range(0.5f, 2f)]
        public float speed = 1f;

        [Tooltip("Energopotreblenie za sekundu ispolzovaniya.")]
        [Range(0f, 10f)]
        public float energyConsumptionRate = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — UPGRADES
        // ══════════════════════════════════════════════════════════

        [Header("── Upgrades ────────────────────────────────")]
        [Tooltip("Maksimalnoe kolichestvo slotov dlya uluchsheniy.")]
        [Range(0, 3)]
        public int maxUpgradeSlots = 2;

        [Tooltip("Tekuschie ustanovlennye uluchsheniya (max 3).")]
        public ToolUpgradeData[] installedUpgrades = new ToolUpgradeData[3];

        [Tooltip("Authored modular loadout consumed by the NativeArray-backed equipment runtime.")]
        public ToolModuleData[] defaultModules = new ToolModuleData[3];

        [Tooltip("Optional fallback heat-generation rate copied into the modular runtime when the concrete tool does not override it.")]
        [Range(0f, 4f)]
        public float authoredHeatGenerationRate = 0f;

        [Tooltip("Optional fallback cooldown rate copied into the modular runtime when the concrete tool does not override it.")]
        [Range(0f, 4f)]
        public float authoredCooldownRate = 0f;

        [Tooltip("Optional fallback recoil impulse copied into the modular runtime when the concrete tool does not override it.")]
        [Range(0f, 12f)]
        public float authoredRecoilImpulse = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REPAIR
        // ══════════════════════════════════════════════════════════

        [Header("── Repair ──────────────────────────────────")]
        [Tooltip("Stoimost polnogo remonta (v edinitsah resursa).")]
        [Range(1, 100)]
        public int repairCostFull = 10;

        [Tooltip("ID resursa dlya remonta (naprimer, 'titanium').")]
        public string repairResourceID = "titanium";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LOCALIZATION
        // ══════════════════════════════════════════════════════════

        [Header("── Localization ────────────────────────────")]
        [Tooltip("Klyuch lokalizatsii dlya nazvaniya instrumenta.")]
        public string nameLocKey = "TOOL_LASER_CUTTER_NAME";

        [Tooltip("Klyuch lokalizatsii dlya opisaniya instrumenta.")]
        public string descriptionLocKey = "TOOL_LASER_CUTTER_DESC";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CALCULATED STATS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vozvraschaet itogovuyu effektivnost s uchetom uluchsheniy.
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
        /// Vozvraschaet itogovuyu skorost s uchetom uluchsheniy.
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
        /// Vozvraschaet itogovoe energopotreblenie s uchetom uluchsheniy.
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

            return Mathf.Max(0.1f, total); // minimum 0.1
        }

        /// <summary>
        /// Proveryaet, est li svobodnyy slot dlya uluchsheniya.
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
        /// Ustanavlivaet uluchshenie v pervyy svobodnyy slot.
        /// Vozvraschaet true esli uspeshno.
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

            return false; // net svobodnyh slotov
        }

        /// <summary>
        /// Udalyaet uluchshenie iz slota.
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

        /// <summary>
        /// Copies the authored modular loadout into the caller-provided scratch buffer.
        /// Runtime systems must not retain or mutate the ScriptableObject array directly.
        /// </summary>
        public int CopyDefaultModules(ToolModuleData[] destination)
        {
            if (destination == null || destination.Length == 0 || defaultModules == null || defaultModules.Length == 0)
                return 0;

            int copyCount = Math.Min(Math.Min(destination.Length, defaultModules.Length), Math.Max(0, maxUpgradeSlots));
            for (int i = 0; i < copyCount; i++)
                destination[i] = defaultModules[i];

            for (int i = copyCount; i < destination.Length; i++)
                destination[i] = null;

            return copyCount;
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
