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
            latitudeDegrees = Math.Clamp(latitudeDegrees, -maxLatitude, maxLatitude);

            float latitudeFactor = Math.Abs(latitudeDegrees) / maxLatitude;
            float deepSeaTemp = deepSeaEquatorTemp - ((deepSeaEquatorTemp - deepSeaPoleTemp) * latitudeFactor);
            float drop = (surfaceTemp - deepSeaTemp) * (1f - (float)Math.Exp(-gradientCoefficient * depth));
            float result = surfaceTemp - drop;

            return surfaceTemp > deepSeaTemp ? Math.Max(deepSeaTemp, result) : Math.Min(deepSeaTemp, result);
        }
    }
}
