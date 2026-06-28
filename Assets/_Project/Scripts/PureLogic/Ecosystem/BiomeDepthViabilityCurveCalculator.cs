using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for BiomeDepthViabilityCurveCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BiomeDepthViabilityCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDepth">Parameter representing the currentDepth (float).</param>
        /// <param name="targetOptimalDepth">Parameter representing the targetOptimalDepth (float).</param>
        /// <param name="depthTolerance">Parameter representing the depthTolerance (float).</param>
        /// <returns>Returns Suitability multiplier 0.0 to 1.0 of type float.</returns>
        public static float Compute(float currentDepth, float targetOptimalDepth, float depthTolerance)
        {
            if (float.IsNaN(currentDepth) || float.IsInfinity(currentDepth) ||
                float.IsNaN(targetOptimalDepth) || float.IsInfinity(targetOptimalDepth) ||
                float.IsNaN(depthTolerance) || float.IsInfinity(depthTolerance))
            {
                return 0f;
            }

            if (currentDepth < 0f) currentDepth = 0f;
            if (targetOptimalDepth < 0f) targetOptimalDepth = 0f;
            if (depthTolerance <= 0f) depthTolerance = 0.0001f;

            float difference = currentDepth - targetOptimalDepth;
            float exponent = -(difference * difference) / (2f * depthTolerance * depthTolerance);

            float result = (float)Math.Exp(exponent);

            if (result < 0.1f && Math.Abs(difference) > depthTolerance)
            {
                return 0f;
            }

            return Math.Clamp(result, 0f, 1f);
        }
    }
}
