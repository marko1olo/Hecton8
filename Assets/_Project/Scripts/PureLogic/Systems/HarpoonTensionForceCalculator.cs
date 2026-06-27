using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HarpoonTensionForceCalculator.
    /// Extracted from HarpoonTensionSolver328.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HarpoonTensionForceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentLength">Parameter representing the currentLength (float).</param>
        /// <param name="restLength">Parameter representing the restLength (float).</param>
        /// <param name="stiffness">Parameter representing the stiffness (float).</param>
        /// <param name="dampingCoeff">Parameter representing the dampingCoeff (float).</param>
        /// <param name="extensionVelocity">Parameter representing the extensionVelocity (float).</param>
        /// <returns>Returns tension force in Newtons of type float.</returns>
        public static float Compute(float currentLength, float restLength, float stiffness, float dampingCoeff, float extensionVelocity)
        {
            if (float.IsNaN(currentLength) || float.IsInfinity(currentLength) ||
                float.IsNaN(restLength) || float.IsInfinity(restLength) ||
                float.IsNaN(stiffness) || float.IsInfinity(stiffness) ||
                float.IsNaN(dampingCoeff) || float.IsInfinity(dampingCoeff) ||
                float.IsNaN(extensionVelocity) || float.IsInfinity(extensionVelocity))
            {
                return 0f;
            }

            float clampedCurrentLength = Math.Max(0f, currentLength);
            float clampedRestLength = Math.Max(0f, restLength);
            float clampedStiffness = Math.Max(0f, stiffness);
            float clampedDamping = Math.Max(0f, dampingCoeff);

            float stretch = clampedCurrentLength - clampedRestLength;
            if (stretch <= 0f)
            {
                return 0f;
            }

            float force = (stretch * clampedStiffness) + (extensionVelocity * clampedDamping);

            return Math.Max(0f, force);
        }
    }
}
