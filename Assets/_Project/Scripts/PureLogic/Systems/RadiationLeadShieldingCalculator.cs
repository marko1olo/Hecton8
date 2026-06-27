using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for RadiationLeadShieldingCalculator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class RadiationLeadShieldingCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="rawRadiationLevel">Parameter representing the rawRadiationLevel (float).</param>
        /// <param name="leadThicknessCm">Parameter representing the leadThicknessCm (float).</param>
        /// <param name="shieldingQuality">Parameter representing the shieldingQuality (float).</param>
        /// <returns>Returns Absorbed radiation dose of type float.</returns>
        public static float Compute(float rawRadiationLevel, float leadThicknessCm, float shieldingQuality)
        {
            float safeRawLevel = Math.Max(0f, rawRadiationLevel);

            if (float.IsNaN(safeRawLevel) || float.IsInfinity(safeRawLevel)) return 0f;
            if (float.IsNaN(leadThicknessCm)) return safeRawLevel;
            if (float.IsPositiveInfinity(leadThicknessCm)) return 0f;
            if (float.IsNaN(shieldingQuality)) return safeRawLevel;
            if (float.IsPositiveInfinity(shieldingQuality)) return 0f;

            float safeThickness = Math.Max(0f, leadThicknessCm);
            float safeQuality = Math.Max(0f, shieldingQuality);

            // Objective: Calculate radiation dose reduction after traveling through shielding materials of specific thickness.
            // Constraints: Exponential decay formula.
            // dose = rawLevel * e^(-thickness * quality)

            double exponent = -safeThickness * safeQuality;
            float decayFactor = (float)Math.Exp(exponent);

            if (float.IsNaN(decayFactor) || float.IsInfinity(decayFactor)) return safeRawLevel;

            float dose = safeRawLevel * decayFactor;

            return Math.Max(0f, dose);
        }
    }
}
