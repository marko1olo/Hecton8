using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoxelSdfBooleanSubtraction.
    /// Extracted from HectonVoxelEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoxelSdfBooleanSubtraction
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="densityField">Parameter representing the densityField.</param>
        /// <param name="sphereCenter">Parameter representing the sphereCenter (Vector3).</param>
        /// <param name="sphereRadius">Parameter representing the sphereRadius (float).</param>
        /// <param name="gridResolution">Parameter representing the gridResolution (int).</param>
        /// <param name="worldScale">Parameter representing the worldScale (float).</param>
        /// <param name="minWorldScale">Parameter representing the minimum worldScale (float) to avoid division by zero.</param>
        /// <param name="minGridResolution">Parameter representing the minimum gridResolution (int).</param>
        /// <param name="safeSmoothK">Parameter representing a small safe value to prevent div by zero in smooth interpolation.</param>
        /// <param name="smoothK">Parameter representing the smoothing coefficient.</param>
        /// <returns>Returns modified density field of type float[,,].</returns>
        public static float[,,] Calculate(
            float[,,] densityField,
            Vector3 sphereCenter,
            float sphereRadius,
            int gridResolution,
            float worldScale,
            float minWorldScale = 0.0001f,
            int minGridResolution = 1,
            float safeSmoothK = 0.0001f,
            float smoothK = 0.5f)
        {
            if (densityField == null) throw new ArgumentNullException(nameof(densityField));

            int resX = densityField.GetLength(0);
            int resY = densityField.GetLength(1);
            int resZ = densityField.GetLength(2);

            float[,,] resultField = new float[resX, resY, resZ];

            float safeSphereRadius = Math.Max(0f, sphereRadius);
            float safeWorldScale = Math.Max(minWorldScale, worldScale);
            int safeGridResolution = Math.Max(minGridResolution, gridResolution);
            float voxelStep = safeWorldScale / safeGridResolution;

            float blendRadius = Math.Max(smoothK, voxelStep);

            for (int x = 0; x < resX; x++)
            {
                for (int y = 0; y < resY; y++)
                {
                    for (int z = 0; z < resZ; z++)
                    {
                        Vector3 voxelWorldPos = new Vector3(x, y, z) * voxelStep;

                        float distToCenter = Vector3.Distance(voxelWorldPos, sphereCenter);

                        // SDSphere
                        float sphereDist = distToCenter - safeSphereRadius;

                        float currentDensity = densityField[x, y, z];

                        // We want to subtract the sphere from the base density.
                        // SmoothSubtractionQuadratic(distCarve, distBase, k) = SmoothMaxQuadratic(distBase, -distCarve, k)
                        // If we pass sphereDist directly as distCarve:
                        // newDensity = SmoothSubtractionQuadratic(sphereDist, currentDensity, blendRadius)
                        float newDensity = SmoothSubtractionQuadratic(sphereDist, currentDensity, blendRadius, safeSmoothK);

                        resultField[x, y, z] = newDensity;
                    }
                }
            }

            return resultField;
        }

        private static float SmoothSubtractionQuadratic(float distCarve, float distBase, float k, float safeK)
        {
            return SmoothMaxQuadratic(distBase, -distCarve, k, safeK);
        }

        private static float SmoothMaxQuadratic(float a, float b, float k, float safeK)
        {
            return -SmoothMinQuadratic(-a, -b, k, safeK);
        }

        private static float SmoothMinQuadratic(float a, float b, float k, float safeK)
        {
            float safe_K = Math.Max(safeK, k);
            float h = Math.Clamp(0.5f + 0.5f * (b - a) / safe_K, 0.0f, 1.0f);
            float lerp = b + (a - b) * h;
            return lerp - safe_K * h * (1.0f - h);
        }
    }
}
