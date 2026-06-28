using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ThermoclineResistanceCalculator.
    /// Extracted from HydrodynamicKccRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ThermoclineResistanceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDepth">Parameter representing the currentDepth (float).</param>
        /// <param name="thermoclineDepth">Parameter representing the thermoclineDepth (float).</param>
        /// <param name="thermoclineThickness">Parameter representing the thermoclineThickness (float).</param>
        /// <param name="playerSpeed">Parameter representing the playerSpeed (float).</param>
        /// <param name="resistanceForce">Parameter representing the resistanceForce (float).</param>
        /// <returns>Returns resistance multiplier 0.0-1.0 of type float.</returns>
        public static float Compute(float currentDepth, float thermoclineDepth, float thermoclineThickness, float playerSpeed, float resistanceForce)
        {
            if (thermoclineThickness <= 0f) return 0f;
            if (resistanceForce <= 0f) return 0f;
            if (float.IsNaN(currentDepth) || float.IsInfinity(currentDepth) ||
                float.IsNaN(thermoclineDepth) || float.IsInfinity(thermoclineDepth) ||
                float.IsNaN(thermoclineThickness) || float.IsInfinity(thermoclineThickness) ||
                float.IsNaN(playerSpeed) || float.IsInfinity(playerSpeed) ||
                float.IsNaN(resistanceForce) || float.IsInfinity(resistanceForce))
            {
                return 0f;
            }

            float distanceToThermocline = Math.Abs(currentDepth - thermoclineDepth);
            float halfThickness = thermoclineThickness * 0.5f;

            if (distanceToThermocline >= halfThickness)
            {
                return 0f;
            }

            float normalizedDistance = distanceToThermocline / halfThickness;
            float falloff = 1f - (normalizedDistance * normalizedDistance);

            // Resistance is based on the falloff directly multiplied by resistanceForce and normalized playerSpeed
            float resistanceMultiplier = falloff * playerSpeed * resistanceForce;

            return Math.Clamp(resistanceMultiplier, 0f, 1f);
        }
    }
}
