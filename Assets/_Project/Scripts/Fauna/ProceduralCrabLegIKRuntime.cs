using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabLegEntityState
    {
        public int IsActive;
        public int LegStartIndex;
        public int LegCount;
        public int GroundLayerMask;
        public int RaycastBudgetMode;
        public int FrameIndex;
        public int Health;
        public int CorpseState;
        public int StateFlags;
        public int LeftStepCursor;
        public int RightStepCursor;
        public float DeltaTime;
        public float StrideLengthSq;
        public float StepDuration;
        public float StepHeight;
        public float RaycastHeight;
        public float RaycastDistance;
        public float ContactOffset;
        public float VelocityLeadSeconds;
        public float Scale;
        public float BodyHeight;
        public float UpperLegLength;
        public float LowerLegLength;
        public float SpatialHashAvoidanceStrength;
        public float SpatialHashAvoidanceMaxOffset;
        public float3 RootPosition;
        public float3 Velocity;
        public float3 SpatialHashAvoidanceOffset;
        public quaternion RootRotation;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabLegStepState
    {
        public float3 StepFrom;
        public float3 StepTo;
        public float StepTimer;
        public float StepDuration;
        public float StepHeight;
        public byte IsStepping;
        public byte Side;
        public byte IsGrounded;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabBodyPose
    {
        public float4x4 BodyMatrix;
        public float3 BodyNormal;
        public int IsActive;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabSolvedJointMatrices
    {
        public float4x4 UpperJointMatrix;
        public float4x4 LowerJointMatrix;
        public float4x4 FootJointMatrix;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabIkTelemetryEntry
    {
        public int FrameIndex;
        public int ActiveEntityCount;
        public int EntityIndex;
        public int Flags;
        public float3 RootPosition;
        public float3 FirstFootPosition;
        public float3 BodyNormal;
        public uint StateHash;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabGroundRaycastBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProceduralCrabLegEntityState> Entities;
        public NativeArray<RaycastCommand> Commands;
        public NativeArray<int> RaycastLegMask;

        public void Execute(int index)
        {
            int entityIndex = index / ProceduralCrabLegIKRuntime.MaxLegsPerEntity;
            int localLegIndex = index - (entityIndex * ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            ProceduralCrabLegEntityState entity = Entities[entityIndex];

            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0 ||
                (entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagCorpse) != 0 ||
                entity.Health <= 0 ||
                localLegIndex >= entity.LegCount ||
                !ShouldRaycastLeg(in entity, localLegIndex))
            {
                WriteDisabledCommand(index);
                return;
            }

            float safeScale = math.max(0.0001f, entity.Scale);
            float3 homeLocal = ProceduralCrabLegIKRuntime.ResolveLegHomeLocal(localLegIndex, entity.LegCount) * safeScale;
            float3 ledHome = entity.RootPosition + math.rotate(entity.RootRotation, homeLocal) + (entity.Velocity * math.max(0f, entity.VelocityLeadSeconds));
            float3 origin = new float3(ledHome.x, entity.RootPosition.y + math.max(0.01f, entity.RaycastHeight), ledHome.z);
            QueryParameters query = new QueryParameters(entity.GroundLayerMask, false, QueryTriggerInteraction.Ignore, false);

            Commands[index] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(origin),
                Vector3.down,
                query,
                math.max(0.01f, entity.RaycastDistance));
            RaycastLegMask[index] = 1;
        }

        private static bool ShouldRaycastLeg(in ProceduralCrabLegEntityState entity, int localLegIndex)
        {
            if (entity.RaycastBudgetMode != ProceduralCrabLegIKRuntime.RaycastBudgetLowTwoLegs)
                return true;

            int safeLegCount = math.clamp(entity.LegCount, ProceduralCrabLegIKRuntime.MinLegsPerEntity, ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            int pairStart = ((entity.FrameIndex * 2) % safeLegCount);
            int pairEnd = pairStart + 1;
            if (pairEnd >= safeLegCount)
                pairEnd = 0;

            return localLegIndex == pairStart || localLegIndex == pairEnd;
        }

        private void WriteDisabledCommand(int commandIndex)
        {
            Commands[commandIndex] = new RaycastCommand(
                Vector3.zero,
                Vector3.down,
                new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore, false),
                0.0f);
            RaycastLegMask[commandIndex] = 0;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabGroundTargetResolveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [ReadOnly] public NativeArray<RaycastHit> Hits;
        [ReadOnly] public NativeArray<int> RaycastLegMask;
        public NativeArray<float3> TargetFootPositions;
        public NativeArray<ProceduralCrabLegStepState> StepStates;

        public void Execute(int index)
        {
            int entityIndex = index / ProceduralCrabLegIKRuntime.MaxLegsPerEntity;
            int localLegIndex = index - (entityIndex * ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            ProceduralCrabLegEntityState entity = Entities[entityIndex];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0 ||
                localLegIndex >= entity.LegCount ||
                RaycastLegMask[index] == 0)
                return;

            RaycastHit hit = Hits[index];
            int legIndex = entity.LegStartIndex + localLegIndex;
            ProceduralCrabLegStepState state = StepStates[legIndex];
            if (!HasHit(in hit))
            {
                state.IsGrounded = 0;
                StepStates[legIndex] = state;
                return;
            }

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(hit.normal), new float3(0f, 1f, 0f));
            float3 rawAvoidance = entity.SpatialHashAvoidanceOffset * math.saturate(entity.SpatialHashAvoidanceStrength);
            float3 avoidance = ClampVectorLength(rawAvoidance, math.max(0f, entity.SpatialHashAvoidanceMaxOffset));
            TargetFootPositions[legIndex] = ContextualPhysicalIkMath.ToFloat3(hit.point) + (normal * math.max(0f, entity.ContactOffset)) + avoidance;

            state.IsGrounded = 1;
            StepStates[legIndex] = state;
        }

        private static bool HasHit(in RaycastHit hit)
        {
            return hit.distance > 0.0f || math.lengthsq(ContextualPhysicalIkMath.ToFloat3(hit.normal)) > 0.0001f;
        }

        private static float3 ClampVectorLength(float3 value, float maxLength)
        {
            float lengthSq = math.lengthsq(value);
            float maxLengthSq = maxLength * maxLength;
            if (lengthSq <= maxLengthSq || lengthSq <= 0.000001f)
                return value;

            return value * (maxLength * math.rsqrt(lengthSq));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabStepSchedulerJob : IJobParallelFor
    {
        public NativeArray<ProceduralCrabLegEntityState> Entities;
        public NativeArray<float3> FootPositions;
        public NativeArray<float3> TargetFootPositions;
        public NativeArray<ProceduralCrabLegStepState> StepStates;

        public void Execute(int entityIndex)
        {
            ProceduralCrabLegEntityState entity = Entities[entityIndex];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0)
                return;

            int legCount = math.clamp(entity.LegCount, ProceduralCrabLegIKRuntime.MinLegsPerEntity, ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            int leftLocked = 0;
            int rightLocked = 0;
            int legStart = entity.LegStartIndex;
            bool isCorpse = (entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagCorpse) != 0 || entity.Health <= 0;

            for (int localLegIndex = 0; localLegIndex < legCount; localLegIndex++)
            {
                ProceduralCrabLegStepState state = StepStates[legStart + localLegIndex];
                if (state.IsStepping == 0)
                    continue;

                if ((state.Side & 1) == 0)
                    leftLocked = 1;
                else
                    rightLocked = 1;
            }

            for (int localLegIndex = 0; localLegIndex < legCount; localLegIndex++)
            {
                int legIndex = legStart + localLegIndex;
                ProceduralCrabLegStepState state = StepStates[legIndex];
                float3 current = FootPositions[legIndex];
                float3 target = TargetFootPositions[legIndex];

                if (isCorpse)
                {
                    state.IsStepping = 0;
                    state.StepTimer = 0f;
                    target.y = entity.RootPosition.y;
                    FootPositions[legIndex] = target;
                    TargetFootPositions[legIndex] = target;
                    StepStates[legIndex] = state;
                    continue;
                }

                if (state.IsStepping != 0)
                {
                    AdvanceStep(ref state, ref current, target, entity.DeltaTime);
                    FootPositions[legIndex] = current;
                    StepStates[legIndex] = state;
                    continue;
                }
            }

            if (!isCorpse)
            {
                if (leftLocked == 0)
                    TryTriggerStepForSide(
                        0,
                        legCount,
                        legStart,
                        entity.StrideLengthSq,
                        entity.StepDuration,
                        entity.StepHeight,
                        entity.DeltaTime,
                        ref entity.LeftStepCursor);

                if (rightLocked == 0)
                    TryTriggerStepForSide(
                        1,
                        legCount,
                        legStart,
                        entity.StrideLengthSq,
                        entity.StepDuration,
                        entity.StepHeight,
                        entity.DeltaTime,
                        ref entity.RightStepCursor);
            }

            Entities[entityIndex] = entity;
        }

        private void TryTriggerStepForSide(
            int side,
            int legCount,
            int legStart,
            float strideLengthSq,
            float stepDuration,
            float stepHeight,
            float deltaTime,
            ref int cursor)
        {
            cursor = NormalizeCursorToSide(cursor, side, legCount);
            int sideLegCount = math.max(1, legCount >> 1);
            for (int attempt = 0; attempt < sideLegCount; attempt++)
            {
                int localLegIndex = (cursor + (attempt * 2)) % legCount;
                int legIndex = legStart + localLegIndex;
                ProceduralCrabLegStepState state = StepStates[legIndex];
                if (state.IsStepping != 0 || state.IsGrounded == 0)
                    continue;

                float3 current = FootPositions[legIndex];
                float3 target = TargetFootPositions[legIndex];
                float3 planarDelta = new float3(target.x - current.x, 0f, target.z - current.z);
                if (math.lengthsq(planarDelta) <= strideLengthSq)
                    continue;

                state.IsStepping = 1;
                state.StepTimer = 0f;
                state.StepDuration = math.max(0.01f, stepDuration);
                state.StepHeight = math.max(0f, stepHeight);
                state.StepFrom = current;
                state.StepTo = target;

                AdvanceStep(ref state, ref current, target, deltaTime);
                FootPositions[legIndex] = current;
                StepStates[legIndex] = state;
                cursor = NormalizeCursorToSide(localLegIndex + 2, side, legCount);
                return;
            }
        }

        private static int NormalizeCursorToSide(int cursor, int side, int legCount)
        {
            int safeLegCount = math.max(ProceduralCrabLegIKRuntime.MinLegsPerEntity, legCount);
            int safeCursor = cursor % safeLegCount;
            if (safeCursor < 0)
                safeCursor += safeLegCount;

            if ((safeCursor & 1) != side)
                safeCursor = (safeCursor + 1) % safeLegCount;

            return safeCursor;
        }

        private static void AdvanceStep(ref ProceduralCrabLegStepState state, ref float3 current, float3 target, float deltaTime)
        {
            state.StepTo = target;
            state.StepTimer = math.min(state.StepDuration, state.StepTimer + math.max(0f, deltaTime));
            float t = math.saturate(state.StepTimer * math.rcp(math.max(0.01f, state.StepDuration)));
            float3 horizontal = math.lerp(state.StepFrom, state.StepTo, t);
            float centeredT = (t * 2f) - 1f;
            float lift01 = math.saturate(1.0f - (centeredT * centeredT));
            current = new float3(horizontal.x, horizontal.y + (lift01 * state.StepHeight), horizontal.z);

            if (t >= 0.999f)
            {
                current = state.StepTo;
                state.StepFrom = state.StepTo;
                state.IsStepping = 0;
                state.StepTimer = 0f;
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabLegAupRebaseJob : IJobParallelFor
    {
        public NativeArray<float3> FootPositions;
        public NativeArray<float3> TargetFootPositions;
        public NativeArray<ProceduralCrabLegStepState> StepStates;
        public float3 ShiftOffset;

        public void Execute(int index)
        {
            FootPositions[index] -= ShiftOffset;
            TargetFootPositions[index] -= ShiftOffset;

            ProceduralCrabLegStepState state = StepStates[index];
            state.StepFrom -= ShiftOffset;
            state.StepTo -= ShiftOffset;
            StepStates[index] = state;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabEntityAupRebaseJob : IJobParallelFor
    {
        public NativeArray<ProceduralCrabLegEntityState> Entities;
        public NativeArray<ProceduralCrabBodyPose> BodyPoses;
        public float3 ShiftOffset;

        public void Execute(int index)
        {
            ProceduralCrabLegEntityState entity = Entities[index];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) != 0)
            {
                entity.RootPosition -= ShiftOffset;
                Entities[index] = entity;
            }

            ProceduralCrabBodyPose pose = BodyPoses[index];
            if (pose.IsActive == 0)
                return;

            float4 c3 = pose.BodyMatrix.c3;
            pose.BodyMatrix.c3 = new float4(c3.x - ShiftOffset.x, c3.y - ShiftOffset.y, c3.z - ShiftOffset.z, c3.w);
            BodyPoses[index] = pose;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabBodyTiltJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [ReadOnly] public NativeArray<float3> FootPositions;
        public NativeArray<ProceduralCrabBodyPose> BodyPoses;
        public float BodyVisualScale;

        public void Execute(int index)
        {
            ProceduralCrabLegEntityState entity = Entities[index];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0)
            {
                BodyPoses[index] = default;
                return;
            }

            int legStart = entity.LegStartIndex;
            int legCount = math.clamp(entity.LegCount, ProceduralCrabLegIKRuntime.MinLegsPerEntity, ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            float3 p1 = FootPositions[legStart];
            float3 p2 = FootPositions[legStart + math.min(2, legCount - 1)];
            float3 p3 = FootPositions[legStart + legCount - 1];
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(math.rotate(entity.RootRotation, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));
            float3 normal = ContextualPhysicalIkMath.SafeNormalize(math.cross(p1 - p2, p3 - p2), rootUp);
            normal = math.select(normal, -normal, math.dot(normal, rootUp) < 0f);

            float3 rootForward = ContextualPhysicalIkMath.SafeNormalize(math.rotate(entity.RootRotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
            float3 projectedForward = rootForward - (normal * math.dot(rootForward, normal));
            projectedForward = ContextualPhysicalIkMath.SafeNormalize(projectedForward, rootForward);
            quaternion bodyRotation = quaternion.LookRotationSafe(projectedForward, normal);
            float3 bodyPosition = entity.RootPosition + (normal * math.max(0f, entity.BodyHeight));
            float visualScale = math.max(0.0001f, entity.Scale * math.max(0.0001f, BodyVisualScale));

            BodyPoses[index] = new ProceduralCrabBodyPose
            {
                BodyMatrix = float4x4.TRS(bodyPosition, bodyRotation, new float3(visualScale, visualScale, visualScale)),
                BodyNormal = normal,
                IsActive = (entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagCorpse) != 0 || entity.Health <= 0 ? 2 : 1
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProceduralCrabAnalyticalTwoBoneIkJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [ReadOnly] public NativeArray<float3> FootPositions;
        [ReadOnly] public NativeArray<ProceduralCrabBodyPose> BodyPoses;
        public NativeArray<ProceduralCrabSolvedJointMatrices> SolvedJointMatrices;
        public float JointVisualScale;

        public void Execute(int index)
        {
            int entityIndex = index / ProceduralCrabLegIKRuntime.MaxLegsPerEntity;
            int localLegIndex = index - (entityIndex * ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            ProceduralCrabLegEntityState entity = Entities[entityIndex];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0 || localLegIndex >= entity.LegCount)
            {
                SolvedJointMatrices[index] = default;
                return;
            }

            float safeScale = math.max(0.0001f, entity.Scale);
            float3 hipLocal = ProceduralCrabLegIKRuntime.ResolveLegHipLocal(localLegIndex, entity.LegCount) * safeScale;
            float3 hipPosition = entity.RootPosition + math.rotate(entity.RootRotation, hipLocal);
            float3 footPosition = FootPositions[index];
            float sideSign = (localLegIndex & 1) == 0 ? -1f : 1f;
            float3 rootRight = ContextualPhysicalIkMath.SafeNormalize(math.rotate(entity.RootRotation, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
            float3 rootUp = BodyPoses[entityIndex].IsActive != 0
                ? BodyPoses[entityIndex].BodyNormal
                : ContextualPhysicalIkMath.SafeNormalize(math.rotate(entity.RootRotation, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));
            float3 polePosition = hipPosition + (rootRight * sideSign * math.max(0.05f, entity.UpperLegLength * safeScale)) + (rootUp * 0.05f);

            SolveAnalyticalTwoBone(
                hipPosition,
                footPosition,
                polePosition,
                math.max(0.01f, entity.UpperLegLength * safeScale),
                math.max(0.01f, entity.LowerLegLength * safeScale),
                rootUp,
                out float3 kneePosition,
                out float3 solvedFootPosition,
                out quaternion upperRotation,
                out quaternion lowerRotation,
                out quaternion footRotation);

            float jointScale = math.max(0.0001f, JointVisualScale * safeScale);
            SolvedJointMatrices[index] = new ProceduralCrabSolvedJointMatrices
            {
                UpperJointMatrix = float4x4.TRS(hipPosition, upperRotation, new float3(jointScale, jointScale, jointScale)),
                LowerJointMatrix = float4x4.TRS(kneePosition, lowerRotation, new float3(jointScale, jointScale, jointScale)),
                FootJointMatrix = float4x4.TRS(solvedFootPosition, footRotation, new float3(jointScale, jointScale, jointScale))
            };
        }

        private static void SolveAnalyticalTwoBone(
            float3 hipPosition,
            float3 footTarget,
            float3 polePosition,
            float upperLength,
            float lowerLength,
            float3 fallbackUp,
            out float3 kneePosition,
            out float3 solvedFootPosition,
            out quaternion upperRotation,
            out quaternion lowerRotation,
            out quaternion footRotation)
        {
            float3 hipToTarget = footTarget - hipPosition;
            float targetDistanceSq = math.max(0.000001f, math.lengthsq(hipToTarget));
            float targetDistance = targetDistanceSq * math.rsqrt(targetDistanceSq);
            float minReach = math.abs(upperLength - lowerLength) + 0.001f;
            float maxReach = math.max(minReach + 0.001f, upperLength + lowerLength - 0.001f);
            float clampedDistance = math.clamp(targetDistance, minReach, maxReach);
            float3 targetDirection = ContextualPhysicalIkMath.SafeNormalize(hipToTarget, new float3(0f, -1f, 0f));

            float3 poleOffset = polePosition - hipPosition;
            float3 projectedPole = poleOffset - (targetDirection * math.dot(poleOffset, targetDirection));
            float3 bendDirection = ContextualPhysicalIkMath.SafeNormalize(projectedPole, fallbackUp);

            float upperDenominator = math.max(0.0001f, 2f * upperLength * clampedDistance);
            float upperCos = ((upperLength * upperLength) + (clampedDistance * clampedDistance) - (lowerLength * lowerLength)) *
                math.rcp(upperDenominator);
            upperCos = math.clamp(upperCos, -1f, 1f);

            float upperSinSq = math.saturate(1f - (upperCos * upperCos));
            float upperSin = upperSinSq * math.rsqrt(math.max(upperSinSq, 0.000001f));
            float3 upperDirection = ContextualPhysicalIkMath.SafeNormalize((targetDirection * upperCos) + (bendDirection * upperSin), targetDirection);

            kneePosition = hipPosition + (upperDirection * upperLength);
            solvedFootPosition = hipPosition + (targetDirection * clampedDistance);
            float3 lowerDirection = ContextualPhysicalIkMath.SafeNormalize(solvedFootPosition - kneePosition, targetDirection);
            float3 footForward = ContextualPhysicalIkMath.SafeNormalize(math.cross(bendDirection, fallbackUp), targetDirection);
            footForward = ContextualPhysicalIkMath.SafeNormalize(footForward - (fallbackUp * math.dot(footForward, fallbackUp)), targetDirection);

            upperRotation = quaternion.LookRotationSafe(upperDirection, bendDirection);
            lowerRotation = quaternion.LookRotationSafe(lowerDirection, bendDirection);
            footRotation = quaternion.LookRotationSafe(footForward, fallbackUp);
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9915)]
    internal sealed class ProceduralCrabLegIKRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener
    {
        internal const int MinLegsPerEntity = 4;
        internal const int MaxLegsPerEntity = 6;
        internal const int RaycastBudgetLowTwoLegs = 0;
        internal const int RaycastBudgetHighAllLegs = 1;
        internal const int EntityFlagActive = 1 << 0;
        internal const int EntityFlagCorpse = 1 << 1;

        private const int DefaultMaxEntities = 128;
        private const int MinCommandsPerJob = 32;
        private const int TelemetryCapacity = 300;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ANIM_PROCEDURAL_BEHAVIOR.bin";
        private const string NativeMemoryOwner = nameof(ProceduralCrabLegIKRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int BodyPoseBufferId = Shader.PropertyToID("_H8CrabBodyPoseBuffer");
        private static readonly int LegJointBufferId = Shader.PropertyToID("_H8CrabLegJointBuffer");

        [Header("Crab IK Capacity")]
        [Tooltip("Maximum data-only crab entities owned by this runtime.")]
        [SerializeField] private int _maxEntities = DefaultMaxEntities;

        [Tooltip("Default leg count for newly registered entities. Clamped to 4 or 6.")]
        [SerializeField] private int _defaultLegCount = MaxLegsPerEntity;

        [Header("Grounding")]
        [Tooltip("Layer mask for asynchronous ground raycasts.")]
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        [Tooltip("Meters above the body root where ground probes start.")]
        [SerializeField] private float _raycastHeight = 1.2f;

        [Tooltip("Meters below the probe origin allowed for ground acquisition.")]
        [SerializeField] private float _raycastDistance = 3.0f;

        [Tooltip("Meters added along the hit normal to avoid foot z-fighting.")]
        [SerializeField] private float _contactOffset = 0.025f;

        [Tooltip("Seconds of root velocity used to lead foot home probes.")]
        [SerializeField] private float _velocityLeadSeconds = 0.08f;

        [Tooltip("Max meters spatial-hash avoidance may offset a foot target.")]
        [SerializeField] private float _maxAvoidanceFootOffset = 0.35f;

        [Header("Step Solver")]
        [Tooltip("Planar meters before a foot is allowed to step.")]
        [SerializeField] private float _strideLength = 0.55f;

        [Tooltip("Seconds for a complete foot step.")]
        [SerializeField] private float _stepDuration = 0.14f;

        [Tooltip("Meters of procedural arc lift at step midpoint.")]
        [SerializeField] private float _stepHeight = 0.16f;

        [Header("Analytical IK")]
        [Tooltip("Meters between body root and solved body mesh plane.")]
        [SerializeField] private float _bodyHeight = 0.22f;

        [Tooltip("Upper segment length before entity scale is applied.")]
        [SerializeField] private float _upperLegLength = 0.38f;

        [Tooltip("Lower segment length before entity scale is applied.")]
        [SerializeField] private float _lowerLegLength = 0.44f;

        [Tooltip("Uniform visual scale for GPU joint matrices.")]
        [SerializeField] private float _jointVisualScale = 0.035f;

        [Header("GPU Draw")]
        [Tooltip("Indirect crab body mesh. The shader reads body and leg buffers.")]
        [SerializeField] private Mesh _crabBodyMesh;

        [Tooltip("Indirect crab material. Material buffers are rebound by this runtime.")]
        [SerializeField] private Material _crabBodyMaterial;

        [Tooltip("World-space bounds for crab indirect rendering.")]
        [SerializeField] private Bounds _renderBounds = new Bounds(Vector3.zero, new Vector3(512f, 256f, 512f));

        [Tooltip("Shadow mode for the indirect crab body draw.")]
        [SerializeField] private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.On;

        [Tooltip("Enables Graphics.RenderMeshIndirect submission for data-only crabs.")]
        [SerializeField] private bool _renderIndirect = true;

        // COLD ALLOC: bool[maxEntities] - stable crab slot activity bitset - owner: ProceduralCrabLegIKRuntime
        private bool[] _slotActive;
        // COLD ALLOC: int[maxEntities] - free-slot stack for data-only crab registration - owner: ProceduralCrabLegIKRuntime
        private int[] _freeSlots;

        private NativeArray<ProceduralCrabLegEntityState> _entities;
        private NativeArray<float3> _footPositions;
        private NativeArray<float3> _targetFootPositions;
        private NativeArray<ProceduralCrabLegStepState> _stepStates;
        private NativeArray<RaycastCommand> _raycastCommands;
        private NativeArray<RaycastHit> _raycastHits;
        private NativeArray<int> _raycastLegMask;
        private NativeArray<ProceduralCrabBodyPose> _bodyPoses;
        private NativeArray<ProceduralCrabSolvedJointMatrices> _solvedJointMatrices;
        private NativeArray<ProceduralCrabIkTelemetryEntry> _telemetryRing;

        private GraphicsBuffer _bodyPoseGraphicsBuffer;
        private GraphicsBuffer _jointMatrixGraphicsBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private Mesh _argsUploadMesh;
        private int _argsUploadInstanceCount = -1;

        private JobHandle _pendingHandle;
        private JobHandle _disposeHandle;
        private bool _pipelineScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _telemetryDumped;
        private bool _pendingOriginShiftRebase;
        private int _freeSlotCount;
        private int _frameIndex;
        private int _lastActiveEntityCount;
        private int _telemetryCursor;
        private float3 _pendingOriginShiftOffset;

        internal NativeArray<float3> FootPositions => _footPositions;
        internal NativeArray<float3> TargetFootPositions => _targetFootPositions;

        private int EntityCapacity => _entities.IsCreated ? _entities.Length : math.clamp(_maxEntities, 1, 4096);

        private int LegCapacity => _footPositions.IsCreated ? _footPositions.Length : EntityCapacity * MaxLegsPerEntity;

        private void Awake()
        {
            EnsureManagedSlotBuffers();
            InitializeFreeSlots();
            EnsurePersistentBuffers();
        }

        private void OnEnable()
        {
            EnsureManagedSlotBuffers();
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
            JobHandle dependency = _pipelineScheduled ? _pendingHandle : default;
            DisposeBuffers(dependency);
            ReleaseGraphicsBuffers();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f || !_footPositions.IsCreated)
                return;

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (_pipelineScheduled)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _pendingHandle))
                {
                    QueueOriginShiftRebase(offset);
                    return;
                }

                _pipelineScheduled = false;
            }

            ApplyOriginShiftRebase(offset);
        }

        public void Tick(float deltaTime)
        {
            if (!_entities.IsCreated || _pipelineScheduled)
                return;

            int activeCount = CaptureFrameState(deltaTime);
            if (activeCount <= 0)
                return;

            ScheduleGroundAndStepPipeline();
        }

        public void LateFrameTick()
        {
            if (!_pipelineScheduled)
            {
                ApplyPendingOriginShiftRebase();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: false))
                return;

            _pipelineScheduled = false;
            if (ApplyPendingOriginShiftRebase())
                return;

            WriteTelemetryFrame();
            UploadAndRenderIndirect();
        }

        internal bool TryRegisterEntity(float3 rootPosition, quaternion rootRotation, int legCount, float scale, out int slotIndex)
        {
            slotIndex = -1;
            EnsureManagedSlotBuffers();
            EnsurePersistentBuffers();

            if (_freeSlotCount <= 0)
                return false;

            int freeStackIndex = _freeSlotCount - 1;
            slotIndex = _freeSlots[freeStackIndex];
            _freeSlotCount = freeStackIndex;
            _slotActive[slotIndex] = true;

            ProceduralCrabLegEntityState entity = BuildDefaultEntityState(slotIndex, rootPosition, rootRotation, legCount, scale);
            _entities[slotIndex] = entity;
            SeedLegTargets(in entity);
            return true;
        }

        internal void UnregisterEntity(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex])
                return;

            _slotActive[slotIndex] = false;
            ProceduralCrabLegEntityState entity = _entities[slotIndex];
            ClearLegRange(in entity);
            _entities[slotIndex] = default;
            if (_bodyPoses.IsCreated)
                _bodyPoses[slotIndex] = default;
            _freeSlots[_freeSlotCount] = slotIndex;
            _freeSlotCount++;
        }

        internal void SetEntityPose(int slotIndex, float3 rootPosition, quaternion rootRotation, float3 velocity, int health)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex])
                return;

            ProceduralCrabLegEntityState entity = _entities[slotIndex];
            entity.RootPosition = SanitizeFiniteFloat3(rootPosition, entity.RootPosition);
            entity.RootRotation = SanitizeFiniteQuaternion(rootRotation, entity.RootRotation);
            entity.Velocity = SanitizeFiniteFloat3(velocity, float3.zero);
            entity.Health = health;
            if (health <= 0)
            {
                entity.CorpseState = 1;
                entity.StateFlags |= EntityFlagCorpse;
            }
            _entities[slotIndex] = entity;
        }

        internal void SetSpatialHashAvoidance(int slotIndex, float3 separationOffset, float strength)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex])
                return;

            ProceduralCrabLegEntityState entity = _entities[slotIndex];
            entity.SpatialHashAvoidanceOffset = math.select(separationOffset, float3.zero, !math.all(math.isfinite(separationOffset)));
            entity.SpatialHashAvoidanceStrength = math.isfinite(strength) ? math.saturate(strength) : 0f;
            _entities[slotIndex] = entity;
        }

        private void EnsureManagedSlotBuffers()
        {
            int capacity = EntityCapacity;
            if (_slotActive != null && _slotActive.Length == capacity)
                return;

            _slotActive = new bool[capacity];
            _freeSlots = new int[capacity];
            InitializeFreeSlots();
        }

        private void InitializeFreeSlots()
        {
            if (_freeSlots == null)
                return;

            _freeSlotCount = _freeSlots.Length;
            for (int i = 0; i < _freeSlots.Length; i++)
            {
                _slotActive[i] = false;
                _freeSlots[i] = i;
            }
        }

        private void EnsurePersistentBuffers()
        {
            int entityCapacity = EntityCapacity;
            int legCapacity = LegCapacity;
            if (!_entities.IsCreated)
            {
                _entities = new NativeArray<ProceduralCrabLegEntityState>(
                    entityCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralCrabLegEntityState> - S.O.A. crab entity state - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_entities, NativeMemoryOwner, nameof(_entities), NativeMemoryLifetime);
            }

            if (!_footPositions.IsCreated)
            {
                _footPositions = new NativeArray<float3>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3> - solved foot positions S.O.A. - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_footPositions, NativeMemoryOwner, nameof(_footPositions), NativeMemoryLifetime);
            }

            if (!_targetFootPositions.IsCreated)
            {
                _targetFootPositions = new NativeArray<float3>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3> - target foot positions S.O.A. - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_targetFootPositions, NativeMemoryOwner, nameof(_targetFootPositions), NativeMemoryLifetime);
            }

            if (!_stepStates.IsCreated)
            {
                _stepStates = new NativeArray<ProceduralCrabLegStepState>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralCrabLegStepState> - per-leg scheduler state - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_stepStates, NativeMemoryOwner, nameof(_stepStates), NativeMemoryLifetime);
            }

            if (!_raycastCommands.IsCreated)
            {
                _raycastCommands = new NativeArray<RaycastCommand>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand> - async crab ground probes - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_raycastCommands, NativeMemoryOwner, nameof(_raycastCommands), NativeMemoryLifetime);
            }

            if (!_raycastHits.IsCreated)
            {
                _raycastHits = new NativeArray<RaycastHit>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit> - async crab ground probe results - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_raycastHits, NativeMemoryOwner, nameof(_raycastHits), NativeMemoryLifetime);
            }

            if (!_raycastLegMask.IsCreated)
            {
                _raycastLegMask = new NativeArray<int>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int> - low-tier raycast budget mask - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_raycastLegMask, NativeMemoryOwner, nameof(_raycastLegMask), NativeMemoryLifetime);
            }

            if (!_bodyPoses.IsCreated)
            {
                _bodyPoses = new NativeArray<ProceduralCrabBodyPose>(
                    entityCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralCrabBodyPose> - GPU body pose upload source - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_bodyPoses, NativeMemoryOwner, nameof(_bodyPoses), NativeMemoryLifetime);
            }

            if (!_solvedJointMatrices.IsCreated)
            {
                _solvedJointMatrices = new NativeArray<ProceduralCrabSolvedJointMatrices>(
                    legCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralCrabSolvedJointMatrices> - GPU leg joint matrix upload source - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_solvedJointMatrices, NativeMemoryOwner, nameof(_solvedJointMatrices), NativeMemoryLifetime);
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<ProceduralCrabIkTelemetryEntry>(
                    TelemetryCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralCrabIkTelemetryEntry>[300] - black-box crab IK telemetry ring - owner: ProceduralCrabLegIKRuntime
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeMemoryLifetime);
            }
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            DisposeNativeArray(ref _entities, dependency);
            DisposeNativeArray(ref _footPositions, dependency);
            DisposeNativeArray(ref _targetFootPositions, dependency);
            DisposeNativeArray(ref _stepStates, dependency);
            DisposeNativeArray(ref _raycastCommands, dependency);
            DisposeNativeArray(ref _raycastHits, dependency);
            DisposeNativeArray(ref _raycastLegMask, dependency);
            DisposeNativeArray(ref _bodyPoses, dependency);
            DisposeNativeArray(ref _solvedJointMatrices, dependency);
            DisposeNativeArray(ref _telemetryRing, dependency);
            _disposeHandle = default;
            _pendingHandle = default;
            _pipelineScheduled = false;
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
            if (_registeredUpdate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredUpdate && _registeredLateFrame)
                return;

            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLateFrame = false;
        }

        private void TryUnregister()
        {
            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

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

        private int CaptureFrameState(float deltaTime)
        {
            int activeCount = 0;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            int raycastBudgetMode = tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350
                ? RaycastBudgetLowTwoLegs
                : RaycastBudgetHighAllLegs;

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            int frameIndex = _frameIndex++;
            for (int slotIndex = 0; slotIndex < EntityCapacity; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ProceduralCrabLegEntityState entity = _entities[slotIndex];
                entity.DeltaTime = safeDeltaTime;
                entity.FrameIndex = frameIndex;
                entity.RaycastBudgetMode = raycastBudgetMode;
                _entities[slotIndex] = entity;
                activeCount++;
            }

            _lastActiveEntityCount = activeCount;
            return activeCount;
        }

        private void ScheduleGroundAndStepPipeline()
        {
            ProceduralCrabGroundRaycastBuildJob buildJob = new ProceduralCrabGroundRaycastBuildJob
            {
                Entities = _entities,
                Commands = _raycastCommands,
                RaycastLegMask = _raycastLegMask
            };

            JobHandle buildHandle = buildJob.Schedule(LegCapacity, MinCommandsPerJob);
            JobHandle raycastHandle = RaycastCommand.ScheduleBatch(_raycastCommands, _raycastHits, MinCommandsPerJob, buildHandle);

            ProceduralCrabGroundTargetResolveJob targetJob = new ProceduralCrabGroundTargetResolveJob
            {
                Entities = _entities,
                Hits = _raycastHits,
                RaycastLegMask = _raycastLegMask,
                TargetFootPositions = _targetFootPositions,
                StepStates = _stepStates
            };
            JobHandle targetHandle = targetJob.Schedule(LegCapacity, MinCommandsPerJob, raycastHandle);

            ProceduralCrabStepSchedulerJob stepJob = new ProceduralCrabStepSchedulerJob
            {
                Entities = _entities,
                FootPositions = _footPositions,
                TargetFootPositions = _targetFootPositions,
                StepStates = _stepStates
            };

            JobHandle stepHandle = stepJob.Schedule(EntityCapacity, 16, targetHandle);

            ProceduralCrabBodyTiltJob bodyTiltJob = new ProceduralCrabBodyTiltJob
            {
                Entities = _entities,
                FootPositions = _footPositions,
                BodyPoses = _bodyPoses,
                BodyVisualScale = 1f
            };
            JobHandle bodyHandle = bodyTiltJob.Schedule(EntityCapacity, 16, stepHandle);

            ProceduralCrabAnalyticalTwoBoneIkJob ikJob = new ProceduralCrabAnalyticalTwoBoneIkJob
            {
                Entities = _entities,
                FootPositions = _footPositions,
                BodyPoses = _bodyPoses,
                SolvedJointMatrices = _solvedJointMatrices,
                JointVisualScale = math.max(0.0001f, _jointVisualScale)
            };

            _pendingHandle = ikJob.Schedule(LegCapacity, MinCommandsPerJob, bodyHandle);
            _pipelineScheduled = true;
        }

        private void UploadAndRenderIndirect()
        {
            if (!_renderIndirect || _crabBodyMesh == null || _crabBodyMaterial == null)
                return;

            if (!_bodyPoses.IsCreated || !_solvedJointMatrices.IsCreated)
                return;

            EnsureGraphicsBuffers();
            if (_bodyPoseGraphicsBuffer == null || _jointMatrixGraphicsBuffer == null || _indirectArgsBuffer == null)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(_bodyPoseGraphicsBuffer, _bodyPoses, EntityCapacity);
            GraphicsBufferUploadUtility.UploadNativeArray(_jointMatrixGraphicsBuffer, _solvedJointMatrices, LegCapacity);
            _crabBodyMaterial.SetBuffer(BodyPoseBufferId, _bodyPoseGraphicsBuffer);
            _crabBodyMaterial.SetBuffer(LegJointBufferId, _jointMatrixGraphicsBuffer);
            UploadIndirectArgs(EntityCapacity);

            RenderParams renderParams = new RenderParams(_crabBodyMaterial)
            {
                worldBounds = _renderBounds,
                layer = gameObject.layer,
                shadowCastingMode = _shadowCastingMode,
                receiveShadows = true,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshIndirect(renderParams, _crabBodyMesh, _indirectArgsBuffer, 1, 0);
        }

        private void EnsureGraphicsBuffers()
        {
            if (_bodyPoseGraphicsBuffer == null)
                _bodyPoseGraphicsBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabBodyPose>(EntityCapacity); // COLD ALLOC: GraphicsBuffer[body poses] - indirect crab body S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_jointMatrixGraphicsBuffer == null)
                _jointMatrixGraphicsBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabSolvedJointMatrices>(LegCapacity); // COLD ALLOC: GraphicsBuffer[joint matrices] - indirect crab leg S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_indirectArgsBuffer == null)
            {
                _indirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - indirect crab draw args - owner: ProceduralCrabLegIKRuntime
            }
        }

        private void UploadIndirectArgs(int instanceCount)
        {
            if (_indirectArgsBuffer == null || _crabBodyMesh == null || instanceCount <= 0)
                return;

            if (_argsUploadMesh == _crabBodyMesh && _argsUploadInstanceCount == instanceCount)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _indirectArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = _crabBodyMesh.GetIndexCount(0),
                instanceCount = (uint)instanceCount,
                startIndex = _crabBodyMesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, _crabBodyMesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _indirectArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _argsUploadMesh = _crabBodyMesh;
            _argsUploadInstanceCount = instanceCount;
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _bodyPoseGraphicsBuffer);
            ReleaseGraphicsBuffer(ref _jointMatrixGraphicsBuffer);
            ReleaseGraphicsBuffer(ref _indirectArgsBuffer);
            _argsUploadMesh = null;
            _argsUploadInstanceCount = -1;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void WriteTelemetryFrame()
        {
            if (!_telemetryRing.IsCreated || !_entities.IsCreated || !_footPositions.IsCreated || !_bodyPoses.IsCreated)
                return;

            int firstActiveSlot = -1;
            for (int slotIndex = 0; slotIndex < EntityCapacity; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                firstActiveSlot = slotIndex;
                break;
            }

            ProceduralCrabIkTelemetryEntry entry = default;
            entry.FrameIndex = _frameIndex;
            entry.ActiveEntityCount = _lastActiveEntityCount;
            entry.EntityIndex = firstActiveSlot;

            if (firstActiveSlot >= 0)
            {
                ProceduralCrabLegEntityState entity = _entities[firstActiveSlot];
                float3 firstFoot = _footPositions[entity.LegStartIndex];
                ProceduralCrabBodyPose bodyPose = _bodyPoses[firstActiveSlot];
                bool invalidTelemetry = !math.all(math.isfinite(entity.RootPosition)) ||
                    !math.all(math.isfinite(firstFoot)) ||
                    !math.all(math.isfinite(bodyPose.BodyNormal));

                entry.RootPosition = SanitizeFiniteFloat3(entity.RootPosition, float3.zero);
                entry.FirstFootPosition = SanitizeFiniteFloat3(firstFoot, entry.RootPosition);
                entry.BodyNormal = SanitizeFiniteFloat3(bodyPose.BodyNormal, new float3(0f, 1f, 0f));
                entry.Flags = entity.Health <= 0 ? 0x02 : 0x01;
                entry.StateHash = ComputeTelemetryHash(entry.RootPosition, entry.FirstFootPosition, entry.BodyNormal, entity.Health);

                if (invalidTelemetry && !_telemetryDumped)
                {
                    entry.Flags |= 0x8000;
                    _telemetryRing[_telemetryCursor % TelemetryCapacity] = entry;
                    DumpTelemetryBlackBoxOnce();
                    _telemetryCursor++;
                    return;
                }
            }

            _telemetryRing[_telemetryCursor % TelemetryCapacity] = entry;
            _telemetryCursor++;
        }

        private static uint ComputeTelemetryHash(float3 rootPosition, float3 firstFoot, float3 bodyNormal, int health)
        {
            uint rootHash = math.hash(rootPosition);
            uint footHash = math.hash(firstFoot);
            uint normalHash = math.hash(bodyNormal);
            return rootHash ^ (footHash * 16777619u) ^ (normalHash * 2166136261u) ^ (uint)health;
        }

        private void DumpTelemetryBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, TelemetryDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(TelemetryCapacity);
            writer.Write(_telemetryCursor);
            for (int i = 0; i < TelemetryCapacity; i++)
            {
                ProceduralCrabIkTelemetryEntry entry = _telemetryRing[i];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.ActiveEntityCount);
                writer.Write(entry.EntityIndex);
                writer.Write(entry.Flags);
                writer.Write(entry.RootPosition.x);
                writer.Write(entry.RootPosition.y);
                writer.Write(entry.RootPosition.z);
                writer.Write(entry.FirstFootPosition.x);
                writer.Write(entry.FirstFootPosition.y);
                writer.Write(entry.FirstFootPosition.z);
                writer.Write(entry.BodyNormal.x);
                writer.Write(entry.BodyNormal.y);
                writer.Write(entry.BodyNormal.z);
                writer.Write(entry.StateHash);
            }
        }

        private ProceduralCrabLegEntityState BuildDefaultEntityState(
            int slotIndex,
            float3 rootPosition,
            quaternion rootRotation,
            int legCount,
            float scale)
        {
            float safeStride = ClampFiniteMin(_strideLength, 0.01f, 0.55f);
            float safeScale = ClampFiniteMin(scale, 0.01f, 1f);
            float3 safeRootPosition = SanitizeFiniteFloat3(rootPosition, float3.zero);
            quaternion safeRootRotation = SanitizeFiniteQuaternion(rootRotation, quaternion.identity);
            int requestedLegCount = legCount > 0 ? legCount : _defaultLegCount;
            int safeLegCount = requestedLegCount <= MinLegsPerEntity ? MinLegsPerEntity : MaxLegsPerEntity;
            return new ProceduralCrabLegEntityState
            {
                IsActive = 1,
                StateFlags = EntityFlagActive,
                LegStartIndex = slotIndex * MaxLegsPerEntity,
                LegCount = safeLegCount,
                GroundLayerMask = _groundLayerMask.value,
                RaycastBudgetMode = RaycastBudgetHighAllLegs,
                Health = 1,
                LeftStepCursor = 0,
                RightStepCursor = 1,
                StrideLengthSq = safeStride * safeStride,
                StepDuration = ClampFiniteMin(_stepDuration, 0.01f, 0.14f),
                StepHeight = ClampFiniteMin(_stepHeight, 0f, 0.16f),
                RaycastHeight = ClampFiniteMin(_raycastHeight, 0.01f, 1.2f),
                RaycastDistance = ClampFiniteMin(_raycastDistance, 0.01f, 3.0f),
                ContactOffset = ClampFiniteMin(_contactOffset, 0f, 0.025f),
                VelocityLeadSeconds = ClampFiniteMin(_velocityLeadSeconds, 0f, 0.08f),
                Scale = safeScale,
                BodyHeight = ClampFiniteMin(_bodyHeight, 0f, 0.22f),
                UpperLegLength = ClampFiniteMin(_upperLegLength, 0.01f, 0.38f),
                LowerLegLength = ClampFiniteMin(_lowerLegLength, 0.01f, 0.44f),
                SpatialHashAvoidanceMaxOffset = ClampFiniteMin(_maxAvoidanceFootOffset, 0f, 0.18f),
                RootPosition = safeRootPosition,
                RootRotation = safeRootRotation
            };
        }

        private void QueueOriginShiftRebase(float3 offset)
        {
            _pendingOriginShiftOffset += offset;
            _pendingOriginShiftRebase = true;
        }

        private bool ApplyPendingOriginShiftRebase()
        {
            if (!_pendingOriginShiftRebase)
                return false;

            float3 offset = _pendingOriginShiftOffset;
            _pendingOriginShiftOffset = float3.zero;
            _pendingOriginShiftRebase = false;
            ApplyOriginShiftRebase(offset);
            return true;
        }

        private void ApplyOriginShiftRebase(float3 offset)
        {
            if (!_entities.IsCreated || !_footPositions.IsCreated || !_targetFootPositions.IsCreated || !_stepStates.IsCreated)
                return;

            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            for (int slotIndex = 0; slotIndex < EntityCapacity; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ProceduralCrabLegEntityState entity = _entities[slotIndex];
                entity.RootPosition -= offset;
                _entities[slotIndex] = entity;

                for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
                {
                    int legIndex = entity.LegStartIndex + localLegIndex;
                    _footPositions[legIndex] -= offset;
                    _targetFootPositions[legIndex] -= offset;

                    ProceduralCrabLegStepState state = _stepStates[legIndex];
                    state.StepFrom -= offset;
                    state.StepTo -= offset;
                    _stepStates[legIndex] = state;
                }

                if (!_bodyPoses.IsCreated)
                    continue;

                ProceduralCrabBodyPose pose = _bodyPoses[slotIndex];
                if (pose.IsActive == 0)
                    continue;

                float4 c3 = pose.BodyMatrix.c3;
                pose.BodyMatrix.c3 = new float4(c3.x - offset.x, c3.y - offset.y, c3.z - offset.z, c3.w);
                _bodyPoses[slotIndex] = pose;
            }
        }

        private void DumpTelemetryBlackBoxOnce()
        {
            if (_telemetryDumped)
                return;

            DumpTelemetryBlackBox();
            _telemetryDumped = true;
        }

        private static float ClampFiniteMin(float value, float minValue, float fallback)
        {
            return math.isfinite(value) ? math.max(minValue, value) : fallback;
        }

        private static float3 SanitizeFiniteFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static quaternion SanitizeFiniteQuaternion(quaternion value, quaternion fallback)
        {
            if (!math.all(math.isfinite(value.value)))
                return fallback;

            float lengthSq = math.lengthsq(value.value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return new quaternion(value.value * math.rsqrt(lengthSq));
        }

        private void SeedLegTargets(in ProceduralCrabLegEntityState entity)
        {
            for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
            {
                int legIndex = entity.LegStartIndex + localLegIndex;
                if (localLegIndex >= entity.LegCount)
                {
                    _footPositions[legIndex] = default;
                    _targetFootPositions[legIndex] = default;
                    _stepStates[legIndex] = default;
                    continue;
                }

                float3 homeLocal = ResolveLegHomeLocal(localLegIndex, entity.LegCount) * entity.Scale;
                float3 world = entity.RootPosition + math.rotate(entity.RootRotation, homeLocal);
                _footPositions[legIndex] = world;
                _targetFootPositions[legIndex] = world;
                _stepStates[legIndex] = new ProceduralCrabLegStepState
                {
                    StepFrom = world,
                    StepTo = world,
                    StepDuration = entity.StepDuration,
                    StepHeight = entity.StepHeight,
                    Side = (byte)(localLegIndex & 1),
                    IsGrounded = 0
                };
            }
        }

        private void ClearLegRange(in ProceduralCrabLegEntityState entity)
        {
            for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
            {
                int legIndex = entity.LegStartIndex + localLegIndex;
                _footPositions[legIndex] = default;
                _targetFootPositions[legIndex] = default;
                _stepStates[legIndex] = default;
                if (_solvedJointMatrices.IsCreated)
                    _solvedJointMatrices[legIndex] = default;
            }
        }

        private bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && _slotActive != null && slotIndex < _slotActive.Length;
        }

        internal static float3 ResolveLegHomeLocal(int localLegIndex, int legCount)
        {
            int pairCount = math.max(1, legCount >> 1);
            int pairIndex = math.min(pairCount - 1, localLegIndex >> 1);
            float pairT = pairCount <= 1 ? 0.5f : pairIndex * math.rcp(pairCount - 1);
            float side = (localLegIndex & 1) == 0 ? -1f : 1f;
            return new float3(side * 0.42f, 0f, math.lerp(0.52f, -0.52f, pairT));
        }

        internal static float3 ResolveLegHipLocal(int localLegIndex, int legCount)
        {
            int pairCount = math.max(1, legCount >> 1);
            int pairIndex = math.min(pairCount - 1, localLegIndex >> 1);
            float pairT = pairCount <= 1 ? 0.5f : pairIndex * math.rcp(pairCount - 1);
            float side = (localLegIndex & 1) == 0 ? -1f : 1f;
            return new float3(side * 0.22f, 0f, math.lerp(0.38f, -0.38f, pairT));
        }
    }
}
