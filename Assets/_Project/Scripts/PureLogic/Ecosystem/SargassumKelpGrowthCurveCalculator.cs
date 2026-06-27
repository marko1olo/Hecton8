using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for SargassumKelpGrowthCurveCalculator.
    /// Extracted from WorldProceduralScatterDirectorMigratorySargassum.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SargassumKelpGrowthCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentSize'>Parameter representing the currentSize (float).</param>
        /// <param name='maxClusterSize'>Parameter representing the maxClusterSize (float).</param>
        /// <param name='growthRate'>Parameter representing the growthRate (float).</param>
        /// <param name='deltaHours'>Parameter representing the deltaHours (float).</param>
        /// <returns>Returns New cluster size of type float.</returns>
        public static float Compute(float currentSize, float maxClusterSize, float growthRate, float deltaHours)
        {
            if (float.IsNaN(currentSize) || float.IsNaN(maxClusterSize) || float.IsNaN(growthRate) || float.IsNaN(deltaHours) ||
                float.IsInfinity(currentSize) || float.IsInfinity(maxClusterSize) || float.IsInfinity(growthRate) || float.IsInfinity(deltaHours))
            {
                if (float.IsNaN(currentSize) || float.IsInfinity(currentSize) || currentSize < 0f) return 0f;
                if (float.IsNaN(maxClusterSize) || maxClusterSize < 0f) return currentSize;
                return Math.Min(currentSize, maxClusterSize);
            }

            if (currentSize <= 0f) return 0f;
            if (maxClusterSize <= 0f) return 0f;
            if (currentSize >= maxClusterSize) return maxClusterSize;

            growthRate = Math.Max(0f, growthRate);
            deltaHours = Math.Max(0f, deltaHours);

            if (growthRate == 0f || deltaHours == 0f) return currentSize;

            float growth = growthRate * currentSize * (1f - (currentSize / maxClusterSize)) * deltaHours;
            float newSize = currentSize + growth;

            if (float.IsNaN(newSize) || float.IsInfinity(newSize)) return maxClusterSize;

            if (newSize > maxClusterSize) newSize = maxClusterSize;
            if (newSize < 0f) newSize = 0f;

            return newSize;
        }
    }
}
