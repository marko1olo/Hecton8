using System;
using System.Buffers.Binary;
using System.IO;
using Unity.Collections;

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
        private const int CsvReadBufferBytes = 1024;
        private const int CsvMaxLineBytes = 256;
        private const uint SourceHashLegacy = 0x4F53484Fu; // OSHO
        private const uint SourceHashCsv = 0x4353564Fu; // CSVO
        private const uint HashArenaLimitBytes = 0x1E733BB8u;
        private const uint HashBufferCapacity = 0x34F22EE8u;
        private const uint HashHotCapacity = 0x3ECDC1EDu;
        private const uint HashColdCapacity = 0x4AA14DD4u;
        private const uint HashBucketCapacity = 0x5B8908DCu;
        private const uint HashScalabilityProfile = 0x9E7709EAu;

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
            if (vault.TryGetBufferHandle(BufferID.VaultMemoryLayoutConfig, out VaultBufferHandle<VaultMemoryLayoutConfig> existing) &&
                existing.IsCreated)
            {
                config = existing.GetElementAsReadOnlyRef(vault, 0);
            }

            ParseCsvOverrideStream(csvPath, ref config);

            config.SourceHash = SourceHashCsv;
            WriteConfigToVault(vault, in config);
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

            foreach (string file in Directory.EnumerateFiles(root, "memory_layout_metrics.h8bin", SearchOption.AllDirectories))
            {
                if (TryReadLegacyHeader(file, scalabilityProfile, out config))
                    return true;
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

        private static void ParseCsvOverrideStream(string csvPath, ref VaultMemoryLayoutConfig config)
        {
            Span<byte> readBuffer = stackalloc byte[CsvReadBufferBytes];
            Span<byte> lineBuffer = stackalloc byte[CsvMaxLineBytes];
            int lineLength = 0;
            bool lineOverflow = false;

            using FileStream stream = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                CsvReadBufferBytes,
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
            }
        }

        private static void WriteConfigToVault(IDataVault vault, in VaultMemoryLayoutConfig config)
        {
            VaultBufferHandle<VaultMemoryLayoutConfig> handle = vault.GetBufferHandle<VaultMemoryLayoutConfig>(
                BufferID.VaultMemoryLayoutConfig,
                1,
                SystemID.CoreDataVault,
                NativeArrayOptions.UninitializedMemory);
            if (!handle.IsCreated)
                return;

            ref VaultMemoryLayoutConfig stored = ref handle.GetElementAsRef(vault, 0);
            stored = config;
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
    }
}
