using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CeilingConcavityAirPocketVolumeCalculator.
    /// Extracted from HectonVoxelEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CeilingConcavityAirPocketVolumeCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='normal'>Parameter representing the normal (Vector3).</param>
        /// <param name='ceilingDepth'>Parameter representing the ceilingDepth (float).</param>
        /// <param name='waterlineClearance'>Parameter representing the waterlineClearance (float).</param>
        /// <param name='boundaryRadius'>Parameter representing the boundaryRadius (float).</param>
        /// <returns>Returns Trapped air volume cubic meters of type float.</returns>
        public static float Compute(Vector3 normal, float ceilingDepth, float waterlineClearance, float boundaryRadius)
        {
            // Note: The specific logic for computing geometric volume based on these arguments
            // wasn't found in HectonVoxelEngine.cs. The monolith flags pockets but delegates the scalar metric.
            // Following task instructions to author pure geometric volume math.
            // "Flat ceilings yield 0 volume. Inverted dome ceiling normals yield maximum pocket volumes."

            // Step 1 - Parameter Validation
            if (float.IsNaN(ceilingDepth) || float.IsInfinity(ceilingDepth)) ceilingDepth = 0f;
            if (float.IsNaN(waterlineClearance) || float.IsInfinity(waterlineClearance)) waterlineClearance = 0f;
            if (float.IsNaN(boundaryRadius) || float.IsInfinity(boundaryRadius)) boundaryRadius = 0f;

            if (float.IsNaN(normal.X) || float.IsInfinity(normal.X) ||
                float.IsNaN(normal.Y) || float.IsInfinity(normal.Y) ||
                float.IsNaN(normal.Z) || float.IsInfinity(normal.Z))
            {
                normal = new Vector3(0f, -1f, 0f);
            }

            ceilingDepth = Math.Max(0f, ceilingDepth);
            waterlineClearance = Math.Max(0f, waterlineClearance);
            boundaryRadius = Math.Max(0f, boundaryRadius);

            float totalHeight = ceilingDepth + waterlineClearance;
            if (totalHeight <= 0f || boundaryRadius <= 0f)
                return 0f;

            // Step 2 - Business Logic: Geometric bounding volume
            // Max volume = Volume of a cylinder = PI * r^2 * h.
            float maxVolume = (float)Math.PI * boundaryRadius * boundaryRadius * totalHeight;

            // Step 3 - Concavity Evaluation
            // If the ceiling normal is straight down (0, -1, 0), it's a flat ceiling. Yields 0 volume.
            // The more the normal deviates from perfectly straight down, the higher the volume scalar.
            // E.g., an inverted dome has varying normals pointing inward.
            float downDot = Math.Max(-1f, Math.Min(1f, normal.Y));

            // Flat ceilings (Y = -1) -> flatness = 1.
            float flatness = Math.Max(0f, -downDot);
            float concavityFactor = 1f - flatness;

            float candidateVolume = maxVolume * concavityFactor;

            // Step 4 - Boundary Guarding & Output
            candidateVolume = Math.Max(0f, candidateVolume);
            return float.IsNaN(candidateVolume) || float.IsInfinity(candidateVolume) ? 0f : candidateVolume;
        }
    }
}
