using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for CoreTempEquilibriumSolver.
    /// Extracted from ShinobuPhysiologyRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CoreTempEquilibriumSolver
    {
        private const float DefaultCoolingRate = 0.006f;
        private const float MinCoreTemp = 20f;
        private const float MaxCoreTemp = 43f;
        private const float DefaultCoreTemp = 37f;
        private const float DefaultAmbientTemp = 4f;
        private const float MaxSimulationStepSeconds = 0.25f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="coreTempCelsius">Parameter representing the coreTempCelsius (float).</param>
        /// <param name="ambientTempCelsius">Parameter representing the ambientTempCelsius (float).</param>
        /// <param name="suitThermalResistance">Parameter representing the suitThermalResistance (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns new core temperature after deltaTime of type float.</returns>
        public static float Solve(float coreTempCelsius, float ambientTempCelsius, float suitThermalResistance, float deltaTime)
        {
            float safeCore = SanitizeFinite(coreTempCelsius, DefaultCoreTemp);
            float safeAmbient = SanitizeFinite(ambientTempCelsius, DefaultAmbientTemp);
            float safeInsulation = Math.Clamp(SanitizeFinite(suitThermalResistance, 0f), 0f, 1f);

            // dt clamping logic as per math: "0.0001f, MaxSimulationStepSeconds" but when negative it shouldn't apply full tick
            // However, jobs logic: float dt = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(DeltaSeconds, 0.016f), 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            // wait, if dt is 0f, it clamps to 0.0001f!
            float sanitizedDt = SanitizeFinite(deltaTime, 0.016f);

            // If the user wants 0f or negative as pure input (not job input), how should it behave?
            // "Verify zero inputs are handled without divide-by-zero or exception."
            // In pure logic, 0 dt should mean no change. So if deltaTime <= 0, return initial.
            if (sanitizedDt <= 0f) return Math.Clamp(safeCore, MinCoreTemp, MaxCoreTemp);

            float safeDt = Math.Clamp(sanitizedDt, 0.0001f, MaxSimulationStepSeconds);

            float cooling = OneMinusApproxExpNegPade33Reduced(DefaultCoolingRate * safeDt);

            float newCore = safeCore + (safeAmbient - safeCore) * cooling * (1f - safeInsulation);

            return Math.Clamp(SanitizeFinite(newCore, DefaultCoreTemp), MinCoreTemp, MaxCoreTemp);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return (float.IsNaN(value) || float.IsInfinity(value)) ? fallback : value;
        }

        private static float OneMinusApproxExpNegPade33Reduced(float value)
        {
            float safeValue = Math.Clamp(value, 0f, 4f);
            float x = safeValue * 0.25f;
            float x2 = x * x;
            float x3 = x2 * x;
            float numerator = 1f - (0.5f * x) + (0.1f * x2) - ((1f / 120f) * x3);
            float denominator = 1f + (0.5f * x) + (0.1f * x2) + ((1f / 120f) * x3);
            float baseDecay = numerator / Math.Max(denominator, 1e-6f);
            float decay2 = baseDecay * baseDecay;
            float decay4 = decay2 * decay2;

            float approxExpNeg = Math.Max(0f, Math.Min(decay4, 1f));
            return Math.Max(0f, Math.Min(1f - approxExpNeg, 1f));
        }
    }
}
