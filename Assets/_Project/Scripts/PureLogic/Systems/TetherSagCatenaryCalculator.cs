using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for TetherSagCatenaryCalculator.
    /// Extracted from TetherInstance.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class TetherSagCatenaryCalculator
    {
        private const float MinDistance = 0.0001f;
        private const float EmpiricalWeightFactor = 0.1f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="anchorASeparationX">Parameter representing the anchorASeparationX (float).</param>
        /// <param name="anchorBHeight">Parameter representing the anchorBHeight (float).</param>
        /// <param name="cableLength">Parameter representing the cableLength (float).</param>
        /// <param name="cableWeightPerMeter">Parameter representing the cableWeightPerMeter (float).</param>
        /// <returns>Returns maxSagDepthMeters, float (tensionAtAnchors) of type float.</returns>
        public static float Compute(float anchorASeparationX, float anchorBHeight, float cableLength, float cableWeightPerMeter)
        {
            if (float.IsNaN(anchorASeparationX) || float.IsNaN(anchorBHeight) ||
                float.IsNaN(cableLength) || float.IsNaN(cableWeightPerMeter))
            {
                return 0f;
            }
            if (float.IsInfinity(anchorASeparationX) || float.IsInfinity(anchorBHeight) ||
                float.IsInfinity(cableLength) || float.IsInfinity(cableWeightPerMeter))
            {
                return 0f;
            }

            float sepX = Math.Max(MinDistance, Math.Abs(anchorASeparationX));
            float heightY = Math.Abs(anchorBHeight);

            float distSq = sepX * sepX + heightY * heightY;
            float straightDist = (float)Math.Sqrt(distSq);

            float safeLength = Math.Max(straightDist, Math.Max(0f, cableLength));
            float slack = safeLength - straightDist;

            float safeWeight = Math.Max(MinDistance, cableWeightPerMeter);

            float sag = (float)Math.Sqrt(slack * straightDist) * (1f + slack * safeWeight * EmpiricalWeightFactor);
            return float.IsInfinity(sag) || float.IsNaN(sag) ? float.MaxValue : sag;
        }
    }
}
