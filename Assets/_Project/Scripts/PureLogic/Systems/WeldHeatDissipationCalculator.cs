using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for WeldHeatDissipationCalculator.
    /// Extracted from RepairTool.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class WeldHeatDissipationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        public static (float heatDissipatedJoules, float newWeldTemp) Compute(
            float weldTemperatureCelsius,
            float waterTempCelsius,
            float weldAreaM2,
            float heatTransferCoeff,
            float deltaTime,
            float weldMassKg,
            float specificHeatCapacity)
        {
            if (float.IsNaN(weldTemperatureCelsius) || float.IsInfinity(weldTemperatureCelsius) ||
                float.IsNaN(waterTempCelsius) || float.IsInfinity(waterTempCelsius) ||
                float.IsNaN(weldAreaM2) || float.IsInfinity(weldAreaM2) ||
                float.IsNaN(heatTransferCoeff) || float.IsInfinity(heatTransferCoeff) ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                float.IsNaN(weldMassKg) || float.IsInfinity(weldMassKg) ||
                float.IsNaN(specificHeatCapacity) || float.IsInfinity(specificHeatCapacity))
            {
                float safeTemp = float.IsNaN(weldTemperatureCelsius) || float.IsInfinity(weldTemperatureCelsius) ? 0f : weldTemperatureCelsius;
                return (0f, safeTemp);
            }

            if (weldMassKg <= 0f)
                throw new ArgumentOutOfRangeException(nameof(weldMassKg), "Mass must be strictly positive.");
            if (specificHeatCapacity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(specificHeatCapacity), "Specific heat capacity must be strictly positive.");

            deltaTime = Math.Max(0f, deltaTime);
            weldAreaM2 = Math.Max(0f, weldAreaM2);
            heatTransferCoeff = Math.Max(0f, heatTransferCoeff);

            float tempDifference = weldTemperatureCelsius - waterTempCelsius;

            if (tempDifference == 0f || heatTransferCoeff == 0f || weldAreaM2 == 0f || deltaTime == 0f)
            {
                return (0f, weldTemperatureCelsius);
            }

            float heatCapacity = weldMassKg * specificHeatCapacity;
            float k = (heatTransferCoeff * weldAreaM2) / heatCapacity;

            float expFactor = (float)Math.Exp(-k * deltaTime);
            float newWeldTemp = waterTempCelsius + tempDifference * expFactor;

            // To prevent float precision issues causing overshoot
            if (tempDifference > 0f && newWeldTemp < waterTempCelsius)
                newWeldTemp = waterTempCelsius;
            else if (tempDifference < 0f && newWeldTemp > waterTempCelsius)
                newWeldTemp = waterTempCelsius;

            float heatDissipatedJoules = heatCapacity * (weldTemperatureCelsius - newWeldTemp);

            return (heatDissipatedJoules, newWeldTemp);
        }
    }
}
