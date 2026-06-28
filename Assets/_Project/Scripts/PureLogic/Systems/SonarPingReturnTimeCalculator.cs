using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SonarPingReturnTimeCalculator.
    /// Extracted from ScannerTool.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SonarPingReturnTimeCalculator
    {
        /// <summary>
        /// Computes the sonar ping return time and doppler shifted frequency.
        /// </summary>
        /// <param name="distanceMeters">The distance to the target in meters.</param>
        /// <param name="soundSpeedMps">The base speed of sound in meters per second.</param>
        /// <param name="pingFrequencyHz">The frequency of the ping in Hertz.</param>
        /// <param name="waterTemperature">The water temperature in Celsius.</param>
        /// <param name="targetRadialVelocityMps">The radial velocity of the target (positive is moving away).</param>
        /// <param name="tempCoefficient">The coefficient modifying sound speed based on temperature.</param>
        /// <param name="minSoundSpeedMps">The minimum allowable speed of sound.</param>
        /// <param name="maxSoundSpeedMps">The maximum allowable speed of sound.</param>
        /// <returns>A tuple containing (returnTimeSeconds, dopplerShiftedFrequencyHz).</returns>
        public static (float returnTimeSeconds, float dopplerShiftedFrequencyHz) Compute(
            float distanceMeters,
            float soundSpeedMps,
            float pingFrequencyHz,
            float waterTemperature,
            float targetRadialVelocityMps,
            float tempCoefficient,
            float minSoundSpeedMps,
            float maxSoundSpeedMps)
        {
            if (float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters) ||
                float.IsNaN(soundSpeedMps) || float.IsInfinity(soundSpeedMps) ||
                float.IsNaN(pingFrequencyHz) || float.IsInfinity(pingFrequencyHz) ||
                float.IsNaN(waterTemperature) || float.IsInfinity(waterTemperature) ||
                float.IsNaN(targetRadialVelocityMps) || float.IsInfinity(targetRadialVelocityMps) ||
                float.IsNaN(tempCoefficient) || float.IsInfinity(tempCoefficient) ||
                float.IsNaN(minSoundSpeedMps) || float.IsInfinity(minSoundSpeedMps) ||
                float.IsNaN(maxSoundSpeedMps) || float.IsInfinity(maxSoundSpeedMps))
            {
                return (0f, 0f);
            }

            if (distanceMeters < 0f)
            {
                distanceMeters = 0f;
            }

            float adjustedSpeed = soundSpeedMps + (waterTemperature * tempCoefficient);

            float clampedSpeed = adjustedSpeed;
            if (clampedSpeed < minSoundSpeedMps) clampedSpeed = minSoundSpeedMps;
            if (clampedSpeed > maxSoundSpeedMps) clampedSpeed = maxSoundSpeedMps;

            float returnTimeSeconds = 0f;
            if (clampedSpeed > 0f)
            {
                returnTimeSeconds = (distanceMeters * 2f) / clampedSpeed;
            }

            float dopplerShiftedFrequencyHz = 0f;
            float denominator = clampedSpeed + targetRadialVelocityMps;

            if (denominator != 0f && clampedSpeed > 0f)
            {
                dopplerShiftedFrequencyHz = pingFrequencyHz * (clampedSpeed / denominator);
            }

            if (dopplerShiftedFrequencyHz < 0f)
            {
                dopplerShiftedFrequencyHz = 0f;
            }

            return (returnTimeSeconds, dopplerShiftedFrequencyHz);
        }
    }
}
