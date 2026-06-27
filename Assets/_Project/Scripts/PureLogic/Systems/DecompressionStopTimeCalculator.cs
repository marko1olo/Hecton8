using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DecompressionStopTimeCalculator.
    /// Extracted from GasDynamicsSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DecompressionStopTimeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="maxDepthReached">Parameter representing the maxDepthReached (float).</param>
        /// <param name="timeAtDepthMin">Parameter representing the timeAtDepthMin (float).</param>
        /// <param name="ascentRate">Parameter representing the ascentRate (float).</param>
        /// <param name="stopDepthMeters">Parameter representing the stopDepthMeters (float).</param>
        /// <returns>Returns requiredStopTimeMinutes of type float.</returns>
        public static float Compute(float maxDepthReached, float timeAtDepthMin, float ascentRate, float stopDepthMeters)
        {
            if (float.IsNaN(maxDepthReached) || float.IsInfinity(maxDepthReached) ||
                float.IsNaN(timeAtDepthMin) || float.IsInfinity(timeAtDepthMin) ||
                float.IsNaN(ascentRate) || float.IsInfinity(ascentRate) ||
                float.IsNaN(stopDepthMeters) || float.IsInfinity(stopDepthMeters))
            {
                return 0f;
            }

            float safeMaxDepthReached = Math.Max(0f, maxDepthReached);
            float safeTimeAtDepthMin = Math.Max(0f, timeAtDepthMin);
            float safeAscentRate = Math.Max(0f, ascentRate);
            float safeStopDepthMeters = Math.Max(0f, stopDepthMeters);

            float rawStop = (safeMaxDepthReached * safeTimeAtDepthMin * safeAscentRate) - safeStopDepthMeters;

            if (float.IsNaN(rawStop) || float.IsInfinity(rawStop))
            {
                return 0f;
            }

            return Math.Max(0f, rawStop);
        }
    }
}
