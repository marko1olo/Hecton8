using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoxelExplosionDeformationVolumeCalculator.
    /// Extracted from VoxelDeformationSmokeTester.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoxelExplosionDeformationVolumeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentSdf'>Parameter representing the currentSdf (float).</param>
        /// <param name='distanceToEpicenter'>Parameter representing the distanceToEpicenter (float).</param>
        /// <param name='explosionRadius'>Parameter representing the explosionRadius (float).</param>
        /// <param name='blastForce'>Parameter representing the blastForce (float).</param>
        /// <returns>Returns New SDF value of type float.</returns>
        public static float Compute(float currentSdf, float distanceToEpicenter, float explosionRadius, float blastForce)
        {
            if (float.IsNaN(currentSdf) || float.IsNaN(distanceToEpicenter) || float.IsNaN(explosionRadius) || float.IsNaN(blastForce) ||
                float.IsInfinity(currentSdf) || float.IsInfinity(distanceToEpicenter) || float.IsInfinity(explosionRadius) || float.IsInfinity(blastForce))
            {
                return float.IsNaN(currentSdf) || float.IsInfinity(currentSdf) ? 0f : currentSdf;
            }

            if (explosionRadius <= 0f || blastForce <= 0f)
            {
                return currentSdf;
            }

            float clampedDistance = distanceToEpicenter < 0f ? 0f : distanceToEpicenter;

            if (clampedDistance >= explosionRadius)
            {
                return currentSdf;
            }

            float normalizedDistance = clampedDistance / explosionRadius;
            float falloff = 1f - normalizedDistance;

            // Quadratic falloff for smooth displacement curve
            float displacement = blastForce * (falloff * falloff);

            return currentSdf - displacement;
        }
    }
}
