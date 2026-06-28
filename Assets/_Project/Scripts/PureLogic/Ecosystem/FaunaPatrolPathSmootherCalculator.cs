using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaPatrolPathSmootherCalculator.
    /// Extracted from FaunaDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaPatrolPathSmootherCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="p0">Parameter representing the p0 (Vector3).</param>
        /// <param name="p1">Parameter representing the p1 (Vector3).</param>
        /// <param name="p2">Parameter representing the p2 (Vector3).</param>
        /// <param name="p3">Parameter representing the p3 (Vector3).</param>
        /// <param name="t">Parameter representing the t (float).</param>
        /// <returns>Returns Interpolated position of type Vector3.</returns>
        public static Vector3 Compute(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            if (float.IsNaN(p0.X) || float.IsNaN(p0.Y) || float.IsNaN(p0.Z) || float.IsInfinity(p0.X) || float.IsInfinity(p0.Y) || float.IsInfinity(p0.Z)) p0 = Vector3.Zero;
            if (float.IsNaN(p1.X) || float.IsNaN(p1.Y) || float.IsNaN(p1.Z) || float.IsInfinity(p1.X) || float.IsInfinity(p1.Y) || float.IsInfinity(p1.Z)) p1 = Vector3.Zero;
            if (float.IsNaN(p2.X) || float.IsNaN(p2.Y) || float.IsNaN(p2.Z) || float.IsInfinity(p2.X) || float.IsInfinity(p2.Y) || float.IsInfinity(p2.Z)) p2 = Vector3.Zero;
            if (float.IsNaN(p3.X) || float.IsNaN(p3.Y) || float.IsNaN(p3.Z) || float.IsInfinity(p3.X) || float.IsInfinity(p3.Y) || float.IsInfinity(p3.Z)) p3 = Vector3.Zero;

            if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;

            float safeT = Math.Clamp(t, 0f, 1f);

            float t2 = safeT * safeT;
            float t3 = t2 * safeT;

            Vector3 value = (
                (p1 * 2.0f) +
                ((-p0 + p2) * safeT) +
                ((p0 * 2.0f) - (p1 * 5.0f) + (p2 * 4.0f) - p3) * t2 +
                ((-p0) + (p1 * 3.0f) - (p2 * 3.0f) + p3) * t3) * 0.5f;

            if (float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z) || float.IsInfinity(value.X) || float.IsInfinity(value.Y) || float.IsInfinity(value.Z))
                return p1;

            return value;
        }
    }
}
