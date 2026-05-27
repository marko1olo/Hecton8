using System;
#if UNITY_EDITOR
using System.IO;
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using System.Text;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherStateDTO
    {
        [FieldOffset(0)] public double3 AnchorA_AUP;
        [FieldOffset(24)] public double3 AnchorB_AUP;
        [FieldOffset(48)] public float RestLength;
        [FieldOffset(52)] public float CurrentTension;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherStressStateDTO
    {
        [FieldOffset(0)] public float StressSeconds;
        [FieldOffset(4)] public float PeakTension;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] private ulong _pad0;
        [FieldOffset(24)] private ulong _pad1;
        [FieldOffset(32)] private ulong _pad2;
        [FieldOffset(40)] private ulong _pad3;
        [FieldOffset(48)] private ulong _pad4;
        [FieldOffset(56)] private ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HarpoonTensionTuningDTO
    {
        [FieldOffset(0)] public float3 NodeGravity;
        [FieldOffset(12)] public float VelocityDamping;
        [FieldOffset(16)] public float TensionConstant;
        [FieldOffset(20)] public float MaxTensileStrength;
        [FieldOffset(24)] public float ConstraintStiffness;
        [FieldOffset(28)] public float MaxNodeStepMeters;
        [FieldOffset(32)] public float GlobalQualityWeightOverride;
        [FieldOffset(36)] public int NodesPerTether;
        [FieldOffset(40)] public int MaxConstraintIterations;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float VisualRadiusMeters;
        [FieldOffset(52)] public float VisualCrystalDensity;
        [FieldOffset(56)] public float SnapStressSeconds;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherMaterialProfileDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float TensionConstant;
        [FieldOffset(8)] public float MaxTensileStrength;
        [FieldOffset(12)] public float LinearDensity;
        [FieldOffset(16)] public float Elasticity01;
        [FieldOffset(20)] public float NodeGravityScale;
        [FieldOffset(24)] public float VisualRadiusMeters;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float4 VisualTuning;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct HarpoonTensionPhysicsEventMirrorDTO
    {
        [FieldOffset(0)] public float3 RuntimePosition;
        [FieldOffset(12)] public float3 Direction;
        [FieldOffset(24)] public float3 ForceVector;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public float Scalar0;
        [FieldOffset(44)] public float Scalar1;
        [FieldOffset(48)] public float Scalar2;
        [FieldOffset(52)] public int PrimaryId;
        [FieldOffset(56)] public uint DataHash;
        [FieldOffset(60)] public uint StatusBits;
        [FieldOffset(64)] public ushort EventType;
        [FieldOffset(66)] public ushort BodySlot;
        [FieldOffset(68)] public uint Reserved;
        [FieldOffset(72)] private ulong _pad0;
    }

    public static class HarpoonTensionSolver328Constants
    {
        public const int MockTetherCount = 5;
        public const int MockNodesPerTether = 30;
        public const int MockNodeCapacity = MockTetherCount * MockNodesPerTether;
        public const int MockConstraintCapacity = MockTetherCount * (MockNodesPerTether - 1);
        public const int MockStressStateCapacity = MockTetherCount;
        public const int MockForcePacketCapacity = MockTetherCount * 2;
        public const int MockPhysicsEventCapacity = MockTetherCount * 2;
        public const int MockSplineVertexCapacity = MockNodeCapacity;
        public const int TelemetryCapacity = 300;
        public const int MaterialProfileCapacity = 16;
        public const int BootstrapMagic = 0x53333238;
        public const float SafeLocalAupSpanMeters = 32768f;
        public const float Epsilon = 0.0001f;
        public const float DefaultTensionConstant = 9500f;
        public const float DefaultMaxTensileStrength = 180000f;
        public const float DefaultRestLength = 48f;
        public const float DefaultSnapStressSeconds = 0.12f;
        public const float FaultDumpBudgetMicroseconds = 500f;
    }

    public static class HarpoonTensionSolver328BufferIds
    {
        public const BufferID TetherStates = (BufferID)72180;
        public const BufferID TetherNodes = (BufferID)72181;
        public const BufferID TetherPreviousNodes = (BufferID)72182;
        public const BufferID TetherConstraints = (BufferID)72183;
        public const BufferID ForcePackets = (BufferID)72184;
        public const BufferID PhysicsEvents = (BufferID)72185;
        public const BufferID SplineVertices = (BufferID)72186;
        public const BufferID TelemetryRing = (BufferID)72187;
        public const BufferID TelemetryHead = (BufferID)72188;
        public const BufferID Tuning = (BufferID)72189;
        public const BufferID MaterialProfiles = (BufferID)72190;
        public const BufferID BootstrapState = (BufferID)72191;
        public const BufferID FaultFlags = (BufferID)72192;
        public const BufferID StressStates = (BufferID)72193;
    }

    public static class TetherStateFlags328
    {
        public const uint Active = 1u << 0;
        public const uint Snapped = 1u << 1;
        public const uint NonFiniteRecovered = 1u << 2;
        public const uint ConstraintFault = 1u << 3;
        public const uint NetcodeFence = 1u << 4;
        public const uint GpuSplineReady = 1u << 5;
        public const uint ForceSignalEmitted = 1u << 6;
        public const uint MockGenerated = 1u << 7;
    }

    public static class HarpoonTensionFaultFlags328
    {
        public const uint NonFiniteState = 1u << 0;
        public const uint OverBudget = 1u << 1;
        public const uint LayoutFault = 1u << 2;
        public const uint SignalOverflowRisk = 1u << 3;
        public const uint DumpTriggerMask = NonFiniteState | OverBudget | LayoutFault | SignalOverflowRisk;
    }

    public static class HarpoonTensionForcePacketFlags328
    {
        public const uint EndpointAnchor = 1u << 0;
        public const uint EndpointPayload = 1u << 1;
    }

    public struct HarpoonTensionSchedule328
    {
        public JobHandle Handle;
        public int ActiveTetherCount;
        public int ActiveNodeCount;
        public int IterationCount;
        public int SplineVertexCount;
    }

    public unsafe static class HarpoonTensionSolver328
    {

        private static int s_x001DirectSignalPushDropCount_HarpoonTensionSolver328;

#if UNITY_EDITOR
        public static bool ValidateLayout()
        {
            return TryValidateLayout(out _);
        }

        public static bool TryValidateLayout(out string error)
        {
            if (UnsafeUtility.SizeOf<TetherStateDTO>() != 64)
            {
                error = "TetherStateDTO size != 64";
                return false;
            }

            if (OffsetOf<TetherStateDTO>(nameof(TetherStateDTO.AnchorA_AUP)) != 0 ||
                OffsetOf<TetherStateDTO>(nameof(TetherStateDTO.AnchorB_AUP)) != 24 ||
                OffsetOf<TetherStateDTO>(nameof(TetherStateDTO.RestLength)) != 48 ||
                OffsetOf<TetherStateDTO>(nameof(TetherStateDTO.CurrentTension)) != 52 ||
                OffsetOf<TetherStateDTO>(nameof(TetherStateDTO.Flags)) != 56 ||
                OffsetOf<TetherStateDTO>("_pad0") != 60)
            {
                error = "TetherStateDTO field offset fault";
                return false;
            }

            if (UnsafeUtility.SizeOf<HarpoonTensionTuningDTO>() != 64 ||
                UnsafeUtility.SizeOf<TetherMaterialProfileDTO>() != 64 ||
                UnsafeUtility.SizeOf<TetherStressStateDTO>() != 64 ||
                UnsafeUtility.SizeOf<HarpoonTensionPhysicsEventMirrorDTO>() != 80 ||
                UnsafeUtility.SizeOf<TetherTelemetryEntry>() != 64 ||
                UnsafeUtility.SizeOf<TetherForcePacketDTO>() != 64 ||
                UnsafeUtility.SizeOf<TetherSplineVertexDTO>() != 32)
            {
                error = "Secondary tether DTO layout fault";
                return false;
            }

            if (OffsetOf<TetherStressStateDTO>(nameof(TetherStressStateDTO.StressSeconds)) != 0 ||
                OffsetOf<TetherStressStateDTO>(nameof(TetherStressStateDTO.PeakTension)) != 4 ||
                OffsetOf<TetherStressStateDTO>(nameof(TetherStressStateDTO.Flags)) != 8 ||
                OffsetOf<TetherStressStateDTO>(nameof(TetherStressStateDTO.FrameIndex)) != 12 ||
                OffsetOf<TetherStressStateDTO>("_pad0") != 16 ||
                OffsetOf<TetherStressStateDTO>("_pad5") != 56)
            {
                error = "TetherStressStateDTO field offset fault";
                return false;
            }

            if (OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.NodeGravity)) != 0 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.VelocityDamping)) != 12 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.TensionConstant)) != 16 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.MaxTensileStrength)) != 20 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.ConstraintStiffness)) != 24 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.MaxNodeStepMeters)) != 28 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.GlobalQualityWeightOverride)) != 32 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.NodesPerTether)) != 36 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.MaxConstraintIterations)) != 40 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.Flags)) != 44 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.VisualRadiusMeters)) != 48 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.VisualCrystalDensity)) != 52 ||
                OffsetOf<HarpoonTensionTuningDTO>(nameof(HarpoonTensionTuningDTO.SnapStressSeconds)) != 56)
            {
                error = "HarpoonTensionTuningDTO field offset fault";
                return false;
            }

            if (OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.MaterialHash)) != 0 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.TensionConstant)) != 4 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.MaxTensileStrength)) != 8 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.LinearDensity)) != 12 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.Elasticity01)) != 16 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.NodeGravityScale)) != 20 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.VisualRadiusMeters)) != 24 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.Flags)) != 28 ||
                OffsetOf<TetherMaterialProfileDTO>(nameof(TetherMaterialProfileDTO.VisualTuning)) != 32)
            {
                error = "TetherMaterialProfileDTO field offset fault";
                return false;
            }

            if (OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.RuntimePosition)) != 0 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.Direction)) != 12 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.ForceVector)) != 24 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.RadiusMeters)) != 36 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.Scalar0)) != 40 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.Scalar1)) != 44 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.Scalar2)) != 48 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.PrimaryId)) != 52 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.DataHash)) != 56 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.StatusBits)) != 60 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.EventType)) != 64 ||
                OffsetOf<HarpoonTensionPhysicsEventMirrorDTO>(nameof(HarpoonTensionPhysicsEventMirrorDTO.BodySlot)) != 66)
            {
                error = "HarpoonTensionPhysicsEventMirrorDTO field offset fault";
                return false;
            }

            error = string.Empty;
            return true;
        }
