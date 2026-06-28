using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for NitrogenNarcosisModel.
    /// Extracted from ShinobuPhysiologyRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class NitrogenNarcosisModel
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="depthMeters">Parameter representing the depthMeters (float).</param>
        /// <param name="timeAtDepthSeconds">Parameter representing the timeAtDepthSeconds (float).</param>
        /// <param name="narcosisOnsetDepth">Parameter representing the narcosisOnsetDepth (float).</param>
        /// <param name="maxImpairment">Parameter representing the maxImpairment (float).</param>
        /// <returns>Returns impairmentFactor 0.0-1.0 of type float.</returns>
        public static float Evaluate(float depthMeters, float timeAtDepthSeconds, float narcosisOnsetDepth, float maxImpairment)
        {
            if (float.IsNaN(depthMeters) || float.IsNaN(timeAtDepthSeconds) ||
                float.IsNaN(narcosisOnsetDepth) || float.IsNaN(maxImpairment))
            {
                return 0f;
            }

            float clampedMax = Math.Max(0f, Math.Min(1f, maxImpairment));

            if (depthMeters <= narcosisOnsetDepth || timeAtDepthSeconds <= 0f)
            {
                return 0f;
            }

            float effectiveDepth = depthMeters - narcosisOnsetDepth;
            if (float.IsInfinity(effectiveDepth) || float.IsInfinity(timeAtDepthSeconds))
            {
                return clampedMax;
            }

            // Using Log(1+effectiveDepth) ensures positive results.
            float depthLog = (float)Math.Log(1f + effectiveDepth);
            float timeLog = (float)Math.Log(1f + timeAtDepthSeconds);

            float impairment = (depthLog * timeLog);

            if (float.IsNaN(impairment) || float.IsInfinity(impairment))
            {
                return clampedMax;
            }

            return Math.Min(impairment, clampedMax);
        }
    }
}
