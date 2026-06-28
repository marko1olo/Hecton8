using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ParasiteLatchDragCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ParasiteLatchDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="latchedCount">Parameter representing the latchedCount (int).</param>
        /// <param name="currentVelocity">Parameter representing the currentVelocity (Vector3).</param>
        /// <param name="dragCoefficient">Parameter representing the dragCoefficient (float).</param>
        /// <param name="harvesterPull">Parameter representing the harvesterPull (Vector3).</param>
        /// <param name="maxLatchedCap">Maximum number of parasites for scaling.</param>
        /// <returns>Returns Drift velocity offset of type Vector3.</returns>
        public static Vector3 Compute(
            int latchedCount,
            Vector3 currentVelocity,
            float dragCoefficient,
            Vector3 harvesterPull,
            int maxLatchedCap = 64)
        {
            if (latchedCount <= 0)
            {
                return Vector3.Zero;
            }

            if (!IsFinite(currentVelocity) || float.IsNaN(dragCoefficient) || float.IsInfinity(dragCoefficient) || !IsFinite(harvesterPull))
            {
                return Vector3.Zero;
            }

            int clampedCount = Math.Clamp(latchedCount, 0, Math.Max(1, maxLatchedCap));
            float safeDragCoeff = Math.Max(0f, dragCoefficient);

            // Exponential scaling up to a cap
            float countRatio = (float)clampedCount / Math.Max(1, maxLatchedCap);
            float scale = countRatio * countRatio;

            // Calculate drift velocity penalty from drag (opposes current velocity)
            Vector3 dragPenalty = -currentVelocity * (safeDragCoeff * scale);

            // The harvester pull is an offset in the direction of the pull.
            Vector3 pullOffset = harvesterPull * scale;

            Vector3 result = dragPenalty + pullOffset;

            // Boundary Guarding: robust check against overflow
            if (!IsFinite(result))
            {
                return Vector3.Zero;
            }

            return result;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
