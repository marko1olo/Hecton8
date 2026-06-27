using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for BiomeDiscoveryBitmaskTracker.
    /// Extracted from BiomeDiscoveryBitMask.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BiomeDiscoveryBitmaskTracker
    {
        private const int MinBiomeIndex = 0;
        private const int MaxBiomeIndex = 31;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentMask'>Parameter representing the currentMask (uint).</param>
        /// <param name='biomeIndex'>Parameter representing the biomeIndex (int).</param>
        /// <returns>Returns Updated mask of type uint.</returns>
        public static uint Calculate(uint currentMask, int biomeIndex)
        {
            // Clamp biomeIndex between 0 and 31
            int clampedIndex = biomeIndex < MinBiomeIndex ? MinBiomeIndex : (biomeIndex > MaxBiomeIndex ? MaxBiomeIndex : biomeIndex);

            // Set the corresponding bit and return
            return currentMask | (1u << clampedIndex);
        }
    }
}
