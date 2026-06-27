using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FireOxygenConsumptionCalculator.
    /// Extracted from SubmarineAtmosphereSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FireOxygenConsumptionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="fireIntensity01">Parameter representing the fireIntensity01 (float).</param>
        /// <param name="compartmentVolumeM3">Parameter representing the compartmentVolumeM3 (float).</param>
        /// <param name="o2Fraction">Parameter representing the o2Fraction (float).</param>
        /// <param name="maxO2ConsumptionRate">Parameter representing the maxO2ConsumptionRate (float).</param>
        /// <returns>Returns o2ConsumptionRateKgPerSec, float (newO2Fraction after deltaTime) of type float.</returns>
        public static float Compute(float fireIntensity01, float compartmentVolumeM3, float o2Fraction, float maxO2ConsumptionRate)
        {
            if (float.IsNaN(fireIntensity01) || float.IsNaN(compartmentVolumeM3) || float.IsNaN(o2Fraction) || float.IsNaN(maxO2ConsumptionRate))
            {
                return 0f;
            }

            if (float.IsInfinity(fireIntensity01) || float.IsInfinity(compartmentVolumeM3) || float.IsInfinity(o2Fraction) || float.IsInfinity(maxO2ConsumptionRate))
            {
                return 0f;
            }

            float minClamp = 0f;
            float maxClamp = 1f;

            float clampedIntensity = Math.Max(minClamp, Math.Min(maxClamp, fireIntensity01));
            float clampedO2 = Math.Max(minClamp, Math.Min(maxClamp, o2Fraction));
            float clampedVolume = Math.Max(minClamp, compartmentVolumeM3);
            float clampedMaxRate = Math.Max(minClamp, maxO2ConsumptionRate);

            if (clampedIntensity <= minClamp || clampedO2 <= minClamp || clampedVolume <= minClamp)
            {
                return minClamp;
            }

            return clampedIntensity * clampedMaxRate;
        }
    }
}
