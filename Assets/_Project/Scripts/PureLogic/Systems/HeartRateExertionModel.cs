using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HeartRateExertionModel.
    /// Extracted from ShinobuPhysiologyRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HeartRateExertionModel
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentHR">Parameter representing the currentHR (float).</param>
        /// <param name="exertion01">Parameter representing the exertion01 (float).</param>
        /// <param name="stressLevel01">Parameter representing the stressLevel01 (float).</param>
        /// <param name="restingHR">Parameter representing the restingHR (float).</param>
        /// <param name="maxHR">Parameter representing the maxHR (float).</param>
        /// <param name="adaptationSpeed">Parameter representing the adaptationSpeed (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns current heart rate BPM of type float.</returns>
        public static float Evaluate(float currentHR, float exertion01, float stressLevel01, float restingHR, float maxHR, float adaptationSpeed, float deltaTime)
        {
            if (float.IsNaN(currentHR) || float.IsInfinity(currentHR)) throw new ArgumentOutOfRangeException(nameof(currentHR), "currentHR cannot be NaN or Infinity.");
            if (float.IsNaN(exertion01) || float.IsInfinity(exertion01)) throw new ArgumentOutOfRangeException(nameof(exertion01), "exertion01 cannot be NaN or Infinity.");
            if (float.IsNaN(stressLevel01) || float.IsInfinity(stressLevel01)) throw new ArgumentOutOfRangeException(nameof(stressLevel01), "stressLevel01 cannot be NaN or Infinity.");
            if (float.IsNaN(restingHR) || float.IsInfinity(restingHR)) throw new ArgumentOutOfRangeException(nameof(restingHR), "restingHR cannot be NaN or Infinity.");
            if (float.IsNaN(maxHR) || float.IsInfinity(maxHR)) throw new ArgumentOutOfRangeException(nameof(maxHR), "maxHR cannot be NaN or Infinity.");
            if (float.IsNaN(adaptationSpeed) || float.IsInfinity(adaptationSpeed)) throw new ArgumentOutOfRangeException(nameof(adaptationSpeed), "adaptationSpeed cannot be NaN or Infinity.");
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime cannot be NaN or Infinity.");

            if (restingHR < 0f) throw new ArgumentOutOfRangeException(nameof(restingHR), "restingHR cannot be negative.");
            if (maxHR < restingHR) throw new ArgumentOutOfRangeException(nameof(maxHR), "maxHR cannot be less than restingHR.");
            if (adaptationSpeed < 0f) throw new ArgumentOutOfRangeException(nameof(adaptationSpeed), "adaptationSpeed cannot be negative.");
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), "deltaTime cannot be negative.");

            exertion01 = Math.Clamp(exertion01, 0f, 1f);
            stressLevel01 = Math.Clamp(stressLevel01, 0f, 1f);

            float totalLoad = Math.Clamp(exertion01 + stressLevel01, 0f, 1f);

            float targetHR = restingHR + (maxHR - restingHR) * totalLoad;

            // Smooth approach to target HR
            float diff = targetHR - currentHR;
            float change = diff * adaptationSpeed * deltaTime;

            // Do not overshoot
            if (Math.Abs(change) > Math.Abs(diff))
            {
                change = diff;
            }

            float newHR = currentHR + change;

            return Math.Clamp(newHR, restingHR, maxHR);
        }
    }
}
