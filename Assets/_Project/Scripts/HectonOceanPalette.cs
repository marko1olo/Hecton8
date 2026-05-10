// ============================================================================
// HECTON-8 — HectonOceanPalette.cs
// ScriptableObject: kollektsiya biomnyh profiley podvodnoy sredy.
//
// Massiv profiley indeksirovan po MapMagic splat layer index.
// Element [0] = pervyy vyhod Biomes Set nody MapMagic.
//
// HectonUnderwaterVisuals poluchaet biomeIndex cherez MapMagicBiomeEvents.
// i vybiraet tselevoy profil iz etogo massiva.
//
// LORNYY PORYaDOK:
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
        [Header("═══ BIOME PROFILES ═══")]
        [Tooltip("Massiv biomnyh profiley.\n" +
                 "Indeks = MapMagic splat layer index.\n" +
                 "[0] = Shallow Grave, [1] = Golden Zone, i t.d.")]
        [SerializeField]
        private HectonBiomeProfile[] biomeProfiles;

        [Header("═══ SURFACE DEFAULTS ═══")]
        [Tooltip("Profil dlya poverhnosti (nad vodoy).\n" +
                 "Sbrasyvaet vse podvodnye effekty k dnevnym znacheniyam.")]
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
                    error = $"Biome profile at index [{i}] is null.";
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
