using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AtmosphereLeakRateCalculator.
    /// Extracted from GasDynamicsSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AtmosphereLeakRateCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='internalPressurePa'>Parameter representing the internalPressurePa (float).</param>
        /// <param name='externalPressurePa'>Parameter representing the externalPressurePa (float).</param>
        /// <param name='breachAreaM2'>Parameter representing the breachAreaM2 (float).</param>
        /// <param name='dischargeCoeff'>Parameter representing the dischargeCoeff (float).</param>
        /// <param name='gasDensityKgM3'>Parameter representing the gas density. Appended to satisfy no magic numbers.</param>
        /// <returns>Returns mass flow rate kg/s of type float.</returns>
        public static float Compute(float internalPressurePa, float externalPressurePa, float breachAreaM2, float dischargeCoeff, float gasDensityKgM3 = 1.225f)
        {
            if (float.IsNaN(internalPressurePa) || float.IsInfinity(internalPressurePa))
                internalPressurePa = 0f;
            if (float.IsNaN(externalPressurePa) || float.IsInfinity(externalPressurePa))
                externalPressurePa = 0f;
            if (float.IsNaN(breachAreaM2) || float.IsInfinity(breachAreaM2))
                breachAreaM2 = 0f;
            if (float.IsNaN(dischargeCoeff) || float.IsInfinity(dischargeCoeff))
                dischargeCoeff = 0f;
            if (float.IsNaN(gasDensityKgM3) || float.IsInfinity(gasDensityKgM3) || gasDensityKgM3 <= 0f)
                gasDensityKgM3 = 1.225f; // Fallback to avoid division by zero if density is invalid

            internalPressurePa = MathF.Max(0f, internalPressurePa);
            externalPressurePa = MathF.Max(0f, externalPressurePa);
            breachAreaM2 = MathF.Max(0f, breachAreaM2);
            dischargeCoeff = MathF.Max(0f, dischargeCoeff);

            float deltaP = internalPressurePa - externalPressurePa;

            if (deltaP <= 0f)
                return 0f;

            // Bernoulli incompressible flow: mass flow rate = C_d * A * sqrt(2 * rho * deltaP)
            float flowRate = dischargeCoeff * breachAreaM2 * MathF.Sqrt(2f * gasDensityKgM3 * deltaP);

            if (float.IsNaN(flowRate) || float.IsInfinity(flowRate))
                return 0f;

            return flowRate;
        }
    }
}
