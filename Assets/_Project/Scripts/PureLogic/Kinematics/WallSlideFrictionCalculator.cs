using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for WallSlideFrictionCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class WallSlideFrictionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="slideVelocity">Parameter representing the slideVelocity (float).</param>
        /// <param name="wallFrictionCoeff">Parameter representing the wallFrictionCoeff (float).</param>
        /// <param name="gravityScale">Parameter representing the gravityScale (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns resulting slide velocity after friction deceleration of type float.</returns>
        public static float Compute(float slideVelocity, float wallFrictionCoeff, float gravityScale, float deltaTime)
        {
            if (float.IsNaN(slideVelocity) || float.IsInfinity(slideVelocity)) slideVelocity = 0f;
            if (float.IsNaN(wallFrictionCoeff)) wallFrictionCoeff = 0f;
            if (float.IsNaN(gravityScale) || float.IsInfinity(gravityScale)) gravityScale = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;

            if (deltaTime <= 0f) return slideVelocity;

            // clamp coefficients
            wallFrictionCoeff = Math.Max(0f, wallFrictionCoeff);
            gravityScale = Math.Max(0f, gravityScale);

            if (float.IsInfinity(wallFrictionCoeff)) return 0f;

            float deceleration = wallFrictionCoeff * gravityScale * deltaTime;

            float sign = Math.Sign(slideVelocity);
            float speed = Math.Abs(slideVelocity);

            float newSpeed = Math.Max(0f, speed - deceleration);
            return newSpeed * sign;
        }
    }
}
