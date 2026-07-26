// ============================================================================
// HECTON-8 - GlobalWorldSampler.cs
// SHINOBU_41 unified MapMagic height + voxel SDF geological synthesis sampler.
// No Unity Physics, no collider dependency, no managed allocation in jobs.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core.Memory;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#endif

namespace Hecton8.World
{
    [Flags]
    public enum GlobalWorldSamplerConfigFlags : byte
    {
        None = 0,
        // Legacy ABI bit. Runtime quality is GlobalQualityWeight; this only reports survival sampling pressure.
        ForceSurvivalSamplingPressure = 1 << 0,
        ForceMathLodLow = ForceSurvivalSamplingPressure,
        EnableSmoothMin = 1 << 1,
        EnableMicroNoise = 1 << 2,
        EnableCeiling = 1 << 3,
        EnableCavernOverride = 1 << 4,
        EnableSdf = 1 << 5
    }

    [Flags]
    public enum GlobalWorldSamplerQueryFlags : byte
    {
        None = 0,
        EstimateNormal = 1 << 0
    }

    [Flags]
    public enum GlobalWorldSamplerResultFlags : byte
    {
        None = 0,
        HardFloor = 1 << 0,
        CaveSampled = 1 << 1,
        CavernOverride = 1 << 2,
        SurvivalSamplingPressure = 1 << 3,
        MathLodLow = SurvivalSamplingPressure,
        SmoothMin = 1 << 4,
        Ceiling = 1 << 5,
        InvalidInput = 1 << 6,
        NormalEstimated = 1 << 7
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GlobalWorldSamplerTelemetryEntry
    {
        // 64-byte black-box row. No custom packing: ARM64 reads stay naturally aligned.
        // 00 float3 LocalPosition, 12 float Distance, 16 uint Frame, 20 uint Hash.
        // 24 int SampleCount, 28 int Warning, 32 float3 Normal, 44 byte/byte/ushort,
        // 48..63 explicit int padding/reserve.
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Distance;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint QueryHash;
        [FieldOffset(24)] public int SampleCount;
        [FieldOffset(28)] public int WarningCode;
        [FieldOffset(32)] public float3 Normal;
        [FieldOffset(44)] public byte MaterialID;
        [FieldOffset(45)] public byte Flags;
        [FieldOffset(46)] public ushort SectorIndex;
        [FieldOffset(48)] public int Reserved0;
        [FieldOffset(52)] public int Reserved1;
        [FieldOffset(56)] public int Reserved2;
        [FieldOffset(60)] public int Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GlobalWorldSamplerCounterBlock
    {
        // One hot atomic counter per cache line. Value lives at offset 0; 60 bytes are reserved padding.
        [FieldOffset(0)] public int Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TerrainSampleDTO
    {
        // 32 bytes: one 16-byte hot lane plus biome hash and deterministic tail padding.
        [FieldOffset(0)] public float3 Normal;
        [FieldOffset(12)] public float Distance;
        [FieldOffset(16)] public uint BiomeHash;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MapMagicCellDTO
    {
        // 16 bytes. Height is sampled first; type/wetness stay byte-addressable for Burst lanes.
        [FieldOffset(0)] public float Height;
        [FieldOffset(4)] public short TerrainType;
        [FieldOffset(6)] public byte Wetness;
        [FieldOffset(7)] private byte _alignmentPad;
        [FieldOffset(8)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainPayloadHeaderDTO
    {
        // Cold OSHINO binary header mirror. Runtime buffers still hydrate into aligned NativeArrays.
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong PayloadBytes;
        [FieldOffset(16)] public uint Version;
        [FieldOffset(20)] public uint HeaderBytes;
        [FieldOffset(24)] public uint Width;
        [FieldOffset(28)] public uint Height;
        [FieldOffset(32)] public uint Depth;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float HeightScale;
        [FieldOffset(44)] public float SdfRange;
        [FieldOffset(48)] public uint Crc32;
        [FieldOffset(52)] public uint EndianTag;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerrainSampleResult
    {
        // 64-byte DTO. The hot normal+distance header is exactly one 16-byte lane.
        // 00 float3 Normal, 12 float Distance, 16 float3 LocalPosition, 28 float Height.
        // 32..47 distance scalars, 48 uint Hash, 52 ushort Sector, 54..55 bytes,
        // 56 int Revision, 60 uint BiomeHash.
        [FieldOffset(0)] public float3 Normal;
        [FieldOffset(12)] public float Distance;
        [FieldOffset(16)] public float3 LocalPosition;
        [FieldOffset(28)] public float Height;
        [FieldOffset(32)] public float Distance2D;
        [FieldOffset(36)] public float Distance3D;
        [FieldOffset(40)] public float SeaDistance;
        [FieldOffset(44)] public float GradientEpsilon;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public ushort SectorIndex;
        [FieldOffset(54)] public byte MaterialID;
        [FieldOffset(55)] public byte Flags;
        [FieldOffset(56)] public int SampleRevision;
        [FieldOffset(60)] public uint BiomeHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlobalWorldSamplerQuery
    {
        // 00 double3 AUP, 24 uint Frame, 28 byte Flags, 29 byte pad, 30 ushort pad.
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte Padding0;
        [FieldOffset(30)] public ushort Padding1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlobalWorldSamplerQualityState
    {
        // 32 bytes. Dispatcher-owned state for deterministic quality hysteresis.
        // 00 current, 04 target, 08 tick delta, 12 shed rate, 16 recover rate,
        // 20 frame, 24..31 padding.
        [FieldOffset(0)] public float CurrentWeight;
        [FieldOffset(4)] public float TargetWeight;
        [FieldOffset(8)] public float SimulationTickDelta;
        [FieldOffset(12)] public float ShedPerSecond;
        [FieldOffset(16)] public float RecoverPerSecond;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public struct GlobalWorldSamplerScalarData
    {
        [FieldOffset(0)] public double3 ActiveChunkOriginAup;
        [FieldOffset(24)] public double3 HeightOriginAup;
        [FieldOffset(48)] public double3 SdfOriginAup;
        [FieldOffset(72)] public float3 HeightSize;
        [FieldOffset(84)] public int HeightResolution;
        [FieldOffset(88)] public float3 SdfCellSize;
        [FieldOffset(100)] public int3 SdfDimensions;
        [FieldOffset(112)] public float SdfRange;
        [FieldOffset(116)] public float SeaLevel;
        [FieldOffset(120)] public float SeamSmoothMeters;
        [FieldOffset(124)] public float MicroNoiseAmplitude;
        [FieldOffset(128)] public float MicroNoiseFrequency;
        [FieldOffset(132)] public float GlobalQualityWeight;
        [FieldOffset(136)] public float BiomeBlendMeters;
        [FieldOffset(140)] public float ErosionFlattenStrength;
        [FieldOffset(144)] public float NormalEpsilon;
        [FieldOffset(148)] public float MaxLocalMeters;
        [FieldOffset(152)] public float3 ErosionNormalBias;
        [FieldOffset(164)] public float SectorSizeMeters;
        [FieldOffset(168)] public int SectorOriginX;
        [FieldOffset(172)] public int SectorOriginZ;
        [FieldOffset(176)] public int SectorCountX;
        [FieldOffset(180)] public int SectorCountZ;
        [FieldOffset(184)] public uint HeightAliasHash;
        [FieldOffset(188)] public uint SdfAliasHash;
        [FieldOffset(192)] public uint MaterialAliasHash;
        [FieldOffset(196)] public uint DefaultBiomeHash;
        [FieldOffset(200)] public int Revision;
        [FieldOffset(204)] public int Reserved0;
        [FieldOffset(208)] public byte ConfigFlags;
        [FieldOffset(209)] public byte DefaultMaterialId;
        [FieldOffset(210)] public byte HardFloorMaterialId;
        [FieldOffset(211)] public byte CaveMaterialFallback;
        [FieldOffset(212)] private uint _pad0;
        [FieldOffset(216)] private uint _pad1;
        [FieldOffset(220)] private uint _pad2;
    }

    public struct GlobalWorldSamplerData
    {
        // Unity NativeArray<T> handles are pointer-bearing 8-byte-aligned job handles.
        // Scalar payload after the handles is 184 bytes:
        // 00..71 double3 origins, 72..179 float/int config+hashes/reserve, 180..183 bytes.
        [ReadOnly] public NativeArray<ushort> HeightSamples;
        [ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [ReadOnly] public NativeArray<byte> EncodedSdf;
        [ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [ReadOnly] public NativeArray<uint> CaveSectorMask;
        [ReadOnly] public NativeArray<uint> BiomeAtlas;
        [ReadOnly] public NativeArray<byte> ErosionMask;
        [ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [ReadOnly] public NativeArray<long> ActiveSectorPointers;

        [NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;

        public double3 ActiveChunkOriginAup;
        public double3 HeightOriginAup;
        public double3 SdfOriginAup;

        public float3 HeightSize;
        public int HeightResolution;

        public float3 SdfCellSize;
        public int3 SdfDimensions;
        public float SdfRange;

        public float SeaLevel;
        public float SeamSmoothMeters;
        public float MicroNoiseAmplitude;
        public float MicroNoiseFrequency;
        public float GlobalQualityWeight;
        public float BiomeBlendMeters;
        public float ErosionFlattenStrength;
        public float NormalEpsilon;
        public float MaxLocalMeters;
        public float3 ErosionNormalBias;

        public float SectorSizeMeters;
        public int SectorOriginX;
        public int SectorOriginZ;
        public int SectorCountX;
        public int SectorCountZ;

        public uint HeightAliasHash;
        public uint SdfAliasHash;
        public uint MaterialAliasHash;
        public uint DefaultBiomeHash;
        public int Revision;
        public int Reserved0;

        public byte ConfigFlags;
        public byte DefaultMaterialId;
        public byte HardFloorMaterialId;
        public byte CaveMaterialFallback;
    }

    public static unsafe class GlobalWorldSampler
    {
        public const int TelemetryRingLength = 300;
        public const int ThroughputWarningThreshold = 800000;
        public const int WarningThroughputExceeded = 1;
        public const int WarningInvalidNumber = 2;
        public const int WarningOutOfBoundsOrUnloaded = 3;
        public const int WarningFrameHeartbeat = 0;
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_TERRAIN_SPLICER.bin";
        public const int CounterBlockSizeBytes = 64;
        public const int CounterBlockValueOffset = 0;
        public const int TerrainSampleDTOSizeBytes = 32;
        public const int TerrainSampleDTONormalOffset = 0;
        public const int TerrainSampleDTODistanceOffset = 12;
        public const int TerrainSampleDTOBiomeHashOffset = 16;
        public const int TerrainSampleDTOPaddingOffset = 20;
        public const int TerrainSampleDTOTailPaddingOffset = 24;
        public const int MapMagicCellDTOSizeBytes = 16;
        public const int MapMagicCellDTOHeightOffset = 0;
        public const int MapMagicCellDTOTerrainTypeOffset = 4;
        public const int MapMagicCellDTOWetnessOffset = 6;
        public const int MapMagicCellDTOAlignmentPadOffset = 7;
        public const int MapMagicCellDTOTailPaddingOffset = 8;
        public const int TerrainPayloadHeaderDTOSizeBytes = 64;
        public const int TerrainPayloadHeaderMagicOffset = 0;
        public const int TerrainPayloadHeaderPayloadBytesOffset = 8;
        public const int TerrainPayloadHeaderVersionOffset = 16;
        public const int TerrainPayloadHeaderHeaderBytesOffset = 20;
        public const int TerrainPayloadHeaderWidthOffset = 24;
        public const int TerrainPayloadHeaderHeightOffset = 28;
        public const int TerrainPayloadHeaderDepthOffset = 32;
        public const int TerrainPayloadHeaderFlagsOffset = 36;
        public const int TerrainPayloadHeaderHeightScaleOffset = 40;
        public const int TerrainPayloadHeaderSdfRangeOffset = 44;
        public const int TerrainPayloadHeaderCrc32Offset = 48;
        public const int TerrainPayloadHeaderEndianTagOffset = 52;
        public const int TerrainPayloadHeaderPad0Offset = 56;
        public const int TerrainPayloadHeaderPad1Offset = 60;
        public const int TerrainSampleResultSizeBytes = 64;
        public const int TerrainSampleResultNormalOffset = 0;
        public const int TerrainSampleResultDistanceOffset = 12;
        public const int TerrainSampleResultLocalPositionOffset = 16;
        public const int TerrainSampleResultHeightOffset = 28;
        public const int TerrainSampleResultDistance2DOffset = 32;
        public const int TerrainSampleResultDistance3DOffset = 36;
        public const int TerrainSampleResultSeaDistanceOffset = 40;
        public const int TerrainSampleResultGradientEpsilonOffset = 44;
        public const int TerrainSampleResultStateHashOffset = 48;
        public const int TerrainSampleResultSectorIndexOffset = 52;
        public const int TerrainSampleResultMaterialIdOffset = 54;
        public const int TerrainSampleResultFlagsOffset = 55;
        public const int TerrainSampleResultSampleRevisionOffset = 56;
        public const int TerrainSampleResultBiomeHashOffset = 60;
        public const int TerrainSampleResultReservedOffset = 60;
        public const int TelemetryEntrySizeBytes = 64;
        public const int TelemetryEntryLocalPositionOffset = 0;
        public const int TelemetryEntryDistanceOffset = 12;
        public const int TelemetryEntryFrameOffset = 16;
        public const int TelemetryEntryQueryHashOffset = 20;
        public const int TelemetryEntrySampleCountOffset = 24;
        public const int TelemetryEntryWarningCodeOffset = 28;
        public const int TelemetryEntryNormalOffset = 32;
        public const int TelemetryEntryMaterialIdOffset = 44;
        public const int TelemetryEntryFlagsOffset = 45;
        public const int TelemetryEntrySectorIndexOffset = 46;
        public const int TelemetryEntryReservedOffset = 48;
        public const int QuerySizeBytes = 32;
        public const int QueryAupOffset = 0;
        public const int QueryFrameOffset = 24;
        public const int QueryFlagsOffset = 28;
        public const int QueryPadding0Offset = 29;
        public const int QueryPadding1Offset = 30;
        public const int QualityStateSizeBytes = 32;
        public const int QualityStateCurrentWeightOffset = 0;
        public const int QualityStateTargetWeightOffset = 4;
        public const int QualityStateSimulationTickDeltaOffset = 8;
        public const int QualityStateShedPerSecondOffset = 12;
        public const int QualityStateRecoverPerSecondOffset = 16;
        public const int QualityStateFrameOffset = 20;
        public const int QualityStatePad0Offset = 24;
        public const int QualityStatePad1Offset = 28;
        public const int DataScalarPayloadBytes = 224;
        public const int CounterBlockCount = 8;
        public const int DefaultTerrainQueryBatchCount = 64;
        public const int DefaultGradientBatchCount = 64;
        public const int DefaultRaymarchBatchCount = 32;
        public const int DefaultQualityFilterBatchCount = 16;
        public const int LowQualityCadenceDivisor = 12;
        public const int TelemetryDumpVersion = 2;
        public const ulong TelemetryDumpMagic = 0x00384E4F54434548UL;
        public const ulong TerrainPayloadHeaderMagic = 0x004F454748385448UL;

        private const float QuantizedHeightScale = 1.0f / 65535.0f;
        private const float DefaultNormalEpsilon = 0.35f;
        private const float DefaultMaxLocalMeters = 32768f;
        public const float DefaultSeaLevel = 14.02f;
        private const float DefaultQualityWeight = 1.0f;
        private const float DefaultBiomeBlendMeters = 3.5f;
        private const float DefaultErosionFlattenStrength = 0.35f;
        private const float ExpensiveSamplingStartWeight = 0.30f;
        private const float InactiveDistanceSentinel = 1048576f;
        private const int EstimatedPolynomialSmoothMinNs = 8;
        private const int SampleCounterTotalIndex = 0;
        private const int SampleCounterTelemetryCursorIndex = 1;
        private const int SampleCounterDumpRequestIndex = 2;
        private const int SampleCounterDumpReasonIndex = 3;
        private const int SampleCounterOutOfBoundsIndex = 4;
        private const int SampleCounterSmoothMinCountIndex = 5;
        private const int SampleCounterSmoothMinNsEstimateIndex = 6;
        public const float MinimumRaymarchStep = 0.05f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GlobalWorldSamplerQuery Query(double3 aup, uint frame, GlobalWorldSamplerQueryFlags flags = GlobalWorldSamplerQueryFlags.None)
        {
            GlobalWorldSamplerQuery query;
            BuildQuery(aup, frame, flags, out query);
            return query;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BuildQuery(double3 aup, uint frame, GlobalWorldSamplerQueryFlags flags, out GlobalWorldSamplerQuery query)
        {
            query.Aup = aup;
            query.Frame = frame;
            query.Flags = (byte)flags;
            query.Padding0 = 0;
            query.Padding1 = 0;
        }

        public static void GetStructLayoutBytes(out int terrainResultBytes, out int telemetryEntryBytes, out int queryBytes, out int dataBytes)
        {
            terrainResultBytes = UnsafeUtility.SizeOf<TerrainSampleResult>();
            telemetryEntryBytes = UnsafeUtility.SizeOf<GlobalWorldSamplerTelemetryEntry>();
            queryBytes = UnsafeUtility.SizeOf<GlobalWorldSamplerQuery>();
            dataBytes = UnsafeUtility.SizeOf<GlobalWorldSamplerScalarData>();
        }

        public static void GetRuntimeDtoLayoutBytes(out int terrainDtoBytes, out int mapMagicCellBytes, out int terrainResultBytes, out int counterBlockBytes)
        {
            terrainDtoBytes = UnsafeUtility.SizeOf<TerrainSampleDTO>();
            mapMagicCellBytes = UnsafeUtility.SizeOf<MapMagicCellDTO>();
            terrainResultBytes = UnsafeUtility.SizeOf<TerrainSampleResult>();
            counterBlockBytes = UnsafeUtility.SizeOf<GlobalWorldSamplerCounterBlock>();
        }

        public static bool ValidateStructLayout()
        {
            return UnsafeUtility.SizeOf<TerrainSampleDTO>() == TerrainSampleDTOSizeBytes &&
                   UnsafeUtility.SizeOf<MapMagicCellDTO>() == MapMagicCellDTOSizeBytes &&
                   UnsafeUtility.SizeOf<TerrainPayloadHeaderDTO>() == TerrainPayloadHeaderDTOSizeBytes &&
                   UnsafeUtility.SizeOf<TerrainSampleResult>() == TerrainSampleResultSizeBytes &&
                   UnsafeUtility.SizeOf<GlobalWorldSamplerCounterBlock>() == CounterBlockSizeBytes &&
                   UnsafeUtility.SizeOf<GlobalWorldSamplerTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<GlobalWorldSamplerQualityState>() == QualityStateSizeBytes &&
                   UnsafeUtility.SizeOf<GlobalWorldSamplerScalarData>() == DataScalarPayloadBytes &&
                   UnsafeUtility.SizeOf<GlobalWorldSamplerQuery>() == QuerySizeBytes;
        }

        public static int GetPayloadHeaderLayoutBytes()
        {
            return UnsafeUtility.SizeOf<TerrainPayloadHeaderDTO>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TerrainSampleDTO GetSampleRef(NativeArray<TerrainSampleDTO> samples, int index)
        {
            return ref UnsafeUtility.AsRef<TerrainSampleDTO>((byte*)samples.GetUnsafePtr() + index * TerrainSampleDTOSizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToDTO(in TerrainSampleResult result, out TerrainSampleDTO dto)
        {
            dto.Normal = result.Normal;
            dto.Distance = result.Distance;
            dto.BiomeHash = result.BiomeHash;
            dto._pad0 = 0u;
            dto._pad1 = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 AupToChunkLocalFloat3(double3 aup, double3 activeChunkOriginAup)
        {
            double3 local = aup - activeChunkOriginAup;
            return Float3((float)local.x, (float)local.y, (float)local.z);
        }

        public static GlobalWorldSamplerData FromDataVaultAliases(
            NativeArray<ushort> heightSamples,
            NativeArray<byte> heightMaterialIds,
            NativeArray<byte> encodedSdf,
            NativeArray<byte> sdfMaterialIds,
            NativeArray<uint> caveSectorMask,
            NativeArray<int> sampleCounter,
            NativeArray<GlobalWorldSamplerTelemetryEntry> telemetryRing,
            double3 activeChunkOriginAup,
            double3 heightOriginAup,
            float3 heightSize,
            int heightResolution,
            double3 sdfOriginAup,
            float3 sdfCellSize,
            int3 sdfDimensions,
            float sdfRange,
            uint heightAliasHash,
            uint sdfAliasHash,
            uint materialAliasHash,
            int revision)
        {
            GlobalWorldSamplerData data = default;
            data.HeightSamples = heightSamples;
            data.HeightMaterialIds = heightMaterialIds;
            data.EncodedSdf = encodedSdf;
            data.SdfMaterialIds = sdfMaterialIds;
            data.CaveSectorMask = caveSectorMask;
            data.BiomeAtlas = default;
            data.ErosionMask = default;
            data.SdfOverrideMask = default;
            data.ActiveSectorPointers = default;
            data.SampleCounter = sampleCounter;
            data.CounterBlocks = default;
            data.TelemetryRing = telemetryRing;
            data.ActiveChunkOriginAup = activeChunkOriginAup;
            data.HeightOriginAup = heightOriginAup;
            data.SdfOriginAup = sdfOriginAup;
            data.HeightSize = heightSize;
            data.HeightResolution = heightResolution;
            data.SdfCellSize = sdfCellSize;
            data.SdfDimensions = sdfDimensions;
            data.SdfRange = math.max(sdfRange, 0.001f);
            data.SeaLevel = DefaultSeaLevel;
            data.SeamSmoothMeters = 1.25f;
            data.MicroNoiseAmplitude = 0f;
            data.MicroNoiseFrequency = 0.025f;
            data.GlobalQualityWeight = DefaultQualityWeight;
            data.BiomeBlendMeters = DefaultBiomeBlendMeters;
            data.ErosionFlattenStrength = DefaultErosionFlattenStrength;
            data.NormalEpsilon = DefaultNormalEpsilon;
            data.MaxLocalMeters = DefaultMaxLocalMeters;
            data.ErosionNormalBias = Float3(0f, 1f, 0f);
            data.SectorSizeMeters = 64f;
            data.SectorOriginX = 0;
            data.SectorOriginZ = 0;
            data.SectorCountX = 0;
            data.SectorCountZ = 0;
            data.HeightAliasHash = heightAliasHash;
            data.SdfAliasHash = sdfAliasHash;
            data.MaterialAliasHash = materialAliasHash;
            data.DefaultBiomeHash = materialAliasHash != 0u ? materialAliasHash : 0x42494F4Du;
            data.Revision = revision;
            data.Reserved0 = 0;
            data.ConfigFlags = (byte)(GlobalWorldSamplerConfigFlags.EnableSdf |
                                      GlobalWorldSamplerConfigFlags.EnableSmoothMin |
                                      GlobalWorldSamplerConfigFlags.EnableCavernOverride);
            data.DefaultMaterialId = 1;
            data.HardFloorMaterialId = 255;
            data.CaveMaterialFallback = 16;
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GlobalWorldSamplerScalarData ExtractScalarData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSamplerScalarData scalar = default;
            scalar.ActiveChunkOriginAup = data.ActiveChunkOriginAup;
            scalar.HeightOriginAup = data.HeightOriginAup;
            scalar.SdfOriginAup = data.SdfOriginAup;
            scalar.HeightSize = data.HeightSize;
            scalar.HeightResolution = data.HeightResolution;
            scalar.SdfCellSize = data.SdfCellSize;
            scalar.SdfDimensions = data.SdfDimensions;
            scalar.SdfRange = data.SdfRange;
            scalar.SeaLevel = data.SeaLevel;
            scalar.SeamSmoothMeters = data.SeamSmoothMeters;
            scalar.MicroNoiseAmplitude = data.MicroNoiseAmplitude;
            scalar.MicroNoiseFrequency = data.MicroNoiseFrequency;
            scalar.GlobalQualityWeight = data.GlobalQualityWeight;
            scalar.BiomeBlendMeters = data.BiomeBlendMeters;
            scalar.ErosionFlattenStrength = data.ErosionFlattenStrength;
            scalar.NormalEpsilon = data.NormalEpsilon;
            scalar.MaxLocalMeters = data.MaxLocalMeters;
            scalar.ErosionNormalBias = data.ErosionNormalBias;
            scalar.SectorSizeMeters = data.SectorSizeMeters;
            scalar.SectorOriginX = data.SectorOriginX;
            scalar.SectorOriginZ = data.SectorOriginZ;
            scalar.SectorCountX = data.SectorCountX;
            scalar.SectorCountZ = data.SectorCountZ;
            scalar.HeightAliasHash = data.HeightAliasHash;
            scalar.SdfAliasHash = data.SdfAliasHash;
            scalar.MaterialAliasHash = data.MaterialAliasHash;
            scalar.DefaultBiomeHash = data.DefaultBiomeHash;
            scalar.Revision = data.Revision;
            scalar.Reserved0 = data.Reserved0;
            scalar.ConfigFlags = data.ConfigFlags;
            scalar.DefaultMaterialId = data.DefaultMaterialId;
            scalar.HardFloorMaterialId = data.HardFloorMaterialId;
            scalar.CaveMaterialFallback = data.CaveMaterialFallback;
            return scalar;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyScalarData(ref GlobalWorldSamplerData data, in GlobalWorldSamplerScalarData scalar)
        {
            data.ActiveChunkOriginAup = scalar.ActiveChunkOriginAup;
            data.HeightOriginAup = scalar.HeightOriginAup;
            data.SdfOriginAup = scalar.SdfOriginAup;
            data.HeightSize = scalar.HeightSize;
            data.HeightResolution = scalar.HeightResolution;
            data.SdfCellSize = scalar.SdfCellSize;
            data.SdfDimensions = scalar.SdfDimensions;
            data.SdfRange = scalar.SdfRange;
            data.SeaLevel = scalar.SeaLevel;
            data.SeamSmoothMeters = scalar.SeamSmoothMeters;
            data.MicroNoiseAmplitude = scalar.MicroNoiseAmplitude;
            data.MicroNoiseFrequency = scalar.MicroNoiseFrequency;
            data.GlobalQualityWeight = scalar.GlobalQualityWeight;
            data.BiomeBlendMeters = scalar.BiomeBlendMeters;
            data.ErosionFlattenStrength = scalar.ErosionFlattenStrength;
            data.NormalEpsilon = scalar.NormalEpsilon;
            data.MaxLocalMeters = scalar.MaxLocalMeters;
            data.ErosionNormalBias = scalar.ErosionNormalBias;
            data.SectorSizeMeters = scalar.SectorSizeMeters;
            data.SectorOriginX = scalar.SectorOriginX;
            data.SectorOriginZ = scalar.SectorOriginZ;
            data.SectorCountX = scalar.SectorCountX;
            data.SectorCountZ = scalar.SectorCountZ;
            data.HeightAliasHash = scalar.HeightAliasHash;
            data.SdfAliasHash = scalar.SdfAliasHash;
            data.MaterialAliasHash = scalar.MaterialAliasHash;
            data.DefaultBiomeHash = scalar.DefaultBiomeHash;
            data.Revision = scalar.Revision;
            data.Reserved0 = scalar.Reserved0;
            data.ConfigFlags = scalar.ConfigFlags;
            data.DefaultMaterialId = scalar.DefaultMaterialId;
            data.HardFloorMaterialId = scalar.HardFloorMaterialId;
            data.CaveMaterialFallback = scalar.CaveMaterialFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GlobalWorldSamplerData FromJobAliases(
            NativeArray<ushort> heightSamples,
            NativeArray<byte> heightMaterialIds,
            NativeArray<byte> encodedSdf,
            NativeArray<byte> sdfMaterialIds,
            NativeArray<uint> caveSectorMask,
            NativeArray<uint> biomeAtlas,
            NativeArray<byte> erosionMask,
            NativeArray<uint> sdfOverrideMask,
            NativeArray<long> activeSectorPointers,
            NativeArray<int> sampleCounter,
            NativeArray<GlobalWorldSamplerCounterBlock> counterBlocks,
            NativeArray<GlobalWorldSamplerTelemetryEntry> telemetryRing,
            GlobalWorldSamplerScalarData scalar)
        {
            GlobalWorldSamplerData data = default;
            data.HeightSamples = heightSamples;
            data.HeightMaterialIds = heightMaterialIds;
            data.EncodedSdf = encodedSdf;
            data.SdfMaterialIds = sdfMaterialIds;
            data.CaveSectorMask = caveSectorMask;
            data.BiomeAtlas = biomeAtlas;
            data.ErosionMask = erosionMask;
            data.SdfOverrideMask = sdfOverrideMask;
            data.ActiveSectorPointers = activeSectorPointers;
            data.SampleCounter = sampleCounter;
            data.CounterBlocks = counterBlocks;
            data.TelemetryRing = telemetryRing;
            ApplyScalarData(ref data, in scalar);
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExtractJobAliases(
            in GlobalWorldSamplerData data,
            out NativeArray<ushort> heightSamples,
            out NativeArray<byte> heightMaterialIds,
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> sdfMaterialIds,
            out NativeArray<uint> caveSectorMask,
            out NativeArray<uint> biomeAtlas,
            out NativeArray<byte> erosionMask,
            out NativeArray<uint> sdfOverrideMask,
            out NativeArray<long> activeSectorPointers,
            out NativeArray<int> sampleCounter,
            out NativeArray<GlobalWorldSamplerCounterBlock> counterBlocks,
            out NativeArray<GlobalWorldSamplerTelemetryEntry> telemetryRing,
            out GlobalWorldSamplerScalarData scalar)
        {
            heightSamples = data.HeightSamples;
            heightMaterialIds = data.HeightMaterialIds;
            encodedSdf = data.EncodedSdf;
            sdfMaterialIds = data.SdfMaterialIds;
            caveSectorMask = data.CaveSectorMask;
            biomeAtlas = data.BiomeAtlas;
            erosionMask = data.ErosionMask;
            sdfOverrideMask = data.SdfOverrideMask;
            activeSectorPointers = data.ActiveSectorPointers;
            sampleCounter = data.SampleCounter;
            counterBlocks = data.CounterBlocks;
            telemetryRing = data.TelemetryRing;
            scalar = ExtractScalarData(in data);
        }

        public static JobHandle ScheduleBatchSampler(
            in GlobalWorldSamplerData data,
            NativeArray<double3> positionsAup,
            NativeArray<TerrainSampleResult> results,
            uint frame,
            byte estimateNormals,
            JobHandle inputDeps,
            int innerLoopBatchCount = DefaultTerrainQueryBatchCount)
        {
            int count = ResolveScheduleCount(positionsAup.IsCreated ? positionsAup.Length : 0, results.IsCreated ? results.Length : 0);
            if (count <= 0)
            {
                return inputDeps;
            }

            BatchSamplerJob job = default;
            job.SetData(in data);
            job.PositionsAup = positionsAup;
            job.Results = results;
            job.Frame = frame;
            job.EstimateNormals = estimateNormals;
            job.Padding0 = 0;
            job.Padding1 = 0;
            return job.Schedule(count, ResolveBatchCount(innerLoopBatchCount, DefaultTerrainQueryBatchCount), inputDeps);
        }

        public static JobHandle ScheduleLocalBatchSampler(
            in GlobalWorldSamplerData data,
            NativeArray<float3> positionsLocal,
            NativeArray<TerrainSampleResult> results,
            uint frame,
            byte estimateNormals,
            JobHandle inputDeps,
            int innerLoopBatchCount = DefaultTerrainQueryBatchCount)
        {
            int count = ResolveScheduleCount(positionsLocal.IsCreated ? positionsLocal.Length : 0, results.IsCreated ? results.Length : 0);
            if (count <= 0)
            {
                return inputDeps;
            }

            BatchLocalSamplerJob job = default;
            job.SetData(in data);
            job.PositionsLocal = positionsLocal;
            job.Results = results;
            job.Frame = frame;
            job.EstimateNormals = estimateNormals;
            job.Padding0 = 0;
            job.Padding1 = 0;
            return job.Schedule(count, ResolveBatchCount(innerLoopBatchCount, DefaultTerrainQueryBatchCount), inputDeps);
        }

        public static JobHandle ScheduleGradientNormals(
            in GlobalWorldSamplerData data,
            NativeArray<double3> positionsAup,
            NativeArray<TerrainSampleResult> results,
            uint frame,
            JobHandle inputDeps,
            int minIndicesPerJobCount = DefaultGradientBatchCount)
        {
            int count = ResolveScheduleCount(positionsAup.IsCreated ? positionsAup.Length : 0, results.IsCreated ? results.Length : 0);
            if (count <= 0)
            {
                return inputDeps;
            }

            GradientNormalEstimationBatchJob job = default;
            job.SetData(in data);
            job.PositionsAup = positionsAup;
            job.Results = results;
            job.Frame = frame;
            return job.ScheduleBatch(count, ResolveBatchCount(minIndicesPerJobCount, DefaultGradientBatchCount), inputDeps);
        }

        public static JobHandle ScheduleMockTerrainStress(
            in GlobalWorldSamplerData data,
            NativeArray<MockTerrainQuerySignal> signals,
            NativeArray<TerrainSampleDTO> results,
            double3 originAup,
            float3 extentsMeters,
            uint frame,
            uint seed,
            JobHandle inputDeps,
            int innerLoopBatchCount = DefaultTerrainQueryBatchCount)
        {
            int count = results.IsCreated ? results.Length : 0;
            if (count <= 0)
            {
                return inputDeps;
            }

            MockTerrainQueryStressJob job = default;
            job.SetData(in data);
            job.Signals = signals;
            job.Results = results;
            job.OriginAup = originAup;
            job.ExtentsMeters = extentsMeters;
            job.Frame = frame;
            job.Seed = seed;
            return job.Schedule(count, ResolveBatchCount(innerLoopBatchCount, DefaultTerrainQueryBatchCount), inputDeps);
        }

        public static JobHandle ScheduleMockRaymarch(
            in GlobalWorldSamplerData data,
            NativeArray<float3> rayDirectionsLocal,
            NativeArray<TerrainSampleResult> hits,
            double3 originAup,
            uint frame,
            float maxDistance,
            float maxStepMeters,
            int maxSteps,
            JobHandle inputDeps,
            int innerLoopBatchCount = DefaultRaymarchBatchCount)
        {
            int count = hits.IsCreated ? hits.Length : 0;
            if (count <= 0)
            {
                return inputDeps;
            }

            MockBoidRaymarchJob job = default;
            job.SetData(in data);
            job.RayDirectionsLocal = rayDirectionsLocal;
            job.Hits = hits;
            job.OriginAup = originAup;
            job.Frame = frame;
            job.MaxDistance = maxDistance;
            job.MaxStepMeters = maxStepMeters;
            job.MaxSteps = maxSteps;
            return job.Schedule(count, ResolveBatchCount(innerLoopBatchCount, DefaultRaymarchBatchCount), inputDeps);
        }

        public static JobHandle ScheduleQualityWeightFilter(
            NativeArray<GlobalWorldSamplerQualityState> qualityStates,
            JobHandle inputDeps,
            int innerLoopBatchCount = DefaultQualityFilterBatchCount)
        {
            if (!qualityStates.IsCreated || qualityStates.Length == 0)
            {
                return inputDeps;
            }

            QualityWeightFilterJob job;
            job.QualityStates = qualityStates;
            return job.Schedule(qualityStates.Length, ResolveBatchCount(innerLoopBatchCount, DefaultQualityFilterBatchCount), inputDeps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSamplingCadenceDivisor(float qualityWeight)
        {
            float quality = IsFinite(qualityWeight) ? math.saturate(qualityWeight) : DefaultQualityWeight;
            float qualityCurve = quality * quality * (3f - (2f * quality));
            float cadence = math.lerp((float)LowQualityCadenceDivisor, 1f, qualityCurve);
            return math.clamp((int)math.floor(cadence + 0.5f), 1, LowQualityCadenceDivisor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSampleOnFrame(uint frame, float qualityWeight)
        {
            int cadenceDivisor = ResolveSamplingCadenceDivisor(qualityWeight);
            return cadenceDivisor <= 1 || frame % (uint)cadenceDivisor == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FilterGlobalQualityWeight(float previousWeight, float targetWeight, float simulationTickDelta, float shedPerSecond = 4f, float recoverPerSecond = 1f)
        {
            float previous = IsFinite(previousWeight) ? math.saturate(previousWeight) : DefaultQualityWeight;
            float target = IsFinite(targetWeight) ? math.saturate(targetWeight) : previous;
            float dt = IsFinite(simulationTickDelta) ? math.max(simulationTickDelta, 0f) : 0f;
            float shedRate = SafePositive(shedPerSecond, 4f);
            float recoverRate = SafePositive(recoverPerSecond, 1f);
            float shedding = math.step(target, previous);
            float rate = math.lerp(recoverRate, shedRate, shedding);
            float delta = target - previous;
            float maxDelta = rate * dt;
            return math.saturate(previous + math.clamp(delta, -maxDelta, maxDelta));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BuildQualityState(
            float currentWeight,
            float targetWeight,
            float simulationTickDelta,
            uint frame,
            out GlobalWorldSamplerQualityState state,
            float shedPerSecond = 4f,
            float recoverPerSecond = 1f)
        {
            state.CurrentWeight = IsFinite(currentWeight) ? math.saturate(currentWeight) : DefaultQualityWeight;
            state.TargetWeight = IsFinite(targetWeight) ? math.saturate(targetWeight) : state.CurrentWeight;
            state.SimulationTickDelta = IsFinite(simulationTickDelta) ? math.max(simulationTickDelta, 0f) : 0f;
            state.ShedPerSecond = SafePositive(shedPerSecond, 4f);
            state.RecoverPerSecond = SafePositive(recoverPerSecond, 1f);
            state.Frame = frame;
            state._pad0 = 0u;
            state._pad1 = 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FilterQualityState(ref GlobalWorldSamplerQualityState state)
        {
            state.CurrentWeight = FilterGlobalQualityWeight(
                state.CurrentWeight,
                state.TargetWeight,
                state.SimulationTickDelta,
                state.ShedPerSecond,
                state.RecoverPerSecond);
            state.TargetWeight = IsFinite(state.TargetWeight) ? math.saturate(state.TargetWeight) : state.CurrentWeight;
            state.SimulationTickDelta = IsFinite(state.SimulationTickDelta) ? math.max(state.SimulationTickDelta, 0f) : 0f;
            state.ShedPerSecond = SafePositive(state.ShedPerSecond, 4f);
            state.RecoverPerSecond = SafePositive(state.RecoverPerSecond, 1f);
            state._pad0 = 0u;
            state._pad1 = 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyQualityState(ref GlobalWorldSamplerData data, in GlobalWorldSamplerQualityState state)
        {
            data.GlobalQualityWeight = IsFinite(state.CurrentWeight) ? math.saturate(state.CurrentWeight) : DefaultQualityWeight;
        }

        public static void ResetFrameTelemetryCounters(in GlobalWorldSamplerData data)
        {
            ResetCounter(data, SampleCounterTotalIndex);
            ResetCounter(data, SampleCounterOutOfBoundsIndex);
            ResetCounter(data, SampleCounterSmoothMinCountIndex);
            ResetCounter(data, SampleCounterSmoothMinNsEstimateIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sample(in GlobalWorldSamplerData data, in GlobalWorldSamplerQuery query, out TerrainSampleResult result)
        {
            SampleDistanceOnly(data, query, out result);

            if ((query.Flags & (byte)GlobalWorldSamplerQueryFlags.EstimateNormal) != 0 &&
                (result.Flags & (byte)GlobalWorldSamplerResultFlags.HardFloor) == 0)
            {
                EstimateNormal(data, query, ref result);
            }

            SanitizeResult(ref result, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SampleDistanceOnly(in GlobalWorldSamplerData data, in GlobalWorldSamplerQuery query, out TerrainSampleResult result)
        {
            float3 localPosition = AupToChunkLocalFloat3(query.Aup, data.ActiveChunkOriginAup);
            float qualityWeight = ResolveQualityWeight(data);
            if (!IsFinite(query.Aup) ||
                !IsFinite(data.ActiveChunkOriginAup) ||
                !IsSafeLocalPosition(localPosition, data.MaxLocalMeters))
            {
                IncrementOutOfBounds(data);
                BuildHardFloorResult(data, localPosition, query.Frame, (byte)GlobalWorldSamplerResultFlags.InvalidInput, out result);
                SanitizeResult(ref result, data);
                WriteTelemetryWarning(data, WarningInvalidNumber, query.Frame, localPosition, result);
                RequestTelemetryDump(data, WarningInvalidNumber);
                return;
            }

            if (!TryResolveActiveSector(data, query.Aup, out ushort sectorIndex))
            {
                IncrementOutOfBounds(data);
                BuildHardFloorResult(data, localPosition, query.Frame, 0, out result);
                result.SectorIndex = sectorIndex;
                result.StateHash = BuildStateHash(localPosition, result.Distance, data.Revision, sectorIndex);
                SanitizeResult(ref result, data);
                WriteTelemetryWarning(data, WarningOutOfBoundsOrUnloaded, query.Frame, localPosition, result);
                return;
            }

            if (!TrySampleHeight(data, query.Aup, qualityWeight, out float terrainHeightLocal, out float terrainDistance, out byte heightMaterial, out uint biomeHash))
            {
                IncrementOutOfBounds(data);
                BuildHardFloorResult(data, localPosition, query.Frame, 0, out result);
                result.SectorIndex = sectorIndex;
                result.StateHash = BuildStateHash(localPosition, result.Distance, data.Revision, sectorIndex);
                SanitizeResult(ref result, data);
                return;
            }

            byte resultFlags = 0;
            float finalDistance = terrainDistance;
            float sdfDistance = InactiveDistanceSentinel;
            byte materialId = heightMaterial;

            bool sdfEnabled = (data.ConfigFlags & (byte)GlobalWorldSamplerConfigFlags.EnableSdf) != 0;
            bool caveSectorActive = sdfEnabled && HasCaveSector(data, query.Aup, out sectorIndex);

            if (caveSectorActive && TrySampleSdf(data, query.Aup, qualityWeight, out sdfDistance, out byte sdfMaterial))
            {
                resultFlags |= (byte)GlobalWorldSamplerResultFlags.CaveSampled;

                bool cavernOverride = (data.ConfigFlags & (byte)GlobalWorldSamplerConfigFlags.EnableCavernOverride) != 0 &&
                                      HasSdfOverride(data, sectorIndex) &&
                                      sdfDistance < 0f &&
                                      localPosition.y < terrainHeightLocal;

                if (cavernOverride)
                {
                    finalDistance = sdfDistance;
                    materialId = sdfMaterial;
                    resultFlags |= (byte)GlobalWorldSamplerResultFlags.CavernOverride;
                }
                else
                {
                    bool smooth = (data.ConfigFlags & (byte)GlobalWorldSamplerConfigFlags.EnableSmoothMin) != 0 &&
                                  data.SeamSmoothMeters > 0.0001f;
                    finalDistance = smooth
                        ? SmoothMin(terrainDistance, sdfDistance, data.SeamSmoothMeters)
                        : math.min(terrainDistance, sdfDistance);

                    if (smooth)
                    {
                        RecordSmoothMinEstimate(data);
                        resultFlags |= (byte)GlobalWorldSamplerResultFlags.SmoothMin;
                    }

                    if (sdfDistance <= terrainDistance)
                    {
                        materialId = sdfMaterial;
                    }
                }
            }

            float seaDistance = InactiveDistanceSentinel;
            if ((data.ConfigFlags & (byte)GlobalWorldSamplerConfigFlags.EnableCeiling) != 0)
            {
                float seaLocalY = (float)(data.SeaLevel - data.ActiveChunkOriginAup.y);
                seaDistance = seaLocalY - localPosition.y;
                if (localPosition.y > seaLocalY)
                {
                    finalDistance = math.min(finalDistance, seaDistance);
                    resultFlags |= (byte)GlobalWorldSamplerResultFlags.Ceiling;
                }
            }

            result.Normal = Float3(0f, 1f, 0f);
            result.Distance = finalDistance;
            result.LocalPosition = localPosition;
            result.Height = terrainHeightLocal;
            result.Distance2D = terrainDistance;
            result.Distance3D = sdfDistance;
            result.SeaDistance = seaDistance;
            result.GradientEpsilon = SafePositive(data.NormalEpsilon, 0.001f);
            result.StateHash = BuildStateHash(localPosition, finalDistance, data.Revision, sectorIndex);
            result.SectorIndex = sectorIndex;
            result.MaterialID = materialId;
            result.Flags = resultFlags;
            result.SampleRevision = data.Revision;
            result.BiomeHash = biomeHash;
            SanitizeResult(ref result, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EstimateNormal(in GlobalWorldSamplerData data, in GlobalWorldSamplerQuery query, ref TerrainSampleResult result)
        {
            float qualityWeight = ResolveQualityWeight(data);
            float expensiveWeight = ResolveExpensiveSamplingWeight(qualityWeight);
            if (expensiveWeight <= 0.0001f)
            {
                result.Normal = Float3(0f, 1f, 0f);
                result.Flags |= (byte)GlobalWorldSamplerResultFlags.NormalEstimated;
                return;
            }

            float epsilon = SafePositive(result.GradientEpsilon, 0.001f);
            double e = epsilon;

            TerrainSampleResult d0;
            TerrainSampleResult d1;
            TerrainSampleResult d2;
            TerrainSampleResult d3;

            var q0 = query;
            var q1 = query;
            var q2 = query;
            var q3 = query;
            q0.Flags = 0;
            q1.Flags = 0;
            q2.Flags = 0;
            q3.Flags = 0;

            q0.Aup += Double3(e, -e, -e);
            q1.Aup += Double3(-e, -e, e);
            q2.Aup += Double3(-e, e, -e);
            q3.Aup += Double3(e, e, e);

            SampleDistanceOnly(data, q0, out d0);
            SampleDistanceOnly(data, q1, out d1);
            SampleDistanceOnly(data, q2, out d2);
            SampleDistanceOnly(data, q3, out d3);

            float3 normal =
                Float3(1f, -1f, -1f) * d0.Distance +
                Float3(-1f, -1f, 1f) * d1.Distance +
                Float3(-1f, 1f, -1f) * d2.Distance +
                Float3(1f, 1f, 1f) * d3.Distance;

            float3 tetraNormal = NormalizeSafe(normal, Float3(0f, 1f, 0f));
            result.Normal = expensiveWeight < 0.9999f
                ? NormalizeSafe(math.lerp(Float3(0f, 1f, 0f), tetraNormal, expensiveWeight), Float3(0f, 1f, 0f))
                : tetraNormal;

            float erosion01 = SampleErosionAtAup(data, query.Aup);
            if (erosion01 > 0.0001f && data.ErosionFlattenStrength > 0f && IsFinite(data.ErosionNormalBias))
            {
                float3 biased = result.Normal + data.ErosionNormalBias * (erosion01 * data.ErosionFlattenStrength * qualityWeight);
                result.Normal = NormalizeSafe(biased, result.Normal);
            }

            result.Flags |= (byte)GlobalWorldSamplerResultFlags.NormalEstimated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SanitizeResult(ref TerrainSampleResult result, in GlobalWorldSamplerData data)
        {
            result.LocalPosition = IsFinite(result.LocalPosition) ? result.LocalPosition : float3.zero;
            result.Normal = NormalizeSafe(result.Normal, Float3(0f, 1f, 0f));
            result.Distance = ClampFiniteDistance(result.Distance, 0f);
            result.Height = ClampFiniteDistance(result.Height, result.LocalPosition.y);
            result.Distance2D = ClampFiniteDistance(result.Distance2D, result.Distance);
            result.Distance3D = ClampFiniteDistance(result.Distance3D, InactiveDistanceSentinel);
            result.SeaDistance = ClampFiniteDistance(result.SeaDistance, InactiveDistanceSentinel);
            result.GradientEpsilon = SafePositive(result.GradientEpsilon, SafePositive(data.NormalEpsilon, 0.001f));
            result.StateHash = BuildStateHash(result.LocalPosition, result.Distance, result.SampleRevision, result.SectorIndex);
            result.BiomeHash = result.BiomeHash != 0u ? result.BiomeHash : ResolveDefaultBiomeHash(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothMin(float a, float b, float k)
        {
            a = IsFinite(a) ? a : 0f;
            b = IsFinite(b) ? b : 0f;
            float safeK = SafePositive(k, 0.0001f);
            float h = math.saturate(0.5f + 0.5f * (b - a) / safeK);
            return math.lerp(b, a, h) - safeK * h * (1f - h);
        }

        public static void MockTerrainGenerator(ref GlobalWorldSamplerData data, float sineFrequency, float caveRadius, float caveDepth)
        {
            sineFrequency = math.max(sineFrequency, 0.0001f);
            caveRadius = math.max(caveRadius, 0.25f);

            int resolution = data.HeightResolution;
            if (data.HeightSamples.IsCreated &&
                TryGetHeightSampleCount(resolution, out int heightSampleCount) &&
                data.HeightSamples.Length >= heightSampleCount)
            {
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float u = resolution > 1 ? (float)x / (resolution - 1) : 0f;
                        float v = resolution > 1 ? (float)z / (resolution - 1) : 0f;
                        float wave = 0.5f + 0.25f * Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((u + v) * sineFrequency * math.PI * 2f);
                        int index = x + z * resolution;
                        data.HeightSamples[index] = (ushort)math.clamp((int)math.round(wave * 65535f), 0, 65535);

                        if (data.HeightMaterialIds.IsCreated && index < data.HeightMaterialIds.Length)
                        {
                            data.HeightMaterialIds[index] = (byte)(wave > 0.56f ? 4 : 2);
                        }

                        if (data.BiomeAtlas.IsCreated && index < data.BiomeAtlas.Length)
                        {
                            uint biomeSeed = wave > 0.56f ? 0x42494F34u : 0x42494F32u;
                            data.BiomeAtlas[index] = HashBiomeCell(biomeSeed, x, z);
                        }

                        if (data.ErosionMask.IsCreated && index < data.ErosionMask.Length)
                        {
                            float ridge = math.abs(Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((u - v) * sineFrequency * math.PI * 2f));
                            data.ErosionMask[index] = (byte)math.clamp((int)math.round(ridge * 255f), 0, 255);
                        }
                    }
                }
            }

            if (data.EncodedSdf.IsCreated &&
                TryGetVoxelCount(data.SdfDimensions, out int voxelCount) &&
                IsFinite(data.SdfCellSize) &&
                data.SdfCellSize.x > 0f &&
                data.SdfCellSize.y > 0f &&
                data.SdfCellSize.z > 0f &&
                data.EncodedSdf.Length >= voxelCount)
            {
                float3 volumeSize = Float3(
                    data.SdfCellSize.x * math.max(data.SdfDimensions.x - 1, 1),
                    data.SdfCellSize.y * math.max(data.SdfDimensions.y - 1, 1),
                    data.SdfCellSize.z * math.max(data.SdfDimensions.z - 1, 1));

                float3 center = Float3(volumeSize.x * 0.5f, volumeSize.y * 0.5f - caveDepth, volumeSize.z * 0.5f);

                for (int z = 0; z < data.SdfDimensions.z; z++)
                {
                    for (int y = 0; y < data.SdfDimensions.y; y++)
                    {
                        for (int x = 0; x < data.SdfDimensions.x; x++)
                        {
                            int index = SdfIndex(x, y, z, data.SdfDimensions);
                            float3 p = Float3(x * data.SdfCellSize.x, y * data.SdfCellSize.y, z * data.SdfCellSize.z);
                            float d = math.length(p - center) - caveRadius;
                            data.EncodedSdf[index] = EncodeSdf(d, data.SdfRange);

                            if (data.SdfMaterialIds.IsCreated && index < data.SdfMaterialIds.Length)
                            {
                                data.SdfMaterialIds[index] = (byte)(d < 0f ? 18 : 8);
                            }
                        }
                    }
                }
            }

            if (data.CaveSectorMask.IsCreated)
            {
                for (int i = 0; i < data.CaveSectorMask.Length; i++)
                {
                    data.CaveSectorMask[i] = uint.MaxValue;
                }
            }

            if (data.SdfOverrideMask.IsCreated)
            {
                for (int i = 0; i < data.SdfOverrideMask.Length; i++)
                {
                    data.SdfOverrideMask[i] = uint.MaxValue;
                }
            }

            if (data.ActiveSectorPointers.IsCreated)
            {
                for (int i = 0; i < data.ActiveSectorPointers.Length; i++)
                {
                    data.ActiveSectorPointers[i] = 1L + i;
                }
            }
        }

        public static void MockGeologyGenerator(ref GlobalWorldSamplerData data, float sineFrequency, float caveRadius, float caveDepth)
        {
            MockTerrainGenerator(ref data, sineFrequency, caveRadius, caveDepth);
        }

        public static bool TryDumpTelemetryBuffer(NativeArray<GlobalWorldSamplerTelemetryEntry> telemetryRing, string dumpPath = DefaultDumpPath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0 || string.IsNullOrEmpty(dumpPath))
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                int headerBytes = 20;
                int rowBytes = UnsafeUtility.SizeOf<GlobalWorldSamplerTelemetryEntry>();
                int totalBytes = headerBytes + telemetryRing.Length * rowBytes;
                payload = Hecton8.Core.NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(GlobalWorldSampler),
                    "GlobalWorldSamplerTelemetryDumpPayload");
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteUInt64LittleEndian(bytes, 0, TelemetryDumpMagic);
                WriteInt32LittleEndian(bytes, 8, TelemetryDumpVersion);
                WriteInt32LittleEndian(bytes, 12, telemetryRing.Length);
                WriteInt32LittleEndian(bytes, 16, rowBytes);
                int writeOffset = headerBytes;
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    GlobalWorldSamplerTelemetryEntry entry = telemetryRing[i];
                    WriteTelemetryEntryLittleEndian(bytes.Slice(writeOffset, rowBytes), in entry);
                    writeOffset += rowBytes;
                }

                return Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, totalBytes);
            }
            catch
            {
                return false;
            }
            finally
            {
                Hecton8.Core.NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(GlobalWorldSampler),
                    "GlobalWorldSamplerTelemetryDumpPayload");
            }
        }

        private static void WriteTelemetryEntryLittleEndian(Span<byte> destination, in GlobalWorldSamplerTelemetryEntry entry)
        {
            WriteFloat3LittleEndian(destination, 0, entry.LocalPosition);
            WriteSingleLittleEndian(destination, 12, entry.Distance);
            WriteUInt32LittleEndian(destination, 16, entry.Frame);
            WriteUInt32LittleEndian(destination, 20, entry.QueryHash);
            WriteInt32LittleEndian(destination, 24, entry.SampleCount);
            WriteInt32LittleEndian(destination, 28, entry.WarningCode);
            WriteFloat3LittleEndian(destination, 32, entry.Normal);
            destination[44] = entry.MaterialID;
            destination[45] = entry.Flags;
            WriteUInt16LittleEndian(destination, 46, entry.SectorIndex);
            WriteInt32LittleEndian(destination, 48, entry.Reserved0);
            WriteInt32LittleEndian(destination, 52, entry.Reserved1);
            WriteInt32LittleEndian(destination, 56, entry.Reserved2);
            WriteInt32LittleEndian(destination, 60, entry.Reserved3);
        }

        private static void WriteFloat3LittleEndian(Span<byte> destination, int offset, float3 value)
        {
            WriteSingleLittleEndian(destination, offset, value.x);
            WriteSingleLittleEndian(destination, offset + 4, value.y);
            WriteSingleLittleEndian(destination, offset + 8, value.z);
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt16LittleEndian(Span<byte> destination, int offset, ushort value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
        }

        private static void WriteUInt64LittleEndian(Span<byte> destination, int offset, ulong value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
        }

        public static bool TryReadTerrainPayloadHeader(ReadOnlySpan<byte> source, byte sourceBigEndian, out TerrainPayloadHeaderDTO header)
        {
            header = default;
            if (source.Length < TerrainPayloadHeaderDTOSizeBytes)
            {
                return false;
            }

            bool bigEndian = sourceBigEndian != 0;
            header.Magic = ReadU64Endian(source, TerrainPayloadHeaderMagicOffset, bigEndian);
            header.PayloadBytes = ReadU64Endian(source, TerrainPayloadHeaderPayloadBytesOffset, bigEndian);
            header.Version = ReadU32Endian(source, TerrainPayloadHeaderVersionOffset, bigEndian);
            header.HeaderBytes = ReadU32Endian(source, TerrainPayloadHeaderHeaderBytesOffset, bigEndian);
            header.Width = ReadU32Endian(source, TerrainPayloadHeaderWidthOffset, bigEndian);
            header.Height = ReadU32Endian(source, TerrainPayloadHeaderHeightOffset, bigEndian);
            header.Depth = ReadU32Endian(source, TerrainPayloadHeaderDepthOffset, bigEndian);
            header.Flags = ReadU32Endian(source, TerrainPayloadHeaderFlagsOffset, bigEndian);
            header.HeightScale = math.asfloat(ReadU32Endian(source, TerrainPayloadHeaderHeightScaleOffset, bigEndian));
            header.SdfRange = math.asfloat(ReadU32Endian(source, TerrainPayloadHeaderSdfRangeOffset, bigEndian));
            header.Crc32 = ReadU32Endian(source, TerrainPayloadHeaderCrc32Offset, bigEndian);
            header.EndianTag = ReadU32Endian(source, TerrainPayloadHeaderEndianTagOffset, bigEndian);
            header._pad0 = 0u;
            header._pad1 = 0u;

            return header.Magic == TerrainPayloadHeaderMagic &&
                   header.HeaderBytes >= TerrainPayloadHeaderDTOSizeBytes &&
                   header.Width > 0u &&
                   header.Height > 0u &&
                   IsFinite(header.HeightScale) &&
                   IsFinite(header.SdfRange);
        }

        public static bool TryFlushRequestedTelemetryDump(in GlobalWorldSamplerData data, string dumpPath = DefaultDumpPath)
        {
            if (!data.TelemetryRing.IsCreated ||
                !TryGetCounterPointer(data, SampleCounterDumpRequestIndex, out int* requestCounter))
            {
                return false;
            }

            int request = Interlocked.Exchange(ref requestCounter[0], 0);
            return request != 0 && TryDumpTelemetryBuffer(data.TelemetryRing, dumpPath);
        }

        internal static int AddSampleCount(in GlobalWorldSamplerData data, int amount)
        {
            if (amount <= 0)
            {
                return ReadCounter(data, SampleCounterTotalIndex);
            }

            if (!TryGetCounterPointer(data, SampleCounterTotalIndex, out int* counter))
            {
                return 0;
            }

            return Interlocked.Add(ref counter[0], amount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AccumulateSampleCost(int currentCost, int additionalCost)
        {
            if (additionalCost <= 0)
            {
                return currentCost;
            }

            return currentCost > int.MaxValue - additionalCost
                ? int.MaxValue
                : currentCost + additionalCost;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveTerrainSampleCost(in GlobalWorldSamplerData data, byte estimateNormals, in TerrainSampleResult result)
        {
            if (estimateNormals == 0 ||
                (result.Flags & (byte)GlobalWorldSamplerResultFlags.HardFloor) != 0)
            {
                return 1;
            }

            float expensiveWeight = ResolveExpensiveSamplingWeight(ResolveQualityWeight(data));
            return expensiveWeight > 0.0001f ? 5 : 1;
        }

        internal static bool ShouldTripThroughputWarning(int total)
        {
            return ShouldTripThroughputWarning(total - 1, total);
        }

        internal static bool ShouldTripThroughputWarning(int previousTotal, int total)
        {
            return (previousTotal <= ThroughputWarningThreshold && total > ThroughputWarningThreshold) ||
                   (total > ThroughputWarningThreshold && (total & 1023) == 0);
        }

        internal static void RecordThroughputWarning(in GlobalWorldSamplerData data, uint frame, in TerrainSampleResult result)
        {
            TerrainSampleResult safeResult = result;
            SanitizeResult(ref safeResult, data);
            WriteTelemetryWarning(data, WarningThroughputExceeded, frame, safeResult.LocalPosition, safeResult);
            RequestTelemetryDump(data, WarningThroughputExceeded);
        }

        internal static void IncrementOutOfBounds(in GlobalWorldSamplerData data)
        {
            if (!TryGetCounterPointer(data, SampleCounterOutOfBoundsIndex, out int* counter))
            {
                return;
            }

            Interlocked.Increment(ref counter[0]);
        }

        internal static void RecordSmoothMinEstimate(in GlobalWorldSamplerData data)
        {
            if (!TryGetCounterPointer(data, SampleCounterSmoothMinCountIndex, out int* countCounter) ||
                !TryGetCounterPointer(data, SampleCounterSmoothMinNsEstimateIndex, out int* estimateCounter))
            {
                return;
            }

            Interlocked.Increment(ref countCounter[0]);
            Interlocked.Add(ref estimateCounter[0], EstimatedPolynomialSmoothMinNs);
        }

        internal static void WriteTelemetryFrame(in GlobalWorldSamplerData data, uint frame, in TerrainSampleResult result)
        {
            WriteTelemetryEntry(data, WarningFrameHeartbeat, frame, result.LocalPosition, result);
        }

        internal static void WriteTelemetryWarning(in GlobalWorldSamplerData data, int warningCode, uint frame, float3 localPosition, in TerrainSampleResult result)
        {
            WriteTelemetryEntry(data, warningCode, frame, localPosition, result);
        }

        internal static void RequestTelemetryDump(in GlobalWorldSamplerData data, int reasonCode)
        {
            if (!TryGetCounterPointer(data, SampleCounterDumpRequestIndex, out int* requestCounter))
            {
                return;
            }

            Interlocked.Exchange(ref requestCounter[0], 1);
            if (TryGetCounterPointer(data, SampleCounterDumpReasonIndex, out int* reasonCounter))
            {
                Interlocked.Exchange(ref reasonCounter[0], reasonCode);
            }
        }

        private static void WriteTelemetryEntry(in GlobalWorldSamplerData data, int warningCode, uint frame, float3 localPosition, in TerrainSampleResult result)
        {
            NativeArray<GlobalWorldSamplerTelemetryEntry> telemetryRing = data.TelemetryRing;
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
            {
                return;
            }

            int sampleCount = ReadCounter(data, SampleCounterTotalIndex);
            int outOfBoundsCount = ReadCounter(data, SampleCounterOutOfBoundsIndex);
            int smoothMinEstimateNs = ReadCounter(data, SampleCounterSmoothMinNsEstimateIndex);

            int cursor = sampleCount;
            if (TryGetCounterPointer(data, SampleCounterTelemetryCursorIndex, out int* telemetryCursor))
            {
                cursor = Interlocked.Increment(ref telemetryCursor[0]);
            }

            TerrainSampleResult safeResult = result;
            SanitizeResult(ref safeResult, data);
            float3 safeLocalPosition = IsFinite(localPosition) ? localPosition : safeResult.LocalPosition;

            int slot = PositiveModulo(cursor, telemetryRing.Length);
            GlobalWorldSamplerTelemetryEntry entry;
            entry.LocalPosition = safeLocalPosition;
            entry.Distance = safeResult.Distance;
            entry.Frame = frame;
            entry.QueryHash = safeResult.StateHash;
            entry.SampleCount = sampleCount;
            entry.WarningCode = warningCode;
            entry.Normal = safeResult.Normal;
            entry.MaterialID = safeResult.MaterialID;
            entry.Flags = safeResult.Flags;
            entry.SectorIndex = safeResult.SectorIndex;
            entry.Reserved0 = smoothMinEstimateNs;
            entry.Reserved1 = outOfBoundsCount;
            float telemetryQuality = IsFinite(data.GlobalQualityWeight) ? math.saturate(data.GlobalQualityWeight) : DefaultQualityWeight;
            entry.Reserved2 = (int)math.round(telemetryQuality * 1000000f);
            entry.Reserved3 = (int)(safeResult.BiomeHash & 0x7FFFFFFFu);
            telemetryRing[slot] = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetCounterPointer(in GlobalWorldSamplerData data, int index, out int* counter)
        {
            counter = null;
            if (index < 0)
            {
                return false;
            }

            if (data.CounterBlocks.IsCreated && data.CounterBlocks.Length > index)
            {
                counter = (int*)((byte*)data.CounterBlocks.GetUnsafePtr() + index * CounterBlockSizeBytes + CounterBlockValueOffset);
                return true;
            }

            if (data.SampleCounter.IsCreated && data.SampleCounter.Length > index)
            {
                counter = (int*)data.SampleCounter.GetUnsafePtr() + index;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadCounter(in GlobalWorldSamplerData data, int index)
        {
            return TryGetCounterPointer(data, index, out int* counter) ? *counter : 0;
        }

        private static void ResetCounter(in GlobalWorldSamplerData data, int index)
        {
            if (TryGetCounterPointer(data, index, out int* counter))
            {
                Interlocked.Exchange(ref counter[0], 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TrySampleHeight(
            in GlobalWorldSamplerData data,
            double3 aup,
            float qualityWeight,
            out float terrainHeightLocal,
            out float terrainDistance,
            out byte materialId,
            out uint biomeHash)
        {
            terrainHeightLocal = 0f;
            terrainDistance = 0f;
            materialId = data.DefaultMaterialId;
            biomeHash = ResolveDefaultBiomeHash(data);

            int resolution = data.HeightResolution;
            if (!data.HeightSamples.IsCreated ||
                !TryGetHeightSampleCount(resolution, out int heightSampleCount) ||
                data.HeightSamples.Length < heightSampleCount ||
                !IsFinite(data.HeightSize) ||
                !IsFinite(data.HeightOriginAup) ||
                !IsFinite(data.ActiveChunkOriginAup) ||
                data.HeightSize.x <= 0f ||
                data.HeightSize.y <= 0f ||
                data.HeightSize.z <= 0f)
            {
                return false;
            }

            double3 terrainLocalDouble = aup - data.HeightOriginAup;
            if (!IsFinite(terrainLocalDouble) ||
                terrainLocalDouble.x < 0d ||
                terrainLocalDouble.z < 0d ||
                terrainLocalDouble.x > data.HeightSize.x ||
                terrainLocalDouble.z > data.HeightSize.z)
            {
                return false;
            }

            float gridX = (float)(terrainLocalDouble.x / data.HeightSize.x) * (resolution - 1);
            float gridZ = (float)(terrainLocalDouble.z / data.HeightSize.z) * (resolution - 1);
            float nearestHeight01 = SampleHeightNearest(data.HeightSamples, resolution, gridX, gridZ);
            float expensiveWeight = ResolveExpensiveSamplingWeight(qualityWeight);
            float height01 = nearestHeight01;
            if (expensiveWeight > 0.0001f)
            {
                float bilinearHeight01 = SampleHeightBilinear(data.HeightSamples, resolution, gridX, gridZ);
                height01 = math.lerp(nearestHeight01, bilinearHeight01, expensiveWeight);
            }

            biomeHash = SampleBiomeHash(data, resolution, gridX, gridZ, qualityWeight);

            if ((data.ConfigFlags & (byte)GlobalWorldSamplerConfigFlags.EnableMicroNoise) != 0 &&
                data.MicroNoiseAmplitude != 0f &&
                expensiveWeight > 0.0001f)
            {
                float erosion01 = SampleGridByteMask(data.ErosionMask, resolution, gridX, gridZ, qualityWeight);
                float2 noisePosition = Float2((float)terrainLocalDouble.x, (float)terrainLocalDouble.z) * math.max(data.MicroNoiseFrequency, 0.00001f);
                float flatten = math.saturate(1f - erosion01 * math.saturate(data.ErosionFlattenStrength));
                float detailScale = expensiveWeight * flatten;
                float detailNoise = noise.snoise(noisePosition);
                float overkillWeight = ResolveOverkillSamplingWeight(qualityWeight);
                if (overkillWeight > 0.0001f)
                {
                    detailNoise += noise.snoise(noisePosition * 2.173f + Float2(17.0f, -31.0f)) * (0.5f * overkillWeight);
                }

                height01 += detailNoise * data.MicroNoiseAmplitude * detailScale / SafePositive(data.HeightSize.y, 0.001f);
                height01 = math.saturate(height01);
            }

            if (!IsFinite(height01))
            {
                return false;
            }

            double heightAupY = data.HeightOriginAup.y + (height01 * data.HeightSize.y);
            terrainHeightLocal = (float)(heightAupY - data.ActiveChunkOriginAup.y);

            float queryLocalY = (float)(aup.y - data.ActiveChunkOriginAup.y);
            terrainDistance = queryLocalY - terrainHeightLocal;
            if (!IsFinite(terrainHeightLocal) || !IsFinite(terrainDistance))
            {
                return false;
            }

            int nearestX = (int)math.round(gridX);
            int nearestZ = (int)math.round(gridZ);
            int materialIndex = ClampGridIndex(nearestX, nearestZ, resolution);
            if (data.HeightMaterialIds.IsCreated && materialIndex < data.HeightMaterialIds.Length)
            {
                materialId = data.HeightMaterialIds[materialIndex];
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleHeightNearest(NativeArray<ushort> samples, int resolution, float gridX, float gridZ)
        {
            int nearestX = (int)math.round(math.clamp(gridX, 0f, resolution - 1));
            int nearestZ = (int)math.round(math.clamp(gridZ, 0f, resolution - 1));
            return samples[ClampGridIndex(nearestX, nearestZ, resolution)] * QuantizedHeightScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleHeightBilinear(NativeArray<ushort> samples, int resolution, float gridX, float gridZ)
        {
            float clampedX = math.clamp(gridX, 0f, resolution - 1);
            float clampedZ = math.clamp(gridZ, 0f, resolution - 1);

            int x0 = (int)math.floor(clampedX);
            int z0 = (int)math.floor(clampedZ);
            int x1 = math.min(x0 + 1, resolution - 1);
            int z1 = math.min(z0 + 1, resolution - 1);

            float tx = clampedX - x0;
            float tz = clampedZ - z0;

            float h00 = samples[x0 + z0 * resolution] * QuantizedHeightScale;
            float h10 = samples[x1 + z0 * resolution] * QuantizedHeightScale;
            float h01 = samples[x0 + z1 * resolution] * QuantizedHeightScale;
            float h11 = samples[x1 + z1 * resolution] * QuantizedHeightScale;

            float hx0 = math.lerp(h00, h10, tx);
            float hx1 = math.lerp(h01, h11, tx);
            return math.lerp(hx0, hx1, tz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleGridByteMaskBilinear(NativeArray<byte> samples, int resolution, float gridX, float gridZ)
        {
            if (!samples.IsCreated || resolution <= 1)
            {
                return 0f;
            }

            if (!TryGetHeightSampleCount(resolution, out int sampleCount) || samples.Length < sampleCount)
            {
                return 0f;
            }

            float clampedX = math.clamp(gridX, 0f, resolution - 1);
            float clampedZ = math.clamp(gridZ, 0f, resolution - 1);
            int x0 = (int)math.floor(clampedX);
            int z0 = (int)math.floor(clampedZ);
            int x1 = math.min(x0 + 1, resolution - 1);
            int z1 = math.min(z0 + 1, resolution - 1);
            float tx = clampedX - x0;
            float tz = clampedZ - z0;
            const float inv255 = 1.0f / 255.0f;
            float h00 = samples[x0 + z0 * resolution] * inv255;
            float h10 = samples[x1 + z0 * resolution] * inv255;
            float h01 = samples[x0 + z1 * resolution] * inv255;
            float h11 = samples[x1 + z1 * resolution] * inv255;
            return math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleGridByteMask(NativeArray<byte> samples, int resolution, float gridX, float gridZ, float qualityWeight)
        {
            if (!samples.IsCreated ||
                resolution <= 1 ||
                !TryGetHeightSampleCount(resolution, out int sampleCount) ||
                samples.Length < sampleCount)
            {
                return 0f;
            }

            float expensiveWeight = ResolveExpensiveSamplingWeight(qualityWeight);
            int nearestX = (int)math.round(math.clamp(gridX, 0f, resolution - 1));
            int nearestZ = (int)math.round(math.clamp(gridZ, 0f, resolution - 1));
            float nearest = samples[ClampGridIndex(nearestX, nearestZ, resolution)] * (1f / 255f);
            if (expensiveWeight <= 0.0001f)
            {
                return nearest;
            }

            float bilinear = SampleGridByteMaskBilinear(samples, resolution, gridX, gridZ);
            return math.lerp(nearest, bilinear, expensiveWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint SampleBiomeHash(in GlobalWorldSamplerData data, int resolution, float gridX, float gridZ, float qualityWeight)
        {
            if (!data.BiomeAtlas.IsCreated ||
                !TryGetHeightSampleCount(resolution, out int sampleCount) ||
                data.BiomeAtlas.Length < sampleCount)
            {
                return ResolveDefaultBiomeHash(data);
            }

            float clampedX = math.clamp(gridX, 0f, resolution - 1);
            float clampedZ = math.clamp(gridZ, 0f, resolution - 1);
            float expensiveWeight = ResolveExpensiveSamplingWeight(qualityWeight);
            int nearestX = (int)math.round(clampedX);
            int nearestZ = (int)math.round(clampedZ);
            if (expensiveWeight <= 0.0001f)
            {
                return data.BiomeAtlas[ClampGridIndex(nearestX, nearestZ, resolution)];
            }

            int x0 = (int)math.floor(clampedX);
            int z0 = (int)math.floor(clampedZ);
            int x1 = math.min(x0 + 1, resolution - 1);
            int z1 = math.min(z0 + 1, resolution - 1);
            float tx = SmoothStep01(clampedX - x0);
            float tz = SmoothStep01(clampedZ - z0);

            uint h00 = data.BiomeAtlas[x0 + z0 * resolution];
            uint h10 = data.BiomeAtlas[x1 + z0 * resolution];
            uint h01 = data.BiomeAtlas[x0 + z1 * resolution];
            uint h11 = data.BiomeAtlas[x1 + z1 * resolution];

            float pixelMetersX = data.HeightSize.x / math.max(resolution - 1, 1);
            float pixelMetersZ = data.HeightSize.z / math.max(resolution - 1, 1);
            float cellEdgeMeters = math.min(math.min(tx, 1f - tx) * pixelMetersX, math.min(tz, 1f - tz) * pixelMetersZ);
            float borderWeight = SmoothStep01(1f - math.saturate(cellEdgeMeters / SafePositive(data.BiomeBlendMeters, DefaultBiomeBlendMeters)));
            float blendWeight = math.saturate(expensiveWeight * borderWeight);

            uint hx0 = BlendBiomeHashes(h00, h10, tx * blendWeight);
            uint hx1 = BlendBiomeHashes(h01, h11, tx * blendWeight);
            return BlendBiomeHashes(hx0, hx1, tz * blendWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BlendBiomeHashes(uint a, uint b, float t)
        {
            if (a == b)
            {
                return a;
            }

            uint q = (uint)math.clamp((int)math.round(math.saturate(t) * 65535f), 0, 65535);
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ RotateLeft(b, 13)) * 16777619u;
            hash = (hash ^ q) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashBiomeCell(uint seed, int x, int z)
        {
            uint hash = seed != 0u ? seed : 2166136261u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TrySampleSdf(in GlobalWorldSamplerData data, double3 aup, float qualityWeight, out float distance, out byte materialId)
        {
            distance = InactiveDistanceSentinel;
            materialId = data.CaveMaterialFallback != 0 ? data.CaveMaterialFallback : data.DefaultMaterialId;

            if (!data.EncodedSdf.IsCreated ||
                !TryGetVoxelCount(data.SdfDimensions, out int voxelCount) ||
                !IsFinite(data.SdfCellSize) ||
                !IsFinite(data.SdfOriginAup) ||
                data.SdfCellSize.x <= 0f ||
                data.SdfCellSize.y <= 0f ||
                data.SdfCellSize.z <= 0f)
            {
                return false;
            }

            if (data.EncodedSdf.Length < voxelCount)
            {
                return false;
            }

            double3 sdfLocalDouble = aup - data.SdfOriginAup;
            if (!IsFinite(sdfLocalDouble))
            {
                return false;
            }

            float3 grid = Float3(
                (float)(sdfLocalDouble.x / data.SdfCellSize.x),
                (float)(sdfLocalDouble.y / data.SdfCellSize.y),
                (float)(sdfLocalDouble.z / data.SdfCellSize.z));

            if (!IsFinite(grid) ||
                grid.x < 0f ||
                grid.y < 0f ||
                grid.z < 0f ||
                grid.x > data.SdfDimensions.x - 1 ||
                grid.y > data.SdfDimensions.y - 1 ||
                grid.z > data.SdfDimensions.z - 1)
            {
                return false;
            }

            int3 nearest = Int3(
                (int)math.round(grid.x),
                (int)math.round(grid.y),
                (int)math.round(grid.z));
            nearest = math.clamp(nearest, int3.zero, data.SdfDimensions - 1);
            int nearestIndex = SdfIndex(nearest.x, nearest.y, nearest.z, data.SdfDimensions);
            float range = SafePositive(data.SdfRange, 0.001f);
            float nearestDistance = DecodeSdf(data.EncodedSdf[nearestIndex], range);
            materialId = SampleSdfMaterial(data, nearestIndex);
            float expensiveWeight = ResolveExpensiveSamplingWeight(qualityWeight);

            if (expensiveWeight <= 0.0001f ||
                data.SdfDimensions.x < 2 ||
                data.SdfDimensions.y < 2 ||
                data.SdfDimensions.z < 2)
            {
                distance = nearestDistance;
                return true;
            }

            int3 p0 = Int3((int)math.floor(grid.x), (int)math.floor(grid.y), (int)math.floor(grid.z));
            int3 p1 = math.min(p0 + 1, data.SdfDimensions - 1);
            float3 t = grid - p0;

            float c000 = DecodeSdf(data.EncodedSdf[SdfIndex(p0.x, p0.y, p0.z, data.SdfDimensions)], range);
            float c100 = DecodeSdf(data.EncodedSdf[SdfIndex(p1.x, p0.y, p0.z, data.SdfDimensions)], range);
            float c010 = DecodeSdf(data.EncodedSdf[SdfIndex(p0.x, p1.y, p0.z, data.SdfDimensions)], range);
            float c110 = DecodeSdf(data.EncodedSdf[SdfIndex(p1.x, p1.y, p0.z, data.SdfDimensions)], range);
            float c001 = DecodeSdf(data.EncodedSdf[SdfIndex(p0.x, p0.y, p1.z, data.SdfDimensions)], range);
            float c101 = DecodeSdf(data.EncodedSdf[SdfIndex(p1.x, p0.y, p1.z, data.SdfDimensions)], range);
            float c011 = DecodeSdf(data.EncodedSdf[SdfIndex(p0.x, p1.y, p1.z, data.SdfDimensions)], range);
            float c111 = DecodeSdf(data.EncodedSdf[SdfIndex(p1.x, p1.y, p1.z, data.SdfDimensions)], range);

            float x00 = math.lerp(c000, c100, t.x);
            float x10 = math.lerp(c010, c110, t.x);
            float x01 = math.lerp(c001, c101, t.x);
            float x11 = math.lerp(c011, c111, t.x);
            float y0 = math.lerp(x00, x10, t.y);
            float y1 = math.lerp(x01, x11, t.y);
            float trilinearDistance = math.lerp(y0, y1, t.z);
            distance = math.lerp(nearestDistance, trilinearDistance, expensiveWeight);
            if (!IsFinite(distance))
            {
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveActiveSector(in GlobalWorldSamplerData data, double3 aup, out ushort sectorIndex)
        {
            sectorIndex = 0;
            if (!IsSectorRoutingConfigured(data))
            {
                return true;
            }

            if (!TryResolveSectorFlat(data, aup, out sectorIndex, out int flat))
            {
                return false;
            }

            if (!data.ActiveSectorPointers.IsCreated || data.ActiveSectorPointers.Length == 0)
            {
                return true;
            }

            return flat >= 0 && flat < data.ActiveSectorPointers.Length && data.ActiveSectorPointers[flat] != 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasSdfOverride(in GlobalWorldSamplerData data, ushort sectorIndex)
        {
            if (!data.SdfOverrideMask.IsCreated || data.SdfOverrideMask.Length == 0)
            {
                return true;
            }

            int wordIndex = sectorIndex >> 5;
            int bitIndex = sectorIndex & 31;
            if (wordIndex < 0 || wordIndex >= data.SdfOverrideMask.Length)
            {
                return false;
            }

            return (data.SdfOverrideMask[wordIndex] & (1u << bitIndex)) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSectorRoutingConfigured(in GlobalWorldSamplerData data)
        {
            return data.SectorCountX > 0 &&
                   data.SectorCountZ > 0 &&
                   IsFinite(data.SectorSizeMeters) &&
                   data.SectorSizeMeters > 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSectorFlat(in GlobalWorldSamplerData data, double3 aup, out ushort sectorIndex, out int flat)
        {
            sectorIndex = 0;
            flat = 0;

            if (!IsSectorRoutingConfigured(data) ||
                !TryGetSectorCount(data.SectorCountX, data.SectorCountZ, out int sectorCount))
            {
                return false;
            }

            double3 terrainLocal = aup - data.HeightOriginAup;
            if (!IsFinite(terrainLocal))
            {
                return false;
            }

            double invSectorSize = 1d / math.max((double)data.SectorSizeMeters, 0.0001d);
            double sectorXd = math.floor(terrainLocal.x * invSectorSize) - data.SectorOriginX;
            double sectorZd = math.floor(terrainLocal.z * invSectorSize) - data.SectorOriginZ;
            if (!IsFinite(sectorXd) ||
                !IsFinite(sectorZd) ||
                sectorXd < int.MinValue ||
                sectorZd < int.MinValue ||
                sectorXd > int.MaxValue ||
                sectorZd > int.MaxValue)
            {
                return false;
            }

            int sectorX = (int)sectorXd;
            int sectorZ = (int)sectorZd;
            if (sectorX < 0 || sectorZ < 0 || sectorX >= data.SectorCountX || sectorZ >= data.SectorCountZ)
            {
                return false;
            }

            flat = sectorX + sectorZ * data.SectorCountX;
            if (flat < 0 || flat >= sectorCount)
            {
                return false;
            }

            sectorIndex = (ushort)math.min(flat, ushort.MaxValue);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasCaveSector(in GlobalWorldSamplerData data, double3 aup, out ushort sectorIndex)
        {
            sectorIndex = 0;
            if (!data.CaveSectorMask.IsCreated || data.CaveSectorMask.Length == 0 || !IsSectorRoutingConfigured(data))
            {
                return true;
            }

            if (!TryResolveSectorFlat(data, aup, out sectorIndex, out int flat))
            {
                return false;
            }

            int wordIndex = flat >> 5;
            int bitIndex = flat & 31;
            if (wordIndex < 0 || wordIndex >= data.CaveSectorMask.Length)
            {
                return false;
            }

            return (data.CaveSectorMask[wordIndex] & (1u << bitIndex)) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte SampleSdfMaterial(in GlobalWorldSamplerData data, int index)
        {
            if (data.SdfMaterialIds.IsCreated && index >= 0 && index < data.SdfMaterialIds.Length)
            {
                return data.SdfMaterialIds[index];
            }

            return data.CaveMaterialFallback != 0 ? data.CaveMaterialFallback : data.DefaultMaterialId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BuildHardFloorResult(in GlobalWorldSamplerData data, float3 localPosition, uint frame, byte extraFlags, out TerrainSampleResult result)
        {
            localPosition = IsFinite(localPosition) ? localPosition : float3.zero;
            result.Normal = Float3(0f, 1f, 0f);
            result.Distance = 0f;
            result.LocalPosition = localPosition;
            result.Height = localPosition.y;
            result.Distance2D = 0f;
            result.Distance3D = InactiveDistanceSentinel;
            result.SeaDistance = InactiveDistanceSentinel;
            result.GradientEpsilon = SafePositive(data.NormalEpsilon, 0.001f);
            result.StateHash = BuildStateHash(localPosition, 0f, data.Revision, 0);
            result.SectorIndex = 0;
            result.MaterialID = data.HardFloorMaterialId != 0 ? data.HardFloorMaterialId : data.DefaultMaterialId;
            result.Flags = (byte)((byte)GlobalWorldSamplerResultFlags.HardFloor | extraFlags);
            result.SampleRevision = data.Revision;
            result.BiomeHash = ResolveDefaultBiomeHash(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte EncodeSdf(float distance, float range)
        {
            distance = IsFinite(distance) ? distance : 0f;
            float normalized = math.saturate((distance / SafePositive(range, 0.001f)) * 0.5f + 0.5f);
            return (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeSdf(byte encoded, float range)
        {
            return (((float)encoded * (1f / 255f)) * 2f - 1f) * SafePositive(range, 0.001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SdfIndex(int x, int y, int z, int3 dimensions)
        {
            return x + dimensions.x * (y + dimensions.y * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampGridIndex(int x, int z, int resolution)
        {
            int cx = math.clamp(x, 0, resolution - 1);
            int cz = math.clamp(z, 0, resolution - 1);
            return cx + cz * resolution;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(float3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 Float3(float x, float y, float z)
        {
            float3 value;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float2 Float2(float x, float y)
        {
            float2 value;
            value.x = x;
            value.y = y;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 Double3(double x, double y, double z)
        {
            double3 value;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 Int3(int x, int y, int z)
        {
            int3 value;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(float value)
        {
            return !math.isnan(value) && math.abs(value) < 3.402823e38f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(double3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(double value)
        {
            return value > -1.7976931348623157E+308 && value < 1.7976931348623157E+308;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSafeLocalPosition(float3 value, float maxMeters)
        {
            float safeMax = SafePositive(maxMeters, DefaultMaxLocalMeters);
            return IsFinite(value) && math.cmax(math.abs(value)) <= safeMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafePositive(float value, float fallback)
        {
            return IsFinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampFiniteDistance(float value, float fallback)
        {
            float safe = IsFinite(value) ? value : fallback;
            return math.clamp(safe, -InactiveDistanceSentinel, InactiveDistanceSentinel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveQualityWeight(in GlobalWorldSamplerData data)
        {
            return IsFinite(data.GlobalQualityWeight)
                ? math.saturate(data.GlobalQualityWeight)
                : DefaultQualityWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveExpensiveSamplingWeight(float qualityWeight)
        {
            float quality = IsFinite(qualityWeight) ? math.saturate(qualityWeight) : DefaultQualityWeight;
            float ramp = math.saturate((quality - ExpensiveSamplingStartWeight) / (1f - ExpensiveSamplingStartWeight));
            return ramp * ramp * (3f - (2f * ramp));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOverkillSamplingWeight(float qualityWeight)
        {
            float quality = IsFinite(qualityWeight) ? math.saturate(qualityWeight) : DefaultQualityWeight;
            float ramp = math.saturate((quality - 0.75f) * 4f);
            return ramp * ramp * (3f - (2f * ramp));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveScheduleCount(int inputCount, int outputCount)
        {
            if (inputCount <= 0 || outputCount <= 0)
            {
                return 0;
            }

            return math.min(inputCount, outputCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveBatchCount(int requested, int fallback)
        {
            int resolved = requested > 0 ? requested : fallback;
            return math.clamp(resolved, 1, 1024);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return IsFinite(value) && IsFinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadU32Endian(ReadOnlySpan<byte> source, int offset, bool sourceBigEndian)
        {
            uint value =
                (uint)source[offset] |
                ((uint)source[offset + 1] << 8) |
                ((uint)source[offset + 2] << 16) |
                ((uint)source[offset + 3] << 24);

            return sourceBigEndian ? ReverseBytes32(value) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadU64Endian(ReadOnlySpan<byte> source, int offset, bool sourceBigEndian)
        {
            uint word0 = ReadU32Endian(source, offset, sourceBigEndian);
            uint word1 = ReadU32Endian(source, offset + 4, sourceBigEndian);
            return sourceBigEndian
                ? ((ulong)word0 << 32) | word1
                : ((ulong)word1 << 32) | word0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveDefaultBiomeHash(in GlobalWorldSamplerData data)
        {
            uint hash = data.DefaultBiomeHash != 0u ? data.DefaultBiomeHash : data.MaterialAliasHash;
            return hash != 0u ? hash : 0x42494F4Du;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleErosionAtAup(in GlobalWorldSamplerData data, double3 aup)
        {
            int resolution = data.HeightResolution;
            if (!data.ErosionMask.IsCreated ||
                !TryGetHeightSampleCount(resolution, out int sampleCount) ||
                data.ErosionMask.Length < sampleCount ||
                !IsFinite(data.HeightSize) ||
                data.HeightSize.x <= 0f ||
                data.HeightSize.z <= 0f)
            {
                return 0f;
            }

            double3 terrainLocalDouble = aup - data.HeightOriginAup;
            if (!IsFinite(terrainLocalDouble) ||
                terrainLocalDouble.x < 0d ||
                terrainLocalDouble.z < 0d ||
                terrainLocalDouble.x > data.HeightSize.x ||
                terrainLocalDouble.z > data.HeightSize.z)
            {
                return 0f;
            }

            float gridX = (float)(terrainLocalDouble.x / data.HeightSize.x) * (resolution - 1);
            float gridZ = (float)(terrainLocalDouble.z / data.HeightSize.z) * (resolution - 1);
            return SampleGridByteMaskBilinear(data.ErosionMask, resolution, gridX, gridZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetHeightSampleCount(int resolution, out int count)
        {
            count = 0;
            if (resolution <= 1 || resolution > 46340)
            {
                return false;
            }

            count = resolution * resolution;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetVoxelCount(int3 dimensions, out int count)
        {
            count = 0;
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
            {
                return false;
            }

            long xy = (long)dimensions.x * dimensions.y;
            long total = xy * dimensions.z;
            if (total <= 0L || total > int.MaxValue)
            {
                return false;
            }

            count = (int)total;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetSectorCount(int countX, int countZ, out int count)
        {
            count = 0;
            if (countX <= 0 || countZ <= 0)
            {
                return false;
            }

            long total = (long)countX * countZ;
            if (total <= 0L || total > int.MaxValue)
            {
                return false;
            }

            count = (int)total;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModulo(int value, int divisor)
        {
            int mod = value % divisor;
            return mod < 0 ? mod + divisor : mod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNegativeZero(float value)
        {
            return value == 0f ? 0f : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildStateHash(float3 localPosition, float distance, int revision, ushort sectorIndex)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(SanitizeNegativeZero(localPosition.x))) * 16777619u;
            hash = (hash ^ math.asuint(SanitizeNegativeZero(localPosition.y))) * 16777619u;
            hash = (hash ^ math.asuint(SanitizeNegativeZero(localPosition.z))) * 16777619u;
            hash = (hash ^ math.asuint(SanitizeNegativeZero(distance))) * 16777619u;
            hash = (hash ^ (uint)revision) * 16777619u;
            hash = (hash ^ sectorIndex) * 16777619u;
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BatchSamplerJob : IJobParallelForBatch
    {
        [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
        [NoAlias, ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<byte> EncodedSdf;
        [NoAlias, ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<uint> CaveSectorMask;
        [NoAlias, ReadOnly] public NativeArray<uint> BiomeAtlas;
        [NoAlias, ReadOnly] public NativeArray<byte> ErosionMask;
        [NoAlias, ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [NoAlias, ReadOnly] public NativeArray<long> ActiveSectorPointers;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;
        public GlobalWorldSamplerScalarData ScalarData;
        [NoAlias, ReadOnly] public NativeArray<double3> PositionsAup;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TerrainSampleResult> Results;
        public uint Frame;
        public byte EstimateNormals;
        public byte Padding0;
        public ushort Padding1;

        public void SetData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSampler.ExtractJobAliases(
                in data,
                out HeightSamples,
                out HeightMaterialIds,
                out EncodedSdf,
                out SdfMaterialIds,
                out CaveSectorMask,
                out BiomeAtlas,
                out ErosionMask,
                out SdfOverrideMask,
                out ActiveSectorPointers,
                out SampleCounter,
                out CounterBlocks,
                out TelemetryRing,
                out ScalarData);
        }

        private GlobalWorldSamplerData BuildData()
        {
            return GlobalWorldSampler.FromJobAliases(
                HeightSamples,
                HeightMaterialIds,
                EncodedSdf,
                SdfMaterialIds,
                CaveSectorMask,
                BiomeAtlas,
                ErosionMask,
                SdfOverrideMask,
                ActiveSectorPointers,
                SampleCounter,
                CounterBlocks,
                TelemetryRing,
                ScalarData);
        }

        public void Execute(int startIndex, int count)
        {
            GlobalWorldSamplerData data = BuildData();
            int end = startIndex + count;
            for (int index = startIndex; index < end; index++)
            {
                GlobalWorldSampler.BuildQuery(
                    PositionsAup[index],
                    Frame,
                    EstimateNormals != 0 ? GlobalWorldSamplerQueryFlags.EstimateNormal : GlobalWorldSamplerQueryFlags.None,
                    out GlobalWorldSamplerQuery query);

                GlobalWorldSampler.Sample(data, query, out TerrainSampleResult result);
                Results[index] = result;

                if (index == 0)
                {
                    GlobalWorldSampler.WriteTelemetryFrame(data, Frame, result);
                }

                int sampleCost = GlobalWorldSampler.ResolveTerrainSampleCost(data, EstimateNormals, result);
                int total = GlobalWorldSampler.AddSampleCount(data, sampleCost);
                if (GlobalWorldSampler.ShouldTripThroughputWarning(total - sampleCost, total))
                {
                    GlobalWorldSampler.RecordThroughputWarning(data, Frame, result);
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BatchLocalSamplerJob : IJobParallelForBatch
    {
        [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
        [NoAlias, ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<byte> EncodedSdf;
        [NoAlias, ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<uint> CaveSectorMask;
        [NoAlias, ReadOnly] public NativeArray<uint> BiomeAtlas;
        [NoAlias, ReadOnly] public NativeArray<byte> ErosionMask;
        [NoAlias, ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [NoAlias, ReadOnly] public NativeArray<long> ActiveSectorPointers;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;
        public GlobalWorldSamplerScalarData ScalarData;
        [NoAlias, ReadOnly] public NativeArray<float3> PositionsLocal;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TerrainSampleResult> Results;
        public uint Frame;
        public byte EstimateNormals;
        public byte Padding0;
        public ushort Padding1;

        public void SetData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSampler.ExtractJobAliases(
                in data,
                out HeightSamples,
                out HeightMaterialIds,
                out EncodedSdf,
                out SdfMaterialIds,
                out CaveSectorMask,
                out BiomeAtlas,
                out ErosionMask,
                out SdfOverrideMask,
                out ActiveSectorPointers,
                out SampleCounter,
                out CounterBlocks,
                out TelemetryRing,
                out ScalarData);
        }

        private GlobalWorldSamplerData BuildData()
        {
            return GlobalWorldSampler.FromJobAliases(
                HeightSamples,
                HeightMaterialIds,
                EncodedSdf,
                SdfMaterialIds,
                CaveSectorMask,
                BiomeAtlas,
                ErosionMask,
                SdfOverrideMask,
                ActiveSectorPointers,
                SampleCounter,
                CounterBlocks,
                TelemetryRing,
                ScalarData);
        }

        public void Execute(int startIndex, int count)
        {
            GlobalWorldSamplerData data = BuildData();
            int end = startIndex + count;
            for (int index = startIndex; index < end; index++)
            {
                float3 local = PositionsLocal[index];
                double3 aup = data.ActiveChunkOriginAup + GlobalWorldSampler.Double3(local.x, local.y, local.z);
                GlobalWorldSampler.BuildQuery(
                    aup,
                    Frame,
                    EstimateNormals != 0 ? GlobalWorldSamplerQueryFlags.EstimateNormal : GlobalWorldSamplerQueryFlags.None,
                    out GlobalWorldSamplerQuery query);

                GlobalWorldSampler.Sample(data, query, out TerrainSampleResult result);
                Results[index] = result;

                if (index == 0)
                {
                    GlobalWorldSampler.WriteTelemetryFrame(data, Frame, result);
                }

                int sampleCost = GlobalWorldSampler.ResolveTerrainSampleCost(data, EstimateNormals, result);
                int total = GlobalWorldSampler.AddSampleCount(data, sampleCost);
                if (GlobalWorldSampler.ShouldTripThroughputWarning(total - sampleCost, total))
                {
                    GlobalWorldSampler.RecordThroughputWarning(data, Frame, result);
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct QualityWeightFilterJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerQualityState> QualityStates;

        public void Execute(int index)
        {
            GlobalWorldSamplerQualityState state = QualityStates[index];
            GlobalWorldSampler.FilterQualityState(ref state);
            QualityStates[index] = state;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockTerrainQuerySignal
    {
        // 64 bytes: one cache line; double3 AUP first, scalar lanes after 24 bytes.
        [FieldOffset(0)]
        public double3 Aup;
        [FieldOffset(24)]
        public float QualityWeight;
        [FieldOffset(28)]
        public uint Seed;
        [FieldOffset(32)]
        public uint Frame;
        [FieldOffset(36)]
        public uint _pad0;
        [FieldOffset(40)]
        private ulong _pad1;
        [FieldOffset(48)]
        private ulong _pad2;
        [FieldOffset(56)]
        private ulong _pad3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockTerrainQueryStressJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
        [NoAlias, ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<byte> EncodedSdf;
        [NoAlias, ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<uint> CaveSectorMask;
        [NoAlias, ReadOnly] public NativeArray<uint> BiomeAtlas;
        [NoAlias, ReadOnly] public NativeArray<byte> ErosionMask;
        [NoAlias, ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [NoAlias, ReadOnly] public NativeArray<long> ActiveSectorPointers;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;
        public GlobalWorldSamplerScalarData ScalarData;
        [NoAlias, ReadOnly] public NativeArray<MockTerrainQuerySignal> Signals;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TerrainSampleDTO> Results;
        public double3 OriginAup;
        public float3 ExtentsMeters;
        public uint Frame;
        public uint Seed;

        public void SetData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSampler.ExtractJobAliases(
                in data,
                out HeightSamples,
                out HeightMaterialIds,
                out EncodedSdf,
                out SdfMaterialIds,
                out CaveSectorMask,
                out BiomeAtlas,
                out ErosionMask,
                out SdfOverrideMask,
                out ActiveSectorPointers,
                out SampleCounter,
                out CounterBlocks,
                out TelemetryRing,
                out ScalarData);
        }

        private GlobalWorldSamplerData BuildData()
        {
            return GlobalWorldSampler.FromJobAliases(
                HeightSamples,
                HeightMaterialIds,
                EncodedSdf,
                SdfMaterialIds,
                CaveSectorMask,
                BiomeAtlas,
                ErosionMask,
                SdfOverrideMask,
                ActiveSectorPointers,
                SampleCounter,
                CounterBlocks,
                TelemetryRing,
                ScalarData);
        }

        public void Execute(int index)
        {
            MockTerrainQuerySignal signal = Signals.IsCreated && index < Signals.Length
                ? Signals[index]
                : BuildProceduralSignal(index);
            GlobalWorldSamplerData data = BuildData();
            data.GlobalQualityWeight = math.saturate(signal.QualityWeight);

            GlobalWorldSampler.BuildQuery(signal.Aup, signal.Frame, GlobalWorldSamplerQueryFlags.EstimateNormal, out GlobalWorldSamplerQuery query);
            GlobalWorldSampler.Sample(data, query, out TerrainSampleResult result);
            GlobalWorldSampler.ToDTO(result, out TerrainSampleDTO dto);
            ref TerrainSampleDTO target = ref GlobalWorldSampler.GetSampleRef(Results, index);
            target = dto;

            int sampleCost = GlobalWorldSampler.ResolveTerrainSampleCost(data, 1, result);
            int total = GlobalWorldSampler.AddSampleCount(data, sampleCost);
            if (GlobalWorldSampler.ShouldTripThroughputWarning(total - sampleCost, total))
            {
                GlobalWorldSampler.RecordThroughputWarning(data, signal.Frame, result);
            }
        }

        private MockTerrainQuerySignal BuildProceduralSignal(int index)
        {
            uint state = Seed ^ Frame ^ ((uint)index * 747796405u + 2891336453u);
            Unity.Mathematics.Random random;
            random.state = state != 0u ? state : 1u;
            float rx = random.NextFloat(-1f, 1f);
            float ry = random.NextFloat(-1f, 1f);
            float rz = random.NextFloat(-1f, 1f);
            float q = random.NextFloat(0f, 1f);

            MockTerrainQuerySignal signal = default;
            signal.Aup = OriginAup + GlobalWorldSampler.Double3(rx * ExtentsMeters.x, ry * ExtentsMeters.y, rz * ExtentsMeters.z);
            signal.QualityWeight = q;
            signal.Seed = random.state;
            signal.Frame = Frame;
            signal._pad0 = 0u;
            return signal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GradientNormalEstimationBatchJob : IJobParallelForBatch
    {
        [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
        [NoAlias, ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<byte> EncodedSdf;
        [NoAlias, ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<uint> CaveSectorMask;
        [NoAlias, ReadOnly] public NativeArray<uint> BiomeAtlas;
        [NoAlias, ReadOnly] public NativeArray<byte> ErosionMask;
        [NoAlias, ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [NoAlias, ReadOnly] public NativeArray<long> ActiveSectorPointers;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;
        public GlobalWorldSamplerScalarData ScalarData;
        [NoAlias, ReadOnly] public NativeArray<double3> PositionsAup;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TerrainSampleResult> Results;
        public uint Frame;

        public void SetData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSampler.ExtractJobAliases(
                in data,
                out HeightSamples,
                out HeightMaterialIds,
                out EncodedSdf,
                out SdfMaterialIds,
                out CaveSectorMask,
                out BiomeAtlas,
                out ErosionMask,
                out SdfOverrideMask,
                out ActiveSectorPointers,
                out SampleCounter,
                out CounterBlocks,
                out TelemetryRing,
                out ScalarData);
        }

        private GlobalWorldSamplerData BuildData()
        {
            return GlobalWorldSampler.FromJobAliases(
                HeightSamples,
                HeightMaterialIds,
                EncodedSdf,
                SdfMaterialIds,
                CaveSectorMask,
                BiomeAtlas,
                ErosionMask,
                SdfOverrideMask,
                ActiveSectorPointers,
                SampleCounter,
                CounterBlocks,
                TelemetryRing,
                ScalarData);
        }

        public void Execute(int startIndex, int count)
        {
            GlobalWorldSamplerData data = BuildData();
            int end = startIndex + count;
            int sampleCost = 0;
            TerrainSampleResult firstResult = default;
            for (int index = startIndex; index < end; index++)
            {
                GlobalWorldSampler.BuildQuery(PositionsAup[index], Frame, GlobalWorldSamplerQueryFlags.EstimateNormal, out GlobalWorldSamplerQuery query);
                GlobalWorldSampler.Sample(data, query, out TerrainSampleResult result);
                Results[index] = result;
                sampleCost = GlobalWorldSampler.AccumulateSampleCost(
                    sampleCost,
                    GlobalWorldSampler.ResolveTerrainSampleCost(data, 1, result));
                if (index == startIndex)
                {
                    firstResult = result;
                }
            }

            int total = GlobalWorldSampler.AddSampleCount(data, sampleCost);
            if (sampleCost > 0 && GlobalWorldSampler.ShouldTripThroughputWarning(total - sampleCost, total))
            {
                GlobalWorldSampler.RecordThroughputWarning(data, Frame, firstResult);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockBoidRaymarchJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
        [NoAlias, ReadOnly] public NativeArray<byte> HeightMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<byte> EncodedSdf;
        [NoAlias, ReadOnly] public NativeArray<byte> SdfMaterialIds;
        [NoAlias, ReadOnly] public NativeArray<uint> CaveSectorMask;
        [NoAlias, ReadOnly] public NativeArray<uint> BiomeAtlas;
        [NoAlias, ReadOnly] public NativeArray<byte> ErosionMask;
        [NoAlias, ReadOnly] public NativeArray<uint> SdfOverrideMask;
        [NoAlias, ReadOnly] public NativeArray<long> ActiveSectorPointers;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SampleCounter;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerCounterBlock> CounterBlocks;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;
        public GlobalWorldSamplerScalarData ScalarData;
        [NoAlias, ReadOnly] public NativeArray<float3> RayDirectionsLocal;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<TerrainSampleResult> Hits;
        public double3 OriginAup;
        public uint Frame;
        public float MaxDistance;
        public float MaxStepMeters;
        public int MaxSteps;

        public void SetData(in GlobalWorldSamplerData data)
        {
            GlobalWorldSampler.ExtractJobAliases(
                in data,
                out HeightSamples,
                out HeightMaterialIds,
                out EncodedSdf,
                out SdfMaterialIds,
                out CaveSectorMask,
                out BiomeAtlas,
                out ErosionMask,
                out SdfOverrideMask,
                out ActiveSectorPointers,
                out SampleCounter,
                out CounterBlocks,
                out TelemetryRing,
                out ScalarData);
        }

        private GlobalWorldSamplerData BuildData()
        {
            return GlobalWorldSampler.FromJobAliases(
                HeightSamples,
                HeightMaterialIds,
                EncodedSdf,
                SdfMaterialIds,
                CaveSectorMask,
                BiomeAtlas,
                ErosionMask,
                SdfOverrideMask,
                ActiveSectorPointers,
                SampleCounter,
                CounterBlocks,
                TelemetryRing,
                ScalarData);
        }

        public void Execute(int index)
        {
            GlobalWorldSamplerData data = BuildData();
            float3 direction = RayDirectionsLocal.IsCreated && index < RayDirectionsLocal.Length
                ? RayDirectionsLocal[index]
                : FallbackRayDirection(index);

            float directionLengthSq = math.lengthsq(direction);
            direction = GlobalWorldSampler.IsFinite(direction) && GlobalWorldSampler.IsFinite(directionLengthSq) && directionLengthSq > 0.000001f
                ? direction * math.rsqrt(directionLengthSq)
                : GlobalWorldSampler.Float3(0f, -1f, 0f);

            float maxDistance = math.max(MaxDistance, 0.1f);
            float maxStep = math.max(MaxStepMeters, GlobalWorldSampler.MinimumRaymarchStep);
            float qualityWeight = GlobalWorldSampler.IsFinite(data.GlobalQualityWeight)
                ? math.saturate(data.GlobalQualityWeight)
                : 1f;
            float expensiveWeight = GlobalWorldSampler.ResolveExpensiveSamplingWeight(qualityWeight);
            int maxSteps = math.max((int)math.round(math.lerp(1f, math.max(MaxSteps, 1), expensiveWeight)), 1);
            float traveled = 0f;
            TerrainSampleResult last = default;

            for (int step = 0; step < maxSteps && traveled <= maxDistance; step++)
            {
                double3 aup = OriginAup + GlobalWorldSampler.Double3(direction.x, direction.y, direction.z) * traveled;
                GlobalWorldSampler.BuildQuery(aup, Frame, GlobalWorldSamplerQueryFlags.None, out GlobalWorldSamplerQuery query);
                GlobalWorldSampler.SampleDistanceOnly(data, query, out last);
                GlobalWorldSampler.SanitizeResult(ref last, data);

                int sampleCost = 1;
                int total = GlobalWorldSampler.AddSampleCount(data, sampleCost);
                if (GlobalWorldSampler.ShouldTripThroughputWarning(total - sampleCost, total))
                {
                    GlobalWorldSampler.RecordThroughputWarning(data, Frame, last);
                }

                if (last.Distance <= 0.025f)
                {
                    break;
                }

                traveled += math.clamp(math.abs(last.Distance), GlobalWorldSampler.MinimumRaymarchStep, maxStep);
            }

            if (index < Hits.Length)
            {
                Hits[index] = last;
            }

            if (index == 0)
            {
                GlobalWorldSampler.WriteTelemetryFrame(data, Frame, last);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 FallbackRayDirection(int index)
        {
            float a = index * 2.3999632f;
            float y = 1f - 2f * math.frac(index * 0.61803398875f);
            float r = math.sqrt(math.max(0f, 1f - y * y));
            Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(a, out float sin, out float cos);
            return GlobalWorldSampler.Float3(cos * r, y, sin * r);
        }
    }

#if UNITY_EDITOR
    public sealed class MathTerrainProbeWindow : EditorWindow
    {
        private const int HeightResolution = 65;
        private const int ProbeVaultBufferCapacity = 14;
        private const long ProbeVaultArenaBytes = 2L * 1024L * 1024L;
        private const int ProbeCounterLength = 8;
        private const string ProbeCsvPath = "biome_atlas_overrides.csv";
        private const double CsvPollIntervalSeconds = 0.5d;
        private const SystemID ProbeOwner = SystemID.TerrainSeams;
        private const BufferID ProbeHeightSamplesBuffer = BufferID.GlobalWorldSampler_ProbeHeightSamplesBuffer;
        private const BufferID ProbeHeightMaterialsBuffer = BufferID.GlobalWorldSampler_ProbeHeightMaterialsBuffer;
        private const BufferID ProbeEncodedSdfBuffer = BufferID.GlobalWorldSampler_ProbeEncodedSdfBuffer;
        private const BufferID ProbeSdfMaterialsBuffer = BufferID.GlobalWorldSampler_ProbeSdfMaterialsBuffer;
        private const BufferID ProbeSectorMaskBuffer = BufferID.GlobalWorldSampler_ProbeSectorMaskBuffer;
        private const BufferID ProbeCountersBuffer = BufferID.GlobalWorldSampler_ProbeCountersBuffer;
        private const BufferID ProbeTelemetryBuffer = BufferID.GlobalWorldSampler_ProbeTelemetryBuffer;
        private const BufferID ProbeBiomeAtlasBuffer = BufferID.GlobalWorldSampler_ProbeBiomeAtlasBuffer;
        private const BufferID ProbeErosionMaskBuffer = BufferID.GlobalWorldSampler_ProbeErosionMaskBuffer;
        private const BufferID ProbeSdfOverrideBuffer = BufferID.GlobalWorldSampler_ProbeSdfOverrideBuffer;
        private const BufferID ProbeActiveSectorsBuffer = BufferID.GlobalWorldSampler_ProbeActiveSectorsBuffer;
        private const BufferID ProbeCounterBlocksBuffer = BufferID.GlobalWorldSampler_ProbeCounterBlocksBuffer;
        private const BufferID ProbeCsvBuffer = BufferID.GlobalWorldSampler_ProbeCsvBuffer;
        private const int ProbeCsvBufferLength = 4096;
        private static readonly int3 SdfDimensions = GlobalWorldSampler.Int3(64, 40, 64);

        private struct ProbeVaultLane<T> where T : struct
        {
            public VaultGenerationHandle<T> Handle;
            public uint ExpectedBufferID;
            public int Length;
        }

        private IDataVault _probeVault;
        private ProbeVaultLane<ushort> _heightSamplesHandle;
        private ProbeVaultLane<byte> _heightMaterialsHandle;
        private ProbeVaultLane<byte> _encodedSdfHandle;
        private ProbeVaultLane<byte> _sdfMaterialsHandle;
        private ProbeVaultLane<uint> _sectorMaskHandle;
        private ProbeVaultLane<uint> _biomeAtlasHandle;
        private ProbeVaultLane<byte> _erosionMaskHandle;
        private ProbeVaultLane<uint> _sdfOverrideHandle;
        private ProbeVaultLane<long> _activeSectorsHandle;
        private ProbeVaultLane<int> _sampleCounterHandle;
        private ProbeVaultLane<GlobalWorldSamplerCounterBlock> _counterBlocksHandle;
        private ProbeVaultLane<GlobalWorldSamplerTelemetryEntry> _telemetryHandle;
        private ProbeVaultLane<byte> _csvBufferHandle;

        private TerrainSampleResult _lastHit;
        private Toggle _csvHotReloadToggle;
        private Slider _qualityWeightSlider;
        private Slider _sineFrequencySlider;
        private Slider _caveRadiusSlider;
        private Slider _caveDepthSlider;
        private FloatField _distanceField;
        private IntegerField _materialField;
        private IntegerField _flagsField;
        private bool _hasHit;
        private bool _csvHotReload;
        private float _qualityWeight = 1.0f;
        private float _sineFrequency = 3.0f;
        private float _caveRadius = 16.0f;
        private float _caveDepth = 4.0f;
        private long _csvLastWriteTicks;
        private double _nextCsvPollTime;

        [MenuItem("Hecton8/World/Math-Terrain Probe", priority = 238)]
        public static void Open()
        {
            GetWindow<MathTerrainProbeWindow>("Math-Terrain Probe");
        }

        private void OnEnable()
        {
            Allocate();
            if (!TryLoadCsvMockProfile(silent: true))
            {
                RebuildMockData();
            }

                SceneView.duringSceneGui -= OnSceneGui;
                SceneView.duringSceneGui += OnSceneGui;
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGui;
            Dispose();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 6f;

            Label title = new Label("SHINOBU_41 Burst sampler probe");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            _qualityWeightSlider = new Slider("Force Quality Weight", 0f, 1f);
            _qualityWeightSlider.SetValueWithoutNotify(_qualityWeight);
            _qualityWeightSlider.RegisterValueChangedCallback(OnQualityWeightChanged);
            root.Add(_qualityWeightSlider);

            _sineFrequencySlider = new Slider("Sine Frequency", 0.25f, 12f);
            _sineFrequencySlider.SetValueWithoutNotify(_sineFrequency);
            _sineFrequencySlider.RegisterValueChangedCallback(OnSineFrequencyChanged);
            root.Add(_sineFrequencySlider);

            _caveRadiusSlider = new Slider("Cave Radius", 2f, 30f);
            _caveRadiusSlider.SetValueWithoutNotify(_caveRadius);
            _caveRadiusSlider.RegisterValueChangedCallback(OnCaveRadiusChanged);
            root.Add(_caveRadiusSlider);

            _caveDepthSlider = new Slider("Cave Depth", -16f, 20f);
            _caveDepthSlider.SetValueWithoutNotify(_caveDepth);
            _caveDepthSlider.RegisterValueChangedCallback(OnCaveDepthChanged);
            root.Add(_caveDepthSlider);

            _csvHotReloadToggle = new Toggle("Hot Reload CSV");
            _csvHotReloadToggle.SetValueWithoutNotify(_csvHotReload);
            _csvHotReloadToggle.RegisterValueChangedCallback(OnCsvHotReloadChanged);
            root.Add(_csvHotReloadToggle);
            root.Add(new Label(ProbeCsvPath));

            root.Add(new Button(OnRebuildButtonClicked) { text = "Rebuild Mock Terrain" });
            root.Add(new Button(OnLoadCsvButtonClicked) { text = "Load CSV Mock Profile" });
            root.Add(new Button(OnSaveCsvButtonClicked) { text = "Save CSV Mock Profile" });
            root.Add(new Button(OnDumpBlackBoxButtonClicked) { text = "Dump Black Box" });

            _distanceField = new FloatField("Distance");
            _distanceField.SetEnabled(false);
            root.Add(_distanceField);

            _materialField = new IntegerField("MaterialID");
            _materialField.SetEnabled(false);
            root.Add(_materialField);

            _flagsField = new IntegerField("Flags");
            _flagsField.SetEnabled(false);
            root.Add(_flagsField);

            SyncControlsFromState();
        }

        private void OnQualityWeightChanged(ChangeEvent<float> evt)
        {
            _qualityWeight = Mathf.Clamp01(evt.newValue);
            RebuildAndRepaint();
        }

        private void OnSineFrequencyChanged(ChangeEvent<float> evt)
        {
            _sineFrequency = evt.newValue;
            RebuildAndRepaint();
        }

        private void OnCaveRadiusChanged(ChangeEvent<float> evt)
        {
            _caveRadius = evt.newValue;
            RebuildAndRepaint();
        }

        private void OnCaveDepthChanged(ChangeEvent<float> evt)
        {
            _caveDepth = evt.newValue;
            RebuildAndRepaint();
        }

        private void OnCsvHotReloadChanged(ChangeEvent<bool> evt)
        {
            _csvHotReload = evt.newValue;
            _nextCsvPollTime = 0d;
        }

        private void OnRebuildButtonClicked()
        {
            RebuildAndRepaint();
        }

        private void OnLoadCsvButtonClicked()
        {
            TryLoadCsvMockProfile(silent: false);
        }

        private void OnSaveCsvButtonClicked()
        {
            SaveCsvMockProfile();
        }

        private void OnDumpBlackBoxButtonClicked()
        {
            if (TryBuildProbeData(out GlobalWorldSamplerData data))
            {
                GlobalWorldSampler.TryFlushRequestedTelemetryDump(data);
            }

            if (TryResolveProbeBuffers(
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out NativeArray<GlobalWorldSamplerTelemetryEntry> telemetry))
            {
                GlobalWorldSampler.TryDumpTelemetryBuffer(telemetry);
            }
        }

        private void OnEditorUpdate()
        {
            if (!_csvHotReload)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextCsvPollTime)
            {
                return;
            }

            _nextCsvPollTime = now + CsvPollIntervalSeconds;
            if (!File.Exists(ProbeCsvPath))
            {
                return;
            }

            long ticks = File.GetLastWriteTimeUtc(ProbeCsvPath).Ticks;
            if (ticks != _csvLastWriteTicks)
            {
                TryLoadCsvMockProfile(silent: true);
            }
        }

        private void RebuildAndRepaint()
        {
            RebuildMockData();
            SceneView.RepaintAll();
        }

        private void SyncControlsFromState()
        {
            if (_qualityWeightSlider != null)
            {
                _qualityWeightSlider.SetValueWithoutNotify(_qualityWeight);
            }

            if (_csvHotReloadToggle != null)
            {
                _csvHotReloadToggle.SetValueWithoutNotify(_csvHotReload);
            }

            if (_sineFrequencySlider != null)
            {
                _sineFrequencySlider.SetValueWithoutNotify(_sineFrequency);
            }

            if (_caveRadiusSlider != null)
            {
                _caveRadiusSlider.SetValueWithoutNotify(_caveRadius);
            }

            if (_caveDepthSlider != null)
            {
                _caveDepthSlider.SetValueWithoutNotify(_caveDepth);
            }

            if (_distanceField != null)
            {
                _distanceField.SetValueWithoutNotify(_hasHit ? _lastHit.Distance : 0f);
            }

            if (_materialField != null)
            {
                _materialField.SetValueWithoutNotify(_hasHit ? _lastHit.MaterialID : 0);
            }

            if (_flagsField != null)
            {
                _flagsField.SetValueWithoutNotify(_hasHit ? _lastHit.Flags : 0);
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (sceneView.camera == null ||
                !TryResolveProbeBuffers(
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            Vector3 probeOrigin = sceneView.camera.transform.position;
            Vector3 probeDirection = sceneView.camera.transform.forward;
            _hasHit = TraceEditorProbe(probeOrigin, probeDirection, out _lastHit);
            SyncControlsFromState();

            if (_hasHit)
            {
                Vector3 point = new Vector3(_lastHit.LocalPosition.x, _lastHit.LocalPosition.y, _lastHit.LocalPosition.z);
                Vector3 normal = new Vector3(_lastHit.Normal.x, _lastHit.Normal.y, _lastHit.Normal.z);
                Handles.color = Color.green;
                Handles.SphereHandleCap(0, point, Quaternion.identity, 0.6f, EventType.Repaint);
                Handles.DrawLine(point, point + normal * 2f);
            }
        }

        private bool TraceEditorProbe(Vector3 probeOrigin, Vector3 probeDirection, out TerrainSampleResult result)
        {
            result = default;
            if (!TryBuildProbeData(out GlobalWorldSamplerData data))
            {
                return false;
            }

            double3 origin = GlobalWorldSampler.Double3(probeOrigin.x, probeOrigin.y, probeOrigin.z);
            float3 direction = GlobalWorldSampler.Float3(probeDirection.x, probeDirection.y, probeDirection.z);
            float directionLengthSq = math.lengthsq(direction);
            if (directionLengthSq <= 0.000001f)
            {
                return false;
            }

            direction *= math.rsqrt(directionLengthSq);
            float traveled = 0f;
            for (int i = 0; i < 128 && traveled < 200f; i++)
            {
                double3 aup = origin + GlobalWorldSampler.Double3(direction.x, direction.y, direction.z) * traveled;
                GlobalWorldSampler.BuildQuery(aup, 0, GlobalWorldSamplerQueryFlags.EstimateNormal, out GlobalWorldSamplerQuery query);
                GlobalWorldSampler.Sample(data, query, out result);
                if (!GlobalWorldSampler.IsFinite(result.Distance))
                {
                    return false;
                }

                if ((result.Flags & (byte)GlobalWorldSamplerResultFlags.HardFloor) == 0 && result.Distance <= 0.05f)
                {
                    return true;
                }

                traveled += Mathf.Clamp(Mathf.Abs(result.Distance), GlobalWorldSampler.MinimumRaymarchStep, 4f);
            }

            return false;
        }

        private unsafe bool TryLoadCsvMockProfile(bool silent)
        {
            if (!File.Exists(ProbeCsvPath) || !TryResolveCsvBuffer(out NativeArray<byte> csvBuffer))
            {
                return false;
            }

            int bytesRead;
            try
            {
                using (var stream = new FileStream(ProbeCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int byteCapacity = math.min(csvBuffer.Length, ProbeCsvBufferLength);
                    bytesRead = stream.Read(new Span<byte>(csvBuffer.GetUnsafePtr(), byteCapacity));
                }
            }
            catch
            {
                return false;
            }

            bool changed = TryParseCsvBytes(new ReadOnlySpan<byte>(csvBuffer.GetUnsafeReadOnlyPtr(), bytesRead));
            if (changed)
            {
                _csvLastWriteTicks = File.GetLastWriteTimeUtc(ProbeCsvPath).Ticks;
                SyncControlsFromState();
                RebuildAndRepaint();
                return true;
            }

            return !silent && SaveCsvMockProfile();
        }

        private bool SaveCsvMockProfile()
        {
            try
            {
                string directory = Path.GetDirectoryName(ProbeCsvPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var writer = new StreamWriter(ProbeCsvPath, false))
                {
                    writer.WriteLine("SineFrequency,CaveRadius,CaveDepth,QualityWeight");
                    WriteCsvFloat(writer, _sineFrequency);
                    writer.Write(',');
                    WriteCsvFloat(writer, _caveRadius);
                    writer.Write(',');
                    WriteCsvFloat(writer, _caveDepth);
                    writer.Write(',');
                    WriteCsvFloat(writer, _qualityWeight);
                    writer.WriteLine();
                }

                _csvLastWriteTicks = File.GetLastWriteTimeUtc(ProbeCsvPath).Ticks;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseCsvBytes(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                int lineLength = bytes.Slice(cursor).IndexOf((byte)'\n');
                if (lineLength < 0)
                {
                    lineLength = bytes.Length - cursor;
                }

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(cursor, lineLength));
                cursor += lineLength + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                {
                    continue;
                }

                if (TryApplyCsvLine(line))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private bool TryApplyCsvLine(ReadOnlySpan<byte> line)
        {
            int c0 = line.IndexOf((byte)',');
            if (c0 < 0)
            {
                return false;
            }

            ReadOnlySpan<byte> first = TrimAscii(line.Slice(0, c0));
            ReadOnlySpan<byte> tail = line.Slice(c0 + 1);
            int c1 = tail.IndexOf((byte)',');
            ReadOnlySpan<byte> second = c1 >= 0 ? TrimAscii(tail.Slice(0, c1)) : TrimAscii(tail);
            ReadOnlySpan<byte> rest = c1 >= 0 ? tail.Slice(c1 + 1) : ReadOnlySpan<byte>.Empty;
            int c2 = rest.IndexOf((byte)',');
            ReadOnlySpan<byte> third = c1 >= 0 ? TrimAscii(c2 >= 0 ? rest.Slice(0, c2) : rest) : ReadOnlySpan<byte>.Empty;
            ReadOnlySpan<byte> fourth = c2 >= 0 ? TrimAscii(rest.Slice(c2 + 1)) : ReadOnlySpan<byte>.Empty;

            if (TryParseAsciiFloat(first, out float sineFrequency) &&
                TryParseAsciiFloat(second, out float caveRadius) &&
                TryParseAsciiFloat(third, out float caveDepth))
            {
                _sineFrequency = Mathf.Clamp(sineFrequency, 0.25f, 12f);
                _caveRadius = Mathf.Clamp(caveRadius, 2f, 30f);
                _caveDepth = Mathf.Clamp(caveDepth, -16f, 20f);
                if (TryParseAsciiFloat(fourth, out float qualityWeight))
                {
                    _qualityWeight = Mathf.Clamp01(qualityWeight);
                }

                return true;
            }

            ReadOnlySpan<byte> valueToken = third.Length > 0 ? third : second;
            if (!TryParseAsciiFloat(valueToken, out float value))
            {
                return false;
            }

            uint keyHash = third.Length > 0
                ? CombineHashes(HashAsciiLower(first), HashAsciiLower(second))
                : HashAsciiLower(first);
            uint parameterHash = third.Length > 0 ? HashAsciiLower(second) : keyHash;
            return ApplyCsvOverride(keyHash, parameterHash, value);
        }

        private bool ApplyCsvOverride(uint keyHash, uint parameterHash, float value)
        {
            const uint sineFrequencyHash = 0xAC69BD26u;
            const uint caveRadiusHash = 0x80D23A6Eu;
            const uint caveDepthHash = 0xFA169A3Du;
            const uint qualityWeightHash = 0x397033AEu;
            const uint abyssalTrenchBaseDepthHash = 0x8B8E940Au;
            const uint baseDepthHash = 0x6B44BDBDu;

            if (keyHash == sineFrequencyHash || parameterHash == sineFrequencyHash)
            {
                _sineFrequency = Mathf.Clamp(value, 0.25f, 12f);
                return true;
            }

            if (keyHash == caveRadiusHash || parameterHash == caveRadiusHash)
            {
                _caveRadius = Mathf.Clamp(value, 2f, 30f);
                return true;
            }

            if (keyHash == caveDepthHash ||
                parameterHash == caveDepthHash ||
                keyHash == abyssalTrenchBaseDepthHash ||
                parameterHash == baseDepthHash)
            {
                _caveDepth = Mathf.Clamp(value, -16f, 20f);
                return true;
            }

            if (keyHash == qualityWeightHash || parameterHash == qualityWeightHash)
            {
                _qualityWeight = Mathf.Clamp01(value);
                return true;
            }

            return false;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsAsciiWhitespace(value[start]))
            {
                start++;
            }

            while (end >= start && IsAsciiWhitespace(value[end]))
            {
                end--;
            }

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> token, out float value)
        {
            token = TrimAscii(token);
            value = 0f;
            if (token.Length == 0)
            {
                return false;
            }

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool anyDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                integer = integer * 10f + (token[index] - (byte)'0');
                index++;
                anyDigit = true;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    fraction += (token[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                    anyDigit = true;
                }
            }

            if (!anyDigit)
            {
                return false;
            }

            float result = (integer + fraction) * sign;

            if (index < token.Length && (token[index] == (byte)'e' || token[index] == (byte)'E'))
            {
                index++;
                float expSign = 1f;
                if (index < token.Length && token[index] == (byte)'-')
                {
                    expSign = -1f;
                    index++;
                }
                else if (index < token.Length && token[index] == (byte)'+')
                {
                    index++;
                }

                int expValue = 0;
                bool anyExpDigit = false;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    expValue = expValue * 10 + (token[index] - (byte)'0');
                    index++;
                    anyExpDigit = true;
                }

                if (!anyExpDigit)
                {
                    return false;
                }

                float expFactor = math.pow(10f, expSign * expValue);
                result *= expFactor;
            }

            value = result;
            return true;
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> token)
        {
            token = TrimAscii(token);
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                {
                    c = (byte)(c + 32);
                }

                if (c == (byte)'_' || c == (byte)' ' || c == (byte)'-')
                {
                    continue;
                }

                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }

        private static uint CombineHashes(uint a, uint b)
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            return hash;
        }

        private static void WriteCsvFloat(StreamWriter writer, float value)
        {
            Span<char> buffer = stackalloc char[32];
            if (!value.TryFormat(buffer, out int written, "R", CultureInfo.InvariantCulture))
            {
                writer.Write('0');
                return;
            }

            for (int i = 0; i < written; i++)
            {
                writer.Write(buffer[i]);
            }
        }

        private ProbeVaultLane<T> AcquireProbeLane<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (_probeVault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = _probeVault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                ProbeOwner,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            return new ProbeVaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };
        }

        private static bool IsProbeLaneBound<T>(in ProbeVaultLane<T> lane) where T : struct
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private NativeArray<T> OpenProbeLane<T>(in ProbeVaultLane<T> lane) where T : struct
        {
            if (_probeVault == null ||
                !IsProbeLaneBound(in lane) ||
                !_probeVault.TryResolveHandle(in lane.Handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < lane.Length)
            {
                return default;
            }

            return buffer;
        }

        private void Allocate()
        {
            Dispose();
            _probeVault = GlobalDataVault.Create(ProbeVaultBufferCapacity, ProbeVaultArenaBytes);

            _heightSamplesHandle = AcquireProbeLane<ushort>(
                ProbeHeightSamplesBuffer,
                HeightResolution * HeightResolution,
                NativeArrayOptions.UninitializedMemory);
            _heightMaterialsHandle = AcquireProbeLane<byte>(
                ProbeHeightMaterialsBuffer,
                HeightResolution * HeightResolution,
                NativeArrayOptions.UninitializedMemory);

            int sdfCount = SdfDimensions.x * SdfDimensions.y * SdfDimensions.z;
            _encodedSdfHandle = AcquireProbeLane<byte>(
                ProbeEncodedSdfBuffer,
                sdfCount,
                NativeArrayOptions.UninitializedMemory);
            _sdfMaterialsHandle = AcquireProbeLane<byte>(
                ProbeSdfMaterialsBuffer,
                sdfCount,
                NativeArrayOptions.UninitializedMemory);
            _sectorMaskHandle = AcquireProbeLane<uint>(
                ProbeSectorMaskBuffer,
                4,
                NativeArrayOptions.UninitializedMemory);
            _biomeAtlasHandle = AcquireProbeLane<uint>(
                ProbeBiomeAtlasBuffer,
                HeightResolution * HeightResolution,
                NativeArrayOptions.UninitializedMemory);
            _erosionMaskHandle = AcquireProbeLane<byte>(
                ProbeErosionMaskBuffer,
                HeightResolution * HeightResolution,
                NativeArrayOptions.UninitializedMemory);
            _sdfOverrideHandle = AcquireProbeLane<uint>(
                ProbeSdfOverrideBuffer,
                4,
                NativeArrayOptions.UninitializedMemory);
            _activeSectorsHandle = AcquireProbeLane<long>(
                ProbeActiveSectorsBuffer,
                4,
                NativeArrayOptions.UninitializedMemory);
            _sampleCounterHandle = AcquireProbeLane<int>(
                ProbeCountersBuffer,
                ProbeCounterLength,
                NativeArrayOptions.ClearMemory);
            _counterBlocksHandle = AcquireProbeLane<GlobalWorldSamplerCounterBlock>(
                ProbeCounterBlocksBuffer,
                GlobalWorldSampler.CounterBlockCount,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = AcquireProbeLane<GlobalWorldSamplerTelemetryEntry>(
                ProbeTelemetryBuffer,
                GlobalWorldSampler.TelemetryRingLength,
                NativeArrayOptions.ClearMemory);
            _csvBufferHandle = AcquireProbeLane<byte>(
                ProbeCsvBuffer,
                ProbeCsvBufferLength,
                NativeArrayOptions.UninitializedMemory);
        }

        private void Dispose()
        {
            if (_probeVault != null)
            {
                _probeVault.Dispose();
                _probeVault = null;
            }

            _heightSamplesHandle = default;
            _heightMaterialsHandle = default;
            _encodedSdfHandle = default;
            _sdfMaterialsHandle = default;
            _sectorMaskHandle = default;
            _biomeAtlasHandle = default;
            _erosionMaskHandle = default;
            _sdfOverrideHandle = default;
            _activeSectorsHandle = default;
            _sampleCounterHandle = default;
            _counterBlocksHandle = default;
            _telemetryHandle = default;
            _csvBufferHandle = default;
        }

        private void RebuildMockData()
        {
            if (!TryBuildProbeData(out GlobalWorldSamplerData data))
            {
                return;
            }

            GlobalWorldSampler.MockGeologyGenerator(ref data, _sineFrequency, _caveRadius, _caveDepth);
        }

        private bool TryBuildProbeData(out GlobalWorldSamplerData data)
        {
            data = default;
            if (!TryResolveProbeBuffersExtended(
                    out NativeArray<ushort> heightSamples,
                    out NativeArray<byte> heightMaterials,
                    out NativeArray<byte> encodedSdf,
                    out NativeArray<byte> sdfMaterials,
                    out NativeArray<uint> sectorMask,
                    out NativeArray<uint> biomeAtlas,
                    out NativeArray<byte> erosionMask,
                    out NativeArray<uint> sdfOverrideMask,
                    out NativeArray<long> activeSectors,
                    out NativeArray<int> sampleCounter,
                    out NativeArray<GlobalWorldSamplerCounterBlock> counterBlocks,
                    out NativeArray<GlobalWorldSamplerTelemetryEntry> telemetry))
            {
                return false;
            }

            data = GlobalWorldSampler.FromDataVaultAliases(
                heightSamples,
                heightMaterials,
                encodedSdf,
                sdfMaterials,
                sectorMask,
                sampleCounter,
                telemetry,
                double3.zero,
                GlobalWorldSampler.Double3(-64d, -48d, -64d),
                GlobalWorldSampler.Float3(128f, 96f, 128f),
                HeightResolution,
                GlobalWorldSampler.Double3(-64d, -64d, -64d),
                GlobalWorldSampler.Float3(2f, 2f, 2f),
                SdfDimensions,
                24f,
                0x484D4D31u,
                0x53444631u,
                0x4D415431u,
                1);

            data.BiomeAtlas = biomeAtlas;
            data.ErosionMask = erosionMask;
            data.SdfOverrideMask = sdfOverrideMask;
            data.ActiveSectorPointers = activeSectors;
            data.CounterBlocks = counterBlocks;
            data.SeaLevel = GlobalWorldSampler.DefaultSeaLevel;
            data.NormalEpsilon = 0.5f;
            data.SeamSmoothMeters = 1.5f;
            data.MicroNoiseAmplitude = 0.75f;
            data.MicroNoiseFrequency = 0.055f;
            data.BiomeBlendMeters = 3.5f;
            data.ErosionFlattenStrength = 0.4f;
            data.ErosionNormalBias = GlobalWorldSampler.Float3(0.15f, 0.35f, 0.05f);
            data.MaxLocalMeters = 512f;
            data.SectorSizeMeters = 64f;
            data.SectorCountX = 2;
            data.SectorCountZ = 2;
            ApplyFlags(ref data);
            return true;
        }

        private bool TryResolveProbeBuffers(
            out NativeArray<ushort> heightSamples,
            out NativeArray<byte> heightMaterials,
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> sdfMaterials,
            out NativeArray<uint> sectorMask,
            out NativeArray<int> sampleCounter,
            out NativeArray<GlobalWorldSamplerTelemetryEntry> telemetry)
        {
            return TryResolveProbeBuffersExtended(
                out heightSamples,
                out heightMaterials,
                out encodedSdf,
                out sdfMaterials,
                out sectorMask,
                out _,
                out _,
                out _,
                out _,
                out sampleCounter,
                out _,
                out telemetry);
        }

        private bool TryResolveProbeBuffersExtended(
            out NativeArray<ushort> heightSamples,
            out NativeArray<byte> heightMaterials,
            out NativeArray<byte> encodedSdf,
            out NativeArray<byte> sdfMaterials,
            out NativeArray<uint> sectorMask,
            out NativeArray<uint> biomeAtlas,
            out NativeArray<byte> erosionMask,
            out NativeArray<uint> sdfOverrideMask,
            out NativeArray<long> activeSectors,
            out NativeArray<int> sampleCounter,
            out NativeArray<GlobalWorldSamplerCounterBlock> counterBlocks,
            out NativeArray<GlobalWorldSamplerTelemetryEntry> telemetry)
        {
            heightSamples = default;
            heightMaterials = default;
            encodedSdf = default;
            sdfMaterials = default;
            sectorMask = default;
            biomeAtlas = default;
            erosionMask = default;
            sdfOverrideMask = default;
            activeSectors = default;
            sampleCounter = default;
            counterBlocks = default;
            telemetry = default;

            if (_probeVault == null)
            {
                return false;
            }

            heightSamples = OpenProbeLane(in _heightSamplesHandle);
            heightMaterials = OpenProbeLane(in _heightMaterialsHandle);
            encodedSdf = OpenProbeLane(in _encodedSdfHandle);
            sdfMaterials = OpenProbeLane(in _sdfMaterialsHandle);
            sectorMask = OpenProbeLane(in _sectorMaskHandle);
            biomeAtlas = OpenProbeLane(in _biomeAtlasHandle);
            erosionMask = OpenProbeLane(in _erosionMaskHandle);
            sdfOverrideMask = OpenProbeLane(in _sdfOverrideHandle);
            activeSectors = OpenProbeLane(in _activeSectorsHandle);
            sampleCounter = OpenProbeLane(in _sampleCounterHandle);
            counterBlocks = OpenProbeLane(in _counterBlocksHandle);
            telemetry = OpenProbeLane(in _telemetryHandle);

            return heightSamples.IsCreated &&
                   heightMaterials.IsCreated &&
                   encodedSdf.IsCreated &&
                   sdfMaterials.IsCreated &&
                   sectorMask.IsCreated &&
                   biomeAtlas.IsCreated &&
                   erosionMask.IsCreated &&
                   sdfOverrideMask.IsCreated &&
                   activeSectors.IsCreated &&
                   sampleCounter.IsCreated &&
                   counterBlocks.IsCreated &&
                   telemetry.IsCreated;
        }

        private bool TryResolveCsvBuffer(out NativeArray<byte> csvBuffer)
        {
            csvBuffer = default;
            if (_probeVault == null || !IsProbeLaneBound(in _csvBufferHandle))
            {
                return false;
            }

            csvBuffer = OpenProbeLane(in _csvBufferHandle);
            return csvBuffer.IsCreated && csvBuffer.Length >= ProbeCsvBufferLength;
        }

        private void ApplyFlags(ref GlobalWorldSamplerData data)
        {
            var flags = GlobalWorldSamplerConfigFlags.EnableSdf |
                        GlobalWorldSamplerConfigFlags.EnableSmoothMin |
                        GlobalWorldSamplerConfigFlags.EnableCavernOverride |
                        GlobalWorldSamplerConfigFlags.EnableCeiling |
                        GlobalWorldSamplerConfigFlags.EnableMicroNoise;

            data.ConfigFlags = (byte)flags;
            data.GlobalQualityWeight = Mathf.Clamp01(_qualityWeight);
        }
    }
#endif
}
