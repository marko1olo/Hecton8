using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for LedgeGrabImpulseCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LedgeGrabImpulseCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='playerVelocity'>Parameter representing the playerVelocity (Vector3).</param>
        /// <param name='ledgeNormal'>Parameter representing the ledgeNormal (Vector3).</param>
        /// <param name='pullUpForce'>Parameter representing the pullUpForce (float).</param>
        /// <param name='cancelFraction'>Parameter representing the cancelFraction (float).</param>
        /// <returns>Returns new velocity after ledge grab impulse applied of type Vector3.</returns>
        public static Vector3 Compute(Vector3 playerVelocity, Vector3 ledgeNormal, float pullUpForce, float cancelFraction)
        {
            if (float.IsNaN(playerVelocity.X) || float.IsNaN(playerVelocity.Y) || float.IsNaN(playerVelocity.Z) ||
                float.IsNaN(ledgeNormal.X) || float.IsNaN(ledgeNormal.Y) || float.IsNaN(ledgeNormal.Z) ||
                float.IsNaN(pullUpForce) || float.IsNaN(cancelFraction))
            {
                return Vector3.Zero;
            }

            if (float.IsInfinity(playerVelocity.X) || float.IsInfinity(playerVelocity.Y) || float.IsInfinity(playerVelocity.Z) ||
                float.IsInfinity(ledgeNormal.X) || float.IsInfinity(ledgeNormal.Y) || float.IsInfinity(ledgeNormal.Z) ||
                float.IsInfinity(pullUpForce) || float.IsInfinity(cancelFraction))
            {
                return Vector3.Zero;
            }

            cancelFraction = Math.Clamp(cancelFraction, 0f, 1f);
            pullUpForce = Math.Max(0f, pullUpForce);

            // Ledge normal check
            if (ledgeNormal.LengthSquared() < 0.0001f)
            {
                ledgeNormal = Vector3.UnitY;
            }
            else
            {
                ledgeNormal = Vector3.Normalize(ledgeNormal);
            }

            // We project out the inward velocity relative to the ledge to avoid clipping through.
            // If the player is moving into the ledge (dot < 0), we cancel that component.
            Vector3 newVelocity = playerVelocity;
            float inwardDot = Vector3.Dot(newVelocity, ledgeNormal);
            if (inwardDot < 0f)
            {
                newVelocity -= ledgeNormal * inwardDot;
            }

            // Cancel vertical velocity by cancelFraction
            newVelocity.Y *= (1f - cancelFraction);

            // Add pullUpForce upwards
            newVelocity.Y += pullUpForce;

            return newVelocity;
        }
    }
}
