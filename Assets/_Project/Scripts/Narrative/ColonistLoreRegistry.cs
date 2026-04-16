using Hecton.Localization;
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

        public bool TryGetEntry(string discoveryId, out LoreEntry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].discoveryId == discoveryId)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
