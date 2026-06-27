using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for StressSpawnEscalationCalculator.
    /// Extracted from StressDrivenSpawnDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StressSpawnEscalationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="playerStressLevel01">Parameter representing the playerStressLevel01 (float).</param>
        /// <param name="baseSpawnRate">Parameter representing the baseSpawnRate (float).</param>
        /// <param name="stressEscalationMultiplier">Parameter representing the stressEscalationMultiplier (float).</param>
        /// <param name="maxSpawnRate">Parameter representing the maxSpawnRate (float).</param>
        /// <returns>Returns currentSpawnRate of type float.</returns>
        public static float Compute(float playerStressLevel01, float baseSpawnRate, float stressEscalationMultiplier, float maxSpawnRate)
        {
            if (float.IsNaN(playerStressLevel01) || float.IsInfinity(playerStressLevel01))
                playerStressLevel01 = 0f;
            if (float.IsNaN(baseSpawnRate) || float.IsInfinity(baseSpawnRate))
                baseSpawnRate = 0f;
            if (float.IsNaN(stressEscalationMultiplier) || float.IsInfinity(stressEscalationMultiplier))
                stressEscalationMultiplier = 0f;
            if (float.IsNaN(maxSpawnRate) || float.IsInfinity(maxSpawnRate))
                maxSpawnRate = baseSpawnRate;

            playerStressLevel01 = Math.Max(0f, Math.Min(1f, playerStressLevel01));
            baseSpawnRate = Math.Max(0f, baseSpawnRate);
            stressEscalationMultiplier = Math.Max(0f, stressEscalationMultiplier);
            maxSpawnRate = Math.Max(baseSpawnRate, maxSpawnRate);

            float escalation = playerStressLevel01 * stressEscalationMultiplier;
            float currentSpawnRate = baseSpawnRate + escalation;

            return Math.Max(0f, Math.Min(currentSpawnRate, maxSpawnRate));
        }
    }
}
