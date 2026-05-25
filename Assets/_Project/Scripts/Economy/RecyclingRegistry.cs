using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Runtime-only recycling overlay registry used by mods and first-party systems.
    /// </summary>
    public static class RecyclingRegistry
    {
        // COLD ALLOC: Dictionary<string,ResourceStack[]>[32] - mod/editor compatibility seam keyed by stable item ID - owner: RecyclingRegistry
        private static readonly Dictionary<string, ResourceStack[]> _customYields =
            new Dictionary<string, ResourceStack[]>(32, System.StringComparer.Ordinal);
        // COLD ALLOC: Dictionary<uint,ResourceStack[]>[32] - runtime recycle lookup keyed by item hash - owner: RecyclingRegistry
        private static readonly Dictionary<uint, ResourceStack[]> _customYieldsByHash =
            new Dictionary<uint, ResourceStack[]>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _customYields.Clear();
            _customYieldsByHash.Clear();
        }

        /// <summary>
        /// Registers or replaces a custom recycle yield for the specified item ID.
        /// </summary>
        /// <param name="legacyItemId">Stable source item identifier. Converted to hash at the seam.</param>
        /// <param name="yield">Returned stacks granted when the item is recycled.</param>
        /// <param name="error">Validation error when registration fails.</param>
        public static bool TryRegister(string legacyItemId, IList<ResourceStack> yield, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(legacyItemId))
            {
                error = "Source item ID is empty.";
                return false;
            }

            if (yield == null || yield.Count == 0)
            {
                error = "Recycle yield is empty.";
                return false;
            }

            if (!TryCloneYield(yield, out ResourceStack[] clonedStacks, out error))
                return false;

            string stableId = legacyItemId.Trim();
            uint itemHash = unchecked((uint)LocHash.Compute(stableId));
            _customYields[stableId] = clonedStacks;
            if (itemHash != 0u)
                _customYieldsByHash[itemHash] = clonedStacks;
            return true;
        }

        /// <summary>
        /// Registers or replaces a custom recycle yield for the specified source item hash.
        /// </summary>
        public static bool TryRegister(uint targetHashId, IList<ResourceStack> yield, out string error)
        {
            error = null;
            if (targetHashId == 0u)
            {
                error = "Source item hash is zero.";
                return false;
            }

            if (!TryCloneYield(yield, out ResourceStack[] clonedStacks, out error))
                return false;

            _customYieldsByHash[targetHashId] = clonedStacks;
            return true;
        }

        /// <summary>
        /// Registers or replaces a custom recycle yield for the specified source item.
        /// </summary>
        public static bool TryRegister(ItemData sourceItem, IList<ResourceStack> yield, out string error)
        {
            error = null;

            if (sourceItem == null)
            {
                error = "Source item is null.";
                return false;
            }

            return TryRegister(unchecked((uint)sourceItem.PersistentHashId), yield, out error);
        }

        /// <summary>
        /// Resolves a registered recycle override for the specified item ID.
        /// </summary>
        public static bool TryGetYield(string legacyItemId, out ResourceStack[] yield)
        {
            if (string.IsNullOrWhiteSpace(legacyItemId))
            {
                yield = null;
                return false;
            }

            string stableId = legacyItemId.Trim();
            uint itemHash = unchecked((uint)LocHash.Compute(stableId));
            if (itemHash != 0u && _customYieldsByHash.TryGetValue(itemHash, out yield))
                return true;

            return _customYields.TryGetValue(stableId, out yield);
        }

        /// <summary>
        /// Resolves a registered recycle override for the specified item hash.
        /// </summary>
        public static bool TryGetYield(uint targetHashId, out ResourceStack[] yield)
        {
            if (targetHashId == 0u)
            {
                yield = null;
                return false;
            }

            return _customYieldsByHash.TryGetValue(targetHashId, out yield);
        }

        /// <summary>
        /// Removes any previously registered recycle override for the specified item ID.
        /// </summary>
        public static void Clear(string legacyItemId)
        {
            if (string.IsNullOrWhiteSpace(legacyItemId))
                return;

            string stableId = legacyItemId.Trim();
            _customYields.Remove(stableId);
            uint itemHash = unchecked((uint)LocHash.Compute(stableId));
            if (itemHash != 0u)
                _customYieldsByHash.Remove(itemHash);
        }

        /// <summary>
        /// Removes any previously registered recycle override for the specified source item hash.
        /// </summary>
        public static void Clear(uint targetHashId)
        {
            if (targetHashId != 0u)
                _customYieldsByHash.Remove(targetHashId);
        }

        private static bool TryCloneYield(IList<ResourceStack> yield, out ResourceStack[] clonedStacks, out string error)
        {
            clonedStacks = null;
            error = null;
            if (yield == null || yield.Count == 0)
            {
                error = "Recycle yield is empty.";
                return false;
            }

            int stackCount = yield.Count;
            clonedStacks = new ResourceStack[stackCount]; // COLD ALLOC: ResourceStack[yield.Count] - immutable overlay snapshot - owner: RecyclingRegistry
            for (int i = 0; i < stackCount; i++)
            {
                ResourceStack stack = yield[i];
                if (stack.Item == null)
                {
                    error = $"Recycle yield entry {i} has no item.";
                    return false;
                }

                if (stack.Amount <= 0)
                {
                    error = $"Recycle yield entry {i} amount must be greater than zero.";
                    return false;
                }

                clonedStacks[i] = stack;
            }

            return true;
        }
    }
}
