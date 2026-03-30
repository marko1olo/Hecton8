using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomePlayProfile", menuName = "Hecton/Environment/Biome Play Profile", order = 106)]
    public sealed class HectonBiomePlayProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.play.generic";
        public string profileLabel = "Generic Biome Play";

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
    }
}
