using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeResourcePlanProfile", menuName = "Hecton/Environment/Biome Resource Plan Profile", order = 107)]
    public sealed class HectonBiomeResourcePlanProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.resource_plan.generic";
        public string profileLabel = "Generic Resource Plan";

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
    }
}
