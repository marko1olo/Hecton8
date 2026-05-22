using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.SaveSystem
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EntityDeltaHeaderDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint CompressedSize;
        [FieldOffset(12)] public uint UncompressedSize;
        [FieldOffset(16)] public ulong XXHash3Checksum;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EntityDeltaRleStreamHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint DenseBytes;
        [FieldOffset(12)] public uint StoredBytes;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct EntityDeltaDataRecordDTO
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public uint EntityKindHash;
        [FieldOffset(40)] public ulong StableEntityHash;
        [FieldOffset(48)] public uint ArchetypeHash;
        [FieldOffset(52)] public uint InventoryHash;
        [FieldOffset(56)] public uint InstanceUid;
        [FieldOffset(60)] public ushort Quantity;
        [FieldOffset(62)] public ushort HealthMilli;
        [FieldOffset(64)] public ushort HungerMilli;
        [FieldOffset(66)] public ushort IntegrityMilli;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public uint BaselineHash32;
        [FieldOffset(76)] public uint SimulationTick;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EntityDeltaBlockCounter64
    {
        [FieldOffset(0)] public uint DeltaCount;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint TombstoneCount;
        [FieldOffset(12)] public uint EncodedBytes;
        [FieldOffset(16)] public ulong SectorHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint PrunedTombstones;
        [FieldOffset(32)] public ulong HashXor;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityCompressionTelemetryEntry
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ulong PayloadHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint FullSnapshotBytes;
        [FieldOffset(24)] public uint DenseDeltaBytes;
        [FieldOffset(28)] public uint RleBytes;
        [FieldOffset(32)] public uint CompressedBytes;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float BurstTimeMs;
        [FieldOffset(44)] public float DiskWriteLatencyMs;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public float CompressionEffort01;
        [FieldOffset(56)] public uint IoPressureMicro;
        [FieldOffset(60)] public uint DeltaEntityCount;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityDeltaCompressionTuningDTO
    {
        [FieldOffset(0)] public ulong ProfileHash;
        [FieldOffset(8)] public uint SchemaHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float TombstoneMaxDays;
        [FieldOffset(20)] public float Lz4MinEffort01;
        [FieldOffset(24)] public float Lz4MaxEffort01;
        [FieldOffset(28)] public float LowQualityWriteHz;
        [FieldOffset(32)] public float HighQualityWriteHz;
        [FieldOffset(36)] public float IoPressureBias01;
        [FieldOffset(40)] public float MaxWalWriteMillis;
        [FieldOffset(44)] public uint MaxBytesPerFrame;
        [FieldOffset(48)] public float MockMutationRate01;
        [FieldOffset(52)] public float RleMinSaving01;
        [FieldOffset(56)] public uint DehydrationFadeTicks;
        [FieldOffset(60)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityDeltaSectorStatsDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public int SectorX;
        [FieldOffset(12)] public int SectorY;
        [FieldOffset(16)] public int SectorZ;
        [FieldOffset(20)] public uint FullSnapshotBytes;
        [FieldOffset(24)] public uint DenseDeltaBytes;
        [FieldOffset(28)] public uint RleBytes;
        [FieldOffset(32)] public uint CompressedBytes;
        [FieldOffset(36)] public uint DeltaEntities;
        [FieldOffset(40)] public uint ActiveEntities;
        [FieldOffset(44)] public float CompressionRatio01;
        [FieldOffset(48)] public float DeltaRatio01;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EntityCompressionProfileDTO
    {
        [FieldOffset(0)] public ulong ProfileHash;
        [FieldOffset(8)] public uint EntityKindHash;
        [FieldOffset(12)] public float Fidelity01;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public ushort HealthDeltaMilli;
        [FieldOffset(22)] public ushort InventoryDeltaMask;
        [FieldOffset(24)] public uint StateMask;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EntityDeltaMockSchemaDTO
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong SchemaHash;
        [FieldOffset(16)] public uint Version;
        [FieldOffset(20)] public uint HeaderBytes;
        [FieldOffset(24)] public uint RecordBytes;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint DefaultEntityCapacity;
        [FieldOffset(36)] public uint DefaultSectorMeters;
        [FieldOffset(40)] public ulong Seed;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EntityDeltaTelemetryDumpHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint EntryStride;
        [FieldOffset(16)] public uint Cursor;
        [FieldOffset(20)] public uint ReasonFlags;
        [FieldOffset(24)] public uint RingCapacity;
        [FieldOffset(28)] public uint HeaderBytes;
        [FieldOffset(32)] public ulong FirstSectorHash;
        [FieldOffset(40)] public ulong LastSectorHash;
        [FieldOffset(48)] public uint FirstFrame;
        [FieldOffset(52)] public uint LastFrame;
        [FieldOffset(56)] public ulong _pad0;
    }

    public struct EntityDeltaCompressionVaultBufferSet
    {
        public NativeArray<byte> SchemaBytes;
        public NativeArray<EntityDeltaDataRecordDTO> CurrentRecords;
        public NativeArray<EntityDeltaDataRecordDTO> BaselineRecords;
        public NativeArray<EntityDeltaDataRecordDTO> DeltaRecords;
        internal NativeArray<EntityDeltaBlockCounter64> BlockCounters;
        public NativeArray<byte> DenseBytes;
        public NativeArray<byte> RleBytes;
        public NativeArray<byte> CompressedBytes;
        public NativeArray<byte> WalPayloadBytes;
        public NativeArray<int> Lz4HashTable;
        internal NativeArray<EntityDeltaHeaderDTO> Headers;
        public NativeArray<int> Counters;
        public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<EntityDeltaCompressionTuningDTO> Tuning;
        public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
        public NativeArray<byte> CsvScratch;
        public NativeArray<EntityCompressionProfileDTO> Profiles;
    }

    public static unsafe class EntityDeltaCompressionArchitecture
    {
        public const int TelemetryRingFrames = 300;
        public const int DefaultBlockEntities = 128;
        public const int DefaultSchemaBytes = 256;
        public const int HashTableSlots = 4096;
        public const int CounterCapacity = 32;
        public const int DefaultSectorMeters = 100;
        public const uint EntityFlagActive = 1u;
        public const uint EntityFlagTombstone = 1u << 1;
        public const uint EntityFlagDehydrated = 1u << 2;
        public const uint EntityFlagHighFidelity = 1u << 3;
        public const uint EntityFlagDynamic = 1u << 4;
        public const uint EntityFlagPruned = 1u << 5;

        internal const int CounterDeltaRecordCount = 0;
        internal const int CounterActiveEntityCount = 1;
        internal const int CounterTombstoneCount = 2;
        internal const int CounterDenseBytes = 3;
        internal const int CounterRleBytes = 4;
        internal const int CounterCompressedBytes = 5;
        internal const int CounterFullSnapshotBytes = 6;
        internal const int CounterFailure = 7;
        internal const int CounterCompressionFlags = 8;
        internal const int CounterBlockCount = 9;
        internal const int CounterParsedProfiles = 10;
        internal const int CounterCsvFailure = 11;
        internal const int CounterWalPayloadBytes = 12;
        internal const int CounterPrunedTombstones = 13;
        internal const int CounterDehydratedCount = 14;
        internal const int CounterAuditSamples = 15;
        internal const int CounterAuditSmallerPayloads = 16;
        internal const int CounterAuditPass = 17;
        internal const int CounterWalEnvelopeAuditPass = 18;
        internal const int CounterDecodeDenseBytes = 19;
        internal const int CounterDecodeRecordCount = 20;
        internal const int CounterDecodePass = 21;
        internal const int CounterAuditCompressedRatioPpm = 22;
        internal const int CounterAuditSavingsPpm = 23;
        internal const int CounterAuditByteSavingsPass = 24;

        internal const ulong MockSchemaMagic = 0x45544E44534C5441UL; // ATLSDNTE little-endian marker.
        internal const ulong AuditPpmScale = 1000000ul;
        internal const uint HeaderFlagLz4 = 1u;
        internal const uint HeaderFlagRaw = 1u << 1;
        internal const uint HeaderFlagRle = 1u << 2;
        internal const uint HeaderFlagRleBypassed = 1u << 3;
        internal const uint HeaderFlagPruned = 1u << 4;
        internal const uint HeaderFlagChecksumValid = 1u << 5;
        internal const uint HeaderFlagFatal = 1u << 31;
        internal const uint RleStreamMagic = 0x45524C45u; // ELRE little-endian marker.
        internal const uint RleStreamFlagPairs = 1u;
        internal const uint RleStreamFlagRawDense = 1u << 1;
        internal const uint RleStreamFlagLittleEndianRecords = 1u << 8;
        internal const uint RleStreamFlagBigEndianRecords = 1u << 9;
        internal const uint RleStreamEndianMask = RleStreamFlagLittleEndianRecords | RleStreamFlagBigEndianRecords;
        internal const uint TelemetryFlagDiskLatencyPatched = 1u << 8;
        internal const uint TelemetryFlagDiskLatencySpike = 1u << 9;
        internal const uint TelemetryDumpMagic = 0x45445741u; // AWDE little-endian marker.
        internal const uint TelemetryDumpVersion = 1u;
        internal const int FailureCodeCompressionAlias = 0x53434C41; // ALCS
        internal const int FailureCodeDecodeAlias = 0x53444C41; // ALDS
        public const uint SchemaHash = 0x45445231u; // EDR1
        internal const ulong ChecksumSeed = 0x58484833454E5445UL;
        internal const uint DefaultTicksPerDay = 86400u;
        internal const int Lz4LastLiterals = 5;
        internal const int Lz4MfLimit = 12;

        private const uint KeyTombstoneDays = 0xC22DF47Au;
        private const uint KeyLz4MinEffort01 = 0xC61A63F9u;
        private const uint KeyLz4MaxEffort01 = 0x36DBE327u;
        private const uint KeyLowQualityWriteHz = 0x5AF6437Eu;
        private const uint KeyHighQualityWriteHz = 0x7087A0CAu;
        private const uint KeyIoPressureBias01 = 0xC6B9CB43u;
        private const uint KeyMaxWalWriteMs = 0xEC81F34Fu;
        private const uint KeyMaxBytesPerFrame = 0xA1229643u;
        private const uint KeyMockMutationRate = 0x18DB9276u;
        private const uint KeyRleMinSaving = 0x01B478E8u;
        private const uint KeyProfile = 0x4674CAEEu;

        private static readonly ProfilerMarker ScheduleCompressionPipelineMarker = new ProfilerMarker("H8.Save.EntityDelta.ScheduleCompression");
        private static readonly ProfilerMarker ScheduleWalPayloadDecodePipelineMarker = new ProfilerMarker("H8.Save.EntityDelta.ScheduleDecode");

        public static bool TryResolveVaultBuffers(
            IDataVault vault,
            int entityCapacity,
            int maxDeltaRecords,
            int stagingCapacityBytes,
            int sectorStatsCapacity,
            int profileCapacity,
            out EntityDeltaCompressionVaultBufferSet buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            int safeEntities = math.max(1, entityCapacity);
            int safeDeltas = math.max(1, math.min(maxDeltaRecords <= 0 ? safeEntities : maxDeltaRecords, safeEntities));
            int safeBytes = Align16(math.max(UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>() + UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>(), stagingCapacityBytes));
            int rleSafeBytes = ResolveRleStagingBytes(safeBytes);
            int safeStats = math.max(1, sectorStatsCapacity);
            int safeProfiles = math.max(1, profileCapacity);
            int blockCount = ResolveBlockCount(safeEntities, DefaultBlockEntities);

            buffers.SchemaBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaSchemaBytes, DefaultSchemaBytes, NativeArrayOptions.UninitializedMemory);
            buffers.CurrentRecords = ResolveVaultBuffer<EntityDeltaDataRecordDTO>(vault, BufferID.SaveEntityDeltaCurrentRecords, safeEntities, NativeArrayOptions.UninitializedMemory);
            buffers.BaselineRecords = ResolveVaultBuffer<EntityDeltaDataRecordDTO>(vault, BufferID.SaveEntityDeltaBaselineRecords, safeEntities, NativeArrayOptions.UninitializedMemory);
            buffers.DeltaRecords = ResolveVaultBuffer<EntityDeltaDataRecordDTO>(vault, BufferID.SaveEntityDeltaRecords, safeDeltas, NativeArrayOptions.UninitializedMemory);
            buffers.BlockCounters = ResolveVaultBuffer<EntityDeltaBlockCounter64>(vault, BufferID.SaveEntityDeltaBlockCounters, blockCount, NativeArrayOptions.ClearMemory);
            buffers.DenseBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaDenseBytes, safeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.RleBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaRleBytes, rleSafeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.CompressedBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaCompressedBytes, rleSafeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.WalPayloadBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaWalPayloadBytes, rleSafeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.Lz4HashTable = ResolveVaultBuffer<int>(vault, BufferID.SaveEntityDeltaLz4HashTable, HashTableSlots, NativeArrayOptions.UninitializedMemory);
            buffers.Headers = ResolveVaultBuffer<EntityDeltaHeaderDTO>(vault, BufferID.SaveEntityDeltaHeaders, safeStats, NativeArrayOptions.UninitializedMemory);
            buffers.Counters = ResolveVaultBuffer<int>(vault, BufferID.SaveEntityDeltaCounters, CounterCapacity, NativeArrayOptions.ClearMemory);
            buffers.TelemetryRing = ResolveVaultBuffer<EntityCompressionTelemetryEntry>(vault, BufferID.SaveEntityDeltaTelemetryRing, TelemetryRingFrames, NativeArrayOptions.ClearMemory);
            buffers.TelemetryCursor = ResolveVaultBuffer<int>(vault, BufferID.SaveEntityDeltaTelemetryCursor, 1, NativeArrayOptions.ClearMemory);
            buffers.Tuning = ResolveVaultBuffer<EntityDeltaCompressionTuningDTO>(vault, BufferID.SaveEntityDeltaTuning, 1, NativeArrayOptions.ClearMemory);
            buffers.SectorStats = ResolveVaultBuffer<EntityDeltaSectorStatsDTO>(vault, BufferID.SaveEntityDeltaSectorStats, safeStats, NativeArrayOptions.ClearMemory);
            buffers.CsvScratch = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaCsvScratch, 16384, NativeArrayOptions.UninitializedMemory);
            buffers.Profiles = ResolveVaultBuffer<EntityCompressionProfileDTO>(vault, BufferID.SaveEntityDeltaProfiles, safeProfiles, NativeArrayOptions.ClearMemory);

            return buffers.SchemaBytes.IsCreated &&
                   buffers.CurrentRecords.IsCreated &&
                   buffers.BaselineRecords.IsCreated &&
                   buffers.DeltaRecords.IsCreated &&
                   buffers.BlockCounters.IsCreated &&
                   buffers.DenseBytes.IsCreated &&
                   buffers.RleBytes.IsCreated &&
                   buffers.CompressedBytes.IsCreated &&
                   buffers.WalPayloadBytes.IsCreated &&
                   buffers.Lz4HashTable.IsCreated &&
                   buffers.Headers.IsCreated &&
                   buffers.Counters.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.Tuning.IsCreated &&
                   buffers.SectorStats.IsCreated &&
                   buffers.CsvScratch.IsCreated &&
                   buffers.Profiles.IsCreated;
        }

        public static void GenerateEmergencyMockEntitySchema(NativeArray<byte> destination, uint seed)
        {
            if (!destination.IsCreated || destination.Length <= 0)
                return;

            uint state = seed != 0u ? seed : 0x154E7717u;
            for (int i = 0; i < destination.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                destination[i] = unchecked((byte)(state >> ((i & 3) << 3)));
            }

            if (destination.Length < UnsafeUtility.SizeOf<EntityDeltaMockSchemaDTO>())
                return;

            byte* destinationPtr = (byte*)destination.GetUnsafePtr();
            WriteMockSchemaLittleEndian(
                destinationPtr,
                seed,
                (uint)UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>(),
                (uint)UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>());
        }

        public static bool TryGenerateEmergencyMockEntitySchema(IDataVault vault, uint seed)
        {
            if (!TryResolveEmergencyMockEntitySchemaBuffer(vault, out NativeArray<byte> schemaBytes))
                return false;

            GenerateEmergencyMockEntitySchema(schemaBytes, seed);
            return true;
        }

        private static bool TryResolveEmergencyMockEntitySchemaBuffer(IDataVault vault, out NativeArray<byte> schemaBytes)
        {
            schemaBytes = default;
            if (vault == null)
                return false;

            schemaBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveEntityDeltaSchemaBytes, DefaultSchemaBytes, NativeArrayOptions.UninitializedMemory);
            if (!schemaBytes.IsCreated)
                return false;

            return true;
        }

        public static EntityDeltaCompressionTuningDTO BuildDefaultTuning()
        {
            return new EntityDeltaCompressionTuningDTO
            {
                ProfileHash = 0x5348494E4F425531UL,
                SchemaHash = EntityDeltaCompressionArchitecture.SchemaHash,
                Flags = 0u,
                TombstoneMaxDays = 3f,
                Lz4MinEffort01 = 0.10f,
                Lz4MaxEffort01 = 0.88f,
                LowQualityWriteHz = 5f,
                HighQualityWriteHz = 30f,
                IoPressureBias01 = 0.42f,
                MaxWalWriteMillis = 0.35f,
                MaxBytesPerFrame = 128u * 1024u,
                MockMutationRate01 = 0.08f,
                RleMinSaving01 = 0.015f,
                DehydrationFadeTicks = 12u,
                _pad0 = 0u
            };
        }

        public static EntityDeltaCompressionTuningDTO ResolveRuntimeTuning(NativeArray<EntityDeltaCompressionTuningDTO> tuningBuffer)
        {
            EntityDeltaCompressionTuningDTO tuning = tuningBuffer.IsCreated && tuningBuffer.Length > 0 && tuningBuffer[0].SchemaHash != 0u
                ? tuningBuffer[0]
                : BuildDefaultTuning();

            tuning.TombstoneMaxDays = math.clamp(SanitizeFinite(tuning.TombstoneMaxDays, 3f), 0.25f, 30f);
            tuning.Lz4MinEffort01 = math.saturate(SanitizeFinite(tuning.Lz4MinEffort01, 0.10f));
            tuning.Lz4MaxEffort01 = math.max(tuning.Lz4MinEffort01, math.saturate(SanitizeFinite(tuning.Lz4MaxEffort01, 0.88f)));
            tuning.LowQualityWriteHz = math.max(1f, SanitizeFinite(tuning.LowQualityWriteHz, 5f));
            tuning.HighQualityWriteHz = math.max(tuning.LowQualityWriteHz, SanitizeFinite(tuning.HighQualityWriteHz, 30f));
            tuning.IoPressureBias01 = math.saturate(SanitizeFinite(tuning.IoPressureBias01, 0.42f));
            tuning.MaxWalWriteMillis = math.max(0.05f, SanitizeFinite(tuning.MaxWalWriteMillis, 0.35f));
            tuning.MockMutationRate01 = math.saturate(SanitizeFinite(tuning.MockMutationRate01, 0.08f));
            tuning.RleMinSaving01 = math.saturate(SanitizeFinite(tuning.RleMinSaving01, 0.015f));
            if (tuning.MaxBytesPerFrame < 1024u)
                tuning.MaxBytesPerFrame = 128u * 1024u;
            if (tuning.DehydrationFadeTicks == 0u)
                tuning.DehydrationFadeTicks = 12u;

            return tuning;
        }

        public static JobHandle ScheduleCompressionPipeline(
            EntityDeltaCompressionVaultBufferSet buffers,
            int entityCount,
            int3 sectorCoord,
            uint simulationFrame,
            float globalQualityWeight,
            float ioPressure01,
            JobHandle dependency,
            bool injectMockState = false,
            float lastDiskWriteLatencyMs = 0f)
        {
            using var profilerScope = ScheduleCompressionPipelineMarker.Auto();
            if (!buffers.CurrentRecords.IsCreated ||
                !buffers.BaselineRecords.IsCreated ||
                !buffers.DeltaRecords.IsCreated ||
                !buffers.BlockCounters.IsCreated ||
                !buffers.DenseBytes.IsCreated ||
                !buffers.RleBytes.IsCreated ||
                !buffers.CompressedBytes.IsCreated ||
                !buffers.WalPayloadBytes.IsCreated ||
                !buffers.Lz4HashTable.IsCreated ||
                !buffers.Headers.IsCreated ||
                !buffers.Counters.IsCreated)
            {
                return dependency;
            }

            int safeEntityCount = math.clamp(entityCount <= 0 ? buffers.CurrentRecords.Length : entityCount, 1, math.min(buffers.CurrentRecords.Length, buffers.BaselineRecords.Length));
            int blockCount = ResolveBlockCount(safeEntityCount, DefaultBlockEntities);
            int maxDeltasPerBlock = ResolveMaxDeltasPerBlock(buffers.DeltaRecords.Length, blockCount);
            ulong sectorHash = ResolveSectorHash(sectorCoord);
            if (buffers.Counters.Length < CounterCapacity || HasCompressionPipelineAliasViolation(in buffers))
            {
                return new EntityScheduleFailureJob
                {
                    Counters = buffers.Counters,
                    Headers = buffers.Headers,
                    SectorStats = buffers.SectorStats,
                    SectorHash = sectorHash,
                    SectorCoord = sectorCoord,
                    FailureCode = FailureCodeCompressionAlias
                }.Schedule(dependency);
            }

            EntityDeltaCompressionTuningDTO tuning = ResolveRuntimeTuning(buffers.Tuning);
            uint tombstoneMaxTicks = ResolveTombstoneMaxTicks(tuning.TombstoneMaxDays);
            float effort01 = ResolveCompressionEffort01(globalQualityWeight, ioPressure01, lastDiskWriteLatencyMs, in tuning);

            JobHandle sourceReady = dependency;
            if (injectMockState)
            {
                sourceReady = new GenerateMockEntityStateJob
                {
                    CurrentRecords = buffers.CurrentRecords,
                    BaselineRecords = buffers.BaselineRecords,
                    SectorHash = sectorHash,
                    SectorCoord = sectorCoord,
                    SimulationFrame = simulationFrame,
                    GlobalQualityWeight = globalQualityWeight,
                    MutationRate01 = tuning.MockMutationRate01,
                    EntityCount = safeEntityCount
                }.Schedule(safeEntityCount, 128, dependency);
            }

            JobHandle prune = new EntityTombstonePruneJob
            {
                CurrentRecords = buffers.CurrentRecords,
                BlockCounters = buffers.BlockCounters,
                EntityCount = safeEntityCount,
                BlockEntityCount = DefaultBlockEntities,
                SectorHash = sectorHash,
                SimulationFrame = simulationFrame,
                TombstoneMaxTicks = tombstoneMaxTicks
            }.Schedule(blockCount, 1, sourceReady);

            JobHandle extract = new ExtractEntityDeltaJob
            {
                CurrentRecords = buffers.CurrentRecords,
                BaselineRecords = buffers.BaselineRecords,
                DeltaRecords = buffers.DeltaRecords,
                BlockCounters = buffers.BlockCounters,
                EntityCount = safeEntityCount,
                BlockEntityCount = DefaultBlockEntities,
                MaxDeltasPerBlock = maxDeltasPerBlock,
                SectorHash = sectorHash
            }.Schedule(blockCount, 1, prune);

            JobHandle finalize = new EntityDeltaFinalizeJob
            {
                BlockCounters = buffers.BlockCounters,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                BlockCount = blockCount,
                EntityCount = safeEntityCount,
                SectorHash = sectorHash,
                SectorCoord = sectorCoord
            }.Schedule(extract);

            JobHandle pack = new EntityDeltaDensePackJob
            {
                DeltaRecords = buffers.DeltaRecords,
                BlockCounters = buffers.BlockCounters,
                DenseBytes = buffers.DenseBytes,
                Counters = buffers.Counters,
                BlockCount = blockCount,
                MaxDeltasPerBlock = maxDeltasPerBlock
            }.Schedule(finalize);

            JobHandle rle = new EntityRlePreconditionJob
            {
                Source = buffers.DenseBytes,
                Destination = buffers.RleBytes,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                RleMinSaving01 = tuning.RleMinSaving01
            }.Schedule(pack);

            JobHandle lz4 = new EntityLz4CompressionJob
            {
                Source = buffers.RleBytes,
                Destination = buffers.CompressedBytes,
                HashTable = buffers.Lz4HashTable,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                SectorHash = sectorHash,
                CompressionEffort01 = effort01,
                IoPressure01 = ioPressure01,
                SourceLengthCounterIndex = CounterRleBytes
            }.Schedule(rle);

            JobHandle checksum = new EntityDeltaChecksumHeaderJob
            {
                CompressedBytes = buffers.CompressedBytes,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                SectorHash = sectorHash
            }.Schedule(lz4);

            JobHandle walPack = new EntityWalPayloadPackJob
            {
                Headers = buffers.Headers,
                CompressedBytes = buffers.CompressedBytes,
                WalPayloadBytes = buffers.WalPayloadBytes,
                Counters = buffers.Counters
            }.Schedule(checksum);

            JobHandle walAudit = new EntityWalPayloadEnvelopeAuditJob
            {
                WalPayloadBytes = buffers.WalPayloadBytes,
                Headers = buffers.Headers,
                Counters = buffers.Counters,
                SectorStats = buffers.SectorStats
            }.Schedule(walPack);

            if (!buffers.TelemetryRing.IsCreated || !buffers.TelemetryCursor.IsCreated)
                return walAudit;

            JobHandle telemetry = new EntityDeltaTelemetryRecordJob
            {
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                TelemetryRing = buffers.TelemetryRing,
                TelemetryCursor = buffers.TelemetryCursor,
                Frame = simulationFrame,
                BurstTimeMs = 0f,
                DiskWriteLatencyMs = 0f,
                GlobalQualityWeight = globalQualityWeight,
                CompressionEffort01 = effort01,
                IoPressure01 = ioPressure01
            }.Schedule(walAudit);

            return new EntityDeltaCompressionRatioAuditJob
            {
                TelemetryRing = buffers.TelemetryRing,
                Counters = buffers.Counters,
                MinimumSamples = math.min(32, buffers.TelemetryRing.Length),
                RequiredPassRatio01 = 0.99f,
                RequiredByteSavings01 = 0.99f
            }.Schedule(telemetry);
        }

        public static JobHandle ScheduleCompressionProfileCsvParse(
            NativeArray<byte> csvBytes,
            NativeArray<EntityDeltaCompressionTuningDTO> tuning,
            NativeArray<EntityCompressionProfileDTO> profiles,
            NativeArray<int> counters,
            int byteCount,
            JobHandle dependency)
        {
            if (!csvBytes.IsCreated || !tuning.IsCreated || !profiles.IsCreated)
                return dependency;

            return new EntityCompressionProfileCsvParseJob
            {
                CsvBytes = csvBytes,
                Tuning = tuning,
                Profiles = profiles,
                Counters = counters,
                ByteCount = byteCount
            }.Schedule(dependency);
        }

        public static JobHandle ScheduleWalPayloadDecodePipeline(
            NativeArray<byte> walPayloadBytes,
            int byteCount,
            EntityDeltaCompressionVaultBufferSet buffers,
            JobHandle dependency)
        {
            return ScheduleWalPayloadDecodePipeline(
                walPayloadBytes,
                byteCount,
                buffers.RleBytes,
                buffers.DenseBytes,
                buffers.DeltaRecords,
                buffers.Headers,
                buffers.Counters,
                dependency);
        }

        public static JobHandle ScheduleWalPayloadDecodePipeline(
            int byteCount,
            EntityDeltaCompressionVaultBufferSet buffers,
            JobHandle dependency)
        {
            return ScheduleWalPayloadDecodePipeline(
                buffers.WalPayloadBytes,
                byteCount,
                buffers,
                dependency);
        }

        public static JobHandle ScheduleWalPayloadDecodePipeline(
            NativeArray<byte> walPayloadBytes,
            int byteCount,
            NativeArray<byte> rleBytes,
            NativeArray<byte> denseBytes,
            NativeArray<EntityDeltaDataRecordDTO> deltaRecords,
            NativeArray<EntityDeltaHeaderDTO> headers,
            NativeArray<int> counters,
            JobHandle dependency)
        {
            using var profilerScope = ScheduleWalPayloadDecodePipelineMarker.Auto();
            if (!walPayloadBytes.IsCreated ||
                !rleBytes.IsCreated ||
                !denseBytes.IsCreated ||
                !deltaRecords.IsCreated ||
                !headers.IsCreated ||
                !counters.IsCreated ||
                counters.Length < CounterCapacity)
            {
                return dependency;
            }

            if (HasWalDecodeAliasViolation(walPayloadBytes, rleBytes, denseBytes, deltaRecords, headers, counters))
            {
                return new EntityScheduleFailureJob
                {
                    Counters = counters,
                    Headers = headers,
                    SectorStats = default,
                    SectorHash = 0UL,
                    SectorCoord = default,
                    FailureCode = FailureCodeDecodeAlias
                }.Schedule(dependency);
            }

            JobHandle decode = new EntityWalPayloadDecodeJob
            {
                WalPayloadBytes = walPayloadBytes,
                RleBytes = rleBytes,
                Headers = headers,
                Counters = counters,
                ByteCount = byteCount
            }.Schedule(dependency);

            return new EntityRleStreamExpandToRecordsJob
            {
                RleBytes = rleBytes,
                DenseBytes = denseBytes,
                DeltaRecords = deltaRecords,
                Counters = counters
            }.Schedule(decode);
        }

        public static bool TryEnqueueEntityDeltaWalWrite(
            IAsyncPersistenceService persistence,
            NativeArray<byte> walPayloadBytes,
            NativeArray<int> counters,
            NativeArray<EntityDeltaHeaderDTO> headers,
            uint frame)
        {
            if (persistence == null ||
                !walPayloadBytes.IsCreated ||
                !counters.IsCreated ||
                !headers.IsCreated ||
                headers.Length <= 0 ||
                counters.Length < CounterCapacity)
            {
                return false;
            }

            int byteCount = counters[CounterWalPayloadBytes];
            if (byteCount <= UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>() ||
                byteCount > walPayloadBytes.Length ||
                counters[CounterFailure] != 0 ||
                counters[CounterWalEnvelopeAuditPass] != 1)
            {
                return false;
            }

            EntityDeltaHeaderDTO header = headers[0];
            uint sourceHash = (uint)(header.XXHash3Checksum ^ (header.XXHash3Checksum >> 32));
            long pagerSectorHash = unchecked((long)ResolvePagerSectorHash(header.SectorHash, H8WorldPagePayloadTypes.EntityDeltaRle));
            return persistence.TryEnqueueChunkPageWrite(
                pagerSectorHash,
                H8WorldPagePayloadTypes.EntityDeltaRle,
                walPayloadBytes,
                byteCount,
                sourceHash,
                frame);
        }

        public static bool TryEnqueueEntityDeltaWalWrite(
            IAsyncPersistenceService persistence,
            EntityDeltaCompressionVaultBufferSet buffers,
            uint frame)
        {
            return TryEnqueueEntityDeltaWalWrite(
                persistence,
                buffers.WalPayloadBytes,
                buffers.Counters,
                buffers.Headers,
                frame);
        }

        public static bool TryRequestEntityDeltaWalRead(
            IAsyncPersistenceService persistence,
            ulong sectorHash,
            uint requestId,
            out H8WorldPageReadTicket ticket)
        {
            ticket = default;
            if (persistence == null || requestId == 0u)
                return false;

            long pagerSectorHash = unchecked((long)ResolvePagerSectorHash(sectorHash, H8WorldPagePayloadTypes.EntityDeltaRle));
            return persistence.TryRequestChunkPageRead(
                pagerSectorHash,
                H8WorldPagePayloadTypes.EntityDeltaRle,
                requestId,
                out ticket);
        }

        public static bool TryRequestEntityDeltaWalRead(
            IAsyncPersistenceService persistence,
            int3 sectorCoord,
            uint requestId,
            out H8WorldPageReadTicket ticket)
        {
            return TryRequestEntityDeltaWalRead(
                persistence,
                ResolveSectorHash(sectorCoord),
                requestId,
                out ticket);
        }

        public static bool TryCopyCompletedEntityDeltaWalPayload(
            IAsyncPersistenceService persistence,
            in H8WorldPageReadTicket ticket,
            NativeArray<byte> walPayloadBytes,
            out int bytesWritten,
            out H8WorldPageStatus status)
        {
            bytesWritten = 0;
            status = H8WorldPageStatus.Rejected;
            if (persistence == null ||
                !walPayloadBytes.IsCreated ||
                ticket.PayloadType != H8WorldPagePayloadTypes.EntityDeltaRle)
            {
                return false;
            }

            bool copied = persistence.TryCopyCompletedChunkPage(
                in ticket,
                walPayloadBytes,
                out bytesWritten,
                out status);
            if (!copied)
                return false;

            if (status == H8WorldPageStatus.Ready)
            {
                int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                if (bytesWritten < headerBytes || bytesWritten > walPayloadBytes.Length)
                {
                    bytesWritten = 0;
                    status = H8WorldPageStatus.Rejected;
                    return false;
                }
            }

            return true;
        }

        public static bool TryCopyCompletedEntityDeltaWalPayload(
            IAsyncPersistenceService persistence,
            in H8WorldPageReadTicket ticket,
            EntityDeltaCompressionVaultBufferSet buffers,
            out int bytesWritten,
            out H8WorldPageStatus status)
        {
            return TryCopyCompletedEntityDeltaWalPayload(
                persistence,
                in ticket,
                buffers.WalPayloadBytes,
                out bytesWritten,
                out status);
        }

        public static ulong ResolveSectorHash(in EntityDeltaDataRecordDTO record)
        {
            return ResolveSectorHash(new int3(SaturatingLongToInt(record.SectorX), SaturatingLongToInt(record.SectorY), SaturatingLongToInt(record.SectorZ)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ResolveSectorHash(int3 sectorCoord)
        {
            uint x = EncodeSignedMorton21(sectorCoord.x);
            uint y = EncodeSignedMorton21(sectorCoord.y);
            uint z = EncodeSignedMorton21(sectorCoord.z);
            ulong morton = (ExpandBits21(x) << 0) | (ExpandBits21(y) << 1) | (ExpandBits21(z) << 2);
            return morton ^ 0xD6E8FEB86659FD93UL;
        }

        public static ulong ResolveSectorHashFromAupMillimeters(long aupMillimetersX, long aupMillimetersY, long aupMillimetersZ, int sectorMeters = DefaultSectorMeters)
        {
            long sectorMillimeters = math.max(1, sectorMeters) * 1000L;
            int3 sectorCoord = new int3(
                SaturatingLongToInt(FloorDiv(aupMillimetersX, sectorMillimeters)),
                SaturatingLongToInt(FloorDiv(aupMillimetersY, sectorMillimeters)),
                SaturatingLongToInt(FloorDiv(aupMillimetersZ, sectorMillimeters)));
            return ResolveSectorHash(sectorCoord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ResolvePagerSectorHash(ulong sectorHash, uint payloadType)
        {
            ulong x = sectorHash ^ ((ulong)payloadType << 32) ^ 0x9E3779B97F4A7C15UL;
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBlockCount(int itemCount, int blockItems)
        {
            int items = math.max(1, itemCount);
            int block = math.max(1, blockItems);
            return (items + block - 1) / block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMaxDeltasPerBlock(int maxDeltaRecords, int blockCount)
        {
            int blocks = math.max(1, blockCount);
            return math.max(1, (math.max(1, maxDeltaRecords) + blocks - 1) / blocks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align16(int value)
        {
            return (math.max(0, value) + 15) & ~15;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRleStagingBytes(int denseCapacityBytes)
        {
            int dense = math.max(UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>(), denseCapacityBytes);
            int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>() + UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
            if (dense > (int.MaxValue - headerBytes) / 2)
                return int.MaxValue & ~15;

            return Align16(headerBytes + (dense * 2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCompressionEffort01(float globalQualityWeight, float ioPressure01, float diskLatencyMs)
        {
            EntityDeltaCompressionTuningDTO tuning = BuildDefaultTuning();
            return ResolveCompressionEffort01(globalQualityWeight, ioPressure01, diskLatencyMs, in tuning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCompressionEffort01(
            float globalQualityWeight,
            float ioPressure01,
            float diskLatencyMs,
            in EntityDeltaCompressionTuningDTO tuning)
        {
            float quality = Sanitize01(globalQualityWeight);
            float pressureBias = math.saturate(SanitizeFinite(tuning.IoPressureBias01, 0.42f));
            float pressureScale = math.lerp(0.7f, 1.6f, pressureBias);
            float pressure = Sanitize01((ioPressure01 * pressureScale) + math.saturate(diskLatencyMs * 0.25f));
            float curvedQuality = quality * quality * (3f - (2f * quality));
            float curvedPressure = pressure * pressure * (3f - (2f * pressure));
            float minEffort = math.saturate(SanitizeFinite(tuning.Lz4MinEffort01, 0.10f));
            float maxEffort = math.max(minEffort, math.saturate(SanitizeFinite(tuning.Lz4MaxEffort01, 0.88f)));
            float tunedEffort = math.lerp(minEffort, maxEffort, curvedQuality);
            float thermalFloor = math.max(0.02f, minEffort * 0.3f);
            return math.saturate(math.lerp(tunedEffort, thermalFloor, curvedPressure));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWriteHz(float globalQualityWeight, in EntityDeltaCompressionTuningDTO tuning)
        {
            float quality = Sanitize01(globalQualityWeight);
            float curve = quality * quality * (3f - (2f * quality));
            float low = math.max(1f, SanitizeFinite(tuning.LowQualityWriteHz, 5f));
            float high = math.max(low, SanitizeFinite(tuning.HighQualityWriteHz, 30f));
            return math.lerp(low, high, curve);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveTombstoneMaxTicks(float tombstoneMaxDays)
        {
            float days = math.clamp(SanitizeFinite(tombstoneMaxDays, 3f), 0.25f, 30f);
            return (uint)math.max(1, (int)math.round(days * DefaultTicksPerDay));
        }

        internal static void WriteHeaderLittleEndian(byte* destination, in EntityDeltaHeaderDTO header)
        {
            if (destination == null)
                return;

            WriteULongLittleEndian(destination, 0, header.SectorHash);
            WriteUIntLittleEndian(destination, 8, header.CompressedSize);
            WriteUIntLittleEndian(destination, 12, header.UncompressedSize);
            WriteULongLittleEndian(destination, 16, header.XXHash3Checksum);
            WriteUIntLittleEndian(destination, 24, header._pad0);
            WriteUIntLittleEndian(destination, 28, header._pad1);
        }

        internal static void WriteRleStreamHeaderLittleEndian(byte* destination, uint flags, uint denseBytes, uint storedBytes)
        {
            if (destination == null)
                return;

            WriteUIntLittleEndian(destination, 0, RleStreamMagic);
            WriteUIntLittleEndian(destination, 4, flags);
            WriteUIntLittleEndian(destination, 8, denseBytes);
            WriteUIntLittleEndian(destination, 12, storedBytes);
        }

        internal static EntityDeltaRleStreamHeaderDTO ReadRleStreamHeaderLittleEndian(byte* source)
        {
            if (source == null)
                return default;

            return new EntityDeltaRleStreamHeaderDTO
            {
                Magic = ReadUIntLittleEndian(source, 0),
                Flags = ReadUIntLittleEndian(source, 4),
                DenseBytes = ReadUIntLittleEndian(source, 8),
                StoredBytes = ReadUIntLittleEndian(source, 12)
            };
        }

        internal static EntityDeltaHeaderDTO ReadHeaderLittleEndian(byte* source)
        {
            if (source == null)
                return default;

            return new EntityDeltaHeaderDTO
            {
                SectorHash = ReadULongLittleEndian(source, 0),
                CompressedSize = ReadUIntLittleEndian(source, 8),
                UncompressedSize = ReadUIntLittleEndian(source, 12),
                XXHash3Checksum = ReadULongLittleEndian(source, 16),
                _pad0 = ReadUIntLittleEndian(source, 24),
                _pad1 = ReadUIntLittleEndian(source, 28)
            };
        }

        private static void WriteTelemetryDumpHeaderLittleEndian(
            byte* destination,
            uint entryCount,
            uint entryStride,
            uint cursor,
            uint reasonFlags,
            uint ringCapacity,
            uint headerBytes,
            in EntityCompressionTelemetryEntry first,
            in EntityCompressionTelemetryEntry last)
        {
            if (destination == null)
                return;

            WriteUIntLittleEndian(destination, 0, TelemetryDumpMagic);
            WriteUIntLittleEndian(destination, 4, TelemetryDumpVersion);
            WriteUIntLittleEndian(destination, 8, entryCount);
            WriteUIntLittleEndian(destination, 12, entryStride);
            WriteUIntLittleEndian(destination, 16, cursor);
            WriteUIntLittleEndian(destination, 20, reasonFlags);
            WriteUIntLittleEndian(destination, 24, ringCapacity);
            WriteUIntLittleEndian(destination, 28, headerBytes);
            WriteULongLittleEndian(destination, 32, first.SectorHash);
            WriteULongLittleEndian(destination, 40, last.SectorHash);
            WriteUIntLittleEndian(destination, 48, first.Frame);
            WriteUIntLittleEndian(destination, 52, last.Frame);
            WriteULongLittleEndian(destination, 56, 0UL);
        }

        private static void WriteTelemetryEntryLittleEndian(byte* destination, in EntityCompressionTelemetryEntry entry)
        {
            if (destination == null)
                return;

            WriteULongLittleEndian(destination, 0, entry.SectorHash);
            WriteULongLittleEndian(destination, 8, entry.PayloadHash);
            WriteUIntLittleEndian(destination, 16, entry.Frame);
            WriteUIntLittleEndian(destination, 20, entry.FullSnapshotBytes);
            WriteUIntLittleEndian(destination, 24, entry.DenseDeltaBytes);
            WriteUIntLittleEndian(destination, 28, entry.RleBytes);
            WriteUIntLittleEndian(destination, 32, entry.CompressedBytes);
            WriteUIntLittleEndian(destination, 36, entry.Flags);
            WriteUIntLittleEndian(destination, 40, math.asuint(entry.BurstTimeMs));
            WriteUIntLittleEndian(destination, 44, math.asuint(entry.DiskWriteLatencyMs));
            WriteUIntLittleEndian(destination, 48, math.asuint(entry.GlobalQualityWeight));
            WriteUIntLittleEndian(destination, 52, math.asuint(entry.CompressionEffort01));
            WriteUIntLittleEndian(destination, 56, entry.IoPressureMicro);
            WriteUIntLittleEndian(destination, 60, entry.DeltaEntityCount);
        }

        private static void WriteMockSchemaLittleEndian(byte* destination, uint seed, uint headerBytes, uint recordBytes)
        {
            if (destination == null)
                return;

            WriteULongLittleEndian(destination, 0, MockSchemaMagic);
            WriteULongLittleEndian(destination, 8, ((ulong)seed << 32) | EntityDeltaCompressionArchitecture.SchemaHash);
            WriteUIntLittleEndian(destination, 16, 1u);
            WriteUIntLittleEndian(destination, 20, headerBytes);
            WriteUIntLittleEndian(destination, 24, recordBytes);
            WriteUIntLittleEndian(destination, 28, 0u);
            WriteUIntLittleEndian(destination, 32, 4096u);
            WriteUIntLittleEndian(destination, 36, DefaultSectorMeters);
            WriteULongLittleEndian(destination, 40, seed);
            WriteULongLittleEndian(destination, 48, 0UL);
            WriteULongLittleEndian(destination, 56, 0UL);
        }

        public static bool VerifyCompressedPayloadChecksum(NativeArray<byte> compressedBytes, int byteCount, in EntityDeltaHeaderDTO header)
        {
            if (!compressedBytes.IsCreated)
                return false;

            if (byteCount < 0 || byteCount > compressedBytes.Length)
                return false;

            int count = byteCount;
            byte* ptr = (byte*)compressedBytes.GetUnsafeReadOnlyPtr();
            return VerifyCompressedPayloadChecksum(ptr, count, in header);
        }

        public static bool TryReadAndVerifyWalPayload(
            NativeArray<byte> walPayloadBytes,
            int byteCount,
            out EntityDeltaHeaderDTO header)
        {
            header = default;
            if (!walPayloadBytes.IsCreated)
                return false;

            int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
            if (byteCount < 0 || byteCount > walPayloadBytes.Length)
                return false;

            int count = byteCount;
            if (count < headerBytes)
                return false;

            byte* ptr = (byte*)walPayloadBytes.GetUnsafeReadOnlyPtr();
            header = ReadHeaderLittleEndian(ptr);
            int compressedBytes = header.CompressedSize > int.MaxValue ? -1 : (int)header.CompressedSize;
            int uncompressedBytes = header.UncompressedSize > int.MaxValue ? -1 : (int)header.UncompressedSize;
            if (compressedBytes < 0 || uncompressedBytes < 0 || compressedBytes != count - headerBytes)
                return false;

            if (count == headerBytes)
                return compressedBytes == 0 &&
                       uncompressedBytes == 0 &&
                       header.XXHash3Checksum == 0UL;

            int streamHeaderBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
            if (uncompressedBytes < streamHeaderBytes || compressedBytes > uncompressedBytes)
                return false;

            if (!VerifyCompressedPayloadChecksum(ptr + headerBytes, compressedBytes, in header))
                return false;

            if (compressedBytes == uncompressedBytes)
                return TryValidateRleStreamPayload(ptr + headerBytes, compressedBytes, out _, out _);

            return true;
        }

        public static bool TryValidateRleStreamPayload(
            NativeArray<byte> streamBytes,
            int byteCount,
            out uint denseBytes,
            out uint streamFlags)
        {
            denseBytes = 0u;
            streamFlags = 0u;
            if (!streamBytes.IsCreated)
                return false;

            if (byteCount < 0 || byteCount > streamBytes.Length)
                return false;

            int count = byteCount;
            byte* ptr = (byte*)streamBytes.GetUnsafeReadOnlyPtr();
            return TryValidateRleStreamPayload(ptr, count, out denseBytes, out streamFlags);
        }

        public static bool RunSelfAudit(NativeArray<int> results)
        {
            if (!results.IsCreated || results.Length < 18)
                return false;

            results[0] = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
            results[1] = UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>();
            results[2] = UnsafeUtility.SizeOf<EntityDeltaBlockCounter64>();
            results[3] = UnsafeUtility.SizeOf<EntityCompressionTelemetryEntry>();
            results[4] = UnsafeUtility.SizeOf<EntityDeltaCompressionTuningDTO>();
            results[5] = UnsafeUtility.SizeOf<EntityDeltaSectorStatsDTO>();
            results[6] = UnsafeUtility.SizeOf<EntityCompressionProfileDTO>();
            results[7] = UnsafeUtility.SizeOf<EntityDeltaMockSchemaDTO>();
            results[8] = OffsetOfHeaderSectorHash();
            results[9] = OffsetOfHeaderCompressedSize();
            results[10] = OffsetOfHeaderUncompressedSize();
            results[11] = OffsetOfHeaderChecksum();
            results[12] = OffsetOfHeaderPad0();
            results[13] = OffsetOfHeaderPad1();
            results[14] = OffsetOfRecordStableEntityHash();
            results[15] = UnsafeUtility.SizeOf<EntityDeltaTelemetryDumpHeaderDTO>();
            results[16] = OffsetOfRecordFlags();
            results[17] = OffsetOfRecordSimulationTick();
            if (results.Length > 19)
            {
                results[18] = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
                results[19] = OffsetOfRleStoredBytes();
            }
            if (results.Length > 20)
                results[20] = CounterWalEnvelopeAuditPass;
            if (results.Length > 21)
                results[21] = CounterDecodePass;
            if (results.Length > 22)
                results[22] = unchecked((int)RleStreamFlagLittleEndianRecords);
            if (results.Length > 23)
                results[23] = CounterAuditCompressedRatioPpm;
            if (results.Length > 24)
                results[24] = CounterAuditSavingsPpm;
            if (results.Length > 25)
                results[25] = CounterAuditByteSavingsPass;
            bool basePass = results[0] == 32 &&
                            results[1] == 80 &&
                            results[2] == 64 &&
                            results[3] == 64 &&
                            results[4] == 64 &&
                            results[5] == 64 &&
                            results[6] == 32 &&
                            results[7] == 64 &&
                            results[8] == 0 &&
                            results[9] == 8 &&
                            results[10] == 12 &&
                            results[11] == 16 &&
                            results[12] == 24 &&
                            results[13] == 28 &&
                            results[14] == 40 &&
                            results[15] == 64 &&
                            results[16] == 68 &&
                            results[17] == 76;
            return basePass &&
                   (results.Length <= 19 || (results[18] == 16 && results[19] == 12)) &&
                   (results.Length <= 20 || results[20] == 18) &&
                   (results.Length <= 21 || results[21] == 21) &&
                   (results.Length <= 22 || results[22] == 256) &&
                   (results.Length <= 23 || results[23] == 22) &&
                   (results.Length <= 24 || results[24] == 23) &&
                   (results.Length <= 25 || results[25] == 24);
        }

        public static bool RunCompressionRatioSelfAudit(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            int minimumSamples,
            float requiredPassRatio01 = 0.99f,
            float requiredByteSavings01 = 0.99f)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int samples = 0;
            int smaller = 0;
            ulong fullBytes = 0ul;
            ulong compressedBytes = 0ul;
            for (int i = 0; i < telemetryRing.Length; i++)
            {
                EntityCompressionTelemetryEntry entry = telemetryRing[i];
                if (entry.FullSnapshotBytes == 0u || (entry.Flags & HeaderFlagFatal) != 0u)
                    continue;

                samples++;
                fullBytes += entry.FullSnapshotBytes;
                compressedBytes += entry.CompressedBytes;
                if (entry.CompressedBytes < entry.FullSnapshotBytes)
                    smaller++;
            }

            if (samples < math.max(1, minimumSamples))
                return false;

            float ratio = (float)smaller / samples;
            int requiredSavingsPpm = RequiredSavingsPpm(requiredByteSavings01);
            int observedSavingsPpm = CalculateSavingsPpm(fullBytes, compressedBytes);
            return ratio >= math.saturate(requiredPassRatio01) && observedSavingsPpm >= requiredSavingsPpm;
        }

        public static JobHandle ScheduleCompressionRatioSelfAudit(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> counters,
            int minimumSamples,
            float requiredPassRatio01,
            JobHandle dependency,
            float requiredByteSavings01 = 0.99f)
        {
            if (!telemetryRing.IsCreated || !counters.IsCreated || counters.Length < CounterCapacity)
                return dependency;

            return new EntityDeltaCompressionRatioAuditJob
            {
                TelemetryRing = telemetryRing,
                Counters = counters,
                MinimumSamples = minimumSamples,
                RequiredPassRatio01 = requiredPassRatio01,
                RequiredByteSavings01 = requiredByteSavings01
            }.Schedule(dependency);
        }

        public static bool TryDumpTelemetryRing(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            string path = "Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin")
        {
            return TryDumpTelemetryRing(telemetryRing, default, 0u, path);
        }

        public static bool TryDumpTelemetryRing(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            uint reasonFlags = 0u,
            string path = "Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin")
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0 || string.IsNullOrEmpty(path))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int stride = UnsafeUtility.SizeOf<EntityCompressionTelemetryEntry>();
                int capacity = math.min(TelemetryRingFrames, telemetryRing.Length);
                int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) % capacity : 0;
                int entryCount = CountTelemetryEntries(telemetryRing, capacity);
                int start = entryCount >= capacity ? cursor : 0;
                int headerBytes = UnsafeUtility.SizeOf<EntityDeltaTelemetryDumpHeaderDTO>();
                if (headerBytes != 64 || stride != 64)
                    return false;

                EntityCompressionTelemetryEntry first = entryCount > 0 ? telemetryRing[start] : default;
                EntityCompressionTelemetryEntry last = entryCount > 0 ? telemetryRing[(start + entryCount - 1) % capacity] : default;
                byte* headerBytesLe = stackalloc byte[64];
                byte* telemetryBytesLe = stackalloc byte[64];
                WriteTelemetryDumpHeaderLittleEndian(
                    headerBytesLe,
                    (uint)entryCount,
                    (uint)stride,
                    (uint)cursor,
                    reasonFlags,
                    (uint)capacity,
                    (uint)headerBytes,
                    first,
                    last);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>(headerBytesLe, headerBytes));
                    for (int i = 0; i < entryCount; i++)
                    {
                        int index = (start + i) % capacity;
                        WriteTelemetryEntryLittleEndian(telemetryBytesLe, telemetryRing[index]);
                        stream.Write(new ReadOnlySpan<byte>(telemetryBytesLe, stride));
                    }
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static bool TryDumpTelemetryRingOnLatencySpike(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            float diskWriteLatencyMs,
            float thresholdMs = 50f,
            string path = "Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin")
        {
            float latency = math.isfinite(diskWriteLatencyMs) ? diskWriteLatencyMs : 0f;
            float threshold = math.max(0f, math.isfinite(thresholdMs) ? thresholdMs : 50f);
            return latency >= threshold && TryDumpTelemetryRing(telemetryRing, telemetryCursor, TelemetryFlagDiskLatencySpike, path);
        }

        public static JobHandle ScheduleDiskLatencyTelemetryPatch(
            NativeArray<EntityCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            ulong sectorHash,
            uint frame,
            float diskWriteLatencyMs,
            JobHandle dependency,
            float spikeThresholdMs = 50f,
            bool matchFrame = true)
        {
            if (!telemetryRing.IsCreated || !telemetryCursor.IsCreated)
                return dependency;

            return new EntityDeltaDiskLatencyTelemetryPatchJob
            {
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
                SectorHash = sectorHash,
                Frame = frame,
                DiskWriteLatencyMs = diskWriteLatencyMs,
                SpikeThresholdMs = spikeThresholdMs,
                MatchFrame = matchFrame ? (byte)1 : (byte)0
            }.Schedule(dependency);
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.SavePersistence,
                options);
            return vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        private static bool VerifyCompressedPayloadChecksum(byte* ptr, int count, in EntityDeltaHeaderDTO header)
        {
            if (count <= 0)
                return header.XXHash3Checksum == 0UL;

            if (ptr == null)
                return false;

            SaveStateMerkleTree.Hash128(ptr, count, header.SectorHash ^ ChecksumSeed, out ulong lo, out ulong hi);
            ulong checksum = lo ^ ((hi << 32) | (hi >> 32));
            return checksum == header.XXHash3Checksum;
        }

        private static bool TryValidateRleStreamPayload(byte* ptr, int byteCount, out uint denseBytes, out uint streamFlags)
        {
            denseBytes = 0u;
            streamFlags = 0u;
            int headerBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
            if (ptr == null || byteCount < headerBytes)
                return false;

            EntityDeltaRleStreamHeaderDTO header = ReadRleStreamHeaderLittleEndian(ptr);
            uint modeMask = RleStreamFlagPairs | RleStreamFlagRawDense;
            uint endianMask = header.Flags & RleStreamEndianMask;
            if (header.Magic != RleStreamMagic ||
                (header.Flags & modeMask) == 0u ||
                (header.Flags & modeMask) == modeMask ||
                endianMask == 0u ||
                endianMask == RleStreamEndianMask ||
                header.DenseBytes == 0u ||
                header.DenseBytes > int.MaxValue ||
                header.StoredBytes > int.MaxValue ||
                header.StoredBytes != (uint)(byteCount - headerBytes))
            {
                return false;
            }

            int recordBytes = UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>();
            if ((header.DenseBytes % (uint)recordBytes) != 0u)
                return false;

            if ((header.Flags & RleStreamFlagRawDense) != 0u)
            {
                if (header.StoredBytes != header.DenseBytes)
                    return false;
            }
            else
            {
                if ((header.StoredBytes & 1u) != 0u ||
                    header.StoredBytes > header.DenseBytes * 2u ||
                    !TryValidateRlePairs(ptr + headerBytes, (int)header.StoredBytes, header.DenseBytes))
                {
                    return false;
                }
            }

            denseBytes = header.DenseBytes;
            streamFlags = header.Flags;
            return true;
        }

        private static bool TryValidateRlePairs(byte* ptr, int byteCount, uint denseBytes)
        {
            if (ptr == null || byteCount < 0 || (byteCount & 1) != 0)
                return false;

            uint decoded = 0u;
            for (int i = 0; i < byteCount; i += 2)
            {
                byte run = ptr[i];
                if (run == 0)
                    return false;

                if ((uint)run > denseBytes - decoded)
                    return false;

                decoded += run;
            }

            return decoded == denseBytes;
        }

        private static int CountTelemetryEntries(NativeArray<EntityCompressionTelemetryEntry> telemetryRing, int capacity)
        {
            int count = 0;
            int limit = math.min(capacity, telemetryRing.Length);
            for (int i = 0; i < limit; i++)
            {
                EntityCompressionTelemetryEntry entry = telemetryRing[i];
                if (entry.SectorHash != 0UL ||
                    entry.PayloadHash != 0UL ||
                    entry.FullSnapshotBytes != 0u ||
                    entry.CompressedBytes != 0u ||
                    entry.Flags != 0u ||
                    entry.Frame != 0u)
                {
                    count++;
                }
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateCompressedRatioPpm(ulong fullBytes, ulong compressedBytes)
        {
            if (fullBytes == 0ul)
                return 0;

            if (compressedBytes >= fullBytes)
                return (int)AuditPpmScale;

            ulong roundedUp = (compressedBytes * AuditPpmScale + fullBytes - 1ul) / fullBytes;
            return roundedUp > AuditPpmScale ? (int)AuditPpmScale : (int)roundedUp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateSavingsPpm(ulong fullBytes, ulong compressedBytes)
        {
            return (int)AuditPpmScale - CalculateCompressedRatioPpm(fullBytes, compressedBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RequiredSavingsPpm(float requiredByteSavings01)
        {
            float safe = math.saturate(math.isfinite(requiredByteSavings01) ? requiredByteSavings01 : 0.99f);
            return math.clamp((int)math.round(safe * (float)AuditPpmScale), 0, (int)AuditPpmScale);
        }

        private static int OffsetOfHeaderSectorHash()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.SectorHash) - origin);
        }

        private static int OffsetOfHeaderCompressedSize()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.CompressedSize) - origin);
        }

        private static int OffsetOfHeaderUncompressedSize()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.UncompressedSize) - origin);
        }

        private static int OffsetOfHeaderChecksum()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.XXHash3Checksum) - origin);
        }

        private static int OffsetOfHeaderPad0()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value._pad0) - origin);
        }

        private static int OffsetOfHeaderPad1()
        {
            EntityDeltaHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value._pad1) - origin);
        }

        private static int OffsetOfRecordStableEntityHash()
        {
            EntityDeltaDataRecordDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.StableEntityHash) - origin);
        }

        private static int OffsetOfRecordFlags()
        {
            EntityDeltaDataRecordDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.Flags) - origin);
        }

        private static int OffsetOfRecordSimulationTick()
        {
            EntityDeltaDataRecordDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.SimulationTick) - origin);
        }

        private static int OffsetOfRleStoredBytes()
        {
            EntityDeltaRleStreamHeaderDTO value = default;
            byte* origin = (byte*)UnsafeUtility.AddressOf(ref value);
            return (int)((byte*)UnsafeUtility.AddressOf(ref value.StoredBytes) - origin);
        }

        private struct NativeByteRange
        {
            public ulong Start;
            public ulong End;
        }

        private static bool HasCompressionPipelineAliasViolation(in EntityDeltaCompressionVaultBufferSet buffers)
        {
            const int RangeCapacity = 16;
            NativeByteRange* ranges = stackalloc NativeByteRange[RangeCapacity];
            int count = 0;
            bool ok =
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.CurrentRecords) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.BaselineRecords) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.DeltaRecords) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.BlockCounters) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.DenseBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.RleBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.CompressedBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.WalPayloadBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.Lz4HashTable) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.Headers) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.Counters) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.TelemetryRing) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.TelemetryCursor) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.Tuning) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.SectorStats) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, buffers.Profiles);
            if (!ok)
                return true;

            return HasNativeRangeOverlap(ranges, count);
        }

        private static bool HasWalDecodeAliasViolation(
            NativeArray<byte> walPayloadBytes,
            NativeArray<byte> rleBytes,
            NativeArray<byte> denseBytes,
            NativeArray<EntityDeltaDataRecordDTO> deltaRecords,
            NativeArray<EntityDeltaHeaderDTO> headers,
            NativeArray<int> counters)
        {
            const int RangeCapacity = 6;
            NativeByteRange* ranges = stackalloc NativeByteRange[RangeCapacity];
            int count = 0;
            bool ok =
                TryAddNativeRange(ranges, RangeCapacity, ref count, walPayloadBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, rleBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, denseBytes) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, deltaRecords) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, headers) &&
                TryAddNativeRange(ranges, RangeCapacity, ref count, counters);
            if (!ok)
                return true;

            return HasNativeRangeOverlap(ranges, count);
        }

        private static bool TryAddNativeRange<T>(
            NativeByteRange* ranges,
            int capacity,
            ref int count,
            NativeArray<T> array) where T : struct
        {
            if (ranges == null)
                return false;

            if (!TryGetNativeRange(array, out NativeByteRange range))
                return true;

            if (count >= capacity)
                return false;

            ranges[count] = range;
            count++;
            return true;
        }

        private static bool TryGetNativeRange<T>(NativeArray<T> array, out NativeByteRange range) where T : struct
        {
            range = default;
            if (!array.IsCreated || array.Length <= 0)
                return false;

            int stride = UnsafeUtility.SizeOf<T>();
            long byteLength = (long)array.Length * stride;
            if (byteLength <= 0L)
                return false;

            ulong start = ((UIntPtr)array.GetUnsafeReadOnlyPtr()).ToUInt64();
            ulong size = (ulong)byteLength;
            if (start > ulong.MaxValue - size)
            {
                range.Start = start;
                range.End = ulong.MaxValue;
                return true;
            }

            range.Start = start;
            range.End = start + size;
            return range.End > range.Start;
        }

        private static bool HasNativeRangeOverlap(NativeByteRange* ranges, int count)
        {
            if (ranges == null || count <= 1)
                return false;

            for (int i = 0; i < count - 1; i++)
            {
                NativeByteRange left = ranges[i];
                for (int j = i + 1; j < count; j++)
                {
                    NativeByteRange right = ranges[j];
                    if (left.Start < right.End && right.Start < left.End)
                        return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T ElementAsRef<T>(NativeArray<T> array, int index) where T : struct
        {
            return ref UnsafeUtility.AsRef<T>((byte*)array.GetUnsafePtr() + (index * UnsafeUtility.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T ElementAsReadOnlyRef<T>(NativeArray<T> array, int index) where T : struct
        {
            return ref UnsafeUtility.AsRef<T>((byte*)array.GetUnsafeReadOnlyPtr() + (index * UnsafeUtility.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsActive(in EntityDeltaDataRecordDTO record)
        {
            return record.StableEntityHash != 0UL || record.InstanceUid != 0u || (record.Flags & (EntityFlagActive | EntityFlagTombstone)) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDefaultRecord(in EntityDeltaDataRecordDTO record)
        {
            return record.SectorX == 0L &&
                   record.SectorY == 0L &&
                   record.SectorZ == 0L &&
                   record.LocalX == 0f &&
                   record.LocalY == 0f &&
                   record.LocalZ == 0f &&
                   record.EntityKindHash == 0u &&
                   record.StableEntityHash == 0UL &&
                   record.ArchetypeHash == 0u &&
                   record.InventoryHash == 0u &&
                   record.InstanceUid == 0u &&
                   record.Quantity == 0 &&
                   record.HealthMilli == 0 &&
                   record.HungerMilli == 0 &&
                   record.IntegrityMilli == 0 &&
                   record.Flags == 0u &&
                   record.BaselineHash32 == 0u &&
                   record.SimulationTick == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasFiniteLocalOffset(in EntityDeltaDataRecordDTO record)
        {
            return math.isfinite(record.LocalX) &&
                   math.isfinite(record.LocalY) &&
                   math.isfinite(record.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ComputeRecordHash64(in EntityDeltaDataRecordDTO record)
        {
            ulong h = 14695981039346656037UL;
            h = HashMix64(h, (ulong)record.SectorX);
            h = HashMix64(h, (ulong)record.SectorY);
            h = HashMix64(h, (ulong)record.SectorZ);
            h = HashMix64(h, math.asuint(record.LocalX));
            h = HashMix64(h, math.asuint(record.LocalY));
            h = HashMix64(h, math.asuint(record.LocalZ));
            h = HashMix64(h, record.EntityKindHash);
            h = HashMix64(h, record.StableEntityHash);
            h = HashMix64(h, record.ArchetypeHash);
            h = HashMix64(h, record.InventoryHash);
            h = HashMix64(h, record.InstanceUid);
            h = HashMix64(h, ((ulong)record.Quantity << 48) | ((ulong)record.HealthMilli << 32) | ((ulong)record.HungerMilli << 16) | record.IntegrityMilli);
            h = HashMix64(h, record.Flags);
            h = HashMix64(h, record.BaselineHash32);
            h = HashMix64(h, record.SimulationTick);
            return h == 0UL ? 1UL : h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong HashMix64(ulong hash, ulong value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SaturatingUIntToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SaturatingAdd(uint left, uint right)
        {
            return left > uint.MaxValue - right ? uint.MaxValue : left + right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long FloorDiv(long value, long divisor)
        {
            long safeDivisor = divisor == 0L ? 1L : divisor;
            long quotient = value / safeDivisor;
            long remainder = value % safeDivisor;
            return remainder != 0L && ((remainder < 0L) != (safeDivisor < 0)) ? quotient - 1L : quotient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SaturatingLongToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint EncodeSignedMorton21(int value)
        {
            const long bias = 1L << 20;
            const long max = (1L << 21) - 1L;
            long biased = (long)value + bias;
            if (biased <= 0L)
                return 0u;
            if (biased >= max)
                return (uint)max;
            return (uint)biased;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExpandBits21(uint value)
        {
            ulong x = value & 0x1FFFFFUL;
            x = (x | (x << 32)) & 0x001F00000000FFFFUL;
            x = (x | (x << 16)) & 0x001F0000FF0000FFUL;
            x = (x | (x << 8)) & 0x100F00F00F00F00FUL;
            x = (x | (x << 4)) & 0x10C30C30C30C30C3UL;
            x = (x | (x << 2)) & 0x1249249249249249UL;
            return x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUIntLittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUIntLittleEndian(byte* source, int offset)
        {
            return (uint)source[offset] |
                   ((uint)source[offset + 1] << 8) |
                   ((uint)source[offset + 2] << 16) |
                   ((uint)source[offset + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUShortLittleEndian(byte* destination, int offset, ushort value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUShortLittleEndian(byte* source, int offset)
        {
            return (ushort)(source[offset] | (source[offset + 1] << 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadUShortBigEndian(byte* source, int offset)
        {
            return (ushort)((source[offset] << 8) | source[offset + 1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteULongLittleEndian(byte* destination, int offset, ulong value)
        {
            WriteUIntLittleEndian(destination, offset, (uint)value);
            WriteUIntLittleEndian(destination, offset + 4, (uint)(value >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadULongLittleEndian(byte* source, int offset)
        {
            return ReadUIntLittleEndian(source, offset) |
                   ((ulong)ReadUIntLittleEndian(source, offset + 4) << 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUIntBigEndian(byte* source, int offset)
        {
            return ((uint)source[offset] << 24) |
                   ((uint)source[offset + 1] << 16) |
                   ((uint)source[offset + 2] << 8) |
                   source[offset + 3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadULongBigEndian(byte* source, int offset)
        {
            return ((ulong)ReadUIntBigEndian(source, offset) << 32) |
                   ReadUIntBigEndian(source, offset + 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteRecordLittleEndian(byte* destination, int offset, in EntityDeltaDataRecordDTO record)
        {
            WriteULongLittleEndian(destination, offset + 0, unchecked((ulong)record.SectorX));
            WriteULongLittleEndian(destination, offset + 8, unchecked((ulong)record.SectorY));
            WriteULongLittleEndian(destination, offset + 16, unchecked((ulong)record.SectorZ));
            WriteUIntLittleEndian(destination, offset + 24, math.asuint(record.LocalX));
            WriteUIntLittleEndian(destination, offset + 28, math.asuint(record.LocalY));
            WriteUIntLittleEndian(destination, offset + 32, math.asuint(record.LocalZ));
            WriteUIntLittleEndian(destination, offset + 36, record.EntityKindHash);
            WriteULongLittleEndian(destination, offset + 40, record.StableEntityHash);
            WriteUIntLittleEndian(destination, offset + 48, record.ArchetypeHash);
            WriteUIntLittleEndian(destination, offset + 52, record.InventoryHash);
            WriteUIntLittleEndian(destination, offset + 56, record.InstanceUid);
            WriteUShortLittleEndian(destination, offset + 60, record.Quantity);
            WriteUShortLittleEndian(destination, offset + 62, record.HealthMilli);
            WriteUShortLittleEndian(destination, offset + 64, record.HungerMilli);
            WriteUShortLittleEndian(destination, offset + 66, record.IntegrityMilli);
            WriteUIntLittleEndian(destination, offset + 68, record.Flags);
            WriteUIntLittleEndian(destination, offset + 72, record.BaselineHash32);
            WriteUIntLittleEndian(destination, offset + 76, record.SimulationTick);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static EntityDeltaDataRecordDTO ReadRecordLittleEndian(byte* source, int offset)
        {
            return new EntityDeltaDataRecordDTO
            {
                SectorX = unchecked((long)ReadULongLittleEndian(source, offset + 0)),
                SectorY = unchecked((long)ReadULongLittleEndian(source, offset + 8)),
                SectorZ = unchecked((long)ReadULongLittleEndian(source, offset + 16)),
                LocalX = math.asfloat(ReadUIntLittleEndian(source, offset + 24)),
                LocalY = math.asfloat(ReadUIntLittleEndian(source, offset + 28)),
                LocalZ = math.asfloat(ReadUIntLittleEndian(source, offset + 32)),
                EntityKindHash = ReadUIntLittleEndian(source, offset + 36),
                StableEntityHash = ReadULongLittleEndian(source, offset + 40),
                ArchetypeHash = ReadUIntLittleEndian(source, offset + 48),
                InventoryHash = ReadUIntLittleEndian(source, offset + 52),
                InstanceUid = ReadUIntLittleEndian(source, offset + 56),
                Quantity = ReadUShortLittleEndian(source, offset + 60),
                HealthMilli = ReadUShortLittleEndian(source, offset + 62),
                HungerMilli = ReadUShortLittleEndian(source, offset + 64),
                IntegrityMilli = ReadUShortLittleEndian(source, offset + 66),
                Flags = ReadUIntLittleEndian(source, offset + 68),
                BaselineHash32 = ReadUIntLittleEndian(source, offset + 72),
                SimulationTick = ReadUIntLittleEndian(source, offset + 76)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static EntityDeltaDataRecordDTO ReadRecordBigEndian(byte* source, int offset)
        {
            return new EntityDeltaDataRecordDTO
            {
                SectorX = unchecked((long)ReadULongBigEndian(source, offset + 0)),
                SectorY = unchecked((long)ReadULongBigEndian(source, offset + 8)),
                SectorZ = unchecked((long)ReadULongBigEndian(source, offset + 16)),
                LocalX = math.asfloat(ReadUIntBigEndian(source, offset + 24)),
                LocalY = math.asfloat(ReadUIntBigEndian(source, offset + 28)),
                LocalZ = math.asfloat(ReadUIntBigEndian(source, offset + 32)),
                EntityKindHash = ReadUIntBigEndian(source, offset + 36),
                StableEntityHash = ReadULongBigEndian(source, offset + 40),
                ArchetypeHash = ReadUIntBigEndian(source, offset + 48),
                InventoryHash = ReadUIntBigEndian(source, offset + 52),
                InstanceUid = ReadUIntBigEndian(source, offset + 56),
                Quantity = ReadUShortBigEndian(source, offset + 60),
                HealthMilli = ReadUShortBigEndian(source, offset + 62),
                HungerMilli = ReadUShortBigEndian(source, offset + 64),
                IntegrityMilli = ReadUShortBigEndian(source, offset + 66),
                Flags = ReadUIntBigEndian(source, offset + 68),
                BaselineHash32 = ReadUIntBigEndian(source, offset + 72),
                SimulationTick = ReadUIntBigEndian(source, offset + 76)
            };
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityScheduleFailureJob : IJob
        {
            public NativeArray<int> Counters;
            public NativeArray<EntityDeltaHeaderDTO> Headers;
            public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;
            public int3 SectorCoord;
            public int FailureCode;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                Counters[CounterDeltaRecordCount] = 0;
                Counters[CounterDenseBytes] = 0;
                Counters[CounterRleBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterFailure] = 1;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterWalEnvelopeAuditPass] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;

                if (Headers.IsCreated && Headers.Length > 0)
                {
                    Headers[0] = new EntityDeltaHeaderDTO
                    {
                        SectorHash = SectorHash,
                        CompressedSize = 0u,
                        UncompressedSize = 0u,
                        XXHash3Checksum = 0UL,
                        _pad0 = unchecked((uint)FailureCode),
                        _pad1 = 0u
                    };
                }

                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    EntityDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.SectorHash = SectorHash;
                    stats.SectorX = SectorCoord.x;
                    stats.SectorY = SectorCoord.y;
                    stats.SectorZ = SectorCoord.z;
                    stats.DenseDeltaBytes = 0u;
                    stats.RleBytes = 0u;
                    stats.CompressedBytes = 0u;
                    stats.DeltaEntities = 0u;
                    stats.Flags |= HeaderFlagFatal;
                    SectorStats[0] = stats;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateMockEntityStateJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Unity's parallel-for restriction cannot infer that Execute(index) writes only the same index in CurrentRecords and
            // BaselineRecords. The suppressed check is a false positive here because the only write indices are the scheduler-owned
            // parallel-for index after the EntityCount/Length guards, so two workers cannot target the same record slot.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Keeping the safety restriction and writing through NativeArray<T>[index] was rejected because the hot DTO mutation path
            // needs UnsafeUtility.AsRef access to avoid defensive copies. Duplicating mock state into per-worker scratch buffers was
            // rejected because it would add Vault capacity and a second copy phase solely to satisfy an editor/CI fallback route.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: job instance N owns CurrentRecords[N] and, when present, BaselineRecords[N] exclusively for the duration of
            // this job. No other scheduled stage writes these arrays until this handle completes and the next pipeline stage depends
            // on it, preserving one-writer-per-record ownership.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaDataRecordDTO> CurrentRecords;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaDataRecordDTO> BaselineRecords;
            public ulong SectorHash;
            public int3 SectorCoord;
            public uint SimulationFrame;
            public float GlobalQualityWeight;
            public float MutationRate01;
            public int EntityCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)EntityCount || index >= CurrentRecords.Length)
                    return;

                uint seed = BuildDeterministicSeed(SectorHash, SimulationFrame, (uint)index);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
                float mutateRate = math.saturate(math.isfinite(MutationRate01) ? MutationRate01 : 0.08f);
                float activeRate = math.lerp(0.10f, 0.42f, quality * quality);
                float active = math.step(rng.NextFloat(), activeRate);
                uint kind = 0xED100000u | (uint)(seed & 31u);
                uint uid = (uint)(index + 1);
                EntityDeltaDataRecordDTO baseline = default;
                EntityDeltaDataRecordDTO current = default;

                if (active > 0f)
                {
                    float3 local = new float3(
                        (rng.NextFloat() - 0.5f) * DefaultSectorMeters,
                        (rng.NextFloat() - 0.5f) * DefaultSectorMeters,
                        (rng.NextFloat() - 0.5f) * DefaultSectorMeters);
                    ushort health = (ushort)math.clamp(500 + (int)(seed & 511u), 1, 1000);
                    ushort hunger = (ushort)math.clamp((int)((seed >> 9) & 1023u), 0, 1000);
                    ushort integrity = (ushort)math.clamp(700 + (int)((seed >> 18) & 255u), 1, 1000);
                    uint flags = EntityFlagActive | EntityFlagDynamic;
                    if ((seed & 7u) == 0u)
                        flags |= EntityFlagDehydrated;
                    if ((seed & 63u) == 0u)
                        flags = EntityFlagTombstone | EntityFlagPruned;

                    baseline = new EntityDeltaDataRecordDTO
                    {
                        SectorX = SectorCoord.x,
                        SectorY = SectorCoord.y,
                        SectorZ = SectorCoord.z,
                        LocalX = local.x,
                        LocalY = local.y,
                        LocalZ = local.z,
                        EntityKindHash = kind,
                        StableEntityHash = ((ulong)kind << 32) ^ uid ^ 0xA0761D6478BD642FUL,
                        ArchetypeHash = 0xA5000000u | (seed & 255u),
                        InventoryHash = (seed & 3u) == 0u ? 0u : (0xB6000000u | ((seed >> 8) & 65535u)),
                        InstanceUid = uid,
                        Quantity = (ushort)math.max(1, (int)(seed & 7u)),
                        HealthMilli = health,
                        HungerMilli = hunger,
                        IntegrityMilli = integrity,
                        Flags = flags & ~EntityFlagPruned,
                        BaselineHash32 = 0u,
                        SimulationTick = SimulationFrame
                    };
                    baseline.BaselineHash32 = (uint)ComputeRecordHash64(in baseline);
                    current = baseline;

                    if (rng.NextFloat() < mutateRate)
                    {
                        current.HealthMilli = (ushort)math.clamp(current.HealthMilli - (ushort)(1 + (seed & 127u)), 1, 1000);
                        current.HungerMilli = (ushort)math.clamp(current.HungerMilli + (ushort)(seed & 63u), 0, 1000);
                        current.InventoryHash ^= 0x6D2B79F5u ^ seed;
                        current.Flags |= EntityFlagDehydrated;
                        current.SimulationTick = SimulationFrame;
                    }
                }

                if (BaselineRecords.IsCreated && index < BaselineRecords.Length)
                    ElementAsRef(BaselineRecords, index) = baseline;
                ElementAsRef(CurrentRecords, index) = current;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint BuildDeterministicSeed(ulong sectorHash, uint frame, uint index)
            {
                ulong x = sectorHash ^ ((ulong)frame << 32) ^ index ^ 0xD1B54A32D192ED03UL;
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return (uint)(x | 1UL);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityTombstonePruneJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Unity's restriction is conservative because each worker writes a slice of CurrentRecords rather than only index == job
            // index. The block partition is non-overlapping by construction: start = blockIndex * BlockEntityCount and end is clamped
            // to the next block boundary, so two valid block indices cannot write the same record or counter row.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Serial tombstone pruning was rejected because it would make long-session tombstone cleanup a single-worker save spike.
            // A separate compacted tombstone buffer was rejected because the SaveSystem lane owns flat Vault records and must not add
            // a second authority table for the same entity existence fact.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: block worker B owns CurrentRecords[B * BlockEntityCount, min(end)) and BlockCounters[B]. BlockEntityCount is
            // clamped to at least 1, BlockCounters bounds gate every worker, and extraction depends on the prune handle before reading
            // CurrentRecords or BlockCounters.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaDataRecordDTO> CurrentRecords;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaBlockCounter64> BlockCounters;
            public int EntityCount;
            public int BlockEntityCount;
            public ulong SectorHash;
            public uint SimulationFrame;
            public uint TombstoneMaxTicks;

            public void Execute(int blockIndex)
            {
                if (!CurrentRecords.IsCreated || !BlockCounters.IsCreated || (uint)blockIndex >= (uint)BlockCounters.Length)
                    return;

                int blockSize = math.max(1, BlockEntityCount);
                int start = blockIndex * blockSize;
                int end = math.min(math.min(EntityCount, CurrentRecords.Length), start + blockSize);
                uint pruned = 0u;
                for (int i = start; i < end; i++)
                {
                    ref EntityDeltaDataRecordDTO record = ref ElementAsRef(CurrentRecords, i);
                    if ((record.Flags & EntityFlagTombstone) == 0u || ResolveSectorHash(in record) != SectorHash)
                        continue;

                    uint age = SimulationFrame >= record.SimulationTick ? SimulationFrame - record.SimulationTick : 0u;
                    if (age < math.max(1u, TombstoneMaxTicks))
                        continue;

                    record = default;
                    pruned++;
                }

                ElementAsRef(BlockCounters, blockIndex) = new EntityDeltaBlockCounter64
                {
                    DeltaCount = 0u,
                    ActiveCount = 0u,
                    TombstoneCount = 0u,
                    EncodedBytes = 0u,
                    SectorHash = SectorHash,
                    Flags = pruned > 0u ? HeaderFlagPruned : 0u,
                    PrunedTombstones = pruned,
                    HashXor = 0UL,
                    _pad0 = 0UL,
                    _pad1 = 0UL,
                    _pad2 = 0UL
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct ExtractEntityDeltaJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaDataRecordDTO> CurrentRecords;
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaDataRecordDTO> BaselineRecords;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Unity cannot prove that appends into DeltaRecords are partitioned because each block writes a variable count. The
            // suppression is valid because every block writes only inside [blockIndex * MaxDeltasPerBlock, blockBase + MaxDeltasPerBlock)
            // and the counter write is exactly BlockCounters[blockIndex].
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A NativeList<byte>.ParallelWriter route was rejected because this lane is Vault-owned and must avoid runtime growth,
            // allocator pressure, and unstable append order. Atomic global append counters were rejected because they would introduce
            // contention and false sharing into the delta extraction inner loop.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: block worker B scans CurrentRecords for its own [start,end) window and writes delta rows only to its fixed
            // DeltaRecords block plus BlockCounters[B]. MaxDeltasPerBlock bounds every write, and the finalize/dense-pack jobs depend
            // on this handle before reading any produced rows.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaDataRecordDTO> DeltaRecords;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<EntityDeltaBlockCounter64> BlockCounters;
            public int EntityCount;
            public int BlockEntityCount;
            public int MaxDeltasPerBlock;
            public ulong SectorHash;

            public void Execute(int blockIndex)
            {
                if (!CurrentRecords.IsCreated || !DeltaRecords.IsCreated || !BlockCounters.IsCreated || (uint)blockIndex >= (uint)BlockCounters.Length)
                    return;

                ref readonly EntityDeltaBlockCounter64 previous = ref ElementAsReadOnlyRef(BlockCounters, blockIndex);
                int blockSize = math.max(1, BlockEntityCount);
                int start = blockIndex * blockSize;
                int end = math.min(math.min(EntityCount, CurrentRecords.Length), start + blockSize);
                int maxDeltas = math.max(1, MaxDeltasPerBlock);
                int writeBase = blockIndex * maxDeltas;
                int writeLimit = math.min(DeltaRecords.Length, writeBase + maxDeltas);
                int write = writeBase;
                uint activeCount = 0u;
                uint tombstoneCount = 0u;
                uint dehydratedCount = 0u;
                uint flags = previous.Flags;
                ulong hashXor = previous.HashXor;
                bool overflow = false;

                for (int i = start; i < end; i++)
                {
                    ref readonly EntityDeltaDataRecordDTO current = ref ElementAsReadOnlyRef(CurrentRecords, i);
                    if (!IsActive(in current) || ResolveSectorHash(in current) != SectorHash)
                        continue;

                    activeCount++;
                    if ((current.Flags & EntityFlagTombstone) != 0u)
                        tombstoneCount++;
                    if ((current.Flags & EntityFlagDehydrated) != 0u)
                        dehydratedCount++;
                    if (!HasFiniteLocalOffset(in current))
                    {
                        flags |= HeaderFlagFatal;
                        continue;
                    }

                    EntityDeltaDataRecordDTO baseline = default;
                    if (BaselineRecords.IsCreated && i < BaselineRecords.Length)
                    {
                        ref readonly EntityDeltaDataRecordDTO baselineRef = ref ElementAsReadOnlyRef(BaselineRecords, i);
                        baseline = baselineRef;
                    }
                    ulong currentHash = ComputeRecordHash64(in current);
                    ulong baselineHash = IsDefaultRecord(in baseline) ? 0UL : ComputeRecordHash64(in baseline);
                    if (currentHash == baselineHash)
                        continue;

                    EntityDeltaDataRecordDTO delta = current;
                    delta.BaselineHash32 = (uint)(baselineHash ^ (baselineHash >> 32));
                    if (write < writeLimit)
                    {
                        ElementAsRef(DeltaRecords, write) = delta;
                        write++;
                        hashXor ^= currentHash;
                    }
                    else
                    {
                        overflow = true;
                    }
                }

                uint deltaCount = (uint)math.max(0, write - writeBase);
                if (dehydratedCount > 0u)
                    flags |= EntityFlagDehydrated;
                if (overflow)
                    flags |= HeaderFlagFatal;

                ElementAsRef(BlockCounters, blockIndex) = new EntityDeltaBlockCounter64
                {
                    DeltaCount = deltaCount,
                    ActiveCount = activeCount,
                    TombstoneCount = tombstoneCount,
                    EncodedBytes = deltaCount * (uint)UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>(),
                    SectorHash = SectorHash,
                    Flags = flags,
                    PrunedTombstones = previous.PrunedTombstones,
                    HashXor = hashXor,
                    _pad0 = (ulong)dehydratedCount,
                    _pad1 = 0UL,
                    _pad2 = 0UL
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaFinalizeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaBlockCounter64> BlockCounters;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
            public int BlockCount;
            public int EntityCount;
            public ulong SectorHash;
            public int3 SectorCoord;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                int blockLimit = BlockCounters.IsCreated ? math.min(BlockCount, BlockCounters.Length) : 0;
                uint deltaCount = 0u;
                uint activeCount = 0u;
                uint tombstones = 0u;
                uint denseBytes = 0u;
                uint pruned = 0u;
                uint dehydrated = 0u;
                uint flags = 0u;
                for (int i = 0; i < blockLimit; i++)
                {
                    EntityDeltaBlockCounter64 counter = BlockCounters[i];
                    deltaCount = SaturatingAdd(deltaCount, counter.DeltaCount);
                    activeCount = SaturatingAdd(activeCount, counter.ActiveCount);
                    tombstones = SaturatingAdd(tombstones, counter.TombstoneCount);
                    denseBytes = SaturatingAdd(denseBytes, counter.EncodedBytes);
                    pruned = SaturatingAdd(pruned, counter.PrunedTombstones);
                    dehydrated = SaturatingAdd(dehydrated, (uint)math.min(counter._pad0, (ulong)uint.MaxValue));
                    flags |= counter.Flags;
                }

                int recordBytes = UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>();
                int fullBytes = EntityCount > int.MaxValue / recordBytes ? int.MaxValue : EntityCount * recordBytes;
                Counters[CounterDeltaRecordCount] = SaturatingUIntToInt(deltaCount);
                Counters[CounterActiveEntityCount] = SaturatingUIntToInt(activeCount);
                Counters[CounterTombstoneCount] = SaturatingUIntToInt(tombstones);
                Counters[CounterDenseBytes] = SaturatingUIntToInt(denseBytes);
                Counters[CounterRleBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterFullSnapshotBytes] = fullBytes;
                Counters[CounterFailure] = (flags & HeaderFlagFatal) != 0u ? 1 : 0;
                Counters[CounterCompressionFlags] = (int)flags;
                Counters[CounterBlockCount] = blockLimit;
                Counters[CounterWalPayloadBytes] = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                Counters[CounterPrunedTombstones] = SaturatingUIntToInt(pruned);
                Counters[CounterDehydratedCount] = SaturatingUIntToInt(dehydrated);
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterWalEnvelopeAuditPass] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;

                if (Headers.IsCreated && Headers.Length > 0)
                {
                    Headers[0] = new EntityDeltaHeaderDTO
                    {
                        SectorHash = SectorHash,
                        CompressedSize = 0u,
                        UncompressedSize = denseBytes,
                        XXHash3Checksum = 0UL,
                        _pad0 = 0u,
                        _pad1 = 0u
                    };
                }

                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    SectorStats[0] = new EntityDeltaSectorStatsDTO
                    {
                        SectorHash = SectorHash,
                        SectorX = SectorCoord.x,
                        SectorY = SectorCoord.y,
                        SectorZ = SectorCoord.z,
                        FullSnapshotBytes = (uint)math.max(0, fullBytes),
                        DenseDeltaBytes = denseBytes,
                        RleBytes = 0u,
                        CompressedBytes = 0u,
                        DeltaEntities = deltaCount,
                        ActiveEntities = activeCount,
                        CompressionRatio01 = fullBytes > 0 ? math.saturate((float)denseBytes / fullBytes) : 0f,
                        DeltaRatio01 = EntityCount > 0 ? math.saturate((float)deltaCount / EntityCount) : 0f,
                        Flags = flags,
                        _pad0 = 0UL
                    };
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaDensePackJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaDataRecordDTO> DeltaRecords;
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaBlockCounter64> BlockCounters;
            [NoAlias] public NativeArray<byte> DenseBytes;
            [NoAlias] public NativeArray<int> Counters;
            public int BlockCount;
            public int MaxDeltasPerBlock;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                if (!DeltaRecords.IsCreated || !BlockCounters.IsCreated || !DenseBytes.IsCreated)
                {
                    FailDensePack();
                    return;
                }

                if (Counters[CounterFailure] != 0)
                {
                    ResetDownstreamByteCounters();
                    return;
                }

                int recordBytes = UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>();
                int write = 0;
                int blockLimit = math.min(BlockCount, BlockCounters.Length);
                int maxDeltas = math.max(1, MaxDeltasPerBlock);
                byte* destination = (byte*)DenseBytes.GetUnsafePtr();
                for (int block = 0; block < blockLimit; block++)
                {
                    int sourceRecord = block * maxDeltas;
                    int count = (int)math.min(BlockCounters[block].DeltaCount, (uint)maxDeltas);
                    int bytes = count * recordBytes;
                    if (sourceRecord < 0 || sourceRecord + count > DeltaRecords.Length || write > DenseBytes.Length - bytes)
                    {
                        FailDensePack();
                        return;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        ref readonly EntityDeltaDataRecordDTO record = ref ElementAsReadOnlyRef(DeltaRecords, sourceRecord + i);
                        WriteRecordLittleEndian(destination, write, in record);
                        write += recordBytes;
                    }
                }

                Counters[CounterDenseBytes] = write;
            }

            private void FailDensePack()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                ResetDownstreamByteCounters();
            }

            private void ResetDownstreamByteCounters()
            {
                Counters[CounterDenseBytes] = 0;
                Counters[CounterRleBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterWalEnvelopeAuditPass] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityRlePreconditionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [NoAlias] public NativeArray<byte> Destination;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
            public float RleMinSaving01;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                if (!Source.IsCreated || !Destination.IsCreated || !Headers.IsCreated || Headers.Length <= 0)
                {
                    FailRlePrecondition();
                    return;
                }

                int sourceLength = math.clamp(Counters[CounterDenseBytes], 0, Source.Length);
                if (sourceLength <= 0 || Counters[CounterFailure] != 0)
                {
                    Counters[CounterRleBytes] = 0;
                    EntityDeltaHeaderDTO header = Headers[0];
                    header.UncompressedSize = 0u;
                    Headers[0] = header;
                    return;
                }

                int streamHeaderBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
                if (Destination.Length < streamHeaderBytes || sourceLength > Destination.Length - streamHeaderBytes)
                {
                    FailRlePrecondition();
                    return;
                }

                int write = streamHeaderBytes;
                int read = 0;
                bool rleOverflow = false;
                while (read < sourceLength)
                {
                    byte value = Source[read];
                    int run = 1;
                    while (read + run < sourceLength && run < 255 && Source[read + run] == value)
                        run++;

                    if (write > Destination.Length - 2)
                    {
                        rleOverflow = true;
                        break;
                    }

                    Destination[write++] = (byte)run;
                    Destination[write++] = value;
                    read += run;
                }

                float minSaving = math.saturate(math.isfinite(RleMinSaving01) ? RleMinSaving01 : 0.015f);
                int requiredSavingBytes = (int)math.ceil(sourceLength * minSaving);
                int rlePayloadBytes = math.max(0, write - streamHeaderBytes);
                bool useRle = !rleOverflow && rlePayloadBytes + requiredSavingBytes < sourceLength;
                int storedPayloadBytes = useRle ? rlePayloadBytes : sourceLength;
                if (!useRle)
                    UnsafeUtility.MemCpy((byte*)Destination.GetUnsafePtr() + streamHeaderBytes, Source.GetUnsafeReadOnlyPtr(), sourceLength);

                WriteRleStreamHeaderLittleEndian(
                    (byte*)Destination.GetUnsafePtr(),
                    (useRle ? RleStreamFlagPairs : RleStreamFlagRawDense) | RleStreamFlagLittleEndianRecords,
                    (uint)sourceLength,
                    (uint)storedPayloadBytes);

                int storedBytes = streamHeaderBytes + storedPayloadBytes;
                Counters[CounterRleBytes] = storedBytes;
                Counters[CounterCompressionFlags] = (Counters[CounterCompressionFlags] & ~(int)(HeaderFlagRle | HeaderFlagRleBypassed)) | (useRle ? (int)HeaderFlagRle : (int)HeaderFlagRleBypassed);
                if (Headers.IsCreated && Headers.Length > 0)
                {
                    EntityDeltaHeaderDTO header = Headers[0];
                    header.UncompressedSize = (uint)storedBytes;
                    Headers[0] = header;
                }
                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    EntityDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.RleBytes = (uint)storedBytes;
                    stats.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    SectorStats[0] = stats;
                }
            }

            private void FailRlePrecondition()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterRleBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterWalEnvelopeAuditPass] = 0;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;
                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    EntityDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.RleBytes = 0u;
                    stats.CompressedBytes = 0u;
                    stats.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    SectorStats[0] = stats;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityLz4CompressionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [NoAlias] public NativeArray<byte> Destination;
            [NoAlias] public NativeArray<int> HashTable;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;
            public float CompressionEffort01;
            public float IoPressure01;
            public int SourceLengthCounterIndex;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                if (!Source.IsCreated || !Destination.IsCreated || !HashTable.IsCreated || !Headers.IsCreated || Headers.Length <= 0)
                {
                    FailCompressionStage();
                    return;
                }

                int sourceLength = SourceLengthCounterIndex >= 0 && SourceLengthCounterIndex < Counters.Length ? Counters[SourceLengthCounterIndex] : 0;
                sourceLength = math.clamp(sourceLength, 0, Source.Length);
                if (sourceLength <= 0 || Counters[CounterFailure] != 0)
                {
                    Headers[0] = new EntityDeltaHeaderDTO
                    {
                        SectorHash = SectorHash,
                        CompressedSize = 0u,
                        UncompressedSize = 0u,
                        XXHash3Checksum = 0UL,
                        _pad0 = 0u,
                        _pad1 = 0u
                    };
                    Counters[CounterCompressedBytes] = 0;
                    Counters[CounterWalPayloadBytes] = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                    WriteSectorStatsCompression(0, Counters[CounterCompressionFlags]);
                    return;
                }

                float effort = math.saturate(math.isfinite(CompressionEffort01) ? CompressionEffort01 : 0f);
                float pressure = math.saturate(math.isfinite(IoPressure01) ? IoPressure01 : 0f);
                int hashCapacity = HashTable.Length;
                if (hashCapacity <= 0)
                {
                    FailCompressionStage();
                    return;
                }

                int minHashSlots = math.min(256, hashCapacity);
                int activeHashSlots = math.clamp((int)math.round(math.lerp(512f, hashCapacity, effort * (1f - (pressure * 0.25f)))), minHashSlots, hashCapacity);
                int minMatchLength = math.clamp((int)math.round(math.lerp(8f, 4f, effort)), 4, 8);
                int probeStep = math.clamp((int)math.round(math.lerp(4f, 1f, effort)), 1, 4);

                for (int i = 0; i < activeHashSlots; i++)
                    HashTable[i] = -1;

                int compressedBytes = sourceLength >= 16
                    ? CompressBlock(sourceLength, activeHashSlots, minMatchLength, probeStep)
                    : -1;
                bool useRaw = compressedBytes <= 0 || compressedBytes >= sourceLength;
                int storedBytes = useRaw ? sourceLength : compressedBytes;
                if (storedBytes > Destination.Length)
                {
                    FailCompressionStage();
                    return;
                }

                if (useRaw)
                    UnsafeUtility.MemCpy(Destination.GetUnsafePtr(), Source.GetUnsafeReadOnlyPtr(), sourceLength);

                Counters[CounterCompressedBytes] = storedBytes;
                Counters[CounterCompressionFlags] = (Counters[CounterCompressionFlags] & ~(int)(HeaderFlagLz4 | HeaderFlagRaw)) | (useRaw ? (int)HeaderFlagRaw : (int)HeaderFlagLz4);
                Counters[CounterWalPayloadBytes] = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>() + storedBytes;
                Headers[0] = new EntityDeltaHeaderDTO
                {
                    SectorHash = SectorHash,
                    CompressedSize = (uint)storedBytes,
                    UncompressedSize = (uint)sourceLength,
                    XXHash3Checksum = 0UL,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                WriteSectorStatsCompression(storedBytes, Counters[CounterCompressionFlags]);
            }

            private void FailCompressionStage()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterWalEnvelopeAuditPass] = 0;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;
                WriteSectorStatsCompression(0, Counters[CounterCompressionFlags]);
            }

            private int CompressBlock(int sourceLength, int activeHashSlots, int minMatchLength, int probeStep)
            {
                int anchor = 0;
                int read = 0;
                int write = 0;
                int literalTailLimit = math.max(0, sourceLength - Lz4LastLiterals);
                int lastMatchStart = math.min(math.max(0, sourceLength - minMatchLength), math.max(0, sourceLength - Lz4MfLimit));
                while (read <= lastMatchStart)
                {
                    uint sequence = ReadUInt32(read);
                    int hash = ResolveLz4Hash(sequence, activeHashSlots);
                    int previous = HashTable[hash];
                    HashTable[hash] = read;

                    if (previous >= 0 &&
                        read - previous <= ushort.MaxValue &&
                        previous + minMatchLength <= sourceLength &&
                        read + minMatchLength <= literalTailLimit &&
                        EqualsBytes(previous, read, minMatchLength))
                    {
                        int matchLength = minMatchLength;
                        while (read + matchLength < literalTailLimit &&
                               Source[previous + matchLength] == Source[read + matchLength])
                        {
                            matchLength++;
                        }

                        if (!WriteSequence(anchor, read, previous, matchLength, ref write))
                            return -1;

                        read += matchLength;
                        anchor = read;
                        continue;
                    }

                    read += probeStep;
                }

                if (!WriteLastLiterals(anchor, sourceLength - anchor, ref write) || write >= sourceLength)
                    return -1;

                return write;
            }

            private uint ReadUInt32(int offset)
            {
                return (uint)Source[offset] |
                       ((uint)Source[offset + 1] << 8) |
                       ((uint)Source[offset + 2] << 16) |
                       ((uint)Source[offset + 3] << 24);
            }

            private bool EqualsBytes(int left, int right, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Source[left + i] != Source[right + i])
                        return false;
                }

                return true;
            }

            private static int ResolveLz4Hash(uint sequence, int hashLength)
            {
                uint mixed = sequence * 2654435761u;
                return hashLength <= 1 ? 0 : (int)(mixed % (uint)hashLength);
            }

            private bool WriteSequence(int anchor, int read, int previous, int matchLength, ref int write)
            {
                int literalLength = read - anchor;
                int tokenOffset = write++;
                if (tokenOffset >= Destination.Length)
                    return false;

                byte token = (byte)(math.min(literalLength, 15) << 4);
                if (!WriteLengthExtension(literalLength, ref write))
                    return false;

                if (!CopySource(anchor, literalLength, ref write))
                    return false;

                int offset = read - previous;
                if (write + 2 > Destination.Length)
                    return false;

                Destination[write++] = unchecked((byte)offset);
                Destination[write++] = unchecked((byte)(offset >> 8));
                int matchCode = matchLength - 4;
                token |= (byte)math.min(matchCode, 15);
                Destination[tokenOffset] = token;
                return WriteLengthExtension(matchCode, ref write);
            }

            private bool WriteLastLiterals(int sourceOffset, int length, ref int write)
            {
                int tokenOffset = write++;
                if (tokenOffset >= Destination.Length)
                    return false;

                Destination[tokenOffset] = (byte)(math.min(length, 15) << 4);
                return WriteLengthExtension(length, ref write) && CopySource(sourceOffset, length, ref write);
            }

            private bool WriteLengthExtension(int length, ref int write)
            {
                int remaining = length - 15;
                while (remaining >= 255)
                {
                    if (write >= Destination.Length)
                        return false;

                    Destination[write++] = 255;
                    remaining -= 255;
                }

                if (remaining >= 0)
                {
                    if (write >= Destination.Length)
                        return false;

                    Destination[write++] = (byte)remaining;
                }

                return true;
            }

            private bool CopySource(int sourceOffset, int length, ref int write)
            {
                if (length < 0 || sourceOffset < 0 || sourceOffset > Source.Length - length || write > Destination.Length - length)
                    return false;

                for (int i = 0; i < length; i++)
                    Destination[write + i] = Source[sourceOffset + i];

                write += length;
                return true;
            }

            private void WriteSectorStatsCompression(int storedBytes, int flags)
            {
                if (!SectorStats.IsCreated || SectorStats.Length <= 0)
                    return;

                EntityDeltaSectorStatsDTO stats = SectorStats[0];
                uint compressed = (uint)math.max(0, storedBytes);
                stats.CompressedBytes = compressed;
                stats.CompressionRatio01 = stats.FullSnapshotBytes > 0u ? math.saturate((float)compressed / stats.FullSnapshotBytes) : 0f;
                stats.Flags = unchecked((uint)flags);
                SectorStats[0] = stats;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaChecksumHeaderJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CompressedBytes;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;

            public void Execute()
            {
                if (!CompressedBytes.IsCreated || !Counters.IsCreated || !Headers.IsCreated || Headers.Length <= 0 || Counters.Length < CounterCapacity)
                    return;

                EntityDeltaHeaderDTO header = Headers[0];
                int count = math.clamp(Counters[CounterCompressedBytes], 0, CompressedBytes.Length);
                if (count <= 0)
                {
                    header.XXHash3Checksum = 0UL;
                    Headers[0] = header;
                    return;
                }

                byte* ptr = (byte*)CompressedBytes.GetUnsafeReadOnlyPtr();
                SaveStateMerkleTree.Hash128(ptr, count, SectorHash ^ ChecksumSeed, out ulong lo, out ulong hi);
                header.XXHash3Checksum = lo ^ ((hi << 32) | (hi >> 32));
                Headers[0] = header;
                Counters[CounterCompressionFlags] |= (int)HeaderFlagChecksumValid;
                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    EntityDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    SectorStats[0] = stats;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityWalPayloadPackJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [ReadOnly, NoAlias] public NativeArray<byte> CompressedBytes;
            [NoAlias] public NativeArray<byte> WalPayloadBytes;
            [NoAlias] public NativeArray<int> Counters;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                if (!Headers.IsCreated ||
                    !CompressedBytes.IsCreated ||
                    !WalPayloadBytes.IsCreated ||
                    Headers.Length <= 0)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    Counters[CounterWalPayloadBytes] = 0;
                    return;
                }

                int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                int compressedBytes = math.clamp(Counters[CounterCompressedBytes], 0, CompressedBytes.Length);
                int required = headerBytes + compressedBytes;
                if (Counters[CounterFailure] != 0 || required > WalPayloadBytes.Length)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterWalPayloadBytes] = 0;
                    return;
                }

                byte* destination = (byte*)WalPayloadBytes.GetUnsafePtr();
                EntityDeltaHeaderDTO header = Headers[0];
                WriteHeaderLittleEndian(destination, in header);
                if (compressedBytes > 0)
                {
                    UnsafeUtility.MemCpy(
                        destination + headerBytes,
                        CompressedBytes.GetUnsafeReadOnlyPtr(),
                        compressedBytes);
                }

                Counters[CounterWalPayloadBytes] = required;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityWalPayloadEnvelopeAuditJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> WalPayloadBytes;
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<EntityDeltaSectorStatsDTO> SectorStats;

            public void Execute()
            {
                if (!WalPayloadBytes.IsCreated ||
                    !Headers.IsCreated ||
                    !Counters.IsCreated ||
                    Headers.Length <= 0 ||
                    Counters.Length < CounterCapacity)
                {
                    return;
                }

                Counters[CounterWalEnvelopeAuditPass] = 0;
                if (Counters[CounterFailure] != 0)
                    return;

                int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                int byteCount = Counters[CounterWalPayloadBytes];
                if (byteCount < headerBytes || byteCount > WalPayloadBytes.Length)
                {
                    Fail();
                    return;
                }

                byte* ptr = (byte*)WalPayloadBytes.GetUnsafeReadOnlyPtr();
                EntityDeltaHeaderDTO expected = Headers[0];
                EntityDeltaHeaderDTO actual = ReadHeaderLittleEndian(ptr);
                if (!HeaderEquals(in expected, in actual))
                {
                    Fail();
                    return;
                }

                if (byteCount == headerBytes)
                {
                    if (actual.CompressedSize == 0u &&
                        actual.UncompressedSize == 0u &&
                        actual.XXHash3Checksum == 0UL)
                    {
                        Counters[CounterWalEnvelopeAuditPass] = 1;
                        return;
                    }

                    Fail();
                    return;
                }

                int compressedBytes = actual.CompressedSize > int.MaxValue ? -1 : (int)actual.CompressedSize;
                int uncompressedBytes = actual.UncompressedSize > int.MaxValue ? -1 : (int)actual.UncompressedSize;
                int streamHeaderBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
                if (compressedBytes < 0 ||
                    uncompressedBytes < streamHeaderBytes ||
                    compressedBytes > uncompressedBytes ||
                    compressedBytes != byteCount - headerBytes)
                {
                    Fail();
                    return;
                }

                byte* payload = ptr + headerBytes;
                if (!VerifyCompressedPayloadChecksum(payload, compressedBytes, in actual))
                {
                    Fail();
                    return;
                }

                if (compressedBytes == uncompressedBytes &&
                    !TryValidateRleStreamPayload(payload, compressedBytes, out _, out _))
                {
                    Fail();
                    return;
                }

                Counters[CounterWalEnvelopeAuditPass] = 1;
            }

            private void Fail()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterWalPayloadBytes] = 0;
                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    EntityDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    SectorStats[0] = stats;
                }
            }

            private static bool HeaderEquals(in EntityDeltaHeaderDTO expected, in EntityDeltaHeaderDTO actual)
            {
                return expected.SectorHash == actual.SectorHash &&
                       expected.CompressedSize == actual.CompressedSize &&
                       expected.UncompressedSize == actual.UncompressedSize &&
                       expected.XXHash3Checksum == actual.XXHash3Checksum &&
                       expected._pad0 == actual._pad0 &&
                       expected._pad1 == actual._pad1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityWalPayloadDecodeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> WalPayloadBytes;
            [NoAlias] public NativeArray<byte> RleBytes;
            [NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<int> Counters;
            public int ByteCount;

            public void Execute()
            {
                if (!WalPayloadBytes.IsCreated ||
                    !RleBytes.IsCreated ||
                    !Headers.IsCreated ||
                    !Counters.IsCreated ||
                    Headers.Length <= 0 ||
                    Counters.Length < CounterCapacity)
                {
                    return;
                }

                ResetDecodeCounters();
                int headerBytes = UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>();
                if (ByteCount < headerBytes || ByteCount > WalPayloadBytes.Length)
                {
                    Fail();
                    return;
                }

                byte* source = (byte*)WalPayloadBytes.GetUnsafeReadOnlyPtr();
                EntityDeltaHeaderDTO header = ReadHeaderLittleEndian(source);
                Headers[0] = header;

                if (ByteCount == headerBytes)
                {
                    if (header.CompressedSize == 0u &&
                        header.UncompressedSize == 0u &&
                        header.XXHash3Checksum == 0UL)
                    {
                        Counters[CounterWalPayloadBytes] = headerBytes;
                        Counters[CounterDecodePass] = 1;
                        return;
                    }

                    Fail();
                    return;
                }

                int compressedBytes = header.CompressedSize > int.MaxValue ? -1 : (int)header.CompressedSize;
                int uncompressedBytes = header.UncompressedSize > int.MaxValue ? -1 : (int)header.UncompressedSize;
                int streamHeaderBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
                if (compressedBytes < 0 ||
                    uncompressedBytes < streamHeaderBytes ||
                    compressedBytes > uncompressedBytes ||
                    compressedBytes != ByteCount - headerBytes ||
                    uncompressedBytes > RleBytes.Length)
                {
                    Fail();
                    return;
                }

                byte* payload = source + headerBytes;
                if (!VerifyCompressedPayloadChecksum(payload, compressedBytes, in header))
                {
                    Fail();
                    return;
                }

                byte* destination = (byte*)RleBytes.GetUnsafePtr();
                if (compressedBytes == uncompressedBytes)
                {
                    UnsafeUtility.MemCpy(destination, payload, compressedBytes);
                    Counters[CounterCompressionFlags] = (int)(HeaderFlagRaw | HeaderFlagChecksumValid);
                }
                else
                {
                    if (!TryDecodeLz4Block(payload, compressedBytes, destination, RleBytes.Length, uncompressedBytes))
                    {
                        Fail();
                        return;
                    }

                    Counters[CounterCompressionFlags] = (int)(HeaderFlagLz4 | HeaderFlagChecksumValid);
                }

                Counters[CounterWalPayloadBytes] = ByteCount;
                Counters[CounterCompressedBytes] = compressedBytes;
                Counters[CounterRleBytes] = uncompressedBytes;
            }

            private void ResetDecodeCounters()
            {
                Counters[CounterFailure] = 0;
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterRleBytes] = 0;
                Counters[CounterDenseBytes] = 0;
                Counters[CounterCompressionFlags] = 0;
                Counters[CounterDeltaRecordCount] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
                Counters[CounterAuditSamples] = 0;
                Counters[CounterAuditSmallerPayloads] = 0;
                Counters[CounterAuditPass] = 0;
                Counters[CounterAuditCompressedRatioPpm] = 0;
                Counters[CounterAuditSavingsPpm] = 0;
                Counters[CounterAuditByteSavingsPass] = 0;
            }

            private void Fail()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterWalPayloadBytes] = 0;
                Counters[CounterCompressedBytes] = 0;
                Counters[CounterRleBytes] = 0;
                Counters[CounterDenseBytes] = 0;
                Counters[CounterDeltaRecordCount] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
            }

            private static bool TryDecodeLz4Block(byte* source, int sourceLength, byte* destination, int destinationCapacity, int expectedBytes)
            {
                if (source == null || destination == null || sourceLength <= 0 || expectedBytes < 0 || expectedBytes > destinationCapacity)
                    return false;

                int read = 0;
                int write = 0;
                while (read < sourceLength)
                {
                    byte token = source[read++];
                    int literalLength;
                    if (!TryReadLz4Length(source, sourceLength, ref read, token >> 4, out literalLength))
                        return false;

                    if (literalLength < 0 || read > sourceLength - literalLength || write > destinationCapacity - literalLength)
                        return false;

                    UnsafeUtility.MemCpy(destination + write, source + read, literalLength);
                    read += literalLength;
                    write += literalLength;
                    if (read == sourceLength)
                        return write == expectedBytes;

                    if (read > sourceLength - 2)
                        return false;

                    int matchOffset = source[read] | (source[read + 1] << 8);
                    read += 2;
                    if (matchOffset <= 0 || matchOffset > write)
                        return false;

                    int matchLength;
                    if (!TryReadLz4Length(source, sourceLength, ref read, token & 15, out matchLength))
                        return false;

                    matchLength += 4;
                    if (matchLength < 4 || write > destinationCapacity - matchLength)
                        return false;

                    int matchSource = write - matchOffset;
                    for (int i = 0; i < matchLength; i++)
                        destination[write + i] = destination[matchSource + i];

                    write += matchLength;
                }

                return write == expectedBytes;
            }

            private static bool TryReadLz4Length(byte* source, int sourceLength, ref int read, int tokenLength, out int length)
            {
                length = tokenLength;
                if (tokenLength != 15)
                    return true;

                byte extension;
                do
                {
                    if (read >= sourceLength)
                        return false;

                    extension = source[read++];
                    if (length > int.MaxValue - extension)
                        return false;

                    length += extension;
                }
                while (extension == 255);

                return true;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityRleStreamExpandToRecordsJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> RleBytes;
            [NoAlias] public NativeArray<byte> DenseBytes;
            [NoAlias] public NativeArray<EntityDeltaDataRecordDTO> DeltaRecords;
            [NoAlias] public NativeArray<int> Counters;

            public void Execute()
            {
                if (!RleBytes.IsCreated ||
                    !DenseBytes.IsCreated ||
                    !DeltaRecords.IsCreated ||
                    !Counters.IsCreated ||
                    Counters.Length < CounterCapacity)
                {
                    return;
                }

                Counters[CounterDecodePass] = 0;
                Counters[CounterDenseBytes] = 0;
                Counters[CounterDeltaRecordCount] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                if (Counters[CounterFailure] != 0)
                    return;

                int rleLength = Counters[CounterRleBytes];
                if (rleLength < 0 || rleLength > RleBytes.Length)
                {
                    Fail();
                    return;
                }

                if (rleLength == 0)
                {
                    Counters[CounterDenseBytes] = 0;
                    Counters[CounterDeltaRecordCount] = 0;
                    Counters[CounterDecodeDenseBytes] = 0;
                    Counters[CounterDecodeRecordCount] = 0;
                    Counters[CounterDecodePass] = 1;
                    return;
                }

                byte* source = (byte*)RleBytes.GetUnsafeReadOnlyPtr();
                if (!TryValidateRleStreamPayload(source, rleLength, out uint denseByteCount, out uint streamFlags) ||
                    denseByteCount > DenseBytes.Length)
                {
                    Fail();
                    return;
                }

                int streamHeaderBytes = UnsafeUtility.SizeOf<EntityDeltaRleStreamHeaderDTO>();
                EntityDeltaRleStreamHeaderDTO streamHeader = ReadRleStreamHeaderLittleEndian(source);
                byte* payload = source + streamHeaderBytes;
                byte* dense = (byte*)DenseBytes.GetUnsafePtr();
                if ((streamFlags & RleStreamFlagRawDense) != 0u)
                {
                    UnsafeUtility.MemCpy(dense, payload, (int)denseByteCount);
                }
                else if (!TryExpandRlePairs(payload, (int)streamHeader.StoredBytes, dense, (int)denseByteCount))
                {
                    Fail();
                    return;
                }

                int recordBytes = UnsafeUtility.SizeOf<EntityDeltaDataRecordDTO>();
                if (denseByteCount % (uint)recordBytes != 0u)
                {
                    Fail();
                    return;
                }

                int recordCount = (int)(denseByteCount / (uint)recordBytes);
                if (recordCount > DeltaRecords.Length)
                {
                    Fail();
                    return;
                }

                for (int i = 0; i < recordCount; i++)
                {
                    int sourceOffset = i * recordBytes;
                    EntityDeltaDataRecordDTO record = (streamFlags & RleStreamFlagLittleEndianRecords) != 0u
                        ? ReadRecordLittleEndian(dense, sourceOffset)
                        : ReadRecordBigEndian(dense, sourceOffset);
                    if (!HasFiniteLocalOffset(in record))
                    {
                        Fail();
                        return;
                    }

                    ElementAsRef(DeltaRecords, i) = record;
                }

                Counters[CounterDenseBytes] = (int)denseByteCount;
                Counters[CounterDecodeDenseBytes] = (int)denseByteCount;
                Counters[CounterDeltaRecordCount] = recordCount;
                Counters[CounterDecodeRecordCount] = recordCount;
                Counters[CounterDecodePass] = 1;
            }

            private void Fail()
            {
                Counters[CounterFailure] = 1;
                Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                Counters[CounterDenseBytes] = 0;
                Counters[CounterDeltaRecordCount] = 0;
                Counters[CounterDecodeDenseBytes] = 0;
                Counters[CounterDecodeRecordCount] = 0;
                Counters[CounterDecodePass] = 0;
            }

            private static bool TryExpandRlePairs(byte* source, int sourceBytes, byte* destination, int expectedBytes)
            {
                if (source == null || destination == null || sourceBytes < 0 || (sourceBytes & 1) != 0 || expectedBytes < 0)
                    return false;

                int write = 0;
                for (int read = 0; read < sourceBytes; read += 2)
                {
                    int run = source[read];
                    byte value = source[read + 1];
                    if (run <= 0 || write > expectedBytes - run)
                        return false;

                    for (int i = 0; i < run; i++)
                        destination[write + i] = value;

                    write += run;
                }

                return write == expectedBytes;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaTelemetryRecordJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<int> Counters;
            [ReadOnly, NoAlias] public NativeArray<EntityDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> TelemetryCursor;
            public uint Frame;
            public float BurstTimeMs;
            public float DiskWriteLatencyMs;
            public float GlobalQualityWeight;
            public float CompressionEffort01;
            public float IoPressure01;

            public void Execute()
            {
                if (!Counters.IsCreated || !Headers.IsCreated || !TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                    return;

                int cursor = math.max(0, TelemetryCursor[0]);
                int index = cursor % math.min(TelemetryRingFrames, TelemetryRing.Length);
                EntityDeltaHeaderDTO header = Headers.Length > 0 ? Headers[0] : default;
                TelemetryRing[index] = new EntityCompressionTelemetryEntry
                {
                    SectorHash = header.SectorHash,
                    PayloadHash = header.XXHash3Checksum,
                    Frame = Frame,
                    FullSnapshotBytes = Counters.Length > CounterFullSnapshotBytes ? (uint)math.max(0, Counters[CounterFullSnapshotBytes]) : 0u,
                    DenseDeltaBytes = Counters.Length > CounterDenseBytes ? (uint)math.max(0, Counters[CounterDenseBytes]) : 0u,
                    RleBytes = Counters.Length > CounterRleBytes ? (uint)math.max(0, Counters[CounterRleBytes]) : 0u,
                    CompressedBytes = Counters.Length > CounterCompressedBytes ? (uint)math.max(0, Counters[CounterCompressedBytes]) : 0u,
                    Flags = Counters.Length > CounterCompressionFlags ? unchecked((uint)Counters[CounterCompressionFlags]) : 0u,
                    BurstTimeMs = SanitizeMs(BurstTimeMs),
                    DiskWriteLatencyMs = SanitizeMs(DiskWriteLatencyMs),
                    GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f),
                    CompressionEffort01 = math.saturate(math.isfinite(CompressionEffort01) ? CompressionEffort01 : 0f),
                    IoPressureMicro = (uint)math.round(math.saturate(math.isfinite(IoPressure01) ? IoPressure01 : 0f) * 1000000f),
                    DeltaEntityCount = Counters.Length > CounterDeltaRecordCount ? (uint)math.max(0, Counters[CounterDeltaRecordCount]) : 0u
                };
                TelemetryCursor[0] = (cursor + 1) % TelemetryRingFrames;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeMs(float value)
            {
                return math.max(0f, math.isfinite(value) ? value : 0f);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaDiskLatencyTelemetryPatchJob : IJob
        {
            [NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;
            [ReadOnly, NoAlias] public NativeArray<int> TelemetryCursor;
            public ulong SectorHash;
            public uint Frame;
            public float DiskWriteLatencyMs;
            public float SpikeThresholdMs;
            public byte MatchFrame;

            public void Execute()
            {
                if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                    return;

                int length = math.min(TelemetryRingFrames, TelemetryRing.Length);
                int cursor = math.max(0, TelemetryCursor[0]);
                float latency = math.max(0f, math.isfinite(DiskWriteLatencyMs) ? DiskWriteLatencyMs : 0f);
                float threshold = math.max(0f, math.isfinite(SpikeThresholdMs) ? SpikeThresholdMs : 50f);
                uint patchFlags = TelemetryFlagDiskLatencyPatched | (latency >= threshold ? TelemetryFlagDiskLatencySpike : 0u);
                for (int step = 0; step < length; step++)
                {
                    int index = (cursor - 1 - step + length) % length;
                    EntityCompressionTelemetryEntry entry = TelemetryRing[index];
                    if (entry.SectorHash != SectorHash || (MatchFrame != 0 && entry.Frame != Frame))
                        continue;

                    entry.DiskWriteLatencyMs = latency;
                    entry.Flags |= patchFlags;
                    TelemetryRing[index] = entry;
                    return;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityDeltaCompressionRatioAuditJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> Counters;
            public int MinimumSamples;
            public float RequiredPassRatio01;
            public float RequiredByteSavings01;

            public void Execute()
            {
                if (!TelemetryRing.IsCreated || !Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                int samples = 0;
                int smallerPayloads = 0;
                ulong fullBytes = 0ul;
                ulong compressedBytes = 0ul;
                int length = math.min(TelemetryRingFrames, TelemetryRing.Length);
                for (int i = 0; i < length; i++)
                {
                    EntityCompressionTelemetryEntry entry = TelemetryRing[i];
                    if (entry.FullSnapshotBytes == 0u || (entry.Flags & HeaderFlagFatal) != 0u)
                        continue;

                    samples++;
                    fullBytes += entry.FullSnapshotBytes;
                    compressedBytes += entry.CompressedBytes;
                    if (entry.CompressedBytes < entry.FullSnapshotBytes)
                        smallerPayloads++;
                }

                int requiredSamples = math.max(1, MinimumSamples);
                float required = math.saturate(math.isfinite(RequiredPassRatio01) ? RequiredPassRatio01 : 0.99f);
                float observed = samples > 0 ? (float)smallerPayloads / samples : 0f;
                int compressedRatioPpm = CalculateCompressedRatioPpm(fullBytes, compressedBytes);
                int savingsPpm = (int)AuditPpmScale - compressedRatioPpm;
                int requiredSavingsPpm = RequiredSavingsPpm(RequiredByteSavings01);
                int byteSavingsPass = samples >= requiredSamples && savingsPpm >= requiredSavingsPpm ? 1 : 0;
                Counters[CounterAuditSamples] = samples;
                Counters[CounterAuditSmallerPayloads] = smallerPayloads;
                Counters[CounterAuditCompressedRatioPpm] = compressedRatioPpm;
                Counters[CounterAuditSavingsPpm] = savingsPpm;
                Counters[CounterAuditByteSavingsPass] = byteSavingsPass;
                Counters[CounterAuditPass] = samples >= requiredSamples && observed >= required && byteSavingsPass == 1 ? 1 : 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EntityCompressionProfileCsvParseJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CsvBytes;
            [NoAlias] public NativeArray<EntityDeltaCompressionTuningDTO> Tuning;
            [NoAlias] public NativeArray<EntityCompressionProfileDTO> Profiles;
            [NoAlias] public NativeArray<int> Counters;
            public int ByteCount;

            public void Execute()
            {
                if (!CsvBytes.IsCreated || !Tuning.IsCreated || Tuning.Length <= 0 || !Profiles.IsCreated || Profiles.Length <= 0)
                    return;

                EntityDeltaCompressionTuningDTO tuning = Tuning[0].SchemaHash == 0u ? BuildDefaultTuning() : Tuning[0];
                int limit = math.clamp(ByteCount <= 0 ? CsvBytes.Length : ByteCount, 0, CsvBytes.Length);
                int lineStart = 0;
                int parsed = 0;
                int failure = 0;
                byte* data = (byte*)CsvBytes.GetUnsafeReadOnlyPtr();
                for (int i = 0; i <= limit; i++)
                {
                    if (i < limit && data[i] != (byte)'\n' && data[i] != (byte)'\r')
                        continue;

                    if (TryParseLine(data, lineStart, i, ref tuning, Profiles))
                        parsed++;
                    else if (i > lineStart)
                        failure++;

                    while (i + 1 < limit && (data[i + 1] == (byte)'\n' || data[i + 1] == (byte)'\r'))
                        i++;

                    lineStart = i + 1;
                }

                Tuning[0] = tuning;
                if (Counters.IsCreated && Counters.Length > CounterCsvFailure)
                {
                    Counters[CounterParsedProfiles] = parsed;
                    Counters[CounterCsvFailure] = failure;
                }
            }

            private static bool TryParseLine(byte* data, int start, int end, ref EntityDeltaCompressionTuningDTO tuning, NativeArray<EntityCompressionProfileDTO> profiles)
            {
                int firstStart = SkipWhitespace(data, start, end);
                if (firstStart == 0 &&
                    end - firstStart >= 3 &&
                    data[firstStart] == 0xEF &&
                    data[firstStart + 1] == 0xBB &&
                    data[firstStart + 2] == 0xBF)
                {
                    firstStart += 3;
                }

                if (firstStart >= end || data[firstStart] == (byte)'#')
                    return true;

                int field0Start = firstStart;
                int field0End = FindSeparator(data, field0Start, end);
                if (field0End >= end)
                    return false;

                int field1Start = SkipWhitespace(data, field0End + 1, end);
                int field1End = FindSeparator(data, field1Start, end);
                int field2Start = field1End < end ? SkipWhitespace(data, field1End + 1, end) : end;
                int field2End = field2Start < end ? FindSeparator(data, field2Start, end) : end;
                int field3Start = field2End < end ? SkipWhitespace(data, field2End + 1, end) : end;
                int field3End = field3Start < end ? FindSeparator(data, field3Start, end) : end;

                field0End = TrimEndWhitespace(data, field0Start, field0End);
                field1End = TrimValueEnd(data, field1Start, field1End);
                field2End = TrimValueEnd(data, field2Start, field2End);
                field3End = TrimValueEnd(data, field3Start, field3End);
                uint keyHash = HashAsciiLower(data + field0Start, field0End - field0Start);

                if (TryApplyTuning(keyHash, data, field1Start, field1End, ref tuning))
                    return true;

                int nameStart = field0Start;
                int nameEnd = field0End;
                int fidelityStart = field1Start;
                int fidelityEnd = field1End;
                int flagsStart = field2Start;
                int flagsEnd = field2End;
                if (keyHash == KeyProfile)
                {
                    nameStart = field1Start;
                    nameEnd = field1End;
                    fidelityStart = field2Start;
                    fidelityEnd = field2End;
                    flagsStart = field3Start;
                    flagsEnd = field3End;
                }

                if (nameStart >= nameEnd || fidelityStart >= fidelityEnd)
                    return false;

                uint entityHash = TryParseUInt(data, nameStart, nameEnd, out uint parsedHash)
                    ? parsedHash
                    : HashAsciiLower(data + nameStart, nameEnd - nameStart);
                if (entityHash == 0u)
                    entityHash = 1u;

                if (!TryParseFloat(data, fidelityStart, fidelityEnd, out float fidelity))
                    return false;

                uint flags = 0u;
                if (flagsStart < flagsEnd)
                    TryParseUInt(data, flagsStart, flagsEnd, out flags);

                EntityCompressionProfileDTO profile = new EntityCompressionProfileDTO
                {
                    ProfileHash = HashAsciiLower64(data + nameStart, nameEnd - nameStart),
                    EntityKindHash = entityHash,
                    Fidelity01 = math.saturate(fidelity),
                    Flags = flags,
                    HealthDeltaMilli = (ushort)math.round(math.lerp(250f, 25f, math.saturate(fidelity))),
                    InventoryDeltaMask = 0xFFFF,
                    StateMask = 0xFFFFFFFFu,
                    _pad0 = 0u
                };
                return TryStoreProfile(profiles, in profile);
            }

            private static bool TryApplyTuning(uint keyHash, byte* data, int valueStart, int valueEnd, ref EntityDeltaCompressionTuningDTO tuning)
            {
                if (!TryParseFloat(data, valueStart, valueEnd, out float value))
                    return false;

                switch (keyHash)
                {
                    case KeyTombstoneDays:
                        tuning.TombstoneMaxDays = math.clamp(value, 0.25f, 30f);
                        return true;
                    case KeyLz4MinEffort01:
                        tuning.Lz4MinEffort01 = math.saturate(value);
                        return true;
                    case KeyLz4MaxEffort01:
                        tuning.Lz4MaxEffort01 = math.saturate(value);
                        return true;
                    case KeyLowQualityWriteHz:
                        tuning.LowQualityWriteHz = math.max(1f, value);
                        return true;
                    case KeyHighQualityWriteHz:
                        tuning.HighQualityWriteHz = math.max(tuning.LowQualityWriteHz, value);
                        return true;
                    case KeyIoPressureBias01:
                        tuning.IoPressureBias01 = math.saturate(value);
                        return true;
                    case KeyMaxWalWriteMs:
                        tuning.MaxWalWriteMillis = math.max(0.05f, value);
                        return true;
                    case KeyMaxBytesPerFrame:
                        tuning.MaxBytesPerFrame = (uint)math.max(1024, (int)math.round(value));
                        return true;
                    case KeyMockMutationRate:
                        tuning.MockMutationRate01 = math.saturate(value);
                        return true;
                    case KeyRleMinSaving:
                        tuning.RleMinSaving01 = math.saturate(value);
                        return true;
                    default:
                        return false;
                }
            }

            private static bool TryStoreProfile(NativeArray<EntityCompressionProfileDTO> profiles, in EntityCompressionProfileDTO profile)
            {
                int length = profiles.Length;
                if (length <= 0)
                    return false;

                int start = (int)(profile.EntityKindHash % (uint)length);
                for (int probe = 0; probe < length; probe++)
                {
                    int index = start + probe;
                    if (index >= length)
                        index -= length;

                    EntityCompressionProfileDTO existing = profiles[index];
                    if (existing.EntityKindHash != 0u && existing.EntityKindHash != profile.EntityKindHash)
                        continue;

                    profiles[index] = profile;
                    return true;
                }

                return false;
            }

            private static int FindSeparator(byte* data, int start, int end)
            {
                int i = start;
                while (i < end && data[i] != (byte)',' && data[i] != (byte)'=' && data[i] != (byte)'\n' && data[i] != (byte)'\r')
                    i++;
                return i;
            }

            private static int SkipWhitespace(byte* data, int start, int end)
            {
                int i = start;
                while (i < end && (data[i] == (byte)' ' || data[i] == (byte)'\t'))
                    i++;
                return i;
            }

            private static int TrimEndWhitespace(byte* data, int start, int end)
            {
                int i = end;
                while (i > start && (data[i - 1] == (byte)' ' || data[i - 1] == (byte)'\t'))
                    i--;
                return i;
            }

            private static int TrimValueEnd(byte* data, int start, int end)
            {
                int valueEnd = end;
                for (int i = start; i < end; i++)
                {
                    if (data[i] == (byte)'#')
                    {
                        valueEnd = i;
                        break;
                    }
                }
                return TrimEndWhitespace(data, start, valueEnd);
            }

            private static bool TryParseUInt(byte* data, int start, int end, out uint value)
            {
                value = 0u;
                start = SkipWhitespace(data, start, end);
                end = TrimValueEnd(data, start, end);
                if (start >= end)
                    return false;

                bool hex = end - start > 2 && data[start] == (byte)'0' && (data[start + 1] == (byte)'x' || data[start + 1] == (byte)'X');
                int i = hex ? start + 2 : start;
                bool digit = false;
                uint result = 0u;
                while (i < end)
                {
                    byte c = data[i++];
                    uint d;
                    if (c >= (byte)'0' && c <= (byte)'9')
                        d = (uint)(c - (byte)'0');
                    else if (hex && c >= (byte)'a' && c <= (byte)'f')
                        d = (uint)(10 + c - (byte)'a');
                    else if (hex && c >= (byte)'A' && c <= (byte)'F')
                        d = (uint)(10 + c - (byte)'A');
                    else
                        return false;

                    digit = true;
                    result = hex ? ((result << 4) | d) : (result * 10u) + d;
                }

                value = result;
                return digit;
            }

            private static bool TryParseFloat(byte* data, int start, int end, out float value)
            {
                value = 0f;
                start = SkipWhitespace(data, start, end);
                end = TrimValueEnd(data, start, end);
                if (start >= end)
                    return false;

                int sign = 1;
                int i = start;
                if (data[i] == (byte)'-' || data[i] == (byte)'+')
                {
                    sign = data[i] == (byte)'-' ? -1 : 1;
                    i++;
                }

                float whole = 0f;
                bool digit = false;
                while (i < end && data[i] >= (byte)'0' && data[i] <= (byte)'9')
                {
                    digit = true;
                    whole = (whole * 10f) + (data[i] - (byte)'0');
                    i++;
                }

                float fraction = 0f;
                float scale = 1f;
                if (i < end && data[i] == (byte)'.')
                {
                    i++;
                    while (i < end && data[i] >= (byte)'0' && data[i] <= (byte)'9')
                    {
                        digit = true;
                        scale *= 10f;
                        fraction += (data[i] - (byte)'0') / scale;
                        i++;
                    }
                }

                if (!digit || i != end)
                    return false;

                value = sign * (whole + fraction);
                return math.isfinite(value);
            }

            private static uint HashAsciiLower(byte* data, int length)
            {
                uint hash = 2166136261u;
                for (int i = 0; i < length; i++)
                {
                    byte c = data[i];
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    hash ^= c;
                    hash *= 16777619u;
                }

                return hash;
            }

            private static ulong HashAsciiLower64(byte* data, int length)
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < length; i++)
                {
                    byte c = data[i];
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    hash ^= c;
                    hash *= 1099511628211UL;
                }

                return hash == 0UL ? 1UL : hash;
            }
        }
    }
}
