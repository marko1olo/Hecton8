using System;

namespace Hecton8.PureLogic.Systems
{
    public static class AmbientTemperatureDepthGradientCalculator
    {
        public static float Compute(
            float surfaceTemp,
            float depth,
            float latitudeDegrees,
            float maxLatitude = 90f,
            float deepSeaEquatorTemp = 2.0f,
            float deepSeaPoleTemp = -2.0f,
            float gradientCoefficient = 0.01f)
        {
            if (float.IsNaN(surfaceTemp) || float.IsInfinity(surfaceTemp)) return 0f;
            if (float.IsNaN(depth) || float.IsInfinity(depth)) return surfaceTemp;
            if (float.IsNaN(latitudeDegrees) || float.IsInfinity(latitudeDegrees)) latitudeDegrees = 0f;

            depth = Math.Max(0f, depth);

            // maxLatitude is a divisor and a clamp bound, so it needs the same validation as
            // the state inputs above. Previously maxLatitude == 0 evaluated 0f/0f and returned
            // NaN, and a negative maxLatitude produced inverted bounds that made Math.Clamp
            // throw ArgumentException from inside this allocation-free calculator.
            // A sign slip is read as the band magnitude; a degenerate band reads as equatorial.
            float safeMaxLatitude = float.IsFinite(maxLatitude) ? Math.Abs(maxLatitude) : 90f;

            float latitudeFactor;
            if (safeMaxLatitude <= 0f)
            {
                latitudeFactor = 0f;
            }
            else
            {
                latitudeDegrees = Math.Clamp(latitudeDegrees, -safeMaxLatitude, safeMaxLatitude);
                latitudeFactor = Math.Abs(latitudeDegrees) / safeMaxLatitude;
            }
            float deepSeaTemp = deepSeaEquatorTemp - ((deepSeaEquatorTemp - deepSeaPoleTemp) * latitudeFactor);
            float drop = (surfaceTemp - deepSeaTemp) * (1f - (float)Math.Exp(-gradientCoefficient * depth));
            float result = surfaceTemp - drop;

            return surfaceTemp > deepSeaTemp ? Math.Max(deepSeaTemp, result) : Math.Min(deepSeaTemp, result);
        }
    }
}
