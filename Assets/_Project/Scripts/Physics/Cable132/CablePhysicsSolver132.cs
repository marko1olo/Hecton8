using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    public static class CablePhysics132Constants
    {
        public const int MockTetherCount = 5;
        public const int MockNodesPerTether = 50;
        public const int MaxSplineVerticesPerTether = 64;
        public const int MockNodeCapacity = MockTetherCount * MockNodesPerTether;
        public const int MockConstraintCapacity = MockTetherCount * (MockNodesPerTether - 1);
        public const int MockSplineVertexCapacity = MockTetherCount * MaxSplineVerticesPerTether;
        public const int PhysicsEventCapacity = MockConstraintCapacity;
        public const int TelemetryCapacity = 300;
        public const int TelemetryDumpHeaderBytes = 12;
        public const int TelemetryDumpRowBytes = 56;
        public const string TelemetryDumpPayloadLabel = "cablePhysics132TelemetryDumpPayload";
        public const int MaterialCapacity = 16;
        public const int BootstrapMagic = 0x53483132;
        public const uint EventLaneHash = 0x54455448u;
        public const float SafeLocalAupSpanMeters = 32768f;
    }

    public static class CablePhysics132BufferIds
    {
        public const BufferID CableNodes = BufferID.CablePhysicsSolver132_CableNodes;
        public const BufferID CableConstraints = BufferID.CablePhysicsSolver132_CableConstraints;
        public const BufferID SplineVertices = BufferID.CablePhysicsSolver132_SplineVertices;
        public const BufferID SegmentTensions = BufferID.CablePhysicsSolver132_SegmentTensions;
        public const BufferID PhysicsEvents = BufferID.CablePhysicsSolver132_PhysicsEvents;
        public const BufferID TelemetryRing = BufferID.CablePhysicsSolver132_TelemetryRing;
        public const BufferID TelemetryHead = BufferID.CablePhysicsSolver132_TelemetryHead;
        public const BufferID PinnedAups = BufferID.CablePhysicsSolver132_PinnedAups;
        public const BufferID PinnedMask = BufferID.CablePhysicsSolver132_PinnedMask;
        public const BufferID Tuning = BufferID.CablePhysicsSolver132_Tuning;
        public const BufferID CableMaterials = BufferID.CablePhysicsSolver132_CableMaterials;
        public const BufferID BootstrapState = BufferID.CablePhysicsSolver132_BootstrapState;
        public const BufferID Endpoints = BufferID.CablePhysicsSolver132_Endpoints;
    }

    internal static class CableNodeFlags132
    {
        public const uint Pinned = 1u << 0;
        public const uint NonFiniteRecovered = 1u << 1;
        public const uint ConstraintFault = 1u << 2;
        public const uint NetcodeFence = 1u << 3;
        public const uint TetherTensionEvent = 1u << 16;
        public const uint SignalDrop = 1u << 17;
    }

    public struct CableSplineUploadTicket132
    {
        public GraphicsBuffer Destination;
        public JobHandle Handle;
        public int Count;
        public byte Active;
    }

    public struct CableSplineIndirectArgsUploadTicket132
    {
        public GraphicsBuffer Destination;
        public JobHandle Handle;
        public byte Active;
    }

    public static unsafe class CablePhysicsSolver132
    {
        private const uint ScheduledMockPinCableNodes = 1u << 0;
        private const uint ScheduledMockPinCableConstraints = 1u << 1;
        private const uint ScheduledMockPinEndpoints = 1u << 2;
        private const uint ScheduledMockPinSplineVertices = 1u << 3;
        private const uint ScheduledMockPinSegmentTensions = 1u << 4;
        private const uint ScheduledMockPinPhysicsEvents = 1u << 5;
        private const uint ScheduledMockPinTelemetryRing = 1u << 6;
        private const uint ScheduledMockPinTelemetryHead = 1u << 7;
        private const uint ScheduledMockPinPinnedAups = 1u << 8;
        private const uint ScheduledMockPinPinnedMask = 1u << 9;
        private const uint ScheduledMockPinTuning = 1u << 10;

        private static IDataVault _scheduledMockPinVault;
        private static uint _scheduledMockPinMask;

        private static readonly ulong BootstrapMutationGuardMask =
            VaultMutationGuardBit(CablePhysics132BufferIds.BootstrapState) |
            VaultMutationGuardBit(CablePhysics132BufferIds.CableNodes) |
            VaultMutationGuardBit(CablePhysics132BufferIds.CableConstraints) |
            VaultMutationGuardBit(CablePhysics132BufferIds.Endpoints) |
            VaultMutationGuardBit(CablePhysics132BufferIds.SplineVertices) |
            VaultMutationGuardBit(CablePhysics132BufferIds.SegmentTensions) |
            VaultMutationGuardBit(CablePhysics132BufferIds.PhysicsEvents) |
            VaultMutationGuardBit(CablePhysics132BufferIds.TelemetryRing) |
            VaultMutationGuardBit(CablePhysics132BufferIds.TelemetryHead) |
            VaultMutationGuardBit(CablePhysics132BufferIds.PinnedAups) |
            VaultMutationGuardBit(CablePhysics132BufferIds.PinnedMask) |
            VaultMutationGuardBit(CablePhysics132BufferIds.Tuning) |
            VaultMutationGuardBit(CablePhysics132BufferIds.CableMaterials);

        public static bool ValidateLayout()
        {
            return VerletCableLayout.ValidateTetherAupLayouts() &&
                   UnsafeUtility.SizeOf<CableNodeDTO>() == VerletCableLayout.CableNodeStrideBytes &&
                   UnsafeUtility.SizeOf<TetherTelemetryEntry>() == VerletCableLayout.TetherTelemetryStrideBytes;
        }

        public static int ResolveIterationCount(float globalQualityWeight)
        {
            return ResolveIterationCount(globalQualityWeight, 15);
        }

        public static int ResolveIterationCount(float globalQualityWeight, int requestedMaxIterations)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int maxIterations = requestedMaxIterations > 0 ? math.clamp(requestedMaxIterations, 2, 15) : 15;
            return math.clamp((int)math.lerp(2f, maxIterations, q), 2, maxIterations);
        }

        public static int ResolveSplineVerticesPerCable(float globalQualityWeight, int requestedSplineSteps)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int high = requestedSplineSteps > 0
                ? math.clamp(requestedSplineSteps, 10, CablePhysics132Constants.MaxSplineVerticesPerTether)
                : CablePhysics132Constants.MockNodesPerTether;
            return math.clamp((int)math.lerp(10f, high, Smooth01(q)), 2, CablePhysics132Constants.MaxSplineVerticesPerTether);
        }

        public static void EnsureMockBuffers(IDataVault vault, float globalQualityWeight, uint frameIndex)
        {
            if (vault == null)
                return;

            if (!TryAcquireCableMutationGuard(vault, BootstrapMutationGuardMask))
                return;

            try
            {
                if (!TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.BootstrapState,
                        1,
                        NativeArrayOptions.ClearMemory,
                        out NativeArray<int> bootstrap))
                {
                    return;
                }

                if (bootstrap.IsCreated && bootstrap.Length > 0 && bootstrap[0] == CablePhysics132Constants.BootstrapMagic)
                    return;

                if (!TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.CableNodes,
                        CablePhysics132Constants.MockNodeCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<CableNodeDTO> nodes) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.CableConstraints,
                        CablePhysics132Constants.MockConstraintCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<TetherConstraintDTO> constraints) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.Endpoints,
                        CablePhysics132Constants.MockTetherCount,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<TetherEndpointAupDTO> endpoints) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.SplineVertices,
                        CablePhysics132Constants.MockSplineVertexCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<TetherSplineVertexDTO> vertices) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.SegmentTensions,
                        CablePhysics132Constants.MockConstraintCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<float> tensions) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.PhysicsEvents,
                        CablePhysics132Constants.PhysicsEventCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<PhysicsEventPayload> events) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.TelemetryRing,
                        CablePhysics132Constants.TelemetryCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<TetherTelemetryEntry> telemetryRing) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.TelemetryHead,
                        1,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<int> telemetryHead) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.PinnedAups,
                        CablePhysics132Constants.MockNodeCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<double3> pinnedAups) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.PinnedMask,
                        CablePhysics132Constants.MockNodeCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<byte> pinnedMask) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.Tuning,
                        1,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<VerletCableTuningDTO> tuning) ||
                    !TryOpenOrAcquireVaultView(
                        vault,
                        CablePhysics132BufferIds.CableMaterials,
                        CablePhysics132Constants.MaterialCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<CableMaterialDTO> materials))
                {
                    return;
                }

                ZeroInitCableBuffersDirect(vertices, tensions, events, telemetryRing, telemetryHead, tuning);
                GenerateMockTethersDirect(
                    nodes,
                    constraints,
                    endpoints,
                    materials,
                    bootstrap,
                    pinnedAups,
                    pinnedMask,
                    globalQualityWeight);
            }
            finally
            {
                vault.ReleaseMutationGuard(BootstrapMutationGuardMask);
            }
        }

        public static bool TryHasMockBuffers(IDataVault vault)
        {
            return TryResolveMockBuffers(
                vault,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private static bool TryResolveMockBuffers(
            IDataVault vault,
            out NativeArray<CableNodeDTO> nodes,
            out NativeArray<TetherConstraintDTO> constraints,
            out NativeArray<TetherEndpointAupDTO> endpoints,
            out NativeArray<TetherSplineVertexDTO> vertices,
            out NativeArray<float> segmentTensions,
            out NativeArray<PhysicsEventPayload> physicsEvents,
            out NativeArray<TetherTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryHead,
            out NativeArray<double3> pinnedAups,
            out NativeArray<byte> pinnedMask,
            out NativeArray<VerletCableTuningDTO> tuning)
        {
            nodes = default;
            constraints = default;
            endpoints = default;
            vertices = default;
            segmentTensions = default;
            physicsEvents = default;
            telemetryRing = default;
            telemetryHead = default;
            pinnedAups = default;
            pinnedMask = default;
            tuning = default;

            return TryOpenExistingVaultView(vault, CablePhysics132BufferIds.CableNodes, out nodes) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.CableConstraints, out constraints) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.Endpoints, out endpoints) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.SplineVertices, out vertices) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.SegmentTensions, out segmentTensions) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.PhysicsEvents, out physicsEvents) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.TelemetryRing, out telemetryRing) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.TelemetryHead, out telemetryHead) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.PinnedAups, out pinnedAups) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.PinnedMask, out pinnedMask) &&
                   TryOpenExistingVaultView(vault, CablePhysics132BufferIds.Tuning, out tuning);
        }

        public static bool TryScheduleMockFromVault(
            IDataVault vault,
            uint frameIndex,
            float fixedDeltaTime,
            float3 gravityAcceleration,
            float3 externalAbyssalFlow,
            double3 cameraAup,
            float globalQualityWeight,
            float cpuMicroseconds,
            JobHandle dependency,
            out JobHandle handle)
        {
            handle = dependency;
            if (!TryLockMockScheduleBuffers(vault))
                return false;

            bool scheduled = false;
            try
            {
                if (!TryResolveMockBuffers(
                        vault,
                        out NativeArray<CableNodeDTO> nodes,
                        out NativeArray<TetherConstraintDTO> constraints,
                        out NativeArray<TetherEndpointAupDTO> endpoints,
                        out NativeArray<TetherSplineVertexDTO> vertices,
                        out NativeArray<float> segmentTensions,
                        out NativeArray<PhysicsEventPayload> physicsEvents,
                        out NativeArray<TetherTelemetryEntry> telemetryRing,
                        out NativeArray<int> telemetryHead,
                        out NativeArray<double3> pinnedAups,
                        out NativeArray<byte> pinnedMask,
                        out NativeArray<VerletCableTuningDTO> tuning))
                {
                    return false;
                }

                handle = ScheduleMock(
                    nodes,
                    constraints,
                    endpoints,
                    vertices,
                    segmentTensions,
                    physicsEvents,
                    telemetryRing,
                    telemetryHead,
                    pinnedAups,
                    pinnedMask,
                    tuning,
                    AcquirePhysicsEventWriter(),
                    AcquirePhysicsEventWriterBudget(),
                    frameIndex,
                    fixedDeltaTime,
                    gravityAcceleration,
                    externalAbyssalFlow,
                    cameraAup,
                    globalQualityWeight,
                    cpuMicroseconds,
                    dependency);
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseMockScheduleBufferPins(vault);
            }
        }

        public static void ReleaseMockScheduleBufferPins(IDataVault vault)
        {
            IDataVault pinVault = _scheduledMockPinVault ?? vault;
            uint pinMask = _scheduledMockPinMask;
            _scheduledMockPinVault = null;
            _scheduledMockPinMask = 0u;
            if (pinVault == null || pinMask == 0u)
                return;

            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinTuning, CablePhysics132BufferIds.Tuning);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinPinnedMask, CablePhysics132BufferIds.PinnedMask);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinPinnedAups, CablePhysics132BufferIds.PinnedAups);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinTelemetryHead, CablePhysics132BufferIds.TelemetryHead);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinTelemetryRing, CablePhysics132BufferIds.TelemetryRing);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinPhysicsEvents, CablePhysics132BufferIds.PhysicsEvents);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinSegmentTensions, CablePhysics132BufferIds.SegmentTensions);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinSplineVertices, CablePhysics132BufferIds.SplineVertices);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinEndpoints, CablePhysics132BufferIds.Endpoints);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinCableConstraints, CablePhysics132BufferIds.CableConstraints);
            TryUnlockMockSchedulePin(pinVault, pinMask, ScheduledMockPinCableNodes, CablePhysics132BufferIds.CableNodes);
        }

        public static JobHandle ScheduleMock(
            NativeArray<CableNodeDTO> nodes,
            NativeArray<TetherConstraintDTO> constraints,
            NativeArray<TetherEndpointAupDTO> endpoints,
            NativeArray<TetherSplineVertexDTO> vertices,
            NativeArray<float> segmentTensions,
            NativeArray<PhysicsEventPayload> physicsEvents,
            NativeArray<TetherTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            NativeArray<double3> pinnedAups,
            NativeArray<byte> pinnedMask,
            NativeArray<VerletCableTuningDTO> tuning,
            global::Hecton8.Core.MpscSignalRingBuffer<PhysicsEventPayload>.ParallelWriter physicsEventWriter,
            NativeArray<int> physicsEventWriterBudget,
            uint frameIndex,
            float fixedDeltaTime,
            float3 gravityAcceleration,
            float3 externalAbyssalFlow,
            double3 cameraAup,
            float globalQualityWeight,
            float cpuMicroseconds,
            JobHandle dependency)
        {
            if (!nodes.IsCreated ||
                !constraints.IsCreated ||
                !endpoints.IsCreated ||
                !vertices.IsCreated ||
                !segmentTensions.IsCreated ||
                !physicsEvents.IsCreated ||
                !telemetryRing.IsCreated ||
                !telemetryHead.IsCreated)
            {
                return dependency;
            }

            CableNodeDTO* nodePtr = (CableNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes);
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            VerletCableTuningDTO tuningValue = tuning.IsCreated && tuning.Length > 0 ? tuning[0] : default;
            int iterations = ResolveIterationCount(q, tuningValue.ConstraintIterations);
            float3 gravity = math.all(math.isfinite(tuningValue.Gravity)) && math.lengthsq(tuningValue.Gravity) > 0.000001f
                ? tuningValue.Gravity
                : gravityAcceleration;
            float damping = math.isfinite(tuningValue.FluidFriction) && tuningValue.FluidFriction > 0f
                ? math.clamp(tuningValue.FluidFriction, 0.80f, 0.999f)
                : math.lerp(0.965f, 0.992f, Smooth01(q));
            float breakForce = math.isfinite(tuningValue.BreakForce) && tuningValue.BreakForce > 0f
                ? tuningValue.BreakForce
                : 18000f;
            float tensionScale = math.max(1f, breakForce * 0.0013333334f);
            float3 externalFlowAcceleration = math.all(math.isfinite(externalAbyssalFlow))
                ? externalAbyssalFlow * math.lerp(0.015f, 0.08f, q)
                : float3.zero;
            int verticesPerCable = ResolveSplineVerticesPerCable(q, (int)math.round(tuningValue.Reserved0));
            int cableCount = math.min(
                CablePhysics132Constants.MockTetherCount,
                math.min(nodes.Length / CablePhysics132Constants.MockNodesPerTether, vertices.Length / math.max(1, verticesPerCable)));
            int totalSplineVertices = math.max(0, cableCount * verticesPerCable);
            JobHandle clearHandle = new ClearFrameCableOutputsJob
            {
                Vertices = vertices,
                SegmentTensions = segmentTensions,
                PhysicsEvents = physicsEvents
            }.Schedule(dependency);

            JobHandle endpointHandle = new AdvanceMockCableEndpointsJob
            {
                Nodes = nodePtr,
                NodeCount = nodes.Length,
                Endpoints = endpoints,
                PinnedAUPs = pinnedAups,
                PinnedMask = pinnedMask,
                FrameIndex = frameIndex,
                GlobalQualityWeight = q
            }.Schedule(clearHandle);

            JobHandle integrateHandle = new SimulateCablePointsJob
            {
                Nodes = nodePtr,
                NodeCount = nodes.Length,
                PinnedAUPs = pinnedAups,
                PinnedMask = pinnedMask,
                GravityAcceleration = gravity,
                AbyssalCurrentAcceleration = ResolveAbyssalCurrent(q, frameIndex) + externalFlowAcceleration,
                SimulationTickDelta = math.clamp(fixedDeltaTime, 0.001f, 0.05f),
                VelocityDamping = damping,
                MaxStepMeters = math.lerp(0.12f, 1.25f, q),
                GlobalQualityWeight = q
            }.Schedule(nodes.Length, 32, endpointHandle);

            JobHandle solveHandle = new SolveCableConstraintsJob
            {
                Nodes = nodePtr,
                NodeCount = nodes.Length,
                Constraints = constraints,
                SegmentTensions = segmentTensions,
                PhysicsEvents = physicsEvents,
                PhysicsEventWriter = physicsEventWriter,
                PhysicsEventWriterBudget = physicsEventWriterBudget,
                PhysicsEventWriterEnabled = 1,
                CameraAUP = cameraAup,
                IterationCount = iterations,
                TensionForceScale = tensionScale,
                InvSnapTension = math.rcp(math.max(100f, breakForce)),
                FrameIndex = frameIndex,
                GlobalQualityWeight = q
            }.Schedule(integrateHandle);

            JobHandle splineHandle = solveHandle;
            if (totalSplineVertices > 0)
            {
                splineHandle = new GenerateSplineVerticesJob
                {
                    Nodes = nodePtr,
                    NodeCount = nodes.Length,
                    SegmentTensions = segmentTensions,
                    Vertices = vertices,
                    CameraAUP = cameraAup,
                    NodesPerCable = CablePhysics132Constants.MockNodesPerTether,
                    VerticesPerCable = verticesPerCable,
                    CableCount = cableCount,
                    TotalVertexCount = totalSplineVertices,
                    GlobalQualityWeight = q
                }.Schedule(totalSplineVertices, 32, solveHandle);
            }

            return new RecordCableTetherTelemetryJob
            {
                Nodes = nodePtr,
                NodeCount = nodes.Length,
                SegmentTensions = segmentTensions,
                TelemetryRing = telemetryRing,
                TelemetryHead = telemetryHead,
                FrameIndex = frameIndex,
                IterationCount = iterations,
                CpuMicroseconds = cpuMicroseconds,
                GlobalQualityWeight = q
            }.Schedule(splineHandle);
        }

        private static global::Hecton8.Core.MpscSignalRingBuffer<PhysicsEventPayload>.ParallelWriter AcquirePhysicsEventWriter()
        {
            SignalBus<PhysicsEventPayload>.EnsureInitialized();
            return SignalBus<PhysicsEventPayload>.ParallelWriter;
        }

        private static NativeArray<int> AcquirePhysicsEventWriterBudget()
        {
            SignalBus<PhysicsEventPayload>.EnsureInitialized();
            return SignalBus<PhysicsEventPayload>.ParallelWriterBudget;
        }

        public static bool TrySampleLatestTelemetry(IDataVault vault, out TetherTelemetryEntry telemetry)
        {
            telemetry = default;
            if (!TryReadExistingVaultView(vault, CablePhysics132BufferIds.TelemetryRing, out NativeArray<TetherTelemetryEntry>.ReadOnly ring) ||
                !TryReadExistingVaultView(vault, CablePhysics132BufferIds.TelemetryHead, out NativeArray<int>.ReadOnly headArray))
                return false;

            if (!ring.IsCreated || !headArray.IsCreated || ring.Length <= 0 || headArray.Length <= 0)
                return false;

            int capacity = math.min(CablePhysics132Constants.TelemetryCapacity, ring.Length);
            int head = headArray[0];
            int lastHead = head <= 0 ? capacity - 1 : math.min(head - 1, capacity - 1);
            telemetry = ring[lastHead];
            return telemetry.FrameIndex != 0u || telemetry.NodeCount > 0;
        }

        public static bool TryDumpCableSurgeon(IDataVault vault, uint reasonFlags)
        {
            return TryDumpLatestVault(vault, string.Empty, reasonFlags);
        }

        public static bool TryDumpLatestVault(IDataVault vault, string projectRoot, uint reasonFlags)
        {
            if (!TryReadExistingVaultView(vault, CablePhysics132BufferIds.TelemetryRing, out NativeArray<TetherTelemetryEntry>.ReadOnly ring))
                return false;

            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            bool primary = WriteTelemetryDump("Docs/AgentLogs/Dump_SHINOBU_132.bin", ring, reasonFlags);
            bool surgeon = WriteTelemetryDump("Docs/AgentLogs/Dump_CABLE_SURGEON.bin", ring, reasonFlags);
            return primary && surgeon;
        }

        public static bool TrySampleTuning(IDataVault vault, out VerletCableTuningDTO tuning)
        {
            tuning = default;
            if (!TryReadExistingVaultView(vault, CablePhysics132BufferIds.Tuning, out NativeArray<VerletCableTuningDTO>.ReadOnly tuningView) ||
                !tuningView.IsCreated ||
                tuningView.Length <= 0)
            {
                return false;
            }

            tuning = SanitizeEditorTuning(tuningView[0]);
            return true;
        }

        public static bool TryWriteTuning(IDataVault vault, in VerletCableTuningDTO tuning)
        {
            if (!TryOpenOrAcquireWritableVaultView(
                    vault,
                    CablePhysics132BufferIds.Tuning,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<VerletCableTuningDTO> handle,
                    out NativeArray<VerletCableTuningDTO> tuningView) ||
                !tuningView.IsCreated ||
                tuningView.Length <= 0)
            {
                return false;
            }

            try
            {
                tuningView[0] = SanitizeEditorTuning(in tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Physics);
            }
        }

