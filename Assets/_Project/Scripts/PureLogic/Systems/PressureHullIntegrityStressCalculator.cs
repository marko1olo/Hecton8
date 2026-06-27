using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PressureHullIntegrityStressCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PressureHullIntegrityStressCalculator
    {
        /// <summary>
        /// Computes integrity damage applied to a submarine pressure hull based on crush depth thresholds and impacts.
        /// </summary>
        /// <param name="currentDepth">Parameter representing the currentDepth (float).</param>
        /// <param name="crushDepth">Parameter representing the crushDepth (float).</param>
        /// <param name="impactForce">Parameter representing the impactForce (float).</param>
        /// <param name="structuralIntegrity">Parameter representing the structuralIntegrity (float).</param>
        /// <returns>Returns Integrity damage delta of type float.</returns>
        public static float Compute(float currentDepth, float crushDepth, float impactForce, float structuralIntegrity)
        {
            if (float.IsNaN(currentDepth) || float.IsNaN(crushDepth) || float.IsNaN(impactForce) || float.IsNaN(structuralIntegrity) ||
                float.IsInfinity(currentDepth) || float.IsInfinity(crushDepth) || float.IsInfinity(impactForce) || float.IsInfinity(structuralIntegrity))
            {
                return 0f;
            }

            float pressureDamage = 0f;
            if (currentDepth > crushDepth)
            {
                pressureDamage = currentDepth - crushDepth;
            }

            // Mix pressure and force using the 72% / 28% balance from the monolith
            float totalDamage = (pressureDamage * 0.72f) + (impactForce * 0.28f);

            // Scale by integrity (using standard clamping for zero-protection)
            float safeIntegrity = Math.Max(0.01f, structuralIntegrity);
            return totalDamage * safeIntegrity;
        }
    }
}
