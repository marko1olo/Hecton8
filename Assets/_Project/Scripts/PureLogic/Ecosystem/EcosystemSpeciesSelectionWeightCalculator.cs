using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for EcosystemSpeciesSelectionWeightCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class EcosystemSpeciesSelectionWeightCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseWeight">Parameter representing the baseWeight (float).</param>
        /// <param name="creditCost">Parameter representing the creditCost (float).</param>
        /// <param name="currentAvailableCredits">Parameter representing the currentAvailableCredits (float).</param>
        /// <returns>Returns Adjusted selection weight of type float.</returns>
        public static float Compute(float baseWeight, float creditCost, float currentAvailableCredits)
        {
            if (float.IsNaN(baseWeight) || float.IsInfinity(baseWeight)) return 0f;
            if (float.IsNaN(creditCost) || float.IsInfinity(creditCost)) return 0f;
            if (float.IsNaN(currentAvailableCredits) || float.IsInfinity(currentAvailableCredits)) return 0f;

            if (baseWeight <= 0f) return 0f;
            if (creditCost <= 0f) return baseWeight;

            return currentAvailableCredits + 0.0001f >= creditCost ? baseWeight : 0f;
        }
    }
}
