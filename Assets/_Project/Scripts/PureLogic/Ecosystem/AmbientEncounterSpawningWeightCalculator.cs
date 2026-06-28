using System;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for AmbientEncounterSpawningWeightCalculator.
    /// Extracted from EncounterDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AmbientEncounterSpawningWeightCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseWeight">Parameter representing the baseWeight (float).</param>
        /// <param name="playerStress01">Parameter representing the playerStress01 (float).</param>
        /// <param name="cooldownRemaining">Parameter representing the cooldownRemaining (float).</param>
        /// <returns>Returns Encounter probability weight of type float.</returns>
        public static float Compute(float baseWeight, float playerStress01, float cooldownRemaining)
        {
            if (cooldownRemaining > 0f)
                return 0f;

            if (float.IsNaN(baseWeight) || float.IsInfinity(baseWeight))
                baseWeight = 0f;

            if (float.IsNaN(playerStress01) || float.IsInfinity(playerStress01))
                playerStress01 = 0f;

            baseWeight = Math.Max(0f, baseWeight);
            playerStress01 = Math.Max(0f, Math.Min(1f, playerStress01));

            float weight = baseWeight * (1f + playerStress01);
            return Math.Max(0f, weight);
        }
    }
}
