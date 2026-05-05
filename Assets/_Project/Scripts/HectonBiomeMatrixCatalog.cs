using UnityEngine;

namespace Hecton8.Environment
{
    public enum HectonBiomeVisualFamily : byte
    {
        Sand = 0,
        Basalt = 1,
        Kelp = 2,
        Brine = 3,
        Volcanic = 4,
        Coral = 5,
        Abyssal = 6,
        Alien = 7
    }

    public static class HectonBiomeVisualFamilyUtility
    {
        public const int VisualFamilyCount = 8;
        public const int PrimaryVisualFamilyShift = 0;
        public const int SecondaryVisualFamilyShift = 3;
        public const int BlendShift = 6;
        public const uint VisualFamilyMask = 0x7u;
        public const uint BlendMask = 0xFFu;

        public static byte MapToVisualFamily(int biomeId)
        {
            switch (biomeId)
            {
                // biome.family.sediment_drift
                case 4:
                case 8:
                case 13:
                case 14:
                case 24:
                case 28:
                case 30:
                case 32:
                case 36:
                    return (byte)HectonBiomeVisualFamily.Sand;

                // biome.family.tectonic_spine, granite_escarpment, rift_spine, metallic_hadal
                case 7:
                case 9:
                case 11:
                case 15:
                case 17:
                case 18:
                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                case 25:
                case 26:
                case 27:
                case 29:
                case 31:
                case 35:
                case 38:
                case 43:
                case 57:
                case 59:
                case 60:
                case 69:
                case 70:
                case 71:
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 80:
                case 81:
                case 83:
                case 85:
                case 86:
                case 87:
                case 88:
                case 97:
                case 105:
                case 108:
                    return (byte)HectonBiomeVisualFamily.Basalt;

                // biome.family.littoral_karst
                case 1:
                case 2:
                    return (byte)HectonBiomeVisualFamily.Kelp;

                // biome.family.chemosynthetic_brine
                case 37:
                case 58:
                case 78:
                    return (byte)HectonBiomeVisualFamily.Brine;

                // biome.family.volcanic_glass, volcanic_hadal
                case 3:
                case 10:
                case 41:
                case 42:
                case 77:
                case 82:
                case 84:
                case 98:
                case 106:
                    return (byte)HectonBiomeVisualFamily.Volcanic;

                // biome.family.fossil_reef, crystal_growth
                case 5:
                case 6:
                case 12:
                case 16:
                case 34:
                    return (byte)HectonBiomeVisualFamily.Coral;

                // biome.family.abyssal_silt
                case 39:
                case 44:
                case 45:
                case 46:
                case 47:
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 55:
                case 56:
                case 61:
                case 62:
                case 63:
                case 64:
                case 65:
                case 66:
                case 67:
                case 68:
                    return (byte)HectonBiomeVisualFamily.Abyssal;

                // biome.family.rift_void
                case 33:
                case 40:
                case 79:
                case 89:
                case 90:
                case 91:
                case 92:
                case 93:
                case 94:
                case 95:
                case 96:
                case 99:
                case 100:
                case 101:
                case 102:
                case 103:
                case 104:
                case 107:
                    return (byte)HectonBiomeVisualFamily.Alien;

                default:
                    return (byte)HectonBiomeVisualFamily.Abyssal;
            }
        }

        public static uint PackCompactInfluence(byte primaryBiomeId, byte secondaryBiomeId, byte blend255)
        {
            uint primaryVisualFamily = (uint)MapToVisualFamily(primaryBiomeId) & VisualFamilyMask;
            uint secondaryVisualFamily = secondaryBiomeId != 0
                ? (uint)MapToVisualFamily(secondaryBiomeId) & VisualFamilyMask
                : 0u;

            return primaryVisualFamily |
                   (secondaryVisualFamily << SecondaryVisualFamilyShift) |
                   (((uint)blend255 & BlendMask) << BlendShift);
        }
    }

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
