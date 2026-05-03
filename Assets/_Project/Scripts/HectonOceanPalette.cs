// ============================================================================
// HECTON-8 — HectonOceanPalette.cs
// ScriptableObject: коллекция биомных профилей подводной среды.
//
// Массив профилей индексирован по MapMagic splat layer index.
// Элемент [0] = первый выход Biomes Set ноды MapMagic.
//
// HectonUnderwaterVisuals получает biomeIndex через MapMagicBiomeEvents.
// и выбирает целевой профиль из этого массива.
//
// ЛОРНЫЙ ПОРЯДОК:
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
        [Tooltip("Массив биомных профилей.\n" +
                 "Индекс = MapMagic splat layer index.\n" +
                 "[0] = Shallow Grave, [1] = Golden Zone, и т.д.")]
        [SerializeField]
        private HectonBiomeProfile[] biomeProfiles;

        [Header("═══ SURFACE DEFAULTS ═══")]
        [Tooltip("Профиль для поверхности (над водой).\n" +
                 "Сбрасывает все подводные эффекты к дневным значениям.")]
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
