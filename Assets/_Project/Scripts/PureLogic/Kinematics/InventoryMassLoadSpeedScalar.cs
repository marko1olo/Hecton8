using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for InventoryMassLoadSpeedScalar.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class InventoryMassLoadSpeedScalar
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="carriedMassKg">Parameter representing the carriedMassKg (float).</param>
        /// <param name="carryCapacityKg">Parameter representing the carryCapacityKg (float).</param>
        /// <param name="baseSpeed">Parameter representing the baseSpeed (float).</param>
        /// <returns>Returns Final velocity speed scalar of type float.</returns>
        public static float Calculate(float carriedMassKg, float carryCapacityKg, float baseSpeed)
        {
            if (float.IsNaN(carriedMassKg) || float.IsInfinity(carriedMassKg)) carriedMassKg = 0f;
            if (float.IsNaN(carryCapacityKg) || float.IsInfinity(carryCapacityKg)) carryCapacityKg = 1f;
            if (float.IsNaN(baseSpeed) || float.IsInfinity(baseSpeed)) baseSpeed = 0f;

            float safeMass = Math.Max(0f, carriedMassKg);
            float safeCapacity = Math.Max(0.001f, carryCapacityKg);

            float loadRatio = safeMass / safeCapacity;
            float clampedLoad = Math.Max(0f, Math.Min(1f, loadRatio));

            float logProgress = (float)(Math.Log(1.0 + clampedLoad) / Math.Log(2.0));

            return 1.0f - (1.0f - baseSpeed) * logProgress;
        }
    }
}
