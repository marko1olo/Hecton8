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

        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<float3> IntendedMovements;
        public NativeArray<float3> DragSolvedVelocities;
        public NativeArray<PlayerKinematicsTelemetryEntry> TelemetryRing;
        public int TelemetryWriteIndex;
        public uint TelemetryFrameSequence;

        private const int VaultPositionsFlag = 1 << 0;
        private const int VaultVelocitiesFlag = 1 << 1;
        private const int VaultIntendedMovementsFlag = 1 << 2;
        private const int VaultDragSolvedVelocitiesFlag = 1 << 3;
        private const int VaultTelemetryRingFlag = 1 << 4;
        private int _vaultNativeStateMask;

        public bool IsCreated()
        {
            return Positions.IsCreated &&
                   Velocities.IsCreated &&
                   IntendedMovements.IsCreated &&
                   DragSolvedVelocities.IsCreated &&
                   TelemetryRing.IsCreated;
        }

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
            if (!IsCreated())
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
                VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    math.max(1, count),
                    SystemID.GameplayPlayer,
                    NativeArrayOptions.ClearMemory);
                if (handle.BufferID == unchecked((uint)(int)bufferId) &&
                    vault.TryResolveHandle(in handle, out NativeArray<T> vaultArray) &&
                    vaultArray.IsCreated)
                {
                    _vaultNativeStateMask |= vaultFlag;
                    return vaultArray;
                }
            }

            _vaultNativeStateMask &= ~vaultFlag;
            return default;
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
}
