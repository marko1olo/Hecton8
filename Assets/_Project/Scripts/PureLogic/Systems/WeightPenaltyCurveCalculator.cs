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
        /// <returns>Returns speedMultiplier of type float.</returns>
        public static float Compute(float currentWeightKg, float maxCarryKg, float penaltyStartFraction, float maxSpeedPenalty01)
        {
            if (float.IsNaN(currentWeightKg) || float.IsInfinity(currentWeightKg)) currentWeightKg = 0f;
            if (float.IsNaN(maxCarryKg) || float.IsInfinity(maxCarryKg)) maxCarryKg = 1f;
            if (float.IsNaN(penaltyStartFraction) || float.IsInfinity(penaltyStartFraction)) penaltyStartFraction = 0f;
            if (float.IsNaN(maxSpeedPenalty01) || float.IsInfinity(maxSpeedPenalty01)) maxSpeedPenalty01 = 0f;

            currentWeightKg = Math.Max(0f, currentWeightKg);
            maxCarryKg = Math.Max(0.0001f, maxCarryKg); // Prevent divide by zero

            float loadFraction = currentWeightKg / maxCarryKg;

            // Clamp penalty start to [0, 1] but avoid precisely 1 for division
            penaltyStartFraction = Math.Clamp(penaltyStartFraction, 0f, 0.9999f);

            float penalizedFraction = (loadFraction - penaltyStartFraction) / (1f - penaltyStartFraction);
            penalizedFraction = Math.Clamp(penalizedFraction, 0f, 1f);

            float speedMultiplier = 1f - (penalizedFraction * maxSpeedPenalty01);
            return Math.Max(0f, speedMultiplier);
        }
    }
}
