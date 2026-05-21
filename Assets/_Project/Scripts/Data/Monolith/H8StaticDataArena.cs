using System;
using System.Diagnostics;
using System.IO;
#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

namespace Hecton8.Data
{
    /// <summary>
    /// Boot-owned static data arena for monolithic baked content blobs.
    /// </summary>
    public static unsafe class H8StaticDataArena
    {
        private const long MaxBlobBytes = 256L * 1024L * 1024L;
        private const uint MissingUtf8Offset = uint.MaxValue;
        private const int StaticLocalizationItemsSection = 0;
        private const int StaticLocalizationCreaturesSection = 1;
        private const int StaticLocalizationBiomesSection = 2;
        private const int StaticLocalizationGhostModulesSection = 3;
        private const int StaticLocalizationSopErrorsSection = 4;
        private const int StaticLocalizationSectionCount = 5;
        private const BufferID DataMonolithPayloadBufferId = (BufferID)71103;
        private const BufferID DataMonolithTelemetryRingBufferId = (BufferID)71104;
        private const BufferID DataMonolithTelemetryCursorBufferId = (BufferID)71105;
        private const uint PathFlagFileStream = 1u;
        private const uint PathFlagMemoryMappedFile = 2u;
        private const uint PathFlagVaultBacked = 4u;
        private const uint PathFlagStreamingUriStaged = 8u;

        private static IDataVault _vault;
        private static VaultGenerationHandle<byte> _arenaHandle;
        private static VaultGenerationHandle<H8DataMonolithTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<int> _telemetryCursorHandle;
        private static H8DataBlobHeader _header;
        private static H8DataBlobDirectory _directory;
        private static int _residentBlobBytes;
        private static int _loaded;
        private static int _writeLocked;
        private static int _telemetryFrame;
        private static long _lastReadTicks;
        private static uint _lastReadPathFlags;

        /// <summary>True when a valid blob is resident.</summary>
        public static bool IsLoaded => Volatile.Read(ref _loaded) != 0;

        /// <summary>True when the boot Ready phase has locked the resident arena against writes.</summary>
        public static bool IsWriteLocked => Volatile.Read(ref _writeLocked) != 0;

        /// <summary>Resident blob header.</summary>
        public static H8DataBlobHeader Header => _header;

        /// <summary>Resident blob directory.</summary>
        public static H8DataBlobDirectory Directory => _directory;

        /// <summary>Resident byte count.</summary>
        public static int ByteLength => _residentBlobBytes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        /// <summary>
        /// Loads the default monolith file from StreamingAssets.
        /// </summary>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="failIfMissing">If true, a missing file is reported as a boot failure.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when the blob loaded and passed checksum validation.</returns>
        public static bool TryInitializeFromStreamingAssets(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
            string absolutePath = BuildStreamingAssetsLocation(
                Application.streamingAssetsPath,
                H8DataLayoutConstants.DefaultStreamingAssetsRelativePath);

            uint pathFlags = 0u;
            if (TryStageStreamingAssetsUriToCache(absolutePath, out string stagedPath))
            {
                absolutePath = stagedPath;
                pathFlags |= PathFlagStreamingUriStaged;
            }

            bool loaded = TryInitializeFromFile(
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                pathFlags,
                out status);
            if (!loaded && failIfMissing)
                throw new FatalArchitectureException("Data Monolith boot failure: " + status);

            return loaded;
        }

        private static string BuildStreamingAssetsLocation(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root))
                return relativePath;

            if (IsFilesystemPath(root))
                return Path.Combine(root, relativePath);

            string normalizedRoot = root.EndsWith("/", StringComparison.Ordinal) ? root : root + "/";
            return normalizedRoot + relativePath.Replace('\\', '/');
        }

        private static bool IsFilesystemPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) &&
                   path.IndexOf("://", StringComparison.Ordinal) < 0;
        }

        private static bool TryStageStreamingAssetsUriToCache(string streamingUri, out string cachedPath)
        {
            cachedPath = null;
            if (string.IsNullOrEmpty(streamingUri) || IsFilesystemPath(streamingUri))
                return false;

#if UNITY_WEBGL
            return false;
#else
            string cachePath = null;
            string tempPath = null;
            try
            {
                string cacheDirectory = Path.Combine(Application.temporaryCachePath, "Hecton8", "DataMonolith");
                System.IO.Directory.CreateDirectory(cacheDirectory);
                cachePath = Path.Combine(cacheDirectory, "static_data.h8bin");
                tempPath = cachePath + ".tmp";

                TryDeleteFile(tempPath);
                using UnityWebRequest request = new UnityWebRequest(streamingUri, UnityWebRequest.kHttpVerbGET);
                request.downloadHandler = new DownloadHandlerFile(tempPath)
                {
                    removeFileOnAbort = true
                };
                request.disposeDownloadHandlerOnDispose = true;
                request.timeout = 30;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                    Thread.Sleep(1);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    TryDeleteFile(tempPath);
                    return false;
                }

                if (!File.Exists(tempPath))
                    return false;

                TryDeleteFile(cachePath);
                File.Move(tempPath, cachePath);
                cachedPath = cachePath;
                return true;
            }
            catch (Exception)
            {
                TryDeleteFile(tempPath);
                return false;
            }