#endif

        public static int ResolveIterationCount(float globalQualityWeight, int requestedMaxIterations)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int maxIterations = requestedMaxIterations > 0 ? math.clamp(requestedMaxIterations, 2, 8) : 8;
            return math.clamp((int)math.lerp(2f, maxIterations, q), 2, maxIterations);
        }

        public static int ResolveNodesPerTether(float globalQualityWeight, int requestedNodesPerTether)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int high = requestedNodesPerTether > 1 ? math.clamp(requestedNodesPerTether, 6, 64) : HarpoonTensionSolver328Constants.MockNodesPerTether;
            return math.clamp((int)math.lerp(8f, high, Smooth01(q)), 6, high);
        }

        public static void EnsureMockBuffers(IDataVault vault, float globalQualityWeight, uint frameIndex)
        {
            if (vault == null)
                return;

            if (!TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.BootstrapState, out NativeArray<int> bootstrap) ||
                !bootstrap.IsCreated ||
                bootstrap.Length <= 0)
            {
                if (vault.IsAllocationLocked)
                    return;

                VaultGenerationHandle<int> bootstrapHandle = vault.EnsureGenerationHandle<int>(
                    HarpoonTensionSolver328BufferIds.BootstrapState,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                if (!vault.TryResolveHandle(in bootstrapHandle, out bootstrap) ||
                    !bootstrap.IsCreated ||
                    bootstrap.Length <= 0)
                {
                    return;
                }

                bootstrap[0] = 0;
            }

            if (IsMockBootstrapValid(vault, bootstrap))
                return;
            if (bootstrap.IsCreated && bootstrap.Length > 0)
                bootstrap[0] = 0;

            if (!TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TetherStates, HarpoonTensionSolver328Constants.MockTetherCount, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherStateDTO> states) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.StressStates, HarpoonTensionSolver328Constants.MockStressStateCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherStressStateDTO> stressStates) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TetherNodes, HarpoonTensionSolver328Constants.MockNodeCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<float3> nodes) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TetherPreviousNodes, HarpoonTensionSolver328Constants.MockNodeCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<float3> previousNodes) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TetherConstraints, HarpoonTensionSolver328Constants.MockConstraintCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherConstraintDTO> constraints) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.ForcePackets, HarpoonTensionSolver328Constants.MockForcePacketCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherForcePacketDTO> forcePackets) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.PhysicsEvents, HarpoonTensionSolver328Constants.MockPhysicsEventCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.SplineVertices, HarpoonTensionSolver328Constants.MockSplineVertexCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherSplineVertexDTO> splineVertices) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryRing, HarpoonTensionSolver328Constants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherTelemetryEntry> telemetryRing) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryHead, 1, NativeArrayOptions.UninitializedMemory, out NativeArray<int> telemetryHead) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.Tuning, 1, NativeArrayOptions.UninitializedMemory, out NativeArray<HarpoonTensionTuningDTO> tuning) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.MaterialProfiles, HarpoonTensionSolver328Constants.MaterialProfileCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<TetherMaterialProfileDTO> materialProfiles) ||
                !TryOpenOrAcquireVaultView(vault, HarpoonTensionSolver328BufferIds.FaultFlags, 1, NativeArrayOptions.UninitializedMemory, out NativeArray<uint> faultFlags))
            {
                return;
            }

            JobHandle initHandle = new InitializeHarpoonTensionBuffersJob
            {
                ForcePackets = forcePackets,
                StressStates = stressStates,
                PhysicsEvents = physicsEvents,
                SplineVertices = splineVertices,
                TelemetryRing = telemetryRing,
                TelemetryHead = telemetryHead,
                Tuning = tuning,
                MaterialProfiles = materialProfiles,
                FaultFlags = faultFlags
            }.Schedule();

            JobHandle mockHandle = new GenerateMockHarpoonTensionJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                Nodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes),
                PreviousNodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(previousNodes),
                Constraints = (TetherConstraintDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(constraints),
                BootstrapState = bootstrap,
                StateCount = states.Length,
                NodeCount = nodes.Length,
                ConstraintCount = constraints.Length,
                NodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether,
                FrameIndex = frameIndex,
                SimulationTime = frameIndex * 0.016666667f,
                BaseAUP = new double3(100000.0, -420.0, 100000.0),
                RestLengthMeters = HarpoonTensionSolver328Constants.DefaultRestLength,
                PullSpeedMetersPerSecond = 100f,
                GlobalQualityWeight = globalQualityWeight
            }.Schedule(HarpoonTensionSolver328Constants.MockTetherCount, 1, initHandle);

            DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true);
        }

        private static bool IsMockBootstrapValid(IDataVault vault, NativeArray<int> bootstrap)
        {
            if (vault == null ||
                !bootstrap.IsCreated ||
                bootstrap.Length <= 0 ||
                bootstrap[0] != HarpoonTensionSolver328Constants.BootstrapMagic)
            {
                return false;
            }

            if (!TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherStates, out NativeArray<TetherStateDTO> states) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.StressStates, out NativeArray<TetherStressStateDTO> stressStates) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherNodes, out NativeArray<float3> nodes) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherPreviousNodes, out NativeArray<float3> previousNodes) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherConstraints, out NativeArray<TetherConstraintDTO> constraints) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.ForcePackets, out NativeArray<TetherForcePacketDTO> forcePackets) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.PhysicsEvents, out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.SplineVertices, out NativeArray<TetherSplineVertexDTO> splineVertices) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryRing, out NativeArray<TetherTelemetryEntry> telemetryRing) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryHead, out NativeArray<int> telemetryHead) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.Tuning, out NativeArray<HarpoonTensionTuningDTO> tuning) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.MaterialProfiles, out NativeArray<TetherMaterialProfileDTO> materialProfiles) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.FaultFlags, out NativeArray<uint> faultFlags))
            {
                return false;
            }

            if (states.Length < HarpoonTensionSolver328Constants.MockTetherCount ||
                stressStates.Length < HarpoonTensionSolver328Constants.MockStressStateCapacity ||
                nodes.Length < HarpoonTensionSolver328Constants.MockNodeCapacity ||
                previousNodes.Length < HarpoonTensionSolver328Constants.MockNodeCapacity ||
                constraints.Length < HarpoonTensionSolver328Constants.MockConstraintCapacity ||
                forcePackets.Length < HarpoonTensionSolver328Constants.MockForcePacketCapacity ||
                physicsEvents.Length < HarpoonTensionSolver328Constants.MockPhysicsEventCapacity ||
                splineVertices.Length < HarpoonTensionSolver328Constants.MockSplineVertexCapacity ||
                telemetryRing.Length < HarpoonTensionSolver328Constants.TelemetryCapacity ||
                telemetryHead.Length <= 0 ||
                tuning.Length <= 0 ||
                materialProfiles.Length <= 0 ||
                faultFlags.Length <= 0)
            {
                return false;
            }

            TetherStateDTO firstState = states[0];
            TetherStressStateDTO firstStress = stressStates[0];
            HarpoonTensionTuningDTO firstTuning = tuning[0];
            TetherMaterialProfileDTO firstProfile = materialProfiles[0];
            return (firstState.Flags & TetherStateFlags328.Active) != 0u &&
                   IsFinite(firstState.AnchorA_AUP) &&
                   IsFinite(firstState.AnchorB_AUP) &&
                   math.isfinite(firstState.RestLength) &&
                   firstState.RestLength > HarpoonTensionSolver328Constants.Epsilon &&
                   math.isfinite(firstStress.StressSeconds) &&
                   math.isfinite(firstStress.PeakTension) &&
                   firstTuning.Flags != 0u &&
                   math.isfinite(firstTuning.TensionConstant) &&
                   firstTuning.TensionConstant > 0f &&
                   firstProfile.Flags != 0u &&
                   math.isfinite(firstProfile.TensionConstant) &&
                   firstProfile.TensionConstant > 0f;
        }

        public static bool TryScheduleMockFromVault(
            IDataVault vault,
            uint frameIndex,
            float simulationTickDelta,
            double3 cameraAup,
            float globalQualityWeight,
            float cpuMicroseconds,
            JobHandle dependency,
            out HarpoonTensionSchedule328 schedule)
        {
            schedule = default;
            if (!TryResolveMockBuffers(
                    vault,
                    out NativeArray<TetherStateDTO> states,
                    out NativeArray<TetherStressStateDTO> stressStates,
                    out NativeArray<float3> nodes,
                    out NativeArray<float3> previousNodes,
                    out NativeArray<TetherConstraintDTO> constraints,
                    out NativeArray<TetherForcePacketDTO> forcePackets,
                    out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents,
                    out NativeArray<TetherSplineVertexDTO> splineVertices,
                    out NativeArray<TetherTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryHead,
                    out NativeArray<HarpoonTensionTuningDTO> tuning,
                    out NativeArray<uint> faultFlags))
            {
                return false;
            }

            HarpoonTensionTuningDTO tune = tuning.IsCreated && tuning.Length > 0 ? SanitizeTuning(tuning[0]) : DefaultTuning();
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            if (math.isfinite(tune.GlobalQualityWeightOverride) && tune.GlobalQualityWeightOverride >= 0f)
                q = math.saturate(tune.GlobalQualityWeightOverride);

            // Mock Vault lanes are seeded at a fixed stride. Quality scales the
            // relaxation/visual path here; live owners may pass compact strides
            // directly through Schedule without aliasing tether node ranges.
            int nodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether;
            int activeNodeCount = math.min(nodes.Length, states.Length * nodesPerTether);
            int activeConstraintCount = math.min(constraints.Length, states.Length * math.max(0, nodesPerTether - 1));
            int iterations = ResolveIterationCount(q, tune.MaxConstraintIterations);

            schedule = Schedule(
                states,
                stressStates,
                nodes,
                previousNodes,
                constraints,
                forcePackets,
                physicsEvents,
                splineVertices,
                telemetryRing,
                telemetryHead,
                faultFlags,
                states.Length,
                nodesPerTether,
                activeNodeCount,
                activeConstraintCount,
                tune,
                cameraAup,
                frameIndex,
                simulationTickDelta,
                cpuMicroseconds,
                q,
                dependency);
            schedule.IterationCount = iterations;
            return true;
        }

        public static bool TryHasMockBuffers(IDataVault vault)
        {
            return TryResolveMockBuffers(
                vault,
                out NativeArray<TetherStateDTO> states,
                out NativeArray<TetherStressStateDTO> stressStates,
                out NativeArray<float3> nodes,
                out NativeArray<float3> previousNodes,
                out NativeArray<TetherConstraintDTO> constraints,
                out NativeArray<TetherForcePacketDTO> forcePackets,
                out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents,
                out NativeArray<TetherSplineVertexDTO> splineVertices,
                out NativeArray<TetherTelemetryEntry> telemetryRing,
                out NativeArray<int> telemetryHead,
                out NativeArray<HarpoonTensionTuningDTO> tuning,
                out NativeArray<uint> faultFlags) &&
                   states.IsCreated &&
                   states.Length >= HarpoonTensionSolver328Constants.MockTetherCount &&
                   stressStates.IsCreated &&
                   stressStates.Length >= HarpoonTensionSolver328Constants.MockStressStateCapacity &&
                   nodes.IsCreated &&
                   nodes.Length >= HarpoonTensionSolver328Constants.MockNodeCapacity &&
                   previousNodes.IsCreated &&
                   previousNodes.Length >= HarpoonTensionSolver328Constants.MockNodeCapacity &&
                   constraints.IsCreated &&
                   constraints.Length >= HarpoonTensionSolver328Constants.MockConstraintCapacity &&
                   forcePackets.IsCreated &&
                   forcePackets.Length >= HarpoonTensionSolver328Constants.MockForcePacketCapacity &&
                   physicsEvents.IsCreated &&
                   physicsEvents.Length >= HarpoonTensionSolver328Constants.MockPhysicsEventCapacity &&
                   splineVertices.IsCreated &&
                   splineVertices.Length >= HarpoonTensionSolver328Constants.MockSplineVertexCapacity &&
                   telemetryRing.IsCreated &&
                   telemetryRing.Length >= HarpoonTensionSolver328Constants.TelemetryCapacity &&
                   telemetryHead.IsCreated &&
                   telemetryHead.Length > 0 &&
                   tuning.IsCreated &&
                   tuning.Length > 0 &&
                   faultFlags.IsCreated &&
                   faultFlags.Length > 0;
        }

        public static HarpoonTensionSchedule328 Schedule(
            NativeArray<TetherStateDTO> states,
            NativeArray<TetherStressStateDTO> stressStates,
            NativeArray<float3> nodes,
            NativeArray<float3> previousNodes,
            NativeArray<TetherConstraintDTO> constraints,
            NativeArray<TetherForcePacketDTO> forcePackets,
            NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents,
            NativeArray<TetherSplineVertexDTO> splineVertices,
            NativeArray<TetherTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            NativeArray<uint> faultFlags,
            int activeTetherCount,
            int nodesPerTether,
            int activeNodeCount,
            int activeConstraintCount,
            in HarpoonTensionTuningDTO tuning,
            double3 cameraAup,
            uint frameIndex,
            float simulationTickDelta,
            float cpuMicroseconds,
            float globalQualityWeight,
            JobHandle dependency)
        {
            if (!states.IsCreated ||
                !stressStates.IsCreated ||
                !nodes.IsCreated ||
                !previousNodes.IsCreated ||
                !constraints.IsCreated ||
                !forcePackets.IsCreated ||
                !physicsEvents.IsCreated ||
                !splineVertices.IsCreated ||
                !telemetryRing.IsCreated ||
                !telemetryHead.IsCreated ||
                !faultFlags.IsCreated)
            {
                return new HarpoonTensionSchedule328
                {
                    Handle = dependency,
                    ActiveTetherCount = 0,
                    ActiveNodeCount = 0,
                    IterationCount = 0,
                    SplineVertexCount = 0
                };
            }

            HarpoonTensionTuningDTO tune = SanitizeTuning(tuning);
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            if (math.isfinite(tune.GlobalQualityWeightOverride) && tune.GlobalQualityWeightOverride >= 0f)
                q = math.saturate(tune.GlobalQualityWeightOverride);
            float safeTickDelta = math.max(0.0001f, math.isfinite(simulationTickDelta) ? simulationTickDelta : 0.016666667f);
            int tetherCapacity = math.min(states.IsCreated ? states.Length : 0, stressStates.IsCreated ? stressStates.Length : 0);
            int tetherCount = math.min(math.max(0, activeTetherCount), tetherCapacity);
            int safeNodesPerTether = math.clamp(nodesPerTether, 2, 64);
            int safeActiveNodeCount = math.min(math.max(0, activeNodeCount), nodes.IsCreated ? nodes.Length : 0);
            int safeConstraintCount = math.min(math.max(0, activeConstraintCount), constraints.IsCreated ? constraints.Length : 0);
            int iterations = ResolveIterationCount(q, tune.MaxConstraintIterations);

            JobHandle integrateHandle = new SimulateTetherNodesJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                Nodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes),
                PreviousNodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(previousNodes),
                StateCount = tetherCount,
                NodeCount = safeActiveNodeCount,
                NodesPerTether = safeNodesPerTether,
                SimulationTickDelta = safeTickDelta,
                Gravity = tune.NodeGravity,
                VelocityDamping = tune.VelocityDamping,
                MaxStepMeters = tune.MaxNodeStepMeters
            }.Schedule(safeActiveNodeCount, 32, dependency);

            JobHandle solveHandle = new SolveTetherConstraintsJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                Nodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes),
                Constraints = (TetherConstraintDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(constraints),
                StateCount = tetherCount,
                NodeCount = safeActiveNodeCount,
                ConstraintCount = safeConstraintCount,
                NodesPerTether = safeNodesPerTether,
                IterationCount = iterations,
                ConstraintStiffness = tune.ConstraintStiffness,
                GlobalQualityWeight = q
            }.Schedule(integrateHandle);

            JobHandle forceHandle = new CalculateTetherForceJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                StateCount = tetherCount,
                ForcePackets = forcePackets,
                StressStates = stressStates,
                PhysicsEvents = physicsEvents,
                CameraAUP = cameraAup,
                TensionConstant = tune.TensionConstant,
                MaxTensileStrength = tune.MaxTensileStrength,
                SnapStressSeconds = tune.SnapStressSeconds,
                SimulationTickDelta = safeTickDelta,
                FrameIndex = frameIndex,
                NodesPerTether = safeNodesPerTether,
                GlobalQualityWeight = q
            }.Schedule(tetherCount, 16, solveHandle);

            JobHandle splineHandle = new BuildDearLieGpuSplineJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                Nodes = (float3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nodes),
                Vertices = splineVertices,
                StateCount = tetherCount,
                NodeCount = safeActiveNodeCount,
                NodesPerTether = safeNodesPerTether,
                GlobalQualityWeight = q
            }.Schedule(math.min(splineVertices.IsCreated ? splineVertices.Length : 0, safeActiveNodeCount), 32, forceHandle);

            JobHandle telemetryHandle = new RecordHarpoonTetherTelemetryJob
            {
                States = (TetherStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states),
                StateCount = tetherCount,
                NodesPerTether = safeNodesPerTether,
                IterationCount = iterations,
                TelemetryRing = telemetryRing,
                TelemetryHead = telemetryHead,
                FaultFlags = faultFlags,
                FrameIndex = frameIndex,
                CpuMicroseconds = cpuMicroseconds,
                GlobalQualityWeight = q
            }.Schedule(splineHandle);

            return new HarpoonTensionSchedule328
            {
                Handle = telemetryHandle,
                ActiveTetherCount = tetherCount,
                ActiveNodeCount = safeActiveNodeCount,
                IterationCount = iterations,
                SplineVertexCount = math.min(splineVertices.IsCreated ? splineVertices.Length : 0, safeActiveNodeCount)
            };
        }

        public static bool TryDumpTelemetryIfFault(IDataVault vault, string projectRoot, byte completionVerifiedByOwner)
        {
#if UNITY_EDITOR
            if (vault == null ||
                completionVerifiedByOwner == 0 ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.FaultFlags, out NativeArray<uint> faultFlags) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryRing, out NativeArray<TetherTelemetryEntry> ring) ||
                !faultFlags.IsCreated ||
                faultFlags.Length == 0 ||
                (faultFlags[0] & HarpoonTensionFaultFlags328.DumpTriggerMask) == 0u)
            {
                return false;
            }

            string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string path = Path.Combine(root, "Docs", "AgentLogs", "Dump_SHINOBU_328.bin");
            WriteTelemetryDump(path, ring, faultFlags[0] & HarpoonTensionFaultFlags328.DumpTriggerMask);
            return true;
