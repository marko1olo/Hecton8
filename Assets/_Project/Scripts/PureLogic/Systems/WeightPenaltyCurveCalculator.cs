using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for WeightPenaltyCurveCalculator.
    /// Extracted from PlayerInventory.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class WeightPenaltyCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentWeightKg">Parameter representing the currentWeightKg (float).</param>
        /// <param name="maxCarryKg">Parameter representing the maxCarryKg (float).</param>
        /// <param name="penaltyStartFraction">Parameter representing the penaltyStartFraction (float).</param>
        /// <param name="maxSpeedPenalty01">Parameter representing the maxSpeedPenalty01 (float).</param>
        /// <returns>Returns speedMultiplier, float (staminaDrainMultiplier) of type float.</returns>
        public static float Compute(float currentWeightKg, float maxCarryKg, float penaltyStartFraction, float maxSpeedPenalty01)
        {
            if (float.IsNaN(currentWeightKg) || float.IsInfinity(currentWeightKg)) currentWeightKg = 0f;
            if (float.IsNaN(maxCarryKg) || float.IsInfinity(maxCarryKg) || maxCarryKg <= 0f) maxCarryKg = 1f;
            if (float.IsNaN(penaltyStartFraction) || float.IsInfinity(penaltyStartFraction)) penaltyStartFraction = 0f;
            if (float.IsNaN(maxSpeedPenalty01) || float.IsInfinity(maxSpeedPenalty01)) maxSpeedPenalty01 = 0f;

            currentWeightKg = Math.Max(0f, currentWeightKg);
            maxCarryKg = Math.Max(0.001f, maxCarryKg);
            penaltyStartFraction = Math.Clamp(penaltyStartFraction, 0f, 1f);
            maxSpeedPenalty01 = Math.Clamp(maxSpeedPenalty01, 0f, 1f);

            float minSpeedMultiplier = 1f - maxSpeedPenalty01;

            float loadFraction = currentWeightKg / maxCarryKg;

            float safeLoadFraction = Math.Clamp(loadFraction, 0f, 1f);

            if (safeLoadFraction <= penaltyStartFraction)
            {
                return 1f;
            }

            if (penaltyStartFraction >= 1f)
            {
                return minSpeedMultiplier;
            }

            float penaltyRange = 1f - penaltyStartFraction;
            float activePenaltyFraction = (safeLoadFraction - penaltyStartFraction) / penaltyRange;

            float speedMultiplier = 1f - (activePenaltyFraction * maxSpeedPenalty01);
            return Math.Clamp(speedMultiplier, minSpeedMultiplier, 1f);
        }
    }
}
