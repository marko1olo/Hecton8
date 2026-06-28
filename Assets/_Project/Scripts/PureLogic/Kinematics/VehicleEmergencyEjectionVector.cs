using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for VehicleEmergencyEjectionVector.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VehicleEmergencyEjectionVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='vehicleVel'>Parameter representing the vehicleVel (Vector3).</param>
        /// <param name='vehicleForward'>Parameter representing the vehicleForward (Vector3).</param>
        /// <param name='vehicleUp'>Parameter representing the vehicleUp (Vector3).</param>
        /// <param name='severity'>Parameter representing the severity (float).</param>
        /// <returns>Returns Ejection impulse vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 vehicleVel, Vector3 vehicleForward, Vector3 vehicleUp, float severity)
        {
            return CalculateImpulse(vehicleVel, vehicleForward, vehicleUp, severity, 7.5f, 3.4f, 0.85f, 1.35f, 0.75f, 1.2f, 0.0001f);
        }

        public static Vector3 CalculateImpulse(
            Vector3 vehicleVel,
            Vector3 vehicleForward,
            Vector3 vehicleUp,
            float severity,
            float horizontalImpulseScale = 7.5f,
            float verticalImpulseScale = 3.4f,
            float horizontalLerpMin = 0.85f,
            float horizontalLerpMax = 1.35f,
            float verticalLerpMin = 0.75f,
            float verticalLerpMax = 1.2f,
            float epsilonSquared = 0.0001f)
        {
            if (float.IsNaN(vehicleVel.X) || float.IsNaN(vehicleVel.Y) || float.IsNaN(vehicleVel.Z) ||
                float.IsNaN(vehicleForward.X) || float.IsNaN(vehicleForward.Y) || float.IsNaN(vehicleForward.Z) ||
                float.IsNaN(vehicleUp.X) || float.IsNaN(vehicleUp.Y) || float.IsNaN(vehicleUp.Z) ||
                float.IsNaN(severity) || float.IsNaN(horizontalImpulseScale) || float.IsNaN(verticalImpulseScale) ||
                float.IsNaN(horizontalLerpMin) || float.IsNaN(horizontalLerpMax) || float.IsNaN(verticalLerpMin) || float.IsNaN(verticalLerpMax) || float.IsNaN(epsilonSquared))
            {
                return Vector3.Zero;
            }

            severity = Math.Clamp(severity, 0f, 1f);

            Vector3 planarVelocity = new Vector3(vehicleVel.X, 0f, vehicleVel.Z);
            Vector3 lateralDirection = Vector3.Zero;

            if (planarVelocity.LengthSquared() > epsilonSquared)
            {
                // Note: The original code used a fallback to NormalizeVectorRsqrt which is approximated here by Vector3.Normalize
                // In HectonPlayerMovement.cs:
                // lateralDirection = -NormalizeVectorRsqrt(planarVelocity, Vector3.zero);
                lateralDirection = -Vector3.Normalize(planarVelocity);
            }
            else
            {
                // Fallback to -vehicleForward if no velocity
                // In HectonPlayerMovement.cs:
                // Vector3 fallbackLateralDirection = new Vector3(-fallbackSinYaw, 0f, -fallbackCosYaw);
                // The fallback is effectively -bodyYaw (which is -forward when projected on XZ)
                Vector3 fallbackDir = -vehicleForward;
                fallbackDir.Y = 0f;
                if (fallbackDir.LengthSquared() > epsilonSquared)
                {
                    lateralDirection = Vector3.Normalize(fallbackDir);
                }
                else
                {
                    lateralDirection = new Vector3(0, 0, -1);
                }
            }

            float horizontalLerp = horizontalLerpMin + (horizontalLerpMax - horizontalLerpMin) * severity;
            float verticalLerp = verticalLerpMin + (verticalLerpMax - verticalLerpMin) * severity;

            Vector3 bailoutImpulse = lateralDirection * (horizontalImpulseScale * horizontalLerp);
            bailoutImpulse.Y += verticalImpulseScale * verticalLerp;

            if (float.IsInfinity(bailoutImpulse.X) || float.IsInfinity(bailoutImpulse.Y) || float.IsInfinity(bailoutImpulse.Z) ||
                float.IsNaN(bailoutImpulse.X) || float.IsNaN(bailoutImpulse.Y) || float.IsNaN(bailoutImpulse.Z))
            {
                return Vector3.Zero;
            }

            return bailoutImpulse;
        }
    }
}
