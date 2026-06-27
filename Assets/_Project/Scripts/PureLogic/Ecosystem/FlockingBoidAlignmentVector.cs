using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FlockingBoidAlignmentVector.
    /// Extracted from HectonBoidController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FlockingBoidAlignmentVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="boidVelocity">Parameter representing the boidVelocity (Vector3).</param>
        /// <param name="averageNeighborVelocity">Parameter representing the averageNeighborVelocity (Vector3).</param>
        /// <param name="maxSteerForce">Parameter representing the maxSteerForce (float).</param>
        /// <returns>Returns Alignment steer vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 boidVelocity, Vector3 averageNeighborVelocity, float maxSteerForce)
        {
            // Calculate steering force to align boid forward vector with average neighbor velocities.

            // Check max steer force for bounds
            if (float.IsNaN(maxSteerForce) || float.IsInfinity(maxSteerForce) || maxSteerForce <= 0f)
            {
                return Vector3.Zero;
            }

            if (!IsFinite(boidVelocity) || !IsFinite(averageNeighborVelocity))
            {
                return Vector3.Zero;
            }

            // If average neighbor velocity is near zero, no alignment can be calculated
            if (averageNeighborVelocity.LengthSquared() < 0.000001f)
            {
                return Vector3.Zero;
            }

            // From compute shader:
            // float3 avgVel    = alignmentSum * invNeighbours;
            // float3 desired   = SafeNormalize(avgVel) * _MaxSpeed;
            // float3 steer     = (desired - vel) * hasNeighbours;
            // acceleration    += steer * _AlignmentWeight;
            // NOTE: Here _MaxSpeed seems to be missing from the parameters if we want to follow the shader exactly,
            // but the instructions ask us to calculate the steer vector up to maxSteerForce clamping.
            // Let's interpret "desired" velocity as the normalized neighbor velocity * speed.
            // The instructions say "Calculate steering force to align boid forward vector with average neighbor velocities."
            // A standard boid alignment force:
            // desired = normalize(averageNeighborVelocity)
            // steer = desired - boidVelocity
            // steer = clamp_magnitude(steer, maxSteerForce)
            // Wait, wait, actually if averageNeighborVelocity is already the desired velocity, then steer is just avg neighbor vel - boid vel.
            // If avg neighbor velocity is the exact velocity of neighbors, standard boid alignment is:
            // desired velocity = normalize(averageNeighborVelocity) * maxSpeed
            // But we don't have maxSpeed here. We only have averageNeighborVelocity.
            // So perhaps steer = averageNeighborVelocity - boidVelocity, then clamp magnitude to maxSteerForce.

            Vector3 steer = averageNeighborVelocity - boidVelocity;

            float steerSqrMag = steer.LengthSquared();
            if (steerSqrMag > maxSteerForce * maxSteerForce && steerSqrMag > 0f)
            {
                steer = Vector3.Normalize(steer) * maxSteerForce;
            }

            return steer;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
