using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for StormWaveHeightBeaufortCalculator.
    /// Extracted from HectonSurfaceWeatherDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StormWaveHeightBeaufortCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='beaufortNumber'>Parameter representing the beaufortNumber (float).</param>
        /// <param name='fetchDistanceKm'>Parameter representing the fetchDistanceKm (float).</param>
        /// <param name='windDurationHours'>Parameter representing the windDurationHours (float).</param>
        /// <returns>Returns significantWaveHeightMeters, float (dominantPeriodSeconds) of type float.</returns>
        public static float Compute(
            float beaufortNumber,
            float fetchDistanceKm,
            float windDurationHours,
            float minBeaufort = 0f,
            float maxBeaufort = 12f,
            float minFetch = 0f,
            float minDuration = 0f,
            float beaufortWindSpeedCoeff = 0.836f,
            float beaufortWindSpeedPower = 1.5f,
            float fullyDevelopedCoeff = 0.22f,
            float gravity = 9.81f,
            float fetchEffectCoeff = 0.01f,
            float durationEffectCoeff = 0.1f,
            float oneConstant = 1f,
            float zeroConstant = 0f)
        {
            if (float.IsNaN(beaufortNumber) || float.IsInfinity(beaufortNumber)) beaufortNumber = zeroConstant;
            if (float.IsNaN(fetchDistanceKm) || float.IsInfinity(fetchDistanceKm)) fetchDistanceKm = zeroConstant;
            if (float.IsNaN(windDurationHours) || float.IsInfinity(windDurationHours)) windDurationHours = zeroConstant;

            float clampedBeaufort = Math.Max(minBeaufort, Math.Min(maxBeaufort, beaufortNumber));
            float clampedFetch = Math.Max(minFetch, fetchDistanceKm);
            float clampedDuration = Math.Max(minDuration, windDurationHours);

            if (clampedBeaufort <= zeroConstant)
            {
                return zeroConstant;
            }

            float windSpeed = beaufortWindSpeedCoeff * (float)Math.Pow(clampedBeaufort, beaufortWindSpeedPower);
            float validGravity = gravity != zeroConstant ? gravity : oneConstant;

            float maxWaveHeight = fullyDevelopedCoeff * windSpeed * windSpeed / validGravity;

            float fetchFactor = oneConstant - (float)Math.Exp(-fetchEffectCoeff * clampedFetch);
            float durationFactor = oneConstant - (float)Math.Exp(-durationEffectCoeff * clampedDuration);

            return Math.Max(zeroConstant, maxWaveHeight * fetchFactor * durationFactor);
        }
    }
}
