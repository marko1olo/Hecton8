using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldPopulationRule", menuName = "Hecton8/World/Population Rule")]
    public sealed class WorldPopulationRule : ScriptableObject
    {
        [Header("Identity")]
        public string ruleId = "population.rule.generic";
        public string ruleLabel = "Generic Population Rule";

        [Header("Target")]
        public WorldZoneAnchor.ZoneKind zoneKind = WorldZoneAnchor.ZoneKind.Generic;
        public WorldZoneAnchor.ZoneTier minTier = WorldZoneAnchor.ZoneTier.Starter;
        public WorldZoneAnchor.ZoneTier maxTier = WorldZoneAnchor.ZoneTier.Endgame;
        public WorldContentSocket.ContentKind contentKind = WorldContentSocket.ContentKind.Generic;

        [Header("Future Population")]
        public string prefabFamily = string.Empty;
        public WorldPrefabFamilyProfile familyProfile;
        public string gameplayPurpose = "Generic world population.";
        [TextArea(1, 3)] public string biomeFitSummary = string.Empty;
        public HectonBiomeFamilyProfile[] preferredBiomeFamilies;
        [Range(0.1f, 3f)] public float densityWeight = 1f;
        public int suggestedClusterCount = 1;
        public int suggestedMinCount = 1;
        public int suggestedMaxCount = 1;

        public bool Matches(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (zone == null)
                return false;

            if (zoneKind != WorldZoneAnchor.ZoneKind.Generic && zone.Kind != zoneKind)
                return false;

            if (zone.Tier < minTier || zone.Tier > maxTier)
                return false;

            if (contentKind != WorldContentSocket.ContentKind.Generic)
            {
                if (socket == null || socket.Kind != contentKind)
                    return false;
            }

            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0)
            {
                HectonBiomeFamilyProfile zoneBiomeFamily = zone.DominantBiomeFamily;
                bool biomeMatch = false;

                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == null || preferredBiomeFamilies[i] != zoneBiomeFamily)
                        continue;

                    biomeMatch = true;
                    break;
                }

                if (!biomeMatch)
                    return false;
            }

            return true;
        }
    }
}
