using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for RockAlignmentSplineNormalCalculator.
    /// Extracted from HectonRockManager.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class RockAlignmentSplineNormalCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="splineTangent">Parameter representing the splineTangent (Vector3).</param>
        /// <param name="terrainNormal">Parameter representing the terrainNormal (Vector3).</param>
        /// <returns>Returns 4-element float array representing orientation quaternion of type float[].</returns>
        public static float[] Compute(Vector3 splineTangent, Vector3 terrainNormal)
        {
            float lenTangentSq = splineTangent.LengthSquared();
            float lenNormalSq = terrainNormal.LengthSquared();

            if (float.IsNaN(lenTangentSq) || float.IsNaN(lenNormalSq) ||
                float.IsInfinity(lenTangentSq) || float.IsInfinity(lenNormalSq))
            {
                return new float[] { 0f, 0f, 0f, 1f }; // Identity
            }

            Vector3 up = terrainNormal;
            if (lenNormalSq < 0.000001f)
            {
                up = Vector3.UnitY;
            }
            else
            {
                up = Vector3.Normalize(up);
            }

            Vector3 forward = splineTangent;
            if (lenTangentSq < 0.000001f)
            {
                forward = Vector3.UnitZ;
            }
            else
            {
                forward = Vector3.Normalize(forward);
            }

            // Prevent collinearity issues
            if (Math.Abs(Vector3.Dot(forward, up)) > 0.9999f)
            {
                // If forward is collinear with up, choose an arbitrary orthogonal vector
                forward = Math.Abs(up.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
                forward = Vector3.Normalize(forward - Vector3.Dot(forward, up) * up);
            }

            // Create rotation looking along 'forward' with 'up' vector
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            Vector3 orthoForward = Vector3.Cross(right, up);

            // Create rotation matrix and convert to quaternion
            Matrix4x4 matrix = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                orthoForward.X, orthoForward.Y, orthoForward.Z, 0,
                0, 0, 0, 1
            );

            Quaternion q = Quaternion.CreateFromRotationMatrix(matrix);
            q = Quaternion.Normalize(q);

            return new float[] { q.X, q.Y, q.Z, q.W };
        }
    }
}
