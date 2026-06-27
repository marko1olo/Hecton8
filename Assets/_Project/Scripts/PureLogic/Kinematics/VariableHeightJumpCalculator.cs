using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for VariableHeightJumpCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VariableHeightJumpCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='heldTime'>Parameter representing the heldTime (float).</param>
        /// <param name='maxJumpTime'>Parameter representing the maxJumpTime (float).</param>
        /// <param name='minJumpVelocity'>Parameter representing the minJumpVelocity (float).</param>
        /// <param name='maxJumpVelocity'>Parameter representing the maxJumpVelocity (float).</param>
        /// <returns>Returns vertical jump velocity of type float.</returns>
        public static float Compute(float heldTime, float maxJumpTime, float minJumpVelocity, float maxJumpVelocity)
        {
            if (float.IsNaN(heldTime) || float.IsNaN(maxJumpTime) || float.IsNaN(minJumpVelocity) || float.IsNaN(maxJumpVelocity))
                return minJumpVelocity;

            if (maxJumpTime <= 0f) return maxJumpVelocity;

            float t = Math.Clamp(heldTime / maxJumpTime, 0f, 1f);
            // Smoothstep
            t = t * t * (3f - 2f * t);

            return minJumpVelocity + (maxJumpVelocity - minJumpVelocity) * t;
        }
    }
}
