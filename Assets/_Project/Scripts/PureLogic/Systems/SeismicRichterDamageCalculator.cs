using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SeismicRichterDamageCalculator.
    /// Extracted from HectonSeismicTideDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SeismicRichterDamageCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model for seismic damage.
        /// </summary>
        public static float Compute(
            float richterMagnitude,
            float distanceKm,
            float structuralIntegrity01,
            float dampingFactor,
            float baseAmplitudeScale,
            float richterBase,
            float distanceDecayPower,
            float minDistanceKm,
            float minDamage,
            float maxDamage)
        {
            if (float.IsNaN(richterMagnitude) || float.IsInfinity(richterMagnitude) ||
                float.IsNaN(distanceKm) || float.IsInfinity(distanceKm) ||
                float.IsNaN(structuralIntegrity01) || float.IsInfinity(structuralIntegrity01) ||
                float.IsNaN(dampingFactor) || float.IsInfinity(dampingFactor) ||
                float.IsNaN(baseAmplitudeScale) || float.IsInfinity(baseAmplitudeScale) ||
                float.IsNaN(richterBase) || float.IsInfinity(richterBase) ||
                float.IsNaN(distanceDecayPower) || float.IsInfinity(distanceDecayPower) ||
                float.IsNaN(minDistanceKm) || float.IsInfinity(minDistanceKm) ||
                float.IsNaN(minDamage) || float.IsInfinity(minDamage) ||
                float.IsNaN(maxDamage) || float.IsInfinity(maxDamage))
            {
                return minDamage;
            }

            if (distanceKm < 0f) distanceKm = 0f;
            float effectiveDistance = Math.Max(distanceKm, minDistanceKm);

            float magnitudeEffect = (float)Math.Pow(richterBase, richterMagnitude);
            float distanceFalloff = 1f / (float)Math.Pow(effectiveDistance, distanceDecayPower);

            float integrityMultiplier = Math.Max(0f, 1f - structuralIntegrity01);
            float dampingMultiplier = Math.Max(0f, 1f - dampingFactor);

            float rawDamage = magnitudeEffect * distanceFalloff * integrityMultiplier * dampingMultiplier * baseAmplitudeScale;

            if (float.IsNaN(rawDamage) || float.IsInfinity(rawDamage))
            {
                return minDamage;
            }

            if (rawDamage < minDamage) return minDamage;
            if (rawDamage > maxDamage) return maxDamage;
            return rawDamage;
        }

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='richterMagnitude'>Parameter representing the richterMagnitude (float).</param>
        /// <param name='distanceKm'>Parameter representing the distanceKm (float).</param>
        /// <param name='structuralIntegrity01'>Parameter representing the structuralIntegrity01 (float).</param>
        /// <param name='dampingFactor'>Parameter representing the dampingFactor (float).</param>
        /// <returns>Returns damageDealt01, float (shakeAmplitude) of type float.</returns>
        public static float Compute(float richterMagnitude, float distanceKm, float structuralIntegrity01, float dampingFactor)
        {
             return Compute(richterMagnitude, distanceKm, structuralIntegrity01, dampingFactor, 1e-7f, 10f, 2f, 1f, 0f, 1f);
        }
    }
}
