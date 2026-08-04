using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    public static class FluidPressureJacobiSolver
    {
        public static float[,,] Solve(float[,,] pressureField, float[,,] divergenceField, float gridSpacing)
        {
            if (pressureField == null) throw new ArgumentNullException(nameof(pressureField));
            if (divergenceField == null) throw new ArgumentNullException(nameof(divergenceField));

            int sizeX = pressureField.GetLength(0);
            int sizeY = pressureField.GetLength(1);
            int sizeZ = pressureField.GetLength(2);

            if (divergenceField.GetLength(0) != sizeX ||
                divergenceField.GetLength(1) != sizeY ||
                divergenceField.GetLength(2) != sizeZ)
            {
                throw new ArgumentException("Divergence field dimensions must match pressure field.");
            }

            if (float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing) || gridSpacing <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gridSpacing), "Grid spacing must be finite and greater than zero.");
            }

            float[,,] newPressure = new float[sizeX, sizeY, sizeZ];
            float dx2 = gridSpacing * gridSpacing;

            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    for (int k = 0; k < sizeZ; k++)
                    {
                        float p_i1 = (i + 1 < sizeX) ? pressureField[i + 1, j, k] : pressureField[i, j, k];
                        float p_i0 = (i - 1 >= 0)    ? pressureField[i - 1, j, k] : pressureField[i, j, k];
                        float p_j1 = (j + 1 < sizeY) ? pressureField[i, j + 1, k] : pressureField[i, j, k];
                        float p_j0 = (j - 1 >= 0)    ? pressureField[i, j - 1, k] : pressureField[i, j, k];
                        float p_k1 = (k + 1 < sizeZ) ? pressureField[i, j, k + 1] : pressureField[i, j, k];
                        float p_k0 = (k - 1 >= 0)    ? pressureField[i, j, k - 1] : pressureField[i, j, k];

                        float div = divergenceField[i, j, k];
                        if (float.IsNaN(div) || float.IsInfinity(div)) div = 0f;

                        float p_curr = (p_i1 + p_i0 + p_j1 + p_j0 + p_k1 + p_k0 - dx2 * div) / 6f;

                        if (float.IsNaN(p_curr) || float.IsInfinity(p_curr))
                        {
                            p_curr = 0f;
                        }

                        // Clamp to prevent extreme values / overflow
                        if (p_curr > float.MaxValue) p_curr = float.MaxValue;
                        if (p_curr < float.MinValue) p_curr = float.MinValue;

                        newPressure[i, j, k] = p_curr;
                    }
                }
            }

            return newPressure;
        }

        /// <summary>
        /// Allocation-free variant of <see cref="Solve"/>. Performs one Jacobi pass, writing the
        /// result into the caller-provided <paramref name="outputPressure"/> buffer. The caller is
        /// responsible for ping-ponging <paramref name="pressureField"/> and
        /// <paramref name="outputPressure"/> between iterations. This lets hot paths (e.g. the
        /// GPU-less CPU fluid fallback) reuse pooled grids instead of allocating a fresh array per
        /// iteration. The three buffers must share identical dimensions.
        /// </summary>
        public static void SolveBuffered(
            float[,,] pressureField, float[,,] divergenceField, float gridSpacing,
            float[,,] outputPressure)
        {
            if (pressureField == null || divergenceField == null || outputPressure == null)
            {
                return;
            }

            int sizeX = pressureField.GetLength(0);
            int sizeY = pressureField.GetLength(1);
            int sizeZ = pressureField.GetLength(2);

            if (outputPressure.GetLength(0) != sizeX ||
                outputPressure.GetLength(1) != sizeY ||
                outputPressure.GetLength(2) != sizeZ)
            {
                return;
            }

            if (float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing) || gridSpacing <= 0f)
            {
                return;
            }

            float dx2 = gridSpacing * gridSpacing;

            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    for (int k = 0; k < sizeZ; k++)
                    {
                        float p_i1 = (i + 1 < sizeX) ? pressureField[i + 1, j, k] : pressureField[i, j, k];
                        float p_i0 = (i - 1 >= 0)    ? pressureField[i - 1, j, k] : pressureField[i, j, k];
                        float p_j1 = (j + 1 < sizeY) ? pressureField[i, j + 1, k] : pressureField[i, j, k];
                        float p_j0 = (j - 1 >= 0)    ? pressureField[i, j - 1, k] : pressureField[i, j, k];
                        float p_k1 = (k + 1 < sizeZ) ? pressureField[i, j, k + 1] : pressureField[i, j, k];
                        float p_k0 = (k - 1 >= 0)    ? pressureField[i, j, k - 1] : pressureField[i, j, k];

                        float div = divergenceField[i, j, k];
                        if (float.IsNaN(div) || float.IsInfinity(div)) div = 0f;

                        float p_curr = (p_i1 + p_i0 + p_j1 + p_j0 + p_k1 + p_k0 - dx2 * div) / 6f;

                        if (float.IsNaN(p_curr) || float.IsInfinity(p_curr))
                        {
                            p_curr = 0f;
                        }

                        // Clamp to prevent extreme values / overflow
                        if (p_curr > float.MaxValue) p_curr = float.MaxValue;
                        if (p_curr < float.MinValue) p_curr = float.MinValue;

                        outputPressure[i, j, k] = p_curr;
                    }
                }
            }
        }
    }
}
