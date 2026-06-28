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
        /// <param name='pingPower'>Parameter representing the pingPower (float).</param>
        /// <param name='distance'>Parameter representing the distance (float).</param>
        /// <param name='turbidityCoefficient'>Parameter representing the turbidityCoefficient (float).</param>
        /// <returns>Returns Return signal strength 0.0 to 1.0 of type float.</returns>
        public static float Compute(float pingPower, float distance, float turbidityCoefficient)
        {
            if (float.IsNaN(pingPower) || float.IsNaN(distance) || float.IsNaN(turbidityCoefficient))
            {
                return 0f;
            }

            if (float.IsPositiveInfinity(distance) || float.IsPositiveInfinity(turbidityCoefficient))
            {
                return 0f;
            }

            float safePingPower = Math.Max(0f, pingPower);
            float safeDistance = Math.Max(0f, distance);
            float safeTurbidity = Math.Max(0f, turbidityCoefficient);

            if (float.IsInfinity(safePingPower) && safeDistance * safeTurbidity > 0f)
            {
                return 0f;
            }

            double exponent = -2.0 * safeDistance * safeTurbidity;
            float attenuation = (float)Math.Exp(exponent);
            float result = safePingPower * attenuation;

            if (float.IsInfinity(result))
            {
               return 1f;
            }

            return Math.Clamp(result, 0f, 1f);
        }
    }
}
