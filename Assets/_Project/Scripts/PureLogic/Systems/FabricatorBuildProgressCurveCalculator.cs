using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FabricatorBuildProgressCurveCalculator.
    /// Extracted from Fabricator.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FabricatorBuildProgressCurveCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentProgress">Parameter representing the currentProgress (float).</param>
        /// <param name="rawBuildTime">Parameter representing the rawBuildTime (float).</param>
        /// <param name="toolTemp">Parameter representing the toolTemp (float).</param>
        /// <param name="powerLevel01">Parameter representing the powerLevel01 (float).</param>
        /// <param name="deltaSeconds">Parameter representing the deltaSeconds (float).</param>
        /// <returns>Returns New progress percentage 0.0 to 1.0 of type float.</returns>
        public static float Compute(float currentProgress, float rawBuildTime, float toolTemp, float powerLevel01, float deltaSeconds)
        {
            float duration = Math.Max(0.001f, float.IsNaN(rawBuildTime) || float.IsInfinity(rawBuildTime) ? 0.001f : rawBuildTime);
            float power = Math.Clamp(float.IsNaN(powerLevel01) || float.IsInfinity(powerLevel01) ? 0f : powerLevel01, 0f, 1f);
            float thermal = Math.Clamp(float.IsNaN(toolTemp) || float.IsInfinity(toolTemp) ? 1f : toolTemp, 0f, 1f);
            float previousProgress = float.IsNaN(currentProgress) || float.IsInfinity(currentProgress) ? 0f : Math.Clamp(currentProgress, 0f, 1f);
            float deltaSec = Math.Max(0f, float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : deltaSeconds);

            bool paused = power <= 0.0001f;
            float delta = paused ? 0f : (deltaSec * power * thermal) / duration;

            float progress = Math.Clamp(previousProgress + delta, 0f, 1f);

            return float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : progress;
        }
    }
}
