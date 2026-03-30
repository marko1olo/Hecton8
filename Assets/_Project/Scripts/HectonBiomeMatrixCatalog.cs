using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeMatrixCatalog", menuName = "Hecton/Environment/Biome Matrix Catalog", order = 103)]
    public sealed class HectonBiomeMatrixCatalog : ScriptableObject
    {
        [SerializeField] private HectonBiomeMatrixProfile[] profiles = new HectonBiomeMatrixProfile[108];

        public int Count => profiles != null ? profiles.Length : 0;
        public HectonBiomeMatrixProfile[] Profiles => profiles;

        public HectonBiomeMatrixProfile GetByMatrixIndex(int matrixIndex)
        {
            if (profiles == null || profiles.Length == 0)
                return null;

            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile != null && profile.matrixIndex == matrixIndex)
                    return profile;
            }

            return null;
        }

        public HectonBiomeMatrixProfile Resolve(int tier, HectonBiomeMatrixProfile.CardinalRegion region)
        {
            if (profiles == null || profiles.Length == 0)
                return null;

            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                if (profile.depthTier == tier && profile.region == region)
                    return profile;
            }

            return null;
        }

        public bool Validate(out string error)
        {
            if (profiles == null || profiles.Length != 108)
            {
                error = "Biome matrix catalog must contain exactly 108 slots.";
                return false;
            }

            bool[] seen = new bool[109];
            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                {
                    error = $"Biome matrix slot {i} is null.";
                    return false;
                }

                if (profile.matrixIndex < 1 || profile.matrixIndex > 108)
                {
                    error = $"Biome '{profile.name}' has invalid matrixIndex {profile.matrixIndex}.";
                    return false;
                }

                if (seen[profile.matrixIndex])
                {
                    error = $"Duplicate matrixIndex {profile.matrixIndex}.";
                    return false;
                }

                seen[profile.matrixIndex] = true;
            }

            error = null;
            return true;
        }
    }
}
