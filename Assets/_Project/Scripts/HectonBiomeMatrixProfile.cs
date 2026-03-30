using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeMatrixProfile", menuName = "Hecton/Environment/Biome Matrix Profile", order = 102)]
    public sealed class HectonBiomeMatrixProfile : ScriptableObject
    {
        public enum CardinalRegion
        {
            North,
            South,
            East,
            West
        }

        [Header("Identity")]
        public int matrixIndex = 1;
        public int depthTier = 1;
        public CardinalRegion region = CardinalRegion.North;
        public string biomeName = "Unnamed Matrix Biome";

        [Header("Depth")]
        public float minDepthMeters = 0f;
        public float maxDepthMeters = 100f;

        [Header("Lore")]
        [TextArea(2, 5)] public string shortDescription = string.Empty;
        public bool isPlaceholder;

        [Header("Player Framing")]
        [TextArea(2, 4)] public string visitPurpose = string.Empty;
        [TextArea(2, 4)] public string commonRewardHook = string.Empty;
        [TextArea(2, 4)] public string rareRewardHook = string.Empty;
        [TextArea(2, 4)] public string landmarkIdentity = string.Empty;
        [TextArea(2, 4)] public string safePocketIdentity = string.Empty;
        [TextArea(2, 4)] public string riskSummary = string.Empty;
        [TextArea(2, 4)] public string extractionFocus = string.Empty;
        [TextArea(2, 4)] public string landmarkGuidance = string.Empty;
        [Range(1, 5)] public int loosePickupBias = 3;
        [Range(1, 5)] public int nodeExtractionBias = 3;
        [Range(1, 5)] public int salvageBias = 2;
        [Range(1, 5)] public int commonResourceBias = 3;
        [Range(1, 5)] public int uncommonResourceBias = 3;
        [Range(1, 5)] public int rareResourceBias = 3;
        [Range(1, 5)] public int routePressure = 3;
        [Range(1, 5)] public int landmarkStrength = 3;
        [Range(1, 5)] public int rewardPull = 3;
        [Range(1, 5)] public int survivalPressure = 3;

        [Header("Future Integration")]
        public HectonBiomeProfile runtimeVisualProfile;
        public string familyId = string.Empty;
        public HectonBiomeFamilyProfile familyProfile;
        public string suggestedZoneFamily = string.Empty;
        public string progressionRole = string.Empty;
    }
}
