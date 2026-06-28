using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SuitBatteryThermalEfficiencyCalculator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SuitBatteryThermalEfficiencyCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="ambientTemperatureCelsius">Parameter representing the ambientTemperatureCelsius (float).</param>
        /// <param name="batteryDrainRate">Parameter representing the batteryDrainRate (float).</param>
        /// <returns>Returns Temperature-adjusted discharge rate multiplier of type float.</returns>
        public static float Compute(float ambientTemperatureCelsius, float batteryDrainRate)
        {
            if (float.IsNaN(ambientTemperatureCelsius) || float.IsInfinity(ambientTemperatureCelsius))
            {
                return 1f;
            }

            if (float.IsNaN(batteryDrainRate) || float.IsInfinity(batteryDrainRate))
            {
                return 1f;
            }

            if (batteryDrainRate <= 0f)
            {
                return 1f;
            }

            // Calculate the multiplier based on the thermal curve math
            float multiplier = 1.0f;
            if (ambientTemperatureCelsius <= 0f)
            {
                multiplier = 1.5f;
            }
            else if (ambientTemperatureCelsius < 20f)
            {
                // Linear interpolation between 0C (1.5x) and 20C (1.0x)
                float t = ambientTemperatureCelsius / 20f;
                multiplier = 1.5f - (0.5f * t);
            }
            else
            {
                multiplier = 1.0f;
            }

            // Return the calculated multiplier
            return multiplier;
        }
    }
}