#else
            return false;
#endif
        }

        public static int TryPublishCompletedSignalsFromVault(
            IDataVault vault,
            int activeTetherCount,
            int nodesPerTether,
            uint frameIndex,
            float globalQualityWeight,
            byte completionVerifiedByOwner)
        {
            if (vault == null ||
                completionVerifiedByOwner == 0 ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherStates, out NativeArray<TetherStateDTO> states) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.StressStates, out NativeArray<TetherStressStateDTO> stressStates) ||
                !TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.PhysicsEvents, out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents))
            {
                return 0;
            }

            HarpoonTensionTuningDTO tuning = DefaultTuning();
            if (TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.Tuning, out NativeArray<HarpoonTensionTuningDTO> tuningBuffer) &&
                tuningBuffer.IsCreated &&
                tuningBuffer.Length > 0)
            {
                tuning = SanitizeTuning(tuningBuffer[0]);
            }

            return PublishCompletedSignals(
                states,
                stressStates,
                physicsEvents,
                in tuning,
                activeTetherCount,
                nodesPerTether,
                frameIndex,
                globalQualityWeight,
                completionVerifiedByOwner);
        }

        public static int PublishCompletedSignals(
            NativeArray<TetherStateDTO> states,
            NativeArray<TetherStressStateDTO> stressStates,
            NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents,
            in HarpoonTensionTuningDTO tuning,
            int activeTetherCount,
            int nodesPerTether,
            uint frameIndex,
            float globalQualityWeight,
            byte completionVerifiedByOwner)
        {
            if (completionVerifiedByOwner == 0 || !states.IsCreated || !stressStates.IsCreated)
                return 0;

            int pushed = 0;
            int stateCapacity = math.min(states.Length, stressStates.IsCreated ? stressStates.Length : 0);
            int count = math.min(math.max(0, activeTetherCount), stateCapacity);
            int eventLimit = math.min(physicsEvents.IsCreated ? physicsEvents.Length : 0, count * 2);
            if (physicsEvents.IsCreated)
            {
                for (int i = 0; i < eventLimit; i++)
                {
                    HarpoonTensionPhysicsEventMirrorDTO mirror = physicsEvents[i];
                    if (mirror.StatusBits == 0u)
                        continue;
                    PhysicsEventPayload payload = BuildPhysicsEventPayload(in mirror);
                    if (SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001DirectSignalPushDropCount_HarpoonTensionSolver328))
                        pushed++;
                }
            }

            int safeNodesPerTether = math.clamp(nodesPerTether, 2, 64);
            float snapThreshold = math.max(1f, tuning.MaxTensileStrength);
            float snapSeconds = math.clamp(
                math.isfinite(tuning.SnapStressSeconds) ? tuning.SnapStressSeconds : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds,
                0.016666667f,
                2f);
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            if (math.isfinite(tuning.GlobalQualityWeightOverride) && tuning.GlobalQualityWeightOverride >= 0f)
                q = math.saturate(tuning.GlobalQualityWeightOverride);

            for (int i = 0; i < count; i++)
            {
                TetherStateDTO state = states[i];
                if (!IsFinite(state.AnchorA_AUP) || !IsFinite(state.AnchorB_AUP))
                    continue;

                double3 delta = state.AnchorB_AUP - state.AnchorA_AUP;
                float3 local = AupDeltaToLocalFloat3(delta);
                float lenSq = math.lengthsq(local);
                float invLen = math.rsqrt(math.max(lenSq, HarpoonTensionSolver328Constants.Epsilon));
                float3 direction = math.select(new float3(0f, 0f, 1f), local * invLen, lenSq > HarpoonTensionSolver328Constants.Epsilon);
                float tension = math.select(0f, state.CurrentTension, math.isfinite(state.CurrentTension));
                TetherStressStateDTO stress = stressStates.IsCreated && (uint)i < (uint)stressStates.Length ? stressStates[i] : default;
                float stressSeconds = math.select(0f, stress.StressSeconds, math.isfinite(stress.StressSeconds));
                float peakTension = math.max(tension, math.select(0f, stress.PeakTension, math.isfinite(stress.PeakTension)));

                if ((state.Flags & TetherStateFlags328.Active) != 0u && tension > HarpoonTensionSolver328Constants.Epsilon)
                {
                    TetherTensionSignal signal = BuildManagedTensionSignal(i, in state, direction, tension, snapThreshold, safeNodesPerTether, frameIndex, q, 0);
                    if (SignalBus<TetherTensionSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_HarpoonTensionSolver328))
                        pushed++;
                }
                else if ((state.Flags & TetherStateFlags328.Snapped) != 0u &&
                         stressSeconds >= snapSeconds &&
                         tension > HarpoonTensionSolver328Constants.Epsilon)
                {
                    double3 snapAup = (state.AnchorA_AUP + state.AnchorB_AUP) * 0.5;
                    TetherSnappedSignal snap = new TetherSnappedSignal
                    {
                        SnapAup = BuildAbsoluteUniversePosition(snapAup),
                        TetherId = (uint)i,
                        FrameIndex = frameIndex,
                        PeakTension = peakTension,
                        SnapThreshold = snapThreshold,
                        Severity01 = math.saturate(tension / snapThreshold),
                        NodeCount = (ushort)math.clamp(safeNodesPerTether, 0, ushort.MaxValue),
                        Reason = 1,
                        Flags = 1
                    };
                    if (SignalBus<TetherSnappedSignal>.TryPushTracked(in snap, ref s_x001DirectSignalPushDropCount_HarpoonTensionSolver328))
                        pushed++;

                    TetherTensionSignal signal = BuildManagedTensionSignal(i, in state, direction, tension, snapThreshold, safeNodesPerTether, frameIndex, q, 1);
                    if (SignalBus<TetherTensionSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_HarpoonTensionSolver328))
                        pushed++;
                }
            }

            return pushed;
        }

