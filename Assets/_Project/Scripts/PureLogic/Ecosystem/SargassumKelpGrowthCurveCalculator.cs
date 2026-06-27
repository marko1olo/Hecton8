using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for SargassumKelpGrowthCurveCalculator.
    /// Extracted from WorldProceduralScatterDirectorMigratorySargassum.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SargassumKelpGrowthCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentSize">Parameter representing the currentSize (float).</param>
        /// <param name="maxClusterSize">Parameter representing the maxClusterSize (float).</param>
        /// <param name="growthRate">Parameter representing the growthRate (float).</param>
        /// <param name="deltaHours">Parameter representing the deltaHours (float).</param>
        /// <returns>Returns New cluster size of type float.</returns>
        public static float Compute(float currentSize, float maxClusterSize, float growthRate, float deltaHours)
        {
            // Edge cases
            if (float.IsNaN(currentSize) || float.IsInfinity(currentSize)) currentSize = 0f;
            if (float.IsNaN(maxClusterSize) || float.IsInfinity(maxClusterSize)) maxClusterSize = 0f;
            if (float.IsNaN(growthRate)) growthRate = 0f;
            if (float.IsNaN(deltaHours) || float.IsInfinity(deltaHours)) deltaHours = 0f;

            if (float.IsPositiveInfinity(growthRate))
            {
                return Math.Min(Math.Max(0f, maxClusterSize), maxClusterSize); // Effectively returns maxClusterSize or 0
            }

            if (float.IsNegativeInfinity(growthRate))
            {
                 growthRate = 0f;
            }

            // Clamping negatives
            currentSize = Math.Max(0f, currentSize);
            maxClusterSize = Math.Max(0f, maxClusterSize);
            growthRate = Math.Max(0f, growthRate);
            deltaHours = Math.Max(0f, deltaHours);

            if (maxClusterSize == 0f || currentSize >= maxClusterSize)
                return Math.Min(currentSize, maxClusterSize);

            // Logistic differential curve: dS/dt = r * S * (1 - S/K)
            // Exact solution: S(t) = K / (1 + ((K - S0) / S0) * e^(-rt))

            if (currentSize == 0f)
                return 0f;

            double K = maxClusterSize;
            double S0 = currentSize;
            double r = growthRate;
            double t = deltaHours;

            double exponent = -r * t;
            // Prevent exponent from causing overflow or underflow issues
            if (exponent > 100) return (float)S0; // No change essentially if negative growth but we clamped it
            if (exponent < -100) return (float)K;

            double expPart = Math.Exp(exponent);

            double denominator = 1.0 + ((K - S0) / S0) * expPart;

            if (double.IsNaN(denominator) || double.IsInfinity(denominator) || denominator == 0.0)
                return (float)K;

            double newSize = K / denominator;

            if (double.IsNaN(newSize) || double.IsInfinity(newSize))
                return (float)K;

            float finalSize = (float)newSize;
            return Math.Min(finalSize, maxClusterSize);
        }
    }
}
