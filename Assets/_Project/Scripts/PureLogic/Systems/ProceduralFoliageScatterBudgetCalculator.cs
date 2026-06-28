using System;
using System.Numerics;
namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ProceduralFoliageScatterBudgetCalculator.
    /// Extracted from ScatterBudgetController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ProceduralFoliageScatterBudgetCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='targetFps'>Parameter representing the targetFps (float).</param>
        /// <param name='currentFps'>Parameter representing the currentFps (float).</param>
        /// <param name='baseBudget'>Parameter representing the baseBudget (int).</param>
        /// <param name='qualityWeight'>Parameter representing the qualityWeight (float).</param>
        /// <returns>Returns Instance budget limit of type int.</returns>
        public static int Compute(float targetFps, float currentFps, int baseBudget, float qualityWeight)
        {
            if (baseBudget <= 0) return 0;
            float safeTargetFps = float.IsNaN(targetFps) || float.IsInfinity(targetFps) ? 0f : targetFps;
            float safeCurrentFps = float.IsNaN(currentFps) || float.IsInfinity(currentFps) || currentFps < 0f ? 0f : currentFps;
            float safeQualityWeight = float.IsNaN(qualityWeight) || float.IsInfinity(qualityWeight) || qualityWeight < 0f ? 0f : qualityWeight;
            safeQualityWeight = Math.Min(safeQualityWeight, 1f);
            // FPS constraint
            float fpsRatio = safeTargetFps > 0f ? safeCurrentFps / safeTargetFps : 1f;
            fpsRatio = Math.Min(fpsRatio, 1f);
            // "Poor FPS throttles budget downwards" -> fpsRatio
            // "Low quality weight heavily limits max budget" -> qualityWeight
            long budgetLimitLong = (long)Math.Round((double)baseBudget * (double)fpsRatio * (double)safeQualityWeight);
            int budgetLimit = budgetLimitLong > int.MaxValue ? int.MaxValue : (int)budgetLimitLong;
            if (budgetLimit < 0) return 0;
            if (budgetLimit > baseBudget) return baseBudget;
            return budgetLimit;
        }
    }
}
