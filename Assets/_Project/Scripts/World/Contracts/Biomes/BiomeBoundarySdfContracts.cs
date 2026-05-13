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
        HasSecondaryBiome = 1 << 4
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BiomeBoundarySdfSettings
    {
        public int2 Resolution;
        public double2 OriginAupXZ;
        public float CellSizeMeters;
        public float BlendWidthMeters;
        public int SampleRadiusCells;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BiomeBoundarySdfResult
    {
        public byte BiomeA;
        public byte BiomeB;
        public byte SampleDiameter;
        public byte Flags;
        public uint BiomeAHash;
        public uint BiomeBHash;
        public float BlendFactor01;
        public float BoundaryDistanceMeters;
        public float PrimaryWeight;
        public float SecondaryWeight;
        public int2 MacroCell;
    }
}
