using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for EcosystemSpawnCreditBudgeting.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class EcosystemSpawnCreditBudgeting
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentCredits'>Parameter representing the currentCredits (float).</param>
        /// <param name='maxCredits'>Parameter representing the maxCredits (float).</param>
        /// <param name='regenRate'>Parameter representing the regenRate (float).</param>
        /// <param name='deltaSeconds'>Parameter representing the deltaSeconds (float).</param>
        /// <returns>Returns New credit budget of type float.</returns>
        public static float Calculate(float currentCredits, float maxCredits, float regenRate, float deltaSeconds)
        {
            if (float.IsNaN(currentCredits)) currentCredits = 0f;
            if (float.IsNaN(maxCredits)) maxCredits = 0f;
            if (float.IsNaN(regenRate)) regenRate = 0f;
            if (float.IsNaN(deltaSeconds)) deltaSeconds = 0f;

            currentCredits = Math.Max(0f, currentCredits);
            maxCredits = Math.Max(0f, maxCredits);
            deltaSeconds = Math.Max(0f, deltaSeconds);

            float accumulatedCredits = regenRate * deltaSeconds;
            float newBudget = currentCredits + accumulatedCredits;

            return Math.Min(maxCredits, Math.Max(0f, newBudget));
        }
    }
}
