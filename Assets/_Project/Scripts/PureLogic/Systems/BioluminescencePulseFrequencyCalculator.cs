using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BioluminescencePulseFrequencyCalculator.
    /// Extracted from HectonBiolumManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BioluminescencePulseFrequencyCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="creatureStressLevel01">Parameter representing the creatureStressLevel01 (float).</param>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="baseFrequencyHz">Parameter representing the baseFrequencyHz (float).</param>
        /// <param name="stressFrequencyMultiplier">Parameter representing the stressFrequencyMultiplier (float).</param>
        /// <param name="depthFrequencyMultiplier">Parameter representing the depthFrequencyMultiplier (float).</param>
        /// <returns>Returns pulseFrequencyHz of type float.</returns>
        public static float Compute(float creatureStressLevel01, float depthMeters, float baseFrequencyHz, float stressFrequencyMultiplier, float depthFrequencyMultiplier)
        {
            // Parameter validation
            float validStress = SanitizeFinite(creatureStressLevel01, 0f);
            float validDepth = SanitizeFinite(depthMeters, 0f);
            float validBaseFreq = SanitizeFinite(baseFrequencyHz, 0f);
            float validStressMult = SanitizeFinite(stressFrequencyMultiplier, 1f);
            float validDepthMult = SanitizeFinite(depthFrequencyMultiplier, 0f);

            // Constraint guarding
            validStress = Clamp(validStress, 0f, 1f);
            validDepth = Math.Max(0f, validDepth);
            validBaseFreq = Math.Max(0f, validBaseFreq);
            validStressMult = Math.Max(0f, validStressMult);
            validDepthMult = Math.Max(0f, validDepthMult);

            // Business Logic:
            // base * (1 + stress * (multiplier - 1)) + depth * depthMult
            // e.g. "No stress, surface: base. Max stress: multiplied. Deep adds further boost."
            float stressFactor = validStressMult; // By default 1 or higher. Let's do lerp: 1 + stress * (mult - 1)
            float effectiveStressMultiplier = 1f + validStress * (validStressMult - 1f);

            float stressAppliedFreq = validBaseFreq * Math.Max(0f, effectiveStressMultiplier);
            float depthBoost = validDepth * validDepthMult;

            float rawFrequency = stressAppliedFreq + depthBoost;

            // Final output bounds
            return Math.Max(0f, rawFrequency);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
