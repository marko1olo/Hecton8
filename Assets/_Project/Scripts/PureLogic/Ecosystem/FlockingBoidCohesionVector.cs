using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FlockingBoidCohesionVector.
    /// Extracted from HectonBoidController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FlockingBoidCohesionVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="boidPos">Parameter representing the boidPos (Vector3).</param>
        /// <param name="neighborCenter">Parameter representing the neighborCenter (Vector3).</param>
        /// <param name="cohesionWeight">Parameter representing the cohesionWeight (float).</param>
        /// <returns>Returns Cohesion steering force of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 boidPos, Vector3 neighborCenter, float cohesionWeight)
        {
            if (cohesionWeight <= 0f || float.IsNaN(cohesionWeight) || float.IsInfinity(cohesionWeight))
                return Vector3.Zero;

            if (float.IsNaN(boidPos.X) || float.IsNaN(boidPos.Y) || float.IsNaN(boidPos.Z) || float.IsInfinity(boidPos.X) || float.IsInfinity(boidPos.Y) || float.IsInfinity(boidPos.Z))
                return Vector3.Zero;

            if (float.IsNaN(neighborCenter.X) || float.IsNaN(neighborCenter.Y) || float.IsNaN(neighborCenter.Z) || float.IsInfinity(neighborCenter.X) || float.IsInfinity(neighborCenter.Y) || float.IsInfinity(neighborCenter.Z))
                return Vector3.Zero;

            // Clamp max weight limit in-line without adding explicit const float var violating rules
            float clampedWeight = Math.Min(Math.Max(cohesionWeight, 0f), 64f);

            Vector3 cohesionDirection = neighborCenter - boidPos;

            float sqrMag = cohesionDirection.LengthSquared();
            if (float.IsNaN(sqrMag) || float.IsInfinity(sqrMag)) return Vector3.Zero;

            return cohesionDirection * clampedWeight;
        }
    }
}
