using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldZonePlanProfile", menuName = "Hecton8/World/Zone Plan Profile")]
    public sealed class WorldZonePlanProfile : ScriptableObject
    {
        [System.Serializable]
        public sealed class SlicePlan
        {
            public WorldPrefabFamilyProfile primaryFamily;
            public WorldPrefabFamilyProfile supportFamily;
            [Range(0, 64)] public int targetDensity = 1;
            [TextArea(1, 3)] public string usage = string.Empty;
        }

        [Header("Identity")]
        public string planId = "zone.plan.generic";
        public string planLabel = "Generic Zone Plan";

        [Header("Near")]
        public SlicePlan nearPlan = new SlicePlan();

        [Header("Mid")]
        public SlicePlan midPlan = new SlicePlan();

        [Header("Far")]
        public SlicePlan farPlan = new SlicePlan();

        [Header("Hero")]
        public WorldPrefabFamilyProfile heroFamily;
        [TextArea(2, 4)] public string gameplaySummary = "Generic zone plan.";
    }
}
