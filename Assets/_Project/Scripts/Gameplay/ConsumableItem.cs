// ============================================================================
// HECTON-8 — ConsumableItem.cs
// Runtime handler for consumable items (food, water, medkits).
//
// ARCHITECTURE:
//   • Static utility class - no MonoBehaviour, no Update
//   • Called by PlayerInventory when item is used
//   • Zero GC: cached refs, cached descriptions, no string lowercase churn
//
// INTEGRATION:
//   • ItemData already has consumable fields (oxygenRestore, energyRestore, etc.)
//   • HectonSurvivalSystem provides RefillOxygen, RechargeEnergy, Repair
//   • PlayerInventory calls TryConsume when player uses a consumable
//
// USE DURATION:
//   • ItemData.UseDuration defines time to consume (0 = instant)
//   • Food items typically 1s, medkits 3s, oxygen pipes 0s (instant)
//   • Caller is responsible for implementing the consumption delay
// ============================================================================

namespace Hecton8.Gameplay
{
    using System.Collections.Generic;
    using System.Text;
    using Hecton8.Audio;
    using Hecton8.Bootstrap;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Static utility for consuming items and applying their effects.
    /// Called by inventory system when player uses a consumable.
    /// </summary>
    public static class ConsumableItem
    {
        // COLD ALLOC: Dictionary[32] — cached consumable tooltip strings — owner: ConsumableItem
        private static readonly Dictionary<ItemData, string> s_effectDescriptionCache = new Dictionary<ItemData, string>(32);
        // COLD ALLOC: StringBuilder[64] — one-shot consumable tooltip assembly — owner: ConsumableItem
        private static readonly StringBuilder s_effectDescriptionBuilder = new StringBuilder(64);
        private static HectonSurvivalSystem s_cachedSurvivalSystem;

        /// <summary>
        /// Attempts to consume an item and apply its effects.
        /// Called by PlayerInventory when player uses a consumable from inventory.
        /// </summary>
        /// <param name="item">The item data to consume.</param>
        /// <param name="survivalSystem">The player's survival system (can be null).</param>
        /// <returns>True if the item was consumed successfully.</returns>
        public static bool TryConsume(ItemData item, HectonSurvivalSystem survivalSystem)
        {
            if (item == null || !item.isConsumable)
                return false;

            if (survivalSystem != null)
                ApplyEffects(item, survivalSystem);

            if (item.useSound != null && Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance != null)
                Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance.PlayStatic2D(item.useSound, 1f);

            return true;
        }

        /// <summary>
        /// Attempts to consume an item, automatically resolving the player's survival system.
        /// Used by PlayerActionController when a delayed action completes.
        /// </summary>
        /// <param name="item">The item data to consume.</param>
        /// <returns>True if the item was consumed successfully.</returns>
        public static bool TryConsume(ItemData item)
        {
            return TryConsume(item, ResolveSurvivalSystem());
        }

        /// <summary>
        /// Attempts to consume an item from world (pickup).
        /// Automatically resolves the player's survival system.
        /// </summary>
        /// <param name="item">The item data to consume.</param>
        /// <returns>True if the item was consumed successfully.</returns>
        public static bool TryConsumeFromWorld(ItemData item)
        {
            if (item == null || !item.isConsumable)
                return false;

            return TryConsume(item, ResolveSurvivalSystem());
        }

        /// <summary>
        /// Gets the use duration for an item in seconds.
        /// Returns 0 for instant-use items.
        /// </summary>
        public static float GetUseDuration(ItemData item)
        {
            if (item == null || !item.isConsumable)
                return 0f;

            return item.UseDuration;
        }

        /// <summary>
        /// Checks if an item requires a consumption animation/delay.
        /// </summary>
        public static bool RequiresUseTime(ItemData item)
        {
            return GetUseDuration(item) > 0f;
        }

        /// <summary>
        /// Gets a description of what this consumable does.
        /// Used for tooltips and HUD.
        /// </summary>
        public static string GetEffectDescription(ItemData item)
        {
            if (item == null || !item.isConsumable)
                return string.Empty;

            if (s_effectDescriptionCache.TryGetValue(item, out string cachedDescription))
                return cachedDescription;

            StringBuilder builder = s_effectDescriptionBuilder;
            builder.Clear();

            AppendEffectSegment(builder, Mathf.RoundToInt(item.oxygenRestore), "O2");
            AppendEffectSegment(builder, Mathf.RoundToInt(item.energyRestore), "Energy");
            AppendEffectSegment(builder, Mathf.RoundToInt(item.integrityRestore), "Integrity");
            AppendEffectSegment(builder, Mathf.RoundToInt(item.hungerRestore), "Food");
            AppendEffectSegment(builder, Mathf.RoundToInt(item.thirstRestore), "Water");

            cachedDescription = builder.ToString();
            s_effectDescriptionCache[item] = cachedDescription;
            return cachedDescription;
        }

        /// <summary>
        /// Checks if an item has any consumable effects.
        /// </summary>
        public static bool HasAnyEffect(ItemData item)
        {
            if (item == null || !item.isConsumable)
                return false;

            return item.oxygenRestore > 0f ||
                   item.energyRestore > 0f ||
                   item.integrityRestore > 0f ||
                   item.hungerRestore > 0f ||
                   item.thirstRestore > 0f;
        }

        private static void ApplyEffects(ItemData item, HectonSurvivalSystem survival)
        {
            if (survival == null)
                return;

            if (item.oxygenRestore > 0f)
                survival.RefillOxygen(item.oxygenRestore);

            if (item.energyRestore > 0f)
                survival.RechargeEnergy(item.energyRestore);

            if (item.integrityRestore > 0f)
                survival.Repair(item.integrityRestore);

            if (item.hungerRestore > 0f)
                survival.AddHunger(item.hungerRestore);

            if (item.thirstRestore > 0f)
                survival.AddThirst(item.thirstRestore);

            if (HectonSurvivalSystem.ShouldApplyNutritionalToxicityOnConsume(item))
                survival.ApplyNutritionalToxicity();
        }

        private static HectonSurvivalSystem ResolveSurvivalSystem()
        {
            if (s_cachedSurvivalSystem != null)
                return s_cachedSurvivalSystem;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                return null;

            s_cachedSurvivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();
            return s_cachedSurvivalSystem;
        }

        private static void AppendEffectSegment(StringBuilder builder, int value, string label)
        {
            if (value <= 0)
                return;

            if (builder.Length > 0)
                builder.Append("  ");

            builder.Append('+').Append(value).Append(' ').Append(label);
        }
    }
}

