using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for InertiaTransferCalculator.
    /// Extracted from PlayerKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class InertiaTransferCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='vehicleVelocity'>Parameter representing the vehicleVelocity (Vector3).</param>
        /// <param name='playerVelocity'>Parameter representing the playerVelocity (Vector3).</param>
        /// <param name='transferFraction'>Parameter representing the transferFraction (float).</param>
        /// <param name='playerMass'>Parameter representing the playerMass (float).</param>
        /// <param name='vehicleMass'>Parameter representing the vehicleMass (float).</param>
        /// <returns>Returns player velocity after momentum transfer of type Vector3.</returns>
        public static Vector3 Compute(Vector3 vehicleVelocity, Vector3 playerVelocity, float transferFraction, float playerMass, float vehicleMass)
        {
            if (!IsFinite(vehicleVelocity) || !IsFinite(playerVelocity) || float.IsNaN(transferFraction) || float.IsNaN(playerMass) || float.IsNaN(vehicleMass))
            {
                return IsFinite(playerVelocity) ? playerVelocity : Vector3.Zero;
            }

            float safePlayerMass = Math.Max(0f, playerMass);
            float safeVehicleMass = Math.Max(0f, vehicleMass);
            float safeTransfer = Math.Clamp(transferFraction, 0f, 1f);

            double totalMass = (double)safePlayerMass + (double)safeVehicleMass;

            Vector3 targetVelocity;
            if (totalMass > 0.0)
            {
                double px = (double)playerVelocity.X * safePlayerMass;
                double py = (double)playerVelocity.Y * safePlayerMass;
                double pz = (double)playerVelocity.Z * safePlayerMass;

                double vx = (double)vehicleVelocity.X * safeVehicleMass;
                double vy = (double)vehicleVelocity.Y * safeVehicleMass;
                double vz = (double)vehicleVelocity.Z * safeVehicleMass;

                double finalX = (px + vx) / totalMass;
                double finalY = (py + vy) / totalMass;
                double finalZ = (pz + vz) / totalMass;

                targetVelocity = new Vector3(
                    ClampToFloat(finalX),
                    ClampToFloat(finalY),
                    ClampToFloat(finalZ)
                );
            }
            else
            {
                targetVelocity = vehicleVelocity;
            }

            return Vector3.Lerp(playerVelocity, targetVelocity, safeTransfer);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }

        private static float ClampToFloat(double value)
        {
            if (double.IsNaN(value)) return 0f;
            if (value > float.MaxValue) return float.MaxValue;
            if (value < float.MinValue) return float.MinValue;
            return (float)value;
        }
    }
}
