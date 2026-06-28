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
            if (float.IsNaN(windVector.X) || float.IsNaN(windVector.Y) || float.IsNaN(windStrength) || float.IsNaN(depth) || float.IsNaN(decayRate))
            {
                return Vector3.Zero;
            }
            if (float.IsInfinity(windVector.X) || float.IsInfinity(windVector.Y) || float.IsInfinity(windStrength) || float.IsInfinity(depth) || float.IsInfinity(decayRate))
            {
                return Vector3.Zero;
            }

            float safeDepth = Math.Max(0f, depth);
            float safeDecay = Math.Max(0f, decayRate);

            float decayFactor = safeDepth * safeDecay;
            float surfaceAdvection01 = 1f - Math.Min(Math.Max(decayFactor, 0f), 1f);

            float forceX = windVector.X * windStrength * surfaceAdvection01;
            float forceZ = windVector.Y * windStrength * surfaceAdvection01;

            if (float.IsNaN(forceX) || float.IsNaN(forceZ) || float.IsInfinity(forceX) || float.IsInfinity(forceZ))
            {
                return Vector3.Zero;
            }

            return new Vector3(forceX, 0f, forceZ);
        }
    }
}
