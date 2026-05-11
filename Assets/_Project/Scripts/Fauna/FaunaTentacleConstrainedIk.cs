using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FaunaTentacleConstrainedIkChain
    {
        public const uint TipAnchoredMask = 1u << 0;

        [FieldOffset(0)] public int FirstJointIndex;
        [FieldOffset(4)] public int TargetAupIndex;
        [FieldOffset(8)] public float SegmentLength;
        [FieldOffset(12)] public float Weight;
        [FieldOffset(16)] public float BendSign;
        [FieldOffset(20)] public float CurveOffsetMeters;
        [FieldOffset(24)] public uint StateMask;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FaunaTentacleJointPose
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Phase;
        [FieldOffset(16)] public float3 Forward;
        [FieldOffset(28)] public uint StateMask;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct FaunaTentacleConstrainedIkJob : IJobParallelFor
    {
        private const float MinLengthSq = 0.000001f;
        private const float InvThree = 0.33333334f;

        [ReadOnly] public NativeArray<FaunaTentacleConstrainedIkChain> Chains;
        [ReadOnly] public NativeArray<AbsoluteUniversePosition> TipTargetsAup;
        public AbsoluteUniversePosition ReferenceAup;
        public NativeArray<FaunaTentacleJointPose> JointPoses;

        public void Execute(int index)
        {
            FaunaTentacleConstrainedIkChain chain = Chains[index];
            int jointIndex = chain.FirstJointIndex;
            if (jointIndex < 0 || jointIndex + 3 >= JointPoses.Length || chain.TargetAupIndex < 0 || chain.TargetAupIndex >= TipTargetsAup.Length)
                return;

            FaunaTentacleJointPose rootPose = JointPoses[jointIndex];
            FaunaTentacleJointPose joint1 = JointPoses[jointIndex + 1];
            FaunaTentacleJointPose joint2 = JointPoses[jointIndex + 2];
            FaunaTentacleJointPose tipPose = JointPoses[jointIndex + 3];

            AbsoluteUniversePosition targetAup = TipTargetsAup[chain.TargetAupIndex];
            float3 targetPosition = AbsoluteUniversePosition.ToCameraRelativeFloat3(in targetAup, in ReferenceAup);
            float3 rootPosition = rootPose.Position;
            float3 toTarget = targetPosition - rootPosition;
            float targetDistanceSq = math.lengthsq(toTarget);
            float3 direction = toTarget * math.rsqrt(math.max(targetDistanceSq, MinLengthSq));
            float segmentLength = ResolveSegmentLength(chain.SegmentLength, rootPose.Position, tipPose.Position);
            float reach = math.min(targetDistanceSq * math.rsqrt(math.max(targetDistanceSq, MinLengthSq)), segmentLength * 3f);
            float curve = math.max(0f, chain.CurveOffsetMeters) * math.select(1f, -1f, chain.BendSign < 0f);
            float3 side = ResolveDominantSide(direction);
            float3 solvedTip = rootPosition + (direction * reach);
            bool tipAnchored = (chain.StateMask & FaunaTentacleConstrainedIkChain.TipAnchoredMask) != 0u;
            solvedTip = math.select(solvedTip, targetPosition, tipAnchored);

            float3 step = (solvedTip - rootPosition) * InvThree;
            float3 solved1 = rootPosition + step + (side * curve);
            float3 solved2 = rootPosition + (step * 2f) - (side * curve);
            float weight = math.saturate(chain.Weight);

            joint1.Position = math.lerp(joint1.Position, solved1, weight);
            joint2.Position = math.lerp(joint2.Position, solved2, weight);
            tipPose.Position = math.lerp(tipPose.Position, solvedTip, weight);
            joint1.Forward = ContextualPhysicalIkMath.SafeNormalize(joint2.Position - joint1.Position, direction);
            joint2.Forward = ContextualPhysicalIkMath.SafeNormalize(tipPose.Position - joint2.Position, direction);
            tipPose.Forward = direction;

            JointPoses[jointIndex + 1] = joint1;
            JointPoses[jointIndex + 2] = joint2;
            JointPoses[jointIndex + 3] = tipPose;
        }

        private static float ResolveSegmentLength(float authoredLength, float3 rootPosition, float3 tipPosition)
        {
            if (authoredLength > 0.0001f)
                return authoredLength;

            float3 span = tipPosition - rootPosition;
            float spanSq = math.lengthsq(span);
            return math.max(0.0001f, (spanSq * math.rsqrt(math.max(spanSq, MinLengthSq))) * InvThree);
        }

        private static float3 ResolveDominantSide(float3 direction)
        {
            float3 absolute = math.abs(direction);
            if (absolute.y <= absolute.x && absolute.y <= absolute.z)
                return new float3(0f, math.select(1f, -1f, direction.y < 0f), 0f);

            if (absolute.x <= absolute.z)
                return new float3(math.select(1f, -1f, direction.x < 0f), 0f, 0f);

            return new float3(0f, 0f, math.select(1f, -1f, direction.z < 0f));
        }
    }
}
