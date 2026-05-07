using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.World;
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
        public HectonMusicBiomeProfile musicBiomeProfile;
        public string familyId = string.Empty;
        public HectonBiomeFamilyProfile familyProfile;
        public string suggestedZoneFamily = string.Empty;
        public string progressionRole = string.Empty;

        [Header("Biome Flow Override")]
        public bool hasAmbientFlowOverride;
        public Vector3 ambientFlowOverride = Vector3.zero;
        [Range(0f, 1f)] public float ambientFlowOverrideWeight = 1f;

        [Header("Gravity / Buoyancy")]
        [Tooltip("Multiplier applied to upward ForceMode.Acceleration buoyancy while this matrix biome is the active medium.")]
        [Range(0.05f, 3f)] public float gravityMultiplier = 1f;

        [Tooltip("Multiplier applied to upward buoyancy force while this matrix biome is the active volumetric medium.")]
        [Range(0.05f, 3f)] public float buoyancyMultiplier = 1f;

        public float GravityMultiplier => Mathf.Clamp(gravityMultiplier * buoyancyMultiplier, 0.05f, 3f);
        public float BuoyancyMultiplier => Mathf.Clamp(buoyancyMultiplier, 0.05f, 3f);

        [Header("Transition VFX")]
        public bool emitsSeismicDustOnEntry;
        [Range(0.1f, 1f)] public float seismicDustRadiusScale = 0.35f;
        [Range(0f, 2f)] public float seismicDustSeafloorOffsetMeters = 0.18f;

        [Header("Procedural Memory")]
        public WorldProceduralClusterFocus primaryClusterFocus = WorldProceduralClusterFocus.None;
        public WorldProceduralClusterFocus secondaryClusterFocus = WorldProceduralClusterFocus.None;
        public WorldProceduralStructureFocus primaryStructureFocus = WorldProceduralStructureFocus.None;
        public WorldProceduralStructureFocus secondaryStructureFocus = WorldProceduralStructureFocus.None;
        public WorldProceduralFaunaMood faunaMood = WorldProceduralFaunaMood.None;

        [Header("Preferred Content Categories")]
        public WorldPrefabFamilyProfile[] preferredGroundFamilies;
        public WorldPrefabFamilyProfile[] preferredClusterFamilies;
        public WorldPrefabFamilyProfile[] preferredStructureFamilies;
        public WorldPrefabFamilyProfile[] preferredSpawnFamilies;

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
