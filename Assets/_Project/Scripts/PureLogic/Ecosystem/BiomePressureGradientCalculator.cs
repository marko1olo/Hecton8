using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for BiomePressureGradientCalculator.
    /// Extracted from ShinobuEcosystemBalancer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BiomePressureGradientCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="biomePressures">Parameter representing the biomePressures (float[]).</param>
        /// <param name="adjacencyMap">Parameter representing the adjacencyMap (int[]).</param>
        /// <param name="migrationRate">Parameter representing the migrationRate (float).</param>
        /// <returns>Returns migrationFlows between biomes of type float[].</returns>
        public static float[] Compute(float[] biomePressures, int[] adjacencyMap, float migrationRate)
        {
            if (biomePressures == null || adjacencyMap == null)
            {
                return Array.Empty<float>();
            }

            int biomeCount = biomePressures.Length;
            if (biomeCount == 0 || adjacencyMap.Length != biomeCount * biomeCount)
            {
                return new float[biomeCount];
            }

            if (float.IsNaN(migrationRate) || float.IsInfinity(migrationRate) || migrationRate < 0f)
            {
                migrationRate = 0f;
            }

            const float maxFlow = 1000000f;
            float[] migrationFlows = new float[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                float currentPressure = biomePressures[i];
                if (float.IsNaN(currentPressure) || float.IsInfinity(currentPressure))
                {
                    currentPressure = 0f;
                }

                float netFlow = 0f;

                for (int j = 0; j < biomeCount; j++)
                {
                    if (i == j) continue;

                    int adjacency = adjacencyMap[i * biomeCount + j];
                    if (adjacency <= 0) continue;

                    float neighborPressure = biomePressures[j];
                    if (float.IsNaN(neighborPressure) || float.IsInfinity(neighborPressure))
                    {
                        neighborPressure = 0f;
                    }

                    // Positive if current pressure is higher (outflow)
                    // Negative if neighbor pressure is higher (inflow)
                    float pressureDifference = currentPressure - neighborPressure;

                    netFlow += pressureDifference * adjacency * migrationRate;
                }

                // Clamp to prevent infinite values or catastrophic overflow
                if (float.IsNaN(netFlow)) netFlow = 0f;
                if (netFlow > maxFlow) netFlow = maxFlow;
                if (netFlow < -maxFlow) netFlow = -maxFlow;

                migrationFlows[i] = netFlow;
            }

            return migrationFlows;
        }
    }
}
