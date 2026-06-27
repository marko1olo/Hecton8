using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PitchTrimCorrectionCalculator.
    /// Extracted from SubmarineDynamicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PitchTrimCorrectionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="pitchAngleDeg">Parameter representing the pitchAngleDeg (float).</param>
        /// <param name="trimGain">Parameter representing the trimGain (float).</param>
        /// <param name="maxTrimForceN">Parameter representing the maxTrimForceN (float).</param>
        /// <param name="pitchAngularVelocity">Parameter representing the pitchAngularVelocity (float).</param>
        /// <param name="dampingCoeff">Parameter representing the dampingCoeff (float).</param>
        /// <returns>Returns pitchCorrectionTorque Nm of type float.</returns>
        public static float Compute(float pitchAngleDeg, float trimGain, float maxTrimForceN, float pitchAngularVelocity, float dampingCoeff)
        {
            if (float.IsNaN(pitchAngleDeg) || float.IsInfinity(pitchAngleDeg))
                pitchAngleDeg = 0f;

            if (float.IsNaN(trimGain) || float.IsInfinity(trimGain))
                trimGain = 0f;

            if (float.IsNaN(maxTrimForceN) || float.IsInfinity(maxTrimForceN))
                maxTrimForceN = 0f;

            if (float.IsNaN(pitchAngularVelocity) || float.IsInfinity(pitchAngularVelocity))
                pitchAngularVelocity = 0f;

            if (float.IsNaN(dampingCoeff) || float.IsInfinity(dampingCoeff))
                dampingCoeff = 0f;

            float safeTrimGain = Math.Max(0f, trimGain);
            float safeDampingCoeff = Math.Max(0f, dampingCoeff);
            float safeMaxForce = Math.Max(0f, maxTrimForceN);

            float pTerm = pitchAngleDeg * safeTrimGain;
            float dTerm = pitchAngularVelocity * safeDampingCoeff;

            float correction = pTerm - dTerm;

            if (float.IsNaN(correction))
                correction = 0f;

            if (correction > safeMaxForce)
                correction = safeMaxForce;
            else if (correction < -safeMaxForce)
                correction = -safeMaxForce;

            return correction;
        }
    }
}
