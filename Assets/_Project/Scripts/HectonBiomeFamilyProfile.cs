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
        [Header("Identity")]
        public string familyId = "biome.family.generic";
        public string familyLabel = "Generic Biome Family";

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

        private void OnEnable()
        {
            RefreshRuntimeHashes();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshRuntimeHashes();
        }
#endif

        private void RefreshRuntimeHashes()
        {
            _familyHashId = LocHash.ComputeAsciiLowerInvariant(familyId);
        }
    }
}
