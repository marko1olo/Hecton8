using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaHypnosisPullForceCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaHypnosisPullForceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="playerPos">Parameter representing the playerPos (Vector3).</param>
        /// <param name="sourcePos">Parameter representing the sourcePos (Vector3).</param>
        /// <param name="acceleration">Parameter representing the acceleration (float).</param>
        /// <param name="playerMass">Parameter representing the playerMass (float).</param>
        /// <param name="lockDuration">Parameter representing the lockDuration (float).</param>
        /// <param name="minMagnitudeSqr">The minimum square magnitude threshold below which calculations stop (float).</param>
        /// <param name="epsilon">The minimal float clamping boundary to avoid divide-by-zero (float).</param>
        /// <returns>Returns Resulting force vector of type Vector3.</returns>
        public static Vector3 Compute(Vector3 playerPos, Vector3 sourcePos, float acceleration, float playerMass, float lockDuration, float minMagnitudeSqr = 0.0001f, float epsilon = 0.000001f)
        {
            if (float.IsNaN(acceleration) || float.IsInfinity(acceleration) || acceleration <= minMagnitudeSqr)
                return Vector3.Zero;

            if (float.IsNaN(playerMass) || float.IsInfinity(playerMass) || playerMass <= minMagnitudeSqr)
                return Vector3.Zero;

            if (float.IsNaN(playerPos.X) || float.IsNaN(playerPos.Y) || float.IsNaN(playerPos.Z) ||
                float.IsInfinity(playerPos.X) || float.IsInfinity(playerPos.Y) || float.IsInfinity(playerPos.Z))
                return Vector3.Zero;

            if (float.IsNaN(sourcePos.X) || float.IsNaN(sourcePos.Y) || float.IsNaN(sourcePos.Z) ||
                float.IsInfinity(sourcePos.X) || float.IsInfinity(sourcePos.Y) || float.IsInfinity(sourcePos.Z))
                return Vector3.Zero;

            Vector3 toSource = sourcePos - playerPos;
            float sqrMagnitude = toSource.LengthSquared();
            if (sqrMagnitude <= minMagnitudeSqr)
                return Vector3.Zero;

            // Apply true inverse-square falloff (force decreases proportionally to the square of the distance)
            float falloff = 1f / MathF.Max(sqrMagnitude, epsilon);
            Vector3 direction = Vector3.Normalize(toSource);
            Vector3 forceVector = direction * (falloff * playerMass * acceleration);

            return forceVector;
        }
    }
}
