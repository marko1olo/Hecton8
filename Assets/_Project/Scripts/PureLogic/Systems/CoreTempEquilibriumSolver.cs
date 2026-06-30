using System;
using System.Numerics;
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="coreTempCelsius">Parameter representing the coreTempCelsius (float).</param>
        /// <param name="ambientTempCelsius">Parameter representing the ambientTempCelsius (float).</param>
        /// <param name="suitThermalResistance">Parameter representing the suitThermalResistance (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <param name="coolingRate">Parameter representing the hypothermia cooling rate (float).</param>
        /// <param name="minTemp">Parameter representing the minimum clamp temperature (float).</param>
        /// <param name="maxTemp">Parameter representing the maximum clamp temperature (float).</param>
        /// <returns>Returns new core temperature after deltaTime of type float.</returns>
        public static float Solve(float coreTempCelsius, float ambientTempCelsius, float suitThermalResistance, float deltaTime, float coolingRate, float minTemp, float maxTemp)
        {
            float safeCore = ClampFinite(coreTempCelsius, 37f);
            float safeAmbient = ClampFinite(ambientTempCelsius, 4f);
            float safeDt = Math.Max(0f, ClampFinite(deltaTime, 0f));
            float safeResistance = Math.Clamp(ClampFinite(suitThermalResistance, 0f), 0f, 1f);
            float safeCoolingRate = Math.Max(0f, ClampFinite(coolingRate, 0.006f));

            float cooling = OneMinusApproxExpNegPade33Reduced(safeCoolingRate * safeDt);

            float newCore = safeCore + (safeAmbient - safeCore) * cooling * (1f - safeResistance);
            return Math.Clamp(newCore, minTemp, maxTemp);
        }

        private static float ClampFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }

        private static float OneMinusApproxExpNegPade33Reduced(float value)
        {
            float safe = Math.Max(0f, Math.Min(value, 4f));
            if (!float.IsFinite(safe)) safe = 0f;

            float x = safe * 0.25f;
            float x2 = x * x;
            float x3 = x2 * x;
            float numerator = 1f - (0.5f * x) + (0.1f * x2) - ((1f / 120f) * x3);
            float denominator = 1f + (0.5f * x) + (0.1f * x2) + ((1f / 120f) * x3);

        }
    }
}
