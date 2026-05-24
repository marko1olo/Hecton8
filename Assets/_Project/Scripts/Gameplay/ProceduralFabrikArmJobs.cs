using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Blittable chain descriptor for caller-owned FABRIK arm buffers.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ProceduralFabrikArmChain
    {
        public int PositionStart;
        public int SegmentLengthStart;
        public int RotationStart;
        public int JointCount;
        public int IterationCount;
        public float ToleranceMeters;
        public float Padding0;
        public float Padding1;

        public ProceduralFabrikArmChain(
            int positionStart,
            int segmentLengthStart,
            int rotationStart,
            int jointCount,
            int iterationCount,
            float toleranceMeters)
        {
            PositionStart = positionStart;
            SegmentLengthStart = segmentLengthStart;
            RotationStart = rotationStart;
            JointCount = jointCount;
            IterationCount = iterationCount;
            ToleranceMeters = toleranceMeters;
            Padding0 = 0f;
            Padding1 = 0f;
        }
    }

    /// <summary>
    /// Blittable target packet consumed by the Burst FABRIK solve.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ProceduralFabrikArmTarget
    {
        public float3 TargetPositionWS;
        public float3 PoleNormalWS;
        public quaternion WristRotationWS;
        public float PositionWeight01;
        public float Padding0;

        public ProceduralFabrikArmTarget(
            float3 targetPositionWS,
            float3 poleNormalWS,
            quaternion wristRotationWS,
            float positionWeight01)
        {
            TargetPositionWS = targetPositionWS;
            PoleNormalWS = poleNormalWS;
            WristRotationWS = wristRotationWS;
            PositionWeight01 = positionWeight01;
            Padding0 = 0f;
        }
    }

    /// <summary>
    /// Alloc-free FABRIK arm solver. The owner allocates all NativeArray buffers once and schedules this job.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ProceduralFabrikArmSolveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProceduralFabrikArmChain> Chains;
        [ReadOnly] public NativeArray<ProceduralFabrikArmTarget> Targets;
        [ReadOnly] public NativeArray<float> SegmentLengths;

        // NATIVE SAFETY EXCEPTION: each chain owns a disjoint slice of JointPositions.
        // The dispatcher schedules one job index per chain, so writes are deterministic when callers do not overlap slices.
        [NativeDisableParallelForRestriction] public NativeArray<float3> JointPositionsWS;

        // NATIVE SAFETY EXCEPTION: each chain owns a disjoint rotation slice matching its position slice.
        [NativeDisableParallelForRestriction] public NativeArray<quaternion> JointRotationsWS;

        public void Execute(int index)
        {
            if (index < 0 || index >= Chains.Length || index >= Targets.Length)
                return;

            ProceduralFabrikArmChain chain = Chains[index];
            int jointCount = chain.JointCount;
            if (jointCount < 2)
                return;

            int positionStart = chain.PositionStart;
            int segmentStart = chain.SegmentLengthStart;
            int segmentCount = jointCount - 1;
            if (positionStart < 0 ||
                segmentStart < 0 ||
                positionStart + jointCount > JointPositionsWS.Length ||
                segmentStart + segmentCount > SegmentLengths.Length)
            {
                return;
            }

            ProceduralFabrikArmTarget target = Targets[index];
            float positionWeight = math.saturate(target.PositionWeight01);
            if (positionWeight <= 0.0001f)
            {
                WriteRotations(chain, target, segmentCount);
                return;
            }

            float3 rootPosition = JointPositionsWS[positionStart];
            int tipIndex = positionStart + segmentCount;
            float3 currentTip = JointPositionsWS[tipIndex];
            float3 targetPosition = math.lerp(currentTip, target.TargetPositionWS, positionWeight);
            float totalLength = ResolveTotalLength(segmentStart, segmentCount);
            float3 rootToTarget = targetPosition - rootPosition;
            float rootToTargetSq = math.lengthsq(rootToTarget);

            if (rootToTargetSq >= totalLength * totalLength)
            {
                StretchToUnreachableTarget(positionStart, segmentStart, segmentCount, rootPosition, rootToTarget);
            }
            else
            {
                SolveReachableTarget(positionStart, segmentStart, segmentCount, rootPosition, targetPosition, chain);
                ApplyArmPole(positionStart, segmentCount, target);
            }

            WriteRotations(chain, target, segmentCount);
        }

        private float ResolveTotalLength(int segmentStart, int segmentCount)
        {
            float totalLength = 0f;
            for (int i = 0; i < segmentCount; i++)
                totalLength += math.max(0.0001f, SegmentLengths[segmentStart + i]);

            return totalLength;
        }

        private void StretchToUnreachableTarget(
            int positionStart,
            int segmentStart,
            int segmentCount,
            float3 rootPosition,
            float3 rootToTarget)
        {
            float3 direction = SafeNormalize(rootToTarget, new float3(0f, 0f, 1f));
            JointPositionsWS[positionStart] = rootPosition;
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = math.max(0.0001f, SegmentLengths[segmentStart + i]);
                int nextIndex = positionStart + i + 1;
                JointPositionsWS[nextIndex] = JointPositionsWS[nextIndex - 1] + direction * segmentLength;
            }
        }

        private void SolveReachableTarget(
            int positionStart,
            int segmentStart,
            int segmentCount,
            float3 rootPosition,
            float3 targetPosition,
            ProceduralFabrikArmChain chain)
        {
            int tipIndex = positionStart + segmentCount;
            int iterations = math.clamp(chain.IterationCount <= 0 ? 4 : chain.IterationCount, 1, 8);
            float toleranceSq = math.max(0.000001f, chain.ToleranceMeters * chain.ToleranceMeters);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                JointPositionsWS[tipIndex] = targetPosition;
                for (int i = segmentCount - 1; i >= 0; i--)
                {
                    int jointIndex = positionStart + i;
                    int nextIndex = jointIndex + 1;
                    float segmentLength = math.max(0.0001f, SegmentLengths[segmentStart + i]);
                    float3 direction = SafeNormalize(JointPositionsWS[jointIndex] - JointPositionsWS[nextIndex], new float3(0f, 0f, -1f));
                    JointPositionsWS[jointIndex] = JointPositionsWS[nextIndex] + direction * segmentLength;
                }

                JointPositionsWS[positionStart] = rootPosition;
                for (int i = 0; i < segmentCount; i++)
                {
                    int jointIndex = positionStart + i;
                    int nextIndex = jointIndex + 1;
                    float segmentLength = math.max(0.0001f, SegmentLengths[segmentStart + i]);
                    float3 direction = SafeNormalize(JointPositionsWS[nextIndex] - JointPositionsWS[jointIndex], new float3(0f, 0f, 1f));
                    JointPositionsWS[nextIndex] = JointPositionsWS[jointIndex] + direction * segmentLength;
                }

                if (math.lengthsq(JointPositionsWS[tipIndex] - targetPosition) <= toleranceSq)
                    break;
            }
        }

        private void ApplyArmPole(int positionStart, int segmentCount, ProceduralFabrikArmTarget target)
        {
            if (segmentCount != 2)
                return;

            int elbowIndex = positionStart + 1;
            int wristIndex = positionStart + 2;
            float3 shoulder = JointPositionsWS[positionStart];
            float3 wrist = JointPositionsWS[wristIndex];
            float3 shoulderToWrist = wrist - shoulder;
            float3 axis = SafeNormalize(shoulderToWrist, new float3(0f, 0f, 1f));
            float3 pole = ProjectPole(target.PoleNormalWS, axis);
            float3 shoulderToElbow = JointPositionsWS[elbowIndex] - shoulder;
            float alongAxis = math.dot(shoulderToElbow, axis);
            float3 radial = shoulderToElbow - axis * alongAxis;
            float radialLength = FastLengthApprox(radial);
            JointPositionsWS[elbowIndex] = shoulder + axis * alongAxis + pole * radialLength;
        }

        private void WriteRotations(
            ProceduralFabrikArmChain chain,
            ProceduralFabrikArmTarget target,
            int segmentCount)
        {
            if (!JointRotationsWS.IsCreated || chain.RotationStart < 0)
                return;

            int rotationStart = chain.RotationStart;
            if (rotationStart + segmentCount >= JointRotationsWS.Length)
                return;

            int positionStart = chain.PositionStart;
            for (int i = 0; i < segmentCount; i++)
            {
                float3 forward = JointPositionsWS[positionStart + i + 1] - JointPositionsWS[positionStart + i];
                float3 up = ProjectPole(target.PoleNormalWS, SafeNormalize(forward, new float3(0f, 0f, 1f)));
                JointRotationsWS[rotationStart + i] = quaternion.LookRotationSafe(forward, up);
            }

            JointRotationsWS[rotationStart + segmentCount] = target.WristRotationWS;
        }

        private static float3 ProjectPole(float3 poleNormal, float3 forward)
        {
            float3 safeForward = SafeNormalize(forward, new float3(0f, 0f, 1f));
            float3 projected = poleNormal - safeForward * math.dot(poleNormal, safeForward);
            return SafeNormalize(projected, new float3(0f, 1f, 0f));
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float FastLengthApprox(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq * math.rsqrt(math.max(lengthSq, 0.000001f));
        }
    }
}
