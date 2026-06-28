using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SurvivalSuitOxygenBurnRate.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SurvivalSuitOxygenBurnRate
    {
        private const float BaseInputFallback = 0f;
        private const float PressureInputFallback = 1f;
        private const float PressureExponent = 1.5f;
        private const float ThrusterExponent = 2.0f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseO2Rate">Parameter representing the baseO2Rate (float).</param>
        /// <param name="movementStaminaBurn">Parameter representing the movementStaminaBurn (float).</param>
        /// <param name="ambientPressure">Parameter representing the ambientPressure (float).</param>
        /// <returns>Returns Oxygen usage per second of type float.</returns>
        public static float Calculate(float baseO2Rate, float movementStaminaBurn, float ambientPressure)
        {
            float safeBaseO2Rate = ValidateInput(baseO2Rate, BaseInputFallback, BaseInputFallback);
            float safeMovementStaminaBurn = ValidateInput(movementStaminaBurn, BaseInputFallback, BaseInputFallback);
            float safeAmbientPressure = ValidateInput(ambientPressure, PressureInputFallback, PressureInputFallback);

            // Ensure thruster load and high pressure scale consumption exponentially
            float pressureFactor = (float)Math.Pow(safeAmbientPressure, PressureExponent);
            float thrusterFactor = (float)Math.Pow(safeMovementStaminaBurn, ThrusterExponent);

            // Calculate base consumption
            float consumption = safeBaseO2Rate * pressureFactor * (1f + thrusterFactor);

            // Boundary guarding to avoid extreme overflow or unphysical outcomes
            return Math.Clamp(consumption, BaseInputFallback, float.MaxValue);
        }

        private static float ValidateInput(float value, float minValue, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallbackValue;
            }
            return Math.Max(value, minValue);
        }
    }
}
