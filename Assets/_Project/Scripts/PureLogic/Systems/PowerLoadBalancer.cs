using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PowerLoadBalancer.
    /// Extracted from PowerGrid.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PowerLoadBalancer
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="totalSupplyWatts">Parameter representing the totalSupplyWatts (float).</param>
        /// <param name="consumerDemands">Parameter representing the consumerDemands (float[]).</param>
        /// <param name="consumerPriorities">Parameter representing the consumerPriorities (int[]).</param>
        /// <returns>Returns allocatedWatts per consumer of type float[].</returns>
        public static float[] Calculate(float totalSupplyWatts, float[] consumerDemands, int[] consumerPriorities)
        {
            if (consumerDemands == null || consumerPriorities == null)
            {
                throw new ArgumentNullException();
            }

            if (consumerDemands.Length != consumerPriorities.Length)
            {
                throw new ArgumentException("Arrays must have the same length");
            }

            if (float.IsNaN(totalSupplyWatts) || float.IsInfinity(totalSupplyWatts))
            {
                totalSupplyWatts = 0f;
            }

            float safeSupply = Math.Max(0f, totalSupplyWatts);
            int len = consumerDemands.Length;
            float[] allocated = new float[len];
            float[] safeDemands = new float[len];

            for (int i = 0; i < len; i++)
            {
                safeDemands[i] = float.IsNaN(consumerDemands[i]) || float.IsInfinity(consumerDemands[i]) ? 0f : Math.Max(0f, consumerDemands[i]);
            }

            int[] sortedIndices = new int[len];
            for (int i = 0; i < len; i++)
            {
                sortedIndices[i] = i;
            }

            Array.Sort(sortedIndices, (a, b) =>
            {
                int priorityComparison = consumerPriorities[b].CompareTo(consumerPriorities[a]);
                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }
                return a.CompareTo(b);
            });

            float remainingSupply = safeSupply;

            for (int i = 0; i < len; i++)
            {
                int idx = sortedIndices[i];
                float demand = safeDemands[idx];

                if (remainingSupply >= demand)
                {
                    allocated[idx] = demand;
                    remainingSupply -= demand;
                }
                else
                {
                    allocated[idx] = remainingSupply;
                    remainingSupply = 0f;
                }
            }

            return allocated;
        }
    }
}
