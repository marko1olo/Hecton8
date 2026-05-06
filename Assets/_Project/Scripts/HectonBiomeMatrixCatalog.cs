using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MapToVisualFamily(int biomeId)
        {
            return (byte)ResolveVisualFamilyFast(biomeId);
        }

        public static VisualFamily ResolveVisualFamily(int biomeId)
        {
            return (uint)biomeId < (uint)HectonBiomeMatrixCatalog.VisualFamiliesByBiomeId.Length
                ? HectonBiomeMatrixCatalog.VisualFamiliesByBiomeId[biomeId]
                : VisualFamily.Abyssal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VisualFamily ResolveVisualFamilyFast(int biomeId)
        {
            switch (biomeId)
            {
                case 1:
                case 2:
                    return VisualFamily.Vegetation;

                case 4:
                case 8:
                case 13:
                case 14:
                case 24:
                case 28:
                case 30:
                case 32:
                case 36:
                    return VisualFamily.Sand;

                case 5:
                case 6:
                case 12:
                case 16:
                case 34:
                    return VisualFamily.Coral;

                case 3:
                case 10:
                case 41:
                case 42:
                case 77:
                case 82:
                case 84:
                case 98:
                case 106:
                    return VisualFamily.Volcanic;

                case 57:
                case 59:
                case 80:
                case 81:
                case 105:
                case 108:
                    return VisualFamily.Ruin;

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
                    return VisualFamily.Void;
            }

            if ((biomeId >= 44 && biomeId <= 56) ||
                (biomeId >= 61 && biomeId <= 68) ||
                biomeId == 37 ||
                biomeId == 39 ||
                biomeId == 58 ||
                biomeId == 78)
            {
                return VisualFamily.Abyssal;
            }

            if ((biomeId >= 69 && biomeId <= 76) ||
                (biomeId >= 85 && biomeId <= 88) ||
                biomeId == 7 ||
                biomeId == 9 ||
                biomeId == 11 ||
                (biomeId >= 15 && biomeId <= 23) ||
                biomeId == 25 ||
                biomeId == 26 ||
                biomeId == 27 ||
                biomeId == 29 ||
                biomeId == 31 ||
                biomeId == 35 ||
                biomeId == 38 ||
                biomeId == 43 ||
                biomeId == 60 ||
                biomeId == 83 ||
                biomeId == 97)
            {
                return VisualFamily.Rock;
            }

            return VisualFamily.Abyssal;
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
