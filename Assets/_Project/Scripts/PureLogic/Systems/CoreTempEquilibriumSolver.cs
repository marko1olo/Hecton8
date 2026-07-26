using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Computes/evaluates the mathematical model.
    /// </summary>
    public static class CoreTempEquilibriumSolver
    {
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

            float cooling = OneMinusApproxExpNeg(safeCoolingRate * safeDt);

            float newCore = safeCore + (safeAmbient - safeCore) * cooling * (1f - safeResistance);
            return Math.Clamp(newCore, minTemp, maxTemp);
        }

        private static float ClampFinite(float value, float fallback)
        {
            return float.IsFinite(value) ? value : fallback;
        }

        /// <summary>
        /// Returns the Newton-cooling blend factor <c>1 - exp(-value)</c>.
        /// Uses range reduction by 4 so the Pade(3,3) approximant of exp(-x) stays
        /// inside its accurate band (x &lt;= 1), then recomposes the full exponent
        /// with <c>exp(-value) = (exp(-value/4))^4</c> via two squarings.
        /// Recomposition is mandatory: without it the factor collapses to
        /// <c>1 - exp(-value/4)</c>, which is roughly 4x too small for small inputs.
        /// </summary>
        private static float OneMinusApproxExpNeg(float value)
        {
            float safe = Math.Max(0f, Math.Min(value, 4f));
            if (!float.IsFinite(safe)) safe = 0f;

            float x = safe * 0.25f;
            float x2 = x * x;
            float x3 = x2 * x;
            float numerator = 1f - (0.5f * x) + (0.1f * x2) - ((1f / 120f) * x3);
            float denominator = 1f + (0.5f * x) + (0.1f * x2) + ((1f / 120f) * x3);
            float quarterDecay = numerator / denominator;

            // Undo the range reduction: (exp(-value/4))^4 == exp(-value).
            float halfDecay = quarterDecay * quarterDecay;
            float decay = halfDecay * halfDecay;

            return Math.Max(0f, Math.Min(1f, 1f - decay));
        }
    }
}
