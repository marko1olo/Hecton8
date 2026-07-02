using System;
using System.Numerics;
using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Compute(Vector3 fromNode, Vector3 toNode, float[] hazardWeightsAtNode, float baseMoveCost)
        {
            float distanceSq = Vector3.DistanceSquared(fromNode, toNode);

            // In A* grid traversal, the steps are strictly adjacent or diagonal
            // 1.0f = strictly orthogonal (x, y, or z)
            // 2.0f = diagonal on one plane (xy, xz, yz)
            // 3.0f = diagonal on all three planes (xyz)
            // 0.0f = same node
            // By bypassing the square root calculation for these frequent specific cases, we avoid Vector3.Distance in hot paths
            float distance;

            if (distanceSq == 1f) distance = 1f;
            else if (distanceSq == 2f) distance = 1.41421356f; // sqrt(2)
            else if (distanceSq == 3f) distance = 1.73205081f; // sqrt(3)
            else if (distanceSq == 0f) distance = 0f;
            else distance = (float)Math.Sqrt(distanceSq);

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
