using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    internal static class OutpostGenerationContractLayout
    {
        public const int OutpostGenerationSnapshotStrideBytes = 64;
        public const int OutpostInteractableSpawnStrideBytes = 32;
    }

    /// <summary>
    /// High-level generation phase for the deterministic marauder outpost runtime.
    /// </summary>
    public enum OutpostGenerationState : byte
    {
        Idle = 0,
        Solving = 1,
        ExtractingMatrices = 2,
        Ready = 3,
        Faulted = 4
    }

    /// <summary>
    /// Compact quality branch used by outpost math LOD and visual overkill selection.
    /// </summary>
    public enum OutpostGenerationQualityTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Immutable state sample published by the outpost generation service.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OutpostGenerationContractLayout.OutpostGenerationSnapshotStrideBytes)]
    public struct OutpostGenerationSnapshot
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint WorldSeed;
        [FieldOffset(12)] public uint GenerationSequence;
        [FieldOffset(16)] public float3 OriginMeters;
        [FieldOffset(28)] public int3 Dimensions;
        [FieldOffset(40)] public int ShellMatrixCount;
        [FieldOffset(44)] public int InteractableCount;
        [FieldOffset(48)] public float OutpostAge01;
        [FieldOffset(52)] public OutpostGenerationQualityTier QualityTier;
        [FieldOffset(53)] public OutpostGenerationState State;
        [FieldOffset(54)] public ushort Flags;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Deferred pooled proxy spawn emitted by native matrix extraction. Shell pieces never become GameObjects.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OutpostGenerationContractLayout.OutpostInteractableSpawnStrideBytes)]
    public struct OutpostInteractableSpawn
    {
        [FieldOffset(0)] public float3 PositionMeters;
        [FieldOffset(12)] public float RotationYRadians;
        [FieldOffset(16)] public ushort CellIndex;
        [FieldOffset(18)] public byte Kind;
        [FieldOffset(19)] public byte Flags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    /// <summary>
    /// Registry-facing outpost generation surface. Runtime ownership stays in World.Outposts.
    /// </summary>
    public interface IOutpostGenerationService : IDisposable
    {
        bool IsGenerated { get; }
        bool IsBusy { get; }
        ulong FirstBaseHash { get; }
        OutpostGenerationSnapshot LatestSnapshot { get; }

        bool TryRequestGeneration(ulong sectorHash, float3 originMeters, uint worldSeed);
        bool TryGetWfcGrid(out NativeArray<byte>.ReadOnly cells, out int3 dimensions, out int cellCount, out uint gridHash, out uint generationSequence);
        bool TryGetShellMatrices(out NativeArray<float4x4>.ReadOnly matrices, out int matrixCount, out uint generationSequence);
        bool TryGetShellGraphicsBuffer(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer, out int instanceCount, out uint generationSequence);
        void ApplyAupShift(float3 shiftMeters, uint shiftFrameId);
    }
}
