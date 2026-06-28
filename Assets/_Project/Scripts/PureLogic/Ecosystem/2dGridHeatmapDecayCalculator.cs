using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for _2dGridHeatmapDecayCalculator.
    /// Extracted from EcosystemDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class _2dGridHeatmapDecayCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="grid">Parameter representing the grid (byte[]).</param>
        /// <param name="decayRate">Parameter representing the decayRate (float).</param>
        /// <param name="deltaSeconds">Parameter representing the deltaSeconds (float).</param>
        /// <param name="decayThreshold">Parameter representing the minimum threshold for decay to occur (float).</param>
        /// <returns>Returns Decayed grid of type byte[].</returns>
        public static byte[] Compute(byte[] grid, float decayRate, float deltaSeconds, float decayThreshold = 0.0001f)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            // Clamp parameters
            float clampedDecayRate = Math.Max(0f, float.IsNaN(decayRate) || float.IsInfinity(decayRate) ? 0f : decayRate);
            float clampedDeltaSeconds = Math.Max(0f, float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : deltaSeconds);

            // Compute decay factor
            float decayFactor = Math.Max(0f, 1f - (clampedDecayRate * clampedDeltaSeconds));

            // To ensure 0 values when completely decayed
            if (decayFactor < decayThreshold) decayFactor = 0f;

            byte[] newGrid = new byte[grid.Length];

            for (int i = 0; i < grid.Length; i++)
            {
                float decayedValue = grid[i] * decayFactor;

                // Clamp to byte range
                int finalValue = (int)Math.Round(Math.Max(0f, Math.Min(255f, decayedValue)));
                newGrid[i] = (byte)finalValue;
            }

            return newGrid;
        }
    }
}
