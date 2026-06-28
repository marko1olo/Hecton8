using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AudioDistanceAttenuationCurveCalculator.
    /// Extracted from SpatialAudioManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AudioDistanceAttenuationCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='initialDb'>Parameter representing the initialDb (float).</param>
        /// <param name='distance'>Parameter representing the distance (float).</param>
        /// <param name='absorptionRateDbPerMeter'>Parameter representing the absorptionRateDbPerMeter (float).</param>
        /// <returns>Returns Resulting volume Db of type float.</returns>
        public static float Compute(float initialDb, float distance, float absorptionRateDbPerMeter)
        {
            if (float.IsNaN(initialDb) || float.IsInfinity(initialDb)) return 0f;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return 0f;
            if (float.IsNaN(absorptionRateDbPerMeter) || float.IsInfinity(absorptionRateDbPerMeter)) return 0f;

            if (distance <= 0f) return initialDb;

            float safeDistance = distance < 0f ? 0f : distance;

            // Formula for decibel attenuation over distance:
            // Attenuation = 20 * log10(distance) + absorptionRate * distance
            // Resulting Db = initialDb - Attenuation

            float attenuation = 20f * (float)Math.Log10(Math.Max(1f, safeDistance)) + absorptionRateDbPerMeter * safeDistance;

            return initialDb - attenuation;
        }
    }
}
