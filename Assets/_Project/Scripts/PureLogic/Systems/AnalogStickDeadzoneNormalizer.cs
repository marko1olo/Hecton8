using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AnalogStickDeadzoneNormalizer.
    /// Extracted from InputDispatcher.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AnalogStickDeadzoneNormalizer
    {
        private const float MaxInnerDeadzone = 0.95f;
        private const float MinDivisorEpsilon = 0.0001f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="rawValue">Parameter representing the rawValue (float).</param>
        /// <param name="innerDeadzone">Parameter representing the innerDeadzone (float).</param>
        /// <param name="outerDeadzone">Parameter representing the outerDeadzone (float).</param>
        /// <returns>Returns normalizedValue 0.0-1.0 of type float.</returns>
        public static float Normalize(float rawValue, float innerDeadzone, float outerDeadzone)
        {
            float inner = Math.Clamp(innerDeadzone, 0f, MaxInnerDeadzone);
            float outer = Math.Clamp(outerDeadzone, inner + MinDivisorEpsilon, 1f);

            float divisor = Math.Max(outer - inner, MinDivisorEpsilon);
            float normalized = (rawValue - inner) / divisor;

            if (float.IsNaN(normalized)) return 0f;

            return Math.Clamp(normalized, 0f, 1f);
        }
    }
}
