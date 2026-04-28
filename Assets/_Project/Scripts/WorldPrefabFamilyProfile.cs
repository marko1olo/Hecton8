using System;
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

            [NonSerialized] private bool _isCheapProxy;
            [NonSerialized] private int _variantHash;

            internal bool IsCheapProxy => _isCheapProxy;
            internal int VariantHash => _variantHash;

            internal void RefreshRuntimeCache()
            {
                _variantHash = string.IsNullOrWhiteSpace(variantId)
                    ? 0
                    : Hecton.Localization.LocHash.Compute(variantId);
                _isCheapProxy = !string.IsNullOrWhiteSpace(variantId)
                    && variantId.EndsWith(".proxy.simple", StringComparison.Ordinal);
            }
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
        public bool overrideStreamingLayer;
        public WorldStreamingLayer streamingLayerOverride = WorldStreamingLayer.Flora;
        public bool contributesLargeThreatZone;
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

        [Header("Generative Geology")]
        public WorldGenerativeGeologyProfile generativeGeologyProfile;

        [Header("Future Integration")]
        public string futurePrefabRoot = string.Empty;
        [TextArea(2, 4)] public string gameplayRole = "Generic world family.";
        [System.NonSerialized] private string _generatedVariantId;
        [NonSerialized] private int _familyHash;
        [NonSerialized] private bool _isCheapProxyFamily;
        [NonSerialized] private bool _isPassiveSpawnFamily;
        [NonSerialized] private bool _isPredatorSpawnFamily;
        [NonSerialized] private bool _isLargeThreatFamilyHint;

        public int FamilyHash => _familyHash;
        public bool IsCheapProxyFamily => _isCheapProxyFamily;
        public bool IsPassiveSpawnFamily => _isPassiveSpawnFamily;
        public bool IsPredatorSpawnFamily => _isPredatorSpawnFamily;

        public string GeneratedVariantId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_generatedVariantId))
                    return _generatedVariantId;

                string resolvedFamilyId = string.IsNullOrWhiteSpace(familyId)
                    ? "world.family.generic"
                    : familyId;
                _generatedVariantId = string.Concat(resolvedFamilyId, ".generated");
                return _generatedVariantId;
            }
        }

        private void OnEnable()
        {
            RefreshRuntimeCache();
            RefreshVariantRuntimeCaches();
        }

        public bool UsesGenerativeGeology()
        {
            if (generativeGeologyProfile != null && generativeGeologyProfile.IsEnabled)
                return true;

            return proceduralDomain == ProceduralDomain.RockArch
                || proceduralDomain == ProceduralDomain.RockShelf
                || proceduralDomain == ProceduralDomain.Landmark
                || proceduralDomain == ProceduralDomain.CaveEntrance;
        }

        public WorldStreamingLayer ResolveStreamingLayer()
        {
            if (overrideStreamingLayer)
                return streamingLayerOverride;

            if (ResolveContributesLargeThreatZone())
                return WorldStreamingLayer.LargeThreats;

            return proceduralDomain switch
            {
                ProceduralDomain.Kelp => WorldStreamingLayer.Flora,
                ProceduralDomain.Plant => WorldStreamingLayer.Flora,
                ProceduralDomain.Coral => WorldStreamingLayer.Flora,
                ProceduralDomain.Debris => WorldStreamingLayer.Debris,
                ProceduralDomain.ResourcePocket => WorldStreamingLayer.Resources,
                ProceduralDomain.CreatureSpawn => WorldStreamingLayer.Fauna,
                ProceduralDomain.Egg => WorldStreamingLayer.Fauna,
                ProceduralDomain.RuinModule => WorldStreamingLayer.Construction,
                ProceduralDomain.PowerRoute => WorldStreamingLayer.Construction,
                ProceduralDomain.ServiceScar => WorldStreamingLayer.Construction,
                ProceduralDomain.HazardPocket => WorldStreamingLayer.Construction,
                ProceduralDomain.SafePocket => WorldStreamingLayer.Construction,
                ProceduralDomain.Rock => WorldStreamingLayer.TerrainLod,
                ProceduralDomain.RockCluster => WorldStreamingLayer.TerrainLod,
                ProceduralDomain.RockArch => WorldStreamingLayer.TerrainLod,
                ProceduralDomain.RockShelf => WorldStreamingLayer.TerrainLod,
                ProceduralDomain.CaveEntrance => WorldStreamingLayer.TerrainLod,
                ProceduralDomain.Landmark => WorldStreamingLayer.TerrainLod,
                _ => scatterLayer switch
                {
                    ScatterLayer.Spawn => WorldStreamingLayer.Fauna,
                    ScatterLayer.Structure => WorldStreamingLayer.Construction,
                    ScatterLayer.Cluster => WorldStreamingLayer.Debris,
                    _ => WorldStreamingLayer.Flora
                }
            };
        }

        public bool ResolveContributesLargeThreatZone()
        {
            return contributesLargeThreatZone || _isLargeThreatFamilyHint;
        }

        private static bool LooksLikeLargeThreatFamilyId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.ToLowerInvariant();
            return normalized.Contains("leviathan")
                || normalized.Contains("large_threat")
                || normalized.Contains("large-threat")
                || normalized.Contains("apex")
                || normalized.Contains("macrozone");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _generatedVariantId = null;
            RefreshRuntimeCache();
            RefreshVariantRuntimeCaches();
        }
#endif

        private void RefreshRuntimeCache()
        {
            _familyHash = !string.IsNullOrWhiteSpace(familyId)
                ? Hecton.Localization.LocHash.Compute(familyId)
                : unchecked((int)EntityId.ToULong(GetEntityId()));
            _isCheapProxyFamily = _familyHash == Hecton.Localization.LocHash.Compute("family.coral.low")
                || _familyHash == Hecton.Localization.LocHash.Compute("family.coral.massive")
                || _familyHash == Hecton.Localization.LocHash.Compute("family.landmark.spire")
                || _familyHash == Hecton.Localization.LocHash.Compute("family.cave.entrance");
            _isPassiveSpawnFamily = _familyHash == Hecton.Localization.LocHash.Compute("family.creature.spawn.passive");
            _isPredatorSpawnFamily = _familyHash == Hecton.Localization.LocHash.Compute("family.creature.spawn.predator");
            _isLargeThreatFamilyHint = LooksLikeLargeThreatFamilyId(familyId) || LooksLikeLargeThreatFamilyId(gameplayRole);
        }

        private void RefreshVariantRuntimeCaches()
        {
            if (variants == null)
                return;

            for (int i = 0; i < variants.Length; i++)
                variants[i]?.RefreshRuntimeCache();
        }
    }
}
