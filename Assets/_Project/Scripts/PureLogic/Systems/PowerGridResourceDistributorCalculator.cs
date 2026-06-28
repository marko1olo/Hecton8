using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PowerGridResourceDistributorCalculator.
    /// Extracted from PowerGridManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PowerGridResourceDistributorCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="generatedPower">Parameter representing the generatedPower (float).</param>
        /// <param name="nodeDemands">Parameter representing the nodeDemands (float[]).</param>
        /// <param name="nodePriorities">Parameter representing the nodePriorities (int[]).</param>
        /// <returns>Returns Allocated power per node of type float[].</returns>
        public static float[] Compute(float generatedPower, float[] nodeDemands, int[] nodePriorities)
        {
            if (nodeDemands == null) throw new ArgumentNullException(nameof(nodeDemands));
            if (nodePriorities == null) throw new ArgumentNullException(nameof(nodePriorities));
            if (nodeDemands.Length != nodePriorities.Length) throw new ArgumentException("Lengths of demands and priorities must match.");

            int len = nodeDemands.Length;
            float[] allocatedPower = new float[len];

            // Handle invalid float values
            if (float.IsNaN(generatedPower) || float.IsInfinity(generatedPower) || generatedPower < 0f)
            {
                generatedPower = 0f;
            }

            // Create index array to sort by priority (descending, so higher priority first)
            int[] indices = new int[len];
            for (int i = 0; i < len; i++)
            {
                indices[i] = i;
            }

            // Sort indices by priority descending
            Array.Sort(indices, (a, b) => nodePriorities[b].CompareTo(nodePriorities[a]));

            float remainingPower = generatedPower;

            for (int i = 0; i < len; i++)
            {
                int index = indices[i];
                float demand = nodeDemands[index];

                // Handle invalid float values
                if (float.IsNaN(demand) || float.IsInfinity(demand) || demand < 0f)
                {
                    demand = 0f;
                }

                if (remainingPower >= demand)
                {
                    allocatedPower[index] = demand;
                    remainingPower -= demand;
                }
                else
                {
                    allocatedPower[index] = remainingPower;
                    remainingPower = 0f;
                }
            }

            return allocatedPower;
        }
    }
}
