using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldPrefabFamilyProfile", menuName = "Hecton8/World/Prefab Family Profile")]
    public sealed class WorldPrefabFamilyProfile : ScriptableObject
    {
        public enum ProceduralDomain
        {
            Generic,
            Rock,
            RockCluster,
            RockArch,
            RockShelf,
            Kelp,
            Plant,
            Coral,
            Egg,
            Debris,
            RuinModule,
            CaveEntrance,
            Landmark,
            CreatureSpawn,
            ResourcePocket,
            HazardPocket,
            SafePocket,
            PowerRoute,
            ServiceScar
        }

        public enum PlacementMode
        {
            Scatter,
            Cluster,
            Patch,
            Solitary,
            Landmark,
            SpawnAnchor,
            SocketDriven
        }

        public enum BudgetClass
        {
            Light,
            Medium,
            Heavy
        }

        public enum ScatterLayer
        {
            Ground,
            Cluster,
            Structure,
            Spawn
        }

        public enum StructureAccentRole
        {
            None,
            NaturalLandmark,
            TechFragment,
            CaveRead,
            BiologicalSilhouette
        }

        public enum ClusterAccentRole
        {
            None,
            FertileGrowth,
            BiologicalNest,
            ResourcePocket,
            ShelterPocket,
            HazardPocket,
            DebrisField,
            RockCover
        }

        [System.Serializable]
        public sealed class VariantEntry
        {
            public string variantId = "variant.generic";
            public GameObject prefab;
            [Min(1)] public int weight = 1;
            public bool proxyOnly = true;
            public bool finalReady;
            public Vector2 uniformScaleRange = Vector2.one;
        }

        [Header("Identity")]
        public string familyId = "world.family.generic";
        public string familyLabel = "Generic World Family";

        [Header("Usage")]
        public WorldSliceAnchor.SliceState defaultFidelity = WorldSliceAnchor.SliceState.Mid;
        public BudgetClass budgetClass = BudgetClass.Medium;
        public bool expectsCollision;
        public bool expectsInteraction;

        [Header("Procedural Placement")]
        public ProceduralDomain proceduralDomain = ProceduralDomain.Generic;
        public ScatterLayer scatterLayer = ScatterLayer.Ground;
        public StructureAccentRole structureAccentRole = StructureAccentRole.None;
        public ClusterAccentRole clusterAccentRole = ClusterAccentRole.None;
        public PlacementMode placementMode = PlacementMode.Scatter;
        public bool allowMapMagicScatter = true;
        public bool allowRuntimeScatter = true;
        public bool allowProxyPrimitives = true;
        [Min(0.1f)] public float minSpacingMeters = 4f;
        [Min(0f)] public float clusterRadiusMeters = 8f;
        [Min(1)] public int clusterCountMin = 1;
        [Min(1)] public int clusterCountMax = 3;
        public string heatmapChannel = string.Empty;
        public Color proxyColor = new Color(0.2f, 0.8f, 1f, 1f);

        [Header("Soft Affinity")]
        public HectonBiomeFamilyProfile[] preferredBiomeFamilies = new HectonBiomeFamilyProfile[0];
        public WorldZoneAnchor.ZoneKind[] preferredZoneKinds = new WorldZoneAnchor.ZoneKind[0];
        [Range(0f, 1f)] public float biomeAffinityWeight = 0.22f;
        [Range(0f, 1f)] public float zoneAffinityWeight = 0.14f;
        public WorldProceduralPattern primaryPattern = WorldProceduralPattern.SedimentResources;
        public WorldProceduralPattern secondaryPattern = WorldProceduralPattern.ReefNavigation;
        [Range(0f, 1f)] public float patternAffinityWeight = 0.22f;

        [Header("Variants")]
        public VariantEntry[] variants = new VariantEntry[0];

        [Header("Future Integration")]
        public string futurePrefabRoot = string.Empty;
        [TextArea(2, 4)] public string gameplayRole = "Generic world family.";
    }
}
