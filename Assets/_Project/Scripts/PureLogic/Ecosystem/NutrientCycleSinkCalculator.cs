using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for NutrientCycleSinkCalculator.
    /// Extracted from MacroEcosystemMathematicianRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class NutrientCycleSinkCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="deadBiomass">Parameter representing the deadBiomass (float).</param>
        /// <param name="decompositionRate">Parameter representing the decompositionRate (float).</param>
        /// <param name="nutrientPool">Parameter representing the nutrientPool (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newNutrientPool of type float. (remainingBiomass is updated inline if needed, but signature dictates returning float)</returns>
        public static float Compute(float deadBiomass, float decompositionRate, float nutrientPool, float deltaTime)
        {
            if (float.IsNaN(deadBiomass) || float.IsInfinity(deadBiomass)) deadBiomass = 0f;
            if (float.IsNaN(decompositionRate) || float.IsInfinity(decompositionRate)) decompositionRate = 0f;
            if (float.IsNaN(nutrientPool) || float.IsInfinity(nutrientPool)) nutrientPool = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;

            if (deadBiomass <= 0f || deltaTime <= 0f || decompositionRate <= 0f)
                return Math.Max(0f, nutrientPool);

            float convertedBiomass = deadBiomass * decompositionRate * deltaTime;

            // Cannot convert more biomass than is available
            if (convertedBiomass > deadBiomass)
            {
                convertedBiomass = deadBiomass;
            }

            // Mass conservation: biomass lost == nutrients gained
            float newNutrientPool = nutrientPool + convertedBiomass;

            return Math.Max(0f, newNutrientPool);
        }
    }
}
