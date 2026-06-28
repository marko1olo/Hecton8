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
            if (biomePressures == null || adjacencyMap == null || biomePressures.Length == 0)
            {
                return Array.Empty<float>();
            }

            int count = biomePressures.Length;
            if (adjacencyMap.Length < count * count)
            {
                // Invalid adjacency map size, return array of zeros
                return new float[count];
            }

            float[] flows = new float[count];

            if (float.IsNaN(migrationRate) || float.IsInfinity(migrationRate) || migrationRate <= 0f)
            {
                return flows; // zero flow
            }

            // To handle multiple outflows, we need to accumulate the desired outflows
            // and if the total outflow exceeds the available pressure, we scale it down.
            float[] totalOutflows = new float[count];

            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(biomePressures[i]) || float.IsInfinity(biomePressures[i]))
                {
                    continue;
                }

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;

                    if (float.IsNaN(biomePressures[j]) || float.IsInfinity(biomePressures[j]))
                    {
                        continue;
                    }

                    int adjIndex = i * count + j;
                    if (adjacencyMap[adjIndex] != 0)
                    {
                        float gradient = biomePressures[i] - biomePressures[j];
                        if (gradient > 0f)
                        {
                            float desiredFlow = gradient * migrationRate;
                            if (!float.IsNaN(desiredFlow) && !float.IsInfinity(desiredFlow))
                            {
                                totalOutflows[i] += desiredFlow;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(biomePressures[i]) || float.IsInfinity(biomePressures[i])) continue;

                float maxOutflow = Math.Max(0f, biomePressures[i]);
                float scale = 1f;

                if (totalOutflows[i] > maxOutflow && totalOutflows[i] > 0f)
                {
                    scale = maxOutflow / totalOutflows[i];
                }

                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;

                    if (float.IsNaN(biomePressures[j]) || float.IsInfinity(biomePressures[j])) continue;

                    int adjIndex = i * count + j;
                    if (adjacencyMap[adjIndex] != 0)
                    {
                        float gradient = biomePressures[i] - biomePressures[j];
                        if (gradient > 0f)
                        {
                            float desiredFlow = gradient * migrationRate;
                            if (!float.IsNaN(desiredFlow) && !float.IsInfinity(desiredFlow))
                            {
                                float actualFlow = desiredFlow * scale;
                                flows[i] -= actualFlow;
                                flows[j] += actualFlow;
                            }
                        }
                    }
                }
            }

            // Failsafe boundary clamping
            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(flows[i]) || float.IsInfinity(flows[i]))
                {
                    flows[i] = 0f;
                }

                if (!float.IsNaN(biomePressures[i]) && !float.IsInfinity(biomePressures[i]))
                {
                    if (flows[i] < -Math.Max(0f, biomePressures[i]))
                    {
                        flows[i] = -Math.Max(0f, biomePressures[i]);
                    }
                }
            }

            return flows;
        }
    }
}
