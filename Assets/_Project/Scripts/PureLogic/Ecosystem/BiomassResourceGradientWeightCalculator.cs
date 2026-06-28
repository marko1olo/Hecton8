using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for BiomassResourceGradientWeightCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BiomassResourceGradientWeightCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="localFoodHeatValue">Parameter representing the localFoodHeatValue (float).</param>
        /// <param name="optimalFoodThreshold">Parameter representing the optimalFoodThreshold (float).</param>
        /// <param name="baseWeight">Parameter representing the baseWeight (float).</param>
        /// <returns>Returns Heat-adjusted weight of type float.</returns>
        public static float Compute(float localFoodHeatValue, float optimalFoodThreshold, float baseWeight)
        {
            if (float.IsNaN(localFoodHeatValue) || float.IsInfinity(localFoodHeatValue))
                localFoodHeatValue = 0f;
            if (float.IsNaN(optimalFoodThreshold) || float.IsInfinity(optimalFoodThreshold))
                optimalFoodThreshold = 0f;
            if (float.IsNaN(baseWeight) || float.IsInfinity(baseWeight))
                baseWeight = 0f;

            if (baseWeight < 0f)
                baseWeight = 0f;

            localFoodHeatValue = Math.Max(0f, localFoodHeatValue);
            optimalFoodThreshold = Math.Max(0f, optimalFoodThreshold);

            if (optimalFoodThreshold == 0f)
            {
                return baseWeight;
            }

            float heatRatio = localFoodHeatValue / optimalFoodThreshold;

            // Higher food heat value yields multiplier > 1.0
            float multiplier = Math.Max(0f, heatRatio);

            return baseWeight * multiplier;
        }
    }
}
