using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LunarPhaseCalculator.
    /// Extracted from HectonCelestialEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LunarPhaseCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="worldTimeSeconds">Parameter representing the worldTimeSeconds (float).</param>
        /// <param name="lunarCycleLengthSeconds">Parameter representing the lunarCycleLengthSeconds (float).</param>
        /// <returns>Returns phaseAngleDeg 0-360, float (illuminationFraction 0-1) of type float.</returns>
        public static float Compute(float worldTimeSeconds, float lunarCycleLengthSeconds)
        {
            if (float.IsNaN(worldTimeSeconds) || float.IsInfinity(worldTimeSeconds))
                worldTimeSeconds = 0f;
            if (float.IsNaN(lunarCycleLengthSeconds) || float.IsInfinity(lunarCycleLengthSeconds) || lunarCycleLengthSeconds <= 0.0001f)
                return 0f;

            worldTimeSeconds = Math.Max(0f, worldTimeSeconds);

            float phase01 = (worldTimeSeconds / lunarCycleLengthSeconds) % 1.0f;
            if (phase01 < 0f) phase01 += 1.0f;

            return phase01 * 360f;
        }
    }
}
