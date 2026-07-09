using Unity.Collections;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ContextualPhysicalIkMath
    {
        public static float3 ToFloat3(Vector3 value) => (float3)value;
        public static float3 ToFloat3(double3 value) => (float3)value;
        public static float3 ToFloat3(float3 value) => value;

        private const float MinimumLengthSq = 0.00000001f;
        private const float MinimumDistance = 0.0001f;

        public static float SmoothAlpha(float sharpness, float deltaTime)
        {
            float safeSharpness = SanitizeNonNegative(sharpness);
            float safeDeltaTime = SanitizeNonNegative(deltaTime);
            return SanitizeUnit(1.0f - ApproximateExpNegPositive(safeSharpness * safeDeltaTime));
        }

        public static float SmoothScalar(float current, float target, float sharpness, float deltaTime)
        {
            float safeCurrent = math.select(current, 0.0f, !math.isfinite(current));
            float safeTarget = math.select(target, 0.0f, !math.isfinite(target));
            float value = math.lerp(safeCurrent, safeTarget, SmoothAlpha(sharpness, deltaTime));
            return math.select(value, safeTarget, !math.isfinite(value));
        }

        public static float3 SmoothVector(float3 current, float3 target, float sharpness, float deltaTime)
        {
            float3 safeCurrent = SanitizeFloat3(current, float3.zero);
            float3 safeTarget = SanitizeFloat3(target, safeCurrent);
            float3 value = math.lerp(safeCurrent, safeTarget, SmoothAlpha(sharpness, deltaTime));
            return SanitizeFloat3(value, safeTarget);
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
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumLengthSq)
            {
                return (math.any(math.isnan(fallback)) || !math.all(math.isfinite(fallback)))
                    ? new float3(0.0f, 0.0f, 1.0f)
                    : fallback;
            }

            return value * math.rsqrt(math.max(lengthSq, MinimumLengthSq));
        }

        public static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float3 safeP1 = SanitizeFloat3(p1, float3.zero);
            p0 = SanitizeFloat3(p0, safeP1);
            p1 = safeP1;
            p2 = SanitizeFloat3(p2, safeP1);
            p3 = SanitizeFloat3(p3, p2);
            float safeT = SanitizeUnit(t);
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
            float3 safeFallback = SanitizeFloat3(fallback, new float3(0.0f, 0.0f, 1.0f));
            float3 safeP1 = SanitizeFloat3(p1, float3.zero);
            p0 = SanitizeFloat3(p0, safeP1);
            p1 = safeP1;
            p2 = SanitizeFloat3(p2, safeP1);
            p3 = SanitizeFloat3(p3, p2);
            float safeT = SanitizeUnit(t);
            float t2 = safeT * safeT;
            float3 tangent = 0.5f * (
                (-p0 + p2) +
                (2.0f * ((2.0f * p0) - (5.0f * p1) + (4.0f * p2) - p3) * safeT) +
                (3.0f * ((-p0) + (3.0f * p1) - (3.0f * p2) + p3) * t2));

            tangent = math.select(tangent, safeFallback, !math.all(math.isfinite(tangent)));
            return SafeNormalize(tangent, safeFallback);
        }

        public static float3 EvaluateSpinePosition(
            float3 rootPosition,
            float3 chestTarget,
            float3 headTarget,
            float3 headForward,
            float normalizedT)
        {
            rootPosition = SanitizeFloat3(rootPosition, float3.zero);
            chestTarget = SanitizeFloat3(chestTarget, rootPosition + new float3(0.0f, 0.25f, 0.1f));
            headTarget = SanitizeFloat3(headTarget, chestTarget + new float3(0.0f, 0.25f, 0.1f));
            headForward = SanitizeFloat3(headForward, new float3(0.0f, 0.0f, 1.0f));
            float3 rootToChest = chestTarget - rootPosition;
            float rootToChestLength = math.max(0.1f, ApproximateLengthNoSqrt(rootToChest));
            float3 rootForward = SafeNormalize(rootToChest, new float3(0.0f, 0.0f, 1.0f));
            float3 safeHeadForward = SafeNormalize(headForward, rootForward);
            float3 beforeRoot = rootPosition - (rootForward * (rootToChestLength * 0.25f));
            float3 afterHead = headTarget + (safeHeadForward * (rootToChestLength * 0.2f));
            float clampedT = SanitizeUnit(normalizedT);

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
            fallback = SanitizeFloat3(fallback, new float3(0.0f, 0.0f, 1.0f));
            rootPosition = SanitizeFloat3(rootPosition, float3.zero);
            chestTarget = SanitizeFloat3(chestTarget, rootPosition + fallback * 0.25f);
            headTarget = SanitizeFloat3(headTarget, chestTarget + fallback * 0.25f);
            headForward = SanitizeFloat3(headForward, fallback);
            float3 rootToChest = chestTarget - rootPosition;
            float rootToChestLength = math.max(0.1f, ApproximateLengthNoSqrt(rootToChest));
            float3 rootForward = SafeNormalize(rootToChest, fallback);
            float3 safeHeadForward = SafeNormalize(headForward, rootForward);
            float3 beforeRoot = rootPosition - (rootForward * (rootToChestLength * 0.25f));
            float3 afterHead = headTarget + (safeHeadForward * (rootToChestLength * 0.2f));
            float clampedT = SanitizeUnit(normalizedT);

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

            float safeDt = math.max(0.0001f, SanitizeNonNegative(deltaTime));
            float safeStiffness = SanitizeNonNegative(stiffness);
            float safeDamping = SanitizeNonNegative(damping);
            float3 acceleration = ((targetPosition - currentPosition) * safeStiffness) - (currentVelocity * safeDamping);
            acceleration = math.select(acceleration, float3.zero, !math.all(math.isfinite(acceleration)));
            currentVelocity += acceleration * safeDt;
            currentVelocity = math.select(currentVelocity, float3.zero, !math.all(math.isfinite(currentVelocity)));
            currentPosition += currentVelocity * safeDt;
            currentPosition = math.select(currentPosition, targetPosition, !math.all(math.isfinite(currentPosition)));
        }

        public static float EvaluateExtensionResistance01(float distanceToTarget, float maxReach)
        {
            float safeDistanceToTarget = SanitizeNonNegative(distanceToTarget);
            float safeMaxReach = math.max(0.0001f, SanitizeNonNegative(maxReach));
            float threshold = safeMaxReach * 0.98f;
            float falloff = math.max(0.0001f, safeMaxReach - threshold);
            return SanitizeUnit((safeDistanceToTarget - threshold) * math.rcp(falloff));
        }

        public static float EvaluateExtensionResistanceFromDistanceSq01(float distanceToTargetSq, float maxReach)
        {
            float safeDistanceToTargetSq = SanitizeNonNegative(distanceToTargetSq);
            float safeMaxReach = math.max(0.0001f, SanitizeNonNegative(maxReach));
            float threshold = safeMaxReach * 0.98f;
            float thresholdSq = threshold * threshold;
            float maxReachSq = safeMaxReach * safeMaxReach;
            float falloffSq = math.max(0.0001f, maxReachSq - thresholdSq);
            return SanitizeUnit((safeDistanceToTargetSq - thresholdSq) * math.rcp(falloffSq));
        }

        public static float EvaluateMuscleTension(float3 restPosition, float3 targetPosition, float maxReach)
        {
            restPosition = SanitizeFloat3(restPosition, float3.zero);
            targetPosition = SanitizeFloat3(targetPosition, restPosition);
            float safeMaxReach = math.max(0.0001f, SanitizeNonNegative(maxReach));
            float reachSq = safeMaxReach * safeMaxReach;
            float deltaSq = math.lengthsq(targetPosition - restPosition);
            return SanitizeUnit(deltaSq * math.rcp(reachSq));
        }

        public static quaternion FastDirectionDeltaNoTrig(float3 from, float3 to)
        {
            float3 fromDir = SafeNormalize(from, new float3(0.0f, 1.0f, 0.0f));
            float3 toDir = SafeNormalize(to, fromDir);
            float dot = math.clamp(math.dot(fromDir, toDir), -1.0f, 1.0f);

            if (dot > 0.9999f)
                return quaternion.identity;

            if (dot < -0.9999f)
            {
                float3 axis = ResolvePerpendicularAxis(fromDir);
                return new quaternion(axis.x, axis.y, axis.z, 0.0f);
            }

            float3 cross = math.cross(fromDir, toDir);
            quaternion rotation = new quaternion(cross.x, cross.y, cross.z, 1.0f + dot);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        public static quaternion AlignEndEffectorToNormal(quaternion currentWorldRotation, float3 targetNormal)
        {
            currentWorldRotation = NormalizeQuaternionNoSqrt(currentWorldRotation);
            float3 safeNormal = SafeNormalize(targetNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentUp = math.mul(currentWorldRotation, new float3(0.0f, 1.0f, 0.0f));
            quaternion normalDelta = FastDirectionDeltaNoTrig(currentUp, safeNormal);
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
            rootPosition = SanitizeFloat3(rootPosition, float3.zero);
            middlePosition = SanitizeFloat3(middlePosition, rootPosition + new float3(0.0f, -0.35f, 0.0f));
            endPosition = SanitizeFloat3(endPosition, middlePosition + new float3(0.0f, -0.35f, 0.0f));
            targetPosition = SanitizeFloat3(targetPosition, endPosition);
            polePosition = SanitizeFloat3(polePosition, rootPosition + new float3(0.0f, 0.0f, 1.0f));
            currentUpperWorldRotation = NormalizeQuaternionNoSqrt(currentUpperWorldRotation);
            currentLowerWorldRotation = NormalizeQuaternionNoSqrt(currentLowerWorldRotation);
            upperLength = math.max(MinimumDistance, SanitizeNonNegative(upperLength));
            lowerLength = math.max(MinimumDistance, SanitizeNonNegative(lowerLength));
            reachSafetyMargin = SanitizeNonNegative(reachSafetyMargin);

            float3 toTarget = targetPosition - rootPosition;
            float targetDistanceSq = math.lengthsq(toTarget);
            if (!math.isfinite(targetDistanceSq) || targetDistanceSq <= MinimumLengthSq)
            {
                upperWorldRotation = currentUpperWorldRotation;
                lowerWorldRotation = currentLowerWorldRotation;
                solvedEndPosition = endPosition;
                return;
            }

            float targetDistance = math.max(
                MinimumDistance,
                targetDistanceSq * math.rsqrt(math.max(targetDistanceSq, MinimumLengthSq)));
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
            float upperCos = ((upperLength * upperLength) + (clampedDistance * clampedDistance) - (lowerLength * lowerLength)) *
                math.rcp(upperDenominator);
            upperCos = math.clamp(upperCos, -1.0f, 1.0f);
            upperCos = math.select(upperCos, 1.0f, !math.isfinite(upperCos));

            float bendCos = upperCos;
            float bendSinSq = SanitizeUnit(1.0f - (bendCos * bendCos));
            float bendSin = bendSinSq * math.rsqrt(math.max(bendSinSq, MinimumLengthSq));
            bendSin = math.select(bendSin, 0.0f, bendSinSq <= MinimumLengthSq || !math.isfinite(bendSin));

            float3 desiredUpperDirection = SafeNormalize((targetDirection * bendCos) + (bendDirection * bendSin), targetDirection);
            float3 solvedMiddlePosition = rootPosition + (desiredUpperDirection * upperLength);

            float3 desiredTargetPosition = rootPosition + (targetDirection * clampedDistance);
            float3 desiredLowerDirection = SafeNormalize(desiredTargetPosition - solvedMiddlePosition, targetDirection);
            float3 currentUpperDirection = SafeNormalize(middlePosition - rootPosition, targetDirection);
            float3 currentLowerDirection = SafeNormalize(endPosition - middlePosition, targetDirection);

            quaternion upperDelta = FastDirectionDeltaNoTrig(currentUpperDirection, desiredUpperDirection);
            quaternion lowerDelta = FastDirectionDeltaNoTrig(currentLowerDirection, desiredLowerDirection);

            upperWorldRotation = NormalizeQuaternionNoSqrt(math.mul(upperDelta, currentUpperWorldRotation));
            lowerWorldRotation = NormalizeQuaternionNoSqrt(math.mul(lowerDelta, currentLowerWorldRotation));
            solvedEndPosition = solvedMiddlePosition + (desiredLowerDirection * lowerLength);
        }

        private static quaternion NormalizeQuaternionNoSqrt(quaternion value)
        {
            if (!math.all(math.isfinite(value.value)))
                return quaternion.identity;

            float4 v = value.value;
            float rawLenSq = math.dot(v, v);
            if (!math.isfinite(rawLenSq) || rawLenSq <= MinimumLengthSq)
                return quaternion.identity;

            float lenSq = math.max(rawLenSq, MinimumLengthSq);
            v *= math.rsqrt(lenSq);
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
            float3 rootPosition = SanitizeFloat3(scratchPositions[scratchStartIndex], float3.zero);
            scratchPositions[scratchStartIndex] = rootPosition;
            scratchPositions[lastPointIndex] = SanitizeFloat3(scratchPositions[lastPointIndex], rootPosition);
            targetPosition = SanitizeFloat3(targetPosition, scratchPositions[lastPointIndex]);
            polePosition = SanitizeFloat3(polePosition, rootPosition + new float3(0.0f, 1.0f, 0.0f));

            float totalLength = 0.0f;
            for (int i = 0; i < boneCount - 1; i++)
                totalLength += SanitizeNonNegative(segmentLengths[lengthStartIndex + i]);

            float3 rootToTarget = targetPosition - rootPosition;
            float reachLimit = math.max(0.0001f, totalLength - 0.0001f);
            if (math.lengthsq(rootToTarget) >= reachLimit * reachLimit)
            {
                float3 reachDirection = SafeNormalize(rootToTarget, new float3(0.0f, 0.0f, 1.0f));
                scratchPositions[scratchStartIndex] = rootPosition;
                for (int i = 1; i < boneCount; i++)
                {
                    float segmentLength = SanitizeNonNegative(segmentLengths[lengthStartIndex + i - 1]);
                    scratchPositions[scratchStartIndex + i] = scratchPositions[scratchStartIndex + i - 1] + (reachDirection * segmentLength);
                }

                return;
            }

            float safeTolerance = math.max(0.0001f, SanitizeNonNegative(tolerance));
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
                    float segmentLength = SanitizeNonNegative(segmentLengths[lengthStartIndex + i]);
                    float3 direction = SafeNormalize(scratchPositions[currentIndex] - scratchPositions[nextIndex], new float3(0.0f, 0.0f, 1.0f));
                    scratchPositions[currentIndex] = scratchPositions[nextIndex] + (direction * segmentLength);
                }

                scratchPositions[scratchStartIndex] = rootPosition;
                for (int i = 1; i < boneCount; i++)
                {
                    int previousIndex = scratchStartIndex + i - 1;
                    int currentIndex = previousIndex + 1;
                    float segmentLength = SanitizeNonNegative(segmentLengths[lengthStartIndex + i - 1]);
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

            float poleLengthSq = math.lengthsq(poleVector);
            float3 poleDirection = poleVector * math.rsqrt(math.max(poleLengthSq, MinimumLengthSq));
            for (int i = 1; i < boneCount - 1; i++)
            {
                int currentIndex = scratchStartIndex + i;
                float3 jointOffset = scratchPositions[currentIndex] - rootPosition;
                float jointAxisOffset = math.dot(jointOffset, axis);
                float3 projectedJoint = jointOffset - (axis * math.dot(jointOffset, axis));
                float projectedLengthSq = math.lengthsq(projectedJoint);
                if (projectedLengthSq <= MinimumLengthSq)
                    continue;

                float projectedRadius = projectedLengthSq * math.rsqrt(math.max(projectedLengthSq, MinimumLengthSq));
                scratchPositions[currentIndex] = rootPosition + (axis * jointAxisOffset) + (poleDirection * projectedRadius);
            }

            scratchPositions[scratchStartIndex] = rootPosition;
            for (int i = 1; i < boneCount; i++)
            {
                int previousIndex = scratchStartIndex + i - 1;
                int currentIndex = previousIndex + 1;
                float segmentLength = SanitizeNonNegative(segmentLengths[lengthStartIndex + i - 1]);
                float3 direction = SafeNormalize(scratchPositions[currentIndex] - scratchPositions[previousIndex], new float3(0.0f, 0.0f, 1.0f));
                scratchPositions[currentIndex] = scratchPositions[previousIndex] + (direction * segmentLength);
            }
        }

        public static Quaternion ToUnityQuaternion(quaternion value)
        {
            value = NormalizeQuaternionNoSqrt(value);
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        public static quaternion ToMathematicsQuaternion(Quaternion value)
        {
            quaternion rotation = new quaternion(value.x, value.y, value.z, value.w);
            if (!math.all(math.isfinite(rotation.value)))
                return rotation;

            float lengthSq = math.dot(rotation.value, rotation.value);
            return math.isfinite(lengthSq) && lengthSq > MinimumLengthSq
                ? NormalizeQuaternionNoSqrt(rotation)
                : rotation;
        }

        public static Vector3 ToUnityVector3(float3 value)
        {
            value = SanitizeFloat3(value, float3.zero);
            return new Vector3(value.x, value.y, value.z);
        }

        private static float ApproximateExpNegPositive(float value)
        {
            float x = math.min(SanitizeNonNegative(value), 24.0f);
            float x2 = x * x;
            float x3 = x2 * x;
            return math.rcp(1.0f + x + (0.48f * x2) + (0.235f * x3));
        }

        private static float ApproximateLengthNoSqrt(float3 value)
        {
            value = SanitizeFloat3(value, float3.zero);
            float3 absolute = math.abs(value);
            float max = math.cmax(absolute);
            float min = math.cmin(absolute);
            float mid = absolute.x + absolute.y + absolute.z - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private static float SanitizeUnit(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private static float3 ResolvePerpendicularAxis(float3 direction)
        {
            float3 absolute = math.abs(direction);
            float3 basis = absolute.x <= absolute.y && absolute.x <= absolute.z
                ? new float3(1.0f, 0.0f, 0.0f)
                : absolute.y <= absolute.z
                    ? new float3(0.0f, 1.0f, 0.0f)
                    : new float3(0.0f, 0.0f, 1.0f);

            return SafeNormalize(math.cross(direction, basis), new float3(0.0f, 0.0f, 1.0f));
        }
    }
}
