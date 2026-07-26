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

            // Configuration parameters are validated as strictly as the state parameters above.
            // A non-finite or negative tuning value previously leaked NaN into the returned
            // deceleration (corrupting player velocity), and a negative maxDragClamp inverted
            // the sign so the "drag" accelerated the player instead of slowing it.
            if (!float.IsFinite(brokenSuitMultiplier)) brokenSuitMultiplier = 0f;
            if (!float.IsFinite(epsilon)) epsilon = 0f;
            brokenSuitMultiplier = Math.Max(0f, brokenSuitMultiplier);
            epsilon = Math.Max(0f, epsilon);

            speed = Math.Max(0f, speed);
            depthMeters = Math.Max(0f, depthMeters);
            suitIntegrity01 = Math.Clamp(suitIntegrity01, 0f, 1f);
            baseDragCoeff = Math.Max(0f, baseDragCoeff);

            if (speed <= epsilon || baseDragCoeff <= epsilon) return 0f;

            // Math constraint: "Quadratic drag at surface, cubic at depth"
            // "Max depth + broken suit = peak drag. Continuity at depth transition."
            // Assuming 100 meters is fully deep.
            // An unusable depth scale (zero, negative, or non-finite - e.g. an unassigned
            // config field) fails safe to surface behaviour instead of dividing by zero.
            // depthMeters == depthScaleMax == 0 previously evaluated 0f/0f -> NaN.
            float depthBlend01 = float.IsFinite(depthScaleMax) && depthScaleMax > 0f
                ? Math.Clamp(depthMeters / depthScaleMax, 0f, 1f)
                : 0f;

            float quadraticPart = speed * speed;
            float cubicPart = speed * speed * speed;

            float dragCurve = (quadraticPart * (1f - depthBlend01)) + (cubicPart * depthBlend01);

            // "broken suit = peak drag" -> suitIntegrity01 = 0 should amplify drag.
            // Let's multiply by a broken suit multiplier (e.g. up to 2x or 3x drag when broken)
            float integrityMultiplier = 1f + (1f - suitIntegrity01) * brokenSuitMultiplier;

            float rawDrag = baseDragCoeff * dragCurve * integrityMultiplier;

            // Output Return: Clamp to a realistic deceleration limit.
            // Drag is a deceleration magnitude and must never be returned negative.
            if (!float.IsFinite(maxDragClamp))
                return Math.Max(0f, rawDrag);

            return Math.Max(0f, Math.Min(rawDrag, Math.Max(0f, maxDragClamp)));
        }
    }
}
