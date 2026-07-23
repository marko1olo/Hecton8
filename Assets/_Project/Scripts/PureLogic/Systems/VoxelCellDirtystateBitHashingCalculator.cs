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
            return hash & (uint)(gridDimension - 1);
        }
    }
}
