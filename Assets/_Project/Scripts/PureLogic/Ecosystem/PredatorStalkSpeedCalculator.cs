using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for PredatorStalkSpeedCalculator.
    /// Extracted from FaunaKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PredatorStalkSpeedCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="distanceToPrey">Parameter representing the distanceToPrey (float).</param>
        /// <param name="stalkRadius">Parameter representing the stalkRadius (float).</param>
        /// <param name="maxStalkSpeed">Parameter representing the maxStalkSpeed (float).</param>
        /// <param name="maxChaseSpeed">Parameter representing the maxChaseSpeed (float).</param>
        /// <param name="preyAwarenessLevel">Parameter representing the preyAwarenessLevel (float).</param>
        /// <returns>Returns current movement speed of type float.</returns>
        public static float Compute(float distanceToPrey, float stalkRadius, float maxStalkSpeed, float maxChaseSpeed, float preyAwarenessLevel)
        {
            if (float.IsNaN(distanceToPrey) || float.IsInfinity(distanceToPrey)) distanceToPrey = 0f;
            if (float.IsNaN(stalkRadius) || float.IsInfinity(stalkRadius)) stalkRadius = 1f;
            if (float.IsNaN(maxStalkSpeed) || float.IsInfinity(maxStalkSpeed)) maxStalkSpeed = 0f;
            if (float.IsNaN(maxChaseSpeed) || float.IsInfinity(maxChaseSpeed)) maxChaseSpeed = 0f;
            if (float.IsNaN(preyAwarenessLevel) || float.IsInfinity(preyAwarenessLevel)) preyAwarenessLevel = 0f;

            float validDistance = Math.Max(0f, distanceToPrey);
            float validStalkRadius = Math.Max(0.0001f, stalkRadius);
            float validMaxStalkSpeed = Math.Max(0f, maxStalkSpeed);
            float validMaxChaseSpeed = Math.Max(0f, maxChaseSpeed);

            float awareness = Math.Max(0f, Math.Min(1f, preyAwarenessLevel));

            float speedBasedOnDistance = validDistance <= validStalkRadius ? validMaxStalkSpeed : validMaxChaseSpeed;

            return speedBasedOnDistance + (validMaxChaseSpeed - speedBasedOnDistance) * awareness;
        }
    }
}
