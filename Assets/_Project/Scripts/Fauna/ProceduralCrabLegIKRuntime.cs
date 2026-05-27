using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct ProceduralCrabLegEntityState
    {
        [FieldOffset(0)] public int IsActive;
        [FieldOffset(4)] public int LegStartIndex;
        [FieldOffset(8)] public int LegCount;
        [FieldOffset(16)] public int SurfaceProbeBudgetMode;
        [FieldOffset(20)] public int FrameIndex;
        [FieldOffset(24)] public int Health;
        [FieldOffset(28)] public int CorpseState;
        [FieldOffset(32)] public int StateFlags;
        [FieldOffset(36)] public int LeftStepCursor;
        [FieldOffset(40)] public int RightStepCursor;
        [FieldOffset(44)] public float DeltaTime;
        [FieldOffset(48)] public float StrideLengthSq;
        [FieldOffset(52)] public float StepDuration;
        [FieldOffset(56)] public float StepHeight;
        [FieldOffset(60)] public float GroundProbeHeight;
        [FieldOffset(64)] public float GroundProbeDistance;
        [FieldOffset(68)] public float ContactOffset;
        [FieldOffset(72)] public float VelocityLeadSeconds;
        [FieldOffset(76)] public float Scale;
        [FieldOffset(80)] public float BodyHeight;
        [FieldOffset(84)] public float UpperLegLength;
        [FieldOffset(88)] public float LowerLegLength;
        [FieldOffset(92)] public float SpatialHashAvoidanceStrength;
        [FieldOffset(96)] public float SpatialHashAvoidanceMaxOffset;
        [FieldOffset(100)] public float3 RootPosition;
        [FieldOffset(112)] public float3 Velocity;
        [FieldOffset(124)] public float3 SpatialHashAvoidanceOffset;
        [FieldOffset(136)] public quaternion RootRotation;
        [FieldOffset(152)] private ulong _pad0;
        [FieldOffset(160)] private ulong _pad1;
        [FieldOffset(168)] private ulong _pad2;
        [FieldOffset(176)] private ulong _pad3;
        [FieldOffset(184)] private ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ProceduralCrabLegStepState
    {
        [FieldOffset(0)] public float3 StepFrom;
        [FieldOffset(12)] public float3 StepTo;
        [FieldOffset(24)] public float StepTimer;
        [FieldOffset(28)] public float StepDuration;
        [FieldOffset(32)] public float StepHeight;
        [FieldOffset(36)] public byte IsStepping;
        [FieldOffset(37)] public byte Side;
        [FieldOffset(38)] public byte IsGrounded;
        [FieldOffset(39)] public byte Reserved;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct ProceduralCrabBodyPose
    {
        [FieldOffset(0)] public float4x4 BodyMatrix;
        [FieldOffset(64)] public float3 BodyNormal;
        [FieldOffset(76)] public int IsActive;
        [FieldOffset(80)] private ulong _pad0;
        [FieldOffset(88)] private ulong _pad1;
        [FieldOffset(96)] private ulong _pad2;
        [FieldOffset(104)] private ulong _pad3;
        [FieldOffset(112)] private ulong _pad4;
        [FieldOffset(120)] private ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct ProceduralCrabSolvedJointMatrices
    {
        [FieldOffset(0)] public float4x4 UpperJointMatrix;
        [FieldOffset(64)] public float4x4 LowerJointMatrix;
        [FieldOffset(128)] public float4x4 FootJointMatrix;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ProceduralCrabIkTelemetryEntry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public int ActiveEntityCount;
        [FieldOffset(8)] public int EntityIndex;
        [FieldOffset(12)] public int Flags;
        [FieldOffset(16)] public float3 RootPosition;
        [FieldOffset(28)] public float3 FirstFootPosition;
        [FieldOffset(40)] public float3 BodyNormal;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] private ulong _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabGroundTargetResolveJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [NoAlias] public NativeArray<float3> TargetFootPositions;
        [NoAlias] public NativeArray<ProceduralCrabLegStepState> StepStates;

        public void Execute(int index)
        {
            int entityIndex = index / ProceduralCrabLegIKRuntime.MaxLegsPerEntity;
            int localLegIndex = index - (entityIndex * ProceduralCrabLegIKRuntime.MaxLegsPerEntity);
            ProceduralCrabLegEntityState entity = Entities[entityIndex];
            if ((entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagActive) == 0 ||
                (entity.StateFlags & ProceduralCrabLegIKRuntime.EntityFlagCorpse) != 0 ||
                entity.Health <= 0 ||
                localLegIndex >= entity.LegCount)
                return;

            int legIndex = entity.LegStartIndex + localLegIndex;
            float safeScale = math.max(0.0001f, entity.Scale);
            float3 homeLocal = ProceduralCrabLegIKRuntime.ResolveLegHomeLocal(localLegIndex, entity.LegCount) * safeScale;
            float3 ledHome = entity.RootPosition + math.rotate(entity.RootRotation, homeLocal) + (entity.Velocity * math.max(0f, entity.VelocityLeadSeconds));
            float3 rootUp = ContextualPhysicalIkMath.SafeNormalize(math.rotate(entity.RootRotation, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));
            float3 rawAvoidance = entity.SpatialHashAvoidanceOffset * math.saturate(entity.SpatialHashAvoidanceStrength);
            float3 avoidance = ClampVectorLength(rawAvoidance, math.max(0f, entity.SpatialHashAvoidanceMaxOffset));
            float maxVerticalDelta = math.max(0.01f, entity.GroundProbeHeight + entity.GroundProbeDistance);
            float3 previousTarget = TargetFootPositions[legIndex];
            float3 target = ledHome + (rootUp * math.max(0f, entity.ContactOffset)) + avoidance;
            if (math.all(math.isfinite(previousTarget)))
                target.y = math.clamp(target.y, previousTarget.y - maxVerticalDelta, previousTarget.y + maxVerticalDelta);

            ProceduralCrabLegStepState state = StepStates[legIndex];
            TargetFootPositions[legIndex] = target;
            state.IsGrounded = 1;
            StepStates[legIndex] = state;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabStepSchedulerJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [NoAlias] public NativeArray<float3> FootPositions;
        [NoAlias] public NativeArray<float3> TargetFootPositions;
        [NoAlias] public NativeArray<ProceduralCrabLegStepState> StepStates;

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
                float verticalDelta = math.abs(target.y - current.y);
                float verticalStepThreshold = math.max(0.025f, stepHeight * 0.5f);
                if (math.lengthsq(planarDelta) <= strideLengthSq && verticalDelta <= verticalStepThreshold)
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabLegAupRebaseJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> FootPositions;
        [NoAlias] public NativeArray<float3> TargetFootPositions;
        [NoAlias] public NativeArray<ProceduralCrabLegStepState> StepStates;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabEntityAupRebaseJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [NoAlias] public NativeArray<ProceduralCrabBodyPose> BodyPoses;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabBodyTiltJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [ReadOnly, NoAlias] public NativeArray<float3> FootPositions;
        [NoAlias] public NativeArray<ProceduralCrabBodyPose> BodyPoses;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralCrabAnalyticalTwoBoneIkJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ProceduralCrabLegEntityState> Entities;
        [ReadOnly, NoAlias] public NativeArray<float3> FootPositions;
        [ReadOnly, NoAlias] public NativeArray<ProceduralCrabBodyPose> BodyPoses;
        [NoAlias] public NativeArray<ProceduralCrabSolvedJointMatrices> SolvedJointMatrices;
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
    internal sealed class ProceduralCrabLegIKRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        internal const int MinLegsPerEntity = 4;
        internal const int MaxLegsPerEntity = 6;
        internal const int SurfaceProbeBudgetAllLegs = 1;
        internal const int EntityFlagActive = 1 << 0;
        internal const int EntityFlagCorpse = 1 << 1;

        private const int DefaultMaxEntities = 128;
        private const int MinLegsPerJob = 32;
        private const int TelemetryCapacity = 300;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_13AI.bin";
        private static readonly int BodyPoseBufferId = Shader.PropertyToID("_H8CrabBodyPoseBuffer");
        private static readonly int LegJointBufferId = Shader.PropertyToID("_H8CrabLegJointBuffer");

        [Header("Crab IK Capacity")]
        [Tooltip("Maximum data-only crab entities owned by this runtime.")]
        [SerializeField] private int _maxEntities = DefaultMaxEntities;

        [Tooltip("Default leg count for newly registered entities. Clamped to 4 or 6.")]
        [SerializeField] private int _defaultLegCount = MaxLegsPerEntity;

        [Header("Grounding")]
        [Tooltip("Meters above the body root allowed for analytic surface probe vertical relaxation.")]
        [SerializeField] private float _groundProbeHeight = 1.2f;

        [Tooltip("Meters below the probe origin allowed for analytic surface target relaxation.")]
        [SerializeField] private float _groundProbeDistance = 3.0f;

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

        private IDataVault _dataVault;
        private VaultGenerationHandle<ProceduralCrabLegEntityState> _entitiesHandle;
        private VaultGenerationHandle<float3> _footPositionsHandle;
        private VaultGenerationHandle<float3> _targetFootPositionsHandle;
        private VaultGenerationHandle<ProceduralCrabLegStepState> _stepStatesHandle;
        private VaultGenerationHandle<ProceduralCrabBodyPose> _bodyPosesHandle;
        private VaultGenerationHandle<ProceduralCrabSolvedJointMatrices> _solvedJointMatricesHandle;
        private VaultGenerationHandle<ProceduralCrabIkTelemetryEntry> _telemetryRingHandle;

        private GraphicsBuffer _bodyPoseGraphicsBufferA;
        private GraphicsBuffer _bodyPoseGraphicsBufferB;
        private GraphicsBuffer _activeBodyPoseGraphicsBuffer;
        private GraphicsBuffer _jointMatrixGraphicsBufferA;
        private GraphicsBuffer _jointMatrixGraphicsBufferB;
        private GraphicsBuffer _activeJointMatrixGraphicsBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private Mesh _argsUploadMesh;
        private int _argsUploadInstanceCount = -1;
        private int _graphicsUploadBufferIndex;

        private JobHandle _pendingHandle;
        private bool _pipelineScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _telemetryDumped;
        private bool _pendingOriginShiftRebase;
        private int _freeSlotCount;
        private int _frameIndex;
        private int _lastActiveEntityCount;
        private int _telemetryCursor;
        private float3 _pendingOriginShiftOffset;

        internal NativeArray<float3>.ReadOnly FootPositions =>
            TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers) ? buffers.FootPositions.AsReadOnly() : default;

        internal NativeArray<float3>.ReadOnly TargetFootPositions =>
            TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers) ? buffers.TargetFootPositions.AsReadOnly() : default;

        private int EntityCapacity => math.clamp(_maxEntities, 1, 4096);

        private int LegCapacity => EntityCapacity * MaxLegsPerEntity;

        private ref struct CrabLegVaultBuffers
        {
            public NativeArray<ProceduralCrabLegEntityState> Entities;
            public NativeArray<float3> FootPositions;
            public NativeArray<float3> TargetFootPositions;
            public NativeArray<ProceduralCrabLegStepState> StepStates;
            public NativeArray<ProceduralCrabBodyPose> BodyPoses;
            public NativeArray<ProceduralCrabSolvedJointMatrices> SolvedJointMatrices;
            public NativeArray<ProceduralCrabIkTelemetryEntry> TelemetryRing;
        }

        private void Awake()
        {
            EnsureManagedSlotBuffers();
            InitializeFreeSlots();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
        }

        private void OnEnable()
        {
            EnsureManagedSlotBuffers();
            RefreshColdDependencies();
            TryRegisterHotSwapListener();
            EnsurePersistentBuffers();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            CompletePendingPipelineForTeardown();
            TryUnregisterOriginShiftListener();
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            TryUnregisterHotSwapListener();
            JobHandle dependency = _pipelineScheduled ? _pendingHandle : default;
            DisposeBuffers(dependency);
            ReleaseGraphicsBuffers();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f || !HasPersistentBuffers())
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
            if (_pipelineScheduled || !TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return;

            int activeCount = CaptureFrameState(deltaTime, buffers);
            if (activeCount <= 0)
                return;

            ScheduleGroundAndStepPipeline(in buffers);
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

        private void CompletePendingPipelineForTeardown()
        {
            if (!_pipelineScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);
            _pendingHandle = default;
            _pipelineScheduled = false;
        }

        internal bool TryRegisterEntity(float3 rootPosition, quaternion rootRotation, int legCount, float scale, out int slotIndex)
        {
            slotIndex = -1;
            EnsureManagedSlotBuffers();
            EnsurePersistentBuffers();
            if (!TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return false;

            if (_freeSlotCount <= 0)
                return false;

            int freeStackIndex = _freeSlotCount - 1;
            slotIndex = _freeSlots[freeStackIndex];
            _freeSlotCount = freeStackIndex;
            _slotActive[slotIndex] = true;

            ProceduralCrabLegEntityState entity = BuildDefaultEntityState(slotIndex, rootPosition, rootRotation, legCount, scale);
            buffers.Entities[slotIndex] = entity;
            SeedLegTargets(in entity, buffers);
            return true;
        }

        internal void UnregisterEntity(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex] || !TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return;

            _slotActive[slotIndex] = false;
            ProceduralCrabLegEntityState entity = buffers.Entities[slotIndex];
            ClearLegRange(in entity, buffers);
            buffers.Entities[slotIndex] = default;
            buffers.BodyPoses[slotIndex] = default;
            _freeSlots[_freeSlotCount] = slotIndex;
            _freeSlotCount++;
        }

        internal void SetEntityPose(int slotIndex, float3 rootPosition, quaternion rootRotation, float3 velocity, int health)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex] || !TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return;

            ProceduralCrabLegEntityState entity = buffers.Entities[slotIndex];
            entity.RootPosition = SanitizeFiniteFloat3(rootPosition, entity.RootPosition);
            entity.RootRotation = SanitizeFiniteQuaternion(rootRotation, entity.RootRotation);
            entity.Velocity = SanitizeFiniteFloat3(velocity, float3.zero);
            entity.Health = health;
            if (health <= 0)
            {
                entity.CorpseState = 1;
                entity.StateFlags |= EntityFlagCorpse;
            }
            buffers.Entities[slotIndex] = entity;
        }

        internal void SetSpatialHashAvoidance(int slotIndex, float3 separationOffset, float strength)
        {
            if (!IsValidSlot(slotIndex) || !_slotActive[slotIndex] || !TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return;

            ProceduralCrabLegEntityState entity = buffers.Entities[slotIndex];
            entity.SpatialHashAvoidanceOffset = math.select(separationOffset, float3.zero, !math.all(math.isfinite(separationOffset)));
            entity.SpatialHashAvoidanceStrength = math.isfinite(strength) ? math.saturate(strength) : 0f;
            buffers.Entities[slotIndex] = entity;
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
            if (TryResolvePersistentBuffers(out _))
                return;

            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _entitiesHandle = vault.EnsureGenerationHandle<ProceduralCrabLegEntityState>(BufferID.ProceduralCrabLegEntities, entityCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _footPositionsHandle = vault.EnsureGenerationHandle<float3>(BufferID.ProceduralCrabLegFootPositions, legCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _targetFootPositionsHandle = vault.EnsureGenerationHandle<float3>(BufferID.ProceduralCrabLegTargetFootPositions, legCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _stepStatesHandle = vault.EnsureGenerationHandle<ProceduralCrabLegStepState>(BufferID.ProceduralCrabLegStepStates, legCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _bodyPosesHandle = vault.EnsureGenerationHandle<ProceduralCrabBodyPose>(BufferID.ProceduralCrabBodyPoses, entityCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _solvedJointMatricesHandle = vault.EnsureGenerationHandle<ProceduralCrabSolvedJointMatrices>(BufferID.ProceduralCrabSolvedJointMatrices, legCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<ProceduralCrabIkTelemetryEntry>(BufferID.ProceduralCrabIkTelemetryRing, TelemetryCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);

            if (!TryResolvePersistentBuffers(out _))
            {
                ReleaseVaultHandles(_dataVault);
                ClearVaultHandles();
            }
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            JobHandle disposeDependency = dependency;
            if (_pipelineScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _pendingHandle);

            DispatcherJobSwap.TryComplete(ref disposeDependency, forceComplete: true);
            ReleaseVaultHandles(_dataVault);
            ClearVaultHandles();
            _pendingHandle = default;
            _pipelineScheduled = false;
        }

        private void RefreshColdDependencies()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault, null);
        }

        private void ClearVaultHandles()
        {
            _entitiesHandle = default;
            _footPositionsHandle = default;
            _targetFootPositionsHandle = default;
            _stepStatesHandle = default;
            _bodyPosesHandle = default;
            _solvedJointMatricesHandle = default;
            _telemetryRingHandle = default;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _entitiesHandle);
            ReleaseVaultHandle(vault, ref _footPositionsHandle);
            ReleaseVaultHandle(vault, ref _targetFootPositionsHandle);
            ReleaseVaultHandle(vault, ref _stepStatesHandle);
            ReleaseVaultHandle(vault, ref _bodyPosesHandle);
            ReleaseVaultHandle(vault, ref _solvedJointMatricesHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.AnimationFauna)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private bool HasPersistentBuffers()
        {
            return TryResolvePersistentBuffers(out _);
        }

        private bool TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers)
        {
            buffers = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _entitiesHandle, BufferID.ProceduralCrabLegEntities, out buffers.Entities) ||
                !TryResolveVaultBuffer(vault, in _footPositionsHandle, BufferID.ProceduralCrabLegFootPositions, out buffers.FootPositions) ||
                !TryResolveVaultBuffer(vault, in _targetFootPositionsHandle, BufferID.ProceduralCrabLegTargetFootPositions, out buffers.TargetFootPositions) ||
                !TryResolveVaultBuffer(vault, in _stepStatesHandle, BufferID.ProceduralCrabLegStepStates, out buffers.StepStates) ||
                !TryResolveVaultBuffer(vault, in _bodyPosesHandle, BufferID.ProceduralCrabBodyPoses, out buffers.BodyPoses) ||
                !TryResolveVaultBuffer(vault, in _solvedJointMatricesHandle, BufferID.ProceduralCrabSolvedJointMatrices, out buffers.SolvedJointMatrices) ||
                !TryResolveVaultBuffer(vault, in _telemetryRingHandle, BufferID.ProceduralCrabIkTelemetryRing, out buffers.TelemetryRing))
            {
                buffers = default;
                return false;
            }

            int entityCapacity = EntityCapacity;
            int legCapacity = LegCapacity;
            return buffers.Entities.IsCreated &&
                buffers.FootPositions.IsCreated &&
                buffers.TargetFootPositions.IsCreated &&
                buffers.StepStates.IsCreated &&
                buffers.BodyPoses.IsCreated &&
                buffers.SolvedJointMatrices.IsCreated &&
                buffers.TelemetryRing.IsCreated &&
                buffers.Entities.Length >= entityCapacity &&
                buffers.FootPositions.Length >= legCapacity &&
                buffers.TargetFootPositions.Length >= legCapacity &&
                buffers.StepStates.Length >= legCapacity &&
                buffers.BodyPoses.Length >= entityCapacity &&
                buffers.SolvedJointMatrices.Length >= legCapacity &&
                buffers.TelemetryRing.Length >= TelemetryCapacity;
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private void TryRegister()
        {
            if (_registeredUpdate || !Application.isPlaying)
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault vault = currentService as IDataVault;
            RebindDataVaultForLifecycle(vault, previousService as IDataVault);
            EnsurePersistentBuffers();
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault releaseVaultFallback)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            CompletePendingPipelineForTeardown();
            ReleaseVaultHandles(_dataVault ?? releaseVaultFallback);
            ClearVaultHandles();
            _dataVault = nextVault;
            InitializeFreeSlots();
            _pendingOriginShiftOffset = float3.zero;
            _pendingOriginShiftRebase = false;
            _lastActiveEntityCount = 0;
            _telemetryCursor = 0;
            _telemetryDumped = false;
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

        private int CaptureFrameState(float deltaTime, CrabLegVaultBuffers buffers)
        {
            int activeCount = 0;
            const int surfaceProbeBudgetMode = SurfaceProbeBudgetAllLegs;

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            int frameIndex = _frameIndex++;
            for (int slotIndex = 0; slotIndex < EntityCapacity; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ProceduralCrabLegEntityState entity = buffers.Entities[slotIndex];
                entity.DeltaTime = safeDeltaTime;
                entity.FrameIndex = frameIndex;
                entity.SurfaceProbeBudgetMode = surfaceProbeBudgetMode;
                buffers.Entities[slotIndex] = entity;
                activeCount++;
            }

            _lastActiveEntityCount = activeCount;
            return activeCount;
        }

        private void ScheduleGroundAndStepPipeline(in CrabLegVaultBuffers buffers)
        {
            ProceduralCrabGroundTargetResolveJob targetJob = new ProceduralCrabGroundTargetResolveJob
            {
                Entities = buffers.Entities,
                TargetFootPositions = buffers.TargetFootPositions,
                StepStates = buffers.StepStates
            };
            JobHandle targetHandle = targetJob.Schedule(LegCapacity, MinLegsPerJob);

            ProceduralCrabStepSchedulerJob stepJob = new ProceduralCrabStepSchedulerJob
            {
                Entities = buffers.Entities,
                FootPositions = buffers.FootPositions,
                TargetFootPositions = buffers.TargetFootPositions,
                StepStates = buffers.StepStates
            };

            JobHandle stepHandle = stepJob.Schedule(EntityCapacity, 16, targetHandle);

            ProceduralCrabBodyTiltJob bodyTiltJob = new ProceduralCrabBodyTiltJob
            {
                Entities = buffers.Entities,
                FootPositions = buffers.FootPositions,
                BodyPoses = buffers.BodyPoses,
                BodyVisualScale = 1f
            };
            JobHandle bodyHandle = bodyTiltJob.Schedule(EntityCapacity, 16, stepHandle);

            ProceduralCrabAnalyticalTwoBoneIkJob ikJob = new ProceduralCrabAnalyticalTwoBoneIkJob
            {
                Entities = buffers.Entities,
                FootPositions = buffers.FootPositions,
                BodyPoses = buffers.BodyPoses,
                SolvedJointMatrices = buffers.SolvedJointMatrices,
                JointVisualScale = math.max(0.0001f, _jointVisualScale)
            };

            _pendingHandle = ikJob.Schedule(LegCapacity, MinLegsPerJob, bodyHandle);
            _pipelineScheduled = true;
        }

        private void UploadAndRenderIndirect()
        {
            if (!_renderIndirect || _crabBodyMesh == null || _crabBodyMaterial == null)
                return;

            if (!TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
                return;

            EnsureGraphicsBuffers();
            if (_bodyPoseGraphicsBufferA == null ||
                _bodyPoseGraphicsBufferB == null ||
                _jointMatrixGraphicsBufferA == null ||
                _jointMatrixGraphicsBufferB == null ||
                _indirectArgsBuffer == null)
                return;

            GraphicsBuffer bodyWriteBuffer = _graphicsUploadBufferIndex == 0 ? _bodyPoseGraphicsBufferA : _bodyPoseGraphicsBufferB;
            GraphicsBuffer jointWriteBuffer = _graphicsUploadBufferIndex == 0 ? _jointMatrixGraphicsBufferA : _jointMatrixGraphicsBufferB;
            GraphicsBufferUploadUtility.UploadNativeArray(bodyWriteBuffer, buffers.BodyPoses, EntityCapacity);
            GraphicsBufferUploadUtility.UploadNativeArray(jointWriteBuffer, buffers.SolvedJointMatrices, LegCapacity);
            _activeBodyPoseGraphicsBuffer = bodyWriteBuffer;
            _activeJointMatrixGraphicsBuffer = jointWriteBuffer;
            _graphicsUploadBufferIndex ^= 1;
            _crabBodyMaterial.SetBuffer(BodyPoseBufferId, _activeBodyPoseGraphicsBuffer);
            _crabBodyMaterial.SetBuffer(LegJointBufferId, _activeJointMatrixGraphicsBuffer);
            UploadIndirectArgs(EntityCapacity);

            RenderParams renderParams = new RenderParams(_crabBodyMaterial)
            {
                worldBounds = _renderBounds,
                layer = gameObject.layer,
                shadowCastingMode = _shadowCastingMode,
                receiveShadows = true,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, _crabBodyMesh, _indirectArgsBuffer, 1, 0);
        }

        private void EnsureGraphicsBuffers()
        {
            if (_bodyPoseGraphicsBufferA == null)
                _bodyPoseGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabBodyPose>(EntityCapacity); // COLD ALLOC: GraphicsBuffer[body poses A] - indirect crab body S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_bodyPoseGraphicsBufferB == null)
                _bodyPoseGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabBodyPose>(EntityCapacity); // COLD ALLOC: GraphicsBuffer[body poses B] - indirect crab body S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_jointMatrixGraphicsBufferA == null)
                _jointMatrixGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabSolvedJointMatrices>(LegCapacity); // COLD ALLOC: GraphicsBuffer[joint matrices A] - indirect crab leg S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_jointMatrixGraphicsBufferB == null)
                _jointMatrixGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<ProceduralCrabSolvedJointMatrices>(LegCapacity); // COLD ALLOC: GraphicsBuffer[joint matrices B] - indirect crab leg S.O.A. upload - owner: ProceduralCrabLegIKRuntime

            if (_activeBodyPoseGraphicsBuffer == null)
                _activeBodyPoseGraphicsBuffer = _bodyPoseGraphicsBufferA;
            if (_activeJointMatrixGraphicsBuffer == null)
                _activeJointMatrixGraphicsBuffer = _jointMatrixGraphicsBufferA;

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
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = _crabBodyMesh.GetIndexCount(0),
                    instanceCount = (uint)instanceCount,
                    startIndex = _crabBodyMesh.GetIndexStart(0),
                    baseVertexIndex = (uint)Mathf.Max(0, _crabBodyMesh.GetBaseVertex(0)),
                    startInstance = 0u
                };
            }
            finally
            {
                _indirectArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _argsUploadMesh = _crabBodyMesh;
            _argsUploadInstanceCount = instanceCount;
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _bodyPoseGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _bodyPoseGraphicsBufferB);
            ReleaseGraphicsBuffer(ref _jointMatrixGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _jointMatrixGraphicsBufferB);
            ReleaseGraphicsBuffer(ref _indirectArgsBuffer);
            _activeBodyPoseGraphicsBuffer = null;
            _activeJointMatrixGraphicsBuffer = null;
            _graphicsUploadBufferIndex = 0;
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
            if (!TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
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
                ProceduralCrabLegEntityState entity = buffers.Entities[firstActiveSlot];
                float3 firstFoot = buffers.FootPositions[entity.LegStartIndex];
                ProceduralCrabBodyPose bodyPose = buffers.BodyPoses[firstActiveSlot];
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
                    buffers.TelemetryRing[_telemetryCursor % TelemetryCapacity] = entry;
                    DumpTelemetryBlackBoxOnce();
                    _telemetryCursor++;
                    return;
                }
            }

            buffers.TelemetryRing[_telemetryCursor % TelemetryCapacity] = entry;
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
            if (!TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
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
                ProceduralCrabIkTelemetryEntry entry = buffers.TelemetryRing[i];
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
                SurfaceProbeBudgetMode = SurfaceProbeBudgetAllLegs,
                Health = 1,
                LeftStepCursor = 0,
                RightStepCursor = 1,
                StrideLengthSq = safeStride * safeStride,
                StepDuration = ClampFiniteMin(_stepDuration, 0.01f, 0.14f),
                StepHeight = ClampFiniteMin(_stepHeight, 0f, 0.16f),
                GroundProbeHeight = ClampFiniteMin(_groundProbeHeight, 0.01f, 1.2f),
                GroundProbeDistance = ClampFiniteMin(_groundProbeDistance, 0.01f, 3.0f),
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
            if (!TryResolvePersistentBuffers(out CrabLegVaultBuffers buffers))
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

                ProceduralCrabLegEntityState entity = buffers.Entities[slotIndex];
                entity.RootPosition -= offset;
                buffers.Entities[slotIndex] = entity;

                for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
                {
                    int legIndex = entity.LegStartIndex + localLegIndex;
                    buffers.FootPositions[legIndex] -= offset;
                    buffers.TargetFootPositions[legIndex] -= offset;

                    ProceduralCrabLegStepState state = buffers.StepStates[legIndex];
                    state.StepFrom -= offset;
                    state.StepTo -= offset;
                    buffers.StepStates[legIndex] = state;
                }

                ProceduralCrabBodyPose pose = buffers.BodyPoses[slotIndex];
                if (pose.IsActive == 0)
                    continue;

                float4 c3 = pose.BodyMatrix.c3;
                pose.BodyMatrix.c3 = new float4(c3.x - offset.x, c3.y - offset.y, c3.z - offset.z, c3.w);
                buffers.BodyPoses[slotIndex] = pose;
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

        private void SeedLegTargets(in ProceduralCrabLegEntityState entity, CrabLegVaultBuffers buffers)
        {
            for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
            {
                int legIndex = entity.LegStartIndex + localLegIndex;
                if (localLegIndex >= entity.LegCount)
                {
                    buffers.FootPositions[legIndex] = default;
                    buffers.TargetFootPositions[legIndex] = default;
                    buffers.StepStates[legIndex] = default;
                    continue;
                }

                float3 homeLocal = ResolveLegHomeLocal(localLegIndex, entity.LegCount) * entity.Scale;
                float3 world = entity.RootPosition + math.rotate(entity.RootRotation, homeLocal);
                buffers.FootPositions[legIndex] = world;
                buffers.TargetFootPositions[legIndex] = world;
                buffers.StepStates[legIndex] = new ProceduralCrabLegStepState
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

        private void ClearLegRange(in ProceduralCrabLegEntityState entity, CrabLegVaultBuffers buffers)
        {
            for (int localLegIndex = 0; localLegIndex < MaxLegsPerEntity; localLegIndex++)
            {
                int legIndex = entity.LegStartIndex + localLegIndex;
                buffers.FootPositions[legIndex] = default;
                buffers.TargetFootPositions[legIndex] = default;
                buffers.StepStates[legIndex] = default;
                buffers.SolvedJointMatrices[legIndex] = default;
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
