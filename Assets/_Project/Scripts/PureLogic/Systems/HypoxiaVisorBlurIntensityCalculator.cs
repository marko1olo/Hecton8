using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HypoxiaVisorBlurIntensityCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HypoxiaVisorBlurIntensityCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='oxygenLevel01'>Parameter representing the oxygenLevel01 (float).</param>
        /// <param name='elapsedSeconds'>Parameter representing the elapsedSeconds (float).</param>
        /// <param name='recoveryRate'>Parameter representing the recoveryRate (float).</param>
        /// <param name='safeThreshold'>Parameter representing the safe oxygen threshold before hypoxia begins (float).</param>
        /// <param name='exponentialThreshold'>Parameter representing the oxygen threshold below which blur scales exponentially (float).</param>
        /// <param name='maxBlur'>Parameter representing the maximum allowed blur magnitude (float).</param>
        /// <param name='chromaticMultiplier'>Parameter representing the multiplier for chromatic intensity relative to blur (float).</param>
        /// <param name='maxChromatic'>Parameter representing the maximum allowed chromatic intensity (float).</param>
        /// <param name='timeScaleCap'>Parameter representing the maximum capped time factor for exponential scaling (float).</param>
        /// <returns>Returns X=Blur magnitude, Y=Chromatic intensity of type Vector2.</returns>
        public static Vector2 Compute(
            float oxygenLevel01,
            float elapsedSeconds,
            float recoveryRate,
            float safeThreshold,
            float exponentialThreshold,
            float maxBlur,
            float chromaticMultiplier,
            float maxChromatic,
            float timeScaleCap)
        {
            if (float.IsNaN(oxygenLevel01) || float.IsInfinity(oxygenLevel01)) oxygenLevel01 = 1f;
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) elapsedSeconds = 0f;
            if (float.IsNaN(recoveryRate) || float.IsInfinity(recoveryRate)) recoveryRate = 0.001f;

            float safeOxygen = Math.Clamp(oxygenLevel01, 0f, 1f);
            float safeTime = Math.Max(0f, elapsedSeconds);
            float safeRecovery = Math.Max(0.001f, recoveryRate);

            float safeThresholdClamped = Math.Max(0.001f, safeThreshold);

            if (safeOxygen >= safeThresholdClamped)
            {
                return new Vector2(0f, 0f);
            }

            float baseIntensity = (safeThresholdClamped - safeOxygen) / safeThresholdClamped;
            float blur = baseIntensity;

            if (safeOxygen < exponentialThreshold)
            {
                float timeFactor = Math.Min(safeTime * safeRecovery, timeScaleCap);
                float exponentialFactor = (float)(Math.Exp(timeFactor) - 1.0);
                blur += exponentialFactor;
            }

            blur = Math.Clamp(blur, 0f, maxBlur);
            float chromatic = Math.Clamp(blur * chromaticMultiplier, 0f, maxChromatic);

            return new Vector2(blur, chromatic);
        }
    }
}
