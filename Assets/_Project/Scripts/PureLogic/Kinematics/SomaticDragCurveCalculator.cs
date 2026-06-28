using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SomaticDragCurveCalculator.
    /// Extracted from PlayerKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SomaticDragCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="speed">Parameter representing the speed (float).</param>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="suitIntegrity01">Parameter representing the suitIntegrity01 (float).</param>
        /// <param name="baseDragCoeff">Parameter representing the baseDragCoeff (float).</param>
        /// <returns>Returns drag deceleration m/s^2 of type float.</returns>
        public static float Compute(float speed, float depthMeters, float suitIntegrity01, float baseDragCoeff, float depthScaleMax, float brokenSuitMultiplier, float maxDragClamp, float epsilon)
        {
            // Parameter Validation
            if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0f;
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters)) depthMeters = 0f;
            if (float.IsNaN(suitIntegrity01) || float.IsInfinity(suitIntegrity01)) suitIntegrity01 = 1f;
            if (float.IsNaN(baseDragCoeff) || float.IsInfinity(baseDragCoeff)) baseDragCoeff = 0f;

            speed = Math.Max(0f, speed);
            depthMeters = Math.Max(0f, depthMeters);
            suitIntegrity01 = Math.Clamp(suitIntegrity01, 0f, 1f);
            baseDragCoeff = Math.Max(0f, baseDragCoeff);

            if (speed <= epsilon || baseDragCoeff <= epsilon) return 0f;

            // Math constraint: "Quadratic drag at surface, cubic at depth"
            // "Max depth + broken suit = peak drag. Continuity at depth transition."
            // Assuming 100 meters is fully deep.
            float depthBlend01 = Math.Clamp(depthMeters / depthScaleMax, 0f, 1f);

            float quadraticPart = speed * speed;
            float cubicPart = speed * speed * speed;

            float dragCurve = (quadraticPart * (1f - depthBlend01)) + (cubicPart * depthBlend01);

            // "broken suit = peak drag" -> suitIntegrity01 = 0 should amplify drag.
            // Let's multiply by a broken suit multiplier (e.g. up to 2x or 3x drag when broken)
            float integrityMultiplier = 1f + (1f - suitIntegrity01) * brokenSuitMultiplier;

            float rawDrag = baseDragCoeff * dragCurve * integrityMultiplier;

            // Output Return: Clamp to a realistic deceleration limit
            return Math.Min(rawDrag, maxDragClamp);
        }
    }
}
