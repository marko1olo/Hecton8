using Hecton8.Environment;
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
    }
}
