using Hecton.Localization;
using UnityEngine;
using Hecton8.World;
using Hecton8.Items;
using Hecton8.Tools;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeFamilyProfile", menuName = "Hecton/Environment/Biome Family Profile", order = 104)]
    public sealed class HectonBiomeFamilyProfile : ScriptableObject
    {
        private const string DefaultFamilyId = "biome.family.generic";
        private const string DefaultFamilyLabel = "Generic Biome Family";

        [Header("Identity")]
        public string familyId = DefaultFamilyId;
        public string familyLabel = DefaultFamilyLabel;

        [Header("Direction")]
        public Color debugColor = Color.white;
        [TextArea(2, 4)] public string geologicalIdentity = "Generic geology.";
        [TextArea(2, 4)] public string gameplayIdentity = "Generic biome gameplay role.";

        [Header("Mood")]
        public string atmosphereMood = "neutral";
        public string navigationStyle = "balanced";
        public string hazardStyle = "mixed";
        public string landmarkStyle = "subtle";

        [Header("Resources")]
        public string primaryResourceTheme = "general_minerals";
        public string secondaryResourceTheme = "general_salvage";
        public string suggestedZoneFamily = "resources.clutter.mid";
        public string progressionFeeling = "neutral";
        public ItemData primaryResource;
        public ItemData secondaryResource;
        public ItemData tertiaryResource;
        public ItemData signatureComponent;
        public HectonBiomeResourcePlanProfile resourcePlanProfile;
        public HectonBiomeResourceChannelProfile resourceChannelProfile;
        public HectonBiomeLandmarkPlanProfile landmarkPlanProfile;
        public HectonBiomeSpatialPatternProfile spatialPatternProfile;

        [Header("Atmosphere")]
        public AtmosphereProfile atmosphereProfile;

        [Header("Fauna")]
        public HectonFaunaFamilyProfile faunaFamilyProfile;

        [Header("Player Use")]
        public ToolLoadoutPreset recommendedLoadoutPreset;
        public HectonBiomePlayProfile playProfile;

        [Header("World Links")]
        public WorldPrefabFamilyProfile nearInteractiveFamily;
        public WorldPrefabFamilyProfile midVisualFamily;
        public WorldPrefabFamilyProfile farSilhouetteFamily;
        public WorldZonePlanProfile preferredZonePlan;

        private int _familyHashId;

        public int FamilyHashId => _familyHashId;
        public string RuntimeFamilyId => !string.IsNullOrWhiteSpace(familyId) ? familyId : DefaultFamilyId;
        public string RuntimeFamilyLabel => !string.IsNullOrWhiteSpace(familyLabel) ? familyLabel : DefaultFamilyLabel;
        public string RuntimeAtmosphereMood => RuntimeTextOrFallback(atmosphereMood, "neutral");
        public string RuntimeNavigationStyle => RuntimeTextOrFallback(navigationStyle, "balanced");
        public string RuntimeHazardStyle => RuntimeTextOrFallback(hazardStyle, "mixed");
        public string RuntimeLandmarkStyle => RuntimeTextOrFallback(landmarkStyle, "subtle");
        public string RuntimePrimaryResourceTheme => RuntimeTextOrFallback(primaryResourceTheme, "general_minerals");
        public string RuntimeSecondaryResourceTheme => RuntimeTextOrFallback(secondaryResourceTheme, "general_salvage");
        public string RuntimeSuggestedZoneFamily => RuntimeTextOrFallback(suggestedZoneFamily, "resources.clutter.mid");
        public string RuntimeProgressionFeeling => RuntimeTextOrFallback(progressionFeeling, "neutral");

        private void OnEnable()
        {
            RefreshRuntimeHashes();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            familyId = NormalizeAuthoringText(familyId, DefaultFamilyId);
            familyLabel = NormalizeAuthoringText(familyLabel, DefaultFamilyLabel);
            atmosphereMood = NormalizeAuthoringText(atmosphereMood, "neutral");
            navigationStyle = NormalizeAuthoringText(navigationStyle, "balanced");
            hazardStyle = NormalizeAuthoringText(hazardStyle, "mixed");
            landmarkStyle = NormalizeAuthoringText(landmarkStyle, "subtle");
            primaryResourceTheme = NormalizeAuthoringText(primaryResourceTheme, "general_minerals");
            secondaryResourceTheme = NormalizeAuthoringText(secondaryResourceTheme, "general_salvage");
            suggestedZoneFamily = NormalizeAuthoringText(suggestedZoneFamily, "resources.clutter.mid");
            progressionFeeling = NormalizeAuthoringText(progressionFeeling, "neutral");
            RefreshRuntimeHashes();
        }
#endif

        private void RefreshRuntimeHashes()
        {
            _familyHashId = LocHash.ComputeAsciiLowerInvariant(RuntimeFamilyId);
        }

        private static string RuntimeTextOrFallback(string value, string fallback)
        {
            return !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

#if UNITY_EDITOR
        private static string NormalizeAuthoringText(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim();
        }
#endif
    }
}
