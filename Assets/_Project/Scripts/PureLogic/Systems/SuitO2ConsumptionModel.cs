using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SuitO2ConsumptionModel.
    /// Extracted from ShinobuPhysiologyRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SuitO2ConsumptionModel
    {
        private const float BaseMultiplier = 1.0f;
        private const float MaxExertionPenalty = 2.0f;
        private const float MaxSealDamagePenalty = 2.0f;
        private const float MinDepthAtm = 1.0f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="exertionLevel01">Parameter representing the exertionLevel01 (float).</param>
        /// <param name="suitSealIntegrity01">Parameter representing the suitSealIntegrity01 (float).</param>
        /// <param name="baseO2ConsumptionRate">Parameter representing the baseO2ConsumptionRate (float).</param>
        /// <param name="depthAtm">Parameter representing the depthAtm (float).</param>
        /// <returns>Returns O2 consumed per second of type float.</returns>
        public static float Evaluate(float exertionLevel01, float suitSealIntegrity01, float baseO2ConsumptionRate, float depthAtm)
        {
            float safeExertion = float.IsNaN(exertionLevel01) || float.IsInfinity(exertionLevel01) ? 0f : exertionLevel01;
            float safeSeal = float.IsNaN(suitSealIntegrity01) || float.IsInfinity(suitSealIntegrity01) ? 1f : suitSealIntegrity01;
            float safeBaseRate = float.IsNaN(baseO2ConsumptionRate) || float.IsInfinity(baseO2ConsumptionRate) ? 0f : baseO2ConsumptionRate;
            float safeDepth = float.IsNaN(depthAtm) || float.IsInfinity(depthAtm) ? MinDepthAtm : depthAtm;

            safeExertion = Math.Max(0f, Math.Min(safeExertion, 1f));
            safeSeal = Math.Max(0f, Math.Min(safeSeal, 1f));
            safeBaseRate = Math.Max(0f, safeBaseRate);
            safeDepth = Math.Max(MinDepthAtm, safeDepth);

            float exertionMultiplier = BaseMultiplier + (safeExertion * MaxExertionPenalty);
            float leakPenalty = (1.0f - safeSeal) * MaxSealDamagePenalty;
            float totalMultiplier = exertionMultiplier + leakPenalty;

            float result = safeBaseRate * totalMultiplier * safeDepth;

            return float.IsInfinity(result) || float.IsNaN(result) ? float.MaxValue : result;
        }
    }
}
