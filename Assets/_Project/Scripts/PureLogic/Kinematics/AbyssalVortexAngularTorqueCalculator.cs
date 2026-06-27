using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for AbyssalVortexAngularTorqueCalculator.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AbyssalVortexAngularTorqueCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="hullPos">Parameter representing the hullPos (Vector3).</param>
        /// <param name="vortexCenter">Parameter representing the vortexCenter (Vector3).</param>
        /// <param name="vortexAxis">Parameter representing the vortexAxis (Vector3).</param>
        /// <param name="angularVelocity">Parameter representing the angularVelocity (float).</param>
        /// <param name="hullMass">Parameter representing the hullMass (float).</param>
        /// <returns>Returns Angular velocity impulse torque vector of type Vector3.</returns>
        public static Vector3 Compute(Vector3 hullPos, Vector3 vortexCenter, Vector3 vortexAxis, float angularVelocity, float hullMass)
        {
            if (!IsFinite(hullPos) || !IsFinite(vortexCenter) || !IsFinite(vortexAxis) ||
                float.IsNaN(angularVelocity) || float.IsInfinity(angularVelocity) ||
                float.IsNaN(hullMass) || float.IsInfinity(hullMass))
            {
                return Vector3.Zero;
            }

            if (hullMass <= 0f)
            {
                return Vector3.Zero;
            }

            float axisSq = vortexAxis.LengthSquared();
            if (axisSq <= float.Epsilon)
            {
                return Vector3.Zero;
            }

            Vector3 axisNorm = vortexAxis / (float)Math.Sqrt(axisSq);
            Vector3 offset = hullPos - vortexCenter;

            // Orthogonal distance to the vortex axis using cross product magnitude
            Vector3 tangential = Vector3.Cross(axisNorm, offset);
            float orthogonalDistance = tangential.Length();

            // magnitude scales with distance from center core (orthogonal)
            float torqueMagnitude = orthogonalDistance * angularVelocity * hullMass;

            // Torque vector aligns with vortex axis
            Vector3 torque = axisNorm * torqueMagnitude;

            if (!IsFinite(torque))
            {
                return Vector3.Zero;
            }

            return torque;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X) &&
                   !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) &&
                   !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
