using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HypothermiaShiverCurveCalculator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HypothermiaShiverCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='coreTempCelsius'>Parameter representing the coreTempCelsius (float).</param>
        /// <param name='normalTemp'>Parameter representing the normalTemp (float).</param>
        /// <param name='shiversOnset'>Parameter representing the shiversOnset (float).</param>
        /// <param name='incapacitationTemp'>Parameter representing the incapacitationTemp (float).</param>
        /// <returns>Returns penaltyFactor 0.0 normal to 1.0 incapacitated of type float.</returns>
        public static float Compute(float coreTempCelsius, float normalTemp, float shiversOnset, float incapacitationTemp)
        {
            if (float.IsNaN(coreTempCelsius) || float.IsInfinity(coreTempCelsius) ||
                float.IsNaN(shiversOnset) || float.IsInfinity(shiversOnset) ||
                float.IsNaN(incapacitationTemp) || float.IsInfinity(incapacitationTemp))
            {
                return 0f;
            }

            if (coreTempCelsius >= shiversOnset)
            {
                return 0f;
            }

            float range = shiversOnset - incapacitationTemp;
            float divisor = Math.Max(0.01f, range);
            float penaltyFactor = (shiversOnset - coreTempCelsius) / divisor;

            if (float.IsNaN(penaltyFactor) || float.IsInfinity(penaltyFactor))
            {
                return 0f;
            }

            return Math.Max(0f, Math.Min(1f, penaltyFactor));
        }
    }
}
