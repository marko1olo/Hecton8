using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for StrafeAngleBlendWeightCalculator.
    /// Extracted from PlayerKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StrafeAngleBlendWeightCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="velocityDir">Parameter representing the velocityDir (Vector3).</param>
        /// <param name="facingDir">Parameter representing the facingDir (Vector3).</param>
        /// <param name="fullStrafeAngleDeg">Parameter representing the fullStrafeAngleDeg (float).</param>
        /// <returns>Returns -1.0 left, 0 forward, 1.0 right of type float strafeBlendWeight.</returns>
        public static float Compute(Vector3 velocityDir, Vector3 facingDir, float fullStrafeAngleDeg)
        {
            if (!IsFinite(velocityDir) || !IsFinite(facingDir) ||
                float.IsNaN(fullStrafeAngleDeg) || float.IsInfinity(fullStrafeAngleDeg) ||
                fullStrafeAngleDeg <= 0.000001f)
            {
                return 0f;
            }

            // Project onto XZ plane
            Vector3 v = new Vector3(velocityDir.X, 0f, velocityDir.Z);
            Vector3 f = new Vector3(facingDir.X, 0f, facingDir.Z);

            float lenVSq = v.LengthSquared();
            float lenFSq = f.LengthSquared();

            if (lenVSq < 0.000001f || lenFSq < 0.000001f || float.IsInfinity(lenVSq) || float.IsInfinity(lenFSq))
            {
                return 0f;
            }

            v /= (float)Math.Sqrt(lenVSq);
            f /= (float)Math.Sqrt(lenFSq);

            float dot = Vector3.Dot(f, v);
            dot = Math.Clamp(dot, -1f, 1f);

            // Cross product Y component for signed angle
            float crossY = f.Z * v.X - f.X * v.Z;

            float angleRad = (float)Math.Atan2(crossY, dot);
            float angleDeg = angleRad * (180f / (float)Math.PI);

            float weight = angleDeg / fullStrafeAngleDeg;

            return Math.Clamp(weight, -1f, 1f);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.X) || float.IsInfinity(v.X) ||
                     float.IsNaN(v.Y) || float.IsInfinity(v.Y) ||
                     float.IsNaN(v.Z) || float.IsInfinity(v.Z));
        }
    }
}