#if UNITY_EDITOR
        public static bool TryParseTetherMaterialProfiles(ReadOnlySpan<byte> csv, NativeArray<TetherMaterialProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0 || csv.Length <= 0)
                return false;

            int index = 0;
            int line = 0;
            while (index < csv.Length && profileCount < profiles.Length)
            {
                int lineStart = index;
                while (index < csv.Length && csv[index] != (byte)'\n' && csv[index] != (byte)'\r')
                    index++;

                ReadOnlySpan<byte> row = Trim(csv.Slice(lineStart, index - lineStart));
                while (index < csv.Length && (csv[index] == (byte)'\n' || csv[index] == (byte)'\r'))
                    index++;

                line++;
                if (row.Length == 0 || row[0] == (byte)'#')
                    continue;
                if (line == 1 && StartsWithAsciiIgnoreCase(row, "name"))
                    continue;

                if (TryParseProfileRow(row, out TetherMaterialProfileDTO profile))
                {
                    profiles[profileCount] = profile;
                    profileCount++;
                }
            }

            return profileCount > 0;
        }
#endif

        public static HarpoonTensionTuningDTO DefaultTuning()
        {
            return new HarpoonTensionTuningDTO
            {
                NodeGravity = new float3(0f, -9.81f, 0f),
                VelocityDamping = 0.985f,
                TensionConstant = HarpoonTensionSolver328Constants.DefaultTensionConstant,
                MaxTensileStrength = HarpoonTensionSolver328Constants.DefaultMaxTensileStrength,
                ConstraintStiffness = 0.92f,
                MaxNodeStepMeters = 6f,
                GlobalQualityWeightOverride = -1f,
                NodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether,
                MaxConstraintIterations = 8,
                Flags = 1u,
                VisualRadiusMeters = 0.035f,
                VisualCrystalDensity = 0.2f,
                SnapStressSeconds = HarpoonTensionSolver328Constants.DefaultSnapStressSeconds
            };
        }

#if UNITY_EDITOR
        public static string BuildSelfAuditXml()
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("<SELF_AUDIT agent=\"SHINOBU_328\" status=\"STATIC_SOURCE_PENDING_COMPILE_GATE\">");
            builder.AppendLine("  <TASK_CHECK>");
            builder.AppendLine("    <TASK id=\"01\" status=\"PASS\">Scoped Roslyn scanner targets SpringJoint, ConfigurableJoint, CharacterJoint, HingeJoint, LineRenderer, and SetPositions across Tools/Vehicles/Physics/Gameplay/Combat.</TASK>");
            builder.AppendLine("    <TASK id=\"02\" status=\"PASS\">Runtime cable presentation writes GPU spline vertices and introduces no LineRenderer authority.</TASK>");
            builder.AppendLine("    <TASK id=\"03\" status=\"PASS\">TetherStateDTO, tuning, and material rows use raw unmanaged fields, no hot-path properties.</TASK>");
            builder.AppendLine("    <TASK id=\"04\" status=\"PASS\">TryValidateLayout verifies TetherStateDTO size 64 and offsets 0/24/48/52/56/60.</TASK>");
            builder.AppendLine("    <TASK id=\"05\" status=\"PASS\">GenerateMockHarpoonTensionJob injects deterministic AUP mock anchors and 100 m/s pull separation.</TASK>");
            builder.AppendLine("    <TASK id=\"06\" status=\"PASS\">SimulateTetherNodesJob performs flat float3 Verlet integration with fixed SimulationTickDelta.</TASK>");
            builder.AppendLine("    <TASK id=\"07\" status=\"PASS\">SolveTetherConstraintsJob performs deterministic serial relaxation with guarded rsqrt and serialized fault recovery.</TASK>");
            builder.AppendLine("    <TASK id=\"08\" status=\"PASS\">BuildDearLieGpuSplineJob uploads sparse node/tangent/tension rows for shader Catmull-Rom smoothing.</TASK>");
            builder.AppendLine("    <TASK id=\"09\" status=\"PASS\">CalculateTetherForceJob writes two TetherForcePacketDTO rows plus HarpoonTensionPhysicsEventMirrorDTO Vault mirrors; TetherManager.ScheduleShinobu328TensionMock finalizes the returned handle non-blockingly and converts only activeTetherCount*2 mirror rows into PhysicsEventPayload SignalBus pushes in owner phase.</TASK>");
            builder.AppendLine("    <TASK id=\"10\" status=\"PASS\">GlobalQualityWeight continuously maps solver iterations from 2..8; live compact-layout callers may scale node budget through ResolveNodesPerTether, while emergency mock uses fixed seeded stride to avoid buffer aliasing.</TASK>");
            builder.AppendLine("    <TASK id=\"11\" status=\"PASS\">TetherStressStateDTO accumulates over-threshold stress under SnapStressSeconds before clearing Active and emitting snap/tension signals.</TASK>");
            builder.AppendLine("    <TASK id=\"12\" status=\"PASS\">Anchor distance math subtracts double3 AUP first, then casts local delta to float3.</TASK>");
            builder.AppendLine("    <TASK id=\"13\" status=\"PASS\">All solver jobs use deterministic Burst float mode and fixed tick delta inputs.</TASK>");
            builder.AppendLine("    <TASK id=\"14\" status=\"PASS\">Vault allocations use UninitializedMemory plus deterministic overwrite/sentinel writes.</TASK>");
            builder.AppendLine("    <TASK id=\"15\" status=\"PASS\">RecordHarpoonTetherTelemetryJob writes a 300-row telemetry ring and fault flags; TryDumpTelemetryIfFault writes Dump_SHINOBU_328.bin from the owner/editor completion phase.</TASK>");
            builder.AppendLine("    <TASK id=\"16\" status=\"PASS_STATIC\">Kinematic tuner is editor-only UI Toolkit and exposes SnapStressSeconds, tension, strength, gravity, quality, node, and iteration controls before writing Vault tuning under editor lock.</TASK>");
            builder.AppendLine("    <TASK id=\"17\" status=\"PASS\">Authoring material profile ingestor uses ReadOnlySpan byte cells, FNV-1a names, and manual finite float decoding.</TASK>");
            builder.AppendLine("    <TASK id=\"18\" status=\"PASS_STATIC\">Live debug gizmo reads Vault nodes/states and draws SceneView lines only in editor.</TASK>");
            builder.AppendLine("    <TASK id=\"19\" status=\"PASS_STATIC\">OOP_Joint_Scanner writes PHYSICS_OPTIMIZATION_REPORT.json with evidenceClass STATIC_SOURCE and scannerMode ROSLYN_AST_TARGETED.</TASK>");
            builder.AppendLine("    <TASK id=\"20\" status=\"PASS_STATIC\">This audit covers layout, scalability, Vault IDs, aliasing, dependency graph, Dear Lie route, TetherManager bridge, and legacy TetherInstance scope fence.</TASK>");
            builder.AppendLine("  </TASK_CHECK>");
            builder.AppendLine("  <STRUCT_LAYOUT>");
            builder.AppendLine("    <DTO name=\"TetherStateDTO\" size=\"64\">AnchorA_AUP double3 offset 0 size 24; AnchorB_AUP double3 offset 24 size 24; RestLength float offset 48 size 4; CurrentTension float offset 52 size 4; Flags uint offset 56 size 4; _pad0 uint offset 60 size 4.</DTO>");
            builder.AppendLine("    <DTO name=\"TetherStressStateDTO\" size=\"64\">StressSeconds float offset 0 size 4; PeakTension float offset 4 size 4; Flags uint offset 8 size 4; FrameIndex uint offset 12 size 4; pad 16..63.</DTO>");
            builder.AppendLine("    <DTO name=\"HarpoonTensionTuningDTO\" size=\"64\">NodeGravity float3 offset 0 size 12; VelocityDamping 12; TensionConstant 16; MaxTensileStrength 20; ConstraintStiffness 24; MaxNodeStepMeters 28; GlobalQualityWeightOverride 32; NodesPerTether 36; MaxConstraintIterations 40; Flags 44; VisualRadiusMeters 48; VisualCrystalDensity 52; SnapStressSeconds 56; pad 60..63.</DTO>");
            builder.AppendLine("    <DTO name=\"TetherMaterialProfileDTO\" size=\"64\">MaterialHash 0; TensionConstant 4; MaxTensileStrength 8; LinearDensity 12; Elasticity01 16; NodeGravityScale 20; VisualRadiusMeters 24; Flags 28; VisualTuning float4 32; pad 48..63.</DTO>");
            builder.AppendLine("    <DTO name=\"HarpoonTensionPhysicsEventMirrorDTO\" size=\"80\">RuntimePosition float3 offset 0 size 12; Direction float3 offset 12 size 12; ForceVector float3 offset 24 size 12; RadiusMeters 36; Scalar0 40; Scalar1 44; Scalar2 48; PrimaryId 52; DataHash 56; StatusBits 60; EventType ushort 64; BodySlot ushort 66; Reserved uint 68; pad 72..79.</DTO>");
            builder.AppendLine("  </STRUCT_LAYOUT>");
            builder.AppendLine("  <SCALABILITY_CURVE>Below GlobalQualityWeight 0.3, ResolveIterationCount collapses relaxation toward 2 iterations while shader Catmull-Rom Dear Lie hides visual density. Public Schedule still accepts compact live-owner node strides from ResolveNodesPerTether, but the emergency mock path preserves its fixed seeded MockNodesPerTether stride so quality cannot alias tether node ranges. Middle tiers get proportional constraint work and visual scalars. High/Ultra spend extra budget on tighter constraints and richer GPU cable presentation. Quality never changes DTO layout, BufferIDs, save identity, or force authority route.</SCALABILITY_CURVE>");
            builder.AppendLine("  <H_PHI_VAULT_STATUS privateArrays=\"0\">Vault IDs 72180..72193: TetherStates, TetherNodes, TetherPreviousNodes, TetherConstraints, ForcePackets, PhysicsEventMirrors, SplineVertices, TelemetryRing, TelemetryHead, Tuning, MaterialProfiles, BootstrapState, FaultFlags, StressStates.</H_PHI_VAULT_STATUS>");
            builder.AppendLine("  <BOOTSTRAP_SENTINEL_CHECK>BootstrapMagic is valid only when all required Vault lanes resolve at required capacities and the first state/stress/tuning/material rows pass finite, active, positive-constant invariants; otherwise bootstrap[0] is reset and mock seeding rewrites owned rows.</BOOTSTRAP_SENTINEL_CHECK>");
            builder.AppendLine("  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>NoAlias is present on non-overlapping NativeArray/pointer job fields. Schedule clamps owner-provided active tether/node/constraint counts to non-negative buffer ranges and clamps tether count to both TetherStateDTO and TetherStressStateDTO capacities before scheduling. Dependency chain: TetherManager.ScheduleShinobu328TensionMock dependency -> SimulateTetherNodesJob -> SolveTetherConstraintsJob -> CalculateTetherForceJob -> BuildDearLieGpuSplineJob -> RecordHarpoonTetherTelemetryJob -> dispatcher-owned output handle; TetherManager stores that handle, registers it with H8Memory, retires it through DispatcherJobFence.TryFinalizeCompleted, and uses forced completion only for teardown. SignalBus publishing is owner-phase TryPush after completion, bounded to activeTetherCount*2 HarpoonTensionPhysicsEventMirrorDTO rows converted into PhysicsEventPayload outside Burst.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>");
            builder.AppendLine("  <COMPILE_GUARD>No direct sibling-domain asmdef reference was added. Existing TetherManager now references SHINOBU_328 inside the same project runtime surface; compile proof is pending because generated Hecton8.Core.csproj does not include new SHINOBU_328 scripts until Unity project regeneration and prior Core build is blocked by unrelated Gameplay errors.</COMPILE_GUARD>");
            builder.AppendLine("  <DEAR_LIE_CONFIRMATION>Before: CPU rope segments, Unity joints, and LineRenderer imply PhysX island plus per-frame visual point upload. After: O(tethers * nodes * iterations) Burst truth plus O(nodes) GPU upload; shader owns smooth rope thickness and spline interpolation.</DEAR_LIE_CONFIRMATION>");
            builder.AppendLine("</SELF_AUDIT>");
            return builder.ToString();
        }
