using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DepthGaugeNonlinearCalculator.
    /// Extracted from VisorHUDController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DepthGaugeNonlinearCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="maxDisplayDepth">Parameter representing the maxDisplayDepth (float).</param>
        /// <param name="minAngleDeg">Parameter representing the minAngleDeg (float).</param>
        /// <param name="maxAngleDeg">Parameter representing the maxAngleDeg (float).</param>
        /// <returns>Returns needleAngleDeg of type float.</returns>
        public static float Compute(float depthMeters, float maxDisplayDepth, float minAngleDeg, float maxAngleDeg)
        {
            if (maxDisplayDepth <= 0f) return minAngleDeg;

            float clampedDepth = Math.Clamp(depthMeters, 0f, maxDisplayDepth);
            if (clampedDepth <= 0f) return minAngleDeg;

            // Log scale: first 10m uses more arc than 10m at 500m.
            float logDepth = (float)Math.Log10(1.0 + clampedDepth);
            float logMaxDepth = (float)Math.Log10(1.0 + maxDisplayDepth);

            float t = logDepth / logMaxDepth;

            if (float.IsNaN(t) || float.IsInfinity(t)) return minAngleDeg;

            t = Math.Clamp(t, 0f, 1f);

            return minAngleDeg + t * (maxAngleDeg - minAngleDeg);
        }
    }
}
