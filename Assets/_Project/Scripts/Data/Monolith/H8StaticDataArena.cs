using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Data
{
    /// <summary>
    /// Boot-owned static data arena for monolithic baked content blobs.
    /// </summary>
    public static unsafe class H8StaticDataArena
    {
        private const long MaxBlobBytes = 256L * 1024L * 1024L;
        private const int MissingUtf8Offset = -1;
        private const int StaticLocalizationItemsSection = 0;
        private const int StaticLocalizationCreaturesSection = 1;
        private const int StaticLocalizationBiomesSection = 2;
        private const int StaticLocalizationGhostModulesSection = 3;
        private const int StaticLocalizationSopErrorsSection = 4;
        private const int StaticLocalizationSectionCount = 5;

        private static NativeArray<byte> _arena;
        private static H8DataBlobHeader _header;
        private static H8DataBlobDirectory _directory;
        private static int _residentBlobBytes;
        private static int _loaded;
        private static int _writeLocked;

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
            string absolutePath = Path.Combine(
                Application.streamingAssetsPath,
                H8DataLayoutConstants.DefaultStreamingAssetsRelativePath);

            return TryInitializeFromFile(
                absolutePath,
                expectedWorldSeed,
                expectedAppVersionHash,
                failIfMissing,
                out status);
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
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                status = H8DataBlobLoadStatus.Missing;
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
                return false;
            }

            if (info.Length > MaxBlobBytes || info.Length > int.MaxValue)
            {
                status = H8DataBlobLoadStatus.FileTooLarge;
                return false;
            }

            int blobBytes = (int)info.Length;
            ShutdownArenaOnly();
            _arena = new NativeArray<byte>(
                ComputeArenaCapacity(blobBytes),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[>=10MB static reserve] - static data monolith arena - owner: H8StaticDataArena
            NativeMemorySentinel.RegisterNativeArray(
                _arena,
                nameof(H8StaticDataArena),
                nameof(_arena),
                NativeAllocationLifetime.Session);

            _residentBlobBytes = blobBytes;
            if (!TryReadWholeFileIntoArena(absolutePath, blobBytes, out status))
            {
                ShutdownArenaOnly();
                return false;
            }

            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
            return true;
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
            _arena = new NativeArray<byte>(
                ComputeArenaCapacity(sourceBytes),
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[>=10MB static reserve] - static data monolith memory copy target - owner: H8StaticDataArena
            NativeMemorySentinel.RegisterNativeArray(
                _arena,
                nameof(H8StaticDataArena),
                nameof(_arena),
                NativeAllocationLifetime.Session);

            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_arena);
            _residentBlobBytes = sourceBytes;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, sourceBytes, source, sourceBytes))
            {
                ShutdownArenaOnly();
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }

            if (!TryValidateResidentArena(expectedWorldSeed, expectedAppVersionHash, out status))
            {
                ShutdownArenaOnly();
                return false;
            }

            Volatile.Write(ref _loaded, 1);
            LockReady();
            status = H8DataBlobLoadStatus.Loaded;
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
            if (!IsLoaded || !_arena.IsCreated || _directory.SectionCount == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
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

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
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

        /// <summary>
        /// Provides the resident blob as a read-only native array for Burst jobs.
        /// </summary>
        /// <param name="arena">Resident arena.</param>
        /// <returns>True when a blob is loaded.</returns>
        public static bool TryGetArena(out NativeArray<byte> arena)
        {
            arena = _arena;
            return IsLoaded && _arena.IsCreated;
        }

        /// <summary>
        /// Provides the resident blob and the valid byte count inside the larger static arena.
        /// </summary>
        public static bool TryGetResidentBlob(out NativeArray<byte> arena, out int blobBytes)
        {
            arena = _arena;
            blobBytes = _residentBlobBytes;
            return IsLoaded && _arena.IsCreated && _residentBlobBytes > 0;
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
                   TryReadLocalizedText(record.AddressableKeyUtf8Offset, destination, out key);
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
            text = default;
            if (!IsLoaded || !_arena.IsCreated || utf8Offset == MissingUtf8Offset || destination.Length == 0 || _directory.LocalizationBytes == 0)
                return false;

            if ((uint)utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int maxBytes = (int)_directory.LocalizationBytes - utf8Offset;
            int byteLength = 0;
            while (byteLength < maxBytes && locPtr[utf8Offset + byteLength] != 0)
                byteLength++;

            if (byteLength <= 0)
                return false;

            ReadOnlySpan<byte> utf8 = new ReadOnlySpan<byte>(locPtr + utf8Offset, byteLength);
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
            if (!IsLoaded || !_arena.IsCreated || _directory.LocalizationBytes == 0)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            utf8Bytes = new ReadOnlySpan<byte>(basePtr + _directory.LocalizationOffset, (int)_directory.LocalizationBytes);
            return true;
        }

        /// <summary>
        /// Returns a bounded UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, int byteLength, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded ||
                !_arena.IsCreated ||
                utf8Offset == MissingUtf8Offset ||
                byteLength < 0 ||
                _directory.LocalizationBytes == 0)
            {
                return false;
            }

            if ((uint)utf8Offset >= _directory.LocalizationBytes ||
                (uint)byteLength > _directory.LocalizationBytes - (uint)utf8Offset)
            {
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            utf8Bytes = new ReadOnlySpan<byte>(locPtr + utf8Offset, byteLength);
            return true;
        }

        /// <summary>
        /// Returns one null-terminated UTF-8 localization slice without decoding or allocation.
        /// </summary>
        public static bool TryGetLocalizedUtf8Span(int utf8Offset, out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = default;
            if (!IsLoaded || !_arena.IsCreated || utf8Offset == MissingUtf8Offset || _directory.LocalizationBytes == 0)
                return false;

            if ((uint)utf8Offset >= _directory.LocalizationBytes)
                return false;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            byte* locPtr = basePtr + _directory.LocalizationOffset;
            int maxBytes = (int)_directory.LocalizationBytes - utf8Offset;
            int byteLength = 0;
            while (byteLength < maxBytes && locPtr[utf8Offset + byteLength] != 0)
                byteLength++;

            if (byteLength <= 0)
                return false;

            utf8Bytes = new ReadOnlySpan<byte>(locPtr + utf8Offset, byteLength);
            return true;
        }

        /// <summary>
        /// Counts primary static-data hash aliases that resolve to LocData slices.
        /// </summary>
        public static int GetStaticLocalizationReferenceCount()
        {
            if (!IsLoaded || !_arena.IsCreated || _directory.LocalizationBytes == 0)
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
            if (!IsLoaded || !_arena.IsCreated || _directory.LocalizationBytes == 0)
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
                if (TryBuildStaticLocalizationReference(records[index].HashId, records[index].NameUtf8Offset, out reference))
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
                if (TryBuildStaticLocalizationReference(records[index].SpeciesHash, records[index].DisplayNameUtf8Offset, out reference))
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
                if (TryBuildStaticLocalizationReference(records[index].BiomeHash, records[index].DisplayNameUtf8Offset, out reference))
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
                if (TryBuildStaticLocalizationReference(records[index].ModuleHash, records[index].DisplayNameUtf8Offset, out reference))
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
                if (TryBuildStaticLocalizationReference(records[index].ErrorHash, records[index].MessageUtf8Offset, out reference))
                    return true;
            }

            reference = default;
            return false;
        }

        private static bool TryBuildStaticLocalizationReference(
            uint keyHash,
            int utf8Offset,
            out H8StaticLocalizationReference reference)
        {
            reference = default;
            if (keyHash == 0u ||
                !TryGetLocalizedUtf8Span(utf8Offset, out ReadOnlySpan<byte> utf8Bytes) ||
                utf8Bytes.Length <= 0)
            {
                return false;
            }

            reference = new H8StaticLocalizationReference
            {
                KeyHash = keyHash,
                Utf8Offset = utf8Offset,
                ByteLength = utf8Bytes.Length
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
            if (!_arena.IsCreated || _arena.Length <= H8DataLayoutConstants.HeaderSizeBytes)
                return 0UL;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            uint2 hash = xxHash3.Hash64(
                basePtr + H8DataLayoutConstants.HeaderSizeBytes,
                _residentBlobBytes - H8DataLayoutConstants.HeaderSizeBytes);
            return ((ulong)hash.y << 32) | hash.x;
        }

        private static bool TryReadWholeFileIntoArena(string absolutePath, int expectedBytes, out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            try
            {
                byte[] source = File.ReadAllBytes(absolutePath); // COLD ALLOC: byte[file bytes] - boot-only single I/O staging before native blit - owner: H8StaticDataArena
                if (source.Length != expectedBytes)
                {
                    status = H8DataBlobLoadStatus.ReadFailed;
                    return false;
                }

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_arena);
                fixed (byte* sourcePtr = source)
                {
                    if (!UnsafeMemoryCopyGuard.TryMemCpy(destination, _arena.Length, sourcePtr, source.Length))
                    {
                        status = H8DataBlobLoadStatus.ReadFailed;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                status = H8DataBlobLoadStatus.ReadFailed;
                return false;
            }
        }

        private static bool TryValidateResidentArena(
            uint expectedWorldSeed,
            uint expectedAppVersionHash,
            out H8DataBlobLoadStatus status)
        {
            status = H8DataBlobLoadStatus.None;
            if (!_arena.IsCreated || _residentBlobBytes < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            _header = UnsafeUtility.ReadArrayElement<H8DataBlobHeader>(basePtr, 0);
            _directory = UnsafeUtility.ReadArrayElement<H8DataBlobDirectory>(basePtr + H8DataLayoutConstants.HeaderSizeBytes, 0);

            if (_directory.Magic != H8DataLayoutConstants.BlobMagic)
            {
                status = H8DataBlobLoadStatus.BadMagic;
                return false;
            }

            if (_directory.FormatVersion != H8DataLayoutConstants.FormatVersion)
            {
                status = H8DataBlobLoadStatus.UnsupportedVersion;
                return false;
            }

            if (_directory.BlobBytes != _residentBlobBytes)
            {
                status = H8DataBlobLoadStatus.FileTooSmall;
                return false;
            }

            if (expectedWorldSeed != 0u && _header.WorldSeed != 0u && _header.WorldSeed != expectedWorldSeed)
            {
                status = H8DataBlobLoadStatus.HeaderMismatch;
                return false;
            }

            if (expectedAppVersionHash != 0u && _header.AppVersionHash != 0u && _header.AppVersionHash != expectedAppVersionHash)
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
            if (_directory.SectionCount == 0)
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

            if (_directory.LocalizationBytes > 0u &&
                (_directory.LocalizationOffset > _directory.BlobBytes ||
                 _directory.LocalizationBytes > _directory.BlobBytes - _directory.LocalizationOffset))
            {
                return false;
            }

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_arena);
            H8DataSectionEntry* entries = (H8DataSectionEntry*)(basePtr + _directory.SectionTableOffset);
            for (int i = 0; i < _directory.SectionCount; i++)
            {
                H8DataSectionEntry section = entries[i];
                if (!IsSectionRangeValid(in section))
                    return false;
            }

            return true;
        }

        private static bool IsSectionRangeValid(in H8DataSectionEntry section)
        {
            if (section.RecordSize == 0u)
                return section.Count == 0u && section.OffsetBytes == 0u;

            if (section.Count == 0u)
                return true;

            if ((section.OffsetBytes & (H8DataLayoutConstants.SectionAlignmentBytes - 1u)) != 0u)
                return false;

            ulong byteCount = (ulong)section.RecordSize * section.Count;
            return section.OffsetBytes <= _directory.BlobBytes &&
                   byteCount <= _directory.BlobBytes - section.OffsetBytes;
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
            if (!_arena.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_arena);
            _arena.Dispose();
            _arena = default;
        }
    }
}
