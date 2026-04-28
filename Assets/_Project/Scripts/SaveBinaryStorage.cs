using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
                    UnsafeUtility.MemCpy(destination, firstBuffer, firstByteCount);

                if (secondBuffer != null && secondByteCount > 0)
                    UnsafeUtility.MemCpy(destination + math.max(firstByteCount, 0), secondBuffer, secondByteCount);

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
                UnsafeUtility.MemCpy(buffer, mappedPointer + accessor.PointerOffset, byteCount);
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
        internal const ushort CurrentVersion = 0x0006;
        internal const ushort MinimumSupportedVersion = 0x0003;
        internal const byte CurrentCompatMask = 0x0B;
        internal const byte FlagLz4Blocks = 0x01;
        internal const int CurrentHeaderSize = 52;
        internal const int LegacyHeaderSize = 44;
        internal const int BlockSizeBytes = 256 * 1024;
        internal const int RawPayloadCapacityBytes = 64 * 1024 * 1024;
        internal const int MaxCompressedPayloadBytes = 67378176;

        private const long UnixEpochTicks = 621355968000000000L;
        private const int PayloadPrefixSizeBytes = 60;
        private const int PackedQuestStateSectionHeaderSize = 8;
        private const int PersistentWorldSectionHeaderSize = 12;
        private const int EcosystemSectionHeaderSize = 4;
        private const int SaveFileHeaderPrefixSize = 8;
        private const int LegacyHeaderHashSizeBytes = 36;
        private const int CurrentHeaderHashSizeBytes = 44;
        private const ushort First64BitHashVersion = 0x0004;
        private const ushort CompactPersistentWorldSectionVersion = 0x0005;
        private const ushort EcosystemSectionVersion = 0x0006;
        private const string Lz4DllName = "liblz4";

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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PayloadPrefixSizeBytes)]
        private struct PayloadPrefix
        {
            public ulong TimestampUnixMs;
            public float PlayTimeSeconds;
            public AbsoluteUniversePosition PlayerPosition;
            public int SaveDataVersion;
            public uint SaveDataByteLength;
            public ushort SceneNameByteLength;
            public ushort GameVersionByteLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PackedQuestStateSectionHeaderSize)]
        private struct PackedQuestStateSectionHeader
        {
            public uint WordCount;
            public uint Checksum;
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
            NativeArray<uint> packedQuestStateWords,
            NativeArray<byte> voxelDeltaSnapshot,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out string error)
        {
            error = string.Empty;
            NativeParallelHashMap<int3, ushort> persistentWorldChunkLookup = default;
            NativeList<int3> persistentWorldChunkTable = default;
            NativeParallelHashMap<ulong, ushort> persistentWorldItemHashLookup = default;
            NativeList<ulong> persistentWorldItemHashTable = default;

            try
            {
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

                int entityCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
                if (entityCount > 0 &&
                    !TryBuildPersistentWorldSectionTables(
                        persistentWorldDeltas,
                        out persistentWorldChunkLookup,
                        out persistentWorldChunkTable,
                        out persistentWorldItemHashLookup,
                        out persistentWorldItemHashTable,
                        out error))
                {
                    return false;
                }

                int packedQuestWordCount = packedQuestStateWords.IsCreated ? packedQuestStateWords.Length : 0;
                int ecosystemSectorCount = ecosystemSectorStates.IsCreated ? ecosystemSectorStates.Length : 0;
                int voxelDeltaByteLength = voxelDeltaSnapshot.IsCreated ? voxelDeltaSnapshot.Length : 0;
                int packedQuestSectionLength = packedQuestWordCount > 0
                    ? PackedQuestStateSectionHeaderSize + (packedQuestWordCount * UnsafeUtility.SizeOf<uint>())
                    : 0;
                int persistentWorldSectionLength = entityCount > 0
                    ? ComputePersistentWorldSectionLength(entityCount, persistentWorldChunkTable.Length, persistentWorldItemHashTable.Length)
                    : 0;
                int ecosystemSectionLength = ComputeEcosystemSectionLength(ecosystemSectorCount);

                byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
                byte* compressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);
                UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);

                int payloadCursor = PayloadPrefixSizeBytes + sceneBytesLength + versionBytesLength;
                if (!SaveBinaryPayloadCodec.TryWrite(data, AddByteOffset(rawPtr, payloadCursor), rawBuffer.Length - payloadCursor, out int saveDataByteLength, out error))
                    return false;

                int rawPayloadLength = payloadCursor + saveDataByteLength + packedQuestSectionLength + persistentWorldSectionLength + ecosystemSectionLength + voxelDeltaByteLength;
                if (rawPayloadLength > rawBuffer.Length)
                {
                    error = $"Save payload ({rawPayloadLength} bytes) exceeded the {rawBuffer.Length} byte raw buffer ceiling.";
                    return false;
                }

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
                payloadCursor = PayloadPrefixSizeBytes;
                CopyUtf16StringToUnmanaged(sceneName, AddByteOffset(rawPtr, payloadCursor));
                payloadCursor += sceneBytesLength;
                CopyUtf16StringToUnmanaged(gameVersion, AddByteOffset(rawPtr, payloadCursor));
                payloadCursor += versionBytesLength;
                payloadCursor += saveDataByteLength;
                int packedQuestOffsetInPayload = payloadCursor;

                if (packedQuestWordCount > 0)
                {
                    PackedQuestStateSectionHeader packedQuestHeader = new PackedQuestStateSectionHeader
                    {
                        WordCount = (uint)packedQuestWordCount,
                        Checksum = ComputePackedQuestStateChecksum(packedQuestStateWords)
                    };

                    UnsafeUtility.CopyStructureToPtr(ref packedQuestHeader, AddByteOffset(rawPtr, payloadCursor));
                    payloadCursor += PackedQuestStateSectionHeaderSize;

                    void* packedQuestSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateWords);
                    UnsafeUtility.MemCpy(AddByteOffset(rawPtr, payloadCursor), packedQuestSourcePtr, packedQuestWordCount * UnsafeUtility.SizeOf<uint>());
                    payloadCursor += packedQuestWordCount * UnsafeUtility.SizeOf<uint>();
                }

                int entityOffsetInPayload = payloadCursor;
                if (entityCount > 0)
                {
                    WritePersistentWorldSection(
                        AddByteOffset(rawPtr, payloadCursor),
                        persistentWorldDeltas,
                        persistentWorldChunkLookup,
                        persistentWorldChunkTable,
                        persistentWorldItemHashLookup,
                        persistentWorldItemHashTable);

                    payloadCursor += persistentWorldSectionLength;
                }

                WriteEcosystemSection(AddByteOffset(rawPtr, payloadCursor), ecosystemSectorStates);
                payloadCursor += ecosystemSectionLength;

                if (voxelDeltaByteLength > 0)
                {
                    void* voxelSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(voxelDeltaSnapshot);
                    UnsafeUtility.MemCpy(AddByteOffset(rawPtr, payloadCursor), voxelSourcePtr, voxelDeltaByteLength);
                    payloadCursor += voxelDeltaByteLength;
                }

                ulong payloadHash = Hash64(rawPtr, rawPayloadLength);

                SaveFileHeader header = new SaveFileHeader
                {
                    MagicValue = Magic,
                    Version = CurrentVersion,
                    CompatMask = CurrentCompatMask,
                    Flags = FlagLz4Blocks,
                    TimestampUnixMs = timestampUnixMs,
                    DeltaCount = (uint)packedQuestWordCount,
                    EntityCount = (uint)entityCount,
                    PlayerOffset = CurrentHeaderSize,
                    DeltaOffset = (uint)(CurrentHeaderSize + packedQuestOffsetInPayload),
                    EntityOffset = (uint)(CurrentHeaderSize + entityOffsetInPayload),
                    HashPayload64 = payloadHash,
                    HashHeader64 = 0UL
                };

                header.HashHeader64 = ComputeHeaderHash(ref header);

                if (!TryCompressPayload(rawPtr, rawPayloadLength, compressedPtr, compressedBuffer.Length, out int compressedPayloadLength, out error))
                    return false;

                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                byte* headerPtr = stackalloc byte[CurrentHeaderSize];
                UnsafeUtility.MemClear(headerPtr, CurrentHeaderSize);
                UnsafeUtility.CopyStructureToPtr(ref header, headerPtr);

                if (!AsyncWriteManager.WriteAll(
                        absolutePath,
                        headerPtr,
                        CurrentHeaderSize,
                        compressedPtr,
                        compressedPayloadLength,
                        out error))
                {
                    return false;
                }

                if (!TryReadMetadata(absolutePath, metadata.SlotName, rawBuffer, out _, out _, out string verifyError))
                {
                    error = $"Write-verify failed: {verifyError}";
                    return false;
                }

                metadata.Checksum = payloadHash.ToString("X16");
                return true;
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
            }
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

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefix prefix, out byte* rawPtr, out int rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = PayloadPrefixSizeBytes;
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

        internal static bool TryLoadSaveData(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            out SaveData data,
            out uint[] packedQuestStateWords,
            out PersistentWorldDeltaRecord[] persistentWorldDeltas,
            out EcosystemSectorSaveRecord[] ecosystemSectorStates,
            out NativeArray<byte> voxelDeltaSnapshot,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            data = null;
            packedQuestStateWords = null;
            persistentWorldDeltas = null;
            ecosystemSectorStates = null;
            voxelDeltaSnapshot = default;
            metadata = null;
            detectedVersion = 0;

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefix prefix, out byte* rawPtr, out int rawPayloadLength, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = PayloadPrefixSizeBytes;
            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf16String(rawPtr, rawPayloadLength, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
            if (saveDataLength < 0 || cursor + saveDataLength > rawPayloadLength)
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

        internal static int Lz4BlockCompress(byte* source, int sourceLength, byte* destination, int destinationCapacity)
        {
            if (source == null || destination == null || sourceLength <= 0 || destinationCapacity <= 8)
                return 0;

            int blockCount = (sourceLength + BlockSizeBytes - 1) / BlockSizeBytes;
            int sourceOffset = 0;
            int destinationOffset = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                int rawBlockLength = math.min(BlockSizeBytes, sourceLength - sourceOffset);
                if (destinationOffset + 8 > destinationCapacity)
                    return 0;

                byte* blockSource = source + sourceOffset;
                byte* blockDestination = destination + destinationOffset + 8;
                int blockDestinationCapacity = destinationCapacity - destinationOffset - 8;
                int blockCompressedLength = LZ4Compress(blockSource, blockDestination, rawBlockLength, blockDestinationCapacity);
                if (blockCompressedLength <= 0)
                    return 0;

                UnsafeUtility.WriteArrayElement(destination + destinationOffset, 0, blockCompressedLength);
                UnsafeUtility.WriteArrayElement(destination + destinationOffset + 4, 0, rawBlockLength);

                sourceOffset += rawBlockLength;
                destinationOffset += 8 + blockCompressedLength;
            }

            return destinationOffset;
        }

        private static bool TryReadPayload(
            string absolutePath,
            NativeArray<byte> rawBuffer,
            out SaveFileHeader header,
            out PayloadPrefix prefix,
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

                rawPayloadLength = Lz4BlockDecompress(AddByteOffset(filePtr, headerSizeBytes), compressedPayloadLength, rawPtr, rawBuffer.Length);
                if (rawPayloadLength <= 0)
                {
                    error = "LZ4 block decompression failed.";
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

                if (rawPayloadLength < PayloadPrefixSizeBytes)
                {
                    error = "Payload prefix is truncated.";
                    return false;
                }

                prefix = UnsafeUtility.ReadArrayElement<PayloadPrefix>(rawPtr, 0);
                int metadataBytes = PayloadPrefixSizeBytes + prefix.SceneNameByteLength + prefix.GameVersionByteLength;
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
            out uint[] packedQuestStateWords,
            out string error)
        {
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

            PackedQuestStateSectionHeader sectionHeader = UnsafeUtility.ReadArrayElement<PackedQuestStateSectionHeader>(AddByteOffset(rawPtr, packedQuestSectionOffset), 0);
            if (sectionHeader.WordCount != header.DeltaCount)
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
                UnsafeUtility.MemCpy(
                    destinationPtr,
                    packedQuestSourcePtr,
                    packedQuestWordCount * UnsafeUtility.SizeOf<uint>());
            }

            uint computedChecksum = ComputePackedQuestStateChecksum(packedQuestStateWords);
            if (computedChecksum != sectionHeader.Checksum)
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
                UnsafeUtility.MemCpy(destinationPtr, entitySourcePtr, entityBytes);
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
            UnsafeUtility.MemCpy(destinationPtr, AddByteOffset(rawPtr, voxelSectionOffset), voxelByteLength);
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
            chunkLookup = new NativeParallelHashMap<int3, ushort>(capacity, Allocator.Persistent);
            chunkTable = new NativeList<int3>(capacity, Allocator.Persistent);
            itemHashLookup = new NativeParallelHashMap<ulong, ushort>(capacity, Allocator.Persistent);
            itemHashTable = new NativeList<ulong>(capacity, Allocator.Persistent);
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
            PersistentWorldSectionHeader sectionHeader = new PersistentWorldSectionHeader
            {
                ChunkCount = (uint)chunkTable.Length,
                ItemHashCount = (uint)itemHashTable.Length,
                RecordCount = (uint)(persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0)
            };

            UnsafeUtility.CopyStructureToPtr(ref sectionHeader, destination);
            int cursor = PersistentWorldSectionHeaderSize;

            if (chunkTable.Length > 0)
            {
                void* chunkSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(chunkTable.AsArray());
                UnsafeUtility.MemCpy(AddByteOffset(destination, cursor), chunkSourcePtr, chunkTable.Length * UnsafeUtility.SizeOf<int3>());
                cursor += chunkTable.Length * UnsafeUtility.SizeOf<int3>();
            }

            if (itemHashTable.Length > 0)
            {
                void* itemSourcePtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(itemHashTable.AsArray());
                UnsafeUtility.MemCpy(AddByteOffset(destination, cursor), itemSourcePtr, itemHashTable.Length * UnsafeUtility.SizeOf<ulong>());
                cursor += itemHashTable.Length * UnsafeUtility.SizeOf<ulong>();
            }

            int recordCount = persistentWorldDeltas.IsCreated ? persistentWorldDeltas.Length : 0;
            for (int i = 0; i < recordCount; i++)
            {
                PersistentWorldDeltaRecord deltaRecord = persistentWorldDeltas[i];
                PersistentWorldSaveRecord16 saveRecord = default;
                if (deltaRecord.IsValid &&
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
                if (saveRecord.ChunkIndex >= chunkCount || saveRecord.ItemHashIndex >= itemHashCount)
                {
                    error = "Persistent-world delta section lookup index is out of range.";
                    persistentWorldDeltas = null;
                    return false;
                }

                persistentWorldDeltas[i] = new PersistentWorldDeltaRecord
                {
                    ChunkId = UnsafeUtility.ReadArrayElement<int3>(chunkTablePtr, saveRecord.ChunkIndex),
                    ItemPersistentIdHash = UnsafeUtility.ReadArrayElement<ulong>(itemHashTablePtr, saveRecord.ItemHashIndex),
                    InstanceUid = saveRecord.InstanceUid,
                    PackedLocalPosition = saveRecord.PackedLocalPosition,
                    Quantity = saveRecord.Quantity == 0 ? (ushort)1 : saveRecord.Quantity,
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
            UnsafeUtility.MemCpy(
                AddByteOffset(destination, EcosystemSectionHeaderSize),
                sourcePtr,
                recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>());
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
                UnsafeUtility.MemCpy(
                    destinationPtr,
                    AddByteOffset(rawPtr, ecosystemSectionOffset + EcosystemSectionHeaderSize),
                    recordCount * UnsafeUtility.SizeOf<EcosystemSectorSaveRecord>());
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
            out string error)
        {
            compressedPayloadLength = Lz4BlockCompress(rawPtr, rawPayloadLength, compressedPtr, compressedCapacity);
            if (compressedPayloadLength > 0)
            {
                error = string.Empty;
                return true;
            }

            error = "LZ4 block compression failed.";
            return false;
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

            if (header.PlayerOffset != expectedHeaderSize)
            {
                error = $"Player payload offset {header.PlayerOffset} does not match fixed header size {expectedHeaderSize}.";
                return false;
            }

            if (header.DeltaOffset < header.PlayerOffset || header.EntityOffset < header.DeltaOffset)
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

            if (byteLength < 0 || cursor < 0 || cursor + byteLength > sourceLength)
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
            value = new string((char*)AddByteOffset(source, cursor), 0, charCount);
            cursor += byteLength;
            return true;
        }

        private static void CopyUtf16StringToUnmanaged(string source, byte* destination)
        {
            if (string.IsNullOrEmpty(source))
                return;

            fixed (char* sourcePtr = source)
            {
                UnsafeUtility.MemCpy(destination, sourcePtr, source.Length * sizeof(char));
            }
        }

        private static int Lz4BlockDecompress(byte* source, int compressedLength, byte* destination, int destinationCapacity)
        {
            if (source == null ||
                destination == null ||
                compressedLength <= 0 ||
                compressedLength > MaxCompressedPayloadBytes ||
                destinationCapacity <= 0 ||
                destinationCapacity > RawPayloadCapacityBytes)
                return 0;

            const int BlockHeaderBytes = 8;
            const int MinimumCompressedBlockBytes = BlockHeaderBytes + 1;
            if (compressedLength < MinimumCompressedBlockBytes)
                return 0;

            int sourceOffset = 0;
            int destinationOffset = 0;
            int blockIterations = 0;
            int maxBlocksFromSource = compressedLength / MinimumCompressedBlockBytes;
            int maxBlocksFromDestination = (destinationCapacity + BlockSizeBytes - 1) / BlockSizeBytes;
            int maxBlockIterations = math.min(maxBlocksFromSource, maxBlocksFromDestination);
            if (maxBlockIterations <= 0)
                return 0;

            while (sourceOffset < compressedLength && blockIterations < maxBlockIterations)
            {
                blockIterations++;
                if (sourceOffset + BlockHeaderBytes > compressedLength)
                    return 0;

                int blockCompressedLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset, 0);
                int blockRawLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset + 4, 0);
                if (blockCompressedLength <= 0 || blockRawLength <= 0)
                    return 0;

                if (blockRawLength > BlockSizeBytes)
                    return 0;

                sourceOffset += BlockHeaderBytes;
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
