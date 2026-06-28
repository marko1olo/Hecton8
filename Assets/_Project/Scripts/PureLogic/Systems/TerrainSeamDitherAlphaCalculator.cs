using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for TerrainSeamDitherAlphaCalculator.
    /// Extracted from SeamGapDitherRenderer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class TerrainSeamDitherAlphaCalculator
    {
        private static readonly float[] DitherThresholds = new float[]
        {
            1.0f / 17.0f,  9.0f / 17.0f,  3.0f / 17.0f, 11.0f / 17.0f,
            13.0f / 17.0f,  5.0f / 17.0f, 15.0f / 17.0f,  7.0f / 17.0f,
            4.0f / 17.0f, 12.0f / 17.0f,  2.0f / 17.0f, 10.0f / 17.0f,
            16.0f / 17.0f,  8.0f / 17.0f, 14.0f / 17.0f,  6.0f / 17.0f
        };

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='x'>Parameter representing the x (int).</param>
        /// <param name='y'>Parameter representing the y (int).</param>
        /// <param name='blendFactor01'>Parameter representing the blendFactor01 (float).</param>
        /// <returns>Returns Dither transparency output of type float.</returns>
        public static float Compute(int x, int y, float blendFactor01)
        {
            // Clamp blend factor to [0, 1]
            float clampedBlend = blendFactor01;
            if (float.IsNaN(clampedBlend)) clampedBlend = 0f;
            else if (clampedBlend < 0f) clampedBlend = 0f;
            else if (clampedBlend > 1f) clampedBlend = 1f;

            // Handle negative coordinates using modulo arithmetic behavior of bitwise operations or explicit floor mod.
            // Using a simple bitwise AND works beautifully for power-of-two grids like 4x4.
            // It safely wraps negative coordinates to positive [0, 3] range.
            int wrappedX = x & 3;
            int wrappedY = y & 3;

            // Matrix lookup index: (x % 4) * 4 + (y % 4)
            int index = (wrappedX << 2) + wrappedY;

            // Get the dither threshold from the 4x4 matrix
            float threshold = DitherThresholds[index];

            // If blendFactor is greater than or equal to the threshold, the pixel is fully visible (alpha = 1)
            // Otherwise, it's transparent (alpha = 0).
            return clampedBlend >= threshold ? 1.0f : 0.0f;
        }
    }
}
