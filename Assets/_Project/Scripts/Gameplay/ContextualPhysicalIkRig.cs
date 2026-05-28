using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Caves;
using Hecton8.Interaction;
using Hecton8.World;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ContextualPhysicalIkTwoBoneSetup
    {
        [FieldOffset(0)] public int ParentHandleIndex;
        [FieldOffset(4)] public int UpperHandleIndex;
        [FieldOffset(8)] public int LowerHandleIndex;
        [FieldOffset(12)] public int EndHandleIndex;
        [FieldOffset(16)] public byte TargetChannel;
        [FieldOffset(17)] public byte Enabled;
        [FieldOffset(18)] private ushort _pad0;
        [FieldOffset(20)] public float UpperLength;
        [FieldOffset(24)] public float LowerLength;
        [FieldOffset(28)] public float BaseBlend;
        [FieldOffset(32)] public float ReachSafetyMargin;
        [FieldOffset(36)] public float3 PoleLocalOffset;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ContextualPhysicalIkAppendageChainRuntime
    {
        [FieldOffset(0)] public int ParentHandleIndex;
        [FieldOffset(4)] public int FirstBoneHandleIndex;
        [FieldOffset(8)] public int BoneCount;
        [FieldOffset(12)] public int FirstLengthIndex;
        [FieldOffset(16)] public int FirstScratchIndex;
        [FieldOffset(20)] public int TargetIndex;
        [FieldOffset(24)] public int Iterations;
        [FieldOffset(28)] public float Tolerance;
        [FieldOffset(32)] public float Blend;
        [FieldOffset(36)] public float3 PoleLocalOffset;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ContextualPhysicalIkSpineChainRuntime
    {
        [FieldOffset(0)] public int ParentHandleIndex;
        [FieldOffset(4)] public int FirstBoneHandleIndex;
        [FieldOffset(8)] public int BoneCount;
        [FieldOffset(12)] public int TargetStartIndex;
        [FieldOffset(16)] public float Blend;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ContextualPhysicalIkSecondaryChainRuntime
    {
        [FieldOffset(0)] public int ParentHandleIndex;
        [FieldOffset(4)] public int FirstBoneHandleIndex;
        [FieldOffset(8)] public int BoneCount;
        [FieldOffset(12)] public int FirstStateIndex;
        [FieldOffset(16)] public float Stiffness;
        [FieldOffset(20)] public float Damping;
        [FieldOffset(24)] public float Blend;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct ContextualPhysicalIkAppendageTarget
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Weight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ContextualPhysicalIkSecondaryState
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ContextualPhysicalIkCachedPoseState
    {
        [FieldOffset(0)] public quaternion Rotation;
        [FieldOffset(16)] public float3 Position;
        [FieldOffset(28)] public byte HasRotation;
        [FieldOffset(29)] public byte HasPosition;
        [FieldOffset(30)] private ushort _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    internal struct ContextualPhysicalIkApplyJob : IAnimationJob
    {
        public const int PelvisHandleIndex = 0;
        private const int SpineTargetCountPerChain = 3;
        private const float SpineSlopeLeanShare = 0.35f;
        private const int MaxAppendageIterations = 12;

        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly TargetFrames;
        [ReadOnly, NoAlias] public NativeArray<TransformStreamHandle> StreamHandles;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkTwoBoneSetup> TwoBoneSetups;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkAppendageChainRuntime> AppendageChains;
        [ReadOnly, NoAlias] public NativeArray<float> AppendageSegmentLengths;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkAppendageTarget> AppendageTargets;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkSpineChainRuntime> SpineChains;
        [ReadOnly, NoAlias] public NativeArray<float3> SpineTargets;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkSecondaryChainRuntime> SecondaryChains;

        [NoAlias] public NativeArray<float3> AppendageScratchPositions;
        [NoAlias] public NativeArray<ContextualPhysicalIkSecondaryState> SecondaryStates;
        [NoAlias] public NativeArray<ContextualPhysicalIkCachedPoseState> CachedLocalPoseStates;
        [NoAlias] public NativeArray<float> MuscleBulgeOutput;
        public int EntityIndex;
        public float PelvisPositionBlend;
        public float PelvisRotationBlend;
        public float OverExtensionResistanceRadians;

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (EntityIndex < 0 ||
                !TargetFrames.IsCreated ||
                EntityIndex >= TargetFrames.Length ||
                !StreamHandles.IsCreated ||
                StreamHandles.Length <= PelvisHandleIndex)
            {
                return;
            }

            ContextualPhysicalIkTargetFrame frame = TargetFrames[EntityIndex];
            if (frame.ShouldComputeThisFrame == 0)
            {
                ApplyCachedPose(stream);
                return;
            }

            if (MuscleBulgeOutput.IsCreated && MuscleBulgeOutput.Length > 0)
                MuscleBulgeOutput[0] = 0.0f;

            ProcessPelvis(stream, in frame);

            if (TwoBoneSetups.IsCreated)
            {
                for (int i = 0; i < TwoBoneSetups.Length; i++)
                    ProcessTwoBoneLimb(stream, in frame, TwoBoneSetups[i]);
            }

            if (AppendageChains.IsCreated && AppendageTargets.IsCreated)
            {
                for (int i = 0; i < AppendageChains.Length; i++)
                    ProcessAppendage(stream, AppendageChains[i]);
            }

            if (SpineChains.IsCreated && SpineTargets.IsCreated)
            {
                for (int i = 0; i < SpineChains.Length; i++)
                    ProcessSpine(stream, SpineChains[i], in frame);
            }

            if (SecondaryChains.IsCreated && SecondaryStates.IsCreated)
            {
                for (int i = 0; i < SecondaryChains.Length; i++)
                    ProcessSecondary(stream, in frame, SecondaryChains[i]);
            }
        }

        private void ProcessPelvis(AnimationStream stream, in ContextualPhysicalIkTargetFrame frame)
        {
            TransformStreamHandle pelvisHandle = StreamHandles[PelvisHandleIndex];
            Vector3 currentLocalPosition = pelvisHandle.GetLocalPosition(stream);
            float3 currentLocalPositionFloat = ContextualPhysicalIkMath.ToFloat3(currentLocalPosition);
            float3 comOffset = IsFinite(frame.ComOffsetLocal) ? frame.ComOffsetLocal : float3.zero;
            if (!IsFinite(currentLocalPositionFloat))
                return;

            float pelvisPositionBlend = SanitizeBlend(PelvisPositionBlend);
            float pelvisRotationBlend = SanitizeBlend(PelvisRotationBlend);
            float3 desiredLocalPosition = currentLocalPositionFloat + comOffset;
            if (!IsFinite(desiredLocalPosition))
                return;

            float3 blendedPosition = math.lerp(
                currentLocalPositionFloat,
                desiredLocalPosition,
                pelvisPositionBlend);
            if (!IsFinite(blendedPosition))
                return;

            Quaternion currentLocalRotation = pelvisHandle.GetLocalRotation(stream);
            quaternion currentLocalRotationQ = ContextualPhysicalIkMath.ToMathematicsQuaternion(currentLocalRotation);
            if (!IsFinite(currentLocalRotationQ))
                return;

            quaternion yawRotation = ApproximateAxisRotationNoTrig(new float3(0.0f, 1.0f, 0.0f), frame.PelvisYawRadians);
            quaternion leanRotation = ApproximateSmallEulerXzNoTrig(frame.ComLeanRadians.x, frame.ComLeanRadians.y);
            quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(currentLocalRotationQ, math.mul(yawRotation, leanRotation)));
            quaternion blendedRotation = ApproximateNlerpNoSqrt(currentLocalRotationQ, desiredLocalRotation, pelvisRotationBlend);
            if (!IsFinite(blendedRotation))
                return;

            Vector3 blendedPositionUnity = ContextualPhysicalIkMath.ToUnityVector3(blendedPosition);
            pelvisHandle.SetLocalPosition(stream, blendedPositionUnity);
            pelvisHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedRotation));
            CacheLocalPosition(PelvisHandleIndex, blendedPosition);
            CacheLocalRotation(PelvisHandleIndex, blendedRotation);
        }

        private void ProcessTwoBoneLimb(AnimationStream stream, in ContextualPhysicalIkTargetFrame frame, in ContextualPhysicalIkTwoBoneSetup setup)
        {
            if (setup.Enabled == 0)
                return;

            ContextualPhysicalIkContactTarget target = ResolveTarget(in frame, setup.TargetChannel);
            float weight = SanitizeBlend(target.Blend) * SanitizeBlend(setup.BaseBlend);
            if (weight <= 0.0001f)
                return;

            if (!IsTwoBoneSetupValid(in setup, in target))
                return;

            TransformStreamHandle parentHandle = StreamHandles[setup.ParentHandleIndex];
            TransformStreamHandle upperHandle = StreamHandles[setup.UpperHandleIndex];
            TransformStreamHandle lowerHandle = StreamHandles[setup.LowerHandleIndex];
            TransformStreamHandle endHandle = StreamHandles[setup.EndHandleIndex];

            float3 rootPosition = ContextualPhysicalIkMath.ToFloat3(upperHandle.GetPosition(stream));
            float3 middlePosition = ContextualPhysicalIkMath.ToFloat3(lowerHandle.GetPosition(stream));
            float3 endPosition = ContextualPhysicalIkMath.ToFloat3(endHandle.GetPosition(stream));
            quaternion parentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(parentHandle.GetRotation(stream));
            quaternion currentUpperWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(upperHandle.GetRotation(stream));
            quaternion currentLowerWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(lowerHandle.GetRotation(stream));
            quaternion currentEndWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(endHandle.GetRotation(stream));
            if (!IsFinite(rootPosition) ||
                !IsFinite(middlePosition) ||
                !IsFinite(endPosition) ||
                !IsFinite(parentWorldRotation) ||
                !IsFinite(currentUpperWorldRotation) ||
                !IsFinite(currentLowerWorldRotation) ||
                !IsFinite(currentEndWorldRotation))
            {
                return;
            }

            float3 polePosition = rootPosition + math.mul(parentWorldRotation, setup.PoleLocalOffset);
            if (!IsFinite(polePosition))
                return;

            ContextualPhysicalIkMath.SolveTwoBone(
                rootPosition,
                middlePosition,
                endPosition,
                currentUpperWorldRotation,
                currentLowerWorldRotation,
                setup.UpperLength,
                setup.LowerLength,
                target.WorldPosition,
                polePosition,
                setup.ReachSafetyMargin,
                out quaternion desiredUpperWorldRotation,
                out quaternion desiredLowerWorldRotation,
                out _);

            float maxReach = math.max(0.0001f, setup.UpperLength + setup.LowerLength - math.max(0.02f, setup.ReachSafetyMargin));
            float distanceToTargetSq = math.lengthsq(target.WorldPosition - rootPosition);
            if (!math.isfinite(distanceToTargetSq))
                return;

            float extensionResistance01 = ContextualPhysicalIkMath.EvaluateExtensionResistanceFromDistanceSq01(distanceToTargetSq, maxReach);
            if (extensionResistance01 > 0.0f)
            {
                float3 targetDirection = ContextualPhysicalIkMath.SafeNormalize(target.WorldPosition - rootPosition, new float3(0.0f, 0.0f, 1.0f));
                float3 poleVector = polePosition - rootPosition;
                float3 projectedPole = poleVector - (targetDirection * math.dot(poleVector, targetDirection));
                float3 bendDirection = ContextualPhysicalIkMath.SafeNormalize(projectedPole, math.mul(parentWorldRotation, new float3(1.0f, 0.0f, 0.0f)));
                float3 torqueAxis = ContextualPhysicalIkMath.SafeNormalize(math.cross(targetDirection, bendDirection), new float3(0.0f, 1.0f, 0.0f));
                float overExtensionResistance = SanitizeNonNegative(OverExtensionResistanceRadians);
                quaternion resistanceRotation = ApproximateAxisRotationNoTrig(torqueAxis, -overExtensionResistance * extensionResistance01);
                desiredUpperWorldRotation = NormalizeQuaternionNoSqrt(math.mul(resistanceRotation, desiredUpperWorldRotation));
                desiredLowerWorldRotation = NormalizeQuaternionNoSqrt(math.mul(resistanceRotation, desiredLowerWorldRotation));
            }

            quaternion desiredEndWorldRotation = ContextualPhysicalIkMath.AlignEndEffectorToNormal(currentEndWorldRotation, target.WorldNormal);

            quaternion currentUpperLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(upperHandle.GetLocalRotation(stream));
            quaternion currentLowerLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(lowerHandle.GetLocalRotation(stream));
            quaternion currentEndLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(endHandle.GetLocalRotation(stream));
            if (!IsFinite(currentUpperLocalRotation) ||
                !IsFinite(currentLowerLocalRotation) ||
                !IsFinite(currentEndLocalRotation))
            {
                return;
            }

            quaternion desiredUpperLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(parentWorldRotation), desiredUpperWorldRotation));
            quaternion desiredLowerLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(desiredUpperWorldRotation), desiredLowerWorldRotation));
            quaternion desiredEndLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(desiredLowerWorldRotation), desiredEndWorldRotation));

            quaternion blendedUpperLocalRotation = ApproximateNlerpNoSqrt(currentUpperLocalRotation, desiredUpperLocalRotation, weight);
            quaternion blendedLowerLocalRotation = ApproximateNlerpNoSqrt(currentLowerLocalRotation, desiredLowerLocalRotation, weight);
            quaternion blendedEndLocalRotation = ApproximateNlerpNoSqrt(currentEndLocalRotation, desiredEndLocalRotation, weight);
            if (!IsFinite(blendedUpperLocalRotation) ||
                !IsFinite(blendedLowerLocalRotation) ||
                !IsFinite(blendedEndLocalRotation))
            {
                return;
            }

            upperHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedUpperLocalRotation));
            lowerHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLowerLocalRotation));
            endHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedEndLocalRotation));

            CacheLocalRotation(setup.UpperHandleIndex, blendedUpperLocalRotation);
            CacheLocalRotation(setup.LowerHandleIndex, blendedLowerLocalRotation);
            CacheLocalRotation(setup.EndHandleIndex, blendedEndLocalRotation);

            float tension = math.max(
                ContextualPhysicalIkMath.EvaluateMuscleTension(endPosition, target.WorldPosition, maxReach),
                extensionResistance01);
            AccumulateMuscleBulge(tension * weight);
        }

        private void ProcessAppendage(AnimationStream stream, in ContextualPhysicalIkAppendageChainRuntime chain)
        {
            if (!IsAppendageChainValid(in chain))
                return;

            ContextualPhysicalIkAppendageTarget target = AppendageTargets[chain.TargetIndex];
            float weight = SanitizeBlend(target.Weight) * SanitizeBlend(chain.Blend);
            if (weight <= 0.0001f || !IsFinite(target.Position) || !IsFinite(chain.PoleLocalOffset))
                return;

            for (int i = 0; i < chain.BoneCount; i++)
            {
                float3 bonePosition = ContextualPhysicalIkMath.ToFloat3(
                    StreamHandles[chain.FirstBoneHandleIndex + i].GetPosition(stream));
                if (!IsFinite(bonePosition))
                    return;

                AppendageScratchPositions[chain.FirstScratchIndex + i] = bonePosition;
            }

            quaternion parentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(
                StreamHandles[chain.ParentHandleIndex].GetRotation(stream));
            if (!IsFinite(parentWorldRotation) || !AreAppendageLengthsValid(in chain))
                return;

            float3 rootPosition = AppendageScratchPositions[chain.FirstScratchIndex];
            float3 polePosition = rootPosition + math.mul(parentWorldRotation, chain.PoleLocalOffset);
            if (!IsFinite(polePosition))
                return;

            ContextualPhysicalIkMath.SolveFabrik(
                AppendageScratchPositions,
                chain.FirstScratchIndex,
                AppendageSegmentLengths,
                chain.FirstLengthIndex,
                chain.BoneCount,
                target.Position,
                chain.Iterations,
                chain.Tolerance,
                polePosition);

            if (!AreAppendageScratchPositionsFinite(in chain))
                return;

            quaternion previousWorldRotation = parentWorldRotation;
            for (int boneIndex = 0; boneIndex < chain.BoneCount - 1; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int childHandleIndex = handleIndex + 1;
                TransformStreamHandle boneHandle = StreamHandles[handleIndex];
                TransformStreamHandle childHandle = StreamHandles[childHandleIndex];

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                float3 currentChildPosition = ContextualPhysicalIkMath.ToFloat3(childHandle.GetPosition(stream));
                if (!IsFinite(currentBonePosition) || !IsFinite(currentChildPosition))
                    return;

                float3 currentDirection = ContextualPhysicalIkMath.SafeNormalize(currentChildPosition - currentBonePosition, new float3(0.0f, 0.0f, 1.0f));

                float3 solvedBonePosition = AppendageScratchPositions[chain.FirstScratchIndex + boneIndex];
                float3 solvedChildPosition = AppendageScratchPositions[chain.FirstScratchIndex + boneIndex + 1];
                if (!IsFinite(solvedBonePosition) || !IsFinite(solvedChildPosition))
                    return;

                float3 desiredDirection = ContextualPhysicalIkMath.SafeNormalize(solvedChildPosition - solvedBonePosition, currentDirection);

                quaternion currentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetRotation(stream));
                if (!IsFinite(currentWorldRotation))
                    return;

                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));

                quaternion currentLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetLocalRotation(stream));
                if (!IsFinite(currentLocalRotation))
                    return;

                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
                if (!IsFinite(blendedLocalRotation))
                    return;

                boneHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLocalRotation));
                CacheLocalRotation(handleIndex, blendedLocalRotation);
                previousWorldRotation = desiredWorldRotation;
                if (!IsFinite(previousWorldRotation))
                    return;
            }
        }

        private void ProcessSpine(AnimationStream stream, in ContextualPhysicalIkSpineChainRuntime chain, in ContextualPhysicalIkTargetFrame frame)
        {
            if (!IsSpineChainValid(in chain))
                return;

            float weight = SanitizeBlend(chain.Blend);
            if (weight <= 0.0001f)
                return;

            float3 chestTarget = SpineTargets[chain.TargetStartIndex + 0];
            float3 headTarget = SpineTargets[chain.TargetStartIndex + 1];
            float3 headForwardReference = SpineTargets[chain.TargetStartIndex + 2];
            if (!IsFinite(chestTarget) || !IsFinite(headTarget) || !IsFinite(headForwardReference))
                return;

            TransformStreamHandle parentHandle = StreamHandles[chain.ParentHandleIndex];
            float3 previousWorldPosition = ContextualPhysicalIkMath.ToFloat3(parentHandle.GetPosition(stream));
            quaternion previousWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(parentHandle.GetRotation(stream));
            float3 rootPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[chain.FirstBoneHandleIndex].GetPosition(stream));
            if (!IsFinite(previousWorldPosition) || !IsFinite(previousWorldRotation) || !IsFinite(rootPosition))
                return;

            float3 headForward = ContextualPhysicalIkMath.SafeNormalize(headForwardReference - headTarget, new float3(0.0f, 0.0f, 1.0f));
            float invBoneSpan = math.rcp(math.max(1.0f, chain.BoneCount - 1.0f));
            quaternion slopeLeanRotation = ApproximateSmallEulerXzNoTrig(
                frame.ComLeanRadians.x * SpineSlopeLeanShare,
                frame.ComLeanRadians.y * SpineSlopeLeanShare);

            for (int boneIndex = 0; boneIndex < chain.BoneCount; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                TransformStreamHandle boneHandle = StreamHandles[handleIndex];
                float3 currentLocalPosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetLocalPosition(stream));
                quaternion currentLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetLocalRotation(stream));
                quaternion currentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetRotation(stream));
                if (!IsFinite(currentLocalPosition) || !IsFinite(currentLocalRotation) || !IsFinite(currentWorldRotation))
                    return;

                float normalizedT = boneIndex * invBoneSpan;
                float nextT = SanitizeBlend((boneIndex + 1) * invBoneSpan);

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                if (!IsFinite(currentBonePosition))
                    return;

                float3 currentDirection;
                if (boneIndex < chain.BoneCount - 1)
                {
                    float3 nextBonePosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[handleIndex + 1].GetPosition(stream));
                    if (!IsFinite(nextBonePosition))
                        return;

                    currentDirection = ContextualPhysicalIkMath.SafeNormalize(
                        nextBonePosition - currentBonePosition,
                        new float3(0.0f, 0.0f, 1.0f));
                }
                else
                {
                    currentDirection = ContextualPhysicalIkMath.SafeNormalize(
                        math.mul(currentWorldRotation, new float3(0.0f, 0.0f, 1.0f)),
                        new float3(0.0f, 0.0f, 1.0f));
                }

                float3 desiredBonePosition = ContextualPhysicalIkMath.EvaluateSpinePosition(
                    rootPosition,
                    chestTarget,
                    headTarget,
                    headForward,
                    normalizedT);
                float3 desiredNextPosition = ContextualPhysicalIkMath.EvaluateSpinePosition(
                    rootPosition,
                    chestTarget,
                    headTarget,
                    headForward,
                    nextT);
                float3 desiredDirection = ContextualPhysicalIkMath.SafeNormalize(
                    desiredNextPosition - desiredBonePosition,
                    ContextualPhysicalIkMath.EvaluateSpineTangent(
                        rootPosition,
                        chestTarget,
                        headTarget,
                        headForward,
                        normalizedT,
                        currentDirection));
                float3 desiredLocalPosition = math.rotate(
                    math.inverse(previousWorldRotation),
                    desiredBonePosition - previousWorldPosition);
                float3 blendedLocalPosition = math.lerp(currentLocalPosition, desiredLocalPosition, weight);
                if (!IsFinite(desiredBonePosition) ||
                    !IsFinite(desiredNextPosition) ||
                    !IsFinite(desiredDirection) ||
                    !IsFinite(blendedLocalPosition))
                {
                    return;
                }

                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));
                quaternion leanedDesiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(slopeLeanRotation, desiredWorldRotation));
                desiredWorldRotation = ApproximateNlerpNoSqrt(desiredWorldRotation, leanedDesiredWorldRotation, normalizedT * weight);
                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
                if (!IsFinite(blendedLocalRotation))
                    return;

                boneHandle.SetLocalPosition(stream, ContextualPhysicalIkMath.ToUnityVector3(blendedLocalPosition));
                boneHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLocalRotation));
                CacheLocalPosition(handleIndex, blendedLocalPosition);
                CacheLocalRotation(handleIndex, blendedLocalRotation);
                previousWorldPosition = previousWorldPosition + math.rotate(previousWorldRotation, blendedLocalPosition);
                previousWorldRotation = NormalizeQuaternionNoSqrt(math.mul(previousWorldRotation, blendedLocalRotation));
                if (!IsFinite(previousWorldPosition) || !IsFinite(previousWorldRotation))
                    return;
            }
        }

        private void ProcessSecondary(AnimationStream stream, in ContextualPhysicalIkTargetFrame frame, in ContextualPhysicalIkSecondaryChainRuntime chain)
        {
            if (!IsSecondaryChainValid(in chain))
                return;

            float weight = SanitizeBlend(chain.Blend);
            if (weight <= 0.0001f)
                return;

            float safeDeltaTime = math.max(0.0001f, frame.DeltaTime);
            float stiffness = SanitizeNonNegative(chain.Stiffness);
            float damping = SanitizeNonNegative(chain.Damping);
            for (int boneIndex = 0; boneIndex < chain.BoneCount; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int stateIndex = chain.FirstStateIndex + boneIndex;
                float3 targetPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[handleIndex].GetPosition(stream));
                if (!IsFinite(targetPosition))
                    return;

                ContextualPhysicalIkSecondaryState state = SecondaryStates[stateIndex];
                float3 currentPosition = IsFinite(state.Position) ? state.Position : targetPosition;
                float3 currentVelocity = IsFinite(state.Velocity) ? state.Velocity : float3.zero;

                if (math.lengthsq(currentPosition) <= 0.000001f && math.lengthsq(currentVelocity) <= 0.000001f)
                    currentPosition = targetPosition;

                ContextualPhysicalIkMath.IntegrateSpringDamper(
                    targetPosition,
                    stiffness,
                    damping,
                    safeDeltaTime,
                    ref currentPosition,
                    ref currentVelocity);
                if (!IsFinite(currentPosition) || !IsFinite(currentVelocity))
                {
                    currentPosition = targetPosition;
                    currentVelocity = float3.zero;
                }

                state.Position = currentPosition;
                state.Velocity = currentVelocity;
                SecondaryStates[stateIndex] = state;
            }

            quaternion previousWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(
                StreamHandles[chain.ParentHandleIndex].GetRotation(stream));
            if (!IsFinite(previousWorldRotation))
                return;

            for (int boneIndex = 0; boneIndex < chain.BoneCount - 1; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int nextHandleIndex = handleIndex + 1;
                int stateIndex = chain.FirstStateIndex + boneIndex;
                int nextStateIndex = stateIndex + 1;

                TransformStreamHandle boneHandle = StreamHandles[handleIndex];
                quaternion currentLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetLocalRotation(stream));
                quaternion currentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetRotation(stream));
                if (!IsFinite(currentLocalRotation) || !IsFinite(currentWorldRotation))
                    return;

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                float3 currentChildPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[nextHandleIndex].GetPosition(stream));
                if (!IsFinite(currentBonePosition) || !IsFinite(currentChildPosition))
                    return;

                float3 currentDirection = ContextualPhysicalIkMath.SafeNormalize(currentChildPosition - currentBonePosition, new float3(0.0f, 0.0f, 1.0f));
                float3 desiredDirection = ContextualPhysicalIkMath.SafeNormalize(
                    SecondaryStates[nextStateIndex].Position - SecondaryStates[stateIndex].Position,
                    currentDirection);
                if (!IsFinite(desiredDirection))
                    return;

                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));
                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
                if (!IsFinite(blendedLocalRotation))
                    return;

                boneHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLocalRotation));
                CacheLocalRotation(handleIndex, blendedLocalRotation);
                previousWorldRotation = desiredWorldRotation;
            }
        }

        private void ApplyCachedPose(AnimationStream stream)
        {
            if (StreamHandles.IsCreated && CachedLocalPoseStates.IsCreated)
            {
                int cachedLocalPoseCount = math.min(StreamHandles.Length, CachedLocalPoseStates.Length);
                for (int i = 0; i < cachedLocalPoseCount; i++)
                {
                    ContextualPhysicalIkCachedPoseState cachedState = CachedLocalPoseStates[i];
                    if (cachedState.HasRotation == 0 && cachedState.HasPosition == 0)
                        continue;

                    if (cachedState.HasPosition != 0)
                    {
                        if (!IsFinite(cachedState.Position))
                        {
                            cachedState.HasPosition = 0;
                            CachedLocalPoseStates[i] = cachedState;
                        }
                        else
                        {
                            StreamHandles[i].SetLocalPosition(stream, ContextualPhysicalIkMath.ToUnityVector3(cachedState.Position));
                        }
                    }

                    if (cachedState.HasRotation != 0)
                    {
                        if (!IsFinite(cachedState.Rotation))
                        {
                            cachedState.HasRotation = 0;
                            CachedLocalPoseStates[i] = cachedState;
                            continue;
                        }

                        StreamHandles[i].SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(cachedState.Rotation));
                    }
                }
            }
        }

        private void CacheLocalRotation(int handleIndex, quaternion rotation)
        {
            if (!CachedLocalPoseStates.IsCreated || handleIndex < 0 || handleIndex >= CachedLocalPoseStates.Length)
                return;

            if (!IsFinite(rotation))
                return;

            ContextualPhysicalIkCachedPoseState cachedState = CachedLocalPoseStates[handleIndex];
            cachedState.Rotation = rotation;
            cachedState.HasRotation = 1;
            CachedLocalPoseStates[handleIndex] = cachedState;
        }

        private void CacheLocalPosition(int handleIndex, float3 position)
        {
            if (!CachedLocalPoseStates.IsCreated || handleIndex < 0 || handleIndex >= CachedLocalPoseStates.Length)
                return;

            if (!IsFinite(position))
                return;

            ContextualPhysicalIkCachedPoseState cachedState = CachedLocalPoseStates[handleIndex];
            cachedState.Position = position;
            cachedState.HasPosition = 1;
            CachedLocalPoseStates[handleIndex] = cachedState;
        }

        private void AccumulateMuscleBulge(float value)
        {
            if (!MuscleBulgeOutput.IsCreated || MuscleBulgeOutput.Length <= 0)
                return;

            float safeValue = SanitizeBlend(value);
            float current = SanitizeBlend(MuscleBulgeOutput[0]);
            MuscleBulgeOutput[0] = math.max(current, safeValue);
        }

        private static quaternion ApproximateNlerpNoSqrt(quaternion from, quaternion to, float t)
        {
            if (!IsFinite(from) || !IsFinite(to))
                return quaternion.identity;

            return NormalizeQuaternionNoSqrt(CinematicMath.FastNlerp(from, to, SanitizeBlend(t)));
        }

        private static quaternion NormalizeQuaternionNoSqrt(quaternion value)
        {
            if (!IsFinite(value))
                return quaternion.identity;

            float4 v = value.value;
            float rawLenSq = math.dot(v, v);
            if (!math.isfinite(rawLenSq) || rawLenSq <= 0.000001f)
                return quaternion.identity;

            float lenSq = math.max(rawLenSq, 0.000001f);
            v *= math.rsqrt(lenSq);
            return new quaternion(v);
        }

        private static quaternion ApproximateSmallEulerXzNoTrig(float pitchRadians, float rollRadians)
        {
            pitchRadians = math.select(pitchRadians, 0.0f, !math.isfinite(pitchRadians));
            rollRadians = math.select(rollRadians, 0.0f, !math.isfinite(rollRadians));
            ApproximateSinCosNoTrig(pitchRadians * 0.5f, out float pitchSin, out float pitchCos);
            ApproximateSinCosNoTrig(rollRadians * 0.5f, out float rollSin, out float rollCos);
            quaternion pitch = new quaternion(pitchSin, 0.0f, 0.0f, pitchCos);
            quaternion roll = new quaternion(0.0f, 0.0f, rollSin, rollCos);
            float4 value = math.mul(pitch, roll).value;
            float lenSq = math.max(math.dot(value, value), 0.000001f);
            value *= 1.5f - (0.5f * lenSq);
            return NormalizeQuaternionNoSqrt(new quaternion(value));
        }

        private static quaternion ApproximateAxisRotationNoTrig(float3 axis, float angleRadians)
        {
            axis = ContextualPhysicalIkMath.SafeNormalize(axis, new float3(0.0f, 1.0f, 0.0f));
            angleRadians = math.select(angleRadians, 0.0f, !math.isfinite(angleRadians));
            ApproximateSinCosNoTrig(angleRadians * 0.5f, out float sinHalf, out float cosHalf);
            return NormalizeQuaternionNoSqrt(new quaternion(axis.x * sinHalf, axis.y * sinHalf, axis.z * sinHalf, cosHalf));
        }

        private static void ApproximateSinCosNoTrig(float x, out float sin, out float cos)
        {
            x = math.select(x, 0.0f, !math.isfinite(x));
            float clamped = math.clamp(x, -1.5707964f, 1.5707964f);
            float x2 = clamped * clamped;
            sin = clamped * (1.0f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = 1.0f - (x2 * (0.5f - (x2 * 0.041666667f)));
        }

        private bool IsTwoBoneSetupValid(in ContextualPhysicalIkTwoBoneSetup setup, in ContextualPhysicalIkContactTarget target)
        {
            return IsHandleIndexValid(setup.ParentHandleIndex) &&
                IsHandleIndexValid(setup.UpperHandleIndex) &&
                IsHandleIndexValid(setup.LowerHandleIndex) &&
                IsHandleIndexValid(setup.EndHandleIndex) &&
                setup.UpperLength > 0.0001f &&
                setup.LowerLength > 0.0001f &&
                math.isfinite(setup.UpperLength) &&
                math.isfinite(setup.LowerLength) &&
                math.isfinite(setup.ReachSafetyMargin) &&
                setup.ReachSafetyMargin >= 0.0f &&
                IsFinite(setup.PoleLocalOffset) &&
                IsFinite(target.WorldPosition) &&
                math.isfinite(target.Blend);
        }

        private bool IsAppendageChainValid(in ContextualPhysicalIkAppendageChainRuntime chain)
        {
            return StreamHandles.IsCreated &&
                AppendageScratchPositions.IsCreated &&
                AppendageTargets.IsCreated &&
                AppendageSegmentLengths.IsCreated &&
                chain.BoneCount >= 2 &&
                IsHandleRangeValid(chain.FirstBoneHandleIndex, chain.BoneCount) &&
                IsHandleIndexValid(chain.ParentHandleIndex) &&
                IsNativeRangeValid(chain.FirstScratchIndex, chain.BoneCount, AppendageScratchPositions.Length) &&
                IsNativeRangeValid(chain.FirstLengthIndex, chain.BoneCount - 1, AppendageSegmentLengths.Length) &&
                chain.TargetIndex >= 0 &&
                chain.TargetIndex < AppendageTargets.Length &&
                chain.Iterations >= 1 &&
                chain.Iterations <= MaxAppendageIterations &&
                math.isfinite(chain.Tolerance) &&
                chain.Tolerance > 0.0f &&
                math.isfinite(chain.Blend);
        }

        private bool AreAppendageLengthsValid(in ContextualPhysicalIkAppendageChainRuntime chain)
        {
            int lengthCount = chain.BoneCount - 1;
            for (int i = 0; i < lengthCount; i++)
            {
                float length = AppendageSegmentLengths[chain.FirstLengthIndex + i];
                if (!math.isfinite(length) || length <= 0.0001f)
                    return false;
            }

            return true;
        }

        private bool AreAppendageScratchPositionsFinite(in ContextualPhysicalIkAppendageChainRuntime chain)
        {
            for (int i = 0; i < chain.BoneCount; i++)
            {
                if (!IsFinite(AppendageScratchPositions[chain.FirstScratchIndex + i]))
                    return false;
            }

            return true;
        }

        private bool IsSpineChainValid(in ContextualPhysicalIkSpineChainRuntime chain)
        {
            return StreamHandles.IsCreated &&
                SpineTargets.IsCreated &&
                chain.BoneCount >= 5 &&
                IsHandleRangeValid(chain.FirstBoneHandleIndex, chain.BoneCount) &&
                IsHandleIndexValid(chain.ParentHandleIndex) &&
                IsNativeRangeValid(chain.TargetStartIndex, SpineTargetCountPerChain, SpineTargets.Length) &&
                math.isfinite(chain.Blend);
        }

        private bool IsSecondaryChainValid(in ContextualPhysicalIkSecondaryChainRuntime chain)
        {
            return StreamHandles.IsCreated &&
                SecondaryStates.IsCreated &&
                chain.BoneCount >= 2 &&
                IsHandleRangeValid(chain.FirstBoneHandleIndex, chain.BoneCount) &&
                IsHandleIndexValid(chain.ParentHandleIndex) &&
                IsNativeRangeValid(chain.FirstStateIndex, chain.BoneCount, SecondaryStates.Length) &&
                math.isfinite(chain.Blend);
        }

        private bool IsHandleIndexValid(int index)
        {
            return StreamHandles.IsCreated && index >= 0 && index < StreamHandles.Length;
        }

        private bool IsHandleRangeValid(int startIndex, int count)
        {
            return StreamHandles.IsCreated &&
                startIndex >= 0 &&
                count > 0 &&
                startIndex <= StreamHandles.Length - count;
        }

        private static bool IsNativeRangeValid(int startIndex, int count, int length)
        {
            return startIndex >= 0 &&
                count > 0 &&
                startIndex <= length - count;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(quaternion value)
        {
            if (!math.all(math.isfinite(value.value)))
                return false;

            float lengthSq = math.dot(value.value, value.value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f;
        }

        private static float SanitizeBlend(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static ContextualPhysicalIkContactTarget ResolveTarget(in ContextualPhysicalIkTargetFrame frame, byte targetChannel)
        {
            switch (targetChannel)
            {
                case 0:
                    return frame.LeftFoot;
                case 1:
                    return frame.RightFoot;
                case 2:
                    return frame.LeftHand;
                case 3:
                    return frame.RightHand;
                default:
                    return default;
            }
        }
    }

    /// <summary>
    /// Per-entity contextual physical IK authoring and runtime bridge.
    /// Caches stream handles once, publishes ground/tunnel probe state to the shared runtime owner,
    /// and injects an Animation C# job into the existing Animator playable graph.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Hecton8/Gameplay/Contextual Physical IK Rig")]
    public sealed class ContextualPhysicalIkRig : MonoBehaviour, IOriginShiftListener, IPhysicalHandIkTargetSink, IGlobalRegistryHotSwapListener
    {
        private const int PelvisHandleIndex = 0;
        private const int LeftLegUpperHandleIndex = 1;
        private const int LeftLegLowerHandleIndex = 2;
        private const int LeftFootHandleIndex = 3;
        private const int RightLegUpperHandleIndex = 4;
        private const int RightLegLowerHandleIndex = 5;
        private const int RightFootHandleIndex = 6;
        private const int LeftArmParentHandleIndex = 7;
        private const int LeftArmUpperHandleIndex = 8;
        private const int LeftArmLowerHandleIndex = 9;
        private const int LeftHandHandleIndex = 10;
        private const int RightArmParentHandleIndex = 11;
        private const int RightArmUpperHandleIndex = 12;
        private const int RightArmLowerHandleIndex = 13;
        private const int RightHandHandleIndex = 14;
        private const int BaseHandleCount = 15;
        private const int SpineTargetCountPerChain = 3;
        private const float Tier0DistanceMax = 10.0f;
        private const float Tier1DistanceMax = 25.0f;
        private const float ThrottleHysteresisMeters = 4.0f;
        private const float Tier0UpgradeDistanceSq = (Tier0DistanceMax - ThrottleHysteresisMeters) * (Tier0DistanceMax - ThrottleHysteresisMeters);
        private const float Tier0DowngradeDistanceSq = (Tier0DistanceMax + ThrottleHysteresisMeters) * (Tier0DistanceMax + ThrottleHysteresisMeters);
        private const float Tier1UpgradeDistanceSq = (Tier1DistanceMax - ThrottleHysteresisMeters) * (Tier1DistanceMax - ThrottleHysteresisMeters);
        private const float Tier1DowngradeDistanceSq = (Tier1DistanceMax + ThrottleHysteresisMeters) * (Tier1DistanceMax + ThrottleHysteresisMeters);
        private const int MaxRendererSearchDepth = 32;
        private const int MaxRendererSearchNodes = 512;
        private const float PredictiveRepairLatchDistance = 0.3f;
        private const float PredictiveRepairLatchDistanceSq = PredictiveRepairLatchDistance * PredictiveRepairLatchDistance;
        private const float UpperArmVisibilityProxyRadius = 0.35f;
        private const float UpperArmVisibilityProxyRadiusSq = UpperArmVisibilityProxyRadius * UpperArmVisibilityProxyRadius;
        private const float ColdShiverPhaseWrap = 1024.0f;
        private const float BreathingPhaseWrap = 1024.0f;
        private const float ExternalWallHandHoldSeconds = 0.12f;
        private const float ExternalSqueezePoleHoldSeconds = 0.18f;
        private const float ExternalSqueezePoleLocalMeters = 0.075f;
        private const float MinimumIkQualityWeight01 = 0.25f;
        private const float MinimumWallTouchQualityWeight01 = 0.15f;
        private const float MinimumBreathingRateWeight01 = 0.75f;
        private const float LowQualityThrottleDistanceBias = 1.65f;
        private const int MaxAppendageIterations = 12;
        private const string NativeMemoryOwner = nameof(ContextualPhysicalIkRig);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const float MaxAcceptedOriginShiftMeters = 10000.0f;
        private const float MaxAcceptedOriginShiftMetersSq = MaxAcceptedOriginShiftMeters * MaxAcceptedOriginShiftMeters;
        private static readonly float3 HeadToChestSocketLocalOffset = new float3(0.0f, -0.32f, -0.08f);
        private static readonly float3 HeadForwardReferenceLocalOffset = new float3(0.0f, 0.0f, 0.25f);
        private static readonly int MuscleBulgeShaderId = Shader.PropertyToID("_MuscleBulge");

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes IK authoring chains from character rig data.
        private struct AppendageChainAuthoring
        {
            [Tooltip("Parent transform that owns the first chain bone. Used to convert solved world rotations into local space.")]
            public Transform parentTransform;

            [Tooltip("Ordered chain bones from root to tip.")]
            public Transform[] bones;

            [Tooltip("Optional world-space target transform for the appendage tip.")]
            public Transform targetTransform;

            [Tooltip("Optional pole/hint transform for appendage bend bias.")]
            public Transform poleHint;

            [Tooltip("Optional voxel runtime used to snap this appendage onto voxel corners during wall-climbing.")]
            public HectonVoxelVolume voxelVolume;

            [Tooltip("Optional transform whose up axis is used as the wall normal when voxel snapping is enabled.")]
            public Transform surfaceNormalSource;

            [Tooltip("If enabled, the resolved appendage target is snapped onto the nearest voxel corner on the assigned runtime volume.")]
            public bool snapTargetToVoxelCorner;

            [Tooltip("Maximum FABRIK iterations used for this chain.")]
            [Range(1, 12)]
            public int iterations;

            [Tooltip("Termination tolerance for this FABRIK chain.")]
            [Min(0.0001f)]
            public float tolerance;

            [Tooltip("Base blend weight applied to this appendage chain.")]
            [Range(0.0f, 1.0f)]
            public float blend;
        }
#pragma warning restore 0649

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes IK authoring chains from character rig data.
        private struct SpineChainAuthoring
        {
            [Tooltip("Parent transform for the first spine bone.")]
            public Transform parentTransform;

            [Tooltip("Ordered spine bones from hip/base to neck/head.")]
            public Transform[] bones;

            [Tooltip("Chest or upper-spine target used to shape the spline midpoint.")]
            public Transform chestTarget;

            [Tooltip("Head target used to shape the spline endpoint.")]
            public Transform headTarget;

            [Tooltip("Optional forward reference used to stabilize the head tangent.")]
            public Transform headForwardReference;

            [Tooltip("Blend weight for spinal curvature IK.")]
            [Range(0.0f, 1.0f)]
            public float blend;
        }
#pragma warning restore 0649

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes IK authoring chains from character rig data.
        private struct SecondaryChainAuthoring
        {
            [Tooltip("Parent transform for the first secondary-motion bone.")]
            public Transform parentTransform;

            [Tooltip("Ordered bones that should trail with spring-damper motion.")]
            public Transform[] bones;

            [Tooltip("Spring stiffness for this chain.")]
            [Min(0.0f)]
            public float stiffness;

            [Tooltip("Velocity damping for this chain.")]
            [Min(0.0f)]
            public float damping;

            [Tooltip("Blend weight for this chain.")]
            [Range(0.0f, 1.0f)]
            public float blend;
        }
#pragma warning restore 0649

        [Header("Core References")]
        [Tooltip("Animator owning the live playable graph that will be wrapped by the IK job.")]
        [SerializeField] private Animator animator;

        [Tooltip("Stable root transform used to capture contextual probe state.")]
        [SerializeField] private Transform characterRoot;

        [Tooltip("Pelvis/hip bone used for center-of-mass shift and leg parenting.")]
        [SerializeField] private Transform pelvis;

        [Header("Terrain / Tunnel Probes")]
        [Tooltip("World-space probe origin used for the left foot ground ray.")]
        [SerializeField] private Transform leftFootProbe;

        [Tooltip("World-space probe origin used for the right foot ground ray.")]
        [SerializeField] private Transform rightFootProbe;

        [Tooltip("World-space probe origin used for the left hand brace ray.")]
        [SerializeField] private Transform leftHandProbe;

        [Tooltip("World-space probe origin used for the right hand brace ray.")]
        [SerializeField] private Transform rightHandProbe;

        [Header("Predictive Repair Latching")]
        [Tooltip("Optional repair target that exposes AUP snap points for predictive hand IK latching.")]
        [SerializeField] private MonoBehaviour predictiveRepairTargetBehaviour;

        [Tooltip("Tracked left controller transform. Falls back to the left hand probe when unset.")]
        [SerializeField] private Transform leftControllerProbe;

        [Tooltip("Tracked right controller transform. Falls back to the right hand probe when unset.")]
        [SerializeField] private Transform rightControllerProbe;

        [Tooltip("Controller direction dot threshold required before predictive hand latching starts.")]
        [SerializeField, Range(0.1f, 0.98f)] private float predictiveRepairDirectionDot = 0.72f;

        [Tooltip("Blend sharpness for predictive repair latching; high values hide one-frame controller latency.")]
        [SerializeField, Range(1.0f, 32.0f)] private float predictiveRepairBlendSharpness = 18.0f;

        [Header("VR Arm Culling")]
        [Tooltip("Disables upper-arm renderers after they remain outside the VR camera FOV for the hysteresis window.")]
        [SerializeField] private bool enableUpperArmFovCulling;

        [Tooltip("Upper-arm renderers that may be hidden in narrow-FOV VR to avoid elbow clipping.")]
        [SerializeField] private Renderer[] upperArmRenderers;

        [Tooltip("Seconds the upper arms must remain outside view before renderers are disabled.")]
        [SerializeField, Range(0.05f, 1.0f)] private float upperArmCullHysteresisSeconds = 0.2f;

        [Tooltip("Minimum camera-forward dot for upper-arm visibility.")]
        [SerializeField, Range(-0.2f, 0.8f)] private float upperArmFovDotThreshold = 0.08f;

        [Tooltip("Terrain layers used for foot-placement raycasts.")]
        [SerializeField] private LayerMask groundMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Wall/cave layers used for tunnel and hand-brace raycasts.")]
        [SerializeField] private LayerMask wallMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("Left Leg")]
        [Tooltip("Left upper-leg/thigh bone.")]
        [SerializeField] private Transform leftUpperLeg;

        [Tooltip("Left lower-leg/shin bone.")]
        [SerializeField] private Transform leftLowerLeg;

        [Tooltip("Left foot bone.")]
        [SerializeField] private Transform leftFoot;

        [Tooltip("Optional left knee pole hint.")]
        [SerializeField] private Transform leftKneeHint;

        [Header("Right Leg")]
        [Tooltip("Right upper-leg/thigh bone.")]
        [SerializeField] private Transform rightUpperLeg;

        [Tooltip("Right lower-leg/shin bone.")]
        [SerializeField] private Transform rightLowerLeg;

        [Tooltip("Right foot bone.")]
        [SerializeField] private Transform rightFoot;

        [Tooltip("Optional right knee pole hint.")]
        [SerializeField] private Transform rightKneeHint;

        [Header("Left Arm")]
        [Tooltip("Parent transform for the left upper arm. Usually chest/clavicle.")]
        [SerializeField] private Transform leftArmParent;

        [Tooltip("Left upper-arm bone.")]
        [SerializeField] private Transform leftUpperArm;

        [Tooltip("Left lower-arm bone.")]
        [SerializeField] private Transform leftLowerArm;

        [Tooltip("Left hand bone.")]
        [SerializeField] private Transform leftHand;

        [Tooltip("Optional left elbow pole hint.")]
        [SerializeField] private Transform leftElbowHint;

        [Header("Right Arm")]
        [Tooltip("Parent transform for the right upper arm. Usually chest/clavicle.")]
        [SerializeField] private Transform rightArmParent;

        [Tooltip("Right upper-arm bone.")]
        [SerializeField] private Transform rightUpperArm;

        [Tooltip("Right lower-arm bone.")]
        [SerializeField] private Transform rightLowerArm;

        [Tooltip("Right hand bone.")]
        [SerializeField] private Transform rightHand;

        [Tooltip("Optional right elbow pole hint.")]
        [SerializeField] private Transform rightElbowHint;

        [Header("Optional FABRIK Appendages")]
        [Tooltip("Optional multi-joint appendage chains solved with FABRIK inside the animation job.")]
        [SerializeField] private AppendageChainAuthoring[] appendageChains;
        [SerializeField] private SpineChainAuthoring spineChain;
        [SerializeField] private SecondaryChainAuthoring[] secondaryChains;

        [Header("Contextual Tuning")]
        [Tooltip("If disabled, foot terrain adaptation stays fully in authored animation.")]
        [SerializeField] private bool enableFootPlacement = true;

        [Tooltip("If disabled, tunnel hand bracing stays fully in authored animation.")]
        [SerializeField] private bool enableHandBracing = true;

        [Tooltip("Ground probe distance multiplier relative to cached leg reach.")]
        [SerializeField, Range(0.5f, 2.0f)] private float footProbeDistanceScale = 1.2f;

        [Tooltip("Hand-brace probe distance multiplier relative to cached arm reach.")]
        [SerializeField, Range(0.5f, 2.0f)] private float handProbeDistanceScale = 1.0f;

        [Tooltip("Offset applied away from the supporting surface when planting a foot.")]
        [SerializeField, Range(0.0f, 0.15f)] private float footContactOffset = 0.02f;

        [Tooltip("Offset applied away from the supporting wall when bracing a hand.")]
        [SerializeField, Range(0.0f, 0.2f)] private float handContactOffset = 0.08f;

        [Tooltip("Forward clearance distance used to decide when tunnel bracing should activate.")]
        [SerializeField, Range(0.1f, 3.0f)] private float tunnelClearanceDistance = 1.5f;

        [Tooltip("Distance band used to fade hand bracing in and out near walls.")]
        [SerializeField, Range(0.05f, 1.0f)] private float handBraceFadeDistance = 0.5f;

        [Tooltip("Exponential smoothing sharpness for target positions.")]
        [SerializeField, Range(1.0f, 32.0f)] private float targetPositionSharpness = 16.0f;

        [Tooltip("Exponential smoothing sharpness for target normals.")]
        [SerializeField, Range(1.0f, 32.0f)] private float targetNormalSharpness = 12.0f;

        [Tooltip("Exponential smoothing sharpness for blend weights.")]
        [SerializeField, Range(1.0f, 32.0f)] private float blendFadeSharpness = 10.0f;

        [Tooltip("Maximum terrain delta-height that can feed COM shift.")]
        [SerializeField, Range(0.05f, 1.0f)] private float maxDeltaHeight = 0.8f;

        [Tooltip("How strongly uneven left/right footing shifts the pelvis laterally.")]
        [SerializeField, Range(0.0f, 1.0f)] private float comShiftLateralFactor = 0.4f;

        [Tooltip("How strongly step-up magnitude pushes the pelvis forward.")]
        [SerializeField, Range(0.0f, 1.0f)] private float comShiftForwardFactor = 0.3f;

        [Tooltip("How strongly step-up magnitude compresses the pelvis downward.")]
        [SerializeField, Range(0.0f, 1.0f)] private float comShiftVerticalFactor = 0.2f;

        [Tooltip("Smoothing sharpness for pelvis offset and lean.")]
        [SerializeField, Range(1.0f, 32.0f)] private float comResponseSharpness = 8.0f;

        [Tooltip("Maximum lateral pelvis shift in local space.")]
        [SerializeField, Range(0.0f, 0.5f)] private float maxComLateral = 0.25f;

        [Tooltip("Maximum forward pelvis shift in local space.")]
        [SerializeField, Range(0.0f, 0.3f)] private float maxComForward = 0.15f;

        [Tooltip("Maximum downward pelvis compression in local space.")]
        [SerializeField, Range(0.0f, 0.2f)] private float maxComVertical = 0.12f;

        [Tooltip("Maximum forward pelvis lean applied from terrain adaptation, in degrees.")]
        [SerializeField, Range(0.0f, 30.0f)] private float comLeanPitchDegrees = 12.0f;

        [Tooltip("Maximum side-roll pelvis lean applied from uneven footing, in degrees.")]
        [SerializeField, Range(0.0f, 20.0f)] private float comLeanRollDegrees = 8.0f;

        [Tooltip("Global blend weight applied to pelvis positional adaptation.")]
        [SerializeField, Range(0.0f, 1.0f)] private float pelvisPositionBlend = 1.0f;

        [Tooltip("Global blend weight applied to pelvis lean.")]
        [SerializeField, Range(0.0f, 1.0f)] private float pelvisRotationBlend = 1.0f;

        [Tooltip("Safety margin kept below full extension to avoid solver singularities.")]
        [SerializeField, Range(0.001f, 0.1f)] private float reachSafetyMargin = 0.02f;

        [Tooltip("Base blend applied to foot-placement limbs.")]
        [SerializeField, Range(0.0f, 1.0f)] private float footLimbBlend = 1.0f;

        [Tooltip("Base blend applied to hand-brace limbs.")]
        [SerializeField, Range(0.0f, 1.0f)] private float handLimbBlend = 1.0f;

        [Header("Tool Hand IK")]
        [Tooltip("Enables short camera-forward collision rays that retract the hands before the held tool clips into geometry.")]
        [SerializeField] private bool enableToolRetraction = true;

        [Tooltip("Minimum GlobalQualityWeight before wall-touch reaches non-zero weight. Values near zero keep the feature visible on weak devices; higher values defer it continuously.")]
        [FormerlySerializedAs("disableWallTouchOnLowTier")]
        [SerializeField, Range(0.0f, 0.95f)] private float wallTouchQualityRampFloor01 = MinimumWallTouchQualityWeight01;

        [Tooltip("Left palm can brace against walls only while the left hand is empty.")]
        [SerializeField] private bool leftHandEmptyForWallTouch = true;

        [Tooltip("Horizontal camera-space offset for each hand collision ray.")]
        [SerializeField, Range(0.0f, 0.4f)] private float cameraHandLateralOffset = 0.12f;

        [Tooltip("Vertical camera-space offset for each hand collision ray.")]
        [SerializeField, Range(-0.2f, 0.3f)] private float cameraHandVerticalOffset = -0.08f;

        [Tooltip("Maximum distance for the two short tool collision rays.")]
        [SerializeField, Range(0.1f, 0.8f)] private float toolCollisionDistance = 0.5f;

        [Tooltip("Backward hand retraction applied at full blockage.")]
        [SerializeField, Range(0.0f, 0.5f)] private float toolRetractionBackDistance = 0.22f;

        [Tooltip("Upward hand lift applied at full blockage.")]
        [SerializeField, Range(0.0f, 0.3f)] private float toolRetractionLiftDistance = 0.1f;

        [Tooltip("Blend cap for collision-driven tool retraction.")]
        [SerializeField, Range(0.0f, 1.0f)] private float toolRetractionBlend = 1.0f;

        [Tooltip("Reciprocal decay sharpness for additive tool recoil offsets.")]
        [SerializeField, Range(1.0f, 64.0f)] private float toolRecoilDecaySharpness = 18.0f;

        [Tooltip("Hard cap for additive tool recoil offsets before they enter the IK job.")]
        [SerializeField, Range(0.0f, 0.4f)] private float toolRecoilMaxOffsetMeters = 0.16f;

        [Tooltip("Blend sharpness for dashboard/terminal hand snaps.")]
        [SerializeField, Range(1.0f, 48.0f)] private float terminalSnapBlendSharpness = 24.0f;

        [Tooltip("Adds a small deterministic hand tremor when the survival environment is below the cold threshold.")]
        [SerializeField] private bool enableColdShiver = true;

        [Tooltip("Environment temperature below which cold shiver starts.")]
        [SerializeField, Range(-20.0f, 20.0f)] private float coldShiverTemperatureThresholdCelsius = 5.0f;

        [Tooltip("Temperature drop needed to reach full cold shiver blend.")]
        [SerializeField, Range(1.0f, 25.0f)] private float coldShiverFullDeltaCelsius = 10.0f;

        [Tooltip("Maximum cold shiver target offset in meters.")]
        [SerializeField, Range(0.0f, 0.03f)] private float coldShiverAmplitudeMeters = 0.006f;

        [Tooltip("Cold shiver triangle-wave frequency.")]
        [SerializeField, Range(4.0f, 32.0f)] private float coldShiverFrequencyHz = 18.0f;

        [Tooltip("Blend sharpness for entering or leaving cold shiver.")]
        [SerializeField, Range(1.0f, 24.0f)] private float coldShiverBlendSharpness = 7.0f;

        [Tooltip("Applies deterministic respiration offsets to spine and shoulder targets.")]
        [SerializeField] private bool enableProceduralBreathing = true;

        [Tooltip("Base respiration rate for relaxed posture.")]
        [SerializeField, Range(0.05f, 1.2f)] private float breathingBaseRateHz = 0.22f;

        [Tooltip("Additional respiration rate at full stress.")]
        [SerializeField, Range(0.0f, 2.0f)] private float breathingStressRateHz = 0.9f;

        [Tooltip("Maximum chest/head offset from procedural breathing.")]
        [SerializeField, Range(0.0f, 0.05f)] private float breathingAmplitudeMeters = 0.012f;

        [Tooltip("Deterministic high-stress respiratory jitter cap.")]
        [SerializeField, Range(0.0f, 0.025f)] private float breathingStressJitterMeters = 0.006f;

        [Tooltip("Blend sharpness for stress-fed respiration changes.")]
        [SerializeField, Range(1.0f, 24.0f)] private float breathingBlendSharpness = 8.0f;

        [SerializeField, Range(0.0f, 30.0f)] private float overExtensionResistanceDegrees = 12.0f;
        [SerializeField] private Renderer muscleBulgeRenderer;
        [SerializeField, Min(0)] private int muscleBulgeMaterialSlot;
        [SerializeField, Range(0.0f, 2.0f)] private float muscleBulgeScale = 1.0f;
        [SerializeField, Range(1.0f, 32.0f)] private float muscleBulgeSharpness = 12.0f;

        private ContextualPhysicalIkRuntime _runtime;
        private PlayableGraph _graph;
        private PlayableOutput _wrappedOutput;
        private Playable _wrappedSourcePlayable;
        private AnimationScriptPlayable _ikPlayable;

        // COLD ALLOC: RigNativeBufferSet[1] - native IK animation buffer owner indirection - owner: ContextualPhysicalIkRig
        private RigNativeBufferSet _nativeBuffers = new RigNativeBufferSet();

        private ref NativeArray<TransformStreamHandle> _streamHandles => ref _nativeBuffers.StreamHandles;
        private ref NativeArray<ContextualPhysicalIkTwoBoneSetup> _twoBoneSetups => ref _nativeBuffers.TwoBoneSetups;
        private ref NativeArray<ContextualPhysicalIkAppendageChainRuntime> _appendageChainRuntimes => ref _nativeBuffers.AppendageChainRuntimes;
        private ref NativeArray<float> _appendageSegmentLengths => ref _nativeBuffers.AppendageSegmentLengths;
        private ref NativeArray<ContextualPhysicalIkAppendageTarget> _appendageTargets => ref _nativeBuffers.AppendageTargets;
        private ref NativeArray<float3> _appendageScratchPositions => ref _nativeBuffers.AppendageScratchPositions;
        private ref NativeArray<ContextualPhysicalIkSpineChainRuntime> _spineChainRuntimes => ref _nativeBuffers.SpineChainRuntimes;
        private ref NativeArray<float3> _spineTargets => ref _nativeBuffers.SpineTargets;
        private ref NativeArray<ContextualPhysicalIkSecondaryChainRuntime> _secondaryChainRuntimes => ref _nativeBuffers.SecondaryChainRuntimes;
        private ref NativeArray<ContextualPhysicalIkSecondaryState> _secondaryStates => ref _nativeBuffers.SecondaryStates;
        private ref NativeArray<ContextualPhysicalIkCachedPoseState> _cachedLocalPoseStates => ref _nativeBuffers.CachedLocalPoseStates;
        private ref NativeArray<float> _muscleBulgeOutput => ref _nativeBuffers.MuscleBulgeOutput;
        private ref NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly _currentTargetFrames => ref _nativeBuffers.CurrentTargetFrames;

        private Transform[] _appendageTargetSources;
        private Transform[] _appendageFallbackTips;
        private HectonVoxelVolume[] _appendageVoxelVolumes;
        private Transform[] _appendageSurfaceNormalSources;

        private IKinematicRepairTarget _predictiveRepairTarget;
        private AbsoluteUniversePosition _previousLeftControllerAup;
        private AbsoluteUniversePosition _previousRightControllerAup;
        private Vector3 _predictiveLeftHandPosition;
        private Vector3 _predictiveRightHandPosition;
        private Vector3 _predictiveLeftHandNormal = Vector3.up;
        private Vector3 _predictiveRightHandNormal = Vector3.up;
        private Vector3 _externalWallLeftHandPosition;
        private Vector3 _externalWallRightHandPosition;
        private Vector3 _externalWallLeftHandNormal = Vector3.up;
        private Vector3 _externalWallRightHandNormal = Vector3.up;
        private Vector3 _leftToolRecoilOffset;
        private Vector3 _rightToolRecoilOffset;
        private Vector3 _terminalRightHandPosition;
        private Vector3 _terminalRightHandNormal = Vector3.up;
        private float _predictiveLeftHandBlend;
        private float _predictiveRightHandBlend;
        private float _externalWallLeftHandBlend;
        private float _externalWallRightHandBlend;
        private float _externalWallLeftHandHoldTimer;
        private float _externalWallRightHandHoldTimer;
        private float _terminalRightHandBlend;
        private float _terminalRightHandTargetBlend;
        private float _terminalRightHandHoldTimer;
        private float _coldShiverBlend;
        private float _coldShiverPhase;
        private float _breathingBlend;
        private float _breathingPhase;
        private float _playerStress01;
        private float _externalSqueezePoleBlend;
        private float _externalSqueezePoleHoldTimer;
        private float _upperArmCullTimer;
        private Material _muscleBulgeMaterialInstance;
        private List<Material> _muscleBulgeSharedMaterials;
        private Material _muscleBulgeOriginalMaterial;
        private float _muscleBulgeCurrent;
        private float _cachedLeftLegReach;
        private float _cachedRightLegReach;
        private float _cachedLeftArmReach;
        private float _cachedRightArmReach;
        private float3 _baseLeftArmPoleLocalOffset;
        private float3 _baseRightArmPoleLocalOffset;

        private int _lastPlayerStressSignalSequence;
        private int _entitySlot = -1;
        private int _terminalRightHandSourceId;
        private int _spineHandleStartIndex = BaseHandleCount;
        private int _secondaryHandleStartIndex = BaseHandleCount;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private byte _stableThrottleTier;
        private bool _runtimeInitialized;
        private bool _animationInjected;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _muscleBulgeMaterialInitialized;
        private bool _attemptedMuscleBulgeRendererResolve;
        private bool _hasPreviousLeftPredictiveControllerPose;
        private bool _hasPreviousRightPredictiveControllerPose;
        private bool _terminalRightHandActive;
        private bool _upperArmRenderersVisible = true;

        private void OnEnable()
        {
            TryResolveReferences();
            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            EnsureRuntimeInitialized();
            TryInitializeAnimationInjection();
            TryRegisterWithRuntime();
            TryRegisterOriginShiftListener();
        }

        private void Start()
        {
            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            EnsureRuntimeInitialized();
            TryInitializeAnimationInjection();
            TryRegisterWithRuntime();
        }

        private void OnDisable()
        {
            SetUpperArmRenderersVisible(true);
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterFromRuntime();
            TearDownAnimationInjection();
            DisposeRuntimeArrays();
        }

        private void OnDestroy()
        {
            SetUpperArmRenderersVisible(true);
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterFromRuntime();
            TearDownAnimationInjection();
            DisposeRuntimeArrays();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
        }

        private void RefreshColdRegistryDependencies()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!Application.isPlaying || _registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            TryResolveReferences();
        }
#endif

        public void AddRecoil(float3 impulse)
        {
            if (!math.all(math.isfinite(impulse)))
                return;

            float recoilCap = SanitizeNonNegativeScalar(toolRecoilMaxOffsetMeters);
            _rightToolRecoilOffset = ClampVectorNoSqrt(
                _rightToolRecoilOffset + ContextualPhysicalIkMath.ToUnityVector3(impulse),
                recoilCap);
            _leftToolRecoilOffset = ClampVectorNoSqrt(
                _leftToolRecoilOffset + ContextualPhysicalIkMath.ToUnityVector3(impulse * 0.45f),
                recoilCap);
        }

        public void SetLeftHandEmptyForWallTouch(bool isEmpty)
        {
            leftHandEmptyForWallTouch = isEmpty;
        }

        public void SetTerminalHandTarget(in PhysicalHandIkTarget target)
        {
            if (target.HandSide != PhysicalHandSide.Right ||
                !IsFiniteVector(target.WorldPosition) ||
                !IsFiniteQuaternion(target.WorldRotation))
            {
                return;
            }

            _terminalRightHandSourceId = target.SourceId;
            _terminalRightHandPosition = target.WorldPosition;
            _terminalRightHandNormal = target.WorldRotation * Vector3.up;
            float3 terminalNormal = ContextualPhysicalIkMath.ToFloat3(_terminalRightHandNormal);
            float terminalNormalLengthSq = math.lengthsq(terminalNormal);
            if (!math.all(math.isfinite(terminalNormal)) ||
                !math.isfinite(terminalNormalLengthSq) ||
                terminalNormalLengthSq <= 0.0001f)
            {
                _terminalRightHandNormal = Vector3.up;
            }
            else
            {
                _terminalRightHandNormal = NormalizeVectorNoSqrt(_terminalRightHandNormal, Vector3.up);
            }

            _terminalRightHandHoldTimer = SanitizeNonNegativeScalar(target.HoldSeconds);
            _terminalRightHandTargetBlend = SanitizeUnitScalar(target.Blend);
            _terminalRightHandActive = true;
        }

        public void ClearTerminalHandTarget(int sourceId)
        {
            if (!_terminalRightHandActive || sourceId != _terminalRightHandSourceId)
                return;

            _terminalRightHandHoldTimer = 0.0f;
            _terminalRightHandTargetBlend = 0.0f;
        }

        internal void AssignEntitySlot(int entitySlot, NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly targetFrames)
        {
            _entitySlot = entitySlot;
            _currentTargetFrames = targetFrames;
            UpdateJobDataTargetFrames();
        }

        internal void OnTargetBufferSwapped(NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly targetFrames)
        {
            _currentTargetFrames = targetFrames;
            UpdateJobDataTargetFrames();
        }

        internal void ApplyExternalWallHandTargets(in PlayerKinematicsHandTarget leftTarget, in PlayerKinematicsHandTarget rightTarget)
        {
            if ((leftTarget.Flags & PlayerKinematicsHandTarget.FlagSqueeze) != 0 ||
                (rightTarget.Flags & PlayerKinematicsHandTarget.FlagSqueeze) != 0)
            {
                _externalSqueezePoleHoldTimer = ExternalSqueezePoleHoldSeconds;
            }

            if (leftTarget.Hit != 0 && math.all(math.isfinite(leftTarget.Position)))
            {
                _externalWallLeftHandPosition = ContextualPhysicalIkMath.ToUnityVector3(leftTarget.Position);
                _externalWallLeftHandNormal = ContextualPhysicalIkMath.ToUnityVector3(
                    ContextualPhysicalIkMath.SafeNormalize(leftTarget.Normal, new float3(0.0f, 1.0f, 0.0f)));
                _externalWallLeftHandBlend = SanitizeUnitScalar(leftTarget.Blend);
                _externalWallLeftHandHoldTimer = ExternalWallHandHoldSeconds;
            }
            else
            {
                _externalWallLeftHandHoldTimer = 0.0f;
            }

            if (rightTarget.Hit != 0 && math.all(math.isfinite(rightTarget.Position)))
            {
                _externalWallRightHandPosition = ContextualPhysicalIkMath.ToUnityVector3(rightTarget.Position);
                _externalWallRightHandNormal = ContextualPhysicalIkMath.ToUnityVector3(
                    ContextualPhysicalIkMath.SafeNormalize(rightTarget.Normal, new float3(0.0f, 1.0f, 0.0f)));
                _externalWallRightHandBlend = SanitizeUnitScalar(rightTarget.Blend);
                _externalWallRightHandHoldTimer = ExternalWallHandHoldSeconds;
            }
            else
            {
                _externalWallRightHandHoldTimer = 0.0f;
            }
        }

        internal bool CaptureScheduledState(
            float deltaTime,
            uint frameIndex,
            float3 viewerPosition,
            float3 viewerForward,
            float3 viewerUp,
            float3 viewerRight,
            bool hasViewerPosition,
            ref ContextualPhysicalIkEntityState entityState)
        {
            if (!isActiveAndEnabled)
                return false;

            if (!EnsureRuntimeInitialized())
                return false;

            if (!TryInitializeAnimationInjection())
                return false;

            if (_entitySlot < 0)
                return false;

            float qualityWeight01 = ResolveIkQualityWeight01();
            float footQualityWeight01 = ResolveFootIkQualityWeight01(qualityWeight01);
            float wallTouchQualityWeight01 = ResolveWallTouchQualityWeight01(qualityWeight01);
            bool lowerBodyIkEnabled = enableFootPlacement;
            bool wallTouchEnabled = enableHandBracing;
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegativeScalar(deltaTime));

            RefreshPlayerStress();
            TickBreathingState(safeDeltaTime, qualityWeight01);
            TickExternalSqueezePoleState(safeDeltaTime);
            ApplyExternalSqueezePoleBias();
            CaptureSpineTargets(qualityWeight01);
            CaptureAppendageTargets();
            ApplyMuscleBulgeSignal(safeDeltaTime);
            CapturePredictiveRepairLatch(safeDeltaTime, wallTouchQualityWeight01);
            TickToolHandTransientState(safeDeltaTime);
            TickUpperArmFovCulling(safeDeltaTime);

            Vector3 rootPositionUnity = characterRoot.position;
            Quaternion rootRotationUnity = characterRoot.rotation;
            if (!IsFiniteVector(rootPositionUnity) || !IsFiniteQuaternion(rootRotationUnity))
                return false;

            float3 rootPosition = ContextualPhysicalIkMath.ToFloat3(rootPositionUnity);
            quaternion rootRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(rootRotationUnity);
            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(rootRotation, new float3(0.0f, 0.0f, 1.0f)),
                new float3(0.0f, 0.0f, 1.0f));
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(rootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(rootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            ResolveColdShiverOffsets(rootRight, rootUp, out float3 leftColdShiverOffset, out float3 rightColdShiverOffset);
            float viewerDistanceSq = 0.0f;
            bool hasFiniteViewerPose = hasViewerPosition &&
                math.all(math.isfinite(viewerPosition)) &&
                math.all(math.isfinite(viewerForward)) &&
                math.all(math.isfinite(viewerUp)) &&
                math.all(math.isfinite(viewerRight));
            if (hasFiniteViewerPose)
            {
                viewerDistanceSq = math.lengthsq(rootPosition - viewerPosition);
                viewerDistanceSq = math.select(viewerDistanceSq, 0.0f, !math.isfinite(viewerDistanceSq));
            }

            float throttleDistanceSq = viewerDistanceSq * math.lerp(LowQualityThrottleDistanceBias, 1.0f, qualityWeight01);
            ResolveThrottleState(frameIndex, _entitySlot, throttleDistanceSq, ref _stableThrottleTier, out int updateThisFrame, out byte throttleTier, out uint updateBitfield);
            entityState.IsActive = 1;
            entityState.EnableFootPlacement = lowerBodyIkEnabled ? 1 : 0;
            entityState.EnableHandBracing = enableHandBracing ? 1 : 0;
            entityState.EnableWallTouch = wallTouchEnabled ? 1 : 0;
            entityState.LeftHandEmpty = leftHandEmptyForWallTouch ? 1 : 0;
            entityState.EnableToolRetraction = enableToolRetraction ? 1 : 0;
            entityState.HasCameraPose = hasFiniteViewerPose ? 1 : 0;
            entityState.DeltaTime = safeDeltaTime;
            entityState.RootPosition = rootPosition;
            entityState.RootRotation = rootRotation;
            entityState.PelvisPosition = ReadPositionOrFallback(pelvis, entityState.RootPosition);
            entityState.LeftFootProbeOrigin = ReadPositionOrFallback(leftFootProbe, entityState.RootPosition);
            entityState.RightFootProbeOrigin = ReadPositionOrFallback(rightFootProbe, entityState.RootPosition);
            entityState.LeftHandProbeOrigin = ReadPositionOrFallback(leftHandProbe, entityState.RootPosition);
            entityState.RightHandProbeOrigin = ReadPositionOrFallback(rightHandProbe, entityState.RootPosition);
            entityState.PredictiveLeftHandPosition = SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_predictiveLeftHandPosition), rootPosition);
            entityState.PredictiveRightHandPosition = SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_predictiveRightHandPosition), rootPosition);
            entityState.PredictiveLeftHandNormal = ContextualPhysicalIkMath.SafeNormalize(SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_predictiveLeftHandNormal), new float3(0.0f, 1.0f, 0.0f)), new float3(0.0f, 1.0f, 0.0f));
            entityState.PredictiveRightHandNormal = ContextualPhysicalIkMath.SafeNormalize(SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_predictiveRightHandNormal), new float3(0.0f, 1.0f, 0.0f)), new float3(0.0f, 1.0f, 0.0f));
            entityState.CameraPosition = hasFiniteViewerPose ? viewerPosition : rootPosition;
            entityState.CameraForward = hasFiniteViewerPose ? ContextualPhysicalIkMath.SafeNormalize(viewerForward, rootForward) : rootForward;
            entityState.CameraUp = hasFiniteViewerPose ? ContextualPhysicalIkMath.SafeNormalize(viewerUp, rootUp) : rootUp;
            entityState.CameraRight = hasFiniteViewerPose ? ContextualPhysicalIkMath.SafeNormalize(viewerRight, rootRight) : rootRight;
            entityState.LeftToolRecoilOffset = SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_leftToolRecoilOffset), float3.zero);
            entityState.RightToolRecoilOffset = SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_rightToolRecoilOffset), float3.zero);
            entityState.LeftColdShiverOffset = leftColdShiverOffset;
            entityState.RightColdShiverOffset = rightColdShiverOffset;
            entityState.DashboardRightHandPosition = SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_terminalRightHandPosition), rootPosition);
            entityState.DashboardRightHandNormal = ContextualPhysicalIkMath.SafeNormalize(SanitizeFloat3Value(ContextualPhysicalIkMath.ToFloat3(_terminalRightHandNormal), new float3(0.0f, 1.0f, 0.0f)), new float3(0.0f, 1.0f, 0.0f));
            entityState.LeftLegReach = SanitizeNonNegativeScalar(_cachedLeftLegReach);
            entityState.RightLegReach = SanitizeNonNegativeScalar(_cachedRightLegReach);
            entityState.LeftArmReach = SanitizeNonNegativeScalar(_cachedLeftArmReach);
            entityState.RightArmReach = SanitizeNonNegativeScalar(_cachedRightArmReach);
            entityState.PredictiveLeftHandBlend = SanitizeUnitScalar(_predictiveLeftHandBlend);
            entityState.PredictiveRightHandBlend = SanitizeUnitScalar(_predictiveRightHandBlend);
            entityState.CameraHandLateralOffset = SanitizeNonNegativeScalar(cameraHandLateralOffset);
            entityState.CameraHandVerticalOffset = math.select(cameraHandVerticalOffset, 0.0f, !math.isfinite(cameraHandVerticalOffset));
            entityState.ToolCollisionDistance = SanitizeNonNegativeScalar(toolCollisionDistance);
            entityState.ToolRetractionBackDistance = SanitizeNonNegativeScalar(toolRetractionBackDistance);
            entityState.ToolRetractionLiftDistance = SanitizeNonNegativeScalar(toolRetractionLiftDistance);
            entityState.ToolRetractionBlend = SanitizeUnitScalar(toolRetractionBlend);
            entityState.ToolRecoilMaxOffset = SanitizeNonNegativeScalar(toolRecoilMaxOffsetMeters);
            entityState.DashboardRightHandBlend = SanitizeUnitScalar(_terminalRightHandBlend);
            entityState.ColdShiverBlend = SanitizeUnitScalar(_coldShiverBlend);
            entityState.FootContactOffset = SanitizeNonNegativeScalar(footContactOffset) * footQualityWeight01;
            entityState.HandContactOffset = SanitizeNonNegativeScalar(handContactOffset) * wallTouchQualityWeight01;
            entityState.FootProbeDistanceScale = SanitizeNonNegativeScalar(footProbeDistanceScale) * footQualityWeight01;
            entityState.HandProbeDistanceScale = SanitizeNonNegativeScalar(handProbeDistanceScale) * wallTouchQualityWeight01;
            entityState.GroundLayerMask = groundMask.value;
            entityState.WallLayerMask = wallMask.value;
            entityState.TunnelClearanceDistance = SanitizeNonNegativeScalar(tunnelClearanceDistance);
            entityState.HandBraceFadeDistance = SanitizeNonNegativeScalar(handBraceFadeDistance) * math.lerp(0.35f, 1.0f, wallTouchQualityWeight01);
            entityState.TargetPositionSharpness = SanitizeNonNegativeScalar(targetPositionSharpness);
            entityState.TargetNormalSharpness = SanitizeNonNegativeScalar(targetNormalSharpness);
            entityState.BlendFadeSharpness = SanitizeNonNegativeScalar(blendFadeSharpness);
            entityState.MaxDeltaHeight = SanitizeNonNegativeScalar(maxDeltaHeight);
            entityState.ComShiftLateralFactor = SanitizeNonNegativeScalar(comShiftLateralFactor) * footQualityWeight01;
            entityState.ComShiftForwardFactor = SanitizeNonNegativeScalar(comShiftForwardFactor) * footQualityWeight01;
            entityState.ComShiftVerticalFactor = SanitizeNonNegativeScalar(comShiftVerticalFactor) * footQualityWeight01;
            entityState.ComResponseSharpness = SanitizeNonNegativeScalar(comResponseSharpness);
            entityState.ComLeanPitchRadians = SanitizeNonNegativeScalar(math.radians(comLeanPitchDegrees));
            entityState.ComLeanRollRadians = SanitizeNonNegativeScalar(math.radians(comLeanRollDegrees));
            entityState.MaxComLateral = SanitizeNonNegativeScalar(maxComLateral);
            entityState.MaxComForward = SanitizeNonNegativeScalar(maxComForward);
            entityState.MaxComVertical = SanitizeNonNegativeScalar(maxComVertical);
            entityState.UpdateThisFrame = updateThisFrame;
            entityState.ViewerDistanceSq = viewerDistanceSq;
            entityState.UpdateBitfield = updateBitfield;
            entityState.ThrottleTier = throttleTier;
            return true;
        }

        private void TickToolHandTransientState(float deltaTime)
        {
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegativeScalar(deltaTime));
            float recoilDecay = math.rcp(1.0f + (SanitizeNonNegativeScalar(toolRecoilDecaySharpness) * safeDeltaTime));
            float recoilCap = SanitizeNonNegativeScalar(toolRecoilMaxOffsetMeters);
            _leftToolRecoilOffset = ClampVectorNoSqrt(_leftToolRecoilOffset * recoilDecay, recoilCap);
            _rightToolRecoilOffset = ClampVectorNoSqrt(_rightToolRecoilOffset * recoilDecay, recoilCap);
            TickColdShiverState(safeDeltaTime);

            if (_terminalRightHandActive)
            {
                _terminalRightHandHoldTimer = math.max(0.0f, SanitizeNonNegativeScalar(_terminalRightHandHoldTimer) - safeDeltaTime);
                if (_terminalRightHandHoldTimer <= 0.0f)
                    _terminalRightHandTargetBlend = 0.0f;
            }

            _terminalRightHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                _terminalRightHandBlend,
                _terminalRightHandActive ? _terminalRightHandTargetBlend : 0.0f,
                terminalSnapBlendSharpness,
                safeDeltaTime));

            if (_terminalRightHandActive &&
                _terminalRightHandHoldTimer <= 0.0f &&
                _terminalRightHandBlend <= 0.0001f)
            {
                _terminalRightHandActive = false;
                _terminalRightHandSourceId = 0;
            }
        }

        private void RefreshPlayerStress()
        {
            if (!SignalBus<PlayerStressSignal>.TryGetLatest(out PlayerStressSignal signal, out int sequence) ||
                sequence == _lastPlayerStressSignalSequence)
            {
                return;
            }

            uint frame = SystemDispatcher.CurrentFrameId;
            if (signal.Frame > frame)
                return;

            _lastPlayerStressSignalSequence = sequence;
            _playerStress01 = SanitizeUnitScalar(signal.Stress01);
        }

        private void TickBreathingState(float deltaTime, float qualityWeight01)
        {
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegativeScalar(deltaTime));
            float targetBlend = enableProceduralBreathing ? 1.0f : 0.0f;
            _breathingBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                _breathingBlend,
                targetBlend,
                breathingBlendSharpness,
                safeDeltaTime));

            if (_breathingBlend <= 0.0001f)
                return;

            float rate = SanitizeNonNegativeScalar(breathingBaseRateHz) + _playerStress01 * SanitizeNonNegativeScalar(breathingStressRateHz);
            rate *= math.lerp(MinimumBreathingRateWeight01, 1.0f, SanitizeUnitScalar(qualityWeight01));
            _breathingPhase = WrapPositivePhase(_breathingPhase + (rate * safeDeltaTime), BreathingPhaseWrap);
        }

        private void TickExternalSqueezePoleState(float deltaTime)
        {
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegativeScalar(deltaTime));
            _externalSqueezePoleHoldTimer = math.max(0.0f, SanitizeNonNegativeScalar(_externalSqueezePoleHoldTimer) - safeDeltaTime);
            float targetBlend = _externalSqueezePoleHoldTimer > 0.0f ? 1.0f : 0.0f;
            _externalSqueezePoleBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                _externalSqueezePoleBlend,
                targetBlend,
                predictiveRepairBlendSharpness,
                safeDeltaTime));
        }

        private void ApplyExternalSqueezePoleBias()
        {
            if (!_twoBoneSetups.IsCreated || _twoBoneSetups.Length < 4)
                return;

            float blend = SanitizeUnitScalar(_externalSqueezePoleBlend);
            ContextualPhysicalIkTwoBoneSetup leftArm = _twoBoneSetups[2];
            ContextualPhysicalIkTwoBoneSetup rightArm = _twoBoneSetups[3];
            leftArm.PoleLocalOffset = ResolveSqueezePoleLocalOffset(_baseLeftArmPoleLocalOffset, blend, 1.0f);
            rightArm.PoleLocalOffset = ResolveSqueezePoleLocalOffset(_baseRightArmPoleLocalOffset, blend, -1.0f);
            _twoBoneSetups[2] = leftArm;
            _twoBoneSetups[3] = rightArm;
        }

        private static float3 ResolveSqueezePoleLocalOffset(float3 baseOffset, float blend, float fallbackSideSign)
        {
            float safeBlend = SanitizeUnitScalar(blend);
            bool hasFiniteSideSign = math.isfinite(fallbackSideSign) && math.abs(fallbackSideSign) > 0.0001f;
            float safeFallbackSideSign = math.select(1.0f, math.sign(fallbackSideSign), hasFiniteSideSign);
            if (!math.all(math.isfinite(baseOffset)))
                baseOffset = new float3(ExternalSqueezePoleLocalMeters * safeFallbackSideSign, 0.0f, 0.0f);

            if (safeBlend <= 0.0001f)
                return baseOffset;

            float lateral = baseOffset.x;
            float lateralMagnitude = math.abs(lateral);
            float direction = lateralMagnitude > 0.0001f
                ? -math.sign(lateral)
                : -safeFallbackSideSign;
            float maxShift = lateralMagnitude > 0.0001f
                ? lateralMagnitude * 0.75f
                : ExternalSqueezePoleLocalMeters;
            float shift = direction * math.min(ExternalSqueezePoleLocalMeters * safeBlend, maxShift);
            baseOffset.x += shift;
            return baseOffset;
        }

        private void TickColdShiverState(float deltaTime)
        {
            float targetBlend = ResolveColdShiverTargetBlend();
            _coldShiverBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                _coldShiverBlend,
                targetBlend,
                coldShiverBlendSharpness,
                deltaTime));

            if (_coldShiverBlend <= 0.0001f)
                return;

            _coldShiverPhase = WrapPositivePhase(
                _coldShiverPhase + (SanitizeNonNegativeScalar(coldShiverFrequencyHz) * SanitizeNonNegativeScalar(deltaTime)),
                ColdShiverPhaseWrap);
        }

        private float ResolveColdShiverTargetBlend()
        {
            if (!enableColdShiver)
                return 0.0f;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonSurvivalSystem survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            if (survivalSystem == null)
                return 0.0f;

            float environmentTemperature = survivalSystem.EnvironmentTemperature;
            if (!math.isfinite(environmentTemperature))
                return 0.0f;

            float coldByEnvironment = SanitizeUnitScalar(
                (coldShiverTemperatureThresholdCelsius - environmentTemperature) *
                math.rcp(math.max(1.0f, coldShiverFullDeltaCelsius)));
            float coldByPhysiology = SanitizeUnitScalar(survivalSystem.ColdStressSeverity01);
            return math.max(coldByEnvironment, coldByPhysiology);
        }

        private void ResolveColdShiverOffsets(float3 rootRight, float3 rootUp, out float3 leftOffset, out float3 rightOffset)
        {
            leftOffset = float3.zero;
            rightOffset = float3.zero;

            float amplitude = SanitizeNonNegativeScalar(coldShiverAmplitudeMeters);
            float blend = SanitizeUnitScalar(_coldShiverBlend);
            _coldShiverBlend = blend;
            if (amplitude <= 0.000001f || blend <= 0.0001f)
                return;

            float phase = WrapPositivePhase(_coldShiverPhase, ColdShiverPhaseWrap);
            float leftLateral = CinematicMath.FastTriangleWaveSigned(phase) * amplitude;
            float leftVertical = CinematicMath.FastTriangleWaveSigned((phase * 1.733f) + 0.23f) * amplitude * 0.45f;
            float rightLateral = CinematicMath.FastTriangleWaveSigned(phase + 0.41f) * amplitude;
            float rightVertical = CinematicMath.FastTriangleWaveSigned((phase * 1.619f) + 0.67f) * amplitude * 0.45f;

            leftOffset = SanitizeFloat3Value((rootRight * leftLateral) + (rootUp * leftVertical), float3.zero);
            rightOffset = SanitizeFloat3Value((rootRight * -rightLateral) + (rootUp * rightVertical), float3.zero);
        }

        private void CapturePredictiveRepairLatch(float deltaTime, float wallTouchQualityWeight01)
        {
            Transform leftSource = leftControllerProbe != null ? leftControllerProbe : leftHandProbe;
            Transform rightSource = rightControllerProbe != null ? rightControllerProbe : rightHandProbe;
            Vector3 leftPosition = leftSource != null ? leftSource.position : Vector3.zero;
            Vector3 rightPosition = rightSource != null ? rightSource.position : Vector3.zero;
            bool hasFiniteLeftPosition = leftSource != null && IsFiniteVector(leftPosition);
            bool hasFiniteRightPosition = rightSource != null && IsFiniteVector(rightPosition);
            AbsoluteUniversePosition leftAup = default;
            AbsoluteUniversePosition rightAup = default;
            bool hasLeftAupProof = hasFiniteLeftPosition && TryResolveRuntimeAup(leftPosition, out leftAup);
            bool hasRightAupProof = hasFiniteRightPosition && TryResolveRuntimeAup(rightPosition, out rightAup);

            Vector3 leftVelocity = Vector3.zero;
            Vector3 rightVelocity = Vector3.zero;
            float safeDeltaTime = math.max(SanitizeNonNegativeScalar(deltaTime), 0.0001f);
            if (_hasPreviousLeftPredictiveControllerPose && hasLeftAupProof)
                leftVelocity = ResolveAupVelocity(in leftAup, in _previousLeftControllerAup, safeDeltaTime);
            if (_hasPreviousRightPredictiveControllerPose && hasRightAupProof)
                rightVelocity = ResolveAupVelocity(in rightAup, in _previousRightControllerAup, safeDeltaTime);

            if (hasLeftAupProof)
            {
                _previousLeftControllerAup = leftAup;
                _hasPreviousLeftPredictiveControllerPose = true;
            }
            else
            {
                _hasPreviousLeftPredictiveControllerPose = false;
            }

            if (hasRightAupProof)
            {
                _previousRightControllerAup = rightAup;
                _hasPreviousRightPredictiveControllerPose = true;
            }
            else
            {
                _hasPreviousRightPredictiveControllerPose = false;
            }

            if (_predictiveRepairTarget != null && enableHandBracing)
            {
                if (hasLeftAupProof)
                {
                    ResolvePredictiveRepairLatch(
                        leftSource,
                        true,
                        leftPosition,
                        in leftAup,
                        leftVelocity,
                        safeDeltaTime,
                        ref _predictiveLeftHandPosition,
                        ref _predictiveLeftHandNormal,
                        ref _predictiveLeftHandBlend);
                }
                else
                {
                    _predictiveLeftHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                        _predictiveLeftHandBlend,
                        0.0f,
                        predictiveRepairBlendSharpness,
                        safeDeltaTime));
                }

                if (hasRightAupProof)
                {
                    ResolvePredictiveRepairLatch(
                        rightSource,
                        false,
                        rightPosition,
                        in rightAup,
                        rightVelocity,
                        safeDeltaTime,
                        ref _predictiveRightHandPosition,
                        ref _predictiveRightHandNormal,
                        ref _predictiveRightHandBlend);
                }
                else
                {
                    _predictiveRightHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                        _predictiveRightHandBlend,
                        0.0f,
                        predictiveRepairBlendSharpness,
                        safeDeltaTime));
                }
            }
            else
            {
                _predictiveLeftHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _predictiveLeftHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    safeDeltaTime));
                _predictiveRightHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _predictiveRightHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    safeDeltaTime));
            }

            float wallTouchWeight = SanitizeUnitScalar(wallTouchQualityWeight01);
            _predictiveLeftHandBlend = SanitizeUnitScalar(_predictiveLeftHandBlend * wallTouchWeight);
            _predictiveRightHandBlend = SanitizeUnitScalar(_predictiveRightHandBlend * wallTouchWeight);
            ApplyExternalWallHandTargetsToPredictiveLatch(safeDeltaTime, wallTouchWeight);
        }

        private void ApplyExternalWallHandTargetsToPredictiveLatch(float deltaTime, float wallTouchQualityWeight01)
        {
            float wallTouchWeight = SanitizeUnitScalar(wallTouchQualityWeight01);
            _externalWallLeftHandHoldTimer = SanitizeNonNegativeScalar(_externalWallLeftHandHoldTimer);
            _externalWallRightHandHoldTimer = SanitizeNonNegativeScalar(_externalWallRightHandHoldTimer);
            _externalWallLeftHandBlend = SanitizeUnitScalar(_externalWallLeftHandBlend) * wallTouchWeight;
            _externalWallRightHandBlend = SanitizeUnitScalar(_externalWallRightHandBlend) * wallTouchWeight;

            bool hasExternalWallTargets =
                _externalWallLeftHandHoldTimer > 0.0f ||
                _externalWallRightHandHoldTimer > 0.0f;
            if (wallTouchWeight <= 0.0001f && !hasExternalWallTargets)
            {
                _externalWallLeftHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _externalWallLeftHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    deltaTime));
                _externalWallRightHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _externalWallRightHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    deltaTime));
                _externalWallLeftHandHoldTimer = 0.0f;
                _externalWallRightHandHoldTimer = 0.0f;
                return;
            }

            if (_externalWallLeftHandHoldTimer <= 0.0f || !leftHandEmptyForWallTouch)
            {
                _externalWallLeftHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _externalWallLeftHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    deltaTime));
                _externalWallLeftHandHoldTimer = 0.0f;
            }
            else
            {
                _externalWallLeftHandHoldTimer = math.max(0.0f, _externalWallLeftHandHoldTimer - SanitizeNonNegativeScalar(deltaTime));
                if (_externalWallLeftHandBlend > _predictiveLeftHandBlend && IsFiniteVector(_externalWallLeftHandPosition))
                {
                    _predictiveLeftHandPosition = _externalWallLeftHandPosition;
                    _predictiveLeftHandNormal = NormalizeVectorNoSqrt(_externalWallLeftHandNormal, Vector3.up);
                    _predictiveLeftHandBlend = _externalWallLeftHandBlend;
                }
            }

            if (_externalWallRightHandHoldTimer <= 0.0f)
            {
                _externalWallRightHandBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(
                    _externalWallRightHandBlend,
                    0.0f,
                    predictiveRepairBlendSharpness,
                    deltaTime));
            }
            else
            {
                _externalWallRightHandHoldTimer = math.max(0.0f, _externalWallRightHandHoldTimer - SanitizeNonNegativeScalar(deltaTime));
                if (_externalWallRightHandBlend > _predictiveRightHandBlend && IsFiniteVector(_externalWallRightHandPosition))
                {
                    _predictiveRightHandPosition = _externalWallRightHandPosition;
                    _predictiveRightHandNormal = NormalizeVectorNoSqrt(_externalWallRightHandNormal, Vector3.up);
                    _predictiveRightHandBlend = _externalWallRightHandBlend;
                }
            }
        }

        private void ResolvePredictiveRepairLatch(
            Transform controllerSource,
            bool isLeftHand,
            Vector3 controllerPosition,
            in AbsoluteUniversePosition controllerAup,
            Vector3 controllerVelocity,
            float deltaTime,
            ref Vector3 predictivePosition,
            ref Vector3 predictiveNormal,
            ref float predictiveBlend)
        {
            if (controllerSource == null || _predictiveRepairTarget == null || !IsFiniteVector(controllerVelocity))
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            if (!IsFiniteVector(controllerPosition))
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            if (!_predictiveRepairTarget.TryResolveRepairSnapPoints(
                    controllerPosition,
                    out AbsoluteUniversePosition leftHandAup,
                    out AbsoluteUniversePosition rightHandAup,
                    out _))
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            AbsoluteUniversePosition targetAup = isLeftHand ? leftHandAup : rightHandAup;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in controllerAup, in targetAup);
            float distanceSqFloat = (float)distanceSq;
            if (!math.isfinite(distanceSqFloat) || distanceSqFloat > PredictiveRepairLatchDistanceSq)
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            float3 targetRuntime = targetAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(targetRuntime)))
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            float3 controllerRuntime = ContextualPhysicalIkMath.ToFloat3(controllerPosition);
            float3 targetVector = targetRuntime - controllerRuntime;
            float3 targetDirection = ContextualPhysicalIkMath.SafeNormalize(targetVector, new float3(0.0f, 0.0f, 1.0f));
            float3 velocityDirection = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(controllerVelocity), float3.zero);
            float directionDot = math.dot(velocityDirection, targetDirection);
            float requiredDot = SanitizeUnitScalar(predictiveRepairDirectionDot);
            if (directionDot < requiredDot)
            {
                predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime));
                return;
            }

            Vector3 fallbackNormal = (Vector3)ContextualPhysicalIkMath.SafeNormalize(controllerRuntime - targetRuntime, new float3(0.0f, 1.0f, 0.0f));

            float range01 = SanitizeUnitScalar(1.0f - (distanceSqFloat * math.rcp(PredictiveRepairLatchDistanceSq)));
            float direction01 = SanitizeUnitScalar((directionDot - requiredDot) * math.rcp(math.max(1.0f - requiredDot, 0.0001f)));
            float targetBlend = range01 * direction01;
            predictivePosition = (Vector3)targetRuntime;
            predictiveNormal = fallbackNormal;
            predictiveBlend = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, targetBlend, predictiveRepairBlendSharpness, deltaTime));
        }

        private static Vector3 ResolveAupVelocity(
            in AbsoluteUniversePosition currentAup,
            in AbsoluteUniversePosition previousAup,
            float deltaTime)
        {
            float safeDeltaTime = math.max(SanitizeNonNegativeScalar(deltaTime), 0.0001f);
            float3 currentRuntime = currentAup.ToRuntimeFloat3();
            float3 previousRuntime = previousAup.ToRuntimeFloat3();
            float3 velocity = (currentRuntime - previousRuntime) * math.rcp(safeDeltaTime);
            return math.all(math.isfinite(velocity)) ? (Vector3)velocity : Vector3.zero;
        }

        private void TickUpperArmFovCulling(float deltaTime)
        {
            if (!enableUpperArmFovCulling || upperArmRenderers == null || upperArmRenderers.Length == 0)
            {
                if (!_upperArmRenderersVisible)
                    SetUpperArmRenderersVisible(true);
                _upperArmCullTimer = 0.0f;
                return;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            Transform cameraTransform = playerCamera != null ? playerCamera.transform : null;
            if (cameraTransform == null)
            {
                if (!_upperArmRenderersVisible)
                    SetUpperArmRenderersVisible(true);
                _upperArmCullTimer = 0.0f;
                return;
            }

            bool visible = IsAnyUpperArmRendererInViewCone(cameraTransform);
            if (visible)
            {
                _upperArmCullTimer = 0.0f;
                if (!_upperArmRenderersVisible)
                    SetUpperArmRenderersVisible(true);
                return;
            }

            _upperArmCullTimer += SanitizeNonNegativeScalar(deltaTime);
            if (_upperArmCullTimer >= math.max(0.01f, SanitizeNonNegativeScalar(upperArmCullHysteresisSeconds)) && _upperArmRenderersVisible)
                SetUpperArmRenderersVisible(false);
        }

        private bool IsAnyUpperArmRendererInViewCone(Transform cameraTransform)
        {
            float3 cameraPosition = ContextualPhysicalIkMath.ToFloat3(cameraTransform.position);
            float3 cameraForward = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(cameraTransform.forward),
                new float3(0.0f, 0.0f, 1.0f));
            if (!math.all(math.isfinite(cameraPosition)))
                return true;

            float minimumForwardDot = SanitizeUnitScalar(upperArmFovDotThreshold);
            float minimumForwardDotSq = minimumForwardDot * minimumForwardDot;
            for (int i = 0; i < upperArmRenderers.Length; i++)
            {
                Renderer renderer = upperArmRenderers[i];
                if (renderer == null)
                    continue;

                float3 direction = ContextualPhysicalIkMath.ToFloat3(renderer.bounds.center) - cameraPosition;
                float distanceSq = math.lengthsq(direction);
                if (!math.isfinite(distanceSq))
                    return true;

                if (distanceSq <= UpperArmVisibilityProxyRadiusSq)
                    return true;

                float forwardDot = math.dot(cameraForward, direction);
                if (forwardDot > 0.01f &&
                    forwardDot * forwardDot >= minimumForwardDotSq * distanceSq)
                    return true;
            }

            return false;
        }

        private void SetUpperArmRenderersVisible(bool visible)
        {
            if (upperArmRenderers == null)
                return;

            for (int i = 0; i < upperArmRenderers.Length; i++)
            {
                Renderer renderer = upperArmRenderers[i];
                if (renderer != null)
                    renderer.enabled = visible;
            }

            _upperArmRenderersVisible = visible;
        }

        private bool EnsureRuntimeInitialized()
        {
            if (_runtimeInitialized)
                return true;

            if (animator == null || characterRoot == null || pelvis == null)
                return false;

            int validAppendageChainCount = CountValidAppendageChains(
                out int totalAppendageHandleCount,
                out int totalAppendageLengthCount,
                out int totalAppendageScratchCount);
            bool hasValidSpineChain = TryGetValidSpineChain(out SpineChainAuthoring validSpineChain);
            int validSecondaryChainCount = CountValidSecondaryChains(
                out int totalSecondaryHandleCount,
                out int totalSecondaryStateCount);
            int spineHandleCount = hasValidSpineChain ? 1 + validSpineChain.bones.Length : 0;
            _spineHandleStartIndex = BaseHandleCount + validAppendageChainCount + totalAppendageHandleCount;
            _secondaryHandleStartIndex = _spineHandleStartIndex + spineHandleCount;
            int totalHandleCount = _secondaryHandleStartIndex + totalSecondaryHandleCount;

            _nativeBuffers.Allocate(
                totalHandleCount,
                validAppendageChainCount,
                totalAppendageLengthCount,
                totalAppendageScratchCount,
                hasValidSpineChain,
                validSecondaryChainCount,
                totalSecondaryStateCount);

            if (validAppendageChainCount > 0)
            {
                _appendageTargetSources = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage target source cache - owner: ContextualPhysicalIkRig
                _appendageFallbackTips = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage fallback tip cache - owner: ContextualPhysicalIkRig
                _appendageVoxelVolumes = new HectonVoxelVolume[validAppendageChainCount]; // COLD ALLOC: HectonVoxelVolume[dynamic] - appendage voxel snap owners - owner: ContextualPhysicalIkRig
                _appendageSurfaceNormalSources = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage wall-normal source cache - owner: ContextualPhysicalIkRig
            }
            BindCoreHandles();
            BuildTwoBoneSetups();
            CacheCoreReachLengths();
            BuildAppendageRuntimeData();
            BuildSpineRuntimeData();
            BuildSecondaryRuntimeData();
            TryInitializeMuscleBulgeMaterial();
            animator.ResolveAllStreamHandles();
            _runtimeInitialized = true;
            return true;
        }

        private void BindCoreHandles()
        {
            _streamHandles[PelvisHandleIndex] = BindStreamHandle(pelvis);
            _streamHandles[LeftLegUpperHandleIndex] = BindStreamHandle(leftUpperLeg);
            _streamHandles[LeftLegLowerHandleIndex] = BindStreamHandle(leftLowerLeg);
            _streamHandles[LeftFootHandleIndex] = BindStreamHandle(leftFoot);
            _streamHandles[RightLegUpperHandleIndex] = BindStreamHandle(rightUpperLeg);
            _streamHandles[RightLegLowerHandleIndex] = BindStreamHandle(rightLowerLeg);
            _streamHandles[RightFootHandleIndex] = BindStreamHandle(rightFoot);
            _streamHandles[LeftArmParentHandleIndex] = BindStreamHandle(leftArmParent);
            _streamHandles[LeftArmUpperHandleIndex] = BindStreamHandle(leftUpperArm);
            _streamHandles[LeftArmLowerHandleIndex] = BindStreamHandle(leftLowerArm);
            _streamHandles[LeftHandHandleIndex] = BindStreamHandle(leftHand);
            _streamHandles[RightArmParentHandleIndex] = BindStreamHandle(rightArmParent);
            _streamHandles[RightArmUpperHandleIndex] = BindStreamHandle(rightUpperArm);
            _streamHandles[RightArmLowerHandleIndex] = BindStreamHandle(rightLowerArm);
            _streamHandles[RightHandHandleIndex] = BindStreamHandle(rightHand);
        }

        private void BuildTwoBoneSetups()
        {
            _twoBoneSetups[0] = BuildTwoBoneSetup(
                pelvis,
                leftUpperLeg,
                leftLowerLeg,
                leftFoot,
                leftKneeHint,
                PelvisHandleIndex,
                LeftLegUpperHandleIndex,
                LeftLegLowerHandleIndex,
                LeftFootHandleIndex,
                0,
                footLimbBlend);

            _twoBoneSetups[1] = BuildTwoBoneSetup(
                pelvis,
                rightUpperLeg,
                rightLowerLeg,
                rightFoot,
                rightKneeHint,
                PelvisHandleIndex,
                RightLegUpperHandleIndex,
                RightLegLowerHandleIndex,
                RightFootHandleIndex,
                1,
                footLimbBlend);

            _twoBoneSetups[2] = BuildTwoBoneSetup(
                leftArmParent,
                leftUpperArm,
                leftLowerArm,
                leftHand,
                leftElbowHint,
                LeftArmParentHandleIndex,
                LeftArmUpperHandleIndex,
                LeftArmLowerHandleIndex,
                LeftHandHandleIndex,
                2,
                handLimbBlend);

            _twoBoneSetups[3] = BuildTwoBoneSetup(
                rightArmParent,
                rightUpperArm,
                rightLowerArm,
                rightHand,
                rightElbowHint,
                RightArmParentHandleIndex,
                RightArmUpperHandleIndex,
                RightArmLowerHandleIndex,
                RightHandHandleIndex,
                3,
                handLimbBlend);

            _baseLeftArmPoleLocalOffset = _twoBoneSetups[2].PoleLocalOffset;
            _baseRightArmPoleLocalOffset = _twoBoneSetups[3].PoleLocalOffset;
        }

        private void CacheCoreReachLengths()
        {
            _cachedLeftLegReach = ComputeReach(leftUpperLeg, leftLowerLeg, leftFoot);
            _cachedRightLegReach = ComputeReach(rightUpperLeg, rightLowerLeg, rightFoot);
            _cachedLeftArmReach = ComputeReach(leftUpperArm, leftLowerArm, leftHand);
            _cachedRightArmReach = ComputeReach(rightUpperArm, rightLowerArm, rightHand);
        }

        private void BuildAppendageRuntimeData()
        {
            if (!_appendageChainRuntimes.IsCreated || appendageChains == null)
                return;

            int runtimeChainIndex = 0;
            int handleWriteIndex = BaseHandleCount;
            int lengthWriteIndex = 0;
            int scratchWriteIndex = 0;

            for (int authoringIndex = 0; authoringIndex < appendageChains.Length; authoringIndex++)
            {
                AppendageChainAuthoring authoring = appendageChains[authoringIndex];
                if (!IsValidAppendageChain(authoring))
                    continue;

                Transform parentTransform = authoring.parentTransform != null
                    ? authoring.parentTransform
                    : authoring.bones[0].parent != null
                        ? authoring.bones[0].parent
                        : characterRoot;
                _streamHandles[handleWriteIndex] = BindStreamHandle(parentTransform);
                int parentHandleIndex = handleWriteIndex;
                handleWriteIndex++;

                int firstBoneHandleIndex = handleWriteIndex;
                for (int boneIndex = 0; boneIndex < authoring.bones.Length; boneIndex++)
                {
                    _streamHandles[handleWriteIndex] = BindStreamHandle(authoring.bones[boneIndex]);
                    handleWriteIndex++;
                }

                int firstLengthIndex = lengthWriteIndex;
                for (int boneIndex = 0; boneIndex < authoring.bones.Length - 1; boneIndex++)
                {
                    _appendageSegmentLengths[lengthWriteIndex] = ComputeLength(authoring.bones[boneIndex], authoring.bones[boneIndex + 1]);
                    lengthWriteIndex++;
                }

                _appendageChainRuntimes[runtimeChainIndex] = new ContextualPhysicalIkAppendageChainRuntime
                {
                    ParentHandleIndex = parentHandleIndex,
                    FirstBoneHandleIndex = firstBoneHandleIndex,
                    BoneCount = authoring.bones.Length,
                    FirstLengthIndex = firstLengthIndex,
                    FirstScratchIndex = scratchWriteIndex,
                    TargetIndex = runtimeChainIndex,
                    Iterations = math.clamp(authoring.iterations, 1, MaxAppendageIterations),
                    Tolerance = math.max(0.0001f, SanitizeNonNegativeScalar(authoring.tolerance)),
                    Blend = SanitizeUnitScalar(authoring.blend),
                    PoleLocalOffset = ComputeLocalPoleOffset(parentTransform, authoring.poleHint, authoring.bones[0]),
                };

                _appendageTargetSources[runtimeChainIndex] = authoring.targetTransform;
                _appendageFallbackTips[runtimeChainIndex] = authoring.bones[authoring.bones.Length - 1];
                _appendageVoxelVolumes[runtimeChainIndex] = authoring.snapTargetToVoxelCorner ? authoring.voxelVolume : null;
                _appendageSurfaceNormalSources[runtimeChainIndex] = authoring.surfaceNormalSource != null
                    ? authoring.surfaceNormalSource
                    : authoring.targetTransform != null
                        ? authoring.targetTransform
                        : authoring.bones[authoring.bones.Length - 1];
                scratchWriteIndex += authoring.bones.Length;
                runtimeChainIndex++;
            }
        }

        private void BuildSpineRuntimeData()
        {
            if (!_spineChainRuntimes.IsCreated)
                return;

            if (!TryGetValidSpineChain(out SpineChainAuthoring validSpineChain))
                return;

            Transform parentTransform = validSpineChain.parentTransform != null
                ? validSpineChain.parentTransform
                : validSpineChain.bones[0].parent != null
                    ? validSpineChain.bones[0].parent
                    : characterRoot;

            _streamHandles[_spineHandleStartIndex] = BindStreamHandle(parentTransform);
            for (int boneIndex = 0; boneIndex < validSpineChain.bones.Length; boneIndex++)
                _streamHandles[_spineHandleStartIndex + boneIndex + 1] = BindStreamHandle(validSpineChain.bones[boneIndex]);

            _spineChainRuntimes[0] = new ContextualPhysicalIkSpineChainRuntime
            {
                ParentHandleIndex = _spineHandleStartIndex,
                FirstBoneHandleIndex = _spineHandleStartIndex + 1,
                BoneCount = validSpineChain.bones.Length,
                TargetStartIndex = 0,
                Blend = SanitizeUnitScalar(validSpineChain.blend),
            };
        }

        private void BuildSecondaryRuntimeData()
        {
            if (!_secondaryChainRuntimes.IsCreated || secondaryChains == null)
                return;

            int runtimeChainIndex = 0;
            int handleWriteIndex = _secondaryHandleStartIndex;
            int stateWriteIndex = 0;

            for (int authoringIndex = 0; authoringIndex < secondaryChains.Length; authoringIndex++)
            {
                SecondaryChainAuthoring authoring = secondaryChains[authoringIndex];
                if (!IsValidSecondaryChain(authoring))
                    continue;

                Transform parentTransform = authoring.parentTransform != null
                    ? authoring.parentTransform
                    : authoring.bones[0].parent != null
                        ? authoring.bones[0].parent
                        : characterRoot;

                _streamHandles[handleWriteIndex] = BindStreamHandle(parentTransform);
                int parentHandleIndex = handleWriteIndex;
                handleWriteIndex++;

                int firstBoneHandleIndex = handleWriteIndex;
                for (int boneIndex = 0; boneIndex < authoring.bones.Length; boneIndex++)
                {
                    _streamHandles[handleWriteIndex] = BindStreamHandle(authoring.bones[boneIndex]);
                    Vector3 initialPosition = authoring.bones[boneIndex].position;
                    _secondaryStates[stateWriteIndex + boneIndex] = new ContextualPhysicalIkSecondaryState
                    {
                        Position = IsFiniteVector(initialPosition)
                            ? ContextualPhysicalIkMath.ToFloat3(initialPosition)
                            : float3.zero,
                        Velocity = float3.zero,
                    };
                    handleWriteIndex++;
                }

                _secondaryChainRuntimes[runtimeChainIndex] = new ContextualPhysicalIkSecondaryChainRuntime
                {
                    ParentHandleIndex = parentHandleIndex,
                    FirstBoneHandleIndex = firstBoneHandleIndex,
                    BoneCount = authoring.bones.Length,
                    FirstStateIndex = stateWriteIndex,
                    Stiffness = SanitizeNonNegativeScalar(authoring.stiffness),
                    Damping = SanitizeNonNegativeScalar(authoring.damping),
                    Blend = SanitizeUnitScalar(authoring.blend),
                };

                stateWriteIndex += authoring.bones.Length;
                runtimeChainIndex++;
            }
        }

        private bool TryInitializeAnimationInjection()
        {
            if (_animationInjected)
                return true;

            if (!EnsureRuntimeInitialized())
                return false;

            if (animator == null)
                return false;

            PlayableGraph graph = animator.playableGraph;
            if (!graph.IsValid() || graph.GetOutputCount() <= 0)
                return false;

            PlayableOutput output = graph.GetOutput(0);
            if (!output.IsOutputValid())
                return false;

            Playable sourcePlayable = output.GetSourcePlayable();
            if (!sourcePlayable.IsValid())
            {
                if (graph.GetRootPlayableCount() <= 0)
                    return false;

                sourcePlayable = graph.GetRootPlayable(0);
                if (!sourcePlayable.IsValid())
                    return false;
            }

            ContextualPhysicalIkApplyJob job = BuildApplyJob();
            AnimationScriptPlayable ikPlayable = AnimationScriptPlayable.Create(graph, job, 1);
            ikPlayable.SetProcessInputs(true);

            if (!graph.Connect(sourcePlayable, 0, ikPlayable, 0))
            {
                graph.DestroyPlayable(ikPlayable);
                return false;
            }

            ikPlayable.SetInputWeight(0, 1.0f);
            output.SetSourcePlayable(ikPlayable);

            _graph = graph;
            _wrappedOutput = output;
            _wrappedSourcePlayable = sourcePlayable;
            _ikPlayable = ikPlayable;
            _animationInjected = true;
            return true;
        }

        private ContextualPhysicalIkApplyJob BuildApplyJob()
        {
            return new ContextualPhysicalIkApplyJob
            {
                TargetFrames = _currentTargetFrames,
                StreamHandles = _streamHandles,
                TwoBoneSetups = _twoBoneSetups,
                AppendageChains = _appendageChainRuntimes,
                AppendageSegmentLengths = _appendageSegmentLengths,
                AppendageTargets = _appendageTargets,
                AppendageScratchPositions = _appendageScratchPositions,
                SpineChains = _spineChainRuntimes,
                SpineTargets = _spineTargets,
                SecondaryChains = _secondaryChainRuntimes,
                SecondaryStates = _secondaryStates,
                CachedLocalPoseStates = _cachedLocalPoseStates,
                MuscleBulgeOutput = _muscleBulgeOutput,
                EntityIndex = _entitySlot,
                PelvisPositionBlend = SanitizeUnitScalar(pelvisPositionBlend),
                PelvisRotationBlend = SanitizeUnitScalar(pelvisRotationBlend),
                OverExtensionResistanceRadians = SanitizeNonNegativeScalar(math.radians(overExtensionResistanceDegrees)),
            };
        }

        private void UpdateJobDataTargetFrames()
        {
            if (!_animationInjected || !_ikPlayable.IsValid())
                return;

            ContextualPhysicalIkApplyJob job = _ikPlayable.GetJobData<ContextualPhysicalIkApplyJob>();
            job.TargetFrames = _currentTargetFrames;
            job.EntityIndex = _entitySlot;
            job.PelvisPositionBlend = SanitizeUnitScalar(pelvisPositionBlend);
            job.PelvisRotationBlend = SanitizeUnitScalar(pelvisRotationBlend);
            job.OverExtensionResistanceRadians = SanitizeNonNegativeScalar(math.radians(overExtensionResistanceDegrees));
            _ikPlayable.SetJobData(job);
        }

        private void CaptureSpineTargets(float qualityWeight01)
        {
            if (!_spineChainRuntimes.IsCreated || !_spineTargets.IsCreated || _spineTargets.Length < SpineTargetCountPerChain)
                return;

            if (!TryGetValidSpineChain(out SpineChainAuthoring validSpineChain))
                return;

            Transform headSource = validSpineChain.headTarget != null
                ? validSpineChain.headTarget
                : validSpineChain.bones[validSpineChain.bones.Length - 1];
            Vector3 headPosition = headSource.position;
            if (!IsFiniteVector(headPosition))
                return;

            if (!TryResolveRuntimeAup(headPosition, out AbsoluteUniversePosition headAup))
                return;

            double3 headAbsolute = headAup.ToAbsoluteDouble3();
            Quaternion hmdRotation = IsFiniteQuaternion(headSource.rotation) ? headSource.rotation : Quaternion.identity;

            AbsoluteUniversePosition chestAup = OffsetAupLocal(in headAbsolute, hmdRotation, HeadToChestSocketLocalOffset);
            AbsoluteUniversePosition forwardAup = OffsetAupLocal(in headAbsolute, hmdRotation, HeadForwardReferenceLocalOffset);

            float3 chestTarget = chestAup.ToRuntimeFloat3();
            float3 headTarget = headAup.ToRuntimeFloat3();
            float3 forwardTarget = forwardAup.ToRuntimeFloat3();
            if (_breathingBlend > 0.0001f && enableProceduralBreathing)
            {
                quaternion hmdMathematicsRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(hmdRotation);
                float3 hmdUp = ContextualPhysicalIkMath.SafeNormalize(
                    math.mul(hmdMathematicsRotation, new float3(0.0f, 1.0f, 0.0f)),
                    new float3(0.0f, 1.0f, 0.0f));
                float3 hmdRight = ContextualPhysicalIkMath.SafeNormalize(
                    math.mul(hmdMathematicsRotation, new float3(1.0f, 0.0f, 0.0f)),
                    new float3(1.0f, 0.0f, 0.0f));
                float quality = SanitizeUnitScalar(qualityWeight01);
                float cheapWave = CinematicMath.FastTriangleWaveSigned(_breathingPhase);
                float smoothWave = CinematicMath.FastSin(_breathingPhase * 6.28318530718f);
                float wave = math.lerp(cheapWave, smoothWave, quality);
                float amplitude = SanitizeNonNegativeScalar(breathingAmplitudeMeters) * _breathingBlend * (0.45f + _playerStress01 * 0.55f) * math.lerp(0.65f, 1.0f, quality);
                float jitter = CinematicMath.FastTriangleWaveSigned((_breathingPhase * 3.17f) + 0.19f) *
                    SanitizeNonNegativeScalar(breathingStressJitterMeters) *
                    _playerStress01 *
                    _breathingBlend *
                    math.lerp(0.5f, 1.0f, quality);
                float3 breathOffset = hmdUp * (wave * amplitude) + hmdRight * jitter;
                chestTarget += breathOffset;
                headTarget += breathOffset * 0.35f;
                forwardTarget += breathOffset * 0.2f;
            }

            float3 fallbackHeadTarget = ContextualPhysicalIkMath.ToFloat3(headPosition);
            chestTarget = SanitizeFloat3Value(chestTarget, fallbackHeadTarget);
            headTarget = SanitizeFloat3Value(headTarget, fallbackHeadTarget);
            forwardTarget = SanitizeFloat3Value(forwardTarget, headTarget);
            _spineTargets[0] = chestTarget;
            _spineTargets[1] = headTarget;
            _spineTargets[2] = forwardTarget;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in double3 originAbsolute, Quaternion hmdRotation, float3 localOffset)
        {
            float3 yawOffset = RotateByHmdYawNoTrig(hmdRotation, localOffset);
            double3 resolvedAbsolute = originAbsolute + new double3(yawOffset.x, yawOffset.y, yawOffset.z);
            return AbsoluteUniversePosition.FromAbsolutePosition(resolvedAbsolute);
        }

        private static float3 RotateByHmdYawNoTrig(Quaternion hmdRotation, float3 localOffset)
        {
            float4 q = new float4(hmdRotation.x, hmdRotation.y, hmdRotation.z, hmdRotation.w);
            if (!math.all(math.isfinite(q)))
                return localOffset;

            float yawSin = 2.0f * ((q.x * q.z) + (q.w * q.y));
            float yawCos = 1.0f - (2.0f * ((q.x * q.x) + (q.y * q.y)));
            float lenSq = math.max((yawSin * yawSin) + (yawCos * yawCos), 0.000001f);
            float invLenApprox = 1.5f - (0.5f * lenSq);
            yawSin *= invLenApprox;
            yawCos *= invLenApprox;

            return new float3(
                (yawCos * localOffset.x) + (yawSin * localOffset.z),
                localOffset.y,
                (-yawSin * localOffset.x) + (yawCos * localOffset.z));
        }

        private void CaptureAppendageTargets()
        {
            if (!_appendageChainRuntimes.IsCreated || !_appendageTargets.IsCreated || _appendageTargets.Length <= 0)
                return;

            int chainCount = math.min(_appendageChainRuntimes.Length, _appendageTargets.Length);
            for (int chainIndex = 0; chainIndex < chainCount; chainIndex++)
            {
                Transform targetSource = _appendageTargetSources != null && chainIndex < _appendageTargetSources.Length
                    ? _appendageTargetSources[chainIndex]
                    : null;
                Transform fallbackTip = _appendageFallbackTips != null && chainIndex < _appendageFallbackTips.Length
                    ? _appendageFallbackTips[chainIndex]
                    : null;
                ContextualPhysicalIkAppendageChainRuntime runtime = _appendageChainRuntimes[chainIndex];
                Vector3 targetPosition;
                float weight;

                if (targetSource != null)
                {
                    targetPosition = targetSource.position;
                    weight = runtime.Blend;
                }
                else if (fallbackTip != null)
                {
                    targetPosition = fallbackTip.position;
                    weight = 0.0f;
                }
                else
                {
                    _appendageTargets[chainIndex] = default;
                    continue;
                }

                HectonVoxelVolume voxelVolume = _appendageVoxelVolumes != null && chainIndex < _appendageVoxelVolumes.Length
                    ? _appendageVoxelVolumes[chainIndex]
                    : null;
                if (voxelVolume != null)
                {
                    Transform normalSource = _appendageSurfaceNormalSources != null && chainIndex < _appendageSurfaceNormalSources.Length
                        ? _appendageSurfaceNormalSources[chainIndex]
                        : null;
                    Vector3 targetNormal = normalSource != null && IsFiniteVector(normalSource.up)
                        ? NormalizeVectorNoSqrt(normalSource.up, Vector3.up)
                        : Vector3.up;
                    if (IsFiniteVector(targetPosition) &&
                        voxelVolume.TryGetNearestCorner(targetPosition, targetNormal, out Vector3 snappedCorner) &&
                        IsFiniteVector(snappedCorner))
                    {
                        targetPosition = snappedCorner;
                    }
                }

                bool hasFiniteTargetPosition = IsFiniteVector(targetPosition);
                float3 safePosition = hasFiniteTargetPosition
                    ? ContextualPhysicalIkMath.ToFloat3(targetPosition)
                    : float3.zero;
                _appendageTargets[chainIndex] = new ContextualPhysicalIkAppendageTarget
                {
                    Position = safePosition,
                    Weight = hasFiniteTargetPosition ? SanitizeUnitScalar(weight) : 0.0f,
                };
            }
        }

        private void ApplyMuscleBulgeSignal(float deltaTime)
        {
            if (!_muscleBulgeOutput.IsCreated ||
                _muscleBulgeOutput.Length <= 0 ||
                !_muscleBulgeMaterialInitialized ||
                _muscleBulgeMaterialInstance == null)
                return;

            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegativeScalar(deltaTime));
            float targetBulge = SanitizeUnitScalar(_muscleBulgeOutput[0] * muscleBulgeScale);
            _muscleBulgeCurrent = SanitizeUnitScalar(ContextualPhysicalIkMath.SmoothScalar(_muscleBulgeCurrent, targetBulge, muscleBulgeSharpness, safeDeltaTime));
            _muscleBulgeMaterialInstance.SetFloat(MuscleBulgeShaderId, _muscleBulgeCurrent);
        }

        private void TryRegisterWithRuntime()
        {
            if (_registered)
                return;

            _runtime = ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            if (_runtime == null)
                return;

            if (!_runtime.RegisterRig(this, out int entitySlot))
                return;

            _registered = true;
            AssignEntitySlot(entitySlot, _runtime.CurrentTargetFrames);
        }

        private void TryUnregisterFromRuntime()
        {
            if (!_registered || _runtime == null)
                return;

            _runtime.UnregisterRig(this, _entitySlot);
            _registered = false;
            _entitySlot = -1;
            _runtime = null;
        }

        private void TearDownAnimationInjection()
        {
            if (!_animationInjected)
                return;

            if (_graph.IsValid())
            {
                if (_wrappedOutput.IsOutputValid() && _wrappedSourcePlayable.IsValid())
                    _wrappedOutput.SetSourcePlayable(_wrappedSourcePlayable);

                if (_ikPlayable.IsValid())
                {
                    _graph.Disconnect(_ikPlayable, 0);
                    _graph.DestroyPlayable(_ikPlayable);
                }
            }

            _graph = default;
            _wrappedOutput = default;
            _wrappedSourcePlayable = default;
            _ikPlayable = default;
            _animationInjected = false;
        }

        private void DisposeRuntimeArrays()
        {
            Exception disposeException = null;
            try
            {
                _nativeBuffers.Dispose();
            }
            catch (Exception exception)
            {
                disposeException = exception;
            }
            finally
            {
                _appendageTargetSources = null;
                _appendageFallbackTips = null;
                _appendageVoxelVolumes = null;
                _appendageSurfaceNormalSources = null;
                _spineHandleStartIndex = BaseHandleCount;
                _secondaryHandleStartIndex = BaseHandleCount;
                _cachedLeftLegReach = 0.0f;
                _cachedRightLegReach = 0.0f;
                _cachedLeftArmReach = 0.0f;
                _cachedRightArmReach = 0.0f;
                _baseLeftArmPoleLocalOffset = float3.zero;
                _baseRightArmPoleLocalOffset = float3.zero;
                _predictiveLeftHandBlend = 0.0f;
                _predictiveRightHandBlend = 0.0f;
                _leftToolRecoilOffset = Vector3.zero;
                _rightToolRecoilOffset = Vector3.zero;
                _terminalRightHandBlend = 0.0f;
                _terminalRightHandTargetBlend = 0.0f;
                _terminalRightHandHoldTimer = 0.0f;
                _terminalRightHandActive = false;
                _externalWallLeftHandBlend = 0.0f;
                _externalWallRightHandBlend = 0.0f;
                _externalWallLeftHandHoldTimer = 0.0f;
                _externalWallRightHandHoldTimer = 0.0f;
                _breathingBlend = 0.0f;
                _breathingPhase = 0.0f;
                _playerStress01 = 0.0f;
                _externalSqueezePoleBlend = 0.0f;
                _externalSqueezePoleHoldTimer = 0.0f;
                _lastPlayerStressSignalSequence = 0;
                _hasPreviousLeftPredictiveControllerPose = false;
                _hasPreviousRightPredictiveControllerPose = false;
                ReleaseMuscleBulgeMaterial();
                _runtimeInitialized = false;
            }

            if (disposeException != null)
                throw disposeException;
        }

        private sealed class RigNativeBufferSet : IDisposable
        {
            public NativeArray<TransformStreamHandle> StreamHandles;
            public NativeArray<ContextualPhysicalIkTwoBoneSetup> TwoBoneSetups;
            public NativeArray<ContextualPhysicalIkAppendageChainRuntime> AppendageChainRuntimes;
            public NativeArray<float> AppendageSegmentLengths;
            public NativeArray<ContextualPhysicalIkAppendageTarget> AppendageTargets;
            public NativeArray<float3> AppendageScratchPositions;
            public NativeArray<ContextualPhysicalIkSpineChainRuntime> SpineChainRuntimes;
            public NativeArray<float3> SpineTargets;
            public NativeArray<ContextualPhysicalIkSecondaryChainRuntime> SecondaryChainRuntimes;
            public NativeArray<ContextualPhysicalIkSecondaryState> SecondaryStates;
            public NativeArray<ContextualPhysicalIkCachedPoseState> CachedLocalPoseStates;
            public NativeArray<float> MuscleBulgeOutput;
            public NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly CurrentTargetFrames;

            public void Allocate(
                int totalHandleCount,
                int validAppendageChainCount,
                int totalAppendageLengthCount,
                int totalAppendageScratchCount,
                bool hasValidSpineChain,
                int validSecondaryChainCount,
                int totalSecondaryStateCount)
            {
                Dispose();
                try
                {

                    StreamHandles = new NativeArray<TransformStreamHandle>(
                        totalHandleCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<TransformStreamHandle>[dynamic] - sequential cached stream handles for contextual IK bones - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(StreamHandles, NativeMemoryOwner, nameof(StreamHandles), NativeMemoryLifetime);

                    TwoBoneSetups = new NativeArray<ContextualPhysicalIkTwoBoneSetup>(
                        4,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTwoBoneSetup>[4] - fixed humanoid limb solve descriptors - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(TwoBoneSetups, NativeMemoryOwner, nameof(TwoBoneSetups), NativeMemoryLifetime);

                if (validAppendageChainCount > 0)
                {
                    AppendageChainRuntimes = new NativeArray<ContextualPhysicalIkAppendageChainRuntime>(
                        validAppendageChainCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkAppendageChainRuntime>[dynamic] - appendage FABRIK descriptors - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(AppendageChainRuntimes, NativeMemoryOwner, nameof(AppendageChainRuntimes), NativeMemoryLifetime);

                    AppendageSegmentLengths = new NativeArray<float>(
                        totalAppendageLengthCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[dynamic] - appendage segment lengths - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(AppendageSegmentLengths, NativeMemoryOwner, nameof(AppendageSegmentLengths), NativeMemoryLifetime);

                    AppendageTargets = new NativeArray<ContextualPhysicalIkAppendageTarget>(
                        validAppendageChainCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkAppendageTarget>[dynamic] - appendage target positions and weights - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(AppendageTargets, NativeMemoryOwner, nameof(AppendageTargets), NativeMemoryLifetime);

                    AppendageScratchPositions = new NativeArray<float3>(
                        totalAppendageScratchCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[dynamic] - appendage FABRIK scratch positions - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(AppendageScratchPositions, NativeMemoryOwner, nameof(AppendageScratchPositions), NativeMemoryLifetime);
                }

                if (hasValidSpineChain)
                {
                    SpineChainRuntimes = new NativeArray<ContextualPhysicalIkSpineChainRuntime>(
                        1,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSpineChainRuntime>[1] - spline spine chain descriptor - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(SpineChainRuntimes, NativeMemoryOwner, nameof(SpineChainRuntimes), NativeMemoryLifetime);

                    SpineTargets = new NativeArray<float3>(
                        SpineTargetCountPerChain,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[3] - chest/head spline targets - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(SpineTargets, NativeMemoryOwner, nameof(SpineTargets), NativeMemoryLifetime);
                }

                if (validSecondaryChainCount > 0)
                {
                    SecondaryChainRuntimes = new NativeArray<ContextualPhysicalIkSecondaryChainRuntime>(
                        validSecondaryChainCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSecondaryChainRuntime>[dynamic] - secondary motion chain descriptors - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(SecondaryChainRuntimes, NativeMemoryOwner, nameof(SecondaryChainRuntimes), NativeMemoryLifetime);

                    SecondaryStates = new NativeArray<ContextualPhysicalIkSecondaryState>(
                        totalSecondaryStateCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSecondaryState>[dynamic] - secondary motion positions and velocities - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(SecondaryStates, NativeMemoryOwner, nameof(SecondaryStates), NativeMemoryLifetime);
                }

                CachedLocalPoseStates = new NativeArray<ContextualPhysicalIkCachedPoseState>(
                    totalHandleCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkCachedPoseState>[dynamic] - cached limb and appendage local pose states - owner: RigNativeBufferSet
                NativeMemorySentinel.RegisterNativeArray(CachedLocalPoseStates, NativeMemoryOwner, nameof(CachedLocalPoseStates), NativeMemoryLifetime);

                    MuscleBulgeOutput = new NativeArray<float>(
                        1,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - previous-frame muscle tension signal - owner: RigNativeBufferSet
                    NativeMemorySentinel.RegisterNativeArray(MuscleBulgeOutput, NativeMemoryOwner, nameof(MuscleBulgeOutput), NativeMemoryLifetime);
                }
                catch
                {
                    try
                    {
                        Dispose();
                    }
                    catch
                    {
                    }

                    throw;
                }
            }

            public void Dispose()
            {
                Exception firstException = null;
                DisposeNativeArrayBestEffort(ref StreamHandles, ref firstException, nameof(StreamHandles));
                DisposeNativeArrayBestEffort(ref TwoBoneSetups, ref firstException, nameof(TwoBoneSetups));
                DisposeNativeArrayBestEffort(ref AppendageChainRuntimes, ref firstException, nameof(AppendageChainRuntimes));
                DisposeNativeArrayBestEffort(ref AppendageSegmentLengths, ref firstException, nameof(AppendageSegmentLengths));
                DisposeNativeArrayBestEffort(ref AppendageTargets, ref firstException, nameof(AppendageTargets));
                DisposeNativeArrayBestEffort(ref AppendageScratchPositions, ref firstException, nameof(AppendageScratchPositions));
                DisposeNativeArrayBestEffort(ref SpineChainRuntimes, ref firstException, nameof(SpineChainRuntimes));
                DisposeNativeArrayBestEffort(ref SpineTargets, ref firstException, nameof(SpineTargets));
                DisposeNativeArrayBestEffort(ref SecondaryChainRuntimes, ref firstException, nameof(SecondaryChainRuntimes));
                DisposeNativeArrayBestEffort(ref SecondaryStates, ref firstException, nameof(SecondaryStates));
                DisposeNativeArrayBestEffort(ref CachedLocalPoseStates, ref firstException, nameof(CachedLocalPoseStates));
                DisposeNativeArrayBestEffort(ref MuscleBulgeOutput, ref firstException, nameof(MuscleBulgeOutput));
                CurrentTargetFrames = default;
                ThrowFirstDisposeException(firstException);
            }
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(offset)))
                return;

            float shiftDistanceSq = math.lengthsq(offset);
            if (!math.isfinite(shiftDistanceSq) || shiftDistanceSq > MaxAcceptedOriginShiftMetersSq)
                return;

            if (shiftDistanceSq <= 0.000001f)
                return;

            RebaseWorldSpaceFloat3Array(_spineTargets, offset);
            RebaseAppendageTargets(_appendageTargets, offset);
            RebaseSecondaryStates(_secondaryStates, offset);
            _predictiveLeftHandPosition -= shiftOffset;
            _predictiveRightHandPosition -= shiftOffset;
            _externalWallLeftHandPosition -= shiftOffset;
            _externalWallRightHandPosition -= shiftOffset;
            _terminalRightHandPosition -= shiftOffset;
        }

        private static void RebaseWorldSpaceFloat3Array(NativeArray<float3> values, float3 shiftOffset)
        {
            if (!values.IsCreated)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i] -= shiftOffset;
        }

        private static void RebaseAppendageTargets(NativeArray<ContextualPhysicalIkAppendageTarget> values, float3 shiftOffset)
        {
            if (!values.IsCreated)
                return;

            for (int i = 0; i < values.Length; i++)
            {
                ContextualPhysicalIkAppendageTarget target = values[i];
                target.Position -= shiftOffset;
                values[i] = target;
            }
        }

        private static void RebaseSecondaryStates(NativeArray<ContextualPhysicalIkSecondaryState> values, float3 shiftOffset)
        {
            if (!values.IsCreated)
                return;

            for (int i = 0; i < values.Length; i++)
            {
                ContextualPhysicalIkSecondaryState state = values[i];
                state.Position -= shiftOffset;
                values[i] = state;
            }
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, string sentinelLabel = null) where T : struct
        {
            if (!array.IsCreated)
                return;

            bool sentinelUnregistered = false;
            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
                sentinelUnregistered = true;
                array.Dispose();
                array = default;
            }
            catch
            {
                TryRestoreNativeSentinelRecord(array, sentinelUnregistered, sentinelLabel);
                throw;
            }
        }

        private static void TryRestoreNativeSentinelRecord<T>(
            NativeArray<T> array,
            bool sentinelUnregistered,
            string sentinelLabel) where T : struct
        {
            if (!sentinelUnregistered || !array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, sentinelLabel ?? nameof(DisposeNativeArray), NativeMemoryLifetime);
            }
            catch
            {
            }
        }

        private static void DisposeNativeArrayBestEffort<T>(
            ref NativeArray<T> array,
            ref Exception firstException,
            string sentinelLabel) where T : struct
        {
            try
            {
                DisposeNativeArray(ref array, sentinelLabel);
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }
        }

        private static void ThrowFirstDisposeException(Exception firstException)
        {
            if (firstException != null)
                throw firstException;
        }

        private void TryResolveReferences()
        {
            if (animator == null)
                TryGetComponent(out animator);

            if (characterRoot == null)
                characterRoot = transform;

            if (leftArmParent == null && leftUpperArm != null)
                leftArmParent = leftUpperArm.parent;

            if (rightArmParent == null && rightUpperArm != null)
                rightArmParent = rightUpperArm.parent;

            _predictiveRepairTarget = predictiveRepairTargetBehaviour as IKinematicRepairTarget;

            bool shouldAutoResolveMuscleBulgeRenderer = muscleBulgeRenderer == null &&
                (!Application.isPlaying || !_attemptedMuscleBulgeRendererResolve);
            if (shouldAutoResolveMuscleBulgeRenderer)
            {
                int visitedNodeCount = 0;
                muscleBulgeRenderer = FindFirstSkinnedMeshRenderer(transform, 0, ref visitedNodeCount);
                if (Application.isPlaying)
                    _attemptedMuscleBulgeRendererResolve = true;
            }
        }

        private static Renderer FindFirstSkinnedMeshRenderer(Transform root, int depth, ref int visitedNodeCount)
        {
            if (root == null || depth > MaxRendererSearchDepth || visitedNodeCount >= MaxRendererSearchNodes)
                return null;

            visitedNodeCount++;
            if (root.TryGetComponent(out SkinnedMeshRenderer renderer))
                return renderer;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Renderer childRenderer = FindFirstSkinnedMeshRenderer(root.GetChild(i), depth + 1, ref visitedNodeCount);
                if (childRenderer != null)
                    return childRenderer;

                if (visitedNodeCount >= MaxRendererSearchNodes)
                    break;
            }

            return null;
        }

        private int CountValidAppendageChains(out int totalHandleCount, out int totalLengthCount, out int totalScratchCount)
        {
            int validCount = 0;
            totalHandleCount = 0;
            totalLengthCount = 0;
            totalScratchCount = 0;

            if (appendageChains == null)
                return 0;

            for (int i = 0; i < appendageChains.Length; i++)
            {
                if (!IsValidAppendageChain(appendageChains[i]))
                    continue;

                validCount++;
                totalHandleCount += appendageChains[i].bones.Length;
                totalLengthCount += appendageChains[i].bones.Length - 1;
                totalScratchCount += appendageChains[i].bones.Length;
            }

            return validCount;
        }

        private int CountValidSecondaryChains(out int totalHandleCount, out int totalStateCount)
        {
            int validCount = 0;
            totalHandleCount = 0;
            totalStateCount = 0;

            if (secondaryChains == null)
                return 0;

            for (int i = 0; i < secondaryChains.Length; i++)
            {
                if (!IsValidSecondaryChain(secondaryChains[i]))
                    continue;

                validCount++;
                totalHandleCount += 1 + secondaryChains[i].bones.Length;
                totalStateCount += secondaryChains[i].bones.Length;
            }

            return validCount;
        }

        private bool TryGetValidSpineChain(out SpineChainAuthoring validChain)
        {
            validChain = spineChain;
            return IsValidSpineChain(validChain);
        }

        private bool TryInitializeMuscleBulgeMaterial()
        {
            if (_muscleBulgeMaterialInitialized)
                return _muscleBulgeMaterialInstance != null;

            if (muscleBulgeRenderer == null)
                return false;

            if (_muscleBulgeSharedMaterials == null)
                _muscleBulgeSharedMaterials = new List<Material>(4); // COLD ALLOC: List<Material> - per-rig reusable renderer material slot buffer - owner: ContextualPhysicalIkRig

            _muscleBulgeSharedMaterials.Clear();
            muscleBulgeRenderer.GetSharedMaterials(_muscleBulgeSharedMaterials);
            if (_muscleBulgeSharedMaterials.Count == 0 ||
                muscleBulgeMaterialSlot < 0 ||
                muscleBulgeMaterialSlot >= _muscleBulgeSharedMaterials.Count)
            {
                return false;
            }

            _muscleBulgeOriginalMaterial = _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot];
            if (_muscleBulgeOriginalMaterial == null || !_muscleBulgeOriginalMaterial.HasProperty(MuscleBulgeShaderId))
                return false;

            _muscleBulgeMaterialInstance = new Material(_muscleBulgeOriginalMaterial); // COLD ALLOC: Material[1] - per-rig muscle bulge material instance - owner: ContextualPhysicalIkRig
            _muscleBulgeMaterialInstance.SetFloat(MuscleBulgeShaderId, 0.0f);
            _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot] = _muscleBulgeMaterialInstance;
            muscleBulgeRenderer.SetSharedMaterials(_muscleBulgeSharedMaterials);
            _muscleBulgeCurrent = 0.0f;
            _muscleBulgeMaterialInitialized = true;
            return true;
        }

        private void ReleaseMuscleBulgeMaterial()
        {
            if (muscleBulgeRenderer != null &&
                _muscleBulgeSharedMaterials != null &&
                muscleBulgeMaterialSlot >= 0 &&
                muscleBulgeMaterialSlot < _muscleBulgeSharedMaterials.Count &&
                _muscleBulgeOriginalMaterial != null)
            {
                _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot] = _muscleBulgeOriginalMaterial;
                muscleBulgeRenderer.SetSharedMaterials(_muscleBulgeSharedMaterials);
            }

            if (_muscleBulgeMaterialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_muscleBulgeMaterialInstance);
                else
                    DestroyImmediate(_muscleBulgeMaterialInstance);
            }

            _muscleBulgeMaterialInstance = null;
            _muscleBulgeSharedMaterials?.Clear();
            _muscleBulgeOriginalMaterial = null;
            _muscleBulgeCurrent = 0.0f;
            _muscleBulgeMaterialInitialized = false;
        }

        private static void ResolveThrottleState(
            uint frameIndex,
            int entityId,
            float viewerDistanceSq,
            ref byte stableThrottleTier,
            out int updateThisFrame,
            out byte throttleTier,
            out uint updateBitfield)
        {
            uint entityBits = (uint)math.max(0, entityId);
            stableThrottleTier = ResolveHystereticThrottleTier(viewerDistanceSq, stableThrottleTier);
            throttleTier = stableThrottleTier;

            if (throttleTier == 2)
            {
                updateBitfield = 0x3u;
                updateThisFrame = ((entityBits & updateBitfield) == (frameIndex & updateBitfield)) ? 1 : 0;
                return;
            }

            if (throttleTier == 1)
            {
                updateBitfield = 0x1u;
                updateThisFrame = ((entityBits & updateBitfield) == (frameIndex & updateBitfield)) ? 1 : 0;
                return;
            }

            updateBitfield = 0u;
            updateThisFrame = 1;
        }

        private static byte ResolveHystereticThrottleTier(float viewerDistanceSq, byte stableThrottleTier)
        {
            if (!math.isfinite(viewerDistanceSq) || viewerDistanceSq <= Tier0UpgradeDistanceSq)
                return 0;

            if (stableThrottleTier == 0)
            {
                if (viewerDistanceSq > Tier1DowngradeDistanceSq)
                    return 2;

                return viewerDistanceSq > Tier0DowngradeDistanceSq ? (byte)1 : (byte)0;
            }

            if (stableThrottleTier == 1)
            {
                if (viewerDistanceSq < Tier0UpgradeDistanceSq)
                    return 0;

                return viewerDistanceSq > Tier1DowngradeDistanceSq ? (byte)2 : (byte)1;
            }

            if (viewerDistanceSq < Tier0UpgradeDistanceSq)
                return 0;

            return viewerDistanceSq < Tier1UpgradeDistanceSq ? (byte)1 : (byte)2;
        }

        private static bool IsValidAppendageChain(AppendageChainAuthoring chain)
        {
            if (chain.bones == null || chain.bones.Length < 5)
                return false;

            for (int i = 0; i < chain.bones.Length; i++)
            {
                if (chain.bones[i] == null)
                    return false;
            }

            return true;
        }

        private static bool IsValidSpineChain(SpineChainAuthoring chain)
        {
            if (chain.bones == null || chain.bones.Length < 5)
                return false;

            for (int i = 0; i < chain.bones.Length; i++)
            {
                if (chain.bones[i] == null)
                    return false;
            }

            return true;
        }

        private static bool IsValidSecondaryChain(SecondaryChainAuthoring chain)
        {
            if (chain.bones == null || chain.bones.Length < 2)
                return false;

            for (int i = 0; i < chain.bones.Length; i++)
            {
                if (chain.bones[i] == null)
                    return false;
            }

            return true;
        }

        private ContextualPhysicalIkTwoBoneSetup BuildTwoBoneSetup(
            Transform parent,
            Transform upper,
            Transform lower,
            Transform end,
            Transform poleHint,
            int parentHandleIndex,
            int upperHandleIndex,
            int lowerHandleIndex,
            int endHandleIndex,
            byte targetChannel,
            float baseBlend)
        {
            if (parent == null || upper == null || lower == null || end == null)
                return default;

            return new ContextualPhysicalIkTwoBoneSetup
            {
                ParentHandleIndex = parentHandleIndex,
                UpperHandleIndex = upperHandleIndex,
                LowerHandleIndex = lowerHandleIndex,
                EndHandleIndex = endHandleIndex,
                TargetChannel = targetChannel,
                Enabled = 1,
                UpperLength = ComputeLength(upper, lower),
                LowerLength = ComputeLength(lower, end),
                BaseBlend = SanitizeUnitScalar(baseBlend),
                ReachSafetyMargin = SanitizeNonNegativeScalar(reachSafetyMargin),
                PoleLocalOffset = ComputeLocalPoleOffset(parent, poleHint, lower),
            };
        }

        private TransformStreamHandle BindStreamHandle(Transform target)
        {
            return animator != null && target != null ? animator.BindStreamTransform(target) : default;
        }

        private static float ComputeReach(Transform upper, Transform lower, Transform end)
        {
            if (upper == null || lower == null || end == null)
                return 0.0f;

            return ComputeLength(upper, lower) + ComputeLength(lower, end);
        }

        private static float ComputeLength(Transform first, Transform second)
        {
            if (first == null || second == null)
                return 0.0f;

            Vector3 firstPosition = first.position;
            Vector3 secondPosition = second.position;
            if (!IsFiniteVector(firstPosition) || !IsFiniteVector(secondPosition))
                return 0.0f;

            float3 delta = ContextualPhysicalIkMath.ToFloat3(firstPosition - secondPosition);
            float lengthSq = math.lengthsq(delta);
            if (!math.all(math.isfinite(delta)) ||
                !math.isfinite(lengthSq) ||
                lengthSq <= 0.00000001f)
            {
                return 0.0f;
            }

            return lengthSq * math.rsqrt(math.max(lengthSq, 0.00000001f));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z) || float.IsInfinity(value.w))
                return false;

            float lengthSq = (value.x * value.x) + (value.y * value.y) + (value.z * value.z) + (value.w * value.w);
            return !float.IsNaN(lengthSq) && !float.IsInfinity(lengthSq) && lengthSq > 0.000001f;
        }

        private static float SanitizeUnitScalar(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeNonNegativeScalar(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static float WrapPositivePhase(float phase, float wrap)
        {
            float safeWrap = math.max(SanitizeNonNegativeScalar(wrap), 0.0001f);
            if (!math.isfinite(phase))
                return 0.0f;

            phase -= math.floor(phase * math.rcp(safeWrap)) * safeWrap;
            return !math.isfinite(phase) || phase < 0.0f ? 0.0f : phase;
        }

        private static float3 SanitizeFloat3Value(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private static float3 ReadPositionOrFallback(Transform source, float3 fallback)
        {
            if (source == null)
                return fallback;

            Vector3 position = source.position;
            return IsFiniteVector(position) ? ContextualPhysicalIkMath.ToFloat3(position) : fallback;
        }

        private static float ResolveIkQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1.0f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private static float ResolveFootIkQualityWeight01(float qualityWeight01)
        {
            return math.lerp(MinimumIkQualityWeight01, 1.0f, SanitizeUnitScalar(qualityWeight01));
        }

        private float ResolveWallTouchQualityWeight01(float qualityWeight01)
        {
            float quality = SanitizeUnitScalar(qualityWeight01);
            float floor = math.min(SanitizeUnitScalar(wallTouchQualityRampFloor01), 0.95f);
            float span = math.max(0.0001f, 1.0f - floor);
            return math.saturate((quality - floor) * math.rcp(span));
        }

        private static Vector3 NormalizeVectorNoSqrt(Vector3 value, Vector3 fallback)
        {
            float3 v = ContextualPhysicalIkMath.ToFloat3(value);
            float lengthSq = math.lengthsq(v);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(v)))
                return fallback;

            return ContextualPhysicalIkMath.ToUnityVector3(v * math.rsqrt(lengthSq));
        }

        private static Vector3 ClampVectorNoSqrt(Vector3 value, float maxLength)
        {
            if (!IsFiniteVector(value))
                return Vector3.zero;

            float safeMaxLength = SanitizeNonNegativeScalar(maxLength);
            if (safeMaxLength <= 0.000001f)
                return Vector3.zero;

            float3 v = ContextualPhysicalIkMath.ToFloat3(value);
            float lengthSq = math.lengthsq(v);
            float maxLengthSq = safeMaxLength * safeMaxLength;
            if (lengthSq <= maxLengthSq)
                return value;

            return ContextualPhysicalIkMath.ToUnityVector3(v * (safeMaxLength * math.rsqrt(math.max(lengthSq, 0.000001f))));
        }

        private static float3 ComputeLocalPoleOffset(Transform parent, Transform poleHint, Transform fallbackJoint)
        {
            Transform source = poleHint != null ? poleHint : fallbackJoint;
            if (parent == null || source == null)
                return new float3(0.0f, 0.0f, 0.25f);

            Vector3 parentPosition = parent.position;
            Vector3 sourcePosition = source.position;
            Quaternion parentRotation = parent.rotation;
            if (!IsFiniteVector(parentPosition) ||
                !IsFiniteVector(sourcePosition) ||
                !IsFiniteQuaternion(parentRotation))
            {
                return new float3(0.0f, 0.0f, 0.25f);
            }

            Quaternion inverseParentRotation = Quaternion.Inverse(parentRotation);
            Vector3 localOffset = inverseParentRotation * (sourcePosition - parentPosition);
            float3 localOffsetFloat3 = ContextualPhysicalIkMath.ToFloat3(localOffset);
            float localOffsetLengthSq = math.lengthsq(localOffsetFloat3);
            if (!math.all(math.isfinite(localOffsetFloat3)) ||
                !math.isfinite(localOffsetLengthSq) ||
                localOffsetLengthSq <= 0.0001f)
            {
                return new float3(0.0f, 0.0f, 0.25f);
            }

            return localOffsetFloat3;
        }
    }
}
