using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldZonePlanProfile", menuName = "Hecton8/World/Zone Plan Profile")]
    public sealed class WorldZonePlanProfile : ScriptableObject
    {
        public enum SpatialRelation
        {
            AlongMainRoute,
            NearRouteAnchor,
            BehindCover,
            OffMainRoute,
            AtBranchPoint,
            AroundHeroObject,
            BehindHazardGate,
            AtRouteTerminus
        }

        [System.Serializable]
        public sealed class SlicePlan
        {
            public WorldPrefabFamilyProfile primaryFamily;
            public WorldPrefabFamilyProfile supportFamily;
            [Range(0, 64)] public int targetDensity = 1;
            [TextArea(1, 3)] public string usage = string.Empty;
        }

        [System.Serializable]
        public sealed class RolePlan
        {
            public WorldPrefabFamilyProfile family;
            public SpatialRelation relation = SpatialRelation.OffMainRoute;
            public WorldSliceAnchor.SliceState preferredSlice = WorldSliceAnchor.SliceState.Mid;
            [Range(0, 16)] public int targetCount = 1;
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

        [Header("Biome Spatial Roles")]
        public RolePlan resourcePocketPlan = new RolePlan();
        public RolePlan nodeClusterPlan = new RolePlan();
        public RolePlan safePocketPlan = new RolePlan();
        public RolePlan buildSocketPlan = new RolePlan();
        public RolePlan powerSpinePlan = new RolePlan();
        public RolePlan serviceChokePlan = new RolePlan();
        public RolePlan routeAnchorPlan = new RolePlan();
        public RolePlan hazardGatePlan = new RolePlan();
        public RolePlan rareObjectivePlan = new RolePlan();
    }
}
