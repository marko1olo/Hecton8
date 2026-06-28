using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for ExtinctionRiskIndexCalculator.
    /// Extracted from ShinobuEcosystemBalancer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ExtinctionRiskIndexCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentPop'>Parameter representing the currentPop (float).</param>
        /// <param name='minViablePop'>Parameter representing the minViablePop (float).</param>
        /// <param name='habitatQuality01'>Parameter representing the habitatQuality01 (float).</param>
        /// <param name='predationPressure01'>Parameter representing the predationPressure01 (float).</param>
        /// <returns>Returns extinctionRiskIndex 0.0 safe to 1.0 critical of type float.</returns>
        public static float Compute(
            float currentPop,
            float minViablePop,
            float habitatQuality01,
            float predationPressure01,
            float minSafePopScale = 1.0f,
            float popRiskMax = 1.0f,
            float defaultMaxRisk = 1.0f,
            float defaultMinRisk = 0.0f)
        {
            if (float.IsNaN(currentPop)) currentPop = defaultMinRisk;
            if (float.IsNaN(minViablePop)) minViablePop = defaultMinRisk;
            if (float.IsNaN(habitatQuality01)) habitatQuality01 = defaultMinRisk;
            if (float.IsNaN(predationPressure01)) predationPressure01 = defaultMinRisk;

            if (float.IsInfinity(currentPop)) currentPop = float.MaxValue;
            if (float.IsInfinity(minViablePop)) minViablePop = float.MaxValue;
            if (float.IsInfinity(habitatQuality01)) habitatQuality01 = defaultMaxRisk;
            if (float.IsInfinity(predationPressure01)) predationPressure01 = defaultMaxRisk;

            currentPop = Math.Max(defaultMinRisk, currentPop);
            minViablePop = Math.Max(defaultMinRisk, minViablePop);
            habitatQuality01 = Math.Max(defaultMinRisk, Math.Min(defaultMaxRisk, habitatQuality01));
            predationPressure01 = Math.Max(defaultMinRisk, Math.Min(defaultMaxRisk, predationPressure01));

            float popRisk = defaultMinRisk;
            if (minViablePop > defaultMinRisk)
            {
                if (currentPop < minViablePop)
                {
                    popRisk = (minViablePop - currentPop) / minViablePop;
                }
            }
            else if (currentPop <= defaultMinRisk && minViablePop <= defaultMinRisk)
            {
                popRisk = defaultMaxRisk;
            }

            float habitatRisk = defaultMaxRisk - habitatQuality01;

            // Pop above viable, good habitat: low risk. Below viable: high risk. Predation alone can elevate.
            float combinedRisk = Math.Max(popRisk, habitatRisk);

            combinedRisk = combinedRisk + predationPressure01 * (defaultMaxRisk - combinedRisk);

            if (float.IsNaN(combinedRisk)) return defaultMaxRisk;

            return Math.Max(defaultMinRisk, Math.Min(defaultMaxRisk, combinedRisk));
        }
    }
}
