using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaObstacleAvoidanceVector.
    /// Extracted from HectonDirectorAI.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaObstacleAvoidanceVector
    {
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="forwardDirection">Parameter representing the forwardDirection (Vector3).</param>
        /// <param name="hitNormal">Parameter representing the hitNormal (Vector3).</param>
        /// <param name="distanceToObstacle">Parameter representing the distanceToObstacle (float).</param>
        /// <param name="avoidanceRadius">Parameter representing the avoidanceRadius (float).</param>
        /// <returns>Returns Avoidance steering vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 forwardDirection, Vector3 hitNormal, float distanceToObstacle, float avoidanceRadius)
        {
            // Sanitize inputs
            if (!IsFinite(forwardDirection) || !IsFinite(hitNormal))
            {
                return Vector3.Zero;
            }

            if (float.IsNaN(distanceToObstacle) || float.IsInfinity(distanceToObstacle))
            {
                distanceToObstacle = 0f;
            }

            if (float.IsNaN(avoidanceRadius) || float.IsInfinity(avoidanceRadius))
            {
                avoidanceRadius = 0f;
            }

            // Clamp boundary
            distanceToObstacle = Math.Max(0f, distanceToObstacle);
            avoidanceRadius = Math.Max(0f, avoidanceRadius);

            if (avoidanceRadius <= Epsilon)
            {
                return Vector3.Zero;
            }

            if (distanceToObstacle >= avoidanceRadius)
            {
                return Vector3.Zero;
            }

            // Normal needs to point somewhere valid
            if (hitNormal.LengthSquared() <= Epsilon)
            {
                return Vector3.Zero;
            }

            // Normalize safely
            Vector3 safeNormal = Vector3.Normalize(hitNormal);

            // Compute inversely scaled push force based on distance
            float pushIntensity = (avoidanceRadius - distanceToObstacle) / avoidanceRadius;
            pushIntensity = Math.Max(0f, Math.Min(1f, pushIntensity));

            // Force scales inversely with distance and points along normal
            return safeNormal * pushIntensity;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
