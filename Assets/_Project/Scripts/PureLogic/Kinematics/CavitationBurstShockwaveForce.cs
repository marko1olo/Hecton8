using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for CavitationBurstShockwaveForce.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CavitationBurstShockwaveForce
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="bodyPos">Parameter representing the bodyPos (Vector3).</param>
        /// <param name="burstCenter">Parameter representing the burstCenter (Vector3).</param>
        /// <param name="burstEnergy">Parameter representing the burstEnergy (float).</param>
        /// <param name="waterDensity">Parameter representing the waterDensity (float).</param>
        /// <returns>Returns Blast acceleration impulse vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 bodyPos, Vector3 burstCenter, float burstEnergy, float waterDensity)
        {
            // Edge cases
            if (float.IsNaN(bodyPos.X) || float.IsNaN(bodyPos.Y) || float.IsNaN(bodyPos.Z) ||
                float.IsNaN(burstCenter.X) || float.IsNaN(burstCenter.Y) || float.IsNaN(burstCenter.Z) ||
                float.IsNaN(burstEnergy) || float.IsNaN(waterDensity) ||
                float.IsInfinity(bodyPos.X) || float.IsInfinity(bodyPos.Y) || float.IsInfinity(bodyPos.Z) ||
                float.IsInfinity(burstCenter.X) || float.IsInfinity(burstCenter.Y) || float.IsInfinity(burstCenter.Z) ||
                float.IsInfinity(burstEnergy) || float.IsInfinity(waterDensity))
            {
                return Vector3.Zero;
            }

            if (burstEnergy <= 0f || waterDensity <= 0f)
            {
                return Vector3.Zero;
            }

            Vector3 radial = bodyPos - burstCenter;
            float radialDistanceSq = radial.LengthSquared();

            // Protect against division by zero.
            if (radialDistanceSq < 0.000001f)
            {
                return Vector3.Zero; // Body is exactly at center, forces cancel out or undefined direction
            }

            float distance = (float)Math.Sqrt(radialDistanceSq);

            // Normalize
            Vector3 direction = radial / distance;

            // Formula constraints: Inverse-cube falloff over distance. Energy scales impulse linearly.
            // Pressure P = Energy / (Distance^3 * Density) ?
            // We just need to implement inverse cube falloff: 1 / (distance^3).

            float distanceCubed = distance * distance * distance;

            // Avoid overflow when distance is very small
            if (distanceCubed < 0.0001f)
            {
                 distanceCubed = 0.0001f;
            }

            // Calculation based on standard theoretical models for spherical underwater explosions (simplified)
            // or just satisfying constraints: energy scales impulse linearly, inverse-cube falloff
            float magnitude = burstEnergy / (distanceCubed * waterDensity);

            // Extreme output clamping
            if (float.IsInfinity(magnitude) || float.IsNaN(magnitude))
            {
                return Vector3.Zero;
            }

            return direction * magnitude;
        }
    }
}
