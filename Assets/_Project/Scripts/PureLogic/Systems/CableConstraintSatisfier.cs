using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CableConstraintSatisfier.
    /// Extracted from CablePhysicsSolver132.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CableConstraintSatisfier
    {
        private const float MinConstraintLengthSq = 1e-6f;
        private const float MinConstraintLength = 1e-3f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="posA">Parameter representing the posA (Vector3).</param>
        /// <param name="posB">Parameter representing the posB (Vector3).</param>
        /// <param name="restLength">Parameter representing the restLength (float).</param>
        /// <param name="stiffness">Parameter representing the stiffness (float).</param>
        /// <returns>Returns Vector3 newPosA, Vector3 newPosB of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 posA, Vector3 posB, float restLength, float stiffness)
        {
            if (!IsFinite(posA) || !IsFinite(posB) || !IsFinite(restLength) || !IsFinite(stiffness))
                return Vector3.Zero;

            Vector3 delta = posB - posA;
            float lenSq = delta.LengthSquared();

            if (lenSq <= MinConstraintLengthSq)
            {
                return Vector3.Zero; // Too close, avoid division by zero
            }

            float len = (float)Math.Sqrt(lenSq);
            float invLen = 1f / len;

            float safeRestLength = Math.Max(MinConstraintLength, restLength);
            float error = len - safeRestLength;

            float safeStiffness = Math.Clamp(stiffness, 0f, 1f);

            // Correction calculated assuming equal mass (invMassA = 1, invMassB = 1 -> invMassSum = 2)
            float correctionFactor = (error * invLen * safeStiffness) * 0.5f;
            Vector3 correction = delta * correctionFactor;

            if (!IsFinite(correction))
                return Vector3.Zero;

            return correction;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
    }
}
