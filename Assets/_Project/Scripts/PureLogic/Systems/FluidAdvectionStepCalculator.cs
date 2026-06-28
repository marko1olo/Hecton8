using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FluidAdvectionStepCalculator.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FluidAdvectionStepCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="velocityFieldX">Parameter representing the velocityFieldX (float[]).</param>
        /// <param name="velocityFieldY">Parameter representing the velocityFieldY (float[]).</param>
        /// <param name="velocityFieldZ">Parameter representing the velocityFieldZ (float[]).</param>
        /// <param name="x">Parameter representing the x (int).</param>
        /// <param name="y">Parameter representing the y (int).</param>
        /// <param name="z">Parameter representing the z (int).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <param name="gridSpacing">Parameter representing the gridSpacing (float).</param>
        /// <returns>Returns advectedValue at grid position of type float.</returns>
        public static float Compute(float[] velocityFieldX, float[] velocityFieldY, float[] velocityFieldZ, int x, int y, int z, float deltaTime, float gridSpacing)
        {
            if (velocityFieldX == null || velocityFieldY == null || velocityFieldZ == null)
            {
                return 0f;
            }

            int length = velocityFieldX.Length;
            if (length == 0 || length != velocityFieldY.Length || length != velocityFieldZ.Length)
            {
                return 0f;
            }

            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return 0f;
            }

            if (float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing) || gridSpacing <= 0.0001f)
            {
                return 0f;
            }

            if (deltaTime <= 0f)
            {
                deltaTime = 0f;
            }

            int n = (int)Math.Round(Math.Pow(length, 1.0 / 3.0));
            if (n * n * n != length)
            {
                return 0f; // invalid dimensions
            }

            if (x < 0 || x >= n || y < 0 || y >= n || z < 0 || z >= n)
            {
                return 0f;
            }

            int index = x + y * n + z * n * n;
            float vx = velocityFieldX[index];
            float vy = velocityFieldY[index];
            float vz = velocityFieldZ[index];

            if (float.IsNaN(vx) || float.IsNaN(vy) || float.IsNaN(vz) ||
                float.IsInfinity(vx) || float.IsInfinity(vy) || float.IsInfinity(vz))
            {
                return 0f;
            }

            float tx = x - vx * (deltaTime / gridSpacing);
            float ty = y - vy * (deltaTime / gridSpacing);
            float tz = z - vz * (deltaTime / gridSpacing);

            if (float.IsNaN(tx) || float.IsInfinity(tx) ||
                float.IsNaN(ty) || float.IsInfinity(ty) ||
                float.IsNaN(tz) || float.IsInfinity(tz))
            {
                return 0f;
            }

            tx = Math.Clamp(tx, 0f, n - 1f);
            ty = Math.Clamp(ty, 0f, n - 1f);
            tz = Math.Clamp(tz, 0f, n - 1f);

            int x0 = (int)Math.Floor(tx);
            int y0 = (int)Math.Floor(ty);
            int z0 = (int)Math.Floor(tz);

            int x1 = Math.Min(x0 + 1, n - 1);
            int y1 = Math.Min(y0 + 1, n - 1);
            int z1 = Math.Min(z0 + 1, n - 1);

            float sx = tx - x0;
            float sy = ty - y0;
            float sz = tz - z0;

            float c000 = velocityFieldX[x0 + y0 * n + z0 * n * n];
            float c100 = velocityFieldX[x1 + y0 * n + z0 * n * n];
            float c010 = velocityFieldX[x0 + y1 * n + z0 * n * n];
            float c110 = velocityFieldX[x1 + y1 * n + z0 * n * n];
            float c001 = velocityFieldX[x0 + y0 * n + z1 * n * n];
            float c101 = velocityFieldX[x1 + y0 * n + z1 * n * n];
            float c011 = velocityFieldX[x0 + y1 * n + z1 * n * n];
            float c111 = velocityFieldX[x1 + y1 * n + z1 * n * n];

            float c00 = c000 * (1f - sx) + c100 * sx;
            float c10 = c010 * (1f - sx) + c110 * sx;
            float c01 = c001 * (1f - sx) + c101 * sx;
            float c11 = c011 * (1f - sx) + c111 * sx;

            float c0 = c00 * (1f - sy) + c10 * sy;
            float c1 = c01 * (1f - sy) + c11 * sy;

            float c = c0 * (1f - sz) + c1 * sz;

            if (float.IsNaN(c) || float.IsInfinity(c))
            {
                return 0f;
            }

            return c;
        }
    }
}