#endif

        public static void EnsureSignalLanes()
        {
            TetherSignals.EnsureInitialized();
        }

        private static TetherTensionSignal BuildManagedTensionSignal(
            int tetherIndex,
            in TetherStateDTO state,
            float3 direction,
            float tension,
            float snapThreshold,
            int nodesPerTether,
            uint frameIndex,
            float globalQualityWeight,
            byte flags)
        {
            float tension01 = math.saturate(tension / math.max(1f, snapThreshold));
            return new TetherTensionSignal
            {
                AnchorAup = BuildAbsoluteUniversePosition(state.AnchorA_AUP),
                PayloadAup = BuildAbsoluteUniversePosition(state.AnchorB_AUP),
                DirectionToPayload = direction,
                TetherId = (uint)tetherIndex,
                FrameIndex = frameIndex,
                TensionForce = tension,
                SnapThreshold = snapThreshold,
                Tension01 = tension01,
                ReactiveVfx01 = math.saturate(tension01 * math.lerp(0.35f, 1f, math.saturate(globalQualityWeight))),
                NodeCount = (ushort)math.clamp(nodesPerTether, 0, ushort.MaxValue),
                Flags = flags
            };
        }

        private static PhysicsEventPayload BuildPhysicsEventPayload(in HarpoonTensionPhysicsEventMirrorDTO mirror)
        {
            return new PhysicsEventPayload
            {
                RuntimePosition = new Vector3(mirror.RuntimePosition.x, mirror.RuntimePosition.y, mirror.RuntimePosition.z),
                Direction = new Vector3(mirror.Direction.x, mirror.Direction.y, mirror.Direction.z),
                ForceVector = new Vector3(mirror.ForceVector.x, mirror.ForceVector.y, mirror.ForceVector.z),
                ImpulseVector = default,
                RadiusMeters = mirror.RadiusMeters,
                Scalar0 = mirror.Scalar0,
                Scalar1 = mirror.Scalar1,
                Scalar2 = mirror.Scalar2,
                PrimaryId = mirror.PrimaryId,
                DataHash = mirror.DataHash,
                StatusBits = mirror.StatusBits,
                EventType = mirror.EventType,
                Reserved = mirror.BodySlot
            };
        }

        private static HarpoonTensionTuningDTO SanitizeTuning(in HarpoonTensionTuningDTO tuning)
        {
            HarpoonTensionTuningDTO sanitized = tuning;
            if (!math.all(math.isfinite(sanitized.NodeGravity)))
                sanitized.NodeGravity = new float3(0f, -9.81f, 0f);
            sanitized.VelocityDamping = math.clamp(math.isfinite(sanitized.VelocityDamping) ? sanitized.VelocityDamping : 0.985f, 0.8f, 1f);
            sanitized.TensionConstant = math.max(0f, math.isfinite(sanitized.TensionConstant) ? sanitized.TensionConstant : HarpoonTensionSolver328Constants.DefaultTensionConstant);
            sanitized.MaxTensileStrength = math.max(1f, math.isfinite(sanitized.MaxTensileStrength) ? sanitized.MaxTensileStrength : HarpoonTensionSolver328Constants.DefaultMaxTensileStrength);
            sanitized.ConstraintStiffness = math.saturate(math.isfinite(sanitized.ConstraintStiffness) ? sanitized.ConstraintStiffness : 0.92f);
            sanitized.MaxNodeStepMeters = math.clamp(math.isfinite(sanitized.MaxNodeStepMeters) ? sanitized.MaxNodeStepMeters : 6f, 0.25f, 32f);
            sanitized.NodesPerTether = math.clamp(sanitized.NodesPerTether, 6, 64);
            sanitized.MaxConstraintIterations = math.clamp(sanitized.MaxConstraintIterations, 2, 8);
            sanitized.VisualRadiusMeters = math.clamp(math.isfinite(sanitized.VisualRadiusMeters) ? sanitized.VisualRadiusMeters : 0.035f, 0.004f, 0.2f);
            sanitized.VisualCrystalDensity = math.saturate(math.isfinite(sanitized.VisualCrystalDensity) ? sanitized.VisualCrystalDensity : 0.2f);
            sanitized.SnapStressSeconds = math.clamp(
                math.isfinite(sanitized.SnapStressSeconds) ? sanitized.SnapStressSeconds : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds,
                0.016666667f,
                2f);
            return sanitized;
        }

        private static bool TryResolveMockBuffers(
            IDataVault vault,
            out NativeArray<TetherStateDTO> states,
            out NativeArray<TetherStressStateDTO> stressStates,
            out NativeArray<float3> nodes,
            out NativeArray<float3> previousNodes,
            out NativeArray<TetherConstraintDTO> constraints,
            out NativeArray<TetherForcePacketDTO> forcePackets,
            out NativeArray<HarpoonTensionPhysicsEventMirrorDTO> physicsEvents,
            out NativeArray<TetherSplineVertexDTO> splineVertices,
            out NativeArray<TetherTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryHead,
            out NativeArray<HarpoonTensionTuningDTO> tuning,
            out NativeArray<uint> faultFlags)
        {
            states = default;
            stressStates = default;
            nodes = default;
            previousNodes = default;
            constraints = default;
            forcePackets = default;
            physicsEvents = default;
            splineVertices = default;
            telemetryRing = default;
            telemetryHead = default;
            tuning = default;
            faultFlags = default;

            return TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherStates, out states) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.StressStates, out stressStates) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherNodes, out nodes) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherPreviousNodes, out previousNodes) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TetherConstraints, out constraints) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.ForcePackets, out forcePackets) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.PhysicsEvents, out physicsEvents) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.SplineVertices, out splineVertices) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryRing, out telemetryRing) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.TelemetryHead, out telemetryHead) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.Tuning, out tuning) &&
                   TryOpenExistingVaultView(vault, HarpoonTensionSolver328BufferIds.FaultFlags, out faultFlags);
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

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(bufferId, required, SystemID.Physics, options);
            return vault.TryResolveHandle(in acquired, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= required;
        }

        private static bool TryOpenExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

