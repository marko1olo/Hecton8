using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldProceduralBiomeFamilyContextProfile", menuName = "Hecton8/World/Procedural Biome Family Context Profile")]
    public sealed class WorldProceduralBiomeFamilyContextProfile : ScriptableObject
    {
        [Header("Identity")]
        public HectonBiomeFamilyProfile biomeFamily;
        public string label = "Generic Biome Context";
        [TextArea(2, 5)] public string summary = "Generic biome-driven context for procedural fill.";

        [Header("Layer Budget Scales")]
        [Min(0f)] public float groundBudgetScale = 1f;
        [Min(0f)] public float clusterBudgetScale = 1f;
        [Min(0f)] public float structureBudgetScale = 1f;
        [Min(0f)] public float spawnBudgetScale = 1f;

        [Header("Domain Biases")]
        [Range(-0.4f, 0.4f)] public float rockBias;
        [Range(-0.4f, 0.4f)] public float kelpBias;
        [Range(-0.4f, 0.4f)] public float plantBias;
        [Range(-0.4f, 0.4f)] public float coralBias;
        [Range(-0.4f, 0.4f)] public float eggBias;
        [Range(-0.4f, 0.4f)] public float debrisBias;
        [Range(-0.4f, 0.4f)] public float ruinBias;
        [Range(-0.4f, 0.4f)] public float caveBias;
        [Range(-0.4f, 0.4f)] public float landmarkBias;
        [Range(-0.4f, 0.4f)] public float creatureSpawnBias;
        [Range(-0.4f, 0.4f)] public float resourcePocketBias;
        [Range(-0.4f, 0.4f)] public float hazardPocketBias;
        [Range(-0.4f, 0.4f)] public float safePocketBias;
        [Range(-0.4f, 0.4f)] public float powerRouteBias;
        [Range(-0.4f, 0.4f)] public float serviceScarBias;

        [Header("Cluster Accent Biases")]
        [Range(-0.4f, 0.4f)] public float fertileGrowthBias;
        [Range(-0.4f, 0.4f)] public float biologicalNestBias;
        [Range(-0.4f, 0.4f)] public float resourcePocketAccentBias;
        [Range(-0.4f, 0.4f)] public float shelterPocketBias;
        [Range(-0.4f, 0.4f)] public float hazardPocketAccentBias;
        [Range(-0.4f, 0.4f)] public float debrisFieldBias;
        [Range(-0.4f, 0.4f)] public float rockCoverBias;

        [Header("Structure Accent Biases")]
        [Range(-0.4f, 0.4f)] public float naturalLandmarkBias;
        [Range(-0.4f, 0.4f)] public float techFragmentBias;
        [Range(-0.4f, 0.4f)] public float caveReadBias;
        [Range(-0.4f, 0.4f)] public float biologicalSilhouetteBias;

        [Header("Spawn Biases")]
        [Range(-0.4f, 0.4f)] public float passiveSpawnBias;
        [Range(-0.4f, 0.4f)] public float predatorSpawnBias;

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

        public float GetDomainBias(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => rockBias,
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => rockBias,
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => Mathf.Max(rockBias, landmarkBias),
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => kelpBias,
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => plantBias,
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => coralBias,
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => eggBias,
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => debrisBias,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => ruinBias,
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => caveBias,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => landmarkBias,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => creatureSpawnBias,
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => resourcePocketBias,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => hazardPocketBias,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => safePocketBias,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => powerRouteBias,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => serviceScarBias,
                _ => 0f
            };
        }

        public float GetClusterAccentBias(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth => fertileGrowthBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest => biologicalNestBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket => resourcePocketAccentBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket => shelterPocketBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket => hazardPocketAccentBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField => debrisFieldBias,
                WorldPrefabFamilyProfile.ClusterAccentRole.RockCover => rockCoverBias,
                _ => 0f
            };
        }

        public float GetStructureAccentBias(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => naturalLandmarkBias,
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => techFragmentBias,
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => caveReadBias,
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => biologicalSilhouetteBias,
                _ => 0f
            };
        }

        public float GetSpawnBias(bool passive, bool predator)
        {
            if (predator)
                return predatorSpawnBias;

            if (passive)
                return passiveSpawnBias;

            return 0f;
        }
    }
}
