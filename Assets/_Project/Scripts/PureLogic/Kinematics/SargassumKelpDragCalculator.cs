using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SargassumKelpDragCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SargassumKelpDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='sargassumDensity01'>Parameter representing the sargassumDensity01 (float).</param>
        /// <param name='currentSpeed'>Parameter representing the currentSpeed (float).</param>
        /// <param name='bodyTangleWeight'>Parameter representing the bodyTangleWeight (float).</param>
        /// <returns>Returns Speed multiplier 0.0 to 1.0 of type float.</returns>
        public static float Compute(float sargassumDensity01, float currentSpeed, float bodyTangleWeight)
        {
            // Step 1 - Parameter Validation
            if (float.IsNaN(sargassumDensity01))
            {
                sargassumDensity01 = 0f;
            }
            sargassumDensity01 = Math.Clamp(sargassumDensity01, 0f, 1f);

            if (float.IsNaN(currentSpeed) || currentSpeed < 0f)
            {
                currentSpeed = 0f;
            }

            if (float.IsNaN(bodyTangleWeight) || bodyTangleWeight < 0f)
            {
                bodyTangleWeight = 0f;
            }

            // if density is 0, return 1.0 multiplier (no penalty)
            if (sargassumDensity01 <= 0f)
            {
                return 1.0f;
            }

            // Step 2 - Business Logic
            // Based on constraints: 0 density = 1.0. High speed and high density must drag speed down to a crawl.

            float entanglementMinDensity = 0.28f;
            float entanglementSpeedThreshold = 1.5f;

            // Density Gate (from SargassumGlobalDragManager.cs)
            float densityGate = 0f;
            if (sargassumDensity01 > entanglementMinDensity)
            {
                float t = (sargassumDensity01 - entanglementMinDensity) / (1f - entanglementMinDensity);
                densityGate = Math.Clamp(t, 0f, 1f);
            }

            // Speed Gate (from SargassumGlobalDragManager.cs)
            float speedGate = 0f;
            float safeThreshold = Math.Max(0.01f, entanglementSpeedThreshold);
            float currentSpeedRatio = Math.Clamp(currentSpeed / safeThreshold, 0f, 1f);
            speedGate = currentSpeedRatio;

            // Compute Base Entanglement
            float entanglement01 = densityGate * speedGate;

            // Minimum Speed Multiplier (from SargassumGlobalDragManager.cs)
            float minSpeedMultiplier = 0.58f;

            // Calculate base multiplier (SpeedMultiplier)
            float speedMultiplier = 1f + (minSpeedMultiplier - 1f) * sargassumDensity01;

            // Applying bodyTangleWeight as an additional factor based on problem statement
            // Weight increases penalty. Note: In Unity game, mass is about ~80kg, scaling around that.
            // Using 100kg as a scale factor.
            float weightFactor = Math.Min(bodyTangleWeight / 100.0f, 1.0f);

            // Tangle weight adds an additional penalty based on entanglement01
            float entanglementPenalty = entanglement01 * (0.5f + weightFactor * 0.5f);

            // Step 3 - Boundary Guarding
            // Final multiplier
            float multiplier = speedMultiplier - entanglementPenalty;

            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                multiplier = 0f;
            }

            // Step 4 - Output Return
            return Math.Clamp(multiplier, 0.0f, 1.0f);
        }
    }
}
