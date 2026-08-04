using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VorticityConfinementForceCalculator.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VorticityConfinementForceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='velocityFieldX'>Parameter representing the velocityFieldX (float[,,]).</param>
        /// <param name='velocityFieldY'>Parameter representing the velocityFieldY (float[,,]).</param>
        /// <param name='velocityFieldZ'>Parameter representing the velocityFieldZ (float[,,]).</param>
        /// <param name='confinementEpsilon'>Parameter representing the confinementEpsilon (float).</param>
        /// <param name='gridSpacing'>Parameter representing the gridSpacing (float).</param>
        /// <returns>Returns Tuple containing float[,,] vorticityConfinementFX, float[,,] vorticityConfinementFY, float[,,] vorticityConfinementFZ.</returns>
        public static Tuple<float[,,], float[,,], float[,,]> Compute(float[,,] velocityFieldX, float[,,] velocityFieldY, float[,,] velocityFieldZ, float confinementEpsilon, float gridSpacing)
        {
            if (velocityFieldX == null || velocityFieldY == null || velocityFieldZ == null)
            {
                return Tuple.Create(new float[0, 0, 0], new float[0, 0, 0], new float[0, 0, 0]);
            }

            int dimX = velocityFieldX.GetLength(0);
            int dimY = velocityFieldX.GetLength(1);
            int dimZ = velocityFieldX.GetLength(2);

            if (dimX == 0 || dimY == 0 || dimZ == 0)
            {
                return Tuple.Create(new float[0, 0, 0], new float[0, 0, 0], new float[0, 0, 0]);
            }

            if (dimY != velocityFieldY.GetLength(1) || dimZ != velocityFieldY.GetLength(2) || dimX != velocityFieldY.GetLength(0) ||
                dimY != velocityFieldZ.GetLength(1) || dimZ != velocityFieldZ.GetLength(2) || dimX != velocityFieldZ.GetLength(0))
            {
                return Tuple.Create(new float[dimX, dimY, dimZ], new float[dimX, dimY, dimZ], new float[dimX, dimY, dimZ]);
            }

            float[,,] forceX = new float[dimX, dimY, dimZ];
            float[,,] forceY = new float[dimX, dimY, dimZ];
            float[,,] forceZ = new float[dimX, dimY, dimZ];

            if (float.IsNaN(confinementEpsilon) || float.IsInfinity(confinementEpsilon) ||
                float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing) ||
                gridSpacing <= 0.0001f || confinementEpsilon <= 0f)
            {
                return Tuple.Create(forceX, forceY, forceZ);
            }

            float safeGridSpacing = Math.Max(0.0001f, gridSpacing);
            float safeEpsilon = Math.Max(0f, confinementEpsilon);
            float invSpacing2 = 1.0f / (2.0f * safeGridSpacing);

            // Step 1: Compute Vorticity Vector Field w = curl(v)
            Vector3[,,] vorticity = new Vector3[dimX, dimY, dimZ];
            float[,,] vorticityMag = new float[dimX, dimY, dimZ];

            for (int x = 1; x < dimX - 1; x++)
            {
                for (int y = 1; y < dimY - 1; y++)
                {
                    for (int z = 1; z < dimZ - 1; z++)
                    {
                        float dwz_dy = (velocityFieldZ[x, y + 1, z] - velocityFieldZ[x, y - 1, z]) * invSpacing2;
                        float dwy_dz = (velocityFieldY[x, y, z + 1] - velocityFieldY[x, y, z - 1]) * invSpacing2;

                        float dwx_dz = (velocityFieldX[x, y, z + 1] - velocityFieldX[x, y, z - 1]) * invSpacing2;
                        float dwz_dx = (velocityFieldZ[x + 1, y, z] - velocityFieldZ[x - 1, y, z]) * invSpacing2;

                        float dwy_dx = (velocityFieldY[x + 1, y, z] - velocityFieldY[x - 1, y, z]) * invSpacing2;
                        float dwx_dy = (velocityFieldX[x, y + 1, z] - velocityFieldX[x, y - 1, z]) * invSpacing2;

                        float curlX = dwz_dy - dwy_dz;
                        float curlY = dwx_dz - dwz_dx;
                        float curlZ = dwy_dx - dwx_dy;

                        if (float.IsNaN(curlX) || float.IsInfinity(curlX)) curlX = 0f;
                        if (float.IsNaN(curlY) || float.IsInfinity(curlY)) curlY = 0f;
                        if (float.IsNaN(curlZ) || float.IsInfinity(curlZ)) curlZ = 0f;

                        vorticity[x, y, z] = new Vector3(curlX, curlY, curlZ);
                        vorticityMag[x, y, z] = vorticity[x, y, z].Length();
                    }
                }
            }

            // Step 2: Compute Gradient of Vorticity Magnitude N = grad(|w|) and Force
            for (int x = 1; x < dimX - 1; x++)
            {
                for (int y = 1; y < dimY - 1; y++)
                {
                    for (int z = 1; z < dimZ - 1; z++)
                    {
                        float dMag_dx = (vorticityMag[x + 1, y, z] - vorticityMag[x - 1, y, z]) * invSpacing2;
                        float dMag_dy = (vorticityMag[x, y + 1, z] - vorticityMag[x, y - 1, z]) * invSpacing2;
                        float dMag_dz = (vorticityMag[x, y, z + 1] - vorticityMag[x, y, z - 1]) * invSpacing2;

                        if (float.IsNaN(dMag_dx) || float.IsInfinity(dMag_dx)) dMag_dx = 0f;
                        if (float.IsNaN(dMag_dy) || float.IsInfinity(dMag_dy)) dMag_dy = 0f;
                        if (float.IsNaN(dMag_dz) || float.IsInfinity(dMag_dz)) dMag_dz = 0f;

                        Vector3 gradMag = new Vector3(dMag_dx, dMag_dy, dMag_dz);
                        float gradMagLength = gradMag.Length();

                        Vector3 force = Vector3.Zero;
                        if (gradMagLength > 0.000001f)
                        {
                            Vector3 N = gradMag / gradMagLength;
                            Vector3 w = vorticity[x, y, z];

                            Vector3 N_cross_w = Vector3.Cross(N, w);
                            force = safeEpsilon * safeGridSpacing * N_cross_w;

                            if (float.IsNaN(force.X) || float.IsInfinity(force.X)) force.X = 0f;
                            if (float.IsNaN(force.Y) || float.IsInfinity(force.Y)) force.Y = 0f;
                            if (float.IsNaN(force.Z) || float.IsInfinity(force.Z)) force.Z = 0f;
                        }

                        forceX[x, y, z] = force.X;
                        forceY[x, y, z] = force.Y;
                        forceZ[x, y, z] = force.Z;
                    }
                }
            }

            return Tuple.Create(forceX, forceY, forceZ);
        }

        /// <summary>
        /// Allocation-free variant of <see cref="Compute"/>. Writes force components into the
        /// caller-provided <paramref name="forceX"/>/<paramref name="forceY"/>/<paramref name="forceZ"/>
        /// buffers and returns void, so hot paths (e.g. the GPU-less CPU fluid fallback) can reuse
        /// pooled grids across frames instead of allocating fresh arrays per call.
        /// <paramref name="vorticity"/> and <paramref name="vorticityMag"/> are scratch buffers of
        /// the same size; only their interior cells are read back after each step, so they may be
        /// pooled as well. All buffers must be at least dimX*dimY*dimZ in each rank.
        /// </summary>
        public static void ComputeBuffered(
            float[,,] velocityFieldX, float[,,] velocityFieldY, float[,,] velocityFieldZ,
            float confinementEpsilon, float gridSpacing,
            float[,,] forceX, float[,,] forceY, float[,,] forceZ,
            Vector3[,,] vorticity, float[,,] vorticityMag)
        {
            if (velocityFieldX == null || velocityFieldY == null || velocityFieldZ == null ||
                forceX == null || forceY == null || forceZ == null ||
                vorticity == null || vorticityMag == null)
            {
                return;
            }

            int dimX = velocityFieldX.GetLength(0);
            int dimY = velocityFieldX.GetLength(1);
            int dimZ = velocityFieldX.GetLength(2);

            if (dimX == 0 || dimY == 0 || dimZ == 0)
            {
                return;
            }

            if (float.IsNaN(confinementEpsilon) || float.IsInfinity(confinementEpsilon) ||
                float.IsNaN(gridSpacing) || float.IsInfinity(gridSpacing) ||
                gridSpacing <= 0.0001f || confinementEpsilon <= 0f)
            {
                return;
            }

            float safeGridSpacing = Math.Max(0.0001f, gridSpacing);
            float safeEpsilon = Math.Max(0f, confinementEpsilon);
            float invSpacing2 = 1.0f / (2.0f * safeGridSpacing);

            // Zero the boundary of the pooled force grids so boundary cells behave like the
            // original fresh (zero-initialized) arrays; only interior cells are populated below.
            for (int i = 0; i < dimX; i++)
            {
                for (int j = 0; j < dimY; j++)
                {
                    forceX[i, j, 0] = 0f; forceY[i, j, 0] = 0f; forceZ[i, j, 0] = 0f;
                    forceX[i, j, dimZ - 1] = 0f; forceY[i, j, dimZ - 1] = 0f; forceZ[i, j, dimZ - 1] = 0f;
                    forceX[i, 0, j] = 0f; forceY[i, 0, j] = 0f; forceZ[i, 0, j] = 0f;
                    forceX[i, dimY - 1, j] = 0f; forceY[i, dimY - 1, j] = 0f; forceZ[i, dimY - 1, j] = 0f;
                    forceX[0, i, j] = 0f; forceY[0, i, j] = 0f; forceZ[0, i, j] = 0f;
                    forceX[dimX - 1, i, j] = 0f; forceY[dimX - 1, i, j] = 0f; forceZ[dimX - 1, i, j] = 0f;
                }
            }

            // Step 1: Compute Vorticity Vector Field w = curl(v)
            for (int x = 1; x < dimX - 1; x++)
            {
                for (int y = 1; y < dimY - 1; y++)
                {
                    for (int z = 1; z < dimZ - 1; z++)
                    {
                        float dwz_dy = (velocityFieldZ[x, y + 1, z] - velocityFieldZ[x, y - 1, z]) * invSpacing2;
                        float dwy_dz = (velocityFieldY[x, y, z + 1] - velocityFieldY[x, y, z - 1]) * invSpacing2;

                        float dwx_dz = (velocityFieldX[x, y, z + 1] - velocityFieldX[x, y, z - 1]) * invSpacing2;
                        float dwz_dx = (velocityFieldZ[x + 1, y, z] - velocityFieldZ[x - 1, y, z]) * invSpacing2;

                        float dwy_dx = (velocityFieldY[x + 1, y, z] - velocityFieldY[x - 1, y, z]) * invSpacing2;
                        float dwx_dy = (velocityFieldX[x, y + 1, z] - velocityFieldX[x, y - 1, z]) * invSpacing2;

                        float curlX = dwz_dy - dwy_dz;
                        float curlY = dwx_dz - dwz_dx;
                        float curlZ = dwy_dx - dwx_dy;

                        if (float.IsNaN(curlX) || float.IsInfinity(curlX)) curlX = 0f;
                        if (float.IsNaN(curlY) || float.IsInfinity(curlY)) curlY = 0f;
                        if (float.IsNaN(curlZ) || float.IsInfinity(curlZ)) curlZ = 0f;

                        Vector3 curl = new Vector3(curlX, curlY, curlZ);
                        vorticity[x, y, z] = curl;
                        vorticityMag[x, y, z] = curl.Length();
                    }
                }
            }

            // Step 2: Compute Gradient of Vorticity Magnitude N = grad(|w|) and Force
            for (int x = 1; x < dimX - 1; x++)
            {
                for (int y = 1; y < dimY - 1; y++)
                {
                    for (int z = 1; z < dimZ - 1; z++)
                    {
                        float dMag_dx = (vorticityMag[x + 1, y, z] - vorticityMag[x - 1, y, z]) * invSpacing2;
                        float dMag_dy = (vorticityMag[x, y + 1, z] - vorticityMag[x, y - 1, z]) * invSpacing2;
                        float dMag_dz = (vorticityMag[x, y, z + 1] - vorticityMag[x, y, z - 1]) * invSpacing2;

                        if (float.IsNaN(dMag_dx) || float.IsInfinity(dMag_dx)) dMag_dx = 0f;
                        if (float.IsNaN(dMag_dy) || float.IsInfinity(dMag_dy)) dMag_dy = 0f;
                        if (float.IsNaN(dMag_dz) || float.IsInfinity(dMag_dz)) dMag_dz = 0f;

                        Vector3 gradMag = new Vector3(dMag_dx, dMag_dy, dMag_dz);
                        float gradMagLength = gradMag.Length();

                        Vector3 force = Vector3.Zero;
                        if (gradMagLength > 0.000001f)
                        {
                            Vector3 N = gradMag / gradMagLength;
                            Vector3 w = vorticity[x, y, z];

                            Vector3 N_cross_w = Vector3.Cross(N, w);
                            force = safeEpsilon * safeGridSpacing * N_cross_w;

                            if (float.IsNaN(force.X) || float.IsInfinity(force.X)) force.X = 0f;
                            if (float.IsNaN(force.Y) || float.IsInfinity(force.Y)) force.Y = 0f;
                            if (float.IsNaN(force.Z) || float.IsInfinity(force.Z)) force.Z = 0f;
                        }

                        forceX[x, y, z] = force.X;
                        forceY[x, y, z] = force.Y;
                        forceZ[x, y, z] = force.Z;
                    }
                }
            }
        }
    }
}
