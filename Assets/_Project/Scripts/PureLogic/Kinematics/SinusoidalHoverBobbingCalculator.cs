using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SinusoidalHoverBobbingCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SinusoidalHoverBobbingCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='baseHeight'>Parameter representing the baseHeight (float).</param>
        /// <param name='timeSeconds'>Parameter representing the timeSeconds (float).</param>
        /// <param name='frequency'>Parameter representing the frequency (float).</param>
        /// <param name='amplitude'>Parameter representing the amplitude (float).</param>
        /// <returns>Returns Adjusted hover height of type float.</returns>
        public static float Compute(float baseHeight, float timeSeconds, float frequency, float amplitude)
        {
            if (float.IsNaN(baseHeight) || float.IsInfinity(baseHeight) || baseHeight < 0f)
                baseHeight = 0f;

            if (float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
                timeSeconds = 0f;

            if (float.IsNaN(frequency) || float.IsInfinity(frequency) || frequency < 0f)
                frequency = 0f;

            if (float.IsNaN(amplitude) || float.IsInfinity(amplitude) || amplitude < 0f)
                amplitude = 0f;

            if (frequency == 0f || amplitude == 0f)
                return baseHeight;

            float phase = timeSeconds * frequency * (float)(2.0 * Math.PI);
            float sineVal = (float)Math.Sin(phase);
            float offset = sineVal * amplitude;

            float result = baseHeight + offset;

            return Math.Max(0f, result);
        }
    }
}
