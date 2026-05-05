using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Quest;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal static unsafe class AsyncWriteManager
    {
        internal struct ReadOnlyMapping
        {
            public FileStream FileStream;
            public MemoryMappedFile FileMapping;
            public MemoryMappedViewAccessor Accessor;
            public IntPtr View;
            public long Length;
        }

        internal static bool WriteAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            return WriteAll(absolutePath, buffer, byteCount, null, 0, out error);
        }

        internal static bool WriteAll(
            string absolutePath,
            void* firstBuffer,
            int firstByteCount,
            void* secondBuffer,
            int secondByteCount,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native write path is empty.";
                return false;
            }

            int totalBytes = math.max(firstByteCount, 0) + math.max(secondByteCount, 0);
            if (totalBytes <= 0)
            {
                error = "Native write requested zero bytes.";
                return false;
            }

            FileStream fileStream = null;
            MemoryMappedFile memoryMappedFile = null;
            MemoryMappedViewAccessor accessor = null;
            byte* mappedPointer = null;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                fileStream.SetLength(totalBytes);
                memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, totalBytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                accessor = memoryMappedFile.CreateViewAccessor(0L, totalBytes, MemoryMappedFileAccess.Write);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPointer);
                byte* destination = mappedPointer + accessor.PointerOffset;

                if (firstBuffer != null && firstByteCount > 0)
                {
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destination, totalBytes, firstBuffer, firstByteCount))
                    {
                        error = "Native mapped write first segment exceeded destination bounds.";
                        return false;
                    }
                }

                if (secondBuffer != null && secondByteCount > 0)
                {
                    int secondOffset = math.max(firstByteCount, 0);
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destination + secondOffset, totalBytes - secondOffset, secondBuffer, secondByteCount))
                    {
                        error = "Native mapped write second segment exceeded destination bounds.";
                        return false;
                    }
                }

                accessor.Flush();
                fileStream.Flush(true);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Memory-mapped write failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (accessor != null && mappedPointer != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                accessor?.Dispose();
                memoryMappedFile?.Dispose();
                fileStream?.Dispose();
            }
        }

        internal static bool TryReadAll(string absolutePath, void* buffer, int byteCount, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || buffer == null || byteCount <= 0)
            {
                error = "Native read request is invalid.";
                return false;
            }

            FileStream fileStream = null;
            MemoryMappedFile memoryMappedFile = null;
            MemoryMappedViewAccessor accessor = null;
            byte* mappedPointer = null;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = fileStream.Length;

                if (fileLength < byteCount)
                {
                    error = $"Native mapped read exceeded file length for '{absolutePath}'.";
                    return false;
                }

                memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                accessor = memoryMappedFile.CreateViewAccessor(0L, byteCount, MemoryMappedFileAccess.Read);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPointer);
                if (!UnsafeMemoryCopyGuard.SafeCopy(buffer, byteCount, mappedPointer + accessor.PointerOffset, byteCount))
                {
                    error = "Native mapped read exceeded destination bounds.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Memory-mapped read failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (accessor != null && mappedPointer != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                accessor?.Dispose();
                memoryMappedFile?.Dispose();
                fileStream?.Dispose();
            }
        }

        internal static bool TryGetFileLength(string absolutePath, out long fileLength, out string error)
        {
            fileLength = 0L;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

            try
            {
                fileLength = new FileInfo(absolutePath).Length;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to read file length for '{absolutePath}': {ex.Message}";
                return false;
            }
        }

        internal static bool TryOpenReadOnlyMapping(string absolutePath, out ReadOnlyMapping mapping, out string error)
        {
            mapping = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Native read path is empty.";
                return false;
            }

            FileStream fileStream = null;
            MemoryMappedFile memoryMappedFile = null;
            MemoryMappedViewAccessor accessor = null;
            byte* mappedPointer = null;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = fileStream.Length;

                if (fileLength <= 0L)
                {
                    error = $"Mapped read requested an empty file for '{absolutePath}'.";
                    return false;
                }

                memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                accessor = memoryMappedFile.CreateViewAccessor(0L, fileLength, MemoryMappedFileAccess.Read);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPointer);
                IntPtr mappedView = (IntPtr)(mappedPointer + accessor.PointerOffset);

                mapping = new ReadOnlyMapping
                {
                    FileStream = fileStream,
                    FileMapping = memoryMappedFile,
                    Accessor = accessor,
                    View = mappedView,
                    Length = fileLength
                };

                fileStream = null;
                memoryMappedFile = null;
                accessor = null;
                mappedPointer = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Memory-mapped open failed for '{absolutePath}': {ex.Message}";
                return false;
            }
            finally
            {
                if (accessor != null && mappedPointer != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                accessor?.Dispose();
                memoryMappedFile?.Dispose();
                fileStream?.Dispose();
            }
        }

        internal static void CloseReadOnlyMapping(ref ReadOnlyMapping mapping)
        {
            if (mapping.Accessor != null)
                mapping.Accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            mapping.Accessor?.Dispose();
            mapping.FileMapping?.Dispose();
            mapping.FileStream?.Dispose();
            mapping = default;
        }
    }
    internal static unsafe class SaveBinaryStorage
    {
        internal const uint Magic = 0x48454354u;
        internal const ushort CurrentVersion = 0x0008;
        internal const uint ExplorationMortonBuildSalt32 = 0x48384D4Fu;
        internal const int ExplorationMortonMaskAlignmentBytes = 64;
        internal const ushort MinimumSupportedVersion = 0x0003;
        internal const byte CurrentCompatMask = 0x0B;
        internal const byte FlagLz4Blocks = 0x01;
        internal const byte FlagTokenSubstitution = 0x02;
        internal const byte FlagIndexedSectorBlocks = 0x04;
        internal const int CurrentHeaderSize = 52;
        internal const int LegacyHeaderSize = 44;
        internal const int BlockSizeBytes = 256 * 1024;
        internal const int RawPayloadCapacityBytes = 64 * 1024 * 1024;
        internal const int MaxCompressedPayloadBytes = 67378176;

        private const long UnixEpochTicks = 621355968000000000L;
        private const int PayloadPrefixSizeBytes = SaveDataMigration_AupV8.CurrentPayloadPrefixSizeBytes;
        private const int PackedQuestStateSectionHeaderSize = 64;
        private const int PersistentWorldSectionHeaderSize = 12;
        private const int EcosystemSectionHeaderSize = 4;
        private const int SaveFileHeaderPrefixSize = 8;
        private const int LegacyHeaderHashSizeBytes = 36;
        private const int CurrentHeaderHashSizeBytes = 44;
        private const ushort First64BitHashVersion = 0x0004;
        private const ushort CompactPersistentWorldSectionVersion = 0x0005;
        private const ushort EcosystemSectionVersion = 0x0006;
        private const ushort TokenizedPayloadVersion = 0x0007;
        private const ushort IndexedBlockStorageVersion = 0x0008;
        private const int TokenizedPayloadHeaderSize = 8;
        private const int TokenBlockSizeBytes = 16;
        private const int MaxTokenCount = 64;
        private const byte TokenEscapeMarker = 0xFF;
        private const int IndexedSectorDirectoryHeaderSize = 16;
        private const int IndexedSectorBlockHeaderSize = 8;
        private const int IndexedSectorDirectorySlotCount = 4096;
        private const int StandardCompressedBlockHeaderBytes = 8;
        internal const int ModPayloadSubBlockSizeBytes = SaveBinaryPayloadCodec.ProtectedLz4BlockSizeBytes;
        internal const int ModPayloadHeaderSizeBytes = 32;
        internal const int ModPayloadMaxBytes = ModPayloadSubBlockSizeBytes - ModPayloadHeaderSizeBytes;
        internal const int IndexedSectorDirectoryCapacity = IndexedSectorDirectorySlotCount;
        private const int PersistentWorldSectorEdgeLengthMeters = 1000;
        private const double PersistentWorldSectorLoadRadiusMeters = 1000.0;
        private const double PersistentWorldSectorLoadRadiusSq = PersistentWorldSectorLoadRadiusMeters * PersistentWorldSectorLoadRadiusMeters;
        internal const int DefaultIndexedPersistentWorldChunkSizeMeters = 64;
        private const ushort PersistentWorldDeletedItemHashIndex = ushort.MaxValue;
        private const uint ModPayloadMagic = 0x50444F4Du; // "MODP"
        private const ushort ModPayloadVersion = 1;
        private const ulong ModPayloadSectorPrefix = 0x4D50000000000000UL;
        private const ulong ModPayloadSectorMask = 0xFFFF000000000000UL;
        private const string Lz4DllName = "liblz4";
        private const string NativeMemoryOwner = nameof(SaveBinaryStorage);
        private const string EntityStateWriteSourceStatesLabel = "indexedSectorEntityStateSourceStates";
        private const string EntityStateWriteSortEntriesLabel = "indexedSectorEntityStateSortEntries";
        private const string EntityStateWriteRadixScratchLabel = "indexedSectorEntityStateRadixScratch";
        private const string EntityStateWriteSortedStatesLabel = "indexedSectorEntityStateSortedStates";
        private const string EntityStateWriteFileBytesLabel = "indexedSectorEntityStateFileBytes";
        private const string EntityStateWriteResultLengthLabel = "indexedSectorEntityStateResultLength";
        private const string EntityStateWriteRadixCountsLabel = "indexedSectorEntityStateRadixCounts";
        private const string EntityStateWriteRadixOffsetsLabel = "indexedSectorEntityStateRadixOffsets";
        private static int s_indexedSectorQuarantineReported;
        private static readonly uint _indexedSectorQuarantineWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Save.IndexedSectorQuarantine"));

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = TokenizedPayloadHeaderSize)]
        private struct TokenizedPayloadHeader
        {
            public uint ExpandedPayloadLength;
            public ushort TokenCount;
            public ushort Reserved;
        }

        private readonly struct TokenKey : IEquatable<TokenKey>
        {
            public readonly ulong A;
            public readonly ulong B;

            public TokenKey(ulong a, ulong b)
            {
                A = a;
                B = b;
            }

            public bool Equals(TokenKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is TokenKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(A, B);
            }
        }

        private sealed class TokenStats
        {
            public TokenKey Key;
            public int Count;
            public int Index = -1;
        }

        private sealed class IndexedSectorGroup
        {
            public long SectorHash;
            public List<PersistentWorldDeltaRecord> Records;
        }

        private readonly struct IndexedSectorCommitTarget
        {
            public readonly bool ReusedExistingSlot;
            public readonly bool InsertedNewSlot;
            public readonly int SlotIndex;
            public readonly long WriteOffset;
            public readonly long NewFileLength;

            public IndexedSectorCommitTarget(bool reusedExistingSlot, bool insertedNewSlot, int slotIndex, long writeOffset, long newFileLength)
            {
                ReusedExistingSlot = reusedExistingSlot;
                InsertedNewSlot = insertedNewSlot;
                SlotIndex = slotIndex;
                WriteOffset = writeOffset;
                NewFileLength = newFileLength;
            }
        }

        internal struct IndexedSectorEntityStateWriteHandle
        {
            internal bool IsCreated;
            internal string AbsolutePath;
            internal JobHandle Handle;
            public NativeArray<EntityDataRecord> SourceStates;
            public NativeArray<SectorEntityStateSortEntry> SortEntries;
            public NativeArray<SectorEntityStateSortEntry> RadixScratch;
            public NativeArray<EntityDataRecord> SortedEntityStates;
            public NativeArray<byte> FileBytes;
            public NativeArray<int> ResultLength;
            public NativeArray<int> RadixCounts;
            public NativeArray<int> RadixOffsets;

            internal bool IsCompleted => IsCreated && Handle.IsCompleted;

            internal void RegisterNativeMemorySentinel()
            {
                RegisterArray(SourceStates, EntityStateWriteSourceStatesLabel);
                RegisterArray(SortEntries, EntityStateWriteSortEntriesLabel);
                RegisterArray(RadixScratch, EntityStateWriteRadixScratchLabel);
                RegisterArray(SortedEntityStates, EntityStateWriteSortedStatesLabel);
                RegisterArray(FileBytes, EntityStateWriteFileBytesLabel);
                RegisterArray(ResultLength, EntityStateWriteResultLengthLabel);
                RegisterArray(RadixCounts, EntityStateWriteRadixCountsLabel);
                RegisterArray(RadixOffsets, EntityStateWriteRadixOffsetsLabel);
            }

            private static void RegisterArray<T>(NativeArray<T> array, string label) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            }

            private void UnregisterNativeMemorySentinel()
            {
                UnregisterArray(SourceStates);
                UnregisterArray(SortEntries);
                UnregisterArray(RadixScratch);
                UnregisterArray(SortedEntityStates);
                UnregisterArray(FileBytes);
                UnregisterArray(ResultLength);
                UnregisterArray(RadixCounts);
                UnregisterArray(RadixOffsets);
            }

            private static void UnregisterArray<T>(NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                NativeMemorySentinel.UnregisterNativeArray(array);
            }

            internal void Dispose()
            {
                UnregisterNativeMemorySentinel();

                if (SourceStates.IsCreated)
                    SourceStates.Dispose();
                if (SortEntries.IsCreated)
                    SortEntries.Dispose();
                if (RadixScratch.IsCreated)
                    RadixScratch.Dispose();
                if (SortedEntityStates.IsCreated)
                    SortedEntityStates.Dispose();
                if (FileBytes.IsCreated)
                    FileBytes.Dispose();
                if (ResultLength.IsCreated)
                    ResultLength.Dispose();
                if (RadixCounts.IsCreated)
                    RadixCounts.Dispose();
                if (RadixOffsets.IsCreated)
                    RadixOffsets.Dispose();

                this = default;
            }

            internal JobHandle DisposeDeferred(JobHandle dependency)
            {
                UnregisterNativeMemorySentinel();

                JobHandle disposeHandle = JobHandle.CombineDependencies(Handle, dependency);
                if (SourceStates.IsCreated)
                    disposeHandle = SourceStates.Dispose(disposeHandle);
                if (SortEntries.IsCreated)
                    disposeHandle = SortEntries.Dispose(disposeHandle);
                if (RadixScratch.IsCreated)
                    disposeHandle = RadixScratch.Dispose(disposeHandle);
                if (SortedEntityStates.IsCreated)
                    disposeHandle = SortedEntityStates.Dispose(disposeHandle);
                if (FileBytes.IsCreated)
                    disposeHandle = FileBytes.Dispose(disposeHandle);
                if (ResultLength.IsCreated)
                    disposeHandle = ResultLength.Dispose(disposeHandle);
                if (RadixCounts.IsCreated)
                    disposeHandle = RadixCounts.Dispose(disposeHandle);
                if (RadixOffsets.IsCreated)
                    disposeHandle = RadixOffsets.Dispose(disposeHandle);

                this = default;
                return disposeHandle;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct IndexedBlockDecompressJob : IJob
        {
            [ReadOnly] public NativeArray<byte> CompressedPayload;
            public NativeArray<byte> DecompressedPayload;
            public NativeArray<int> ResultLength;
            public uint BlockFlags;

            public void Execute()
            {
                byte* compressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(CompressedPayload);
                byte* decompressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DecompressedPayload);

                int failedBlockIndex = -1;
                int decompressedLength = SaveBinaryStorage.Lz4BlockDecompressStandard(
                    compressedPtr,
                    CompressedPayload.Length,
                    decompressedPtr,
                    DecompressedPayload.Length,
                    out failedBlockIndex);
                if (decompressedLength > 0 && (BlockFlags & FlagTokenSubstitution) != 0)
                {
                    if (!TryExpandTokenizedPayloadInPlace(decompressedPtr, decompressedLength, DecompressedPayload.Length, out decompressedLength, out _))
                        decompressedLength = 0;
                }

                ResultLength[0] = decompressedLength;
            }
        }

        internal struct SectorEntityStateSortEntry
        {
            public ulong SortKey;
            public EntityDataRecord Record;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildSectorEntityStateSortEntriesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityDataRecord> SourceStates;
            public NativeArray<SectorEntityStateSortEntry> Entries;
            public int ChunkSizeMeters;

            public void Execute(int index)
            {
                EntityDataRecord record = SourceStates[index];
                AbsoluteUniversePositionBlit128 alignedPosition = record.Position;
                AbsoluteUniversePosition position = new AbsoluteUniversePosition
                {
                    GridX = alignedPosition.GridX,
                    GridY = alignedPosition.GridY,
                    GridZ = alignedPosition.GridZ,
                    LocalX = alignedPosition.Local.x,
                    LocalY = alignedPosition.Local.y,
                    LocalZ = alignedPosition.Local.z
                };

                Entries[index] = new SectorEntityStateSortEntry
                {
                    SortKey = SaveBinaryPayloadCodec.BuildSectorEntitySpatialSortKey(in position, ChunkSizeMeters),
                    Record = record
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RadixSortSectorEntityStateEntriesJob : IJob
        {
            public NativeArray<SectorEntityStateSortEntry> Entries;
            public NativeArray<SectorEntityStateSortEntry> Scratch;
            public NativeArray<int> Counts;
            public NativeArray<int> Offsets;

            public void Execute()
            {
                int entryCount = Entries.Length;
                if (entryCount <= 1)
                    return;

                RadixPass(Entries, Scratch, 0);
                RadixPass(Scratch, Entries, 16);
                RadixPass(Entries, Scratch, 32);
                RadixPass(Scratch, Entries, 48);
            }

            private void RadixPass(
                NativeArray<SectorEntityStateSortEntry> source,
                NativeArray<SectorEntityStateSortEntry> destination,
                int shift)
            {
                for (int i = 0; i < Counts.Length; i++)
                {
                    Counts[i] = 0;
                    Offsets[i] = 0;
                }

                for (int i = 0; i < source.Length; i++)
                {
                    int bucket = (int)((source[i].SortKey >> shift) & 0xFFFFUL);
                    Counts[bucket]++;
                }

                int cursor = 0;
                for (int i = 0; i < Counts.Length; i++)
                {
                    Offsets[i] = cursor;
                    cursor += Counts[i];
                }

                for (int i = 0; i < source.Length; i++)
                {
                    SectorEntityStateSortEntry entry = source[i];
                    int bucket = (int)((entry.SortKey >> shift) & 0xFFFFUL);
                    int destinationIndex = Offsets[bucket]++;
                    destination[destinationIndex] = entry;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ExtractSortedSectorEntityStatesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorEntityStateSortEntry> Entries;
            [WriteOnly] public NativeArray<EntityDataRecord> SortedStates;

            public void Execute(int index)
            {
                SortedStates[index] = Entries[index].Record;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CompressSectorEntityStateJob : IJob
        {
            [ReadOnly] public NativeArray<EntityDataRecord> SortedStates;
            public NativeArray<byte> FileBytes;
            public NativeArray<int> ResultLength;
            public long SectorHash;

            public void Execute()
            {
                int recordCount = SortedStates.Length;
                if (recordCount <= 0 || !FileBytes.IsCreated || FileBytes.Length <= UnsafeUtility.SizeOf<SectorEntityStateFileHeader>())
                {
                    ResultLength[0] = 0;
                    return;
                }

                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(FileBytes);
                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SortedStates);
                int rawByteLength = recordCount * UnsafeUtility.SizeOf<EntityDataRecord>();
                int compressedLength = SaveBinaryStorage.Lz4BlockCompress(
                    rawPtr,
                    rawByteLength,
                    filePtr + UnsafeUtility.SizeOf<SectorEntityStateFileHeader>(),
                    FileBytes.Length - UnsafeUtility.SizeOf<SectorEntityStateFileHeader>());
                if (compressedLength <= 0)
                {
                    ResultLength[0] = 0;
                    return;
                }

                SectorEntityStateFileHeader header = new SectorEntityStateFileHeader
                {
                    SectorHash = SectorHash,
                    CompressedSize = compressedLength,
                    DecompressedSize = rawByteLength,
                    RecordCount = (uint)recordCount,
                    Checksum = 0u
                };
                UnsafeUtility.CopyStructureToPtr(ref header, filePtr);
                ResultLength[0] = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>() + compressedLength;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = IndexedSectorDirectoryHeaderSize)]
        private struct IndexedSectorDirectoryHeader
        {
            public uint SectorCount;
            public int ChunkSizeMeters;
            public int MetadataCompressedSize;
            public int MetadataDecompressedSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = IndexedSectorBlockHeaderSize)]
        private struct IndexedSectorBlockHeader
        {
            public uint Flags;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]
        internal struct SectorEntry
        {
            public long SectorHash;
            public long ByteOffset;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Checksum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
        private struct SectorOverrideFileHeader
        {
            public long SectorHash;
            public int CompressedSize;
            public int DecompressedSize;
            public uint Checksum;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
        private struct SectorEntityStateFileHeader
        {
            public long SectorHash;
            public int CompressedSize;
            public int DecompressedSize;
            public uint RecordCount;
            public uint Checksum;
        }

        internal readonly struct IndexedSectorEntryInfo
        {
            public readonly long SectorHash;
            public readonly long ByteOffset;
            public readonly int CompressedSize;
            public readonly int DecompressedSize;
            public readonly uint Checksum;

            public IndexedSectorEntryInfo(long sectorHash, long byteOffset, int compressedSize, int decompressedSize, uint checksum)
            {
                SectorHash = sectorHash;
                ByteOffset = byteOffset;
                CompressedSize = compressedSize;
                DecompressedSize = decompressedSize;
                Checksum = checksum;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = ModPayloadHeaderSizeBytes)]
        private struct ModPayloadSubSectorHeader
        {
            public uint Magic;
            public ushort Version;
            public ushort HeaderSize;
            public uint ModHash;
            public ushort PayloadLength;
            public ushort Flags;
            public long PagedSectorHash;
            public uint PayloadChecksum;
            public uint Reserved;
        }

        internal readonly struct ModPayloadSectorInfo
        {
            public readonly long SectorHash;
            public readonly uint ModHash;
            public readonly long PagedSectorHash;
            public readonly int PayloadLength;
            public readonly uint PayloadChecksum;

            public ModPayloadSectorInfo(long sectorHash, uint modHash, long pagedSectorHash, int payloadLength, uint payloadChecksum)
            {
                SectorHash = sectorHash;
                ModHash = modHash;
                PagedSectorHash = pagedSectorHash;
                PayloadLength = payloadLength;
                PayloadChecksum = payloadChecksum;
            }
        }

        internal delegate bool ModPayloadReadHandler(
            in ModPayloadSectorInfo sectorInfo,
            NativeArray<byte> payloadBytes,
            int payloadLength,
            out string error);

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SaveFileHeaderPrefixSize)]
        private struct SaveFileHeaderPrefix
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = CurrentHeaderSize)]
        internal struct SaveFileHeader
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public ulong TimestampUnixMs;
            public uint DeltaCount;
            public uint EntityCount;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
            public ulong HashPayload64;
            public ulong HashHeader64;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = LegacyHeaderSize)]
        private struct LegacySaveFileHeader
        {
            public uint MagicValue;
            public ushort Version;
            public byte CompatMask;
            public byte Flags;
            public ulong TimestampUnixMs;
            public uint DeltaCount;
            public uint EntityCount;
            public uint PlayerOffset;
            public uint DeltaOffset;
            public uint EntityOffset;
            public uint HashHeader32;
            public uint HashPayload32;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
        public struct DeltaCell
        {
            /// <summary>
            /// Packed universe-space cell key.
            /// </summary>
            public ulong UniverseKey;

            /// <summary>
            /// Signed-distance delta in meters.
            /// </summary>
            public float SdfValue;

            /// <summary>
            /// Material palette index.
            /// </summary>
            public byte MaterialId;

            /// <summary>
            /// Packed per-cell state flags.
            /// </summary>
            public byte Flags;

            /// <summary>
            /// Material-specific metadata payload.
            /// </summary>
            public ushort Metadata;

            /// <summary>
            /// Reserved expansion bytes required by the current 20-byte mandate.
            /// </summary>
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = PayloadPrefixSizeBytes)]
        private struct PayloadPrefix
        {
            [FieldOffset(0)]
            public ulong TimestampUnixMs;
            [FieldOffset(8)]
            public float PlayTimeSeconds;
            [FieldOffset(12)]
            public AbsoluteUniversePosition PlayerPosition;
            [FieldOffset(60)]
            public int SaveDataVersion;
            [FieldOffset(64)]
            public uint SaveDataByteLength;
            [FieldOffset(68)]
            public ushort SceneNameByteLength;
            [FieldOffset(70)]
            public ushort GameVersionByteLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PersistentWorldSectionHeaderSize)]
        private struct PersistentWorldSectionHeader
        {
            public uint ChunkCount;
            public uint ItemHashCount;
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = EcosystemSectionHeaderSize)]
        private struct EcosystemSectionHeader
        {
            public uint RecordCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        private struct PersistentWorldSaveRecord16
        {
            public uint PackedLocalPosition;
            public uint InstanceUid;
            public ushort Quantity;
            public byte ItemFlags;
            public byte Reserved;
            public ushort ChunkIndex;
            public ushort ItemHashIndex;
        }

        internal static void WarmRuntime()
        {
            byte value = 0;
            _ = xxHash3.Hash64(&value, 1L);
        }

        internal static bool IsBinaryContainer(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return false;

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out _)
                || fileLength < sizeof(uint))
            {
                return false;
            }

            uint headerValue = 0u;
            return AsyncWriteManager.TryReadAll(absolutePath, &headerValue, sizeof(uint), out _)
                && headerValue == Magic;
        }

        internal static bool TryWriteSaveFile(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            return TryWriteSaveFileIndexedV8(
                absolutePath,
                metadata,
                data,
                persistentWorldDeltas,
                ecosystemSectorStates,
                packedQuestHeader,
                packedQuestStateWords,
                voxelDeltaSnapshot,
                rawBuffer,
                compressedBuffer,
                DefaultIndexedPersistentWorldChunkSizeMeters,
                out payloadHash64,
                out rawPayloadLength,
                out error);
        }

        internal static bool TryReadMetadata(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            metadata = null;
            detectedVersion = 0;

            if (TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping v8Mapping, out SaveFileHeader v8Header, out _, out string headerError))
            {
                try
                {
                    if ((v8Header.Flags & FlagIndexedSectorBlocks) != 0 && v8Header.Version >= IndexedBlockStorageVersion)
                    {
                        return TryReadMetadataIndexedV8(absolutePath, slotName, rawBuffer, in v8Header, ref v8Mapping, out metadata, out detectedVersion, out error);
                    }
                }
                finally
                {
                    AsyncWriteManager.CloseReadOnlyMapping(ref v8Mapping);
                }
            }
            else if (!string.IsNullOrEmpty(headerError))
            {
                error = headerError;
                return false;
            }

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefixInfo prefix, out byte* rawPtr, out int rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        private static bool TryWriteSaveFileIndexedV8(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates,
            QuestSaveHeader packedQuestHeader,
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            int chunkSizeMeters,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out string error)
        {
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Save path is empty.";
                return false;
            }

            if (!rawBuffer.IsCreated || !compressedBuffer.IsCreated)
            {
                error = "Native save buffers are not initialized.";
                return false;
            }

            if (metadata == null || data == null)
            {
                error = "Save payload is null.";
                return false;
            }

            string sceneName = string.IsNullOrEmpty(metadata.SceneName) ? "Unknown" : metadata.SceneName;
            string gameVersion = string.IsNullOrEmpty(metadata.GameVersion) ? Application.version : metadata.GameVersion;
            int sceneBytesLength = checked(sceneName.Length * sizeof(char));
            int versionBytesLength = checked(gameVersion.Length * sizeof(char));
            if (sceneBytesLength > ushort.MaxValue || versionBytesLength > ushort.MaxValue)
            {
                error = "Save metadata strings exceed the payload prefix limits.";
                return false;
            }

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);
            UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);
            UnsafeUtility.MemClear(filePtr, compressedBuffer.Length);

            int packedQuestWordCount = packedQuestStateWords.IsCreated ? packedQuestStateWords.Length : 0;
            int ecosystemSectorCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            int voxelDeltaByteLength = voxelDeltaSnapshot.IsCreated ? voxelDeltaSnapshot.Length : 0;
            int packedQuestSectionLength = packedQuestWordCount > 0
                ? PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>())
                : 0;
            int ecosystemSectionLength = ComputeEcosystemSectionLength(ecosystemSectorCount);

            int metadataCursor = PayloadPrefixSizeBytes + sceneBytesLength + versionBytesLength;
            if (!SaveBinaryPayloadCodec.TryWrite(data, AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, out int saveDataByteLength, out error))
                return false;

            ulong timestampUnixMs = ToUnixMilliseconds(metadata.Timestamp);
            PayloadPrefix prefix = new PayloadPrefix
            {
                TimestampUnixMs = timestampUnixMs,
                PlayTimeSeconds = metadata.PlayTimeSeconds,
                PlayerPosition = ToAup(metadata.PlayerPosition),
                SaveDataVersion = math.max(data.version, 0),
                SaveDataByteLength = (uint)saveDataByteLength,
                SceneNameByteLength = (ushort)sceneBytesLength,
                GameVersionByteLength = (ushort)versionBytesLength
            };

            UnsafeUtility.CopyStructureToPtr(ref prefix, rawPtr);
            metadataCursor = PayloadPrefixSizeBytes;
            CopyUtf16StringToUnmanaged(sceneName, AddByteOffset(rawPtr, metadataCursor), sceneBytesLength);
            metadataCursor += sceneBytesLength;
            CopyUtf16StringToUnmanaged(gameVersion, AddByteOffset(rawPtr, metadataCursor), versionBytesLength);
            metadataCursor += versionBytesLength;
            metadataCursor += saveDataByteLength;
            int packedQuestOffsetInMetadataPayload = metadataCursor;

            if (packedQuestWordCount > 0)
            {
                QuestSaveHeader serializedQuestHeader = packedQuestHeader;
                serializedQuestHeader.Magic = QuestSaveHeader.HeaderMagic;
                serializedQuestHeader.FlagCount = (uint)packedQuestWordCount;
                serializedQuestHeader.Checksum = ComputePackedQuestStateChecksum(packedQuestStateWords);
                UnsafeUtility.CopyStructureToPtr(ref serializedQuestHeader, AddByteOffset(rawPtr, metadataCursor));
                metadataCursor += PackedQuestStateSectionHeaderSize;

                void* packedQuestSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateWords);
                int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, packedQuestSourcePtr, packedQuestBytes))
                {
                    error = "Packed quest-state write exceeded metadata buffer bounds.";
                    return false;
                }

                metadataCursor += packedQuestBytes;
            }

            int ecosystemOffsetInMetadataPayload = metadataCursor;
            WriteEcosystemSection(AddByteOffset(rawPtr, metadataCursor), ecosystemSectorStates);
            metadataCursor += ecosystemSectionLength;

            if (voxelDeltaByteLength > 0)
            {
                void* voxelSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(rawPtr, metadataCursor), rawBuffer.Length - metadataCursor, voxelSourcePtr, voxelDeltaByteLength))
                {
                    error = "Voxel delta snapshot write exceeded metadata buffer bounds.";
                    return false;
                }

                metadataCursor += voxelDeltaByteLength;
            }

            int metadataRawLength = metadataCursor;
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);

            List<IndexedSectorGroup> sectorGroups = BuildIndexedSectorGroups(persistentWorldDeltas, chunkSizeMeters);
            int sectorCount = sectorGroups.Count;
            if (sectorCount > IndexedSectorDirectorySlotCount)
            {
                long overflowSectorHash = sectorGroups[IndexedSectorDirectorySlotCount].SectorHash;
                ReportIndexedSectorDirectoryCapacityExceeded(overflowSectorHash, sectorCount);
                sectorCount = IndexedSectorDirectorySlotCount;
            }

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
            int metadataBlockOffset = CurrentHeaderSize + directoryBytes;
            int fileCursor = metadataBlockOffset;

            bool anyTokenSubstitution = false;
            int metadataCompressedSize = 0;
            if (!TryWriteIndexedCompressedBlock(
                    rawPtr,
                    metadataRawLength,
                    filePtr,
                    compressedBuffer.Length,
                    ref fileCursor,
                    out metadataCompressedSize,
                    out uint metadataBlockFlags,
                    out error))
            {
                return false;
            }

            anyTokenSubstitution |= (metadataBlockFlags & FlagTokenSubstitution) != 0;

            SectorEntry[] sectorEntries = new SectorEntry[IndexedSectorDirectorySlotCount];
            int totalEntityCount = CountIndexedSectorRecords(sectorGroups, sectorCount);
            NativeParallelHashMap<int3, ushort> persistentWorldChunkLookup = default;
            NativeList<int3> persistentWorldChunkTable = default;
            NativeParallelHashMap<ulong, ushort> persistentWorldItemHashLookup = default;
            NativeList<ulong> persistentWorldItemHashTable = default;
            NativeArray<PersistentWorldDeltaRecord> sectorRecordBuffer = default;

            try
            {
                for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
                {
                    IndexedSectorGroup group = sectorGroups[sectorIndex];
                    int recordCount = group.Records != null ? group.Records.Count : 0;
                    if (recordCount <= 0)
                    {
                        continue;
                    }

                    sectorRecordBuffer = new NativeArray<PersistentWorldDeltaRecord>(recordCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int i = 0; i < recordCount; i++)
                        sectorRecordBuffer[i] = group.Records[i];

                    if (!TryBuildPersistentWorldSectionTables(
                            sectorRecordBuffer,
                            out persistentWorldChunkLookup,
                            out persistentWorldChunkTable,
                            out persistentWorldItemHashLookup,
                            out persistentWorldItemHashTable,
                            out error))
                    {
                        return false;
                    }

                    int sectorRawLength = ComputePersistentWorldSectionLength(recordCount, persistentWorldChunkTable.Length, persistentWorldItemHashTable.Length);
                    UnsafeUtility.MemClear(rawPtr, sectorRawLength);
                    WritePersistentWorldSection(
                        rawPtr,
                        sectorRecordBuffer,
                        persistentWorldChunkLookup,
                        persistentWorldChunkTable,
                        persistentWorldItemHashLookup,
                        persistentWorldItemHashTable);

                    long sectorByteOffset = fileCursor;
                    if (!TryWriteIndexedCompressedBlock(
                            rawPtr,
                            sectorRawLength,
                            filePtr,
                            compressedBuffer.Length,
                            ref fileCursor,
                            out int sectorCompressedSize,
                            out uint sectorBlockFlags,
                            out error))
                    {
                        return false;
                    }

                    anyTokenSubstitution |= (sectorBlockFlags & FlagTokenSubstitution) != 0;
                    if (!TryAssignIndexedSectorEntry(sectorEntries, group.SectorHash, new SectorEntry
                    {
                        SectorHash = group.SectorHash,
                        ByteOffset = sectorByteOffset,
                        CompressedSize = sectorCompressedSize,
                        DecompressedSize = sectorRawLength,
                        Checksum = 0u
                    }, out error))
                    {
                        return false;
                    }

                    if (persistentWorldItemHashTable.IsCreated)
                        persistentWorldItemHashTable.Dispose();
                    if (persistentWorldItemHashLookup.IsCreated)
                        persistentWorldItemHashLookup.Dispose();
                    if (persistentWorldChunkTable.IsCreated)
                        persistentWorldChunkTable.Dispose();
                    if (persistentWorldChunkLookup.IsCreated)
                        persistentWorldChunkLookup.Dispose();
                    if (sectorRecordBuffer.IsCreated)
                        sectorRecordBuffer.Dispose();

                    persistentWorldItemHashTable = default;
                    persistentWorldItemHashLookup = default;
                    persistentWorldChunkTable = default;
                    persistentWorldChunkLookup = default;
                    sectorRecordBuffer = default;
                }
            }
            finally
            {
                if (persistentWorldItemHashTable.IsCreated)
                    persistentWorldItemHashTable.Dispose();
                if (persistentWorldItemHashLookup.IsCreated)
                    persistentWorldItemHashLookup.Dispose();
                if (persistentWorldChunkTable.IsCreated)
                    persistentWorldChunkTable.Dispose();
                if (persistentWorldChunkLookup.IsCreated)
                    persistentWorldChunkLookup.Dispose();
                if (sectorRecordBuffer.IsCreated)
                    sectorRecordBuffer.Dispose();
            }

            IndexedSectorDirectoryHeader directoryHeader = new IndexedSectorDirectoryHeader
            {
                SectorCount = (uint)sectorCount,
                ChunkSizeMeters = math.max(1, chunkSizeMeters),
                MetadataCompressedSize = metadataCompressedSize,
                MetadataDecompressedSize = metadataRawLength
            };
            UnsafeUtility.CopyStructureToPtr(ref directoryHeader, filePtr + CurrentHeaderSize);

            int directoryCursor = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize;
            for (int i = 0; i < sectorEntries.Length; i++)
            {
                UnsafeUtility.CopyStructureToPtr(ref sectorEntries[i], filePtr + directoryCursor);
                directoryCursor += UnsafeUtility.SizeOf<SectorEntry>();
            }

            ulong directoryHash64 = Hash64(filePtr + CurrentHeaderSize, directoryBytes);
            payloadHash64 = metadataHash64 ^ directoryHash64;
            rawPayloadLength = metadataRawLength;

            SaveFileHeader header = new SaveFileHeader
            {
                MagicValue = Magic,
                Version = CurrentVersion,
                CompatMask = CurrentCompatMask,
                Flags = (byte)(FlagLz4Blocks | FlagIndexedSectorBlocks),
                TimestampUnixMs = timestampUnixMs,
                DeltaCount = (uint)packedQuestWordCount,
                EntityCount = (uint)math.max(totalEntityCount, 0),
                PlayerOffset = (uint)metadataBlockOffset,
                DeltaOffset = (uint)(metadataBlockOffset + packedQuestOffsetInMetadataPayload),
                EntityOffset = (uint)metadataBlockOffset,
                HashPayload64 = payloadHash64,
                HashHeader64 = 0UL
            };

            if (anyTokenSubstitution)
                header.Flags |= FlagTokenSubstitution;

            header.HashHeader64 = ComputeHeaderHash(ref header);
            UnsafeUtility.CopyStructureToPtr(ref header, filePtr);

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!AsyncWriteManager.WriteAll(absolutePath, filePtr, fileCursor, out error))
                return false;

            metadata.Checksum = payloadHash64.ToString("X16");
            return true;
        }

        private static bool TryReadValidatedHeader(
            string absolutePath,
            out AsyncWriteManager.ReadOnlyMapping mapping,
            out SaveFileHeader header,
            out int headerSizeBytes,
            out string error)
        {
            mapping = default;
            header = default;
            headerSizeBytes = 0;
            error = string.Empty;

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out mapping, out error))
                return false;

            bool success = false;
            try
            {
                if (mapping.Length < SaveFileHeaderPrefixSize)
                {
                    error = "Save file is smaller than the header prefix.";
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                SaveFileHeaderPrefix headerPrefix = UnsafeUtility.ReadArrayElement<SaveFileHeaderPrefix>(filePtr, 0);
                if (headerPrefix.MagicValue != Magic)
                {
                    error = "Save file magic mismatch.";
                    return false;
                }

                headerSizeBytes = ResolveHeaderSize(headerPrefix.Version);
                if (headerSizeBytes <= 0)
                {
                    error = $"Unsupported save header version {headerPrefix.Version}.";
                    return false;
                }

                if (mapping.Length < headerSizeBytes)
                {
                    error = "Save file is smaller than its declared header size.";
                    return false;
                }

                if (headerPrefix.Version >= First64BitHashVersion)
                {
                    header = UnsafeUtility.ReadArrayElement<SaveFileHeader>(filePtr, 0);
                }
                else
                {
                    LegacySaveFileHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacySaveFileHeader>(filePtr, 0);
                    header = ConvertLegacyHeader(in legacyHeader);
                }

                if (!TryValidateHeader(header, out error))
                    return false;

                ulong expectedHeaderHash = ComputeHeaderHash(ref header);
                if (expectedHeaderHash != header.HashHeader64)
                {
                    error = $"Save header checksum mismatch. Expected 0x{expectedHeaderHash:X16}, found 0x{header.HashHeader64:X16}.";
                    return false;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        private static bool TryReadIndexedDirectory(
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out IndexedSectorDirectoryHeader directoryHeader,
            out SectorEntry[] sectorEntries,
            out string error)
        {
            directoryHeader = default;
            sectorEntries = null;
            error = string.Empty;

            if (!TryValidateIndexedBlockStorageHeader(in header, out error))
            {
                return false;
            }

            int directoryOffset = CurrentHeaderSize;
            if (mapping.Length < directoryOffset ||
                (long)directoryOffset > mapping.Length - IndexedSectorDirectoryHeaderSize)
            {
                error = "Indexed sector directory header is truncated.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            directoryHeader = UnsafeUtility.ReadArrayElement<IndexedSectorDirectoryHeader>(filePtr + directoryOffset, 0);
            int sectorCount = checked((int)directoryHeader.SectorCount);
            int safeChunkSizeMeters = math.max(1, directoryHeader.ChunkSizeMeters);
            directoryHeader.ChunkSizeMeters = safeChunkSizeMeters;

            if (sectorCount < 0 || sectorCount > IndexedSectorDirectorySlotCount)
            {
                error = $"Indexed sector directory count {sectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                return false;
            }

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
            if ((long)directoryOffset > mapping.Length - directoryBytes)
            {
                error = "Indexed sector directory exceeds the file bounds.";
                return false;
            }

            long directoryEndOffset = directoryOffset + (long)directoryBytes;
            long metadataOffset = header.PlayerOffset;
            if (metadataOffset < directoryEndOffset || metadataOffset >= mapping.Length)
            {
                error = "Indexed metadata block offset is out of bounds.";
                return false;
            }

            if (directoryHeader.MetadataCompressedSize < 0 ||
                metadataOffset > mapping.Length - directoryHeader.MetadataCompressedSize)
            {
                error = "Indexed metadata block size is out of bounds.";
                return false;
            }

            long metadataEndOffset = metadataOffset + directoryHeader.MetadataCompressedSize;
            sectorEntries = new SectorEntry[IndexedSectorDirectorySlotCount];
            int entryCursor = directoryOffset + IndexedSectorDirectoryHeaderSize;
            int populatedCount = 0;
            for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
            {
                SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                if (IsIndexedSectorEntryPopulated(in entry) &&
                    !IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                {
                    error = $"Indexed sector entry {i} exceeded the file bounds.";
                    return false;
                }

                sectorEntries[i] = entry;
                if (IsIndexedSectorEntryPopulated(in entry))
                    populatedCount++;
                entryCursor += UnsafeUtility.SizeOf<SectorEntry>();
            }

            if (populatedCount != sectorCount)
            {
                error = $"Indexed sector directory count mismatch. Header={sectorCount}, Populated={populatedCount}.";
                return false;
            }

            return true;
        }

        private static bool TryValidateIndexedBlockStorageHeader(in SaveFileHeader header, out string error)
        {
            error = string.Empty;
            if ((header.Flags & FlagIndexedSectorBlocks) == 0)
            {
                error = "Save header is not an indexed sector container.";
                return false;
            }

            if (header.Version != IndexedBlockStorageVersion)
            {
                error = $"Indexed sector block decompression requires save header version 0x{IndexedBlockStorageVersion:X4}. Found 0x{header.Version:X4}.";
                return false;
            }

            return true;
        }

        private static bool IsIndexedSectorEntryPopulated(in SectorEntry entry)
        {
            return entry.CompressedSize > 0 && entry.ByteOffset >= CurrentHeaderSize;
        }

        private static bool IsIndexedSectorEntryWithinFileBounds(
            in SectorEntry entry,
            long minimumByteOffset,
            long fileLength)
        {
            if (!IsIndexedSectorEntryPopulated(in entry) ||
                minimumByteOffset < CurrentHeaderSize ||
                fileLength < minimumByteOffset)
            {
                return false;
            }

            return SaveIndexedSectorBoundsMath.IsIndexedSectorBlockWithinFileBounds(
                entry.ByteOffset,
                entry.CompressedSize,
                minimumByteOffset,
                fileLength);
        }

        private static int ResolveIndexedSectorDirectorySlot(long sectorHash)
        {
            ulong hash = unchecked((ulong)sectorHash);
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            return (int)(hash & (IndexedSectorDirectorySlotCount - 1));
        }

        private static bool TryAssignIndexedSectorEntry(SectorEntry[] sectorEntries, long sectorHash, in SectorEntry entry, out string error)
        {
            error = string.Empty;
            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector directory slot buffer is invalid.";
                return false;
            }

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                if (!IsIndexedSectorEntryPopulated(in sectorEntries[slot]) || sectorEntries[slot].SectorHash == sectorHash)
                {
                    sectorEntries[slot] = entry;
                    return true;
                }
            }

            error = $"Indexed sector directory is full while assigning sector 0x{sectorHash:X16}.";
            ReportIndexedSectorDirectoryCapacityExceeded(sectorHash, IndexedSectorDirectorySlotCount + 1);
            return false;
        }

        private static bool TryFindIndexedSectorEntryIndex(SectorEntry[] sectorEntries, long sectorHash, out int slotIndex)
        {
            slotIndex = -1;
            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
                return false;

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                SectorEntry entry = sectorEntries[slot];
                if (!IsIndexedSectorEntryPopulated(in entry))
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    slotIndex = slot;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveIndexedSectorCommitTarget(
            SectorEntry[] sectorEntries,
            long sectorHash,
            int overrideCompressedSize,
            long originalLength,
            out IndexedSectorCommitTarget commitTarget,
            out int sectorCountDelta,
            out string error)
        {
            commitTarget = default;
            sectorCountDelta = 0;
            error = string.Empty;

            if (sectorEntries == null || sectorEntries.Length != IndexedSectorDirectorySlotCount)
            {
                error = "Indexed sector commit directory buffer is invalid.";
                return false;
            }

            if (overrideCompressedSize <= 0 || originalLength < CurrentHeaderSize)
            {
                error = "Indexed sector commit sizing is invalid.";
                return false;
            }

            if (!TryResolveAppendFileLength(originalLength, overrideCompressedSize, out long appendFileLength, out error))
                return false;

            if (TryFindIndexedSectorEntryIndex(sectorEntries, sectorHash, out int existingSlotIndex))
            {
                commitTarget = new IndexedSectorCommitTarget(
                    reusedExistingSlot: false,
                    insertedNewSlot: false,
                    slotIndex: existingSlotIndex,
                    writeOffset: originalLength,
                    newFileLength: appendFileLength);
                return true;
            }

            int startSlot = ResolveIndexedSectorDirectorySlot(sectorHash);
            for (int probe = 0; probe < IndexedSectorDirectorySlotCount; probe++)
            {
                int slot = (startSlot + probe) & (IndexedSectorDirectorySlotCount - 1);
                if (IsIndexedSectorEntryPopulated(in sectorEntries[slot]))
                    continue;

                commitTarget = new IndexedSectorCommitTarget(
                    reusedExistingSlot: false,
                    insertedNewSlot: true,
                    slotIndex: slot,
                    writeOffset: originalLength,
                    newFileLength: appendFileLength);
                sectorCountDelta = 1;
                return true;
            }

            error = $"Indexed sector directory is full while resolving commit target for sector 0x{sectorHash:X16}.";
            ReportIndexedSectorDirectoryCapacityExceeded(sectorHash, IndexedSectorDirectorySlotCount + 1);
            return false;
        }

        private static bool TryResolveAppendFileLength(
            long originalLength,
            int appendBytes,
            out long newFileLength,
            out string error)
        {
            newFileLength = 0L;
            error = string.Empty;
            if (originalLength < 0L || appendBytes <= 0 || originalLength > long.MaxValue - appendBytes)
            {
                error = "Indexed sector append length overflowed the file range.";
                return false;
            }

            newFileLength = originalLength + appendBytes;
            return true;
        }

        private static bool IsMmfMoveRangeWithinFile(long offset, long length, long totalFileSize)
        {
            return offset >= 0L &&
                   length >= 0L &&
                   totalFileSize >= 0L &&
                   offset <= totalFileSize &&
                   length <= totalFileSize - offset;
        }

        private static int CountIndexedSectorRecords(List<IndexedSectorGroup> sectorGroups, int sectorCount)
        {
            if (sectorGroups == null || sectorCount <= 0)
                return 0;

            int safeCount = math.min(sectorCount, sectorGroups.Count);
            int total = 0;
            for (int i = 0; i < safeCount; i++)
            {
                IndexedSectorGroup group = sectorGroups[i];
                if (group.Records != null)
                    total = checked(total + group.Records.Count);
            }

            return total;
        }

        private static void ReportIndexedSectorDirectoryCapacityExceeded(long sectorHash, int attemptedSectorCount)
        {
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                $"[SaveBinaryStorage] Indexed sector directory capacity exceeded. " +
                $"capacity={IndexedSectorDirectorySlotCount}, attempted={attemptedSectorCount}, sector=0x{sectorHash:X16}. " +
                "Chunk save dropped to protect the fixed-size v8 directory.");
#endif
        }

        private static bool TryReadIndexedCompressedBlock(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            long blockOffset,
            int storedBlockLength,
            int expectedDecompressedLength,
            NativeArray<byte> destinationBuffer,
            out int decompressedLength,
            out string error)
        {
            decompressedLength = 0;
            error = string.Empty;

            if (!destinationBuffer.IsCreated || expectedDecompressedLength <= 0 || expectedDecompressedLength > destinationBuffer.Length)
            {
                error = "Indexed block destination buffer is invalid.";
                return false;
            }

            if (storedBlockLength <= IndexedSectorBlockHeaderSize)
            {
                error = "Indexed block is smaller than the block header.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            IndexedSectorBlockHeader blockHeader = UnsafeUtility.ReadArrayElement<IndexedSectorBlockHeader>(filePtr + blockOffset, 0);
            int compressedPayloadLength = storedBlockLength - IndexedSectorBlockHeaderSize;
            using NativeArray<byte> compressedPayload = new NativeArray<byte>(compressedPayloadLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using NativeArray<int> resultLength = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            byte* compressedPayloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedPayload);
            if (!UnsafeMemoryCopyGuard.SafeCopy(compressedPayloadPtr, compressedPayload.Length, filePtr + blockOffset + IndexedSectorBlockHeaderSize, compressedPayloadLength))
            {
                error = "Indexed block compressed payload copy exceeded destination bounds.";
                return false;
            }

            IndexedBlockDecompressJob decompressJob = new IndexedBlockDecompressJob
            {
                CompressedPayload = compressedPayload,
                DecompressedPayload = destinationBuffer,
                ResultLength = resultLength,
                BlockFlags = blockHeader.Flags
            };

            JobHandle decompressHandle = decompressJob.Schedule();
            DispatcherJobSwap.TryComplete(ref decompressHandle, forceComplete: true);
            decompressedLength = resultLength[0];

            if (decompressedLength <= 0)
            {
                error = "Indexed LZ4 block decompression failed.";
                return false;
            }

            if (decompressedLength != expectedDecompressedLength)
            {
                error = $"Indexed block length mismatch. Expected {expectedDecompressedLength} bytes, got {decompressedLength}.";
                return false;
            }

            return true;
        }

        private static bool TryReadPersistentWorldSectionFromBuffer(
            byte* sectionPtr,
            int sectionLength,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            error = string.Empty;

            if (sectionPtr == null || sectionLength < PersistentWorldSectionHeaderSize)
            {
                error = "Indexed persistent-world sector section is truncated.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(sectionPtr, 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int expectedLength = ComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount);
            if (expectedLength != sectionLength)
            {
                error = "Indexed persistent-world sector length does not match its compact header.";
                return false;
            }

            persistentWorldDeltas = recordCount > 0 ? new PersistentWorldDeltaRecord[recordCount] : Array.Empty<PersistentWorldDeltaRecord>();
            if (recordCount <= 0)
                return true;

            int cursor = PersistentWorldSectionHeaderSize;
            byte* chunkTablePtr = sectionPtr + cursor;
            cursor += chunkCount * UnsafeUtility.SizeOf<int3>();
            byte* itemHashTablePtr = sectionPtr + cursor;
            cursor += itemHashCount * UnsafeUtility.SizeOf<ulong>();
            byte* saveRecordPtr = sectionPtr + cursor;

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldSaveRecord16 saveRecord = UnsafeUtility.ReadArrayElement<PersistentWorldSaveRecord16>(saveRecordPtr, i);
                bool isDeleted = (((PersistentWorldItemFlags)saveRecord.ItemFlags) & PersistentWorldItemFlags.Deleted) != 0;
                if (saveRecord.ChunkIndex >= chunkCount)
                {
                    error = "Indexed persistent-world record referenced an out-of-range lookup table entry.";
                    return false;
                }

                int3 chunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex);
                ulong itemHash = 0UL;
                if (!isDeleted)
                {
                    if (saveRecord.ItemHashIndex >= itemHashCount)
                    {
                        error = "Indexed persistent-world record referenced an out-of-range item-hash entry.";
                        return false;
                    }

                    itemHash = UnsafeUtility.ReadArrayElement<ulong>(itemHashTablePtr, saveRecord.ItemHashIndex);
                }

                persistentWorldDeltas[i] = new PersistentWorldDeltaRecord
                {
                    ChunkId = chunkId,
                    ItemPersistentIdHash = itemHash,
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = isDeleted ? (ushort)1 : (saveRecord.Quantity < 1 ? (ushort)1 : saveRecord.Quantity),
                    ItemFlags = saveRecord.ItemFlags,
                    Reserved = saveRecord.Reserved
                };
            }

            return true;
        }

        private static bool TryReadMetadataIndexedV8(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            metadata = null;
            detectedVersion = 0;
            error = string.Empty;

            if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out _, out error))
                return false;

            if (!TryReadIndexedCompressedBlock(ref mapping, header.PlayerOffset, directoryHeader.MetadataCompressedSize, directoryHeader.MetadataDecompressedSize, rawBuffer, out int metadataRawLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            ulong directoryHash64 = header.PlayerOffset > CurrentHeaderSize
                ? Hash64((byte*)mapping.View + CurrentHeaderSize, (int)(header.PlayerOffset - CurrentHeaderSize))
                : 0UL;
            if ((metadataHash64 ^ directoryHash64) != header.HashPayload64)
            {
                error = "Indexed metadata aggregate checksum mismatch.";
                return false;
            }

            if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, metadataRawLength, header.Version, out PayloadPrefixInfo prefix, out error))
                return false;

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };
            return true;
        }

        private static bool TryLoadSaveDataIndexedV8(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out int detectedVersion,
            out bool indexedBackupRecoveryUsed,
            out string error)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldDeltas = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            detectedVersion = 0;
            indexedBackupRecoveryUsed = false;
            error = string.Empty;

            if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                return false;

            if (!TryReadIndexedCompressedBlock(ref mapping, header.PlayerOffset, directoryHeader.MetadataCompressedSize, directoryHeader.MetadataDecompressedSize, rawBuffer, out int metadataRawLength, out error))
                return false;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            ulong metadataHash64 = Hash64(rawPtr, metadataRawLength);
            ulong directoryHash64 = header.PlayerOffset > CurrentHeaderSize
                ? Hash64((byte*)mapping.View + CurrentHeaderSize, (int)(header.PlayerOffset - CurrentHeaderSize))
                : 0UL;
            payloadHash64 = metadataHash64 ^ directoryHash64;
            if (payloadHash64 != header.HashPayload64)
            {
                error = "Indexed aggregate payload checksum mismatch.";
                return false;
            }

            if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, metadataRawLength, header.Version, out PayloadPrefixInfo prefix, out error))
                return false;

            detectedVersion = prefix.SaveDataVersion;

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;
            if (!TryReadUtf16String(rawPtr, metadataRawLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
            if (!IsByteRangeWithin(cursor, saveDataLength, metadataRawLength))
            {
                error = "Indexed metadata save-data range is invalid.";
                return false;
            }

            if (!SaveBinaryPayloadCodec.TryRead(AddByteOffset(rawPtr, cursor), saveDataLength, out data, out int bytesRead, out error))
                return false;
            if (bytesRead != saveDataLength)
            {
                error = "Indexed metadata save-data length mismatch.";
                return false;
            }

            int payloadCursor = cursor + saveDataLength;
            if (header.DeltaCount > 0)
            {
                if (!IsByteRangeWithin(payloadCursor, PackedQuestStateSectionHeaderSize, metadataRawLength))
                {
                    error = "Indexed packed quest section header is truncated.";
                    return false;
                }

                packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
                if (packedQuestHeader.Magic != QuestSaveHeader.HeaderMagic)
                {
                    error = "Indexed packed quest section magic mismatch.";
                    return false;
                }

                if (packedQuestHeader.FlagCount != header.DeltaCount)
                {
                    error = "Indexed packed quest section count mismatch.";
                    return false;
                }

                packedQuestStateWords = new uint[packedQuestHeader.FlagCount];
                payloadCursor += PackedQuestStateSectionHeaderSize;
                if (packedQuestHeader.FlagCount > 0)
                {
                    fixed (uint* destinationPtr = packedQuestStateWords)
                    {
                        int packedQuestBytes = checked((int)packedQuestHeader.FlagCount) * UnsafeUtility.SizeOf<uint>();
                        if (!IsByteRangeWithin(payloadCursor, packedQuestBytes, metadataRawLength))
                        {
                            error = "Indexed packed quest section exceeds the metadata payload bounds.";
                            return false;
                        }

                        if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>(), AddByteOffset(rawPtr, payloadCursor), packedQuestBytes))
                        {
                            error = "Indexed packed quest-state copy exceeded destination bounds.";
                            return false;
                        }
                    }

                    if (ComputePackedQuestStateChecksum(packedQuestStateWords) != packedQuestHeader.Checksum)
                    {
                        error = "Indexed packed quest section checksum mismatch.";
                        return false;
                    }
                }

                payloadCursor += checked((int)packedQuestHeader.FlagCount) * UnsafeUtility.SizeOf<uint>();
            }
            else
            {
                packedQuestHeader = default;
                packedQuestStateWords = Array.Empty<uint>();
            }

            if (!IsByteRangeWithin(payloadCursor, EcosystemSectionHeaderSize, metadataRawLength))
            {
                error = "Indexed ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader ecosystemHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, payloadCursor), 0);
            int ecosystemRecordCount = checked((int)ecosystemHeader.RecordCount);
            int ecosystemSectionLength = ComputeEcosystemSectionLength(ecosystemRecordCount);
            if (!IsByteRangeWithin(payloadCursor, ecosystemSectionLength, metadataRawLength))
            {
                error = "Indexed ecosystem section exceeds the metadata payload bounds.";
                return false;
            }

            ecosystemSectorStates = ecosystemRecordCount > 0 ? new EcosystemSectorSaveRecord[ecosystemRecordCount] : Array.Empty<EcosystemSectorSaveRecord>();
            if (ecosystemRecordCount > 0)
            {
                fixed (EcosystemSectorSaveRecord* destinationPtr = ecosystemSectorStates)
                {
                    int ecosystemBytes = ecosystemRecordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(), AddByteOffset(rawPtr, payloadCursor + EcosystemSectionHeaderSize), ecosystemBytes))
                    {
                        error = "Indexed ecosystem section copy exceeded destination bounds.";
                        return false;
                    }
                }
            }

            payloadCursor += ecosystemSectionLength;
            int voxelByteLength = math.max(0, metadataRawLength - payloadCursor);
            if (voxelByteLength > 0)
            {
                voxelDeltaSnapshot = new NativeArray<byte>(voxelByteLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                void* voxelDestinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
                if (!UnsafeMemoryCopyGuard.SafeCopy(voxelDestinationPtr, voxelDeltaSnapshot.Length, AddByteOffset(rawPtr, payloadCursor), voxelByteLength))
                {
                    error = "Indexed voxel delta snapshot copy exceeded destination bounds.";
                    return false;
                }
            }

            List<PersistentWorldDeltaRecord> aggregatedWorldDeltas = new List<PersistentWorldDeltaRecord>(256);
            if (sectorEntries != null)
            {
                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (entry.CompressedSize <= 0 || entry.DecompressedSize <= 0)
                        continue;

                    if (!IsPersistentWorldPagedSectorWithinLoadWindow(entry.SectorHash, in prefix.PlayerPosition))
                        continue;

                    using NativeArray<byte> sectorRaw = new NativeArray<byte>(entry.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, sectorRaw, out _, out error))
                    {
                        string sectorError = error;
                        if (TryAppendIndexedPersistentWorldSectorFromBackup(absolutePath, entry.SectorHash, aggregatedWorldDeltas, out string backupError))
                        {
                            indexedBackupRecoveryUsed = true;
                            error = string.Empty;
                            continue;
                        }

                        ReportIndexedSectorQuarantine(entry.SectorHash, sectorError, backupError);
                        error = string.Empty;
                        continue;
                    }

                    byte* sectorRawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sectorRaw);
                    if (!TryReadPersistentWorldSectionFromBuffer(sectorRawPtr, entry.DecompressedSize, out PersistentWorldDeltaRecord[] sectorRecords, out error))
                    {
                        string sectorError = error;
                        if (TryAppendIndexedPersistentWorldSectorFromBackup(absolutePath, entry.SectorHash, aggregatedWorldDeltas, out string backupError))
                        {
                            indexedBackupRecoveryUsed = true;
                            error = string.Empty;
                            continue;
                        }

                        ReportIndexedSectorQuarantine(entry.SectorHash, sectorError, backupError);
                        error = string.Empty;
                        continue;
                    }

                    if (sectorRecords != null && sectorRecords.Length > 0)
                        aggregatedWorldDeltas.AddRange(sectorRecords);
                }
            }

            persistentWorldDeltas = aggregatedWorldDeltas.Count > 0 ? aggregatedWorldDeltas.ToArray() : Array.Empty<PersistentWorldDeltaRecord>();
            rawPayloadLength = metadataRawLength;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };
            return true;
        }

        internal static bool TryReadIndexedPersistentWorldDirectory(
            string absolutePath,
            List<IndexedSectorEntryInfo> results,
            out int chunkSizeMeters,
            out string error)
        {
            chunkSizeMeters = DefaultIndexedPersistentWorldChunkSizeMeters;
            error = string.Empty;
            if (results == null)
            {
                error = "Indexed sector result list is null.";
                return false;
            }

            results.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if ((header.Flags & FlagIndexedSectorBlocks) == 0 || header.Version < IndexedBlockStorageVersion)
                {
                    error = "Save file is not an indexed sector container.";
                    return false;
                }

                if (!TryReadIndexedDirectory(in header, ref mapping, out IndexedSectorDirectoryHeader directoryHeader, out SectorEntry[] sectorEntries, out error))
                    return false;

                chunkSizeMeters = directoryHeader.ChunkSizeMeters;
                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    results.Add(new IndexedSectorEntryInfo(entry.SectorHash, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, entry.Checksum));
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool TryCorruptFirstIndexedSectorBlockForSmoke(
            string absolutePath,
            out long sectorHash,
            out string error)
        {
            sectorHash = 0L;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Smoke corruption path is missing.";
                return false;
            }

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping readMapping, out SaveFileHeader header, out _, out error))
                return false;

            SectorEntry selectedEntry = default;
            bool foundSector = false;
            try
            {
                if (!TryReadIndexedDirectory(in header, ref readMapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                for (int i = 0; i < sectorEntries.Length; i++)
                {
                    SectorEntry entry = sectorEntries[i];
                    if (!IsIndexedSectorEntryPopulated(in entry) ||
                        entry.CompressedSize <= IndexedSectorBlockHeaderSize + StandardCompressedBlockHeaderBytes ||
                        !IsIndexedSectorEntryWithinFileBounds(in entry, header.PlayerOffset, readMapping.Length))
                    {
                        continue;
                    }

                    selectedEntry = entry;
                    foundSector = true;
                    break;
                }
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref readMapping);
            }

            if (!foundSector)
            {
                error = "Smoke corruption could not find an indexed sector block.";
                return false;
            }

            FileStream fileStream = null;
            MemoryMappedFile fileMapping = null;
            MemoryMappedViewAccessor accessor = null;
            byte* filePtr = null;
            try
            {
                fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fileMapping = MemoryMappedFile.CreateFromFile(fileStream, null, fileStream.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                accessor = fileMapping.CreateViewAccessor(0L, fileStream.Length, MemoryMappedFileAccess.ReadWrite);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref filePtr);
                byte* mappedFilePtr = filePtr + accessor.PointerOffset;

                long payloadOffset = selectedEntry.ByteOffset + IndexedSectorBlockHeaderSize + StandardCompressedBlockHeaderBytes;
                byte storedByte = UnsafeUtility.ReadArrayElement<byte>(mappedFilePtr + payloadOffset, 0);
                UnsafeUtility.WriteArrayElement(mappedFilePtr + payloadOffset, 0, (byte)(storedByte ^ 0x5Au));
                accessor.Flush();
                fileStream.Flush(true);
                sectorHash = selectedEntry.SectorHash;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Smoke corruption failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (accessor != null && filePtr != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                accessor?.Dispose();
                fileMapping?.Dispose();
                fileStream?.Dispose();
            }
        }
#endif

        internal static bool TryLoadIndexedPersistentWorldSectors(
            string absolutePath,
            NativeArray<long> desiredSectorHashes,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (!desiredSectorHashes.IsCreated || !destination.IsCreated)
            {
                error = "Indexed sector paging inputs are invalid.";
                return false;
            }

            destination.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in header, ref mapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                for (int requestedIndex = 0; requestedIndex < desiredSectorHashes.Length; requestedIndex++)
                {
                    long desiredSectorHash = desiredSectorHashes[requestedIndex];
                    if (desiredSectorHash == long.MinValue)
                        continue;

                    if (!TryFindIndexedSectorEntryIndex(sectorEntries, desiredSectorHash, out int sectorEntryIndex))
                        continue;

                    SectorEntry entry = sectorEntries[sectorEntryIndex];
                    if (!TryLoadIndexedPersistentWorldSectorRecordsCore(ref mapping, in entry, destination, out string sectorError))
                    {
                        string backupPath = ResolveIndexedSaveBackupPath(absolutePath);
                        string backupError = "backup not attempted";
                        bool backupRecovered = false;
                        if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                            backupRecovered = TryLoadIndexedPersistentWorldSectorFromBackup(backupPath, desiredSectorHash, destination, out backupError);

                        if (!backupRecovered)
                        {
                            ReportIndexedSectorQuarantine(desiredSectorHash, sectorError, backupError);
                            error = $"QUARANTINED indexed sector 0x{desiredSectorHash:X16}; pristine reset sector will be generated by runtime streaming. Primary: {sectorError} Backup: {backupError}";
                            continue;
                        }
                    }
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        private static bool TryLoadIndexedPersistentWorldSectorRecordsCore(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            in SectorEntry entry,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;

            using NativeArray<byte> sectorRaw = new NativeArray<byte>(entry.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, sectorRaw, out int decompressedLength, out error))
                return false;

            if (decompressedLength != entry.DecompressedSize)
            {
                error = $"Indexed sector length mismatch for sector 0x{entry.SectorHash:X16}.";
                return false;
            }

            byte* sectorRawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sectorRaw);
            if (!TryReadPersistentWorldSectionFromBuffer(sectorRawPtr, entry.DecompressedSize, out PersistentWorldDeltaRecord[] sectorRecords, out error))
                return false;

            for (int recordIndex = 0; recordIndex < sectorRecords.Length; recordIndex++)
                destination.Add(sectorRecords[recordIndex]);

            return true;
        }

        private static bool TryLoadIndexedPersistentWorldSectorFromBackup(
            string backupPath,
            long desiredSectorHash,
            NativeList<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (!TryReadValidatedHeader(backupPath, out AsyncWriteManager.ReadOnlyMapping backupMapping, out SaveFileHeader backupHeader, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in backupHeader, ref backupMapping, out _, out SectorEntry[] backupEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(backupEntries, desiredSectorHash, out int backupSectorEntryIndex))
                {
                    error = $"Indexed sector 0x{desiredSectorHash:X16} is missing from backup save.";
                    return false;
                }

                SectorEntry backupEntry = backupEntries[backupSectorEntryIndex];
                return TryLoadIndexedPersistentWorldSectorRecordsCore(ref backupMapping, in backupEntry, destination, out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref backupMapping);
            }
        }

        private static bool TryAppendIndexedPersistentWorldSectorFromBackup(
            string primaryPath,
            long desiredSectorHash,
            List<PersistentWorldDeltaRecord> destination,
            out string error)
        {
            error = string.Empty;
            if (destination == null)
            {
                error = "Indexed sector backup append destination is null.";
                return false;
            }

            string backupPath = ResolveIndexedSaveBackupPath(primaryPath);
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            {
                error = $"Indexed sector 0x{desiredSectorHash:X16} backup is missing.";
                return false;
            }

            NativeList<PersistentWorldDeltaRecord> backupRecords = new NativeList<PersistentWorldDeltaRecord>(16, Allocator.Temp);
            try
            {
                if (!TryLoadIndexedPersistentWorldSectorFromBackup(backupPath, desiredSectorHash, backupRecords, out error))
                    return false;

                for (int i = 0; i < backupRecords.Length; i++)
                    destination.Add(backupRecords[i]);

                return true;
            }
            finally
            {
                if (backupRecords.IsCreated)
                    backupRecords.Dispose();
            }
        }

        internal static bool TryWriteIndexedPersistentWorldSectorOverride(
            string absolutePath,
            long sectorHash,
            NativeArray<PersistentWorldDeltaRecord> sectorRecords,
            int chunkSizeMeters,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Sector override path is empty.";
                return false;
            }

            if (!sectorRecords.IsCreated)
            {
                error = "Sector override record array is not initialized.";
                return false;
            }

            if (!TryBuildPersistentWorldSectionTables(
                    sectorRecords,
                    out NativeParallelHashMap<int3, ushort> chunkLookup,
                    out NativeList<int3> chunkTable,
                    out NativeParallelHashMap<ulong, ushort> itemHashLookup,
                    out NativeList<ulong> itemHashTable,
                    out error))
            {
                return false;
            }

            try
            {
                int rawSectionLength = ComputePersistentWorldSectionLength(sectorRecords.Length, chunkTable.Length, itemHashTable.Length);
                int blockCount = math.max(1, (rawSectionLength + BlockSizeBytes - 1) / BlockSizeBytes);
                int compressedCapacity = rawSectionLength + (rawSectionLength / 255) + 16 + (blockCount * StandardCompressedBlockHeaderBytes) + IndexedSectorBlockHeaderSize + 32;
                int fileCapacity = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacity;

                using NativeArray<byte> rawSectionBytes = new NativeArray<byte>(rawSectionLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                using NativeArray<byte> fileBytes = new NativeArray<byte>(fileCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* rawSectionPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawSectionBytes);
                byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
                WritePersistentWorldSection(rawSectionPtr, sectorRecords, chunkLookup, chunkTable, itemHashLookup, itemHashTable);

                int fileCursor = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (!TryWriteIndexedCompressedBlock(
                        rawSectionPtr,
                        rawSectionLength,
                        filePtr,
                        fileBytes.Length,
                        ref fileCursor,
                        out int storedBlockLength,
                        out uint blockFlags,
                        out error))
                {
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = new SectorOverrideFileHeader
                {
                    SectorHash = sectorHash,
                    CompressedSize = storedBlockLength,
                    DecompressedSize = rawSectionLength,
                    Checksum = 0u,
                    Flags = blockFlags
                };

                UnsafeUtility.CopyStructureToPtr(ref overrideHeader, filePtr);
                return AsyncWriteManager.WriteAll(absolutePath, filePtr, fileCursor, out error);
            }
            finally
            {
                if (chunkLookup.IsCreated)
                    chunkLookup.Dispose();
                if (chunkTable.IsCreated)
                    chunkTable.Dispose();
                if (itemHashLookup.IsCreated)
                    itemHashLookup.Dispose();
                if (itemHashTable.IsCreated)
                    itemHashTable.Dispose();
            }
        }

        internal static bool TryReadIndexedPersistentWorldSectorOverride(
            string absolutePath,
            out long sectorHash,
            out PersistentWorldDeltaRecord[] sectorRecords,
            out string error)
        {
            sectorHash = 0L;
            sectorRecords = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Sector override file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out error))
                return false;

            try
            {
                int overrideHeaderSize = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (mapping.Length < overrideHeaderSize + IndexedSectorBlockHeaderSize)
                {
                    error = "Sector override file is truncated.";
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = UnsafeUtility.ReadArrayElement<SectorOverrideFileHeader>((byte*)mapping.View, 0);
                sectorHash = overrideHeader.SectorHash;
                if (overrideHeader.CompressedSize <= IndexedSectorBlockHeaderSize ||
                    overrideHeader.DecompressedSize <= 0 ||
                    mapping.Length < overrideHeaderSize ||
                    overrideHeader.CompressedSize > mapping.Length - overrideHeaderSize)
                {
                    error = "Sector override header is invalid.";
                    return false;
                }

                using NativeArray<byte> rawBytes = new NativeArray<byte>(overrideHeader.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                if (!TryReadIndexedCompressedBlock(
                        ref mapping,
                        overrideHeaderSize,
                        overrideHeader.CompressedSize,
                        overrideHeader.DecompressedSize,
                        rawBytes,
                        out int decompressedLength,
                        out error))
                {
                    return false;
                }

                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBytes);
                if (decompressedLength != overrideHeader.DecompressedSize)
                {
                    error = "Sector override decompressed length mismatch.";
                    return false;
                }

                return TryReadPersistentWorldSectionFromBuffer(rawPtr, decompressedLength, out sectorRecords, out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryWriteIndexedSectorEntityStateOverride(
            string absolutePath,
            long sectorHash,
            NativeArray<EntityDataRecord> entityStates,
            int chunkSizeMeters,
            out string error)
        {
            if (!TryScheduleIndexedSectorEntityStateOverrideWrite(
                    absolutePath,
                    sectorHash,
                    entityStates,
                    chunkSizeMeters,
                    out IndexedSectorEntityStateWriteHandle writeHandle,
                    out error))
            {
                return false;
            }

            SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);
            JobHandle.ScheduleBatchedJobs();
            error = "Synchronous sector entity-state override writes are disabled. Use the scheduled dehydration pipeline.";
            return false;
        }

        internal static bool TryScheduleIndexedSectorEntityStateOverrideWrite(
            string absolutePath,
            long sectorHash,
            NativeArray<EntityDataRecord> entityStates,
            int chunkSizeMeters,
            out IndexedSectorEntityStateWriteHandle writeHandle,
            out string error)
        {
            writeHandle = default;
            error = string.Empty;
            if (string.IsNullOrEmpty(absolutePath))
            {
                error = "Sector entity-state override path is empty.";
                return false;
            }

            if (!entityStates.IsCreated)
            {
                error = "Sector entity-state override source array is not initialized.";
                return false;
            }

            int recordCount = entityStates.Length;
            if (recordCount <= 0)
            {
                error = "Sector entity-state override contains no records.";
                return false;
            }

            int rawByteLength = checked(recordCount * UnsafeUtility.SizeOf<EntityDataRecord>());
            int blockCount = math.max(1, (rawByteLength + BlockSizeBytes - 1) / BlockSizeBytes);
            int compressedCapacity = rawByteLength + (rawByteLength / 255) + 16 + (blockCount * StandardCompressedBlockHeaderBytes);
            int fileCapacity = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>() + compressedCapacity;

            try
            {
                writeHandle.IsCreated = true;
                writeHandle.AbsolutePath = absolutePath;
                writeHandle.SourceStates = new NativeArray<EntityDataRecord>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.SortEntries = new NativeArray<SectorEntityStateSortEntry>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.RadixScratch = new NativeArray<SectorEntityStateSortEntry>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.SortedEntityStates = new NativeArray<EntityDataRecord>(recordCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.FileBytes = new NativeArray<byte>(fileCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                writeHandle.ResultLength = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RadixCounts = new NativeArray<int>(1 << 16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RadixOffsets = new NativeArray<int>(1 << 16, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writeHandle.RegisterNativeMemorySentinel();
            }
            catch (Exception ex)
            {
                writeHandle.Dispose();
                error = $"Sector entity-state write buffers could not be allocated: {ex.Message}";
                return false;
            }

            for (int i = 0; i < recordCount; i++)
                writeHandle.SourceStates[i] = entityStates[i];

            BuildSectorEntityStateSortEntriesJob buildJob = new BuildSectorEntityStateSortEntriesJob
            {
                SourceStates = writeHandle.SourceStates,
                Entries = writeHandle.SortEntries,
                ChunkSizeMeters = math.max(1, chunkSizeMeters)
            };
            RadixSortSectorEntityStateEntriesJob sortJob = new RadixSortSectorEntityStateEntriesJob
            {
                Entries = writeHandle.SortEntries,
                Scratch = writeHandle.RadixScratch,
                Counts = writeHandle.RadixCounts,
                Offsets = writeHandle.RadixOffsets
            };
            ExtractSortedSectorEntityStatesJob extractJob = new ExtractSortedSectorEntityStatesJob
            {
                Entries = writeHandle.SortEntries,
                SortedStates = writeHandle.SortedEntityStates
            };
            CompressSectorEntityStateJob compressJob = new CompressSectorEntityStateJob
            {
                SortedStates = writeHandle.SortedEntityStates,
                FileBytes = writeHandle.FileBytes,
                ResultLength = writeHandle.ResultLength,
                SectorHash = sectorHash
            };

            try
            {
                JobHandle buildHandle = buildJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)));
                JobHandle sortHandle = sortJob.Schedule(buildHandle);
                JobHandle extractHandle = extractJob.Schedule(recordCount, math.min(64, math.max(1, recordCount)), sortHandle);
                writeHandle.Handle = compressJob.Schedule(extractHandle);
            }
            catch (Exception ex)
            {
                writeHandle.Dispose();
                error = $"Sector entity-state write job scheduling failed: {ex.Message}";
                return false;
            }

            return true;
        }

        internal static bool TryCompleteIndexedSectorEntityStateOverrideWrite(
            ref IndexedSectorEntityStateWriteHandle writeHandle,
            out string error)
        {
            error = string.Empty;
            if (!writeHandle.IsCreated)
            {
                error = "Sector entity-state write handle is not initialized.";
                return false;
            }

            if (!writeHandle.Handle.IsCompleted)
            {
                error = "Sector entity-state write job is still running.";
                return false;
            }

            DispatcherJobSwap.TryComplete(ref writeHandle.Handle, forceComplete: true);

            int fileLength = writeHandle.ResultLength[0];
            if (fileLength <= UnsafeUtility.SizeOf<SectorEntityStateFileHeader>())
            {
                error = "Sector entity-state LZ4 compression failed.";
                writeHandle.Dispose();
                return false;
            }

            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writeHandle.FileBytes);
            bool written = AsyncWriteManager.WriteAll(writeHandle.AbsolutePath, filePtr, fileLength, out error);
            writeHandle.Dispose();
            return written;
        }

        internal static void DisposeIndexedSectorEntityStateOverrideWrite(ref IndexedSectorEntityStateWriteHandle writeHandle)
        {
            if (!writeHandle.IsCreated)
                return;

            SaveBinaryStorage.DisposeIndexedSectorEntityStateOverrideWriteDeferred(ref writeHandle, default);
            JobHandle.ScheduleBatchedJobs();
        }

        internal static JobHandle DisposeIndexedSectorEntityStateOverrideWriteDeferred(
            ref IndexedSectorEntityStateWriteHandle writeHandle,
            JobHandle dependency)
        {
            if (!writeHandle.IsCreated)
                return dependency;

            return writeHandle.DisposeDeferred(dependency);
        }

        internal static bool TryReadIndexedSectorEntityStateOverride(
            string absolutePath,
            out long sectorHash,
            out EntityDataRecord[] entityStates,
            out string error)
        {
            sectorHash = 0L;
            entityStates = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Sector entity-state override file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out error))
                return false;

            try
            {
                int headerSize = UnsafeUtility.SizeOf<SectorEntityStateFileHeader>();
                if (mapping.Length < headerSize + 8)
                {
                    error = "Sector entity-state override file is truncated.";
                    return false;
                }

                SectorEntityStateFileHeader header = UnsafeUtility.ReadArrayElement<SectorEntityStateFileHeader>((byte*)mapping.View, 0);
                sectorHash = header.SectorHash;
                if (header.CompressedSize <= 0 ||
                    header.DecompressedSize <= 0 ||
                    header.RecordCount == 0 ||
                    mapping.Length < headerSize ||
                    header.CompressedSize > mapping.Length - headerSize)
                {
                    error = "Sector entity-state override header is invalid.";
                    return false;
                }

                int expectedByteLength = checked((int)header.RecordCount * UnsafeUtility.SizeOf<EntityDataRecord>());
                if (expectedByteLength != header.DecompressedSize)
                {
                    error = "Sector entity-state override byte count does not match the record count.";
                    return false;
                }

                using NativeArray<byte> rawBytes = new NativeArray<byte>(header.DecompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBytes);
                int failedBlockIndex = -1;
                int decompressedLength = Lz4BlockDecompress(
                    (byte*)mapping.View + headerSize,
                    header.CompressedSize,
                    rawPtr,
                    header.DecompressedSize,
                    out failedBlockIndex);
                if (decompressedLength != header.DecompressedSize)
                {
                    error = "Sector entity-state override decompression failed.";
                    return false;
                }

                entityStates = new EntityDataRecord[header.RecordCount];
                fixed (EntityDataRecord* destinationPtr = entityStates)
                {
                    if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, entityStates.Length * UnsafeUtility.SizeOf<EntityDataRecord>(), rawPtr, decompressedLength))
                    {
                        error = "Entity-state override copy exceeded destination bounds.";
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static long ComputeModPayloadPagedSectorHash(long gridX, float localX, long gridZ, float localZ)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double absoluteX = (gridX * cellSize) + localX;
            double absoluteZ = (gridZ * cellSize) + localZ;
            int2 sectorCoord = new int2(
                (int)math.floor(absoluteX / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absoluteZ / PersistentWorldSectorEdgeLengthMeters));
            return PackSectorHash(sectorCoord);
        }

        internal static long ComputePersistentWorldPagedSectorHash(in AbsoluteUniversePosition position)
        {
            return PackSectorHash(QuantizePersistentWorldPagedSector(in position));
        }

        private static int2 QuantizePersistentWorldPagedSector(in AbsoluteUniversePosition position)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolute.x / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PersistentWorldSectorEdgeLengthMeters));
        }

        private static int2 UnpackSectorHash(long sectorHash)
        {
            return new int2((int)(sectorHash >> 32), unchecked((int)(uint)sectorHash));
        }

        private static bool IsPersistentWorldPagedSectorWithinLoadWindow(long sectorHash, in AbsoluteUniversePosition centerPosition)
        {
            int2 sector = UnpackSectorHash(sectorHash);
            double3 center = centerPosition.ToAbsoluteDouble3();
            double sectorCenterX = ((double)sector.x + 0.5) * PersistentWorldSectorEdgeLengthMeters;
            double sectorCenterZ = ((double)sector.y + 0.5) * PersistentWorldSectorEdgeLengthMeters;
            double deltaX = sectorCenterX - center.x;
            double deltaZ = sectorCenterZ - center.z;
            return (deltaX * deltaX) + (deltaZ * deltaZ) <= PersistentWorldSectorLoadRadiusSq;
        }

        private static void ReportIndexedSectorQuarantine(long sectorHash, string primaryError, string backupError)
        {
            Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 1);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _indexedSectorQuarantineWarningHash,
                unchecked((uint)sectorHash),
                1f);
            CrashTelemetryBuffer.ReportSaveSystemCriticalFault();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SaveBinaryStorage] QUARANTINED indexed sector 0x{sectorHash:X16}. Primary: {primaryError} Backup: {backupError}");
