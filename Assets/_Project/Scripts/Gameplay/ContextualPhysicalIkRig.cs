using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Caves;
using Hecton8.Interaction;
using Hecton8.World;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkTwoBoneSetup
    {
        public int ParentHandleIndex;
        public int UpperHandleIndex;
        public int LowerHandleIndex;
        public int EndHandleIndex;
        public byte TargetChannel;
        public byte Enabled;
        public float UpperLength;
        public float LowerLength;
        public float BaseBlend;
        public float ReachSafetyMargin;
        public float3 PoleLocalOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkAppendageChainRuntime
    {
        public int ParentHandleIndex;
        public int FirstBoneHandleIndex;
        public int BoneCount;
        public int FirstLengthIndex;
        public int FirstScratchIndex;
        public int TargetIndex;
        public int Iterations;
        public float Tolerance;
        public float Blend;
        public float3 PoleLocalOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkSpineChainRuntime
    {
        public int ParentHandleIndex;
        public int FirstBoneHandleIndex;
        public int BoneCount;
        public int TargetStartIndex;
        public float Blend;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkSecondaryChainRuntime
    {
        public int ParentHandleIndex;
        public int FirstBoneHandleIndex;
        public int BoneCount;
        public int FirstStateIndex;
        public float Stiffness;
        public float Damping;
        public float Blend;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkAppendageTarget
    {
        public float3 Position;
        public float Weight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkSecondaryState
    {
        public float3 Position;
        public float3 Velocity;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkCachedPoseState
    {
        public quaternion Rotation;
        public float3 Position;
        public byte HasRotation;
        public byte HasPosition;
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkApplyJob : IAnimationJob
    {
        public const int PelvisHandleIndex = 0;
        private const float SpineSlopeLeanShare = 0.35f;

        [ReadOnly] public NativeArray<ContextualPhysicalIkTargetFrame> TargetFrames;
        [ReadOnly] public NativeArray<TransformStreamHandle> StreamHandles;
        [ReadOnly] public NativeArray<ContextualPhysicalIkTwoBoneSetup> TwoBoneSetups;
        [ReadOnly] public NativeArray<ContextualPhysicalIkAppendageChainRuntime> AppendageChains;
        [ReadOnly] public NativeArray<float> AppendageSegmentLengths;
        [ReadOnly] public NativeArray<ContextualPhysicalIkAppendageTarget> AppendageTargets;
        [ReadOnly] public NativeArray<ContextualPhysicalIkSpineChainRuntime> SpineChains;
        [ReadOnly] public NativeArray<float3> SpineTargets;
        [ReadOnly] public NativeArray<ContextualPhysicalIkSecondaryChainRuntime> SecondaryChains;

        public NativeArray<float3> AppendageScratchPositions;
        public NativeArray<ContextualPhysicalIkSecondaryState> SecondaryStates;
        public NativeArray<ContextualPhysicalIkCachedPoseState> CachedLocalPoseStates;
        public NativeArray<float> MuscleBulgeOutput;
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
            float3 desiredLocalPosition = ContextualPhysicalIkMath.ToFloat3(currentLocalPosition) + frame.ComOffsetLocal;
            float3 blendedPosition = math.lerp(
                ContextualPhysicalIkMath.ToFloat3(currentLocalPosition),
                desiredLocalPosition,
                math.saturate(PelvisPositionBlend));
            Vector3 blendedPositionUnity = ContextualPhysicalIkMath.ToUnityVector3(blendedPosition);
            pelvisHandle.SetLocalPosition(stream, blendedPositionUnity);

            Quaternion currentLocalRotation = pelvisHandle.GetLocalRotation(stream);
            quaternion currentLocalRotationQ = ContextualPhysicalIkMath.ToMathematicsQuaternion(currentLocalRotation);
            quaternion yawRotation = ApproximateAxisRotationNoTrig(new float3(0.0f, 1.0f, 0.0f), frame.PelvisYawRadians);
            quaternion leanRotation = ApproximateSmallEulerXzNoTrig(frame.ComLeanRadians.x, frame.ComLeanRadians.y);
            quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(currentLocalRotationQ, math.mul(yawRotation, leanRotation)));
            quaternion blendedRotation = ApproximateNlerpNoSqrt(currentLocalRotationQ, desiredLocalRotation, PelvisRotationBlend);
            pelvisHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedRotation));
            CacheLocalPosition(PelvisHandleIndex, blendedPosition);
            CacheLocalRotation(PelvisHandleIndex, blendedRotation);
        }

        private void ProcessTwoBoneLimb(AnimationStream stream, in ContextualPhysicalIkTargetFrame frame, in ContextualPhysicalIkTwoBoneSetup setup)
        {
            if (setup.Enabled == 0)
                return;

            ContextualPhysicalIkContactTarget target = ResolveTarget(in frame, setup.TargetChannel);
            float weight = math.saturate(target.Blend * setup.BaseBlend);
            if (weight <= 0.0001f)
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
            float3 polePosition = rootPosition + math.mul(parentWorldRotation, setup.PoleLocalOffset);

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
            float extensionResistance01 = ContextualPhysicalIkMath.EvaluateExtensionResistanceFromDistanceSq01(distanceToTargetSq, maxReach);
            if (extensionResistance01 > 0.0f)
            {
                float3 targetDirection = ContextualPhysicalIkMath.SafeNormalize(target.WorldPosition - rootPosition, new float3(0.0f, 0.0f, 1.0f));
                float3 poleVector = polePosition - rootPosition;
                float3 projectedPole = poleVector - (targetDirection * math.dot(poleVector, targetDirection));
                float3 bendDirection = ContextualPhysicalIkMath.SafeNormalize(projectedPole, math.mul(parentWorldRotation, new float3(1.0f, 0.0f, 0.0f)));
                float3 torqueAxis = ContextualPhysicalIkMath.SafeNormalize(math.cross(targetDirection, bendDirection), new float3(0.0f, 1.0f, 0.0f));
                quaternion resistanceRotation = ApproximateAxisRotationNoTrig(torqueAxis, -OverExtensionResistanceRadians * extensionResistance01);
                desiredUpperWorldRotation = NormalizeQuaternionNoSqrt(math.mul(resistanceRotation, desiredUpperWorldRotation));
                desiredLowerWorldRotation = NormalizeQuaternionNoSqrt(math.mul(resistanceRotation, desiredLowerWorldRotation));
            }

            quaternion desiredEndWorldRotation = ContextualPhysicalIkMath.AlignEndEffectorToNormal(currentEndWorldRotation, target.WorldNormal);

            quaternion currentUpperLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(upperHandle.GetLocalRotation(stream));
            quaternion currentLowerLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(lowerHandle.GetLocalRotation(stream));
            quaternion currentEndLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(endHandle.GetLocalRotation(stream));

            quaternion desiredUpperLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(parentWorldRotation), desiredUpperWorldRotation));
            quaternion desiredLowerLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(desiredUpperWorldRotation), desiredLowerWorldRotation));
            quaternion desiredEndLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(desiredLowerWorldRotation), desiredEndWorldRotation));

            quaternion blendedUpperLocalRotation = ApproximateNlerpNoSqrt(currentUpperLocalRotation, desiredUpperLocalRotation, weight);
            quaternion blendedLowerLocalRotation = ApproximateNlerpNoSqrt(currentLowerLocalRotation, desiredLowerLocalRotation, weight);
            quaternion blendedEndLocalRotation = ApproximateNlerpNoSqrt(currentEndLocalRotation, desiredEndLocalRotation, weight);

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
            if (chain.BoneCount < 2 || !AppendageScratchPositions.IsCreated)
                return;

            ContextualPhysicalIkAppendageTarget target = AppendageTargets[chain.TargetIndex];
            float weight = math.saturate(target.Weight * chain.Blend);
            if (weight <= 0.0001f)
                return;

            for (int i = 0; i < chain.BoneCount; i++)
            {
                AppendageScratchPositions[chain.FirstScratchIndex + i] = ContextualPhysicalIkMath.ToFloat3(
                    StreamHandles[chain.FirstBoneHandleIndex + i].GetPosition(stream));
            }

            quaternion parentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(
                StreamHandles[chain.ParentHandleIndex].GetRotation(stream));
            float3 rootPosition = AppendageScratchPositions[chain.FirstScratchIndex];
            float3 polePosition = rootPosition + math.mul(parentWorldRotation, chain.PoleLocalOffset);

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

            quaternion previousWorldRotation = parentWorldRotation;
            for (int boneIndex = 0; boneIndex < chain.BoneCount - 1; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int childHandleIndex = handleIndex + 1;
                TransformStreamHandle boneHandle = StreamHandles[handleIndex];
                TransformStreamHandle childHandle = StreamHandles[childHandleIndex];

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                float3 currentChildPosition = ContextualPhysicalIkMath.ToFloat3(childHandle.GetPosition(stream));
                float3 currentDirection = ContextualPhysicalIkMath.SafeNormalize(currentChildPosition - currentBonePosition, new float3(0.0f, 0.0f, 1.0f));

                float3 solvedBonePosition = AppendageScratchPositions[chain.FirstScratchIndex + boneIndex];
                float3 solvedChildPosition = AppendageScratchPositions[chain.FirstScratchIndex + boneIndex + 1];
                float3 desiredDirection = ContextualPhysicalIkMath.SafeNormalize(solvedChildPosition - solvedBonePosition, currentDirection);

                quaternion currentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetRotation(stream));
                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));

                quaternion currentLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetLocalRotation(stream));
                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
                boneHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLocalRotation));
                CacheLocalRotation(handleIndex, blendedLocalRotation);
                previousWorldRotation = desiredWorldRotation;
            }
        }

        private void ProcessSpine(AnimationStream stream, in ContextualPhysicalIkSpineChainRuntime chain, in ContextualPhysicalIkTargetFrame frame)
        {
            if (chain.BoneCount < 5 || !StreamHandles.IsCreated || !SpineTargets.IsCreated)
                return;

            float weight = math.saturate(chain.Blend);
            if (weight <= 0.0001f)
                return;

            float3 chestTarget = SpineTargets[chain.TargetStartIndex + 0];
            float3 headTarget = SpineTargets[chain.TargetStartIndex + 1];
            float3 headForwardReference = SpineTargets[chain.TargetStartIndex + 2];
            TransformStreamHandle parentHandle = StreamHandles[chain.ParentHandleIndex];
            float3 previousWorldPosition = ContextualPhysicalIkMath.ToFloat3(parentHandle.GetPosition(stream));
            quaternion previousWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(parentHandle.GetRotation(stream));
            float3 rootPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[chain.FirstBoneHandleIndex].GetPosition(stream));
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

                float normalizedT = boneIndex * invBoneSpan;
                float nextT = math.saturate((boneIndex + 1) * invBoneSpan);

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                float3 currentDirection;
                if (boneIndex < chain.BoneCount - 1)
                {
                    currentDirection = ContextualPhysicalIkMath.SafeNormalize(
                        ContextualPhysicalIkMath.ToFloat3(StreamHandles[handleIndex + 1].GetPosition(stream)) - currentBonePosition,
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

                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));
                quaternion leanedDesiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(slopeLeanRotation, desiredWorldRotation));
                desiredWorldRotation = ApproximateNlerpNoSqrt(desiredWorldRotation, leanedDesiredWorldRotation, normalizedT * weight);
                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
                boneHandle.SetLocalPosition(stream, ContextualPhysicalIkMath.ToUnityVector3(blendedLocalPosition));
                boneHandle.SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(blendedLocalRotation));
                CacheLocalPosition(handleIndex, blendedLocalPosition);
                CacheLocalRotation(handleIndex, blendedLocalRotation);
                previousWorldPosition = previousWorldPosition + math.rotate(previousWorldRotation, blendedLocalPosition);
                previousWorldRotation = NormalizeQuaternionNoSqrt(math.mul(previousWorldRotation, blendedLocalRotation));
            }
        }

        private void ProcessSecondary(AnimationStream stream, in ContextualPhysicalIkTargetFrame frame, in ContextualPhysicalIkSecondaryChainRuntime chain)
        {
            if (chain.BoneCount < 2 || !StreamHandles.IsCreated || !SecondaryStates.IsCreated)
                return;

            float weight = math.saturate(chain.Blend);
            if (weight <= 0.0001f)
                return;

            float safeDeltaTime = math.max(0.0001f, frame.DeltaTime);
            for (int boneIndex = 0; boneIndex < chain.BoneCount; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int stateIndex = chain.FirstStateIndex + boneIndex;
                float3 targetPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[handleIndex].GetPosition(stream));
                ContextualPhysicalIkSecondaryState state = SecondaryStates[stateIndex];
                float3 currentPosition = state.Position;
                float3 currentVelocity = state.Velocity;

                if (math.lengthsq(currentPosition) <= 0.000001f && math.lengthsq(currentVelocity) <= 0.000001f)
                    currentPosition = targetPosition;

                ContextualPhysicalIkMath.IntegrateSpringDamper(
                    targetPosition,
                    chain.Stiffness,
                    chain.Damping,
                    safeDeltaTime,
                    ref currentPosition,
                    ref currentVelocity);

                state.Position = currentPosition;
                state.Velocity = currentVelocity;
                SecondaryStates[stateIndex] = state;
            }

            quaternion previousWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(
                StreamHandles[chain.ParentHandleIndex].GetRotation(stream));
            for (int boneIndex = 0; boneIndex < chain.BoneCount - 1; boneIndex++)
            {
                int handleIndex = chain.FirstBoneHandleIndex + boneIndex;
                int nextHandleIndex = handleIndex + 1;
                int stateIndex = chain.FirstStateIndex + boneIndex;
                int nextStateIndex = stateIndex + 1;

                TransformStreamHandle boneHandle = StreamHandles[handleIndex];
                quaternion currentLocalRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetLocalRotation(stream));
                quaternion currentWorldRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(boneHandle.GetRotation(stream));

                float3 currentBonePosition = ContextualPhysicalIkMath.ToFloat3(boneHandle.GetPosition(stream));
                float3 currentChildPosition = ContextualPhysicalIkMath.ToFloat3(StreamHandles[nextHandleIndex].GetPosition(stream));
                float3 currentDirection = ContextualPhysicalIkMath.SafeNormalize(currentChildPosition - currentBonePosition, new float3(0.0f, 0.0f, 1.0f));
                float3 desiredDirection = ContextualPhysicalIkMath.SafeNormalize(
                    SecondaryStates[nextStateIndex].Position - SecondaryStates[stateIndex].Position,
                    currentDirection);

                quaternion desiredWorldRotation = NormalizeQuaternionNoSqrt(math.mul(
                    ContextualPhysicalIkMath.FastDirectionDeltaNoTrig(currentDirection, desiredDirection),
                    currentWorldRotation));
                quaternion desiredLocalRotation = NormalizeQuaternionNoSqrt(math.mul(math.inverse(previousWorldRotation), desiredWorldRotation));
                quaternion blendedLocalRotation = ApproximateNlerpNoSqrt(currentLocalRotation, desiredLocalRotation, weight);
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
                        StreamHandles[i].SetLocalPosition(stream, ContextualPhysicalIkMath.ToUnityVector3(cachedState.Position));

                    if (cachedState.HasRotation != 0)
                        StreamHandles[i].SetLocalRotation(stream, ContextualPhysicalIkMath.ToUnityQuaternion(cachedState.Rotation));
                }
            }
        }

        private void CacheLocalRotation(int handleIndex, quaternion rotation)
        {
            if (!CachedLocalPoseStates.IsCreated || handleIndex < 0 || handleIndex >= CachedLocalPoseStates.Length)
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

            ContextualPhysicalIkCachedPoseState cachedState = CachedLocalPoseStates[handleIndex];
            cachedState.Position = position;
            cachedState.HasPosition = 1;
            CachedLocalPoseStates[handleIndex] = cachedState;
        }

        private void AccumulateMuscleBulge(float value)
        {
            if (!MuscleBulgeOutput.IsCreated || MuscleBulgeOutput.Length <= 0)
                return;

            MuscleBulgeOutput[0] = math.max(MuscleBulgeOutput[0], value);
        }

        private static quaternion ApproximateNlerpNoSqrt(quaternion from, quaternion to, float t)
        {
            return CinematicMath.FastNlerp(from, to, t);
        }

        private static quaternion NormalizeQuaternionNoSqrt(quaternion value)
        {
            float4 v = value.value;
            float lenSq = math.max(math.dot(v, v), 0.000001f);
            v *= math.rsqrt(lenSq);
            return new quaternion(v);
        }

        private static quaternion ApproximateSmallEulerXzNoTrig(float pitchRadians, float rollRadians)
        {
            ApproximateSinCosNoTrig(pitchRadians * 0.5f, out float pitchSin, out float pitchCos);
            ApproximateSinCosNoTrig(rollRadians * 0.5f, out float rollSin, out float rollCos);
            quaternion pitch = new quaternion(pitchSin, 0.0f, 0.0f, pitchCos);
            quaternion roll = new quaternion(0.0f, 0.0f, rollSin, rollCos);
            float4 value = math.mul(pitch, roll).value;
            float lenSq = math.max(math.dot(value, value), 0.000001f);
            value *= 1.5f - (0.5f * lenSq);
            return new quaternion(value);
        }

        private static quaternion ApproximateAxisRotationNoTrig(float3 axis, float angleRadians)
        {
            ApproximateSinCosNoTrig(angleRadians * 0.5f, out float sinHalf, out float cosHalf);
            return new quaternion(axis.x * sinHalf, axis.y * sinHalf, axis.z * sinHalf, cosHalf);
        }

        private static void ApproximateSinCosNoTrig(float x, out float sin, out float cos)
        {
            float clamped = math.clamp(x, -1.5707964f, 1.5707964f);
            float x2 = clamped * clamped;
            sin = clamped * (1.0f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = 1.0f - (x2 * (0.5f - (x2 * 0.041666667f)));
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
    public sealed class ContextualPhysicalIkRig : MonoBehaviour, IOriginShiftListener, IPhysicalHandIkTargetSink
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
        private const float Tier0DistanceMaxSq = Tier0DistanceMax * Tier0DistanceMax;
        private const float Tier1DistanceMaxSq = Tier1DistanceMax * Tier1DistanceMax;
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
        private const string NativeMemoryOwner = nameof(ContextualPhysicalIkRig);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
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

        [Tooltip("If enabled, low/MX350/unknown tiers keep tool retraction but skip wall-touch hand bracing.")]
        [SerializeField] private bool disableWallTouchOnLowTier = true;

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

        private NativeArray<TransformStreamHandle> _streamHandles;
        private NativeArray<ContextualPhysicalIkTwoBoneSetup> _twoBoneSetups;
        private NativeArray<ContextualPhysicalIkAppendageChainRuntime> _appendageChainRuntimes;
        private NativeArray<float> _appendageSegmentLengths;
        private NativeArray<ContextualPhysicalIkAppendageTarget> _appendageTargets;
        private NativeArray<float3> _appendageScratchPositions;
        private NativeArray<ContextualPhysicalIkSpineChainRuntime> _spineChainRuntimes;
        private NativeArray<float3> _spineTargets;
        private NativeArray<ContextualPhysicalIkSecondaryChainRuntime> _secondaryChainRuntimes;
        private NativeArray<ContextualPhysicalIkSecondaryState> _secondaryStates;
        private NativeArray<ContextualPhysicalIkCachedPoseState> _cachedLocalPoseStates;
        private NativeArray<float> _muscleBulgeOutput;
        private NativeArray<ContextualPhysicalIkTargetFrame> _currentTargetFrames;

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
        private Material[] _muscleBulgeSharedMaterials;
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
        private bool _runtimeInitialized;
        private bool _animationInjected;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _muscleBulgeMaterialInitialized;
        private bool _attemptedMuscleBulgeRendererResolve;
        private bool _hasPreviousLeftPredictiveControllerPose;
        private bool _hasPreviousRightPredictiveControllerPose;
        private bool _terminalRightHandActive;
        private bool _upperArmRenderersVisible = true;

        private void OnEnable()
        {
            TryResolveReferences();
            EnsureRuntimeInitialized();
            TryInitializeAnimationInjection();
            TryRegisterWithRuntime();
            TryRegisterOriginShiftListener();
        }

        private void Start()
        {
            EnsureRuntimeInitialized();
            TryInitializeAnimationInjection();
            TryRegisterWithRuntime();
        }

        private void OnDisable()
        {
            SetUpperArmRenderersVisible(true);
            TryUnregisterOriginShiftListener();
            TryUnregisterFromRuntime();
            TearDownAnimationInjection();
            DisposeRuntimeArrays();
        }

        private void OnDestroy()
        {
            SetUpperArmRenderersVisible(true);
            TryUnregisterOriginShiftListener();
            TryUnregisterFromRuntime();
            TearDownAnimationInjection();
            DisposeRuntimeArrays();
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

            float recoilCap = math.max(0.0f, toolRecoilMaxOffsetMeters);
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
            if (!IsFiniteVector(_terminalRightHandNormal) || _terminalRightHandNormal.sqrMagnitude <= 0.0001f)
                _terminalRightHandNormal = Vector3.up;
            else
                _terminalRightHandNormal = NormalizeVectorNoSqrt(_terminalRightHandNormal, Vector3.up);

            _terminalRightHandHoldTimer = math.max(0.0f, target.HoldSeconds);
            _terminalRightHandTargetBlend = math.saturate(target.Blend);
            _terminalRightHandActive = true;
        }

        public void ClearTerminalHandTarget(int sourceId)
        {
            if (!_terminalRightHandActive || sourceId != _terminalRightHandSourceId)
                return;

            _terminalRightHandHoldTimer = 0.0f;
            _terminalRightHandTargetBlend = 0.0f;
        }

        internal void AssignEntitySlot(int entitySlot, NativeArray<ContextualPhysicalIkTargetFrame> targetFrames)
        {
            _entitySlot = entitySlot;
            _currentTargetFrames = targetFrames;
            UpdateJobDataTargetFrames();
        }

        internal void OnTargetBufferSwapped(NativeArray<ContextualPhysicalIkTargetFrame> targetFrames)
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
                _externalWallLeftHandBlend = math.saturate(leftTarget.Blend);
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
                _externalWallRightHandBlend = math.saturate(rightTarget.Blend);
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

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool lowTier = IsLowTier(tier);
            bool xrActive = HectonXRRuntimeState.IsXRActive;
            bool lowerBodyIkEnabled = enableFootPlacement && (xrActive || !lowTier);
            bool wallTouchEnabled = enableHandBracing && (!disableWallTouchOnLowTier || !lowTier);

            RefreshPlayerStress();
            TickBreathingState(deltaTime, lowTier);
            TickExternalSqueezePoleState(deltaTime);
            ApplyExternalSqueezePoleBias();
            CaptureSpineTargets(lowTier);
            CaptureAppendageTargets();
            ApplyMuscleBulgeSignal(deltaTime);
            CapturePredictiveRepairLatch(deltaTime, wallTouchEnabled);
            TickToolHandTransientState(deltaTime);
            TickUpperArmFovCulling(deltaTime);

            float3 rootPosition = ContextualPhysicalIkMath.ToFloat3(characterRoot.position);
            quaternion rootRotation = ContextualPhysicalIkMath.ToMathematicsQuaternion(characterRoot.rotation);
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(rootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(rootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            ResolveColdShiverOffsets(rootRight, rootUp, out float3 leftColdShiverOffset, out float3 rightColdShiverOffset);
            float viewerDistanceSq = hasViewerPosition
                ? math.lengthsq(rootPosition - viewerPosition)
                : 0.0f;
            ResolveThrottleState(frameIndex, _entitySlot, viewerDistanceSq, out int updateThisFrame, out byte throttleTier, out uint updateBitfield);
            entityState.IsActive = 1;
            entityState.EnableFootPlacement = lowerBodyIkEnabled ? 1 : 0;
            entityState.EnableHandBracing = enableHandBracing ? 1 : 0;
            entityState.EnableWallTouch = wallTouchEnabled ? 1 : 0;
            entityState.LeftHandEmpty = leftHandEmptyForWallTouch ? 1 : 0;
            entityState.EnableToolRetraction = enableToolRetraction ? 1 : 0;
            entityState.HasCameraPose = hasViewerPosition ? 1 : 0;
            entityState.DeltaTime = deltaTime;
            entityState.RootPosition = rootPosition;
            entityState.RootRotation = rootRotation;
            entityState.PelvisPosition = pelvis != null ? ContextualPhysicalIkMath.ToFloat3(pelvis.position) : entityState.RootPosition;
            entityState.LeftFootProbeOrigin = leftFootProbe != null ? ContextualPhysicalIkMath.ToFloat3(leftFootProbe.position) : entityState.RootPosition;
            entityState.RightFootProbeOrigin = rightFootProbe != null ? ContextualPhysicalIkMath.ToFloat3(rightFootProbe.position) : entityState.RootPosition;
            entityState.LeftHandProbeOrigin = leftHandProbe != null ? ContextualPhysicalIkMath.ToFloat3(leftHandProbe.position) : entityState.RootPosition;
            entityState.RightHandProbeOrigin = rightHandProbe != null ? ContextualPhysicalIkMath.ToFloat3(rightHandProbe.position) : entityState.RootPosition;
            entityState.PredictiveLeftHandPosition = ContextualPhysicalIkMath.ToFloat3(_predictiveLeftHandPosition);
            entityState.PredictiveRightHandPosition = ContextualPhysicalIkMath.ToFloat3(_predictiveRightHandPosition);
            entityState.PredictiveLeftHandNormal = ContextualPhysicalIkMath.ToFloat3(_predictiveLeftHandNormal);
            entityState.PredictiveRightHandNormal = ContextualPhysicalIkMath.ToFloat3(_predictiveRightHandNormal);
            entityState.CameraPosition = viewerPosition;
            entityState.CameraForward = viewerForward;
            entityState.CameraUp = viewerUp;
            entityState.CameraRight = viewerRight;
            entityState.LeftToolRecoilOffset = ContextualPhysicalIkMath.ToFloat3(_leftToolRecoilOffset);
            entityState.RightToolRecoilOffset = ContextualPhysicalIkMath.ToFloat3(_rightToolRecoilOffset);
            entityState.LeftColdShiverOffset = leftColdShiverOffset;
            entityState.RightColdShiverOffset = rightColdShiverOffset;
            entityState.DashboardRightHandPosition = ContextualPhysicalIkMath.ToFloat3(_terminalRightHandPosition);
            entityState.DashboardRightHandNormal = ContextualPhysicalIkMath.ToFloat3(_terminalRightHandNormal);
            entityState.LeftLegReach = _cachedLeftLegReach;
            entityState.RightLegReach = _cachedRightLegReach;
            entityState.LeftArmReach = _cachedLeftArmReach;
            entityState.RightArmReach = _cachedRightArmReach;
            entityState.PredictiveLeftHandBlend = _predictiveLeftHandBlend;
            entityState.PredictiveRightHandBlend = _predictiveRightHandBlend;
            entityState.CameraHandLateralOffset = cameraHandLateralOffset;
            entityState.CameraHandVerticalOffset = cameraHandVerticalOffset;
            entityState.ToolCollisionDistance = toolCollisionDistance;
            entityState.ToolRetractionBackDistance = toolRetractionBackDistance;
            entityState.ToolRetractionLiftDistance = toolRetractionLiftDistance;
            entityState.ToolRetractionBlend = toolRetractionBlend;
            entityState.ToolRecoilMaxOffset = toolRecoilMaxOffsetMeters;
            entityState.DashboardRightHandBlend = _terminalRightHandBlend;
            entityState.ColdShiverBlend = _coldShiverBlend;
            entityState.FootContactOffset = footContactOffset;
            entityState.HandContactOffset = handContactOffset;
            entityState.FootProbeDistanceScale = footProbeDistanceScale;
            entityState.HandProbeDistanceScale = handProbeDistanceScale;
            entityState.GroundLayerMask = groundMask.value;
            entityState.WallLayerMask = wallMask.value;
            entityState.TunnelClearanceDistance = tunnelClearanceDistance;
            entityState.HandBraceFadeDistance = handBraceFadeDistance;
            entityState.TargetPositionSharpness = targetPositionSharpness;
            entityState.TargetNormalSharpness = targetNormalSharpness;
            entityState.BlendFadeSharpness = blendFadeSharpness;
            entityState.MaxDeltaHeight = maxDeltaHeight;
            entityState.ComShiftLateralFactor = comShiftLateralFactor;
            entityState.ComShiftForwardFactor = comShiftForwardFactor;
            entityState.ComShiftVerticalFactor = comShiftVerticalFactor;
            entityState.ComResponseSharpness = comResponseSharpness;
            entityState.ComLeanPitchRadians = math.radians(comLeanPitchDegrees);
            entityState.ComLeanRollRadians = math.radians(comLeanRollDegrees);
            entityState.MaxComLateral = maxComLateral;
            entityState.MaxComForward = maxComForward;
            entityState.MaxComVertical = maxComVertical;
            entityState.UpdateThisFrame = updateThisFrame;
            entityState.ViewerDistanceSq = viewerDistanceSq;
            entityState.UpdateBitfield = updateBitfield;
            entityState.ThrottleTier = throttleTier;
            return true;
        }

        private void TickToolHandTransientState(float deltaTime)
        {
            float safeDeltaTime = math.max(0.0001f, deltaTime);
            float recoilDecay = math.rcp(1.0f + (math.max(0.0f, toolRecoilDecaySharpness) * safeDeltaTime));
            _leftToolRecoilOffset *= recoilDecay;
            _rightToolRecoilOffset *= recoilDecay;
            TickColdShiverState(safeDeltaTime);

            if (_terminalRightHandActive)
            {
                _terminalRightHandHoldTimer = math.max(0.0f, _terminalRightHandHoldTimer - safeDeltaTime);
                if (_terminalRightHandHoldTimer <= 0.0f)
                    _terminalRightHandTargetBlend = 0.0f;
            }

            _terminalRightHandBlend = ContextualPhysicalIkMath.SmoothScalar(
                _terminalRightHandBlend,
                _terminalRightHandActive ? _terminalRightHandTargetBlend : 0.0f,
                terminalSnapBlendSharpness,
                safeDeltaTime);

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
            if (!GlobalSignals.TryGetLatestPlayerStressSignal(out PlayerStressSignal signal, out int sequence) ||
                sequence == _lastPlayerStressSignalSequence)
            {
                return;
            }

            _lastPlayerStressSignalSequence = sequence;
            _playerStress01 = math.saturate(signal.Stress01);
        }

        private void TickBreathingState(float deltaTime, bool lowTier)
        {
            float safeDeltaTime = math.max(0.0001f, deltaTime);
            float targetBlend = enableProceduralBreathing ? 1.0f : 0.0f;
            _breathingBlend = ContextualPhysicalIkMath.SmoothScalar(
                _breathingBlend,
                targetBlend,
                breathingBlendSharpness,
                safeDeltaTime);

            if (_breathingBlend <= 0.0001f)
                return;

            float rate = math.max(0.0f, breathingBaseRateHz) + _playerStress01 * math.max(0.0f, breathingStressRateHz);
            if (lowTier)
                rate *= 0.75f;
            _breathingPhase += rate * safeDeltaTime;
            if (_breathingPhase >= BreathingPhaseWrap)
                _breathingPhase -= BreathingPhaseWrap;
        }

        private void TickExternalSqueezePoleState(float deltaTime)
        {
            float safeDeltaTime = math.max(0.0001f, deltaTime);
            _externalSqueezePoleHoldTimer = math.max(0.0f, _externalSqueezePoleHoldTimer - safeDeltaTime);
            float targetBlend = _externalSqueezePoleHoldTimer > 0.0f ? 1.0f : 0.0f;
            _externalSqueezePoleBlend = ContextualPhysicalIkMath.SmoothScalar(
                _externalSqueezePoleBlend,
                targetBlend,
                predictiveRepairBlendSharpness,
                safeDeltaTime);
        }

        private void ApplyExternalSqueezePoleBias()
        {
            if (!_twoBoneSetups.IsCreated || _twoBoneSetups.Length < 4)
                return;

            float blend = math.saturate(_externalSqueezePoleBlend);
            ContextualPhysicalIkTwoBoneSetup leftArm = _twoBoneSetups[2];
            ContextualPhysicalIkTwoBoneSetup rightArm = _twoBoneSetups[3];
            leftArm.PoleLocalOffset = ResolveSqueezePoleLocalOffset(_baseLeftArmPoleLocalOffset, blend, 1.0f);
            rightArm.PoleLocalOffset = ResolveSqueezePoleLocalOffset(_baseRightArmPoleLocalOffset, blend, -1.0f);
            _twoBoneSetups[2] = leftArm;
            _twoBoneSetups[3] = rightArm;
        }

        private static float3 ResolveSqueezePoleLocalOffset(float3 baseOffset, float blend, float fallbackSideSign)
        {
            float safeBlend = math.saturate(blend);
            if (safeBlend <= 0.0001f)
                return baseOffset;

            float lateral = baseOffset.x;
            float lateralMagnitude = math.abs(lateral);
            float direction = lateralMagnitude > 0.0001f
                ? -math.sign(lateral)
                : -math.sign(fallbackSideSign);
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
            _coldShiverBlend = ContextualPhysicalIkMath.SmoothScalar(
                _coldShiverBlend,
                targetBlend,
                coldShiverBlendSharpness,
                deltaTime);

            if (_coldShiverBlend <= 0.0001f)
                return;

            _coldShiverPhase += math.max(0.0f, coldShiverFrequencyHz) * deltaTime;
            if (_coldShiverPhase >= ColdShiverPhaseWrap)
                _coldShiverPhase -= ColdShiverPhaseWrap;
        }

        private float ResolveColdShiverTargetBlend()
        {
            if (!enableColdShiver)
                return 0.0f;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonSurvivalSystem survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            if (survivalSystem == null)
                return 0.0f;

            float environmentTemperature = survivalSystem.EnvironmentTemperature;
            if (!math.isfinite(environmentTemperature))
                return 0.0f;

            float coldByEnvironment = math.saturate(
                (coldShiverTemperatureThresholdCelsius - environmentTemperature) *
                math.rcp(math.max(1.0f, coldShiverFullDeltaCelsius)));
            float coldByPhysiology = math.saturate(survivalSystem.ColdStressSeverity01);
            return math.max(coldByEnvironment, coldByPhysiology);
        }

        private void ResolveColdShiverOffsets(float3 rootRight, float3 rootUp, out float3 leftOffset, out float3 rightOffset)
        {
            leftOffset = float3.zero;
            rightOffset = float3.zero;

            float amplitude = math.max(0.0f, coldShiverAmplitudeMeters);
            if (amplitude <= 0.000001f || _coldShiverBlend <= 0.0001f)
                return;

            float phase = _coldShiverPhase;
            float leftLateral = CinematicMath.FastTriangleWaveSigned(phase) * amplitude;
            float leftVertical = CinematicMath.FastTriangleWaveSigned((phase * 1.733f) + 0.23f) * amplitude * 0.45f;
            float rightLateral = CinematicMath.FastTriangleWaveSigned(phase + 0.41f) * amplitude;
            float rightVertical = CinematicMath.FastTriangleWaveSigned((phase * 1.619f) + 0.67f) * amplitude * 0.45f;

            leftOffset = (rootRight * leftLateral) + (rootUp * leftVertical);
            rightOffset = (rootRight * -rightLateral) + (rootUp * rightVertical);
        }

        private void CapturePredictiveRepairLatch(float deltaTime, bool wallTouchEnabled)
        {
            Transform leftSource = leftControllerProbe != null ? leftControllerProbe : leftHandProbe;
            Transform rightSource = rightControllerProbe != null ? rightControllerProbe : rightHandProbe;
            Vector3 leftPosition = leftSource != null ? leftSource.position : Vector3.zero;
            Vector3 rightPosition = rightSource != null ? rightSource.position : Vector3.zero;
            AbsoluteUniversePosition leftAup = leftSource != null ? AbsoluteUniversePosition.FromRuntimePosition(leftPosition) : default;
            AbsoluteUniversePosition rightAup = rightSource != null ? AbsoluteUniversePosition.FromRuntimePosition(rightPosition) : default;

            Vector3 leftVelocity = Vector3.zero;
            Vector3 rightVelocity = Vector3.zero;
            float safeDeltaTime = math.max(deltaTime, 0.0001f);
            if (_hasPreviousLeftPredictiveControllerPose && leftSource != null)
                leftVelocity = ResolveAupVelocity(in leftAup, in _previousLeftControllerAup, safeDeltaTime);
            if (_hasPreviousRightPredictiveControllerPose && rightSource != null)
                rightVelocity = ResolveAupVelocity(in rightAup, in _previousRightControllerAup, safeDeltaTime);

            if (leftSource != null)
            {
                _previousLeftControllerAup = leftAup;
                _hasPreviousLeftPredictiveControllerPose = true;
            }
            else
            {
                _hasPreviousLeftPredictiveControllerPose = false;
            }

            if (rightSource != null)
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
                _predictiveLeftHandBlend = ContextualPhysicalIkMath.SmoothScalar(_predictiveLeftHandBlend, 0.0f, predictiveRepairBlendSharpness, safeDeltaTime);
                _predictiveRightHandBlend = ContextualPhysicalIkMath.SmoothScalar(_predictiveRightHandBlend, 0.0f, predictiveRepairBlendSharpness, safeDeltaTime);
            }

            ApplyExternalWallHandTargetsToPredictiveLatch(safeDeltaTime, wallTouchEnabled);
        }

        private void ApplyExternalWallHandTargetsToPredictiveLatch(float deltaTime, bool wallTouchEnabled)
        {
            bool hasExternalWallTargets =
                _externalWallLeftHandHoldTimer > 0.0f ||
                _externalWallRightHandHoldTimer > 0.0f;
            if (!wallTouchEnabled && !hasExternalWallTargets)
            {
                _externalWallLeftHandBlend = ContextualPhysicalIkMath.SmoothScalar(_externalWallLeftHandBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                _externalWallRightHandBlend = ContextualPhysicalIkMath.SmoothScalar(_externalWallRightHandBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                _externalWallLeftHandHoldTimer = 0.0f;
                _externalWallRightHandHoldTimer = 0.0f;
                return;
            }

            if (_externalWallLeftHandHoldTimer <= 0.0f || !leftHandEmptyForWallTouch)
            {
                _externalWallLeftHandBlend = ContextualPhysicalIkMath.SmoothScalar(_externalWallLeftHandBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                _externalWallLeftHandHoldTimer = 0.0f;
            }
            else
            {
                _externalWallLeftHandHoldTimer = math.max(0.0f, _externalWallLeftHandHoldTimer - math.max(0.0f, deltaTime));
                if (_externalWallLeftHandBlend > _predictiveLeftHandBlend && IsFiniteVector(_externalWallLeftHandPosition))
                {
                    _predictiveLeftHandPosition = _externalWallLeftHandPosition;
                    _predictiveLeftHandNormal = NormalizeVectorNoSqrt(_externalWallLeftHandNormal, Vector3.up);
                    _predictiveLeftHandBlend = _externalWallLeftHandBlend;
                }
            }

            if (_externalWallRightHandHoldTimer <= 0.0f)
            {
                _externalWallRightHandBlend = ContextualPhysicalIkMath.SmoothScalar(_externalWallRightHandBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
            }
            else
            {
                _externalWallRightHandHoldTimer = math.max(0.0f, _externalWallRightHandHoldTimer - math.max(0.0f, deltaTime));
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
                predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                return;
            }

            if (!IsFiniteVector(controllerPosition))
            {
                predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                return;
            }

            if (!_predictiveRepairTarget.TryResolveRepairSnapPoints(
                    controllerPosition,
                    out AbsoluteUniversePosition leftHandAup,
                    out AbsoluteUniversePosition rightHandAup,
                    out _))
            {
                predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                return;
            }

            AbsoluteUniversePosition targetAup = isLeftHand ? leftHandAup : rightHandAup;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in controllerAup, in targetAup);
            if (distanceSq > PredictiveRepairLatchDistanceSq)
            {
                predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                return;
            }

            float3 targetRuntime = targetAup.ToRuntimeFloat3();
            float3 controllerRuntime = ContextualPhysicalIkMath.ToFloat3(controllerPosition);
            float3 targetVector = targetRuntime - controllerRuntime;
            float3 targetDirection = ContextualPhysicalIkMath.SafeNormalize(targetVector, new float3(0.0f, 0.0f, 1.0f));
            float3 velocityDirection = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(controllerVelocity), float3.zero);
            float directionDot = math.dot(velocityDirection, targetDirection);
            float requiredDot = math.saturate(predictiveRepairDirectionDot);
            if (directionDot < requiredDot)
            {
                predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, 0.0f, predictiveRepairBlendSharpness, deltaTime);
                return;
            }

            Vector3 fallbackNormal = (Vector3)ContextualPhysicalIkMath.SafeNormalize(controllerRuntime - targetRuntime, new float3(0.0f, 1.0f, 0.0f));

            float range01 = math.saturate(1.0f - ((float)distanceSq * math.rcp(PredictiveRepairLatchDistanceSq)));
            float direction01 = math.saturate((directionDot - requiredDot) * math.rcp(math.max(1.0f - requiredDot, 0.0001f)));
            float targetBlend = range01 * direction01;
            predictivePosition = (Vector3)targetRuntime;
            predictiveNormal = fallbackNormal;
            predictiveBlend = ContextualPhysicalIkMath.SmoothScalar(predictiveBlend, targetBlend, predictiveRepairBlendSharpness, deltaTime);
        }

        private static Vector3 ResolveAupVelocity(
            in AbsoluteUniversePosition currentAup,
            in AbsoluteUniversePosition previousAup,
            float deltaTime)
        {
            float safeDeltaTime = math.max(deltaTime, 0.0001f);
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

            Camera playerCamera = GlobalRegistry.Player != null ? GlobalRegistry.Player.PlayerCamera : null;
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

            _upperArmCullTimer += math.max(0.0f, deltaTime);
            if (_upperArmCullTimer >= math.max(0.01f, upperArmCullHysteresisSeconds) && _upperArmRenderersVisible)
                SetUpperArmRenderersVisible(false);
        }

        private bool IsAnyUpperArmRendererInViewCone(Transform cameraTransform)
        {
            float3 cameraPosition = ContextualPhysicalIkMath.ToFloat3(cameraTransform.position);
            float3 cameraForward = ContextualPhysicalIkMath.ToFloat3(cameraTransform.forward);
            float minimumForwardDot = math.max(0.0f, upperArmFovDotThreshold);
            float minimumForwardDotSq = minimumForwardDot * minimumForwardDot;
            for (int i = 0; i < upperArmRenderers.Length; i++)
            {
                Renderer renderer = upperArmRenderers[i];
                if (renderer == null)
                    continue;

                float3 direction = ContextualPhysicalIkMath.ToFloat3(renderer.bounds.center) - cameraPosition;
                float distanceSq = math.lengthsq(direction);
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

            _streamHandles = new NativeArray<TransformStreamHandle>(
                totalHandleCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<TransformStreamHandle>[dynamic] - sequential cached stream handles for contextual IK bones - owner: ContextualPhysicalIkRig
            _twoBoneSetups = new NativeArray<ContextualPhysicalIkTwoBoneSetup>(
                4,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTwoBoneSetup>[4] - fixed humanoid limb solve descriptors - owner: ContextualPhysicalIkRig

            if (validAppendageChainCount > 0)
            {
                _appendageChainRuntimes = new NativeArray<ContextualPhysicalIkAppendageChainRuntime>(
                    validAppendageChainCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkAppendageChainRuntime>[dynamic] - appendage FABRIK descriptors - owner: ContextualPhysicalIkRig
                _appendageSegmentLengths = new NativeArray<float>(
                    totalAppendageLengthCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[dynamic] - appendage segment lengths - owner: ContextualPhysicalIkRig
                _appendageTargets = new NativeArray<ContextualPhysicalIkAppendageTarget>(
                    validAppendageChainCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkAppendageTarget>[dynamic] - appendage target positions and weights - owner: ContextualPhysicalIkRig
                _appendageScratchPositions = new NativeArray<float3>(
                    totalAppendageScratchCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[dynamic] - appendage FABRIK scratch positions - owner: ContextualPhysicalIkRig
                _appendageTargetSources = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage target source cache - owner: ContextualPhysicalIkRig
                _appendageFallbackTips = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage fallback tip cache - owner: ContextualPhysicalIkRig
            }

            if (validAppendageChainCount > 0)
            {
                _appendageVoxelVolumes = new HectonVoxelVolume[validAppendageChainCount]; // COLD ALLOC: HectonVoxelVolume[dynamic] - appendage voxel snap owners - owner: ContextualPhysicalIkRig
                _appendageSurfaceNormalSources = new Transform[validAppendageChainCount]; // COLD ALLOC: Transform[dynamic] - appendage wall-normal source cache - owner: ContextualPhysicalIkRig
            }

            if (hasValidSpineChain)
            {
                _spineChainRuntimes = new NativeArray<ContextualPhysicalIkSpineChainRuntime>(
                    1,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSpineChainRuntime>[1] - spline spine chain descriptor - owner: ContextualPhysicalIkRig
                _spineTargets = new NativeArray<float3>(
                    SpineTargetCountPerChain,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[3] - chest/head spline targets - owner: ContextualPhysicalIkRig
            }

            if (validSecondaryChainCount > 0)
            {
                _secondaryChainRuntimes = new NativeArray<ContextualPhysicalIkSecondaryChainRuntime>(
                    validSecondaryChainCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSecondaryChainRuntime>[dynamic] - secondary motion chain descriptors - owner: ContextualPhysicalIkRig
                _secondaryStates = new NativeArray<ContextualPhysicalIkSecondaryState>(
                    totalSecondaryStateCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkSecondaryState>[dynamic] - secondary motion positions and velocities - owner: ContextualPhysicalIkRig
            }

            _cachedLocalPoseStates = new NativeArray<ContextualPhysicalIkCachedPoseState>(
                totalHandleCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkCachedPoseState>[dynamic] - cached limb and appendage local pose states - owner: ContextualPhysicalIkRig
            _muscleBulgeOutput = new NativeArray<float>(
                1,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - previous-frame muscle tension signal - owner: ContextualPhysicalIkRig

            RegisterNativeMemorySentinel();
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
                    Iterations = math.max(1, authoring.iterations),
                    Tolerance = math.max(0.0001f, authoring.tolerance),
                    Blend = math.saturate(authoring.blend),
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
                Blend = math.saturate(validSpineChain.blend),
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
                    _secondaryStates[stateWriteIndex + boneIndex] = new ContextualPhysicalIkSecondaryState
                    {
                        Position = ContextualPhysicalIkMath.ToFloat3(authoring.bones[boneIndex].position),
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
                    Stiffness = math.max(0.0f, authoring.stiffness),
                    Damping = math.max(0.0f, authoring.damping),
                    Blend = math.saturate(authoring.blend),
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
                PelvisPositionBlend = pelvisPositionBlend,
                PelvisRotationBlend = pelvisRotationBlend,
                OverExtensionResistanceRadians = math.radians(overExtensionResistanceDegrees),
            };
        }

        private void UpdateJobDataTargetFrames()
        {
            if (!_animationInjected || !_ikPlayable.IsValid())
                return;

            ContextualPhysicalIkApplyJob job = _ikPlayable.GetJobData<ContextualPhysicalIkApplyJob>();
            job.TargetFrames = _currentTargetFrames;
            job.EntityIndex = _entitySlot;
            _ikPlayable.SetJobData(job);
        }

        private void CaptureSpineTargets(bool lowTier)
        {
            if (!_spineChainRuntimes.IsCreated || !_spineTargets.IsCreated)
                return;

            if (!TryGetValidSpineChain(out SpineChainAuthoring validSpineChain))
                return;

            Transform headSource = validSpineChain.headTarget != null
                ? validSpineChain.headTarget
                : validSpineChain.bones[validSpineChain.bones.Length - 1];
            Vector3 headPosition = headSource.position;
            if (!IsFiniteVector(headPosition))
                return;

            AbsoluteUniversePosition headAup = AbsoluteUniversePosition.FromRuntimePosition(headPosition);
            double3 headAbsolute = headAup.ToAbsoluteDouble3();
            Quaternion hmdRotation = headSource.rotation;

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
                float wave = lowTier
                    ? CinematicMath.FastTriangleWaveSigned(_breathingPhase)
                    : math.sin(_breathingPhase * 6.28318530718f);
                float amplitude = math.max(0.0f, breathingAmplitudeMeters) * _breathingBlend * (0.45f + _playerStress01 * 0.55f);
                float jitter = CinematicMath.FastTriangleWaveSigned((_breathingPhase * 3.17f) + 0.19f) *
                    math.max(0.0f, breathingStressJitterMeters) *
                    _playerStress01 *
                    _breathingBlend;
                float3 breathOffset = hmdUp * (wave * amplitude) + hmdRight * jitter;
                chestTarget += breathOffset;
                headTarget += breathOffset * 0.35f;
                forwardTarget += breathOffset * 0.2f;
            }

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
            if (!_appendageChainRuntimes.IsCreated || !_appendageTargets.IsCreated)
                return;

            for (int chainIndex = 0; chainIndex < _appendageChainRuntimes.Length; chainIndex++)
            {
                Transform targetSource = _appendageTargetSources[chainIndex];
                Transform fallbackTip = _appendageFallbackTips[chainIndex];
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
                    Vector3 targetNormal = normalSource != null ? normalSource.up : Vector3.up;
                    if (voxelVolume.TryGetNearestCorner(targetPosition, targetNormal, out Vector3 snappedCorner))
                        targetPosition = snappedCorner;
                }

                _appendageTargets[chainIndex] = new ContextualPhysicalIkAppendageTarget
                {
                    Position = ContextualPhysicalIkMath.ToFloat3(targetPosition),
                    Weight = weight,
                };
            }
        }

        private void ApplyMuscleBulgeSignal(float deltaTime)
        {
            if (!_muscleBulgeOutput.IsCreated || !_muscleBulgeMaterialInitialized || _muscleBulgeMaterialInstance == null)
                return;

            float safeDeltaTime = math.max(0.0001f, deltaTime);
            float targetBulge = math.saturate(_muscleBulgeOutput[0] * muscleBulgeScale);
            _muscleBulgeCurrent = ContextualPhysicalIkMath.SmoothScalar(_muscleBulgeCurrent, targetBulge, muscleBulgeSharpness, safeDeltaTime);
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
            DisposeNativeArray(ref _streamHandles);
            DisposeNativeArray(ref _twoBoneSetups);
            DisposeNativeArray(ref _appendageChainRuntimes);
            DisposeNativeArray(ref _appendageSegmentLengths);
            DisposeNativeArray(ref _appendageTargets);
            DisposeNativeArray(ref _appendageScratchPositions);
            DisposeNativeArray(ref _spineChainRuntimes);
            DisposeNativeArray(ref _spineTargets);
            DisposeNativeArray(ref _secondaryChainRuntimes);
            DisposeNativeArray(ref _secondaryStates);
            DisposeNativeArray(ref _cachedLocalPoseStates);
            DisposeNativeArray(ref _muscleBulgeOutput);
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

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_streamHandles, NativeMemoryOwner, nameof(_streamHandles), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_twoBoneSetups, NativeMemoryOwner, nameof(_twoBoneSetups), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_appendageChainRuntimes, NativeMemoryOwner, nameof(_appendageChainRuntimes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_appendageSegmentLengths, NativeMemoryOwner, nameof(_appendageSegmentLengths), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_appendageTargets, NativeMemoryOwner, nameof(_appendageTargets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_appendageScratchPositions, NativeMemoryOwner, nameof(_appendageScratchPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_spineChainRuntimes, NativeMemoryOwner, nameof(_spineChainRuntimes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_spineTargets, NativeMemoryOwner, nameof(_spineTargets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_secondaryChainRuntimes, NativeMemoryOwner, nameof(_secondaryChainRuntimes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_secondaryStates, NativeMemoryOwner, nameof(_secondaryStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_cachedLocalPoseStates, NativeMemoryOwner, nameof(_cachedLocalPoseStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_muscleBulgeOutput, NativeMemoryOwner, nameof(_muscleBulgeOutput), NativeMemoryLifetime);
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
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
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

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(default);
            array = default;
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

            _muscleBulgeSharedMaterials = muscleBulgeRenderer.sharedMaterials;
            if (_muscleBulgeSharedMaterials == null ||
                muscleBulgeMaterialSlot < 0 ||
                muscleBulgeMaterialSlot >= _muscleBulgeSharedMaterials.Length)
            {
                return false;
            }

            _muscleBulgeOriginalMaterial = _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot];
            if (_muscleBulgeOriginalMaterial == null || !_muscleBulgeOriginalMaterial.HasProperty(MuscleBulgeShaderId))
                return false;

            _muscleBulgeMaterialInstance = new Material(_muscleBulgeOriginalMaterial); // COLD ALLOC: Material[1] - per-rig muscle bulge material instance - owner: ContextualPhysicalIkRig
            _muscleBulgeMaterialInstance.SetFloat(MuscleBulgeShaderId, 0.0f);
            _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot] = _muscleBulgeMaterialInstance;
            muscleBulgeRenderer.sharedMaterials = _muscleBulgeSharedMaterials;
            _muscleBulgeCurrent = 0.0f;
            _muscleBulgeMaterialInitialized = true;
            return true;
        }

        private void ReleaseMuscleBulgeMaterial()
        {
            if (muscleBulgeRenderer != null &&
                _muscleBulgeSharedMaterials != null &&
                muscleBulgeMaterialSlot >= 0 &&
                muscleBulgeMaterialSlot < _muscleBulgeSharedMaterials.Length &&
                _muscleBulgeOriginalMaterial != null)
            {
                _muscleBulgeSharedMaterials[muscleBulgeMaterialSlot] = _muscleBulgeOriginalMaterial;
                muscleBulgeRenderer.sharedMaterials = _muscleBulgeSharedMaterials;
            }

            if (_muscleBulgeMaterialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_muscleBulgeMaterialInstance);
                else
                    DestroyImmediate(_muscleBulgeMaterialInstance);
            }

            _muscleBulgeMaterialInstance = null;
            _muscleBulgeSharedMaterials = null;
            _muscleBulgeOriginalMaterial = null;
            _muscleBulgeCurrent = 0.0f;
            _muscleBulgeMaterialInitialized = false;
        }

        private static void ResolveThrottleState(
            uint frameIndex,
            int entityId,
            float viewerDistanceSq,
            out int updateThisFrame,
            out byte throttleTier,
            out uint updateBitfield)
        {
            uint entityBits = (uint)math.max(0, entityId);

            if (viewerDistanceSq > Tier1DistanceMaxSq)
            {
                throttleTier = 2;
                updateBitfield = 0x3u;
                updateThisFrame = ((entityBits & updateBitfield) == (frameIndex & updateBitfield)) ? 1 : 0;
                return;
            }

            if (viewerDistanceSq > Tier0DistanceMaxSq)
            {
                throttleTier = 1;
                updateBitfield = 0x1u;
                updateThisFrame = ((entityBits & updateBitfield) == (frameIndex & updateBitfield)) ? 1 : 0;
                return;
            }

            throttleTier = 0;
            updateBitfield = 0u;
            updateThisFrame = 1;
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
                BaseBlend = math.saturate(baseBlend),
                ReachSafetyMargin = reachSafetyMargin,
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
            float3 delta = ContextualPhysicalIkMath.ToFloat3(firstPosition - secondPosition);
            float lengthSq = math.lengthsq(delta);
            return lengthSq * math.rsqrt(math.max(lengthSq, 0.00000001f));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z) || float.IsInfinity(value.w));
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
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

            float safeMaxLength = math.max(0.0f, maxLength);
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

            Quaternion inverseParentRotation = Quaternion.Inverse(parent.rotation);
            Vector3 localOffset = inverseParentRotation * (source.position - parent.position);
            if (localOffset.sqrMagnitude <= 0.0001f)
                localOffset = new Vector3(0.0f, 0.0f, 0.25f);

            return ContextualPhysicalIkMath.ToFloat3(localOffset);
        }
    }
}
