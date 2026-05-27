using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct HectonPlayerState
    {
        private const float PredictionHorizonSeconds = 0.1f;

        [FieldOffset(0)]
        public AbsoluteUniversePosition AbsolutePosition;
        [FieldOffset(48)]
        public AbsoluteUniversePosition PredictedAbsolutePosition;
        [FieldOffset(96)]
        public float3 RuntimePosition;
        [FieldOffset(108)]
        public float3 PredictedRuntimePosition;
        [FieldOffset(120)]
        public float3 LinearVelocity;
        [FieldOffset(132)]
        public float3 ExternalAcceleration;
        [FieldOffset(144)]
        public float3 ExternalVelocityChange;
        [FieldOffset(156)]
        public float InventoryLoad01;
        [FieldOffset(160)]
        public float InventoryMovementMultiplier;
        [FieldOffset(164)]
        private uint _pad0;
        [FieldOffset(168)]
        private ulong _pad1;
        [FieldOffset(176)]
        private ulong _pad2;
        [FieldOffset(184)]
        private ulong _pad3;

        public void SyncKinematic(Vector3 runtimePosition, Vector3 linearVelocity)
        {
            RuntimePosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            LinearVelocity = new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z);
            bool hasAupProof = TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition resolvedAup);
            AbsolutePosition = hasAupProof ? resolvedAup : default;
            PredictedAbsolutePosition = hasAupProof
                ? OffsetAup(in resolvedAup, LinearVelocity * PredictionHorizonSeconds)
                : default;
            PredictedRuntimePosition = hasAupProof && PredictedAbsolutePosition.IsFinite()
                ? PredictedAbsolutePosition.ToRuntimeFloat3()
                : RuntimePosition;
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition origin, float3 runtimeOffset)
        {
            return AbsoluteUniversePosition.OffsetMeters(
                in origin,
                new double3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
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

[StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PlayerKinematicsHandTarget
    {
        public const byte FlagBrace = 1 << 0;
        public const byte FlagSqueeze = 1 << 1;

        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float Blend;
        [FieldOffset(28)] public byte Hit;
        [FieldOffset(29)] public byte Flags;
        [FieldOffset(30)] public byte Reserved0;
        [FieldOffset(31)] public byte Reserved1;
    }

[StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PlayerKinematicsTelemetryEntry
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public float3 IntendedMovement;
        [FieldOffset(36)] public float DragCoefficient;
        [FieldOffset(40)] public float WaterDensityScale;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Padding0;
        [FieldOffset(56)] public uint Padding1;
        [FieldOffset(60)] public uint Padding2;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlayerKinematicsLinearDragJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float3> Velocities;
        [NoAlias] public NativeArray<float3> SolvedVelocities;
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

    internal struct PlayerKinematicsNativeState : IDisposable
    {
        public const int KinematicCapacity = 1;
        public const int TelemetryFrameCapacity = 300;

        public int TelemetryWriteIndex;
        public uint TelemetryFrameSequence;

        private VaultGenerationHandle<float3> _positionsHandle;
        private VaultGenerationHandle<float3> _velocitiesHandle;
        private VaultGenerationHandle<float3> _intendedMovementsHandle;
        private VaultGenerationHandle<float3> _dragSolvedVelocitiesHandle;
        private VaultGenerationHandle<PlayerKinematicsTelemetryEntry> _telemetryRingHandle;

        public bool IsCreated(IDataVault dataVault)
        {
            return TryResolveArray(dataVault, in _positionsHandle, KinematicCapacity, out NativeArray<float3> _) &&
                   TryResolveArray(dataVault, in _velocitiesHandle, KinematicCapacity, out NativeArray<float3> _) &&
                   TryResolveArray(dataVault, in _intendedMovementsHandle, KinematicCapacity, out NativeArray<float3> _) &&
                   TryResolveArray(dataVault, in _dragSolvedVelocitiesHandle, KinematicCapacity, out NativeArray<float3> _) &&
                   TryResolveArray(dataVault, in _telemetryRingHandle, TelemetryFrameCapacity, out NativeArray<PlayerKinematicsTelemetryEntry> _);
        }

        public void EnsureCreated(IDataVault dataVault)
        {
            IDataVault vault = dataVault;
            EnsureFloat3Handle(
                ref _positionsHandle,
                BufferID.PlayerKinematicPositions,
                KinematicCapacity,
                vault);
            EnsureFloat3Handle(
                ref _velocitiesHandle,
                BufferID.PlayerKinematicVelocities,
                KinematicCapacity,
                vault);
            EnsureFloat3Handle(
                ref _intendedMovementsHandle,
                BufferID.PlayerKinematicIntendedMovements,
                KinematicCapacity,
                vault);
            EnsureFloat3Handle(
                ref _dragSolvedVelocitiesHandle,
                BufferID.PlayerKinematicDragSolvedVelocities,
                KinematicCapacity,
                vault);

            EnsureHandle(
                ref _telemetryRingHandle,
                BufferID.PlayerKinematicTelemetryRing,
                TelemetryFrameCapacity,
                vault);
        }

        public void WriteKinematicSnapshot(IDataVault dataVault, float3 position, float3 velocity, float3 intendedMovement)
        {
            if (!TryResolveArray(dataVault, in _positionsHandle, KinematicCapacity, out NativeArray<float3> positions) ||
                !TryResolveArray(dataVault, in _velocitiesHandle, KinematicCapacity, out NativeArray<float3> velocities) ||
                !TryResolveArray(dataVault, in _intendedMovementsHandle, KinematicCapacity, out NativeArray<float3> intendedMovements))
                return;

            positions[0] = position;
            velocities[0] = velocity;
            intendedMovements[0] = intendedMovement;
        }

        public void WriteTelemetry(IDataVault dataVault, float dragCoefficient, float waterDensityScale, uint flags)
        {
            if (!TryResolveArray(dataVault, in _telemetryRingHandle, TelemetryFrameCapacity, out NativeArray<PlayerKinematicsTelemetryEntry> telemetryRing) ||
                !TryResolveArray(dataVault, in _positionsHandle, KinematicCapacity, out NativeArray<float3> positions) ||
                !TryResolveArray(dataVault, in _velocitiesHandle, KinematicCapacity, out NativeArray<float3> velocities) ||
                !TryResolveArray(dataVault, in _intendedMovementsHandle, KinematicCapacity, out NativeArray<float3> intendedMovements))
                return;

            int index = TelemetryWriteIndex;
            telemetryRing[index] = new PlayerKinematicsTelemetryEntry
            {
                Position = positions[0],
                Velocity = velocities[0],
                IntendedMovement = intendedMovements[0],
                DragCoefficient = dragCoefficient,
                WaterDensityScale = waterDensityScale,
                Frame = TelemetryFrameSequence,
                Flags = flags
            };

            TelemetryFrameSequence++;
            TelemetryWriteIndex = (index + 1) % TelemetryFrameCapacity;
        }

        public bool TryResolveDragArrays(
            IDataVault dataVault,
            out NativeArray<float3> velocities,
            out NativeArray<float3> dragSolvedVelocities)
        {
            bool hasVelocities = TryResolveArray(dataVault, in _velocitiesHandle, KinematicCapacity, out velocities);
            bool hasSolved = TryResolveArray(dataVault, in _dragSolvedVelocitiesHandle, KinematicCapacity, out dragSolvedVelocities);
            return hasVelocities && hasSolved;
        }

        public bool TryResolveTelemetryRing(
            IDataVault dataVault,
            out NativeArray<PlayerKinematicsTelemetryEntry> telemetryRing)
        {
            return TryResolveArray(dataVault, in _telemetryRingHandle, TelemetryFrameCapacity, out telemetryRing);
        }

        public void ApplyOriginShift(IDataVault dataVault, float3 shiftOffset)
        {
            if (!math.all(math.isfinite(shiftOffset)) || math.lengthsq(shiftOffset) <= 0.000001f)
                return;

            if (TryResolveArray(dataVault, in _positionsHandle, KinematicCapacity, out NativeArray<float3> positions))
                positions[0] -= shiftOffset;

            if (TryResolveArray(dataVault, in _telemetryRingHandle, TelemetryFrameCapacity, out NativeArray<PlayerKinematicsTelemetryEntry> telemetryRing))
            {
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    PlayerKinematicsTelemetryEntry entry = telemetryRing[i];
                    entry.Position -= shiftOffset;
                    telemetryRing[i] = entry;
                }
            }
        }

        private void EnsureFloat3Handle(
            ref VaultGenerationHandle<float3> handle,
            BufferID bufferId,
            int count,
            IDataVault vault)
        {
            EnsureHandle(
                ref handle,
                bufferId,
                count,
                vault);
        }

        private void EnsureHandle<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int count,
            IDataVault vault) where T : struct
        {
            int minimumLength = math.max(1, count);
            if (TryResolveArray(vault, in handle, minimumLength, out NativeArray<T> _))
                return;

            handle = default;
            if (vault != null)
            {
                VaultGenerationHandle<T> acquiredHandle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    minimumLength,
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);
                if (IsExpectedHandle(in acquiredHandle, bufferId) &&
                    vault.TryResolveHandle(in acquiredHandle, out NativeArray<T> vaultArray) &&
                    vaultArray.IsCreated &&
                    vaultArray.Length >= minimumLength)
                {
                    handle = acquiredHandle;
                }
            }
        }

        private static bool TryResolveArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int minimumLength,
            out NativeArray<T> array) where T : struct
        {
            array = default;
            if (vault == null ||
                handle.BufferID == 0u ||
                handle.Generation == 0u ||
                !vault.TryResolveHandle(in handle, out array) ||
                !array.IsCreated ||
                array.Length < math.max(1, minimumLength))
            {
                array = default;
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            ClearHandle(ref _positionsHandle);
            ClearHandle(ref _velocitiesHandle);
            ClearHandle(ref _intendedMovementsHandle);
            ClearHandle(ref _dragSolvedVelocitiesHandle);
            ClearHandle(ref _telemetryRingHandle);

            TelemetryWriteIndex = 0;
            TelemetryFrameSequence = 0u;
        }

        private static void ClearHandle<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
        }

        private static bool IsExpectedHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }
    }
}
