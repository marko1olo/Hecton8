using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PropellerCavitationLimitCalculator.
    /// Extracted from SubmarineDynamicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PropellerCavitationLimitCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='propRPM'>Parameter representing the propRPM (float).</param>
        /// <param name='depthMeters'>Parameter representing the depthMeters (float).</param>
        /// <param name='waterTemperature'>Parameter representing the waterTemperature (float).</param>
        /// <param name='propDiameterM'>Parameter representing the propDiameterM (float).</param>
        /// <param name="atmosphericPressurePa">Optional parameter representing atmospheric pressure in Pa.</param>
        /// <param name="waterDensity">Optional parameter representing water density in kg/m^3.</param>
        /// <param name="gravity">Optional parameter representing gravity in m/s^2.</param>
        /// <param name="criticalSigma">Optional parameter representing critical cavitation number.</param>
        /// <returns>Returns thrustEfficiency01 of type float.</returns>
        public static float Compute(float propRPM, float depthMeters, float waterTemperature, float propDiameterM, float atmosphericPressurePa = 101325f, float waterDensity = 1025f, float gravity = 9.81f, float criticalSigma = 2.5f)
        {
            if (propRPM <= 0f || float.IsNaN(propRPM) || float.IsInfinity(propRPM))
            {
                return 1f;
            }

            // Step 1: Guarding bounds
            float safeDepth = Math.Clamp(depthMeters, 0f, 12000f);
            if (float.IsNaN(safeDepth) || float.IsInfinity(safeDepth)) safeDepth = 0f;

            float safeTemp = Math.Clamp(waterTemperature, -2f, 40f);
            if (float.IsNaN(safeTemp) || float.IsInfinity(safeTemp)) safeTemp = 10f;

            float safeDiameter = Math.Clamp(propDiameterM, 0.1f, 10f);
            if (float.IsNaN(safeDiameter) || float.IsInfinity(safeDiameter)) safeDiameter = 1f;

            // Step 2: Mathematical application
            float staticPressure = atmosphericPressurePa + (waterDensity * gravity * safeDepth);

            // Simplified Antoine equation approximation for water vapor pressure
            float tempK = safeTemp + 273.15f;
            float vaporPressurePa = (float)Math.Exp(20.386f - (5132f / tempK)) * 133.322f;

            // Tip speed
            float rps = Math.Clamp(propRPM / 60f, 0f, 5000f);
            float tipSpeed = (float)Math.PI * safeDiameter * rps;

            // Local dynamic pressure
            float dynamicPressure = 0.5f * waterDensity * tipSpeed * tipSpeed;
            float sigma = (staticPressure - vaporPressurePa) / Math.Max(1f, dynamicPressure);

            if (float.IsNaN(sigma) || float.IsInfinity(sigma))
            {
                return 0.1f;
            }

            if (sigma >= criticalSigma)
            {
                return 1f;
            }

            // Step 3: Guard output
            float efficiency = sigma / criticalSigma;
            return Math.Clamp(efficiency, 0.1f, 1f);
        }
    }
}
