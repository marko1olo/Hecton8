using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ThrusterEfficiencyVsPressureCalculator.
    /// Extracted from PlayerThrusterAudio.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ThrusterEfficiencyVsPressureCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseThrust">Parameter representing the baseThrust (float).</param>
        /// <param name="depthPressureBar">Parameter representing the depthPressureBar (float).</param>
        /// <param name="optimalPressureBar">Parameter representing the optimalPressureBar (float).</param>
        /// <param name="decayRate">Parameter representing the decayRate (float).</param>
        /// <returns>Returns Modified thrust output of type float.</returns>
        public static float Compute(float baseThrust, float depthPressureBar, float optimalPressureBar, float decayRate)
        {
            if (float.IsNaN(baseThrust) || float.IsNaN(depthPressureBar) || float.IsNaN(optimalPressureBar) || float.IsNaN(decayRate))
            {
                return 0f;
            }
            if (float.IsInfinity(baseThrust) || float.IsInfinity(depthPressureBar) || float.IsInfinity(optimalPressureBar) || float.IsInfinity(decayRate))
            {
                return 0f;
            }

            float thrust = baseThrust < 0f ? 0f : baseThrust;
            float pDepth = depthPressureBar < 0f ? 0f : depthPressureBar;
            float pOptimal = optimalPressureBar < 0f ? 0f : optimalPressureBar;
            float rDecay = decayRate < 0f ? 0f : decayRate;

            float diff = Math.Abs(pDepth - pOptimal);
            float decayFactor = (float)Math.Exp(-rDecay * diff);
            float output = thrust * decayFactor;

            if (float.IsNaN(output) || float.IsInfinity(output))
            {
                return 0f;
            }
            if (output < 0f)
            {
                return 0f;
            }

            return output;
        }
    }
}
