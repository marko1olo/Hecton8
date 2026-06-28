using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ScooterBatteryDrainCalculator.
    /// Extracted from MantaScooter.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ScooterBatteryDrainCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="thrustOutput01">Parameter representing the thrustOutput01 (float).</param>
        /// <param name="maxDrainRateWatts">Parameter representing the maxDrainRateWatts (float).</param>
        /// <param name="batteryCapacityWh">Parameter representing the batteryCapacityWh (float).</param>
        /// <param name="currentCharge01">Parameter representing the currentCharge01 (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newCharge01, float (drainThisFrame) of type float.</returns>
        public static float Compute(float thrustOutput01, float maxDrainRateWatts, float batteryCapacityWh, float currentCharge01, float deltaTime)
        {
            if (float.IsNaN(thrustOutput01) || float.IsInfinity(thrustOutput01) ||
                float.IsNaN(maxDrainRateWatts) || float.IsInfinity(maxDrainRateWatts) ||
                float.IsNaN(batteryCapacityWh) || float.IsInfinity(batteryCapacityWh) ||
                float.IsNaN(currentCharge01) || float.IsInfinity(currentCharge01) ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return Math.Clamp(currentCharge01, 0f, 1f);
            }

            if (batteryCapacityWh <= 0f || deltaTime <= 0f || maxDrainRateWatts <= 0f || currentCharge01 <= 0f || thrustOutput01 <= 0f)
            {
                return Math.Clamp(currentCharge01, 0f, 1f);
            }

            float thrustClamp = Math.Clamp(thrustOutput01, 0f, 1f);
            float chargeClamp = Math.Clamp(currentCharge01, 0f, 1f);

            // Calculate watts drained in this frame based on thrust
            float drainThisFrameWatts = maxDrainRateWatts * thrustClamp;

            // Convert to Watt-hours (deltaTime is in seconds, so divide by 3600)
            float drainThisFrameWh = drainThisFrameWatts * (deltaTime / 3600f);

            // Normalize drain to the battery capacity (0 to 1 scale)
            float drainThisFrameNormalized = drainThisFrameWh / batteryCapacityWh;

            // Compute new charge and clamp to 0..1
            float newCharge01 = Math.Max(0f, chargeClamp - drainThisFrameNormalized);

            return Math.Clamp(newCharge01, 0f, 1f);
        }
    }
}
