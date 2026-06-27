using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for CrouchCapsuleLerp.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class CrouchCapsuleLerp
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentHeight">Parameter representing the currentHeight (float).</param>
        /// <param name="targetHeight">Parameter representing the targetHeight (float).</param>
        /// <param name="crouchSpeed">Parameter representing the crouchSpeed (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns interpolated capsule height of type float.</returns>
        public static float Calculate(float currentHeight, float targetHeight, float crouchSpeed, float deltaTime)
        {
            if (deltaTime <= 0f) return currentHeight;
            if (float.IsNaN(currentHeight) || float.IsNaN(targetHeight) || float.IsNaN(crouchSpeed) || float.IsNaN(deltaTime)) return currentHeight;
            if (float.IsInfinity(currentHeight) || float.IsInfinity(targetHeight) || float.IsInfinity(crouchSpeed) || float.IsInfinity(deltaTime)) return currentHeight;

            crouchSpeed = Math.Max(0f, crouchSpeed);

            float diff = targetHeight - currentHeight;
            float step = crouchSpeed * deltaTime;

            if (Math.Abs(diff) <= step)
            {
                return targetHeight;
            }

            return currentHeight + Math.Sign(diff) * step;
        }
    }
}
