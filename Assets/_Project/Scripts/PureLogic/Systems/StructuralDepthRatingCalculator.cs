using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for StructuralDepthRatingCalculator.
    /// Extracted from HullIntegrityRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StructuralDepthRatingCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="crushDepthRating">Parameter representing the crushDepthRating (float).</param>
        /// <param name="hullIntegrity01">Parameter representing the hullIntegrity01 (float).</param>
        /// <param name="fatigueAccumulated">Parameter representing the fatigueAccumulated (float).</param>
        /// <returns>Returns stressFraction 0.0-1.0, float (damageRatePerSecond) of type float.</returns>
        public static float Compute(float depthMeters, float crushDepthRating, float hullIntegrity01, float fatigueAccumulated)
        {
            if (float.IsNaN(depthMeters) || float.IsInfinity(depthMeters) ||
                float.IsNaN(crushDepthRating) || float.IsInfinity(crushDepthRating) ||
                float.IsNaN(hullIntegrity01) || float.IsInfinity(hullIntegrity01) ||
                float.IsNaN(fatigueAccumulated) || float.IsInfinity(fatigueAccumulated))
            {
                return 0f;
            }

            depthMeters = Math.Max(0f, depthMeters);
            crushDepthRating = Math.Max(0.001f, crushDepthRating);
            hullIntegrity01 = Math.Max(0f, Math.Min(1f, hullIntegrity01));
            fatigueAccumulated = Math.Max(0f, fatigueAccumulated);

            if (depthMeters < crushDepthRating)
            {
                return 0f;
            }

            // "At rating: moderate." Let's say moderate is 0.5 at depth = crushDepthRating
            // "Double depth: catastrophic" -> depth/crush ratio 2 = high stress fraction

            float depthExceedance = depthMeters - crushDepthRating;
            float exceedanceRatio = depthExceedance / crushDepthRating;

            // Base stress is 0.5 exactly at crush depth, goes to 1.0 at double depth (exceedanceRatio = 1)
            float baseStress = 0.5f + (0.5f * exceedanceRatio);

            float stressFraction = baseStress * (1f + fatigueAccumulated) * (2f - hullIntegrity01);

            return Math.Max(0f, Math.Min(1f, stressFraction));
        }
    }
}
