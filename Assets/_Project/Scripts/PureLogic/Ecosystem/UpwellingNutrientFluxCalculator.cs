using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for UpwellingNutrientFluxCalculator.
    /// Extracted from NutrientDriftRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class UpwellingNutrientFluxCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="upwellingVelocityMPerDay">Parameter representing the upwellingVelocityMPerDay (float).</param>
        /// <param name="deepNutrientConcentration">Parameter representing the deepNutrientConcentration (float).</param>
        /// <param name="shallowNutrientConcentration">Parameter representing the shallowNutrientConcentration (float).</param>
        /// <param name="mixingDepthM">Parameter representing the mixingDepthM (float).</param>
        /// <returns>Returns nutrientFluxMmolM2PerDay of type float.</returns>
        public static float Compute(float upwellingVelocityMPerDay, float deepNutrientConcentration, float shallowNutrientConcentration, float mixingDepthM)
        {
            if (float.IsNaN(upwellingVelocityMPerDay) || float.IsNaN(deepNutrientConcentration) ||
                float.IsNaN(shallowNutrientConcentration) || float.IsNaN(mixingDepthM))
            {
                return 0f;
            }

            if (float.IsInfinity(upwellingVelocityMPerDay) || float.IsInfinity(deepNutrientConcentration) ||
                float.IsInfinity(shallowNutrientConcentration) || float.IsInfinity(mixingDepthM))
            {
                return 0f;
            }

            // Division by zero protection for mixing depth, though mathematically we might just use it as a limit
            float safeMixingDepth = Math.Max(float.Epsilon, mixingDepthM);

            float clampedVelocity = Math.Max(0f, upwellingVelocityMPerDay);
            float clampedDeep = Math.Max(0f, deepNutrientConcentration);
            float clampedShallow = Math.Max(0f, shallowNutrientConcentration);

            // The flux is driven by the velocity and the difference in concentration.
            // If already mixed (shallow >= deep), flux is zero or reduced.
            float concentrationDiff = Math.Max(0f, clampedDeep - clampedShallow);

            // Mathematically: Flux (mmol/m^2/day) = Velocity (m/day) * ConcentrationDiff (mmol/m^3)
            // We include mixingDepthM as a stabilizing factor to prevent infinite concentration spikes in zero-depth scenarios,
            // or simply validate it's physically meaningful.
            if (safeMixingDepth <= float.Epsilon)
            {
                return 0f; // No mixing volume available
            }

            float flux = clampedVelocity * concentrationDiff;

            // Cap the output to avoid overflow
            return Math.Min(flux, float.MaxValue);
        }
    }
}
