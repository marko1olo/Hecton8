using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for SchoolingSeparationForceCalculator.
    /// Extracted from FaunaKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SchoolingSeparationForceCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="selfPosition">Parameter representing the selfPosition (Vector3).</param>
        /// <param name="neighborPositions">Parameter representing the neighborPositions (Vector3[]).</param>
        /// <param name="separationRadius">Parameter representing the separationRadius (float).</param>
        /// <param name="separationForce">Parameter representing the separationForce (float).</param>
        /// <returns>Returns separation steering force of type Vector3.</returns>
        public static Vector3 Compute(Vector3 selfPosition, Vector3[] neighborPositions, float separationRadius, float separationForce)
        {
            if (neighborPositions == null || neighborPositions.Length == 0)
                return Vector3.Zero;

            if (!IsFiniteVector(selfPosition))
                return Vector3.Zero;

            if (!IsFinite(separationRadius) || separationRadius <= 0.0001f)
                return Vector3.Zero;

            if (!IsFinite(separationForce))
                return Vector3.Zero;

            separationForce = Math.Max(0f, separationForce);

            Vector3 steering = Vector3.Zero;
            int count = 0;
            float radiusSq = separationRadius * separationRadius;

            if (float.IsInfinity(radiusSq))
                return Vector3.Zero;

            for (int i = 0; i < neighborPositions.Length; i++)
            {
                Vector3 neighbor = neighborPositions[i];
                if (!IsFiniteVector(neighbor))
                    continue;

                Vector3 diff = selfPosition - neighbor;
                float distSq = diff.LengthSquared();

                if (distSq < radiusSq)
                {
                    if (distSq < 0.000001f)
                    {
                        diff = new Vector3(0.001f, 0f, 0f);
                        distSq = diff.LengthSquared();
                    }

                    float dist = (float)Math.Sqrt(distSq);
                    Vector3 push = diff / dist;
                    float weight = 1.0f - (dist / separationRadius);

                    steering += push * weight;
                    count++;
                }
            }

            if (count > 0)
            {
                steering /= count;
            }

            Vector3 finalForce = steering * separationForce;

            if (!IsFiniteVector(finalForce))
                return Vector3.Zero;

            return finalForce;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }
    }
}