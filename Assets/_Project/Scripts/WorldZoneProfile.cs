using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldZoneProfile", menuName = "Hecton8/World/Zone Profile")]
    public sealed class WorldZoneProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "zone.profile.generic";
        public string profileLabel = "Generic Zone";

        [Header("Budget")]
        [Range(0.6f, 1.4f)] public float scavengeRadiusScale = 1f;
        [Range(0.6f, 1.4f)] public float spawnScale = 1f;
        [Range(0.6f, 1.4f)] public float colliderRadiusScale = 1f;
        [Range(0.6f, 1.4f)] public float colliderOpsScale = 1f;

        [Header("Slice")]
        [Range(0.75f, 1.35f)] public float sliceNearScale = 1f;
        [Range(0.8f, 1.45f)] public float sliceMidScale = 1f;

        [Header("Future Content Families")]
        public string nearInteractiveFamily = "world.near.generic";
        public string midVisualFamily = "world.mid.generic";
        public string farSilhouetteFamily = "world.far.generic";
        public WorldPrefabFamilyProfile nearInteractiveProfile;
        public WorldPrefabFamilyProfile midVisualProfile;
        public WorldPrefabFamilyProfile farSilhouetteProfile;
        public WorldZonePlanProfile zonePlanProfile;
    }
}
