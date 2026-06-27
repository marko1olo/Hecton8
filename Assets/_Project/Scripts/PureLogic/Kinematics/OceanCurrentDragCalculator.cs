using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for OceanCurrentDragCalculator.
    /// Extracted from HydrodynamicKccRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class OceanCurrentDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="oceanCurrentVelocity">Parameter representing the oceanCurrentVelocity (Vector3).</param>
        /// <param name="playerVelocity">Parameter representing the playerVelocity (Vector3).</param>
        /// <param name="dragCoeff">Parameter representing the dragCoeff (float).</param>
        /// <param name="crossSectionalArea">Parameter representing the crossSectionalArea (float).</param>
        /// <returns>Returns drag force vector of type Vector3.</returns>
        public static Vector3 Compute(Vector3 oceanCurrentVelocity, Vector3 playerVelocity, float dragCoeff, float crossSectionalArea)
        {
            if (!IsFinite(oceanCurrentVelocity) || !IsFinite(playerVelocity))
                return Vector3.Zero;

            float safeDragCoeff = float.IsFinite(dragCoeff) ? Math.Max(0f, dragCoeff) : 0f;
            float safeArea = float.IsFinite(crossSectionalArea) ? Math.Max(0f, crossSectionalArea) : 0f;

            Vector3 relativeVelocity = oceanCurrentVelocity - playerVelocity;
            float speedSq = relativeVelocity.LengthSquared();

            if (speedSq <= 0.000001f)
                return Vector3.Zero;

            float speed = (float)Math.Sqrt(speedSq);
            Vector3 direction = relativeVelocity / speed;

            float dragMagnitude = 0.5f * safeDragCoeff * safeArea * speedSq;
            Vector3 force = direction * dragMagnitude;

            if (!IsFinite(force))
                return Vector3.Zero;

            return force;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
