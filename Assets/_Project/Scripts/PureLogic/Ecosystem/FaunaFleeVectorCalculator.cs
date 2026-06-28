using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for FaunaFleeVectorCalculator.
    /// Extracted from FaunaKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FaunaFleeVectorCalculator
    {
        private const float DefaultEpsilonSquared = 0.000001f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="selfPos">Parameter representing the selfPos (Vector3).</param>
        /// <param name="threatPos">Parameter representing the threatPos (Vector3).</param>
        /// <param name="obstaclePositions">Parameter representing the obstaclePositions (Vector3[]).</param>
        /// <param name="obstacleAvoidRadius">Parameter representing the obstacleAvoidRadius (float).</param>
        /// <param name="fleeBias">Parameter representing the fleeBias (float).</param>
        /// <returns>Returns flee direction, normalized of type Vector3.</returns>
        public static Vector3 Compute(Vector3 selfPos, Vector3 threatPos, Vector3[] obstaclePositions, float obstacleAvoidRadius, float fleeBias)
        {
            if (!IsFinite(selfPos) || !IsFinite(threatPos) || float.IsNaN(obstacleAvoidRadius) || float.IsNaN(fleeBias))
            {
                return Vector3.UnitX;
            }

            float zero = 0f;
            float safeAvoidRadius = Math.Max(zero, obstacleAvoidRadius);
            float safeFleeBias = Math.Max(zero, fleeBias);

            Vector3 fleeVector = selfPos - threatPos;

            if (fleeVector.LengthSquared() < DefaultEpsilonSquared)
            {
                fleeVector = Vector3.UnitX;
            }
            else
            {
                fleeVector = Vector3.Normalize(fleeVector);
            }

            Vector3 avoidanceVector = Vector3.Zero;

            if (obstaclePositions != null)
            {
                for (int i = 0; i < obstaclePositions.Length; i++)
                {
                    Vector3 obs = obstaclePositions[i];
                    if (!IsFinite(obs)) continue;

                    Vector3 toSelf = selfPos - obs;
                    float distSq = toSelf.LengthSquared();

                    if (distSq > DefaultEpsilonSquared && distSq < safeAvoidRadius * safeAvoidRadius)
                    {
                        float dist = (float)Math.Sqrt(distSq);
                        float one = 1f;
                        float weight = (one - (dist / safeAvoidRadius)) * safeFleeBias;
                        avoidanceVector += Vector3.Normalize(toSelf) * weight;
                    }
                    else if (distSq <= DefaultEpsilonSquared && safeAvoidRadius > zero)
                    {
                        avoidanceVector += Vector3.UnitY * safeFleeBias;
                    }
                }
            }

            Vector3 finalVector = fleeVector + avoidanceVector;

            if (finalVector.LengthSquared() < DefaultEpsilonSquared)
            {
                return Vector3.UnitX;
            }

            return Vector3.Normalize(finalVector);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
