using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for EcholocationRangeCalculator.
    /// Extracted from AcousticEcholocationTranslator.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class EcholocationRangeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="emittedSoundPressure">Parameter representing the emittedSoundPressure (float).</param>
        /// <param name="ambientNoiseLevel">Parameter representing the ambientNoiseLevel (float).</param>
        /// <param name="targetReflectivity">Parameter representing the targetReflectivity (float).</param>
        /// <param name="soundAttenuationPerMeter">Parameter representing the soundAttenuationPerMeter (float).</param>
        /// <returns>Returns detectionRangeMeters of type float.</returns>
        public static float Compute(float emittedSoundPressure, float ambientNoiseLevel, float targetReflectivity, float soundAttenuationPerMeter)
        {
            if (float.IsNaN(emittedSoundPressure) || float.IsInfinity(emittedSoundPressure) ||
                float.IsNaN(ambientNoiseLevel) || float.IsInfinity(ambientNoiseLevel) ||
                float.IsNaN(targetReflectivity) || float.IsInfinity(targetReflectivity) ||
                float.IsNaN(soundAttenuationPerMeter) || float.IsInfinity(soundAttenuationPerMeter))
            {
                return 0f;
            }

            // Clamp negative inputs to 0 where appropriate
            float clampedPressure = Math.Max(0f, emittedSoundPressure);
            float clampedNoise = Math.Max(0f, ambientNoiseLevel);
            // Reflectivity can technically be 0 or small, assuming it's a multiplier >= 0
            float clampedReflectivity = Math.Max(0f, targetReflectivity);
            // Attenuation must be strictly positive to avoid division by zero or infinite range
            float clampedAttenuation = Math.Max(0.0001f, soundAttenuationPerMeter);

            // Base signal at target = pressure * reflectivity
            // Detected signal = (signal - noise) / attenuation

            float effectiveSignal = clampedPressure * clampedReflectivity;
            float signalToNoise = effectiveSignal - clampedNoise;

            // if noise is higher than signal, detection range is 0
            if (signalToNoise <= 0f)
            {
                return 0f;
            }

            float range = signalToNoise / clampedAttenuation;

            // Ensure no infinity / clamp negative (already guarded, but double check)
            if (float.IsInfinity(range) || float.IsNaN(range))
            {
                return 0f;
            }

            return Math.Max(0f, range);
        }
    }
}
