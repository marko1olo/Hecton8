using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DronePathfindCostCalculator.
    /// Extracted from DroneFleetNavigationKernel.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DronePathfindCostCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="fromNode">Parameter representing the fromNode (Vector3).</param>
        /// <param name="toNode">Parameter representing the toNode (Vector3).</param>
        /// <param name="hazardWeightsAtNode">Parameter representing the hazardWeightsAtNode (float[]).</param>
        /// <param name="baseMoveCost">Parameter representing the baseMoveCost (float).</param>
        /// <returns>Returns traversalCost of type float.</returns>
        public static float Compute(Vector3 fromNode, Vector3 toNode, float[] hazardWeightsAtNode, float baseMoveCost)
        {
            float distance = Vector3.Distance(fromNode, toNode);
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return float.MaxValue;
            }

            float hazardSum = 0f;
            if (hazardWeightsAtNode != null)
            {
                for (int i = 0; i < hazardWeightsAtNode.Length; i++)
                {
                    float weight = hazardWeightsAtNode[i];
                    if (float.IsNaN(weight) || float.IsInfinity(weight))
                    {
                        return float.MaxValue;
                    }
                    hazardSum += Math.Max(0f, weight);
                }
            }

            if (float.IsNaN(hazardSum) || float.IsInfinity(hazardSum))
            {
                return float.MaxValue;
            }

            float safeBaseCost = Math.Max(0f, baseMoveCost);
            if (float.IsNaN(safeBaseCost) || float.IsInfinity(safeBaseCost))
            {
                return float.MaxValue;
            }

            float result = (distance * safeBaseCost) + hazardSum;
            if (float.IsNaN(result) || float.IsInfinity(result) || result < 0f)
            {
                return float.MaxValue;
            }

            return result;
        }
    }
}
