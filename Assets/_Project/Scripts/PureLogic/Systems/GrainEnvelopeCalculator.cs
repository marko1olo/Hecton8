using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for GrainEnvelopeCalculator.
    /// Extracted from DynamicMusicGranularSynthesizer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class GrainEnvelopeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="grainPositionNormalized">Parameter representing the grainPositionNormalized (float).</param>
        /// <param name="attackFraction">Parameter representing the attackFraction (float).</param>
        /// <param name="decayFraction">Parameter representing the decayFraction (float).</param>
        /// <returns>Returns envelopeAmplitude 0.0-1.0 of type float.</returns>
        public static float Compute(float grainPositionNormalized, float attackFraction, float decayFraction)
        {
            if (float.IsNaN(grainPositionNormalized) || float.IsInfinity(grainPositionNormalized)) return 0f;
            // NaN fractions are unordered garbage and mean silence. Infinite fractions are
            // just out-of-range magnitudes and take the same [0,1] clamp below as any other
            // oversized value — rejecting +Inf while clamping 5.0 to 1 was inconsistent.
            if (float.IsNaN(attackFraction) || float.IsNaN(decayFraction)) return 0f;

            float position = grainPositionNormalized;
            if (position <= 0f || position >= 1f) return 0f;

            float attack = attackFraction < 0f ? 0f : (attackFraction > 1f ? 1f : attackFraction);
            float decay = decayFraction < 0f ? 0f : (decayFraction > 1f ? 1f : decayFraction);

            float totalFractions = attack + decay;
            if (totalFractions > 1f)
            {
                attack /= totalFractions;
                decay /= totalFractions;
            }

            float amplitude = 1f;

            if (attack > 0f && position < attack)
            {
                float phase = (position / attack) * (float)Math.PI;
                amplitude = 0.5f * (1f - (float)Math.Cos(phase));
            }
            else if (decay > 0f && position > 1f - decay)
            {
                float phase = (float)Math.PI + ((position - (1f - decay)) / decay) * (float)Math.PI;
                amplitude = 0.5f * (1f - (float)Math.Cos(phase));
            }
            else if (attack == 0f && position == 0f)
            {
                amplitude = 0f;
            }
            else if (decay == 0f && position == 1f)
            {
                amplitude = 0f;
            }

            return amplitude < 0f ? 0f : (amplitude > 1f ? 1f : amplitude);
        }
    }
}
