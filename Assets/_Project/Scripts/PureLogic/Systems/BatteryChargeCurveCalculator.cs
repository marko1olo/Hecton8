using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BatteryChargeCurveCalculator.
    /// Extracted from PowerGrid.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BatteryChargeCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="chargeLevel01">Parameter representing the chargeLevel01 (float).</param>
        /// <param name="chargerRateWatts">Parameter representing the chargerRateWatts (float).</param>
        /// <param name="batteryCapacityWh">Parameter representing the batteryCapacityWh (float).</param>
        /// <param name="cvTransitionLevel">Parameter representing the cvTransitionLevel (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns new charge level 0.0-1.0, float (actualWattsDrawn) of type float.</returns>
        public static float Compute(float chargeLevel01, float chargerRateWatts, float batteryCapacityWh, float cvTransitionLevel, float deltaTime)
        {
            // Step 1: Parameter Validation
            if (float.IsNaN(chargeLevel01) || float.IsInfinity(chargeLevel01)) chargeLevel01 = 0f;
            if (float.IsNaN(chargerRateWatts) || float.IsInfinity(chargerRateWatts)) chargerRateWatts = 0f;
            if (float.IsNaN(batteryCapacityWh) || float.IsInfinity(batteryCapacityWh)) batteryCapacityWh = 0f;
            if (float.IsNaN(cvTransitionLevel) || float.IsInfinity(cvTransitionLevel)) cvTransitionLevel = 0.8f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;

            chargeLevel01 = Math.Clamp(chargeLevel01, 0f, 1f);
            chargerRateWatts = Math.Max(0f, chargerRateWatts);
            batteryCapacityWh = Math.Max(0f, batteryCapacityWh);
            cvTransitionLevel = Math.Clamp(cvTransitionLevel, 0.001f, 1f);
            deltaTime = Math.Max(0f, deltaTime);

            if (batteryCapacityWh <= 0f || deltaTime <= 0f) return 0f;

            // Step 2 & 3: Business Logic & Boundary Guarding (CC/CV model)
            // Determine maximum possible charge rate based on CC/CV
            float allowedRateWatts = chargerRateWatts;

            if (chargeLevel01 >= cvTransitionLevel)
            {
                // CV Phase: rate tapers off as it approaches 1.0
                // Use a linear taper from max rate at transition level down to 0 at 1.0
                float remainingRange = 1f - cvTransitionLevel;
                if (remainingRange > 0f)
                {
                    float taperFactor = (1f - chargeLevel01) / remainingRange;
                    allowedRateWatts *= taperFactor;
                }
                else
                {
                    allowedRateWatts = 0f; // Instantly full at transition level
                }
            }

            // Calculate actual capacity remaining
            // batteryCapacityWh is Watt-hours. 1 Wh = 3600 Watt-seconds (Joules)
            float capacityWs = batteryCapacityWh * 3600f;
            float currentEnergyWs = chargeLevel01 * capacityWs;
            float remainingCapacityWs = capacityWs - currentEnergyWs;

            // Watts * time = Watt-seconds
            float requestedEnergyWs = allowedRateWatts * deltaTime;

            // Actual energy we can add is limited by remaining capacity
            float actualEnergyWs = Math.Min(requestedEnergyWs, remainingCapacityWs);

            // Calculate actual watts drawn
            float actualWattsDrawn = deltaTime > 0f ? actualEnergyWs / deltaTime : 0f;

            // Return Output
            return actualWattsDrawn;
        }
    }
}
