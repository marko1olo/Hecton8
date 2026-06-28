using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for PressureCrushDamageModel.
    /// Extracted from HydrodynamicKccRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PressureCrushDamageModel
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='depthMeters'>Parameter representing the depthMeters (float).</param>
        /// <param name='crushDepthThreshold'>Parameter representing the crushDepthThreshold (float).</param>
        /// <param name='maxDamageRate'>Parameter representing the maxDamageRate (float).</param>
        /// <param name='exponent'>Parameter representing the exponent (float).</param>
        /// <returns>Returns damagePerSecond of type float.</returns>
        public static float Evaluate(float depthMeters, float crushDepthThreshold, float maxDamageRate, float exponent)
        {
            if (float.IsNaN(depthMeters) || float.IsNaN(crushDepthThreshold) || float.IsNaN(maxDamageRate) || float.IsNaN(exponent))
                return 0f;
            if (crushDepthThreshold <= 0f) return 0f; // Prevent division by zero or invalid thresholds.

            float safeDepth = Math.Max(0f, depthMeters);
            float safeThreshold = Math.Max(0.0001f, crushDepthThreshold);

            if (safeDepth < safeThreshold)
                return 0f;

            float safeExponent = Math.Max(1f, exponent);
            float safeMaxDamage = Math.Max(0f, maxDamageRate);

            // Exponential spike below crush depth
            float depthRatio = safeDepth / safeThreshold;
            float rawDamage = (float)Math.Pow(depthRatio, safeExponent) - 1f;

            // At threshold (ratio = 1.0), Pow(1, e) - 1 = 0.
            // Double depth (ratio = 2.0), Pow(2, e) - 1. If e=2, 3.

            float scaledDamage = rawDamage * safeMaxDamage;

            if (float.IsInfinity(scaledDamage) || float.IsNaN(scaledDamage))
                return float.MaxValue;

            return Math.Max(0f, scaledDamage);
        }
    }
}
