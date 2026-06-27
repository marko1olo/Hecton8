using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for GameStateTensionScorer.
    /// Extracted from HectonMusicDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class GameStateTensionScorer
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='threatLevel01'>Parameter representing the threatLevel01 (float).</param>
        /// <param name='depthNormalized01'>Parameter representing the depthNormalized01 (float).</param>
        /// <param name='oxygenRemaining01'>Parameter representing the oxygenRemaining01 (float).</param>
        /// <param name='playerHP01'>Parameter representing the playerHP01 (float).</param>
        /// <returns>Returns tensionScore 0.0-1.0 of type float.</returns>
        public static float Calculate(float threatLevel01, float depthNormalized01, float oxygenRemaining01, float playerHP01)
        {
            float zero = 0f;
            float one = 1f;

            // Parameter Validation: Guard against NaN and Infinity
            float safeThreat = float.IsNaN(threatLevel01) || float.IsInfinity(threatLevel01) ? zero : threatLevel01;
            float safeDepth = float.IsNaN(depthNormalized01) || float.IsInfinity(depthNormalized01) ? zero : depthNormalized01;
            float safeOxygen = float.IsNaN(oxygenRemaining01) || float.IsInfinity(oxygenRemaining01) ? one : oxygenRemaining01;
            float safeHP = float.IsNaN(playerHP01) || float.IsInfinity(playerHP01) ? one : playerHP01;

            // Business Logic: Clamp values
            float clampedThreat = Math.Max(zero, Math.Min(one, safeThreat));
            float clampedDepth = Math.Max(zero, Math.Min(one, safeDepth));
            float clampedOxygen = Math.Max(zero, Math.Min(one, safeOxygen));
            float clampedHP = Math.Max(zero, Math.Min(one, safeHP));

            // Weighted aggregate.
            float sum = clampedThreat + clampedDepth + (one - clampedOxygen) + (one - clampedHP);
            float factorCount = one + one + one + one;
            float rawTension = sum / factorCount;

            // Boundary Guarding
            return Math.Max(zero, Math.Min(one, rawTension));
        }
    }
}
