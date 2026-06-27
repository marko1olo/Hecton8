using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BioluminescenceIntensityDecayCalculator.
    /// Extracted from HectonBiolumManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BioluminescenceIntensityDecayCalculator
    {
        private const float MinWaterClarity = 0.01f;
        private const float MaxWaterClarity = 1f;

        private const float MinWavelength = 380f;
        private const float MaxWavelength = 750f;

        private const float OptimalBlueWavelength = 450f;

        private const float WavelengthRangeScale = 300f; // Roughly MaxWavelength - OptimalBlueWavelength
        private const float AttenuationScaleBase = 0.01f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="emittedIntensity">Parameter representing the emittedIntensity (float).</param>
        /// <param name="wavelengthNm">Parameter representing the wavelengthNm (float).</param>
        /// <param name="distanceMeters">Parameter representing the distanceMeters (float).</param>
        /// <param name="waterClarity">Parameter representing the waterClarity (float).</param>
        /// <returns>Returns perceivedIntensity of type float.</returns>
        public static float Compute(float emittedIntensity, float wavelengthNm, float distanceMeters, float waterClarity)
        {
            if (float.IsNaN(emittedIntensity) || float.IsInfinity(emittedIntensity) ||
                float.IsNaN(wavelengthNm) || float.IsInfinity(wavelengthNm) ||
                float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters) ||
                float.IsNaN(waterClarity) || float.IsInfinity(waterClarity))
            {
                return 0f;
            }

            float safeEmittedIntensity = Math.Max(0f, emittedIntensity);
            float safeDistance = Math.Max(0f, distanceMeters);
            float safeWaterClarity = Math.Clamp(waterClarity, MinWaterClarity, MaxWaterClarity);
            float safeWavelength = Math.Clamp(wavelengthNm, MinWavelength, MaxWavelength);

            // Water clarity determines baseline murkiness (1 / clarity gives a multiplier)
            float baseAttenuation = 1f / safeWaterClarity;

            // Wavelength-dependent attenuation. Blue travels furthest in water.
            // Red attenuates rapidly.
            float wavelengthDiff = Math.Abs(safeWavelength - OptimalBlueWavelength);

            // Normalize difference to roughly 0-1 range
            float wavelengthAttenuationFactor = 1f + (wavelengthDiff / WavelengthRangeScale);

            // Attenuation coefficient
            float k = AttenuationScaleBase * baseAttenuation * wavelengthAttenuationFactor;

            // Exponential decay: I = I0 * e^(-k * d)
            // Using a clamped calculation to avoid underflow
            float exponent = -k * safeDistance;

            // Math.Exp clamps to 0 for very large negative numbers naturally in standard float precision math
            // but just in case it underflows we make sure we don't return negative/nan
            float attenuation = (float)Math.Exp(exponent);

            if (float.IsNaN(attenuation) || float.IsInfinity(attenuation))
            {
                attenuation = 0f;
            }

            float perceivedIntensity = safeEmittedIntensity * attenuation;

            return Math.Max(0f, perceivedIntensity);
        }
    }
}
