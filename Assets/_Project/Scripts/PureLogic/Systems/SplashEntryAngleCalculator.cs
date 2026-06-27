using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SplashEntryAngleCalculator.
    /// Extracted from BallisticsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SplashEntryAngleCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='entryAngleDeg'>Parameter representing the entryAngleDeg (float).</param>
        /// <param name='projectileMass'>Parameter representing the projectileMass (float).</param>
        /// <param name='velocity'>Parameter representing the velocity (float).</param>
        /// <param name='waterSurfaceTension'>Parameter representing the waterSurfaceTension (float).</param>
        /// <returns>Returns float riccochetProbability01 of type float.</returns>
        public static float Compute(float entryAngleDeg, float projectileMass, float velocity, float waterSurfaceTension)
        {
            // Sanitize inputs
            if (float.IsNaN(entryAngleDeg) || float.IsInfinity(entryAngleDeg)) entryAngleDeg = 90f;
            if (float.IsNaN(projectileMass) || float.IsInfinity(projectileMass) || projectileMass <= 0f) projectileMass = 1f;
            if (float.IsNaN(velocity) || float.IsInfinity(velocity) || velocity < 0f) velocity = 0f;
            if (float.IsNaN(waterSurfaceTension) || float.IsInfinity(waterSurfaceTension) || waterSurfaceTension < 0f) waterSurfaceTension = 0f;

            // Clamp angle to valid physical range (0 to 90 degrees where 90 is perpendicular to surface)
            float clampedAngle = Math.Clamp(entryAngleDeg, 0f, 90f);

            // If entry is straight down or velocity is near zero, no ricochet
            if (clampedAngle >= 89.9f || velocity < 0.001f)
            {
                return 0f;
            }

            // Ricochet probability increases as angle approaches 0 (glancing blow)
            float angleFactor = (float)Math.Cos(clampedAngle * Math.PI / 180.0);

            // Tension and velocity scale the probability up.
            // Mass scales it down (heavier projectiles break through more easily).
            float momentumFactor = (velocity * waterSurfaceTension) / (projectileMass * 100f + 0.0001f);

            // Normalize momentum factor to a sensible range [0, 1]
            float tensionInfluence = Math.Clamp(momentumFactor, 0f, 1f);

            // Combine factors. High angleFactor (shallow angle) + high tensionInfluence = high ricochet.
            float baseProbability = angleFactor * (0.5f + 0.5f * tensionInfluence);

            // Specific requirement: Under 10 deg: high ricochet.
            if (clampedAngle <= 10f)
            {
                baseProbability = Math.Max(baseProbability, 0.8f);
            }

            return Math.Clamp(baseProbability, 0f, 1f);
        }
    }
}
