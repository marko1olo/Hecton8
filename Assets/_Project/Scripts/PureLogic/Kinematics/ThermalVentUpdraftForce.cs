using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ThermalVentUpdraftForce.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ThermalVentUpdraftForce
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="playerPos">Parameter representing the playerPos (Vector3).</param>
        /// <param name="ventCenter">Parameter representing the ventCenter (Vector3).</param>
        /// <param name="ventRadius">Parameter representing the ventRadius (float).</param>
        /// <param name="coreForce">Parameter representing the coreForce (float).</param>
        /// <param name="decayFactor">Parameter representing the decayFactor (float).</param>
        /// <returns>Returns Lift force of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 playerPos, Vector3 ventCenter, float ventRadius, float coreForce, float decayFactor)
        {
            if (ventRadius <= 0f || coreForce == 0f) return Vector3.Zero;

            if (float.IsNaN(playerPos.X) || float.IsNaN(playerPos.Y) || float.IsNaN(playerPos.Z) ||
                float.IsInfinity(playerPos.X) || float.IsInfinity(playerPos.Y) || float.IsInfinity(playerPos.Z) ||
                float.IsNaN(ventCenter.X) || float.IsNaN(ventCenter.Y) || float.IsNaN(ventCenter.Z) ||
                float.IsInfinity(ventCenter.X) || float.IsInfinity(ventCenter.Y) || float.IsInfinity(ventCenter.Z) ||
                float.IsNaN(ventRadius) || float.IsInfinity(ventRadius) ||
                float.IsNaN(coreForce) || float.IsInfinity(coreForce) ||
                float.IsNaN(decayFactor) || float.IsInfinity(decayFactor))
            {
                return Vector3.Zero;
            }

            float dx = playerPos.X - ventCenter.X;
            float dz = playerPos.Z - ventCenter.Z;
            float radialDistSq = dx * dx + dz * dz;
            float radiusSq = ventRadius * ventRadius;

            if (radialDistSq > radiusSq) return Vector3.Zero;
            if (playerPos.Y < ventCenter.Y) return Vector3.Zero;

            float radialDist = (float)Math.Sqrt(radialDistSq);
            float normalizedDist = radialDist / ventRadius;

            float safeDecayFactor = Math.Max(0f, decayFactor);
            float falloff = (float)Math.Pow(1f - normalizedDist, safeDecayFactor);

            falloff = Math.Clamp(falloff, 0f, 1f);

            float forceY = coreForce * falloff;
            return new Vector3(0f, forceY, 0f);
        }
    }
}
