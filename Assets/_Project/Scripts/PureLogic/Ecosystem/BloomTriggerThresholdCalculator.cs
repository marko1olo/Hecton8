using System;
namespace Hecton8.PureLogic.Ecosystem
{
    public static class BloomTriggerThresholdCalculator
    {
        public static (bool bloomTriggered, float bloomIntensity01) Compute(
            float nutrientLevel,
            float lightLevel01,
            float temperatureCelsius,
            float nutrientThreshold,
            float lightThreshold,
            float tempMin,
            float tempMax)
        {
            if (float.IsNaN(nutrientLevel) || float.IsInfinity(nutrientLevel)) nutrientLevel = 0f;
            if (float.IsNaN(lightLevel01) || float.IsInfinity(lightLevel01)) lightLevel01 = 0f;
            if (float.IsNaN(temperatureCelsius) || float.IsInfinity(temperatureCelsius)) temperatureCelsius = 0f;
            if (float.IsNaN(nutrientThreshold) || float.IsInfinity(nutrientThreshold)) nutrientThreshold = 0f;
            if (float.IsNaN(lightThreshold) || float.IsInfinity(lightThreshold)) lightThreshold = 0f;
            if (float.IsNaN(tempMin) || float.IsInfinity(tempMin)) tempMin = 0f;
            if (float.IsNaN(tempMax) || float.IsInfinity(tempMax)) tempMax = 0f;

            nutrientLevel = Math.Max(0f, nutrientLevel);
            lightLevel01 = Math.Max(0f, Math.Min(1f, lightLevel01));

            float safeTempMin = Math.Min(tempMin, tempMax);
            float safeTempMax = Math.Max(tempMin, tempMax);

            bool hasNutrients = nutrientLevel >= nutrientThreshold;
            bool hasLight = lightLevel01 >= lightThreshold;
            bool rightTemperature = temperatureCelsius >= safeTempMin && temperatureCelsius <= safeTempMax;

            bool bloomTriggered = hasNutrients && hasLight && rightTemperature;

            float bloomIntensity01 = 0f;
            if (bloomTriggered)
            {
                float excessNutrients = nutrientLevel - nutrientThreshold;
                if (excessNutrients > 0f)
                {
                    float range = nutrientThreshold > 0f ? nutrientThreshold : 1f;
                    bloomIntensity01 = (float)(1.0 - 1.0 / (1.0 + (excessNutrients / range)));
                }
            }

            return (bloomTriggered, bloomIntensity01);
        }
    }
}
