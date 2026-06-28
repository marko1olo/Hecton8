using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LaserBeamIntensityAttenuationCalculator.
    /// Extracted from LaserCutter.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LaserBeamIntensityAttenuationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="initialIntensity">Parameter representing the initialIntensity (float).</param>
        /// <param name="distanceMeters">Parameter representing the distanceMeters (float).</param>
        /// <param name="waterAttenuationCoeff">Parameter representing the waterAttenuationCoeff (float).</param>
        /// <param name="particulateDensity">Parameter representing the particulateDensity (float).</param>
        /// <returns>Returns intensityAtTarget of type float.</returns>
        public static float Compute(float initialIntensity, float distanceMeters, float waterAttenuationCoeff, float particulateDensity)
        {
            if (float.IsNaN(initialIntensity) || float.IsInfinity(initialIntensity)) return 0f;
            if (float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters)) return 0f;
            if (float.IsNaN(waterAttenuationCoeff) || float.IsInfinity(waterAttenuationCoeff)) return 0f;
            if (float.IsNaN(particulateDensity) || float.IsInfinity(particulateDensity)) return 0f;

            float clampedInitialIntensity = Math.Max(0f, initialIntensity);
            float clampedDistance = Math.Max(0f, distanceMeters);
            float clampedWaterCoeff = Math.Max(0f, waterAttenuationCoeff);
            float clampedParticulateDensity = Math.Max(0f, particulateDensity);

            float totalAttenuation = clampedWaterCoeff + clampedParticulateDensity;
            float exponent = -totalAttenuation * clampedDistance;

            double intensity = clampedInitialIntensity * Math.Exp(exponent);

            if (double.IsNaN(intensity) || double.IsInfinity(intensity)) return 0f;

            return Math.Max(0f, (float)intensity);
        }
    }
}
