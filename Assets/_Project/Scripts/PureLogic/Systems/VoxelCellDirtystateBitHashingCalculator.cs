using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoxelCellDirtystateBitHashingCalculator.
    /// Extracted from HectonVoxelEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoxelCellDirtystateBitHashingCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="cellX">Parameter representing the cellX (int).</param>
        /// <param name="cellY">Parameter representing the cellY (int).</param>
        /// <param name="cellZ">Parameter representing the cellZ (int).</param>
        /// <param name="gridDimension">Parameter representing the gridDimension (int).</param>
        /// <returns>Returns 32-bit hash index of type uint.</returns>
        public static uint Compute(int cellX, int cellY, int cellZ, int gridDimension)
        {
            if (gridDimension <= 0)
            {
                return 0u;
            }

            uint hash = 2166136261u;
            hash = (hash ^ (uint)cellX) * 16777619u;
            hash = (hash ^ (uint)cellY) * 16777619u;
            hash = (hash ^ (uint)cellZ) * 16777619u;

            // To match the behavior of (hash & (uint)(bucketCount - 1)) where bucketCount must be power of two,
            // we will simulate the behavior here or return the full hash masked with gridDimension-1
            // Given the original method is:
            // static int ResolveModifiedCellBucket(int3 cell, int bucketCount)
            // {
            //     uint hash = 2166136261u;
            //     hash = (hash ^ (uint)cell.x) * 16777619u;
            //     ...
            //     return (int)(hash & (uint)(bucketCount - 1));
            // }
            // Let's ensure gridDimension handles it similarly if we're clamping.
            // In the target signature it just says:
            // return (int)(hash & (uint)(bucketCount - 1));

            // If the user requires a uint returned:
            // The signature specifies gridDimension. If gridDimension is treated as bucketCount:
            return hash & (uint)(gridDimension - 1);
        }
    }
}
