using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FabricationCraftTimeModifier.
    /// Extracted from FabricationAssemblerRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FabricationCraftTimeModifier
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseCraftTimeSeconds">Parameter representing the baseCraftTimeSeconds (float).</param>
        /// <param name="benchTier01">Parameter representing the benchTier01 (float).</param>
        /// <param name="playerSkill01">Parameter representing the playerSkill01 (float).</param>
        /// <param name="materialComplexity">Parameter representing the materialComplexity (float).</param>
        /// <param name="minCraftTimeSeconds">The absolute minimum craft time allowed (to avoid magic numbers).</param>
        /// <param name="complexityMultiplierBase">The base multiplier for material complexity (to avoid magic numbers).</param>
        /// <param name="tierSkillCombinedWeight">The maximum reduction factor based on combined tier and skill (to avoid magic numbers).</param>
        /// <param name="minimumTimeFloor">The minimum bound for clamping negative inputs (e.g. 0f or 0.001f).</param>
        /// <param name="maxReductionLimit">The maximum reduction limit applied to time reduction.</param>
        /// <param name="skillTierWeightMultiplier">The multiplier to balance tier and skill impact.</param>
        /// <returns>Returns actualCraftTimeSeconds of type float.</returns>
        public static float Calculate(
            float baseCraftTimeSeconds,
            float benchTier01,
            float playerSkill01,
            float materialComplexity,
            float minCraftTimeSeconds,
            float complexityMultiplierBase,
            float tierSkillCombinedWeight,
            float minimumTimeFloor,
            float maxReductionLimit,
            float skillTierWeightMultiplier)
        {
            if (float.IsNaN(baseCraftTimeSeconds) || float.IsInfinity(baseCraftTimeSeconds)) return minCraftTimeSeconds;
            if (float.IsNaN(benchTier01) || float.IsInfinity(benchTier01)) benchTier01 = minimumTimeFloor;
            if (float.IsNaN(playerSkill01) || float.IsInfinity(playerSkill01)) playerSkill01 = minimumTimeFloor;
            if (float.IsNaN(materialComplexity) || float.IsInfinity(materialComplexity)) materialComplexity = 1f;

            baseCraftTimeSeconds = Math.Max(minimumTimeFloor, baseCraftTimeSeconds);
            benchTier01 = Math.Clamp(benchTier01, minimumTimeFloor, 1f);
            playerSkill01 = Math.Clamp(playerSkill01, minimumTimeFloor, 1f);
            materialComplexity = Math.Max(1f, materialComplexity);
            minCraftTimeSeconds = Math.Max(minimumTimeFloor, minCraftTimeSeconds);

            float complexityScaling = 1f + ((materialComplexity - 1f) * complexityMultiplierBase);
            float maxReductionFactor = Math.Clamp(tierSkillCombinedWeight, minimumTimeFloor, maxReductionLimit);
            float skillAndTierEffect = Math.Clamp((benchTier01 + playerSkill01) * skillTierWeightMultiplier, minimumTimeFloor, 1f);
            float timeReduction = 1f - (skillAndTierEffect * maxReductionFactor);
            float finalTime = baseCraftTimeSeconds * complexityScaling * timeReduction;

            return Math.Max(minCraftTimeSeconds, finalTime);
        }
    }
}
