using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BatteryChargeEfficiencyCurveCalculator.
    /// Extracted from PowerNode.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BatteryChargeEfficiencyCurveCalculator
    {
        private const float DefaultChargeEfficiency = 1.0f;
        private const float ThermalDropThreshold = 0.90f; // 90% capacity

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentCharge">Parameter representing the currentCharge (float).</param>
        /// <param name="maxCapacity">Parameter representing the maxCapacity (float).</param>
        /// <param name="chargePower">Parameter representing the chargePower (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns New charge value of type float.</returns>
        public static float Compute(float currentCharge, float maxCapacity, float chargePower, float deltaTime)
        {
            if (float.IsNaN(currentCharge) || float.IsNaN(maxCapacity) || float.IsNaN(chargePower) || float.IsNaN(deltaTime))
            {
                return float.IsNaN(currentCharge) ? 0f : Math.Max(0f, currentCharge);
            }

            if (float.IsInfinity(currentCharge) || float.IsInfinity(maxCapacity) || float.IsInfinity(chargePower) || float.IsInfinity(deltaTime))
            {
                 if (maxCapacity <= 0f) return 0f;
                 if (currentCharge < 0f) return 0f;
                 if (currentCharge > maxCapacity) return maxCapacity;
                 if (float.IsInfinity(currentCharge) && float.IsInfinity(maxCapacity)) return 0f;
                 return Math.Min(currentCharge, maxCapacity);
            }

            if (maxCapacity <= 0f)
                return 0f;

            float safeCurrentCharge = Math.Max(0f, currentCharge);
            if (safeCurrentCharge >= maxCapacity)
                return maxCapacity;

            float safeChargePower = Math.Max(0f, chargePower);
            float safeDeltaTime = Math.Max(0f, deltaTime);

            if (safeChargePower <= 0f || safeDeltaTime <= 0f)
                return safeCurrentCharge;

            // Thermal inefficiency model: charging efficiency drops significantly above 90%
            float chargeRatio = safeCurrentCharge / maxCapacity;
            float efficiency = DefaultChargeEfficiency;

            if (chargeRatio > ThermalDropThreshold)
            {
                // Efficiency drops linearly from 1.0 at 90% to 0.1 at 100%
                float overThreshold = chargeRatio - ThermalDropThreshold;
                float range = 1.0f - ThermalDropThreshold;
                float normalizedOver = overThreshold / range;

                efficiency = Math.Max(0.1f, 1.0f - (0.9f * normalizedOver));
            }

            float addedCharge = safeChargePower * safeDeltaTime * efficiency;
            float newCharge = safeCurrentCharge + addedCharge;

            return Math.Min(newCharge, maxCapacity);
        }
    }
}
