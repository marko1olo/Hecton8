using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for LotkaVolterraPopulationStep.
    /// Extracted from MacroEcosystemMathematicianRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LotkaVolterraPopulationStep
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="preyPop">Parameter representing the preyPop (float).</param>
        /// <param name="predatorPop">Parameter representing the predatorPop (float).</param>
        /// <param name="preyGrowthRate">Parameter representing the preyGrowthRate (float).</param>
        /// <param name="predationRate">Parameter representing the predationRate (float).</param>
        /// <param name="predatorDeathRate">Parameter representing the predatorDeathRate (float).</param>
        /// <param name="conversionEff">Parameter representing the conversionEff (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newPreyPop, float (newPredatorPop) of type float.</returns>
        public static (float newPreyPop, float newPredatorPop) Step(float preyPop, float predatorPop, float preyGrowthRate, float predationRate, float predatorDeathRate, float conversionEff, float deltaTime)
        {
            // Parameter validation
            if (float.IsNaN(preyPop) || float.IsNaN(predatorPop) || float.IsInfinity(preyPop) || float.IsInfinity(predatorPop))
            {
                return (0f, 0f);
            }
            if (preyPop < 0f) preyPop = 0f;
            if (predatorPop < 0f) predatorPop = 0f;

            if (float.IsNaN(preyGrowthRate) || float.IsNaN(predationRate) || float.IsNaN(predatorDeathRate) || float.IsNaN(conversionEff) || float.IsNaN(deltaTime) ||
                float.IsInfinity(preyGrowthRate) || float.IsInfinity(predationRate) || float.IsInfinity(predatorDeathRate) || float.IsInfinity(conversionEff) || float.IsInfinity(deltaTime))
            {
                return (preyPop, predatorPop);
            }

            float dt = Math.Max(0f, deltaTime);

            // Business logic (Lotka-Volterra with Euler integration)
            float interaction = preyPop * predatorPop;

            float preyNext = preyPop + (preyGrowthRate * preyPop - predationRate * interaction) * dt;
            float predatorNext = predatorPop + (conversionEff * interaction - predatorDeathRate * predatorPop) * dt;

            // Boundary Guarding
            if (float.IsNaN(preyNext) || float.IsInfinity(preyNext) || preyNext < 0f) preyNext = 0f;
            if (float.IsNaN(predatorNext) || float.IsInfinity(predatorNext) || predatorNext < 0f) predatorNext = 0f;

            return (preyNext, predatorNext);
        }
    }
}
