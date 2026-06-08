using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Items;
using Hecton8.Modding;
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
        // COLD ALLOC: Dictionary<string,string>[32] - stable item id to mod owner id cleanup index - owner: RecyclingRegistry
        private static readonly Dictionary<string, string> _customYieldOwnerById =
            new Dictionary<string, string>(32, System.StringComparer.Ordinal);
        // COLD ALLOC: Dictionary<uint,string>[32] - item hash to mod owner id cleanup index - owner: RecyclingRegistry
        private static readonly Dictionary<uint, string> _customYieldOwnerByHash =
            new Dictionary<uint, string>(32);
        // COLD ALLOC: List<string>[16] - owner cleanup stable id scratch - owner: RecyclingRegistry
        private static readonly List<string> _stableIdRemovalScratch = new List<string>(16);
        // COLD ALLOC: List<uint>[16] - owner cleanup item hash scratch - owner: RecyclingRegistry
        private static readonly List<uint> _hashRemovalScratch = new List<uint>(16);
        private static uint _registryRevision;

        internal static uint RegistryRevision => _registryRevision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _customYields.Clear();
            _customYieldsByHash.Clear();
            _customYieldOwnerById.Clear();
            _customYieldOwnerByHash.Clear();
            _stableIdRemovalScratch.Clear();
            _hashRemovalScratch.Clear();
            _registryRevision = 0u;
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
            uint itemHash = ComputeStableItemHash(stableId);
            string ownerId = ResolveActiveOwnerId();
            _customYields[stableId] = clonedStacks;
            RecordOwner(_customYieldOwnerById, stableId, ownerId);
            if (itemHash != 0u)
            {
                _customYieldsByHash[itemHash] = clonedStacks;
                RecordOwner(_customYieldOwnerByHash, itemHash, ownerId);
            }

            NotifyRecycleRegistryChanged();
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
            RecordOwner(_customYieldOwnerByHash, targetHashId, ResolveActiveOwnerId());
            NotifyRecycleRegistryChanged();
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

        internal static bool TryGetYield(uint targetHashId, out ResourceStack[] yield, out uint ownerHash)
        {
            ownerHash = 0u;
            if (!TryGetYield(targetHashId, out yield))
                return false;

            TryResolveOwnerHash(_customYieldOwnerByHash, targetHashId, out ownerHash);
            return true;
        }

        /// <summary>
        /// Removes any previously registered recycle override for the specified item ID.
        /// </summary>
        public static void Clear(string legacyItemId)
        {
            if (string.IsNullOrWhiteSpace(legacyItemId))
                return;

            string stableId = legacyItemId.Trim();
            bool removed = _customYields.Remove(stableId);
            if (_customYieldOwnerById.Remove(stableId))
                removed = true;
            uint itemHash = ComputeStableItemHash(stableId);
            if (itemHash != 0u)
            {
                if (_customYieldsByHash.Remove(itemHash))
                    removed = true;
                if (_customYieldOwnerByHash.Remove(itemHash))
                    removed = true;
            }

            if (removed)
                NotifyRecycleRegistryChanged();
        }

        /// <summary>
        /// Removes any previously registered recycle override for the specified source item hash.
        /// </summary>
        public static void Clear(uint targetHashId)
        {
            if (targetHashId != 0u)
            {
                bool removed = _customYieldsByHash.Remove(targetHashId);
                if (_customYieldOwnerByHash.Remove(targetHashId))
                    removed = true;

                if (removed)
                    NotifyRecycleRegistryChanged();
            }
        }

        internal static bool ClearOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return false;

            bool removed = false;
            _stableIdRemovalScratch.Clear();
            Dictionary<string, string>.Enumerator idEnumerator = _customYieldOwnerById.GetEnumerator();
            while (idEnumerator.MoveNext())
            {
                if (string.Equals(idEnumerator.Current.Value, ownerId, System.StringComparison.Ordinal))
                    _stableIdRemovalScratch.Add(idEnumerator.Current.Key);
            }

            for (int i = 0; i < _stableIdRemovalScratch.Count; i++)
            {
                string stableId = _stableIdRemovalScratch[i];
                _customYields.Remove(stableId);
                _customYieldOwnerById.Remove(stableId);
                removed = true;
            }

            _hashRemovalScratch.Clear();
            Dictionary<uint, string>.Enumerator hashEnumerator = _customYieldOwnerByHash.GetEnumerator();
            while (hashEnumerator.MoveNext())
            {
                if (string.Equals(hashEnumerator.Current.Value, ownerId, System.StringComparison.Ordinal))
                    _hashRemovalScratch.Add(hashEnumerator.Current.Key);
            }

            for (int i = 0; i < _hashRemovalScratch.Count; i++)
            {
                uint itemHash = _hashRemovalScratch[i];
                _customYieldsByHash.Remove(itemHash);
                _customYieldOwnerByHash.Remove(itemHash);
                removed = true;
            }

            _stableIdRemovalScratch.Clear();
            _hashRemovalScratch.Clear();
            if (removed)
                NotifyRecycleRegistryChanged();

            return removed;
        }

        internal static uint ComputeStableItemHash(string legacyItemId)
        {
            if (string.IsNullOrWhiteSpace(legacyItemId))
                return 0u;

            return unchecked((uint)LocHash.Compute(legacyItemId.Trim()));
        }

        private static string ResolveActiveOwnerId()
        {
            return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;
        }

        private static void RecordOwner<TKey>(Dictionary<TKey, string> ownerIndex, TKey key, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return;
            }

            ownerIndex[key] = ownerId;
        }

        private static bool TryResolveOwnerHash<TKey>(Dictionary<TKey, string> ownerIndex, TKey key, out uint ownerHash)
        {
            ownerHash = 0u;
            if (ownerIndex == null ||
                !ownerIndex.TryGetValue(key, out string ownerId) ||
                string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            ownerHash = ModCommandDispatcher.ComputeModHash(ownerId);
            return ownerHash != 0u;
        }

        private static void NotifyRecycleRegistryChanged()
        {
            unchecked
            {
                _registryRevision++;
                if (_registryRevision == 0u)
                    _registryRevision = 1u;
            }

            ModRegistryEvents.NotifyRecycleRegistryChanged();
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
