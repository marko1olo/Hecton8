using System.Collections.Generic;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Runtime fauna scan registry keyed by scanner entry hash.
    /// Maps active fauna templates to stable lore/research unlock hashes without touching hot-path cognition.
    /// </summary>
    internal static class FaunaScanRuntimeRegistry
    {
        // COLD ALLOC: Dictionary<uint,uint[]>[64] - active fauna scan unlock lookup keyed by scan entry hash - owner: FaunaScanRuntimeRegistry
        private static readonly Dictionary<uint, uint[]> _loreUnlocksByEntryHash = new Dictionary<uint, uint[]>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _loreUnlocksByEntryHash.Clear();
        }

        internal static void Register(FaunaDataTemplate template)
        {
            if (template == null)
                return;

            uint[] loreUnlockHashes = template.LoreUnlockHashes;
            if (loreUnlockHashes == null || loreUnlockHashes.Length == 0)
                return;

            uint entryHash = ScanEvents.ComputeEntryHash(template.ScanEntryId);
            if (entryHash == 0u)
                return;

            _loreUnlocksByEntryHash[entryHash] = loreUnlockHashes;
        }

        internal static bool TryGetLoreUnlocks(uint entryHash, out uint[] loreUnlockHashes)
        {
            return _loreUnlocksByEntryHash.TryGetValue(entryHash, out loreUnlockHashes);
        }
    }
}
