using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for BrineSubmersionToxicityRate.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BrineSubmersionToxicityRate
    {
        /// <summary>
        /// Computes the mathematical model.
        /// </summary>
        /// <param name="brineDensity01">Parameter representing the brineDensity01 (float).</param>
        /// <param name="suitShielding01">Parameter representing the suitShielding01 (float).</param>
        /// <param name="elapsedSeconds">Parameter representing the elapsedSeconds (float).</param>
        /// <returns>Returns Toxic dose delta of type float.</returns>
        public static float Calculate(float brineDensity01, float suitShielding01, float elapsedSeconds)
        {
            if (float.IsNaN(brineDensity01) || float.IsInfinity(brineDensity01)) brineDensity01 = 0f;
            if (float.IsNaN(suitShielding01) || float.IsInfinity(suitShielding01)) suitShielding01 = 0f;
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) elapsedSeconds = 0f;

            float effectiveDensity = Math.Max(0f, Math.Min(brineDensity01, 1f));
            float effectiveShielding = Math.Max(0f, Math.Min(suitShielding01, 1f));
            float time = Math.Max(0f, elapsedSeconds);

            float dose = effectiveDensity * (1f - effectiveShielding) * time;

            return dose;
        }
    }
}
