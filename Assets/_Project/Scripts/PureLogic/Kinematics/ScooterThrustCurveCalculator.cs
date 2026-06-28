using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ScooterThrustCurveCalculator.
    /// Extracted from MantaScooter.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ScooterThrustCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="throttleInput01">Parameter representing the throttleInput01 (float).</param>
        /// <param name="currentSpeed">Parameter representing the currentSpeed (float).</param>
        /// <param name="maxSpeed">Parameter representing the maxSpeed (float).</param>
        /// <param name="thrustForce">Parameter representing the thrustForce (float).</param>
        /// <param name="dragCoeff">Parameter representing the dragCoeff (float).</param>
        /// <returns>Returns netForceN of type float.</returns>
        public static float Compute(float throttleInput01, float currentSpeed, float maxSpeed, float thrustForce, float dragCoeff)
        {
            if (float.IsNaN(currentSpeed) || float.IsInfinity(currentSpeed))
                return 0f;
            if (float.IsNaN(throttleInput01) || float.IsInfinity(throttleInput01))
                return 0f;
            if (float.IsNaN(maxSpeed) || float.IsInfinity(maxSpeed))
                return 0f;
            if (float.IsNaN(thrustForce) || float.IsInfinity(thrustForce))
                return 0f;
            if (float.IsNaN(dragCoeff) || float.IsInfinity(dragCoeff))
                return 0f;

            float clampedThrottle = Math.Clamp(throttleInput01, 0f, 1f);
            float safeMaxSpeed = Math.Max(0.0001f, Math.Abs(maxSpeed));

            float dragForce = dragCoeff * currentSpeed;
            float thrustLimit = thrustForce * Math.Max(0f, 1f - (currentSpeed / safeMaxSpeed));

            return (clampedThrottle * thrustLimit) - ((1f - clampedThrottle) * dragForce);
        }
    }
}
