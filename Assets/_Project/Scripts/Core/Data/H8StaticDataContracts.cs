using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Numeric file contracts for the static balance monolith.
    /// </summary>
    public static unsafe class H8StaticDataFormat
    {
        public const string StaticDataFileName = "H8StaticData.bin";
        public const string BabelDictionaryFileName = "Babel_Dictionary.h8bin";
        public const int AlignmentBytes = 16;
        public const int CacheLineBytes = 64;
        public const int BTreeNodeKeyCapacity = 7;
        public const int BTreeNodeChildCapacity = 8;
        public const int MortonBTreeNodeKeyCapacity = 4;
        public const int MortonBTreeNodeChildCapacity = 5;
        public const uint CacheBTreeFlag = 1u << 8;
        public const int TelemetryFrameCount = 300;
        public const int TelemetryDumpHeaderSizeBytes = 32;
        public const ushort FormatVersion = 1;
        public const ushort ExpectedSchemaMajor = 1;
        public const ushort ExpectedSchemaMinor = 2;
        public const uint StaticDataMagic = 0x44533848u;
        public const uint BabelMagic = 0x42413848u;
        public const ulong TelemetryDumpMagic = 0x484543544F4E3800ul;
        public const uint LittleEndianFlag = 1u;
        public const uint SchemaHash = 0x5C43DD40u;
        public const ushort RecordTypeItem = 1;
        public const ushort RecordTypeEconomy = 2;
        public const ushort RecordTypePhysics = 3;
        public const ushort RecordTypeFauna = 4;
        public const ushort MaxPackedRecordType = 15;
        private const long LookupOffsetMask = ~15L;
        private const long LookupRecordTypeMask = MaxPackedRecordType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp16(int value)
        {
            return (value + (AlignmentBytes - 1)) & ~(AlignmentBytes - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignUp16(long value)
        {
            return (value + (AlignmentBytes - 1L)) & ~(AlignmentBytes - 1L);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp64(int value)
        {
            return (value + (CacheLineBytes - 1)) & ~(CacheLineBytes - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignUp64(long value)
        {
            return (value + (CacheLineBytes - 1L)) & ~(CacheLineBytes - 1L);
        }

        public static int RecordSizeBytes(ushort recordType)
        {
            switch (recordType)
            {
                case RecordTypeItem:
                    return UnsafeUtility.SizeOf<H8ItemStaticRecord>();
                case RecordTypeEconomy:
                    return UnsafeUtility.SizeOf<H8EconomyStaticRecord>();
                case RecordTypePhysics:
                    return UnsafeUtility.SizeOf<H8PhysicsStaticRecord>();
                case RecordTypeFauna:
                    return UnsafeUtility.SizeOf<H8FaunaStaticRecord>();
                default:
                    return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort RecordTypeOf<T>() where T : unmanaged
        {
            if (typeof(T) == typeof(H8ItemStaticRecord))
                return RecordTypeItem;
            if (typeof(T) == typeof(H8EconomyStaticRecord))
                return RecordTypeEconomy;
            if (typeof(T) == typeof(H8PhysicsStaticRecord))
                return RecordTypePhysics;
            if (typeof(T) == typeof(H8FaunaStaticRecord))
                return RecordTypeFauna;

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PackLookupValue(long offset, ushort recordType)
        {
            return (offset & LookupOffsetMask) | (recordType & LookupRecordTypeMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanPackRecordType(ushort recordType)
        {
            return recordType > 0 && recordType <= MaxPackedRecordType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UnpackLookupOffset(long packedValue)
        {
            return packedValue & LookupOffsetMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort UnpackLookupRecordType(long packedValue)
        {
            return (ushort)(packedValue & LookupRecordTypeMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long PackBabelSlice(uint offset, uint length)
        {
            return ((long)offset << 32) | length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint UnpackBabelOffset(long packedValue)
        {
            return (uint)(packedValue >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint UnpackBabelLength(long packedValue)
        {
            return (uint)packedValue;
        }
    }

    /// <summary>
    /// Fixed header for H8StaticData.bin. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8StaticDataHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort FormatVersion;
        [FieldOffset(6)] public ushort HeaderSizeBytes;
        [FieldOffset(8)] public ushort SchemaMajor;
        [FieldOffset(10)] public ushort SchemaMinor;
        [FieldOffset(12)] public uint FileByteLength;
        [FieldOffset(16)] public uint PayloadCrc32;
        [FieldOffset(20)] public uint LookupCount;
        [FieldOffset(24)] public uint RecordCount;
        [FieldOffset(28)] public uint LookupOffset;
        [FieldOffset(32)] public uint RecordsOffset;
        [FieldOffset(36)] public uint RecordBytes;
        [FieldOffset(40)] public uint BabelCrc32;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint SchemaHash;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// Hash-to-offset lookup entry. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8StaticDataLookupEntry
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public ushort RecordType;
        [FieldOffset(6)] public ushort ByteSize;
        [FieldOffset(8)] public long Offset;
    }

    /// <summary>
    /// Fixed header for Babel_Dictionary.h8bin. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8BabelDictionaryHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public ushort FormatVersion;
        [FieldOffset(6)] public ushort HeaderSizeBytes;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint IndexOffset;
        [FieldOffset(16)] public uint DataOffset;
        [FieldOffset(20)] public uint FileByteLength;
        [FieldOffset(24)] public uint PayloadCrc32;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>
    /// Hash-to-UTF8 block index entry. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct H8BabelDictionaryEntry
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint Offset;
        [FieldOffset(8)] public uint Length;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Hash-to-UTF8 Babel index row. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BabelIndexDTO
    {
        [FieldOffset(0)] public uint StringHash;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>
    /// Result row for Burst lookup kernels. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BabelLookupResultDTO
    {
        [FieldOffset(0)] public uint TextHash;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// One cache-line B-Tree node. Seven keys and eight child/value lanes consume 60 bytes; Meta occupies the final word.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BTreeNodeDTO
    {
        [FieldOffset(0)] public uint Key0;
        [FieldOffset(4)] public uint Key1;
        [FieldOffset(8)] public uint Key2;
        [FieldOffset(12)] public uint Key3;
        [FieldOffset(16)] public uint Key4;
        [FieldOffset(20)] public uint Key5;
        [FieldOffset(24)] public uint Key6;
        [FieldOffset(28)] public uint Child0;
        [FieldOffset(32)] public uint Child1;
        [FieldOffset(36)] public uint Child2;
        [FieldOffset(40)] public uint Child3;
        [FieldOffset(44)] public uint Child4;
        [FieldOffset(48)] public uint Child5;
        [FieldOffset(52)] public uint Child6;
        [FieldOffset(56)] public uint Child7;
        [FieldOffset(60)] public uint Meta;
    }

    /// <summary>
    /// Generic offset/length result for B-Tree batch lookups. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DataOffsetLengthDTO
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// B-Tree black-box frame sample. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BTreeTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint SearchCount;
        [FieldOffset(8)] public uint AverageDepthQ8;
        [FieldOffset(12)] public uint KeysProcessed;
        [FieldOffset(16)] public uint SlowestLookupNs;
        [FieldOffset(20)] public uint LastHash;
        [FieldOffset(24)] public uint LastResultOffset;
        [FieldOffset(28)] public uint NodeCount;
        [FieldOffset(32)] public uint RootOffset;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public uint PrefetchTouchCount;
        [FieldOffset(48)] public uint ErrorHash;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// Cache-line accumulator consumed by the POST_SIMULATION telemetry flush job. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BTreeTelemetryAccumulatorDTO
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint SearchCount;
        [FieldOffset(8)] public uint DepthSum;
        [FieldOffset(12)] public uint KeysProcessed;
        [FieldOffset(16)] public uint SlowestLookupNs;
        [FieldOffset(20)] public uint LastHash;
        [FieldOffset(24)] public uint LastResultOffset;
        [FieldOffset(28)] public uint NodeCount;
        [FieldOffset(32)] public uint RootOffset;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public uint PrefetchTouchCount;
        [FieldOffset(48)] public uint BatchElapsedNs;
        [FieldOffset(52)] public uint ErrorHash;
        [FieldOffset(56)] public uint BatchCount;
        [FieldOffset(60)] public uint DumpRequestFlag;
    }

    /// <summary>
    /// Cold-boot tuning profile hydrated from btree_tuning_profiles.csv. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BTreeTuningProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint BranchingFactor;
        [FieldOffset(12)] public uint BatchSize;
        [FieldOffset(16)] public uint MaxDepth;
        [FieldOffset(20)] public uint ProfileIndex;
        [FieldOffset(24)] public float PrefetchAggression;
        [FieldOffset(28)] public float QualityMin;
        [FieldOffset(32)] public float QualityMax;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
        [FieldOffset(48)] public uint Reserved2;
        [FieldOffset(52)] public uint Reserved3;
        [FieldOffset(56)] public uint Reserved4;
        [FieldOffset(60)] public uint Reserved5;
    }

    /// <summary>
    /// One cache-line spatial B-Tree node for 64-bit Morton AUP keys. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MortonBTreeNodeDTO
    {
        [FieldOffset(0)] public ulong Key0;
        [FieldOffset(8)] public ulong Key1;
        [FieldOffset(16)] public ulong Key2;
        [FieldOffset(24)] public ulong Key3;
        [FieldOffset(32)] public uint Child0;
        [FieldOffset(36)] public uint Child1;
        [FieldOffset(40)] public uint Child2;
        [FieldOffset(44)] public uint Child3;
        [FieldOffset(48)] public uint Child4;
        [FieldOffset(52)] public uint Meta;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    /// <summary>
    /// Offline spatial record used by SpatialMortonBTreeCompiler. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SpatialMortonBTreeRecordDTO
    {
        [FieldOffset(0)] public ulong MortonKey;
        [FieldOffset(8)] public uint Value;
        [FieldOffset(12)] public uint Reserved0;
    }

    /// <summary>
    /// Caller-owned compiler scratch row for spatial Morton tree levels. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SpatialMortonLevelEntryDTO
    {
        [FieldOffset(0)] public ulong MaxKey;
        [FieldOffset(8)] public int NodeIndex;
        [FieldOffset(12)] public uint Reserved0;
    }

    /// <summary>
    /// Cache-conscious B-Tree helpers for MMF-backed `.h8bin` and `.h8loc` lookup tables.
    /// </summary>
    public static unsafe class H8CacheBTree
    {
        public const BufferID BTreeTelemetryRingBufferId = (BufferID)72070;
        public const BufferID BTreeTelemetryCursorBufferId = (BufferID)72071;
        public const BufferID BTreeTelemetryAccumulatorBufferId = (BufferID)72072;
        public const BufferID BTreeTuningProfilesBufferId = (BufferID)72073;
        public const int BTreeTuningProfileCapacity = 16;
        public const uint BTreeTelemetrySlowBatchFlag = 1u << 0;
        public const uint BTreeTelemetryMalformedFlag = 1u << 1;
        public const uint BTreeTelemetryImmediateSampleFlag = 1u << 2;
        public const uint BTreeSlowBatchThresholdNs = 500000u;
        public const uint NotFound = uint.MaxValue;
        public const uint ResultFoundFlag = 1u;
        public const uint ResultMissingFlag = 2u;
        public const uint ResultMalformedFlag = 4u;
        public const uint LeafNodeFlag = 1u << 8;
        public const uint KeyCountMask = 0x7u;
        public const int MaxTraversalDepth = 32;
        public const int MortonTraversalStackCapacity = MaxTraversalDepth * H8StaticDataFormat.MortonBTreeNodeChildCapacity;
        private const uint UIntSignBit = 0x80000000u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeLeafMeta(int keyCount)
        {
            return LeafNodeFlag | ((uint)keyCount & KeyCountMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeInternalMeta(int keyCount)
        {
            return (uint)keyCount & KeyCountMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetKeyCount(in BTreeNodeDTO node)
        {
            return (int)(node.Meta & KeyCountMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLeaf(in BTreeNodeDTO node)
        {
            return (node.Meta & LeafNodeFlag) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveTree(
            uint flags,
            uint tableOffset,
            uint tableCount,
            uint tableStrideBytes,
            uint treeEndOffset,
            out uint treeOffset,
            out uint rootOffset,
            out uint nodeCount)
        {
            ulong rawTreeOffset = (ulong)tableOffset + ((ulong)tableCount * tableStrideBytes);
            ulong alignedTreeOffset = (rawTreeOffset + (ulong)(H8StaticDataFormat.CacheLineBytes - 1)) &
                                      ~(ulong)(H8StaticDataFormat.CacheLineBytes - 1);
            treeOffset = alignedTreeOffset <= uint.MaxValue ? (uint)alignedTreeOffset : 0u;
            rootOffset = 0u;
            nodeCount = 0u;
            if (alignedTreeOffset > uint.MaxValue ||
                (flags & H8StaticDataFormat.CacheBTreeFlag) == 0u ||
                treeEndOffset <= treeOffset ||
                ((treeOffset | treeEndOffset) & (H8StaticDataFormat.CacheLineBytes - 1u)) != 0u)
            {
                return false;
            }

            uint treeBytes = treeEndOffset - treeOffset;
            if (treeBytes < H8StaticDataFormat.CacheLineBytes ||
                (treeBytes & (H8StaticDataFormat.CacheLineBytes - 1u)) != 0u)
            {
                return false;
            }

            nodeCount = treeBytes / H8StaticDataFormat.CacheLineBytes;
            rootOffset = treeEndOffset - H8StaticDataFormat.CacheLineBytes;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindValue(
            byte* basePointer,
            uint treeOffset,
            uint rootOffset,
            uint treeEndOffset,
            uint targetHash,
            float globalQualityWeight,
            out uint value,
            out uint depth,
            out uint keysProcessed,
            out uint prefetchTouchCount)
        {
            value = NotFound;
            depth = 0u;
            keysProcessed = 0u;
            prefetchTouchCount = 0u;

            if (!IsBTreeRangeValid(basePointer, treeOffset, rootOffset, treeEndOffset))
                return false;

            uint currentOffset = rootOffset;
            uint prefetchSalt = 0u;
            float prefetchWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            for (int guard = 0; guard < MaxTraversalDepth; guard++)
            {
                if (!IsBTreeNodeOffsetValid(treeOffset, treeEndOffset, currentOffset))
                {
                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                BTreeNodeDTO* nodePtr = (BTreeNodeDTO*)(basePointer + currentOffset);
                ref readonly BTreeNodeDTO node = ref UnsafeUtility.AsRef<BTreeNodeDTO>(nodePtr);
                int keyCount = GetKeyCount(in node);
                if ((uint)keyCount > H8StaticDataFormat.BTreeNodeKeyCapacity)
                {
                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                keysProcessed += (uint)keyCount;
                depth++;

                if (IsLeaf(in node))
                {
                    int keyIndex = ScanExactKeyIndex(in node, targetHash, keyCount);
                    if (keyIndex >= 0)
                    {
                        value = GetChild(in node, keyIndex);
                        keysProcessed ^= prefetchSalt & 0u;
                        return true;
                    }

                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                int childIndex = ScanBranchIndex(in node, targetHash, keyCount);
                uint nextOffset = GetChild(in node, childIndex);
                if (ShouldPrefetch(prefetchWeight, depth) &&
                    IsBTreeNodeOffsetValid(treeOffset, treeEndOffset, nextOffset))
                {
                    prefetchSalt ^= TouchNode(basePointer, treeOffset, treeEndOffset, nextOffset);
                    prefetchTouchCount++;
                }

                currentOffset = nextOffset;
            }

            keysProcessed ^= prefetchSalt & 0u;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindValueWithTrace(
            byte* basePointer,
            uint treeOffset,
            uint rootOffset,
            uint treeEndOffset,
            uint targetHash,
            float globalQualityWeight,
            uint* touchedNodeOffsets,
            int touchedNodeCapacity,
            out uint touchedNodeCount,
            out uint value,
            out uint depth,
            out uint keysProcessed,
            out uint prefetchTouchCount)
        {
            value = NotFound;
            depth = 0u;
            keysProcessed = 0u;
            prefetchTouchCount = 0u;
            touchedNodeCount = 0u;

            if (!IsBTreeRangeValid(basePointer, treeOffset, rootOffset, treeEndOffset))
                return false;

            uint currentOffset = rootOffset;
            uint prefetchSalt = 0u;
            float prefetchWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            for (int guard = 0; guard < MaxTraversalDepth; guard++)
            {
                if (!IsBTreeNodeOffsetValid(treeOffset, treeEndOffset, currentOffset))
                {
                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                if (touchedNodeOffsets != null && touchedNodeCount < (uint)math.max(0, touchedNodeCapacity))
                    touchedNodeOffsets[touchedNodeCount] = currentOffset;
                touchedNodeCount++;

                BTreeNodeDTO* nodePtr = (BTreeNodeDTO*)(basePointer + currentOffset);
                ref readonly BTreeNodeDTO node = ref UnsafeUtility.AsRef<BTreeNodeDTO>(nodePtr);
                int keyCount = GetKeyCount(in node);
                if ((uint)keyCount > H8StaticDataFormat.BTreeNodeKeyCapacity)
                {
                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                keysProcessed += (uint)keyCount;
                depth++;

                if (IsLeaf(in node))
                {
                    int keyIndex = ScanExactKeyIndex(in node, targetHash, keyCount);
                    if (keyIndex >= 0)
                    {
                        value = GetChild(in node, keyIndex);
                        keysProcessed ^= prefetchSalt & 0u;
                        return true;
                    }

                    keysProcessed ^= prefetchSalt & 0u;
                    return false;
                }

                int childIndex = ScanBranchIndex(in node, targetHash, keyCount);
                uint nextOffset = GetChild(in node, childIndex);
                if (ShouldPrefetch(prefetchWeight, depth) &&
                    IsBTreeNodeOffsetValid(treeOffset, treeEndOffset, nextOffset))
                {
                    prefetchSalt ^= TouchNode(basePointer, treeOffset, treeEndOffset, nextOffset);
                    prefetchTouchCount++;
                }

                currentOffset = nextOffset;
            }

            keysProcessed ^= prefetchSalt & 0u;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateTelemetry(
            ref BTreeTelemetryAccumulatorDTO accumulator,
            uint frameIndex,
            bool found,
            uint requestedHash,
            uint resultOffset,
            uint depth,
            uint keysProcessed,
            uint prefetchTouchCount,
            uint nodeCount,
            uint rootOffset,
            uint elapsedNs,
            float globalQualityWeight,
            uint errorHash)
        {
            accumulator.FrameIndex = frameIndex;
            accumulator.SearchCount++;
            accumulator.DepthSum += depth;
            accumulator.KeysProcessed += keysProcessed;
            if (elapsedNs > accumulator.SlowestLookupNs)
                accumulator.SlowestLookupNs = elapsedNs;
            accumulator.LastHash = requestedHash;
            accumulator.LastResultOffset = found ? resultOffset : NotFound;
            accumulator.NodeCount = nodeCount;
            accumulator.RootOffset = rootOffset;
            accumulator.GlobalQualityWeight = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            accumulator.PrefetchTouchCount += prefetchTouchCount;
            if (elapsedNs > accumulator.BatchElapsedNs)
                accumulator.BatchElapsedNs = elapsedNs;
            accumulator.BatchCount++;
            accumulator.ErrorHash = errorHash;
            accumulator.Flags |= found ? ResultFoundFlag : ResultMissingFlag;
            if (elapsedNs > BTreeSlowBatchThresholdNs)
                accumulator.Flags |= BTreeTelemetrySlowBatchFlag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BTreeTelemetryEntry BuildTelemetryEntry(in BTreeTelemetryAccumulatorDTO accumulator)
        {
            uint searchCount = accumulator.SearchCount == 0u ? 1u : accumulator.SearchCount;
            ulong averageDepthWide = ((ulong)accumulator.DepthSum << 8) / searchCount;
            uint averageDepthQ8 = averageDepthWide > uint.MaxValue ? uint.MaxValue : (uint)averageDepthWide;
            uint slowestNs = accumulator.SlowestLookupNs > accumulator.BatchElapsedNs ? accumulator.SlowestLookupNs : accumulator.BatchElapsedNs;
            return new BTreeTelemetryEntry
            {
                FrameIndex = accumulator.FrameIndex,
                SearchCount = accumulator.SearchCount,
                AverageDepthQ8 = averageDepthQ8,
                KeysProcessed = accumulator.KeysProcessed,
                SlowestLookupNs = slowestNs,
                LastHash = accumulator.LastHash,
                LastResultOffset = accumulator.LastResultOffset,
                NodeCount = accumulator.NodeCount,
                RootOffset = accumulator.RootOffset,
                Flags = accumulator.Flags,
                GlobalQualityWeight = accumulator.GlobalQualityWeight,
                PrefetchTouchCount = accumulator.PrefetchTouchCount,
                ErrorHash = accumulator.ErrorHash,
                Reserved0 = accumulator.DepthSum,
                Reserved1 = accumulator.BatchCount,
                Reserved2 = accumulator.DumpRequestFlag
            };
        }

        public static bool EnsureTelemetryVaultBuffersCold(
            IDataVault vault,
            out NativeArray<BTreeTelemetryEntry> ring,
            out NativeArray<int> cursor,
            out NativeArray<BTreeTelemetryAccumulatorDTO> accumulator)
        {
            ring = default;
            cursor = default;
            accumulator = default;
            if (vault == null)
                return false;

            VaultGenerationHandle<BTreeTelemetryEntry> ringHandle = vault.GetGenerationHandle<BTreeTelemetryEntry>(
                BTreeTelemetryRingBufferId,
                H8StaticDataFormat.TelemetryFrameCount,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> cursorHandle = vault.GetGenerationHandle<int>(
                BTreeTelemetryCursorBufferId,
                1,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<BTreeTelemetryAccumulatorDTO> accumulatorHandle = vault.GetGenerationHandle<BTreeTelemetryAccumulatorDTO>(
                BTreeTelemetryAccumulatorBufferId,
                1,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);

            return ringHandle.BufferID == unchecked((uint)(int)BTreeTelemetryRingBufferId) &&
                   cursorHandle.BufferID == unchecked((uint)(int)BTreeTelemetryCursorBufferId) &&
                   accumulatorHandle.BufferID == unchecked((uint)(int)BTreeTelemetryAccumulatorBufferId) &&
                   vault.TryResolveHandle(in ringHandle, out ring) &&
                   vault.TryResolveHandle(in cursorHandle, out cursor) &&
                   vault.TryResolveHandle(in accumulatorHandle, out accumulator) &&
                   ring.IsCreated &&
                   ring.Length >= H8StaticDataFormat.TelemetryFrameCount &&
                   cursor.IsCreated &&
                   cursor.Length > 0 &&
                   accumulator.IsCreated &&
                   accumulator.Length > 0;
        }

        public static bool TryResolveTelemetryVaultBuffers(
            IDataVault vault,
            out NativeArray<BTreeTelemetryEntry> ring,
            out NativeArray<int> cursor,
            out NativeArray<BTreeTelemetryAccumulatorDTO> accumulator)
        {
            ring = default;
            cursor = default;
            accumulator = default;

            if (vault == null ||
                !vault.TryGetGenerationHandle<BTreeTelemetryEntry>(BTreeTelemetryRingBufferId, out VaultGenerationHandle<BTreeTelemetryEntry> ringHandle) ||
                !vault.TryGetGenerationHandle<int>(BTreeTelemetryCursorBufferId, out VaultGenerationHandle<int> cursorHandle) ||
                !vault.TryGetGenerationHandle<BTreeTelemetryAccumulatorDTO>(BTreeTelemetryAccumulatorBufferId, out VaultGenerationHandle<BTreeTelemetryAccumulatorDTO> accumulatorHandle) ||
                ringHandle.BufferID != unchecked((uint)(int)BTreeTelemetryRingBufferId) ||
                cursorHandle.BufferID != unchecked((uint)(int)BTreeTelemetryCursorBufferId) ||
                accumulatorHandle.BufferID != unchecked((uint)(int)BTreeTelemetryAccumulatorBufferId) ||
                !vault.TryResolveHandle(in ringHandle, out ring) ||
                !vault.TryResolveHandle(in cursorHandle, out cursor) ||
                !vault.TryResolveHandle(in accumulatorHandle, out accumulator) ||
                !ring.IsCreated ||
                ring.Length < H8StaticDataFormat.TelemetryFrameCount ||
                !cursor.IsCreated ||
                cursor.Length <= 0 ||
                !accumulator.IsCreated ||
                accumulator.Length <= 0)
            {
                ring = default;
                cursor = default;
                accumulator = default;
                return false;
            }

            return true;
        }

        public static JobHandle ScheduleTelemetryPostSimulationFlush(
            NativeArray<BTreeTelemetryEntry> ring,
            NativeArray<int> cursor,
            NativeArray<BTreeTelemetryAccumulatorDTO> accumulator,
            JobHandle dependency)
        {
            if (!ring.IsCreated ||
                ring.Length < H8StaticDataFormat.TelemetryFrameCount ||
                !cursor.IsCreated ||
                cursor.Length <= 0 ||
                !accumulator.IsCreated ||
                accumulator.Length <= 0)
            {
                return dependency;
            }

            FlushBTreeTelemetryPostSimulationJob job = new FlushBTreeTelemetryPostSimulationJob
            {
                Ring = ring,
                Cursor = cursor,
                Accumulator = accumulator
            };
            return job.Schedule(dependency);
        }

        public static bool EnsureTuningProfileVaultBufferCold(IDataVault vault, out NativeArray<BTreeTuningProfileDTO> profiles)
        {
            profiles = default;
            if (vault == null)
                return false;

            VaultGenerationHandle<BTreeTuningProfileDTO> handle = vault.GetGenerationHandle<BTreeTuningProfileDTO>(
                BTreeTuningProfilesBufferId,
                BTreeTuningProfileCapacity,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);
            return handle.BufferID == unchecked((uint)(int)BTreeTuningProfilesBufferId) &&
                   vault.TryResolveHandle(in handle, out profiles) &&
                   profiles.IsCreated &&
                   profiles.Length >= BTreeTuningProfileCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ScanBranchIndex(in BTreeNodeDTO node, uint targetHash, int keyCount)
        {
            int validMask = keyCount >= 7 ? 0x7F : ((1 << keyCount) - 1);
            int geMask = ScanGreaterOrEqualMask(in node, targetHash) & validMask;
            if (geMask == 0)
                return keyCount;

            return math.tzcnt(geMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ScanExactKeyIndex(in BTreeNodeDTO node, uint targetHash, int keyCount)
        {
            int validMask = keyCount >= 7 ? 0x7F : ((1 << keyCount) - 1);
            int eqMask = ScanEqualMask(in node, targetHash) & validMask;
            return eqMask == 0 ? -1 : math.tzcnt(eqMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetKey(in BTreeNodeDTO node, int index)
        {
            switch (index)
            {
                case 0: return node.Key0;
                case 1: return node.Key1;
                case 2: return node.Key2;
                case 3: return node.Key3;
                case 4: return node.Key4;
                case 5: return node.Key5;
                default: return node.Key6;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetChild(in BTreeNodeDTO node, int index)
        {
            switch (index)
            {
                case 0: return node.Child0;
                case 1: return node.Child1;
                case 2: return node.Child2;
                case 3: return node.Child3;
                case 4: return node.Child4;
                case 5: return node.Child5;
                case 6: return node.Child6;
                default: return node.Child7;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetKey(ref BTreeNodeDTO node, int index, uint value)
        {
            switch (index)
            {
                case 0: node.Key0 = value; break;
                case 1: node.Key1 = value; break;
                case 2: node.Key2 = value; break;
                case 3: node.Key3 = value; break;
                case 4: node.Key4 = value; break;
                case 5: node.Key5 = value; break;
                default: node.Key6 = value; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetChild(ref BTreeNodeDTO node, int index, uint value)
        {
            switch (index)
            {
                case 0: node.Child0 = value; break;
                case 1: node.Child1 = value; break;
                case 2: node.Child2 = value; break;
                case 3: node.Child3 = value; break;
                case 4: node.Child4 = value; break;
                case 5: node.Child5 = value; break;
                case 6: node.Child6 = value; break;
                default: node.Child7 = value; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashAupDouble3ToMorton64(double3 absoluteUniversePosition, double cellMeters)
        {
            double safeCell = math.max(0.001d, math.select(0.001d, cellMeters, math.isfinite(cellMeters)));
            long x = QuantizeToMortonLane(absoluteUniversePosition.x, safeCell);
            long y = QuantizeToMortonLane(absoluteUniversePosition.y, safeCell);
            long z = QuantizeToMortonLane(absoluteUniversePosition.z, safeCell);
            return Part21By3((ulong)x) | (Part21By3((ulong)y) << 1) | (Part21By3((ulong)z) << 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeMortonLeafMeta(int keyCount)
        {
            return LeafNodeFlag | ((uint)keyCount & KeyCountMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeMortonInternalMeta(int keyCount)
        {
            return (uint)keyCount & KeyCountMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMortonKeyCount(in MortonBTreeNodeDTO node)
        {
            return (int)(node.Meta & KeyCountMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMortonLeaf(in MortonBTreeNodeDTO node)
        {
            return (node.Meta & LeafNodeFlag) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetMortonKey(in MortonBTreeNodeDTO node, int index)
        {
            switch (index)
            {
                case 0: return node.Key0;
                case 1: return node.Key1;
                case 2: return node.Key2;
                default: return node.Key3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetMortonChild(in MortonBTreeNodeDTO node, int index)
        {
            switch (index)
            {
                case 0: return node.Child0;
                case 1: return node.Child1;
                case 2: return node.Child2;
                case 3: return node.Child3;
                default: return node.Child4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMortonKey(ref MortonBTreeNodeDTO node, int index, ulong value)
        {
            switch (index)
            {
                case 0: node.Key0 = value; break;
                case 1: node.Key1 = value; break;
                case 2: node.Key2 = value; break;
                default: node.Key3 = value; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMortonChild(ref MortonBTreeNodeDTO node, int index, uint value)
        {
            switch (index)
            {
                case 0: node.Child0 = value; break;
                case 1: node.Child1 = value; break;
                case 2: node.Child2 = value; break;
                case 3: node.Child3 = value; break;
                default: node.Child4 = value; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ScanMortonBranchIndex(in MortonBTreeNodeDTO node, ulong targetMorton, int keyCount)
        {
            int index = 0;
            if (keyCount > 0 && node.Key0 < targetMorton) index++;
            if (keyCount > 1 && node.Key1 < targetMorton) index++;
            if (keyCount > 2 && node.Key2 < targetMorton) index++;
            if (keyCount > 3 && node.Key3 < targetMorton) index++;
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ScanMortonExactKeyIndex(in MortonBTreeNodeDTO node, ulong targetMorton, int keyCount)
        {
            if (keyCount > 0 && node.Key0 == targetMorton) return 0;
            if (keyCount > 1 && node.Key1 == targetMorton) return 1;
            if (keyCount > 2 && node.Key2 == targetMorton) return 2;
            if (keyCount > 3 && node.Key3 == targetMorton) return 3;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindMortonValue(
            byte* basePointer,
            uint treeOffset,
            uint rootOffset,
            uint treeEndOffset,
            ulong targetMorton,
            out uint value,
            out uint depth,
            out uint keysProcessed)
        {
            value = NotFound;
            depth = 0u;
            keysProcessed = 0u;
            if (!IsMortonTreeRangeValid(basePointer, treeOffset, rootOffset, treeEndOffset))
                return false;

            uint currentOffset = rootOffset;
            for (int guard = 0; guard < MaxTraversalDepth; guard++)
            {
                if (!IsMortonNodeOffsetValid(treeOffset, treeEndOffset, currentOffset))
                    return false;

                MortonBTreeNodeDTO* nodePtr = (MortonBTreeNodeDTO*)(basePointer + currentOffset);
                ref readonly MortonBTreeNodeDTO node = ref UnsafeUtility.AsRef<MortonBTreeNodeDTO>(nodePtr);
                int keyCount = GetMortonKeyCount(in node);
                if ((uint)keyCount > H8StaticDataFormat.MortonBTreeNodeKeyCapacity)
                    return false;

                depth++;
                keysProcessed += (uint)keyCount;
                if (IsMortonLeaf(in node))
                {
                    int keyIndex = ScanMortonExactKeyIndex(in node, targetMorton, keyCount);
                    if (keyIndex < 0)
                        return false;

                    value = GetMortonChild(in node, keyIndex);
                    return true;
                }

                int childIndex = ScanMortonBranchIndex(in node, targetMorton, keyCount);
                currentOffset = GetMortonChild(in node, childIndex);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFindMortonRangeFirstValue(
            byte* basePointer,
            uint treeOffset,
            uint rootOffset,
            uint treeEndOffset,
            ulong lowerMorton,
            ulong upperMorton,
            out uint value,
            out uint depth,
            out uint keysProcessed)
        {
            value = NotFound;
            depth = 0u;
            keysProcessed = 0u;
            if (upperMorton < lowerMorton ||
                !IsMortonTreeRangeValid(basePointer, treeOffset, rootOffset, treeEndOffset))
            {
                return false;
            }

            uint* stack = stackalloc uint[MortonTraversalStackCapacity];
            int stackCount = 1;
            stack[0] = rootOffset;
            int guard = 0;

            while (stackCount > 0 && guard < MortonTraversalStackCapacity)
            {
                uint currentOffset = stack[--stackCount];
                guard++;
                if (!IsMortonNodeOffsetValid(treeOffset, treeEndOffset, currentOffset))
                    return false;

                MortonBTreeNodeDTO* nodePtr = (MortonBTreeNodeDTO*)(basePointer + currentOffset);
                ref readonly MortonBTreeNodeDTO node = ref UnsafeUtility.AsRef<MortonBTreeNodeDTO>(nodePtr);
                int keyCount = GetMortonKeyCount(in node);
                if ((uint)keyCount > H8StaticDataFormat.MortonBTreeNodeKeyCapacity)
                    return false;

                depth++;
                keysProcessed += (uint)keyCount;
                if (IsMortonLeaf(in node))
                {
                    for (int key = 0; key < keyCount; key++)
                    {
                        ulong morton = GetMortonKey(in node, key);
                        if (morton >= lowerMorton && morton <= upperMorton)
                        {
                            value = GetMortonChild(in node, key);
                            return true;
                        }
                    }

                    continue;
                }

                for (int child = keyCount; child >= 0; child--)
                {
                    ulong previousMax = child == 0 ? 0ul : GetMortonKey(in node, child - 1);
                    ulong childMin = child == 0 ? 0ul : (previousMax == ulong.MaxValue ? ulong.MaxValue : previousMax + 1ul);
                    ulong childMax = child < keyCount ? GetMortonKey(in node, child) : ulong.MaxValue;
                    bool overlaps = lowerMorton <= childMax && upperMorton >= childMin;
                    if (overlaps)
                    {
                        if (stackCount >= MortonTraversalStackCapacity)
                            return false;

                        stack[stackCount] = GetMortonChild(in node, child);
                        stackCount++;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMortonTreeRangeValid(byte* basePointer, uint treeOffset, uint rootOffset, uint treeEndOffset)
        {
            return basePointer != null &&
                   treeOffset <= rootOffset &&
                   treeEndOffset >= H8StaticDataFormat.CacheLineBytes &&
                   rootOffset <= treeEndOffset - H8StaticDataFormat.CacheLineBytes &&
                   (treeOffset & 63u) == 0u &&
                   (rootOffset & 63u) == 0u &&
                   (treeEndOffset & 63u) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMortonNodeOffsetValid(uint treeOffset, uint treeEndOffset, uint currentOffset)
        {
            return treeEndOffset >= H8StaticDataFormat.CacheLineBytes &&
                   currentOffset >= treeOffset &&
                   currentOffset <= treeEndOffset - H8StaticDataFormat.CacheLineBytes &&
                   (currentOffset & 63u) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanGreaterOrEqualMask(in BTreeNodeDTO node, uint targetHash)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                int target = unchecked((int)(targetHash ^ UIntSignBit));
                v128 target4 = X86.Sse2.set1_epi32(target);
                v128 keys0 = new v128(
                    unchecked((int)(node.Key0 ^ UIntSignBit)),
                    unchecked((int)(node.Key1 ^ UIntSignBit)),
                    unchecked((int)(node.Key2 ^ UIntSignBit)),
                    unchecked((int)(node.Key3 ^ UIntSignBit)));
                v128 less0 = X86.Sse2.cmpgt_epi32(target4, keys0);
                int lessMask = CollapseLaneMask4(X86.Sse2.movemask_epi8(less0));

                v128 keys1 = new v128(
                    unchecked((int)(node.Key4 ^ UIntSignBit)),
                    unchecked((int)(node.Key5 ^ UIntSignBit)),
                    unchecked((int)(node.Key6 ^ UIntSignBit)),
                    int.MaxValue);
                v128 less1 = X86.Sse2.cmpgt_epi32(target4, keys1);
                lessMask |= CollapseLaneMask4(X86.Sse2.movemask_epi8(less1)) << 4;
                return (~lessMask) & 0x7F;
            }

            uint4 first = new uint4(node.Key0, node.Key1, node.Key2, node.Key3);
            uint4 second = new uint4(node.Key4, node.Key5, node.Key6, uint.MaxValue);
            int lessFallback = math.bitmask(first < targetHash) | (math.bitmask(second < targetHash) << 4);
            return (~lessFallback) & 0x7F;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanEqualMask(in BTreeNodeDTO node, uint targetHash)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                int target = unchecked((int)targetHash);
                v128 target4 = X86.Sse2.set1_epi32(target);
                v128 keys0 = new v128(unchecked((int)node.Key0), unchecked((int)node.Key1), unchecked((int)node.Key2), unchecked((int)node.Key3));
                int mask = CollapseLaneMask4(X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(keys0, target4)));
                v128 keys1 = new v128(unchecked((int)node.Key4), unchecked((int)node.Key5), unchecked((int)node.Key6), 0);
                mask |= CollapseLaneMask4(X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi32(keys1, target4))) << 4;
                return mask & 0x7F;
            }

            uint4 first = new uint4(node.Key0, node.Key1, node.Key2, node.Key3);
            uint4 second = new uint4(node.Key4, node.Key5, node.Key6, 0u);
            return (math.bitmask(first == targetHash) | (math.bitmask(second == targetHash) << 4)) & 0x7F;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CollapseLaneMask4(int byteMask)
        {
            int mask = 0;
            mask |= (byteMask & 0x000F) != 0 ? 1 : 0;
            mask |= (byteMask & 0x00F0) != 0 ? 2 : 0;
            mask |= (byteMask & 0x0F00) != 0 ? 4 : 0;
            mask |= (byteMask & 0xF000) != 0 ? 8 : 0;
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldPrefetch(float globalQualityWeight, uint depth)
        {
            float weight = math.saturate(globalQualityWeight);
            uint stride = (uint)math.max(1, (int)math.round(math.lerp(4f, 1f, weight)));
            return weight > 0.08f && (depth % stride) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBTreeRangeValid(byte* basePointer, uint treeOffset, uint rootOffset, uint treeEndOffset)
        {
            return basePointer != null &&
                   treeOffset <= rootOffset &&
                   treeEndOffset >= H8StaticDataFormat.CacheLineBytes &&
                   rootOffset <= treeEndOffset - H8StaticDataFormat.CacheLineBytes &&
                   (treeOffset & 63u) == 0u &&
                   (rootOffset & 63u) == 0u &&
                   (treeEndOffset & 63u) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBTreeNodeOffsetValid(uint treeOffset, uint treeEndOffset, uint nodeOffset)
        {
            return treeEndOffset >= H8StaticDataFormat.CacheLineBytes &&
                   nodeOffset >= treeOffset &&
                   nodeOffset <= treeEndOffset - H8StaticDataFormat.CacheLineBytes &&
                   (nodeOffset & 63u) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint TouchNode(byte* basePointer, uint treeOffset, uint treeEndOffset, uint nodeOffset)
        {
            if (!IsBTreeNodeOffsetValid(treeOffset, treeEndOffset, nodeOffset))
                return 0u;

            return UnsafeUtility.ReadArrayElement<uint>(basePointer + nodeOffset, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long QuantizeToMortonLane(double value, double cellMeters)
        {
            double finite = math.select(0d, value, math.isfinite(value));
            long quantized = (long)math.floor(finite / cellMeters);
            long biased = quantized + 1048576L;
            if (biased < 0L)
                return 0L;
            if (biased > 2097151L)
                return 2097151L;
            return biased;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Part21By3(ulong value)
        {
            value &= 0x1FFFFFul;
            value = (value | (value << 32)) & 0x1F00000000FFFFul;
            value = (value | (value << 16)) & 0x1F0000FF0000FFul;
            value = (value | (value << 8)) & 0x100F00F00F00F00Ful;
            value = (value | (value << 4)) & 0x10C30C30C30C30C3ul;
            value = (value | (value << 2)) & 0x1249249249249249ul;
            return value;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct ScanBTreeNodeJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public BTreeNodeDTO* Node;
            [WriteOnly, NoAlias] public NativeArray<int> OutputIndex;
            public uint TargetHash;

            public void Execute()
            {
                if (Node == null || !OutputIndex.IsCreated || OutputIndex.Length <= 0)
                    return;

                ref readonly BTreeNodeDTO node = ref UnsafeUtility.AsRef<BTreeNodeDTO>(Node);
                int keyCount = GetKeyCount(in node);
                OutputIndex[0] = IsLeaf(in node)
                    ? ScanExactKeyIndex(in node, TargetHash, keyCount)
                    : ScanBranchIndex(in node, TargetHash, keyCount);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct TraverseBTreeJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* BasePointer;
            [WriteOnly, NoAlias] public NativeArray<DataOffsetLengthDTO> Output;
            public uint TreeOffset;
            public uint RootOffset;
            public uint TreeEndOffset;
            public uint TargetHash;
            public float GlobalQualityWeight;

            public void Execute()
            {
                if (!Output.IsCreated || Output.Length <= 0)
                    return;

                bool found = TryFindValue(
                    BasePointer,
                    TreeOffset,
                    RootOffset,
                    TreeEndOffset,
                    TargetHash,
                    GlobalQualityWeight,
                    out uint value,
                    out _,
                    out _,
                    out _);

                Output[0] = new DataOffsetLengthDTO
                {
                    Hash = TargetHash,
                    ByteOffset = found ? value : NotFound,
                    ByteLength = 0u,
                    Flags = found ? ResultFoundFlag : ResultMissingFlag
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct DispatchBulkBTreeSearchJob : IJobParallelFor
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* BasePointer;
            [ReadOnly, NoAlias] public NativeArray<uint> RequestedHashes;
            [WriteOnly, NoAlias] public NativeArray<DataOffsetLengthDTO> Output;
            public uint TreeOffset;
            public uint RootOffset;
            public uint TreeEndOffset;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                uint target = RequestedHashes[index];
                bool found = TryFindValue(
                    BasePointer,
                    TreeOffset,
                    RootOffset,
                    TreeEndOffset,
                    target,
                    GlobalQualityWeight,
                    out uint value,
                    out _,
                    out _,
                    out _);

                Output[index] = new DataOffsetLengthDTO
                {
                    Hash = target,
                    ByteOffset = found ? value : NotFound,
                    ByteLength = 0u,
                    Flags = found ? ResultFoundFlag : ResultMissingFlag
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct FlushBTreeTelemetryPostSimulationJob : IJob
        {
            [NoAlias] public NativeArray<BTreeTelemetryEntry> Ring;
            [NoAlias] public NativeArray<int> Cursor;
            [NoAlias] public NativeArray<BTreeTelemetryAccumulatorDTO> Accumulator;

            public void Execute()
            {
                if (!Ring.IsCreated ||
                    !Cursor.IsCreated ||
                    !Accumulator.IsCreated ||
                    Ring.Length < H8StaticDataFormat.TelemetryFrameCount ||
                    Cursor.Length <= 0 ||
                    Accumulator.Length <= 0)
                {
                    return;
                }

                BTreeTelemetryAccumulatorDTO accumulator = Accumulator[0];
                uint dumpRequest = accumulator.BatchElapsedNs > BTreeSlowBatchThresholdNs ? 1u : 0u;
                if (dumpRequest != 0u)
                    accumulator.Flags |= BTreeTelemetrySlowBatchFlag;

                int cursor = Cursor[0];
                if ((uint)cursor >= H8StaticDataFormat.TelemetryFrameCount)
                    cursor = 0;

                accumulator.DumpRequestFlag = dumpRequest;
                Ring[cursor] = BuildTelemetryEntry(in accumulator);
                Cursor[0] = (cursor + 1) % H8StaticDataFormat.TelemetryFrameCount;

                Accumulator[0] = new BTreeTelemetryAccumulatorDTO
                {
                    FrameIndex = accumulator.FrameIndex + 1u,
                    GlobalQualityWeight = accumulator.GlobalQualityWeight,
                    DumpRequestFlag = dumpRequest
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct TraceBTreeTraversalJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* BasePointer;
            [WriteOnly, NoAlias] public NativeArray<DataOffsetLengthDTO> Output;
            [WriteOnly, NoAlias] public NativeArray<uint> TouchedNodeOffsets;
            public uint TreeOffset;
            public uint RootOffset;
            public uint TreeEndOffset;
            public uint TargetHash;
            public float GlobalQualityWeight;

            public void Execute()
            {
                if (!Output.IsCreated || Output.Length <= 0)
                    return;

                uint* trace = null;
                int traceCapacity = 0;
                if (TouchedNodeOffsets.IsCreated && TouchedNodeOffsets.Length > 0)
                {
                    trace = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TouchedNodeOffsets);
                    traceCapacity = TouchedNodeOffsets.Length;
                    for (int i = 0; i < traceCapacity; i++)
                        TouchedNodeOffsets[i] = NotFound;
                }

                bool found = TryFindValueWithTrace(
                    BasePointer,
                    TreeOffset,
                    RootOffset,
                    TreeEndOffset,
                    TargetHash,
                    GlobalQualityWeight,
                    trace,
                    traceCapacity,
                    out uint touchedNodeCount,
                    out uint value,
                    out _,
                    out _,
                    out _);

                Output[0] = new DataOffsetLengthDTO
                {
                    Hash = TargetHash,
                    ByteOffset = found ? value : NotFound,
                    ByteLength = touchedNodeCount,
                    Flags = found ? ResultFoundFlag : ResultMissingFlag
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct GenerateMockBTreeJob : IJob
        {
            [NoAlias] public NativeArray<byte> OutputBytes;
            [WriteOnly, NoAlias] public NativeArray<uint> OutputMetadata;
            public uint TreeOffset;

            public void Execute()
            {
                if (OutputMetadata.IsCreated && OutputMetadata.Length >= 4)
                {
                    OutputMetadata[0] = 0u;
                    OutputMetadata[1] = 0u;
                    OutputMetadata[2] = 0u;
                    OutputMetadata[3] = 0u;
                }

                if (!OutputBytes.IsCreated ||
                    TreeOffset >= (uint)OutputBytes.Length ||
                    (TreeOffset & 63u) != 0u)
                {
                    return;
                }

                byte* basePointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputBytes);
                int nodeCapacity = (OutputBytes.Length - (int)TreeOffset) / H8StaticDataFormat.CacheLineBytes;
                if (nodeCapacity < 10)
                    return;

                int leafCount = math.min(512, nodeCapacity - 1);
                int level1Count = 0;
                int level2Count = 0;
                int totalNodeCount = 0;
                while (leafCount >= 8)
                {
                    level1Count = (leafCount + H8StaticDataFormat.BTreeNodeChildCapacity - 1) / H8StaticDataFormat.BTreeNodeChildCapacity;
                    level2Count = (level1Count + H8StaticDataFormat.BTreeNodeChildCapacity - 1) / H8StaticDataFormat.BTreeNodeChildCapacity;
                    totalNodeCount = leafCount + level1Count + level2Count + 1;
                    if (totalNodeCount <= nodeCapacity)
                        break;

                    leafCount--;
                }

                if (leafCount < 8 || totalNodeCount <= 0)
                    return;

                BTreeNodeDTO* nodes = (BTreeNodeDTO*)(basePointer + TreeOffset);
                for (int leaf = 0; leaf < leafCount; leaf++)
                {
                    BTreeNodeDTO node = default;
                    for (int key = 0; key < H8StaticDataFormat.BTreeNodeKeyCapacity; key++)
                    {
                        uint hash = (uint)((leaf * H8StaticDataFormat.BTreeNodeKeyCapacity) + key + 1);
                        SetKey(ref node, key, hash);
                        SetChild(ref node, key, hash - 1u);
                    }

                    node.Meta = MakeLeafMeta(H8StaticDataFormat.BTreeNodeKeyCapacity);
                    nodes[leaf] = node;
                }

                int level1Start = leafCount;
                for (int level1 = 0; level1 < level1Count; level1++)
                {
                    int firstLeaf = level1 * H8StaticDataFormat.BTreeNodeChildCapacity;
                    int childCount = math.min(H8StaticDataFormat.BTreeNodeChildCapacity, leafCount - firstLeaf);
                    BTreeNodeDTO node = default;
                    for (int child = 0; child < childCount; child++)
                    {
                        int leafIndex = firstLeaf + child;
                        SetChild(ref node, child, TreeOffset + ((uint)leafIndex * H8StaticDataFormat.CacheLineBytes));
                        if (child < childCount - 1)
                            SetKey(ref node, child, (uint)((leafIndex + 1) * H8StaticDataFormat.BTreeNodeKeyCapacity));
                    }

                    node.Meta = MakeInternalMeta(childCount - 1);
                    nodes[level1Start + level1] = node;
                }

                int level2Start = level1Start + level1Count;
                for (int level2 = 0; level2 < level2Count; level2++)
                {
                    int firstLevel1 = level2 * H8StaticDataFormat.BTreeNodeChildCapacity;
                    int childCount = math.min(H8StaticDataFormat.BTreeNodeChildCapacity, level1Count - firstLevel1);
                    BTreeNodeDTO node = default;
                    for (int child = 0; child < childCount; child++)
                    {
                        int level1Index = firstLevel1 + child;
                        SetChild(ref node, child, TreeOffset + ((uint)(level1Start + level1Index) * H8StaticDataFormat.CacheLineBytes));
                        if (child < childCount - 1)
                        {
                            int lastLeafExclusive = math.min(leafCount, (level1Index + 1) * H8StaticDataFormat.BTreeNodeChildCapacity);
                            SetKey(ref node, child, (uint)(lastLeafExclusive * H8StaticDataFormat.BTreeNodeKeyCapacity));
                        }
                    }

                    node.Meta = MakeInternalMeta(childCount - 1);
                    nodes[level2Start + level2] = node;
                }

                int rootIndex = totalNodeCount - 1;
                BTreeNodeDTO root = default;
                for (int child = 0; child < level2Count; child++)
                {
                    SetChild(ref root, child, TreeOffset + ((uint)(level2Start + child) * H8StaticDataFormat.CacheLineBytes));
                    if (child < level2Count - 1)
                    {
                        int lastLevel1Exclusive = math.min(level1Count, (child + 1) * H8StaticDataFormat.BTreeNodeChildCapacity);
                        int lastLeafExclusive = math.min(leafCount, lastLevel1Exclusive * H8StaticDataFormat.BTreeNodeChildCapacity);
                        SetKey(ref root, child, (uint)(lastLeafExclusive * H8StaticDataFormat.BTreeNodeKeyCapacity));
                    }
                }

                root.Meta = MakeInternalMeta(level2Count - 1);
                nodes[rootIndex] = root;
                if (OutputMetadata.IsCreated && OutputMetadata.Length >= 4)
                {
                    OutputMetadata[0] = TreeOffset + ((uint)rootIndex * H8StaticDataFormat.CacheLineBytes);
                    OutputMetadata[1] = (uint)totalNodeCount;
                    OutputMetadata[2] = (uint)(leafCount * H8StaticDataFormat.BTreeNodeKeyCapacity);
                    OutputMetadata[3] = TreeOffset + ((uint)totalNodeCount * H8StaticDataFormat.CacheLineBytes);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct SpatialMortonRangeQueryJob : IJob
        {
            [NoAlias, NativeDisableUnsafePtrRestriction] public byte* BasePointer;
            [WriteOnly, NoAlias] public NativeArray<DataOffsetLengthDTO> Output;
            public uint TreeOffset;
            public uint RootOffset;
            public uint TreeEndOffset;
            public ulong LowerMorton;
            public ulong UpperMorton;

            public void Execute()
            {
                if (!Output.IsCreated || Output.Length <= 0)
                    return;

                bool found = TryFindMortonRangeFirstValue(
                    BasePointer,
                    TreeOffset,
                    RootOffset,
                    TreeEndOffset,
                    LowerMorton,
                    UpperMorton,
                    out uint value,
                    out _,
                    out _);

                Output[0] = new DataOffsetLengthDTO
                {
                    Hash = (uint)LowerMorton,
                    ByteOffset = found ? value : NotFound,
                    ByteLength = 0u,
                    Flags = found ? ResultFoundFlag : ResultMissingFlag
                };
            }
        }
    }

    public static class SpatialMortonBTreeCompiler
    {
        public static bool TryBuild(
            NativeArray<SpatialMortonBTreeRecordDTO> records,
            NativeArray<MortonBTreeNodeDTO> outputNodes,
            NativeArray<SpatialMortonLevelEntryDTO> scratchCurrentLevel,
            NativeArray<SpatialMortonLevelEntryDTO> scratchNextLevel,
            uint treeOffset,
            out int nodeCount)
        {
            nodeCount = 0;
            if ((treeOffset & 63u) != 0u ||
                !outputNodes.IsCreated ||
                outputNodes.Length <= 0 ||
                !scratchCurrentLevel.IsCreated ||
                !scratchNextLevel.IsCreated ||
                scratchCurrentLevel.Length < outputNodes.Length ||
                scratchNextLevel.Length < outputNodes.Length)
            {
                return false;
            }

            if (!records.IsCreated || records.Length == 0)
            {
                MortonBTreeNodeDTO emptyRoot = default;
                emptyRoot.Meta = H8CacheBTree.MakeMortonLeafMeta(0);
                outputNodes[0] = emptyRoot;
                nodeCount = 1;
                return true;
            }

            SortRecordsByMorton(records);

            int leafCount = (records.Length + H8StaticDataFormat.MortonBTreeNodeKeyCapacity - 1) / H8StaticDataFormat.MortonBTreeNodeKeyCapacity;
            int requiredMax = (leafCount * 2) + H8StaticDataFormat.MortonBTreeNodeChildCapacity;
            if (outputNodes.Length < requiredMax)
                return false;

            int currentCount = 0;
            for (int recordIndex = 0; recordIndex < records.Length;)
            {
                MortonBTreeNodeDTO node = default;
                int keyCount = math.min(H8StaticDataFormat.MortonBTreeNodeKeyCapacity, records.Length - recordIndex);
                for (int key = 0; key < keyCount; key++)
                {
                    SpatialMortonBTreeRecordDTO record = records[recordIndex + key];
                    H8CacheBTree.SetMortonKey(ref node, key, record.MortonKey);
                    H8CacheBTree.SetMortonChild(ref node, key, record.Value);
                }

                node.Meta = H8CacheBTree.MakeMortonLeafMeta(keyCount);
                outputNodes[nodeCount] = node;
                scratchCurrentLevel[currentCount] = new SpatialMortonLevelEntryDTO
                {
                    NodeIndex = nodeCount,
                    MaxKey = records[recordIndex + keyCount - 1].MortonKey
                };
                nodeCount++;
                currentCount++;
                recordIndex += keyCount;
            }

            while (currentCount > 1)
            {
                int nextCount = 0;
                for (int levelIndex = 0; levelIndex < currentCount;)
                {
                    int childCount = math.min(H8StaticDataFormat.MortonBTreeNodeChildCapacity, currentCount - levelIndex);
                    MortonBTreeNodeDTO node = default;
                    for (int child = 0; child < childCount; child++)
                    {
                        SpatialMortonLevelEntryDTO childEntry = scratchCurrentLevel[levelIndex + child];
                        H8CacheBTree.SetMortonChild(
                            ref node,
                            child,
                            treeOffset + ((uint)childEntry.NodeIndex * H8StaticDataFormat.CacheLineBytes));

                        if (child < childCount - 1)
                            H8CacheBTree.SetMortonKey(ref node, child, childEntry.MaxKey);
                    }

                    node.Meta = H8CacheBTree.MakeMortonInternalMeta(childCount - 1);
                    outputNodes[nodeCount] = node;
                    scratchNextLevel[nextCount] = new SpatialMortonLevelEntryDTO
                    {
                        NodeIndex = nodeCount,
                        MaxKey = scratchCurrentLevel[levelIndex + childCount - 1].MaxKey
                    };
                    nodeCount++;
                    nextCount++;
                    levelIndex += childCount;
                }

                NativeArray<SpatialMortonLevelEntryDTO> swap = scratchCurrentLevel;
                scratchCurrentLevel = scratchNextLevel;
                scratchNextLevel = swap;
                currentCount = nextCount;
            }

            return true;
        }

        private static void SortRecordsByMorton(NativeArray<SpatialMortonBTreeRecordDTO> records)
        {
            for (int i = 1; i < records.Length; i++)
            {
                SpatialMortonBTreeRecordDTO value = records[i];
                int insert = i - 1;
                while (insert >= 0 && records[insert].MortonKey > value.MortonKey)
                {
                    records[insert + 1] = records[insert];
                    insert--;
                }

                records[insert + 1] = value;
            }
        }
    }

    /// <summary>
    /// Dependency-free text request payload used by Babel vacuum tests. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockTextRequestSignal : ISignal
    {
        [FieldOffset(0)] public uint TextHash;
        [FieldOffset(4)] public uint FrameIndex;
        [FieldOffset(8)] public ushort LocaleId;
        [FieldOffset(10)] public ushort Flags;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>
    /// Decoupled voice-over request. Audio owns consumption. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct PlayVoiceOverSignal : ISignal
    {
        [FieldOffset(0)] public uint TextHash;
        [FieldOffset(4)] public uint VoiceHash;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Blind UI output buffer contract for lookup smoke jobs. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct MockUIBuffer
    {
        [FieldOffset(0)] public byte* Ptr;
        [FieldOffset(8)] public int CapacityBytes;
        [FieldOffset(12)] public int WrittenBytes;
    }

    public partial struct MockSpanConverter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountBytes(ReadOnlySpan<byte> utf8Bytes)
        {
            return utf8Bytes.Length;
        }
    }

    /// <summary>
    /// Static item balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct H8ItemStaticRecord
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint NameHash;
        [FieldOffset(8)] public uint DescriptionHash;
        [FieldOffset(12)] public uint CategoryId;
        [FieldOffset(16)] public int Cost;
        [FieldOffset(20)] public ushort StackMax;
        [FieldOffset(22)] public ushort IconIndex;
        [FieldOffset(24)] public float MassKg;
        [FieldOffset(28)] public float AccessFrequency;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] public uint Reserved2;
    }

    /// <summary>
    /// Static economy balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct H8EconomyStaticRecord
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint NameHash;
        [FieldOffset(8)] public uint DescriptionHash;
        [FieldOffset(12)] public uint ReservedKey;
        [FieldOffset(16)] public float BasePrice;
        [FieldOffset(20)] public float Scarcity01;
        [FieldOffset(24)] public float Demand01;
        [FieldOffset(28)] public float SupplyRefreshSeconds;
        [FieldOffset(32)] public float AccessFrequency;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
    }

    /// <summary>
    /// Static physics balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct H8PhysicsStaticRecord
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint NameHash;
        [FieldOffset(8)] public uint DescriptionHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float MassKg;
        [FieldOffset(20)] public float AddedMass;
        [FieldOffset(24)] public float LinearDrag;
        [FieldOffset(28)] public float Buoyancy;
        [FieldOffset(32)] public float CrushDepthM;
        [FieldOffset(36)] public float AccessFrequency;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
    }

    /// <summary>
    /// Static fauna balance payload. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct H8FaunaStaticRecord
    {
        [FieldOffset(0)] public uint Hash;
        [FieldOffset(4)] public uint NameHash;
        [FieldOffset(8)] public uint DescriptionHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float SwimSpeed;
        [FieldOffset(20)] public float TurnRate;
        [FieldOffset(24)] public float Aggression01;
        [FieldOffset(28)] public float FleeDistanceM;
        [FieldOffset(32)] public float BiolumIntensity;
        [FieldOffset(36)] public float AccessFrequency;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
    }

    /// <summary>
    /// Static data black-box entry. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8StaticDataTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint LastRequestedHash;
        [FieldOffset(12)] public uint LookupCount;
        [FieldOffset(16)] public uint RecordCount;
        [FieldOffset(20)] public uint PayloadCrc32;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint SchemaHash;
        [FieldOffset(32)] public long FileByteLength;
        [FieldOffset(40)] public long LastOffset;
        [FieldOffset(48)] public uint ErrorHash;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    /// <summary>
    /// Fixed header for static-data black-box dumps. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8StaticDataDumpHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint EntrySizeBytes;
        [FieldOffset(16)] public uint SchemaHash;
        [FieldOffset(20)] public uint PayloadCrc32;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Bake result for cold-path editor and test callers.
    /// </summary>
    public struct H8DataBakeResult
    {
        public bool Success;
        public int RecordCount;
        public int StringCount;
        public int PaddingRepairCount;
        public uint StaticDataCrc32;
        public uint BabelCrc32;
        public string StaticDataPath;
        public string BabelPath;
        public string Message;
    }

    /// <summary>
    /// Sanity scan result for static binary validation.
    /// </summary>
    public struct H8StaticDataSanityReport
    {
        public bool IsClean;
        public int RecordsScanned;
        public uint FailedHash;
        public ushort FailedRecordType;
        public string Message;
    }

    /// <summary>
    /// Allocation-free hash helper and hash-manifest cold tool.
    /// </summary>
    public static class H8DataHashTool
    {
        public const uint FnvOffset32 = 2166136261u;
        public const uint FnvPrime32 = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1a32(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset32;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);

                hash = unchecked((hash ^ (byte)c) * FnvPrime32);
            }

            return hash == 0u ? FnvOffset32 : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1a32(ReadOnlySpan<byte> value)
        {
            uint hash = FnvOffset32;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);

                hash = unchecked((hash ^ b) * FnvPrime32);
            }

            return hash == 0u ? FnvOffset32 : hash;
        }

        /// <summary>
        /// Computes FNV-1a over the UTF8 byte representation of human-facing Babel text.
        /// </summary>
        public static uint ComputeFnv1a32Utf8(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset32;
            for (int i = 0; i < value.Length; i++)
            {
                uint codePoint;
                char c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    codePoint = (uint)char.ConvertToUtf32(c, value[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(c))
                {
                    codePoint = 0xFFFDu;
                }
                else
                {
                    codePoint = c;
                }

                hash = HashUtf8CodePoint(hash, codePoint);
            }

            return hash == 0u ? FnvOffset32 : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashByte(uint hash, byte value)
        {
            return unchecked((hash ^ value) * FnvPrime32);
        }

        private static uint HashUtf8CodePoint(uint hash, uint codePoint)
        {
            if (codePoint <= 0x7Fu)
                return HashByte(hash, (byte)codePoint);

            if (codePoint <= 0x7FFu)
            {
                hash = HashByte(hash, (byte)(0xC0u | (codePoint >> 6)));
                return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
            }

            if (codePoint <= 0xFFFFu)
            {
                hash = HashByte(hash, (byte)(0xE0u | (codePoint >> 12)));
                hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 6) & 0x3Fu)));
                return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
            }

            hash = HashByte(hash, (byte)(0xF0u | (codePoint >> 18)));
            hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 12) & 0x3Fu)));
            hash = HashByte(hash, (byte)(0x80u | ((codePoint >> 6) & 0x3Fu)));
            return HashByte(hash, (byte)(0x80u | (codePoint & 0x3Fu)));
        }

#if UNITY_EDITOR
        public static H8DataBakeResult GenerateHashManifest(string csvPath, string outputPath)
        {
            if (string.IsNullOrEmpty(csvPath) || !System.IO.File.Exists(csvPath))
                return Fail("CSV file missing.");

            H8CsvTable table = H8CsvReader.Read(csvPath);
            using (System.IO.FileStream stream = new System.IO.FileStream(
                outputPath,
                System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.Read))
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("Id,Fnv1a32");
                for (int i = 0; i < table.RowCount; i++)
                {
                    string id = table.Get(i, 0);
                    uint hash = ComputeFnv1a32(id.AsSpan());
                    writer.Write(id);
                    writer.Write(',');
                    writer.Write(hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteLine();
                }
            }

            return new H8DataBakeResult
            {
                Success = true,
                RecordCount = table.RowCount,
                StaticDataPath = outputPath,
                Message = "Hash manifest generated."
            };
        }

        private static H8DataBakeResult Fail(string message)
        {
            return new H8DataBakeResult
            {
                Success = false,
                Message = message
            };
        }
#endif
    }

    /// <summary>
    /// Allocation-free cold parser for Data/Balance/btree_tuning_profiles.csv.
    /// </summary>
    public static class BTreeTuningCsvParser
    {
        public const uint ErrorNone = 0u;
        public const uint ErrorOutput = 0x4F555450u;
        public const uint ErrorCapacity = 0x43415021u;
        public const uint ErrorBranching = 0x4252414Eu;
        public const uint ErrorPrefetch = 0x50524621u;
        public const uint ErrorBatch = 0x42415421u;
        public const uint ErrorDepth = 0x44455021u;
        public const uint ErrorFlags = 0x464C4721u;
        public const uint ErrorQuality = 0x51554121u;

        public static bool TryParse(
            ReadOnlySpan<byte> bytes,
            NativeArray<BTreeTuningProfileDTO> output,
            out int count,
            out uint errorHash)
        {
            count = 0;
            errorHash = ErrorNone;
            if (!output.IsCreated || output.Length <= 0)
            {
                errorHash = ErrorOutput;
                return false;
            }

            int index = 0;
            while (index < bytes.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(bytes, ref index);
                if (!TryTrim(line, out int start, out int end))
                    continue;

                line = line.Slice(start, end - start);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int cursor = 0;
                ReadOnlySpan<byte> profile = ReadCell(line, ref cursor);
                if (IsProfileHeader(profile))
                    continue;

                if (count >= output.Length)
                {
                    errorHash = ErrorCapacity;
                    return false;
                }

                if (!TryParseUInt(ReadCell(line, ref cursor), out uint branching) || branching != H8StaticDataFormat.BTreeNodeKeyCapacity)
                {
                    errorHash = ErrorBranching;
                    return false;
                }

                if (!TryParseUnitFloat(ReadCell(line, ref cursor), out float prefetchAggression))
                {
                    errorHash = ErrorPrefetch;
                    return false;
                }

                if (!TryParseUInt(ReadCell(line, ref cursor), out uint batchSize) || batchSize == 0u)
                {
                    errorHash = ErrorBatch;
                    return false;
                }

                if (!TryParseUInt(ReadCell(line, ref cursor), out uint maxDepth) || maxDepth == 0u || maxDepth > H8CacheBTree.MaxTraversalDepth)
                {
                    errorHash = ErrorDepth;
                    return false;
                }

                if (!TryParseUInt(ReadCell(line, ref cursor), out uint flags))
                {
                    errorHash = ErrorFlags;
                    return false;
                }

                if (!TryParseUnitFloat(ReadCell(line, ref cursor), out float qualityMin) ||
                    !TryParseUnitFloat(ReadCell(line, ref cursor), out float qualityMax) ||
                    qualityMax < qualityMin)
                {
                    errorHash = ErrorQuality;
                    return false;
                }

                output[count] = new BTreeTuningProfileDTO
                {
                    ProfileHash = H8DataHashTool.ComputeFnv1a32(profile),
                    Flags = flags,
                    BranchingFactor = branching,
                    BatchSize = batchSize,
                    MaxDepth = maxDepth,
                    ProfileIndex = (uint)count,
                    PrefetchAggression = prefetchAggression,
                    QualityMin = qualityMin,
                    QualityMax = qualityMax,
                    GlobalQualityWeight = math.saturate((qualityMin + qualityMax) * 0.5f)
                };
                count++;
            }

            return count > 0;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            int start = index;
            while (index < bytes.Length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;

            int end = index;
            while (index < bytes.Length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;

            return bytes.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            while (start < end && IsWhitespace(line[start]))
                start++;
            while (end > start && IsWhitespace(line[end - 1]))
                end--;

            return line.Slice(start, end - start);
        }

        private static bool TryTrim(ReadOnlySpan<byte> line, out int start, out int end)
        {
            start = 0;
            end = line.Length;
            if (end >= 3 && line[0] == 0xEF && line[1] == 0xBB && line[2] == 0xBF)
                start = 3;

            while (start < end && IsWhitespace(line[start]))
                start++;
            while (end > start && IsWhitespace(line[end - 1]))
                end--;

            return end > start;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            if (token.Length == 0)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                value = (value * 10u) + (uint)(b - (byte)'0');
            }

            return true;
        }

        private static bool TryParseUnitFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            uint whole = 0u;
            uint fraction = 0u;
            uint scale = 1u;
            bool afterDecimal = false;
            bool any = false;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b == (byte)'.')
                {
                    if (afterDecimal)
                        return false;
                    afterDecimal = true;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                any = true;
                uint digit = (uint)(b - (byte)'0');
                if (afterDecimal)
                {
                    if (scale < 1000000u)
                    {
                        fraction = (fraction * 10u) + digit;
                        scale *= 10u;
                    }
                }
                else
                {
                    whole = (whole * 10u) + digit;
                }
            }

            if (!any)
                return false;

            value = math.saturate((float)whole + ((float)fraction / scale));
            return true;
        }

        private static bool IsProfileHeader(ReadOnlySpan<byte> token)
        {
            return token.Length == 7 &&
                   ToLower(token[0]) == (byte)'p' &&
                   ToLower(token[1]) == (byte)'r' &&
                   ToLower(token[2]) == (byte)'o' &&
                   ToLower(token[3]) == (byte)'f' &&
                   ToLower(token[4]) == (byte)'i' &&
                   ToLower(token[5]) == (byte)'l' &&
                   ToLower(token[6]) == (byte)'e';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }
    }

    /// <summary>
    /// CRC32 without runtime table allocation.
    /// </summary>
    public static unsafe class H8Crc32
    {
        public static uint Compute(byte* data, int byteLength)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < byteLength; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return ~crc;
        }
    }

    internal static unsafe class H8StaticDataBlackBoxDump
    {
        public static void Write(
            string path,
            H8StaticDataTelemetryEntry* ring,
            int cursorValue,
            uint payloadCrc32,
            uint flags)
        {
            if (ring == null || string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if ((uint)cursorValue >= H8StaticDataFormat.TelemetryFrameCount)
                cursorValue = 0;

            int entrySize = UnsafeUtility.SizeOf<H8StaticDataTelemetryEntry>();
            H8StaticDataDumpHeader header = new H8StaticDataDumpHeader
            {
                Magic = H8StaticDataFormat.TelemetryDumpMagic,
                EntryCount = H8StaticDataFormat.TelemetryFrameCount,
                EntrySizeBytes = (uint)entrySize,
                SchemaHash = H8StaticDataFormat.SchemaHash,
                PayloadCrc32 = payloadCrc32,
                Flags = flags
            };

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(new ReadOnlySpan<byte>(&header, UnsafeUtility.SizeOf<H8StaticDataDumpHeader>()));
                for (int i = 0; i < H8StaticDataFormat.TelemetryFrameCount; i++)
                {
                    int sourceIndex = (cursorValue + i) % H8StaticDataFormat.TelemetryFrameCount;
                    stream.Write(new ReadOnlySpan<byte>(ring + sourceIndex, entrySize));
                }
            }
        }
    }

    public static unsafe class H8BTreeTelemetryDump
    {
        public static void Write(
            string path,
            BTreeTelemetryEntry* ring,
            int cursorValue,
            uint flags)
        {
            if (ring == null || string.IsNullOrEmpty(path))
                return;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if ((uint)cursorValue >= H8StaticDataFormat.TelemetryFrameCount)
                cursorValue = 0;

            int entrySize = UnsafeUtility.SizeOf<BTreeTelemetryEntry>();
            H8StaticDataDumpHeader header = new H8StaticDataDumpHeader
            {
                Magic = H8StaticDataFormat.TelemetryDumpMagic,
                EntryCount = H8StaticDataFormat.TelemetryFrameCount,
                EntrySizeBytes = (uint)entrySize,
                SchemaHash = H8StaticDataFormat.SchemaHash ^ 0x42545245u,
                PayloadCrc32 = 0u,
                Flags = flags
            };

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(new ReadOnlySpan<byte>(&header, UnsafeUtility.SizeOf<H8StaticDataDumpHeader>()));
                for (int i = 0; i < H8StaticDataFormat.TelemetryFrameCount; i++)
                {
                    int sourceIndex = (cursorValue + i) % H8StaticDataFormat.TelemetryFrameCount;
                    stream.Write(new ReadOnlySpan<byte>(ring + sourceIndex, entrySize));
                }
            }
        }
    }
}
