using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DroneBatteryReturnThresholdCalculator.
    /// Extracted from DroneFleetManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DroneBatteryReturnThresholdCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentBatteryLevel01'>Parameter representing the currentBatteryLevel01 (float).</param>
        /// <param name='distanceToBase'>Parameter representing the distanceToBase (float).</param>
        /// <param name='batteryDrainPerMeter'>Parameter representing the batteryDrainPerMeter (float).</param>
        /// <param name='safetyMargin01'>Parameter representing the safetyMargin01 (float).</param>
        /// <returns>Returns mustReturnNow, float (remainingOperationalDistance) of type bool.</returns>
        public static bool Compute(float currentBatteryLevel01, float distanceToBase, float batteryDrainPerMeter, float safetyMargin01)
        {
            if (float.IsNaN(currentBatteryLevel01) || float.IsInfinity(currentBatteryLevel01)) currentBatteryLevel01 = 0f;
            if (float.IsNaN(distanceToBase) || float.IsInfinity(distanceToBase)) distanceToBase = 0f;
            if (float.IsNaN(batteryDrainPerMeter) || float.IsInfinity(batteryDrainPerMeter)) batteryDrainPerMeter = 0f;
            if (float.IsNaN(safetyMargin01) || float.IsInfinity(safetyMargin01)) safetyMargin01 = 0f;

            currentBatteryLevel01 = Math.Clamp(currentBatteryLevel01, 0f, 1f);
            distanceToBase = Math.Max(0f, distanceToBase);
            batteryDrainPerMeter = Math.Max(0f, batteryDrainPerMeter);
            safetyMargin01 = Math.Clamp(safetyMargin01, 0f, 1f);

            float batteryRequiredForReturn = distanceToBase * batteryDrainPerMeter;
            float totalBatteryThreshold = batteryRequiredForReturn + safetyMargin01;

            return currentBatteryLevel01 <= totalBatteryThreshold;
        }
    }
}
