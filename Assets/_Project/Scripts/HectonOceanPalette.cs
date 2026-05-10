// ============================================================================
// HECTON-8 - HectonOceanPalette.cs
// ScriptableObject containing underwater biome visual profiles.
//
// Profile array index matches the MapMagic splat layer index.
// Element [0] maps to the first Biomes Set output.
//
// HectonUnderwaterVisuals receives biomeIndex through MapMagicBiomeEvents and
// selects the target profile from this array.
//
// Lore order:
//   [0] = Shallow Grave     [3] = The Drop
//   [1] = Golden Zone       [4] = Abyssal Plain
//   [2] = Industrial Shelf  [5] = The Wound
// ============================================================================

using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(
        fileName = "NewOceanPalette",
        menuName = "Hecton/Environment/Ocean Palette",
        order = 101)]
    public sealed class HectonOceanPalette : ScriptableObject
    {
        [Header("Biome Profiles")]
        [Tooltip("Underwater biome profiles.\n" +
                 "Index = MapMagic splat layer index.\n" +
                 "[0] = Shallow Grave, [1] = Golden Zone, etc.")]
        [SerializeField]
        private HectonBiomeProfile[] biomeProfiles;

        [Header("Surface Defaults")]
        [Tooltip("Above-water fallback profile.\n" +
                 "Resets underwater effects to daylight values.")]
        [SerializeField]
        private HectonBiomeProfile surfaceProfile;

        /// <summary>Total number of biome profiles.</summary>
        public int Count => biomeProfiles != null ? biomeProfiles.Length : 0;

        /// <summary>
        /// Returns biome profile by MapMagic splat layer index.
        /// Bounds-safe: clamps to valid range. Zero GC.
        /// </summary>
        public HectonBiomeProfile GetProfile(int biomeIndex)
        {
            if (biomeProfiles == null || biomeProfiles.Length == 0)
                return surfaceProfile;

            int clampedIndex = Mathf.Clamp(biomeIndex, 0, biomeProfiles.Length - 1);
            HectonBiomeProfile profile = biomeProfiles[clampedIndex];
            return profile != null ? profile : surfaceProfile;
        }

        /// <summary>Above-water profile.</summary>
        public HectonBiomeProfile SurfaceProfile => surfaceProfile;

        /// <summary>Editor validation.</summary>
        public bool Validate(out string error)
        {
            if (biomeProfiles == null || biomeProfiles.Length == 0)
            {
                error = "Biome profiles array is empty.";
                return false;
            }

            for (int i = 0; i < biomeProfiles.Length; i++)
            {
                if (biomeProfiles[i] == null)
                {
                    error = "Biome profile slot is null.";
                    return false;
                }
            }

            if (surfaceProfile == null)
            {
                error = "Surface profile is not assigned.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
