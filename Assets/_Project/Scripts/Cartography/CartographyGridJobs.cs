using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Cartography
{
    public static class CartographyGridConstants
    {
        public const int AupCellSizeMeters = 5000;
        public const int MacroCellSizeMeters = 50;
        public const int AxisBits = 7;
        public const int AxisLength = 1 << AxisBits;
        public const int OriginOffset = AxisLength >> 1;
        public const int BitCount = AxisLength * AxisLength * AxisLength;
        public const int WordCount = BitCount >> 6;
        public const int BlackBoxFrameCount = 300;
        public const int MaxRevealSignalsPerSlowTick = 16;
        public const int MaxPoiRevealPerSlowTick = 64;
        public const int MaxVisibleMapPoints = 32768;
        public const float DefaultPlayerRevealRadiusMeters = MacroCellSizeMeters;
        public const float MaxRevealRadiusMeters = 250f;
    }

    public enum MapRevealSignalFlags : byte
    {
        None = 0,
        Player = 1 << 0,
        Acoustic = 1 << 1,
        Sonar = 1 << 2,
        Poi = 1 << 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CartographyAup
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public float Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MapRevealSignal
    {
        public CartographyAup Center;
        public float RadiusMeters;
        public uint SourceId;
        public MapRevealSignalFlags Flags;
        private byte _pad0;
        private ushort _pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CartographyPoiRecord
    {
        public CartographyAup Position;
        public uint Kind;
        public uint Hash;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CartographyBlackBoxEntry
    {
        public uint FrameIndex;
        public uint Revision;
        public int LastBitIndex;
        public int RevealedSignalCount;
        public int RevealedPoiCount;
        public uint StateFlags;
        public CartographyAup PlayerAup;
    }

    public static class CartographyGridMath
    {
        public static bool TryEncode(
            in CartographyAup aup,
            out int bitIndex,
            out int wordIndex,
            out int bitOffset)
        {
            if (!TryResolveMacroCell(in aup, out int3 macroCell))
            {
                bitIndex = -1;
                wordIndex = -1;
                bitOffset = -1;
                return false;
            }

            return TryEncodeMacroCell(macroCell, out bitIndex, out wordIndex, out bitOffset);
        }

        public static bool TryResolveMacroCell(in CartographyAup aup, out int3 macroCell)
        {
            macroCell = default;
            if (!IsFinite(in aup))
                return false;

            double3 absolute = ToAbsoluteDouble3(in aup);
            double invCell = 1.0 / CartographyGridConstants.MacroCellSizeMeters;
            double3 macro = math.floor(absolute * invCell);
            if (!math.all(math.isfinite(macro)) ||
                macro.x < int.MinValue ||
                macro.y < int.MinValue ||
                macro.z < int.MinValue ||
                macro.x > int.MaxValue ||
                macro.y > int.MaxValue ||
                macro.z > int.MaxValue)
            {
                return false;
            }

            macroCell = new int3((int)macro.x, (int)macro.y, (int)macro.z);
            return true;
        }

        public static bool TryEncodeMacroCell(
            int3 macroCell,
            out int bitIndex,
            out int wordIndex,
            out int bitOffset)
        {
            int localX = WrapMacroAxisToLocal(macroCell.x);
            int localY = WrapMacroAxisToLocal(macroCell.y);
            int localZ = WrapMacroAxisToLocal(macroCell.z);

            bitIndex = localX +
                       (localY * CartographyGridConstants.AxisLength) +
                       (localZ * CartographyGridConstants.AxisLength * CartographyGridConstants.AxisLength);
            wordIndex = bitIndex >> 6;
            bitOffset = bitIndex & 63;
            return (uint)wordIndex < CartographyGridConstants.WordCount;
        }

        public static int WrapMacroAxisToLocal(int macroAxis)
        {
            long shifted = (long)macroAxis + CartographyGridConstants.OriginOffset;
            long wrapped = shifted % CartographyGridConstants.AxisLength;
            if (wrapped < 0)
                wrapped += CartographyGridConstants.AxisLength;

            return (int)wrapped;
        }

        public static int3 DecodeBitIndex(int bitIndex)
        {
            int localX = bitIndex % CartographyGridConstants.AxisLength;
            int yz = bitIndex / CartographyGridConstants.AxisLength;
            int localY = yz % CartographyGridConstants.AxisLength;
            int localZ = yz / CartographyGridConstants.AxisLength;
            return new int3(
                localX - CartographyGridConstants.OriginOffset,
                localY - CartographyGridConstants.OriginOffset,
                localZ - CartographyGridConstants.OriginOffset);
        }

        public static double3 ToAbsoluteDouble3(in CartographyAup aup)
        {
            return new double3(
                ((double)aup.GridX * CartographyGridConstants.AupCellSizeMeters) + aup.LocalX,
                ((double)aup.GridY * CartographyGridConstants.AupCellSizeMeters) + aup.LocalY,
                ((double)aup.GridZ * CartographyGridConstants.AupCellSizeMeters) + aup.LocalZ);
        }

        public static bool IsFinite(in CartographyAup aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CartographyRevealAupCellJob : IJob
    {
        public NativeArray<ulong> DiscoveredSectors;
        public CartographyAup Center;

        public void Execute()
        {
            if (!CartographyGridMath.TryEncode(in Center, out _, out int wordIndex, out int bitOffset))
                return;

            DiscoveredSectors[wordIndex] = DiscoveredSectors[wordIndex] | (1UL << bitOffset);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CartographyRevealSphereJob : IJob
    {
        public NativeArray<ulong> DiscoveredSectors;
        public NativeArray<int> Changed;
        public CartographyAup Center;
        public float RadiusMeters;

        public void Execute()
        {
            if (!CartographyGridMath.TryResolveMacroCell(in Center, out int3 centerCell))
                return;

            double3 centerAbsolute = CartographyGridMath.ToAbsoluteDouble3(in Center);
            double cellSize = CartographyGridConstants.MacroCellSizeMeters;
            double radiusInput = math.isfinite(RadiusMeters) ? RadiusMeters : CartographyGridConstants.MacroCellSizeMeters;
            double radius = math.clamp(
                radiusInput,
                CartographyGridConstants.MacroCellSizeMeters,
                CartographyGridConstants.MaxRevealRadiusMeters);
            double radiusSq = radius * radius;
            int radiusCells = math.max(0, (int)math.ceil(radius / cellSize));

            for (int z = -radiusCells; z <= radiusCells; z++)
            {
                for (int y = -radiusCells; y <= radiusCells; y++)
                {
                    for (int x = -radiusCells; x <= radiusCells; x++)
                    {
                        int3 macroCell = centerCell + new int3(x, y, z);
                        double3 cellCenter = ((double3)macroCell + new double3(0.5)) * cellSize;
                        double3 delta = cellCenter - centerAbsolute;
                        if (math.lengthsq(delta) > radiusSq)
                            continue;

                        if (!CartographyGridMath.TryEncodeMacroCell(macroCell, out _, out int wordIndex, out int bitOffset))
                            continue;

                        ulong before = DiscoveredSectors[wordIndex];
                        ulong after = before | (1UL << bitOffset);
                        if (after == before)
                            continue;

                        DiscoveredSectors[wordIndex] = after;
                        if (Changed.IsCreated)
                            Changed[0] = 1;
                    }
                }
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CartographyInjectPoiJob : IJob
    {
        [ReadOnly] public NativeArray<CartographyPoiRecord> PoiRecords;
        public NativeArray<ulong> DiscoveredSectors;
        public NativeArray<int> Changed;
        public int Count;

        public void Execute()
        {
            int safeCount = math.min(Count, PoiRecords.Length);
            safeCount = math.min(safeCount, CartographyGridConstants.MaxPoiRevealPerSlowTick);
            for (int i = 0; i < safeCount; i++)
            {
                CartographyAup position = PoiRecords[i].Position;
                if (!CartographyGridMath.TryEncode(in position, out _, out int wordIndex, out int bitOffset))
                    continue;

                ulong before = DiscoveredSectors[wordIndex];
                ulong after = before | (1UL << bitOffset);
                if (after == before)
                    continue;

                DiscoveredSectors[wordIndex] = after;
                if (Changed.IsCreated)
                    Changed[0] = 1;
            }
        }
    }
}
