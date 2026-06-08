// ============================================================================
// HECTON-8 — IInteractable.cs
// Interface contract for every interactable object in the game world.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton.Localization;
    using Hecton8.Core;
    using System;
    using UnityEngine;

    public interface IInteractable
    {
        /// <summary>
        /// Called once when the player's spatial target probe first resolves this object.
        /// Use for highlight activation, UI prompts, audio cues.
        /// </summary>
        void OnHoverStart();

        /// <summary>
        /// Called once when the player's spatial target probe leaves this object.
        /// Use for highlight deactivation, hiding UI prompts.
        /// </summary>
        void OnHoverEnd();

        /// <summary>
        /// Called when the player presses the interact key while hovering.
        /// </summary>
        /// <param name="interactor">The Transform of the entity performing
        /// the interaction (player root). Used for positioning, inventory
        /// routing, etc.</param>
        void Interact(Transform interactor);

        /// <summary>
        /// Returns the UI prompt string for this interactable.
        /// CRITICAL: Return a cached string — never allocate here.
        /// Example: "Pick up Titanium" or "Open Airlock Panel"
        /// </summary>
        string GetInteractText();
    }

    public interface IInteractableTextProvider
    {
        /// <summary>
        /// Copies the current interaction prompt into caller-owned storage.
        /// Returns false when the destination is too small or no prompt is available.
        /// </summary>
        bool TryCopyInteractText(Span<char> destination, out int length);
    }

    /// <summary>
    /// Marker for interactables whose <see cref="IInteractable.Interact"/> method owns
    /// accepted-start event publication. PlayerInteraction skips its default attempt
    /// event and generic confirm feedback for these targets so owner-local reject paths
    /// do not emit false positives.
    /// </summary>
    public interface IInteractionStartedEventOwner
    {
    }

    public static class InteractableTextCopy
    {
        public static bool TryCopy(string source, Span<char> destination, out int length)
        {
            return TryCopy(string.IsNullOrEmpty(source) ? ReadOnlySpan<char>.Empty : source.AsSpan(), destination, out length);
        }

        public static bool TryCopy(ReadOnlySpan<char> source, Span<char> destination, out int length)
        {
            length = source.Length;
            if (length <= 0 || destination.Length < length)
            {
                length = 0;
                return false;
            }

            source.CopyTo(destination);
            return true;
        }

        public static int CopyTruncated(ReadOnlySpan<char> source, Span<char> destination)
        {
            int length = Math.Min(source.Length, destination.Length);
            if (length <= 0)
                return 0;

            source.Slice(0, length).CopyTo(destination);
            return length;
        }

        public static ReadOnlySpan<char> ResolveLocalizedSpan(ILocalizationTextReadModel manager, string key, string fallback)
        {
            ReadOnlySpan<char> fallbackSpan = string.IsNullOrEmpty(fallback)
                ? ReadOnlySpan<char>.Empty
                : fallback.AsSpan();
            if (manager == null || string.IsNullOrEmpty(key))
                return fallbackSpan;

            return manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallbackSpan);
        }

        public static bool TryCopyLocalized(ILocalizationTextReadModel manager, string key, string fallback, Span<char> destination, out int length)
        {
            return TryCopy(ResolveLocalizedSpan(manager, key, fallback), destination, out length);
        }

        public static int CopyLocalizedTruncated(ILocalizationTextReadModel manager, string key, string fallback, Span<char> destination)
        {
            return CopyTruncated(ResolveLocalizedSpan(manager, key, fallback), destination);
        }

        public static bool TryCopyConfiguredOrLocalized(string configuredValue, string legacyDefault, string key, ILocalizationTextReadModel manager, Span<char> destination, out int length)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue) &&
                !string.Equals(configuredValue, legacyDefault, StringComparison.Ordinal))
            {
                return TryCopy(configuredValue.AsSpan(), destination, out length);
            }

            return TryCopyLocalized(manager, key, legacyDefault, destination, out length);
        }

        public static int CopyConfiguredOrLocalizedTruncated(string configuredValue, string legacyDefault, string key, ILocalizationTextReadModel manager, Span<char> destination)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue) &&
                !string.Equals(configuredValue, legacyDefault, StringComparison.Ordinal))
            {
                return CopyTruncated(configuredValue.AsSpan(), destination);
            }

            return CopyLocalizedTruncated(manager, key, legacyDefault, destination);
        }

        public static bool TryCopyWithQuantity(string source, int quantity, Span<char> destination, out int length)
        {
            ReadOnlySpan<char> sourceSpan = string.IsNullOrEmpty(source) ? ReadOnlySpan<char>.Empty : source.AsSpan();
            return TryCopyWithQuantity(sourceSpan, quantity, destination, out length);
        }

        public static bool TryCopyWithQuantity(ReadOnlySpan<char> sourceSpan, int quantity, Span<char> destination, out int length)
        {
            int safeQuantity = quantity > 1 ? quantity : 1;
            int digitCount = safeQuantity > 1 ? CountPositiveDigits(safeQuantity) : 0;
            int suffixLength = safeQuantity > 1 ? 2 + digitCount : 0;
            length = sourceSpan.Length + suffixLength;
            if (sourceSpan.IsEmpty || destination.Length < length)
            {
                length = 0;
                return false;
            }

            sourceSpan.CopyTo(destination);
            if (safeQuantity <= 1)
                return true;

            int index = sourceSpan.Length;
            destination[index++] = ' ';
            destination[index++] = 'x';
            for (int divisor = Pow10(digitCount - 1); divisor > 0; divisor /= 10)
            {
                int digit = safeQuantity / divisor;
                destination[index++] = (char)('0' + digit);
                safeQuantity -= digit * divisor;
            }

            return true;
        }

        private static int CountPositiveDigits(int value)
        {
            int digits = 1;
            while (value >= 10)
            {
                value /= 10;
                digits++;
            }

            return digits;
        }

        private static int Pow10(int exponent)
        {
            int value = 1;
            for (int i = 0; i < exponent; i++)
                value *= 10;

            return value;
        }
    }
}
