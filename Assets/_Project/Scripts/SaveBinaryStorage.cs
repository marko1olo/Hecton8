using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    internal static unsafe class SaveBinaryStorage
    {
        internal const uint Magic = 0x48454354u;
        internal const ushort CurrentVersion = 0x0002;
        internal const ushort MinimumSupportedVersion = 0x0001;
        internal const byte LegacyCompatMaskV1 = 0x05;
        internal const byte CurrentCompatMask = 0x0A;
        internal const byte FlagLz4Blocks = 0x01;
        internal const int HeaderSize = 44;
        internal const int BlockSizeBytes = 256 * 1024;
        internal const int RawPayloadCapacityBytes = 64 * 1024 * 1024;
        internal const int MaxCompressedPayloadBytes = 67378176;

        private const int AupCellSizeMeters = 5000;
        private const long UnixEpochTicks = 621355968000000000L;
        private const int PayloadPrefixSizeBytes = 60;
        private const string Lz4DllName = "liblz4";

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = HeaderSize)]
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
        private struct AupPosition
        {
            public long GridX;
            public long GridY;
            public long GridZ;
            public float LocalX;
            public float LocalY;
            public float LocalZ;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = PayloadPrefixSizeBytes)]
        private struct PayloadPrefix
        {
            public ulong TimestampUnixMs;
            public float PlayTimeSeconds;
            public AupPosition PlayerPosition;
            public int SaveDataVersion;
            public uint SaveDataByteLength;
            public ushort SceneNameByteLength;
            public ushort GameVersionByteLength;
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

            byte[] headerBytes = new byte[sizeof(uint)];
            using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Read(headerBytes, 0, headerBytes.Length) != headerBytes.Length)
                    return false;
            }

            fixed (byte* headerPtr = headerBytes)
            {
                return UnsafeUtility.ReadArrayElement<uint>(headerPtr, 0) == Magic;
            }
        }

        internal static bool TryWriteSaveFile(
            string absolutePath,
            SaveMetadata metadata,
            SaveData data,
            NativeArray<byte> rawBuffer,
            NativeArray<byte> compressedBuffer,
            out string error)
        {
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

            ES3Settings payloadSettings = CreatePayloadSettings();
            byte[] saveBytes = ES3.Serialize(data, payloadSettings);
            if (saveBytes == null || saveBytes.Length <= 0)
            {
                error = "Binary payload serialization produced no data.";
                return false;
            }

            byte[] sceneBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(metadata.SceneName) ? "Unknown" : metadata.SceneName);
            byte[] versionBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(metadata.GameVersion) ? Application.version : metadata.GameVersion);

            if (sceneBytes.Length > ushort.MaxValue || versionBytes.Length > ushort.MaxValue)
            {
                error = "Save metadata strings exceed the payload prefix limits.";
                return false;
            }

            int rawPayloadLength = PayloadPrefixSizeBytes + sceneBytes.Length + versionBytes.Length + saveBytes.Length;
            if (rawPayloadLength > rawBuffer.Length)
            {
                error = $"Save payload ({rawPayloadLength} bytes) exceeded the {rawBuffer.Length} byte raw buffer ceiling.";
                return false;
            }

            byte* rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);
            byte* compressedPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(compressedBuffer);

            ulong timestampUnixMs = ToUnixMilliseconds(metadata.Timestamp);
            PayloadPrefix prefix = new PayloadPrefix
            {
                TimestampUnixMs = timestampUnixMs,
                PlayTimeSeconds = metadata.PlayTimeSeconds,
                PlayerPosition = ToAup(metadata.PlayerPosition),
                SaveDataVersion = Mathf.Max(data.version, 0),
                SaveDataByteLength = (uint)saveBytes.Length,
                SceneNameByteLength = (ushort)sceneBytes.Length,
                GameVersionByteLength = (ushort)versionBytes.Length
            };

            UnsafeUtility.MemClear(rawPtr, rawBuffer.Length);
            UnsafeUtility.CopyStructureToPtr(ref prefix, rawPtr);

            int payloadCursor = PayloadPrefixSizeBytes;
            CopyManagedBytesToUnmanaged(sceneBytes, rawPtr + payloadCursor);
            payloadCursor += sceneBytes.Length;
            CopyManagedBytesToUnmanaged(versionBytes, rawPtr + payloadCursor);
            payloadCursor += versionBytes.Length;
            CopyManagedBytesToUnmanaged(saveBytes, rawPtr + payloadCursor);
            payloadCursor += saveBytes.Length;

            uint payloadHash = Hash32(rawPtr, rawPayloadLength);

            SaveFileHeader header = new SaveFileHeader
            {
                MagicValue = Magic,
                Version = CurrentVersion,
                CompatMask = CurrentCompatMask,
                Flags = FlagLz4Blocks,
                TimestampUnixMs = timestampUnixMs,
                DeltaCount = (uint)Mathf.Max(data.voxelDeltaPersistence.totalCellCount, 0),
                EntityCount = 0u,
                PlayerOffset = HeaderSize,
                DeltaOffset = (uint)(HeaderSize + rawPayloadLength),
                EntityOffset = (uint)(HeaderSize + rawPayloadLength),
                HashHeader32 = 0u,
                HashPayload32 = payloadHash
            };

            header.HashHeader32 = ComputeHeaderHash(ref header);

            int compressedPayloadLength = Lz4BlockCompress(rawPtr, rawPayloadLength, compressedPtr, compressedBuffer.Length);
            if (compressedPayloadLength <= 0)
            {
                error = "LZ4 block compression failed.";
                return false;
            }

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] headerBytes = new byte[HeaderSize];
            fixed (byte* headerPtr = headerBytes)
            {
                UnsafeUtility.CopyStructureToPtr(ref header, headerPtr);
            }

            using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(headerBytes, 0, headerBytes.Length);
                using (UnmanagedMemoryStream payloadStream = new UnmanagedMemoryStream(compressedPtr, compressedPayloadLength))
                {
                    payloadStream.CopyTo(stream);
                }

                stream.Flush();
            }

            if (!TryReadMetadata(absolutePath, metadata.SlotName, rawBuffer, out _, out _, out string verifyError))
            {
                error = $"Write-verify failed: {verifyError}";
                return false;
            }

            metadata.Checksum = payloadHash.ToString("X8");
            return true;
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

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefix prefix, out byte* rawPtr, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = PayloadPrefixSizeBytes;
            if (!TryReadUtf8String(rawPtr, rawBuffer.Length, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf8String(rawPtr, rawBuffer.Length, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
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
                Checksum = header.HashPayload32.ToString("X8")
            };

            error = string.Empty;
            return true;
        }

        internal static bool TryLoadSaveData(
            string absolutePath,
            string slotName,
            NativeArray<byte> rawBuffer,
            out SaveData data,
            out SaveMetadata metadata,
            out int detectedVersion,
            out string error)
        {
            data = null;
            metadata = null;
            detectedVersion = 0;

            if (!TryReadPayload(absolutePath, rawBuffer, out SaveFileHeader header, out PayloadPrefix prefix, out byte* rawPtr, out string readError))
            {
                error = readError;
                return false;
            }

            int cursor = PayloadPrefixSizeBytes;
            if (!TryReadUtf8String(rawPtr, rawBuffer.Length, ref cursor, prefix.SceneNameByteLength, out string sceneName, out error))
                return false;

            if (!TryReadUtf8String(rawPtr, rawBuffer.Length, ref cursor, prefix.GameVersionByteLength, out string gameVersion, out error))
                return false;

            int saveDataLength = checked((int)prefix.SaveDataByteLength);
            if (saveDataLength < 0 || cursor + saveDataLength > rawBuffer.Length)
            {
                error = "Save payload byte range is invalid.";
                return false;
            }

            byte[] saveBytes = new byte[saveDataLength];
            CopyUnmanagedBytesToManaged(rawPtr + cursor, saveBytes);

            ES3Settings payloadSettings = CreatePayloadSettings();
            data = ES3.Deserialize<SaveData>(saveBytes, payloadSettings);
            if (data == null)
            {
                error = "Binary save payload deserialized to null.";
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
                Checksum = header.HashPayload32.ToString("X8")
            };

            error = string.Empty;
            return true;
        }

        internal static uint Hash32(void* ptr, long length)
        {
            return xxHash3.Hash64(ptr, length).x;
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
            out string error)
        {
            header = default;
            prefix = default;
            rawPtr = null;
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

            byte[] fileBytes = File.ReadAllBytes(absolutePath);
            if (fileBytes.Length < HeaderSize)
            {
                error = "Save file is smaller than the fixed header.";
                return false;
            }

            rawPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rawBuffer);

            fixed (byte* filePtr = fileBytes)
            {
                header = UnsafeUtility.ReadArrayElement<SaveFileHeader>(filePtr, 0);
                if (header.MagicValue != Magic)
                {
                    error = "Save magic mismatch.";
                    return false;
                }

                if (!TryValidateHeader(header, out error))
                    return false;

                uint computedHeaderHash = Hash32(filePtr, 0x24L);
                if (computedHeaderHash != header.HashHeader32)
                {
                    error = "Header checksum mismatch.";
                    return false;
                }

                int compressedPayloadLength = fileBytes.Length - HeaderSize;
                if (compressedPayloadLength <= 0)
                {
                    error = "Save payload is missing.";
                    return false;
                }

                int rawPayloadLength = Lz4BlockDecompress(filePtr + HeaderSize, compressedPayloadLength, rawPtr, rawBuffer.Length);
                if (rawPayloadLength <= 0)
                {
                    error = "LZ4 block decompression failed.";
                    return false;
                }

                uint computedPayloadHash = Hash32(rawPtr, rawPayloadLength);
                if (computedPayloadHash != header.HashPayload32)
                {
                    error = "Payload checksum mismatch.";
                    return false;
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

                int expectedPayloadBytes = metadataBytes + checked((int)prefix.SaveDataByteLength);
                if (expectedPayloadBytes > rawPayloadLength)
                {
                    error = "Serialized save data exceeds the decompressed payload length.";
                    return false;
                }

                if (RequiresCompatMigration(header))
                {
                    if (!TryMigratePayloadHeader(ref header, out error))
                        return false;
                }
            }

            return true;
        }

        private static ES3Settings CreatePayloadSettings()
        {
            return new ES3Settings
            {
                encryptionType = ES3.EncryptionType.None,
                compressionType = ES3.CompressionType.None
            };
        }

        private static uint ComputeHeaderHash(ref SaveFileHeader header)
        {
            SaveFileHeader copy = header;
            copy.HashHeader32 = 0u;
            copy.HashPayload32 = header.HashPayload32;
            return Hash32(UnsafeUtility.AddressOf(ref copy), 0x24L);
        }

        private static bool TryValidateHeader(SaveFileHeader header, out string error)
        {
            if (header.Version < MinimumSupportedVersion || header.Version > CurrentVersion)
            {
                error = $"Unsupported save header version {header.Version}.";
                return false;
            }

            if (header.CompatMask != LegacyCompatMaskV1 && header.CompatMask != CurrentCompatMask)
            {
                error = $"Unsupported save compatibility mask 0x{header.CompatMask:X2}.";
                return false;
            }

            if ((header.Flags & FlagLz4Blocks) == 0)
            {
                error = "Save payload is not flagged as LZ4 block-compressed.";
                return false;
            }

            if (header.PlayerOffset != HeaderSize)
            {
                error = $"Player payload offset {header.PlayerOffset} does not match fixed header size {HeaderSize}.";
                return false;
            }

            if (header.DeltaOffset < header.PlayerOffset || header.EntityOffset < header.DeltaOffset)
            {
                error = "Save payload offsets are out of order.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool RequiresCompatMigration(SaveFileHeader header)
        {
            return header.Version < CurrentVersion || header.CompatMask != CurrentCompatMask;
        }

        private static bool TryMigratePayloadHeader(ref SaveFileHeader header, out string error)
        {
            if (header.CompatMask == LegacyCompatMaskV1)
            {
                if (header.DeltaCount != 0u || header.EntityCount != 0u)
                {
                    error = "Legacy v1 delta/entity migration is not implemented for populated payload sections.";
                    return false;
                }

                header.Version = CurrentVersion;
                header.CompatMask = CurrentCompatMask;
                error = string.Empty;
                return true;
            }

            if (header.Version < CurrentVersion)
            {
                header.Version = CurrentVersion;
                header.CompatMask = CurrentCompatMask;
                error = string.Empty;
                return true;
            }

            error = $"Unsupported save compatibility migration path from version {header.Version} mask 0x{header.CompatMask:X2}.";
            return false;
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

        private static AupPosition ToAup(Vector3 runtimePosition)
        {
            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);

            float gx = Mathf.Floor(absolutePosition.x / AupCellSizeMeters);
            float gy = Mathf.Floor(absolutePosition.y / AupCellSizeMeters);
            float gz = Mathf.Floor(absolutePosition.z / AupCellSizeMeters);

            long gridX = (long)gx;
            long gridY = (long)gy;
            long gridZ = (long)gz;

            float originX = gridX * AupCellSizeMeters;
            float originY = gridY * AupCellSizeMeters;
            float originZ = gridZ * AupCellSizeMeters;

            return new AupPosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = absolutePosition.x - originX,
                LocalY = absolutePosition.y - originY,
                LocalZ = absolutePosition.z - originZ
            };
        }

        private static Vector3 ToRuntimePosition(AupPosition position)
        {
            Vector3 absolutePosition = new Vector3(
                (position.GridX * AupCellSizeMeters) + position.LocalX,
                (position.GridY * AupCellSizeMeters) + position.LocalY,
                (position.GridZ * AupCellSizeMeters) + position.LocalZ);

            return HectonFloatingOrigin.ToRuntimePosition(absolutePosition);
        }

        private static bool TryReadUtf8String(
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
                error = "UTF-8 metadata block exceeds the payload bounds.";
                return false;
            }

            if (byteLength == 0)
                return true;

            byte[] bytes = new byte[byteLength];
            CopyUnmanagedBytesToManaged(source + cursor, bytes);
            value = Encoding.UTF8.GetString(bytes);
            cursor += byteLength;
            return true;
        }

        private static void CopyManagedBytesToUnmanaged(byte[] source, byte* destination)
        {
            if (source == null || source.Length == 0)
                return;

            fixed (byte* sourcePtr = source)
            {
                UnsafeUtility.MemCpy(destination, sourcePtr, source.Length);
            }
        }

        private static void CopyUnmanagedBytesToManaged(byte* source, byte[] destination)
        {
            if (destination == null || destination.Length == 0)
                return;

            fixed (byte* destinationPtr = destination)
            {
                UnsafeUtility.MemCpy(destinationPtr, source, destination.Length);
            }
        }

        private static int Lz4BlockDecompress(byte* source, int compressedLength, byte* destination, int destinationCapacity)
        {
            if (source == null || destination == null || compressedLength <= 0 || destinationCapacity <= 0)
                return 0;

            int sourceOffset = 0;
            int destinationOffset = 0;

            while (sourceOffset < compressedLength)
            {
                if (sourceOffset + 8 > compressedLength)
                    return 0;

                int blockCompressedLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset, 0);
                int blockRawLength = UnsafeUtility.ReadArrayElement<int>(source + sourceOffset + 4, 0);
                if (blockCompressedLength <= 0 || blockRawLength <= 0)
                    return 0;

                sourceOffset += 8;
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

            return destinationOffset;
        }

        [DllImport(Lz4DllName, EntryPoint = "LZ4_compress_default")]
        private static extern int LZ4Compress(byte* source, byte* destination, int sourceLength, int destinationCapacity);

        [DllImport(Lz4DllName, EntryPoint = "LZ4_decompress_safe")]
        private static extern int LZ4Decompress(byte* source, byte* destination, int compressedLength, int destinationCapacity);
    }
}
