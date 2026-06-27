using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for UnderwaterFogDensityCalculator.
    /// Extracted from HectonUnderwaterVisuals.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class UnderwaterFogDensityCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="biomeType">Parameter representing the biomeType (string).</param>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="baseFogDensity">Parameter representing the baseFogDensity (float).</param>
        /// <param name="particulateLevel">Parameter representing the particulateLevel (float).</param>
        /// <returns>Returns fogDensity of type float.</returns>
        public static float Compute(string biomeType, float depthMeters, float baseFogDensity, float particulateLevel)
        {
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters)) depthMeters = 0f;
            if (float.IsNaN(baseFogDensity) || float.IsInfinity(baseFogDensity)) baseFogDensity = 0f;
            if (float.IsNaN(particulateLevel) || float.IsInfinity(particulateLevel)) particulateLevel = 0f;

            depthMeters = Math.Max(0f, depthMeters);
            baseFogDensity = Math.Max(0f, baseFogDensity);
            particulateLevel = Math.Max(0f, particulateLevel);

            float density = baseFogDensity * particulateLevel;

            if (!string.IsNullOrEmpty(biomeType)) {
                if (biomeType.IndexOf("kelp", StringComparison.OrdinalIgnoreCase) >= 0 || biomeType.IndexOf("forest", StringComparison.OrdinalIgnoreCase) >= 0) {
                    density *= 2.5f;
                } else if (biomeType.IndexOf("brine", StringComparison.OrdinalIgnoreCase) >= 0 || biomeType.IndexOf("pool", StringComparison.OrdinalIgnoreCase) >= 0) {
                    density *= 15.0f;
                } else if (biomeType.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0 || biomeType.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0 || biomeType.IndexOf("shallow", StringComparison.OrdinalIgnoreCase) >= 0) {
                    density *= 1.0f;
                }
            }

            // apply a multiplier based on depth
            float depthScale = 1.0f + (depthMeters * 0.01f);
            density *= depthScale;

            // Clamp maximum density to a realistic range
            density = Math.Min(density, 1.0f);

            return density;
        }
    }
}
