using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoxelSdfTrilinearInterpolationCalculator.
    /// Extracted from HectonVoxelVolume.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoxelSdfTrilinearInterpolationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="cornerValues">Parameter representing the cornerValues (ReadOnlySpan&lt;float&gt;).</param>
        /// <param name="localX">Parameter representing the localX (float).</param>
        /// <param name="localY">Parameter representing the localY (float).</param>
        /// <param name="localZ">Parameter representing the localZ (float).</param>
        /// <returns>Returns Interpolated distance value of type float.</returns>
        public static float Compute(ReadOnlySpan<float> cornerValues, float localX, float localY, float localZ)
        {
            if (cornerValues.Length != 8)
                throw new ArgumentException("cornerValues must have exactly 8 elements.", nameof(cornerValues));

            if (float.IsNaN(localX) || float.IsInfinity(localX))
                localX = 0f;
            if (float.IsNaN(localY) || float.IsInfinity(localY))
                localY = 0f;
            if (float.IsNaN(localZ) || float.IsInfinity(localZ))
                localZ = 0f;

            float tx = Math.Max(0f, Math.Min(1f, localX));
            float ty = Math.Max(0f, Math.Min(1f, localY));
            float tz = Math.Max(0f, Math.Min(1f, localZ));

            // Map safe values without mutating the input
            float v000 = GetSafeValue(cornerValues[0]);
            float v100 = GetSafeValue(cornerValues[1]);
            float v010 = GetSafeValue(cornerValues[2]);
            float v110 = GetSafeValue(cornerValues[3]);
            float v001 = GetSafeValue(cornerValues[4]);
            float v101 = GetSafeValue(cornerValues[5]);
            float v011 = GetSafeValue(cornerValues[6]);
            float v111 = GetSafeValue(cornerValues[7]);

            // c000:0, c100:1, c010:2, c110:3, c001:4, c101:5, c011:6, c111:7
            float c00 = Lerp(v000, v100, tx);
            float c10 = Lerp(v010, v110, tx);
            float c01 = Lerp(v001, v101, tx);
            float c11 = Lerp(v011, v111, tx);
            float c0 = Lerp(c00, c10, ty);
            float c1 = Lerp(c01, c11, ty);
            return Lerp(c0, c1, tz);
        }

        private static float GetSafeValue(float val)
        {
            return (float.IsNaN(val) || float.IsInfinity(val)) ? 0f : val;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
