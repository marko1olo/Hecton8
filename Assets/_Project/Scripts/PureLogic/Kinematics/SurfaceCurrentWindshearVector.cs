using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for SurfaceCurrentWindshearVector.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SurfaceCurrentWindshearVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="windVector">Parameter representing the windVector (Vector2).</param>
        /// <param name="windStrength">Parameter representing the windStrength (float).</param>
        /// <param name="depth">Parameter representing the depth (float).</param>
        /// <param name="decayRate">Parameter representing the decayRate (float).</param>
        /// <returns>Returns Current flow vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector2 windVector, float windStrength, float depth, float decayRate)
        {
            if (float.IsNaN(windStrength) || float.IsInfinity(windStrength) || float.IsNaN(depth) || float.IsInfinity(depth) || float.IsNaN(decayRate) || float.IsInfinity(decayRate))
            {
                return Vector3.Zero;
            }

            if (float.IsNaN(windVector.X) || float.IsInfinity(windVector.X) || float.IsNaN(windVector.Y) || float.IsInfinity(windVector.Y))
            {
                return Vector3.Zero;
            }

            float safeWindStrength = Math.Max(0f, windStrength);
            float safeDepth = Math.Max(0f, depth);
            float safeDecayRate = Math.Max(0.0001f, decayRate);

            float surfaceAdvection01 = 1f - Math.Clamp(safeDepth / safeDecayRate, 0f, 1f);

            return new Vector3(windVector.X, 0f, windVector.Y) * safeWindStrength * surfaceAdvection01;
        }
    }
}
