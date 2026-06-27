using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SprintStaminaGate.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SprintStaminaGate
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='currentStamina'>Parameter representing the currentStamina (float).</param>
        /// <param name='sprintEnterThreshold'>Parameter representing the sprintEnterThreshold (float).</param>
        /// <param name='sprintExitThreshold'>Parameter representing the sprintExitThreshold (float).</param>
        /// <param name='isCurrentlySprinting'>Parameter representing the isCurrentlySprinting (bool).</param>
        /// <returns>Returns canSprint of type bool.</returns>
        public static bool EvaluateGate(float currentStamina, float sprintEnterThreshold, float sprintExitThreshold, bool isCurrentlySprinting)
        {
            if (float.IsNaN(currentStamina) || float.IsInfinity(currentStamina)) currentStamina = 0f;
            if (float.IsNaN(sprintEnterThreshold) || float.IsInfinity(sprintEnterThreshold)) sprintEnterThreshold = 0f;
            if (float.IsNaN(sprintExitThreshold) || float.IsInfinity(sprintExitThreshold)) sprintExitThreshold = 0f;

            currentStamina = Math.Max(0f, currentStamina);
            sprintEnterThreshold = Math.Max(0f, sprintEnterThreshold);
            sprintExitThreshold = Math.Max(0f, sprintExitThreshold);

            if (isCurrentlySprinting)
            {
                return currentStamina > sprintExitThreshold;
            }
            else
            {
                return currentStamina >= sprintEnterThreshold;
            }
        }
    }
}
