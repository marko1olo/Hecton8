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
    }

    [CreateAssetMenu(fileName = "BiomeLandmarkPlanProfile", menuName = "Hecton/Environment/Biome Landmark Plan Profile", order = 108)]
    public sealed class HectonBiomeLandmarkPlanProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.landmark_plan.generic";
        public string profileLabel = "Generic Landmark Plan";

        [Header("Readability")]
        public string dominantLandmarkRole = "General terrain marker";
        public string nearReferenceShape = "Small readable form";
        public string midReferenceShape = "Medium route anchor";
        public string farReferenceShape = "Large silhouette anchor";

        [Header("Player Guidance")]
        [TextArea(2, 4)] public string routeUse = "Used to keep route memory stable.";
        [TextArea(2, 4)] public string safePocketUse = "Used to hint where brief relief can exist.";
        [TextArea(2, 4)] public string emotionalRead = "Should read as a generic memorable place.";
    }

    [CreateAssetMenu(fileName = "BiomeSpatialPatternProfile", menuName = "Hecton/Environment/Biome Spatial Pattern Profile", order = 109)]
    public sealed class HectonBiomeSpatialPatternProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.spatial_pattern.generic";
        public string profileLabel = "Generic Spatial Pattern";

        [Header("Content Pattern")]
        [TextArea(2, 4)] public string resourcePocketPattern = "Loose resources sit in readable nearby pockets.";
        [TextArea(2, 4)] public string nodeClusterPattern = "Nodes appear in small readable clusters.";
        [TextArea(2, 4)] public string safePocketPattern = "Short recovery pockets exist behind obvious cover.";
        [TextArea(2, 4)] public string routeAnchorPattern = "Routes chain from one readable anchor to another.";
        [TextArea(2, 4)] public string rareObjectivePattern = "Rare value sits one layer deeper than routine value.";

        [Header("Rhythm")]
        [TextArea(2, 4)] public string explorationLoop = "Read landmark, clear nearby pocket, commit deeper, then return.";
        [TextArea(2, 4)] public string spatialRead = "The biome should read clearly at near, mid, and far distance.";
        [TextArea(2, 4)] public string playerMemoryHook = "The player remembers this place by one repeated structural pattern.";
    }
}
