using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.Logistics.Grid.Contracts
{
    internal static class WfcOutpostGridContractLayout
    {
        public const int DescriptorStrideBytes = 96;
        public const int PowerNodeStrideBytes = 40;
    }

    /// <summary>
    /// Shared byte layout for the marauder WFC outpost grid. Lower four bits are kind, upper four bits are planar exits.
    /// </summary>
    public static class WfcOutpostGridConstants
    {
        public const int FullWidth = 10;
        public const int FullDepth = 10;
        public const int FullHeight = 5;
        public const int FullCellCount = FullWidth * FullDepth * FullHeight;
        public const int MaxCellCount = FullCellCount;
        public const int MaxDirectedEdges = MaxCellCount * 6;
        public const int TelemetryFrames = 300;

        public const byte Empty = 0;
        public const byte Corridor = 1;
        public const byte Room = 2;
        public const byte Hatch = 3;
        public const byte Datapad = 4;
        public const byte SealedDoor = 5;
        public const byte Window = 6;
        public const byte Pillar = 7;
        public const byte Generator = 8;

        public const byte CellMask = 0x0F;
        public const byte North = 1 << 4;
        public const byte East = 1 << 5;
        public const byte South = 1 << 6;
        public const byte West = 1 << 7;

        public const ushort DescriptorFlagLowTier = 1 << 0;
        public const ushort DescriptorFlagHeightmapFallback = 1 << 1;

        public static int Flatten(int x, int y, int z, int3 dimensions)
        {
            return x + dimensions.x * (z + dimensions.z * y);
        }

        public static bool IsPowerModuleKind(byte packed)
        {
            byte kind = (byte)(packed & CellMask);
            return kind == Corridor ||
                   kind == Room ||
                   kind == Hatch ||
                   kind == Datapad ||
                   kind == SealedDoor ||
                   kind == Window ||
                   kind == Generator;
        }

        public static bool IsDoorKind(byte packed)
        {
            return (packed & CellMask) == SealedDoor;
        }

        public static bool IsRoomLikeKind(byte packed)
        {
            byte kind = (byte)(packed & CellMask);
            return kind == Room ||
                   kind == Hatch ||
                   kind == Datapad ||
                   kind == SealedDoor ||
                   kind == Window ||
                   kind == Generator;
        }
    }

    public static class WfcOutpostGraphCountSlots
    {
        public const int NodeCount = 0;
        public const int DirectedEdgeCount = 1;
        public const int DoorCount = 2;
        public const int FaultFlags = 3;
        public const int RoomCount = 4;
        public const int Count = 5;
    }

    public static class WfcOutpostGraphFaultFlags
    {
        public const int MissingGenerator = 1 << 0;
        public const int CapacityExceeded = 1 << 1;
        public const int InvalidDimensions = 1 << 2;
        public const int InvalidBuffers = 1 << 3;
        public const int NoPowerNodes = 1 << 4;
    }

    /// <summary>
    /// Cold-path descriptor copied with the native WFC byte grid.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = WfcOutpostGridContractLayout.DescriptorStrideBytes)]
    public struct WfcOutpostGridDescriptor
    {
        [FieldOffset(0)]
        public MacroDatabaseAup OriginAup;
        [FieldOffset(48)]
        public int3 Dimensions;
        [FieldOffset(60)]
        public float CellSizeMeters;
        [FieldOffset(64)]
        public float FloorHeightMeters;
        [FieldOffset(68)]
        private uint _pad0;
        [FieldOffset(72)]
        public ulong SectorHash;
        [FieldOffset(80)]
        public uint WorldSeed;
        [FieldOffset(84)]
        public uint GenerationSequence;
        [FieldOffset(88)]
        public uint GridHash;
        [FieldOffset(92)]
        public ushort CellCount;
        [FieldOffset(94)]
        public ushort Flags;
    }

    /// <summary>
    /// SOA node payload produced by Burst translation and consumed by the logistics graph builder.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = WfcOutpostGridContractLayout.PowerNodeStrideBytes)]
    public struct WfcOutpostPowerNode
    {
        [FieldOffset(0)]
        public uint NodeId;
        [FieldOffset(4)]
        public int3 Cell;
        [FieldOffset(16)]
        public float3 LocalOffsetMeters;
        [FieldOffset(28)]
        public ushort CellIndex;
        [FieldOffset(30)]
        public ushort RoomId;
        [FieldOffset(32)]
        public ushort DoorId;
        [FieldOffset(34)]
        public byte Kind;
        [FieldOffset(35)]
        public byte PriorityTier;
        [FieldOffset(36)]
        public byte Flags;
        [FieldOffset(37)]
        public byte Reserved;
        [FieldOffset(38)]
        private ushort _pad0;
    }
}
