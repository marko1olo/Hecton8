using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CaloricDeficitPenaltyCalculator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CaloricDeficitPenaltyCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="caloricBalance">Parameter representing the caloricBalance (float).</param>
        /// <param name="deficitThreshold">Parameter representing the deficitThreshold (float).</param>
        /// <param name="maxPenalty">Parameter representing the maxPenalty (float).</param>
        /// <returns>Returns staminaPenalty01, float (strengthPenalty01) of type float.</returns>
        public static float Compute(float caloricBalance, float deficitThreshold, float maxPenalty)
        {
            if (float.IsNaN(caloricBalance) || float.IsNaN(deficitThreshold) || float.IsNaN(maxPenalty))
                return 0f;

            if (float.IsInfinity(caloricBalance) || float.IsInfinity(deficitThreshold) || float.IsInfinity(maxPenalty))
                return 0f;

            // Clamping inputs to physical/logical bounds
            float validMaxPenalty = Math.Clamp(maxPenalty, 0f, 1f);

            // Deficit threshold is conventionally negative or positive but we treat it as an absolute magnitude of deficit.
            float validDeficitThreshold = Math.Abs(deficitThreshold);

            // Positive balance: no penalty
            if (caloricBalance >= 0f)
                return 0f;

            // At this point caloricBalance is negative. Convert to absolute deficit.
            float deficit = Math.Abs(caloricBalance);

            if (validDeficitThreshold <= 0.0001f) // protect against zero/tiny threshold division
            {
                // If deficit threshold is zero, any deficit applies max penalty
                return deficit > 0f ? validMaxPenalty : 0f;
            }

            // Calculate penalty scale (0 to 1) based on how far past 0 towards the deficitThreshold we are
            float severityScale = deficit / validDeficitThreshold;

            // Clamp severity scale
            severityScale = Math.Clamp(severityScale, 0f, 1f);

            // Apply to max penalty
            return severityScale * validMaxPenalty;
        }
    }
}
