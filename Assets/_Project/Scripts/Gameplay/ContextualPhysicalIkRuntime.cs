using Hecton8.Core;
using Hecton8.World;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
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
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkEntityState
    {
        public int IsActive;
        public int EnableFootPlacement;
        public int EnableHandBracing;
        public int EnableWallTouch;
        public int LeftHandEmpty;
        public int EnableToolRetraction;
        public int HasCameraPose;
        public float DeltaTime;
        public quaternion RootRotation;
        public float3 RootPosition;
        public float3 PelvisPosition;
        public float3 LeftFootProbeOrigin;
        public float3 RightFootProbeOrigin;
        public float3 LeftHandProbeOrigin;
        public float3 RightHandProbeOrigin;
        public float3 PredictiveLeftHandPosition;
        public float3 PredictiveRightHandPosition;
        public float3 PredictiveLeftHandNormal;
        public float3 PredictiveRightHandNormal;
        public float3 CameraPosition;
        public float3 CameraForward;
        public float3 CameraUp;
        public float3 CameraRight;
        public float3 LeftToolRecoilOffset;
        public float3 RightToolRecoilOffset;
        public float3 LeftColdShiverOffset;
        public float3 RightColdShiverOffset;
        public float3 DashboardRightHandPosition;
        public float3 DashboardRightHandNormal;
        public float LeftLegReach;
        public float RightLegReach;
        public float LeftArmReach;
        public float RightArmReach;
        public float PredictiveLeftHandBlend;
        public float PredictiveRightHandBlend;
        public float CameraHandLateralOffset;
        public float CameraHandVerticalOffset;
        public float ToolCollisionDistance;
        public float ToolRetractionBackDistance;
        public float ToolRetractionLiftDistance;
        public float ToolRetractionBlend;
        public float ToolRecoilMaxOffset;
        public float DashboardRightHandBlend;
        public float ColdShiverBlend;
        public float FootContactOffset;
        public float HandContactOffset;
        public float FootProbeDistanceScale;
        public float HandProbeDistanceScale;
        public int GroundLayerMask;
        public int WallLayerMask;
        public float TunnelClearanceDistance;
        public float HandBraceFadeDistance;
        public float TargetPositionSharpness;
        public float TargetNormalSharpness;
        public float BlendFadeSharpness;
        public float MaxDeltaHeight;
        public float ComShiftLateralFactor;
        public float ComShiftForwardFactor;
        public float ComShiftVerticalFactor;
        public float ComResponseSharpness;
        public float ComLeanPitchRadians;
        public float ComLeanRollRadians;
        public float MaxComLateral;
        public float MaxComForward;
        public float MaxComVertical;
        public int UpdateThisFrame;
        public float ViewerDistanceSq;
        public uint UpdateBitfield;
        public byte ThrottleTier;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkTelemetryEntry
    {
        public uint Frame;
        public uint Flags;
        public uint StateHash;
        public ushort ActiveEntities;
        public ushort Reserved;
        public float3 FirstRootPosition;
        public float3 FirstLeftHandTarget;
        public float3 FirstRightHandTarget;
        public float2 FirstHandWeights;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkGroundDetectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ContextualPhysicalIkEntityState> Entities;
        public NativeArray<RaycastCommand> Commands;

        public void Execute(int index)
        {
            int baseCommandIndex = index * ContextualPhysicalIkRuntime.RaysPerEntity;
            ContextualPhysicalIkEntityState entity = Entities[index];

            if (entity.IsActive == 0 || entity.UpdateThisFrame == 0)
            {
                WriteDisabledCommands(baseCommandIndex);
                return;
            }

            QueryParameters groundQuery = new QueryParameters(entity.GroundLayerMask, false, QueryTriggerInteraction.Ignore, false);
            QueryParameters wallQuery = new QueryParameters(entity.WallLayerMask, false, QueryTriggerInteraction.Ignore, false);

            float footPlacementMask = math.select(0.0f, 1.0f, entity.EnableFootPlacement != 0);
            float leftFootDistance = math.max(0.0f, entity.LeftLegReach * entity.FootProbeDistanceScale) * footPlacementMask;
            float rightFootDistance = math.max(0.0f, entity.RightLegReach * entity.FootProbeDistanceScale) * footPlacementMask;
            bool leftHandUsesPredictiveLatch = entity.PredictiveLeftHandBlend > 0.0001f;
            bool rightHandUsesPredictiveLatch = entity.PredictiveRightHandBlend > 0.0001f;
            float wallTouchMask = math.select(0.0f, 1.0f, entity.EnableHandBracing != 0 && entity.EnableWallTouch != 0);
            float leftHandMask = math.select(0.0f, 1.0f, wallTouchMask > 0.0f && entity.LeftHandEmpty != 0 && !leftHandUsesPredictiveLatch);
            float rightHandMask = math.select(0.0f, 1.0f, wallTouchMask > 0.0f && !rightHandUsesPredictiveLatch);
            float leftHandDistance = math.max(0.0f, entity.LeftArmReach * entity.HandProbeDistanceScale) * leftHandMask;
            float rightHandDistance = math.max(0.0f, entity.RightArmReach * entity.HandProbeDistanceScale) * rightHandMask;

            float3 leftBraceDirection = math.mul(entity.RootRotation, new float3(-0.70710677f, -0.70710677f, 0.0f));
            float3 rightBraceDirection = math.mul(entity.RootRotation, new float3(0.70710677f, -0.70710677f, 0.0f));
            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f)),
                new float3(0.0f, 0.0f, 1.0f));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.0f, 1.0f, 0.0f)),
                new float3(0.0f, 1.0f, 0.0f));
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(1.0f, 0.0f, 0.0f)),
                new float3(1.0f, 0.0f, 0.0f));
            float3 cameraForward = ContextualPhysicalIkMath.SafeNormalize(entity.CameraForward, rootForward);
            float3 cameraUp = ContextualPhysicalIkMath.SafeNormalize(entity.CameraUp, rootUp);
            float3 cameraRight = ContextualPhysicalIkMath.SafeNormalize(entity.CameraRight, rootRight);
            float3 cameraPosition = math.select(entity.RootPosition, entity.CameraPosition, entity.HasCameraPose != 0);
            float cameraHandLateralOffset = math.max(0.0f, entity.CameraHandLateralOffset);
            float cameraHandVerticalOffset = entity.CameraHandVerticalOffset;
            float toolDistance = math.max(0.0f, entity.ToolCollisionDistance) *
                math.select(0.0f, 1.0f, entity.EnableToolRetraction != 0 && entity.HasCameraPose != 0);
            float3 leftToolRayOrigin = cameraPosition - (cameraRight * cameraHandLateralOffset) + (cameraUp * cameraHandVerticalOffset);
            float3 rightToolRayOrigin = cameraPosition + (cameraRight * cameraHandLateralOffset) + (cameraUp * cameraHandVerticalOffset);

            Commands[baseCommandIndex + 0] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.LeftFootProbeOrigin),
                Vector3.down,
                groundQuery,
                leftFootDistance);

            Commands[baseCommandIndex + 1] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.RightFootProbeOrigin),
                Vector3.down,
                groundQuery,
                rightFootDistance);

            if (leftHandUsesPredictiveLatch || leftHandDistance <= 0.0001f)
            {
                WriteDisabledCommand(baseCommandIndex + 2);
            }
            else
            {
                Commands[baseCommandIndex + 2] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(entity.LeftHandProbeOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(leftBraceDirection),
                    wallQuery,
                    leftHandDistance);
            }

            if (rightHandUsesPredictiveLatch || rightHandDistance <= 0.0001f)
            {
                WriteDisabledCommand(baseCommandIndex + 3);
            }
            else
            {
                Commands[baseCommandIndex + 3] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(entity.RightHandProbeOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(rightBraceDirection),
                    wallQuery,
                    rightHandDistance);
            }

            if (toolDistance <= 0.0001f)
            {
                WriteDisabledCommand(baseCommandIndex + 4);
                WriteDisabledCommand(baseCommandIndex + 5);
            }
            else
            {
                Commands[baseCommandIndex + 4] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(leftToolRayOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(cameraForward),
                    wallQuery,
                    toolDistance);

                Commands[baseCommandIndex + 5] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(rightToolRayOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(cameraForward),
                    wallQuery,
                    toolDistance);
            }
        }

        private void WriteDisabledCommands(int baseCommandIndex)
        {
            for (int i = 0; i < ContextualPhysicalIkRuntime.RaysPerEntity; i++)
                WriteDisabledCommand(baseCommandIndex + i);
        }

        private void WriteDisabledCommand(int commandIndex)
        {
            Commands[commandIndex] = new RaycastCommand(
                Vector3.zero,
                Vector3.down,
                new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore, false),
                0.0f);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkGroundResponseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ContextualPhysicalIkEntityState> Entities;
        [ReadOnly] public NativeArray<RaycastHit> Hits;
        [ReadOnly] public NativeArray<ContextualPhysicalIkTargetFrame> PreviousTargets;
        public NativeArray<ContextualPhysicalIkTargetFrame> NextTargets;
        public NativeArray<float3> IkTargets;
        public NativeArray<float> IkWeights;

        public void Execute(int index)
        {
            int baseIkIndex = index * ContextualPhysicalIkRuntime.HandsPerEntity;
            ContextualPhysicalIkEntityState entity = Entities[index];
            if (entity.IsActive == 0)
            {
                NextTargets[index] = default;
                IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = float3.zero;
                IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = float3.zero;
                IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = 0.0f;
                IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = 0.0f;
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
            RaycastHit leftFootHit = Hits[baseHitIndex + 0];
            RaycastHit rightFootHit = Hits[baseHitIndex + 1];
            RaycastHit leftHandHit = Hits[baseHitIndex + 2];
            RaycastHit rightHandHit = Hits[baseHitIndex + 3];
            RaycastHit leftToolHit = Hits[baseHitIndex + 4];
            RaycastHit rightToolHit = Hits[baseHitIndex + 5];

            if (entity.UpdateThisFrame == 0)
            {
                WriteIkSoa(baseIkIndex, in next);
                NextTargets[index] = next;
                return;
            }

            if (entity.EnableFootPlacement != 0)
            {
                ResolveContactTarget(
                    ref next.LeftFoot,
                    in previous.LeftFoot,
                    in leftFootHit,
                    entity.LeftFootProbeOrigin,
                    entity.FootContactOffset,
                    1.0f,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);

                ResolveContactTarget(
                    ref next.RightFoot,
                    in previous.RightFoot,
                    in rightFootHit,
                    entity.RightFootProbeOrigin,
                    entity.FootContactOffset,
                    1.0f,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);
            }
            else
            {
                FadeOutTarget(ref next.LeftFoot, in previous.LeftFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeOutTarget(ref next.RightFoot, in previous.RightFoot, entity.BlendFadeSharpness, entity.DeltaTime);
            }

            float tunnelTargetBlend = entity.EnableHandBracing != 0 && entity.EnableWallTouch != 0
                ? ResolveBraceProxyTunnelBlend(in leftHandHit, in rightHandHit, in entity)
                : 0.0f;
            next.TunnelBlend = ContextualPhysicalIkMath.SmoothScalar(previous.TunnelBlend, tunnelTargetBlend, entity.BlendFadeSharpness, entity.DeltaTime);
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

            next.ComOffsetLocal = ContextualPhysicalIkMath.SmoothVector(
                previous.ComOffsetLocal,
                new float3(targetLateral, targetVertical, targetForward),
                entity.ComResponseSharpness,
                entity.DeltaTime);

            next.ComLeanRadians = new float2(
                ContextualPhysicalIkMath.SmoothScalar(previous.ComLeanRadians.x, pitch, entity.ComResponseSharpness, entity.DeltaTime),
                ContextualPhysicalIkMath.SmoothScalar(previous.ComLeanRadians.y, roll, entity.ComResponseSharpness, entity.DeltaTime));

            WriteIkSoa(baseIkIndex, in next);
            NextTargets[index] = next;
        }

        private void WriteIkSoa(int baseIkIndex, in ContextualPhysicalIkTargetFrame frame)
        {
            IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = frame.LeftHand.WorldPosition;
            IkTargets[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = frame.RightHand.WorldPosition;
            IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.LeftHandIndex] = math.saturate(frame.LeftHand.Blend);
            IkWeights[baseIkIndex + ContextualPhysicalIkRuntime.RightHandIndex] = math.saturate(frame.RightHand.Blend);
        }

        private static float2 ResolveSlopeLeanRadians(
            in ContextualPhysicalIkTargetFrame frame,
            in ContextualPhysicalIkEntityState entity)
        {
            float leftBlend = math.saturate(frame.LeftFoot.Blend);
            float rightBlend = math.saturate(frame.RightFoot.Blend);
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
            target = previous;
            target.Blend = ContextualPhysicalIkMath.SmoothScalar(previous.Blend, 0.0f, fadeSharpness, deltaTime);
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
            float targetBlend = math.saturate(predictiveBlend);
            if (targetBlend <= 0.0001f || !math.all(math.isfinite(predictivePosition)))
                return;

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(predictiveNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, predictivePosition);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, normal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, predictivePosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = math.max(target.Blend, ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime));
            target.DeltaHeight = 0.0f;
        }

        private static void ApplyToolRetraction(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            in RaycastHit hit,
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
            float safeCollisionDistance = math.max(0.0001f, collisionDistance);
            if (!HasHit(in hit) || hit.distance >= safeCollisionDistance)
                return;

            float3 forward = ContextualPhysicalIkMath.SafeNormalize(cameraForward, new float3(0.0f, 0.0f, 1.0f));
            float3 up = ContextualPhysicalIkMath.SafeNormalize(cameraUp, new float3(0.0f, 1.0f, 0.0f));
            float blocked01 = math.saturate((safeCollisionDistance - math.max(0.0f, hit.distance)) * math.rcp(safeCollisionDistance));
            float targetBlend = blocked01 * math.saturate(blendScale);
            if (targetBlend <= 0.0001f)
                return;

            float3 hitNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.ToFloat3(hit.normal),
                -forward);
            float3 targetPosition = probeOrigin -
                (forward * (math.max(0.0f, backDistance) * blocked01)) +
                (up * (math.max(0.0f, liftDistance) * blocked01));
            targetPosition = math.select(targetPosition, probeOrigin, !math.all(math.isfinite(targetPosition)));

            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, targetPosition);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, hitNormal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, targetPosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, hitNormal, normalSharpness, deltaTime),
                hitNormal);
            target.Blend = math.max(target.Blend, ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime));
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
            float safeMaxOffset = math.max(0.0f, maxOffset);
            if (safeMaxOffset <= 0.000001f || !math.all(math.isfinite(recoilOffset)))
                return;

            float3 clampedOffset = ClampOffsetNoSqrt(recoilOffset, safeMaxOffset);
            float offsetSq = math.lengthsq(clampedOffset);
            if (offsetSq <= 0.000001f)
                return;

            float maxOffsetSq = math.max(0.000001f, safeMaxOffset * safeMaxOffset);
            float targetBlend = math.saturate(offsetSq * math.rcp(maxOffsetSq));
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
            target.Blend = math.max(target.Blend, ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime));
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
            float activeBlend = math.saturate(blend) * math.saturate(target.Blend);
            if (activeBlend <= 0.0001f || !math.all(math.isfinite(offset)))
                return;

            target.WorldPosition += offset * activeBlend;
        }

        private static void ResolveContactTarget(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            in RaycastHit hit,
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

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(hit.normal), new float3(0.0f, 1.0f, 0.0f));
            float3 point = ContextualPhysicalIkMath.ToFloat3(hit.point) + (normal * contactOffset);

            float3 currentPosition = ResolveSmoothingPosition(in target, in previous, point);
            float3 currentNormal = ResolveSmoothingNormal(in target, in previous, normal);
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, point, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime);
            target.DeltaHeight = math.clamp(point.y - probeOrigin.y, -maxDeltaHeight, maxDeltaHeight);
        }

        private static float3 ResolveSmoothingPosition(
            in ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 fallback)
        {
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
            if (target.Blend > 0.0001f && math.all(math.isfinite(target.WorldNormal)))
                return target.WorldNormal;

            if (previous.Blend > 0.0001f && math.all(math.isfinite(previous.WorldNormal)))
                return previous.WorldNormal;

            return fallback;
        }

        private static bool HasHit(in RaycastHit hit)
        {
            return hit.distance > 0.0f || math.lengthsq(ContextualPhysicalIkMath.ToFloat3(hit.normal)) > 0.0001f;
        }

        private static float ResolveBraceProxyTunnelBlend(
            in RaycastHit leftHandHit,
            in RaycastHit rightHandHit,
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
            in RaycastHit hit,
            float armReach,
            float distanceScale,
            float clearanceDistance,
            float fadeDistance)
        {
            if (!HasHit(in hit))
                return 0.0f;

            float scaledReach = math.max(0.0001f, armReach * math.max(0.0001f, distanceScale));
            float proxyDistance = math.max(0.0001f, math.min(scaledReach, math.max(0.0001f, clearanceDistance)));
            float safeFadeDistance = math.max(0.0001f, fadeDistance);
            return math.saturate((proxyDistance - math.max(0.0f, hit.distance)) * math.rcp(safeFadeDistance));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    internal sealed class ContextualPhysicalIkRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener
    {
        private const int MaxEntities = 128;
        internal const int RaysPerEntity = 6;
        internal const int HandsPerEntity = 2;
        internal const int LeftHandIndex = 0;
        internal const int RightHandIndex = 1;
        private const int TelemetryCapacity = 300;
        private const int MinCommandsPerJob = 32;
        private const float CameraResolveRetryInterval = 1.0f;
        private const string NativeMemoryOwner = nameof(ContextualPhysicalIkRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_PLAYER_TOOL_IK.bin";

        // COLD ALLOC: ContextualPhysicalIkRig[128] - stable slot owner registry for contextual IK entities - owner: ContextualPhysicalIkRuntime
        private readonly ContextualPhysicalIkRig[] _registeredRigs = new ContextualPhysicalIkRig[MaxEntities];
        // COLD ALLOC: bool[128] - active slot bitset for contextual IK entities - owner: ContextualPhysicalIkRuntime
        private readonly bool[] _slotActive = new bool[MaxEntities];
        // COLD ALLOC: int[128] - free-slot stack for contextual IK stable indexing - owner: ContextualPhysicalIkRuntime
        private readonly int[] _freeSlots = new int[MaxEntities];

        private NativeArray<ContextualPhysicalIkEntityState> _scheduledEntityStates;
        private NativeArray<RaycastCommand> _scheduledCommands;
        private NativeArray<RaycastHit> _scheduledHits;
        private NativeArray<ContextualPhysicalIkTargetFrame> _frontTargetFrames;
        private NativeArray<ContextualPhysicalIkTargetFrame> _backTargetFrames;
        private NativeArray<float3> _ikTargets;
        private NativeArray<float> _ikWeights;
        private NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing;

        private JobHandle _pendingGroundResponseHandle;
        private JobHandle _disposeHandle;
        private Transform _cameraTransform;
        private bool _groundResponseScheduled;
        private bool _registered;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private int _freeSlotCount;
        private float _cameraResolveRetryTimer;
        private uint _frameIndex;
        private int _telemetryCursor;
        private bool _telemetryDumped;

        internal NativeArray<ContextualPhysicalIkTargetFrame> CurrentTargetFrames => _frontTargetFrames;

        internal static ContextualPhysicalIkRuntime EnsureRuntimeInstance()
        {
            ContextualPhysicalIkRuntime runtime = GlobalRegistry.ContextualPhysicalIkRuntime;
            if (runtime != null)
                return runtime;

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

            GlobalRegistry.RegisterContextualPhysicalIkRuntime(this);
            InitializeFreeSlots();
            EnsurePersistentBuffers();
        }

        private void OnEnable()
        {
            EnsurePersistentBuffers();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            JobHandle dependency = _groundResponseScheduled ? _pendingGroundResponseHandle : default;
            DisposeBuffers(dependency);
            GlobalRegistry.ClearContextualPhysicalIkRuntime(this);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            CompletePendingGroundResponseForOriginShift();

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            RebaseScheduledEntityStates(offset);
            RebaseTargetFrames(_frontTargetFrames, offset);
            RebaseTargetFrames(_backTargetFrames, offset);
            RebaseFloat3Lanes(_ikTargets, offset);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
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

            if (!CaptureEntityStates(deltaTime, frameIndex, viewerPosition, viewerForward, viewerUp, viewerRight, hasViewerPosition))
                return;

            ScheduleGroundPipeline();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_groundResponseScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: false))
                return;

            SwapTargetBuffers();
            PublishFrontTargetBuffer();
            WriteTelemetrySample(0u);
            _groundResponseScheduled = false;
        }

        internal bool RegisterRig(ContextualPhysicalIkRig rig, out int slotIndex)
        {
            slotIndex = -1;
            if (rig == null || _freeSlotCount <= 0)
                return false;

            int freeStackIndex = _freeSlotCount - 1;
            slotIndex = _freeSlots[freeStackIndex];
            _freeSlotCount = freeStackIndex;

            _registeredRigs[slotIndex] = rig;
            _slotActive[slotIndex] = true;
            ResetTargetSlot(slotIndex);
            rig.AssignEntitySlot(slotIndex, _frontTargetFrames);
            return true;
        }

        internal void UnregisterRig(ContextualPhysicalIkRig rig, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEntities)
                return;

            if (!ReferenceEquals(_registeredRigs[slotIndex], rig))
                return;

            _registeredRigs[slotIndex] = null;
            _slotActive[slotIndex] = false;
            ResetTargetSlot(slotIndex);
            _freeSlots[_freeSlotCount] = slotIndex;
            _freeSlotCount++;
        }

        private void InitializeFreeSlots()
        {
            _freeSlotCount = MaxEntities;
            for (int i = 0; i < MaxEntities; i++)
                _freeSlots[i] = i;
        }

        private void EnsurePersistentBuffers()
        {
            if (!_scheduledEntityStates.IsCreated)
            {
                _scheduledEntityStates = new NativeArray<ContextualPhysicalIkEntityState>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkEntityState>[128] - scheduled IK entity snapshots - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledEntityStates, NativeMemoryOwner, nameof(_scheduledEntityStates), NativeMemoryLifetime);
            }

            if (!_scheduledCommands.IsCreated)
            {
                _scheduledCommands = new NativeArray<RaycastCommand>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[768] - contextual IK ground/hand/tool probes - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledCommands, NativeMemoryOwner, nameof(_scheduledCommands), NativeMemoryLifetime);
            }

            if (!_scheduledHits.IsCreated)
            {
                _scheduledHits = new NativeArray<RaycastHit>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[768] - contextual IK raycast results - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledHits, NativeMemoryOwner, nameof(_scheduledHits), NativeMemoryLifetime);
            }

            if (!_frontTargetFrames.IsCreated)
            {
                _frontTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] - read-side IK target frames - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_frontTargetFrames, NativeMemoryOwner, nameof(_frontTargetFrames), NativeMemoryLifetime);
            }

            if (!_backTargetFrames.IsCreated)
            {
                _backTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] - write-side IK target frames - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_backTargetFrames, NativeMemoryOwner, nameof(_backTargetFrames), NativeMemoryLifetime);
            }

            if (!_ikTargets.IsCreated)
            {
                _ikTargets = new NativeArray<float3>(
                    MaxEntities * HandsPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[256] - SOA hand IK target positions - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_ikTargets, NativeMemoryOwner, nameof(_ikTargets), NativeMemoryLifetime);
            }

            if (!_ikWeights.IsCreated)
            {
                _ikWeights = new NativeArray<float>(
                    MaxEntities * HandsPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[256] - SOA hand IK weights - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_ikWeights, NativeMemoryOwner, nameof(_ikWeights), NativeMemoryLifetime);
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<ContextualPhysicalIkTelemetryEntry>(
                    TelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTelemetryEntry>[300] - PLAYER_TOOL_IK black-box ring - owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeMemoryLifetime);
            }
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            _disposeHandle = default;
            DisposeNativeArray(ref _scheduledEntityStates, dependency);
            DisposeNativeArray(ref _scheduledCommands, dependency);
            DisposeNativeArray(ref _scheduledHits, dependency);
            DisposeNativeArray(ref _frontTargetFrames, dependency);
            DisposeNativeArray(ref _backTargetFrames, dependency);
            DisposeNativeArray(ref _ikTargets, dependency);
            DisposeNativeArray(ref _ikWeights, dependency);
            DisposeNativeArray(ref _telemetryRing, dependency);
            JobHandle.ScheduleBatchedJobs();
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        private void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, array.Dispose(dependency));
            array = default;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            bool lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            if (!updateRegistered || !lateFrameRegistered)
            {
                if (updateRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                if (lateFrameRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                return;
            }

            _registeredUpdate = true;
            _registeredLateFrame = true;
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);

            _registered = false;
            _registeredUpdate = false;
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

        private void CompletePendingGroundResponseForOriginShift()
        {
            if (!_groundResponseScheduled)
                return;

            // COLD SYNC JOB: floating-origin rebasing must not race pending IK target writes.
            DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: true);
            SwapTargetBuffers();
            PublishFrontTargetBuffer();
            _groundResponseScheduled = false;
        }

        private void RebaseScheduledEntityStates(float3 shiftOffset)
        {
            if (!_scheduledEntityStates.IsCreated)
                return;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkEntityState state = _scheduledEntityStates[slotIndex];
                state.RootPosition -= shiftOffset;
                state.PelvisPosition -= shiftOffset;
                state.LeftFootProbeOrigin -= shiftOffset;
                state.RightFootProbeOrigin -= shiftOffset;
                state.LeftHandProbeOrigin -= shiftOffset;
                state.RightHandProbeOrigin -= shiftOffset;
                state.PredictiveLeftHandPosition -= shiftOffset;
                state.PredictiveRightHandPosition -= shiftOffset;
                state.CameraPosition -= shiftOffset;
                state.DashboardRightHandPosition -= shiftOffset;
                _scheduledEntityStates[slotIndex] = state;
            }
        }

        private void RebaseTargetFrames(NativeArray<ContextualPhysicalIkTargetFrame> targetFrames, float3 shiftOffset)
        {
            if (!targetFrames.IsCreated)
                return;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
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
            if (target.Blend <= 0.0001f &&
                math.lengthsq(target.WorldPosition) <= 0.000001f &&
                target.DeltaHeight == 0.0f)
            {
                return;
            }

            target.WorldPosition -= shiftOffset;
        }

        private void RebaseFloat3Lanes(NativeArray<float3> lanes, float3 shiftOffset)
        {
            if (!lanes.IsCreated)
                return;

            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                float3 value = lanes[laneIndex];
                if (math.lengthsq(value) <= 0.000001f)
                    continue;

                lanes[laneIndex] = value - shiftOffset;
            }
        }

        private bool CaptureEntityStates(
            float deltaTime,
            uint frameIndex,
            float3 viewerPosition,
            float3 viewerForward,
            float3 viewerUp,
            float3 viewerRight,
            bool hasViewerPosition)
        {
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
                        hasActiveEntity = true;
                    }
                }

                _scheduledEntityStates[slotIndex] = entityState;
            }

            return hasActiveEntity;
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
            Camera playerCamera = GlobalRegistry.Player != null ? GlobalRegistry.Player.PlayerCamera : null;
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
            ContextualPhysicalIkGroundDetectionJob groundDetectionJob = new ContextualPhysicalIkGroundDetectionJob
            {
                Entities = _scheduledEntityStates,
                Commands = _scheduledCommands,
            };

            JobHandle commandBuildHandle = groundDetectionJob.Schedule(MaxEntities, 32);
            JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
                _scheduledCommands,
                _scheduledHits,
                MinCommandsPerJob,
                commandBuildHandle);
            JobHandle groundDetectionHandle = JobHandle.CombineDependencies(commandBuildHandle, raycastHandle);

            ContextualPhysicalIkGroundResponseJob responseJob = new ContextualPhysicalIkGroundResponseJob
            {
                Entities = _scheduledEntityStates,
                Hits = _scheduledHits,
                PreviousTargets = _frontTargetFrames,
                NextTargets = _backTargetFrames,
                IkTargets = _ikTargets,
                IkWeights = _ikWeights,
            };

            JobHandle responseHandle = responseJob.Schedule(MaxEntities, 32, groundDetectionHandle);
            _pendingGroundResponseHandle = JobHandle.CombineDependencies(groundDetectionHandle, responseHandle);
            _groundResponseScheduled = true;
        }

        private void SwapTargetBuffers()
        {
            NativeArray<ContextualPhysicalIkTargetFrame> swapBuffer = _frontTargetFrames;
            _frontTargetFrames = _backTargetFrames;
            _backTargetFrames = swapBuffer;
        }

        private void PublishFrontTargetBuffer()
        {
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                if (rig == null)
                    continue;

                rig.OnTargetBufferSwapped(_frontTargetFrames);
            }
        }

        private void WriteTelemetrySample(uint reasonFlags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            uint stateHash = 2166136261u;
            ushort activeCount = 0;
            float3 firstRootPosition = float3.zero;
            float3 firstLeftTarget = float3.zero;
            float3 firstRightTarget = float3.zero;
            float2 firstWeights = float2.zero;
            bool capturedFirst = false;
            bool invalid = false;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                activeCount++;
                ContextualPhysicalIkEntityState entity = _scheduledEntityStates.IsCreated ? _scheduledEntityStates[slotIndex] : default;
                ContextualPhysicalIkTargetFrame frame = _frontTargetFrames.IsCreated ? _frontTargetFrames[slotIndex] : default;
                int baseIkIndex = slotIndex * HandsPerEntity;
                float2 weights = _ikWeights.IsCreated
                    ? new float2(
                        _ikWeights[baseIkIndex + LeftHandIndex],
                        _ikWeights[baseIkIndex + RightHandIndex])
                    : float2.zero;

                stateHash = MixHash(stateHash, (uint)slotIndex);
                stateHash = MixHash(stateHash, math.hash(entity.RootPosition));
                stateHash = MixHash(stateHash, math.hash(frame.LeftHand.WorldPosition));
                stateHash = MixHash(stateHash, math.hash(frame.RightHand.WorldPosition));
                stateHash = MixHash(stateHash, math.hash(weights));

                bool slotInvalid =
                    !math.all(math.isfinite(entity.RootPosition)) ||
                    !math.all(math.isfinite(frame.LeftHand.WorldPosition)) ||
                    !math.all(math.isfinite(frame.RightHand.WorldPosition)) ||
                    !math.all(math.isfinite(weights));
                invalid |= slotInvalid;

                if (capturedFirst)
                    continue;

                firstRootPosition = entity.RootPosition;
                firstLeftTarget = frame.LeftHand.WorldPosition;
                firstRightTarget = frame.RightHand.WorldPosition;
                firstWeights = weights;
                capturedFirst = true;
            }

            uint flags = reasonFlags | (invalid ? 0x80000000u : 0u);
            _telemetryRing[_telemetryCursor] = new ContextualPhysicalIkTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                StateHash = stateHash,
                ActiveEntities = activeCount,
                Reserved = 0,
                FirstRootPosition = firstRootPosition,
                FirstLeftHandTarget = firstLeftTarget,
                FirstRightHandTarget = firstRightTarget,
                FirstHandWeights = firstWeights
            };

            _telemetryCursor++;
            if (_telemetryCursor >= TelemetryCapacity)
                _telemetryCursor = 0;

            if (invalid && !_telemetryDumped)
                DumpTelemetry(flags);
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private void DumpTelemetry(uint reasonFlags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            _telemetryDumped = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TelemetryDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(reasonFlags);
                writer.Write((uint)TelemetryCapacity);
                for (int i = 0; i < TelemetryCapacity; i++)
                    WriteTelemetryEntry(writer, _telemetryRing[i]);
            }
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, in ContextualPhysicalIkTelemetryEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.Flags);
            writer.Write(entry.StateHash);
            writer.Write(entry.ActiveEntities);
            writer.Write(entry.Reserved);
            WriteFloat3(writer, entry.FirstRootPosition);
            WriteFloat3(writer, entry.FirstLeftHandTarget);
            WriteFloat3(writer, entry.FirstRightHandTarget);
            writer.Write(entry.FirstHandWeights.x);
            writer.Write(entry.FirstHandWeights.y);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private void ResetTargetSlot(int slotIndex)
        {
            if (_frontTargetFrames.IsCreated)
                _frontTargetFrames[slotIndex] = default;

            if (_backTargetFrames.IsCreated)
                _backTargetFrames[slotIndex] = default;

            int baseIkIndex = slotIndex * HandsPerEntity;
            if (_ikTargets.IsCreated)
            {
                _ikTargets[baseIkIndex + LeftHandIndex] = float3.zero;
                _ikTargets[baseIkIndex + RightHandIndex] = float3.zero;
            }

            if (_ikWeights.IsCreated)
            {
                _ikWeights[baseIkIndex + LeftHandIndex] = 0.0f;
                _ikWeights[baseIkIndex + RightHandIndex] = 0.0f;
            }
        }
    }
}
