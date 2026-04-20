using Hecton.Localization;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Lore object type used by PDA/codex consumers.
    /// </summary>
    public enum LoreObjectType
    {
        DataPad = 0,
        AudioLog = 1,
        Blueprint = 2,
        PersonalItem = 3,
        Terminal = 4,
        Wreckage = 5,
    }

    [System.Serializable]
    public struct LoreEntry
    {
        [Tooltip("Unique discovery ID shared with NarrativeDiscovery.")]
        public string discoveryId;

        [Tooltip("Legacy display name fallback.")]
        public string displayName;

        [Tooltip("Localized display name.")]
        public LocalizedTextReference localizedDisplayName;

        [Tooltip("Lore object type for PDA behavior.")]
        public LoreObjectType objectType;

        [Tooltip("Optional linked audio log.")]
        public AudioLogData linkedAudioLog;

        [Tooltip("Legacy PDA description fallback.")]
        [TextArea(2, 4)] public string description;

        [Tooltip("Localized PDA description.")]
        public LocalizedTextReference localizedDescription;

        [Tooltip("Legacy location hint fallback.")]
        public string locationHint;

        [Tooltip("Localized location hint.")]
        public LocalizedTextReference localizedLocationHint;

        public string DisplayNameOrFallback => localizedDisplayName.ResolveOrFallback(displayName);
        public string DescriptionOrFallback => localizedDescription.ResolveOrFallback(description);
        public string LocationHintOrFallback => localizedLocationHint.ResolveOrFallback(locationHint);
    }

    [CreateAssetMenu(fileName = "ColonistLoreRegistry", menuName = "Hecton8/Narrative/Colonist Lore Registry", order = 5)]
    public sealed class ColonistLoreRegistry : ScriptableObject
    {
        [SerializeField] public LoreEntry[] entries = new LoreEntry[0];
        private readonly Dictionary<string, int> _entryIndexByDiscoveryId = new Dictionary<string, int>(64, System.StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguousDiscoveryIds = new HashSet<string>(System.StringComparer.Ordinal);
        private bool _lookupReady;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGetEntry(string discoveryId, out LoreEntry entry)
        {
            if (string.IsNullOrWhiteSpace(discoveryId))
            {
                entry = default;
                return false;
            }

            if (!_lookupReady)
                RebuildLookup();

            if (_ambiguousDiscoveryIds.Contains(discoveryId) ||
                !_entryIndexByDiscoveryId.TryGetValue(discoveryId, out int index) ||
                entries == null ||
                index < 0 ||
                index >= entries.Length)
            {
                entry = default;
                return false;
            }

            entry = entries[index];
            return true;
        }

        private void RebuildLookup()
        {
            _entryIndexByDiscoveryId.Clear();
            _ambiguousDiscoveryIds.Clear();
            _lookupReady = true;

            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                string discoveryId = entries[i].discoveryId;
                if (string.IsNullOrWhiteSpace(discoveryId))
                    continue;

                if (_ambiguousDiscoveryIds.Contains(discoveryId))
                    continue;

                if (_entryIndexByDiscoveryId.TryGetValue(discoveryId, out int existingIndex))
                {
                    if (existingIndex != i)
                    {
                        _entryIndexByDiscoveryId.Remove(discoveryId);
                        _ambiguousDiscoveryIds.Add(discoveryId);
                    }

                    continue;
                }

                _entryIndexByDiscoveryId.Add(discoveryId, i);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entries == null)
            {
                RebuildLookup();
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                LoreEntry entry = entries[i];
                if (!string.IsNullOrWhiteSpace(entry.discoveryId))
                    entry.discoveryId = entry.discoveryId.Trim();

                entries[i] = entry;
            }

            RebuildLookup();
        }
#endif
    }
}
