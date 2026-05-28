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
    using System;
    using Hecton8.Audio;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Items;
    using UnityEngine;

    /// <summary>
    /// Static utility for consuming items and applying their effects.
    /// Called by inventory system when player uses a consumable.
    /// </summary>
    public static class ConsumableItem
    {
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
            return TryConsume(item, survivalSystem, null);
        }

        public static bool TryConsume(ItemData item, HectonSurvivalSystem survivalSystem, IAudioService audioService)
        {
            if (item == null || !item.isConsumable)
                return false;

            if (survivalSystem != null)
                ApplyEffects(item, survivalSystem);

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

        public static bool TryConsume(ItemData item, IAudioService audioService)
        {
            return TryConsume(item, ResolveSurvivalSystem(), audioService);
        }

        public static bool TryConsumeWithoutAudio(ItemData item)
        {
            if (item == null || !item.isConsumable)
                return false;

            HectonSurvivalSystem survivalSystem = ResolveSurvivalSystem();
            if (survivalSystem != null)
                ApplyEffects(item, survivalSystem);

            return true;
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
        /// Legacy bridge. UI/HUD code must use TryWriteEffectDescription.
        /// </summary>
        [Obsolete("Use TryWriteEffectDescription(ItemData, Span<char>, out int) to avoid managed strings.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static string GetEffectDescription(ItemData item)
        {
            return string.Empty;
        }

        /// <summary>
        /// Writes a consumable effect summary into caller-owned memory.
        /// </summary>
        public static bool TryWriteEffectDescription(ItemData item, Span<char> destination, out int length)
        {
            length = 0;
            if (item == null || !item.isConsumable || destination.Length == 0)
                return false;

            if (!TryAppendEffectSegment(destination, ref length, Mathf.RoundToInt(item.oxygenRestore), "O2"))
                return false;
            if (!TryAppendEffectSegment(destination, ref length, Mathf.RoundToInt(item.energyRestore), "Energy"))
                return false;
            if (!TryAppendEffectSegment(destination, ref length, Mathf.RoundToInt(item.integrityRestore), "Integrity"))
                return false;
            if (!TryAppendEffectSegment(destination, ref length, Mathf.RoundToInt(item.hungerRestore), "Food"))
                return false;
            if (!TryAppendEffectSegment(destination, ref length, Mathf.RoundToInt(item.thirstRestore), "Water"))
                return false;

            return true;
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

            playerTransform.TryGetComponent(out s_cachedSurvivalSystem);
            return s_cachedSurvivalSystem;
        }

        private static bool TryAppendEffectSegment(Span<char> destination, ref int length, int value, ReadOnlySpan<char> label)
        {
            if (value <= 0)
                return true;

            if (length > 0 && !TryAppend(destination, ref length, "  "))
                return false;

            if (length >= destination.Length)
                return false;

            destination[length++] = '+';
            if (!value.TryFormat(destination.Slice(length), out int written))
                return false;

            length += written;
            if (length >= destination.Length)
                return false;

            destination[length++] = ' ';
            return TryAppend(destination, ref length, label);
        }

        private static bool TryAppend(Span<char> destination, ref int length, ReadOnlySpan<char> text)
        {
            if (length < 0 || length + text.Length > destination.Length)
                return false;

            text.CopyTo(destination.Slice(length));
            length += text.Length;
            return true;
        }
    }
}

