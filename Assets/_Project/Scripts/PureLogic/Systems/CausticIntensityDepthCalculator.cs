using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CausticIntensityDepthCalculator.
    /// Extracted from HectonUnderwaterVisuals.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CausticIntensityDepthCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="surfaceCausticIntensity">Parameter representing the surfaceCausticIntensity (float).</param>
        /// <param name="attenuationDepth">Parameter representing the attenuationDepth (float).</param>
        /// <param name="waterClarity">Parameter representing the waterClarity (float).</param>
        /// <returns>Returns causticIntensity01 of type float.</returns>
        public static float Compute(float depthMeters, float surfaceCausticIntensity, float attenuationDepth, float waterClarity)
        {
            if (float.IsNaN(depthMeters) || float.IsNaN(surfaceCausticIntensity) ||
                float.IsNaN(attenuationDepth) || float.IsNaN(waterClarity))
            {
                return 0f;
            }

            if (float.IsInfinity(depthMeters) || float.IsInfinity(surfaceCausticIntensity) ||
                float.IsInfinity(attenuationDepth) || float.IsInfinity(waterClarity))
            {
                return 0f;
            }

            float dMeters = Math.Max(0f, depthMeters);
            float surfCaust = Math.Max(0f, surfaceCausticIntensity);
            float attenDepth = Math.Max(0.001f, attenuationDepth);
            float wClarity = Math.Max(0f, waterClarity);

            // Using the formula inferred from requirements:
            // "Surface: surfaceCausticIntensity. At attenuationDepth: 50%. Murky: faster falloff."
            // and mimicking the exponential attenuation
            // Let's create an exponential decay formula based on clarity and depth.

            // Attenuation factor: k = log(2) / attenuationDepth.
            // If waterClarity modifies this, let's say higher clarity -> slower falloff?
            // "Murky: faster falloff". So let's assume clarity is a scale or power factor.
            // But we need a robust equation that handles edge cases without overflow.

            // We want it to be 50% at attenuationDepth.
            // So factor = Math.Pow(0.5, dMeters / attenDepth)
            // Or factor = Math.Exp(-0.693147f * dMeters / attenDepth)

            // If waterClarity > 0, it increases clarity -> slower falloff.
            // But how is clarity applied? If waterClarity is 1, it's normal. If lower, faster falloff.
            // Let's divide attenDepth by (1 + murkiness) or multiply by clarity.
            // Assuming waterClarity > 0.
            float effectiveAttenDepth = attenDepth * Math.Max(0.001f, wClarity);

            // Calculate ratio
            float depthRatio = dMeters / effectiveAttenDepth;

            // Math.Pow is robust
            // 0.5 ^ (depthRatio) gives 50% at depth == effectiveAttenDepth
            double factorDouble = Math.Pow(0.5, depthRatio);
            float factor = (float)factorDouble;

            if (float.IsNaN(factor) || float.IsInfinity(factor))
                return 0f;

            float result = surfCaust * factor;
            return Math.Max(0f, Math.Min(result, surfCaust));
        }
    }
}
