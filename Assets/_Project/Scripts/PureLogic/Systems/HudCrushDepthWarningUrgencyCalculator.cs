using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HudCrushDepthWarningUrgencyCalculator.
    /// Extracted from HUDNotification.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HudCrushDepthWarningUrgencyCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDepth">Parameter representing the currentDepth (float).</param>
        /// <param name="crushDepth">Parameter representing the crushDepth (float).</param>
        /// <param name="verticalSpeed">Parameter representing the verticalSpeed (float).</param>
        /// <returns>Returns Warning level 0.0 to 1.0 of type float.</returns>
        public static float Compute(float currentDepth, float crushDepth, float verticalSpeed)
        {
            if (float.IsNaN(currentDepth) || float.IsNaN(crushDepth) || float.IsNaN(verticalSpeed) ||
                float.IsInfinity(currentDepth) || float.IsInfinity(crushDepth) || float.IsInfinity(verticalSpeed))
            {
                return 0f;
            }

            float effectiveCrushDepth = Math.Max(crushDepth, 0.001f);
            float projectedDepth = currentDepth + verticalSpeed;
            float urgency = projectedDepth / effectiveCrushDepth;

            if (urgency < 0f) return 0f;
            if (urgency > 1f) return 1f;

            return urgency;
        }
    }
}
