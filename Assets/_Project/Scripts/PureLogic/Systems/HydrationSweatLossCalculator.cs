using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HydrationSweatLossCalculator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HydrationSweatLossCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="exertionLevel01">Parameter representing the exertionLevel01 (float).</param>
        /// <param name="ambientTempCelsius">Parameter representing the ambientTempCelsius (float).</param>
        /// <param name="baseSweatRate">Parameter representing the baseSweatRate (float).</param>
        /// <param name="heatThreshold">Parameter representing the heatThreshold (float).</param>
        /// <returns>Returns waterLostPerHour liters of type float.</returns>
        public static float Compute(float exertionLevel01, float ambientTempCelsius, float baseSweatRate, float heatThreshold)
        {
            if (float.IsNaN(exertionLevel01) || float.IsNaN(ambientTempCelsius) || float.IsNaN(baseSweatRate) || float.IsNaN(heatThreshold))
                return 0f;
            if (float.IsInfinity(exertionLevel01) || float.IsInfinity(ambientTempCelsius) || float.IsInfinity(baseSweatRate) || float.IsInfinity(heatThreshold))
                return 0f;

            float clampedExertion = Math.Max(0f, Math.Min(1f, exertionLevel01));
            float heatStress = Math.Max(0f, ambientTempCelsius - heatThreshold);

            float exertionSweat = baseSweatRate * clampedExertion;
            float heatSweat = baseSweatRate * heatStress;

            float totalSweat = baseSweatRate + exertionSweat + heatSweat;

            return float.IsNaN(totalSweat) || float.IsInfinity(totalSweat) ? 0f : Math.Max(0f, totalSweat);
        }
    }
}
