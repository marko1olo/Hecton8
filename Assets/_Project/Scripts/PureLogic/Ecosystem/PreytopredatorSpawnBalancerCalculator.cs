using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for PreytopredatorSpawnBalancerCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PreytopredatorSpawnBalancerCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="preyCount">Parameter representing the preyCount (int).</param>
        /// <param name="predatorCount">Parameter representing the predatorCount (int).</param>
        /// <param name="optimalRatio">Parameter representing the optimalRatio (float).</param>
        /// <returns>Returns Spawn Allowed of type bool.</returns>
        public static bool Compute(int preyCount, int predatorCount, float optimalRatio)
        {
            preyCount = Math.Max(0, preyCount);
            predatorCount = Math.Max(0, predatorCount);

            if (float.IsNaN(optimalRatio) || float.IsInfinity(optimalRatio))
            {
                return false;
            }

            optimalRatio = Math.Max(0f, optimalRatio);

            if (predatorCount == 0)
            {
                return true;
            }

            float currentRatio = (float)preyCount / predatorCount;
            return currentRatio >= optimalRatio;
        }
    }
}
