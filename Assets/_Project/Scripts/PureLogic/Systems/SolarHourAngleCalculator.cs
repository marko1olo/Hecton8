using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SolarHourAngleCalculator.
    /// Extracted from HectonCelestialEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SolarHourAngleCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="worldTimeSeconds">Parameter representing the worldTimeSeconds (float).</param>
        /// <param name="dayLengthSeconds">Parameter representing the dayLengthSeconds (float).</param>
        /// <param name="latitude">Parameter representing the latitude (float).</param>
        /// <param name="axialTilt">Parameter representing the axialTilt (float).</param>
        /// <returns>Returns sunElevationDeg of type float.</returns>
        public static float Compute(float worldTimeSeconds, float dayLengthSeconds, float latitude, float axialTilt)
        {
            if (float.IsNaN(worldTimeSeconds) || float.IsInfinity(worldTimeSeconds) ||
                float.IsNaN(dayLengthSeconds) || float.IsInfinity(dayLengthSeconds) ||
                float.IsNaN(latitude) || float.IsInfinity(latitude) ||
                float.IsNaN(axialTilt) || float.IsInfinity(axialTilt))
            {
                return 0f;
            }

            if (dayLengthSeconds <= 0f)
            {
                return 0f;
            }

            // Calculate fraction of the day. Assume worldTimeSeconds = 0 is midnight.
            double fraction = (worldTimeSeconds % dayLengthSeconds) / dayLengthSeconds;
            if (fraction < 0.0)
            {
                fraction += 1.0;
            }

            // Noon is at fraction = 0.5. At noon, hour angle is 0.
            double hourAngleDeg = (fraction - 0.5) * 360.0;

            double latRad = latitude * Math.PI / 180.0;
            double declRad = axialTilt * Math.PI / 180.0;
            double haRad = hourAngleDeg * Math.PI / 180.0;

            double sinElevation = Math.Sin(latRad) * Math.Sin(declRad) + Math.Cos(latRad) * Math.Cos(declRad) * Math.Cos(haRad);

            if (sinElevation > 1.0)
            {
                sinElevation = 1.0;
            }
            else if (sinElevation < -1.0)
            {
                sinElevation = -1.0;
            }

            double elevationRad = Math.Asin(sinElevation);
            float sunElevationDeg = (float)(elevationRad * 180.0 / Math.PI);

            return sunElevationDeg;
        }
    }
}
