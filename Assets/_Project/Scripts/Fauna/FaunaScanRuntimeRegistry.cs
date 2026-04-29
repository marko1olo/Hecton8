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
        internal readonly struct FaunaScanMetadata
        {
            public FaunaScanMetadata(uint[] loreUnlockHashes, uint fullLoreHash, FaunaAttackPattern[] attackPatterns)
            {
                LoreUnlockHashes = loreUnlockHashes;
                FullLoreHash = fullLoreHash;
                AttackPatterns = attackPatterns;
            }

            public uint[] LoreUnlockHashes { get; }
            public uint FullLoreHash { get; }
            public FaunaAttackPattern[] AttackPatterns { get; }
        }

        // COLD ALLOC: Dictionary<uint,FaunaScanMetadata>[64] - active fauna scan metadata keyed by scan entry hash - owner: FaunaScanRuntimeRegistry
        private static readonly Dictionary<uint, FaunaScanMetadata> _metadataByEntryHash = new Dictionary<uint, FaunaScanMetadata>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _metadataByEntryHash.Clear();
        }

        internal static void Register(FaunaDataTemplate template)
        {
            if (template == null)
                return;

            uint entryHash = ScanEvents.ComputeEntryHash(template.ScanEntryId);
            if (entryHash == 0u)
                return;

            _metadataByEntryHash[entryHash] = new FaunaScanMetadata(
                template.LoreUnlockHashes,
                template.FullLoreHash,
                template.AttackPatterns);
        }

        internal static bool TryGetLoreUnlocks(uint entryHash, out uint[] loreUnlockHashes)
        {
            if (_metadataByEntryHash.TryGetValue(entryHash, out FaunaScanMetadata metadata))
            {
                loreUnlockHashes = metadata.LoreUnlockHashes;
                return loreUnlockHashes != null && loreUnlockHashes.Length > 0;
            }

            loreUnlockHashes = null;
            return false;
        }

        internal static bool TryGetScanMetadata(uint entryHash, out FaunaScanMetadata metadata)
        {
            return _metadataByEntryHash.TryGetValue(entryHash, out metadata);
        }
    }
}
