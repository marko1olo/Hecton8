using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Compact player kinematic snapshot owned by the locomotion orchestrator.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct HectonPlayerState
    {
        private const float PredictionHorizonSeconds = 0.1f;

        public AbsoluteUniversePosition AbsolutePosition;
        public AbsoluteUniversePosition PredictedAbsolutePosition;
        public float3 RuntimePosition;
        public float3 PredictedRuntimePosition;
        public float3 LinearVelocity;
        public float3 ExternalAcceleration;
        public float3 ExternalVelocityChange;
        public float InventoryLoad01;
        public float InventoryMovementMultiplier;

        public void SyncKinematic(Vector3 runtimePosition, Vector3 linearVelocity)
        {
            AbsolutePosition = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            LinearVelocity = new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z);
            PredictedAbsolutePosition = OffsetAup(in AbsolutePosition, LinearVelocity * PredictionHorizonSeconds);
            PredictedRuntimePosition = PredictedAbsolutePosition.ToRuntimeFloat3();
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition origin, float3 runtimeOffset)
        {
            return AbsoluteUniversePosition.OffsetMeters(
                in origin,
                new double3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z));
        }

        public void SyncExternalKinematic(Vector3 acceleration, Vector3 velocityChange)
        {
            ExternalAcceleration = new float3(acceleration.x, acceleration.y, acceleration.z);
            ExternalVelocityChange = new float3(velocityChange.x, velocityChange.y, velocityChange.z);
        }

        public void SyncEncumbrance(float load01, float movementMultiplier)
        {
            InventoryLoad01 = math.saturate(load01);
            InventoryMovementMultiplier = math.clamp(movementMultiplier, 0f, 1f);
        }

        public void ResetTransient()
        {
            ExternalAcceleration = float3.zero;
            ExternalVelocityChange = float3.zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PlayerKinematicsHandTarget
    {
        public const byte FlagBrace = 1 << 0;
        public const byte FlagSqueeze = 1 << 1;

        public float3 Position;
        public float3 Normal;
        public float Blend;
        public byte Hit;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    internal struct PlayerKinematicsTelemetryEntry
    {
        public float3 Position;
        public float3 Velocity;
        public float3 IntendedMovement;
        public float DragCoefficient;
        public float WaterDensityScale;
        public uint Frame;
        public uint Flags;
        public uint Padding0;
        public uint Padding1;
        public uint Padding2;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct PlayerKinematicsLinearDragJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Velocities;
        public NativeArray<float3> SolvedVelocities;
        public float DragCoefficient;
        public float WaterDensityScale;
        public float DeltaTime;

        public void Execute()
        {
            if (!Velocities.IsCreated || !SolvedVelocities.IsCreated)
                return;

            float3 velocity = Velocities[0];
            if (!math.all(math.isfinite(velocity)))
            {
                SolvedVelocities[0] = float3.zero;
                return;
            }

            float dragFactor = math.saturate(
                math.max(0f, DragCoefficient) *
                math.max(0f, WaterDensityScale) *
                math.max(0f, DeltaTime));
            SolvedVelocities[0] = velocity - (velocity * dragFactor);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct PlayerKinematicsNativeState : IDisposable
    {
        public const int KinematicCapacity = 1;
        public const int TelemetryFrameCapacity = 300;

        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<float3> IntendedMovements;
        public NativeArray<float3> DragSolvedVelocities;
        public NativeArray<PlayerKinematicsTelemetryEntry> TelemetryRing;
        public int TelemetryWriteIndex;
        public uint TelemetryFrameSequence;

        private const string NativeMemoryOwner = nameof(PlayerKinematicsNativeState);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const int VaultPositionsFlag = 1 << 0;
        private const int VaultVelocitiesFlag = 1 << 1;
        private const int VaultIntendedMovementsFlag = 1 << 2;
        private const int VaultDragSolvedVelocitiesFlag = 1 << 3;
        private const int VaultTelemetryRingFlag = 1 << 4;
        private int _vaultNativeStateMask;

        public bool IsCreated =>
            Positions.IsCreated &&
            Velocities.IsCreated &&
            IntendedMovements.IsCreated &&
            DragSolvedVelocities.IsCreated &&
            TelemetryRing.IsCreated;

        public void EnsureCreated(IDataVault dataVault)
        {
            IDataVault vault = dataVault;
            EnsureFloat3Array(
                ref Positions,
                BufferID.PlayerKinematicPositions,
                KinematicCapacity,
                nameof(Positions),
                VaultPositionsFlag,
                vault);
            EnsureFloat3Array(
                ref Velocities,
                BufferID.PlayerKinematicVelocities,
                KinematicCapacity,
                nameof(Velocities),
                VaultVelocitiesFlag,
                vault);
            EnsureFloat3Array(
                ref IntendedMovements,
                BufferID.PlayerKinematicIntendedMovements,
                KinematicCapacity,
                nameof(IntendedMovements),
                VaultIntendedMovementsFlag,
                vault);
            EnsureFloat3Array(
                ref DragSolvedVelocities,
                BufferID.PlayerKinematicDragSolvedVelocities,
                KinematicCapacity,
                nameof(DragSolvedVelocities),
                VaultDragSolvedVelocitiesFlag,
                vault);

            if (!TelemetryRing.IsCreated)
            {
                TelemetryRing = AllocateArray<PlayerKinematicsTelemetryEntry>(
                    BufferID.PlayerKinematicTelemetryRing,
                    TelemetryFrameCapacity,
                    nameof(TelemetryRing),
                    VaultTelemetryRingFlag,
                    vault);
            }
        }

        public void WriteKinematicSnapshot(float3 position, float3 velocity, float3 intendedMovement)
        {
            if (!IsCreated)
                return;

            Positions[0] = position;
            Velocities[0] = velocity;
            IntendedMovements[0] = intendedMovement;
        }

        public void WriteTelemetry(float dragCoefficient, float waterDensityScale, uint flags)
        {
            if (!TelemetryRing.IsCreated || !Positions.IsCreated || !Velocities.IsCreated || !IntendedMovements.IsCreated)
                return;

            int index = TelemetryWriteIndex;
            TelemetryRing[index] = new PlayerKinematicsTelemetryEntry
            {
                Position = Positions[0],
                Velocity = Velocities[0],
                IntendedMovement = IntendedMovements[0],
                DragCoefficient = dragCoefficient,
                WaterDensityScale = waterDensityScale,
                Frame = TelemetryFrameSequence,
                Flags = flags
            };

            TelemetryFrameSequence++;
            TelemetryWriteIndex = (index + 1) % TelemetryFrameCapacity;
        }

        public void ApplyOriginShift(float3 shiftOffset)
        {
            if (!math.all(math.isfinite(shiftOffset)) || math.lengthsq(shiftOffset) <= 0.000001f)
                return;

            if (Positions.IsCreated)
                Positions[0] -= shiftOffset;

            if (TelemetryRing.IsCreated)
            {
                for (int i = 0; i < TelemetryRing.Length; i++)
                {
                    PlayerKinematicsTelemetryEntry entry = TelemetryRing[i];
                    entry.Position -= shiftOffset;
                    TelemetryRing[i] = entry;
                }
            }
        }

        private void EnsureFloat3Array(
            ref NativeArray<float3> array,
            BufferID bufferId,
            int count,
            string label,
            int vaultFlag,
            IDataVault vault)
        {
            if (array.IsCreated)
                return;

            array = AllocateArray<float3>(
                bufferId,
                math.max(1, count),
                label,
                vaultFlag,
                vault);
        }

        private NativeArray<T> AllocateArray<T>(
            BufferID bufferId,
            int count,
            string label,
            int vaultFlag,
            IDataVault vault) where T : struct
        {
            if (vault != null)
            {
                NativeArray<T> vaultArray = vault.GetBuffer<T>(
                    bufferId,
                    math.max(1, count),
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);
                if (vaultArray.IsCreated)
                {
                    _vaultNativeStateMask |= vaultFlag;
                    return vaultArray;
                }
            }

            _vaultNativeStateMask &= ~vaultFlag;
            NativeArray<T> array = H8Memory.Allocate<T>(
                math.max(1, count),
                SystemID.GameplayPlayer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            if (!array.IsCreated)
                return default;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
            return array;
        }

        public void Dispose()
        {
            DisposeArray(ref Positions, VaultPositionsFlag);
            DisposeArray(ref Velocities, VaultVelocitiesFlag);
            DisposeArray(ref IntendedMovements, VaultIntendedMovementsFlag);
            DisposeArray(ref DragSolvedVelocities, VaultDragSolvedVelocitiesFlag);
            DisposeArray(ref TelemetryRing, VaultTelemetryRingFlag);

            TelemetryWriteIndex = 0;
            TelemetryFrameSequence = 0u;
        }

        private void DisposeArray<T>(ref NativeArray<T> array, int vaultFlag) where T : struct
        {
            if (!array.IsCreated)
                return;

            if ((_vaultNativeStateMask & vaultFlag) != 0)
            {
                array = default;
                _vaultNativeStateMask &= ~vaultFlag;
                return;
            }

            NativeMemorySentinel.UnregisterNativeArray(array);
            H8Memory.Release(ref array, SystemID.GameplayPlayer);
        }
    }

    /// <summary>
    /// Owns persistent native buffers used by <see cref="HectonPlayerMotor"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    internal struct HectonPlayerMotorNativeState : IDisposable
    {
        public NativeArray<CapsulecastCommand> ScheduledSweepCommands;
        public NativeArray<RaycastHit> ScheduledSweepResults;
        public JobHandle ScheduledSweepHandle;
        public NativeArray<RaycastCommand> KinematicRepairTargetCommands;
        public NativeArray<RaycastHit> KinematicRepairTargetResults;
        public JobHandle KinematicRepairTargetHandle;

        private const int NativeCacheLineBytes = 64;
        private const int NativeCacheLineMask = NativeCacheLineBytes - 1;
        private const string NativeMemoryOwner = nameof(HectonPlayerMotorNativeState);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        public void EnsureScheduledSweepState(int commandCount, int resultCount)
        {
            int requiredCommandCount = ResolveCacheLinePaddedElementCount<CapsulecastCommand>(commandCount);
            int requiredResultCount = ResolveCacheLinePaddedElementCount<RaycastHit>(resultCount);

            if (ScheduledSweepCommands.IsCreated && ScheduledSweepCommands.Length < requiredCommandCount)
            {
                NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepCommands);
                ScheduledSweepCommands.Dispose();
                ScheduledSweepCommands = default;
            }

            if (ScheduledSweepResults.IsCreated && ScheduledSweepResults.Length < requiredResultCount)
            {
                NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepResults);
                ScheduledSweepResults.Dispose();
                ScheduledSweepResults = default;
            }

            if (!ScheduledSweepCommands.IsCreated)
            {
                ScheduledSweepCommands = new NativeArray<CapsulecastCommand>(
                    requiredCommandCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<CapsulecastCommand>[cache-line padded commandCount] - deferred KCC sweep commands; Allocator.Persistent native storage with 64-byte count padding - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    ScheduledSweepCommands,
                    NativeMemoryOwner,
                    nameof(ScheduledSweepCommands),
                    NativeMemoryLifetime);
            }

            if (!ScheduledSweepResults.IsCreated)
            {
                ScheduledSweepResults = new NativeArray<RaycastHit>(
                    requiredResultCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RaycastHit>[cache-line padded resultCount] - deferred KCC sweep results; Allocator.Persistent native storage with 64-byte count padding - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    ScheduledSweepResults,
                    NativeMemoryOwner,
                    nameof(ScheduledSweepResults),
                    NativeMemoryLifetime);
            }
        }

        private static int ResolveCacheLinePaddedElementCount<T>(int requestedCount) where T : struct
        {
            int safeCount = math.max(1, requestedCount);
            int elementBytes = math.max(1, UnsafeUtility.SizeOf<T>());
            int requestedBytes = safeCount * elementBytes;
            int paddedBytes = (requestedBytes + NativeCacheLineMask) & ~NativeCacheLineMask;
            return math.max(safeCount, (paddedBytes + elementBytes - 1) / elementBytes);
        }

        public void EnsureKinematicRepairTargetState(int commandCount, int resultCount)
        {
            if (!KinematicRepairTargetCommands.IsCreated)
            {
                KinematicRepairTargetCommands = new NativeArray<RaycastCommand>(
                    math.max(1, commandCount),
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[commandCount] - KCC hand IK repair target ray commands - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    KinematicRepairTargetCommands,
                    NativeMemoryOwner,
                    nameof(KinematicRepairTargetCommands),
                    NativeMemoryLifetime);
            }

            if (!KinematicRepairTargetResults.IsCreated)
            {
                KinematicRepairTargetResults = new NativeArray<RaycastHit>(
                    math.max(1, resultCount),
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[resultCount] - KCC hand IK repair target ray results - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    KinematicRepairTargetResults,
                    NativeMemoryOwner,
                    nameof(KinematicRepairTargetResults),
                    NativeMemoryLifetime);
            }
        }

        public void DisposeScheduledSweepState(bool hasDependency, JobHandle dependency)
        {
            if (!ScheduledSweepCommands.IsCreated && !ScheduledSweepResults.IsCreated)
            {
                ScheduledSweepHandle = default;
                return;
            }

            if (!hasDependency)
            {
                if (ScheduledSweepCommands.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepCommands);
                    ScheduledSweepCommands.Dispose();
                }
                if (ScheduledSweepResults.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepResults);
                    ScheduledSweepResults.Dispose();
                }
                ScheduledSweepCommands = default;
                ScheduledSweepResults = default;
                ScheduledSweepHandle = default;
                return;
            }

            JobHandle disposeHandle = dependency;
            if (ScheduledSweepCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepCommands);
                disposeHandle = ScheduledSweepCommands.Dispose(disposeHandle);
            }
            if (ScheduledSweepResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(ScheduledSweepResults);
                disposeHandle = ScheduledSweepResults.Dispose(disposeHandle);
            }

            ScheduledSweepCommands = default;
            ScheduledSweepResults = default;
            ScheduledSweepHandle = disposeHandle;
        }

        public void DisposeKinematicRepairTargetState(bool hasDependency, JobHandle dependency)
        {
            if (!KinematicRepairTargetCommands.IsCreated && !KinematicRepairTargetResults.IsCreated)
            {
                KinematicRepairTargetHandle = default;
                return;
            }

            if (!hasDependency)
            {
                if (KinematicRepairTargetCommands.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(KinematicRepairTargetCommands);
                    KinematicRepairTargetCommands.Dispose();
                }
                if (KinematicRepairTargetResults.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(KinematicRepairTargetResults);
                    KinematicRepairTargetResults.Dispose();
                }
                KinematicRepairTargetCommands = default;
                KinematicRepairTargetResults = default;
                KinematicRepairTargetHandle = default;
                return;
            }

            JobHandle disposeHandle = dependency;
            if (KinematicRepairTargetCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(KinematicRepairTargetCommands);
                disposeHandle = KinematicRepairTargetCommands.Dispose(disposeHandle);
            }
            if (KinematicRepairTargetResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(KinematicRepairTargetResults);
                disposeHandle = KinematicRepairTargetResults.Dispose(disposeHandle);
            }

            KinematicRepairTargetCommands = default;
            KinematicRepairTargetResults = default;
            KinematicRepairTargetHandle = disposeHandle;
        }

        public void Dispose()
        {
            DisposeScheduledSweepState(ScheduledSweepHandle.IsCompleted == false, ScheduledSweepHandle);
            DisposeKinematicRepairTargetState(KinematicRepairTargetHandle.IsCompleted == false, KinematicRepairTargetHandle);
        }
    }
}
