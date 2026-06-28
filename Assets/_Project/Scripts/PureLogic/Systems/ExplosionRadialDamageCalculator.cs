using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ExplosionRadialDamageCalculator.
    /// Extracted from CombatDamageRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ExplosionRadialDamageCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="distanceFromEpicenter">Parameter representing the distanceFromEpicenter (float).</param>
        /// <param name="blastRadius">Parameter representing the blastRadius (float).</param>
        /// <param name="peakDamage">Parameter representing the peakDamage (float).</param>
        /// <param name="minDamage">Parameter representing the minDamage (float).</param>
        /// <returns>Returns damage received of type float.</returns>
        public static float Compute(float distanceFromEpicenter, float blastRadius, float peakDamage, float minDamage)
        {
            if (float.IsNaN(distanceFromEpicenter) || float.IsNaN(blastRadius) || float.IsNaN(peakDamage) || float.IsNaN(minDamage) ||
                float.IsInfinity(distanceFromEpicenter) || float.IsInfinity(blastRadius) || float.IsInfinity(peakDamage) || float.IsInfinity(minDamage))
            {
                return 0f;
            }

            if (distanceFromEpicenter < 0f)
            {
                distanceFromEpicenter = 0f;
            }

            if (blastRadius <= 0f)
            {
                return 0f;
            }

            if (distanceFromEpicenter > blastRadius)
            {
                return 0f;
            }

            // inverse-square falloff: damage = (peakDamage - minDamage) * (1 - (dist/radius))^2 + minDamage
            float normalizedDistance = distanceFromEpicenter / blastRadius;
            float factor = 1.0f - normalizedDistance;

            float damage = ((peakDamage - minDamage) * factor * factor) + minDamage;
            return Math.Max(0f, damage);
        }
    }
}
