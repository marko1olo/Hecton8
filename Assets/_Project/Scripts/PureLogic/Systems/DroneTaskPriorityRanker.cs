using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for DroneTaskPriorityRanker.
    /// Extracted from DroneFleetManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class DroneTaskPriorityRanker
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='urgency'>Parameter representing the urgency (float).</param>
        /// <param name='proximity01'>Parameter representing the proximity01 (float).</param>
        /// <param name='resourceAvailability01'>Parameter representing the resourceAvailability01 (float).</param>
        /// <param name='weights'>Parameter representing the weights (float[]).</param>
        /// <returns>Returns priorityScore of type float.</returns>
        public static float Calculate(float urgency, float proximity01, float resourceAvailability01, float[] weights)
        {
            if (weights == null || weights.Length < 3)
            {
                return 0f;
            }

            if (float.IsNaN(urgency) || float.IsInfinity(urgency) ||
                float.IsNaN(proximity01) || float.IsInfinity(proximity01) ||
                float.IsNaN(resourceAvailability01) || float.IsInfinity(resourceAvailability01))
            {
                return 0f;
            }

            float safeUrgency = Math.Max(0f, urgency);
            float safeProximity = Math.Clamp(proximity01, 0f, 1f);
            float safeResources = Math.Clamp(resourceAvailability01, 0f, 1f);

            float score = (safeUrgency * weights[0]) + (safeProximity * weights[1]) + (safeResources * weights[2]);

            return score;
        }
    }
}
