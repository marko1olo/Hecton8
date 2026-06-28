using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DecompressionNitrogenLoadCalculator.
    /// Extracted from Shinobu namespace / Physiology. Fully stateless and allocation-free.
    /// </summary>
    public static class DecompressionNitrogenLoadCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentLoad">Parameter representing the currentLoad (float).</param>
        /// <param name="breathingGasPressure">Parameter representing the breathingGasPressure (float).</param>
        /// <param name="halflimeMinutes">Parameter representing the halflimeMinutes (float).</param>
        /// <param name="deltaMinutes">Parameter representing the deltaMinutes (float).</param>
        /// <returns>Returns New nitrogen loading pressure of type float.</returns>
        public static float Compute(float currentLoad, float breathingGasPressure, float halflimeMinutes, float deltaMinutes)
        {
            // Sanitize input values
            float safeCurrentLoad = float.IsNaN(currentLoad) || float.IsInfinity(currentLoad) ? 0f : Math.Max(0f, currentLoad);
            float safeBreathingGasPressure = float.IsNaN(breathingGasPressure) || float.IsInfinity(breathingGasPressure) ? 0f : Math.Max(0f, breathingGasPressure);
            float safeHalfTime = float.IsNaN(halflimeMinutes) || float.IsInfinity(halflimeMinutes) ? 1f : Math.Max(0.0001f, halflimeMinutes);
            float safeDeltaMinutes = float.IsNaN(deltaMinutes) || float.IsInfinity(deltaMinutes) ? 0f : Math.Max(0f, deltaMinutes);

            // Compute exponential decay rate
            float k = 0.69314718056f / safeHalfTime; // ln(2) / T1/2
            float effectiveK = Math.Max(k, 0.0001f);

            // Calculate decay factor over delta time
            float decay = (float)Math.Exp(-effectiveK * safeDeltaMinutes);

            // Apply simplified Haldanean equation: NextLoad = CurrentLoad + (InspiredGasPressure - CurrentLoad) * (1 - decay)
            float newLoad = safeCurrentLoad + (safeBreathingGasPressure - safeCurrentLoad) * (1f - decay);

            // Clamp and sanitize output
            return float.IsNaN(newLoad) || float.IsInfinity(newLoad) ? safeCurrentLoad : Math.Max(0f, newLoad);
        }
    }
}
