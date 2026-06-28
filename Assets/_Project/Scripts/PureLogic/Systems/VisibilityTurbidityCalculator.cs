using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VisibilityTurbidityCalculator.
    /// Extracted from HectonAtmosphereManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VisibilityTurbidityCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='turbidityNTU'>Parameter representing the turbidityNTU (float).</param>
        /// <param name='biolumLevelLux'>Parameter representing the biolumLevelLux (float).</param>
        /// <param name='baseVisibilityMeters'>Parameter representing the baseVisibilityMeters (float).</param>
        /// <returns>Returns effectiveVisibilityMeters of type float.</returns>
        public static float Compute(float turbidityNTU, float biolumLevelLux, float baseVisibilityMeters)
        {
            if (float.IsNaN(turbidityNTU) || float.IsInfinity(turbidityNTU))
                throw new ArgumentException("turbidityNTU must be finite.", nameof(turbidityNTU));

            if (float.IsNaN(biolumLevelLux) || float.IsInfinity(biolumLevelLux))
                throw new ArgumentException("biolumLevelLux must be finite.", nameof(biolumLevelLux));

            if (float.IsNaN(baseVisibilityMeters) || float.IsInfinity(baseVisibilityMeters))
                throw new ArgumentException("baseVisibilityMeters must be finite.", nameof(baseVisibilityMeters));

            float clampedBaseVisibility = Math.Max(0f, baseVisibilityMeters);
            float clampedTurbidity = Math.Max(0f, turbidityNTU);
            float clampedBiolum = Math.Max(0f, biolumLevelLux);

            // Visibility is extracted based on logic from the atmosphere manager.
            // Using standard inverse relationship where base visibility is attenuated by turbidity.
            // Biolum mitigates darkness (acting as an additive counter to the turbidity penalty).
            float clearWaterPresence = clampedBaseVisibility / Math.Max(1.0f, clampedTurbidity);
            float biolumContribution = clampedBiolum;

            float effectiveVisibility = clearWaterPresence + biolumContribution;

            return Math.Max(0f, effectiveVisibility);
        }
    }
}
