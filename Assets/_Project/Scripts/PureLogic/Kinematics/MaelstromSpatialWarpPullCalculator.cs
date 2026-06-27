using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for MaelstromSpatialWarpPullCalculator.
    /// Extracted from HectonFluidEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class MaelstromSpatialWarpPullCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='objectPos'>Parameter representing the objectPos (Vector3).</param>
        /// <param name='corePos'>Parameter representing the corePos (Vector3).</param>
        /// <param name='coreRadius'>Parameter representing the coreRadius (float).</param>
        /// <param name='warpStrength'>Parameter representing the warpStrength (float).</param>
        /// <returns>Returns Suction pull vector of type Vector3.</returns>
        public static Vector3 Compute(Vector3 objectPos, Vector3 corePos, float coreRadius, float warpStrength)
        {
            if (!IsFinite(objectPos) || !IsFinite(corePos) || !float.IsFinite(coreRadius) || !float.IsFinite(warpStrength))
                return Vector3.Zero;

            if (coreRadius <= 0f || warpStrength <= 0f)
                return Vector3.Zero;

            float maxDistance = coreRadius * 2f;
            Vector3 diff = corePos - objectPos;
            float distanceSq = diff.LengthSquared();

            if (distanceSq <= 0.000001f || distanceSq >= maxDistance * maxDistance)
                return Vector3.Zero;

            float distance = (float)Math.Sqrt(distanceSq);
            Vector3 direction = diff / distance;

            // Suction increases exponentially as distance approaches coreRadius; zero pull beyond twice the coreRadius.
            // Using a simple exponential factor scaled by distance limits.
            float distanceRatio = distance / coreRadius;
            float exponentialFactor = (float)Math.Exp(1f - distanceRatio);
            float falloff = Math.Max(0f, 1f - (distance / maxDistance));
            float magnitude = warpStrength * exponentialFactor * falloff;

            if (!float.IsFinite(magnitude))
                return Vector3.Zero;

            return direction * magnitude;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
