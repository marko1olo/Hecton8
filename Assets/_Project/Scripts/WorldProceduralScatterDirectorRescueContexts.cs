using System.Collections.Generic;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private struct ScatterRescueTrackingContext
        {
            public int StructureStride;
            public int SpawnStride;
            public FastCandidateMap GroundCandidates;
            public FastCandidateMap ClusterCandidates;
            public Dictionary<long, ScatterCandidate> StructureCandidates;
            public Dictionary<long, ScatterCandidate> SpawnCandidates;
            public FastCandidateMap ClusterFertileCandidates;
            public FastCandidateMap ClusterNestCandidates;
            public FastCandidateMap ClusterResourceCandidates;
            public FastCandidateMap ClusterShelterCandidates;
            public FastCandidateMap ClusterHazardCandidates;
            public FastCandidateMap ClusterDebrisCandidates;
            public FastCandidateMap ClusterRockCandidates;
            public FastCandidateMap StructureNaturalCandidates;
            public FastCandidateMap StructureTechCandidates;
            public FastCandidateMap StructureCaveCandidates;
            public FastCandidateMap StructureBioCandidates;
            public FastCandidateMap PassiveSpawnCandidates;
            public FastCandidateMap PredatorSpawnCandidates;

            public ScatterRescueTrackingContext(
                int structureStride,
                int spawnStride,
                FastCandidateMap groundCandidates,
                FastCandidateMap clusterCandidates,
                Dictionary<long, ScatterCandidate> structureCandidates,
                Dictionary<long, ScatterCandidate> spawnCandidates,
                FastCandidateMap clusterFertileCandidates,
                FastCandidateMap clusterNestCandidates,
                FastCandidateMap clusterResourceCandidates,
                FastCandidateMap clusterShelterCandidates,
                FastCandidateMap clusterHazardCandidates,
                FastCandidateMap clusterDebrisCandidates,
                FastCandidateMap clusterRockCandidates,
                FastCandidateMap structureNaturalCandidates,
                FastCandidateMap structureTechCandidates,
                FastCandidateMap structureCaveCandidates,
                FastCandidateMap structureBioCandidates,
                FastCandidateMap passiveSpawnCandidates,
                FastCandidateMap predatorSpawnCandidates)
            {
                StructureStride = structureStride;
                SpawnStride = spawnStride;
                GroundCandidates = groundCandidates;
                ClusterCandidates = clusterCandidates;
                StructureCandidates = structureCandidates;
                SpawnCandidates = spawnCandidates;
                ClusterFertileCandidates = clusterFertileCandidates;
                ClusterNestCandidates = clusterNestCandidates;
                ClusterResourceCandidates = clusterResourceCandidates;
                ClusterShelterCandidates = clusterShelterCandidates;
                ClusterHazardCandidates = clusterHazardCandidates;
                ClusterDebrisCandidates = clusterDebrisCandidates;
                ClusterRockCandidates = clusterRockCandidates;
                StructureNaturalCandidates = structureNaturalCandidates;
                StructureTechCandidates = structureTechCandidates;
                StructureCaveCandidates = structureCaveCandidates;
                StructureBioCandidates = structureBioCandidates;
                PassiveSpawnCandidates = passiveSpawnCandidates;
                PredatorSpawnCandidates = predatorSpawnCandidates;
            }
        }

        private readonly struct ScatterRescueContext
        {
            public readonly WorldProceduralPattern Pattern;
            public readonly global::Hecton8.Environment.HectonBiomeMatrixProfile BiomeProfile;
            public readonly int ClusterBudget;
            public readonly int StructureStride;
            public readonly int SpawnStride;
            public readonly int StructureBudget;
            public readonly int SpawnBudget;
            public readonly int[] LayerPlacementCounts;
            public readonly int[] ClusterAccentCounts;
            public readonly int[] StructureAccentCounts;
            public readonly ScatterCandidate[] LayerTopCandidates;
            public readonly bool[] LayerTopValid;
            public readonly Dictionary<string, int>[] LayerFamilyCounts;
            public readonly Dictionary<string, int>[] LayerBiomeCounts;
            public readonly FastCandidateMap GroundCandidates;
            public readonly FastCandidateMap ClusterCandidates;
            public readonly Dictionary<long, ScatterCandidate> StructureCandidates;
            public readonly Dictionary<long, ScatterCandidate> SpawnCandidates;
            public readonly FastCandidateMap ClusterFertileCandidates;
            public readonly FastCandidateMap ClusterNestCandidates;
            public readonly FastCandidateMap ClusterResourceCandidates;
            public readonly FastCandidateMap ClusterShelterCandidates;
            public readonly FastCandidateMap ClusterHazardCandidates;
            public readonly FastCandidateMap ClusterDebrisCandidates;
            public readonly FastCandidateMap ClusterRockCandidates;
            public readonly FastCandidateMap StructureNaturalCandidates;
            public readonly FastCandidateMap StructureTechCandidates;
            public readonly FastCandidateMap StructureCaveCandidates;
            public readonly FastCandidateMap StructureBioCandidates;
            public readonly FastCandidateMap PassiveSpawnCandidates;
            public readonly FastCandidateMap PredatorSpawnCandidates;

            public ScatterRescueContext(
                WorldProceduralPattern pattern,
                global::Hecton8.Environment.HectonBiomeMatrixProfile biomeProfile,
                int clusterBudget,
                int structureStride,
                int spawnStride,
                int structureBudget,
                int spawnBudget,
                int[] layerPlacementCounts,
                int[] clusterAccentCounts,
                int[] structureAccentCounts,
                ScatterCandidate[] layerTopCandidates,
                bool[] layerTopValid,
                Dictionary<string, int>[] layerFamilyCounts,
                Dictionary<string, int>[] layerBiomeCounts,
                ScatterRescueTrackingContext trackingContext)
            {
                Pattern = pattern;
                BiomeProfile = biomeProfile;
                ClusterBudget = clusterBudget;
                StructureStride = structureStride;
                SpawnStride = spawnStride;
                StructureBudget = structureBudget;
                SpawnBudget = spawnBudget;
                LayerPlacementCounts = layerPlacementCounts;
                ClusterAccentCounts = clusterAccentCounts;
                StructureAccentCounts = structureAccentCounts;
                LayerTopCandidates = layerTopCandidates;
                LayerTopValid = layerTopValid;
                LayerFamilyCounts = layerFamilyCounts;
                LayerBiomeCounts = layerBiomeCounts;
                GroundCandidates = trackingContext.GroundCandidates;
                ClusterCandidates = trackingContext.ClusterCandidates;
                StructureCandidates = trackingContext.StructureCandidates;
                SpawnCandidates = trackingContext.SpawnCandidates;
                ClusterFertileCandidates = trackingContext.ClusterFertileCandidates;
                ClusterNestCandidates = trackingContext.ClusterNestCandidates;
                ClusterResourceCandidates = trackingContext.ClusterResourceCandidates;
                ClusterShelterCandidates = trackingContext.ClusterShelterCandidates;
                ClusterHazardCandidates = trackingContext.ClusterHazardCandidates;
                ClusterDebrisCandidates = trackingContext.ClusterDebrisCandidates;
                ClusterRockCandidates = trackingContext.ClusterRockCandidates;
                StructureNaturalCandidates = trackingContext.StructureNaturalCandidates;
                StructureTechCandidates = trackingContext.StructureTechCandidates;
                StructureCaveCandidates = trackingContext.StructureCaveCandidates;
                StructureBioCandidates = trackingContext.StructureBioCandidates;
                PassiveSpawnCandidates = trackingContext.PassiveSpawnCandidates;
                PredatorSpawnCandidates = trackingContext.PredatorSpawnCandidates;
            }
        }
    }
}
