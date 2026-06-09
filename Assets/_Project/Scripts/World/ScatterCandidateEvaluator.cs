using Hecton8.Environment;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Stateless acceptance/evaluation facade for procedural scatter candidate math.
    /// </summary>
    internal static class ScatterCandidateEvaluator
    {
        public static int ResolveHeightLayerIndex(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterMath.ResolveHeightLayerIndex(fieldSample, runtimeRule);
        }

        public static int ResolveHeightLayerIndex(
            float caveProximity,
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole)
        {
            return ScatterMath.ResolveHeightLayerIndex(caveProximity, family, structureAccentRole);
        }

        public static bool ShouldEvaluateScatterDomain(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterMath.ShouldEvaluateScatterDomain(fieldSample, runtimeRule);
        }

        public static float GetHorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            return ScatterMath.GetHorizontalDistanceSqr(a, b);
        }

        public static long ComposeScatterGridKey(int cellX, int cellZ)
        {
            return ScatterMath.ComposeScatterGridKey(cellX, cellZ);
        }

        public static float ResolveRequiredDistance(
            WorldProceduralScatterDirector.ScatterPlacement candidate,
            WorldProceduralScatterDirector.ScatterPlacement existing)
        {
            return ScatterMath.ResolveRequiredDistance(candidate, existing);
        }

        public static float GetEffectiveSpacing(WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule)
        {
            return ScatterMath.GetEffectiveSpacing(family, rule);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool RegisterPoissonRejection(ref int rejectionAttempts, int maxRejectionAttempts)
        {
            rejectionAttempts++;
            return rejectionAttempts >= math.max(1, maxRejectionAttempts);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool PassesStrictSubstrateEnvelope(
            WorldProceduralPlacementRule.FloraSubstrateMask requiredSubstrate,
            WorldProceduralPlacementRule.FloraSubstrateMask resolvedSubstrate)
        {
            return requiredSubstrate == WorldProceduralPlacementRule.FloraSubstrateMask.None ||
                   requiredSubstrate == WorldProceduralPlacementRule.FloraSubstrateMask.Any ||
                   requiredSubstrate == WorldProceduralPlacementRule.FloraSubstrateMask.AnyGeology ||
                   (requiredSubstrate & resolvedSubstrate) != 0;
        }

        internal static WorldProceduralPlacementRule.FloraSubstrateMask ResolveFloraSubstrateFromTerrainDetail(
            WorldTerrainDetailEligibilityFlags eligibility,
            WorldTerrainSurfaceMaterialClass dominantMaterial,
            in WorldTerrainSurfaceMaterialWeights weights)
        {
            WorldProceduralPlacementRule.FloraSubstrateMask substrate = WorldProceduralPlacementRule.FloraSubstrateMask.None;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.SandScatter) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Sand;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.RockScatter) != 0 ||
                (eligibility & WorldTerrainDetailEligibilityFlags.TalusBoulder) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Rock;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.ReefScatter) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Reef;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.BrineDeposit) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Brine;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.SeepDeposit) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Seep;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.NoduleScatter) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Nodule;
            if ((eligibility & WorldTerrainDetailEligibilityFlags.RubblePebble) != 0)
                substrate |= WorldProceduralPlacementRule.FloraSubstrateMask.Rubble;

            if (substrate != WorldProceduralPlacementRule.FloraSubstrateMask.None)
                return substrate;

            switch (dominantMaterial)
            {
                case WorldTerrainSurfaceMaterialClass.ShellSand:
                case WorldTerrainSurfaceMaterialClass.ClaySilt:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Sand;
                case WorldTerrainSurfaceMaterialClass.LimestoneShelf:
                case WorldTerrainSurfaceMaterialClass.HardRock:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Rock;
                case WorldTerrainSurfaceMaterialClass.BrineSaltCrust:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Brine;
                case WorldTerrainSurfaceMaterialClass.ManganeseNodulePlain:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Nodule;
                case WorldTerrainSurfaceMaterialClass.ReefRubble:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Reef |
                           WorldProceduralPlacementRule.FloraSubstrateMask.Rubble;
                case WorldTerrainSurfaceMaterialClass.SeepCrust:
                    return WorldProceduralPlacementRule.FloraSubstrateMask.Seep |
                           WorldProceduralPlacementRule.FloraSubstrateMask.Rock;
            }

            float sand = math.saturate(weights.ShellSand + weights.ClaySilt + weights.ReefRubble * 0.35f);
            float rock = math.saturate(weights.HardRock + weights.LimestoneShelf + weights.SeepCrust * 0.30f);
            return sand >= rock
                ? WorldProceduralPlacementRule.FloraSubstrateMask.Sand
                : WorldProceduralPlacementRule.FloraSubstrateMask.Rock;
        }

        internal static bool PassesClusterPatchEnvelope(
            float positionX,
            float positionZ,
            float chunkSize,
            float clusterNoiseThreshold,
            float clusterNoiseScale,
            int ruleIdHash,
            int familyHash)
        {
            if (clusterNoiseThreshold <= 0f)
                return true;

            float safeChunkSize = math.max(1f, chunkSize);
            int chunkX = (int)math.floor(positionX / safeChunkSize);
            int chunkZ = (int)math.floor(positionZ / safeChunkSize);
            float patchMask = ScatterMath.EvaluateClusterPatchMask01(
                positionX,
                positionZ,
                chunkX,
                chunkZ,
                clusterNoiseScale,
                ruleIdHash,
                familyHash);
            return patchMask >= clusterNoiseThreshold;
        }
    }
}
