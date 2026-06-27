using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaPheromoneTrackingVector.
    /// Extracted from FaunaDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaPheromoneTrackingVector
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="faunaPos">Parameter representing the faunaPos (Vector3).</param>
        /// <param name="trailCoords">Parameter representing the trailCoords (Vector3[]).</param>
        /// <param name="trailStrengths">Parameter representing the trailStrengths (float[]).</param>
        /// <returns>Returns Attraction vector of type Vector3.</returns>
        public static Vector3 Calculate(Vector3 faunaPos, Vector3[] trailCoords, float[] trailStrengths)
        {
            if (trailCoords == null || trailStrengths == null || trailCoords.Length == 0 || trailStrengths.Length == 0)
                return Vector3.Zero;

            if (!float.IsFinite(faunaPos.X) || !float.IsFinite(faunaPos.Y) || !float.IsFinite(faunaPos.Z))
                return Vector3.Zero;

            int len = Math.Min(trailCoords.Length, trailStrengths.Length);
            float maxStrength = -float.MaxValue;
            int bestIndex = -1;

            for (int i = 0; i < len; i++)
            {
                float strength = trailStrengths[i];
                if (float.IsNaN(strength) || float.IsInfinity(strength) || strength <= 0f)
                    continue;

                Vector3 coord = trailCoords[i];
                if (!float.IsFinite(coord.X) || !float.IsFinite(coord.Y) || !float.IsFinite(coord.Z))
                    continue;

                if (strength > maxStrength)
                {
                    maxStrength = strength;
                    bestIndex = i;
                }
            }

            if (bestIndex == -1)
                return Vector3.Zero;

            Vector3 targetCoord = trailCoords[bestIndex];
            Vector3 diff = targetCoord - faunaPos;
            float lengthSq = diff.LengthSquared();

            if (lengthSq <= 1e-10f || float.IsNaN(lengthSq) || float.IsInfinity(lengthSq))
                return Vector3.Zero;

            return Vector3.Normalize(diff);
        }
    }
}
