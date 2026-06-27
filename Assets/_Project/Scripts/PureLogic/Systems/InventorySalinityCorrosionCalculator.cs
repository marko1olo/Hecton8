using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for InventorySalinityCorrosionCalculator.
    /// Extracted from PlayerInventory.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class InventorySalinityCorrosionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDurability01">Parameter representing the currentDurability01 (float).</param>
        /// <param name="salinityFactor">Parameter representing the salinityFactor (float).</param>
        /// <param name="baseDegradationRate">Parameter representing the baseDegradationRate (float).</param>
        /// <param name="elapsedSeconds">Parameter representing the elapsedSeconds (float).</param>
        /// <returns>Returns New durability value 0.0 to 1.0 of type float.</returns>
        public static float Compute(float currentDurability01, float salinityFactor, float baseDegradationRate, float elapsedSeconds)
        {
            if (float.IsNaN(currentDurability01) || float.IsInfinity(currentDurability01))
            {
                currentDurability01 = 0f;
            }

            if (float.IsNaN(salinityFactor) || float.IsInfinity(salinityFactor))
            {
                salinityFactor = 0f;
            }

            if (float.IsNaN(baseDegradationRate) || float.IsInfinity(baseDegradationRate))
            {
                baseDegradationRate = 0f;
            }

            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
            {
                elapsedSeconds = 0f;
            }

            float safeCurrentDurability = Math.Max(0f, Math.Min(1f, currentDurability01));
            float safeSalinity = Math.Max(0f, salinityFactor);
            float safeDegradationRate = Math.Max(0f, baseDegradationRate);
            float safeElapsed = Math.Max(0f, elapsedSeconds);

            float effectiveSalinity = safeSalinity;
            if (safeSalinity > 1f)
            {
                effectiveSalinity = 1f + (float)Math.Log(safeSalinity);
            }

            float tickDegradation = safeDegradationRate * effectiveSalinity * safeElapsed;
            float nextDurability = safeCurrentDurability - tickDegradation;

            return Math.Max(0f, Math.Min(1f, nextDurability));
        }
    }
}
