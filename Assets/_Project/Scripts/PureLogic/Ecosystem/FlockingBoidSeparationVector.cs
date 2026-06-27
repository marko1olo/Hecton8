using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FlockingBoidSeparationVector.
    /// Extracted from HectonBoidController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FlockingBoidSeparationVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="boidPos">Parameter representing the boidPos (Vector3).</param>
        /// <param name="obstaclePos">Parameter representing the obstaclePos (Vector3).</param>
        /// <param name="minDistance">Parameter representing the minDistance (float).</param>
        /// <returns>Returns Separation steering force of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 boidPos, Vector3 obstaclePos, float minDistance)
        {
            if (minDistance <= 0f || float.IsNaN(minDistance) || float.IsInfinity(minDistance))
                return Vector3.Zero;

            if (!IsFinite(boidPos) || !IsFinite(obstaclePos))
                return Vector3.Zero;

            Vector3 offset = boidPos - obstaclePos;
            float sqrDistance = offset.LengthSquared();

            // To prevent divide by zero logic in testing edge cases
            if (sqrDistance == 0f)
                return Vector3.UnitY * minDistance; // Arbitrary push if directly on top

            if (sqrDistance >= minDistance * minDistance)
                return Vector3.Zero;

            float distance = MathF.Sqrt(sqrDistance);

            // Force magnitude scales inversely with distance
            float forceMagnitude = (minDistance - distance) / minDistance;

            Vector3 direction = offset / distance;

            return direction * forceMagnitude;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
