using UnityEngine;

namespace Hecton8.Environment
{
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
}
