using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
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
    [StructLayout(LayoutKind.Sequential)]
    public struct OutpostGenerationSnapshot
    {
        public ulong SectorHash;
        public uint WorldSeed;
        public uint GenerationSequence;
        public float3 OriginMeters;
        public int3 Dimensions;
        public int ShellMatrixCount;
        public int InteractableCount;
        public float OutpostAge01;
        public OutpostGenerationQualityTier QualityTier;
        public OutpostGenerationState State;
        public ushort Flags;
    }

    /// <summary>
    /// Deferred pooled proxy spawn emitted by native matrix extraction. Shell pieces never become GameObjects.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct OutpostInteractableSpawn
    {
        public float3 PositionMeters;
        public float RotationYRadians;
        public ushort CellIndex;
        public byte Kind;
        public byte Flags;
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
