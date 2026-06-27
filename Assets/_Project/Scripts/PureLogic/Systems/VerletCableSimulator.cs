using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VerletCableSimulator.
    /// Extracted from CablePhysicsSolver132.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VerletCableSimulator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentPos">Parameter representing the currentPos (Vector3).</param>
        /// <param name="prevPos">Parameter representing the prevPos (Vector3).</param>
        /// <param name="segmentRestLength">Parameter representing the segmentRestLength (float).</param>
        /// <param name="gravity">Parameter representing the gravity (Vector3).</param>
        /// <param name="dampingFactor">Parameter representing the dampingFactor (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newPos of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 currentPos, Vector3 prevPos, float segmentRestLength, Vector3 gravity, float dampingFactor, float deltaTime)
        {
            if (!IsFinite(currentPos) || !IsFinite(prevPos) || !IsFinite(gravity) ||
                float.IsNaN(segmentRestLength) || float.IsInfinity(segmentRestLength) ||
                float.IsNaN(dampingFactor) || float.IsInfinity(dampingFactor) ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                // Fallback to current position if inputs are invalid
                return IsFinite(currentPos) ? currentPos : Vector3.Zero;
            }

            float dt = Math.Max(0.0f, deltaTime);
            float damping = Clamp(dampingFactor, 0.0f, 1.0f);

            Vector3 velocity = (currentPos - prevPos) * damping;
            Vector3 acceleration = gravity;

            Vector3 step = velocity + acceleration * (dt * dt);

            return currentPos + step;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
