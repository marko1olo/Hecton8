using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for DepositDepletionCurveCalculator.
    /// Extracted from ProceduralOreSpawner.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DepositDepletionCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentYield">Parameter representing the currentYield (float).</param>
        /// <param name="extractionRate">Parameter representing the extractionRate (float).</param>
        /// <param name="depletionExponent">Parameter representing the depletionExponent (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newYield, float (extractedAmount) of type float.</returns>
        public static float Compute(float currentYield, float extractionRate, float depletionExponent, float deltaTime)
        {
            if (float.IsNaN(currentYield) || float.IsInfinity(currentYield) || currentYield <= 0f)
                return 0f;

            if (float.IsNaN(extractionRate) || float.IsInfinity(extractionRate) || extractionRate <= 0f)
                return currentYield;

            if (float.IsNaN(depletionExponent) || float.IsInfinity(depletionExponent) || depletionExponent < 0f)
                depletionExponent = 0f;

            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return currentYield;

            float yieldFactor = (float)Math.Pow(currentYield, depletionExponent);
            if (float.IsNaN(yieldFactor) || float.IsInfinity(yieldFactor))
                yieldFactor = 0f;

            float extractedAmount = extractionRate * yieldFactor * deltaTime;

            if (float.IsNaN(extractedAmount) || float.IsInfinity(extractedAmount))
                extractedAmount = 0f;

            if (extractedAmount > currentYield)
                extractedAmount = currentYield;

            if (extractedAmount < 0f)
                extractedAmount = 0f;

            float newYield = currentYield - extractedAmount;

            if (newYield < 0f)
                newYield = 0f;

            return newYield;
        }
    }
}
