using UnityEngine;

namespace Hecton8.Environment
{
    public enum VisualFamily : byte
    {
        Sand = 0,
        Rock = 1,
        Vegetation = 2,
        Coral = 3,
        Ruin = 4,
        Volcanic = 5,
        Abyssal = 6,
        Void = 7
    }

    public static class HectonBiomeVisualFamilyUtility
    {
        public const int VisualFamilyCount = 8;
        public const int PrimaryVisualFamilyShift = 0;
        public const int SecondaryVisualFamilyShift = 3;
        public const int BlendShift = 6;
        public const int FlagsShift = 14;
        public const uint VisualFamilyMask = 0x7u;
        public const uint BlendMask = 0xFFu;
        public const uint GpuPackedMask = (1u << FlagsShift) - 1u;

        public static byte MapToVisualFamily(int biomeId)
        {
            return (byte)ResolveVisualFamily(biomeId);
        }

        public static VisualFamily ResolveVisualFamily(int biomeId)
        {
            return (uint)biomeId < (uint)HectonBiomeMatrixCatalog.VisualFamiliesByBiomeId.Length
                ? HectonBiomeMatrixCatalog.VisualFamiliesByBiomeId[biomeId]
                : VisualFamily.Abyssal;
        }

        public static uint PackCompactInfluence(byte primaryVisualFamilyId, byte secondaryVisualFamilyId, byte blend255)
        {
            uint primaryVisualFamily = (uint)primaryVisualFamilyId & VisualFamilyMask;
            uint secondaryVisualFamily = blend255 != 0
                ? (uint)secondaryVisualFamilyId & VisualFamilyMask
                : 0u;

            return primaryVisualFamily |
                   (secondaryVisualFamily << SecondaryVisualFamilyShift) |
                   (((uint)blend255 & BlendMask) << BlendShift);
        }

        public static uint PackCompactInfluenceFromBiomeIds(byte primaryBiomeId, byte secondaryBiomeId, byte blend255)
        {
            byte primaryVisualFamilyId = MapToVisualFamily(primaryBiomeId);
            byte secondaryVisualFamilyId = blend255 != 0 ? MapToVisualFamily(secondaryBiomeId) : (byte)0;
            return PackCompactInfluence(primaryVisualFamilyId, secondaryVisualFamilyId, blend255);
        }

        public static uint PackCell(byte primaryVisualFamilyId, byte secondaryVisualFamilyId, byte blend255, byte flags)
        {
            return PackCompactInfluence(primaryVisualFamilyId, secondaryVisualFamilyId, blend255) |
                   ((uint)flags << FlagsShift);
        }

        public static uint PackCellFromBiomeIds(byte primaryBiomeId, byte secondaryBiomeId, byte blend255, byte flags)
        {
            return PackCompactInfluenceFromBiomeIds(primaryBiomeId, secondaryBiomeId, blend255) |
                   ((uint)flags << FlagsShift);
        }

        public static byte ExtractPrimaryVisualFamilyId(uint packed)
        {
            return (byte)((packed >> PrimaryVisualFamilyShift) & VisualFamilyMask);
        }

        public static byte ExtractSecondaryVisualFamilyId(uint packed)
        {
            return (byte)((packed >> SecondaryVisualFamilyShift) & VisualFamilyMask);
        }

        public static byte ExtractBlend255(uint packed)
        {
            return (byte)((packed >> BlendShift) & BlendMask);
        }

        public static byte ExtractFlags(uint packed)
        {
            return (byte)(packed >> FlagsShift);
        }

        public static uint ExtractGpuPacked(uint packed)
        {
            return packed & GpuPackedMask;
        }
    }

    [CreateAssetMenu(fileName = "BiomeMatrixCatalog", menuName = "Hecton/Environment/Biome Matrix Catalog", order = 103)]
    public sealed class HectonBiomeMatrixCatalog : ScriptableObject
    {
        public static readonly VisualFamily[] VisualFamiliesByBiomeId =
        {
            VisualFamily.Void,
            VisualFamily.Vegetation, VisualFamily.Vegetation, VisualFamily.Volcanic, VisualFamily.Sand,
            VisualFamily.Coral, VisualFamily.Coral, VisualFamily.Rock, VisualFamily.Sand,
            VisualFamily.Rock, VisualFamily.Volcanic, VisualFamily.Rock, VisualFamily.Coral,
            VisualFamily.Sand, VisualFamily.Sand, VisualFamily.Rock, VisualFamily.Coral,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Sand,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Sand,
            VisualFamily.Rock, VisualFamily.Sand, VisualFamily.Rock, VisualFamily.Sand,
            VisualFamily.Void, VisualFamily.Coral, VisualFamily.Rock, VisualFamily.Sand,
            VisualFamily.Abyssal, VisualFamily.Rock, VisualFamily.Abyssal, VisualFamily.Void,
            VisualFamily.Volcanic, VisualFamily.Volcanic, VisualFamily.Rock, VisualFamily.Abyssal,
            VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal,
            VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal,
            VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal,
            VisualFamily.Ruin, VisualFamily.Abyssal, VisualFamily.Ruin, VisualFamily.Rock,
            VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal,
            VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal, VisualFamily.Abyssal,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock,
            VisualFamily.Volcanic, VisualFamily.Abyssal, VisualFamily.Void, VisualFamily.Ruin,
            VisualFamily.Ruin, VisualFamily.Volcanic, VisualFamily.Rock, VisualFamily.Volcanic,
            VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock, VisualFamily.Rock,
            VisualFamily.Void, VisualFamily.Void, VisualFamily.Void, VisualFamily.Void,
            VisualFamily.Void, VisualFamily.Void, VisualFamily.Void, VisualFamily.Void,
            VisualFamily.Rock, VisualFamily.Volcanic, VisualFamily.Void, VisualFamily.Void,
            VisualFamily.Void, VisualFamily.Void, VisualFamily.Void, VisualFamily.Void,
            VisualFamily.Ruin, VisualFamily.Volcanic, VisualFamily.Void, VisualFamily.Ruin
        };

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
