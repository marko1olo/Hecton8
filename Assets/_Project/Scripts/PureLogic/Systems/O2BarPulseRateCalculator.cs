using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for O2BarPulseRateCalculator.
    /// Extracted from VisorHUDController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class O2BarPulseRateCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="o2Level01">Parameter representing the o2Level01 (float).</param>
        /// <param name="warningThreshold">Parameter representing the warningThreshold (float).</param>
        /// <param name="criticalThreshold">Parameter representing the criticalThreshold (float).</param>
        /// <param name="baseFrequencyHz">Parameter representing the baseFrequencyHz (float).</param>
        /// <param name="maxFrequencyHz">Parameter representing the maxFrequencyHz (float).</param>
        /// <returns>Returns pulseFrequencyHz of type float.</returns>
        public static float Compute(float o2Level01, float warningThreshold, float criticalThreshold, float baseFrequencyHz, float maxFrequencyHz)
        {
            if (float.IsNaN(o2Level01) || float.IsInfinity(o2Level01)) return 0f;
            if (float.IsNaN(warningThreshold) || float.IsInfinity(warningThreshold)) warningThreshold = 0f;
            if (float.IsNaN(criticalThreshold) || float.IsInfinity(criticalThreshold)) criticalThreshold = 0f;
            if (float.IsNaN(baseFrequencyHz) || float.IsInfinity(baseFrequencyHz)) baseFrequencyHz = 0f;
            if (float.IsNaN(maxFrequencyHz) || float.IsInfinity(maxFrequencyHz)) maxFrequencyHz = 0f;

            float clampedO2Level01 = Math.Clamp(o2Level01, 0f, 1f);
            float clampedWarningThreshold = Math.Clamp(warningThreshold, 0f, 1f);
            float clampedCriticalThreshold = Math.Clamp(criticalThreshold, 0f, 1f);

            // If threshold configuration is physically impossible, fallback to 0Hz.
            if (clampedWarningThreshold <= clampedCriticalThreshold || clampedWarningThreshold <= 0f)
            {
                return 0f;
            }

            if (clampedO2Level01 > clampedWarningThreshold)
            {
                return 0f;
            }

            if (clampedO2Level01 <= clampedCriticalThreshold)
            {
                return maxFrequencyHz;
            }

            float ratio = (clampedWarningThreshold - clampedO2Level01) / (clampedWarningThreshold - clampedCriticalThreshold);
            ratio = Math.Clamp(ratio, 0f, 1f);

            float result = baseFrequencyHz + ratio * (maxFrequencyHz - baseFrequencyHz);
            return Math.Max(0f, result); // Ensure frequency doesn't go below 0Hz if baseFrequency was negative
        }
    }
}
