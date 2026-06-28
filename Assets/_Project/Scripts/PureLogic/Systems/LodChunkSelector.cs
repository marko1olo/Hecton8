using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LodChunkSelector.
    /// Extracted from HectonVoxelEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LodChunkSelector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="distanceFromCamera">Parameter representing the distanceFromCamera (float).</param>
        /// <param name="lodDistanceThresholds">Parameter representing the lodDistanceThresholds (float[]).</param>
        /// <param name="maxLodLevel">Parameter representing the maxLodLevel (int).</param>
        /// <returns>Returns selectedLodLevel of type int.</returns>
        public static int Calculate(float distanceFromCamera, float[] lodDistanceThresholds, int maxLodLevel)
        {
            if (float.IsNaN(distanceFromCamera) || float.IsInfinity(distanceFromCamera))
            {
                return 0; // Fallback to highest detail if distance is invalid
            }

            if (distanceFromCamera <= 0f)
            {
                return 0;
            }

            if (lodDistanceThresholds == null || lodDistanceThresholds.Length == 0)
            {
                return 0;
            }

            if (maxLodLevel < 0)
            {
                maxLodLevel = 0;
            }

            int selectedLod = 0;
            for (int i = 0; i < lodDistanceThresholds.Length; i++)
            {
                if (distanceFromCamera > lodDistanceThresholds[i])
                {
                    selectedLod = i + 1;
                }
                else
                {
                    break;
                }
            }

            return Math.Min(selectedLod, maxLodLevel);
        }
    }
}