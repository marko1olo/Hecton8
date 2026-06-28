using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PitchShiftResampleCalculator.
    /// Extracted from DynamicMusicGranularSynthesizer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PitchShiftResampleCalculator
    {
        private const float MinSemitones = -120f;
        private const float MaxSemitones = 120f;
        private const float MinSampleRate = 0f;
        private const float MaxSampleRate = 384000f; // up to 384 kHz
        private const float SemitonesPerOctave = 12f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="semitones">Parameter representing the semitones (float).</param>
        /// <param name="originalSampleRate">Parameter representing the originalSampleRate (float).</param>
        /// <returns>Returns resampleRatio, float (newSampleRate) of type float.</returns>
        public static float Compute(float semitones, float originalSampleRate)
        {
            if (float.IsNaN(semitones) || float.IsInfinity(semitones))
            {
                semitones = 0f;
            }

            if (float.IsNaN(originalSampleRate) || float.IsInfinity(originalSampleRate))
            {
                originalSampleRate = 0f;
            }

            float clampedSemitones = Math.Clamp(semitones, MinSemitones, MaxSemitones);
            float clampedSampleRate = Math.Clamp(originalSampleRate, MinSampleRate, MaxSampleRate);

            if (clampedSampleRate <= 0f)
            {
                return 0f;
            }

            float pitchShiftRatio = MathF.Pow(2f, clampedSemitones / SemitonesPerOctave);
            float newSampleRate = clampedSampleRate * pitchShiftRatio;

            if (float.IsNaN(newSampleRate) || float.IsInfinity(newSampleRate))
            {
                return 0f;
            }

            // Return the clamped final value to avoid arbitrary blowups.
            // Theoretically the max return value could be 384000 * 2^10 = 393,216,000
            // but we constrain it reasonably within valid float limits just by natural math
            // and avoiding non-finites. We'll return it as is since physics max limits are handled
            // depending on caller logic. If a max resample rate is strictly required, clamp it.
            // But usually the ratio applies up/down.
            return Math.Max(0f, newSampleRate);
        }
    }
}