#endif
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Loads one monolith file into a persistent native arena and validates the header checksum.
        /// </summary>
        /// <param name="absolutePath">Absolute path to a `.h8bin` blob.</param>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="failIfMissing">If true, missing file is a boot failure.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when resident data is ready.</returns>
        public static bool TryInitializeFromFile(
            string absolutePath,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            out H8DataBlobLoadStatus status)
        {
            return TryInitializeFromFile(absolutePath, expectedWorldSeed, expectedAppVersionHash, failIfMissing, 0u, out status);
        }

        private static bool TryInitializeFromFile(
            string absolutePath,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            bool failIfMissing,
            uint inheritedPathFlags,
            out H8DataBlobLoadStatus status)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                status = H8DataBlobLoadStatus.Missing;
                if (failIfMissing)
                    RecordFailureTelemetry(status, inheritedPathFlags);

                return !failIfMissing && IsLoaded;
            }

            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            FileInfo info = new FileInfo(absolutePath);
            if (info.Length < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            if (info.Length > MaxBlobBytes || info.Length > int.MaxValue)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            int blobBytes = (int)info.Length;
            ShutdownArenaOnly();
            if (!TryAllocateArena(blobBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                RecordFailureTelemetry(status, inheritedPathFlags);
                return false;
            }

            _residentBlobBytes = blobBytes;
            if (!TryReadWholeFileIntoArena(absolutePath, blobBytes, inheritedPathFlags, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            RecordTelemetry(status, _lastReadTicks, _lastReadTicks, _lastReadPathFlags);
            if (ExceedsTelemetryDumpThreshold(_lastReadTicks))
                DumpTelemetry(status);
            return true;
        }

        private static void RecordFailureTelemetry(H8DataBlobLoadStatus status, uint pathFlags)
        {
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            RecordTelemetry(status, 0L, 0L, pathFlags);
            DumpTelemetry(status);
        }

        /// <summary>
        /// Copies an already-read binary blob into the resident native arena with one guarded MemCpy.
        /// </summary>
        /// <param name="source">Source blob pointer.</param>
        /// <param name="sourceBytes">Source byte count.</param>
        /// <param name="expectedWorldSeed">Expected world seed, or zero to accept seed-agnostic blobs.</param>
        /// <param name="expectedAppVersionHash">Expected app-version hash, or zero to skip version matching.</param>
        /// <param name="status">Load status.</param>
        /// <returns>True when resident data is ready.</returns>
        public static bool TryInitializeFromMemory(
            void* source,
            int sourceBytes,
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            if (source == null || sourceBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            if (sourceBytes > MaxBlobBytes)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                return false;
            }

            if (IsWriteLocked && IsLoaded)
            {
                status = H8DataBlobLoadStatus.ReadyLocked;
                return false;
            }

            ShutdownArenaOnly();
            if (!TryAllocateArena(sourceBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }

            if (!TryRefreshArenaView(out NativeArray<byte> arena) || arena.Length < sourceBytes)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                ShutdownArenaOnly();
                return false;
            }

            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
            _residentBlobBytes = sourceBytes;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, sourceBytes, source, sourceBytes))
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                RecordTelemetry(status, 0L, 0L, 0u);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                RecordTelemetry(status, 0L, 0L, 0u);
                DumpTelemetry(status);
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            RecordTelemetry(status, 0L, 0L, 0u);
            return true;
        }

        /// <summary>
        /// Locks the monolith against writes after boot readiness.
        /// </summary>
        public static void LockReady()
        {
            Interlocked.Exchange(ref _writeLocked, 1);
        }

        /// <summary>
        /// Releases the resident static data arena.
        /// </summary>
        public static void Shutdown()
        {
            Interlocked.Exchange(ref _writeLocked, 0);
            ShutdownArenaOnly();
        }

        /// <summary>
        /// Returns a section entry by numeric section ID.
        /// </summary>
        /// <param name="sectionId">Section identifier.</param>
        /// <param name="section">Resolved section entry.</param>
        /// <returns>True when the section exists and has a valid table entry.</returns>
        public static bool TryGetSection(H8DataSectionId sectionId, out H8DataSectionEntry section)
        {
            section = default;
            if (!IsLoaded || !TryRefreshArenaView(out NativeArray<byte> arena) || _directory.SectionCount == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            H8DataSectionEntry* entries = (H8DataSectionEntry*)(basePtr + _directory.SectionTableOffset);
            uint target = (uint)sectionId;

            uint directIndex = target - 1u;
            if (target != 0u && directIndex < _directory.SectionCount)
            {
                H8DataSectionEntry direct = entries[(int)directIndex];
                if (direct.SectionId == target)
                {
                    section = direct;
                    return IsSectionRangeValid(in section);
                }
            }

            for (int i = 0; i < _directory.SectionCount; i++)
            {
                H8DataSectionEntry entry = entries[i];
                if (entry.SectionId != target)
                    continue;

                section = entry;
                return IsSectionRangeValid(in section);
            }

            return false;
        }

        /// <summary>
        /// Returns a direct typed span over a resident section when the baked record size matches <typeparamref name="T"/>.
        /// </summary>
        public static ReadOnlySpan<T> GetSectionSpan<T>(H8DataSectionId sectionId)
            where T : unmanaged
        {
            return TryGetSectionSpan(sectionId, out ReadOnlySpan<T> records) ? records : ReadOnlySpan<T>.Empty;
        }

        public static ReadOnlySpan<T> GetSectionSpan<T>(uint sectionId)
            where T : unmanaged
        {
            return GetSectionSpan<T>((H8DataSectionId)sectionId);
        }

        /// <summary>
        /// Returns a direct typed span over a resident section without allocating or copying.
        /// </summary>
        public static bool TryGetSectionSpan<T>(H8DataSectionId sectionId, out ReadOnlySpan<T> records)
            where T : unmanaged
        {
            records = ReadOnlySpan<T>.Empty;
            if (!TryGetSection(sectionId, out H8DataSectionEntry section))
                return false;

            int recordSize = UnsafeUtility.SizeOf<T>();
            if (section.RecordSize != (uint)recordSize || section.Count == 0u)
                return false;

            if (!TryRefreshArenaView(out NativeArray<byte> arena))
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            records = new ReadOnlySpan<T>(basePtr + section.OffsetBytes, (int)section.Count);
            return true;
        }

        /// <summary>
        /// Returns a typed pointer to the beginning of a section.
        /// </summary>
        /// <param name="sectionId">Section identifier.</param>
        /// <param name="recordSize">Expected record byte size.</param>
        /// <param name="count">Resolved record count.</param>
        /// <returns>Pointer to the section payload, or null.</returns>
        public static void* GetSectionDataPointer(H8DataSectionId sectionId, int recordSize, out int count)
        {
            count = 0;
            if (recordSize <= 0 || !TryGetSection(sectionId, out H8DataSectionEntry section))
                return null;

            if (section.RecordSize != (uint)recordSize)
                return null;

            count = (int)section.Count;
            if (section.Count == 0)
                return null;

            if (!TryRefreshArenaView(out NativeArray<byte> arena))
                return null;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            return basePtr + section.OffsetBytes;
        }

        /// <summary>
        /// Resolves one item record by dense record index.
        /// </summary>
        public static bool TryGetItemRecord(uint recordIndex, out H8ItemRecord record)
        {
            record = default;
            H8ItemRecord* records = (H8ItemRecord*)GetSectionDataPointer(
                H8DataSectionId.Items,
                H8DataLayoutConstants.ItemRecordSize,
                out int count);

            if (records == null || recordIndex >= (uint)count)
                return false;

            record = records[recordIndex];
            return true;
        }

        /// <summary>
        /// Resolves one compact creature genome block by dense record index.
        /// </summary>
        public static bool TryGetCreatureGenomeBlock(uint recordIndex, out H8CreatureGenomeTraitBlock block)
        {
            block = default;
            H8CreatureTraitRecord* records = (H8CreatureTraitRecord*)GetSectionDataPointer(
                H8DataSectionId.Creatures,
                H8DataLayoutConstants.CreatureTraitRecordSize,
                out int count);

            if (records == null || recordIndex >= (uint)count)
                return false;

            H8CreatureTraitRecord record = records[recordIndex];
            block = new H8CreatureGenomeTraitBlock
            {
                Aggression = math.isfinite(record.Genome.Aggression) ? record.Genome.Aggression : 0f,
                Metabolism = math.isfinite(record.Genome.Metabolism) ? record.Genome.Metabolism : 1f,
                MaxHealth = math.isfinite(record.Genome.MaxHealth) ? record.Genome.MaxHealth : 1f,
                CruiseSpeed = math.isfinite(record.Genome.CruiseSpeed) ? record.Genome.CruiseSpeed : 0f,
                BurstSpeed = math.isfinite(record.Genome.BurstSpeed) ? record.Genome.BurstSpeed : 0f,
                SpawnCreditCost = math.isfinite(record.Genome.SpawnCreditCost) ? record.Genome.SpawnCreditCost : 0f,
                PressureMinMeters = math.isfinite(record.Genome.PressureMinMeters) ? record.Genome.PressureMinMeters : 0f,
                PressureMaxMeters = math.isfinite(record.Genome.PressureMaxMeters) ? record.Genome.PressureMaxMeters : 0f
            };
            return true;
        }

        /// <summary>
        /// Resolves one item record by FNV-1a hash. Item records are sorted by hash at bake time.
        /// </summary>
        public static bool TryFindItemRecordByHash(uint hashId, out H8ItemRecord record)
        {
            record = default;
            if (hashId == 0u)
                return false;

            H8ItemRecord* records = (H8ItemRecord*)GetSectionDataPointer(
                H8DataSectionId.Items,
                H8DataLayoutConstants.ItemRecordSize,
                out int count);

            if (records == null || count <= 0)
                return false;

            return TryFindByHash(records, count, hashId, out record);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public static bool TryFindByHash([NoAlias] H8ItemRecord* records, int count, uint hashId, out H8ItemRecord record)
        {
            record = default;
            if (records == null || count <= 0 || hashId == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8ItemRecord candidate = records[index];
                if (candidate.HashId == hashId)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.HashId < hashId)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        public static bool TryFindByHash(ReadOnlySpan<H8ItemRecord> records, uint hashId, out H8ItemRecord record)
        {
            fixed (H8ItemRecord* ptr = records)
            {
                return TryFindByHash(ptr, records.Length, hashId, out record);
            }
        }

        /// <summary>
        /// Provides the resident blob as a read-only native array for Burst jobs.
        /// </summary>
        /// <param name="arena">Resident arena.</param>
        /// <returns>True when a blob is loaded.</returns>
        public static bool TryGetArena(out NativeArray<byte>.ReadOnly arena)
        {
            arena = default;
            if (!IsLoaded || !TryRefreshArenaView(out NativeArray<byte> mutableArena))
                return false;

            arena = mutableArena.AsReadOnly();
            return true;
        }

        /// <summary>
        /// Provides the resident blob and the valid byte count inside the larger static arena.
        /// </summary>
        public static bool TryGetResidentBlob(out NativeArray<byte>.ReadOnly arena, out int blobBytes)
        {
            arena = default;
            blobBytes = _residentBlobBytes;
            if (!IsLoaded || !TryRefreshArenaView(out NativeArray<byte> mutableArena) || _residentBlobBytes <= 0)
                return false;

            arena = mutableArena.AsReadOnly();
            return true;
        }

        /// <summary>
        /// Resolves a deterministic integer loot CDF entry using a pre-ranged threshold.
        /// </summary>
        public static bool TryResolveLootItem(uint tableHash, uint threshold, out uint itemHash)
        {
            itemHash = 0u;
            if (tableHash == 0u || !TryFindLootTableRange(tableHash, out int start, out int end, out uint totalWeight) || totalWeight == 0u)
                return false;

            if (threshold >= totalWeight)
                threshold = totalWeight - 1u;

            H8LootCdfRecord* records = (H8LootCdfRecord*)GetSectionDataPointer(
                H8DataSectionId.LootCdf,
                UnsafeUtility.SizeOf<H8LootCdfRecord>(),
                out _);

            int low = start;
            int high = end - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8LootCdfRecord record = records[index];
                if (threshold < record.CumulativeWeight)
                {
                    if (index == start || threshold >= records[index - 1].CumulativeWeight)
                    {
                        itemHash = record.ItemHash;
                        return itemHash != 0u;
                    }

                    high = index - 1;
                }
                else
                {
                    low = index + 1;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves one 256x256 biome heatmap cell without querying MapMagic.
        /// </summary>
        public static bool TryGetBiomeHeatmapCell(int x, int y, out uint biomeHash)
        {
            biomeHash = 0u;
            H8BiomeHeatmapCellRecord* records = (H8BiomeHeatmapCellRecord*)GetSectionDataPointer(
                H8DataSectionId.BiomeHeatmap,
                UnsafeUtility.SizeOf<H8BiomeHeatmapCellRecord>(),
                out int count);

            if (records == null || count <= 0)
                return false;

            int clampedX = math.clamp(x, 0, 255);
            int clampedY = math.clamp(y, 0, 255);
            int directIndex = (clampedY << 8) + clampedX;
            if (directIndex >= count)
                return false;

            H8BiomeHeatmapCellRecord direct = records[directIndex];
            if (direct.X != clampedX || direct.Y != clampedY)
                return false;

            biomeHash = direct.BiomeHash;
            return biomeHash != 0u;
        }

        /// <summary>
        /// Resolves total integer weight for a deterministic loot table.
        /// </summary>
        public static bool TryGetLootTableTotalWeight(uint tableHash, out uint totalWeight)
        {
            totalWeight = 0u;
            return tableHash != 0u && TryFindLootTableRange(tableHash, out _, out _, out totalWeight);
        }

        /// <summary>
        /// Resolves static voxel material properties by VoxelID hash.
        /// </summary>
        public static bool TryFindVoxelMaterialRecord(uint voxelHash, out H8VoxelMaterialRecord record)
        {
            record = default;
            H8VoxelMaterialRecord* records = (H8VoxelMaterialRecord*)GetSectionDataPointer(
                H8DataSectionId.VoxelMaterials,
                UnsafeUtility.SizeOf<H8VoxelMaterialRecord>(),
                out int count);

            if (records == null || voxelHash == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8VoxelMaterialRecord candidate = records[index];
                if (candidate.VoxelHash == voxelHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.VoxelHash < voxelHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Resolves static audio Addressables registry data by EventID hash.
        /// </summary>
        public static bool TryFindAudioClipRecord(uint eventHash, out H8AudioClipRegistryRecord record)
        {
            record = default;
            H8AudioClipRegistryRecord* records = (H8AudioClipRegistryRecord*)GetSectionDataPointer(
                H8DataSectionId.AudioClipRegistry,
                UnsafeUtility.SizeOf<H8AudioClipRegistryRecord>(),
                out int count);

            if (records == null || eventHash == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8AudioClipRegistryRecord candidate = records[index];
                if (candidate.EventHash == eventHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.EventHash < eventHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Reads an audio Addressables key into caller-owned char storage.
        /// </summary>
        public static bool TryReadAudioAddressableKey(uint eventHash, Span<char> destination, out ReadOnlySpan<char> key)
        {
            key = default;
            return TryFindAudioClipRecord(eventHash, out H8AudioClipRegistryRecord record) &&
                   TryReadLocalizedText(record.AddressableKeyUtf8Offset, record.AddressableKeyUtf8ByteLength, destination, out key);
        }

        /// <summary>
        /// Resolves one baked depth-pressure LUT sample by index.
        /// </summary>
        public static bool TryGetDepthPressureSample(int sampleIndex, out H8DepthPressureSampleRecord record)
        {
            record = default;
            H8DepthPressureSampleRecord* records = (H8DepthPressureSampleRecord*)GetSectionDataPointer(
                H8DataSectionId.DepthPressureCurve,
                UnsafeUtility.SizeOf<H8DepthPressureSampleRecord>(),
                out int count);

            if (records == null || count <= 0)
                return false;

            int index = math.clamp(sampleIndex, 0, count - 1);
            record = records[index];
            return true;
        }

        /// <summary>
        /// Resolves the nearest baked pressure sample without runtime pow.
        /// </summary>
        public static bool TrySampleDepthPressure(float depthMeters, out H8DepthPressureSampleRecord record)
        {
            record = default;
            if (!math.isfinite(depthMeters))
                depthMeters = 0f;

            int index = (int)math.round(math.clamp(depthMeters, 0f, 5000f) * (255f / 5000f));
            return TryGetDepthPressureSample(index, out record);
        }

        /// <summary>
        /// Resolves static submarine hull constants by part hash.
        /// </summary>
        public static bool TryFindSubmarineHullConstants(uint partHash, out H8SubmarineHullConstantRecord record)
        {
            record = default;
            H8SubmarineHullConstantRecord* records = (H8SubmarineHullConstantRecord*)GetSectionDataPointer(
                H8DataSectionId.SubmarineHullConstants,
                UnsafeUtility.SizeOf<H8SubmarineHullConstantRecord>(),
                out int count);

            if (records == null || partHash == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8SubmarineHullConstantRecord candidate = records[index];
                if (candidate.PartHash == partHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.PartHash < partHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Resolves static physics material constants by surface hash.
        /// </summary>
        public static bool TryFindPhysicsMaterial(uint surfaceHash, out H8PhysicsMaterialRecord record)
        {
            record = default;
            H8PhysicsMaterialRecord* records = (H8PhysicsMaterialRecord*)GetSectionDataPointer(
                H8DataSectionId.PhysicsMaterials,
                UnsafeUtility.SizeOf<H8PhysicsMaterialRecord>(),
                out int count);

            if (records == null || surfaceHash == 0u)
                return false;

            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                H8PhysicsMaterialRecord candidate = records[index];
                if (candidate.SurfaceHash == surfaceHash)
                {
                    record = candidate;
                    return true;
                }

                if (candidate.SurfaceHash < surfaceHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            return false;
        }

        /// <summary>
        /// Decodes a null-terminated UTF-8 localization entry into caller-owned char storage.
        /// </summary>
        /// <param name="utf8Offset">Offset relative to the localization block.</param>
        /// <param name="destination">Caller-owned char destination.</param>
        /// <param name="text">Span over <paramref name="destination"/> containing decoded text.</param>
        /// <returns>True when text was decoded without allocation.</returns>
        public static bool TryReadLocalizedText(int utf8Offset, Span<char> destination, out ReadOnlySpan<char> text)
        {
            if (utf8Offset < 0)
            {
                text = default;
                return false;
            }

            return TryReadLocalizedText((uint)utf8Offset, destination, out text);
        }

        public static bool TryReadLocalizedText(uint utf8Offset, Span<char> destination, out ReadOnlySpan<char> text)
        {
            text = default;
            if (!IsLoaded ||
                !TryRefreshArenaView(out NativeArray<byte> arena) ||
                utf8Offset == MissingUtf8Offset ||
                destination.Length == 0 ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int offset = (int)utf8Offset;
            int maxBytes = (int)_directory.LocalizationBytes - offset;
            int byteLength = 0;
            while (byteLength < maxBytes && locPtr[offset + byteLength] != 0)
                byteLength++;

            if (byteLength <= 0)
                return false;

            ReadOnlySpan<byte> utf8 = new ReadOnlySpan<byte>(locPtr + offset, byteLength);
            int requiredChars = Encoding.UTF8.GetCharCount(utf8);
            if (requiredChars > destination.Length)
                return false;

            int charsWritten = Encoding.UTF8.GetChars(utf8, destination);
            text = destination.Slice(0, charsWritten);
            return true;
        }

        public static bool TryReadLocalizedText(uint utf8Offset, uint byteLength, Span<char> destination, out ReadOnlySpan<char> text)
        {
            text = default;
            if (byteLength == 0u || byteLength > int.MaxValue || destination.Length == 0)
                return false;

            if (!TryGetLocalizedUtf8Span(utf8Offset, (int)byteLength, out ReadOnlySpan<byte> utf8))
                return false;

            int requiredChars = Encoding.UTF8.GetCharCount(utf8);
            if (requiredChars > destination.Length)
                return false;

            int charsWritten = Encoding.UTF8.GetChars(utf8, destination);
            text = destination.Slice(0, charsWritten);
            return true;
        }

        /// <summary>
        /// Returns the full resident LocData UTF-8 byte block without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Block(out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded || !TryRefreshArenaView(out NativeArray<byte> arena) || _directory.LocalizationBytes == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            utf8Bytes = new ReadOnlySpan<byte>(basePtr + _directory.LocalizationOffset, (int)_directory.LocalizationBytes);
            return true;
        }

        /// <summary>
        /// Returns a bounded UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, int byteLength, out ReadOnlySpan<byte> utf8Bytes)
        {
            if (utf8Offset < 0)
            {
                utf8Bytes = default;
                return false;
            }

            return TryGetLocalizedUtf8Span((uint)utf8Offset, byteLength, out utf8Bytes);
        }

        public static bool TryGetLocalizedUtf8Span(uint utf8Offset, int byteLength, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded ||
                !TryRefreshArenaView(out NativeArray<byte> arena) ||
                utf8Offset == MissingUtf8Offset ||
                byteLength < 0 ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes ||
                (uint)byteLength > _directory.LocalizationBytes - utf8Offset)
            {
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            utf8Bytes = new ReadOnlySpan<byte>(locPtr + (int)utf8Offset, byteLength);
            return true;
        }

        /// <summary>
        /// Returns one null-terminated UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, out ReadOnlySpan<byte> utf8Bytes)
        {
            if (utf8Offset < 0)
            {
                utf8Bytes = default;
                return false;
            }

            return TryGetLocalizedUtf8Span((uint)utf8Offset, out utf8Bytes);
        }

        public static bool TryGetLocalizedUtf8Span(uint utf8Offset, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded ||
                !TryRefreshArenaView(out NativeArray<byte> arena) ||
                utf8Offset == MissingUtf8Offset ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if (utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int offset = (int)utf8Offset;
            int maxBytes = (int)_directory.LocalizationBytes - offset;
            int byteLength = 0;
            while (byteLength < maxBytes && locPtr[offset + byteLength] != 0)
                byteLength++;

            if (byteLength <= 0)
                return false;

            utf8Bytes = new ReadOnlySpan<byte>(locPtr + offset, byteLength);
            return true;
        }

        /// <summary>
        /// Counts primary static-data hash aliases that resolve to LocData slices.
        /// </summary>
        public static int GetStaticLocalizationReferenceCount()
        {
            if (!IsLoaded || !TryRefreshArenaView(out _) || _directory.LocalizationBytes == 0)
                return 0;

            int count = 0;
            H8StaticLocalizationCursor cursor = default;
            while (TryGetNextStaticLocalizationReference(ref cursor, out _))
            {
                if (count == int.MaxValue)
                    return count;

                count++;
            }

            return count;
        }

        /// <summary>
        /// Advances a caller-owned cursor over static-data hash aliases without rescanning prior records.
        /// </summary>
        public static bool TryGetNextStaticLocalizationReference(
            ref H8StaticLocalizationCursor cursor,
            out H8StaticLocalizationReference reference)
        {
            reference = default;
            if (!IsLoaded || !TryRefreshArenaView(out _) || _directory.LocalizationBytes == 0)
                return false;

            if (cursor.Section < 0)
                cursor.Section = 0;

            if (cursor.RecordIndex < 0)
                cursor.RecordIndex = 0;

            while (cursor.Section < StaticLocalizationSectionCount)
            {
                bool found;
                switch (cursor.Section)
                {
                    case StaticLocalizationItemsSection:
                        found = TryGetNextItemLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationCreaturesSection:
                        found = TryGetNextCreatureLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationBiomesSection:
                        found = TryGetNextBiomeLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationGhostModulesSection:
                        found = TryGetNextGhostModuleLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    case StaticLocalizationSopErrorsSection:
                        found = TryGetNextSopErrorLocalizationReference(ref cursor.RecordIndex, out reference);
                        break;

                    default:
                        return false;
                }

                if (found)
                    return true;

                cursor.Section++;
                cursor.RecordIndex = 0;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextItemLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            H8ItemRecord* records = (H8ItemRecord*)GetSectionDataPointer(
                H8DataSectionId.Items,
                H8DataLayoutConstants.ItemRecordSize,
                out int recordCount);

            if (records == null || recordCount <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < recordCount)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].HashId, records[index].NameUtf8Offset, records[index].NameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextCreatureLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            H8CreatureTraitRecord* records = (H8CreatureTraitRecord*)GetSectionDataPointer(
                H8DataSectionId.Creatures,
                H8DataLayoutConstants.CreatureTraitRecordSize,
                out int recordCount);

            if (records == null || recordCount <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < recordCount)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].SpeciesHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextBiomeLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            H8BiomeRecord* records = (H8BiomeRecord*)GetSectionDataPointer(
                H8DataSectionId.Biomes,
                H8DataLayoutConstants.BiomeRecordSize,
                out int recordCount);

            if (records == null || recordCount <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < recordCount)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].BiomeHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextGhostModuleLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            H8GhostModuleRecord* records = (H8GhostModuleRecord*)GetSectionDataPointer(
                H8DataSectionId.GhostModules,
                UnsafeUtility.SizeOf<H8GhostModuleRecord>(),
                out int recordCount);

            if (records == null || recordCount <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < recordCount)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].ModuleHash, records[index].DisplayNameUtf8Offset, records[index].DisplayNameUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryGetNextSopErrorLocalizationReference(
            ref int recordIndex,
            out H8StaticLocalizationReference reference)
        {
            H8SopErrorRecord* records = (H8SopErrorRecord*)GetSectionDataPointer(
                H8DataSectionId.SopErrors,
                UnsafeUtility.SizeOf<H8SopErrorRecord>(),
                out int recordCount);

            if (records == null || recordCount <= 0)
            {
                reference = default;
                return false;
            }

            while (recordIndex < recordCount)
            {
                int index = recordIndex++;
                if (TryBuildStaticLocalizationReference(records[index].ErrorHash, records[index].MessageUtf8Offset, records[index].MessageUtf8ByteLength, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryBuildStaticLocalizationReference(
            uint keyHash,
            uint utf8Offset,
            uint byteLength,
            out H8StaticLocalizationReference reference)
        {
            reference = default;
            if (keyHash == 0u ||
                byteLength == 0u ||
                byteLength > int.MaxValue ||
                !TryGetLocalizedUtf8Span(utf8Offset, (int)byteLength, out _))
            {
                return false;
            }

            reference = new H8StaticLocalizationReference
            {
                KeyHash = keyHash,
                Utf8Offset = utf8Offset,
                ByteLength = (int)byteLength
            };
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only live balancing hook. It temporarily opens the write lock, reloads the blob, then restores Ready lock.
        /// </summary>
        public static bool EditorHotReloadFromFile(string absolutePath, out H8DataBlobLoadStatus status)
        {
            Interlocked.Exchange(ref _writeLocked, 0);
            bool loaded = TryInitializeFromFile(absolutePath, 0u, 0u, false, out status);
            LockReady();
            return loaded;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ComputeResidentPayloadHash64()
        {
            if (!TryRefreshArenaView(out NativeArray<byte> arena) || arena.Length <= H8DataLayoutConstants.HeaderSizeBytes)
                return 0UL;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            uint2 hash = xxHash3.Hash64(
                basePtr + H8DataLayoutConstants.HeaderSizeBytes,
                _residentBlobBytes - H8DataLayoutConstants.HeaderSizeBytes);
            return ((ulong)hash.y << 32) | hash.x;
        }

        private static bool TryReadWholeFileIntoArena(string absolutePath, int expectedBytes, uint inheritedPathFlags, out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            long readStart = Stopwatch.GetTimestamp();
            uint pathFlags = PathFlagVaultBacked | inheritedPathFlags;
            _lastReadTicks = 0L;
            _lastReadPathFlags = pathFlags;
            try
            {
                if (!TryRefreshArenaView(out NativeArray<byte> arena) || arena.Length < expectedBytes)
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                    return false;
                }

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(arena);
#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
                if (TryReadViaMemoryMappedFile(absolutePath, destination, arena.Length, expectedBytes))
                {
                    long mmfElapsedTicks = Stopwatch.GetTimestamp() - readStart;
                    _lastReadTicks = mmfElapsedTicks;
                    _lastReadPathFlags = pathFlags | PathFlagMemoryMappedFile;
                    return true;
                }
#endif
                using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                Span<byte> destinationBytes = new Span<byte>(destination, expectedBytes);
                int totalRead = 0;
                while (totalRead < expectedBytes)
                {
                    int read = stream.Read(destinationBytes.Slice(totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                bool ok = totalRead == expectedBytes && stream.Length == expectedBytes;
                status = ok ? H8DataBlobLoadStatus.None : H8DataBlobLoadStatus.ReadFailed;
                long streamElapsedTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadTicks = streamElapsedTicks;
                _lastReadPathFlags = pathFlags | PathFlagFileStream;
                return ok;
            }
            catch (Exception)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                _lastReadTicks = Stopwatch.GetTimestamp() - readStart;
                _lastReadPathFlags = pathFlags;
                return false;
            }
        }

        private static bool ExceedsTelemetryDumpThreshold(long ticks)
        {
            return ticks > Stopwatch.Frequency / 20L;
        }

#if !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS
        private static bool TryReadViaMemoryMappedFile(string absolutePath, byte* destination, int destinationBytes, int expectedBytes)
        {
            if (destination == null || expectedBytes <= 0)
                return false;

            try
            {
                using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(absolutePath, FileMode.Open, null, expectedBytes, MemoryMappedFileAccess.Read);
                using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, expectedBytes, MemoryMappedFileAccess.Read);
                byte* source = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref source);
                try
                {
                    source += (int)accessor.PointerOffset;
                    return UnsafeMemoryCopyGuard.TryMemCpy(destination, destinationBytes, source, expectedBytes);
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
#endif

        private static bool TryValidateResidentArena(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            if (!BitConverter.IsLittleEndian)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (!TryRefreshArenaView(out NativeArray<byte> arena) || _residentBlobBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            _header = UnsafeUtility.ReadArrayElement<H8DataBlobHeader>(basePtr, 0);
            _directory = UnsafeUtility.ReadArrayElement<H8DataBlobDirectory>(basePtr + H8DataLayoutConstants.HeaderSizeBytes, 0);

            if (_header.Magic != H8DataLayoutConstants.BlobMagic ||
                _directory.Magic != H8DataLayoutConstants.BlobMagic)
            {
                status = H8DataBlobLoadStatus.BadMagic;
                return false;
            }

            if (_header.FormatVersion != H8DataLayoutConstants.FormatVersion ||
                _directory.FormatVersion != H8DataLayoutConstants.FormatVersion)
            {
                status = H8DataBlobLoadStatus.UnsupportedVersion;
                return false;
            }

            if (_header.HeaderBytes != H8DataLayoutConstants.HeaderSizeMarker)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (_directory.BlobBytes != _residentBlobBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            if (expectedWorldSeed != 0u && _directory.WorldSeed != 0u && _directory.WorldSeed != expectedWorldSeed)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (expectedAppVersionHash != 0u && _directory.AppVersionHash != 0u && _directory.AppVersionHash != expectedAppVersionHash)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            ulong computedHash = ComputeResidentPayloadHash64();
            if (computedHash != _header.Checksum64)
            {
                status = H8DataBlobLoadStatus.BadChecksum;
                return false;
            }

            if (!IsDirectoryValid())
            {
                status = H8DataBlobLoadStatus.InvalidSectionTable;
                return false;
            }

            status = H8DataBlobLoadStatus.Loaded;
            return true;
        }

        private static bool IsDirectoryValid()
        {
            const ushort ExpectedSectionCount = (ushort)H8DataSectionId.PhysicsConstants;
            if (_directory.SectionCount != ExpectedSectionCount)
                return false;

            if (_directory.SectionTableOffset < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
                return false;

            if (_directory.SectionTableBytes != (uint)(_directory.SectionCount * UnsafeUtility.SizeOf<H8DataSectionEntry>()))
                return false;

            if (_directory.SectionTableOffset > _directory.BlobBytes ||
                _directory.SectionTableBytes > _directory.BlobBytes - _directory.SectionTableOffset)
            {
                return false;
            }

            uint expectedDataStart = _directory.SectionTableOffset + _directory.SectionTableBytes;
            if (_directory.DataStartOffset != expectedDataStart ||
                (_directory.DataStartOffset & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
            {
                return false;
            }

            if (_directory.LocalizationBytes > 0u &&
                (_directory.LocalizationOffset > _directory.BlobBytes ||
                 _directory.LocalizationBytes > _directory.BlobBytes - _directory.LocalizationOffset))
            {
                return false;
            }

            if (!TryRefreshArenaView(out NativeArray<byte> arena))
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(arena);
            H8DataSectionEntry* entries = (H8DataSectionEntry*)(basePtr + _directory.SectionTableOffset);
            bool sawLocalization = false;
            for (int i = 0; i < _directory.SectionCount; i++)
            {
                H8DataSectionEntry section = entries[i];
                uint expectedSectionId = (uint)(i + 1);
                if (section.SectionId != expectedSectionId)
                    return false;

                H8DataSectionId sectionId = (H8DataSectionId)section.SectionId;
                uint expectedRecordSize = H8DataLayoutAudit.GetExpectedRecordSize(sectionId);
                if (expectedRecordSize == 0u || section.RecordSize != expectedRecordSize)
                    return false;

                if (!IsSectionRangeValid(in section))
                    return false;

                if (sectionId == H8DataSectionId.LocalizationUtf8)
                {
                    sawLocalization = true;
                    if (_directory.LocalizationOffset != section.OffsetBytes ||
                        _directory.LocalizationBytes != section.Count)
                    {
                        return false;
                    }
                }
            }

            return _directory.LocalizationBytes == 0u || sawLocalization;
        }

        private static bool IsSectionRangeValid(in H8DataSectionEntry section)
        {
            if (section.RecordSize == 0u)
                return section.Count == 0u && section.OffsetBytes == 0u;

            if (section.Count == 0u)
                return section.OffsetBytes == 0u;

            if ((section.OffsetBytes & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
                return false;

            ulong byteCount = (ulong)section.RecordSize * section.Count;
            return section.OffsetBytes >= _directory.DataStartOffset &&
                   section.OffsetBytes <= _directory.BlobBytes &&
                   byteCount <= _directory.BlobBytes - section.OffsetBytes;
        }

        private static bool TryAllocateArena(int blobBytes)
        {
            int capacity = ComputeArenaCapacity(blobBytes);
            _vault = GlobalRegistry.DataVault;
            _arenaHandle = default;

            if (_vault != null)
            {
                _arenaHandle = _vault.GetGenerationHandle<byte>(
                    DataMonolithPayloadBufferId,
                    capacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.UninitializedMemory);

                if (_vault.TryResolveHandle(in _arenaHandle, out NativeArray<byte> arena) &&
                    arena.IsCreated &&
                    arena.Length >= capacity)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryRefreshArenaView(out NativeArray<byte> arena)
        {
            arena = default;
            IDataVault vault = _vault ?? GlobalRegistry.DataVault;
            if (vault == null || _arenaHandle.BufferID == 0u)
                return false;

            _vault = vault;
            return vault.TryResolveHandle(in _arenaHandle, out arena) && arena.IsCreated;
        }

        private static bool EnsureTelemetry()
        {
            IDataVault vault = _vault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!ReferenceEquals(_vault, vault))
            {
                _vault = vault;
                _telemetryHandle = default;
                _telemetryCursorHandle = default;
            }

            if (_telemetryHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _telemetryHandle, out NativeArray<H8DataMonolithTelemetryEntry> ring) ||
                !ring.IsCreated ||
                ring.Length < H8DataLayoutConstants.TelemetryRingCapacity)
            {
                _telemetryHandle = vault.GetGenerationHandle<H8DataMonolithTelemetryEntry>(
                    DataMonolithTelemetryRingBufferId,
                    H8DataLayoutConstants.TelemetryRingCapacity,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            if (_telemetryCursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> cursor) ||
                !cursor.IsCreated ||
                cursor.Length < 1)
            {
                _telemetryCursorHandle = vault.GetGenerationHandle<int>(
                    DataMonolithTelemetryCursorBufferId,
                    1,
                    SystemID.CoreDataVault,
                    NativeArrayOptions.ClearMemory);
            }

            return TryResolveTelemetry(out _, out _);
        }

        private static bool TryResolveTelemetry(
            out NativeArray<H8DataMonolithTelemetryEntry> ring,
            out NativeArray<int> cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _vault;
            return vault != null &&
                   _telemetryHandle.BufferID != 0u &&
                   _telemetryCursorHandle.BufferID != 0u &&
                   vault.TryResolveHandle(in _telemetryHandle, out ring) &&
                   ring.IsCreated &&
                   ring.Length >= H8DataLayoutConstants.TelemetryRingCapacity &&
                   vault.TryResolveHandle(in _telemetryCursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length >= 1;
        }

        private static void RecordTelemetry(H8DataBlobLoadStatus status, long loadTicks, long ioTicks, uint pathFlags)
        {
            if (!EnsureTelemetry() ||
                !TryResolveTelemetry(out NativeArray<H8DataMonolithTelemetryEntry> ring, out NativeArray<int> cursor))
            {
                return;
            }

            int index = cursor[0];
            if ((uint)index >= H8DataLayoutConstants.TelemetryRingCapacity)
                index = 0;

            uint stateHash = ((uint)_residentBlobBytes * H8DataHash.Fnv1A32Prime) ^
                             ((uint)_directory.SectionCount << 16) ^
                             (uint)status;
            ring[index] = new H8DataMonolithTelemetryEntry
            {
                Checksum64 = _header.Checksum64,
                LoadTicks = loadTicks,
                IoTicks = ioTicks,
                FrameIndex = (uint)Interlocked.Increment(ref _telemetryFrame),
                BlobBytes = (uint)Math.Max(0, _residentBlobBytes),
                SectionCount = _directory.SectionCount,
                LoadStatus = (uint)status,
                PathFlags = pathFlags,
                StateHash = stateHash
            };

            index++;
            if (index >= H8DataLayoutConstants.TelemetryRingCapacity)
                index = 0;
            cursor[0] = index;
        }

        private static void DumpTelemetry(H8DataBlobLoadStatus status)
        {
            if (!EnsureTelemetry() ||
                !TryResolveTelemetry(out NativeArray<H8DataMonolithTelemetryEntry> ring, out NativeArray<int> cursor))
            {
                return;
            }

            try
            {
                string folder = System.IO.Path.GetFullPath("Docs/AgentLogs");
                System.IO.Directory.CreateDirectory(folder);
                WriteTelemetryDump(System.IO.Path.Combine(folder, "Dump_SHINOBU_103.bin"), status, ring, cursor[0]);
                WriteTelemetryDump(System.IO.Path.Combine(folder, "Dump_DATA_MONOLITH.bin"), status, ring, cursor[0]);
            }
            catch (Exception)
            {
            }
        }

        private static void WriteTelemetryDump(
            string path,
            H8DataBlobLoadStatus status,
            NativeArray<H8DataMonolithTelemetryEntry> ring,
            int cursor)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(0x4858444Du);
            writer.Write((uint)status);
            writer.Write(cursor);
            writer.Write(H8DataLayoutConstants.TelemetryRingCapacity);
            writer.Write(UnsafeUtility.SizeOf<H8DataMonolithTelemetryEntry>());
            for (int i = 0; i < H8DataLayoutConstants.TelemetryRingCapacity; i++)
            {
                H8DataMonolithTelemetryEntry entry = ring[i];
                writer.Write(entry.Checksum64);
                writer.Write(entry.LoadTicks);
                writer.Write(entry.IoTicks);
                writer.Write(entry.FrameIndex);
                writer.Write(entry.BlobBytes);
                writer.Write(entry.SectionCount);
                writer.Write(entry.LoadStatus);
                writer.Write(entry.PathFlags);
                writer.Write(entry.StateHash);
                writer.Write(entry.Reserved0);
                writer.Write(entry.Reserved1);
                writer.Write(entry.Reserved2);
                writer.Write(entry.Reserved3);
            }
        }

        private static bool TryFindLootTableRange(uint tableHash, out int start, out int end, out uint totalWeight)
        {
            start = 0;
            end = 0;
            totalWeight = 0u;
            H8LootCdfRecord* records = (H8LootCdfRecord*)GetSectionDataPointer(
                H8DataSectionId.LootCdf,
                UnsafeUtility.SizeOf<H8LootCdfRecord>(),
                out int count);

            if (records == null || count <= 0)
                return false;

            int low = 0;
            int high = count;
            while (low < high)
            {
                int index = low + ((high - low) >> 1);
                if (records[index].TableHash < tableHash)
                    low = index + 1;
                else
                    high = index;
            }

            start = low;
            if (start >= count || records[start].TableHash != tableHash)
                return false;

            low = start + 1;
            high = count;
            while (low < high)
            {
                int index = low + ((high - low) >> 1);
                if (records[index].TableHash <= tableHash)
                    low = index + 1;
                else
                    high = index;
            }

            end = low;
            totalWeight = records[end - 1].TotalWeight;
            return end > start;
        }

        private static int ComputeArenaCapacity(int blobBytes)
        {
            int minimum = H8DataLayoutConstants.DefaultArenaCapacityBytes;
            if (blobBytes <= minimum)
                return minimum;

            return (blobBytes + (H8DataLayoutConstants.SectionAlignmentBytes - 1)) &
                   ~(H8DataLayoutConstants.SectionAlignmentBytes - 1);
        }

        private static void ShutdownArenaOnly()
        {
            Volatile.Write(ref _loaded, 0);
            _header = default;
            _directory = default;
            _residentBlobBytes = 0;

            IDataVault vault = _vault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _arenaHandle);
                ReleaseVaultHandle(vault, ref _telemetryHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            }

            _arenaHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _vault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }
    }
}
