using System.Collections.Generic;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Runtime-only recycling overlay registry used by mods and first-party systems.
    /// </summary>
    public static class RecyclingRegistry
    {
        // COLD ALLOC: Dictionary<string,ResourceStack[]>[32] - runtime recycling overrides keyed by stable item ID - owner: RecyclingRegistry
        private static readonly Dictionary<string, ResourceStack[]> _customYields =
            new Dictionary<string, ResourceStack[]>(32, System.StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _customYields.Clear();
        }

        /// <summary>
        /// Registers or replaces a custom recycle yield for the specified item ID.
        /// </summary>
        /// <param name="itemId">Stable source item identifier.</param>
        /// <param name="yield">Returned stacks granted when the item is recycled.</param>
        /// <param name="error">Validation error when registration fails.</param>
        public static bool TryRegister(string itemId, IList<ResourceStack> yield, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                error = "Source item ID is empty.";
                return false;
            }

            if (yield == null || yield.Count == 0)
            {
                error = "Recycle yield is empty.";
                return false;
            }

            int stackCount = yield.Count;
            ResourceStack[] clonedStacks = new ResourceStack[stackCount]; // COLD ALLOC: ResourceStack[yield.Count] - immutable overlay snapshot - owner: RecyclingRegistry
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

            _customYields[itemId.Trim()] = clonedStacks;
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

            return TryRegister(sourceItem.PersistentId, yield, out error);
        }

        /// <summary>
        /// Resolves a registered recycle override for the specified item ID.
        /// </summary>
        public static bool TryGetYield(string itemId, out ResourceStack[] yield)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                yield = null;
                return false;
            }

            return _customYields.TryGetValue(itemId.Trim(), out yield);
        }

        /// <summary>
        /// Removes any previously registered recycle override for the specified item ID.
        /// </summary>
        public static void Clear(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            _customYields.Remove(itemId.Trim());
        }
    }
}
