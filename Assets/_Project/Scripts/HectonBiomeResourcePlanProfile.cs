using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeResourcePlanProfile", menuName = "Hecton/Environment/Biome Resource Plan Profile", order = 107)]
    public sealed class HectonBiomeResourcePlanProfile : ScriptableObject
    {
        private const int MinBias = 1;
        private const int MaxBias = 5;
        private const string DefaultProfileId = "biome.resource_plan.generic";
        private const string DefaultProfileLabel = "Generic Resource Plan";

        [Header("Identity")]
        public string profileId = DefaultProfileId;
        public string profileLabel = DefaultProfileLabel;

        [Header("Resource Flow")]
        public ItemData commonResource;
        public ItemData uncommonResource;
        public ItemData rareResource;
        public ItemData signatureComponent;

        [Header("Weighting")]
        [Range(1, 5)] public int loosePickupWeight = 3;
        [Range(1, 5)] public int nodeExtractionWeight = 3;
        [Range(1, 5)] public int salvageRecoveryWeight = 2;
        [Range(1, 5)] public int commonResourcePull = 3;
        [Range(1, 5)] public int uncommonResourcePull = 3;
        [Range(1, 5)] public int rareResourcePull = 3;

        [Header("Player Pull")]
        [TextArea(2, 4)] public string earlyReasonToFarm = "General field farming.";
        [TextArea(2, 4)] public string lateReasonToReturn = "General late return value.";
        [TextArea(2, 4)] public string extractionStyle = "Mixed salvage and node extraction.";
        [TextArea(2, 4)] public string routeRewardLogic = "Short loops between landmarks and resource pockets.";

        public string RuntimeProfileId => !string.IsNullOrWhiteSpace(profileId) ? profileId : DefaultProfileId;
        public string RuntimeProfileLabel => !string.IsNullOrWhiteSpace(profileLabel) ? profileLabel : DefaultProfileLabel;
        public string RuntimeEarlyReasonToFarm => RuntimeTextOrFallback(earlyReasonToFarm, "General field farming.");
        public string RuntimeLateReasonToReturn => RuntimeTextOrFallback(lateReasonToReturn, "General late return value.");
        public string RuntimeExtractionStyle => RuntimeTextOrFallback(extractionStyle, "Mixed salvage and node extraction.");
        public string RuntimeRouteRewardLogic => RuntimeTextOrFallback(routeRewardLogic, "Short loops between landmarks and resource pockets.");
        public int RuntimeLoosePickupWeight => ClampBias(loosePickupWeight);
        public int RuntimeNodeExtractionWeight => ClampBias(nodeExtractionWeight);
        public int RuntimeSalvageRecoveryWeight => ClampBias(salvageRecoveryWeight);
        public int RuntimeCommonResourcePull => ClampBias(commonResourcePull);
        public int RuntimeUncommonResourcePull => ClampBias(uncommonResourcePull);
        public int RuntimeRareResourcePull => ClampBias(rareResourcePull);

#if UNITY_EDITOR
        private void OnValidate()
        {
            profileId = NormalizeAuthoringText(profileId, DefaultProfileId);
            profileLabel = NormalizeAuthoringText(profileLabel, DefaultProfileLabel);
            earlyReasonToFarm = NormalizeAuthoringText(earlyReasonToFarm, RuntimeEarlyReasonToFarm);
            lateReasonToReturn = NormalizeAuthoringText(lateReasonToReturn, RuntimeLateReasonToReturn);
            extractionStyle = NormalizeAuthoringText(extractionStyle, RuntimeExtractionStyle);
            routeRewardLogic = NormalizeAuthoringText(routeRewardLogic, RuntimeRouteRewardLogic);
            loosePickupWeight = RuntimeLoosePickupWeight;
            nodeExtractionWeight = RuntimeNodeExtractionWeight;
            salvageRecoveryWeight = RuntimeSalvageRecoveryWeight;
            commonResourcePull = RuntimeCommonResourcePull;
            uncommonResourcePull = RuntimeUncommonResourcePull;
            rareResourcePull = RuntimeRareResourcePull;
        }
#endif

        private static int ClampBias(int value)
        {
            return Mathf.Clamp(value, MinBias, MaxBias);
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
