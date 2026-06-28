using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SubmergedBuoyancyForce.
    /// Extracted from BuoyancyObject.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SubmergedBuoyancyForce
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="volumeM3">Parameter representing the volumeM3 (float).</param>
        /// <param name="fluidDensity">Parameter representing the fluidDensity (float).</param>
        /// <param name="submergedVolume01">Parameter representing the submergedVolume01 (float).</param>
        /// <param name="gravity">Parameter representing the gravity (Vector3).</param>
        /// <returns>Returns Buoyancy force vector of type Vector3.</returns>
        public static Vector3 Calculate(float volumeM3, float fluidDensity, float submergedVolume01, Vector3 gravity)
        {
            // Calculate displaced volume
            // Submerged volume fraction should be between 0 and 1
            float clampedSubmergedVolume01 = Math.Max(0f, Math.Min(submergedVolume01, 1f));

            // Ensure volume is positive
            float safeVolume = Math.Max(0f, volumeM3);

            // Ensure fluid density is non-negative
            float safeFluidDensity = Math.Max(0f, fluidDensity);

            float displacedVolume = safeVolume * clampedSubmergedVolume01;

            // Buoyancy force = - (displaced_volume * fluid_density * gravity)
            Vector3 force = -gravity * displacedVolume * safeFluidDensity;

            // Guard against NaN or Infinity
            if (float.IsNaN(force.X) || float.IsNaN(force.Y) || float.IsNaN(force.Z) ||
                float.IsInfinity(force.X) || float.IsInfinity(force.Y) || float.IsInfinity(force.Z))
            {
                return Vector3.Zero;
            }

            return force;
        }
    }
}
