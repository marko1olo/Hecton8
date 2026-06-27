using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BallastTankController.
    /// Extracted from SubmarineDynamicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BallastTankController
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentFillLevel01">Parameter representing the currentFillLevel01 (float).</param>
        /// <param name="targetFillLevel">Parameter representing the targetFillLevel (float).</param>
        /// <param name="fillRatePerSec">Parameter representing the fillRatePerSec (float).</param>
        /// <param name="ventRatePerSec">Parameter representing the ventRatePerSec (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns new fill level 0.0-1.0 of type float.</returns>
        public static float Calculate(float currentFillLevel01, float targetFillLevel, float fillRatePerSec, float ventRatePerSec, float deltaTime)
        {
            if (float.IsNaN(currentFillLevel01) || float.IsInfinity(currentFillLevel01))
                currentFillLevel01 = 0f;
            if (float.IsNaN(targetFillLevel) || float.IsInfinity(targetFillLevel))
                targetFillLevel = 0f;
            if (float.IsNaN(fillRatePerSec) || float.IsInfinity(fillRatePerSec) || fillRatePerSec < 0f)
                fillRatePerSec = 0f;
            if (float.IsNaN(ventRatePerSec) || float.IsInfinity(ventRatePerSec) || ventRatePerSec < 0f)
                ventRatePerSec = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return Math.Clamp(currentFillLevel01, 0f, 1f);

            float fill = Math.Clamp(currentFillLevel01, 0f, 1f);
            float target = Math.Clamp(targetFillLevel, 0f, 1f);

            if (fill < target)
            {
                fill += fillRatePerSec * deltaTime;
                if (fill > target) fill = target;
            }
            else if (fill > target)
            {
                fill -= ventRatePerSec * deltaTime;
                if (fill < target) fill = target;
            }

            return Math.Clamp(fill, 0f, 1f);
        }
    }
}