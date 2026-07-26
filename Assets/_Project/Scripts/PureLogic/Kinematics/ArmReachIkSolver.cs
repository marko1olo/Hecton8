using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for ArmReachIkSolver.
    /// Extracted from ExosuitKinematicsRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ArmReachIkSolver
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="shoulderPos">Parameter representing the shoulderPos (Vector3).</param>
        /// <param name="targetPos">Parameter representing the targetPos (Vector3).</param>
        /// <param name="upperArmLength">Parameter representing the upperArmLength (float).</param>
        /// <param name="forearmLength">Parameter representing the forearmLength (float).</param>
        /// <param name="epsilon">Small threshold for distance equality checks.</param>
        /// <param name="collinearThreshold">Threshold to determine if direction is too close to the up vector.</param>
        /// <returns>Returns canReach of type Vector3 elbowPos, Vector3 handPos, bool.</returns>
        public static (Vector3 elbowPos, Vector3 handPos, bool canReach) Solve(Vector3 shoulderPos, Vector3 targetPos, float upperArmLength, float forearmLength, float epsilon = 0.0001f, float collinearThreshold = 0.99f)
        {
            // Step 1 - Parameter Validation
            if (float.IsNaN(shoulderPos.X) || float.IsNaN(shoulderPos.Y) || float.IsNaN(shoulderPos.Z) || float.IsInfinity(shoulderPos.X) || float.IsInfinity(shoulderPos.Y) || float.IsInfinity(shoulderPos.Z))
                shoulderPos = Vector3.Zero;

            if (float.IsNaN(targetPos.X) || float.IsNaN(targetPos.Y) || float.IsNaN(targetPos.Z) || float.IsInfinity(targetPos.X) || float.IsInfinity(targetPos.Y) || float.IsInfinity(targetPos.Z))
                targetPos = shoulderPos;

            if (float.IsNaN(upperArmLength) || float.IsInfinity(upperArmLength) || upperArmLength < 0f)
                upperArmLength = 0f;

            if (float.IsNaN(forearmLength) || float.IsInfinity(forearmLength) || forearmLength < 0f)
                forearmLength = 0f;

            // Step 2 - Business Logic
            Vector3 shoulderToTarget = targetPos - shoulderPos;
            float distanceToTarget = shoulderToTarget.Length();
            float maxReach = upperArmLength + forearmLength;

            if (distanceToTarget <= epsilon || maxReach <= epsilon)
            {
                // Target is at shoulder or limbs have no length
                return (shoulderPos, shoulderPos, true);
            }

            Vector3 directionToTarget = shoulderToTarget / distanceToTarget;

            if (distanceToTarget >= maxReach)
            {
                // Max extension. At exact equality the straight arm reaches the target, so
                // canReach must be true there — only strictly-beyond is a failed reach.
                // Handling equality here also keeps the cosine rule below free of the
                // 0/0 case (upperArmLength == 0 with distance == forearmLength).
                Vector3 elbow = shoulderPos + directionToTarget * upperArmLength;
                Vector3 hand = shoulderPos + directionToTarget * maxReach;
                return (elbow, hand, distanceToTarget <= maxReach);
            }

            // Cosine rule to find angle at shoulder
            // a^2 = b^2 + c^2 - 2bc*cos(A)
            // forearmLength^2 = upperArmLength^2 + distanceToTarget^2 - 2*upperArmLength*distanceToTarget*cos(A)
            // cos(A) = (upperArmLength^2 + distanceToTarget^2 - forearmLength^2) / (2 * upperArmLength * distanceToTarget)

            float cosAngle = (upperArmLength * upperArmLength + distanceToTarget * distanceToTarget - forearmLength * forearmLength) / (2 * upperArmLength * distanceToTarget);
            cosAngle = Math.Clamp(cosAngle, -1f, 1f); // Boundary Guarding

            float angle = (float)Math.Acos(cosAngle);

            // We need a plane to bend the elbow. Since this is purely geometric and lacks a hint/pole vector,
            // we'll choose a deterministic orthogonal vector (e.g. up, or fallback to forward).
            Vector3 up = Vector3.UnitY;
            if (Math.Abs(Vector3.Dot(directionToTarget, up)) > collinearThreshold)
            {
                up = Vector3.UnitZ; // fallback if target is directly up/down
            }

            Vector3 right = Vector3.Normalize(Vector3.Cross(up, directionToTarget));

            // Rotate directionToTarget by 'angle' around 'right'
            // Using quaternion math or AxisAngle
            Quaternion rotation = Quaternion.CreateFromAxisAngle(right, angle);
            Vector3 upperArmDirection = Vector3.Transform(directionToTarget, rotation);

            Vector3 elbowPos = shoulderPos + upperArmDirection * upperArmLength;

            // Hand always reaches the target if within maxReach
            return (elbowPos, targetPos, true);
        }
    }
}