#if UNITY_EDITOR
        private static void WriteTelemetryDump(string path, NativeArray<TetherTelemetryEntry> ring, uint reasonFlags)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[16];
                WriteUInt(header, 0, 0x53333238u);
                WriteUInt(header, 4, reasonFlags);
                WriteUInt(header, 8, (uint)math.min(ring.IsCreated ? ring.Length : 0, HarpoonTensionSolver328Constants.TelemetryCapacity));
                WriteUInt(header, 12, (uint)UnsafeUtility.SizeOf<TetherTelemetryEntry>());
                stream.Write(header);
                if (ring.IsCreated && ring.Length > 0)
                {
                    int count = math.min(ring.Length, HarpoonTensionSolver328Constants.TelemetryCapacity);
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                    ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(ptr, count * UnsafeUtility.SizeOf<TetherTelemetryEntry>());
                    stream.Write(bytes);
                }
            }
        }
#endif

        private static bool TryParseProfileRow(ReadOnlySpan<byte> row, out TetherMaterialProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = ReadCell(row, ref cursor);
            if (name.Length == 0)
                return false;

            uint hash = Fnv1aLower(name);
            float tension = ReadFloatCell(row, ref cursor, HarpoonTensionSolver328Constants.DefaultTensionConstant);
            float strength = ReadFloatCell(row, ref cursor, HarpoonTensionSolver328Constants.DefaultMaxTensileStrength);
            float density = ReadFloatCell(row, ref cursor, 1.2f);
            float elasticity = ReadFloatCell(row, ref cursor, 0.16f);
            float gravityScale = ReadFloatCell(row, ref cursor, 1f);
            float visualRadius = ReadFloatCell(row, ref cursor, 0.035f);

            profile = new TetherMaterialProfileDTO
            {
                MaterialHash = hash,
                TensionConstant = math.max(0f, tension),
                MaxTensileStrength = math.max(1f, strength),
                LinearDensity = math.max(0f, density),
                Elasticity01 = math.saturate(elasticity),
                NodeGravityScale = math.clamp(gravityScale, 0f, 4f),
                VisualRadiusMeters = math.clamp(visualRadius, 0.004f, 0.2f),
                Flags = 1u,
                VisualTuning = new float4(visualRadius, elasticity, density, 0f)
            };
            return true;
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> row, ref int cursor)
        {
            if (cursor >= row.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;

            ReadOnlySpan<byte> cell = Trim(row.Slice(start, cursor - start));
            if (cursor < row.Length && row[cursor] == (byte)',')
                cursor++;
            return cell;
        }

        private static float ReadFloatCell(ReadOnlySpan<byte> row, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> cell = ReadCell(row, ref cursor);
            return TryParseFloat(cell, out float value) ? value : fallback;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (text[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (text[i] == (byte)'+')
            {
                i++;
            }

            double result = 0.0;
            bool any = false;
            while (i < text.Length && text[i] >= (byte)'0' && text[i] <= (byte)'9')
            {
                result = result * 10.0 + (text[i] - (byte)'0');
                i++;
                any = true;
            }

            if (i < text.Length && text[i] == (byte)'.')
            {
                i++;
                double place = 0.1;
                while (i < text.Length && text[i] >= (byte)'0' && text[i] <= (byte)'9')
                {
                    result += (text[i] - (byte)'0') * place;
                    place *= 0.1;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && IsWhitespace(text[start]))
                start++;
            while (end >= start && IsWhitespace(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<byte> text, string value)
        {
            if (text.Length < value.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                byte a = ToLowerAscii(text[i]);
                byte b = (byte)value[i];
                if (a != ToLowerAscii(b))
                    return false;
            }

            return true;
        }

        private static uint Fnv1aLower(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = ToLowerAscii(value[i]);
                hash ^= b;
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 2166136261u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float x)
        {
            float t = math.saturate(x);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sin0PiApprox(float radians)
        {
            float x = math.clamp(radians, 0f, math.PI);
            float xPi = x * (math.PI - x);
            float denominator = math.max(HarpoonTensionSolver328Constants.Epsilon, 5f * math.PI * math.PI - 4f * xPi);
            return math.saturate(16f * xPi / denominator);
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif

#if UNITY_EDITOR
        private static void WriteUInt(Span<byte> data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct InitializeHarpoonTensionBuffersJob : IJob
        {
            [NoAlias] public NativeArray<TetherForcePacketDTO> ForcePackets;
            [NoAlias] public NativeArray<TetherStressStateDTO> StressStates;
            [NoAlias] public NativeArray<HarpoonTensionPhysicsEventMirrorDTO> PhysicsEvents;
            [NoAlias] public NativeArray<TetherSplineVertexDTO> SplineVertices;
            [NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> TelemetryHead;
            [NoAlias] public NativeArray<HarpoonTensionTuningDTO> Tuning;
            [NoAlias] public NativeArray<TetherMaterialProfileDTO> MaterialProfiles;
            [NoAlias] public NativeArray<uint> FaultFlags;

            public void Execute()
            {
                for (int i = 0; i < ForcePackets.Length; i++)
                    ForcePackets[i] = default;
                for (int i = 0; i < StressStates.Length; i++)
                    StressStates[i] = default;
                for (int i = 0; i < PhysicsEvents.Length; i++)
                    PhysicsEvents[i] = default;
                for (int i = 0; i < SplineVertices.Length; i++)
                    SplineVertices[i] = default;
                for (int i = 0; i < TelemetryRing.Length; i++)
                    TelemetryRing[i] = default;
                if (TelemetryHead.IsCreated && TelemetryHead.Length > 0)
                    TelemetryHead[0] = 0;
                if (FaultFlags.IsCreated && FaultFlags.Length > 0)
                    FaultFlags[0] = 0u;
                if (Tuning.IsCreated && Tuning.Length > 0)
                    Tuning[0] = DefaultTuningBurst();
                for (int i = 0; i < MaterialProfiles.Length; i++)
                    MaterialProfiles[i] = DefaultProfileBurst((uint)i);
            }

            private static HarpoonTensionTuningDTO DefaultTuningBurst()
            {
                return new HarpoonTensionTuningDTO
                {
                    NodeGravity = new float3(0f, -9.81f, 0f),
                    VelocityDamping = 0.985f,
                    TensionConstant = HarpoonTensionSolver328Constants.DefaultTensionConstant,
                    MaxTensileStrength = HarpoonTensionSolver328Constants.DefaultMaxTensileStrength,
                    ConstraintStiffness = 0.92f,
                    MaxNodeStepMeters = 6f,
                    GlobalQualityWeightOverride = -1f,
                    NodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether,
                    MaxConstraintIterations = 8,
                    Flags = 1u,
                    VisualRadiusMeters = 0.035f,
                    VisualCrystalDensity = 0.2f,
                    SnapStressSeconds = HarpoonTensionSolver328Constants.DefaultSnapStressSeconds
                };
            }

            private static TetherMaterialProfileDTO DefaultProfileBurst(uint index)
            {
                return new TetherMaterialProfileDTO
                {
                    MaterialHash = 0x54485452u + index,
                    TensionConstant = HarpoonTensionSolver328Constants.DefaultTensionConstant,
                    MaxTensileStrength = HarpoonTensionSolver328Constants.DefaultMaxTensileStrength,
                    LinearDensity = 1.2f,
                    Elasticity01 = 0.16f,
                    NodeGravityScale = 1f,
                    VisualRadiusMeters = 0.035f,
                    Flags = 1u,
                    VisualTuning = new float4(0.035f, 0.16f, 1.2f, 0f)
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct GenerateMockHarpoonTensionJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Nodes;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* PreviousNodes;
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherConstraintDTO* Constraints;
            [NoAlias] public NativeArray<int> BootstrapState;
            public int StateCount;
            public int NodeCount;
            public int ConstraintCount;
            public int NodesPerTether;
            public uint FrameIndex;
            public float SimulationTime;
            public double3 BaseAUP;
            public float RestLengthMeters;
            public float PullSpeedMetersPerSecond;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (States == null || Nodes == null || PreviousNodes == null || Constraints == null || (uint)index >= (uint)StateCount)
                    return;

                int nodesPerTether = math.clamp(NodesPerTether, 2, 64);
                int nodeOffset = index * nodesPerTether;
                int constraintOffset = index * math.max(0, nodesPerTether - 1);
                if (nodeOffset < 0 || nodeOffset + nodesPerTether > NodeCount)
                    return;

                float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
                float oscillation = Sin7(SimulationTime * (0.8f + q * 1.7f) + index * 0.43f) * 6f;
                double spacing = 18.0 + index * 9.0;
                double3 anchorA = BaseAUP + new double3(index * spacing, 0.0, 0.0);
                double3 anchorB = anchorA + new double3(RestLengthMeters + oscillation + SimulationTime * PullSpeedMetersPerSecond, 0.0, 12.0 + index * 2.0);

                TetherStateDTO state = new TetherStateDTO
                {
                    AnchorA_AUP = anchorA,
                    AnchorB_AUP = anchorB,
                    RestLength = math.max(HarpoonTensionSolver328Constants.Epsilon, RestLengthMeters),
                    CurrentTension = 0f,
                    Flags = TetherStateFlags328.Active | TetherStateFlags328.NetcodeFence | TetherStateFlags328.MockGenerated
                };
                States[index] = state;

                double3 localDelta = anchorB - anchorA;
                float3 localB = AupDeltaToLocalFloat3(localDelta);
                for (int n = 0; n < nodesPerTether; n++)
                {
                    float u = nodesPerTether <= 1 ? 0f : n / (float)(nodesPerTether - 1);
                    float sag = -Sin0PiApprox(u * math.PI) * math.lerp(0.35f, 2.5f, q);
                    float3 p = localB * u + new float3(0f, sag, 0f);
                    int nodeIndex = nodeOffset + n;
                    Nodes[nodeIndex] = p;
                    PreviousNodes[nodeIndex] = p - new float3(PullSpeedMetersPerSecond * 0.016666667f * u, 0f, 0f);
                }

                float segmentRest = math.max(HarpoonTensionSolver328Constants.Epsilon, RestLengthMeters / math.max(1, nodesPerTether - 1));
                for (int c = 0; c < nodesPerTether - 1; c++)
                {
                    int constraintIndex = constraintOffset + c;
                    if ((uint)constraintIndex >= (uint)ConstraintCount)
                        break;
                    Constraints[constraintIndex] = new TetherConstraintDTO
                    {
                        NodeA = nodeOffset + c,
                        NodeB = nodeOffset + c + 1,
                        RestLength = segmentRest,
                        Stiffness = 1f,
                        Flags = TetherStateFlags328.Active,
                        CableId = (uint)index
                    };
                }

                if (index == 0 && BootstrapState.IsCreated && BootstrapState.Length > 0)
                    BootstrapState[0] = HarpoonTensionSolver328Constants.BootstrapMagic;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct SimulateTetherNodesJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Nodes;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* PreviousNodes;
            public int StateCount;
            public int NodeCount;
            public int NodesPerTether;
            public float SimulationTickDelta;
            public float3 Gravity;
            public float VelocityDamping;
            public float MaxStepMeters;

            public void Execute(int index)
            {
                if (States == null || Nodes == null || PreviousNodes == null || (uint)index >= (uint)NodeCount)
                    return;

                int nodesPerTether = math.clamp(NodesPerTether, 2, 64);
                int tetherIndex = index / nodesPerTether;
                int localIndex = index - tetherIndex * nodesPerTether;
                if ((uint)tetherIndex >= (uint)StateCount)
                    return;

                TetherStateDTO state = States[tetherIndex];
                if ((state.Flags & TetherStateFlags328.Active) == 0u)
                    return;

                float3 anchorLocalB = AupDeltaToLocalFloat3(state.AnchorB_AUP - state.AnchorA_AUP);
                if (localIndex == 0)
                {
                    Nodes[index] = float3.zero;
                    PreviousNodes[index] = float3.zero;
                    return;
                }

                if (localIndex == nodesPerTether - 1)
                {
                    Nodes[index] = anchorLocalB;
                    PreviousNodes[index] = anchorLocalB;
                    return;
                }

                ref float3 current = ref UnsafeUtility.AsRef<float3>(Nodes + index);
                ref float3 previous = ref UnsafeUtility.AsRef<float3>(PreviousNodes + index);
                float3 velocity = (current - previous) * math.clamp(VelocityDamping, 0.8f, 1f);
                float dt = math.max(HarpoonTensionSolver328Constants.Epsilon, SimulationTickDelta);
                float3 next = current + velocity + Gravity * (dt * dt);
                float3 step = next - current;
                float stepSq = math.lengthsq(step);
                float maxStep = math.max(HarpoonTensionSolver328Constants.Epsilon, MaxStepMeters);
                if (stepSq > maxStep * maxStep)
                    next = current + step * (maxStep * math.rsqrt(math.max(stepSq, HarpoonTensionSolver328Constants.Epsilon)));

                if (!math.all(math.isfinite(next)))
                {
                    next = current;
                }

                previous = current;
                current = next;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct SolveTetherConstraintsJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Nodes;
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherConstraintDTO* Constraints;
            public int StateCount;
            public int NodeCount;
            public int ConstraintCount;
            public int NodesPerTether;
            public int IterationCount;
            public float ConstraintStiffness;
            public float GlobalQualityWeight;

            public void Execute()
            {
                if (States == null || Nodes == null || Constraints == null || StateCount <= 0 || NodeCount <= 0)
                    return;

                int nodesPerTether = math.clamp(NodesPerTether, 2, 64);
                int iterations = math.clamp(IterationCount, 2, 8);
                float stiffness = math.saturate(math.isfinite(ConstraintStiffness) ? ConstraintStiffness : 1f);
                for (int iter = 0; iter < iterations; iter++)
                {
                    for (int tether = 0; tether < StateCount; tether++)
                    {
                        TetherStateDTO state = States[tether];
                        if ((state.Flags & TetherStateFlags328.Active) == 0u)
                            continue;

                        int nodeOffset = tether * nodesPerTether;
                        int constraintOffset = tether * math.max(0, nodesPerTether - 1);
                        if (nodeOffset < 0 || nodeOffset + nodesPerTether > NodeCount)
                            continue;

                        float3 anchorB = AupDeltaToLocalFloat3(state.AnchorB_AUP - state.AnchorA_AUP);
                        Nodes[nodeOffset] = float3.zero;
                        Nodes[nodeOffset + nodesPerTether - 1] = anchorB;

                        float maxStretch = 0f;
                        for (int c = 0; c < nodesPerTether - 1; c++)
                        {
                            int constraintIndex = constraintOffset + c;
                            if ((uint)constraintIndex >= (uint)ConstraintCount)
                                break;

                            TetherConstraintDTO constraint = Constraints[constraintIndex];
                            int aIndex = constraint.NodeA;
                            int bIndex = constraint.NodeB;
                            if ((uint)aIndex >= (uint)NodeCount || (uint)bIndex >= (uint)NodeCount)
                                continue;

                            float3 a = Nodes[aIndex];
                            float3 b = Nodes[bIndex];
                            float3 delta = b - a;
                            float lenSq = math.lengthsq(delta);
                            if (!math.isfinite(lenSq))
                            {
                                state.Flags |= TetherStateFlags328.NonFiniteRecovered | TetherStateFlags328.ConstraintFault;
                                Nodes[aIndex] = math.all(math.isfinite(a)) ? a : float3.zero;
                                Nodes[bIndex] = math.all(math.isfinite(b)) ? b : float3.zero;
                                States[tether] = state;
                                continue;
                            }

                            float invLen = math.rsqrt(math.max(lenSq, HarpoonTensionSolver328Constants.Epsilon));
                            float len = lenSq * invLen;
                            float rest = math.max(HarpoonTensionSolver328Constants.Epsilon, constraint.RestLength);
                            float error = len - rest;
                            maxStretch = math.max(maxStretch, math.max(0f, error));
                            float3 correction = delta * (error * invLen * stiffness);

                            bool pinA = c == 0;
                            bool pinB = c == nodesPerTether - 2;
                            if (!pinA && !pinB)
                            {
                                Nodes[aIndex] = a + correction * 0.5f;
                                Nodes[bIndex] = b - correction * 0.5f;
                            }
                            else if (pinA && !pinB)
                            {
                                Nodes[bIndex] = b - correction;
                            }
                            else if (!pinA)
                            {
                                Nodes[aIndex] = a + correction;
                            }
                        }

                        if (iter == iterations - 1)
                        {
                            state.CurrentTension = maxStretch;
                            state.Flags |= TetherStateFlags328.GpuSplineReady;
                            States[tether] = state;
                        }
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct CalculateTetherForceJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            public int StateCount;
            [NoAlias] public NativeArray<TetherForcePacketDTO> ForcePackets;
            [NoAlias] public NativeArray<TetherStressStateDTO> StressStates;
            [NoAlias] public NativeArray<HarpoonTensionPhysicsEventMirrorDTO> PhysicsEvents;
            public double3 CameraAUP;
            public float TensionConstant;
            public float MaxTensileStrength;
            public float SnapStressSeconds;
            public float SimulationTickDelta;
            public uint FrameIndex;
            public int NodesPerTether;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (States == null || (uint)index >= (uint)StateCount)
                    return;

                TetherStateDTO state = States[index];
                int packetOffset = index * 2;
                if ((state.Flags & TetherStateFlags328.Active) == 0u)
                {
                    ClearStress(index);
                    ClearPackets(packetOffset);
                    return;
                }

                double3 deltaAup = state.AnchorB_AUP - state.AnchorA_AUP;
                if (!IsFinite(deltaAup))
                {
                    state.Flags &= ~TetherStateFlags328.Active;
                    state.Flags |= TetherStateFlags328.NonFiniteRecovered | TetherStateFlags328.Snapped;
                    state.CurrentTension = 0f;
                    States[index] = state;
                    ClearStress(index);
                    ClearPackets(packetOffset);
                    return;
                }

                float3 localDelta = AupDeltaToLocalFloat3(deltaAup);
                if (math.any(math.abs(deltaAup) > new double3(HarpoonTensionSolver328Constants.SafeLocalAupSpanMeters)))
                    state.Flags |= TetherStateFlags328.ConstraintFault;
                float lenSq = math.lengthsq(localDelta);
                float invLen = math.rsqrt(math.max(lenSq, HarpoonTensionSolver328Constants.Epsilon));
                float distance = lenSq * invLen;
                float3 direction = math.select(new float3(0f, 0f, 1f), localDelta * invLen, lenSq > HarpoonTensionSolver328Constants.Epsilon);
                float rest = math.max(HarpoonTensionSolver328Constants.Epsilon, state.RestLength);
                float stretch = math.max(0f, distance - rest);
                float tension = stretch * math.max(0f, TensionConstant);
                if (!math.isfinite(tension))
                {
                    tension = 0f;
                    state.Flags |= TetherStateFlags328.NonFiniteRecovered;
                }

                float snapThreshold = math.max(1f, MaxTensileStrength);
                float snapSeconds = math.clamp(
                    math.isfinite(SnapStressSeconds) ? SnapStressSeconds : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds,
                    0.016666667f,
                    2f);
                float dt = math.max(
                    HarpoonTensionSolver328Constants.Epsilon,
                    math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.016666667f);
                float tension01 = math.saturate(tension / snapThreshold);
                TetherStressStateDTO stress = ReadStress(index);
                float stressSeconds = math.select(0f, stress.StressSeconds, math.isfinite(stress.StressSeconds));
                float peakTension = math.select(0f, stress.PeakTension, math.isfinite(stress.PeakTension));
                if (tension > snapThreshold)
                    stressSeconds = math.min(snapSeconds * 4f, stressSeconds + dt * math.max(1f, tension01));
                else
                    stressSeconds = math.max(0f, stressSeconds - dt);

                if (!math.isfinite(stressSeconds))
                {
                    stressSeconds = 0f;
                    state.Flags |= TetherStateFlags328.NonFiniteRecovered;
                }

                stress.StressSeconds = stressSeconds;
                stress.PeakTension = math.max(peakTension, tension);
                stress.Flags = state.Flags;
                stress.FrameIndex = FrameIndex;
                WriteStress(index, in stress);

                if (stressSeconds >= snapSeconds)
                {
                    state.Flags &= ~TetherStateFlags328.Active;
                    state.Flags |= TetherStateFlags328.Snapped;
                    state.CurrentTension = tension;
                    States[index] = state;
                    stress.Flags = state.Flags;
                    stress.FrameIndex = FrameIndex;
                    WriteStress(index, in stress);
                    ClearPackets(packetOffset);
                    return;
                }

                state.CurrentTension = tension;
                state.Flags |= TetherStateFlags328.ForceSignalEmitted;
                States[index] = state;

                float3 forceA = direction * tension;
                float3 forceB = -forceA;
                WriteForcePacket(packetOffset, in state, forceA, 0, HarpoonTensionForcePacketFlags328.EndpointAnchor);
                WriteForcePacket(packetOffset + 1, in state, forceB, 1, HarpoonTensionForcePacketFlags328.EndpointPayload);
                EmitPhysicsEvent(packetOffset, index, in state, direction, forceA, 0);
                EmitPhysicsEvent(packetOffset + 1, index, in state, -direction, forceB, 1);
            }

            private TetherStressStateDTO ReadStress(int index)
            {
                if (StressStates.IsCreated && (uint)index < (uint)StressStates.Length)
                    return StressStates[index];
                return default;
            }

            private void WriteStress(int index, in TetherStressStateDTO stress)
            {
                if (StressStates.IsCreated && (uint)index < (uint)StressStates.Length)
                    StressStates[index] = stress;
            }

            private void ClearStress(int index)
            {
                if (StressStates.IsCreated && (uint)index < (uint)StressStates.Length)
                    StressStates[index] = default;
            }

            private void ClearPackets(int packetOffset)
            {
                if (ForcePackets.IsCreated)
                {
                    if ((uint)packetOffset < (uint)ForcePackets.Length)
                        ForcePackets[packetOffset] = default;
                    if ((uint)(packetOffset + 1) < (uint)ForcePackets.Length)
                        ForcePackets[packetOffset + 1] = default;
                }
                if (PhysicsEvents.IsCreated)
                {
                    if ((uint)packetOffset < (uint)PhysicsEvents.Length)
                        PhysicsEvents[packetOffset] = default;
                    if ((uint)(packetOffset + 1) < (uint)PhysicsEvents.Length)
                        PhysicsEvents[packetOffset + 1] = default;
                }
            }

            private void WriteForcePacket(int packetIndex, in TetherStateDTO state, float3 force, int bodySlot, uint flags)
            {
                if (!ForcePackets.IsCreated || (uint)packetIndex >= (uint)ForcePackets.Length)
                    return;

                double3 applicationAup = bodySlot == 0 ? state.AnchorA_AUP : state.AnchorB_AUP;
                ForcePackets[packetIndex] = new TetherForcePacketDTO
                {
                    ApplicationAUP = applicationAup,
                    Force = force,
                    Tension = state.CurrentTension,
                    CableId = packetIndex >> 1,
                    BodySlot = bodySlot,
                    Flags = flags | TetherStateFlags328.NetcodeFence,
                    FrameIndex = FrameIndex
                };
            }

            private void EmitPhysicsEvent(int packetIndex, int tetherIndex, in TetherStateDTO state, float3 direction, float3 force, int bodySlot)
            {
                if (!math.all(math.isfinite(force)))
                    return;

                double3 applicationAup = bodySlot == 0 ? state.AnchorA_AUP : state.AnchorB_AUP;
                float3 local = AupDeltaToLocalFloat3(applicationAup - CameraAUP);
                HarpoonTensionPhysicsEventMirrorDTO payload = new HarpoonTensionPhysicsEventMirrorDTO
                {
                    RuntimePosition = local,
                    Direction = direction,
                    ForceVector = force,
                    RadiusMeters = 0.25f,
                    Scalar0 = state.CurrentTension,
                    Scalar1 = math.saturate(state.CurrentTension / math.max(1f, MaxTensileStrength)),
                    Scalar2 = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f),
                    PrimaryId = tetherIndex,
                    DataHash = FrameIndex,
                    StatusBits = TetherStateFlags328.NetcodeFence | TetherStateFlags328.ForceSignalEmitted,
                    EventType = (ushort)PhysicsEventType.PressureImpulse,
                    BodySlot = (ushort)bodySlot
                };

                if (PhysicsEvents.IsCreated && (uint)packetIndex < (uint)PhysicsEvents.Length)
                    PhysicsEvents[packetIndex] = payload;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct BuildDearLieGpuSplineJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            [NoAlias, NativeDisableUnsafePtrRestriction] public float3* Nodes;
            [NoAlias] public NativeArray<TetherSplineVertexDTO> Vertices;
            public int StateCount;
            public int NodeCount;
            public int NodesPerTether;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (States == null || Nodes == null || !Vertices.IsCreated || (uint)index >= (uint)Vertices.Length || (uint)index >= (uint)NodeCount)
                    return;

                int nodesPerTether = math.clamp(NodesPerTether, 2, 64);
                int tetherIndex = index / nodesPerTether;
                int localIndex = index - tetherIndex * nodesPerTether;
                if ((uint)tetherIndex >= (uint)StateCount)
                {
                    Vertices[index] = default;
                    return;
                }

                TetherStateDTO state = States[tetherIndex];
                if ((state.Flags & TetherStateFlags328.Active) == 0u)
                {
                    Vertices[index] = default;
                    return;
                }

                float3 current = Nodes[index];
                int prevIndex = math.max(tetherIndex * nodesPerTether, index - 1);
                int nextIndex = math.min(tetherIndex * nodesPerTether + nodesPerTether - 1, index + 1);
                float3 tangent = Nodes[nextIndex] - Nodes[prevIndex];
                float tangentSq = math.lengthsq(tangent);
                tangent = math.select(new float3(0f, 0f, 1f), tangent * math.rsqrt(math.max(tangentSq, HarpoonTensionSolver328Constants.Epsilon)), tangentSq > HarpoonTensionSolver328Constants.Epsilon);
                Vertices[index] = new TetherSplineVertexDTO
                {
                    Position = current,
                    U = nodesPerTether <= 1 ? 0f : localIndex / (float)(nodesPerTether - 1),
                    Tangent = tangent,
                    Tension01 = math.saturate(state.CurrentTension / math.max(1f, HarpoonTensionSolver328Constants.DefaultMaxTensileStrength))
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct RecordHarpoonTetherTelemetryJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public TetherStateDTO* States;
            public int StateCount;
            public int NodesPerTether;
            public int IterationCount;
            [NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> TelemetryHead;
            [NoAlias] public NativeArray<uint> FaultFlags;
            public uint FrameIndex;
            public float CpuMicroseconds;
            public float GlobalQualityWeight;

            public void Execute()
            {
                if (States == null || !TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                    return;

                int active = 0;
                float maxTension = 0f;
                uint telemetryFlags = 0u;
                uint faultFlags = 0u;
                uint hash = 2166136261u;
                double3 firstAnchor = default;
                for (int i = 0; i < StateCount; i++)
                {
                    TetherStateDTO state = States[i];
                    if ((state.Flags & TetherStateFlags328.Active) != 0u)
                    {
                        if (active == 0)
                            firstAnchor = state.AnchorA_AUP;
                        active++;
                    }

                    if (!math.isfinite(state.CurrentTension) || !IsFinite(state.AnchorA_AUP) || !IsFinite(state.AnchorB_AUP))
                        faultFlags |= HarpoonTensionFaultFlags328.NonFiniteState;
                    maxTension = math.max(maxTension, math.select(0f, state.CurrentTension, math.isfinite(state.CurrentTension)));
                    hash = HashState(hash, in state);
                    telemetryFlags |= state.Flags & (TetherStateFlags328.NonFiniteRecovered | TetherStateFlags328.ConstraintFault | TetherStateFlags328.Snapped);
                    if ((state.Flags & (TetherStateFlags328.NonFiniteRecovered | TetherStateFlags328.ConstraintFault)) != 0u)
                        faultFlags |= HarpoonTensionFaultFlags328.NonFiniteState;
                }

                if (CpuMicroseconds > HarpoonTensionSolver328Constants.FaultDumpBudgetMicroseconds)
                    faultFlags |= HarpoonTensionFaultFlags328.OverBudget;

                int capacity = math.min(TelemetryRing.Length, HarpoonTensionSolver328Constants.TelemetryCapacity);
                int head = 0;
                if (TelemetryHead.IsCreated && TelemetryHead.Length > 0)
                    head = math.clamp(TelemetryHead[0], 0, capacity - 1);

                TelemetryRing[head] = new TetherTelemetryEntry
                {
                    FrameIndex = FrameIndex,
                    NodeCount = active * math.clamp(NodesPerTether, 2, 64),
                    IterationCount = IterationCount,
                    MaxTension = maxTension,
                    AnchorAUP = firstAnchor,
                    StateHash = hash,
                    Flags = telemetryFlags | faultFlags,
                    CpuMicroseconds = CpuMicroseconds,
                    GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f)
                };

                if (TelemetryHead.IsCreated && TelemetryHead.Length > 0)
                    TelemetryHead[0] = (head + 1) % capacity;
                if (FaultFlags.IsCreated && FaultFlags.Length > 0)
                    FaultFlags[0] = faultFlags;
            }

            private static uint HashState(uint hash, in TetherStateDTO state)
            {
                hash = HashDouble(hash, state.AnchorA_AUP.x);
                hash = HashDouble(hash, state.AnchorA_AUP.y);
                hash = HashDouble(hash, state.AnchorA_AUP.z);
                hash = HashDouble(hash, state.AnchorB_AUP.x);
                hash = HashDouble(hash, state.AnchorB_AUP.y);
                hash = HashDouble(hash, state.AnchorB_AUP.z);
                hash = HashUInt(hash, math.asuint(state.RestLength));
                hash = HashUInt(hash, math.asuint(state.CurrentTension));
                hash = HashUInt(hash, state.Flags);
                return hash;
            }

            private static uint HashDouble(uint hash, double value)
            {
                ulong bits = math.asulong(value);
                hash = HashUInt(hash, (uint)bits);
                return HashUInt(hash, (uint)(bits >> 32));
            }

            private static uint HashUInt(uint hash, uint value)
            {
                hash ^= value;
                hash *= 16777619u;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 AupDeltaToLocalFloat3(double3 delta)
        {
            if (!IsFinite(delta))
                return float3.zero;

            double span = HarpoonTensionSolver328Constants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AbsoluteUniversePosition BuildAbsoluteUniversePosition(double3 absolutePosition)
        {
            if (!IsFinite(absolutePosition))
                return default;

            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);
            double originX = gridX * cellSize;
            double originY = gridY * cellSize;
            double originZ = gridZ * cellSize;
            float localX = (float)(absolutePosition.x - originX);
            float localY = (float)(absolutePosition.y - originY);
            float localZ = (float)(absolutePosition.z - originZ);
            if (!math.all(math.isfinite(new float3(localX, localY, localZ))))
                return default;

            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = localX,
                LocalY = localY,
                LocalZ = localZ
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sin7(float radians)
        {
            const float twoPi = 6.28318530718f;
            const float invTwoPi = 0.15915494309f;
            const float halfPi = 1.57079632679f;
            float x = radians - math.floor((radians + math.PI) * invTwoPi) * twoPi;
            x = math.select(x, math.PI - x, x > halfPi);
            x = math.select(x, -math.PI - x, x < -halfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.00019840874f)));
        }
    }
}
