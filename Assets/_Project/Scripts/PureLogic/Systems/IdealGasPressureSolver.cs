using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for IdealGasPressureSolver.
    /// Extracted from GasDynamicsSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class IdealGasPressureSolver
    {
        /// <summary>
        /// Universal gas constant (R) in J/(mol·K) or (Pa·m³)/(mol·K).
        /// </summary>
        public const float GasConstant = 8.31446261815324f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="moles">Parameter representing the moles (float).</param>
        /// <param name="temperatureKelvin">Parameter representing the temperatureKelvin (float).</param>
        /// <param name="volumeCubicMeters">Parameter representing the volumeCubicMeters (float).</param>
        /// <returns>Returns pressure in Pascals of type float.</returns>
        public static float Solve(float moles, float temperatureKelvin, float volumeCubicMeters)
        {
            if (float.IsNaN(moles) || float.IsNaN(temperatureKelvin) || float.IsNaN(volumeCubicMeters))
            {
                return 0f;
            }

            if (float.IsInfinity(moles) || float.IsInfinity(temperatureKelvin) || float.IsInfinity(volumeCubicMeters))
            {
                return 0f;
            }

            // Clamp negative inputs to 0 for physical realism
            moles = Math.Max(0f, moles);
            temperatureKelvin = Math.Max(0f, temperatureKelvin);
            volumeCubicMeters = Math.Max(0f, volumeCubicMeters);

            // Guard against division by zero (or very close to zero volume)
            if (volumeCubicMeters <= 1e-6f)
            {
                return 0f;
            }

            // PV = nRT -> P = (n * R * T) / V
            float pressure = (moles * GasConstant * temperatureKelvin) / volumeCubicMeters;

            // Ensure no infinity / NaN from computation
            if (float.IsNaN(pressure) || float.IsInfinity(pressure))
            {
                return 0f;
            }

            // Also clamp pressure to not be negative
            return Math.Max(0f, pressure);
        }
    }
}
