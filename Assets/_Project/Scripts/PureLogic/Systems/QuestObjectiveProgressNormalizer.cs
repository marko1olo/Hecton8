using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for QuestObjectiveProgressNormalizer.
    /// Extracted from QuestStateManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class QuestObjectiveProgressNormalizer
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentCount">Parameter representing the currentCount (float).</param>
        /// <param name="requiredCount">Parameter representing the requiredCount (float).</param>
        /// <param name="isOrdered">Parameter representing the isOrdered (bool).</param>
        /// <returns>Returns normalizedProgress 0.0-1.0 of type float.</returns>
        public static float Normalize(float currentCount, float requiredCount, bool isOrdered)
        {
            if (float.IsNaN(currentCount) || float.IsNaN(requiredCount))
            {
                return 0f;
            }

            if (float.IsInfinity(currentCount) || float.IsInfinity(requiredCount))
            {
                if (float.IsPositiveInfinity(currentCount) && !float.IsPositiveInfinity(requiredCount))
                {
                    return 1f;
                }
                return 0f;
            }

            if (requiredCount <= 0f)
            {
                return 1f;
            }

            if (currentCount <= 0f)
            {
                return 0f;
            }

            float progress = currentCount / requiredCount;
            if (float.IsNaN(progress))
            {
                return 0f;
            }

            return progress > 1f ? 1f : progress;
        }
    }
}
