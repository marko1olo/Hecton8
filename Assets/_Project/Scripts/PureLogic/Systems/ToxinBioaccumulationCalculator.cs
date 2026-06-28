using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ToxinBioaccumulationCalculator.
    /// Extracted from ChemicalInfluenceGrid.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ToxinBioaccumulationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="waterToxinConcentration">Parameter representing the waterToxinConcentration (float).</param>
        /// <param name="biomagnificationFactor">Parameter representing the biomagnificationFactor (float).</param>
        /// <param name="trophicLevel">Parameter representing the trophicLevel (int).</param>
        /// <returns>Returns organicTissueConcentration of type float.</returns>
        public static float Compute(float waterToxinConcentration, float biomagnificationFactor, int trophicLevel)
        {
            if (float.IsNaN(waterToxinConcentration) || float.IsNaN(biomagnificationFactor))
                return 0f;

            if (float.IsInfinity(waterToxinConcentration) || float.IsInfinity(biomagnificationFactor))
                return float.MaxValue;

            float clampedWaterToxin = MathF.Max(0f, waterToxinConcentration);
            float clampedBioMagFactor = MathF.Max(0f, biomagnificationFactor);
            int effectiveTrophicLevel = Math.Max(0, trophicLevel - 1);

            float bioAccumulationMultiplier = MathF.Pow(clampedBioMagFactor, effectiveTrophicLevel);

            if (float.IsInfinity(bioAccumulationMultiplier) || float.IsNaN(bioAccumulationMultiplier))
            {
                 return float.MaxValue;
            }

            float finalConcentration = clampedWaterToxin * bioAccumulationMultiplier;

            if (float.IsInfinity(finalConcentration))
                return float.MaxValue;

            return MathF.Max(0f, finalConcentration);
        }
    }
}
