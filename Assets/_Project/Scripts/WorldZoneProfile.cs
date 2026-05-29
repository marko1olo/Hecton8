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
        public WorldExpeditionLoopProfile expeditionLoopProfile;
        public WorldSandboxAttractionProfile sandboxAttractionProfile;
        public WorldMotivationProfile motivationProfile;

        public float ScavengeRadiusScale => Mathf.Clamp(scavengeRadiusScale, 0.6f, 1.4f);
        public float SpawnScale => Mathf.Clamp(spawnScale, 0.6f, 1.4f);
        public float ColliderRadiusScale => Mathf.Clamp(colliderRadiusScale, 0.6f, 1.4f);
        public float ColliderOpsScale => Mathf.Clamp(colliderOpsScale, 0.6f, 1.4f);
        public float SliceNearScale => Mathf.Clamp(sliceNearScale, 0.75f, 1.35f);
        public float SliceMidScale => Mathf.Clamp(sliceMidScale, 0.8f, 1.45f);

#if UNITY_EDITOR
        private void OnValidate()
        {
            profileId = NormalizeIdentifier(profileId, "zone.profile.generic");
            profileLabel = NormalizeLabel(profileLabel, "Generic Zone");
            nearInteractiveFamily = NormalizeIdentifier(nearInteractiveFamily, "world.near.generic");
            midVisualFamily = NormalizeIdentifier(midVisualFamily, "world.mid.generic");
            farSilhouetteFamily = NormalizeIdentifier(farSilhouetteFamily, "world.far.generic");

            scavengeRadiusScale = ScavengeRadiusScale;
            spawnScale = SpawnScale;
            colliderRadiusScale = ColliderRadiusScale;
            colliderOpsScale = ColliderOpsScale;
            sliceNearScale = SliceNearScale;
            sliceMidScale = SliceMidScale;
        }

        private static string NormalizeIdentifier(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeLabel(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
#endif
    }
}
