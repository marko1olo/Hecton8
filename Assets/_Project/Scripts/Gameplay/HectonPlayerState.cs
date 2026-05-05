using System;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Compact player kinematic snapshot owned by the locomotion orchestrator.
    /// </summary>
    internal struct HectonPlayerState
    {
        public AbsoluteUniversePosition AbsolutePosition;
        public float3 RuntimePosition;
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
    internal struct HectonPlayerMotorNativeState : IDisposable
    {
        public NativeArray<CapsulecastCommand> ScheduledSweepCommands;
        public NativeArray<RaycastHit> ScheduledSweepResults;
        public JobHandle ScheduledSweepHandle;

        private const string NativeMemoryOwner = nameof(HectonPlayerMotorNativeState);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        public void EnsureScheduledSweepState(int commandCount, int resultCount)
        {
            if (!ScheduledSweepCommands.IsCreated)
            {
                ScheduledSweepCommands = new NativeArray<CapsulecastCommand>(
                    math.max(1, commandCount),
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CapsulecastCommand>[commandCount] - deferred KCC sweep commands - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    ScheduledSweepCommands,
                    NativeMemoryOwner,
                    nameof(ScheduledSweepCommands),
                    NativeMemoryLifetime);
            }

            if (!ScheduledSweepResults.IsCreated)
            {
                ScheduledSweepResults = new NativeArray<RaycastHit>(
                    math.max(1, resultCount),
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[resultCount] - deferred KCC sweep results - owner: HectonPlayerMotorNativeState
                NativeMemorySentinel.RegisterNativeArray(
                    ScheduledSweepResults,
                    NativeMemoryOwner,
                    nameof(ScheduledSweepResults),
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

        public void Dispose()
        {
            DisposeScheduledSweepState(ScheduledSweepHandle.IsCompleted == false, ScheduledSweepHandle);
        }
    }
}
