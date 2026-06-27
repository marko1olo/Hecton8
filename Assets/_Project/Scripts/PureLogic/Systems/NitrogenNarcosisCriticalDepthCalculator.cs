using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for NitrogenNarcosisCriticalDepthCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class NitrogenNarcosisCriticalDepthCalculator
    {
        private const float BaselineDepthAtmospheres = 1f;
        private const float MetersPerAtmosphere = 10f;
        private const float NarcosisOnsetPartialPressureAtm = 3.16f; // Standard air at 30m depth
        private const float NarcosisMaxPartialPressureAtm = 5.53f;   // Standard air at ~60m depth

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDepthMeters">Parameter representing the currentDepthMeters (float).</param>
        /// <param name="oxygenFraction">Parameter representing the oxygenFraction (float).</param>
        /// <param name="nitrogenFraction">Parameter representing the nitrogenFraction (float).</param>
        /// <returns>Returns Narcosis intensity scalar 0.0 to 1.0 of type float.</returns>
        public static float Compute(float currentDepthMeters, float oxygenFraction, float nitrogenFraction)
        {
            if (float.IsNaN(currentDepthMeters) || float.IsInfinity(currentDepthMeters))
            {
                if (float.IsPositiveInfinity(currentDepthMeters)) return 1f;
                currentDepthMeters = 0f;
            }
            if (float.IsNaN(oxygenFraction) || float.IsInfinity(oxygenFraction)) oxygenFraction = 0f;
            if (float.IsNaN(nitrogenFraction) || float.IsInfinity(nitrogenFraction)) nitrogenFraction = 0f;

            currentDepthMeters = Math.Max(0f, currentDepthMeters);
            oxygenFraction = Math.Clamp(oxygenFraction, 0f, 1f);
            nitrogenFraction = Math.Clamp(nitrogenFraction, 0f, 1f);

            float totalFraction = oxygenFraction + nitrogenFraction;
            if (totalFraction > 1.0001f)
            {
                float invTotal = 1f / totalFraction;
                oxygenFraction *= invTotal;
                nitrogenFraction *= invTotal;
            }

            float hydrostaticPressureAtm = BaselineDepthAtmospheres + (currentDepthMeters / MetersPerAtmosphere);
            float nitrogenPartialPressureAtm = hydrostaticPressureAtm * nitrogenFraction;

            if (nitrogenPartialPressureAtm <= NarcosisOnsetPartialPressureAtm)
                return 0f;

            float intensity = (nitrogenPartialPressureAtm - NarcosisOnsetPartialPressureAtm) /
                              (NarcosisMaxPartialPressureAtm - NarcosisOnsetPartialPressureAtm);

            return Math.Clamp(intensity, 0f, 1f);
        }
    }
}
