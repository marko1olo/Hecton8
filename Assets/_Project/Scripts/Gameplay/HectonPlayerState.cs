using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
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
            double3 absolute = origin.ToAbsoluteDouble3() + new double3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
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
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CapsulecastCommand>[cache-line padded commandCount] - deferred KCC sweep commands; Allocator.Persistent native storage with 64-byte count padding - owner: HectonPlayerMotorNativeState
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
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[cache-line padded resultCount] - deferred KCC sweep results; Allocator.Persistent native storage with 64-byte count padding - owner: HectonPlayerMotorNativeState
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
