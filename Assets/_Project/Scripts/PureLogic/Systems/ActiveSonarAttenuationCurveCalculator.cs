using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ActiveSonarAttenuationCurveCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ActiveSonarAttenuationCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="pingPower">Parameter representing the pingPower (float).</param>
        /// <param name="distance">Parameter representing the distance (float).</param>
        /// <param name="turbidityCoefficient">Parameter representing the turbidityCoefficient (float).</param>
        /// <returns>Returns Return signal strength 0.0 to 1.0 of type float.</returns>
        public static float Compute(float pingPower, float distance, float turbidityCoefficient)
        {
            if (float.IsNaN(pingPower) || float.IsInfinity(pingPower)) return 0f;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return 0f;
            if (float.IsNaN(turbidityCoefficient) || float.IsInfinity(turbidityCoefficient)) return 0f;

            float safePingPower = Math.Max(0f, pingPower);
            float safeDistance = Math.Max(0f, distance);
            float safeTurbidity = Math.Max(0f, turbidityCoefficient);

            float roundTripDistance = safeDistance * 2f;
            float attenuation = (float)Math.Exp(-safeTurbidity * roundTripDistance);

            float intensity = safePingPower * attenuation;

            if (float.IsNaN(intensity) || float.IsInfinity(intensity)) return 0f;

            return Math.Max(0f, Math.Min(1f, intensity));
        }
    }
}
