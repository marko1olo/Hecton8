// ============================================================================
// HECTON-8 — ToolUpgradeData.cs  v1.0 ENTERPRISE
// Dannye modulya uluchsheniya instrumenta.
// ScriptableObject — ustanavlivaetsya v ToolMetadata.installedUpgrades[].
//
// v1.0 ENTERPRISE FEATURES:
//   [ADD] Stat modifiers — efficiency, speed, energy consumption
//   [ADD] Special effects — durability multiplier, repair cost reduction
//   [ADD] Tier requirements — kakoy uroven instrumenta trebuetsya
//   [ADD] Crafting cost — stoimost sozdaniya uluchsheniya
//   [ADD] Localization keys
//
// ARHITEKTURA:
//   • Odin ToolUpgradeData = odin modul uluchsheniya
//   • Mozhet byt ustanovlen v lyuboy instrument (esli tier podhodit)
//   • Bonusy summiruyutsya s bazovymi statami instrumenta
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
        [Tooltip("Unikalnyy ID uluchsheniya.")]
        public string upgradeID = "upgrade_efficiency_mk1";

        [Tooltip("Nazvanie uluchsheniya (klyuch lokalizatsii).")]
        public string nameLocKey = "UPGRADE_EFFICIENCY_MK1_NAME";

        [Tooltip("Opisanie uluchsheniya (klyuch lokalizatsii).")]
        public string descriptionLocKey = "UPGRADE_EFFICIENCY_MK1_DESC";

        [Tooltip("Ikonka uluchsheniya (dlya UI).")]
        public Sprite icon;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REQUIREMENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Requirements ────────────────────────────")]
        [Tooltip("Minimalnyy tier instrumenta dlya ustanovki.")]
        public ToolTier requiredTier = ToolTier.Basic;

        [Tooltip("Kategorii instrumentov, na kotorye mozhno ustanovit.")]
        public ToolCategory[] compatibleCategories = new ToolCategory[]
        {
            ToolCategory.Utility,
            ToolCategory.Construction
        };

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — STAT MODIFIERS
        // ══════════════════════════════════════════════════════════

        [Header("── Stat Modifiers ──────────────────────────")]
        [Tooltip("Bonus k effektivnosti (+0.2 = +20%).")]
        [Range(-0.5f, 1f)]
        public float efficiencyBonus = 0.2f;

        [Tooltip("Bonus k skorosti (+0.1 = +10%).")]
        [Range(-0.5f, 1f)]
        public float speedBonus = 0f;

        [Tooltip("Modifikator energopotrebleniya (-0.2 = -20%).")]
        [Range(-0.5f, 0.5f)]
        public float energyConsumptionModifier = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SPECIAL EFFECTS
        // ══════════════════════════════════════════════════════════

        [Header("── Special Effects ─────────────────────────")]
        [Tooltip("Mnozhitel iznosa (0.8 = -20% iznosa).")]
        [Range(0.5f, 1.5f)]
        public float durabilityDrainMultiplier = 1f;

        [Tooltip("Snizhenie stoimosti remonta (%).")]
        [Range(0f, 50f)]
        public float repairCostReduction = 0f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CRAFTING
        // ══════════════════════════════════════════════════════════

        [Header("── Crafting ────────────────────────────────")]
        [Tooltip("Stoimost sozdaniya uluchsheniya (v edinitsah resursa).")]
        [Range(1, 50)]
        public int craftingCost = 5;

        [Tooltip("ID resursa dlya sozdaniya (naprimer, 'copper').")]
        public string craftingResourceID = "copper";

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proveryaet sovmestimost s instrumentom.
        /// </summary>
        public bool IsCompatibleWith(ToolMetadata tool)
        {
            if (tool == null) return false;

            // Proverka tier
            if (tool.tier < requiredTier)
                return false;

            // Proverka kategorii
            if (compatibleCategories == null || compatibleCategories.Length == 0)
                return true; // universalnoe uluchshenie

            foreach (ToolCategory cat in compatibleCategories)
            {
                if (tool.category == cat)
                    return true;
            }

            return false;
        }
    }
}
