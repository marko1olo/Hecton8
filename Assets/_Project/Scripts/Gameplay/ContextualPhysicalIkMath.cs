using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ContextualPhysicalIkMath
    {
        private const float MinimumLengthSq = 0.00000001f;
        private const float MinimumDistance = 0.0001f;

        public static float SmoothAlpha(float sharpness, float deltaTime)
        {
            return 1.0f - ApproximateExpNegPositive(math.max(0.0f, sharpness) * math.max(0.0f, deltaTime));
        }

        public static float SmoothScalar(float current, float target, float sharpness, float deltaTime)
        {
            return math.lerp(current, target, SmoothAlpha(sharpness, deltaTime));
        }

        public static float3 SmoothVector(float3 current, float3 target, float sharpness, float deltaTime)
        {
            return math.lerp(current, target, SmoothAlpha(sharpness, deltaTime));
        }

        public static float3 SafeNormalize(float3 value, float3 fallback)
        {
            if (math.any(math.isnan(value)) || !math.all(math.isfinite(value)))
            {
                return (math.any(math.isnan(fallback)) || !math.all(math.isfinite(fallback)))
                    ? new float3(0.0f, 0.0f, 1.0f)
                    : fallback;
            }

            float lengthSq = math.lengthsq(value);
            if (lengthSq <= MinimumLengthSq)
            {
                return (math.any(math.isnan(fallback)) || !math.all(math.isfinite(fallback)))
                    ? new float3(0.0f, 0.0f, 1.0f)
                    : fallback;
            }

            float length = math.max(MinimumDistance, ApproximateLengthNoSqrt(value));
            return value * math.rcp(length);
        }

        public static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float safeT = math.saturate(t);
            float t2 = safeT * safeT;
            float t3 = t2 * safeT;
            float3 value = 0.5f * (
                (2.0f * p1) +
                ((-p0 + p2) * safeT) +
                ((2.0f * p0) - (5.0f * p1) + (4.0f * p2) - p3) * t2 +
                ((-p0) + (3.0f * p1) - (3.0f * p2) + p3) * t3);

            return math.select(value, p1, !math.all(math.isfinite(value)));
        }

        public static float3 CatmullRomTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t, float3 fallback)
        {
            float safeT = math.saturate(t);
            float t2 = safeT * safeT;
            float3 tangent = 0.5f * (
                (-p0 + p2) +
                (2.0f * ((2.0f * p0) - (5.0f * p1) + (4.0f * p2) - p3) * safeT) +
                (3.0f * ((-p0) + (3.0f * p1) - (3.0f * p2) + p3) * t2));

            tangent = math.select(tangent, fallback, !math.all(math.isfinite(tangent)));
            return SafeNormalize(tangent, fallback);
        }

        public static float3 EvaluateSpinePosition(
            float3 rootPosition,
            float3 chestTarget,
            float3 headTarget,
            float3 headForward,
            float normalizedT)
        {
            float3 rootToChest = chestTarget - rootPosition;
            float rootToChestLength = math.max(0.1f, ApproximateLengthNoSqrt(rootToChest));
            float3 rootForward = SafeNormalize(rootToChest, new float3(0.0f, 0.0f, 1.0f));
            float3 safeHeadForward = SafeNormalize(headForward, rootForward);
            float3 beforeRoot = rootPosition - (rootForward * (rootToChestLength * 0.25f));
            float3 afterHead = headTarget + (safeHeadForward * (rootToChestLength * 0.2f));
            float clampedT = math.saturate(normalizedT);

            if (clampedT <= 0.5f)
                return CatmullRom(beforeRoot, rootPosition, chestTarget, headTarget, clampedT * 2.0f);

            return CatmullRom(rootPosition, chestTarget, headTarget, afterHead, (clampedT - 0.5f) * 2.0f);
        }

        public static float3 EvaluateSpineTangent(
            float3 rootPosition,
            float3 chestTarget,
            float3 headTarget,
            float3 headForward,
            float normalizedT,
            float3 fallback)
        {
            float3 rootToChest = chestTarget - rootPosition;
            float rootToChestLength = math.max(0.1f, ApproximateLengthNoSqrt(rootToChest));
            float3 rootForward = SafeNormalize(rootToChest, fallback);
            float3 safeHeadForward = SafeNormalize(headForward, rootForward);
            float3 beforeRoot = rootPosition - (rootForward * (rootToChestLength * 0.25f));
            float3 afterHead = headTarget + (safeHeadForward * (rootToChestLength * 0.2f));
            float clampedT = math.saturate(normalizedT);

            if (clampedT <= 0.5f)
                return CatmullRomTangent(beforeRoot, rootPosition, chestTarget, headTarget, clampedT * 2.0f, fallback);

            return CatmullRomTangent(rootPosition, chestTarget, headTarget, afterHead, (clampedT - 0.5f) * 2.0f, fallback);
        }

        public static void IntegrateSpringDamper(
            float3 targetPosition,
            float stiffness,
            float damping,
            float deltaTime,
            ref float3 currentPosition,
            ref float3 currentVelocity)
        {
            if (math.any(math.isnan(targetPosition)) ||
                math.any(math.isnan(currentPosition)) ||
                math.any(math.isnan(currentVelocity)) ||
                !math.all(math.isfinite(targetPosition)) ||
                !math.all(math.isfinite(currentPosition)) ||
                !math.all(math.isfinite(currentVelocity)))
            {
                currentPosition = math.select(targetPosition, float3.zero, !math.all(math.isfinite(targetPosition)));
                currentVelocity = float3.zero;
                return;
            }

            float safeDt = math.max(0.0001f, deltaTime);
            float safeStiffness = math.max(0.0f, stiffness);
            float safeDamping = math.max(0.0f, damping);
            float3 acceleration = ((targetPosition - currentPosition) * safeStiffness) - (currentVelocity * safeDamping);
            acceleration = math.select(acceleration, float3.zero, !math.all(math.isfinite(acceleration)));
            currentVelocity += acceleration * safeDt;
            currentVelocity = math.select(currentVelocity, float3.zero, !math.all(math.isfinite(currentVelocity)));
            currentPosition += currentVelocity * safeDt;
            currentPosition = math.select(currentPosition, targetPosition, !math.all(math.isfinite(currentPosition)));
        }

        public static float EvaluateExtensionResistance01(float distanceToTarget, float maxReach)
        {
            float safeMaxReach = math.max(0.0001f, maxReach);
            float threshold = safeMaxReach * 0.98f;
            float falloff = math.max(0.0001f, safeMaxReach - threshold);
            return math.saturate((distanceToTarget - threshold) / falloff);
        }

        public static float EvaluateExtensionResistanceFromDistanceSq01(float distanceToTargetSq, float maxReach)
        {
            float safeMaxReach = math.max(0.0001f, maxReach);
            float threshold = safeMaxReach * 0.98f;
            float thresholdSq = threshold * threshold;
            float maxReachSq = safeMaxReach * safeMaxReach;
            float falloffSq = math.max(0.0001f, maxReachSq - thresholdSq);
            return math.saturate((distanceToTargetSq - thresholdSq) / falloffSq);
        }

        public static float EvaluateMuscleTension(float3 restPosition, float3 targetPosition, float maxReach)
        {
            float safeMaxReach = math.max(0.0001f, maxReach);
            float reachSq = safeMaxReach * safeMaxReach;
            float deltaSq = math.lengthsq(targetPosition - restPosition);
            return math.saturate(deltaSq / reachSq);
        }

        public static quaternion FromToRotation(float3 from, float3 to)
        {
            float3 fromDir = SafeNormalize(from, new float3(0.0f, 1.0f, 0.0f));
            float3 toDir = SafeNormalize(to, fromDir);
            float dot = math.clamp(math.dot(fromDir, toDir), -1.0f, 1.0f);

            if (dot > 0.9999f)
                return quaternion.identity;

            if (dot < -0.9999f)
            {
                float3 axis = math.cross(fromDir, new float3(1.0f, 0.0f, 0.0f));
                if (math.lengthsq(axis) <= MinimumLengthSq)
                    axis = math.cross(fromDir, new float3(0.0f, 1.0f, 0.0f));

                axis = SafeNormalize(axis, new float3(0.0f, 0.0f, 1.0f));
                return new quaternion(axis.x, axis.y, axis.z, 0.0f);
            }

            float3 cross = math.cross(fromDir, toDir);
            quaternion rotation = new quaternion(cross.x, cross.y, cross.z, 1.0f + dot);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        public static quaternion AlignEndEffectorToNormal(quaternion currentWorldRotation, float3 targetNormal)
        {
            float3 safeNormal = SafeNormalize(targetNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentUp = math.mul(currentWorldRotation, new float3(0.0f, 1.0f, 0.0f));
            quaternion normalDelta = FromToRotation(currentUp, safeNormal);
            return NormalizeQuaternionNoSqrt(math.mul(normalDelta, currentWorldRotation));
        }

        public static void SolveTwoBone(
            float3 rootPosition,
            float3 middlePosition,
            float3 endPosition,
            quaternion currentUpperWorldRotation,
            quaternion currentLowerWorldRotation,
            float upperLength,
            float lowerLength,
            float3 targetPosition,
            float3 polePosition,
            float reachSafetyMargin,
            out quaternion upperWorldRotation,
            out quaternion lowerWorldRotation,
            out float3 solvedEndPosition)
        {
            float3 toTarget = targetPosition - rootPosition;
            float targetDistanceSq = math.lengthsq(toTarget);
            if (targetDistanceSq <= MinimumLengthSq)
            {
                upperWorldRotation = currentUpperWorldRotation;
                lowerWorldRotation = currentLowerWorldRotation;
                solvedEndPosition = endPosition;
                return;
            }

            float targetDistance = math.max(MinimumDistance, ApproximateLengthNoSqrt(toTarget));
            float inverseTargetDistance = math.rcp(targetDistance);
            float minReach = math.abs(upperLength - lowerLength) + 0.001f;
            float safeReachMargin = math.max(0.02f, reachSafetyMargin);
            float maxReach = math.max(minReach + 0.001f, upperLength + lowerLength - safeReachMargin);
            float clampedDistance = math.clamp(targetDistance, minReach, maxReach);

            float3 targetDirection = toTarget * inverseTargetDistance;
            float3 fallbackBend = SafeNormalize(math.mul(currentUpperWorldRotation, new float3(1.0f, 0.0f, 0.0f)), new float3(1.0f, 0.0f, 0.0f));
            float3 poleVector = polePosition - rootPosition;
            float3 projectedPole = poleVector - (targetDirection * math.dot(poleVector, targetDirection));
            float3 bendDirection = SafeNormalize(projectedPole, fallbackBend);

            float upperDenominator = math.max(2.0f * upperLength * clampedDistance, MinimumDistance);
            float upperCos = ((upperLength * upperLength) + (clampedDistance * clampedDistance) - (lowerLength * lowerLength)) / upperDenominator;
            upperCos = math.clamp(upperCos, -1.0f, 1.0f);
            upperCos = math.select(upperCos, 1.0f, !math.isfinite(upperCos));

            float bendCos = upperCos;
            float bendSinSq = math.saturate(1.0f - (bendCos * bendCos));
            float bendSin = bendSinSq;
            bendSin = math.select(bendSin, 0.0f, bendSinSq <= MinimumLengthSq || !math.isfinite(bendSin));

            float3 desiredUpperDirection = SafeNormalize((targetDirection * bendCos) + (bendDirection * bendSin), targetDirection);
            float3 solvedMiddlePosition = rootPosition + (desiredUpperDirection * upperLength);

            float3 desiredTargetPosition = rootPosition + (targetDirection * clampedDistance);
            float3 desiredLowerDirection = SafeNormalize(desiredTargetPosition - solvedMiddlePosition, targetDirection);
            float3 currentUpperDirection = SafeNormalize(middlePosition - rootPosition, targetDirection);
            float3 currentLowerDirection = SafeNormalize(endPosition - middlePosition, targetDirection);

            quaternion upperDelta = FromToRotation(currentUpperDirection, desiredUpperDirection);
            quaternion lowerDelta = FromToRotation(currentLowerDirection, desiredLowerDirection);

            upperWorldRotation = NormalizeQuaternionNoSqrt(math.mul(upperDelta, currentUpperWorldRotation));
            lowerWorldRotation = NormalizeQuaternionNoSqrt(math.mul(lowerDelta, currentLowerWorldRotation));
            solvedEndPosition = solvedMiddlePosition + (desiredLowerDirection * lowerLength);
        }

        private static quaternion NormalizeQuaternionNoSqrt(quaternion value)
        {
            float4 v = value.value;
            float lenSq = math.max(math.dot(v, v), MinimumLengthSq);
            v *= math.rcp(math.max(0.0001f, 0.5f + (lenSq * 0.5f)));
            return new quaternion(v);
        }

        public static void SolveFabrik(
            NativeArray<float3> scratchPositions,
            int scratchStartIndex,
            NativeArray<float> segmentLengths,
            int lengthStartIndex,
            int boneCount,
            float3 targetPosition,
            int iterations,
            float tolerance,
            float3 polePosition)
        {
            if (boneCount < 2)
                return;

            int lastPointIndex = scratchStartIndex + boneCount - 1;
            float3 rootPosition = scratchPositions[scratchStartIndex];
            float totalLength = 0.0f;
            for (int i = 0; i < boneCount - 1; i++)
                totalLength += segmentLengths[lengthStartIndex + i];

            float3 rootToTarget = targetPosition - rootPosition;
            float reachLimit = totalLength - 0.0001f;
            if (math.lengthsq(rootToTarget) >= reachLimit * reachLimit)
            {
                float3 reachDirection = SafeNormalize(rootToTarget, new float3(0.0f, 0.0f, 1.0f));
                scratchPositions[scratchStartIndex] = rootPosition;
                for (int i = 1; i < boneCount; i++)
                {
                    float segmentLength = segmentLengths[lengthStartIndex + i - 1];
                    scratchPositions[scratchStartIndex + i] = scratchPositions[scratchStartIndex + i - 1] + (reachDirection * segmentLength);
                }

                return;
            }

            float safeTolerance = math.max(0.0001f, tolerance);
            float safeToleranceSq = safeTolerance * safeTolerance;
            int toleranceIterations = safeTolerance <= 0.0025f ? 5 : (safeTolerance <= 0.01f ? 4 : 3);
            int maxIterations = math.clamp(math.max(iterations, toleranceIterations), 3, 5);

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                scratchPositions[lastPointIndex] = targetPosition;
                for (int i = boneCount - 2; i >= 0; i--)
                {
                    int currentIndex = scratchStartIndex + i;
                    int nextIndex = currentIndex + 1;
                    float segmentLength = segmentLengths[lengthStartIndex + i];
                    float3 direction = SafeNormalize(scratchPositions[currentIndex] - scratchPositions[nextIndex], new float3(0.0f, 0.0f, 1.0f));
                    scratchPositions[currentIndex] = scratchPositions[nextIndex] + (direction * segmentLength);
                }

                scratchPositions[scratchStartIndex] = rootPosition;
                for (int i = 1; i < boneCount; i++)
                {
                    int previousIndex = scratchStartIndex + i - 1;
                    int currentIndex = previousIndex + 1;
                    float segmentLength = segmentLengths[lengthStartIndex + i - 1];
                    float3 direction = SafeNormalize(scratchPositions[currentIndex] - scratchPositions[previousIndex], new float3(0.0f, 0.0f, 1.0f));
                    scratchPositions[currentIndex] = scratchPositions[previousIndex] + (direction * segmentLength);
                }

                if (math.lengthsq(scratchPositions[lastPointIndex] - targetPosition) <= safeToleranceSq)
                    break;
            }

            if (boneCount < 3)
                return;

            float3 axis = SafeNormalize(scratchPositions[lastPointIndex] - rootPosition, new float3(0.0f, 1.0f, 0.0f));
            float3 poleOffset = polePosition - rootPosition;
            float3 poleVector = poleOffset - (axis * math.dot(poleOffset, axis));
            if (math.lengthsq(poleVector) <= MinimumLengthSq)
                return;

            float3 poleDirection = SafeNormalize(poleVector, new float3(0.0f, 1.0f, 0.0f));
            for (int i = 1; i < boneCount - 1; i++)
            {
                int currentIndex = scratchStartIndex + i;
                float3 jointOffset = scratchPositions[currentIndex] - rootPosition;
                float3 projectedJoint = jointOffset - (axis * math.dot(jointOffset, axis));
                if (math.lengthsq(projectedJoint) <= MinimumLengthSq)
                    continue;

                quaternion poleRotation = FromToRotation(projectedJoint, poleDirection);
                scratchPositions[currentIndex] = rootPosition + math.rotate(poleRotation, jointOffset);
            }

            scratchPositions[scratchStartIndex] = rootPosition;
            for (int i = 1; i < boneCount; i++)
            {
                int previousIndex = scratchStartIndex + i - 1;
                int currentIndex = previousIndex + 1;
                float segmentLength = segmentLengths[lengthStartIndex + i - 1];
                float3 direction = SafeNormalize(scratchPositions[currentIndex] - scratchPositions[previousIndex], new float3(0.0f, 0.0f, 1.0f));
                scratchPositions[currentIndex] = scratchPositions[previousIndex] + (direction * segmentLength);
            }
        }

        public static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        public static quaternion ToMathematicsQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        public static Vector3 ToUnityVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        public static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float ApproximateExpNegPositive(float value)
        {
            float x = math.min(value, 24.0f);
            float x2 = x * x;
            float x3 = x2 * x;
            return 1.0f / (1.0f + x + (0.48f * x2) + (0.235f * x3));
        }

        private static float ApproximateLengthNoSqrt(float3 value)
        {
            float3 absolute = math.abs(value);
            float max = math.cmax(absolute);
            float min = math.cmin(absolute);
            float mid = absolute.x + absolute.y + absolute.z - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }
    }
}
