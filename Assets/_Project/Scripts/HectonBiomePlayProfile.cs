using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomePlayProfile", menuName = "Hecton/Environment/Biome Play Profile", order = 106)]
    public sealed class HectonBiomePlayProfile : ScriptableObject
    {
        private const int MinBias = 1;
        private const int MaxBias = 5;
        private const string DefaultProfileId = "biome.play.generic";
        private const string DefaultProfileLabel = "Generic Biome Play";

        [Header("Identity")]
        public string profileId = DefaultProfileId;
        public string profileLabel = DefaultProfileLabel;

        [Header("Player Promise")]
        [TextArea(2, 4)] public string whyPlayerComesHere = "General exploration and gathering.";
        [TextArea(2, 4)] public string playerPromise = "A balanced biome with readable routes and mixed rewards.";
        [TextArea(2, 4)] public string traversalRhythm = "Short survey loops with moderate pauses.";

        [Header("Readability")]
        [Range(1, 5)] public int routeClarity = 3;
        [Range(1, 5)] public int landmarkStrength = 3;
        [Range(1, 5)] public int safePocketFrequency = 3;

        [Header("Reward")]
        [Range(1, 5)] public int commonResourceDensity = 3;
        [Range(1, 5)] public int salvageValue = 2;
        [Range(1, 5)] public int rareRewardPull = 2;

        [Header("Pressure")]
        [Range(1, 5)] public int encounterPressure = 3;
        [Range(1, 5)] public int hazardPressure = 3;
        [Range(1, 5)] public int expeditionCommitment = 3;

        [Header("Notes")]
        [TextArea(2, 4)] public string cautionSummary = "Mixed pressure. Respect depth and keep a retreat route.";

        public string RuntimeProfileId => !string.IsNullOrWhiteSpace(profileId) ? profileId : DefaultProfileId;
        public string RuntimeProfileLabel => !string.IsNullOrWhiteSpace(profileLabel) ? profileLabel : DefaultProfileLabel;
        public string RuntimeWhyPlayerComesHere => RuntimeTextOrFallback(whyPlayerComesHere, "General exploration and gathering.");
        public string RuntimePlayerPromise => RuntimeTextOrFallback(playerPromise, "A balanced biome with readable routes and mixed rewards.");
        public string RuntimeTraversalRhythm => RuntimeTextOrFallback(traversalRhythm, "Short survey loops with moderate pauses.");
        public string RuntimeCautionSummary => RuntimeTextOrFallback(cautionSummary, "Mixed pressure. Respect depth and keep a retreat route.");
        public int RuntimeRouteClarity => ClampBias(routeClarity);
        public int RuntimeLandmarkStrength => ClampBias(landmarkStrength);
        public int RuntimeSafePocketFrequency => ClampBias(safePocketFrequency);
        public int RuntimeCommonResourceDensity => ClampBias(commonResourceDensity);
        public int RuntimeSalvageValue => ClampBias(salvageValue);
        public int RuntimeRareRewardPull => ClampBias(rareRewardPull);
        public int RuntimeEncounterPressure => ClampBias(encounterPressure);
        public int RuntimeHazardPressure => ClampBias(hazardPressure);
        public int RuntimeExpeditionCommitment => ClampBias(expeditionCommitment);

#if UNITY_EDITOR
        private void OnValidate()
        {
            profileId = NormalizeAuthoringText(profileId, DefaultProfileId);
            profileLabel = NormalizeAuthoringText(profileLabel, DefaultProfileLabel);
            whyPlayerComesHere = NormalizeAuthoringText(whyPlayerComesHere, RuntimeWhyPlayerComesHere);
            playerPromise = NormalizeAuthoringText(playerPromise, RuntimePlayerPromise);
            traversalRhythm = NormalizeAuthoringText(traversalRhythm, RuntimeTraversalRhythm);
            cautionSummary = NormalizeAuthoringText(cautionSummary, RuntimeCautionSummary);
            routeClarity = RuntimeRouteClarity;
            landmarkStrength = RuntimeLandmarkStrength;
            safePocketFrequency = RuntimeSafePocketFrequency;
            commonResourceDensity = RuntimeCommonResourceDensity;
            salvageValue = RuntimeSalvageValue;
            rareRewardPull = RuntimeRareRewardPull;
            encounterPressure = RuntimeEncounterPressure;
            hazardPressure = RuntimeHazardPressure;
            expeditionCommitment = RuntimeExpeditionCommitment;
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
