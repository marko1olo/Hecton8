using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for LeviathanTentacleSpringCalculator.
    /// Extracted from LeviathanTentacleVerletSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LeviathanTentacleSpringCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentPos">Parameter representing the currentPos (Vector3).</param>
        /// <param name="prevPos">Parameter representing the prevPos (Vector3).</param>
        /// <param name="anchorPos">Parameter representing the anchorPos (Vector3).</param>
        /// <param name="springStrength">Parameter representing the springStrength (float).</param>
        /// <param name="damping">Parameter representing the damping (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns new segment position of type Vector3.</returns>
        public static Vector3 Compute(Vector3 currentPos, Vector3 prevPos, Vector3 anchorPos, float springStrength, float damping, float deltaTime)
        {
            if (!IsFinite(currentPos)) currentPos = Vector3.Zero;
            if (!IsFinite(prevPos)) prevPos = currentPos;
            if (!IsFinite(anchorPos)) anchorPos = currentPos;

            if (!IsFinite(springStrength)) springStrength = 0f;
            if (!IsFinite(damping)) damping = 0f;
            if (!IsFinite(deltaTime)) deltaTime = 0f;

            float safeDeltaTime = Math.Max(0f, Math.Min(deltaTime, 0.05f));
            float safeDamping = Math.Max(0f, Math.Min(damping, 1f));
            float safeSpringStrength = Math.Max(0f, springStrength);

            float dtSq = safeDeltaTime * safeDeltaTime;

            Vector3 velocity = (currentPos - prevPos) * safeDamping;
            Vector3 acceleration = (anchorPos - currentPos) * safeSpringStrength;

            Vector3 nextPos = currentPos + velocity + acceleration * dtSq;

            if (!IsFinite(nextPos)) return currentPos;

            return nextPos;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }
    }
}
