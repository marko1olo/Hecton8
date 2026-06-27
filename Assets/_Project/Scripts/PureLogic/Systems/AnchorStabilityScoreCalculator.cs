using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AnchorStabilityScoreCalculator.
    /// Extracted from ConstructionManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AnchorStabilityScoreCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="contactAreaM2">Parameter representing the contactAreaM2 (float).</param>
        /// <param name="terrainSlopeAngleDeg">Parameter representing the terrainSlopeAngleDeg (float).</param>
        /// <param name="maxStableSlope">Parameter representing the maxStableSlope (float).</param>
        /// <param name="foundationStrength">Parameter representing the foundationStrength (float).</param>
        /// <returns>Returns stabilityScore 0.0-1.0 of type float.</returns>
        public static float Compute(float contactAreaM2, float terrainSlopeAngleDeg, float maxStableSlope, float foundationStrength)
        {
            if (float.IsNaN(contactAreaM2) || float.IsInfinity(contactAreaM2)) contactAreaM2 = 0f;
            if (float.IsNaN(terrainSlopeAngleDeg) || float.IsInfinity(terrainSlopeAngleDeg)) terrainSlopeAngleDeg = 0f;
            if (float.IsNaN(maxStableSlope) || float.IsInfinity(maxStableSlope)) maxStableSlope = 0f;
            if (float.IsNaN(foundationStrength) || float.IsInfinity(foundationStrength)) foundationStrength = 0f;

            float safeContactArea = Math.Max(0f, contactAreaM2);
            float safeSlopeAngle = Math.Max(0f, terrainSlopeAngleDeg);
            float safeMaxSlope = Math.Max(0f, maxStableSlope);
            float safeFoundationStrength = Math.Max(0f, foundationStrength);

            float slopeFactor = 1f;
            if (safeMaxSlope > 0f)
            {
                float slopePenalty = safeSlopeAngle / safeMaxSlope;
                slopeFactor = Math.Max(0f, 1f - slopePenalty);
            }
            else
            {
                slopeFactor = (safeSlopeAngle <= 0f) ? 1f : 0f;
            }

            float baseStability = safeContactArea * safeFoundationStrength;

            // Clamp base stability to 1.0f before multiplying by slope factor
            float clampedBaseStability = Math.Min(1f, baseStability);

            float finalStability = clampedBaseStability * slopeFactor;

            // Final safety clamp
            return Math.Clamp(finalStability, 0f, 1f);
        }
    }
}
