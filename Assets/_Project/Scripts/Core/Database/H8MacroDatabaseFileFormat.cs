using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core.Database
{
    internal static unsafe class H8MacroDatabaseFileFormat
    {
        internal const string Extension = ".h8db";
        internal const string CompactionTempFileName = "world_data_compact.tmp";
        internal const int NodeSizeBytes = 4096;
        internal const int HeaderSizeBytes = 4096;
        internal const int NodeMaxKeys = 169;
        internal const int NodeMinDegree = 85;
        internal const int NodeHeaderSizeBytes = 16;
        internal const int NodeSectorHashesOffset = NodeHeaderSizeBytes;
        internal const int NodeFileOffsetsOffset = NodeSectorHashesOffset + (NodeMaxKeys * sizeof(ulong));
        internal const int NodeChildOffsetsOffset = NodeFileOffsetsOffset + (NodeMaxKeys * sizeof(long));
        internal const int NodeComputedBytes = NodeChildOffsetsOffset + ((NodeMaxKeys + 1) * sizeof(long));
        internal const int PayloadHeaderSizeBytes = 32;
        internal const uint FileMagic = 0x42443848u;
        internal const uint PayloadMagic = 0x4C503848u;
        internal const int Version = 1;

        internal const int HeaderMagicOffset = 0;
        internal const int HeaderVersionOffset = 4;
        internal const int HeaderSizeOffset = 8;
        internal const int HeaderNodeSizeOffset = 12;
        internal const int HeaderRootNodeOffset = 16;
        internal const int HeaderAppendOffset = 24;
        internal const int HeaderSectorSizeOffset = 32;
        internal const int HeaderFlagsOffset = 36;
        internal const int HeaderDeadBytesOffset = 40;
        internal const int HeaderCompactionGenerationOffset = 48;

        internal const int NodeKeyCountOffset = 0;
        internal const int NodeIsLeafOffset = 2;
        internal const int NodeFlagsOffset = 3;
        internal const int NodeNextLeafOffset = 8;

        internal const int PayloadMagicOffset = 0;
        internal const int PayloadHashOffset = 8;
        internal const int PayloadBytesOffset = 16;
        internal const int PayloadVersionOffset = 20;
        internal const int PayloadFlagsOffset = 24;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ReadUInt(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<uint>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUInt(byte* pointer, int offset, uint value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadInt(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<int>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt(byte* pointer, int offset, int value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ReadUShort(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<ushort>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteUShort(byte* pointer, int offset, ushort value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte ReadByte(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<byte>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteByte(byte* pointer, int offset, byte value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadLong(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<long>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteLong(byte* pointer, int offset, long value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadULong(byte* pointer, int offset)
        {
            return UnsafeUtility.ReadArrayElement<ulong>(pointer + offset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteULong(byte* pointer, int offset, ulong value)
        {
            UnsafeUtility.WriteArrayElement(pointer + offset, 0, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ReadNodeSectorHash(byte* nodePointer, int keyIndex)
        {
            return UnsafeUtility.ReadArrayElement<ulong>(nodePointer + NodeSectorHashesOffset, keyIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteNodeSectorHash(byte* nodePointer, int keyIndex, ulong value)
        {
            UnsafeUtility.WriteArrayElement(nodePointer + NodeSectorHashesOffset, keyIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadNodeFileOffset(byte* nodePointer, int keyIndex)
        {
            return UnsafeUtility.ReadArrayElement<long>(nodePointer + NodeFileOffsetsOffset, keyIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteNodeFileOffset(byte* nodePointer, int keyIndex, long value)
        {
            UnsafeUtility.WriteArrayElement(nodePointer + NodeFileOffsetsOffset, keyIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ReadNodeChildOffset(byte* nodePointer, int childIndex)
        {
            return UnsafeUtility.ReadArrayElement<long>(nodePointer + NodeChildOffsetsOffset, childIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteNodeChildOffset(byte* nodePointer, int childIndex, long value)
        {
            UnsafeUtility.WriteArrayElement(nodePointer + NodeChildOffsetsOffset, childIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long AlignUp(long value, int alignment)
        {
            if (alignment <= 1 || value < 0L)
                return value;

            long mask = alignment - 1L;
            if (value > long.MaxValue - mask)
                return long.MaxValue;

            return (value + mask) & ~mask;
        }
    }
}