#endif
        }

        internal static long ComputeModPayloadSectorHash(uint modHash, long pagedSectorHash)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ modHash) * 1099511628211UL;
                hash = (hash ^ (ulong)pagedSectorHash) * 1099511628211UL;
                hash &= 0x0000FFFFFFFFFFFFUL;
                return (long)(ModPayloadSectorPrefix | hash);
            }
        }

        internal static bool IsModPayloadSectorHash(long sectorHash)
        {
            return (((ulong)sectorHash) & ModPayloadSectorMask) == ModPayloadSectorPrefix;
        }

        internal static bool ConsumeIndexedSectorQuarantineFlag()
        {
            return Interlocked.Exchange(ref s_indexedSectorQuarantineReported, 0) != 0;
        }

        internal static bool TryCommitModPayloadSubSector(
            string absoluteSavePath,
            string tempOverridePath,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> payloadBytes,
            int payloadLength,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || string.IsNullOrEmpty(tempOverridePath))
            {
                error = "Mod payload commit paths are invalid.";
                return false;
            }

            if (modHash == 0u)
            {
                error = "Mod payload owner hash is zero.";
                return false;
            }

            if (!payloadBytes.IsCreated || payloadLength < 0 || payloadLength > ModPayloadMaxBytes || payloadLength > payloadBytes.Length)
            {
                error = "Mod payload exceeds the 16KB isolated sub-sector budget.";
                return false;
            }

            using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.ClearMemory);
            int compressedCapacity = ModPayloadSubBlockSizeBytes + (ModPayloadSubBlockSizeBytes / 255) + 16 + StandardCompressedBlockHeaderBytes + IndexedSectorBlockHeaderSize + 32;
            int fileCapacity = UnsafeUtility.SizeOf<SectorOverrideFileHeader>() + compressedCapacity;
            using NativeArray<byte> fileBytes = new NativeArray<byte>(fileCapacity, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            byte* filePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fileBytes);
            byte* payloadSource = payloadLength > 0
                ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(payloadBytes)
                : null;

            if (payloadLength > 0)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(rawPtr + ModPayloadHeaderSizeBytes, rawBlockBytes.Length - ModPayloadHeaderSizeBytes, payloadSource, payloadLength))
                {
                    error = "Mod payload write exceeded sub-sector bounds.";
                    return false;
                }
            }

            ModPayloadSubSectorHeader payloadHeader = new ModPayloadSubSectorHeader
            {
                Magic = ModPayloadMagic,
                Version = ModPayloadVersion,
                HeaderSize = ModPayloadHeaderSizeBytes,
                ModHash = modHash,
                PayloadLength = unchecked((ushort)payloadLength),
                Flags = 0,
                PagedSectorHash = pagedSectorHash,
                PayloadChecksum = 0u,
                Reserved = 0u
            };
            UnsafeUtility.CopyStructureToPtr(ref payloadHeader, rawPtr);

            int fileCursor = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
            if (!TryWriteIndexedCompressedBlock(
                    rawPtr,
                    ModPayloadSubBlockSizeBytes,
                    filePtr,
                    fileBytes.Length,
                    ref fileCursor,
                    out int storedBlockLength,
                    out uint blockFlags,
                    out error))
            {
                return false;
            }

            long sectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            SectorOverrideFileHeader overrideHeader = new SectorOverrideFileHeader
            {
                SectorHash = sectorHash,
                CompressedSize = storedBlockLength,
                DecompressedSize = ModPayloadSubBlockSizeBytes,
                Checksum = 0u,
                Flags = blockFlags
            };

            UnsafeUtility.CopyStructureToPtr(ref overrideHeader, filePtr);
            string tempDirectory = Path.GetDirectoryName(tempOverridePath);
            if (!string.IsNullOrEmpty(tempDirectory))
                Directory.CreateDirectory(tempDirectory);

            if (!AsyncWriteManager.WriteAll(tempOverridePath, filePtr, fileCursor, out error))
                return false;

            return TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error);
        }

        internal static bool TryReadIndexedModPayloadDirectory(
            string absolutePath,
            List<ModPayloadSectorInfo> results,
            out string error)
        {
            error = string.Empty;
            if (results == null)
            {
                error = "Mod payload directory destination is null.";
                return false;
            }

            results.Clear();
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectoryHeaderForMappedScan(
                        in header,
                        ref mapping,
                        out IndexedSectorDirectoryHeader directoryHeader,
                        out int entryCursor,
                        out long metadataEndOffset,
                        out error))
                {
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                int sectorEntrySize = UnsafeUtility.SizeOf<SectorEntry>();
                int populatedCount = 0;
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                    entryCursor += sectorEntrySize;
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                    {
                        error = $"Indexed sector entry {i} exceeded the file bounds.";
                        return false;
                    }

                    populatedCount++;
                    if (!IsModPayloadSectorHash(entry.SectorHash))
                        continue;

                    if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out _))
                        continue;

                    if (payloadHeader.ModHash == 0u ||
                        entry.SectorHash != ComputeModPayloadSectorHash(payloadHeader.ModHash, payloadHeader.PagedSectorHash))
                    {
                        error = "Mod payload directory identity mismatch.";
                        return false;
                    }

                    results.Add(new ModPayloadSectorInfo(
                        entry.SectorHash,
                        payloadHeader.ModHash,
                        payloadHeader.PagedSectorHash,
                        payloadHeader.PayloadLength,
                        payloadHeader.PayloadChecksum));
                }

                if (populatedCount != (int)directoryHeader.SectorCount)
                {
                    error = $"Indexed sector directory count mismatch. Header={directoryHeader.SectorCount}, Populated={populatedCount}.";
                    return false;
                }

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadIndexedModPayloads(
            string absolutePath,
            List<ModPayloadSectorInfo> results,
            NativeArray<byte> payloadBytes,
            ModPayloadReadHandler readHandler,
            out string error)
        {
            error = string.Empty;
            if (!payloadBytes.IsCreated || payloadBytes.Length < ModPayloadMaxBytes || readHandler == null)
            {
                error = "Mod payload batch read request is invalid.";
                return false;
            }

            bool collectResults = results != null;
            if (collectResults)
                results.Clear();

            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectoryHeaderForMappedScan(
                        in header,
                        ref mapping,
                        out IndexedSectorDirectoryHeader directoryHeader,
                        out int entryCursor,
                        out long metadataEndOffset,
                        out error))
                {
                    return false;
                }

                byte* filePtr = (byte*)mapping.View;
                int sectorEntrySize = UnsafeUtility.SizeOf<SectorEntry>();
                int populatedCount = 0;
                int skippedModSectorCount = 0;
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < IndexedSectorDirectorySlotCount; i++)
                {
                    SectorEntry entry = UnsafeUtility.ReadArrayElement<SectorEntry>(filePtr + entryCursor, 0);
                    entryCursor += sectorEntrySize;
                    if (!IsIndexedSectorEntryPopulated(in entry))
                        continue;

                    if (!IsIndexedSectorEntryWithinFileBounds(in entry, metadataEndOffset, mapping.Length))
                    {
                        error = $"Indexed sector entry {i} exceeded the file bounds.";
                        return false;
                    }

                    populatedCount++;
                    if (!IsModPayloadSectorHash(entry.SectorHash))
                        continue;

                    if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out _))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (payloadHeader.ModHash == 0u ||
                        entry.SectorHash != ComputeModPayloadSectorHash(payloadHeader.ModHash, payloadHeader.PagedSectorHash))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    ModPayloadSectorInfo sectorInfo = new ModPayloadSectorInfo(
                        entry.SectorHash,
                        payloadHeader.ModHash,
                        payloadHeader.PagedSectorHash,
                        payloadHeader.PayloadLength,
                        payloadHeader.PayloadChecksum);

                    if (!TryCopyModPayloadFromRawBlock(rawBlockBytes, payloadHeader.PayloadLength, payloadBytes, out error))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (!readHandler(in sectorInfo, payloadBytes, payloadHeader.PayloadLength, out error))
                    {
                        skippedModSectorCount++;
                        continue;
                    }

                    if (collectResults)
                        results.Add(sectorInfo);
                }

                if (populatedCount != (int)directoryHeader.SectorCount)
                {
                    error = $"Indexed sector directory count mismatch. Header={directoryHeader.SectorCount}, Populated={populatedCount}.";
                    return false;
                }

                if (skippedModSectorCount > 0)
                    error = $"Skipped {skippedModSectorCount} invalid mod payload sector(s).";

                return true;
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadModPayloadSubSector(
            string absolutePath,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> destination,
            out int payloadLength,
            out string error)
        {
            payloadLength = 0;
            error = string.Empty;
            if (modHash == 0u || !destination.IsCreated)
            {
                error = "Mod payload read request is invalid.";
                return false;
            }

            long sectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            if (!TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping mapping, out SaveFileHeader header, out _, out error))
                return false;

            try
            {
                if (!TryReadIndexedDirectory(in header, ref mapping, out _, out SectorEntry[] sectorEntries, out error))
                    return false;

                if (!TryFindIndexedSectorEntryIndex(sectorEntries, sectorHash, out int sectorEntryIndex))
                {
                    error = $"Mod payload sector 0x{sectorHash:X16} is missing.";
                    return false;
                }

                SectorEntry entry = sectorEntries[sectorEntryIndex];
                using NativeArray<byte> rawBlockBytes = new NativeArray<byte>(ModPayloadSubBlockSizeBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                if (!TryReadModPayloadHeaderFromEntry(ref mapping, in entry, rawBlockBytes, out ModPayloadSubSectorHeader payloadHeader, out error))
                    return false;

                if (payloadHeader.ModHash != modHash || payloadHeader.PagedSectorHash != pagedSectorHash)
                {
                    error = "Mod payload header identity mismatch.";
                    return false;
                }

                payloadLength = payloadHeader.PayloadLength;
                if (payloadLength > destination.Length)
                {
                    error = "Destination buffer is smaller than the mod payload.";
                    return false;
                }

                return TryCopyModPayloadFromRawBlock(rawBlockBytes, payloadLength, destination, out error);
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref mapping);
            }
        }

        internal static bool TryReadModPayloadSubSector(
            string absolutePath,
            long sectorHash,
            uint modHash,
            long pagedSectorHash,
            NativeArray<byte> destination,
            out int payloadLength,
            out string error)
        {
            long expectedSectorHash = ComputeModPayloadSectorHash(modHash, pagedSectorHash);
            if (sectorHash != expectedSectorHash)
            {
                payloadLength = 0;
                error = "Mod payload sector hash does not match the mod/page identity.";
                return false;
            }

            return TryReadModPayloadSubSector(
                absolutePath,
                modHash,
                pagedSectorHash,
                destination,
                out payloadLength,
                out error);
        }

        private static bool TryReadModPayloadHeaderFromEntry(
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            in SectorEntry entry,
            NativeArray<byte> rawBlockBytes,
            out ModPayloadSubSectorHeader payloadHeader,
            out string error)
        {
            payloadHeader = default;
            if (!rawBlockBytes.IsCreated || rawBlockBytes.Length < ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload read buffer is not initialized.";
                return false;
            }

            if (entry.DecompressedSize != ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload sector decompressed size mismatch.";
                return false;
            }

            if (!TryReadIndexedCompressedBlock(ref mapping, entry.ByteOffset, entry.CompressedSize, entry.DecompressedSize, rawBlockBytes, out int decompressedLength, out error))
                return false;

            if (decompressedLength != ModPayloadSubBlockSizeBytes)
            {
                error = "Mod payload sector length mismatch.";
                return false;
            }

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            payloadHeader = UnsafeUtility.ReadArrayElement<ModPayloadSubSectorHeader>(rawPtr, 0);
            if (payloadHeader.Magic != ModPayloadMagic ||
                payloadHeader.Version != ModPayloadVersion ||
                payloadHeader.HeaderSize != ModPayloadHeaderSizeBytes ||
                payloadHeader.PayloadLength > ModPayloadMaxBytes)
            {
                error = "Mod payload header is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryCopyModPayloadFromRawBlock(
            NativeArray<byte> rawBlockBytes,
            int payloadLength,
            NativeArray<byte> destination,
            out string error)
        {
            error = string.Empty;
            if (!rawBlockBytes.IsCreated ||
                !destination.IsCreated ||
                rawBlockBytes.Length < ModPayloadSubBlockSizeBytes ||
                payloadLength < 0 ||
                payloadLength > ModPayloadMaxBytes ||
                payloadLength > destination.Length)
            {
                error = "Mod payload copy request is invalid.";
                return false;
            }

            if (payloadLength <= 0)
                return true;

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBlockBytes);
            byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, destination.Length, rawPtr + ModPayloadHeaderSizeBytes, payloadLength))
            {
                error = "Mod payload read exceeded destination bounds.";
                return false;
            }

            return true;
        }

        private static bool TryReadIndexedDirectoryHeaderForMappedScan(
            in SaveFileHeader header,
            ref AsyncWriteManager.ReadOnlyMapping mapping,
            out IndexedSectorDirectoryHeader directoryHeader,
            out int entryCursor,
            out long metadataEndOffset,
            out string error)
        {
            directoryHeader = default;
            entryCursor = 0;
            metadataEndOffset = 0L;
            error = string.Empty;

            if (!TryValidateIndexedBlockStorageHeader(in header, out error))
                return false;

            int directoryOffset = CurrentHeaderSize;
            if (mapping.Length < directoryOffset ||
                (long)directoryOffset > mapping.Length - IndexedSectorDirectoryHeaderSize)
            {
                error = "Indexed sector directory header is truncated.";
                return false;
            }

            byte* filePtr = (byte*)mapping.View;
            directoryHeader = UnsafeUtility.ReadArrayElement<IndexedSectorDirectoryHeader>(filePtr + directoryOffset, 0);
            if (directoryHeader.SectorCount > IndexedSectorDirectorySlotCount)
            {
                error = $"Indexed sector directory count {directoryHeader.SectorCount} exceeded slot capacity {IndexedSectorDirectorySlotCount}.";
                return false;
            }

            directoryHeader.ChunkSizeMeters = math.max(1, directoryHeader.ChunkSizeMeters);

            int directoryBytes = IndexedSectorDirectoryHeaderSize + (IndexedSectorDirectorySlotCount * UnsafeUtility.SizeOf<SectorEntry>());
            if ((long)directoryOffset > mapping.Length - directoryBytes)
            {
                error = "Indexed sector directory exceeds the file bounds.";
                return false;
            }

            long directoryEndOffset = directoryOffset + (long)directoryBytes;
            long metadataOffset = header.PlayerOffset;
            if (metadataOffset < directoryEndOffset || metadataOffset >= mapping.Length)
            {
                error = "Indexed metadata block offset is out of bounds.";
                return false;
            }

            if (directoryHeader.MetadataCompressedSize < 0 ||
                metadataOffset > mapping.Length - directoryHeader.MetadataCompressedSize)
            {
                error = "Indexed metadata block size is out of bounds.";
                return false;
            }

            entryCursor = directoryOffset + IndexedSectorDirectoryHeaderSize;
            metadataEndOffset = metadataOffset + directoryHeader.MetadataCompressedSize;
            return true;
        }

        internal static bool TryCommitIndexedPersistentWorldSectorOverride(
            string absoluteSavePath,
            string sectorOverridePath,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || string.IsNullOrEmpty(sectorOverridePath))
            {
                error = "Sector override commit paths are invalid.";
                return false;
            }

            if (!File.Exists(absoluteSavePath) || !File.Exists(sectorOverridePath))
            {
                error = "Sector override commit source file is missing.";
                return false;
            }

            if (!AsyncWriteManager.TryOpenReadOnlyMapping(sectorOverridePath, out AsyncWriteManager.ReadOnlyMapping overrideMapping, out error))
                return false;

            long sectorHash = 0L;
            int overrideCompressedSize = 0;
            int overrideDecompressedSize = 0;
            uint overrideChecksum = 0u;
            NativeArray<byte> overrideBlockBytes = default;
            try
            {
                int overrideHeaderSize = UnsafeUtility.SizeOf<SectorOverrideFileHeader>();
                if (overrideMapping.Length < overrideHeaderSize + IndexedSectorBlockHeaderSize)
                {
                    error = "Sector override commit file is truncated.";
                    return false;
                }

                SectorOverrideFileHeader overrideHeader = UnsafeUtility.ReadArrayElement<SectorOverrideFileHeader>((byte*)overrideMapping.View, 0);
                sectorHash = overrideHeader.SectorHash;
                overrideCompressedSize = overrideHeader.CompressedSize;
                overrideDecompressedSize = overrideHeader.DecompressedSize;
                overrideChecksum = overrideHeader.Checksum;
                if (overrideCompressedSize <= IndexedSectorBlockHeaderSize ||
                    overrideMapping.Length < overrideHeaderSize ||
                    overrideCompressedSize > overrideMapping.Length - overrideHeaderSize)
                {
                    error = "Sector override commit header is invalid.";
                    return false;
                }

                overrideBlockBytes = new NativeArray<byte>(overrideCompressedSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                byte* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(overrideBlockBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, overrideBlockBytes.Length, (byte*)overrideMapping.View + overrideHeaderSize, overrideCompressedSize))
                {
                    error = "Sector override staging copy exceeded destination bounds.";
                    return false;
                }
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref overrideMapping);
            }

            if (!TryReadValidatedHeader(absoluteSavePath, out AsyncWriteManager.ReadOnlyMapping saveMapping, out SaveFileHeader saveHeader, out _, out error))
                return false;

            SectorEntry[] sectorEntries;
            IndexedSectorDirectoryHeader directoryHeader;
            ulong metadataHash64;
            IndexedSectorCommitTarget commitTarget;
            int sectorCountDelta;
            try
            {
                if (!TryReadIndexedDirectory(in saveHeader, ref saveMapping, out directoryHeader, out sectorEntries, out error))
                    return false;

                ulong directoryHash64 = saveHeader.PlayerOffset > CurrentHeaderSize
                    ? Hash64((byte*)saveMapping.View + CurrentHeaderSize, (int)(saveHeader.PlayerOffset - CurrentHeaderSize))
                    : 0UL;
                metadataHash64 = saveHeader.HashPayload64 ^ directoryHash64;

                if (!TryResolveIndexedSectorCommitTarget(
                        sectorEntries,
                        sectorHash,
                        overrideCompressedSize,
                        saveMapping.Length,
                        out commitTarget,
                        out sectorCountDelta,
                        out error))
                {
                    return false;
                }
            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref saveMapping);
            }

            FileStream fileStream = null;
            MemoryMappedFile fileMapping = null;
            MemoryMappedViewAccessor accessor = null;
            byte* filePtr = null;
            try
            {
                long newLength = commitTarget.NewFileLength;
                fileStream = new FileStream(absoluteSavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fileStream.SetLength(newLength);
                fileMapping = MemoryMappedFile.CreateFromFile(fileStream, null, newLength, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                accessor = fileMapping.CreateViewAccessor(0L, newLength, MemoryMappedFileAccess.ReadWrite);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref filePtr);
                byte* mappedFilePtr = filePtr + accessor.PointerOffset;

                byte* overrideBlockPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(overrideBlockBytes);
                if (!UnsafeMemoryCopyGuard.SafeCopy(mappedFilePtr + commitTarget.WriteOffset, newLength - commitTarget.WriteOffset, overrideBlockPtr, overrideCompressedSize))
                {
                    error = "Sector override commit copy exceeded mapped file bounds.";
                    return false;
                }

                if (commitTarget.ReusedExistingSlot)
                {
                    int previousCompressedSize = sectorEntries[commitTarget.SlotIndex].CompressedSize;
                    int trailingSlack = previousCompressedSize - overrideCompressedSize;
                    if (trailingSlack > 0)
                        UnsafeUtility.MemClear(mappedFilePtr + commitTarget.WriteOffset + overrideCompressedSize, trailingSlack);
                }

                int directoryEntryOffset = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize + (commitTarget.SlotIndex * UnsafeUtility.SizeOf<SectorEntry>());
                SectorEntry updatedEntry = new SectorEntry
                {
                    SectorHash = sectorHash,
                    ByteOffset = commitTarget.WriteOffset,
                    CompressedSize = overrideCompressedSize,
                    DecompressedSize = overrideDecompressedSize,
                    Checksum = overrideChecksum
                };
                UnsafeUtility.CopyStructureToPtr(ref updatedEntry, mappedFilePtr + directoryEntryOffset);

                if (commitTarget.InsertedNewSlot && sectorCountDelta != 0)
                {
                    directoryHeader.SectorCount = checked((uint)(directoryHeader.SectorCount + sectorCountDelta));
                    UnsafeUtility.CopyStructureToPtr(ref directoryHeader, mappedFilePtr + CurrentHeaderSize);
                }

                SaveFileHeader updatedHeader = UnsafeUtility.ReadArrayElement<SaveFileHeader>(mappedFilePtr, 0);
                ulong newDirectoryHash64 = updatedHeader.PlayerOffset > CurrentHeaderSize
                    ? Hash64(mappedFilePtr + CurrentHeaderSize, (int)(updatedHeader.PlayerOffset - CurrentHeaderSize))
                    : 0UL;
                updatedHeader.HashPayload64 = metadataHash64 ^ newDirectoryHash64;
                updatedHeader.HashHeader64 = 0UL;
                updatedHeader.HashHeader64 = ComputeHeaderHash(ref updatedHeader);
                UnsafeUtility.CopyStructureToPtr(ref updatedHeader, mappedFilePtr);
                accessor.Flush();
                fileStream.Flush(true);
            }
            catch (Exception ex)
            {
                error = $"Sector override commit failed: {ex.Message}";
                return false;
            }
            finally
            {
                if (accessor != null && filePtr != null)
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();

                accessor?.Dispose();
                fileMapping?.Dispose();
                fileStream?.Dispose();
                if (overrideBlockBytes.IsCreated)
                    overrideBlockBytes.Dispose();
            }

            File.Delete(sectorOverridePath);
            return true;
        }

        internal static bool TryLoadSaveData(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            out SaveData data,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out ulong payloadHash64,
            out int rawPayloadLength,
            out int detectedVersion,
            out bool indexedBackupRecoveryUsed,
            out string error)
        {
            data = null;
            packedQuestHeader = default;
            packedQuestStateWords = null;
            persistentWorldDeltas = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            payloadHash64 = 0UL;
            rawPayloadLength = 0;
            detectedVersion = 0;
            indexedBackupRecoveryUsed = false;

            if (TryReadValidatedHeader(absolutePath, out AsyncWriteManager.ReadOnlyMapping v8Mapping, out SaveFileHeader v8Header, out _, out string headerError))
            {
                try
                {
                    if ((v8Header.Flags & FlagIndexedSectorBlocks) != 0 && v8Header.Version >= IndexedBlockStorageVersion)
                    {
                        return TryLoadSaveDataIndexedV8(
                            absolutePath,
                            slotName,
                            rawBuffer,
                            in v8Header,
                            ref v8Mapping,
                            out data,
                            out packedQuestHeader,
                            out packedQuestStateWords,
                            out persistentWorldDeltas,
                            out ecosystemSectorStates,
                            out voxelDeltaSnapshot,
                            out metadata,
                            out payloadHash64,
                            out rawPayloadLength,
                            out detectedVersion,
                            out indexedBackupRecoveryUsed,
                            out error);
                    }
                }
                finally
                {
                    AsyncWriteManager.CloseReadOnlyMapping(ref v8Mapping);
                }
            }
            else if (!string.IsNullOrEmpty(headerError))
            {
                error = headerError;
                return false;
            }

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefixInfo prefix, out byte* rawPtr, out rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            payloadHash64 = header.HashPayload64;

            int cursor = prefix.PrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
            if (!IsByteRangeWithin(cursor, saveDataLength, rawPayloadLength))
            {
                error = "Save payload byte range is invalid.";
                return false;
            }

            if (!SaveBinaryPayloadCodec.TryRead(AddByteOffset(rawPtr, cursor), saveDataLength, out data, out int bytesRead, out error))
                return false;

            if (bytesRead != saveDataLength)
            {
                error = "Binary save payload length mismatch.";
                return false;
            }

            if (!TryReadPackedQuestStateWords(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    cursor + saveDataLength,
                    out packedQuestHeader,
                    out packedQuestStateWords,
                    out error))
            {
                return false;
            }

            if (!TryReadPersistentWorldDeltas(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    out persistentWorldDeltas,
                    out error))
            {
                return false;
            }

            if (!TryReadEcosystemSectorStates(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    out ecosystemSectorStates,
                    out error))
            {
                return false;
            }

            if (!TryReadVoxelDeltaSnapshot(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    out voxelDeltaSnapshot,
                    out error))
            {
                return false;
            }

            detectedVersion = prefix.SaveDataVersion;
            metadata = new SaveMetadata
            {
                SlotName = slotName,
                GameVersion = gameVersion,
                Timestamp = ToUtcTicks(header.TimestampUnixMs),
                PlayTimeSeconds = prefix.PlayTimeSeconds,
                SceneName = sceneName,
                PlayerPosition = ToRuntimePosition(prefix.PlayerPosition),
                Checksum = FormatPayloadChecksum(in header)
            };

            error = string.Empty;
            return true;
        }

        internal static uint Hash32(void* ptr, long length)
        {
            return xxHash3.Hash64(ptr, length).x;
        }

        internal static int AlignExplorationMortonByteCount(int byteCount)
        {
            if (byteCount <= 0)
                return 0;

            int aligned = (byteCount + (ExplorationMortonMaskAlignmentBytes - 1)) & ~(ExplorationMortonMaskAlignmentBytes - 1);
            return math.min(aligned, ExplorationMapDTO.MortonMaskByteCount);
        }

        internal static uint ComputeExplorationMortonSeed(uint payload)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ ExplorationMortonBuildSalt32) * 16777619u;
                hash = (hash ^ payload) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private static string ResolveIndexedSaveBackupPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || absolutePath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return $"{absolutePath}.bak";
        }

        internal static bool IsIndexedBlockStorageRecoveryError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;

            return error.IndexOf("Save header checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Header checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file magic mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save magic mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file is smaller than", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save file is truncated inside the fixed header", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Unsupported save header version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Save header is not an indexed sector container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed sector block decompression requires save header version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed sector directory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed metadata block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed LZ4 block decompression failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed block length mismatch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Indexed aggregate payload checksum mismatch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static ulong Hash64(void* ptr, long length)
        {
            uint2 hash = xxHash3.Hash64(ptr, length);
            return ((ulong)hash.y << 32) | hash.x;
        }

        private static byte* AddByteOffset(void* source, int byteOffset)
        {
            // Byte-wise pointer math only. Never advance typed pointers by raw byte offsets.
            return (byte*)source + byteOffset;
        }

        private static bool IsByteRangeWithin(int byteOffset, int byteLength, int totalByteLength)
        {
            return byteOffset >= 0 &&
                   byteLength >= 0 &&
                   totalByteLength >= 0 &&
                   byteOffset <= totalByteLength &&
                   byteLength <= totalByteLength - byteOffset;
        }

        private static long ComputePersistentWorldSectorHash(in PersistentWorldDeltaRecord record, int chunkSizeMeters)
        {
            AbsoluteUniversePosition position = record.UnpackPosition(chunkSizeMeters);
            double3 absolute = position.ToAbsoluteDouble3();
            int2 sectorCoord = new int2(
                (int)math.floor(absolute.x / PersistentWorldSectorEdgeLengthMeters),
                (int)math.floor(absolute.z / PersistentWorldSectorEdgeLengthMeters));
            return PackSectorHash(sectorCoord);
        }

        private static long PackSectorHash(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static List<IndexedSectorGroup> BuildIndexedSectorGroups(NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas, int chunkSizeMeters)
        {
            int capacity = persistentWorldDeltas.IsCreated ? math.max(4, persistentWorldDeltas.Length / 16) : 4;
            Dictionary<long, IndexedSectorGroup> groupsByHash = new Dictionary<long, IndexedSectorGroup>(capacity);
            List<IndexedSectorGroup> groups = new List<IndexedSectorGroup>(capacity);

            if (!persistentWorldDeltas.IsCreated || persistentWorldDeltas.Length <= 0)
                return groups;

            int safeChunkSizeMeters = math.max(1, chunkSizeMeters);
            for (int i = 0; i < persistentWorldDeltas.Length; i++)
            {
                PersistentWorldDeltaRecord record = persistentWorldDeltas[i];
                if (!record.IsValid)
                    continue;

                long sectorHash = ComputePersistentWorldSectorHash(in record, safeChunkSizeMeters);
                if (!groupsByHash.TryGetValue(sectorHash, out IndexedSectorGroup group))
                {
                    group = new IndexedSectorGroup
                    {
                        SectorHash = sectorHash,
                        Records = new List<PersistentWorldDeltaRecord>(8)
                    };
                    groupsByHash.Add(sectorHash, group);
                    groups.Add(group);
                }

                group.Records.Add(record);
            }

            groups.Sort(static (a, b) => a.SectorHash.CompareTo(b.SectorHash));
            return groups;
        }

        internal static int Lz4BlockCompress(byte* source, int sourceLength, byte* destination, int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            return Lz4BlockCompressStandard(source, sourceLength, destination, destinationCapacity);
        }

        private static int Lz4BlockCompressStandard(
            byte* source,
            int sourceLength,
            byte* destination,
            int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            int blockCount = (sourceLength + BlockSizeBytes - 1) / BlockSizeBytes;
            int sourceOffset = 0;
            int destinationOffset = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int rawBlockLength = math.min(BlockSizeBytes, sourceLength - sourceOffset);
                if (destinationOffset + StandardCompressedBlockHeaderBytes > destinationCapacity)
                    return 0;

                byte* rawBlockSource = source + sourceOffset;
                byte* blockDestination = destination + destinationOffset + StandardCompressedBlockHeaderBytes;
                int blockDestinationCapacity = destinationCapacity - destinationOffset - StandardCompressedBlockHeaderBytes;
                int blockCompressedLength = LZ4Compress(rawBlockSource, blockDestination, rawBlockLength, blockDestinationCapacity);
                if (blockCompressedLength <= 0)
                    return 0;

                UnsafeUtility.WriteArrayElement(destination + destinationOffset, 0, blockCompressedLength);
                UnsafeUtility.WriteArrayElement(destination + destinationOffset + 4, 0, rawBlockLength);

                sourceOffset += rawBlockLength;
                destinationOffset += StandardCompressedBlockHeaderBytes + blockCompressedLength;
            }

            return destinationOffset;
        }

        private static bool TryReadPayload(
            string absolutePath,
            NativeArray<byte> rawBuffer,
            out SaveFileHeader header,
            out PayloadPrefixInfo prefix,
            out byte* rawPtr,
            out int rawPayloadLength,
            out string error)
        {
            header = default;
            prefix = default;
            rawPtr = null;
            rawPayloadLength = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                error = "Save file is missing.";
                return false;
            }

            if (!rawBuffer.IsCreated)
            {
                error = "Native raw save buffer is not initialized.";
                return false;
            }

            if (!AsyncWriteManager.TryGetFileLength(absolutePath, out long fileLength, out error))
                return false;

            if (fileLength < LegacyHeaderSize)
            {
                error = "Save file is smaller than the fixed header.";
                return false;
            }

            if (fileLength > int.MaxValue)
            {
                error = "Save file exceeds the supported native staging range.";
                return false;
            }

            if (fileLength > CurrentHeaderSize + MaxCompressedPayloadBytes)
            {
                error = "Save file exceeds the maximum supported compressed payload size.";
                return false;
            }

            rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            // MMF path: decompression reads directly from the mapped view into the persistent raw payload buffer.
            AsyncWriteManager.ReadOnlyMapping readMapping = default;
            try
            {
                if (!AsyncWriteManager.TryOpenReadOnlyMapping(absolutePath, out readMapping, out error))
                    return false;

                if (readMapping.Length != fileLength)
                {
                    error = "Mapped save length changed during payload read.";
                    return false;
                }

                byte* filePtr = (byte*)readMapping.View;

                SaveFileHeaderPrefix headerPrefix = UnsafeUtility.ReadArrayElement<SaveFileHeaderPrefix>(filePtr, 0);
                if (headerPrefix.MagicValue != Magic)
                {
                    error = "Save magic mismatch.";
                    return false;
                }

                int headerSizeBytes = ResolveHeaderSize(headerPrefix.Version);
                if (headerSizeBytes <= 0)
                {
                    error = $"Unsupported save header version {headerPrefix.Version}.";
                    return false;
                }

                if (fileLength < headerSizeBytes)
                {
                    error = "Save file is truncated inside the fixed header.";
                    return false;
                }

                bool isCurrentHeader = headerPrefix.Version >= First64BitHashVersion;
                if (isCurrentHeader)
                {
                    header = UnsafeUtility.ReadArrayElement<SaveFileHeader>(filePtr, 0);
                    ulong computedHeaderHash = Hash64(filePtr, CurrentHeaderHashSizeBytes);
                    if (computedHeaderHash != header.HashHeader64)
                    {
                        error = "Header checksum mismatch.";
                        return false;
                    }
                }
                else
                {
                    LegacySaveFileHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacySaveFileHeader>(filePtr, 0);
                    uint computedHeaderHash = Hash32(filePtr, LegacyHeaderHashSizeBytes);
                    if (computedHeaderHash != legacyHeader.HashHeader32)
                    {
                        error = "Header checksum mismatch.";
                        return false;
                    }

                    header = ConvertLegacyHeader(in legacyHeader);
                }

                if (!TryValidateHeader(header, out error))
                    return false;

                int compressedPayloadLength = (int)readMapping.Length - headerSizeBytes;
                if (compressedPayloadLength <= 0)
                {
                    error = "Save payload is missing.";
                    return false;
                }

                if (compressedPayloadLength > MaxCompressedPayloadBytes)
                {
                    error = "Compressed save payload exceeds the supported decoder budget.";
                    return false;
                }

                int failedBlockIndex = -1;
                rawPayloadLength = Lz4BlockDecompress(
                    AddByteOffset(filePtr, headerSizeBytes),
                    compressedPayloadLength,
                    rawPtr,
                    rawBuffer.Length,
                    out failedBlockIndex);
                if (rawPayloadLength <= 0)
                {
                    error = "LZ4 block decompression failed.";
                    return false;
                }

                if ((header.Flags & FlagTokenSubstitution) != 0)
                {
                    if (!TryExpandTokenizedPayloadInPlace(rawPtr, rawPayloadLength, rawBuffer.Length, out rawPayloadLength, out error))
                        return false;
                }

                if (rawPayloadLength > RawPayloadCapacityBytes || rawPayloadLength > rawBuffer.Length)
                {
                    error = "Decompressed save payload exceeded the decoder budget.";
                    return false;
                }

                if (isCurrentHeader)
                {
                    ulong computedPayloadHash = Hash64(rawPtr, rawPayloadLength);
                    if (computedPayloadHash != header.HashPayload64)
                    {
                        error = "Payload checksum mismatch.";
                        return false;
                    }
                }
                else
                {
                    uint computedPayloadHash = Hash32(rawPtr, rawPayloadLength);
                    if (computedPayloadHash != (uint)header.HashPayload64)
                    {
                        error = "Payload checksum mismatch.";
                        return false;
                    }
                }

                if (header.Version < SaveDataMigration_AupV8.AupV8Version)
                {
                    if (!SaveDataMigration_AupV8.TryMigratePayloadToV8(
                            rawPtr,
                            rawPayloadLength,
                            rawBuffer.Length,
                            out prefix,
                            out rawPayloadLength,
                            out int payloadByteShift,
                            out error))
                    {
                        return false;
                    }

                    header.DeltaOffset = checked((uint)((int)header.DeltaOffset + payloadByteShift));
                    header.EntityOffset = checked((uint)((int)header.EntityOffset + payloadByteShift));
                    header.HashPayload64 = Hash64(rawPtr, rawPayloadLength);
                }
                else if (!SaveDataMigration_AupV8.TryReadPayloadPrefix(rawPtr, rawPayloadLength, header.Version, out prefix, out error))
                {
                    return false;
                }

                int metadataBytes = prefix.PrefixSizeBytes + prefix.SceneNameByteLength + prefix.GameVersionByteLength;
                if (metadataBytes > rawPayloadLength)
                {
                    error = "Payload prefix string lengths exceed the decompressed payload length.";
                    return false;
                }

                int playerPayloadLength = metadataBytes + checked((int)prefix.SaveDataByteLength);
                if (playerPayloadLength > rawPayloadLength)
                {
                    error = "Serialized save data exceeds the decompressed payload length.";
                    return false;
                }

                int payloadBaseOffset = ResolvePayloadBaseOffset(in header);
                int deltaSectionOffset = checked((int)header.DeltaOffset) - payloadBaseOffset;
                int entitySectionOffset = checked((int)header.EntityOffset) - payloadBaseOffset;
                if (deltaSectionOffset < playerPayloadLength || deltaSectionOffset > rawPayloadLength)
                {
                    error = "Packed quest-state offset exceeds the decompressed payload bounds.";
                    return false;
                }

                if (entitySectionOffset < deltaSectionOffset || entitySectionOffset > rawPayloadLength)
                {
                    error = "Entity payload offset exceeds the decompressed payload bounds.";
                    return false;
                }

            }
            finally
            {
                AsyncWriteManager.CloseReadOnlyMapping(ref readMapping);
            }

            return true;
        }

        private static bool TryReadPackedQuestStateWords(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            int playerPayloadLength,
            out QuestSaveHeader packedQuestHeader,
            out uint[] packedQuestStateWords,
            out string error)
        {
            packedQuestHeader = default;
            packedQuestStateWords = null;
            error = string.Empty;

            int packedQuestWordCount = checked((int)header.DeltaCount);
            int payloadBaseOffset = ResolvePayloadBaseOffset(in header);
            int packedQuestSectionOffset = checked((int)header.DeltaOffset) - payloadBaseOffset;
            int entitySectionOffset = checked((int)header.EntityOffset) - payloadBaseOffset;
            if (packedQuestWordCount <= 0 || packedQuestSectionOffset == entitySectionOffset)
            {
                if (packedQuestSectionOffset < playerPayloadLength || packedQuestSectionOffset > rawPayloadLength)
                {
                    error = "Packed quest-state section offset is invalid.";
                    return false;
                }

                return true;
            }

            if (packedQuestSectionOffset < playerPayloadLength || packedQuestSectionOffset >= entitySectionOffset)
            {
                error = "Packed quest-state section overlaps the serialized player payload.";
                return false;
            }

            int sectionLength = entitySectionOffset - packedQuestSectionOffset;
            if (sectionLength < PackedQuestStateSectionHeaderSize)
            {
                error = "Packed quest-state section is truncated.";
                return false;
            }

            packedQuestHeader = UnsafeUtility.ReadArrayElement<QuestSaveHeader>(AddByteOffset(rawPtr, packedQuestSectionOffset), 0);
            if (packedQuestHeader.Magic != QuestSaveHeader.HeaderMagic)
            {
                error = "Packed quest-state header magic mismatch.";
                return false;
            }

            if (packedQuestHeader.FlagCount != header.DeltaCount)
            {
                error = "Packed quest-state word count header mismatch.";
                return false;
            }

            int expectedSectionLength = PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>());
            if (sectionLength != expectedSectionLength)
            {
                error = "Packed quest-state section length mismatch.";
                return false;
            }

            packedQuestStateWords = new uint[packedQuestWordCount];
            if (packedQuestWordCount <= 0)
                return true;

            fixed (uint* destinationPtr = packedQuestStateWords)
            {
                byte* packedQuestSourcePtr = AddByteOffset(rawPtr, packedQuestSectionOffset + PackedQuestStateSectionHeaderSize);
                int packedQuestBytes = packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>(), packedQuestSourcePtr, packedQuestBytes))
                {
                    error = "Packed quest-state section copy exceeded destination bounds.";
                    return false;
                }
            }

            uint computedChecksum = ComputePackedQuestStateChecksum(packedQuestStateWords);
            if (computedChecksum != packedQuestHeader.Checksum)
            {
                error = "Packed quest-state checksum mismatch.";
                return false;
            }

            return true;
        }

        private static bool TryReadPersistentWorldDeltas(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            error = string.Empty;

            int entityCount = checked((int)header.EntityCount);
            if (entityCount <= 0)
                return true;

            if (header.Version >= CompactPersistentWorldSectionVersion)
                return TryReadPersistentWorldDeltasV5(rawPtr, rawPayloadLength, header, out persistentWorldDeltas, out error);

            int entityRecordSize = UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>();
            int entitySectionOffset = checked((int)header.EntityOffset) - ResolvePayloadBaseOffset(in header);

            long entityBytesLong = (long)entityCount * entityRecordSize;
            if (entityBytesLong > int.MaxValue)
            {
                error = "Entity payload length exceeds the supported range.";
                return false;
            }

            int entityBytes = (int)entityBytesLong;
            if (entitySectionOffset < 0 || entitySectionOffset + entityBytes > rawPayloadLength)
            {
                error = "Entity payload exceeds the decompressed payload bounds.";
                return false;
            }

            persistentWorldDeltas = new PersistentWorldDeltaRecord[entityCount];
            if (entityBytes == 0)
                return true;

            fixed (PersistentWorldDeltaRecord* destinationPtr = persistentWorldDeltas)
            {
                byte* entitySourcePtr = AddByteOffset(rawPtr, entitySectionOffset);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, persistentWorldDeltas.Length * UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>(), entitySourcePtr, entityBytes))
                {
                    error = "Persistent-world delta copy exceeded destination bounds.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadVoxelDeltaSnapshot(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out NativeArray<byte> voxelDeltaSnapshot,
            out string error)
        {
            voxelDeltaSnapshot = default;
            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out int entitySectionLength, out error))
                return false;

            int voxelSectionOffset = entitySectionOffset + entitySectionLength;
            if (header.Version >= EcosystemSectionVersion)
            {
                if (!TryResolveEcosystemSectionLength(
                        rawPtr,
                        rawPayloadLength,
                        header,
                        entitySectionOffset,
                        entitySectionLength,
                        out int ecosystemSectionOffset,
                        out int ecosystemSectionLength,
                        out error))
                {
                    return false;
                }

                voxelSectionOffset = ecosystemSectionOffset + ecosystemSectionLength;
            }

            if (voxelSectionOffset < 0 || voxelSectionOffset > rawPayloadLength)
            {
                error = "Voxel delta payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            int voxelByteLength = rawPayloadLength - voxelSectionOffset;
            if (voxelByteLength <= 0)
                return true;

            voxelDeltaSnapshot = new NativeArray<byte>(voxelByteLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
            if (!UnsafeMemoryCopyGuard.SafeCopy(destinationPtr, voxelDeltaSnapshot.Length, AddByteOffset(rawPtr, voxelSectionOffset), voxelByteLength))
            {
                error = "Voxel delta payload copy exceeded destination bounds.";
                return false;
            }

            return true;
        }

        private static ulong ComputeHeaderHash(ref SaveFileHeader header)
        {
            SaveFileHeader copy = header;
            copy.HashHeader64 = 0UL;
            return Hash64(UnsafeUtility.AddressOf(ref copy), CurrentHeaderHashSizeBytes);
        }

        private static bool TryBuildPersistentWorldSectionTables(
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            out NativeParallelHashMap<int3, ushort> chunkLookup,
            out NativeList<int3> chunkTable,
            out NativeParallelHashMap<ulong, ushort> itemHashLookup,
            out NativeList<ulong> itemHashTable,
            out string error)
        {
            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int capacity = math.max(recordCount, 1);
            chunkLookup = new NativeParallelHashMap<int3, ushort>(capacity, Allocator.Temp);
            chunkTable = new NativeList<int3>(capacity, Allocator.Temp);
            itemHashLookup = new NativeParallelHashMap<ulong, ushort>(capacity, Allocator.Temp);
            itemHashTable = new NativeList<ulong>(capacity, Allocator.Temp);
            error = string.Empty;

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                if (!deltaRecord.IsValid)
                    continue;

                if (!chunkLookup.ContainsKey(deltaRecord.ChunkId))
                {
                    if (chunkTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta chunk table exceeded 65535 unique chunks.";
                        return false;
                    }

                    ushort chunkIndex = (ushort)chunkTable.Length;
                    chunkLookup.Add(deltaRecord.ChunkId, chunkIndex);
                    chunkTable.Add(deltaRecord.ChunkId);
                }

                if (deltaRecord.IsDeleted)
                    continue;

                if (!itemHashLookup.ContainsKey(deltaRecord.ItemPersistentIdHash))
                {
                    if (itemHashTable.Length >= ushort.MaxValue)
                    {
                        error = "Persistent-world delta item table exceeded 65535 unique item hashes.";
                        return false;
                    }

                    ushort itemIndex = (ushort)itemHashTable.Length;
                    itemHashLookup.Add(deltaRecord.ItemPersistentIdHash, itemIndex);
                    itemHashTable.Add(deltaRecord.ItemPersistentIdHash);
                }
            }

            return true;
        }

        private static int ComputePersistentWorldSectionLength(int entityCount, int chunkCount, int itemHashCount)
        {
            return PersistentWorldSectionHeaderSize +
                   checked(chunkCount * UnsafeUtility.SizeOf<int3>()) +
                   checked(itemHashCount * UnsafeUtility.SizeOf<ulong>()) +
                   checked(entityCount * UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>());
        }

        private static int ComputeEcosystemSectionLength(int recordCount)
        {
            return EcosystemSectionHeaderSize +
                   checked(math.max(recordCount, 0) * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>());
        }

        private static void WritePersistentWorldSection(
            byte* destination,
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltas,
            NativeParallelHashMap<int3, ushort> chunkLookup,
            NativeList<int3> chunkTable,
            NativeParallelHashMap<ulong, ushort> itemHashLookup,
            NativeList<ulong> itemHashTable)
        {
            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            int sectionLength = ComputePersistentWorldSectionLength(recordCount, chunkTable.Length, itemHashTable.Length);
            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)recordCount
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            int cursor = PersistentWorldSectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                int chunkBytes = chunkTable.Length * UnsafeUtility.SizeOf<int3>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, chunkSourcePtr, chunkBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    return;
                }

                cursor += chunkBytes;
            }

            if (itemHashTable.Length > 0)
            {
                void* itemSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(itemHashTable.AsArray());
                int itemBytes = itemHashTable.Length * UnsafeUtility.SizeOf<ulong>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(AddByteOffset(destination, cursor), sectionLength - cursor, itemSourcePtr, itemBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
                    return;
                }

                cursor += itemBytes;
            }

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                if (deltaRecord.IsDeleted &&
                    chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort deletedChunkIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = 1,
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = deletedChunkIndex,
                        ItemHashIndex = PersistentWorldDeletedItemHashIndex
                    };
                }
                else if (deltaRecord.IsValid &&
                         chunkLookup.TryGetValue(deltaRecord.ChunkId, out ushort chunkIndex) &&
                         itemHashLookup.TryGetValue(deltaRecord.ItemPersistentIdHash, out ushort itemHashIndex))
                {
                    saveRecord = new PersistentWorldSaveRecord16
                    {
                        PackedLocalPosition = deltaRecord.PackedLocalPosition,
                        InstanceUid = deltaRecord.InstanceUid,
                        Quantity = (ushort)math.clamp(deltaRecord.Quantity, 1, ushort.MaxValue),
                        ItemFlags = deltaRecord.ItemFlags,
                        Reserved = 0,
                        ChunkIndex = chunkIndex,
                        ItemHashIndex = itemHashIndex
                    };
                }

                UnsafeUtility.CopyStructureToPtr(ref saveRecord, AddByteOffset(destination, cursor));
                cursor += UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>();
            }
        }

        private static bool TryReadPersistentWorldDeltasV5(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out string error)
        {
            persistentWorldDeltas = null;
            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out _, out error))
                return false;

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(AddByteOffset(rawPtr, entitySectionOffset), 0);
            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int recordCount = checked((int)sectionHeader.RecordCount);
            if (recordCount != checked((int)header.EntityCount))
            {
                error = "Persistent-world delta section count mismatch.";
                return false;
            }

            persistentWorldDeltas = new PersistentWorldDeltaRecord[recordCount];
            if (recordCount <= 0)
            {
                error = string.Empty;
                return true;
            }

            int cursor = entitySectionOffset + PersistentWorldSectionHeaderSize;
            byte* chunkTablePtr = AddByteOffset(rawPtr, cursor);
            cursor += chunkCount * UnsafeUtility.SizeOf<int3>();
            byte* itemHashTablePtr = AddByteOffset(rawPtr, cursor);
            cursor += itemHashCount * UnsafeUtility.SizeOf<ulong>();
            byte* saveRecordPtr = AddByteOffset(rawPtr, cursor);

            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldSaveRecord16 saveRecord = UnsafeUtility.ReadArrayElement<PersistentWorldSaveRecord16>(saveRecordPtr, i);
                bool isDeleted = (((PersistentWorldItemFlags)saveRecord.ItemFlags) & PersistentWorldItemFlags.Deleted) != 0;
                if (saveRecord.ChunkIndex >= chunkCount)
                {
                    error = "Persistent-world delta section lookup index is out of range.";
                    persistentWorldDeltas = null;
                    return false;
                }

                ulong itemHash = 0UL;
                if (!isDeleted)
                {
                    if (saveRecord.ItemHashIndex >= itemHashCount)
                    {
                        error = "Persistent-world delta item-hash lookup index is out of range.";
                        persistentWorldDeltas = null;
                        return false;
                    }

                    itemHash = UnsafeUtility.ReadArrayElement<ulong>(itemHashTablePtr, saveRecord.ItemHashIndex);
                }

                persistentWorldDeltas[i] = new PersistentWorldDeltaRecord
                {
                    ChunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex),
                    ItemPersistentIdHash = itemHash,
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = isDeleted ? (ushort)1 : (saveRecord.Quantity == 0 ? (ushort)1 : saveRecord.Quantity),
                    ItemFlags = saveRecord.ItemFlags,
                    Reserved = saveRecord.Reserved
                };
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolvePersistentWorldSectionLength(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out int entitySectionOffset,
            out int entitySectionLength,
            out string error)
        {
            entitySectionOffset = checked((int)header.EntityOffset) - ResolvePayloadBaseOffset(in header);
            entitySectionLength = 0;
            error = string.Empty;

            if (entitySectionOffset < 0 || entitySectionOffset > rawPayloadLength)
            {
                error = "Entity payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (header.Version < CompactPersistentWorldSectionVersion)
            {
                int entityCount = checked((int)header.EntityCount);
                entitySectionLength = entityCount > 0
                    ? checked(entityCount * UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>())
                    : 0;
                if (entitySectionOffset + entitySectionLength > rawPayloadLength)
                {
                    error = "Entity payload exceeds the decompressed payload bounds.";
                    return false;
                }

                return true;
            }

            if (header.EntityCount <= 0)
                return true;

            if (entitySectionOffset + PersistentWorldSectionHeaderSize > rawPayloadLength)
            {
                error = "Persistent-world delta section header is truncated.";
                return false;
            }

            PersistentWorldSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PersistentWorldSectionHeader>(AddByteOffset(rawPtr, entitySectionOffset), 0);
            if (sectionHeader.RecordCount != header.EntityCount)
            {
                error = "Persistent-world delta section header count mismatch.";
                return false;
            }

            int chunkCount = checked((int)sectionHeader.ChunkCount);
            int itemHashCount = checked((int)sectionHeader.ItemHashCount);
            int recordCount = checked((int)sectionHeader.RecordCount);
            entitySectionLength = ComputePersistentWorldSectionLength(recordCount, chunkCount, itemHashCount);
            if (entitySectionOffset + entitySectionLength > rawPayloadLength)
            {
                error = "Persistent-world delta section exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static void WriteEcosystemSection(
            byte* destination,
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorStates)
        {
            int recordCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
            EcosystemSectionHeader sectionHeader = new EcosystemSectionHeader
            {
                RecordCount = (uint)recordCount
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            if (recordCount <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ecosystemSectorStates);
            int recordBytes = recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
            int sectionLength = ComputeEcosystemSectionLength(recordCount);
            if (!UnsafeMemoryCopyGuard.SafeCopy(
                    AddByteOffset(destination, EcosystemSectionHeaderSize),
                    sectionLength - EcosystemSectionHeaderSize,
                    sourcePtr,
                    recordBytes))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
            }
        }

        private static bool TryReadEcosystemSectorStates(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out string error)
        {
            ecosystemSectorStates = null;
            error = string.Empty;

            if (header.Version < EcosystemSectionVersion)
                return true;

            if (!TryResolvePersistentWorldSectionLength(rawPtr, rawPayloadLength, header, out int entitySectionOffset, out int entitySectionLength, out error))
                return false;

            if (!TryResolveEcosystemSectionLength(
                    rawPtr,
                    rawPayloadLength,
                    header,
                    entitySectionOffset,
                    entitySectionLength,
                    out int ecosystemSectionOffset,
                    out int ecosystemSectionLength,
                    out error))
            {
                return false;
            }

            EcosystemSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, ecosystemSectionOffset), 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            ecosystemSectorStates = new EcosystemSectorSaveRecord[recordCount];
            if (recordCount <= 0)
                return true;

            fixed (EcosystemSectorSaveRecord* destinationPtr = ecosystemSectorStates)
            {
                int recordBytes = recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(
                        destinationPtr,
                        ecosystemSectorStates.Length * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>(),
                        AddByteOffset(rawPtr, ecosystemSectionOffset + EcosystemSectionHeaderSize),
                        recordBytes))
                {
                    error = "Ecosystem sector-state copy exceeded destination bounds.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveEcosystemSectionLength(
            byte* rawPtr,
            int rawPayloadLength,
            SaveFileHeader header,
            int entitySectionOffset,
            int entitySectionLength,
            out int ecosystemSectionOffset,
            out int ecosystemSectionLength,
            out string error)
        {
            ecosystemSectionOffset = entitySectionOffset + entitySectionLength;
            ecosystemSectionLength = 0;
            error = string.Empty;

            if (header.Version < EcosystemSectionVersion)
                return true;

            if (ecosystemSectionOffset < 0 || ecosystemSectionOffset > rawPayloadLength)
            {
                error = "Ecosystem payload offset exceeds the decompressed payload bounds.";
                return false;
            }

            if (ecosystemSectionOffset + EcosystemSectionHeaderSize > rawPayloadLength)
            {
                error = "Ecosystem section header is truncated.";
                return false;
            }

            EcosystemSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<EcosystemSectionHeader>(AddByteOffset(rawPtr, ecosystemSectionOffset), 0);
            int recordCount = checked((int)sectionHeader.RecordCount);
            ecosystemSectionLength = ComputeEcosystemSectionLength(recordCount);
            if (ecosystemSectionOffset + ecosystemSectionLength > rawPayloadLength)
            {
                error = "Ecosystem section exceeds the decompressed payload bounds.";
                return false;
            }

            return true;
        }

        private static bool TryCompressPayload(
            byte* rawPtr,
            int rawPayloadLength,
            byte* compressedPtr,
            int compressedCapacity,
            out int compressedPayloadLength,
            out uint blockFlags,
            out string error)
        {
            blockFlags = 0u;
            compressedPayloadLength = 0;
            error = string.Empty;

            if (rawPtr == null || compressedPtr == null || rawPayloadLength <= 0 || compressedCapacity <= 0)
            {
                error = "Compression input is invalid.";
                return false;
            }

            int compressedLength = Lz4BlockCompress(rawPtr, rawPayloadLength, compressedPtr, compressedCapacity);
            if (compressedLength <= 0)
            {
                error = "LZ4 block compression failed.";
                return false;
            }

            compressedPayloadLength = compressedLength;
            return true;
        }

        private static bool TryWriteIndexedCompressedBlock(
            byte* rawPtr,
            int rawPayloadLength,
            byte* destinationFilePtr,
            int destinationCapacity,
            ref int fileCursor,
            out int storedBlockLength,
            out uint blockFlags,
            out string error)
        {
            storedBlockLength = 0;
            blockFlags = 0u;
            error = string.Empty;

            if (rawPtr == null || destinationFilePtr == null || rawPayloadLength <= 0)
            {
                error = "Indexed block compression input is invalid.";
                return false;
            }

            int blockHeaderOffset = fileCursor;
            int payloadDestinationOffset = blockHeaderOffset + IndexedSectorBlockHeaderSize;
            if (payloadDestinationOffset >= destinationCapacity)
            {
                error = "Indexed block header exceeded the destination capacity.";
                return false;
            }

            byte* payloadDestinationPtr = destinationFilePtr + payloadDestinationOffset;
            int remainingCapacity = destinationCapacity - payloadDestinationOffset;
            if (!TryCompressPayload(
                    rawPtr,
                    rawPayloadLength,
                    payloadDestinationPtr,
                    remainingCapacity,
                    out int compressedPayloadLength,
                    out blockFlags,
                    out error))
            {
                return false;
            }

            IndexedSectorBlockHeader blockHeader = new IndexedSectorBlockHeader
            {
                Flags = blockFlags,
                Reserved = 0u
            };

            UnsafeUtility.CopyStructureToPtr(ref blockHeader, destinationFilePtr + blockHeaderOffset);
            storedBlockLength = IndexedSectorBlockHeaderSize + compressedPayloadLength;
            fileCursor += storedBlockLength;
            return true;
        }

        private static bool TryTokenizePayload(
            byte* rawPtr,
            int rawPayloadLength,
            byte* tokenizedDestinationPtr,
            int tokenizedDestinationCapacity,
            out int tokenizedPayloadLength,
            out string error)
        {
            tokenizedPayloadLength = 0;
            error = string.Empty;

            if (rawPtr == null || tokenizedDestinationPtr == null || rawPayloadLength <= TokenBlockSizeBytes)
            {
                error = "Token substitution input is invalid.";
                return false;
            }

            if (tokenizedDestinationCapacity <= TokenizedPayloadHeaderSize + TokenBlockSizeBytes)
            {
                error = "Token substitution buffer is too small.";
                return false;
            }

            List<TokenStats> selectedTokens = BuildTokenTable(rawPtr, rawPayloadLength);
            if (selectedTokens == null || selectedTokens.Count == 0)
            {
                error = "No profitable token table candidates were found.";
                return false;
            }

            int tokenTableBytes = selectedTokens.Count * TokenBlockSizeBytes;
            int writeOffset = TokenizedPayloadHeaderSize + tokenTableBytes;
            if (writeOffset >= tokenizedDestinationCapacity)
            {
                error = "Token substitution header exceeded the destination capacity.";
                return false;
            }

            Dictionary<TokenKey, byte> tokenIndexLookup = new Dictionary<TokenKey, byte>(selectedTokens.Count);
            for (int i = 0; i < selectedTokens.Count; i++)
            {
                selectedTokens[i].Index = i;
                tokenIndexLookup[selectedTokens[i].Key] = (byte)i;
                WriteTokenKey(tokenizedDestinationPtr + TokenizedPayloadHeaderSize + (i * TokenBlockSizeBytes), selectedTokens[i].Key);
            }

            int readOffset = 0;
            while (readOffset < rawPayloadLength)
            {
                if (readOffset + TokenBlockSizeBytes <= rawPayloadLength)
                {
                    TokenKey currentKey = ReadTokenKey(rawPtr + readOffset);
                    if (tokenIndexLookup.TryGetValue(currentKey, out byte tokenIndex))
                    {
                        if (writeOffset + 2 > tokenizedDestinationCapacity)
                        {
                            error = "Token substitution output exceeded the destination capacity.";
                            return false;
                        }

                        tokenizedDestinationPtr[writeOffset++] = tokenIndex;
                        tokenizedDestinationPtr[writeOffset++] = TokenEscapeMarker;
                        readOffset += TokenBlockSizeBytes;
                        continue;
                    }
                }

                byte value = rawPtr[readOffset++];
                if (value == TokenEscapeMarker)
                {
                    if (writeOffset + 2 > tokenizedDestinationCapacity)
                    {
                        error = "Token substitution output exceeded the destination capacity.";
                        return false;
                    }

                    tokenizedDestinationPtr[writeOffset++] = TokenEscapeMarker;
                    tokenizedDestinationPtr[writeOffset++] = TokenEscapeMarker;
                    continue;
                }

                if (writeOffset + 1 > tokenizedDestinationCapacity)
                {
                    error = "Token substitution output exceeded the destination capacity.";
                    return false;
                }

                tokenizedDestinationPtr[writeOffset++] = value;
            }

            if (writeOffset >= rawPayloadLength)
            {
                error = "Token substitution did not reduce the payload size.";
                return false;
            }

            TokenizedPayloadHeader header = new TokenizedPayloadHeader
            {
                ExpandedPayloadLength = (uint)rawPayloadLength,
                TokenCount = (ushort)selectedTokens.Count,
                Reserved = 0
            };
            UnsafeUtility.CopyStructureToPtr(ref header, tokenizedDestinationPtr);
            tokenizedPayloadLength = writeOffset;
            return true;
        }

        private static List<TokenStats> BuildTokenTable(byte* rawPtr, int rawPayloadLength)
        {
            int tokenCandidateCapacity = math.max(16, math.min(MaxTokenCount * 4, rawPayloadLength / TokenBlockSizeBytes));
            Dictionary<TokenKey, TokenStats> statsByKey = new Dictionary<TokenKey, TokenStats>(tokenCandidateCapacity);
            List<TokenStats> selectedTokens = new List<TokenStats>(MaxTokenCount);

            for (int offset = 0; offset + TokenBlockSizeBytes <= rawPayloadLength; offset += TokenBlockSizeBytes)
            {
                TokenKey key = ReadTokenKey(rawPtr + offset);
                if (key.A == 0UL && key.B == 0UL)
                    continue;

                if (!statsByKey.TryGetValue(key, out TokenStats stats))
                {
                    stats = new TokenStats
                    {
                        Key = key,
                        Count = 0
                    };
                    statsByKey.Add(key, stats);
                }

                stats.Count++;
            }

            if (statsByKey.Count == 0)
                return null;

            List<TokenStats> candidates = new List<TokenStats>(statsByKey.Values);
            candidates.Sort(static (a, b) =>
            {
                int countCompare = b.Count.CompareTo(a.Count);
                if (countCompare != 0)
                    return countCompare;

                int aCompare = a.Key.A.CompareTo(b.Key.A);
                if (aCompare != 0)
                    return aCompare;

                return a.Key.B.CompareTo(b.Key.B);
            });

            for (int i = 0; i < candidates.Count && selectedTokens.Count < MaxTokenCount; i++)
            {
                TokenStats candidate = candidates[i];
                int grossSavings = candidate.Count * (TokenBlockSizeBytes - 2);
                int netSavings = grossSavings - TokenBlockSizeBytes;
                if (candidate.Count < 2 || netSavings <= 0)
                    continue;

                selectedTokens.Add(candidate);
            }

            return selectedTokens;
        }

        private static bool TryExpandTokenizedPayloadInPlace(
            byte* payloadPtr,
            int tokenizedPayloadLength,
            int payloadCapacity,
            out int expandedPayloadLength,
            out string error)
        {
            expandedPayloadLength = 0;
            error = string.Empty;

            if (payloadPtr == null || tokenizedPayloadLength < TokenizedPayloadHeaderSize)
            {
                error = "Tokenized payload header is truncated.";
                return false;
            }

            TokenizedPayloadHeader header = UnsafeUtility.ReadArrayElement<TokenizedPayloadHeader>(payloadPtr, 0);
            int tokenCount = header.TokenCount;
            expandedPayloadLength = checked((int)header.ExpandedPayloadLength);
            if (tokenCount <= 0 || tokenCount > MaxTokenCount)
            {
                error = "Tokenized payload declared an invalid token count.";
                return false;
            }

            if (expandedPayloadLength <= 0 || expandedPayloadLength > payloadCapacity)
            {
                error = "Tokenized payload declared an invalid expanded length.";
                return false;
            }

            int tokenTableBytes = tokenCount * TokenBlockSizeBytes;
            int encodedStreamOffset = TokenizedPayloadHeaderSize + tokenTableBytes;
            if (encodedStreamOffset > tokenizedPayloadLength)
            {
                error = "Tokenized payload token table exceeds the decompressed bounds.";
                return false;
            }

            int readOffset = tokenizedPayloadLength - 1;
            int writeOffset = expandedPayloadLength - 1;
            while (readOffset >= encodedStreamOffset)
            {
                byte tail = payloadPtr[readOffset--];
                if (tail != TokenEscapeMarker)
                {
                    payloadPtr[writeOffset--] = tail;
                    continue;
                }

                if (readOffset < encodedStreamOffset)
                {
                    error = "Tokenized payload ended with an incomplete escape marker.";
                    return false;
                }

                byte prefix = payloadPtr[readOffset--];
                if (prefix == TokenEscapeMarker)
                {
                    payloadPtr[writeOffset--] = TokenEscapeMarker;
                    continue;
                }

                if (prefix >= tokenCount)
                {
                    error = "Tokenized payload referenced an invalid token index.";
                    return false;
                }

                if (writeOffset + 1 < TokenBlockSizeBytes)
                {
                    error = "Tokenized payload expanded beyond the destination bounds.";
                    return false;
                }

                byte* tokenSourcePtr = payloadPtr + TokenizedPayloadHeaderSize + (prefix * TokenBlockSizeBytes);
                for (int i = TokenBlockSizeBytes - 1; i >= 0; i--)
                    payloadPtr[writeOffset--] = tokenSourcePtr[i];
            }

            if (writeOffset != -1)
            {
                error = "Tokenized payload expansion did not fully reconstruct the logical payload.";
                return false;
            }

            return true;
        }

        private static TokenKey ReadTokenKey(byte* sourcePtr)
        {
            ulong low = UnsafeUtility.ReadArrayElement<ulong>(sourcePtr, 0);
            ulong high = UnsafeUtility.ReadArrayElement<ulong>(sourcePtr + sizeof(ulong), 0);
            return new TokenKey(low, high);
        }

        private static void WriteTokenKey(byte* destinationPtr, TokenKey key)
        {
            UnsafeUtility.WriteArrayElement(destinationPtr, 0, key.A);
            UnsafeUtility.WriteArrayElement(destinationPtr + sizeof(ulong), 0, key.B);
        }

        private static bool TryValidateHeader(SaveFileHeader header, out string error)
        {
            if (header.Version < MinimumSupportedVersion || header.Version > CurrentVersion)
            {
                error = $"Unsupported save header version {header.Version}.";
                return false;
            }

            if (header.CompatMask != CurrentCompatMask)
            {
                error = $"Unsupported save compatibility mask 0x{header.CompatMask:X2}.";
                return false;
            }

            if ((header.Flags & FlagLz4Blocks) == 0)
            {
                error = "Save payload is not flagged as LZ4 block-compressed.";
                return false;
            }

            int expectedHeaderSize = ResolveExpectedHeaderSize(header.Version);
            if (expectedHeaderSize <= 0)
            {
                error = $"Unsupported save header version {header.Version}.";
                return false;
            }

            if (header.Version < IndexedBlockStorageVersion && header.PlayerOffset != expectedHeaderSize)
            {
                error = $"Player payload offset {header.PlayerOffset} does not match fixed header size {expectedHeaderSize}.";
                return false;
            }

            if (header.Version >= IndexedBlockStorageVersion)
            {
                if (header.PlayerOffset < expectedHeaderSize)
                {
                    error = "Indexed metadata block offset is smaller than the fixed header.";
                    return false;
                }

                if (header.DeltaOffset < header.PlayerOffset)
                {
                    error = "Indexed packed quest-state offset precedes the metadata block.";
                    return false;
                }
            }
            else if (header.DeltaOffset < header.PlayerOffset || header.EntityOffset < header.DeltaOffset)
            {
                error = "Save payload offsets are out of order.";
                return false;
            }

            if (header.DeltaOffset > header.PlayerOffset + RawPayloadCapacityBytes)
            {
                error = "Save packed quest-state offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (header.EntityOffset > header.PlayerOffset + RawPayloadCapacityBytes)
            {
                error = "Save entity payload offset exceeds the raw payload decoder budget.";
                return false;
            }

            if (header.DeltaCount > (uint)(RawPayloadCapacityBytes / UnsafeUtility.SizeOf<uint>()))
            {
                error = "Save packed quest-state count exceeds the decoder budget.";
                return false;
            }

            int maxEntityRecordSize = header.Version >= CompactPersistentWorldSectionVersion
                ? UnsafeUtility.SizeOf<PersistentWorldSaveRecord16>()
                : UnsafeUtility.SizeOf<PersistentWorldDeltaRecord>();
            if (header.EntityCount > (uint)(RawPayloadCapacityBytes / maxEntityRecordSize))
            {
                error = "Save entity count exceeds the decoder budget.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static uint ComputePackedQuestStateChecksum(NativeArray<uint> packedQuestStateWords)
        {
            if (!packedQuestStateWords.IsCreated || packedQuestStateWords.Length <= 0)
                return 0u;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateWords);
            return Hash32(sourcePtr, (long)packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>());
        }

        private static uint ComputePackedQuestStateChecksum(uint[] packedQuestStateWords)
        {
            if (packedQuestStateWords == null || packedQuestStateWords.Length <= 0)
                return 0u;

            fixed (uint* sourcePtr = packedQuestStateWords)
            {
                return Hash32(sourcePtr, (long)packedQuestStateWords.Length * UnsafeUtility.SizeOf<uint>());
            }
        }

        private static SaveFileHeader ConvertLegacyHeader(in LegacySaveFileHeader legacyHeader)
        {
            return new SaveFileHeader
            {
                MagicValue = legacyHeader.MagicValue,
                Version = legacyHeader.Version,
                CompatMask = legacyHeader.CompatMask,
                Flags = legacyHeader.Flags,
                TimestampUnixMs = legacyHeader.TimestampUnixMs,
                DeltaCount = legacyHeader.DeltaCount,
                EntityCount = legacyHeader.EntityCount,
                PlayerOffset = legacyHeader.PlayerOffset,
                DeltaOffset = legacyHeader.DeltaOffset,
                EntityOffset = legacyHeader.EntityOffset,
                HashPayload64 = legacyHeader.HashPayload32,
                HashHeader64 = legacyHeader.HashHeader32
            };
        }

        private static int ResolveHeaderSize(ushort version)
        {
            switch (version)
            {
                case 0x0003:
                    return LegacyHeaderSize;
                case First64BitHashVersion:
                case CompactPersistentWorldSectionVersion:
                case EcosystemSectionVersion:
                case TokenizedPayloadVersion:
                case CurrentVersion:
                    return CurrentHeaderSize;
                default:
                    return 0;
            }
        }

        private static int ResolveExpectedHeaderSize(ushort version)
        {
            return ResolveHeaderSize(version);
        }

        private static int ResolvePayloadBaseOffset(in SaveFileHeader header)
        {
            return checked((int)header.PlayerOffset);
        }

        private static string FormatPayloadChecksum(in SaveFileHeader header)
        {
            return header.Version >= First64BitHashVersion
                ? header.HashPayload64.ToString("X16")
                : ((uint)header.HashPayload64).ToString("X8");
        }

        private static ulong ToUnixMilliseconds(long utcTicks)
        {
            long safeTicks = utcTicks > 0L ? utcTicks : DateTime.UtcNow.Ticks;
            return (ulong)((safeTicks - UnixEpochTicks) / TimeSpan.TicksPerMillisecond);
        }

        private static long ToUtcTicks(ulong unixMilliseconds)
        {
            return UnixEpochTicks + ((long)unixMilliseconds * TimeSpan.TicksPerMillisecond);
        }

        private static AbsoluteUniversePosition ToAup(Vector3 runtimePosition)
        {
            return AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
        }

        private static Vector3 ToRuntimePosition(AbsoluteUniversePosition position)
        {
            float3 runtimePosition = position.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static bool TryReadUtf16String(
            byte* source,
            int sourceLength,
            ref int cursor,
            int byteLength,
            out string value,
            out string error)
        {
            value = string.Empty;
            error = string.Empty;

            if (!IsByteRangeWithin(cursor, byteLength, sourceLength))
            {
                error = "UTF-16 metadata block exceeds the payload bounds.";
                return false;
            }

            if (byteLength == 0)
                return true;

            if ((byteLength & 1) != 0)
            {
                error = "UTF-16 metadata block has an odd byte length.";
                return false;
            }

            int charCount = byteLength / sizeof(char);
            value = string.Intern(new string((char*)AddByteOffset(source, cursor), 0, charCount));
            cursor += byteLength;
            return true;
        }

        private static void CopyUtf16StringToUnmanaged(string source, byte* destination, int destinationCapacityBytes)
        {
            if (string.IsNullOrEmpty(source))
                return;

            fixed (char* sourcePtr = source)
            {
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, destinationCapacityBytes, sourcePtr, source.Length * sizeof(char)))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));
            }
        }

        private static int Lz4BlockDecompress(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity)
        {
            int failedBlockIndex = -1;
            return Lz4BlockDecompress(
                source,
                compressedLength,
                destination,
                destinationCapacity,
                out failedBlockIndex);
        }

        private static int Lz4BlockDecompress(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity,
            out int failedBlockIndex)
        {
            failedBlockIndex = -1;
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
                return 0;

            return Lz4BlockDecompressStandard(
                source,
                compressedLength,
                destination,
                destinationCapacity,
                out failedBlockIndex);
        }

        private static int Lz4BlockDecompressStandard(
            byte* source,
            int compressedLength,
            byte* destination,
            int destinationCapacity,
            out int failedBlockIndex)
        {
            failedBlockIndex = -1;
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
                return 0;

            int blockHeaderBytes = StandardCompressedBlockHeaderBytes;
            int minimumCompressedBlockBytes = blockHeaderBytes + 1;
            if (compressedLength < minimumCompressedBlockBytes)
                return 0;

            int sourceOffset = 0;
            int destinationOffset = 0;
            int blockIterations = 0;
            int maxBlocksFromSource = compressedLength / minimumCompressedBlockBytes;
            int maxBlocksFromDestination = (destinationCapacity + BlockSizeBytes - 1) / BlockSizeBytes;
            int maxBlockIterations = math.min(maxBlocksFromSource, maxBlocksFromDestination);
            if (maxBlockIterations <= 0)
                return 0;

            while (sourceOffset < compressedLength && blockIterations < maxBlockIterations)
            {
                blockIterations++;
                if (sourceOffset + blockHeaderBytes > compressedLength)
                    return 0;

                int blockCompressedLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset, 0);
                int blockRawLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset + 4, 0);
                if (blockCompressedLength <= 0 || blockRawLength <= 0)
                    return 0;

                if (blockRawLength > BlockSizeBytes)
                    return 0;

                sourceOffset += blockHeaderBytes;
                if (sourceOffset + blockCompressedLength > compressedLength)
                    return 0;

                if (destinationOffset + blockRawLength > destinationCapacity)
                    return 0;

                int actualLength = LZ4Decompress(source + sourceOffset, destination + destinationOffset, blockCompressedLength, blockRawLength);
                if (actualLength != blockRawLength)
                    return 0;

                sourceOffset += blockCompressedLength;
                destinationOffset += blockRawLength;
            }

            if (sourceOffset != compressedLength)
                return 0;

            return destinationOffset;
        }

        [DllImport(Lz4DllName, EntryPoint = "LZ4_compress_default")]
        private static extern int LZ4Compress(byte* source, byte* destination, int sourceLength, int destinationCapacity);

        [DllImport(Lz4DllName, EntryPoint = "LZ4_decompress_safe")]
        private static extern int LZ4Decompress(byte* source, byte* destination, int compressedLength, int destinationCapacity);
    }
}
