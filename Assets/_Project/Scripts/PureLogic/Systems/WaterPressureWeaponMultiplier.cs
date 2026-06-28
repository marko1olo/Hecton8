using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for WaterPressureWeaponMultiplier.
    /// Extracted from CombatDamageRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class WaterPressureWeaponMultiplier
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseVelocity">Parameter representing the baseVelocity (float).</param>
        /// <param name="baseRange">Parameter representing the baseRange (float).</param>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="waterDensity">Parameter representing the waterDensity (float).</param>
        /// <param name="referenceDensity">Parameter representing the reference density (float), usually 1000f for water.</param>
        /// <param name="decayConstant">Parameter representing the decay constant (float), e.g. 0.0069314718f for 50% at 100m.</param>
        /// <param name="minExponent">Parameter representing the minimum exponent to prevent underflow (float), e.g. -80f.</param>
        /// <returns>Returns float adjustedVelocity, float adjustedRange of type float.</returns>
        public static (float adjustedVelocity, float adjustedRange) Calculate(
            float baseVelocity,
            float baseRange,
            float depthMeters,
            float waterDensity,
            float referenceDensity,
            float decayConstant,
            float minExponent)
        {
            if (float.IsNaN(baseVelocity) || float.IsInfinity(baseVelocity)) return (0f, 0f);
            if (float.IsNaN(baseRange) || float.IsInfinity(baseRange)) return (baseVelocity, 0f);
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters)) return (baseVelocity, baseRange);
            if (float.IsNaN(waterDensity) || float.IsInfinity(waterDensity)) return (baseVelocity, baseRange);
            if (float.IsNaN(referenceDensity) || float.IsInfinity(referenceDensity) || referenceDensity <= 0f) return (baseVelocity, baseRange);
            if (float.IsNaN(decayConstant) || float.IsInfinity(decayConstant)) return (baseVelocity, baseRange);
            if (float.IsNaN(minExponent) || float.IsInfinity(minExponent)) return (baseVelocity, baseRange);

            float effectiveDepth = Math.Max(0f, depthMeters);
            float effectiveDensity = Math.Max(0f, waterDensity);

            float densityRatio = effectiveDensity / referenceDensity;

            float exponent = -decayConstant * effectiveDepth * densityRatio;

            if (exponent < minExponent) exponent = minExponent;

            float multiplier = (float)Math.Exp(exponent);

            return (baseVelocity * multiplier, baseRange * multiplier);
        }
    }
}
