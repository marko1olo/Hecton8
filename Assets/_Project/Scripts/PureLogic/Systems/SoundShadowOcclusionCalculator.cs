using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SoundShadowOcclusionCalculator.
    /// Extracted from AcousticEcholocationTranslator.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SoundShadowOcclusionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="obstacleSize">Parameter representing the obstacleSize (float).</param>
        /// <param name="distanceToObstacle">Parameter representing the distanceToObstacle (float).</param>
        /// <param name="sourceDistance">Parameter representing the sourceDistance (float).</param>
        /// <param name="soundFrequencyHz">Parameter representing the soundFrequencyHz (float).</param>
        /// <returns>Returns occlusionFactor 0.0-1.0 of type float.</returns>
        public static float Compute(float obstacleSize, float distanceToObstacle, float sourceDistance, float soundFrequencyHz)
        {
            if (float.IsNaN(obstacleSize) || float.IsNaN(distanceToObstacle) || float.IsNaN(sourceDistance) || float.IsNaN(soundFrequencyHz))
            {
                return 0f;
            }

            if (obstacleSize <= 0f || soundFrequencyHz <= 0f || distanceToObstacle <= 0f || sourceDistance <= 0f)
            {
                return 0f;
            }

            if (distanceToObstacle >= sourceDistance)
            {
                return 0f;
            }

            if (float.IsPositiveInfinity(obstacleSize) || float.IsPositiveInfinity(soundFrequencyHz))
            {
                return 1f;
            }

            // Speed of sound in water is approx 1500 m/s
            float waveLength = 1500f / soundFrequencyHz;

            // Fresnel number approximation for diffraction
            float fresnelNumber = (obstacleSize * obstacleSize) / (waveLength * distanceToObstacle);

            // Scale to an occlusion factor (heuristic mapping)
            float occlusion = fresnelNumber * 0.1f;

            // Distance falloff from obstacle to source (if obstacle is very far from source, occlusion might drop)
            float distanceRatio = distanceToObstacle / sourceDistance;
            occlusion *= (1f - distanceRatio);

            if (float.IsNaN(occlusion) || float.IsInfinity(occlusion))
            {
                return 0f;
            }

            if (occlusion < 0f) return 0f;
            if (occlusion > 1f) return 1f;

            return occlusion;
        }
    }
}
