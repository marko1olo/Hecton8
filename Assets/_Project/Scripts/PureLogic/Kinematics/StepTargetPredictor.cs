using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for StepTargetPredictor.
    /// Extracted from ProceduralCrabLegIKRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StepTargetPredictor
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="bodyPos">Parameter representing the bodyPos (Vector3).</param>
        /// <param name="bodyVelocity">Parameter representing the bodyVelocity (Vector3).</param>
        /// <param name="stepAheadFraction">Parameter representing the stepAheadFraction (float).</param>
        /// <param name="legStepRadius">Parameter representing the legStepRadius (float).</param>
        /// <param name="legIndex">Parameter representing the legIndex (int).</param>
        /// <param name="totalLegs">Parameter representing the totalLegs (int).</param>
        /// <returns>Returns predictedStepTarget of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 bodyPos, Vector3 bodyVelocity, float stepAheadFraction, float legStepRadius, int legIndex, int totalLegs)
        {
            // Parameter constraints (boundary guard)
            totalLegs = Math.Max(1, totalLegs);
            legIndex = Math.Clamp(legIndex, 0, totalLegs - 1);

            float safeStepAhead = Math.Max(0f, stepAheadFraction);
            float safeRadius = Math.Max(0f, legStepRadius);

            // Replaces: ProceduralCrabLegIKRuntime.ResolveLegHomeLocal logic
            int pairCount = Math.Max(1, totalLegs >> 1);
            int pairIndex = Math.Min(pairCount - 1, legIndex >> 1);
            float pairT = pairCount <= 1 ? 0.5f : pairIndex * (1f / (pairCount - 1));
            float side = (legIndex & 1) == 0 ? -1f : 1f;

            // Base unrotated local offset (pure translational)
            Vector3 homeLocal = new Vector3(
                side * 0.42f,
                0f,
                0.52f + pairT * (-0.52f - 0.52f) // same as math.lerp(0.52f, -0.52f, pairT)
            ) * safeRadius;

            return bodyPos + homeLocal + (bodyVelocity * safeStepAhead);
        }
    }
}
