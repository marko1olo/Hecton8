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

        /// <summary>
        /// Per-layer placement ceiling. The ceiling can never sit below the floor the same profile
        /// guarantees through <see cref="GetMinimumPlacements"/>, because the acceptance pass treats
        /// this value as a hard reject (WorldProceduralScatterDirector.cs:5027/5054/5081 and
        /// WorldProceduralScatterDirectorCandidateAcceptance.cs:832/853/867 all return false once the
        /// layer count reaches it). A profile whose ceiling sits under its own floor therefore
        /// solicits placements that the acceptance pass rejects one by one, and the layer silently
        /// ends up empty instead of reporting a contradiction.
        /// Ground and Cluster already folded their guaranteed minimum in; Structure and Spawn read
        /// only their target pair, so <c>minStructurePlacements</c> and <c>minSpawnPlacements</c>
        /// could be authored above the ceiling under their <c>[Min(0)]</c> ranges. Structure is the
        /// layer that carries every technogenic accent, and
        /// <see cref="ResolveGuaranteedStructureAccentFloor"/> spends this exact value as its whole
        /// budget - so a Structure ceiling of zero also silently voids the TechFragment guarantee.
        /// </summary>
        public int GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer switch
            {
                WorldPrefabFamilyProfile.ScatterLayer.Ground => Mathf.Max(minGroundPlacements, groundTargetMax),
                WorldPrefabFamilyProfile.ScatterLayer.Cluster => Mathf.Max(minClusterPlacements, clusterTargetMax),
                WorldPrefabFamilyProfile.ScatterLayer.Structure =>
                    Mathf.Max(minStructurePlacements, Mathf.Max(structureTargetMin, structureTargetMax)),
                WorldPrefabFamilyProfile.ScatterLayer.Spawn =>
                    Mathf.Max(minSpawnPlacements, Mathf.Max(spawnTargetMin, spawnTargetMax)),
                _ => 0
            };
        }

        public const int FirstStructureAccentRoleIndex = (int)WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark;
        public const int LastStructureAccentRoleIndex = (int)WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette;

        /// <summary>
        /// Effective per-role structure accent floor.
        /// A role the pattern solicits (authored max above zero) but leaves without an authored
        /// floor is guaranteed one placement whenever the structure layer target can still carry
        /// it after every authored floor is reserved. Without this, an accent the pattern wants
        /// but does not demand is skipped outright by the structure guarantee pass
        /// (WorldProceduralScatterDirector.cs:7091 continues on a zero required count) and has to
        /// win the ordinary heat gate on its own - which the technogenic channels cannot do in the
        /// shallow patterns, because their shaped ceilings sit below every authored technogenic
        /// rule threshold (WorldProceduralFieldSampler.cs:3889/3897 and :3907/3915).
        /// A role the pattern bans outright (authored floor and ceiling both zero) stays at zero.
        /// </summary>
        public int GetStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            int authoredMin = GetAuthoredStructureAccentMin(role);
            if (authoredMin > 0)
                return authoredMin;

            if (GetAuthoredStructureAccentMax(role) <= 0)
                return 0;

            return ResolveGuaranteedStructureAccentFloor(role);
        }

        public int GetStructureAccentMax(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            int authoredMax = GetAuthoredStructureAccentMax(role);
            if (authoredMax <= 0 && GetAuthoredStructureAccentMin(role) <= 0)
                return 0;

            return Mathf.Max(GetStructureAccentMin(role), authoredMax);
        }

        private int GetAuthoredStructureAccentMin(WorldPrefabFamilyProfile.StructureAccentRole role)
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

        private int GetAuthoredStructureAccentMax(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            return role switch
            {
                WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark => Mathf.Max(0, naturalLandmarkMax),
                WorldPrefabFamilyProfile.StructureAccentRole.TechFragment => Mathf.Max(0, techFragmentMax),
                WorldPrefabFamilyProfile.StructureAccentRole.CaveRead => Mathf.Max(0, caveReadMax),
                WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette => Mathf.Max(0, biologicalSilhouetteMax),
                _ => 0
            };
        }

        /// <summary>
        /// Deterministic, allocation-free. Reserves every authored floor first, then walks the four
        /// accent roles in declaration order and grants a floor of one to each solicited role that
        /// still fits inside the structure layer target. Authored floors always win; the guarantee
        /// only ever spends slack the pattern left unclaimed, so it can never push the summed floors
        /// past the layer target the acceptance pass enforces.
        /// </summary>
        private int ResolveGuaranteedStructureAccentFloor(WorldPrefabFamilyProfile.StructureAccentRole role)
        {
            int layerBudget = GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Structure);
            if (layerBudget <= 0)
                return 0;

            int authoredTotal = 0;
            for (int i = FirstStructureAccentRoleIndex; i <= LastStructureAccentRoleIndex; i++)
                authoredTotal += GetAuthoredStructureAccentMin((WorldPrefabFamilyProfile.StructureAccentRole)i);

            int remaining = layerBudget - authoredTotal;
            if (remaining <= 0)
                return 0;

            for (int i = FirstStructureAccentRoleIndex; i <= LastStructureAccentRoleIndex; i++)
            {
                WorldPrefabFamilyProfile.StructureAccentRole candidate =
                    (WorldPrefabFamilyProfile.StructureAccentRole)i;

                if (GetAuthoredStructureAccentMin(candidate) > 0)
                    continue;

                if (GetAuthoredStructureAccentMax(candidate) <= 0)
                    continue;

                if (remaining <= 0)
                    return 0;

                remaining--;
                if (candidate == role)
                    return 1;
            }

            return 0;
        }

        public const int FirstClusterAccentRoleIndex = (int)WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth;
        public const int LastClusterAccentRoleIndex = (int)WorldPrefabFamilyProfile.ClusterAccentRole.RockCover;

        /// <summary>
        /// Effective per-role cluster accent floor. Same contract as
        /// <see cref="GetStructureAccentMin"/>: a role the pattern solicits (authored ceiling ratio
        /// above zero) but leaves without an authored floor is guaranteed one placement while the
        /// cluster layer target still has slack after every authored floor is reserved. Without it
        /// the cluster guarantee pass skips the role outright
        /// (WorldProceduralScatterDirector.cs:6027 continues on a zero required count), which is
        /// what left DebrisField - the salvage/wreck-fragment accent - at zero on FertileShallows
        /// and ReefNavigation. A floor of one can never breach the ratio ceiling because the
        /// acceptance pass floors the allowance at one
        /// (WorldProceduralScatterDirectorCandidateAcceptance.cs:847).
        /// A role the pattern bans outright (authored ratio zero) stays at zero.
        /// </summary>
        public int GetClusterAccentMin(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            int authoredMin = GetAuthoredClusterAccentMin(role);
            if (authoredMin > 0)
                return authoredMin;

            if (GetClusterAccentMaxRatio(role) <= 0f)
                return 0;

            return ResolveGuaranteedClusterAccentFloor(role);
        }

        private int GetAuthoredClusterAccentMin(WorldPrefabFamilyProfile.ClusterAccentRole role)
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

        /// <summary>
        /// Deterministic, allocation-free. Reserves every authored cluster floor first, then walks
        /// the seven cluster accent roles in declaration order and grants a floor of one to each
        /// solicited role that still fits inside the cluster layer target.
        /// </summary>
        private int ResolveGuaranteedClusterAccentFloor(WorldPrefabFamilyProfile.ClusterAccentRole role)
        {
            int layerBudget = GetTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            if (layerBudget <= 0)
                return 0;

            int authoredTotal = 0;
            for (int i = FirstClusterAccentRoleIndex; i <= LastClusterAccentRoleIndex; i++)
                authoredTotal += GetAuthoredClusterAccentMin((WorldPrefabFamilyProfile.ClusterAccentRole)i);

            int remaining = layerBudget - authoredTotal;
            if (remaining <= 0)
                return 0;

            for (int i = FirstClusterAccentRoleIndex; i <= LastClusterAccentRoleIndex; i++)
            {
                WorldPrefabFamilyProfile.ClusterAccentRole candidate =
                    (WorldPrefabFamilyProfile.ClusterAccentRole)i;

                if (GetAuthoredClusterAccentMin(candidate) > 0)
                    continue;

                if (GetClusterAccentMaxRatio(candidate) <= 0f)
                    continue;

                if (remaining <= 0)
                    return 0;

                remaining--;
                if (candidate == role)
                    return 1;
            }

            return 0;
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
