using System;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [Flags]
    public enum BarterOfferValidationFlags : uint
    {
        None = 0u,
        MissingOfferId = 1u << 0,
        MissingOfferName = 1u << 1,
        MissingChannelName = 1u << 2,
        MissingCosts = 1u << 3,
        MissingRewards = 1u << 4,
        InvalidCostItem = 1u << 5,
        InvalidCostAmount = 1u << 6,
        InvalidRewardItem = 1u << 7,
        InvalidRewardAmount = 1u << 8
    }

    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BarterItemAmount
    {
        public ItemData item;
        [Min(1)] public int amount;

        public int RuntimeAmount => amount > 0 ? amount : 1;

        public int RuntimeItemHash => item != null ? item.PersistentHashId : 0;

        public bool IsRuntimeValid => item != null && item.PersistentHashId != 0 && amount > 0;
    }

    [CreateAssetMenu(fileName = "BarterOffer", menuName = "Hecton/Barter Offer", order = 130)]
    public sealed class BarterOfferData : ScriptableObject
    {
        public const string DefaultOfferId = "offer.new";
        public const string DefaultOfferName = "Untitled Exchange";
        public const string DefaultChannelName = "FIELD RELAY";

        [Header("Identity")]
        public string offerId = DefaultOfferId;
        public string offerName = DefaultOfferName;
        [TextArea(2, 5)] public string description = string.Empty;
        public string channelName = DefaultChannelName;

        [Header("Gates")]
        [Tooltip("Optional scan-log entry required before this offer becomes available.")]
        public string requiredScanEntryId = string.Empty;
        [Tooltip("0 = unlimited executions.")]
        [Min(0)] public int repeatLimit = 1;

        [Header("Payload")]
        public BarterItemAmount[] costs = Array.Empty<BarterItemAmount>();
        public BarterItemAmount[] rewards = Array.Empty<BarterItemAmount>();

        [Header("Presentation")]
        public int priority = 0;
        public Sprite icon;

        [NonSerialized] private BarterOfferValidationFlags _validationFlags;
        [NonSerialized] private int _validCostCount;
        [NonSerialized] private int _validRewardCount;
        [NonSerialized] private int _invalidCostCount;
        [NonSerialized] private int _invalidRewardCount;
        [NonSerialized] private int _firstInvalidCostIndex = -1;
        [NonSerialized] private int _firstInvalidRewardIndex = -1;

        public string RuntimeOfferId => BarterOfferRuntimeGuards.TextOrFallback(offerId, DefaultOfferId);

        public string RuntimeOfferName => BarterOfferRuntimeGuards.TextOrFallback(offerName, DefaultOfferName);

        public string RuntimeDescription => BarterOfferRuntimeGuards.TextOrFallback(description, string.Empty);

        public string RuntimeChannelName => BarterOfferRuntimeGuards.TextOrFallback(channelName, DefaultChannelName);

        public string RuntimeRequiredScanEntryId => BarterOfferRuntimeGuards.TextOrFallback(requiredScanEntryId, string.Empty);

        public int RuntimeRepeatLimit => repeatLimit < 0 ? 0 : repeatLimit;

        public int CostSlotCount => costs != null ? costs.Length : 0;

        public int RewardSlotCount => rewards != null ? rewards.Length : 0;

        public int RuntimeCostCount => _validCostCount;

        public int RuntimeRewardCount => _validRewardCount;

        public BarterOfferValidationFlags ValidationFlags => _validationFlags;

        public bool HasValidationErrors => _validationFlags != BarterOfferValidationFlags.None;

        public bool HasBlockingRuntimeErrors =>
            (_validationFlags & (
                BarterOfferValidationFlags.MissingOfferId |
                BarterOfferValidationFlags.MissingCosts |
                BarterOfferValidationFlags.MissingRewards |
                BarterOfferValidationFlags.InvalidCostItem |
                BarterOfferValidationFlags.InvalidCostAmount |
                BarterOfferValidationFlags.InvalidRewardItem |
                BarterOfferValidationFlags.InvalidRewardAmount)) != 0;

        public int InvalidCostCount => _invalidCostCount;

        public int InvalidRewardCount => _invalidRewardCount;

        public int FirstInvalidCostIndex => _firstInvalidCostIndex;

        public int FirstInvalidRewardIndex => _firstInvalidRewardIndex;

        public bool IsRepeatable => RuntimeRepeatLimit == 0 || RuntimeRepeatLimit > 1;

        public bool TryGetCost(int validIndex, out BarterItemAmount entry)
        {
            return TryGetValidBundleEntry(costs, validIndex, out entry);
        }

        public bool TryGetReward(int validIndex, out BarterItemAmount entry)
        {
            return TryGetValidBundleEntry(rewards, validIndex, out entry);
        }

        public bool TryGetCostBySlot(int slot, out BarterItemAmount entry)
        {
            return TryGetBundleEntryBySlot(costs, slot, out entry);
        }

        public bool TryGetRewardBySlot(int slot, out BarterItemAmount entry)
        {
            return TryGetBundleEntryBySlot(rewards, slot, out entry);
        }

        public bool IsCostRuntimeValidBySlot(int slot)
        {
            return IsBundleEntryRuntimeValid(costs, slot);
        }

        public bool IsRewardRuntimeValidBySlot(int slot)
        {
            return IsBundleEntryRuntimeValid(rewards, slot);
        }

        private void OnEnable()
        {
            RebuildValidationCache();
        }

        private void RebuildValidationCache()
        {
            _validationFlags = BarterOfferValidationFlags.None;
            _validCostCount = 0;
            _validRewardCount = 0;
            _invalidCostCount = 0;
            _invalidRewardCount = 0;
            _firstInvalidCostIndex = -1;
            _firstInvalidRewardIndex = -1;

            if (string.IsNullOrWhiteSpace(offerId) || string.Equals(offerId, DefaultOfferId, StringComparison.Ordinal))
                AddValidationFlag(BarterOfferValidationFlags.MissingOfferId);

            if (string.IsNullOrWhiteSpace(offerName))
                AddValidationFlag(BarterOfferValidationFlags.MissingOfferName);

            if (string.IsNullOrWhiteSpace(channelName))
                AddValidationFlag(BarterOfferValidationFlags.MissingChannelName);

            RebuildBundleValidationCache(
                costs,
                BarterOfferValidationFlags.MissingCosts,
                BarterOfferValidationFlags.InvalidCostItem,
                BarterOfferValidationFlags.InvalidCostAmount,
                ref _validCostCount,
                ref _invalidCostCount,
                ref _firstInvalidCostIndex);

            RebuildBundleValidationCache(
                rewards,
                BarterOfferValidationFlags.MissingRewards,
                BarterOfferValidationFlags.InvalidRewardItem,
                BarterOfferValidationFlags.InvalidRewardAmount,
                ref _validRewardCount,
                ref _invalidRewardCount,
                ref _firstInvalidRewardIndex);
        }

        private void RebuildBundleValidationCache(
            BarterItemAmount[] bundle,
            BarterOfferValidationFlags missingFlag,
            BarterOfferValidationFlags invalidItemFlag,
            BarterOfferValidationFlags invalidAmountFlag,
            ref int validCount,
            ref int invalidCount,
            ref int firstInvalidIndex)
        {
            if (bundle == null || bundle.Length == 0)
            {
                AddValidationFlag(missingFlag);
                return;
            }

            for (int i = 0; i < bundle.Length; i++)
            {
                BarterOfferValidationFlags entryFlags = GetBundleEntryValidationFlags(bundle[i], invalidItemFlag, invalidAmountFlag);
                if (entryFlags == BarterOfferValidationFlags.None)
                {
                    validCount++;
                    continue;
                }

                invalidCount++;
                AddValidationFlag(entryFlags);
                if (firstInvalidIndex < 0)
                    firstInvalidIndex = i;
            }

            if (validCount <= 0)
                AddValidationFlag(missingFlag);
        }

        private static BarterOfferValidationFlags GetBundleEntryValidationFlags(
            BarterItemAmount entry,
            BarterOfferValidationFlags invalidItemFlag,
            BarterOfferValidationFlags invalidAmountFlag)
        {
            BarterOfferValidationFlags flags = BarterOfferValidationFlags.None;
            if (entry.item == null || entry.item.PersistentHashId == 0)
                flags |= invalidItemFlag;

            if (entry.amount <= 0)
                flags |= invalidAmountFlag;

            return flags;
        }

        private void AddValidationFlag(BarterOfferValidationFlags flag)
        {
            _validationFlags |= flag;
        }

        private static bool TryGetValidBundleEntry(BarterItemAmount[] bundle, int validIndex, out BarterItemAmount entry)
        {
            entry = default;
            if (bundle == null || validIndex < 0)
                return false;

            int validCursor = 0;
            for (int slot = 0; slot < bundle.Length; slot++)
            {
                BarterItemAmount candidate = bundle[slot];
                if (!candidate.IsRuntimeValid)
                    continue;

                if (validCursor == validIndex)
                {
                    entry = candidate;
                    return true;
                }

                validCursor++;
            }

            return false;
        }

        private static bool TryGetBundleEntryBySlot(BarterItemAmount[] bundle, int slot, out BarterItemAmount entry)
        {
            entry = default;
            if (bundle == null || slot < 0 || slot >= bundle.Length)
                return false;

            entry = bundle[slot];
            return entry.item != null;
        }

        private static bool IsBundleEntryRuntimeValid(BarterItemAmount[] bundle, int slot)
        {
            return bundle != null &&
                   slot >= 0 &&
                   slot < bundle.Length &&
                   bundle[slot].IsRuntimeValid;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(offerId) || string.Equals(offerId, DefaultOfferId, StringComparison.Ordinal))
                offerId = name.ToLowerInvariant().Replace(" ", "_");

            offerId = BarterOfferRuntimeGuards.NormalizeAuthoringText(offerId, DefaultOfferId);
            offerName = BarterOfferRuntimeGuards.NormalizeAuthoringText(offerName, DefaultOfferName);
            description = BarterOfferRuntimeGuards.NormalizeAuthoringText(description, string.Empty);
            channelName = BarterOfferRuntimeGuards.NormalizeAuthoringText(channelName, DefaultChannelName);
            requiredScanEntryId = BarterOfferRuntimeGuards.NormalizeAuthoringText(requiredScanEntryId, string.Empty);
            repeatLimit = RuntimeRepeatLimit;

            if (costs == null)
                costs = Array.Empty<BarterItemAmount>();
            if (rewards == null)
                rewards = Array.Empty<BarterItemAmount>();

            NormalizeBundle(costs);
            NormalizeBundle(rewards);
            RebuildValidationCache();
        }

        private static void NormalizeBundle(BarterItemAmount[] bundle)
        {
            if (bundle == null)
                return;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (bundle[i].amount <= 0)
                    bundle[i].amount = 1;
            }
        }
#endif
    }

    internal static class BarterOfferRuntimeGuards
    {
        public static string TextOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

#if UNITY_EDITOR
        public static string NormalizeAuthoringText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
#endif
    }
}
