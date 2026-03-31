using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldProceduralPatternProfile", menuName = "Hecton8/World/Procedural Pattern Profile")]
    public sealed class WorldProceduralPatternProfile : ScriptableObject
    {
        [Header("Identity")]
        public WorldProceduralPattern pattern = WorldProceduralPattern.SedimentResources;
        public string label = "Sediment Resources";
        [TextArea(2, 5)] public string summary = "Reference sediment resource water.";

        [Header("Layer Budget Scales")]
        [Min(0f)] public float groundBudgetScale = 1f;
        [Min(0f)] public float clusterBudgetScale = 1f;
        [Min(0f)] public float structureBudgetScale = 1f;
        [Min(0f)] public float spawnBudgetScale = 1f;

        [Header("Layer Placement Targets")]
        [Min(0)] public int minGroundPlacements = 12;
        [Min(0)] public int groundTargetMax = 12;
        [Min(0)] public int minClusterPlacements = 4;
        [Min(0)] public int clusterTargetMax = 4;
        [Min(0)] public int minStructurePlacements = 4;
        [Min(0)] public int minSpawnPlacements = 4;

        [Header("Structure Targets")]
        [Min(0)] public int structureTargetMin = 4;
        [Min(0)] public int structureTargetMax = 6;
        [Min(0)] public int naturalLandmarkMin = 1;
        [Min(0)] public int naturalLandmarkMax = 2;
        [Min(0)] public int techFragmentMin;
        [Min(0)] public int techFragmentMax = 1;
        [Min(0)] public int caveReadMin;
        [Min(0)] public int caveReadMax = 1;
        [Min(0)] public int biologicalSilhouetteMin;
        [Min(0)] public int biologicalSilhouetteMax = 1;

        [Header("Cluster Accent Quotas")]
        [Min(0)] public int fertileGrowthMin;
        [Min(0)] public int biologicalNestMin;
        [Min(0)] public int resourcePocketMin;
        [Min(0)] public int shelterPocketMin;
        [Min(0)] public int hazardPocketMin;
        [Min(0)] public int debrisFieldMin;
        [Min(0)] public int rockCoverMin;

        [Header("Cluster Accent Max Ratios")]
        [Range(0f, 1f)] public float fertileGrowthMaxRatio = 1f;
        [Range(0f, 1f)] public float biologicalNestMaxRatio = 1f;
        [Range(0f, 1f)] public float resourcePocketMaxRatio = 1f;
        [Range(0f, 1f)] public float shelterPocketMaxRatio = 1f;
        [Range(0f, 1f)] public float hazardPocketMaxRatio = 1f;
        [Range(0f, 1f)] public float debrisFieldMaxRatio = 1f;
        [Range(0f, 1f)] public float rockCoverMaxRatio = 1f;

        [Header("Spawn Targets")]
        [Min(0)] public int spawnTargetMin = 4;
        [Min(0)] public int spawnTargetMax = 4;
        [Min(0)] public int passiveSpawnMin = 2;
        [Min(0)] public int predatorSpawnMin;
        [Min(0)] public int predatorSpawnMax = 1;

        public float GetBudgetScale(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(0f, groundBudgetScale),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(0f, clusterBudgetScale),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Max(0f, structureBudgetScale),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Max(0f, spawnBudgetScale),
                _ => 1f
            };
        }

        public int GetMinimumPlacements(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(0, minGroundPlacements),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(0, minClusterPlacements),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Max(0, minStructurePlacements),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Max(0, minSpawnPlacements),
                _ => 0
            };
        }

        public int GetTargetMin(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(0, minGroundPlacements),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(0, minClusterPlacements),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Max(0, structureTargetMin),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Max(0, spawnTargetMin),
                _ => 0
            };
        }

        public int GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(minGroundPlacements, groundTargetMax),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(minClusterPlacements, clusterTargetMax),
                WorldPrefabFamilyProfile.ScatterLayer.Structure => Mathf.Max(structureTargetMin, structureTargetMax),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn => Mathf.Max(spawnTargetMin, spawnTargetMax),
                _ => 0
            };
        }

        public int GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => Mathf.Max(0, naturalLandmarkMin),
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => Mathf.Max(0, techFragmentMin),
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => Mathf.Max(0, caveReadMin),
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => Mathf.Max(0, biologicalSilhouetteMin),
                _ => 0
            };
        }

        public int GetStructureAccentMax(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => Mathf.Max(naturalLandmarkMin, naturalLandmarkMax),
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => Mathf.Max(techFragmentMin, techFragmentMax),
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => Mathf.Max(caveReadMin, caveReadMax),
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => Mathf.Max(biologicalSilhouetteMin, biologicalSilhouetteMax),
                _ => 0
            };
        }

        public int GetClusterAccentMin(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => Mathf.Max(0, fertileGrowthMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => Mathf.Max(0, biologicalNestMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => Mathf.Max(0, resourcePocketMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => Mathf.Max(0, shelterPocketMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => Mathf.Max(0, hazardPocketMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => Mathf.Max(0, debrisFieldMin),
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => Mathf.Max(0, rockCoverMin),
                _ => 0
            };
        }

        public float GetClusterAccentMaxRatio(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => Mathf.Clamp01(fertileGrowthMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => Mathf.Clamp01(biologicalNestMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => Mathf.Clamp01(resourcePocketMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => Mathf.Clamp01(shelterPocketMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => Mathf.Clamp01(hazardPocketMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => Mathf.Clamp01(debrisFieldMaxRatio),
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => Mathf.Clamp01(rockCoverMaxRatio),
                _ => 1f
            };
        }
    }
}
