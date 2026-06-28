using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for GasMixturePartialPressureCalculator.
    /// Extracted from GasDynamicsSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class GasMixturePartialPressureCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="totalPressurePa">Parameter representing the totalPressurePa (float).</param>
        /// <param name="gasMoleFractions">Parameter representing the gasMoleFractions (float[]).</param>
        /// <returns>Returns partial pressures for each gas of type float[].</returns>
        public static float[] Compute(float totalPressurePa, float[] gasMoleFractions)
        {
            if (gasMoleFractions == null)
            {
                return new float[0];
            }

            float safeTotalPressure = float.IsNaN(totalPressurePa) || float.IsInfinity(totalPressurePa)
                ? 0f
                : Math.Max(0f, totalPressurePa);

            int length = gasMoleFractions.Length;
            float[] partialPressures = new float[length];
            float sumFractions = 0f;

            for (int i = 0; i < length; i++)
            {
                float fraction = gasMoleFractions[i];
                if (float.IsNaN(fraction) || float.IsInfinity(fraction))
                {
                    fraction = 0f;
                }
                fraction = Math.Max(0f, fraction);
                sumFractions += fraction;
            }

            // If sum is 0, we can't really do anything except 0s.
            if (sumFractions <= 0f)
            {
                for (int i = 0; i < length; i++)
                {
                    partialPressures[i] = 0f;
                }
                return partialPressures;
            }

            for (int i = 0; i < length; i++)
            {
                float fraction = gasMoleFractions[i];
                if (float.IsNaN(fraction) || float.IsInfinity(fraction))
                {
                    fraction = 0f;
                }
                fraction = Math.Max(0f, fraction);

                float normalizedFraction = fraction / sumFractions;
                partialPressures[i] = safeTotalPressure * normalizedFraction;
            }

            return partialPressures;
        }
    }
}
