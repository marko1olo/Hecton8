using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for BuoyancyDensityRatioMath.
    /// Extracted from HydrodynamicKccRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BuoyancyDensityRatioMath
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='playerDensity'>Parameter representing the playerDensity (float).</param>
        /// <param name='fluidDensity'>Parameter representing the fluidDensity (float).</param>
        /// <param name='displacedVolume'>Parameter representing the displacedVolume (float).</param>
        /// <param name='gravity'>Parameter representing the gravity (float).</param>
        /// <returns>Returns net buoyancy force in Newtons of type float.</returns>
        public static float Calculate(float playerDensity, float fluidDensity, float displacedVolume, float gravity)
        {
            if (float.IsNaN(playerDensity) || float.IsInfinity(playerDensity)) playerDensity = 0f;
            if (float.IsNaN(fluidDensity) || float.IsInfinity(fluidDensity)) fluidDensity = 0f;
            if (float.IsNaN(displacedVolume) || float.IsInfinity(displacedVolume)) displacedVolume = 0f;
            if (float.IsNaN(gravity) || float.IsInfinity(gravity)) gravity = 0f;

            if (playerDensity < 0f) playerDensity = 0f;
            if (fluidDensity < 0f) fluidDensity = 0f;
            if (displacedVolume < 0f) displacedVolume = 0f;

            // F_buoyancy = fluidDensity * displacedVolume * gravity
            // F_gravity = playerMass * gravity
            // playerMass = playerDensity * displacedVolume
            // F_net = F_buoyancy - F_gravity
            // F_net = (fluidDensity - playerDensity) * displacedVolume * gravity

            return (fluidDensity - playerDensity) * displacedVolume * gravity;
        }
    }
}
