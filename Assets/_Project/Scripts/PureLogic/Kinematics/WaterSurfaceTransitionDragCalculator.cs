using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for WaterSurfaceTransitionDragCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class WaterSurfaceTransitionDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="entryVelocity">Parameter representing the entryVelocity (Vector3).</param>
        /// <param name="surfaceDensity">Parameter representing the surfaceDensity (float).</param>
        /// <param name="bodyCrossSection">Parameter representing the bodyCrossSection (float).</param>
        /// <returns>Returns Deceleration impulse of type Vector3.</returns>
        public static Vector3 Compute(Vector3 entryVelocity, float surfaceDensity, float bodyCrossSection)
        {
            if (float.IsNaN(entryVelocity.X) || float.IsNaN(entryVelocity.Y) || float.IsNaN(entryVelocity.Z) ||
                float.IsInfinity(entryVelocity.X) || float.IsInfinity(entryVelocity.Y) || float.IsInfinity(entryVelocity.Z))
            {
                return Vector3.Zero;
            }

            if (float.IsNaN(surfaceDensity) || float.IsInfinity(surfaceDensity))
            {
                return Vector3.Zero;
            }

            if (float.IsNaN(bodyCrossSection) || float.IsInfinity(bodyCrossSection))
            {
                return Vector3.Zero;
            }

            float safeDensity = surfaceDensity < 0f ? 0f : surfaceDensity;
            float safeCrossSection = bodyCrossSection < 0f ? 0f : bodyCrossSection;

            float speedSq = entryVelocity.LengthSquared();
            if (speedSq < 0.0001f)
            {
                return Vector3.Zero;
            }

            float speed = (float)Math.Sqrt(speedSq);
            Vector3 direction = entryVelocity / speed;

            float dragMagnitude = 0.5f * safeDensity * speedSq * safeCrossSection;

            if (dragMagnitude > speed)
            {
                dragMagnitude = speed;
            }

            return -direction * dragMagnitude;
        }
    }
}