#if UNITY_EDITOR
        public static bool TryApplyMaterialCsv(IDataVault vault, ReadOnlySpan<byte> csvBytes, out int parsed)
        {
            parsed = 0;
            if (csvBytes.Length <= 0)
                return false;

            Span<CableMaterialDTO> materialScratch = stackalloc CableMaterialDTO[CablePhysics132Constants.MaterialCapacity];
            parsed = CableMaterialCsvParser.ParseHashTable(csvBytes, materialScratch);

            if (!TryOpenOrAcquireWritableVaultView(
                    vault,
                    CablePhysics132BufferIds.CableMaterials,
                    CablePhysics132Constants.MaterialCapacity,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<CableMaterialDTO> handle,
                    out NativeArray<CableMaterialDTO> materials) ||
                !materials.IsCreated ||
                materials.Length <= 0)
            {
                return false;
            }

            try
            {
                int copyCount = math.min(materials.Length, materialScratch.Length);
                for (int i = 0; i < copyCount; i++)
                    materials[i] = materialScratch[i];

                for (int i = copyCount; i < materials.Length; i++)
                    materials[i] = default;

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Physics);
            }
        }
#endif

        private static bool TryOpenOrAcquireWritableVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle))
                return TryAcquireWritableVaultView(vault, bufferId, required, in handle, out buffer);

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                required,
                SystemID.Physics,
                options);
            return TryAcquireWritableVaultView(vault, bufferId, required, in handle, out buffer);
        }

        private static bool TryAcquireWritableVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.SystemID != (uint)SystemID.Physics ||
                handle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.Physics, out buffer))
                return false;

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, SystemID.Physics);
            }
        }

        private static VerletCableTuningDTO SanitizeEditorTuning(in VerletCableTuningDTO tuning)
        {
            VerletCableTuningDTO sanitized = tuning;
            if (math.lengthsq(sanitized.Gravity) <= 0.000001f || !math.all(math.isfinite(sanitized.Gravity)))
                sanitized.Gravity = new float3(0f, -9.80665f, 0f);
            if (!math.isfinite(sanitized.FluidFriction) || sanitized.FluidFriction <= 0f)
                sanitized.FluidFriction = 0.975f;
            if (!math.isfinite(sanitized.StretchThreshold01) || sanitized.StretchThreshold01 <= 0f)
                sanitized.StretchThreshold01 = 0.18f;
            if (!math.isfinite(sanitized.RockFriction01) || sanitized.RockFriction01 <= 0f)
                sanitized.RockFriction01 = 0.58f;
            if (!math.isfinite(sanitized.ReelSpeedMetersPerSecond) || sanitized.ReelSpeedMetersPerSecond <= 0f)
                sanitized.ReelSpeedMetersPerSecond = 18f;
            if (!math.isfinite(sanitized.Reserved0) || sanitized.Reserved0 <= 0f)
                sanitized.Reserved0 = 50f;

            sanitized.FluidFriction = math.saturate(sanitized.FluidFriction);
            sanitized.ConstraintIterations = math.clamp(sanitized.ConstraintIterations, 0, 15);
            sanitized.StretchThreshold01 = math.max(0.001f, sanitized.StretchThreshold01);
            sanitized.BreakForce = math.max(0f, math.isfinite(sanitized.BreakForce) ? sanitized.BreakForce : 0f);
            sanitized.RockFriction01 = math.saturate(sanitized.RockFriction01);
            sanitized.ReelSpeedMetersPerSecond = math.max(0.001f, sanitized.ReelSpeedMetersPerSecond);
            sanitized.Reserved0 = math.clamp(sanitized.Reserved0, 10f, 64f);
            return sanitized;
        }

        private static bool TryOpenOrAcquireVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryResolveHandle(in existing, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= required)
            {
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                required,
                SystemID.Physics,
                options);
            return vault.TryResolveHandle(in acquired, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= required;
        }

        private static bool TryAcquireCableMutationGuard(IDataVault vault, ulong mask)
        {
            return vault != null &&
                   mask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(mask);
        }

        private static bool TryLockMockScheduleBuffers(IDataVault vault)
        {
            if (vault == null ||
                _scheduledMockPinVault != null ||
                _scheduledMockPinMask != 0u ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            _scheduledMockPinVault = vault;
            bool locked = false;
            try
            {
                if (!TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.CableNodes, ScheduledMockPinCableNodes) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.CableConstraints, ScheduledMockPinCableConstraints) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.Endpoints, ScheduledMockPinEndpoints) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.SplineVertices, ScheduledMockPinSplineVertices) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.SegmentTensions, ScheduledMockPinSegmentTensions) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.PhysicsEvents, ScheduledMockPinPhysicsEvents) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.TelemetryRing, ScheduledMockPinTelemetryRing) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.TelemetryHead, ScheduledMockPinTelemetryHead) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.PinnedAups, ScheduledMockPinPinnedAups) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.PinnedMask, ScheduledMockPinPinnedMask) ||
                    !TryLockMockScheduleBuffer(vault, CablePhysics132BufferIds.Tuning, ScheduledMockPinTuning))
                {
                    return false;
                }

                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    ReleaseMockScheduleBufferPins(vault);
            }
        }

        private static bool TryLockMockScheduleBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_scheduledMockPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.Physics))
                return false;

            _scheduledMockPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockMockSchedulePin(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.Physics);
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private static void ZeroInitCableBuffersDirect(
            NativeArray<TetherSplineVertexDTO> vertices,
            NativeArray<float> segmentTensions,
            NativeArray<PhysicsEventPayload> physicsEvents,
            NativeArray<TetherTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            NativeArray<VerletCableTuningDTO> tuning)
        {
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = default;
            for (int i = 0; i < segmentTensions.Length; i++)
                segmentTensions[i] = 0f;
            for (int i = 0; i < physicsEvents.Length; i++)
                physicsEvents[i] = default;
            for (int i = 0; i < telemetryRing.Length; i++)
                telemetryRing[i] = default;
            if (telemetryHead.IsCreated && telemetryHead.Length > 0)
                telemetryHead[0] = 0;
            if (tuning.IsCreated && tuning.Length > 0)
                tuning[0] = DefaultCableTuning();
        }

        private static void GenerateMockTethersDirect(
            NativeArray<CableNodeDTO> nodes,
            NativeArray<TetherConstraintDTO> constraints,
            NativeArray<TetherEndpointAupDTO> endpoints,
            NativeArray<CableMaterialDTO> materials,
            NativeArray<int> bootstrap,
            NativeArray<double3> pinnedAups,
            NativeArray<byte> pinnedMask,
            float globalQualityWeight)
        {
            int tetherCount = math.min(CablePhysics132Constants.MockTetherCount, endpoints.IsCreated ? endpoints.Length : 0);
            int nodesPerTether = CablePhysics132Constants.MockNodesPerTether;
            int nodeCount = nodes.IsCreated ? nodes.Length : 0;
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);

            for (int cable = 0; cable < tetherCount; cable++)
            {
                double3 anchor = new double3(cable * 4.0, -24.0 - cable * 0.25, cable * 2.25);
                double3 payload = anchor + new double3(18.0 + cable * 0.5, -2.0, 7.5);
                float3 current = new float3(0.045f * (1f + cable), -0.018f, 0.035f * (1f + q));
                endpoints[cable] = new TetherEndpointAupDTO
                {
                    AnchorAUP = anchor,
                    PayloadAUP = payload,
                    AbyssalCurrentAcceleration = current,
                    GlobalQualityWeight = q
                };

                int nodeOffset = cable * nodesPerTether;
                int constraintOffset = cable * (nodesPerTether - 1);
                for (int i = 0; i < nodesPerTether; i++)
                {
                    int nodeIndex = nodeOffset + i;
                    if ((uint)nodeIndex >= (uint)nodeCount)
                        continue;

                    float t = i * math.rcp(math.max(1, nodesPerTether - 1));
                    double3 sag = new double3(0.0, -VerletCableSimdMath.SinPolynomial7(t * math.PI) * math.lerp(0.35f, 2.25f, q), 0.0);
                    double3 position = anchor + (payload - anchor) * (double)t + sag;
                    CableNodeDTO node = nodes[nodeIndex];
                    node.CurrentAUP = position;
                    node.PreviousAUP = position - new double3(current.x, current.y, current.z) * 0.016;
                    node.InverseMass = (i == 0 || i == nodesPerTether - 1) ? 0f : 1f;
                    node.Flags = (i == 0 || i == nodesPerTether - 1)
                        ? CableNodeFlags132.Pinned | CableNodeFlags132.NetcodeFence
                        : 0u;
                    nodes[nodeIndex] = node;
                    if (pinnedAups.IsCreated && nodeIndex < pinnedAups.Length)
                        pinnedAups[nodeIndex] = position;
                    if (pinnedMask.IsCreated && nodeIndex < pinnedMask.Length)
                        pinnedMask[nodeIndex] = node.InverseMass <= 0f ? (byte)1 : (byte)0;
                }

                for (int i = 0; i < nodesPerTether - 1; i++)
                {
                    int constraintIndex = constraintOffset + i;
                    if ((uint)constraintIndex >= (uint)constraints.Length)
                        continue;

                    int nodeA = nodeOffset + i;
                    int nodeB = nodeA + 1;
                    if ((uint)nodeA >= (uint)nodeCount || (uint)nodeB >= (uint)nodeCount)
                        continue;

                    double3 restDeltaAup = nodes[nodeB].CurrentAUP - nodes[nodeA].CurrentAUP;
                    float3 restLocal = AupPrecisionMath.DowncastLocalDelta(restDeltaAup, float3.zero);
                    float restLength = VerletCableSimdMath.LengthFromSq(math.lengthsq(restLocal));
                    constraints[constraintIndex] = new TetherConstraintDTO
                    {
                        NodeA = nodeA,
                        NodeB = nodeB,
                        RestLength = math.max(VerletCableLayout.MinConstraintLength, restLength),
                        Stiffness = math.lerp(0.72f, 0.98f, q),
                        Flags = CableNodeFlags132.NetcodeFence,
                        CableId = (uint)cable
                    };
                }
            }

            CableMaterialDTO.GenerateEmergencyMockCables(materials);
            if (bootstrap.IsCreated && bootstrap.Length > 0)
                bootstrap[0] = CablePhysics132Constants.BootstrapMagic;
        }

        private static VerletCableTuningDTO DefaultCableTuning()
        {
            return new VerletCableTuningDTO
            {
                Gravity = new float3(0f, -9.80665f, 0f),
                FluidFriction = 0.975f,
                ConstraintIterations = 0,
                StretchThreshold01 = 0.18f,
                BreakForce = 18000f,
                RockFriction01 = 0.58f,
                ReelSpeedMetersPerSecond = 18f
            };
        }

        private static bool TryOpenExistingVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryReadExistingVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool WriteTelemetryDump(string path, NativeArray<TetherTelemetryEntry>.ReadOnly ring, uint reasonFlags)
        {
            int capacity = math.min(ring.Length, CablePhysics132Constants.TelemetryCapacity);
            long totalBytes = CablePhysics132Constants.TelemetryDumpHeaderBytes +
                              ((long)capacity * CablePhysics132Constants.TelemetryDumpRowBytes);
            if (totalBytes < CablePhysics132Constants.TelemetryDumpHeaderBytes || totalBytes > int.MaxValue)
                return false;

            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)totalBytes,
                    nameof(CablePhysicsSolver132),
                    CablePhysics132Constants.TelemetryDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                WriteUInt32LittleEndian(payload, 0, 0x53483132u);
                WriteUInt32LittleEndian(payload, 4, reasonFlags);
                WriteInt32LittleEndian(payload, 8, capacity);

                int cursor = CablePhysics132Constants.TelemetryDumpHeaderBytes;
                for (int i = 0; i < capacity; i++)
                {
                    WriteTelemetryEntry(payload, cursor, ring[i]);
                    cursor += CablePhysics132Constants.TelemetryDumpRowBytes;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, (int)totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(CablePhysicsSolver132),
                    CablePhysics132Constants.TelemetryDumpPayloadLabel);
            }
        }

        private static void WriteTelemetryEntry(NativeArray<byte> destination, int offset, TetherTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, offset, entry.FrameIndex);
            WriteInt32LittleEndian(destination, offset + 4, entry.NodeCount);
            WriteInt32LittleEndian(destination, offset + 8, entry.IterationCount);
            WriteFloat32LittleEndian(destination, offset + 12, entry.MaxTension);
            WriteFloat64LittleEndian(destination, offset + 16, entry.AnchorAUP.x);
            WriteFloat64LittleEndian(destination, offset + 24, entry.AnchorAUP.y);
            WriteFloat64LittleEndian(destination, offset + 32, entry.AnchorAUP.z);
            WriteUInt32LittleEndian(destination, offset + 40, entry.StateHash);
            WriteUInt32LittleEndian(destination, offset + 44, entry.Flags);
            WriteFloat32LittleEndian(destination, offset + 48, entry.CpuMicroseconds);
            WriteFloat32LittleEndian(destination, offset + 52, entry.GlobalQualityWeight);
        }

        private static void WriteFloat64LittleEndian(NativeArray<byte> destination, int offset, double value)
        {
            WriteUInt64LittleEndian(destination, offset, math.asulong(value));
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        public static bool TryBeginSplineVertexUpload(
            GraphicsBuffer destination,
            NativeArray<TetherSplineVertexDTO> source,
            int count,
            JobHandle dependency,
            out CableSplineUploadTicket132 upload)
        {
            upload = default;
            if (destination == null ||
                !source.IsCreated ||
                count <= 0 ||
                destination.count <= 0 ||
                destination.stride != VerletCableLayout.TetherSplineVertexStrideBytes)
            {
                return false;
            }

            int safeCount = math.min(count, math.min(destination.count, source.Length));
            if (safeCount <= 0)
                return false;

            bool locked = false;
            try
            {
                NativeArray<TetherSplineVertexDTO> mapped = destination.LockBufferForWrite<TetherSplineVertexDTO>(0, safeCount);
                locked = true;
                JobHandle uploadHandle = new CableSplineGpuMemcpyJob
                {
                    Source = source,
                    Destination = (TetherSplineVertexDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                    Count = safeCount,
                    DestinationBytes = (long)mapped.Length * VerletCableLayout.TetherSplineVertexStrideBytes
                }.Schedule(dependency);

                upload = new CableSplineUploadTicket132
                {
                    Destination = destination,
                    Handle = uploadHandle,
                    Count = safeCount,
                    Active = 1
                };
                return true;
            }
            catch
            {
                if (locked)
                    TryUnlockSplineVertexUploadBuffer(destination, safeCount);

                upload = default;
                return false;
            }
        }

        public static bool TryFinalizeSplineVertexUpload(ref CableSplineUploadTicket132 upload)
        {
            if (upload.Active == 0 || upload.Destination == null)
            {
                upload = default;
                return false;
            }

            JobHandle handle = upload.Handle;
            if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                return false;

            bool unlocked = TryUnlockSplineVertexUploadBuffer(upload.Destination, upload.Count);
            upload = default;
            return unlocked;
        }

        public static bool ForceFinalizeSplineVertexUpload(ref CableSplineUploadTicket132 upload)
        {
            if (upload.Active == 0 || upload.Destination == null)
            {
                upload = default;
                return false;
            }

            JobHandle handle = upload.Handle;
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            bool unlocked = TryUnlockSplineVertexUploadBuffer(upload.Destination, upload.Count);
            upload = default;
            return unlocked;
        }

        public static GraphicsBuffer CreateSplineVertexBuffer(int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                safeCapacity,
                VerletCableLayout.TetherSplineVertexStrideBytes);
        }

        public static GraphicsBuffer CreateSplineIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                VerletCableLayout.TetherSplineIndirectArgsStrideBytes);
        }

        public static bool TryBeginSplineIndirectArgsUpload(
            GraphicsBuffer destination,
            int splineVertexCount,
            int verticesPerSplinePoint,
            JobHandle dependency,
            out CableSplineIndirectArgsUploadTicket132 upload)
        {
            upload = default;
            if (destination == null ||
                destination.count < 1 ||
                destination.stride != VerletCableLayout.TetherSplineIndirectArgsStrideBytes ||
                splineVertexCount <= 0)
            {
                return false;
            }

            long expandedVertexCount = (long)math.max(1, verticesPerSplinePoint) * splineVertexCount;
            uint vertexCount = (uint)(expandedVertexCount > uint.MaxValue ? uint.MaxValue : expandedVertexCount);
            if (vertexCount == 0u)
                return false;

            bool locked = false;
            try
            {
                NativeArray<TetherSplineIndirectArgsDTO> mapped = destination.LockBufferForWrite<TetherSplineIndirectArgsDTO>(0, 1);
                locked = true;
                JobHandle uploadHandle = new CableSplineIndirectArgsJob
                {
                    Destination = (TetherSplineIndirectArgsDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                    VertexCountPerInstance = vertexCount,
                    InstanceCount = 1u
                }.Schedule(dependency);

                upload = new CableSplineIndirectArgsUploadTicket132
                {
                    Destination = destination,
                    Handle = uploadHandle,
                    Active = 1
                };
                return true;
            }
            catch
            {
                if (locked)
                    TryUnlockSplineIndirectArgsUploadBuffer(destination);

                upload = default;
                return false;
            }
        }

        public static bool TryFinalizeSplineIndirectArgsUpload(ref CableSplineIndirectArgsUploadTicket132 upload)
        {
            if (upload.Active == 0 || upload.Destination == null)
            {
                upload = default;
                return false;
            }

            JobHandle handle = upload.Handle;
            if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                return false;

            bool unlocked = TryUnlockSplineIndirectArgsUploadBuffer(upload.Destination);
            upload = default;
            return unlocked;
        }

        public static bool ForceFinalizeSplineIndirectArgsUpload(ref CableSplineIndirectArgsUploadTicket132 upload)
        {
            if (upload.Active == 0 || upload.Destination == null)
            {
                upload = default;
                return false;
            }

            JobHandle handle = upload.Handle;
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            bool unlocked = TryUnlockSplineIndirectArgsUploadBuffer(upload.Destination);
            upload = default;
            return unlocked;
        }

        private static bool TryUnlockSplineVertexUploadBuffer(GraphicsBuffer destination, int count)
        {
            try
            {
                destination.UnlockBufferAfterWrite<TetherSplineVertexDTO>(count);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryUnlockSplineIndirectArgsUploadBuffer(GraphicsBuffer destination)
        {
            try
            {
                destination.UnlockBufferAfterWrite<TetherSplineIndirectArgsDTO>(1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryDrawSplineProceduralIndirect(
            Material material,
            Bounds bounds,
            GraphicsBuffer indirectArgsBuffer,
            int layer)
        {
            if (material == null || indirectArgsBuffer == null)
                return false;

            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                bounds,
                MeshTopology.Triangles,
                indirectArgsBuffer,
                0,
                null,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                layer);
            return true;
        }

        private static float3 ResolveAbyssalCurrent(float qualityWeight, uint frameIndex)
        {
            float q = math.saturate(qualityWeight);
            float t = (frameIndex & 1023u) * 0.0125f;
            return new float3(
                VerletCableSimdMath.SinPolynomial7(t * 0.73f) * math.lerp(0.025f, 0.14f, q),
                VerletCableSimdMath.CosPolynomial7(t * 0.37f) * math.lerp(0.01f, 0.06f, q),
                VerletCableSimdMath.SinPolynomial7(t * 0.51f + 1.7f) * math.lerp(0.025f, 0.12f, q));
        }

        private static float Smooth01(float value)
        {
            float q = math.saturate(math.isfinite(value) ? value : 1f);
            return q * q * (3f - 2f * q);
        }
    
        #region JulesLink_VerletCableSimulator
        private static void JulesLink_VerletCableSimulator() { _ = typeof(Hecton8.PureLogic.Systems.VerletCableSimulator); }
        #endregion

        #region JulesLink_CableConstraintSatisfier
        private static void JulesLink_CableConstraintSatisfier() { _ = typeof(Hecton8.PureLogic.Systems.CableConstraintSatisfier); }
        #endregion
}

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AdvanceMockCableEndpointsJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Nodes is the mutable endpoint lane and endpoint/pin NativeArrays provide bounded driver data. Raw pointer access
        // is gated by NodeCount and the fixed cable layout.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Updating endpoint Transforms or managed cable objects was rejected because the simulation is AUP/Burst-owned.
        // Duplicating node state was rejected because it creates rollback shadow state.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is single endpoint advancement before point simulation; this job is the only writer to Nodes in
        // that phase and its handle is chained forward.
        [NoAlias, NativeDisableUnsafePtrRestriction] public CableNodeDTO* Nodes;
        public int NodeCount;
        [NoAlias] public NativeArray<TetherEndpointAupDTO> Endpoints;
        [NoAlias] public NativeArray<double3> PinnedAUPs;
        [NoAlias] public NativeArray<byte> PinnedMask;
        public uint FrameIndex;
        public float GlobalQualityWeight;

        public void Execute()
        {
            int tetherCount = math.min(CablePhysics132Constants.MockTetherCount, Endpoints.IsCreated ? Endpoints.Length : 0);
            int nodesPerTether = CablePhysics132Constants.MockNodesPerTether;
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float time = (FrameIndex & 4095u) * 0.02f;
            for (int cable = 0; cable < tetherCount; cable++)
            {
                TetherEndpointAupDTO endpoint = Endpoints[cable];
                double3 anchor = SanitizeAup(endpoint.AnchorAUP, new double3(cable * 4.0, -24.0, cable * 2.25));
                double phase = time * (0.55 + cable * 0.05);
                double3 payload = anchor + new double3(
                    18.0 + VerletCableSimdMath.SinPolynomial7((float)phase) * math.lerp(0.25f, 1.8f, q),
                    -2.0 + VerletCableSimdMath.CosPolynomial7((float)(phase * 0.73)) * math.lerp(0.12f, 0.65f, q),
                    7.5 + VerletCableSimdMath.SinPolynomial7((float)(phase * 0.37 + 1.1)) * math.lerp(0.25f, 1.2f, q));
                endpoint.PayloadAUP = payload;
                endpoint.AbyssalCurrentAcceleration = new float3(
                    VerletCableSimdMath.SinPolynomial7(time * 0.71f + cable) * math.lerp(0.025f, 0.15f, q),
                    VerletCableSimdMath.CosPolynomial7(time * 0.49f + cable) * math.lerp(0.012f, 0.08f, q),
                    VerletCableSimdMath.SinPolynomial7(time * 0.61f + cable * 0.7f) * math.lerp(0.025f, 0.14f, q));
                endpoint.GlobalQualityWeight = q;
                Endpoints[cable] = endpoint;

                int first = cable * nodesPerTether;
                int last = first + nodesPerTether - 1;
                PinNode(first, anchor);
                PinNode(last, payload);
            }
        }

        private void PinNode(int index, double3 aup)
        {
            if ((uint)index >= (uint)NodeCount)
                return;

            ref CableNodeDTO node = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + index * VerletCableLayout.CableNodeStrideBytes);
            node.CurrentAUP = aup;
            node.PreviousAUP = aup;
            node.InverseMass = 0f;
            node.Flags |= CableNodeFlags132.Pinned | CableNodeFlags132.NetcodeFence;
            if (PinnedAUPs.IsCreated && index < PinnedAUPs.Length)
                PinnedAUPs[index] = aup;
            if (PinnedMask.IsCreated && index < PinnedMask.Length)
                PinnedMask[index] = 1;
        }

        private static double3 SanitizeAup(double3 value, double3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct SimulateCablePointsJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Nodes is a contiguous node lane partitioned by Execute index. Pin lanes are read-only NativeArrays and cannot
        // alias the raw node pointer.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Per-node MonoBehaviour simulation was rejected for scale and GC. A full node copy per iteration was rejected
        // because it doubles memory bandwidth in the hot Verlet loop.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one node row per worker index; no other job writes Nodes during this simulation handle.
        [NoAlias, NativeDisableUnsafePtrRestriction] public CableNodeDTO* Nodes;
        public int NodeCount;
        [ReadOnly, NoAlias] public NativeArray<double3> PinnedAUPs;
        [ReadOnly, NoAlias] public NativeArray<byte> PinnedMask;
        public float3 GravityAcceleration;
        public float3 AbyssalCurrentAcceleration;
        public float SimulationTickDelta;
        public float VelocityDamping;
        public float MaxStepMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)NodeCount)
                return;

            ref CableNodeDTO node = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + index * VerletCableLayout.CableNodeStrideBytes);
            bool pinned = node.InverseMass <= 0f ||
                          (node.Flags & CableNodeFlags132.Pinned) != 0u ||
                          (PinnedMask.IsCreated && index < PinnedMask.Length && PinnedMask[index] != 0);
            if (pinned)
            {
                double3 pinnedAup = PinnedAUPs.IsCreated && index < PinnedAUPs.Length
                    ? SanitizeAup(PinnedAUPs[index], node.CurrentAUP)
                    : SanitizeAup(node.CurrentAUP, double3.zero);
                node.CurrentAUP = pinnedAup;
                node.PreviousAUP = pinnedAup;
                node.InverseMass = 0f;
                node.Flags |= CableNodeFlags132.Pinned | CableNodeFlags132.NetcodeFence;
                return;
            }

            double3 current = SanitizeAup(node.CurrentAUP, node.PreviousAUP);
            double3 previous = SanitizeAup(node.PreviousAUP, current);
            uint flags = node.Flags & ~CableNodeFlags132.NonFiniteRecovered;
            if (!IsFinite(node.CurrentAUP) || !IsFinite(node.PreviousAUP))
                flags |= CableNodeFlags132.NonFiniteRecovered;

            float dt = math.clamp(SimulationTickDelta, 0.001f, 0.05f);
            float damping = math.clamp(VelocityDamping, 0.85f, 0.999f);
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            double3 velocity = (current - previous) * damping;
            double3 acceleration = new double3(
                GravityAcceleration.x + AbyssalCurrentAcceleration.x * math.lerp(0.35f, 1.0f, q),
                GravityAcceleration.y + AbyssalCurrentAcceleration.y * math.lerp(0.35f, 1.0f, q),
                GravityAcceleration.z + AbyssalCurrentAcceleration.z * math.lerp(0.35f, 1.0f, q));
            double3 step = velocity + acceleration * (dt * dt);
            double maxStep = math.max(0.01f, MaxStepMeters);
            double stepSq = math.lengthsq(step);
            if (!math.isfinite(stepSq) || stepSq > maxStep * maxStep)
            {
                step = IsFinite(step) && stepSq > 0.00000001
                    ? step * (maxStep * math.rsqrt(math.max(stepSq, 0.00000001)))
                    : double3.zero;
                flags |= CableNodeFlags132.NonFiniteRecovered;
            }

            node.PreviousAUP = current;
            node.CurrentAUP = current + step;
            node.Flags = flags | CableNodeFlags132.NetcodeFence;
        }

        private static double3 SanitizeAup(double3 value, double3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct SolveCableConstraintsJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Nodes is the mutable cable state lane and Constraints is read-only. The single constraint solve job owns node
        // mutation for its iteration window, so raw pointer safety cannot infer but code order guarantees exclusivity.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Parallel atomics on node corrections were rejected because they cause contention and nondeterministic order.
        // Duplicating node buffers for every iteration was rejected because it multiplies bandwidth.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is serialized constraint solve ownership: this IJob is the only writer to Nodes until it returns
        // the handle consumed by spline generation and telemetry.
        [NoAlias, NativeDisableUnsafePtrRestriction] public CableNodeDTO* Nodes;
        public int NodeCount;
        [ReadOnly, NoAlias] public NativeArray<TetherConstraintDTO> Constraints;
        [NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<PhysicsEventPayload> PhysicsEvents;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns the queue lane; this job is only a producer and never reads from the queue, so Unity's container
        // safety warning cannot model the externally configured single-lane producer contract.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Direct Rigidbody mutation and a managed event bridge were rejected because they break deterministic scheduling
        // or allocate. A local NativeArray mirror alone was rejected because Agent 81 needs the routed force payload.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The scheduler acquires one ParallelWriter for this solve pass, writes finite PhysicsEventPayload values during
        // the final constraint iteration only, and does not dispose or reconfigure the SignalBus lane while the handle lives.
        [NoAlias, NativeDisableContainerSafetyRestriction]
        public global::Hecton8.Core.MpscSignalRingBuffer<PhysicsEventPayload>.ParallelWriter PhysicsEventWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> PhysicsEventWriterBudget;
        public byte PhysicsEventWriterEnabled;
        public double3 CameraAUP;
        public int IterationCount;
        public float TensionForceScale;
        public float InvSnapTension;
        public uint FrameIndex;
        public float GlobalQualityWeight;

        public void Execute()
        {
            int iterations = math.clamp(IterationCount, 2, 15);
            int constraintCount = Constraints.IsCreated ? Constraints.Length : 0;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int constraintIndex = 0; constraintIndex < constraintCount; constraintIndex++)
                    SolveOne(constraintIndex, iteration == iterations - 1);
            }
        }

        private void SolveOne(int constraintIndex, bool emitForce)
        {
            TetherConstraintDTO constraint = Constraints[constraintIndex];
            if ((uint)constraint.NodeA >= (uint)NodeCount || (uint)constraint.NodeB >= (uint)NodeCount)
                return;

            ref CableNodeDTO nodeA = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + constraint.NodeA * VerletCableLayout.CableNodeStrideBytes);
            ref CableNodeDTO nodeB = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + constraint.NodeB * VerletCableLayout.CableNodeStrideBytes);
            double3 aupDelta = nodeB.CurrentAUP - nodeA.CurrentAUP;
            if (!ClampAupDelta(ref aupDelta))
            {
                nodeA.Flags |= CableNodeFlags132.ConstraintFault;
                nodeB.Flags |= CableNodeFlags132.ConstraintFault;
                return;
            }

            float3 delta = new float3((float)aupDelta.x, (float)aupDelta.y, (float)aupDelta.z);
            float lenSq = math.lengthsq(delta);
            if (!math.isfinite(lenSq) || lenSq <= VerletCableLayout.MinConstraintLengthSq)
            {
                nodeA.Flags |= CableNodeFlags132.ConstraintFault;
                nodeB.Flags |= CableNodeFlags132.ConstraintFault;
                return;
            }

            float invLen = math.rsqrt(math.max(lenSq, VerletCableLayout.MinConstraintLengthSq));
            float len = lenSq * invLen;
            float restLength = math.max(VerletCableLayout.MinConstraintLength, constraint.RestLength);
            float error = len - restLength;
            float invMassA = math.max(0f, nodeA.InverseMass);
            float invMassB = math.max(0f, nodeB.InverseMass);
            float invMassSum = invMassA + invMassB;
            if (invMassSum > 0f)
            {
                float stiffness = math.saturate(math.isfinite(constraint.Stiffness) ? constraint.Stiffness : 1f);
                float3 correction = delta * (error * invLen * stiffness / invMassSum);
                if (invMassA > 0f)
                    nodeA.CurrentAUP += new double3(correction.x * invMassA, correction.y * invMassA, correction.z * invMassA);
                if (invMassB > 0f)
                    nodeB.CurrentAUP -= new double3(correction.x * invMassB, correction.y * invMassB, correction.z * invMassB);
            }

            float tension = math.max(0f, error) * math.max(0f, TensionForceScale);
            if (SegmentTensions.IsCreated && constraintIndex < SegmentTensions.Length)
                SegmentTensions[constraintIndex] = tension;

            if (emitForce && tension > 0.0001f)
                EmitPhysicsEvent(constraintIndex, constraint, nodeA.CurrentAUP, nodeB.CurrentAUP, delta * invLen, tension);
        }

        private void EmitPhysicsEvent(int constraintIndex, TetherConstraintDTO constraint, double3 a, double3 b, float3 direction, float tension)
        {
            double3 midpointAup = (a + b) * 0.5;
            double3 local = midpointAup - CameraAUP;
            ClampAupDelta(ref local);
            Vector3 runtimePosition = new Vector3((float)local.x, (float)local.y, (float)local.z);
            Vector3 dir = new Vector3(direction.x, direction.y, direction.z);
            Vector3 force = new Vector3(direction.x * tension, direction.y * tension, direction.z * tension);
            PhysicsEventPayload payload = new PhysicsEventPayload
            {
                RuntimePosition = runtimePosition,
                Direction = dir,
                ForceVector = force,
                ImpulseVector = default,
                RadiusMeters = 0.25f,
                Scalar0 = tension,
                Scalar1 = math.saturate(tension * math.max(0f, InvSnapTension)),
                Scalar2 = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f),
                PrimaryId = (int)constraint.CableId,
                DataHash = FrameIndex,
                StatusBits = constraint.Flags | CableNodeFlags132.NetcodeFence | CableNodeFlags132.TetherTensionEvent,
                EventType = (ushort)PhysicsEventType.PressureImpulse,
                Reserved = 0
            };

            if (PhysicsEvents.IsCreated && constraintIndex < PhysicsEvents.Length)
                PhysicsEvents[constraintIndex] = payload;
            if (PhysicsEventWriterEnabled != 0 &&
                !SignalBus<PhysicsEventPayload>.TryEnqueueBounded(PhysicsEventWriter, PhysicsEventWriterBudget, payload))
            {
                payload.StatusBits |= CableNodeFlags132.SignalDrop;
                if (PhysicsEvents.IsCreated && constraintIndex < PhysicsEvents.Length)
                    PhysicsEvents[constraintIndex] = payload;
            }
        }

        private static bool ClampAupDelta(ref double3 delta)
        {
            if (!IsFinite(delta))
                return false;

            double span = CablePhysics132Constants.SafeLocalAupSpanMeters;
            delta = math.clamp(delta, new double3(-span), new double3(span));
            return true;
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearFrameCableOutputsJob : IJob
    {
        [NoAlias] public NativeArray<TetherSplineVertexDTO> Vertices;
        [NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<PhysicsEventPayload> PhysicsEvents;

        public void Execute()
        {
            for (int i = 0; i < Vertices.Length; i++)
                Vertices[i] = default;
            for (int i = 0; i < SegmentTensions.Length; i++)
                SegmentTensions[i] = 0f;
            for (int i = 0; i < PhysicsEvents.Length; i++)
                PhysicsEvents[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateSplineVerticesJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Nodes is read-only spline input lowered to a pointer for contiguous access. Vertex output is a separate NativeArray
        // lane with its own safety handle and index bounds.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rebuilding spline control points as managed lists was rejected for GC. Copying Nodes into a temporary read array
        // was rejected because it adds a full bandwidth pass before rendering.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is read-only node traversal in this stage; only Vertices[index] ranges are written by workers and
        // the caller fences the generated vertices before GPU upload.
        [NoAlias, NativeDisableUnsafePtrRestriction] public CableNodeDTO* Nodes;
        public int NodeCount;
        [ReadOnly, NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<TetherSplineVertexDTO> Vertices;
        public double3 CameraAUP;
        public int NodesPerCable;
        public int VerticesPerCable;
        public int CableCount;
        public int TotalVertexCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TotalVertexCount || NodesPerCable < 2 || VerticesPerCable < 2)
                return;

            int cable = index / VerticesPerCable;
            if ((uint)cable >= (uint)CableCount)
                return;

            int localIndex = index - cable * VerticesPerCable;
            int nodeOffset = cable * NodesPerCable;
            int constraintOffset = cable * (NodesPerCable - 1);
            int writeIndex = cable * VerticesPerCable + localIndex;
            if ((uint)writeIndex >= (uint)Vertices.Length || nodeOffset + NodesPerCable > NodeCount)
                return;

            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float invMaxVertex = math.rcp(math.max(1, VerticesPerCable - 1));
            float scaled = localIndex * invMaxVertex * (NodesPerCable - 1);
            int segment = math.clamp((int)math.floor(scaled), 0, NodesPerCable - 2);
            float t = math.saturate(scaled - segment);
            float3 p1 = ToCameraLocal(nodeOffset + segment);
            float3 p2 = ToCameraLocal(nodeOffset + segment + 1);
            float3 linear = math.lerp(p1, p2, t);
            float catmullWeight = Smooth01(q);
            float3 position = linear;
            if (catmullWeight > 0.0001f)
            {
                float3 p0 = ToCameraLocal(nodeOffset + math.max(0, segment - 1));
                float3 p3 = ToCameraLocal(nodeOffset + math.min(NodesPerCable - 1, segment + 2));
                position = math.lerp(linear, CatmullRom(p0, p1, p2, p3, t), catmullWeight);
            }

            float3 tangent = p2 - p1;
            float tangentSq = math.lengthsq(tangent);
            tangent = math.isfinite(tangentSq) && tangentSq > 0.000001f
                ? tangent * math.rsqrt(math.max(tangentSq, 0.000001f))
                : new float3(0f, 0f, 1f);

            int tensionIndex = constraintOffset + segment;
            float tension = SegmentTensions.IsCreated && tensionIndex < SegmentTensions.Length
                ? SegmentTensions[tensionIndex]
                : 0f;
            Vertices[writeIndex] = new TetherSplineVertexDTO
            {
                Position = position,
                U = localIndex * invMaxVertex,
                Tangent = tangent,
                Tension01 = math.saturate(tension / 18000f)
            };
        }

        private float3 ToCameraLocal(int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)NodeCount)
                return float3.zero;

            ref CableNodeDTO node = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + nodeIndex * VerletCableLayout.CableNodeStrideBytes);
            double3 delta = node.CurrentAUP - CameraAUP;
            double span = CablePhysics132Constants.SafeLocalAupSpanMeters;
            delta = math.clamp(delta, new double3(-span), new double3(span));
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float Smooth01(float value)
        {
            float q = math.saturate(value);
            return q * q * (3f - 2f * q);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CableSplineGpuMemcpyJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<TetherSplineVertexDTO> Source;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Destination is a GraphicsBuffer.LockBufferForWrite mapping owned by the upload ticket until UnlockBufferAfterWrite.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // SetData and managed staging were rejected because they add CPU stalls or heap traffic on the visual sync route.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The ticket finalizer unlocks only after this single copy job is observed complete or during forced teardown.
        [NoAlias, NativeDisableUnsafePtrRestriction] public TetherSplineVertexDTO* Destination;
        public int Count;
        public long DestinationBytes;

        public void Execute()
        {
            int count = math.min(Count, Source.IsCreated ? Source.Length : 0);
            if (count <= 0 || Destination == null)
                return;

            int elementBytes = VerletCableLayout.TetherSplineVertexStrideBytes;
            if (elementBytes <= 0)
                return;

            long destinationBytes = DestinationBytes > 0L ? DestinationBytes : 0L;
            long destinationCountLong = destinationBytes / elementBytes;
            int destinationCount = destinationCountLong > int.MaxValue ? int.MaxValue : (int)destinationCountLong;
            int copyCount = math.min(count, destinationCount);
            if (copyCount <= 0)
                return;

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            long copyBytes = (long)copyCount * elementBytes;
            if (copyBytes <= 0L)
                return;

            UnsafeUtility.MemCpy(
                Destination,
                source,
                copyBytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CableSplineIndirectArgsJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Destination is a one-row indirect-args mapping created by LockBufferForWrite and never shared with another writer.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // CPU SetData and managed uint[] staging were rejected because this path must stay deterministic and allocation-free.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The upload ticket is the sole owner of the mapping and unlocks only after this job's handle has completed.
        [NoAlias, NativeDisableUnsafePtrRestriction] public TetherSplineIndirectArgsDTO* Destination;
        public uint VertexCountPerInstance;
        public uint InstanceCount;

        public void Execute()
        {
            if (Destination == null)
                return;

            Destination[0] = new TetherSplineIndirectArgsDTO
            {
                VertexCountPerInstance = VertexCountPerInstance,
                InstanceCount = InstanceCount == 0u ? 1u : InstanceCount,
                StartVertex = 0u,
                StartInstance = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct RecordCableTetherTelemetryJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public CableNodeDTO* Nodes;
        public int NodeCount;
        [ReadOnly, NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryHead;
        public uint FrameIndex;
        public int IterationCount;
        public float CpuMicroseconds;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int capacity = math.min(CablePhysics132Constants.TelemetryCapacity, TelemetryRing.Length);
            int head = 0;
            if (TelemetryHead.IsCreated && TelemetryHead.Length > 0)
            {
                head = TelemetryHead[0];
                TelemetryHead[0] = (head + 1) % capacity;
            }

            float maxTension = 0f;
            uint flags = 0u;
            uint hash = 2166136261u;
            double3 anchor = double3.zero;
            int nodeLimit = math.min(NodeCount, CablePhysics132Constants.MockNodeCapacity);
            for (int i = 0; i < nodeLimit; i++)
            {
                ref CableNodeDTO node = ref UnsafeUtility.AsRef<CableNodeDTO>((byte*)Nodes + i * VerletCableLayout.CableNodeStrideBytes);
                flags |= node.Flags & (CableNodeFlags132.NonFiniteRecovered | CableNodeFlags132.ConstraintFault | CableNodeFlags132.NetcodeFence);
                if (i == 0)
                    anchor = node.CurrentAUP;
                hash = HashDouble3(hash, node.CurrentAUP);
                hash = (hash ^ node.Flags) * 16777619u;
            }

            for (int i = 0; i < SegmentTensions.Length; i++)
                maxTension = math.max(maxTension, SegmentTensions[i]);

            TelemetryRing[head % capacity] = new TetherTelemetryEntry
            {
                FrameIndex = FrameIndex,
                NodeCount = nodeLimit,
                IterationCount = math.clamp(IterationCount, 2, 15),
                MaxTension = maxTension,
                AnchorAUP = anchor,
                StateHash = hash,
                Flags = flags,
                CpuMicroseconds = math.max(0f, CpuMicroseconds),
                GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f)
            };
        }

        private static uint HashDouble3(uint hash, double3 value)
        {
            long x = (long)math.round(value.x * 1000.0);
            long y = (long)math.round(value.y * 1000.0);
            long z = (long)math.round(value.z * 1000.0);
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)(x >> 32)) * 16777619u;
            hash = (hash ^ (uint)y) * 16777619u;
            hash = (hash ^ (uint)(y >> 32)) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            hash = (hash ^ (uint)(z >> 32)) * 16777619u;
            return hash;
        }
    }
}
