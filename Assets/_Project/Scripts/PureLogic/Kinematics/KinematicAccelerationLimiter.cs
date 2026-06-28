using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for KinematicAccelerationLimiter.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class KinematicAccelerationLimiter
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentVelocity">Parameter representing the currentVelocity (Vector3).</param>
        /// <param name="targetVelocity">Parameter representing the targetVelocity (Vector3).</param>
        /// <param name="maxAcceleration">Parameter representing the maxAcceleration (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns Clamped velocity delta of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 currentVelocity, Vector3 targetVelocity, float maxAcceleration, float deltaTime)
        {
            if (float.IsNaN(maxAcceleration) || float.IsInfinity(maxAcceleration))
            {
                return Vector3.Zero;
            }

            if (maxAcceleration <= 0f || deltaTime <= 0f)
            {
                return Vector3.Zero;
            }

            if (!IsFinite(currentVelocity) || !IsFinite(targetVelocity))
            {
                return Vector3.Zero;
            }

            Vector3 deltaVelocity = targetVelocity - currentVelocity;
            float maxDeltaVelocityMagnitude = maxAcceleration * deltaTime;

            float deltaVelocitySqrMagnitude = deltaVelocity.LengthSquared();
            if (deltaVelocitySqrMagnitude <= 0.000001f)
            {
                return deltaVelocity;
            }

            float maxDeltaVelocitySqrMagnitude = maxDeltaVelocityMagnitude * maxDeltaVelocityMagnitude;
            if (deltaVelocitySqrMagnitude > maxDeltaVelocitySqrMagnitude)
            {
                float invMagnitude = 1f / MathF.Sqrt(deltaVelocitySqrMagnitude);
                deltaVelocity *= maxDeltaVelocityMagnitude * invMagnitude;
            }

            return deltaVelocity;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
