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

namespace Hecton8.SaveSystem
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct VoxelDeltaRleRunDTO
    {
        [FieldOffset(0)] public ushort StartIndex;
        [FieldOffset(2)] public ushort RunLength;
        [FieldOffset(4)] public sbyte SdfValue;
        [FieldOffset(5)] public byte MaterialId;
        [FieldOffset(6)] public byte Flags;
        [FieldOffset(7)] public byte Reserved0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct VoxelDeltaHeaderDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ulong XXHash3Checksum;
        [FieldOffset(16)] public uint CompressedSize;
        [FieldOffset(20)] public uint UncompressedSize;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint LayoutMarker;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct VoxelDeltaBlockCounter64
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint RunCount;
        [FieldOffset(12)] public uint ModifiedCellCount;
        [FieldOffset(16)] public uint EncodedBytes;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
        [FieldOffset(32)] public uint _pad2;
        [FieldOffset(36)] public uint _pad3;
        [FieldOffset(40)] public uint _pad4;
        [FieldOffset(44)] public uint _pad5;
        [FieldOffset(48)] public uint _pad6;
        [FieldOffset(52)] public uint _pad7;
        [FieldOffset(56)] public uint _pad8;
        [FieldOffset(60)] public uint _pad9;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelDeltaCompressionTelemetryEntry
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ulong PayloadHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint RawBytes;
        [FieldOffset(24)] public uint CompressedBytes;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float BurstTimeMs;
        [FieldOffset(36)] public float DiskWriteLatencyMs;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float CompressionEffort01;
        [FieldOffset(48)] public uint RleRunCount;
        [FieldOffset(52)] public uint PrunedCellCount;
        [FieldOffset(56)] public uint IoPressureMicro;
        [FieldOffset(60)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelDeltaCompressionTuningDTO
    {
        [FieldOffset(0)] public ulong ProfileHash;
        [FieldOffset(8)] public uint SchemaHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float PruneThreshold01;
        [FieldOffset(20)] public float Lz4MinEffort01;
        [FieldOffset(24)] public float Lz4MaxEffort01;
        [FieldOffset(28)] public float LowQualityWriteHz;
        [FieldOffset(32)] public float HighQualityWriteHz;
        [FieldOffset(36)] public float ChunkUnloadDistanceMeters;
        [FieldOffset(40)] public float IoPressureBias01;
        [FieldOffset(44)] public float MaxWalWriteMillis;
        [FieldOffset(48)] public uint MaxBytesPerFrame;
        [FieldOffset(52)] public float DepthMinMeters;
        [FieldOffset(56)] public float DepthMaxMeters;
        [FieldOffset(60)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelDeltaSectorStatsDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public int SectorX;
        [FieldOffset(12)] public int SectorY;
        [FieldOffset(16)] public int SectorZ;
        [FieldOffset(20)] public uint RawBytes;
        [FieldOffset(24)] public uint CompressedBytes;
        [FieldOffset(28)] public uint ModifiedCells;
        [FieldOffset(32)] public uint RleRuns;
        [FieldOffset(36)] public float ModifiedRatio01;
        [FieldOffset(40)] public float CompressionRatio01;
        [FieldOffset(44)] public float VisualFade01;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct VoxelDeltaDearLieStateDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint StartFrame;
        [FieldOffset(12)] public uint DurationTicks;
        [FieldOffset(16)] public float VisualFade01;
        [FieldOffset(20)] public float TargetStrength01;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint Flags;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct VoxelDeltaMockSchemaDTO
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong SchemaHash;
        [FieldOffset(16)] public ulong Seed;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public uint ChunkResolution;
        [FieldOffset(32)] public uint ChunkCellCount;
        [FieldOffset(36)] public uint HeaderBytes;
        [FieldOffset(40)] public uint RleRunBytes;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint _pad0;
        [FieldOffset(52)] public uint _pad1;
        [FieldOffset(56)] public uint _pad2;
        [FieldOffset(60)] public uint _pad3;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct VoxelDeltaTelemetryDumpHeaderDTO
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong FirstSectorHash;
        [FieldOffset(16)] public ulong LastSectorHash;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public uint EntryCount;
        [FieldOffset(32)] public uint EntryStride;
        [FieldOffset(36)] public uint Cursor;
        [FieldOffset(40)] public uint ReasonFlags;
        [FieldOffset(44)] public uint RingCapacity;
        [FieldOffset(48)] public uint HeaderBytes;
        [FieldOffset(52)] public uint FirstFrame;
        [FieldOffset(56)] public uint LastFrame;
        [FieldOffset(60)] public uint _pad0;
    }

    internal ref struct VoxelDeltaCompressionVaultBufferSet
    {
        public NativeArray<byte> SchemaBytes;
        public NativeArray<sbyte> RuntimeDensity;
        public NativeArray<sbyte> BaselineDensity;
        public NativeArray<byte> MaterialIds;
        public NativeArray<byte> CellFlags;
        public NativeArray<VoxelDeltaRleRunDTO> RleRuns;
        public NativeArray<VoxelDeltaBlockCounter64> BlockCounters;
        public NativeArray<byte> RleBytes;
        public NativeArray<byte> CompressedBytes;
        public NativeArray<int> Lz4HashTable;
        public NativeArray<VoxelDeltaHeaderDTO> Headers;
        public NativeArray<int> Counters;
        public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<VoxelDeltaCompressionTuningDTO> Tuning;
        public NativeArray<VoxelDeltaSectorStatsDTO> SectorStats;
    }

    public static unsafe class VoxelDeltaCompressionArchitecture
    {
        public const int ChunkResolution = 32;
        public const int ChunkCellCount = ChunkResolution * ChunkResolution * ChunkResolution;
        public const int TelemetryRingFrames = 300;
        public const int MaxVoxelDeltaSectorStats = 512;
        public const int DefaultBlockCells = 128;
        public const int DefaultSchemaBytes = 256;
        internal const int MaxVoxelDeltaWalPayloadBytes = (256 * 1024) - 64;
        internal const int VoxelDeltaHeaderBytes = 32;
        internal const int VoxelDeltaRleRunBytes = 8;
        internal const int VoxelDeltaDenseFallbackDirtyMaskWordCount = ChunkCellCount / 32;
        internal const int VoxelDeltaDenseFallbackDirtyMaskBytes = VoxelDeltaDenseFallbackDirtyMaskWordCount * sizeof(uint);
        internal const int VoxelDeltaDenseFallbackCellBytes = sizeof(ushort) + sizeof(byte) + sizeof(byte);
        internal const int VoxelDeltaDenseFallbackPayloadBytes = VoxelDeltaDenseFallbackDirtyMaskBytes + (ChunkCellCount * VoxelDeltaDenseFallbackCellBytes);
        internal const int MaxVoxelDeltaRleRunsPerWalPayload = (MaxVoxelDeltaWalPayloadBytes - VoxelDeltaHeaderBytes) / VoxelDeltaRleRunBytes;
        public const int HashTableSlots = 4096;
        public const int CounterCapacity = 24;
        internal const int CounterRleRunCount = 0;
        internal const int CounterModifiedCellCount = 1;
        internal const int CounterRleBytes = 2;
        internal const int CounterPruned = 3;
        internal const int CounterCompressedBytes = 4;
        internal const int CounterRawBytes = 5;
        internal const int CounterFailure = 6;
        internal const int CounterCompressionFlags = 7;
        internal const int CounterBlockCount = 8;
        internal const int CounterTelemetryCursor = 9;
        internal const int CounterParsedProfiles = 10;
        internal const int CounterCsvFailure = 11;
        internal const int CounterWalPayloadBytes = 12;
        public const float DefaultPruneThreshold01 = 0.0001f;
        internal const ulong MockSchemaMagic = 0x48435653444C5441UL; // ATLDVSCH little-endian marker.
        internal const uint HeaderFlagLz4 = 1u;
        internal const uint HeaderFlagRaw = 1u << 1;
        internal const uint HeaderFlagPruned = 1u << 2;
        internal const uint HeaderFlagChecksumValid = 1u << 3;
        internal const uint HeaderFlagDenseFallback = 1u << 4;
        internal const uint HeaderFlagFatal = 1u << 31;
        internal const uint TelemetryFlagDiskLatencyPatched = 1u << 8;
        internal const uint TelemetryFlagDiskLatencySpike = 1u << 9;
        internal const uint TelemetryDumpMagic = 0x56445741u; // AWDV little-endian marker.
        internal const uint TelemetryDumpVersion = 1u;
        internal const uint HeaderAlignedLayoutMarker = 0x31585256u; // VXR1 little-endian marker.
        private const string VoxelDeltaTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1312_VoxelPaging.bin";
        internal const int Lz4LastLiterals = 5;
        internal const int Lz4MfLimit = 12;

        private const uint KeyPruneThreshold01 = 0xE8F5FE20u;
        private const uint KeyLz4MinEffort01 = 0x60F29CAEu;
        private const uint KeyLz4MaxEffort01 = 0x4996CD4Cu;
        private const uint KeyLowQualityWriteHz = 0x5AF6437Eu;
        private const uint KeyHighQualityWriteHz = 0x7087A0CAu;
        private const uint KeyChunkUnloadDistanceM = 0xAE0922BAu;
        private const uint KeyIoPressureBias01 = 0xE50F8A28u;
        private const uint KeyMaxWalWriteMs = 0xEC81F34Fu;
        private const uint KeyMaxBytesPerFrame = 0xA1229643u;
        private const uint KeyBiome = 0x8BAB7EC3u;
        private const uint KeyDepthMinM = 0xE526065Du;
        private const uint KeyDepthMaxM = 0x87AF477Fu;

        internal static bool TryResolveVaultBuffers(
            IDataVault vault,
            int cellCapacity,
            int rleRunCapacity,
            int stagingCapacityBytes,
            int sectorStatsCapacity,
            out VoxelDeltaCompressionVaultBufferSet buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            int safeCells = ChunkCellCount;
            int safeRuns = math.clamp(
                rleRunCapacity <= 0 ? MaxVoxelDeltaRleRunsPerWalPayload : rleRunCapacity,
                1,
                math.min(ChunkCellCount, MaxVoxelDeltaRleRunsPerWalPayload));
            int safeBytes = Align16(math.clamp(
                stagingCapacityBytes <= 0 ? MaxVoxelDeltaWalPayloadBytes : stagingCapacityBytes,
                1024,
                MaxVoxelDeltaWalPayloadBytes));
            int safeStats = math.clamp(sectorStatsCapacity <= 0 ? 1 : sectorStatsCapacity, 1, MaxVoxelDeltaSectorStats);
            int blockCount = ResolveBlockCount(safeCells, DefaultBlockCells);

            buffers.SchemaBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaSchemaBytes, DefaultSchemaBytes, NativeArrayOptions.UninitializedMemory);
            buffers.RuntimeDensity = ResolveVaultBuffer<sbyte>(vault, BufferID.SaveVoxelDeltaRuntimeDensity, safeCells, NativeArrayOptions.UninitializedMemory);
            buffers.BaselineDensity = ResolveVaultBuffer<sbyte>(vault, BufferID.SaveVoxelDeltaBaselineDensity, safeCells, NativeArrayOptions.UninitializedMemory);
            buffers.MaterialIds = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaMaterialIds, safeCells, NativeArrayOptions.UninitializedMemory);
            buffers.CellFlags = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaCellFlags, safeCells, NativeArrayOptions.UninitializedMemory);
            buffers.RleRuns = ResolveVaultBuffer<VoxelDeltaRleRunDTO>(vault, BufferID.SaveVoxelDeltaRleRuns, safeRuns, NativeArrayOptions.UninitializedMemory);
            buffers.BlockCounters = ResolveVaultBuffer<VoxelDeltaBlockCounter64>(vault, BufferID.SaveVoxelDeltaBlockCounters, blockCount, NativeArrayOptions.ClearMemory);
            buffers.RleBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaRleBytes, safeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.CompressedBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaCompressedBytes, safeBytes, NativeArrayOptions.UninitializedMemory);
            buffers.Lz4HashTable = ResolveVaultBuffer<int>(vault, BufferID.SaveVoxelDeltaLz4HashTable, HashTableSlots, NativeArrayOptions.UninitializedMemory);
            buffers.Headers = ResolveVaultBuffer<VoxelDeltaHeaderDTO>(vault, BufferID.SaveVoxelDeltaHeaders, safeStats, NativeArrayOptions.UninitializedMemory);
            buffers.Counters = ResolveVaultBuffer<int>(vault, BufferID.SaveVoxelDeltaCounters, CounterCapacity, NativeArrayOptions.ClearMemory);
            buffers.TelemetryRing = ResolveVaultBuffer<VoxelDeltaCompressionTelemetryEntry>(vault, BufferID.SaveVoxelDeltaTelemetryRing, TelemetryRingFrames, NativeArrayOptions.ClearMemory);
            buffers.TelemetryCursor = ResolveVaultBuffer<int>(vault, BufferID.SaveVoxelDeltaTelemetryCursor, 1, NativeArrayOptions.ClearMemory);
            buffers.Tuning = ResolveVaultBuffer<VoxelDeltaCompressionTuningDTO>(vault, BufferID.SaveVoxelDeltaTuning, 1, NativeArrayOptions.ClearMemory);
            buffers.SectorStats = ResolveVaultBuffer<VoxelDeltaSectorStatsDTO>(vault, BufferID.SaveVoxelDeltaSectorStats, safeStats, NativeArrayOptions.ClearMemory);

            return buffers.SchemaBytes.IsCreated &&
                   buffers.RuntimeDensity.IsCreated &&
                   buffers.BaselineDensity.IsCreated &&
                   buffers.MaterialIds.IsCreated &&
                   buffers.CellFlags.IsCreated &&
                   buffers.RleRuns.IsCreated &&
                   buffers.BlockCounters.IsCreated &&
                   buffers.RleBytes.IsCreated &&
                   buffers.CompressedBytes.IsCreated &&
                   buffers.Lz4HashTable.IsCreated &&
                   buffers.Headers.IsCreated &&
                   buffers.Counters.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.Tuning.IsCreated &&
                   buffers.SectorStats.IsCreated;
        }

        public static void GenerateEmergencyMockVoxelSchema(NativeArray<byte> destination, uint seed)
        {
            if (!destination.IsCreated || destination.Length <= 0)
                return;

            uint state = seed != 0u ? seed : 0xA511E9B3u;
            for (int i = 0; i < destination.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                destination[i] = unchecked((byte)(state >> ((i & 3) << 3)));
            }

            if (destination.Length < UnsafeUtility.SizeOf<VoxelDeltaMockSchemaDTO>())
                return;

            VoxelDeltaMockSchemaDTO schema = new VoxelDeltaMockSchemaDTO
            {
                Magic = MockSchemaMagic,
                SchemaHash = ((ulong)seed << 32) | 0x56584431u,
                Version = 1u,
                ChunkResolution = ChunkResolution,
                ChunkCellCount = ChunkCellCount,
                HeaderBytes = (uint)UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>(),
                RleRunBytes = (uint)UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>(),
                Flags = 0u,
                Seed = seed,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u,
                _pad3 = 0u
            };

            void* destinationPtr = destination.GetUnsafePtr();
            UnsafeUtility.MemCpy(destinationPtr, &schema, UnsafeUtility.SizeOf<VoxelDeltaMockSchemaDTO>());
        }

        public static bool TryGenerateEmergencyMockVoxelSchema(IDataVault vault, uint seed)
        {
            if (!TryResolveEmergencyMockVoxelSchemaBuffer(vault, out NativeArray<byte> schemaBytes))
                return false;

            GenerateEmergencyMockVoxelSchema(schemaBytes, seed);
            return true;
        }

        private static bool TryResolveEmergencyMockVoxelSchemaBuffer(IDataVault vault, out NativeArray<byte> schemaBytes)
        {
            schemaBytes = default;
            if (vault == null)
                return false;

            schemaBytes = ResolveVaultBuffer<byte>(vault, BufferID.SaveVoxelDeltaSchemaBytes, DefaultSchemaBytes, NativeArrayOptions.UninitializedMemory);
            if (!schemaBytes.IsCreated)
                return false;

            return true;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.SavePersistence,
                options);
            return vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        public static VoxelDeltaCompressionTuningDTO BuildDefaultTuning()
        {
            return new VoxelDeltaCompressionTuningDTO
            {
                ProfileHash = 0x5348494E4F425531UL,
                SchemaHash = 0x56584431u,
                Flags = 0u,
                PruneThreshold01 = DefaultPruneThreshold01,
                Lz4MinEffort01 = 0.12f,
                Lz4MaxEffort01 = 0.92f,
                LowQualityWriteHz = 5f,
                HighQualityWriteHz = 30f,
                ChunkUnloadDistanceMeters = 1800f,
                IoPressureBias01 = 0.35f,
                MaxWalWriteMillis = 0.35f,
                MaxBytesPerFrame = 64u * 1024u,
                DepthMinMeters = 0f,
                DepthMaxMeters = 1200f,
                _pad0 = 0u
            };
        }

        internal static VoxelDeltaCompressionTuningDTO ResolveRuntimeTuning(NativeArray<VoxelDeltaCompressionTuningDTO> tuningBuffer)
        {
            VoxelDeltaCompressionTuningDTO tuning = tuningBuffer.IsCreated && tuningBuffer.Length > 0 && tuningBuffer[0].SchemaHash != 0u
                ? tuningBuffer[0]
                : BuildDefaultTuning();

            tuning.PruneThreshold01 = math.clamp(SanitizeFinite(tuning.PruneThreshold01, DefaultPruneThreshold01), 0f, 0.05f);
            tuning.Lz4MinEffort01 = math.saturate(SanitizeFinite(tuning.Lz4MinEffort01, 0.12f));
            tuning.Lz4MaxEffort01 = math.max(tuning.Lz4MinEffort01, math.saturate(SanitizeFinite(tuning.Lz4MaxEffort01, 0.92f)));
            tuning.LowQualityWriteHz = math.max(1f, SanitizeFinite(tuning.LowQualityWriteHz, 5f));
            tuning.HighQualityWriteHz = math.max(tuning.LowQualityWriteHz, SanitizeFinite(tuning.HighQualityWriteHz, 30f));
            tuning.ChunkUnloadDistanceMeters = math.max(64f, SanitizeFinite(tuning.ChunkUnloadDistanceMeters, 1800f));
            tuning.IoPressureBias01 = math.saturate(SanitizeFinite(tuning.IoPressureBias01, 0.35f));
            tuning.MaxWalWriteMillis = math.max(0.05f, SanitizeFinite(tuning.MaxWalWriteMillis, 0.35f));
            tuning.DepthMinMeters = math.max(0f, SanitizeFinite(tuning.DepthMinMeters, 0f));
            tuning.DepthMaxMeters = math.max(tuning.DepthMinMeters + 1f, SanitizeFinite(tuning.DepthMaxMeters, 1200f));
            if (tuning.MaxBytesPerFrame < 1024u)
                tuning.MaxBytesPerFrame = 64u * 1024u;

            return tuning;
        }

        internal static JobHandle ScheduleCompressionPipeline(
            VoxelDeltaCompressionVaultBufferSet buffers,
            int cellCount,
            int maxRunsPerBlock,
            int3 sectorCoord,
            uint simulationFrame,
            float globalQualityWeight,
            float ioPressure01,
            JobHandle dependency,
            bool injectMockDeformation = false,
            float lastDiskWriteLatencyMs = 0f)
        {
            if (!buffers.RuntimeDensity.IsCreated ||
                !buffers.BaselineDensity.IsCreated ||
                !buffers.MaterialIds.IsCreated ||
                !buffers.CellFlags.IsCreated ||
                !buffers.RleRuns.IsCreated ||
                !buffers.BlockCounters.IsCreated ||
                !buffers.RleBytes.IsCreated ||
                !buffers.CompressedBytes.IsCreated ||
                !buffers.Lz4HashTable.IsCreated ||
                !buffers.Headers.IsCreated ||
                !buffers.Counters.IsCreated)
            {
                return dependency;
            }

            int cellLimit = math.min(math.min(ChunkCellCount, ushort.MaxValue), math.min(buffers.RuntimeDensity.Length, buffers.BaselineDensity.Length));
            if (cellLimit <= 0)
                return dependency;

            VoxelDeltaCompressionTuningDTO tuning = ResolveRuntimeTuning(buffers.Tuning);
            int safeCellCount = math.clamp(cellCount <= 0 ? ChunkCellCount : cellCount, 1, cellLimit);
            int safeBlockCells = DefaultBlockCells;
            int blockCount = ResolveBlockCount(safeCellCount, safeBlockCells);
            int safeMaxRunsPerBlock = math.max(1, maxRunsPerBlock <= 0 ? safeBlockCells : maxRunsPerBlock);
            ulong sectorHash = ResolveSectorHash(sectorCoord);
            float effort01 = ResolveCompressionEffort01(globalQualityWeight, ioPressure01, lastDiskWriteLatencyMs, in tuning);

            JobHandle sourceReady = dependency;
            if (injectMockDeformation)
            {
                sourceReady = new MockVoxelDeformationGeneratorJob
                {
                    BaselineDensity = buffers.BaselineDensity,
                    RuntimeDensity = buffers.RuntimeDensity,
                    MaterialIds = buffers.MaterialIds,
                    CellFlags = buffers.CellFlags,
                    SectorHash = sectorHash,
                    SimulationFrame = simulationFrame,
                    GlobalQualityWeight = globalQualityWeight,
                    CellCount = safeCellCount
                }.Schedule(safeCellCount, 128, dependency);
            }

            JobHandle encode = new VoxelRleEncoderJob
            {
                RuntimeDensity = buffers.RuntimeDensity,
                BaselineDensity = buffers.BaselineDensity,
                MaterialIds = buffers.MaterialIds,
                CellFlags = buffers.CellFlags,
                Runs = buffers.RleRuns,
                BlockCounters = buffers.BlockCounters,
                CellCount = safeCellCount,
                BlockCellCount = safeBlockCells,
                MaxRunsPerBlock = safeMaxRunsPerBlock,
                SectorHash = sectorHash
            }.Schedule(blockCount, 1, sourceReady);

            JobHandle finalize = new VoxelDeltaRleFinalizeJob
            {
                BlockCounters = buffers.BlockCounters,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                SectorHash = sectorHash,
                SectorCoord = sectorCoord,
                BlockCount = blockCount,
                CellCount = safeCellCount,
                PruneThreshold01 = tuning.PruneThreshold01
            }.Schedule(encode);

            JobHandle pack = new VoxelDeltaRlePackJob
            {
                Runs = buffers.RleRuns,
                BlockCounters = buffers.BlockCounters,
                RuntimeDensity = buffers.RuntimeDensity,
                MaterialIds = buffers.MaterialIds,
                CellFlags = buffers.CellFlags,
                DestinationBytes = buffers.RleBytes,
                Counters = buffers.Counters,
                BlockCount = blockCount,
                MaxRunsPerBlock = safeMaxRunsPerBlock,
                CellCount = safeCellCount
            }.Schedule(finalize);

            JobHandle lz4 = new VoxelLz4CompressionJob
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
            }.Schedule(pack);

            JobHandle checksum = new VoxelDeltaChecksumHeaderJob
            {
                CompressedBytes = buffers.CompressedBytes,
                Counters = buffers.Counters,
                Headers = buffers.Headers,
                SectorStats = buffers.SectorStats,
                SectorHash = sectorHash
            }.Schedule(lz4);

            JobHandle walPack = new VoxelWalPayloadPackJob
            {
                Headers = buffers.Headers,
                CompressedBytes = buffers.CompressedBytes,
                WalPayloadBytes = buffers.RleBytes,
                Counters = buffers.Counters
            }.Schedule(checksum);

            if (!buffers.TelemetryRing.IsCreated || !buffers.TelemetryCursor.IsCreated)
                return walPack;

            return new VoxelDeltaTelemetryRecordJob
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
            }.Schedule(walPack);
        }

        internal static bool TryEnqueueVoxelDeltaWalWrite(
            IAsyncPersistenceService persistence,
            NativeArray<byte> walPayloadBytes,
            NativeArray<int> counters,
            NativeArray<VoxelDeltaHeaderDTO> headers,
            uint frame)
        {
            if (persistence == null ||
                !walPayloadBytes.IsCreated ||
                !counters.IsCreated ||
                !headers.IsCreated ||
                headers.Length <= 0 ||
                counters.Length <= CounterWalPayloadBytes)
            {
                return false;
            }

            int byteCount = counters[CounterWalPayloadBytes];
            if (byteCount <= UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>() ||
                byteCount > walPayloadBytes.Length ||
                byteCount > MaxVoxelDeltaWalPayloadBytes ||
                counters[CounterFailure] != 0)
            {
                return false;
            }

            VoxelDeltaHeaderDTO header = headers[0];
            uint sourceHash = (uint)(header.XXHash3Checksum ^ (header.XXHash3Checksum >> 32));
            return persistence.TryEnqueueChunkPageWrite(
                unchecked((long)header.SectorHash),
                H8WorldPagePayloadTypes.VoxelDeltaRle,
                walPayloadBytes,
                byteCount,
                sourceHash,
                frame);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveBlockCount(int cellCount, int blockCells)
        {
            int cells = math.max(1, cellCount);
            int block = math.max(1, blockCells);
            return (cells + block - 1) / block;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Align16(int value)
        {
            return (math.max(0, value) + 15) & ~15;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveCompressionEffort01(float globalQualityWeight, float ioPressure01, float diskLatencyMs)
        {
            VoxelDeltaCompressionTuningDTO tuning = BuildDefaultTuning();
            return ResolveCompressionEffort01(globalQualityWeight, ioPressure01, diskLatencyMs, in tuning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveCompressionEffort01(
            float globalQualityWeight,
            float ioPressure01,
            float diskLatencyMs,
            in VoxelDeltaCompressionTuningDTO tuning)
        {
            float quality = Sanitize01(globalQualityWeight);
            float pressureBias = math.saturate(SanitizeFinite(tuning.IoPressureBias01, 0.35f));
            float pressureScale = math.lerp(0.75f, 1.5f, pressureBias);
            float pressure = Sanitize01((ioPressure01 * pressureScale) + math.saturate(diskLatencyMs * 0.25f));
            float curvedQuality = quality * quality * (3f - (2f * quality));
            float curvedPressure = pressure * pressure * (3f - (2f * pressure));
            float minEffort = math.saturate(SanitizeFinite(tuning.Lz4MinEffort01, 0.12f));
            float maxEffort = math.max(minEffort, math.saturate(SanitizeFinite(tuning.Lz4MaxEffort01, 0.92f)));
            float tunedEffort = math.lerp(minEffort, maxEffort, curvedQuality);
            float thermalFloor = math.max(0.02f, minEffort * 0.35f);
            return math.saturate(math.lerp(tunedEffort, thermalFloor, curvedPressure));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveWriteHz(float globalQualityWeight, in VoxelDeltaCompressionTuningDTO tuning)
        {
            float quality = Sanitize01(globalQualityWeight);
            float curve = quality * quality * (3f - (2f * quality));
            float low = math.max(1f, SanitizeFinite(tuning.LowQualityWriteHz, 5f));
            float high = math.max(low, SanitizeFinite(tuning.HighQualityWriteHz, 30f));
            return math.lerp(low, high, curve);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ShouldPruneSector(int modifiedCells, int totalCells, float threshold01)
        {
            int safeTotal = math.max(1, totalCells);
            float ratio = (float)math.max(0, modifiedCells) / safeTotal;
            return ratio > 0f && ratio < math.max(0f, threshold01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ResolveSectorHash(int3 sectorCoord)
        {
            uint x = EncodeSignedMorton21(sectorCoord.x);
            uint y = EncodeSignedMorton21(sectorCoord.y);
            uint z = EncodeSignedMorton21(sectorCoord.z);
            ulong morton = (ExpandBits21(x) << 0) | (ExpandBits21(y) << 1) | (ExpandBits21(z) << 2);
            return morton ^ 0x9E3779B97F4A7C15UL;
        }

        public static ulong ResolveSectorHashFromAupVoxel(long voxelX, long voxelY, long voxelZ, int sectorCellSize)
        {
            int size = math.max(1, sectorCellSize);
            int3 sectorCoord = new int3(
                SaturatingLongToInt(FloorDiv(voxelX, size)),
                SaturatingLongToInt(FloorDiv(voxelY, size)),
                SaturatingLongToInt(FloorDiv(voxelZ, size)));
            return ResolveSectorHash(sectorCoord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteHeaderLittleEndian(byte* destination, in VoxelDeltaHeaderDTO header)
        {
            if (destination == null)
                return;

            WriteULongLittleEndian(destination, 0, header.SectorHash);
            WriteULongLittleEndian(destination, 8, header.XXHash3Checksum);
            WriteUIntLittleEndian(destination, 16, header.CompressedSize);
            WriteUIntLittleEndian(destination, 20, header.UncompressedSize);
            WriteUIntLittleEndian(destination, 24, header.Flags);
            WriteUIntLittleEndian(destination, 28, HeaderAlignedLayoutMarker);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VoxelDeltaHeaderDTO ReadHeaderLittleEndian(byte* source)
        {
            if (source == null)
                return default;

            VoxelDeltaHeaderDTO header = default;
            header.SectorHash = ReadULongLittleEndian(source, 0);
            header.XXHash3Checksum = ReadULongLittleEndian(source, 8);
            header.CompressedSize = ReadUIntLittleEndian(source, 16);
            header.UncompressedSize = ReadUIntLittleEndian(source, 20);
            header.Flags = ReadUIntLittleEndian(source, 24);
            header.LayoutMarker = ReadUIntLittleEndian(source, 28);
            return header;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VoxelDeltaHeaderDTO ReadLegacyHeaderLittleEndian(byte* source)
        {
            if (source == null)
                return default;

            VoxelDeltaHeaderDTO header = default;
            header.SectorHash = ReadULongLittleEndian(source, 0);
            header.CompressedSize = ReadUIntLittleEndian(source, 8);
            header.UncompressedSize = ReadUIntLittleEndian(source, 12);
            header.XXHash3Checksum = ReadULongLittleEndian(source, 16);
            header.Flags = ReadUIntLittleEndian(source, 24);
            header.LayoutMarker = ReadUIntLittleEndian(source, 28);
            return header;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsWalHeaderByteCountValid(int byteCount, int headerBytes, in VoxelDeltaHeaderDTO header, bool requireLayoutMarker)
        {
            if (requireLayoutMarker && header.LayoutMarker != HeaderAlignedLayoutMarker)
                return false;

            if (header.CompressedSize > int.MaxValue)
                return false;

            int compressedBytes = (int)header.CompressedSize;
            return compressedBytes >= 0 && compressedBytes <= byteCount - headerBytes;
        }

        internal static bool VerifyCompressedPayloadChecksum(NativeArray<byte> compressedBytes, int byteCount, in VoxelDeltaHeaderDTO header)
        {
            if (!compressedBytes.IsCreated)
                return false;

            int count = math.clamp(byteCount, 0, compressedBytes.Length);
            byte* ptr = (byte*)compressedBytes.GetUnsafeReadOnlyPtr();
            return VerifyCompressedPayloadChecksum(ptr, count, in header);
        }

        internal static bool TryReadAndVerifyWalPayload(
            NativeArray<byte> walPayloadBytes,
            int byteCount,
            out VoxelDeltaHeaderDTO header)
        {
            header = default;
            if (!walPayloadBytes.IsCreated)
                return false;

            int headerBytes = UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>();
            int count = math.clamp(byteCount, 0, walPayloadBytes.Length);
            if (count < headerBytes)
                return false;

            byte* ptr = (byte*)walPayloadBytes.GetUnsafeReadOnlyPtr();
            header = ReadHeaderLittleEndian(ptr);
            if (IsWalHeaderByteCountValid(count, headerBytes, in header, true))
            {
                int compressedBytes = (int)header.CompressedSize;
                if (VerifyCompressedPayloadChecksum(ptr + headerBytes, compressedBytes, in header))
                    return true;
            }

            VoxelDeltaHeaderDTO legacyHeader = ReadLegacyHeaderLittleEndian(ptr);
            if (!IsWalHeaderByteCountValid(count, headerBytes, in legacyHeader, false))
                return false;

            int legacyCompressedBytes = (int)legacyHeader.CompressedSize;
            if (!VerifyCompressedPayloadChecksum(ptr + headerBytes, legacyCompressedBytes, in legacyHeader))
                return false;

            header = legacyHeader;
            return true;
        }

        private static bool VerifyCompressedPayloadChecksum(byte* ptr, int count, in VoxelDeltaHeaderDTO header)
        {
            if (count <= 0)
                return header.XXHash3Checksum == 0UL;

            if (ptr == null)
                return false;

            SaveStateMerkleTree.Hash128(ptr, count, header.SectorHash ^ 0x58584833564F5845UL, out ulong lo, out ulong hi);
            ulong checksum = lo ^ ((hi << 32) | (hi >> 32));
            return checksum == header.XXHash3Checksum;
        }

        public static bool RunSelfAudit(NativeArray<int> results)
        {
            if (!results.IsCreated || results.Length < 12)
                return false;

            results[0] = UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>();
            results[1] = UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>();
            results[2] = UnsafeUtility.SizeOf<VoxelDeltaBlockCounter64>();
            results[3] = UnsafeUtility.SizeOf<VoxelDeltaCompressionTelemetryEntry>();
            results[4] = UnsafeUtility.SizeOf<VoxelDeltaCompressionTuningDTO>();
            results[5] = UnsafeUtility.SizeOf<VoxelDeltaSectorStatsDTO>();
            results[6] = UnsafeUtility.SizeOf<VoxelDeltaDearLieStateDTO>();
            results[7] = UnsafeUtility.SizeOf<VoxelDeltaMockSchemaDTO>();
            results[8] = HeaderFieldOffset(nameof(VoxelDeltaHeaderDTO.SectorHash));
            results[9] = HeaderFieldOffset(nameof(VoxelDeltaHeaderDTO.CompressedSize));
            results[10] = HeaderFieldOffset(nameof(VoxelDeltaHeaderDTO.UncompressedSize));
            results[11] = HeaderFieldOffset(nameof(VoxelDeltaHeaderDTO.XXHash3Checksum));
            if (results.Length > 12)
                results[12] = UnsafeUtility.SizeOf<VoxelDeltaTelemetryDumpHeaderDTO>();
            return results[0] == 32 &&
                   results[1] == 8 &&
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
                   (results.Length <= 12 || results[12] == 64);
        }

        public static bool RunCompressionRatioSelfAudit(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            int minimumSamples,
            float requiredPassRatio01 = 0.99f)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int samples = 0;
            int smaller = 0;
            for (int i = 0; i < telemetryRing.Length; i++)
            {
                VoxelDeltaCompressionTelemetryEntry entry = telemetryRing[i];
                if (entry.RawBytes == 0u)
                    continue;

                samples++;
                if (entry.CompressedBytes < entry.RawBytes)
                    smaller++;
            }

            if (samples < math.max(1, minimumSamples))
                return false;

            float ratio = (float)smaller / samples;
            return ratio >= math.saturate(requiredPassRatio01);
        }

        public static bool TryDumpTelemetryRing(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            string path = VoxelDeltaTelemetryDumpRelativePath)
        {
            return TryDumpTelemetryRing(telemetryRing, default, 0u, path);
        }

        public static bool TryDumpTelemetryRing(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            uint reasonFlags = 0u,
            string path = VoxelDeltaTelemetryDumpRelativePath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0 || string.IsNullOrEmpty(path))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int stride = UnsafeUtility.SizeOf<VoxelDeltaCompressionTelemetryEntry>();
                int capacity = math.min(TelemetryRingFrames, telemetryRing.Length);
                int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) % capacity : 0;
                int entryCount = CountTelemetryEntries(telemetryRing, capacity);
                int start = entryCount >= capacity ? cursor : 0;
                int headerBytes = UnsafeUtility.SizeOf<VoxelDeltaTelemetryDumpHeaderDTO>();
                VoxelDeltaCompressionTelemetryEntry first = entryCount > 0 ? telemetryRing[start] : default;
                VoxelDeltaCompressionTelemetryEntry last = entryCount > 0 ? telemetryRing[(start + entryCount - 1) % capacity] : default;
                VoxelDeltaTelemetryDumpHeaderDTO header = new VoxelDeltaTelemetryDumpHeaderDTO
                {
                    Magic = TelemetryDumpMagic,
                    Version = TelemetryDumpVersion,
                    EntryCount = (uint)entryCount,
                    EntryStride = (uint)stride,
                    Cursor = (uint)cursor,
                    ReasonFlags = reasonFlags,
                    RingCapacity = (uint)capacity,
                    HeaderBytes = (uint)headerBytes,
                    FirstSectorHash = first.SectorHash,
                    LastSectorHash = last.SectorHash,
                    FirstFrame = first.Frame,
                    LastFrame = last.Frame,
                    _pad0 = 0u
                };
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>((byte*)&header, headerBytes));
                    byte* source = (byte*)telemetryRing.GetUnsafeReadOnlyPtr();
                    for (int i = 0; i < entryCount; i++)
                    {
                        int index = (start + i) % capacity;
                        stream.Write(new ReadOnlySpan<byte>(source + (index * stride), stride));
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

        private static int CountTelemetryEntries(NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing, int capacity)
        {
            int count = 0;
            int limit = math.min(capacity, telemetryRing.Length);
            for (int i = 0; i < limit; i++)
            {
                VoxelDeltaCompressionTelemetryEntry entry = telemetryRing[i];
                if (entry.SectorHash != 0UL ||
                    entry.PayloadHash != 0UL ||
                    entry.RawBytes != 0u ||
                    entry.CompressedBytes != 0u ||
                    entry.Flags != 0u ||
                    entry.Frame != 0u)
                {
                    count++;
                }
            }

            return count;
        }

        public static bool TryDumpTelemetryRingOnLatencySpike(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            float diskWriteLatencyMs,
            float thresholdMs = 50f,
            string path = VoxelDeltaTelemetryDumpRelativePath)
        {
            float latency = math.isfinite(diskWriteLatencyMs) ? diskWriteLatencyMs : 0f;
            float threshold = math.max(0f, math.isfinite(thresholdMs) ? thresholdMs : 50f);
            return latency >= threshold && TryDumpTelemetryRing(telemetryRing, path);
        }

        public static bool TryDumpTelemetryRingOnLatencySpike(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            float diskWriteLatencyMs,
            float thresholdMs = 50f,
            string path = VoxelDeltaTelemetryDumpRelativePath)
        {
            float latency = math.isfinite(diskWriteLatencyMs) ? diskWriteLatencyMs : 0f;
            float threshold = math.max(0f, math.isfinite(thresholdMs) ? thresholdMs : 50f);
            return latency >= threshold && TryDumpTelemetryRing(telemetryRing, telemetryCursor, TelemetryFlagDiskLatencySpike, path);
        }

        public static bool TryDumpTelemetryRingOnSpikeFlag(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            string path = VoxelDeltaTelemetryDumpRelativePath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int capacity = math.min(TelemetryRingFrames, telemetryRing.Length);
            int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) % capacity : 0;
            int count = CountTelemetryEntries(telemetryRing, capacity);
            int start = count >= capacity ? cursor : 0;
            for (int i = 0; i < count; i++)
            {
                VoxelDeltaCompressionTelemetryEntry entry = telemetryRing[(start + i) % capacity];
                if ((entry.Flags & TelemetryFlagDiskLatencySpike) != 0u)
                    return TryDumpTelemetryRing(telemetryRing, telemetryCursor, TelemetryFlagDiskLatencySpike, path);
            }

            return false;
        }

        public static JobHandle ScheduleDiskLatencyTelemetryPatch(
            NativeArray<VoxelDeltaCompressionTelemetryEntry> telemetryRing,
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

            return new VoxelDeltaDiskLatencyTelemetryPatchJob
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

        private static int HeaderFieldOffset(string fieldName)
        {
            return Marshal.OffsetOf(typeof(VoxelDeltaHeaderDTO), fieldName).ToInt32();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SaturatingUIntToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long FloorDiv(long value, int divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder != 0L && ((remainder < 0L) != (divisor < 0)) ? quotient - 1L : quotient;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MockVoxelDeformationGeneratorJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<sbyte> BaselineDensity;
            [NoAlias] public NativeArray<sbyte> RuntimeDensity;
            [NoAlias] public NativeArray<byte> MaterialIds;
            [NoAlias] public NativeArray<byte> CellFlags;
            public ulong SectorHash;
            public uint SimulationFrame;
            public float GlobalQualityWeight;
            public int CellCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)CellCount || index >= RuntimeDensity.Length)
                    return;

                uint seed = BuildDeterministicSeed(SectorHash, SimulationFrame, (uint)index);
                sbyte baseline = (sbyte)math.clamp(((int)((seed >> 8) & 63u)) - 32, -64, 63);
                if (BaselineDensity.IsCreated && index < BaselineDensity.Length)
                    BaselineDensity[index] = baseline;

                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
                float probability = math.lerp(0.015f, 0.22f, quality * quality);
                float active = math.step(rng.NextFloat(), probability);
                int signedDelta = (int)math.round(math.lerp(3f, 28f, quality) * (rng.NextFloat() < 0.5f ? -1f : 1f));
                sbyte density = (sbyte)math.clamp(baseline + ((int)active * signedDelta), -128, 127);
                RuntimeDensity[index] = density;

                if (MaterialIds.IsCreated && index < MaterialIds.Length)
                    MaterialIds[index] = active > 0f ? unchecked((byte)(1u + (seed & 15u))) : (byte)0;

                if (CellFlags.IsCreated && index < CellFlags.Length)
                    CellFlags[index] = active > 0f ? (byte)1 : (byte)0;
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
        internal struct VoxelCarvingTortureJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<sbyte> RuntimeDensity;
            [NoAlias] public NativeArray<byte> MaterialIds;
            [NoAlias] public NativeArray<byte> CellFlags;
            public ulong SectorHash;
            public uint SimulationFrame;
            public float GlobalQualityWeight;
            public int CellCount;
            public int OperationCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)CellCount || index >= RuntimeDensity.Length)
                    return;

                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
                int operations = math.clamp(OperationCount <= 0 ? 128 : OperationCount, 1, 256);
                float3 cell = new float3(index & 31, (index >> 5) & 31, (index >> 10) & 31);
                uint baseSeed = BuildStressSeed(SectorHash, SimulationFrame, (uint)index);
                float carved = 0f;

                for (int op = 0; op < operations; op++)
                {
                    uint opSeed = baseSeed ^ ((uint)op * 0x9E3779B9u);
                    float3 center = new float3(
                        Hash01(opSeed ^ 0xA511E9B3u) * 31f,
                        Hash01(opSeed ^ 0x63D83595u) * 31f,
                        Hash01(opSeed ^ 0xB5297A4Du) * 31f);
                    float radius = math.lerp(3.5f, 10.0f, quality);
                    float distanceSq = math.lengthsq(cell - center);
                    carved = math.max(carved, math.step(distanceSq, radius * radius));
                }

                sbyte density = carved > 0f
                    ? (sbyte)127
                    : (sbyte)math.clamp(((int)((baseSeed >> 9) & 63u)) - 32, -64, 63);
                RuntimeDensity[index] = density;

                if (MaterialIds.IsCreated && index < MaterialIds.Length)
                    MaterialIds[index] = carved > 0f ? (byte)12 : (byte)0;

                if (CellFlags.IsCreated && index < CellFlags.Length)
                    CellFlags[index] = carved > 0f ? (byte)1 : (byte)0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint BuildStressSeed(ulong sectorHash, uint frame, uint index)
            {
                ulong x = sectorHash ^ ((ulong)frame << 32) ^ index ^ 0x6A09E667F3BCC909UL;
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return (uint)(x | 1UL);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Hash01(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) * (1.0f / 16777215.0f);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelRleEncoderJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<sbyte> RuntimeDensity;
            [ReadOnly, NoAlias] public NativeArray<sbyte> BaselineDensity;
            [ReadOnly, NoAlias] public NativeArray<byte> MaterialIds;
            [ReadOnly, NoAlias] public NativeArray<byte> CellFlags;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<VoxelDeltaRleRunDTO> Runs;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<VoxelDeltaBlockCounter64> BlockCounters;
            public int CellCount;
            public int BlockCellCount;
            public int MaxRunsPerBlock;
            public ulong SectorHash;

            public void Execute(int blockIndex)
            {
                if (!RuntimeDensity.IsCreated || !Runs.IsCreated || !BlockCounters.IsCreated || (uint)blockIndex >= (uint)BlockCounters.Length)
                    return;

                int blockCells = math.max(1, BlockCellCount);
                int start = blockIndex * blockCells;
                int end = math.min(math.min(CellCount, RuntimeDensity.Length), start + blockCells);
                int maxRuns = math.max(1, MaxRunsPerBlock);
                int runBase = blockIndex * maxRuns;
                int runLimit = math.min(Runs.Length, runBase + maxRuns);
                int write = runBase;
                int modified = 0;
                bool overflow = false;

                for (int i = start; i < end;)
                {
                    sbyte current = RuntimeDensity[i];
                    sbyte baseline = BaselineDensity.IsCreated && i < BaselineDensity.Length ? BaselineDensity[i] : (sbyte)0;
                    byte material = MaterialIds.IsCreated && i < MaterialIds.Length ? MaterialIds[i] : (byte)0;
                    byte flags = CellFlags.IsCreated && i < CellFlags.Length ? CellFlags[i] : (byte)0;
                    if (current == baseline && material == 0 && flags == 0)
                    {
                        i++;
                        continue;
                    }

                    int runStart = i;
                    int runLength = 1;
                    i++;
                    while (i < end &&
                           runLength < ushort.MaxValue &&
                           RuntimeDensity[i] == current &&
                           (!BaselineDensity.IsCreated || i >= BaselineDensity.Length || BaselineDensity[i] != current) &&
                           (!MaterialIds.IsCreated || i >= MaterialIds.Length || MaterialIds[i] == material) &&
                           (!CellFlags.IsCreated || i >= CellFlags.Length || CellFlags[i] == flags))
                    {
                        runLength++;
                        i++;
                    }

                    modified += runLength;
                    if (write < runLimit)
                    {
                        Runs[write] = new VoxelDeltaRleRunDTO
                        {
                            StartIndex = (ushort)runStart,
                            RunLength = (ushort)runLength,
                            SdfValue = current,
                            MaterialId = material,
                            Flags = flags,
                            Reserved0 = 0
                        };
                        write++;
                    }
                    else
                    {
                        overflow = true;
                    }
                }

                uint runCount = (uint)math.max(0, write - runBase);
                BlockCounters[blockIndex] = new VoxelDeltaBlockCounter64
                {
                    RunCount = runCount,
                    ModifiedCellCount = (uint)math.max(0, modified),
                    EncodedBytes = runCount * (uint)UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>(),
                    Flags = overflow ? HeaderFlagFatal : 0u,
                    SectorHash = SectorHash,
                    _pad0 = 0u,
                    _pad1 = 0u,
                    _pad2 = 0u,
                    _pad3 = 0u,
                    _pad4 = 0u,
                    _pad5 = 0u,
                    _pad6 = 0u,
                    _pad7 = 0u,
                    _pad8 = 0u,
                    _pad9 = 0u
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDeltaRleFinalizeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<VoxelDeltaBlockCounter64> BlockCounters;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<VoxelDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<VoxelDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;
            public int3 SectorCoord;
            public int BlockCount;
            public int CellCount;
            public float PruneThreshold01;

            public void Execute()
            {
                if (!Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                int blockLimit = BlockCounters.IsCreated ? math.min(BlockCount, BlockCounters.Length) : 0;
                uint runCount = 0u;
                uint modified = 0u;
                uint encodedBytes = 0u;
                uint flags = 0u;
                for (int i = 0; i < blockLimit; i++)
                {
                    VoxelDeltaBlockCounter64 counter = BlockCounters[i];
                    runCount += counter.RunCount;
                    modified += counter.ModifiedCellCount;
                    encodedBytes += counter.EncodedBytes;
                    flags |= counter.Flags;
                }

                int safeCells = math.max(1, CellCount);
                float modifiedRatio = (float)modified / safeCells;
                bool pruned = modified > 0u && modifiedRatio < math.max(0f, PruneThreshold01);
                if (pruned)
                {
                    runCount = 0u;
                    encodedBytes = 0u;
                    flags |= HeaderFlagPruned;
                }

                uint rlePayloadLimit = (uint)(MaxVoxelDeltaWalPayloadBytes - VoxelDeltaHeaderBytes);
                bool rlePayloadOverflow =
                    !pruned &&
                    modified > 0u &&
                    (runCount > (uint)MaxVoxelDeltaRleRunsPerWalPayload ||
                     encodedBytes > rlePayloadLimit ||
                     (flags & HeaderFlagFatal) != 0u);
                if (rlePayloadOverflow)
                {
                    encodedBytes = (uint)ResolveDenseRawBytes(safeCells);
                    flags = (flags & ~HeaderFlagFatal) | HeaderFlagDenseFallback;
                }

                Counters[CounterRleRunCount] = SaturatingUIntToInt(runCount);
                Counters[CounterModifiedCellCount] = SaturatingUIntToInt(modified);
                Counters[CounterRleBytes] = SaturatingUIntToInt(encodedBytes);
                Counters[CounterPruned] = pruned ? 1 : 0;
                int denseRawBytes = ResolveDenseRawBytes(safeCells);
                Counters[CounterRawBytes] = denseRawBytes;
                Counters[CounterFailure] = (flags & HeaderFlagFatal) != 0u ? 1 : 0;
                Counters[CounterCompressionFlags] = (int)flags;
                Counters[CounterBlockCount] = blockLimit;

                if (Headers.IsCreated && Headers.Length > 0)
                {
                    Headers[0] = new VoxelDeltaHeaderDTO
                    {
                        SectorHash = SectorHash,
                        CompressedSize = 0u,
                        UncompressedSize = encodedBytes,
                        XXHash3Checksum = 0UL,
                        Flags = flags,
                        LayoutMarker = HeaderAlignedLayoutMarker
                    };
                }

                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    SectorStats[0] = new VoxelDeltaSectorStatsDTO
                    {
                        SectorHash = SectorHash,
                        SectorX = SectorCoord.x,
                        SectorY = SectorCoord.y,
                        SectorZ = SectorCoord.z,
                        RawBytes = (uint)denseRawBytes,
                        CompressedBytes = 0u,
                        ModifiedCells = modified,
                        RleRuns = runCount,
                        ModifiedRatio01 = math.saturate(modifiedRatio),
                        CompressionRatio01 = 0f,
                        VisualFade01 = 0f,
                        Flags = flags,
                        _pad0 = 0u,
                        _pad1 = 0u,
                        _pad2 = 0u
                    };
                }
            }

            private static int ResolveDenseRawBytes(int safeCells)
            {
                int dirtyMaskBytes = (((safeCells + 31) >> 5) * sizeof(uint));
                return safeCells > (int.MaxValue - dirtyMaskBytes) / VoxelDeltaDenseFallbackCellBytes
                    ? int.MaxValue
                    : dirtyMaskBytes + (safeCells * VoxelDeltaDenseFallbackCellBytes);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDeltaRlePackJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<VoxelDeltaRleRunDTO> Runs;
            [ReadOnly, NoAlias] public NativeArray<VoxelDeltaBlockCounter64> BlockCounters;
            [ReadOnly, NoAlias] public NativeArray<sbyte> RuntimeDensity;
            [ReadOnly, NoAlias] public NativeArray<byte> MaterialIds;
            [ReadOnly, NoAlias] public NativeArray<byte> CellFlags;
            [NoAlias] public NativeArray<byte> DestinationBytes;
            [NoAlias] public NativeArray<int> Counters;
            public int BlockCount;
            public int MaxRunsPerBlock;
            public int CellCount;

            public void Execute()
            {
                if (!DestinationBytes.IsCreated || !Counters.IsCreated || Counters.Length < CounterCapacity)
                    return;

                if ((unchecked((uint)Counters[CounterCompressionFlags]) & HeaderFlagDenseFallback) != 0u)
                {
                    PackDenseFallback();
                    return;
                }

                if (!Runs.IsCreated || !BlockCounters.IsCreated)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterRleBytes] = 0;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    return;
                }

                if (Counters[CounterPruned] != 0 || Counters[CounterFailure] != 0)
                {
                    Counters[CounterRleBytes] = 0;
                    return;
                }

                int runStride = UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>();
                int write = 0;
                int destinationLimit = math.max(0, DestinationBytes.Length - 64);
                int blockLimit = math.min(BlockCount, BlockCounters.Length);
                int maxRuns = math.max(1, MaxRunsPerBlock);
                byte* destination = (byte*)DestinationBytes.GetUnsafePtr();
                VoxelDeltaRleRunDTO* source = (VoxelDeltaRleRunDTO*)Runs.GetUnsafeReadOnlyPtr();

                for (int block = 0; block < blockLimit; block++)
                {
                    int sourceRun = block * maxRuns;
                    int count = (int)math.min(BlockCounters[block].RunCount, (uint)maxRuns);
                    int bytes = count * runStride;
                    if (sourceRun < 0 || sourceRun + count > Runs.Length || write > destinationLimit - bytes)
                    {
                        Counters[CounterFailure] = 1;
                        Counters[CounterRleBytes] = 0;
                        return;
                    }

                    UnsafeUtility.MemCpy(destination + write, source + sourceRun, bytes);
                    write += bytes;
                }

                Counters[CounterRleBytes] = write;
            }

            private void PackDenseFallback()
            {
                if (!RuntimeDensity.IsCreated || !MaterialIds.IsCreated || !CellFlags.IsCreated)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterRleBytes] = 0;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    return;
                }

                int cells = math.clamp(CellCount, 0, math.min(RuntimeDensity.Length, math.min(MaterialIds.Length, CellFlags.Length)));
                int dirtyMaskWords = (cells + 31) >> 5;
                int dirtyMaskBytes = dirtyMaskWords * sizeof(uint);
                int denseBytes = dirtyMaskBytes + (cells * VoxelDeltaDenseFallbackCellBytes);
                int destinationLimit = math.max(0, DestinationBytes.Length - 64);
                if (cells <= 0 || denseBytes > destinationLimit || denseBytes > MaxVoxelDeltaWalPayloadBytes - VoxelDeltaHeaderBytes)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterRleBytes] = 0;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    return;
                }

                byte* destination = (byte*)DestinationBytes.GetUnsafePtr();
                sbyte* density = (sbyte*)RuntimeDensity.GetUnsafeReadOnlyPtr();
                byte* material = (byte*)MaterialIds.GetUnsafeReadOnlyPtr();
                byte* flags = (byte*)CellFlags.GetUnsafeReadOnlyPtr();
                uint* dirtyMask = (uint*)destination;
                for (int word = 0; word < dirtyMaskWords; word++)
                    dirtyMask[word] = 0xFFFFFFFFu;

                int tailBits = cells & 31;
                if (tailBits != 0)
                    dirtyMask[dirtyMaskWords - 1] = (1u << tailBits) - 1u;

                ushort* sdf = (ushort*)(destination + dirtyMaskBytes);
                for (int i = 0; i < cells; i++)
                    sdf[i] = (ushort)(math.clamp((int)density[i] + 128, 0, 255) << 8);

                int materialOffset = dirtyMaskBytes + (cells * sizeof(ushort));
                int flagsOffset = materialOffset + cells;
                UnsafeUtility.MemCpy(destination + materialOffset, material, cells);
                UnsafeUtility.MemCpy(destination + flagsOffset, flags, cells);
                Counters[CounterRleBytes] = denseBytes;
                Counters[CounterFailure] = 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelLz4CompressionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [NoAlias] public NativeArray<byte> Destination;
            [NoAlias] public NativeArray<int> HashTable;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<VoxelDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<VoxelDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;
            public float CompressionEffort01;
            public float IoPressure01;
            public int SourceLengthCounterIndex;

            public void Execute()
            {
                if (!Source.IsCreated || !Destination.IsCreated || !HashTable.IsCreated || !Counters.IsCreated || !Headers.IsCreated || Headers.Length <= 0 || Counters.Length < CounterCapacity)
                    return;

                int sourceLength = SourceLengthCounterIndex >= 0 && SourceLengthCounterIndex < Counters.Length ? Counters[SourceLengthCounterIndex] : 0;
                sourceLength = math.clamp(sourceLength, 0, Source.Length);
                if (sourceLength <= 0 || Counters[CounterPruned] != 0 || Counters[CounterFailure] != 0)
                {
                    Headers[0] = new VoxelDeltaHeaderDTO
                    {
                        SectorHash = SectorHash,
                        CompressedSize = 0u,
                        UncompressedSize = 0u,
                        XXHash3Checksum = 0UL,
                        Flags = unchecked((uint)Counters[CounterCompressionFlags]),
                        LayoutMarker = HeaderAlignedLayoutMarker
                    };
                    Counters[CounterCompressedBytes] = 0;
                    Counters[CounterWalPayloadBytes] = UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>();
                    if (Counters[CounterPruned] != 0)
                        Counters[CounterCompressionFlags] |= (int)HeaderFlagPruned;
                    WriteSectorStatsCompression(0, Counters[CounterCompressionFlags]);
                    return;
                }

                float effort = math.saturate(math.isfinite(CompressionEffort01) ? CompressionEffort01 : 0f);
                float pressure = math.saturate(math.isfinite(IoPressure01) ? IoPressure01 : 0f);
                int activeHashSlots = math.clamp((int)math.round(math.lerp(512f, HashTable.Length, effort * (1f - (pressure * 0.25f)))), 256, HashTable.Length);
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
                    Counters[CounterFailure] = 1;
                    Counters[CounterCompressedBytes] = 0;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    WriteSectorStatsCompression(0, Counters[CounterCompressionFlags]);
                    return;
                }

                if (useRaw)
                    UnsafeUtility.MemCpy(Destination.GetUnsafePtr(), Source.GetUnsafeReadOnlyPtr(), sourceLength);

                Counters[CounterCompressedBytes] = storedBytes;
                Counters[CounterCompressionFlags] = (Counters[CounterCompressionFlags] & ~(int)(HeaderFlagLz4 | HeaderFlagRaw)) | (useRaw ? (int)HeaderFlagRaw : (int)HeaderFlagLz4);
                uint headerFlags = unchecked((uint)Counters[CounterCompressionFlags]);
                Counters[CounterWalPayloadBytes] = UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>() + storedBytes;
                Headers[0] = new VoxelDeltaHeaderDTO
                {
                    SectorHash = SectorHash,
                    CompressedSize = (uint)storedBytes,
                    UncompressedSize = (uint)sourceLength,
                    XXHash3Checksum = 0UL,
                    Flags = headerFlags,
                    LayoutMarker = HeaderAlignedLayoutMarker
                };
                WriteSectorStatsCompression(storedBytes, Counters[CounterCompressionFlags]);
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

                VoxelDeltaSectorStatsDTO stats = SectorStats[0];
                uint compressed = (uint)math.max(0, storedBytes);
                stats.CompressedBytes = compressed;
                stats.CompressionRatio01 = stats.RawBytes > 0u ? math.saturate((float)compressed / stats.RawBytes) : 0f;
                stats.Flags = unchecked((uint)flags);
                SectorStats[0] = stats;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDeltaChecksumHeaderJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CompressedBytes;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<VoxelDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<VoxelDeltaSectorStatsDTO> SectorStats;
            public ulong SectorHash;

            public void Execute()
            {
                if (!CompressedBytes.IsCreated || !Counters.IsCreated || !Headers.IsCreated || Headers.Length <= 0 || Counters.Length < CounterCapacity)
                    return;

                VoxelDeltaHeaderDTO header = Headers[0];
                int count = Counters[CounterCompressedBytes];
                if (count < 0 || count > CompressedBytes.Length || count > MaxVoxelDeltaWalPayloadBytes)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterCompressionFlags] |= unchecked((int)HeaderFlagFatal);
                    header.XXHash3Checksum = 0UL;
                    header.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    header.LayoutMarker = HeaderAlignedLayoutMarker;
                    Headers[0] = header;
                    return;
                }

                if (count <= 0)
                {
                    header.XXHash3Checksum = 0UL;
                    Headers[0] = header;
                    return;
                }

                byte* ptr = (byte*)CompressedBytes.GetUnsafeReadOnlyPtr();
                SaveStateMerkleTree.Hash128(ptr, count, SectorHash ^ 0x58584833564F5845UL, out ulong lo, out ulong hi);
                header.XXHash3Checksum = lo ^ ((hi << 32) | (hi >> 32));
                Counters[CounterCompressionFlags] |= (int)HeaderFlagChecksumValid;
                header.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                header.LayoutMarker = HeaderAlignedLayoutMarker;
                Headers[0] = header;
                if (SectorStats.IsCreated && SectorStats.Length > 0)
                {
                    VoxelDeltaSectorStatsDTO stats = SectorStats[0];
                    stats.Flags = unchecked((uint)Counters[CounterCompressionFlags]);
                    SectorStats[0] = stats;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelWalPayloadPackJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<VoxelDeltaHeaderDTO> Headers;
            [ReadOnly, NoAlias] public NativeArray<byte> CompressedBytes;
            [NoAlias] public NativeArray<byte> WalPayloadBytes;
            [NoAlias] public NativeArray<int> Counters;

            public void Execute()
            {
                if (!Headers.IsCreated ||
                    !CompressedBytes.IsCreated ||
                    !WalPayloadBytes.IsCreated ||
                    !Counters.IsCreated ||
                    Headers.Length <= 0 ||
                    Counters.Length <= CounterWalPayloadBytes)
                {
                    return;
                }

                int headerBytes = UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>();
                int compressedBytes = Counters[CounterCompressedBytes];
                if (compressedBytes < 0 ||
                    compressedBytes > CompressedBytes.Length ||
                    compressedBytes > MaxVoxelDeltaWalPayloadBytes - headerBytes)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterWalPayloadBytes] = 0;
                    return;
                }

                int required = headerBytes + compressedBytes;
                if (Counters[CounterFailure] != 0 ||
                    required > WalPayloadBytes.Length ||
                    required > MaxVoxelDeltaWalPayloadBytes)
                {
                    Counters[CounterFailure] = 1;
                    Counters[CounterWalPayloadBytes] = 0;
                    return;
                }

                byte* destination = (byte*)WalPayloadBytes.GetUnsafePtr();
                VoxelDeltaHeaderDTO header = Headers[0];
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
        internal struct VoxelDeltaTelemetryRecordJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<int> Counters;
            [ReadOnly, NoAlias] public NativeArray<VoxelDeltaHeaderDTO> Headers;
            [NoAlias] public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;
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
                VoxelDeltaHeaderDTO header = Headers.Length > 0 ? Headers[0] : default;
                uint rawBytes = Counters.Length > CounterRawBytes ? (uint)math.max(0, Counters[CounterRawBytes]) : 0u;
                uint compressedBytes = Counters.Length > CounterCompressedBytes ? (uint)math.max(0, Counters[CounterCompressedBytes]) : 0u;
                uint flags = Counters.Length > CounterCompressionFlags ? unchecked((uint)Counters[CounterCompressionFlags]) : 0u;
                TelemetryRing[index] = new VoxelDeltaCompressionTelemetryEntry
                {
                    SectorHash = header.SectorHash,
                    PayloadHash = header.XXHash3Checksum,
                    Frame = Frame,
                    RawBytes = rawBytes,
                    CompressedBytes = compressedBytes,
                    Flags = flags,
                    BurstTimeMs = SanitizeMs(BurstTimeMs),
                    DiskWriteLatencyMs = SanitizeMs(DiskWriteLatencyMs),
                    GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f),
                    CompressionEffort01 = math.saturate(math.isfinite(CompressionEffort01) ? CompressionEffort01 : 0f),
                    RleRunCount = Counters.Length > CounterRleRunCount ? (uint)math.max(0, Counters[CounterRleRunCount]) : 0u,
                    PrunedCellCount = Counters.Length > CounterPruned && Counters[CounterPruned] != 0 && Counters.Length > CounterModifiedCellCount ? (uint)math.max(0, Counters[CounterModifiedCellCount]) : 0u,
                    IoPressureMicro = (uint)math.round(math.saturate(math.isfinite(IoPressure01) ? IoPressure01 : 0f) * 1000000f),
                    _pad0 = 0u
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
        internal struct VoxelDeltaDiskLatencyTelemetryPatchJob : IJob
        {
            [NoAlias] public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;
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
                float latency = SanitizeMs(DiskWriteLatencyMs);
                float threshold = math.max(0f, math.isfinite(SpikeThresholdMs) ? SpikeThresholdMs : 50f);
                uint patchFlags = TelemetryFlagDiskLatencyPatched | (latency >= threshold ? TelemetryFlagDiskLatencySpike : 0u);
                for (int step = 0; step < length; step++)
                {
                    int index = (cursor - 1 - step + length) % length;
                    VoxelDeltaCompressionTelemetryEntry entry = TelemetryRing[index];
                    if (entry.SectorHash != SectorHash || (MatchFrame != 0 && entry.Frame != Frame))
                        continue;

                    entry.DiskWriteLatencyMs = latency;
                    entry.Flags |= patchFlags;
                    TelemetryRing[index] = entry;
                    return;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeMs(float value)
            {
                return math.max(0f, math.isfinite(value) ? value : 0f);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDearLieDeformationFadeJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<VoxelDeltaDearLieStateDTO> States;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<VoxelDeltaSectorStatsDTO> SectorStats;
            public uint SimulationFrame;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (!States.IsCreated || (uint)index >= (uint)States.Length)
                    return;

                VoxelDeltaDearLieStateDTO state = States[index];
                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
                uint duration = (uint)math.max(1, (int)math.round(math.lerp(4f, 18f, quality * quality)));
                state.DurationTicks = state.DurationTicks == 0u ? duration : state.DurationTicks;
                uint elapsed = SimulationFrame >= state.StartFrame ? SimulationFrame - state.StartFrame : 0u;
                uint durationTicks = state.DurationTicks == 0u ? 1u : state.DurationTicks;
                float t = math.saturate((float)elapsed / durationTicks);
                float smooth = t * t * (3f - (2f * t));
                state.VisualFade01 = math.lerp(0f, math.saturate(state.TargetStrength01), smooth);
                state.GlobalQualityWeight = quality;
                States[index] = state;

                if (SectorStats.IsCreated && index < SectorStats.Length)
                {
                    VoxelDeltaSectorStatsDTO stats = SectorStats[index];
                    stats.VisualFade01 = state.VisualFade01;
                    SectorStats[index] = stats;
                }
            }
        }

#if UNITY_EDITOR
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelCompressionProfileCsvParseJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> CsvBytes;
            [NoAlias] public NativeArray<VoxelDeltaCompressionTuningDTO> Profiles;
            [NoAlias] public NativeArray<int> Counters;
            public int ByteCount;

            public void Execute()
            {
                if (!CsvBytes.IsCreated || !Profiles.IsCreated || Profiles.Length <= 0)
                    return;

                VoxelDeltaCompressionTuningDTO profile = Profiles[0].SchemaHash == 0u
                    ? BuildDefaultProfile()
                    : Profiles[0];
                int limit = math.clamp(ByteCount <= 0 ? CsvBytes.Length : ByteCount, 0, CsvBytes.Length);
                int lineStart = 0;
                int parsed = 0;
                int failure = 0;
                byte* data = (byte*)CsvBytes.GetUnsafeReadOnlyPtr();
                for (int i = 0; i <= limit; i++)
                {
                    if (i < limit && data[i] != (byte)'\n' && data[i] != (byte)'\r')
                        continue;

                    if (TryParseKeyValueLine(data, lineStart, i, ref profile))
                        parsed++;
                    else if (i > lineStart)
                        failure++;

                    while (i + 1 < limit && (data[i + 1] == (byte)'\n' || data[i + 1] == (byte)'\r'))
                        i++;

                    lineStart = i + 1;
                }

                Profiles[0] = profile;
                if (Counters.IsCreated && Counters.Length > CounterCsvFailure)
                {
                    Counters[CounterParsedProfiles] = parsed;
                    Counters[CounterCsvFailure] = failure;
                }
            }

            private static VoxelDeltaCompressionTuningDTO BuildDefaultProfile()
            {
                return new VoxelDeltaCompressionTuningDTO
                {
                    ProfileHash = 0x5348494E4F425531UL,
                    SchemaHash = 0x56584431u,
                    Flags = 0u,
                    PruneThreshold01 = DefaultPruneThreshold01,
                    Lz4MinEffort01 = 0.12f,
                    Lz4MaxEffort01 = 0.92f,
                    LowQualityWriteHz = 5f,
                    HighQualityWriteHz = 30f,
                    ChunkUnloadDistanceMeters = 1800f,
                    IoPressureBias01 = 0.35f,
                    MaxWalWriteMillis = 0.35f,
                    MaxBytesPerFrame = 64u * 1024u,
                    DepthMinMeters = 0f,
                    DepthMaxMeters = 1200f,
                    _pad0 = 0u
                };
            }

            private static bool TryParseKeyValueLine(byte* data, int start, int end, ref VoxelDeltaCompressionTuningDTO profile)
            {
                int keyStart = SkipWhitespace(data, start, end);
                if (keyStart == 0 &&
                    end - keyStart >= 3 &&
                    data[keyStart] == 0xEF &&
                    data[keyStart + 1] == 0xBB &&
                    data[keyStart + 2] == 0xBF)
                {
                    keyStart += 3;
                }

                if (keyStart >= end || data[keyStart] == (byte)'#')
                    return true;

                int separator = keyStart;
                while (separator < end && data[separator] != (byte)',' && data[separator] != (byte)'=')
                    separator++;

                if (separator >= end)
                    return false;

                int keyEnd = TrimEndWhitespace(data, keyStart, separator);
                int valueStart = SkipWhitespace(data, separator + 1, end);
                int valueEnd = TrimValueEnd(data, valueStart, end);
                uint keyHash = HashAsciiLower(data + keyStart, keyEnd - keyStart);
                if (keyHash == KeyBiome)
                {
                    profile.ProfileHash = HashAsciiLower64(data + valueStart, valueEnd - valueStart);
                    profile.Flags |= 1u << 1;
                    return true;
                }

                if (!TryParseFloat(data, valueStart, valueEnd, out float value))
                    return false;

                switch (keyHash)
                {
                    case KeyPruneThreshold01:
                        profile.PruneThreshold01 = math.clamp(value, 0f, 0.05f);
                        return true;
                    case KeyLz4MinEffort01:
                        profile.Lz4MinEffort01 = math.saturate(value);
                        return true;
                    case KeyLz4MaxEffort01:
                        profile.Lz4MaxEffort01 = math.saturate(value);
                        return true;
                    case KeyLowQualityWriteHz:
                        profile.LowQualityWriteHz = math.max(1f, value);
                        return true;
                    case KeyHighQualityWriteHz:
                        profile.HighQualityWriteHz = math.max(profile.LowQualityWriteHz, value);
                        return true;
                    case KeyChunkUnloadDistanceM:
                        profile.ChunkUnloadDistanceMeters = math.max(64f, value);
                        return true;
                    case KeyIoPressureBias01:
                        profile.IoPressureBias01 = math.saturate(value);
                        return true;
                    case KeyMaxWalWriteMs:
                        profile.MaxWalWriteMillis = math.max(0.05f, value);
                        return true;
                    case KeyMaxBytesPerFrame:
                        profile.MaxBytesPerFrame = (uint)math.max(1024, (int)math.round(value));
                        return true;
                    case KeyDepthMinM:
                        profile.DepthMinMeters = math.max(0f, value);
                        return true;
                    case KeyDepthMaxM:
                        profile.DepthMaxMeters = math.max(profile.DepthMinMeters + 1f, value);
                        return true;
                    default:
                        return false;
                }
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

            private static bool TryParseFloat(byte* data, int start, int end, out float value)
            {
                value = 0f;
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

                if (!digit)
                    return false;

                float exponentScale = 1f;
                if (i < end && (data[i] == (byte)'e' || data[i] == (byte)'E'))
                {
                    i++;
                    int exponentSign = 1;
                    if (i < end && (data[i] == (byte)'-' || data[i] == (byte)'+'))
                    {
                        exponentSign = data[i] == (byte)'-' ? -1 : 1;
                        i++;
                    }

                    int exponent = 0;
                    bool exponentDigit = false;
                    while (i < end && data[i] >= (byte)'0' && data[i] <= (byte)'9')
                    {
                        exponentDigit = true;
                        exponent = math.min(38, (exponent * 10) + (data[i] - (byte)'0'));
                        i++;
                    }

                    if (!exponentDigit)
                        return false;

                    exponentScale = ResolvePow10(exponentSign * exponent);
                }

                if (i != end)
                    return false;

                value = sign * (whole + fraction) * exponentScale;
                return math.isfinite(value);
            }

            private static float ResolvePow10(int signedExponent)
            {
                int steps = math.min(38, math.abs(signedExponent));
                float scale = 1f;
                float factor = signedExponent >= 0 ? 10f : 0.1f;
                for (int i = 0; i < steps; i++)
                    scale *= factor;

                return scale;
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
#endif
    }
}
