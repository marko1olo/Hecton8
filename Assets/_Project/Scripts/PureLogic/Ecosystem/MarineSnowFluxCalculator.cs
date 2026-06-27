using System;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for MarineSnowFluxCalculator.
    /// Extracted from NutrientDriftRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class MarineSnowFluxCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="surfaceProductivity">Parameter representing the surfaceProductivity (float).</param>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="sinkingSpeedMPerDay">Parameter representing the sinkingSpeedMPerDay (float).</param>
        /// <param name="remineralizationRate">Parameter representing the remineralizationRate (float).</param>
        /// <returns>Returns fluxMgM2PerDay of type float.</returns>
        public static float Compute(float surfaceProductivity, float depthMeters, float sinkingSpeedMPerDay, float remineralizationRate)
        {
            if (float.IsNaN(surfaceProductivity) || float.IsInfinity(surfaceProductivity)) surfaceProductivity = 0f;
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters)) depthMeters = 0f;
            if (float.IsNaN(sinkingSpeedMPerDay) || float.IsInfinity(sinkingSpeedMPerDay)) sinkingSpeedMPerDay = 1f;
            if (float.IsNaN(remineralizationRate) || float.IsInfinity(remineralizationRate)) remineralizationRate = 0f;

            surfaceProductivity = Math.Max(0f, surfaceProductivity);
            depthMeters = Math.Max(0f, depthMeters);
            sinkingSpeedMPerDay = Math.Max(0.0001f, sinkingSpeedMPerDay);
            remineralizationRate = Math.Max(0f, remineralizationRate);

            if (surfaceProductivity <= 0f) return 0f;

            float z_ref = sinkingSpeedMPerDay;
            float b = remineralizationRate;
            float normalizedDepth = (depthMeters + z_ref) / z_ref;

            // F(z) = F_surf * ((z + z_ref) / z_ref)^-b
            // This Martin curve approach yields:
            // Surface (0m) = productivity
            // Sinking deeper attenuates the flux, bounded by physically valid constraints.
            float flux = surfaceProductivity * (float)Math.Pow(normalizedDepth, -b);

            return flux;
        }
    }
}
