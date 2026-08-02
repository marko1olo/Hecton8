using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ContextualPhysicalIkContactTarget
    {
        [FieldOffset(0)]
        public float3 WorldPosition;
        [FieldOffset(12)]
        public float3 WorldNormal;
        [FieldOffset(24)]
        public float Blend;
        [FieldOffset(28)]
        public float DeltaHeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct ContextualPhysicalIkTargetFrame
    {
        [FieldOffset(0)]
        public ContextualPhysicalIkContactTarget LeftFoot;
        [FieldOffset(32)]
        public ContextualPhysicalIkContactTarget RightFoot;
        [FieldOffset(64)]
        public ContextualPhysicalIkContactTarget LeftHand;
        [FieldOffset(96)]
        public ContextualPhysicalIkContactTarget RightHand;
        [FieldOffset(128)]
        public float3 ComOffsetLocal;
        [FieldOffset(140)]
        public float2 ComLeanRadians;
        [FieldOffset(148)]
        public float DeltaTime;
        [FieldOffset(152)]
        public float ViewerDistanceSq;
        [FieldOffset(156)]
        public float TunnelBlend;
        [FieldOffset(160)]
        public uint UpdateBitfield;
        [FieldOffset(164)]
        public byte ContextMask;
        [FieldOffset(165)]
        public byte ThrottleTier;
        [FieldOffset(166)]
        public byte ShouldComputeThisFrame;
        [FieldOffset(167)]
        public byte LowerBodyFlags;
        [FieldOffset(168)]
        public float PelvisYawRadians;
        [FieldOffset(172)]
        private uint _pad0;
        [FieldOffset(176)]
        private ulong _pad1;
        [FieldOffset(184)]
        private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 512)]
    internal struct ContextualPhysicalIkEntityState
    {
        [FieldOffset(0)] public int IsActive;
        [FieldOffset(4)] public int EnableFootPlacement;
        [FieldOffset(8)] public int EnableHandBracing;
        [FieldOffset(12)] public int EnableWallTouch;
        [FieldOffset(16)] public int LeftHandEmpty;
        [FieldOffset(20)] public int EnableToolRetraction;
        [FieldOffset(24)] public int HasCameraPose;
        [FieldOffset(28)] public float DeltaTime;
        [FieldOffset(32)] public quaternion RootRotation;
        [FieldOffset(48)] public float3 RootPosition;
        [FieldOffset(60)] public float3 PelvisPosition;
        [FieldOffset(72)] public float3 LeftFootProbeOrigin;
        [FieldOffset(84)] public float3 RightFootProbeOrigin;
        [FieldOffset(96)] public float3 LeftHandProbeOrigin;
        [FieldOffset(108)] public float3 RightHandProbeOrigin;
        [FieldOffset(120)] public float3 PredictiveLeftHandPosition;
        [FieldOffset(132)] public float3 PredictiveRightHandPosition;
        [FieldOffset(144)] public float3 PredictiveLeftHandNormal;
        [FieldOffset(156)] public float3 PredictiveRightHandNormal;
        [FieldOffset(168)] public float3 CameraPosition;
        [FieldOffset(180)] public float3 CameraForward;
        [FieldOffset(192)] public float3 CameraUp;
        [FieldOffset(204)] public float3 CameraRight;
        [FieldOffset(216)] public float3 KccVelocity;
        [FieldOffset(228)] public float3 LeftToolRecoilOffset;
        [FieldOffset(240)] public float3 RightToolRecoilOffset;
        [FieldOffset(252)] public float3 LeftColdShiverOffset;
        [FieldOffset(264)] public float3 RightColdShiverOffset;
        [FieldOffset(276)] public float3 DashboardRightHandPosition;
        [FieldOffset(288)] public float3 DashboardRightHandNormal;
        [FieldOffset(300)] public float LeftLegReach;
        [FieldOffset(304)] public float RightLegReach;
        [FieldOffset(308)] public float LeftArmReach;
        [FieldOffset(312)] public float RightArmReach;
        [FieldOffset(316)] public float PredictiveLeftHandBlend;
        [FieldOffset(320)] public float PredictiveRightHandBlend;
        [FieldOffset(324)] public float CameraHandLateralOffset;
        [FieldOffset(328)] public float CameraHandVerticalOffset;
        [FieldOffset(332)] public float ToolCollisionDistance;
        [FieldOffset(336)] public float ToolRetractionBackDistance;
        [FieldOffset(340)] public float ToolRetractionLiftDistance;
        [FieldOffset(344)] public float ToolRetractionBlend;
        [FieldOffset(348)] public float ToolRecoilMaxOffset;
        [FieldOffset(352)] public float DashboardRightHandBlend;
        [FieldOffset(356)] public float ColdShiverBlend;
        [FieldOffset(360)] public float FootContactOffset;
        [FieldOffset(364)] public float HandContactOffset;
        [FieldOffset(368)] public float FootProbeDistanceScale;
        [FieldOffset(372)] public float HandProbeDistanceScale;
        [FieldOffset(376)] public int GroundLayerMask;
        [FieldOffset(380)] public int WallLayerMask;
        [FieldOffset(384)] public float TunnelClearanceDistance;
        [FieldOffset(388)] public float HandBraceFadeDistance;
        [FieldOffset(392)] public float TargetPositionSharpness;
        [FieldOffset(396)] public float TargetNormalSharpness;
        [FieldOffset(400)] public float BlendFadeSharpness;
        [FieldOffset(404)] public float MaxDeltaHeight;
        [FieldOffset(408)] public float ComShiftLateralFactor;
        [FieldOffset(412)] public float ComShiftForwardFactor;
        [FieldOffset(416)] public float ComShiftVerticalFactor;
        [FieldOffset(420)] public float ComResponseSharpness;
        [FieldOffset(424)] public float ComLeanPitchRadians;
        [FieldOffset(428)] public float ComLeanRollRadians;
        [FieldOffset(432)] public float MaxComLateral;
        [FieldOffset(436)] public float MaxComForward;
        [FieldOffset(440)] public float MaxComVertical;
        [FieldOffset(444)] public int UpdateThisFrame;
        [FieldOffset(448)] public float ViewerDistanceSq;
        [FieldOffset(452)] public uint UpdateBitfield;
        [FieldOffset(456)] public uint FrameIndex;
        [FieldOffset(460)] public int EntitySlot;
        [FieldOffset(464)] public int IsXrActive;
        [FieldOffset(468)] public byte ThrottleTier;
        [FieldOffset(469)] private byte _pad0;
        [FieldOffset(470)] private byte _pad1;
        [FieldOffset(471)] private byte _pad2;
        [FieldOffset(472)] private ulong _pad3;
        [FieldOffset(480)] private ulong _pad4;
        [FieldOffset(488)] private ulong _pad5;
        [FieldOffset(496)] private ulong _pad6;
        [FieldOffset(504)] private ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct ContextualPhysicalIkTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint StateHash;
        [FieldOffset(12)] public ushort ActiveEntities;
        [FieldOffset(14)] public ushort Reserved;
        [FieldOffset(16)] public float3 FirstRootPosition;
        [FieldOffset(28)] public float3 FirstLeftFootTarget;
        [FieldOffset(40)] public float3 FirstRightFootTarget;
        [FieldOffset(52)] public float3 FirstLeftHandTarget;
        [FieldOffset(64)] public float3 FirstRightHandTarget;
        [FieldOffset(76)] public float3 FirstKccVelocity;
        [FieldOffset(88)] public float2 FirstHandWeights;
    }

    internal static class ContextualPhysicalIkLowerBodyConstants
    {
        public const int FeetPerEntity = 2;
        public const int LeftFootIndex = 0;
        public const int RightFootIndex = 1;
        public const byte FlagGrounded = 1 << 0;
        public const byte FlagStepping = 1 << 1;
        public const byte FlagSwimming = 1 << 2;
        public const byte FlagInvalid = 1 << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct ContextualPhysicalIkFootData
    {
        [FieldOffset(0)] public float3 TargetPosition;
        [FieldOffset(12)] public float3 CurrentPosition;
        [FieldOffset(24)] public float3 StepStartPosition;
        [FieldOffset(36)] public float3 SurfaceNormal;
        [FieldOffset(48)] public float StepProgress01;
        [FieldOffset(52)] public float StepThresholdSq;
        [FieldOffset(56)] public float StepHeightMeters;
        [FieldOffset(60)] public float Blend;
        [FieldOffset(64)] public byte Flags;
        [FieldOffset(65)] public byte Side;
        [FieldOffset(66)] public ushort Reserved;
        [FieldOffset(68)] private uint _pad0;
        [FieldOffset(72)] private ulong _pad1;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ContextualPhysicalIkClearHitsJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<KinematicSurfaceHit> Hits;

        public void Execute(int index)
        {
            Hits[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ContextualPhysicalIkGroundResponseJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkEntityState> Entities;
        [ReadOnly, NoAlias] public NativeArray<KinematicSurfaceHit> Hits;
        [ReadOnly, NoAlias] public NativeArray<ContextualPhysicalIkTargetFrame> PreviousTargets;
        [NoAlias] public NativeArray<ContextualPhysicalIkTargetFrame> NextTargets;
        [NoAlias] public NativeArray<float3> IkTargets;
        [NoAlias] public NativeArray<float> IkWeights;
        [NoAlias] public NativeArray<ContextualPhysicalIkFootData> FootData;
        [NoAlias] public NativeArray<float3> FootTargets;
        [NoAlias] public NativeArray<float3> FootCurrentPos;

        public void Execute(int index)
        {
            int baseIkIndex = index * ContextualPhysicalIkRuntime.HandsPerEntity;
            int baseFootIndex = index * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity;
            ContextualPhysicalIkEntityState entity = Entities[index];
            if (entity.IsActive == 0)
            {
                NextTargets[index] = default;
                IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = float3.zero;
                IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = float3.zero;
                IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = 0.0f;
                IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = 0.0f;
                ClearFootSoa(baseFootIndex);
                return;
            }

            ContextualPhysicalIkTargetFrame previous = PreviousTargets[index];
            ContextualPhysicalIkTargetFrame next = previous;
            next.DeltaTime = entity.DeltaTime;
            next.ViewerDistanceSq = entity.ViewerDistanceSq;
            next.UpdateBitfield = entity.UpdateBitfield;
            next.ThrottleTier = entity.ThrottleTier;
            next.ShouldComputeThisFrame = entity.UpdateThisFrame != 0 ? (byte)1 : (byte)0;
            int baseHitIndex = index * ContextualPhysicalIkRuntime.RaysPerEntity;
            KinematicSurfaceHit leftFootHit = Hits[baseHitIndex + 0];
            KinematicSurfaceHit rightFootHit = Hits[baseHitIndex + 1];
            KinematicSurfaceHit leftHandHit = Hits[baseHitIndex + 2];
            KinematicSurfaceHit rightHandHit = Hits[baseHitIndex + 3];
            KinematicSurfaceHit leftToolHit = Hits[baseHitIndex + 4];
            KinematicSurfaceHit rightToolHit = Hits[baseHitIndex + 5];

            if (entity.UpdateThisFrame == 0)
            {
                SanitizeTargetFrame(ref next);
                SanitizeFootDataLane(baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex);
                SanitizeFootDataLane(baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex);
                WriteIkSoa(baseIkIndex, in next);
                WriteFootSoa(baseFootIndex, in next);
                NextTargets[index] = next;
                return;
            }

            ResolveLowerBodyPresence(
                ref next,
                in previous,
                in entity,
                in leftFootHit,
                in rightFootHit,
                baseFootIndex);

            float tunnelTargetBlend = entity.EnableHandBracing != 0 && entity.EnableWallTouch != 0
                ? ResolveBraceProxyTunnelBlend(in leftHandHit, in rightHandHit, in entity)
                : 0.0f;
            next.TunnelBlend = SmoothBlend(previous.TunnelBlend, tunnelTargetBlend, entity.BlendFadeSharpness, entity.DeltaTime);
            next.ContextMask = next.TunnelBlend > 0.05f ? (byte)0x01 : (byte)0x00;

            if (entity.EnableHandBracing != 0 && entity.EnableWallTouch != 0)
            {
                ResolveContactTarget(
                    ref next.LeftHand,
                    in previous.LeftHand,
                    in leftHandHit,
                    entity.LeftHandProbeOrigin,
                    entity.HandContactOffset,
                    next.TunnelBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);

                ResolveContactTarget(
                    ref next.RightHand,
                    in previous.RightHand,
                    in rightHandHit,
                    entity.RightHandProbeOrigin,
                    entity.HandContactOffset,
                    next.TunnelBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);
            }
            else
            {
                FadeOutTarget(ref next.LeftHand, in previous.LeftHand, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeOutTarget(ref next.RightHand, in previous.RightHand, entity.BlendFadeSharpness, entity.DeltaTime);
            }

            ApplyPredictiveLatch(
                ref next.LeftHand,
                in previous.LeftHand,
                entity.PredictiveLeftHandPosition,
                entity.PredictiveLeftHandNormal,
                entity.PredictiveLeftHandBlend,
                entity.TargetPositionSharpness,
                entity.TargetNormalSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyPredictiveLatch(
                ref next.RightHand,
                in previous.RightHand,
                entity.PredictiveRightHandPosition,
                entity.PredictiveRightHandNormal,
                entity.PredictiveRightHandBlend,
                entity.TargetPositionSharpness,
                entity.TargetNormalSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyToolRecoil(
                ref next.LeftHand,
                in previous.LeftHand,
                entity.LeftHandProbeOrigin,
                entity.LeftToolRecoilOffset,
                entity.ToolRecoilMaxOffset,
                entity.TargetPositionSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyToolRecoil(
                ref next.RightHand,
                in previous.RightHand,
                entity.RightHandProbeOrigin,
                entity.RightToolRecoilOffset,
                entity.ToolRecoilMaxOffset,
                entity.TargetPositionSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyToolRetraction(
                ref next.LeftHand,
                in previous.LeftHand,
                in leftToolHit,
                entity.LeftHandProbeOrigin,
                entity.CameraForward,
                entity.CameraUp,
                entity.ToolCollisionDistance,
                entity.ToolRetractionBackDistance,
                entity.ToolRetractionLiftDistance,
                entity.ToolRetractionBlend,
                entity.TargetPositionSharpness,
                entity.TargetNormalSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyToolRetraction(
                ref next.RightHand,
                in previous.RightHand,
                in rightToolHit,
                entity.RightHandProbeOrigin,
                entity.CameraForward,
                entity.CameraUp,
                entity.ToolCollisionDistance,
                entity.ToolRetractionBackDistance,
                entity.ToolRetractionLiftDistance,
                entity.ToolRetractionBlend,
                entity.TargetPositionSharpness,
                entity.TargetNormalSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyPredictiveLatch(
                ref next.RightHand,
                in previous.RightHand,
                entity.DashboardRightHandPosition,
                entity.DashboardRightHandNormal,
                entity.DashboardRightHandBlend,
                entity.TargetPositionSharpness,
                entity.TargetNormalSharpness,
                entity.BlendFadeSharpness,
                entity.DeltaTime);

            ApplyColdShiver(ref next.LeftHand, entity.LeftColdShiverOffset, entity.ColdShiverBlend);
            ApplyColdShiver(ref next.RightHand, entity.RightColdShiverOffset, entity.ColdShiverBlend);
            SanitizeTargetFrame(ref next);

            float leftDelta = next.LeftFoot.DeltaHeight * next.LeftFoot.Blend;
            float rightDelta = next.RightFoot.DeltaHeight * next.RightFoot.Blend;
            float deltaDifference = leftDelta - rightDelta;
            float dominantDelta = math.max(math.abs(leftDelta), math.abs(rightDelta));
            float lateralDirection = deltaDifference >= 0.0f ? -1.0f : 1.0f;
            float2 slopeLeanRadians = ResolveSlopeLeanRadians(in next, in entity);

            float targetLateral = math.clamp(math.abs(deltaDifference) * entity.ComShiftLateralFactor * lateralDirection, -entity.MaxComLateral, entity.MaxComLateral);
            float targetForward = math.clamp(dominantDelta * entity.ComShiftForwardFactor, 0.0f, entity.MaxComForward);
            float targetVertical = math.clamp(-dominantDelta * entity.ComShiftVerticalFactor, -entity.MaxComVertical, 0.0f);
            float pitch = math.clamp((dominantDelta * entity.ComLeanPitchRadians) + slopeLeanRadians.x, -entity.ComLeanPitchRadians, entity.ComLeanPitchRadians);
            float roll = math.clamp((-deltaDifference * entity.ComLeanRollRadians) + slopeLeanRadians.y, -entity.ComLeanRollRadians, entity.ComLeanRollRadians);
            float3 previousComOffset = SanitizeFloat3(previous.ComOffsetLocal, float3.zero);
            float2 previousComLean = SanitizeFloat2(previous.ComLeanRadians, float2.zero);

            next.ComOffsetLocal = ContextualPhysicalIkMath.SmoothVector(
                previousComOffset,
                new float3(targetLateral, targetVertical, targetForward),
                entity.ComResponseSharpness,
                entity.DeltaTime);

            next.ComLeanRadians = new float2(
                SmoothFiniteScalar(previousComLean.x, pitch, entity.ComResponseSharpness, entity.DeltaTime),
                SmoothFiniteScalar(previousComLean.y, roll, entity.ComResponseSharpness, entity.DeltaTime));
            SanitizeTargetFrame(ref next);

            WriteIkSoa(baseIkIndex, in next);
            NextTargets[index] = next;
        }

        private void WriteIkSoa(int baseIkIndex, in ContextualPhysicalIkTargetFrame frame)
        {
            bool leftPositionValid = math.all(math.isfinite(frame.LeftHand.WorldPosition));
            bool rightPositionValid = math.all(math.isfinite(frame.RightHand.WorldPosition));
            bool leftBlendValid = math.isfinite(frame.LeftHand.Blend);
            bool rightBlendValid = math.isfinite(frame.RightHand.Blend);
            IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = math.select(frame.LeftHand.WorldPosition, float3.zero, !leftPositionValid);
            IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = math.select(frame.RightHand.WorldPosition, float3.zero, !rightPositionValid);
            IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = math.select(SanitizeBlend(frame.LeftHand.Blend), 0.0f, !leftPositionValid || !leftBlendValid);
            IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = math.select(SanitizeBlend(frame.RightHand.Blend), 0.0f, !rightPositionValid || !rightBlendValid);
        }

        private void ClearFootSoa(int baseFootIndex)
        {
            int leftIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex;
            int rightIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex;
            if (FootTargets.IsCreated && rightIndex < FootTargets.Length)
            {
                FootTargets[leftIndex] = float3.zero;
                FootTargets[rightIndex] = float3.zero;
            }

            if (FootCurrentPos.IsCreated && rightIndex < FootCurrentPos.Length)
            {
                FootCurrentPos[leftIndex] = float3.zero;
                FootCurrentPos[rightIndex] = float3.zero;
            }

            if (FootData.IsCreated && rightIndex < FootData.Length)
            {
                FootData[leftIndex] = default;
                FootData[rightIndex] = default;
            }
        }

        private void WriteFootSoa(int baseFootIndex, in ContextualPhysicalIkTargetFrame frame)
        {
            int leftIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex;
            int rightIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex;
            float3 leftPosition = math.select(frame.LeftFoot.WorldPosition, float3.zero, !math.all(math.isfinite(frame.LeftFoot.WorldPosition)));
            float3 rightPosition = math.select(frame.RightFoot.WorldPosition, float3.zero, !math.all(math.isfinite(frame.RightFoot.WorldPosition)));
            if (FootTargets.IsCreated && rightIndex < FootTargets.Length)
            {
                FootTargets[leftIndex] = leftPosition;
                FootTargets[rightIndex] = rightPosition;
            }

            if (FootCurrentPos.IsCreated && rightIndex < FootCurrentPos.Length)
            {
                FootCurrentPos[leftIndex] = leftPosition;
                FootCurrentPos[rightIndex] = rightPosition;
            }
        }

        private void ResolveLowerBodyPresence(
            ref ContextualPhysicalIkTargetFrame next,
            in ContextualPhysicalIkTargetFrame previous,
            in ContextualPhysicalIkEntityState entity,
            in KinematicSurfaceHit leftFootHit,
            in KinematicSurfaceHit rightFootHit,
            int baseFootIndex)
        {
            next.PelvisYawRadians = ResolvePelvisYawRadians(in entity);
            next.LowerBodyFlags = 0;

            if (entity.EnableFootPlacement == 0)
            {
                FadeOutTarget(ref next.LeftFoot, in previous.LeftFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeOutTarget(ref next.RightFoot, in previous.RightFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeFootLane(baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex, in next.LeftFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeFootLane(baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex, in next.RightFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                WriteFootSoa(baseFootIndex, in next);
                return;
            }

            bool leftGrounded = TryBuildGroundFootCandidate(
                in leftFootHit,
                entity.LeftFootProbeOrigin,
                entity.FootContactOffset,
                entity.MaxDeltaHeight,
                out float3 leftTarget,
                out float3 leftNormal,
                out float leftDeltaHeight);
            bool rightGrounded = TryBuildGroundFootCandidate(
                in rightFootHit,
                entity.RightFootProbeOrigin,
                entity.FootContactOffset,
                entity.MaxDeltaHeight,
                out float3 rightTarget,
                out float3 rightNormal,
                out float rightDeltaHeight);

            byte leftFlags = leftGrounded ? ContextualPhysicalIkLowerBodyConstants.FlagGrounded : ContextualPhysicalIkLowerBodyConstants.FlagSwimming;
            byte rightFlags = rightGrounded ? ContextualPhysicalIkLowerBodyConstants.FlagGrounded : ContextualPhysicalIkLowerBodyConstants.FlagSwimming;
            float leftBlend = leftGrounded ? 1.0f : ContextualPhysicalIkRuntime.SwimFootBlend;
            float rightBlend = rightGrounded ? 1.0f : ContextualPhysicalIkRuntime.SwimFootBlend;
            if (!leftGrounded)
            {
                BuildSwimFootCandidate(in entity, -1.0f, out leftTarget, out leftNormal);
                leftDeltaHeight = 0.0f;
            }

            if (!rightGrounded)
            {
                BuildSwimFootCandidate(in entity, 1.0f, out rightTarget, out rightNormal);
                rightDeltaHeight = 0.0f;
            }

            int leftFootIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex;
            int rightFootIndex = baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex;
            ContextualPhysicalIkFootData leftData = ReadFootData(leftFootIndex);
            ContextualPhysicalIkFootData rightData = ReadFootData(rightFootIndex);
            bool leftStepping = IsStepping(in leftData);
            bool rightStepping = IsStepping(in rightData);
            if (leftStepping && rightStepping)
            {
                bool keepLeft = leftData.StepProgress01 <= rightData.StepProgress01;
                if (keepLeft)
                {
                    CancelStep(ref rightData);
                    rightStepping = false;
                }
                else
                {
                    CancelStep(ref leftData);
                    leftStepping = false;
                }
            }

            float stepThresholdSq = ResolveStepThresholdSq(in entity);
            bool leftWantsStep = leftGrounded && ShouldTriggerStep(in leftData, in previous.LeftFoot, leftTarget, stepThresholdSq);
            bool rightWantsStep = rightGrounded && ShouldTriggerStep(in rightData, in previous.RightFoot, rightTarget, stepThresholdSq);

            if (leftStepping)
                rightWantsStep = false;
            else if (rightStepping)
                leftWantsStep = false;
            else if (leftWantsStep && rightWantsStep)
            {
                bool chooseLeft = ((entity.FrameIndex + (uint)math.max(0, entity.EntitySlot)) & 1u) == 0u;
                rightWantsStep = !chooseLeft;
                leftWantsStep = chooseLeft;
            }

            UpdateFootLane(
                leftFootIndex,
                ref leftData,
                leftTarget,
                leftNormal,
                leftBlend,
                leftDeltaHeight,
                0,
                leftFlags,
                leftWantsStep,
                !leftGrounded,
                stepThresholdSq,
                in entity,
                in previous.LeftFoot,
                out next.LeftFoot);
            UpdateFootLane(
                rightFootIndex,
                ref rightData,
                rightTarget,
                rightNormal,
                rightBlend,
                rightDeltaHeight,
                1,
                rightFlags,
                rightWantsStep,
                !rightGrounded,
                stepThresholdSq,
                in entity,
                in previous.RightFoot,
                out next.RightFoot);

            next.LowerBodyFlags = (byte)(leftData.Flags | rightData.Flags);
        }

        private ContextualPhysicalIkFootData ReadFootData(int footIndex)
        {
            if (!FootData.IsCreated || footIndex < 0 || footIndex >= FootData.Length)
                return default;

            return SanitizeFootData(FootData[footIndex]);
        }

        private void SanitizeFootDataLane(int footIndex)
        {
            if (!FootData.IsCreated || footIndex < 0 || footIndex >= FootData.Length)
                return;

            FootData[footIndex] = SanitizeFootData(FootData[footIndex]);
        }

        private void FadeFootLane(int footIndex, in ContextualPhysicalIkContactTarget target, float fadeSharpness, float deltaTime)
        {
            if (!FootData.IsCreated || footIndex < 0 || footIndex >= FootData.Length)
                return;

            ContextualPhysicalIkFootData data = FootData[footIndex];
            float3 safePosition = math.select(target.WorldPosition, float3.zero, !math.all(math.isfinite(target.WorldPosition)));
            float3 safeNormal = ContextualPhysicalIkMath.SafeNormalize(target.WorldNormal, new float3(0.0f, 1.0f, 0.0f));
            data.TargetPosition = safePosition;
            data.CurrentPosition = safePosition;
            data.StepStartPosition = safePosition;
            data.SurfaceNormal = safeNormal;
            data.StepProgress01 = 1.0f;
            data.Blend = SmoothBlend(data.Blend, 0.0f, fadeSharpness, deltaTime);
            data.Flags = 0;
            FootData[footIndex] = data;
        }

        private void UpdateFootLane(
            int footIndex,
            ref ContextualPhysicalIkFootData data,
            float3 targetPosition,
            float3 targetNormal,
            float targetBlend,
            float deltaHeight,
            byte side,
            byte candidateFlags,
            bool allowStep,
            bool directSwim,
            float stepThresholdSq,
            in ContextualPhysicalIkEntityState entity,
            in ContextualPhysicalIkContactTarget previousTarget,
            out ContextualPhysicalIkContactTarget resolvedTarget)
        {
            data = SanitizeFootData(data);
            float3 safePelvis = SanitizeFloat3(entity.PelvisPosition, float3.zero);
            float3 safeTarget = math.select(targetPosition, safePelvis, !math.all(math.isfinite(targetPosition)));
            float3 safeNormal = ContextualPhysicalIkMath.SafeNormalize(targetNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentPosition = ResolveFootCurrentPosition(in data, in previousTarget, safeTarget);
            byte flags = candidateFlags;
            if (!math.all(math.isfinite(targetPosition)) || !math.all(math.isfinite(currentPosition)))
            {
                currentPosition = safeTarget;
                flags |= ContextualPhysicalIkLowerBodyConstants.FlagInvalid;
            }

            data.StepStartPosition = math.select(data.StepStartPosition, currentPosition, !math.all(math.isfinite(data.StepStartPosition)));
            data.StepProgress01 = SanitizeBlend(data.StepProgress01);
            data.StepThresholdSq = SanitizeNonNegative(stepThresholdSq);
            data.StepHeightMeters = ContextualPhysicalIkRuntime.StepHeightMeters;
            if (directSwim)
            {
                data.StepProgress01 = 1.0f;
                data.StepStartPosition = currentPosition;
                currentPosition = ContextualPhysicalIkMath.SmoothVector(
                    currentPosition,
                    safeTarget,
                    entity.TargetPositionSharpness,
                    entity.DeltaTime);
            }
            else
            {
                bool stepping = IsStepping(in data);
                if (allowStep && !stepping)
                {
                    data.StepStartPosition = currentPosition;
                    data.StepProgress01 = 0.0f;
                    stepping = true;
                }

                if (stepping)
                {
                    float safeDuration = math.max(0.0001f, ContextualPhysicalIkRuntime.StepDurationSeconds);
                    float progress = SanitizeBlend(data.StepProgress01 + (SanitizeNonNegative(entity.DeltaTime) * math.rcp(safeDuration)));
                    float lift01 = 1.0f - math.abs((progress * 2.0f) - 1.0f);
                    currentPosition = math.lerp(data.StepStartPosition, safeTarget, progress);
                    currentPosition.y += lift01 * data.StepHeightMeters;
                    data.StepProgress01 = progress;
                    if (progress < 0.999f)
                        flags |= ContextualPhysicalIkLowerBodyConstants.FlagStepping;
                    else
                    {
                        currentPosition = safeTarget;
                        data.StepProgress01 = 1.0f;
                    }
                }
                else
                {
                    currentPosition = ContextualPhysicalIkMath.SmoothVector(
                        currentPosition,
                        safeTarget,
                        entity.TargetPositionSharpness,
                        entity.DeltaTime);
                    data.StepProgress01 = 1.0f;
                    data.StepStartPosition = currentPosition;
                }
            }

            if (!math.all(math.isfinite(currentPosition)))
            {
                currentPosition = safeTarget;
                flags |= ContextualPhysicalIkLowerBodyConstants.FlagInvalid;
            }

            float smoothedBlend = SmoothBlend(data.Blend, targetBlend, entity.BlendFadeSharpness, entity.DeltaTime);
            data.TargetPosition = safeTarget;
            data.CurrentPosition = currentPosition;
            data.SurfaceNormal = safeNormal;
            data.Blend = smoothedBlend;
            data.Side = side;
            data.Flags = flags;

            if (FootData.IsCreated && footIndex >= 0 && footIndex < FootData.Length)
                FootData[footIndex] = data;

            if (FootTargets.IsCreated && footIndex >= 0 && footIndex < FootTargets.Length)
                FootTargets[footIndex] = safeTarget;

            if (FootCurrentPos.IsCreated && footIndex >= 0 && footIndex < FootCurrentPos.Length)
                FootCurrentPos[footIndex] = currentPosition;

            resolvedTarget.WorldPosition = currentPosition;
            resolvedTarget.WorldNormal = safeNormal;
            resolvedTarget.Blend = smoothedBlend;
            float safeMaxDeltaHeight = SanitizeNonNegative(entity.MaxDeltaHeight);
            float safeDeltaHeight = math.select(deltaHeight, 0.0f, !math.isfinite(deltaHeight));
            resolvedTarget.DeltaHeight = math.clamp(safeDeltaHeight, -safeMaxDeltaHeight, safeMaxDeltaHeight);
        }

        private static bool TryBuildGroundFootCandidate(
            in KinematicSurfaceHit hit,
            float3 probeOrigin,
            float contactOffset,
            float maxDeltaHeight,
            out float3 targetPosition,
            out float3 targetNormal,
            out float deltaHeight)
        {
            probeOrigin = SanitizeFloat3(probeOrigin, float3.zero);
            float safeContactOffset = SanitizeNonNegative(contactOffset);
            float safeMaxDeltaHeight = SanitizeNonNegative(maxDeltaHeight);
            targetPosition = probeOrigin;
            targetNormal = new float3(0.0f, 1.0f, 0.0f);
            deltaHeight = 0.0f;
            if (!HasHit(in hit) || hit.distance > ContextualPhysicalIkRuntime.GroundPresenceDistanceMeters)
                return false;

            targetNormal = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(hit.normal), targetNormal);
            targetPosition = ContextualPhysicalIkMath.ToFloat3(hit.point) + (targetNormal * safeContactOffset);
            if (!math.all(math.isfinite(targetPosition)))
            {
                targetPosition = probeOrigin;
                return false;
            }

            deltaHeight = math.clamp(targetPosition.y - probeOrigin.y, -safeMaxDeltaHeight, safeMaxDeltaHeight);
            return true;
        }

        private static void BuildSwimFootCandidate(
            in ContextualPhysicalIkEntityState entity,
            float side,
            out float3 targetPosition,
            out float3 targetNormal)
        {
            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f)),
                new float3(0.0f, 0.0f, 1.0f));
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            float3 safePelvis = SanitizeFloat3(entity.PelvisPosition, float3.zero);
            float3 safeKccVelocity = SanitizeFloat3(entity.KccVelocity, float3.zero);
            float3 planarVelocity = safeKccVelocity - (rootUp * math.dot(safeKccVelocity, rootUp));
            float3 swimDirection = ContextualPhysicalIkMath.SafeNormalize(planarVelocity, rootForward);
            float planarSpeedSq = math.lengthsq(planarVelocity);
            if (!math.isfinite(planarSpeedSq) || planarSpeedSq <= 0.0025f)
                swimDirection = rootForward;

            targetNormal = rootUp;
            targetPosition = safePelvis -
                (swimDirection * ContextualPhysicalIkRuntime.SwimBackDistanceMeters) -
                (rootUp * ContextualPhysicalIkRuntime.SwimDownDistanceMeters) +
                (rootRight * (side * ContextualPhysicalIkRuntime.SwimSideOffsetMeters));
            targetPosition = SanitizeFloat3(targetPosition, safePelvis);
        }

        private static float ResolvePelvisYawRadians(in ContextualPhysicalIkEntityState entity)
        {
            if (entity.HasCameraPose == 0)
                return 0.0f;

            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f)),
                new float3(0.0f, 0.0f, 1.0f));
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            float3 cameraPlanar = entity.CameraForward - (rootUp * math.dot(entity.CameraForward, rootUp));
            cameraPlanar = ContextualPhysicalIkMath.SafeNormalize(cameraPlanar, rootForward);
            float signedRight = math.clamp(math.dot(cameraPlanar, rootRight), -1.0f, 1.0f);
            return math.clamp(signedRight * ContextualPhysicalIkRuntime.PelvisCameraYawMaxRadians, -ContextualPhysicalIkRuntime.PelvisCameraYawMaxRadians, ContextualPhysicalIkRuntime.PelvisCameraYawMaxRadians);
        }

        private static bool ShouldTriggerStep(
            in ContextualPhysicalIkFootData data,
            in ContextualPhysicalIkContactTarget previousTarget,
            float3 targetPosition,
            float thresholdSq)
        {
            float3 currentPosition = ResolveFootCurrentPosition(in data, in previousTarget, targetPosition);
            if (!math.all(math.isfinite(currentPosition)) || !math.all(math.isfinite(targetPosition)))
                return false;

            float distanceSq = math.lengthsq(currentPosition - targetPosition);
            return math.isfinite(distanceSq) && distanceSq > SanitizeNonNegative(thresholdSq);
        }

        private static float ResolveStepThresholdSq(in ContextualPhysicalIkEntityState entity)
        {
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            float3 planarVelocity = entity.KccVelocity - (rootUp * math.dot(entity.KccVelocity, rootUp));
            float planarSpeedSq = math.lengthsq(planarVelocity);
            planarSpeedSq = math.select(0.0f, planarSpeedSq, math.isfinite(planarSpeedSq));
            float velocityAllowance = math.min(
                ContextualPhysicalIkRuntime.StepVelocityThresholdMaxMeters,
                planarSpeedSq * ContextualPhysicalIkRuntime.StepVelocityThresholdScale);
            float threshold = ContextualPhysicalIkRuntime.StepTriggerDistanceMeters + velocityAllowance;
            return threshold * threshold;
        }

        private static void CancelStep(ref ContextualPhysicalIkFootData data)
        {
            data = SanitizeFootData(data);
            data.Flags = (byte)(data.Flags & ~ContextualPhysicalIkLowerBodyConstants.FlagStepping);
            data.StepProgress01 = 1.0f;
            data.StepStartPosition = SanitizeFloat3(data.CurrentPosition, float3.zero);
        }

        private static bool IsStepping(in ContextualPhysicalIkFootData data)
        {
            return (data.Flags & ContextualPhysicalIkLowerBodyConstants.FlagStepping) != 0 &&
                math.isfinite(data.StepProgress01) &&
                SanitizeBlend(data.StepProgress01) < 0.999f &&
                math.all(math.isfinite(data.CurrentPosition)) &&
                math.all(math.isfinite(data.StepStartPosition));
        }

        private static float3 ResolveFootCurrentPosition(
            in ContextualPhysicalIkFootData data,
            in ContextualPhysicalIkContactTarget previousTarget,
            float3 fallback)
        {
            fallback = SanitizeFloat3(fallback, float3.zero);
            if (SanitizeBlend(data.Blend) > 0.0001f && math.all(math.isfinite(data.CurrentPosition)))
                return data.CurrentPosition;

            if (SanitizeBlend(previousTarget.Blend) > 0.0001f && math.all(math.isfinite(previousTarget.WorldPosition)))
                return previousTarget.WorldPosition;

            return fallback;
        }

        private static ContextualPhysicalIkFootData SanitizeFootData(ContextualPhysicalIkFootData data)
        {
            bool validPositions =
                math.all(math.isfinite(data.TargetPosition)) &&
                math.all(math.isfinite(data.CurrentPosition)) &&
                math.all(math.isfinite(data.StepStartPosition));
            bool validScalars =
                math.isfinite(data.StepProgress01) &&
                math.isfinite(data.StepThresholdSq) &&
                math.isfinite(data.StepHeightMeters) &&
                math.isfinite(data.Blend);
            if (!validPositions || !validScalars)
            {
                data = default;
                data.SurfaceNormal = new float3(0.0f, 1.0f, 0.0f);
                data.StepProgress01 = 1.0f;
                return data;
            }

            data.SurfaceNormal = ContextualPhysicalIkMath.SafeNormalize(data.SurfaceNormal, new float3(0.0f, 1.0f, 0.0f));
            data.StepProgress01 = SanitizeBlend(data.StepProgress01);
            data.StepThresholdSq = SanitizeNonNegative(data.StepThresholdSq);
            data.StepHeightMeters = SanitizeNonNegative(data.StepHeightMeters);
            data.Blend = SanitizeBlend(data.Blend);
            data.Side = data.Side == 0 ? (byte)0 : (byte)1;
            data.Flags = (byte)(data.Flags & (
                ContextualPhysicalIkLowerBodyConstants.FlagGrounded |
                ContextualPhysicalIkLowerBodyConstants.FlagStepping |
                ContextualPhysicalIkLowerBodyConstants.FlagSwimming |
                ContextualPhysicalIkLowerBodyConstants.FlagInvalid));
            if (data.Blend <= 0.0001f)
            {
                data.Flags = 0;
                data.StepProgress01 = 1.0f;
            }
            else if (data.StepProgress01 >= 0.999f)
            {
                data.Flags = (byte)(data.Flags & ~ContextualPhysicalIkLowerBodyConstants.FlagStepping);
            }

            return data;
        }

        private static float2 ResolveSlopeLeanRadians(
            in ContextualPhysicalIkTargetFrame frame,
            in ContextualPhysicalIkEntityState entity)
        {
            float leftBlend = SanitizeBlend(frame.LeftFoot.Blend);
            float rightBlend = SanitizeBlend(frame.RightFoot.Blend);
            float blendSum = leftBlend + rightBlend;
            float hasFootNormal = math.select(0.0f, 1.0f, blendSum > 0.0001f);
            float3 blendedNormal = (frame.LeftFoot.WorldNormal * leftBlend) + (frame.RightFoot.WorldNormal * rightBlend);
            float3 slopeNormal = ContextualPhysicalIkMath.SafeNormalize(blendedNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f)),
                new float3(0.0f, 0.0f, 1.0f));
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float slopeForward = math.dot(slopeNormal, rootForward) * hasFootNormal;
            float slopeRight = math.dot(slopeNormal, rootRight) * hasFootNormal;

            return new float2(
                math.clamp(-slopeForward * entity.ComLeanPitchRadians, -entity.ComLeanPitchRadians, entity.ComLeanPitchRadians),
                math.clamp(slopeRight * entity.ComLeanRollRadians, -entity.ComLeanRollRadians, entity.ComLeanRollRadians));
        }

        private static void FadeOutTarget(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float fadeSharpness,
            float deltaTime)
        {
            target.WorldPosition = math.select(previous.WorldPosition, float3.zero, !math.all(math.isfinite(previous.WorldPosition)));
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(previous.WorldNormal, new float3(0.0f, 1.0f, 0.0f));
            float previousBlend = SanitizeBlend(previous.Blend);
            target.Blend = SmoothBlend(previousBlend, 0.0f, fadeSharpness, deltaTime);
            target.DeltaHeight = 0.0f;
        }

        private static void ApplyPredictiveLatch(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 predictivePosition,
            float3 predictiveNormal,
            float predictiveBlend,
            float positionSharpness,
            float normalSharpness,
            float fadeSharpness,
            float deltaTime)
        {
            float targetBlend = SanitizeBlend(predictiveBlend);
            if (targetBlend <= 0.0001f || !math.all(math.isfinite(predictivePosition)))
                return;

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(predictiveNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, predictivePosition);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, normal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, predictivePosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = math.max(SanitizeBlend(target.Blend), SmoothBlend(previous.Blend, targetBlend, fadeSharpness, deltaTime));
            target.DeltaHeight = 0.0f;
        }

        private static void ApplyToolRetraction(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            in KinematicSurfaceHit hit,
            float3 probeOrigin,
            float3 cameraForward,
            float3 cameraUp,
            float collisionDistance,
            float backDistance,
            float liftDistance,
            float blendScale,
            float positionSharpness,
            float normalSharpness,
            float fadeSharpness,
            float deltaTime)
        {
            probeOrigin = SanitizeFloat3(probeOrigin, float3.zero);
            float safeCollisionDistance = math.max(0.0001f, SanitizeNonNegative(collisionDistance));
            if (!HasHit(in hit) || hit.distance >= safeCollisionDistance)
                return;

            float3 forward = ContextualPhysicalIkMath.SafeNormalize(cameraForward, new float3(0.0f, 0.0f, 1.0f));
            float3 up = ContextualPhysicalIkMath.SafeNormalize(cameraUp, new float3(0.0f, 1.0f, 0.0f));
            float blocked01 = SanitizeBlend((safeCollisionDistance - SanitizeNonNegative(hit.distance)) * math.rcp(safeCollisionDistance));
            float targetBlend = blocked01 * SanitizeBlend(blendScale);
            if (targetBlend <= 0.0001f)
                return;

            float3 hitNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(hit.normal),
                -forward);
            float3 targetPosition = probeOrigin -
                (forward * (SanitizeNonNegative(backDistance) * blocked01)) +
                (up * (SanitizeNonNegative(liftDistance) * blocked01));
            targetPosition = math.select(targetPosition, probeOrigin, !math.all(math.isfinite(targetPosition)));

            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, targetPosition);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, hitNormal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, targetPosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, hitNormal, normalSharpness, deltaTime),
                hitNormal);
            target.Blend = math.max(SanitizeBlend(target.Blend), SmoothBlend(previous.Blend, targetBlend, fadeSharpness, deltaTime));
            target.DeltaHeight = 0.0f;
        }

        private static void ApplyToolRecoil(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 probeOrigin,
            float3 recoilOffset,
            float maxOffset,
            float positionSharpness,
            float fadeSharpness,
            float deltaTime)
        {
            probeOrigin = SanitizeFloat3(probeOrigin, float3.zero);
            float safeMaxOffset = SanitizeNonNegative(maxOffset);
            if (safeMaxOffset <= 0.000001f || !math.all(math.isfinite(recoilOffset)))
                return;

            float3 clampedOffset = ClampOffsetNoSqrt(recoilOffset, safeMaxOffset);
            float offsetSq = math.lengthsq(clampedOffset);
            if (offsetSq <= 0.000001f)
                return;

            float maxOffsetSq = math.max(0.000001f, safeMaxOffset * safeMaxOffset);
            float targetBlend = SanitizeBlend(offsetSq * math.rcp(maxOffsetSq));
            if (targetBlend <= 0.0001f)
                return;

            float3 targetPosition = probeOrigin + clampedOffset;
            if (!math.all(math.isfinite(targetPosition)))
                targetPosition = probeOrigin;

            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, probeOrigin);
            float3 fallbackNormal = new float3(0.0f, 1.0f, 0.0f);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, targetPosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ResolveSmoothingNormal(in target, in previous, fallbackNormal),
                fallbackNormal);
            target.Blend = math.max(SanitizeBlend(target.Blend), SmoothBlend(previous.Blend, targetBlend, fadeSharpness, deltaTime));
            target.DeltaHeight = 0.0f;
        }

        private static float3 ClampOffsetNoSqrt(float3 value, float maxLength)
        {
            float lengthSq = math.lengthsq(value);
            float maxLengthSq = maxLength * maxLength;
            if (lengthSq <= maxLengthSq)
                return value;

            return value * (maxLength * math.rsqrt(math.max(lengthSq, 0.000001f)));
        }

        private static void ApplyColdShiver(
            ref ContextualPhysicalIkContactTarget target,
            float3 offset,
            float blend)
        {
            float activeBlend = SanitizeBlend(blend) * SanitizeBlend(target.Blend);
            if (activeBlend <= 0.0001f || !math.all(math.isfinite(offset)))
                return;

            float3 adjustedPosition = target.WorldPosition + (offset * activeBlend);
            target.WorldPosition = math.select(adjustedPosition, target.WorldPosition, !math.all(math.isfinite(adjustedPosition)));
        }

        private static void ResolveContactTarget(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            in KinematicSurfaceHit hit,
            float3 probeOrigin,
            float contactOffset,
            float targetBlend,
            float positionSharpness,
            float normalSharpness,
            float fadeSharpness,
            float maxDeltaHeight,
            float deltaTime)
        {
            if (!HasHit(in hit))
            {
                FadeOutTarget(ref target, in previous, fadeSharpness, deltaTime);
                return;
            }

            probeOrigin = SanitizeFloat3(probeOrigin, float3.zero);
            float safeContactOffset = SanitizeNonNegative(contactOffset);
            float safeTargetBlend = SanitizeBlend(targetBlend);
            float safeMaxDeltaHeight = SanitizeNonNegative(maxDeltaHeight);
            float3 normal = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(hit.normal), new float3(0.0f, 1.0f, 0.0f));
            float3 point = ContextualPhysicalIkMath.ToFloat3(hit.point) + (normal * safeContactOffset);

            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, point);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, normal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, point, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = SmoothBlend(previous.Blend, safeTargetBlend, fadeSharpness, deltaTime);
            float deltaHeight = point.y - probeOrigin.y;
            deltaHeight = math.select(deltaHeight, 0.0f, !math.isfinite(deltaHeight));
            target.DeltaHeight = math.clamp(deltaHeight, -safeMaxDeltaHeight, safeMaxDeltaHeight);
        }

        private static float3 ResolveSmoothingPosition(
            in ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 fallback)
        {
            fallback = SanitizeFloat3(fallback, float3.zero);
            if (target.Blend > 0.0001f && math.all(math.isfinite(target.WorldPosition)))
                return target.WorldPosition;

            if (previous.Blend > 0.0001f && math.all(math.isfinite(previous.WorldPosition)))
                return previous.WorldPosition;

            return fallback;
        }

        private static float3 ResolveSmoothingNormal(
            in ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 fallback)
        {
            fallback = ContextualPhysicalIkMath.SafeNormalize(fallback, new float3(0.0f, 1.0f, 0.0f));
            if (target.Blend > 0.0001f && math.all(math.isfinite(target.WorldNormal)))
                return target.WorldNormal;

            if (previous.Blend > 0.0001f && math.all(math.isfinite(previous.WorldNormal)))
                return previous.WorldNormal;

            return fallback;
        }

        private static void SanitizeTargetFrame(ref ContextualPhysicalIkTargetFrame frame)
        {
            SanitizeContactTarget(ref frame.LeftFoot);
            SanitizeContactTarget(ref frame.RightFoot);
            SanitizeContactTarget(ref frame.LeftHand);
            SanitizeContactTarget(ref frame.RightHand);
            frame.ComOffsetLocal = SanitizeFloat3(frame.ComOffsetLocal, float3.zero);
            frame.ComLeanRadians = SanitizeFloat2(frame.ComLeanRadians, float2.zero);
            frame.DeltaTime = SanitizeNonNegative(frame.DeltaTime);
            frame.ViewerDistanceSq = SanitizeNonNegative(frame.ViewerDistanceSq);
            frame.TunnelBlend = SanitizeBlend(frame.TunnelBlend);
            float pelvisYawRadians = math.select(frame.PelvisYawRadians, 0.0f, !math.isfinite(frame.PelvisYawRadians));
            frame.PelvisYawRadians = math.clamp(
                pelvisYawRadians,
                -ContextualPhysicalIkRuntime.PelvisCameraYawMaxRadians,
                ContextualPhysicalIkRuntime.PelvisCameraYawMaxRadians);
        }

        private static void SanitizeContactTarget(ref ContextualPhysicalIkContactTarget target)
        {
            bool validPosition = math.all(math.isfinite(target.WorldPosition));
            bool validBlend = math.isfinite(target.Blend);
            if (!validPosition || !validBlend)
            {
                target.WorldPosition = float3.zero;
                target.Blend = 0.0f;
                target.DeltaHeight = 0.0f;
            }
            else
            {
                target.Blend = SanitizeBlend(target.Blend);
                target.DeltaHeight = math.select(target.DeltaHeight, 0.0f, !math.isfinite(target.DeltaHeight));
            }

            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(target.WorldNormal, new float3(0.0f, 1.0f, 0.0f));
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private static float2 SanitizeFloat2(float2 value, float2 fallback)
        {
            return math.select(value, fallback, !math.all(math.isfinite(value)));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeBlend(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SmoothBlend(float current, float target, float sharpness, float deltaTime)
        {
            float value = ContextualPhysicalIkMath.SmoothScalar(
                SanitizeBlend(current),
                SanitizeBlend(target),
                sharpness,
                deltaTime);
            return SanitizeBlend(value);
        }

        private static float SmoothFiniteScalar(float current, float target, float sharpness, float deltaTime)
        {
            float safeCurrent = math.select(current, 0.0f, !math.isfinite(current));
            float safeTarget = math.select(target, 0.0f, !math.isfinite(target));
            float value = ContextualPhysicalIkMath.SmoothScalar(safeCurrent, safeTarget, sharpness, deltaTime);
            return math.select(value, safeTarget, !math.isfinite(value));
        }

        private static bool HasHit(in KinematicSurfaceHit hit)
        {
            float3 point = ContextualPhysicalIkMath.ToFloat3(hit.point);
            float3 normal = ContextualPhysicalIkMath.ToFloat3(hit.normal);
            float normalLengthSq = math.lengthsq(normal);
            return math.isfinite(hit.distance) &&
                hit.distance >= 0.0f &&
                math.all(math.isfinite(point)) &&
                math.all(math.isfinite(normal)) &&
                math.isfinite(normalLengthSq) &&
                (hit.distance > 0.0f || normalLengthSq > 0.0001f);
        }

        private static float ResolveBraceProxyTunnelBlend(
            in KinematicSurfaceHit leftHandHit,
            in KinematicSurfaceHit rightHandHit,
            in ContextualPhysicalIkEntityState entity)
        {
            float leftBlend = ResolveBraceHitProxyBlend(
                in leftHandHit,
                entity.LeftArmReach,
                entity.HandProbeDistanceScale,
                entity.TunnelClearanceDistance,
                entity.HandBraceFadeDistance);
            float rightBlend = ResolveBraceHitProxyBlend(
                in rightHandHit,
                entity.RightArmReach,
                entity.HandProbeDistanceScale,
                entity.TunnelClearanceDistance,
                entity.HandBraceFadeDistance);
            return math.max(leftBlend, rightBlend);
        }

        private static float ResolveBraceHitProxyBlend(
            in KinematicSurfaceHit hit,
            float armReach,
            float distanceScale,
            float clearanceDistance,
            float fadeDistance)
        {
            if (!HasHit(in hit))
                return 0.0f;

            float scaledReach = math.max(0.0001f, SanitizeNonNegative(armReach) * math.max(0.0001f, SanitizeNonNegative(distanceScale)));
            float proxyDistance = math.max(0.0001f, math.min(scaledReach, math.max(0.0001f, SanitizeNonNegative(clearanceDistance))));
            float safeFadeDistance = math.max(0.0001f, SanitizeNonNegative(fadeDistance));
            return SanitizeBlend((proxyDistance - SanitizeNonNegative(hit.distance)) * math.rcp(safeFadeDistance));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    internal sealed class ContextualPhysicalIkRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int MaxEntities = 128;
        internal const int RaysPerEntity = 6;
        internal const int HandsPerEntity = 2;
        internal const int LeftHandIndex = 0;
        internal const int RightHandIndex = 1;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 96;
        private const int MinCommandsPerJob = 32;
        private const uint TelemetryDumpRetryFrameInterval = 60u;
        private const float CameraResolveRetryInterval = 1.0f;
        internal const float GroundPresenceDistanceMeters = 3.0f;
        internal const float StepTriggerDistanceMeters = 0.22f;
        internal const float StepVelocityThresholdScale = 0.025f;
        internal const float StepVelocityThresholdMaxMeters = 0.10f;
        internal const float StepHeightMeters = 0.11f;
        internal const float StepDurationSeconds = 0.22f;
        internal const float FootRayVelocityLeadScale = 0.01f;
        internal const float FootRayVelocityLeadMaxMeters = 0.18f;
        private const float KccVelocityBindingDistanceMeters = 4.0f;
        private const float KccVelocityBindingDistanceSq = KccVelocityBindingDistanceMeters * KccVelocityBindingDistanceMeters;
        private const uint KccVelocityMaxAgeFrames = 8u;
        internal const float SwimFootBlend = 0.68f;
        internal const float SwimBackDistanceMeters = 0.42f;
        internal const float SwimDownDistanceMeters = 0.55f;
        internal const float SwimSideOffsetMeters = 0.16f;
        internal const float PelvisCameraYawMaxRadians = 0.18f;
        private const SystemID NativeArrayOwnerSystem = SystemID.AnimationLocomotion;
        private const string NativeMemoryAllocationFailureMessage = "H8Memory allocation failed for persistent ContextualPhysicalIkRuntime buffer.";
        private const string NativeMemoryTransientAllocationFailureMessage = "H8Memory allocation failed for transient ContextualPhysicalIkRuntime buffer.";
        private const string NativeMemoryReleaseFailureMessage = "H8Memory release failed for ContextualPhysicalIkRuntime native buffer.";
        private const string NativeMemoryDisposalCompletionFailureMessage = "ContextualPhysicalIkRuntime native disposal completion failed after partial scheduling.";
        private const ulong TelemetryDumpMagic = 0x314753454C4B4948UL;
        private const uint TelemetryReasonOriginShift = 0x00000001u;
        private const uint TelemetryReasonStructuralMutation = 0x00000002u;
        private const uint TelemetryReasonInvalidOriginShift = 0x00000004u;
        private const uint TelemetryReasonNativeStorageInvalid = 0x00000008u;
        private const uint TelemetryReasonRuntimeDisable = 0x00000010u;
        private const float MaxAcceptedOriginShiftMeters = 10000.0f;
        private const float MaxAcceptedOriginShiftMetersSq = MaxAcceptedOriginShiftMeters * MaxAcceptedOriginShiftMeters;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1403_CONTEXTUAL_PHYSICAL_IK.bin";
        private static ContextualPhysicalIkRuntime s_activeRuntime;

        // COLD ALLOC: ContextualPhysicalIkRig[128] - stable slot owner registry for contextual IK entities - owner: ContextualPhysicalIkRuntime
        private readonly ContextualPhysicalIkRig[] _registeredRigs = new ContextualPhysicalIkRig[MaxEntities];
        // COLD ALLOC: bool[128] - active slot bitset for contextual IK entities - owner: ContextualPhysicalIkRuntime
        private readonly bool[] _slotActive = new bool[MaxEntities];
        // COLD ALLOC: int[128] - free-slot stack for contextual IK stable indexing - owner: ContextualPhysicalIkRuntime
        private readonly int[] _freeSlots = new int[MaxEntities];

        // COLD ALLOC: RuntimeNativeBufferSet[1] - native IK buffer owner indirection - owner: ContextualPhysicalIkRuntime
        private RuntimeNativeBufferSet _nativeBuffers = new RuntimeNativeBufferSet();

        private ref NativeArray<ContextualPhysicalIkEntityState> _scheduledEntityStates => ref _nativeBuffers.ScheduledEntityStates;
        private ref NativeArray<KinematicSurfaceHit> _scheduledHits => ref _nativeBuffers.ScheduledHits;
        private ref NativeArray<ContextualPhysicalIkTargetFrame> _frontTargetFrames => ref _nativeBuffers.FrontTargetFrames;
        private ref NativeArray<ContextualPhysicalIkTargetFrame> _backTargetFrames => ref _nativeBuffers.BackTargetFrames;
        private ref NativeArray<float3> _ikTargets => ref _nativeBuffers.IkTargets;
        private ref NativeArray<float> _ikWeights => ref _nativeBuffers.IkWeights;
        private ref NativeArray<ContextualPhysicalIkFootData> _footIkData => ref _nativeBuffers.FootIkData;
        private ref NativeArray<float3> _footTargets => ref _nativeBuffers.FootTargets;
        private ref NativeArray<float3> _footCurrentPos => ref _nativeBuffers.FootCurrentPos;
        private ref NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing => ref _nativeBuffers.TelemetryRing;

        private JobHandle _pendingGroundResponseHandle;
        private JobHandle _disposeHandle;
        private Transform _cameraTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IVoxelSonarSdfReadModel _voxelSdfReadModel;
        private ITerrainProvider _terrainProvider;
        private bool _groundResponseScheduled;
        private bool _registered;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _hasPendingAnimationInjection;
        private int _freeSlotCount;
        private float _cameraResolveRetryTimer;
        private float3 _lastKccVelocity;
        private float3 _lastKccBodyPosition;
        private uint _lastKccVelocityFrame;
        private uint _frameIndex;
        private int _telemetryCursor;
        private uint _nextTelemetryDumpRetryFrame;
        private bool _telemetryDumped;

        internal NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly CurrentTargetFrames =>
            _frontTargetFrames.IsCreated ? _frontTargetFrames.AsReadOnly() : default;

        internal static ContextualPhysicalIkRuntime EnsureRuntimeInstance()
        {
            ContextualPhysicalIkRuntime runtime = GlobalRegistry.ContextualPhysicalIkRuntime;
            if (runtime != null)
                return runtime;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Contextual IK owns somatic target frames; without create, VR/physical IK
            // consumers miss the owner when bootstrap reorders Player-layer wiring.
            GameObject runtimeRoot = new GameObject("[ContextualPhysicalIkRuntime]"); // COLD ALLOC: GameObject[1] - persistent contextual IK runtime owner - owner: ContextualPhysicalIkRuntime
            runtime = runtimeRoot.AddComponent<ContextualPhysicalIkRuntime>();
            GlobalRegistry.RegisterContextualPhysicalIkRuntime(runtime);
            return runtime;
        }

        private void Awake()
        {
            ContextualPhysicalIkRuntime runtime = GlobalRegistry.ContextualPhysicalIkRuntime;
            if (runtime != null && !ReferenceEquals(runtime, this))
            {
                Destroy(gameObject);
                return;
            }

            s_activeRuntime = this;
            GlobalRegistry.RegisterContextualPhysicalIkRuntime(this);
            CachePlayerContextCold();
            CacheSpatialReadModelsCold();
            InitializeFreeSlots();
            EnsurePersistentBuffers();
        }

        private void OnEnable()
        {
            s_activeRuntime = this;
            CachePlayerContextCold();
            CacheSpatialReadModelsCold();
            EnsurePersistentBuffers();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            CompletePendingGroundResponseForRuntimeDisable();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregister();
            DisposeBuffers(default);
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregister();
            JobHandle dependency = _groundResponseScheduled ? _pendingGroundResponseHandle : default;
            Exception disposeException = null;
            try
            {
                DisposeBuffers(dependency);
            }
            catch (Exception exception)
            {
                disposeException = exception;
            }
            finally
            {
                _cachedPlayerContext = null;
                _cameraTransform = null;
                if (ReferenceEquals(s_activeRuntime, this))
                    s_activeRuntime = null;
                GlobalRegistry.ClearContextualPhysicalIkRuntime(this);
            }

            if (disposeException != null)
                throw disposeException;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting -= DisposeActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting += DisposeActiveRuntimeForEditorReload;
        }

        private static void DisposeActiveRuntimeForEditorReload()
        {
            ContextualPhysicalIkRuntime runtime = s_activeRuntime;
            if (runtime == null)
                return;

            runtime.CompletePendingGroundResponseForRuntimeDisable();
            runtime.TryUnregisterOriginShiftListener();
            runtime.TryUnregisterHotSwapListener();
            runtime.TryUnregister();
            runtime.DisposeBuffers(default);
        }
#endif

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;

                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
                    Camera previousCamera = previousContext != null ? previousContext.PlayerCamera : null;
                    if (previousCamera != null && ReferenceEquals(_cameraTransform, previousCamera.transform))
                        _cameraTransform = null;

                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    Camera currentCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
                    if (currentCamera != null)
                        _cameraTransform = currentCamera.transform;

                    _cameraResolveRetryTimer = 0.0f;
                    break;

                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as IVoxelSonarSdfReadModel;
                    break;

                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _terrainProvider = currentService as ITerrainProvider;
                    break;
            }
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(offset)))
            {
                WriteTelemetrySample(TelemetryReasonInvalidOriginShift);
                DumpTelemetry(TelemetryReasonInvalidOriginShift);
                return;
            }

            float shiftDistanceSq = math.lengthsq(offset);
            if (!math.isfinite(shiftDistanceSq) || shiftDistanceSq > MaxAcceptedOriginShiftMetersSq)
            {
                WriteTelemetrySample(TelemetryReasonInvalidOriginShift);
                DumpTelemetry(TelemetryReasonInvalidOriginShift);
                return;
            }

            if (shiftDistanceSq <= 0.000001f)
                return;

            CompletePendingGroundResponseForOriginShift();

            RebaseScheduledEntityStates(offset);
            RebaseTargetFrames(_frontTargetFrames, offset);
            RebaseTargetFrames(_backTargetFrames, offset);
            RebaseWeightedIkTargetLanes(offset);
            RebaseFootSoaLanes(offset);
            RebaseFootData(offset);
            RebaseCachedKccBodyPosition(offset);
        }

        /// <inheritdoc />
        public void FastTick(float deltaTime)
        {
            uint frameIndex = _frameIndex;
            _frameIndex++;
            bool hasViewerPosition = TryResolveViewerPose(
                deltaTime,
                out float3 viewerPosition,
                out float3 viewerForward,
                out float3 viewerUp,
                out float3 viewerRight);

            if (_groundResponseScheduled)
                return;

            float3 kccVelocity = ConsumeKccVelocitySignal(frameIndex);
            if (!HasGroundPipelineStorage())
            {
                WriteTelemetrySample(TelemetryReasonNativeStorageInvalid);
                return;
            }

            if (!CaptureEntityStates(deltaTime, frameIndex, viewerPosition, viewerForward, viewerUp, viewerRight, hasViewerPosition, kccVelocity))
                return;

            ScheduleGroundPipeline();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryCompletePendingAnimationInjection();

            if (!_groundResponseScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: false))
                return;

            SwapTargetBuffers();
            PublishFrontTargetBuffer(applyPresentation: true);
            WriteTelemetrySample(0u);
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        internal bool RegisterRig(ContextualPhysicalIkRig rig, out int slotIndex)
        {
            slotIndex = -1;
            if (rig == null || _freeSlotCount <= 0)
                return false;

            bool completedTargetFrame = CompletePendingGroundResponseForStructuralMutation();
            if (completedTargetFrame)
            {
                PublishFrontTargetBuffer(applyPresentation: false);
                WriteTelemetrySample(TelemetryReasonStructuralMutation);
            }

            int freeStackIndex = _freeSlotCount - 1;
            slotIndex = _freeSlots[freeStackIndex];
            _freeSlotCount = freeStackIndex;

            _registeredRigs[slotIndex] = rig;
            _slotActive[slotIndex] = true;
            ResetTargetSlot(slotIndex);
            rig.AssignEntitySlot(slotIndex, _frontTargetFrames.IsCreated ? _frontTargetFrames.AsReadOnly() : default);
            if (rig.HasPendingAnimationInjection)
                _hasPendingAnimationInjection = true;
            return true;
        }

        internal void UnregisterRig(ContextualPhysicalIkRig rig, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEntities)
                return;

            if (!ReferenceEquals(_registeredRigs[slotIndex], rig))
                return;

            bool completedTargetFrame = CompletePendingGroundResponseForStructuralMutation();

            _registeredRigs[slotIndex] = null;
            _slotActive[slotIndex] = false;
            ResetTargetSlot(slotIndex);
            _freeSlots[_freeSlotCount] = slotIndex;
            _freeSlotCount++;

            if (completedTargetFrame)
            {
                PublishFrontTargetBuffer(applyPresentation: false);
                WriteTelemetrySample(TelemetryReasonStructuralMutation);
            }
        }

        private void TryCompletePendingAnimationInjection()
        {
            if (!_hasPendingAnimationInjection)
                return;

            bool stillPending = false;
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                if (rig == null || !rig.HasPendingAnimationInjection)
                    continue;

                if (!rig.TryCompleteLateFrameAnimationInjection())
                    stillPending = true;
            }

            _hasPendingAnimationInjection = stillPending;
        }

        private void InitializeFreeSlots()
        {
            _freeSlotCount = MaxEntities;
            for (int i = 0; i < MaxEntities; i++)
                _freeSlots[i] = i;
        }

        private void EnsurePersistentBuffers()
        {
            _nativeBuffers.EnsurePersistentBuffers();
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            _disposeHandle = default;
            try
            {
                _nativeBuffers.Dispose(dependency, ref _disposeHandle);
            }
            finally
            {
                JobHandle.ScheduleBatchedJobs();
                ForceCompleteDisposeHandleInPostSimulationWindow(ref _disposeHandle);
                _groundResponseScheduled = false;
                _pendingGroundResponseHandle = default;
            }
        }

        private static void ForceCompleteDisposeHandleInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private sealed class RuntimeNativeBufferSet : IDisposable
        {
            public NativeArray<ContextualPhysicalIkEntityState> ScheduledEntityStates;
            public NativeArray<KinematicSurfaceHit> ScheduledHits;
            public NativeArray<ContextualPhysicalIkTargetFrame> FrontTargetFrames;
            public NativeArray<ContextualPhysicalIkTargetFrame> BackTargetFrames;
            public NativeArray<float3> IkTargets;
            public NativeArray<float> IkWeights;
            public NativeArray<ContextualPhysicalIkFootData> FootIkData;
            public NativeArray<float3> FootTargets;
            public NativeArray<float3> FootCurrentPos;
            public NativeArray<ContextualPhysicalIkTelemetryEntry> TelemetryRing;

            public void EnsurePersistentBuffers()
            {
                try
                {
                    if (!ScheduledEntityStates.IsCreated)
                    {
                        ScheduledEntityStates = CreatePersistentNativeArray<ContextualPhysicalIkEntityState>(
                            MaxEntities,
                            nameof(ScheduledEntityStates),
                            NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkEntityState>[128] - scheduled IK entity snapshots - owner: RuntimeNativeBufferSet
                    }

                    if (!ScheduledHits.IsCreated)
                    {
                        ScheduledHits = CreatePersistentNativeArray<KinematicSurfaceHit>(
                            MaxEntities * RaysPerEntity,
                            nameof(ScheduledHits),
                            NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<KinematicSurfaceHit>[768] - contextual IK surface results - owner: RuntimeNativeBufferSet
                    }

                if (!FrontTargetFrames.IsCreated)
                {
                    FrontTargetFrames = CreatePersistentNativeArray<ContextualPhysicalIkTargetFrame>(
                        MaxEntities,
                        nameof(FrontTargetFrames),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] - read-side IK target frames - owner: RuntimeNativeBufferSet
                }

                if (!BackTargetFrames.IsCreated)
                {
                    BackTargetFrames = CreatePersistentNativeArray<ContextualPhysicalIkTargetFrame>(
                        MaxEntities,
                        nameof(BackTargetFrames),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] - write-side IK target frames - owner: RuntimeNativeBufferSet
                }

                if (!IkTargets.IsCreated)
                {
                    IkTargets = CreatePersistentNativeArray<float3>(
                        MaxEntities * HandsPerEntity,
                        nameof(IkTargets),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[256] - SOA hand IK target positions - owner: RuntimeNativeBufferSet
                }

                if (!IkWeights.IsCreated)
                {
                    IkWeights = CreatePersistentNativeArray<float>(
                        MaxEntities * HandsPerEntity,
                        nameof(IkWeights),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[256] - SOA hand IK weights - owner: RuntimeNativeBufferSet
                }

                if (!FootIkData.IsCreated)
                {
                    FootIkData = CreatePersistentNativeArray<ContextualPhysicalIkFootData>(
                        MaxEntities * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity,
                        nameof(FootIkData),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkFootData>[256] - packed lower-body presence foot lanes - owner: RuntimeNativeBufferSet
                }

                if (!FootTargets.IsCreated)
                {
                    FootTargets = CreatePersistentNativeArray<float3>(
                        MaxEntities * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity,
                        nameof(FootTargets),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[256] - SOA desired foot IK positions - owner: RuntimeNativeBufferSet
                }

                if (!FootCurrentPos.IsCreated)
                {
                    FootCurrentPos = CreatePersistentNativeArray<float3>(
                        MaxEntities * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity,
                        nameof(FootCurrentPos),
                        NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[256] - SOA current stepped foot IK positions - owner: RuntimeNativeBufferSet
                }

                    if (!TelemetryRing.IsCreated)
                    {
                        TelemetryRing = CreatePersistentNativeArray<ContextualPhysicalIkTelemetryEntry>(
                            TelemetryCapacity,
                            nameof(TelemetryRing),
                            NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTelemetryEntry>[300] - PLAYER_TOOL_IK black-box ring - owner: RuntimeNativeBufferSet
                    }
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

            public void Dispose(JobHandle dependency, ref JobHandle disposeHandle)
            {
                Exception firstException = null;
                DisposeNativeArrayBestEffort(ref ScheduledEntityStates, dependency, ref disposeHandle, ref firstException, nameof(ScheduledEntityStates));
                DisposeNativeArrayBestEffort(ref ScheduledHits, dependency, ref disposeHandle, ref firstException, nameof(ScheduledHits));
                DisposeNativeArrayBestEffort(ref FrontTargetFrames, dependency, ref disposeHandle, ref firstException, nameof(FrontTargetFrames));
                DisposeNativeArrayBestEffort(ref BackTargetFrames, dependency, ref disposeHandle, ref firstException, nameof(BackTargetFrames));
                DisposeNativeArrayBestEffort(ref IkTargets, dependency, ref disposeHandle, ref firstException, nameof(IkTargets));
                DisposeNativeArrayBestEffort(ref IkWeights, dependency, ref disposeHandle, ref firstException, nameof(IkWeights));
                DisposeNativeArrayBestEffort(ref FootIkData, dependency, ref disposeHandle, ref firstException, nameof(FootIkData));
                DisposeNativeArrayBestEffort(ref FootTargets, dependency, ref disposeHandle, ref firstException, nameof(FootTargets));
                DisposeNativeArrayBestEffort(ref FootCurrentPos, dependency, ref disposeHandle, ref firstException, nameof(FootCurrentPos));
                DisposeNativeArrayBestEffort(ref TelemetryRing, dependency, ref disposeHandle, ref firstException, nameof(TelemetryRing));
                ThrowFirstDisposeException(firstException);
            }

            public void Dispose()
            {
                JobHandle disposeHandle = default;
                Exception firstException = null;
                try
                {
                    Dispose(default, ref disposeHandle);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    JobHandle.ScheduleBatchedJobs();
                    try
                    {
                        ForceCompleteDisposeHandleInPostSimulationWindow(ref disposeHandle);
                    }
                    catch (Exception exception)
                    {
                        firstException = firstException == null
                            ? exception
                            : new AggregateException(NativeMemoryDisposalCompletionFailureMessage, firstException, exception);
                    }
                }

                ThrowFirstDisposeException(firstException);
            }

            private static NativeArray<T> CreatePersistentNativeArray<T>(
                int length,
                string allocationLabel,
                NativeArrayOptions options) where T : struct
            {
                NativeArray<T> array = H8Memory.Allocate<T>(
                    length,
                    NativeArrayOwnerSystem,
                    Allocator.Persistent,
                    options);

                if (!array.IsCreated || array.Length != length)
                    throw new InvalidOperationException($"{NativeMemoryAllocationFailureMessage} Label={allocationLabel}.");

                return array;
            }

            private static void DisposeNativeArray<T>(
                ref NativeArray<T> array,
                JobHandle dependency,
                ref JobHandle disposeHandle,
                string allocationLabel = null) where T : struct
            {
                if (!array.IsCreated)
                    return;

                JobHandle releaseHandle = H8Memory.Release(ref array, dependency, NativeArrayOwnerSystem);
                disposeHandle = JobHandle.CombineDependencies(disposeHandle, releaseHandle);

                if (array.IsCreated)
                    throw new InvalidOperationException($"{NativeMemoryReleaseFailureMessage} Label={allocationLabel ?? nameof(DisposeNativeArray)}.");
            }

            private static void DisposeNativeArrayBestEffort<T>(
                ref NativeArray<T> array,
                JobHandle dependency,
                ref JobHandle disposeHandle,
                ref Exception firstException,
                string allocationLabel) where T : struct
            {
                try
                {
                    DisposeNativeArray(ref array, dependency, ref disposeHandle, allocationLabel);
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
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            bool fastTickRegistered = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            bool lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            if (!fastTickRegistered || !lateFrameRegistered)
            {
                if (fastTickRegistered)
                    GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                if (lateFrameRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                return;
            }

            _registeredFastTick = true;
            _registeredLateFrame = true;
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (_registeredFastTick)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);

            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);

            _registered = false;
            _registeredFastTick = false;
            _registeredLateFrame = false;
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

        private void CachePlayerContextCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            Camera playerCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
            if (playerCamera != null)
                _cameraTransform = playerCamera.transform;
        }

        private void CacheSpatialReadModelsCold()
        {
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _terrainProvider = GlobalRegistry.Terrain;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
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

        private void CompletePendingGroundResponseForOriginShift()
        {
            if (!_groundResponseScheduled)
                return;

            // COLD SYNC JOB: floating-origin rebasing must not race pending IK target writes.
            ForceCompletePendingGroundResponseInPostSimulationWindow();
            SwapTargetBuffers();
            PublishFrontTargetBuffer(applyPresentation: false);
            WriteTelemetrySample(TelemetryReasonOriginShift);
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        private bool CompletePendingGroundResponseForStructuralMutation()
        {
            if (!_groundResponseScheduled)
                return false;

            // COLD SYNC JOB: lifecycle slot mutation must not race pending IK writes.
            ForceCompletePendingGroundResponseInPostSimulationWindow();
            SwapTargetBuffers();
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
            return true;
        }

        private void CompletePendingGroundResponseForRuntimeDisable()
        {
            if (!_groundResponseScheduled)
                return;

            // COLD SYNC JOB: disabled runtimes must not leave pre-shift target writes pending.
            ForceCompletePendingGroundResponseInPostSimulationWindow();
            SwapTargetBuffers();
            PublishFrontTargetBuffer(applyPresentation: false);
            WriteTelemetrySample(TelemetryReasonRuntimeDisable);
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        private void ForceCompletePendingGroundResponseInPostSimulationWindow()
        {
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private void RebaseScheduledEntityStates(float3 shiftOffset)
        {
            if (!_scheduledEntityStates.IsCreated)
                return;

            int stateCount = math.min(MaxEntities, _scheduledEntityStates.Length);
            for (int slotIndex = 0; slotIndex < stateCount; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkEntityState state = _scheduledEntityStates[slotIndex];
                RebaseFinitePosition(ref state.RootPosition, shiftOffset);
                RebaseFinitePosition(ref state.PelvisPosition, shiftOffset);
                RebaseFinitePosition(ref state.LeftFootProbeOrigin, shiftOffset);
                RebaseFinitePosition(ref state.RightFootProbeOrigin, shiftOffset);
                RebaseFinitePosition(ref state.LeftHandProbeOrigin, shiftOffset);
                RebaseFinitePosition(ref state.RightHandProbeOrigin, shiftOffset);
                RebaseFinitePosition(ref state.PredictiveLeftHandPosition, shiftOffset);
                RebaseFinitePosition(ref state.PredictiveRightHandPosition, shiftOffset);
                RebaseFinitePosition(ref state.CameraPosition, shiftOffset);
                RebaseFinitePosition(ref state.DashboardRightHandPosition, shiftOffset);
                _scheduledEntityStates[slotIndex] = state;
            }
        }

        private static void RebaseFinitePosition(ref float3 position, float3 shiftOffset)
        {
            position = math.select(position - shiftOffset, float3.zero, !math.all(math.isfinite(position)));
        }

        private void RebaseTargetFrames(NativeArray<ContextualPhysicalIkTargetFrame> targetFrames, float3 shiftOffset)
        {
            if (!targetFrames.IsCreated)
                return;

            int frameCount = math.min(MaxEntities, targetFrames.Length);
            for (int slotIndex = 0; slotIndex < frameCount; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkTargetFrame frame = targetFrames[slotIndex];
                RebaseContactTarget(ref frame.LeftFoot, shiftOffset);
                RebaseContactTarget(ref frame.RightFoot, shiftOffset);
                RebaseContactTarget(ref frame.LeftHand, shiftOffset);
                RebaseContactTarget(ref frame.RightHand, shiftOffset);
                targetFrames[slotIndex] = frame;
            }
        }

        private static void RebaseContactTarget(ref ContextualPhysicalIkContactTarget target, float3 shiftOffset)
        {
            if (!math.all(math.isfinite(target.WorldPosition)) ||
                !math.isfinite(target.Blend) ||
                !math.isfinite(target.DeltaHeight))
            {
                target = default;
                target.WorldNormal = new float3(0.0f, 1.0f, 0.0f);
                return;
            }

            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(target.WorldNormal, new float3(0.0f, 1.0f, 0.0f));
            if (target.Blend <= 0.0001f &&
                math.lengthsq(target.WorldPosition) <= 0.000001f &&
                target.DeltaHeight == 0.0f)
            {
                return;
            }

            target.WorldPosition -= shiftOffset;
        }

        private void RebaseWeightedIkTargetLanes(float3 shiftOffset)
        {
            if (!_ikTargets.IsCreated || !_ikWeights.IsCreated)
                return;

            int laneCount = math.min(_ikTargets.Length, _ikWeights.Length);
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float weight = _ikWeights[laneIndex];
                if (!math.isfinite(weight))
                {
                    _ikWeights[laneIndex] = 0.0f;
                    _ikTargets[laneIndex] = float3.zero;
                    continue;
                }

                if (weight <= 0.0001f)
                    continue;

                float3 value = _ikTargets[laneIndex];
                if (!math.all(math.isfinite(value)))
                {
                    _ikWeights[laneIndex] = 0.0f;
                    _ikTargets[laneIndex] = float3.zero;
                    continue;
                }

                _ikTargets[laneIndex] = value - shiftOffset;
            }
        }

        private void RebaseFootSoaLanes(float3 shiftOffset)
        {
            if (!_footIkData.IsCreated)
                return;

            int footLaneCount = _footIkData.Length;
            for (int laneIndex = 0; laneIndex < footLaneCount; laneIndex++)
            {
                ContextualPhysicalIkFootData data = _footIkData[laneIndex];
                if (!math.isfinite(data.Blend) || data.Blend <= 0.0001f)
                    continue;

                if (_footTargets.IsCreated && laneIndex < _footTargets.Length)
                {
                    float3 target = _footTargets[laneIndex];
                    if (math.all(math.isfinite(target)))
                        _footTargets[laneIndex] = target - shiftOffset;
                    else
                        _footTargets[laneIndex] = float3.zero;
                }

                if (_footCurrentPos.IsCreated && laneIndex < _footCurrentPos.Length)
                {
                    float3 current = _footCurrentPos[laneIndex];
                    if (math.all(math.isfinite(current)))
                        _footCurrentPos[laneIndex] = current - shiftOffset;
                    else
                        _footCurrentPos[laneIndex] = float3.zero;
                }
            }
        }

        private void RebaseFootData(float3 shiftOffset)
        {
            if (!_footIkData.IsCreated)
                return;

            for (int laneIndex = 0; laneIndex < _footIkData.Length; laneIndex++)
            {
                ContextualPhysicalIkFootData data = _footIkData[laneIndex];
                if (!math.all(math.isfinite(data.TargetPosition)) ||
                    !math.all(math.isfinite(data.CurrentPosition)) ||
                    !math.all(math.isfinite(data.StepStartPosition)) ||
                    !math.isfinite(data.Blend))
                {
                    _footIkData[laneIndex] = default;
                    continue;
                }

                if (data.Blend <= 0.0001f &&
                    math.lengthsq(data.CurrentPosition) <= 0.000001f &&
                    math.lengthsq(data.TargetPosition) <= 0.000001f)
                {
                    continue;
                }

                data.TargetPosition -= shiftOffset;
                data.CurrentPosition -= shiftOffset;
                data.StepStartPosition -= shiftOffset;
                data.SurfaceNormal = ContextualPhysicalIkMath.SafeNormalize(data.SurfaceNormal, new float3(0.0f, 1.0f, 0.0f));
                _footIkData[laneIndex] = data;
            }
        }

        private void RebaseCachedKccBodyPosition(float3 shiftOffset)
        {
            if (_lastKccVelocityFrame == 0u || !math.all(math.isfinite(_lastKccBodyPosition)))
                return;

            _lastKccBodyPosition -= shiftOffset;
        }

        private bool CaptureEntityStates(
            float deltaTime,
            uint frameIndex,
            float3 viewerPosition,
            float3 viewerForward,
            float3 viewerUp,
            float3 viewerRight,
            bool hasViewerPosition,
            float3 kccVelocity)
        {
            if (!_scheduledEntityStates.IsCreated || _scheduledEntityStates.Length < MaxEntities)
                return false;

            bool hasActiveEntity = false;
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                ContextualPhysicalIkEntityState entityState = default;

                if (_slotActive[slotIndex])
                {
                    ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                    if (rig != null && rig.CaptureScheduledState(
                            deltaTime,
                            frameIndex,
                            viewerPosition,
                            viewerForward,
                            viewerUp,
                            viewerRight,
                            hasViewerPosition,
                            ref entityState))
                    {
                        entityState.KccVelocity = ResolveKccVelocityForEntity(in entityState, kccVelocity);
                        entityState.FrameIndex = frameIndex;
                        entityState.EntitySlot = slotIndex;
                        entityState.IsXrActive = HectonXRRuntimeState.IsXRActive ? 1 : 0;
                        hasActiveEntity = true;
                    }
                }

                _scheduledEntityStates[slotIndex] = entityState;
            }

            return hasActiveEntity;
        }

        private bool HasGroundPipelineStorage()
        {
            int rayLaneCount = MaxEntities * RaysPerEntity;
            int handLaneCount = MaxEntities * HandsPerEntity;
            int footLaneCount = MaxEntities * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity;
            return _scheduledEntityStates.IsCreated && _scheduledEntityStates.Length >= MaxEntities &&
                   _scheduledHits.IsCreated && _scheduledHits.Length >= rayLaneCount &&
                   _frontTargetFrames.IsCreated && _frontTargetFrames.Length >= MaxEntities &&
                   _backTargetFrames.IsCreated && _backTargetFrames.Length >= MaxEntities &&
                   _ikTargets.IsCreated && _ikTargets.Length >= handLaneCount &&
                   _ikWeights.IsCreated && _ikWeights.Length >= handLaneCount &&
                   _footIkData.IsCreated && _footIkData.Length >= footLaneCount &&
                   _footTargets.IsCreated && _footTargets.Length >= footLaneCount &&
                   _footCurrentPos.IsCreated && _footCurrentPos.Length >= footLaneCount;
        }

        private float3 ConsumeKccVelocitySignal(uint fallbackFrame)
        {
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if (CoreDeterminismSignals.TryGetLatestKccVelocity(out Hecton8.Core.Contracts.Signals.KccVelocitySignal signal))
            {
                uint fallbackSignalFrame = currentFrame != 0u ? currentFrame : fallbackFrame;
                uint signalFrame = signal.Frame != 0u ? signal.Frame : fallbackSignalFrame;
                bool signalFrameValid = signalFrame != 0u && signalFrame <= currentFrame;
                uint signalAge = signalFrameValid ? currentFrame - signalFrame : KccVelocityMaxAgeFrames + 1u;
                float3 bodyPosition = signal.BodyAup.ToRuntimeFloat3();
                if (signalAge <= KccVelocityMaxAgeFrames &&
                    math.all(math.isfinite(signal.Velocity)) &&
                    math.all(math.isfinite(bodyPosition)))
                {
                    _lastKccVelocity = signal.Velocity;
                    _lastKccBodyPosition = bodyPosition;
                    _lastKccVelocityFrame = signalFrame;
                }
                else
                {
                    ClearCachedKccVelocity();
                    return float3.zero;
                }
            }

            if (_lastKccVelocityFrame == 0u ||
                _lastKccVelocityFrame > currentFrame ||
                !math.all(math.isfinite(_lastKccVelocity)))
            {
                ClearCachedKccVelocity();
                return float3.zero;
            }

            uint cachedAge = currentFrame - _lastKccVelocityFrame;
            if (cachedAge > KccVelocityMaxAgeFrames)
            {
                ClearCachedKccVelocity();
                return float3.zero;
            }

            return _lastKccVelocity;
        }

        private void ClearCachedKccVelocity()
        {
            _lastKccVelocity = float3.zero;
            _lastKccBodyPosition = float3.zero;
            _lastKccVelocityFrame = 0u;
        }

        private float3 ResolveKccVelocityForEntity(in ContextualPhysicalIkEntityState entity, float3 kccVelocity)
        {
            if (_lastKccVelocityFrame == 0u ||
                !math.all(math.isfinite(kccVelocity)) ||
                !math.all(math.isfinite(_lastKccBodyPosition)) ||
                !math.all(math.isfinite(entity.RootPosition)))
            {
                return float3.zero;
            }

            float distanceSq = math.lengthsq(entity.RootPosition - _lastKccBodyPosition);
            return math.isfinite(distanceSq) && distanceSq <= KccVelocityBindingDistanceSq
                ? kccVelocity
                : float3.zero;
        }

        private bool TryResolveViewerPose(
            float deltaTime,
            out float3 viewerPosition,
            out float3 viewerForward,
            out float3 viewerUp,
            out float3 viewerRight)
        {
            viewerPosition = float3.zero;
            viewerForward = new float3(0.0f, 0.0f, 1.0f);
            viewerUp = new float3(0.0f, 1.0f, 0.0f);
            viewerRight = new float3(1.0f, 0.0f, 0.0f);
            if (_cameraTransform != null)
            {
                viewerPosition = ContextualPhysicalIkMath.ToFloat3(_cameraTransform.position);
                viewerForward = ContextualPhysicalIkMath.SafeNormalize(
                    ContextualPhysicalIkMath.ToFloat3(_cameraTransform.forward),
                    viewerForward);
                viewerUp = ContextualPhysicalIkMath.SafeNormalize(
                    ContextualPhysicalIkMath.ToFloat3(_cameraTransform.up),
                    viewerUp);
                viewerRight = ContextualPhysicalIkMath.SafeNormalize(
                    ContextualPhysicalIkMath.ToFloat3(_cameraTransform.right),
                    viewerRight);
                return true;
            }

            _cameraResolveRetryTimer -= deltaTime;
            if (_cameraResolveRetryTimer > 0.0f)
                return false;

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                return false;

            _cameraTransform = playerCamera.transform;
            viewerPosition = ContextualPhysicalIkMath.ToFloat3(_cameraTransform.position);
            viewerForward = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(_cameraTransform.forward),
                viewerForward);
            viewerUp = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(_cameraTransform.up),
                viewerUp);
            viewerRight = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(_cameraTransform.right),
                viewerRight);
            return true;
        }

        private void ScheduleGroundPipeline()
        {
            FillContextualIkSdfHits();

            ContextualPhysicalIkGroundResponseJob responseJob = default;
            responseJob.Entities = _scheduledEntityStates;
            responseJob.Hits = _scheduledHits;
            responseJob.PreviousTargets = _frontTargetFrames;
            responseJob.NextTargets = _backTargetFrames;
            responseJob.IkTargets = _ikTargets;
            responseJob.IkWeights = _ikWeights;
            responseJob.FootData = _footIkData;
            responseJob.FootTargets = _footTargets;
            responseJob.FootCurrentPos = _footCurrentPos;

            JobHandle responseHandle = responseJob.Schedule(MaxEntities, 32);
            _pendingGroundResponseHandle = responseHandle;
            _groundResponseScheduled = true;
        }

        private void FillContextualIkSdfHits()
        {
            if (!_scheduledHits.IsCreated)
                return;

            for (int i = 0; i < _scheduledHits.Length; i++)
                _scheduledHits[i] = default;

            if (!_scheduledEntityStates.IsCreated)
                return;

            for (int slot = 0; slot < MaxEntities; slot++)
            {
                if (!_slotActive[slot] || slot >= _scheduledEntityStates.Length)
                    continue;

                ContextualPhysicalIkEntityState entity = _scheduledEntityStates[slot];
                if (entity.IsActive == 0 || entity.UpdateThisFrame == 0)
                    continue;

                int baseHitIndex = slot * RaysPerEntity;
                if (baseHitIndex + 5 >= _scheduledHits.Length)
                    continue;

                float3 down = new float3(0.0f, -1.0f, 0.0f);
                float leftFootDistance = math.max(0.01f, entity.LeftLegReach * math.max(0.1f, entity.FootProbeDistanceScale));
                float rightFootDistance = math.max(0.01f, entity.RightLegReach * math.max(0.1f, entity.FootProbeDistanceScale));
                if (TryResolveIkProbe(entity.LeftFootProbeOrigin, down, leftFootDistance, entity.GroundLayerMask, out KinematicSurfaceHit leftFootHit))
                    _scheduledHits[baseHitIndex + 0] = leftFootHit;
                if (TryResolveIkProbe(entity.RightFootProbeOrigin, down, rightFootDistance, entity.GroundLayerMask, out KinematicSurfaceHit rightFootHit))
                    _scheduledHits[baseHitIndex + 1] = rightFootHit;

                float3 forward = ContextualPhysicalIkMath.SafeNormalize(entity.CameraForward, new float3(0.0f, 0.0f, 1.0f));
                float leftHandDistance = math.max(0.01f, entity.LeftArmReach * math.max(0.1f, entity.HandProbeDistanceScale));
                float rightHandDistance = math.max(0.01f, entity.RightArmReach * math.max(0.1f, entity.HandProbeDistanceScale));
                if (TryResolveIkProbe(entity.LeftHandProbeOrigin, forward, leftHandDistance, entity.WallLayerMask, out KinematicSurfaceHit leftHandHit))
                    _scheduledHits[baseHitIndex + 2] = leftHandHit;
                if (TryResolveIkProbe(entity.RightHandProbeOrigin, forward, rightHandDistance, entity.WallLayerMask, out KinematicSurfaceHit rightHandHit))
                    _scheduledHits[baseHitIndex + 3] = rightHandHit;

                float toolDistance = math.max(0.01f, entity.ToolCollisionDistance);
                if (TryResolveIkProbe(entity.LeftHandProbeOrigin, forward, toolDistance, entity.WallLayerMask, out KinematicSurfaceHit leftToolHit))
                    _scheduledHits[baseHitIndex + 4] = leftToolHit;
                if (TryResolveIkProbe(entity.RightHandProbeOrigin, forward, toolDistance, entity.WallLayerMask, out KinematicSurfaceHit rightToolHit))
                    _scheduledHits[baseHitIndex + 5] = rightToolHit;
            }
        }

        private bool TryResolveIkProbe(float3 origin, float3 direction, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(direction)) ||
                !math.isfinite(range) ||
                range <= 0.0f)
            {
                return false;
            }

            float3 safeDirection = ContextualPhysicalIkMath.SafeNormalize(direction, new float3(0.0f, 0.0f, 1.0f));
            if (TryResolveIkSdfProbe(origin, safeDirection, range, layerMask, out hit))
                return true;

            return TryResolveIkTerrainProbe(origin, safeDirection, range, layerMask, out hit);
        }

        private bool TryResolveIkSdfProbe(float3 origin, float3 direction, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
                return false;

            IVoxelSonarSdfReadModel readModel = _voxelSdfReadModel;
            if (readModel == null)
                return false;

            float stepMeters = ResolveIkSdfStepMeters(range);
            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    origin,
                    direction,
                    range,
                    stepMeters,
                    out VoxelSonarSdfRaycastHit sdfHit) ||
                (sdfHit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.all(math.isfinite(sdfHit.Point)) ||
                !math.all(math.isfinite(sdfHit.Normal)) ||
                !math.isfinite(sdfHit.Distance) ||
                sdfHit.Distance < 0.0f ||
                sdfHit.Distance > range)
            {
                return false;
            }

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(sdfHit.Normal, -direction);
            if (math.dot(normal, direction) >= 0.0f)
                normal = -normal;

            hit.point = new Vector3(sdfHit.Point.x, sdfHit.Point.y, sdfHit.Point.z);
            hit.normal = new Vector3(normal.x, normal.y, normal.z);
            hit.distance = math.max(0.0f, sdfHit.Distance);
            return true;
        }

        private bool TryResolveIkTerrainProbe(float3 origin, float3 direction, float range, int layerMask, out KinematicSurfaceHit hit)
        {
            hit = default;
            if (!IncludesAnyLayer(layerMask, HectonLayerMasks.TerrainLayerMask) ||
                direction.y >= -0.0001f)
            {
                return false;
            }

            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider == null ||
                !terrainProvider.IsAvailable ||
                !terrainProvider.TryGetHeight(origin.x, origin.z, out float terrainHeight) ||
                !math.isfinite(terrainHeight))
            {
                return false;
            }

            float distance = (terrainHeight - origin.y) / direction.y;
            if (!math.isfinite(distance) ||
                distance < 0.0f ||
                distance > range)
            {
                return false;
            }

            float3 point = origin + direction * distance;
            Vector3 normal = Vector3.up;
            if (terrainProvider.TryGetNormal(point.x, point.z, 1.0f, out Vector3 sampledNormal) &&
                math.all(math.isfinite(new float3(sampledNormal.x, sampledNormal.y, sampledNormal.z))))
            {
                float3 sampled = ContextualPhysicalIkMath.SafeNormalize(new float3(sampledNormal.x, sampledNormal.y, sampledNormal.z), new float3(0.0f, 1.0f, 0.0f));
                normal = new Vector3(sampled.x, sampled.y, sampled.z);
            }

            if (Vector3.Dot(normal, new Vector3(direction.x, direction.y, direction.z)) >= 0.0f)
                normal = -normal;

            hit.point = new Vector3(point.x, point.y, point.z);
            hit.normal = normal;
            hit.distance = distance;
            return true;
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static float ResolveIkSdfStepMeters(float range)
        {
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1.0f);
            float coarse = math.max(0.10f, range * 0.12f);
            float fine = math.max(0.035f, range * 0.04f);
            return math.lerp(coarse, fine, quality);
        }

        private void SwapTargetBuffers()
        {
            NativeArray<ContextualPhysicalIkTargetFrame> swapBuffer = _frontTargetFrames;
            _frontTargetFrames = _backTargetFrames;
            _backTargetFrames = swapBuffer;
        }

        private void PublishFrontTargetBuffer(bool applyPresentation)
        {
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                if (rig == null)
                    continue;

                rig.OnTargetBufferSwapped(_frontTargetFrames.IsCreated ? _frontTargetFrames.AsReadOnly() : default, applyPresentation);
            }
        }

        private void WriteTelemetrySample(uint reasonFlags)
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length <= 0)
                return;

            uint stateHash = 2166136261u;
            ushort activeCount = 0;
            float3 firstRootPosition = float3.zero;
            float3 firstLeftFootTarget = float3.zero;
            float3 firstRightFootTarget = float3.zero;
            float3 firstLeftTarget = float3.zero;
            float3 firstRightTarget = float3.zero;
            float3 firstKccVelocity = float3.zero;
            float2 firstWeights = float2.zero;
            bool capturedFirst = false;
            bool invalid = false;
            uint lowerBodyFlags = 0u;
            int entityStateCount = _scheduledEntityStates.IsCreated ? _scheduledEntityStates.Length : 0;
            int targetFrameCount = _frontTargetFrames.IsCreated ? _frontTargetFrames.Length : 0;
            int weightCount = _ikWeights.IsCreated ? _ikWeights.Length : 0;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                activeCount++;
                bool hasEntityState = slotIndex < entityStateCount;
                bool hasTargetFrame = slotIndex < targetFrameCount;
                ContextualPhysicalIkEntityState entity = hasEntityState ? _scheduledEntityStates[slotIndex] : default;
                ContextualPhysicalIkTargetFrame frame = hasTargetFrame ? _frontTargetFrames[slotIndex] : default;
                int baseIkIndex = slotIndex * HandsPerEntity;
                bool hasWeightLanes = baseIkIndex + RightHandIndex < weightCount;
                float2 weights = hasWeightLanes
                    ? new float2(
                        _ikWeights[baseIkIndex + LeftHandIndex],
                        _ikWeights[baseIkIndex + RightHandIndex])
                    : float2.zero;
                float3 safeRootPosition = SanitizeTelemetryFloat3(entity.RootPosition, float3.zero);
                float3 safeLeftFootPosition = SanitizeTelemetryFloat3(frame.LeftFoot.WorldPosition, float3.zero);
                float3 safeRightFootPosition = SanitizeTelemetryFloat3(frame.RightFoot.WorldPosition, float3.zero);
                float3 safeLeftHandPosition = SanitizeTelemetryFloat3(frame.LeftHand.WorldPosition, float3.zero);
                float3 safeRightHandPosition = SanitizeTelemetryFloat3(frame.RightHand.WorldPosition, float3.zero);
                float3 safeKccVelocity = SanitizeTelemetryFloat3(entity.KccVelocity, float3.zero);
                float2 safeWeights = SanitizeTelemetryFloat2(weights, float2.zero);

                stateHash = MixHash(stateHash, (uint)slotIndex);
                stateHash = MixHash(stateHash, math.hash(safeRootPosition));
                stateHash = MixHash(stateHash, math.hash(safeLeftFootPosition));
                stateHash = MixHash(stateHash, math.hash(safeRightFootPosition));
                stateHash = MixHash(stateHash, math.hash(safeLeftHandPosition));
                stateHash = MixHash(stateHash, math.hash(safeRightHandPosition));
                stateHash = MixHash(stateHash, math.hash(safeKccVelocity));
                stateHash = MixHash(stateHash, math.hash(safeWeights));
                lowerBodyFlags |= frame.LowerBodyFlags;

                bool slotInvalid =
                    !hasEntityState ||
                    !hasTargetFrame ||
                    !hasWeightLanes ||
                    !math.all(math.isfinite(entity.RootPosition)) ||
                    !math.all(math.isfinite(frame.LeftFoot.WorldPosition)) ||
                    !math.all(math.isfinite(frame.RightFoot.WorldPosition)) ||
                    !math.all(math.isfinite(frame.LeftHand.WorldPosition)) ||
                    !math.all(math.isfinite(frame.RightHand.WorldPosition)) ||
                    !math.all(math.isfinite(entity.KccVelocity)) ||
                    !math.all(math.isfinite(weights));
                invalid |= slotInvalid;

                if (capturedFirst)
                    continue;

                firstRootPosition = safeRootPosition;
                firstLeftFootTarget = safeLeftFootPosition;
                firstRightFootTarget = safeRightFootPosition;
                firstLeftTarget = safeLeftHandPosition;
                firstRightTarget = safeRightHandPosition;
                firstKccVelocity = safeKccVelocity;
                firstWeights = safeWeights;
                capturedFirst = true;
            }

            uint flags = reasonFlags | ((lowerBodyFlags & 0xFFu) << 8) | (invalid ? 0x80000000u : 0u);
            int telemetryCapacity = _telemetryRing.Length;
            int telemetryCursor = (uint)_telemetryCursor < (uint)telemetryCapacity ? _telemetryCursor : 0;
            _telemetryRing[telemetryCursor] = new ContextualPhysicalIkTelemetryEntry
            {
                Frame = SystemDispatcher.CurrentFrameId,
                Flags = flags,
                StateHash = stateHash,
                ActiveEntities = activeCount,
                Reserved = 0,
                FirstRootPosition = firstRootPosition,
                FirstLeftFootTarget = firstLeftFootTarget,
                FirstRightFootTarget = firstRightFootTarget,
                FirstLeftHandTarget = firstLeftTarget,
                FirstRightHandTarget = firstRightTarget,
                FirstKccVelocity = firstKccVelocity,
                FirstHandWeights = firstWeights
            };

            telemetryCursor++;
            _telemetryCursor = telemetryCursor >= telemetryCapacity ? 0 : telemetryCursor;

            if (invalid && !_telemetryDumped)
                DumpTelemetry(flags);
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static NativeArray<T> CreateTransientNativeArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string allocationLabel) where T : struct
        {
            NativeArray<T> array = H8Memory.Allocate<T>(
                length,
                NativeArrayOwnerSystem,
                allocator,
                options);

            if (!array.IsCreated || array.Length != length)
                throw new InvalidOperationException($"{NativeMemoryTransientAllocationFailureMessage} Label={allocationLabel}.");

            return array;
        }

        private static void DisposeTransientNativeArray<T>(ref NativeArray<T> array, string allocationLabel = null) where T : struct
        {
            if (!array.IsCreated)
                return;

            H8Memory.Release(ref array, NativeArrayOwnerSystem);

            if (array.IsCreated)
                throw new InvalidOperationException($"{NativeMemoryReleaseFailureMessage} Label={allocationLabel ?? nameof(DisposeTransientNativeArray)}.");
        }

        private unsafe void DumpTelemetry(uint reasonFlags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            if (_telemetryDumped)
                return;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if (_nextTelemetryDumpRetryFrame != 0u &&
                (int)(currentFrame - _nextTelemetryDumpRetryFrame) < 0)
            {
                return;
            }

            int capacity = _telemetryRing.Length;
            if (capacity <= 0)
                return;

            int entryBytes = UnsafeUtility.SizeOf<ContextualPhysicalIkTelemetryEntry>();
            if (entryBytes <= 0 || capacity > (int.MaxValue - 24) / entryBytes)
                return;

            int head = (uint)_telemetryCursor < (uint)capacity ? _telemetryCursor : 0;
            int payloadBytes = 24 + capacity * entryBytes;
            NativeArray<byte> payload = default;
            bool dumpWritten = false;
            bool dumpAttempted = false;
            try
            {
                dumpAttempted = true;
                payload = CreateTransientNativeArray<byte>(
                    payloadBytes,
                    Allocator.Temp,
                    NativeArrayOptions.ClearMemory,
                    "contextualPhysicalIkTelemetryDumpBytes");
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int writeCursor = 0;
                WriteUInt64LittleEndian(destination, ref writeCursor, TelemetryDumpMagic);
                WriteUInt32LittleEndian(destination, ref writeCursor, reasonFlags);
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)capacity));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)entryBytes));
                WriteUInt32LittleEndian(destination, ref writeCursor, unchecked((uint)head));

                for (int i = 0; i < capacity; i++)
                {
                    int ringIndex = head + i;
                    if (ringIndex >= capacity)
                        ringIndex -= capacity;

                    int rowEnd = writeCursor + entryBytes;
                    ContextualPhysicalIkTelemetryEntry entry = _telemetryRing[ringIndex];
                    WriteTelemetryEntry(destination, ref writeCursor, in entry);
                    if (writeCursor > rowEnd)
                        return;

                    writeCursor = rowEnd;
                }

                dumpWritten = writeCursor == payloadBytes &&
                    NativeFaultDumpWriter.TryWriteAll(TelemetryDumpRelativePath, payload, writeCursor);
            }
            finally
            {
                _telemetryDumped = dumpWritten;
                _nextTelemetryDumpRetryFrame = dumpWritten || !dumpAttempted
                    ? 0u
                    : currentFrame + TelemetryDumpRetryFrameInterval;
                DisposeTransientNativeArray(ref payload, "contextualPhysicalIkTelemetryDumpBytes");
            }
        }

        private static unsafe void WriteTelemetryEntry(byte* destination, ref int cursor, in ContextualPhysicalIkTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteUInt32LittleEndian(destination, ref cursor, entry.StateHash);
            WriteUInt16LittleEndian(destination, ref cursor, entry.ActiveEntities);
            WriteUInt16LittleEndian(destination, ref cursor, entry.Reserved);
            WriteFloat3(destination, ref cursor, entry.FirstRootPosition);
            WriteFloat3(destination, ref cursor, entry.FirstLeftFootTarget);
            WriteFloat3(destination, ref cursor, entry.FirstRightFootTarget);
            WriteFloat3(destination, ref cursor, entry.FirstLeftHandTarget);
            WriteFloat3(destination, ref cursor, entry.FirstRightHandTarget);
            WriteFloat3(destination, ref cursor, entry.FirstKccVelocity);
            float2 weights = SanitizeTelemetryFloat2(entry.FirstHandWeights, float2.zero);
            WriteFloat(destination, ref cursor, weights.x);
            WriteFloat(destination, ref cursor, weights.y);
        }

        private static unsafe void WriteFloat3(byte* destination, ref int cursor, float3 value)
        {
            value = SanitizeTelemetryFloat3(value, float3.zero);
            WriteFloat(destination, ref cursor, value.x);
            WriteFloat(destination, ref cursor, value.y);
            WriteFloat(destination, ref cursor, value.z);
        }

        private static unsafe void WriteFloat(byte* destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static unsafe void WriteUInt16LittleEndian(byte* destination, ref int cursor, ushort value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            cursor += sizeof(ushort);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, ref int cursor, ulong value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            destination[cursor + 4] = (byte)(value >> 32);
            destination[cursor + 5] = (byte)(value >> 40);
            destination[cursor + 6] = (byte)(value >> 48);
            destination[cursor + 7] = (byte)(value >> 56);
            cursor += sizeof(ulong);
        }

        private static float3 SanitizeTelemetryFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private static float2 SanitizeTelemetryFloat2(float2 value, float2 fallback)
        {
            float2 safeFallback = math.select(fallback, float2.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private void ResetTargetSlot(int slotIndex)
        {
            if ((uint)slotIndex >= (uint)MaxEntities)
                return;

            if (_frontTargetFrames.IsCreated && (uint)slotIndex < (uint)_frontTargetFrames.Length)
                _frontTargetFrames[slotIndex] = default;

            if (_backTargetFrames.IsCreated && (uint)slotIndex < (uint)_backTargetFrames.Length)
                _backTargetFrames[slotIndex] = default;

            int baseIkIndex = slotIndex * HandsPerEntity;
            if (_ikTargets.IsCreated && baseIkIndex + RightHandIndex < _ikTargets.Length)
            {
                _ikTargets[baseIkIndex + LeftHandIndex] = float3.zero;
                _ikTargets[baseIkIndex + RightHandIndex] = float3.zero;
            }

            if (_ikWeights.IsCreated && baseIkIndex + RightHandIndex < _ikWeights.Length)
            {
                _ikWeights[baseIkIndex + LeftHandIndex] = 0.0f;
                _ikWeights[baseIkIndex + RightHandIndex] = 0.0f;
            }

            int baseFootIndex = slotIndex * ContextualPhysicalIkLowerBodyConstants.FeetPerEntity;
            if (_footIkData.IsCreated && baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex < _footIkData.Length)
            {
                _footIkData[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex] = default;
                _footIkData[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex] = default;
            }

            if (_footTargets.IsCreated && baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex < _footTargets.Length)
            {
                _footTargets[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex] = float3.zero;
                _footTargets[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex] = float3.zero;
            }

            if (_footCurrentPos.IsCreated && baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex < _footCurrentPos.Length)
            {
                _footCurrentPos[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.LeftFootIndex] = float3.zero;
                _footCurrentPos[baseFootIndex + ContextualPhysicalIkLowerBodyConstants.RightFootIndex] = float3.zero;
            }
        }
    }
}
