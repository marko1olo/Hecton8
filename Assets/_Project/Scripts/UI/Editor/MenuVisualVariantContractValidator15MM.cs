#if UNITY_EDITOR
using System;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    internal static class MenuVisualVariantContractValidator15MM
    {
        private const int ExpectedStyleCount = 15;
        private const int ExpectedConceptCount = 12;
        private const int ExpectedVariantCount = ExpectedStyleCount * ExpectedConceptCount;

        [MenuItem("Hecton8/15MM/Validate Menu Visual Variants")]
        private static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("15MM menu visual variants validated: 15 styles, 12 concepts, 180 combinations.");
        }

        internal static void ValidateOrThrow()
        {
            int failures = 0;
            failures += ValidateCatalogCounts();
            failures += ValidateStyleCatalog();
            failures += ValidateConceptCatalog();
            failures += ValidateVariantGrid();

            if (failures != 0)
                throw new InvalidOperationException("15MM menu visual variant contract failed.");
        }

        private static int ValidateCatalogCounts()
        {
            int failures = 0;
            int styleCount = MenuVisualStyleCatalog.StyleCount;
            int conceptCount = MenuVisualConceptCatalog.ConceptCount;

            if (styleCount != ExpectedStyleCount)
                failures++;

            if (conceptCount != ExpectedConceptCount)
                failures++;

            int variantCount = styleCount * conceptCount;
            if (variantCount != ExpectedVariantCount)
                failures++;

            return failures;
        }

        private static int ValidateStyleCatalog()
        {
            int failures = 0;

            for (int index = 0; index < ExpectedStyleCount; index++)
            {
                MenuVisualStyle style = MenuVisualStyleCatalog.FromIndex(index);
                if (MenuVisualStyleCatalog.ToIndex(style) != index)
                    failures++;

                ReadOnlySpan<char> name = MenuVisualStyleCatalog.GetDisplayName(style);
                if (name.Length == 0)
                    failures++;

                failures += ValidateStyleAtQuality(style, 0f);
                failures += ValidateStyleAtQuality(style, 0.5f);
                failures += ValidateStyleAtQuality(style, 1f);
            }

            if (!MenuVisualStyleCatalog.IsValidStyleIndex(0))
                failures++;

            if (!MenuVisualStyleCatalog.IsValidStyleIndex(ExpectedStyleCount - 1))
                failures++;

            if (MenuVisualStyleCatalog.IsValidStyleIndex(ExpectedStyleCount))
                failures++;

            return failures;
        }

        private static int ValidateConceptCatalog()
        {
            int failures = 0;

            for (int index = 0; index < ExpectedConceptCount; index++)
            {
                MenuVisualConcept concept = MenuVisualConceptCatalog.FromIndex(index);
                if (MenuVisualConceptCatalog.ToIndex(concept) != index)
                    failures++;

                ReadOnlySpan<char> name = MenuVisualConceptCatalog.GetDisplayName(concept);
                if (name.Length == 0)
                    failures++;

                failures += ValidateConceptAtQuality(concept, 0f);
                failures += ValidateConceptAtQuality(concept, 0.5f);
                failures += ValidateConceptAtQuality(concept, 1f);
            }

            if (!MenuVisualConceptCatalog.IsValidConceptIndex(0))
                failures++;

            if (!MenuVisualConceptCatalog.IsValidConceptIndex(ExpectedConceptCount - 1))
                failures++;

            if (MenuVisualConceptCatalog.IsValidConceptIndex(ExpectedConceptCount))
                failures++;

            return failures;
        }

        private static int ValidateVariantGrid()
        {
            int failures = 0;

            for (int styleIndex = 0; styleIndex < ExpectedStyleCount; styleIndex++)
            {
                MenuVisualStyle style = MenuVisualStyleCatalog.FromIndex(styleIndex);
                for (int conceptIndex = 0; conceptIndex < ExpectedConceptCount; conceptIndex++)
                {
                    MenuVisualConcept concept = MenuVisualConceptCatalog.FromIndex(conceptIndex);
                    failures += ValidateStyleAtQuality(style, 0.25f);
                    failures += ValidateConceptAtQuality(concept, 0.75f);
                }
            }

            return failures;
        }

        private static int ValidateStyleAtQuality(MenuVisualStyle style, float quality)
        {
            MenuVisualStyleCatalog.Resolve(style, quality, out MenuVisualStyleState state);

            int failures = 0;
            failures += IsFinite(state.BackgroundColor) ? 0 : 1;
            failures += IsFinite(state.PanelColor) ? 0 : 1;
            failures += IsFinite(state.ButtonColor) ? 0 : 1;
            failures += IsFinite(state.ButtonHoverColor) ? 0 : 1;
            failures += IsFinite(state.PrimaryTextColor) ? 0 : 1;
            failures += IsFinite(state.SecondaryTextColor) ? 0 : 1;
            failures += IsFinite(state.AccentColor) ? 0 : 1;
            failures += IsFinite(state.WarningColor) ? 0 : 1;
            failures += IsFiniteWeight(state.TextGlowWeight) ? 0 : 1;
            failures += IsFiniteWeight(state.InterferenceWeight) ? 0 : 1;
            failures += IsFiniteWeight(state.ScanlineWeight) ? 0 : 1;
            failures += IsFiniteWeight(state.WetGlassWeight) ? 0 : 1;

            return failures;
        }

        private static int ValidateConceptAtQuality(MenuVisualConcept concept, float quality)
        {
            MenuVisualConceptCatalog.Resolve(concept, quality, out MenuVisualConceptState state);

            int failures = 0;
            failures += IsFinite(state.ShellOffset) ? 0 : 1;
            failures += IsFinite(state.HeaderOffset) ? 0 : 1;
            failures += IsFinite(state.ContentOffset) ? 0 : 1;
            failures += IsFinite(state.PanelOffset) ? 0 : 1;
            failures += IsFiniteScale(state.ShellScale) ? 0 : 1;
            failures += IsFiniteScale(state.HeaderScale) ? 0 : 1;
            failures += IsFiniteScale(state.PanelScale) ? 0 : 1;
            failures += IsFiniteAngle(state.ShellRotation) ? 0 : 1;
            failures += IsFiniteAngle(state.HeaderRotation) ? 0 : 1;
            failures += IsFiniteAngle(state.PanelRotation) ? 0 : 1;
            failures += IsFiniteWeight(state.PanelSpread) ? 0 : 1;
            failures += IsFiniteWeight(state.PanelStack) ? 0 : 1;
            failures += IsFiniteWeight(state.MicroMotion) ? 0 : 1;
            failures += IsFiniteWeight(state.WarningBias) ? 0 : 1;

            return failures;
        }

        private static bool IsFinite(Color color)
        {
            return math.isfinite(color.r) &&
                   math.isfinite(color.g) &&
                   math.isfinite(color.b) &&
                   math.isfinite(color.a);
        }

        private static bool IsFinite(Vector2 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y);
        }

        private static bool IsFiniteScale(float value)
        {
            return math.isfinite(value) && value > 0.1f && value < 4f;
        }

        private static bool IsFiniteAngle(float value)
        {
            return math.isfinite(value) && math.abs(value) < 360f;
        }

        private static bool IsFiniteWeight(float value)
        {
            return math.isfinite(value) && value >= 0f && value < 1024f;
        }
    }
}
#endif
