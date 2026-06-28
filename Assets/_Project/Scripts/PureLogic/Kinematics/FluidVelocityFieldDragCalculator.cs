using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for FluidVelocityFieldDragCalculator.
    /// Extracted from SubmarineFluidDynamics.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FluidVelocityFieldDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="hullVelocity">Parameter representing the hullVelocity (Vector3).</param>
        /// <param name="currentVelocity">Parameter representing the currentVelocity (Vector3).</param>
        /// <param name="dragCoefficient">Parameter representing the dragCoefficient (float).</param>
        /// <param name="frontalArea">Parameter representing the frontalArea (float).</param>
        /// <param name="epsilon">Parameter representing the speed epsilon threshold (float).</param>
        /// <returns>Returns Resulting drag force vector of type Vector3.</returns>
        public static Vector3 Compute(Vector3 hullVelocity, Vector3 currentVelocity, float dragCoefficient, float frontalArea, float epsilon = 1e-6f)
        {
            if (float.IsNaN(dragCoefficient) || float.IsInfinity(dragCoefficient) || dragCoefficient < 0f)
            {
                dragCoefficient = 0f;
            }

            if (float.IsNaN(frontalArea) || float.IsInfinity(frontalArea) || frontalArea < 0f)
            {
                frontalArea = 0f;
            }

            if (!IsFinite(hullVelocity)) hullVelocity = Vector3.Zero;
            if (!IsFinite(currentVelocity)) currentVelocity = Vector3.Zero;

            Vector3 relativeVelocity = hullVelocity - currentVelocity;
            float speedSquared = relativeVelocity.LengthSquared();

            if (speedSquared < epsilon)
            {
                return Vector3.Zero;
            }

            Vector3 direction = Vector3.Normalize(relativeVelocity);

            // Based on SubmarineFluidDynamics.cs, the drag is:
            // (-direction * speed * abs(speed) * dragCoefficient) * frontalArea (or just substituting area)
            // Here: direction * speedSquared * dragCoefficient * frontalArea

            // F_drag = -direction * v^2 * C_d * A
            // In SubmarineFluidDynamics.cs: dragForce = -direction * speed * abs(speed) * C_d
            // So:
            Vector3 dragForce = -direction * speedSquared * dragCoefficient * frontalArea;

            // Simple clamp for extreme values to prevent overflow
            if (!IsFinite(dragForce))
            {
                return Vector3.Zero;
            }

            return dragForce;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
