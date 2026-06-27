using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PoissondiscLandmarkSpacingSolver.
    /// Extracted from WorldContentDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PoissondiscLandmarkSpacingSolver
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="candidateCoord">Parameter representing the candidateCoord (Vector3).</param>
        /// <param name="existingCoords">Parameter representing the existingCoords (Vector3[]).</param>
        /// <param name="minDistance">Parameter representing the minDistance (float).</param>
        /// <returns>Returns Placement Allowed of type bool.</returns>
        public static bool Solve(Vector3 candidateCoord, Vector3[] existingCoords, float minDistance)
        {
            if (float.IsNaN(candidateCoord.X) || float.IsNaN(candidateCoord.Y) || float.IsNaN(candidateCoord.Z) ||
                float.IsInfinity(candidateCoord.X) || float.IsInfinity(candidateCoord.Y) || float.IsInfinity(candidateCoord.Z))
            {
                return false;
            }

            if (existingCoords == null)
            {
                return true;
            }

            if (float.IsNaN(minDistance) || float.IsInfinity(minDistance) || minDistance <= 0f)
            {
                minDistance = 0f;
            }

            float minDistanceSqr = minDistance * minDistance;

            for (int i = 0; i < existingCoords.Length; i++)
            {
                Vector3 existing = existingCoords[i];
                if (float.IsNaN(existing.X) || float.IsNaN(existing.Y) || float.IsNaN(existing.Z) ||
                    float.IsInfinity(existing.X) || float.IsInfinity(existing.Y) || float.IsInfinity(existing.Z))
                {
                    continue;
                }

                float distSqr = Vector3.DistanceSquared(candidateCoord, existing);
                if (distSqr < minDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
