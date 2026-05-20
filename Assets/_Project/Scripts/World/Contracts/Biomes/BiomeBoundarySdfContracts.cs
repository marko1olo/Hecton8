using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.Biomes.Contracts
{
    [Flags]
    public enum BiomeBoundarySdfFlags : byte
    {
        None = 0,
        LowTierKernel = 1 << 0,
        ExactCellCenter = 1 << 1,
        MissingMap = 1 << 2,
        InvalidInput = 1 << 3,
        HasSecondaryBiome = 1 << 4,
        OutOfBounds = 1 << 5
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeBoundarySdfSettings
    {
        [FieldOffset(0)] public int2 Resolution;
        [FieldOffset(8)] public double2 OriginAupXZ;
        [FieldOffset(24)] public float CellSizeMeters;
        [FieldOffset(28)] public float BlendWidthMeters;
        [FieldOffset(32)] public int SampleRadiusCells;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private ushort _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeBoundarySdfResult
    {
        [FieldOffset(0)] public byte BiomeA;
        [FieldOffset(1)] public byte BiomeB;
        [FieldOffset(2)] public byte SampleDiameter;
        [FieldOffset(3)] public byte Flags;
        [FieldOffset(4)] public uint BiomeAHash;
        [FieldOffset(8)] public uint BiomeBHash;
        [FieldOffset(12)] public float BlendFactor01;
        [FieldOffset(16)] public float BoundaryDistanceMeters;
        [FieldOffset(20)] public float PrimaryWeight;
        [FieldOffset(24)] public float SecondaryWeight;
        [FieldOffset(28)] public int2 MacroCell;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }
}
