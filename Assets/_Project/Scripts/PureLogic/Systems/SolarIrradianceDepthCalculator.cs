using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SolarIrradianceDepthCalculator.
    /// Extracted from PowerGrid.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SolarIrradianceDepthCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="surfaceIrradianceWm2">Parameter representing the surfaceIrradianceWm2 (float).</param>
        /// <param name="waterAttenuationCoeff">Parameter representing the waterAttenuationCoeff (float).</param>
        /// <param name="panelEfficiency">Parameter representing the panelEfficiency (float).</param>
        /// <returns>Returns powerOutputWatts per square meter of type float.</returns>
        public static float Compute(float depthMeters, float surfaceIrradianceWm2, float waterAttenuationCoeff, float panelEfficiency)
        {
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters)) depthMeters = 0f;
            if (float.IsNaN(surfaceIrradianceWm2) || float.IsInfinity(surfaceIrradianceWm2)) surfaceIrradianceWm2 = 0f;
            if (float.IsNaN(waterAttenuationCoeff) || float.IsInfinity(waterAttenuationCoeff)) waterAttenuationCoeff = 0f;
            if (float.IsNaN(panelEfficiency) || float.IsInfinity(panelEfficiency)) panelEfficiency = 0f;

            depthMeters = Math.Max(0f, depthMeters);
            surfaceIrradianceWm2 = Math.Max(0f, surfaceIrradianceWm2);
            waterAttenuationCoeff = Math.Max(0f, waterAttenuationCoeff);
            panelEfficiency = Math.Max(0f, panelEfficiency);

            float opticalDepth = depthMeters * waterAttenuationCoeff;
            float attenuation = ResolveBeerLambert(opticalDepth);
            float irradiance = surfaceIrradianceWm2 * attenuation;
            return irradiance * panelEfficiency;
        }

        private static float ResolveBeerLambert(float opticalDepth)
        {
            float x = Math.Clamp(opticalDepth, 0f, 40f);

            // To match original math (Pade / cheap blend based on quality),
            // The original uses pade and cheap blend based on quality.
            // But since this pure math should just be pure Beer-Lambert:
            // "Pure C#. Beer-Lambert law."
            return (float)Math.Exp(-x);
        }
    }
}
