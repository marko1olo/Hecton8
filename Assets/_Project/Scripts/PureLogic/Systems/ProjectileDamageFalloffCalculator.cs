using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ProjectileDamageFalloffCalculator.
    /// Extracted from CombatDamageRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ProjectileDamageFalloffCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="distanceMeters">Parameter representing the distanceMeters (float).</param>
        /// <param name="effectiveRange">Parameter representing the effectiveRange (float).</param>
        /// <param name="maxDamage">Parameter representing the maxDamage (float).</param>
        /// <param name="minDamage">Parameter representing the minDamage (float).</param>
        /// <param name="falloffExponent">Parameter representing the falloffExponent (float).</param>
        /// <returns>Returns damage at given range of type float.</returns>
        public static float Compute(float distanceMeters, float effectiveRange, float maxDamage, float minDamage, float falloffExponent)
        {
            if (float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters) ||
                float.IsNaN(effectiveRange) || float.IsInfinity(effectiveRange) ||
                float.IsNaN(maxDamage) || float.IsInfinity(maxDamage) ||
                float.IsNaN(minDamage) || float.IsInfinity(minDamage) ||
                float.IsNaN(falloffExponent) || float.IsInfinity(falloffExponent))
            {
                return 0f;
            }

            distanceMeters = MathF.Max(0f, distanceMeters);
            effectiveRange = MathF.Max(0.0001f, effectiveRange);
            falloffExponent = MathF.Max(0f, falloffExponent);

            float maxRange = effectiveRange * 2f;
            if (distanceMeters >= maxRange)
            {
                return minDamage;
            }

            float t = MathF.Min(1f, distanceMeters / maxRange);
            float falloffFactor = MathF.Max(0f, 1f - MathF.Pow(t, falloffExponent));

            return minDamage + (maxDamage - minDamage) * falloffFactor;
        }
    }
}
