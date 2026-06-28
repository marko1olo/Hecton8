using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoxelMeshHeightSeamBlendCalculator.
    /// Extracted from WorldGenerativeGeologyTerrainSeamApplier.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoxelMeshHeightSeamBlendCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='voxelVertexY'>Parameter representing the voxelVertexY (float).</param>
        /// <param name='terrainHeightY'>Parameter representing the terrainHeightY (float).</param>
        /// <param name='blendWidth'>Parameter representing the blendWidth (float).</param>
        /// <returns>Returns Blend factor 0.0 to 1.0 to weight SDF offset of type float.</returns>
        public static float Compute(float voxelVertexY, float terrainHeightY, float blendWidth)
        {
            if (float.IsNaN(voxelVertexY) || float.IsNaN(terrainHeightY) || float.IsNaN(blendWidth) ||
                float.IsInfinity(voxelVertexY) || float.IsInfinity(terrainHeightY) || float.IsInfinity(blendWidth))
            {
                return 0f;
            }

            if (blendWidth <= 0f)
            {
                return voxelVertexY == terrainHeightY ? 1f : 0f;
            }

            float distance = Math.Abs(voxelVertexY - terrainHeightY);
            if (distance > blendWidth)
            {
                return 0f;
            }

            float blend = 1f - (distance / blendWidth);
            return Math.Max(0f, Math.Min(1f, blend));
        }
    }
}
