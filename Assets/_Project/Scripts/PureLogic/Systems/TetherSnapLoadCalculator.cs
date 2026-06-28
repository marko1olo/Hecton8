using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for TetherSnapLoadCalculator.
    /// Extracted from TetherInstance.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class TetherSnapLoadCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="staticLoadN">Parameter representing the staticLoadN (float).</param>
        /// <param name="dynamicImpactMultiplier">Parameter representing the dynamicImpactMultiplier (float).</param>
        /// <param name="tetherBreakingStrengthN">Parameter representing the tetherBreakingStrengthN (float).</param>
        /// <returns>Returns willSnap, float (snapProbability01) of type bool.</returns>
        public static bool Compute(float staticLoadN, float dynamicImpactMultiplier, float tetherBreakingStrengthN)
        {
            if (float.IsNaN(staticLoadN) || float.IsNaN(dynamicImpactMultiplier) || float.IsNaN(tetherBreakingStrengthN))
            {
                return false;
            }

            float safeStaticLoad = staticLoadN < 0f ? 0f : staticLoadN;
            float safeMultiplier = dynamicImpactMultiplier < 1f ? 1f : dynamicImpactMultiplier;
            float safeStrength = tetherBreakingStrengthN < 0f ? 0f : tetherBreakingStrengthN;

            if (float.IsInfinity(safeStaticLoad) || float.IsInfinity(safeMultiplier))
            {
                return !float.IsInfinity(safeStrength);
            }

            float effectiveLoad = safeStaticLoad * safeMultiplier;

            if (float.IsInfinity(effectiveLoad))
            {
                return !float.IsInfinity(safeStrength);
            }

            return effectiveLoad > safeStrength;
        }
    }
}
