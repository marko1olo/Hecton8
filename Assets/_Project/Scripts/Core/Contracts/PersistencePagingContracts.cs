using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts
{
    public enum H8WorldPageStatus : byte
    {
        None = 0,
        Queued = 1,
        Ready = 2,
        Missing = 3,
        Corrupt = 4,
        IOError = 5,
        Rejected = 6
    }

    public static class H8WorldPagePayloadTypes
    {
        public const uint VoxelDeltaRle = 0x5658524Cu; // VXRL
        public const uint InventoryState = 0x494E5654u; // INVT
        public const uint ChunkDehydratedMetadata = 0x43484452u; // CHDR
        public const uint WfcOutpostState = 0x5746434Fu; // WFCO
    }

    [System.Flags]
    public enum WfcOutpostCellStateFlags : byte
    {
        None = 0,
        DoorOpen = 1 << 0,
        DoorUnlocked = 1 << 1,
        PowerOn = 1 << 2,
        DatapadLooted = 1 << 3
    }

    public enum WfcOutpostPersistenceStatus : byte
    {
        None = 0,
        Ready = 1,
        Missing = 2,
        CorruptLength = 3,
        InvalidGrid = 4,
        ServiceUnavailable = 5,
        DirtyQueued = 6,
        DirtySkippedUnchanged = 7,
        Rejected = 8
    }

    public static class WfcOutpostPersistenceConstants
    {
        public const int GridSizeX = 10;
        public const int GridSizeY = 10;
        public const int GridSizeZ = 5;
        public const int CellCount = GridSizeX * GridSizeY * GridSizeZ;
        public const int MutableBitPlaneCount = 4;
        public const int PackedBitCount = CellCount * MutableBitPlaneCount;
        public const int PackedWordCount = (PackedBitCount + 63) / 64;
        public const int PackedWordBytes = PackedWordCount * sizeof(ulong);
        public const int PayloadHeaderBytes = 32;
        public const int PayloadMaxBytes = PayloadHeaderBytes + PackedWordBytes;
        public const byte MutableFlagMask = (byte)(
            WfcOutpostCellStateFlags.DoorOpen |
            WfcOutpostCellStateFlags.DoorUnlocked |
            WfcOutpostCellStateFlags.PowerOn |
            WfcOutpostCellStateFlags.DatapadLooted);
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct H8WorldPageReadTicket
    {
        [FieldOffset(0)] public long SectorHash;
        [FieldOffset(8)] public uint PayloadType;
        [FieldOffset(12)] public uint RequestId;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public int ByteCount;
        [FieldOffset(24)] public H8WorldPageStatus Status;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] public ushort SlotIndex;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct H8WorldPagerTelemetrySnapshot
    {
        [FieldOffset(0)] public int PendingDiskWrites;
        [FieldOffset(4)] public int PendingDiskReads;
        [FieldOffset(8)] public int PendingReadResults;
        [FieldOffset(12)] public int PageFaults;
        [FieldOffset(16)] public int CorruptReads;
        [FieldOffset(20)] public int CompletedReads;
        [FieldOffset(24)] public int CompletedWrites;
        [FieldOffset(28)] public int DroppedWrites;
        [FieldOffset(32)] public int DroppedReads;
        [FieldOffset(36)] public int IoErrors;
        [FieldOffset(40)] public int QueueHighWatermark;
        [FieldOffset(44)] public int LastPayloadBytes;
        [FieldOffset(48)] public long LastSectorHash;
        [FieldOffset(56)] public uint LastPayloadType;
        [FieldOffset(60)] public uint LastFrame;
    }
}
