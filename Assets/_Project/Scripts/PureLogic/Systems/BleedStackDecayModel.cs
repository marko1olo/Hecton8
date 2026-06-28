using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BleedStackDecayModel.
    /// Extracted from CombatDamageRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BleedStackDecayModel
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentBleedStacks">Parameter representing the currentBleedStacks (float).</param>
        /// <param name="newStacksAdded">Parameter representing the newStacksAdded (float).</param>
        /// <param name="decayRatePerSecond">Parameter representing the decayRatePerSecond (float).</param>
        /// <param name="maxStacks">Parameter representing the maxStacks (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns new bleed stack count, float (damage this frame from bleed) of type float.</returns>
        public static float Evaluate(float currentBleedStacks, float newStacksAdded, float decayRatePerSecond, float maxStacks, float deltaTime)
        {
            if (float.IsNaN(currentBleedStacks) || float.IsInfinity(currentBleedStacks)) currentBleedStacks = 0f;
            if (float.IsNaN(newStacksAdded) || float.IsInfinity(newStacksAdded)) newStacksAdded = 0f;
            if (float.IsNaN(decayRatePerSecond) || float.IsInfinity(decayRatePerSecond)) decayRatePerSecond = 0f;
            if (float.IsNaN(maxStacks) || float.IsInfinity(maxStacks)) maxStacks = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;

            if (currentBleedStacks < 0f) currentBleedStacks = 0f;
            if (newStacksAdded < 0f) newStacksAdded = 0f;
            if (decayRatePerSecond < 0f) decayRatePerSecond = 0f;
            if (maxStacks < 0f) maxStacks = 0f;
            if (deltaTime < 0f) deltaTime = 0f;

            float totalStacks = currentBleedStacks + newStacksAdded;

            if (totalStacks > maxStacks)
            {
                totalStacks = maxStacks;
            }

            float decayAmount = decayRatePerSecond * deltaTime;
            totalStacks -= decayAmount;

            if (totalStacks < 0f)
            {
                totalStacks = 0f;
            }

            return totalStacks;
        }
    }
}
