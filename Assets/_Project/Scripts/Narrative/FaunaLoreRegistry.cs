using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Narrative
{
    public enum FaunaType
    {
        Ambient = 0,
        Predator = 1,
        SoftWall = 2,
        Boss = 3,
        Atlas6Drone = 4,
    }

    [System.Serializable]
    public struct FaunaLoreEntry
    {
        [Tooltip("Creature ID shared with gameplay archetypes.")]
        public string creatureId;

        [Tooltip("Legacy display name fallback.")]
        public string displayName;

        [Tooltip("Localized display name.")]
        public LocalizedTextReference localizedDisplayName;

        [Tooltip("Fauna category.")]
        public FaunaType faunaType;

        [Tooltip("Legacy habitat zone fallback.")]
        public string habitatZone;

        [Tooltip("Localized habitat zone.")]
        public LocalizedTextReference localizedHabitatZone;

        [Tooltip("Approximate size in meters.")]
        public float sizeMeters;

        [Tooltip("Legacy codex description fallback.")]
        [TextArea(3, 6)] public string codexDescription;

        [Tooltip("Localized codex description.")]
        public LocalizedTextReference localizedCodexDescription;

        [Tooltip("Legacy gameplay role fallback.")]
        [TextArea(1, 3)] public string gameplayRole;

        [Tooltip("Localized gameplay role.")]
        public LocalizedTextReference localizedGameplayRole;

        [Tooltip("Legacy scientific basis fallback.")]
        [TextArea(1, 3)] public string scientificBasis;

        [Tooltip("Localized scientific basis.")]
        public LocalizedTextReference localizedScientificBasis;

        [Tooltip("Night behavior flag.")]
        public bool hasNightBehavior;

        [Tooltip("Legacy night behavior fallback.")]
        [TextArea(1, 3)] public string nightBehaviorDescription;

        [Tooltip("Localized night behavior description.")]
        public LocalizedTextReference localizedNightBehaviorDescription;

        public string DisplayNameOrFallback => localizedDisplayName.ResolveOrFallback(displayName);
        public string HabitatZoneOrFallback => localizedHabitatZone.ResolveOrFallback(habitatZone);
        public string CodexDescriptionOrFallback => localizedCodexDescription.ResolveOrFallback(codexDescription);
        public string GameplayRoleOrFallback => localizedGameplayRole.ResolveOrFallback(gameplayRole);
        public string ScientificBasisOrFallback => localizedScientificBasis.ResolveOrFallback(scientificBasis);
        public string NightBehaviorDescriptionOrFallback => localizedNightBehaviorDescription.ResolveOrFallback(nightBehaviorDescription);
    }

    [CreateAssetMenu(fileName = "FaunaLoreRegistry", menuName = "Hecton8/Narrative/Fauna Lore Registry", order = 7)]
    public sealed class FaunaLoreRegistry : ScriptableObject
    {
        [SerializeField] public FaunaLoreEntry[] entries = new FaunaLoreEntry[0];

        public bool TryGetEntry(string creatureId, out FaunaLoreEntry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].creatureId == creatureId)
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
