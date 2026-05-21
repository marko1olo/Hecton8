using System.IO;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    public struct HadalTrenchPayloadValidationResult
    {
        public uint Flags;
        public long FileBytes;
        public int HeaderBytes;
        public int UncompressedBytes;
        public int CompressedBytes;
        public int VentCount;
        public int AdaptiveBlockCount;
        public ulong PayloadHash;
    }

    public static class HadalTrenchPayloadValidationFlags
    {
        public const uint MissingFile = 1u << 0;
        public const uint ShortFile = 1u << 1;
        public const uint MagicMismatch = 1u << 2;
        public const uint EndianMismatch = 1u << 3;
        public const uint HeaderMismatch = 1u << 4;
        public const uint OffsetMismatch = 1u << 5;
        public const uint SizeMismatch = 1u << 6;
        public const uint HashMismatch = 1u << 7;
        public const uint RollbackFlagMissing = 1u << 8;
        public const uint SchemaMismatch = 1u << 9;
        public const uint PreludeMismatch = 1u << 10;
    }

    public static class HadalTrenchPayloadValidator
    {
        private const string DefaultPath = "Assets/StreamingAssets/Hecton8/HadalTrenches/hadal_trench_sector_0000.h8bin";
        private const int StreamHashBufferBytes = 131072;

        [MenuItem("HECTON-8/Hadal Trench Forge/Validate Last Payload")]
        public static void ValidateDefaultMenu()
        {
            bool ok = ValidateFile(DefaultPath, out HadalTrenchPayloadValidationResult result);
            if (ok)
                Debug.Log("[SHINOBU_241] Hadal trench payload validated. bytes=" + result.FileBytes + " hash=0x" + result.PayloadHash.ToString("X16"));
            else
                Debug.LogError("[SHINOBU_241] Hadal trench payload validation failed. flags=0x" + result.Flags.ToString("X8"));
        }

        public static bool ValidateFile(string path, out HadalTrenchPayloadValidationResult result)
        {
            result = default;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.MissingFile;
                return false;
            }

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, StreamHashBufferBytes, FileOptions.SequentialScan);
            result.FileBytes = stream.Length;
            if (result.FileBytes < (long)HadalTrenchBakeConstants.HeaderBytes + 8L)
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.ShortFile;
                return false;
            }

            byte[] headerBytesBuffer = new byte[(int)HadalTrenchBakeConstants.HeaderBytes];
            if (!ReadExact(stream, headerBytesBuffer, headerBytesBuffer.Length))
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.ShortFile;
                return false;
            }

            int offset = 0;
            uint magic = ReadUInt32(headerBytesBuffer, ref offset);
            uint version = ReadUInt32(headerBytesBuffer, ref offset);
            uint flags = ReadUInt32(headerBytesBuffer, ref offset);
            offset += 12;
            offset += 24;
            offset += 4;
            uint compressionMode = ReadUInt32(headerBytesBuffer, ref offset);
            int compressedBytes = ReadInt32(headerBytesBuffer, ref offset);
            int rleRunCount = ReadInt32(headerBytesBuffer, ref offset);
            int ventCount = ReadInt32(headerBytesBuffer, ref offset);
            int adaptiveBlockCount = ReadInt32(headerBytesBuffer, ref offset);
            offset += 16;
            ulong densityPayloadOffset = ReadUInt64(headerBytesBuffer, ref offset);
            ulong ventPayloadOffset = ReadUInt64(headerBytesBuffer, ref offset);
            ulong adaptivePayloadOffset = ReadUInt64(headerBytesBuffer, ref offset);
            ulong payloadHash = ReadUInt64(headerBytesBuffer, ref offset);
            uint headerBytes = ReadUInt32(headerBytesBuffer, ref offset);
            uint endianMarker = ReadUInt32(headerBytesBuffer, ref offset);
            int uncompressedBytes = ReadInt32(headerBytesBuffer, ref offset);
            int densityPreludeBytes = ReadInt32(headerBytesBuffer, ref offset);
            ulong totalFileBytes = ReadUInt64(headerBytesBuffer, ref offset);
            uint sectionAlignmentBytes = ReadUInt32(headerBytesBuffer, ref offset);
            uint checksumType = ReadUInt32(headerBytesBuffer, ref offset);
            uint schemaHash = ReadUInt32(headerBytesBuffer, ref offset);
            offset += 4;

            result.HeaderBytes = (int)headerBytes;
            result.UncompressedBytes = uncompressedBytes;
            result.CompressedBytes = compressedBytes;
            result.VentCount = ventCount;
            result.AdaptiveBlockCount = adaptiveBlockCount;
            result.PayloadHash = payloadHash;

            if (magic != HadalTrenchBakeConstants.H8BinMagic || version != HadalTrenchBakeConstants.FileVersion)
                result.Flags |= HadalTrenchPayloadValidationFlags.MagicMismatch;
            if (endianMarker != HadalTrenchBakeConstants.PayloadEndianMarker)
                result.Flags |= HadalTrenchPayloadValidationFlags.EndianMismatch;
            if (headerBytes != HadalTrenchBakeConstants.HeaderBytes ||
                densityPreludeBytes != 8 ||
                sectionAlignmentBytes != HadalTrenchBakeConstants.PayloadSectionAlignmentBytes)
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.HeaderMismatch;
            }

            if (schemaHash != HadalTrenchBakeConstants.PayloadSchemaHash ||
                checksumType != HadalTrenchBakeConstants.PayloadChecksumFnv1A64 ||
                (compressionMode != (uint)HadalTrenchCompressionMode.Rle &&
                 compressionMode != (uint)HadalTrenchCompressionMode.RleLz4Block))
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.SchemaMismatch;
            }

            if ((flags & HadalTrenchBakeConstants.RollbackExcludedFlag) == 0u)
                result.Flags |= HadalTrenchPayloadValidationFlags.RollbackFlagMissing;

            if (densityPreludeBytes == 8 && result.FileBytes >= (long)headerBytes + 8L)
            {
                byte[] preludeBytes = new byte[8];
                stream.Seek(headerBytes, SeekOrigin.Begin);
                if (ReadExact(stream, preludeBytes, preludeBytes.Length))
                {
                    int preludeOffset = 0;
                    int preludeUncompressedBytes = ReadInt32(preludeBytes, ref preludeOffset);
                    int preludeCompressedBytes = ReadInt32(preludeBytes, ref preludeOffset);
                    if (preludeUncompressedBytes != uncompressedBytes || preludeCompressedBytes != compressedBytes)
                        result.Flags |= HadalTrenchPayloadValidationFlags.PreludeMismatch;
                }
                else
                {
                    result.Flags |= HadalTrenchPayloadValidationFlags.ShortFile;
                }
            }
            else
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.PreludeMismatch;
            }

            long ventBytes = CountBytes(ventCount, UnsafeUtility.SizeOf<ThermalVentSpawnDTO>());
            long adaptiveBytes = CountBytes(adaptiveBlockCount, UnsafeUtility.SizeOf<HadalTrenchAdaptiveBlockDTO>());
            ulong alignment = sectionAlignmentBytes == 0u ? 1ul : sectionAlignmentBytes;
            ulong expectedDensityOffset = (ulong)headerBytes + (ulong)MathMax(0, densityPreludeBytes);
            ulong expectedVentOffset = AlignUp(expectedDensityOffset + (ulong)MathMax(0, compressedBytes), alignment);
            ulong expectedAdaptiveOffset = AlignUp(expectedVentOffset + (ulong)MathMax(0L, ventBytes), alignment);
            ulong expectedTotal = expectedAdaptiveOffset + (ulong)MathMax(0L, adaptiveBytes);

            bool invalidAlignment = sectionAlignmentBytes == 0u ||
                                    densityPayloadOffset % sectionAlignmentBytes != 0u ||
                                    ventPayloadOffset % sectionAlignmentBytes != 0u ||
                                    adaptivePayloadOffset % sectionAlignmentBytes != 0u;
            if (densityPayloadOffset != expectedDensityOffset ||
                ventPayloadOffset != expectedVentOffset ||
                adaptivePayloadOffset != expectedAdaptiveOffset ||
                invalidAlignment)
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.OffsetMismatch;
            }

            if (totalFileBytes != (ulong)result.FileBytes ||
                expectedTotal != (ulong)result.FileBytes ||
                compressedBytes <= 0 ||
                uncompressedBytes <= 0 ||
                rleRunCount <= 0 ||
                ventCount < 0 ||
                adaptiveBlockCount < 0 ||
                ventBytes < 0L ||
                adaptiveBytes < 0L)
            {
                result.Flags |= HadalTrenchPayloadValidationFlags.SizeMismatch;
            }

            if (result.Flags == 0u)
            {
                byte[] hashBuffer = new byte[StreamHashBufferBytes];
                ulong observedHash = 1469598103934665603ul;
                observedHash = HashStreamRange(stream, (long)densityPayloadOffset, compressedBytes, hashBuffer, observedHash);
                observedHash = HashStreamRange(stream, (long)ventPayloadOffset, ventBytes, hashBuffer, observedHash);
                observedHash = HashStreamRange(stream, (long)adaptivePayloadOffset, adaptiveBytes, hashBuffer, observedHash);
                if (observedHash == 0ul)
                    observedHash = 1ul;
                if (observedHash != payloadHash)
                    result.Flags |= HadalTrenchPayloadValidationFlags.HashMismatch;
            }

            return result.Flags == 0u;
        }

        private static int MathMax(int a, int b)
        {
            return a > b ? a : b;
        }

        private static long MathMax(long a, long b)
        {
            return a > b ? a : b;
        }

        private static long CountBytes(int count, int elementBytes)
        {
            if (count < 0 || elementBytes <= 0)
                return -1L;

            return (long)count * (long)elementBytes;
        }

        private static ulong AlignUp(ulong value, ulong alignment)
        {
            ulong safeAlignment = alignment == 0ul ? 1ul : alignment;
            ulong mask = safeAlignment - 1ul;
            return (value + mask) & ~mask;
        }

        private static bool ReadExact(Stream stream, byte[] bytes, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(bytes, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }

            return true;
        }

        private static uint ReadUInt32(byte[] bytes, ref int offset)
        {
            uint value = (uint)(bytes[offset] |
                                (bytes[offset + 1] << 8) |
                                (bytes[offset + 2] << 16) |
                                (bytes[offset + 3] << 24));
            offset += 4;
            return value;
        }

        private static int ReadInt32(byte[] bytes, ref int offset)
        {
            return unchecked((int)ReadUInt32(bytes, ref offset));
        }

        private static ulong ReadUInt64(byte[] bytes, ref int offset)
        {
            ulong lo = ReadUInt32(bytes, ref offset);
            ulong hi = ReadUInt32(bytes, ref offset);
            return lo | (hi << 32);
        }

        private static ulong HashStreamRange(Stream stream, long start, long count, byte[] buffer, ulong hash)
        {
            stream.Seek(start, SeekOrigin.Begin);
            long remaining = count;
            while (remaining > 0L)
            {
                int request = remaining > buffer.Length ? buffer.Length : (int)remaining;
                int read = stream.Read(buffer, 0, request);
                if (read <= 0)
                    return 0ul;

                for (int i = 0; i < read; i++)
                {
                    hash ^= buffer[i];
                    hash *= 1099511628211ul;
                }

                remaining -= read;
            }

            return hash;
        }
    }
}
