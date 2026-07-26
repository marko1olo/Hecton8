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

            float sand = math.saturate(weights.ShellSand + weights.ClaySilt + weights.ReefRubble * 0.35f);
            float rock = math.saturate(weights.HardRock + weights.LimestoneShelf);
            return sand >= rock
                ? WorldProceduralPlacementRule.FloraSubstrateMask.Sand
                : WorldProceduralPlacementRule.FloraSubstrateMask.Rock;
        }

        public static bool ResolveFloraSubstrateFromTerrainDetail(
            in WorldTerrainSurfaceMaterialWeights weights,
            WorldTerrainSurfaceMaterialClass targetMaterial)
        {
            return ResolveFloraSubstrateFromTerrainDetail(in weights, 0f, targetMaterial);
        }

        public static bool ResolveFloraSubstrateFromTerrainDetail(
            in WorldTerrainSurfaceMaterialWeights weights,
            float depthMeters,
            WorldTerrainSurfaceMaterialClass targetMaterial)
        {
            float rock = math.saturate(weights.HardRock + weights.LimestoneShelf);
            switch (targetMaterial)
            {
                case WorldTerrainSurfaceMaterialClass.HardRock:
                    return weights.HardRock >= 0.18f || weights.LimestoneShelf >= 0.16f;
                case WorldTerrainSurfaceMaterialClass.LimestoneShelf:
                    return weights.LimestoneShelf >= 0.16f;
                case WorldTerrainSurfaceMaterialClass.ClaySilt:
                    return weights.ClaySilt >= 0.18f;
                case WorldTerrainSurfaceMaterialClass.BrineSaltCrust:
                    return weights.BrineSaltCrust >= 0.12f;
                case WorldTerrainSurfaceMaterialClass.ManganeseNodulePlain:
                    return depthMeters > 1200f && weights.ManganeseNodulePlain >= 0.12f;
                case WorldTerrainSurfaceMaterialClass.ReefRubble:
                    return weights.ReefRubble >= 0.12f;
                case WorldTerrainSurfaceMaterialClass.SeepCrust:
                    return weights.SeepCrust >= 0.12f;
                case WorldTerrainSurfaceMaterialClass.ShellSand:
                default:
                    return weights.ShellSand >= 0.16f || (weights.ClaySilt + rock < 0.28f);
            }
        }

        public static bool PassesClusterPatchEnvelope(
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

            float patchMask = ScatterMath.EvaluateClusterPatchMask01(
                positionX,
                positionZ,
                0,
                0,
                clusterNoiseScale,
                ruleIdHash,
                familyHash);
            return patchMask >= clusterNoiseThreshold;
        }
    }
}
