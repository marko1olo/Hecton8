using System;
using System.Buffers.Binary;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Cold boot importer for legacy OSHINO memory-layout headers with mock fallback.
    /// </summary>
    public static class VaultLegacyBinaryArchaeology
    {
        private const ulong LegacyMagic = 0x4D454D4C41594F48UL; // HOYALMEM
        private const int MinimumLegacyHeaderBytes = 48;
        private const int FileStreamBufferBytes = 1024;
        public const int CsvScratchBytes = 4096;
        private const int CsvMaxLineBytes = 256;
        private const uint SourceHashLegacy = 0x4F53484Fu; // OSHO
        private const uint SourceHashCsv = 0x4353564Fu; // CSVO
        private const uint HashArenaLimitBytes = 0x1E733BB8u;
        private const uint HashBufferCapacity = 0x34F22EE8u;
        private const uint HashHotCapacity = 0x3ECDC1EDu;
        private const uint HashColdCapacity = 0x4AA14DD4u;
        private const uint HashBucketCapacity = 0x5B8908DCu;
        private const uint HashScalabilityProfile = 0x9E7709EAu;
        private const uint HashStrideAggressiveness = 0x12191D2Eu;

        /// <summary>
        /// Scans batch archives and StreamingAssets for an OSHINO memory-layout binary, then writes a vault config.
        /// </summary>
        public static bool TryBootstrapMemoryLayout(IDataVault vault, string projectRoot, byte scalabilityProfile, out VaultMemoryLayoutConfig config)
        {
            config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            if (vault == null)
                return false;

            bool loaded = false;
            try
            {
                loaded = TryScanRoots(projectRoot, scalabilityProfile, out config);
            }
            catch (FileNotFoundException)
            {
                config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            }
            catch (IOException)
            {
                config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            }
            catch (UnauthorizedAccessException)
            {
                config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            }

            WriteConfigToVault(vault, in config);
            return loaded;
        }

        /// <summary>
        /// Applies a debug CSV override file to the vault config using a span parser.
        /// </summary>
        public static bool TryApplyMemoryOverridesCsv(IDataVault vault, string csvPath)
        {
            if (vault == null || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            VaultMemoryLayoutConfig config = VaultMemoryMath.BuildMockConfig(0);
            if (TryOpenExistingLane(
                    vault,
                    BufferID.VaultMemoryLayoutConfig,
                    1,
                    out NativeArray<VaultMemoryLayoutConfig> existingBuffer))
            {
                config = existingBuffer[0];
            }

            if (!OpenOrAcquireLane(
                    vault,
                    BufferID.VaultMemoryProfileCsvScratch,
                    CsvScratchBytes,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<byte> scratch) ||
                scratch.Length < CsvMaxLineBytes + 64)
            {
                return false;
            }

            ParseCsvOverrideStream(csvPath, scratch, ref config);

            config.SourceHash = SourceHashCsv;
            WriteConfigToVault(vault, in config);
            return true;
        }

        /// <summary>
        /// Slow-tick file monitor for play-mode memory profile overrides. The caller owns the last-write tick cache.
        /// </summary>
        public static bool TryPollMemoryOverridesCsv(IDataVault vault, string csvPath, ref long lastWriteTicks)
        {
            if (vault == null || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(csvPath).Ticks;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (ticks == lastWriteTicks)
                return false;

            if (!TryApplyMemoryOverridesCsv(vault, csvPath))
                return false;

            lastWriteTicks = ticks;
            return true;
        }

        /// <summary>Writes a prepared config into the vault layout buffer.</summary>
        public static void WriteMemoryLayoutConfig(IDataVault vault, in VaultMemoryLayoutConfig config)
        {
            WriteConfigToVault(vault, in config);
        }

        private static bool TryScanRoots(string projectRoot, byte scalabilityProfile, out VaultMemoryLayoutConfig config)
        {
            config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            if (string.IsNullOrEmpty(projectRoot))
                throw new FileNotFoundException("Project root missing.");

            string archiveRoot = Path.Combine(projectRoot, "Docs", "Archive");
            if (TryScanDirectory(archiveRoot, scalabilityProfile, out config))
                return true;

            string streamingRoot = Path.Combine(projectRoot, "StreamingAssets");
            if (TryScanDirectory(streamingRoot, scalabilityProfile, out config))
                return true;

            string unityStreamingRoot = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            if (TryScanDirectory(unityStreamingRoot, scalabilityProfile, out config))
                return true;

            throw new FileNotFoundException("OSHINO memory_layout_metrics.h8bin absent.");
        }

        private static bool TryScanDirectory(string root, byte scalabilityProfile, out VaultMemoryLayoutConfig config)
        {
            config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return false;

            System.Collections.Generic.IEnumerator<string> files = Directory
                .EnumerateFiles(root, "memory_layout_metrics.h8bin", SearchOption.AllDirectories)
                .GetEnumerator();
            using (files)
            {
                while (files.MoveNext())
                {
                    string file = files.Current;
                    if (TryReadLegacyHeader(file, scalabilityProfile, out config))
                        return true;
                }
            }

            return false;
        }

        private static bool TryReadLegacyHeader(string path, byte scalabilityProfile, out VaultMemoryLayoutConfig config)
        {
            config = VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            Span<byte> bytes = stackalloc byte[MinimumLegacyHeaderBytes];
            if (!TryReadHeader(path, bytes))
                return false;

            ulong magic = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, 8));
            if (magic != LegacyMagic)
                return false;

            long arenaLimitBytes = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
            int bufferCapacity = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(24, 4));
            int hotCapacity = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(28, 4));
            int coldCapacity = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(32, 4));
            int bucketCapacity = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(36, 4));
            byte profile = bytes[40];

            config.ArenaLimitBytes = arenaLimitBytes > 0L
                ? Align16(arenaLimitBytes)
                : GlobalDataVault.ResolveArenaCapacityLimit(scalabilityProfile);
            config.BufferCapacity = Clamp(bufferCapacity, 128, 32768);
            config.HotEntityCapacity = Align16(Clamp(hotCapacity, 64, 1048576));
            config.ColdEntityCapacity = Align16(Clamp(coldCapacity, 64, 1048576));
            config.BucketCapacity = Clamp(bucketCapacity, 1, 64);
            config.SourceHash = SourceHashLegacy;
            config.Version = unchecked((uint)BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4)));
            config.ScalabilityProfile = profile;
            config.Flags = 2;
            return true;
        }

        private static bool TryReadHeader(string path, Span<byte> destination)
        {
            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                FileStreamBufferBytes,
                FileOptions.SequentialScan);

            int totalRead = 0;
            while (totalRead < destination.Length)
            {
                int read = stream.Read(destination.Slice(totalRead));
                if (read <= 0)
                    return false;

                totalRead += read;
            }

            return true;
        }

        private static unsafe void ParseCsvOverrideStream(string csvPath, NativeArray<byte> scratch, ref VaultMemoryLayoutConfig config)
        {
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            int lineCapacity = math.min(CsvMaxLineBytes, scratch.Length >> 2);
            int readCapacity = scratch.Length - lineCapacity;
            if (basePtr == null || lineCapacity <= 0 || readCapacity <= 0)
                return;

            Span<byte> lineBuffer = new Span<byte>(basePtr, lineCapacity);
            Span<byte> readBuffer = new Span<byte>(basePtr + lineCapacity, readCapacity);
            int lineLength = 0;
            bool lineOverflow = false;

            using FileStream stream = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                readCapacity,
                FileOptions.SequentialScan);

            while (true)
            {
                int read = stream.Read(readBuffer);
                if (read <= 0)
                    break;

                for (int i = 0; i < read; i++)
                {
                    byte c = readBuffer[i];
                    if (c == (byte)'\n' || c == (byte)'\r')
                    {
                        if (!lineOverflow && lineLength > 0)
                            ParseOverrideLine(lineBuffer.Slice(0, lineLength), ref config);

                        lineLength = 0;
                        lineOverflow = false;
                        continue;
                    }

                    if (lineOverflow)
                        continue;

                    if (lineLength >= lineBuffer.Length)
                    {
                        lineLength = 0;
                        lineOverflow = true;
                        continue;
                    }

                    lineBuffer[lineLength++] = c;
                }
            }

            if (!lineOverflow && lineLength > 0)
                ParseOverrideLine(lineBuffer.Slice(0, lineLength), ref config);
        }

        private static void ParseOverrideLine(ReadOnlySpan<byte> line, ref VaultMemoryLayoutConfig config)
        {
            int comma = -1;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != (byte)',')
                    continue;

                comma = i;
                break;
            }

            if (comma <= 0)
                return;

            ReadOnlySpan<byte> key = Trim(line.Slice(0, comma));
            ReadOnlySpan<byte> valueSpan = Trim(line.Slice(comma + 1));
            if (!TryParseLong(valueSpan, out long value))
                return;

            uint keyHash = HashLowerAscii(key);
            switch (keyHash)
            {
                case HashArenaLimitBytes:
                    config.ArenaLimitBytes = value > 0L ? Align16(value) : config.ArenaLimitBytes;
                    break;
                case HashBufferCapacity:
                    config.BufferCapacity = Clamp((int)value, 128, 32768);
                    break;
                case HashHotCapacity:
                    config.HotEntityCapacity = Align16(Clamp((int)value, 64, 1048576));
                    break;
                case HashColdCapacity:
                    config.ColdEntityCapacity = Align16(Clamp((int)value, 64, 1048576));
                    break;
                case HashBucketCapacity:
                    config.BucketCapacity = Clamp((int)value, 1, 64);
                    break;
                case HashScalabilityProfile:
                    config.ScalabilityProfile = (byte)Clamp((int)value, 0, 3);
                    break;
                case HashStrideAggressiveness:
                    config.StrideAggressiveness = Clamp01Milli(value);
                    break;
            }
        }

        private static void WriteConfigToVault(IDataVault vault, in VaultMemoryLayoutConfig config)
        {
            if (!OpenOrAcquireLane(
                    vault,
                    BufferID.VaultMemoryLayoutConfig,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<VaultMemoryLayoutConfig> buffer))
            {
                return;
            }

            buffer[0] = config;
        }

        private static bool TryOpenExistingLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenLane(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool OpenOrAcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsHandleCreated(in handle, bufferId))
            {
                handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, owner, options);
            }

            return TryOpenLane(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !IsHandleCreated(in handle, bufferId))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start < span.Length && span[start] <= 32)
                start++;
            while (end >= start && span[end] <= 32)
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseLong(ReadOnlySpan<byte> span, out long value)
        {
            value = 0L;
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[0] == (byte)'-')
            {
                negative = true;
                index = 1;
            }

            long result = 0L;
            for (; index < span.Length; index++)
            {
                byte c = span[index];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                result = (result * 10L) + (c - (byte)'0');
            }

            value = negative ? -result : result;
            return true;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
            {
                byte c = key[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static int Align16(int value)
        {
            return (value + 15) & ~15;
        }

        private static long Align16(long value)
        {
            return (value + 15L) & ~15L;
        }

        private static float Clamp01Milli(long value)
        {
            long clamped = value < 0L ? 0L : value > 1000L ? 1000L : value;
            return clamped * 0.001f;
        }
    }
}
