using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Compatibility facade for callers not yet moved directly to <see cref="ScatterMath"/>.
    /// </summary>
    internal static class ScatterEvaluationEngine
    {
        public static int ResolveHeightLayerIndex(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterCandidateEvaluator.ResolveHeightLayerIndex(fieldSample, runtimeRule);
        }

        public static int ResolveHeightLayerIndex(
            float caveProximity,
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole)
        {
            return ScatterCandidateEvaluator.ResolveHeightLayerIndex(caveProximity, family, structureAccentRole);
        }

        public static bool ShouldEvaluateScatterDomain(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            return ScatterCandidateEvaluator.ShouldEvaluateScatterDomain(fieldSample, runtimeRule);
        }

        public static float GetHorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            return ScatterCandidateEvaluator.GetHorizontalDistanceSqr(a, b);
        }

        public static long ComposeScatterGridKey(int cellX, int cellZ)
        {
            return ScatterCandidateEvaluator.ComposeScatterGridKey(cellX, cellZ);
        }

        public static float ResolveRequiredDistance(
            WorldProceduralScatterDirector.ScatterPlacement candidate,
            WorldProceduralScatterDirector.ScatterPlacement existing)
        {
            return ScatterCandidateEvaluator.ResolveRequiredDistance(candidate, existing);
        }

        public static float GetEffectiveSpacing(WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule)
        {
            return ScatterCandidateEvaluator.GetEffectiveSpacing(family, rule);
        }
    }
}
