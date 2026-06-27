using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for ScarcityPriceSpikeCalculator.
    /// Extracted from ResourceScarcityDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ScarcityPriceSpikeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentSupply">Parameter representing the currentSupply (float).</param>
        /// <param name="demandRate">Parameter representing the demandRate (float).</param>
        /// <param name="basePrice">Parameter representing the basePrice (float).</param>
        /// <param name="scarcityElasticity">Parameter representing the scarcityElasticity (float).</param>
        /// <returns>Returns currentPrice, float (scarcityLevel01) of type float.</returns>
        public static float Compute(float currentSupply, float demandRate, float basePrice, float scarcityElasticity)
        {
            if (float.IsNaN(currentSupply) || float.IsNaN(demandRate) || float.IsNaN(basePrice) || float.IsNaN(scarcityElasticity))
                return 0f;

            if (float.IsInfinity(currentSupply) || float.IsInfinity(demandRate) || float.IsInfinity(basePrice) || float.IsInfinity(scarcityElasticity))
                return 0f;

            float supply = Math.Max(0f, currentSupply);
            float demand = Math.Max(0f, demandRate);
            float price = Math.Max(0f, basePrice);
            float elasticity = Math.Max(0f, scarcityElasticity);

            if (demand <= 0.0001f || supply >= demand)
                return price;

            float scarcityLevel01 = (demand - supply) / demand;
            float multiplier = 1f + (scarcityLevel01 * elasticity);

            float currentPrice = price * multiplier;
            if (float.IsInfinity(currentPrice))
                return float.MaxValue;

            return currentPrice;
        }
    }
}
