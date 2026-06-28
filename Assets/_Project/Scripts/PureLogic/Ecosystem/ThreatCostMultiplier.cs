using System;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for ThreatCostMultiplier.
    /// Extracted from ThreatCostTable.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ThreatCostMultiplier
    {
        public const float FreezingTemperatureThreshold = 0f;
        public const float ExtremeDepthThreshold = 500f; // Represents extreme depths reducing cost

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseCost">Parameter representing the baseCost (float).</param>
        /// <param name="temperatureCelsius">Parameter representing the temperatureCelsius (float).</param>
        /// <param name="depth">Parameter representing the depth (float).</param>
        /// <returns>Returns Modified threat cost of type float.</returns>
        public static float Calculate(float baseCost, float temperatureCelsius, float depth)
        {
            // Edge Cases: NaN/Infinity
            if (float.IsNaN(baseCost) || float.IsInfinity(baseCost)) return 0f;
            if (float.IsNaN(temperatureCelsius) || float.IsInfinity(temperatureCelsius)) temperatureCelsius = 20f; // default safe temp
            if (float.IsNaN(depth) || float.IsInfinity(depth)) depth = 0f;

            // Parameter Validation: Clamp baseCost to prevent negative costs
            baseCost = Math.Max(0f, baseCost);
            depth = Math.Max(0f, depth);

            // Business Logic
            float tempMultiplier = 1.0f;
            if (temperatureCelsius < FreezingTemperatureThreshold)
            {
                // Increase threat cost as it gets colder below freezing
                // E.g. -10C -> tempMultiplier = 1 + (10 * 0.05) = 1.5
                float coldDelta = Math.Abs(temperatureCelsius);
                tempMultiplier += coldDelta * 0.05f;
            }

            float depthMultiplier = 1.0f;
            if (depth > ExtremeDepthThreshold)
            {
                // Reduce cost at extreme depths
                // E.g. 1000 depth -> 1000 / 500 = 2.0. Scale = 1 / 2.0 = 0.5
                // We clamp scale to ensure it doesn't go below 0.1 for gameplay sanity.
                float depthFactor = depth / ExtremeDepthThreshold;
                depthMultiplier = Math.Max(0.1f, 1.0f / depthFactor);
            }

            float finalCost = baseCost * tempMultiplier * depthMultiplier;

            // Boundary Guarding: Clamp to positive max value
            finalCost = Math.Max(0f, Math.Min(finalCost, 1000000f));

            return finalCost;
        }
    }
}
