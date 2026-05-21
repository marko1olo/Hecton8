#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.GeologyForge
{
    internal static class TopographyForgeConstants
    {
        public const int BlackBoxFrameCount = 300;
        public const int DefaultSectorResolution = 512;
        public const int MockSectorResolution = 4096;
        public const int PreviewResolution = 128;
        public const int DefaultMacroResolution = 1024;
        public const int DefaultSectorSizeMeters = 512;
        public const int DefaultWorldSizeMeters = 102400;
        public const int MaximumRiftSegments = 64;
        public const int MaximumHeightmapResolution = 4096;
        public const int HeightmapHeaderBytes = 128;
        public const int BiomeMaskHeaderBytes = 128;
        public const int BiomeMaskChannels = 4;
        public const uint HeightmapMagic = 0x484D3854u; // T8MH little-endian
        public const uint BiomeMaskMagic = 0x4D423854u; // T8BM little-endian
        public const uint HeightmapVersion = 1u;
        public const uint HeightmapEndianMarker = 0x01020304u;
        public const uint HeightmapSchemaHash = 0xA2400001u;
        public const uint BiomeMaskSchemaHash = 0xA2400002u;
        public const uint BiomeMaskSemanticsHash = 0x41424752u; // RGBA little-endian semantic tag for fixed float4 weights
        public const uint WarningNaNHeight = 1u << 0;
        public const uint WarningHeightClamped = 1u << 1;
        public const uint WarningLegacyMapMagicGraph = 1u << 2;
        public const uint WarningRuntimeTerrainDebt = 1u << 3;
        public const uint WarningAsyncWriteFailed = 1u << 4;
        public const uint WarningInvalidBiomeMask = 1u << 5;
        public const uint WarningBiomeMaskRecipeOverflow = 1u << 6;
        public const uint RollbackExcludedFlag = 1u << 31;
        public const uint DumpMagic = 0x53483234u; // 42HS little-endian
        public const string CsvPath = "Assets/_Project/Data/Terrain/terrain_macro_biomes.csv";
        public const string SectorOutputFolder = "Assets/StreamingAssets/Hecton8/TerrainHeightmaps";
        public const string MacroOutputPath = "Assets/StreamingAssets/Hecton8/TerrainHeightmaps/macro_heightmap.h8bin";
        public const string MacroBiomeMaskOutputPath = "Assets/StreamingAssets/Hecton8/TerrainHeightmaps/macro_biome_mask.h8bin";
        public const string BakeReportPath = "Docs/Reports/TERRAIN_BAKE_REPORT.json";
        public const string MapMagicInquisitionReportPath = "Docs/Reports/TERRAIN_MAPMAGIC_INQUISITION.json";
        public const string RuntimeScannerReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT_SHINOBU_240.json";
        public const string LayoutAuditReportPath = "Docs/Reports/TERRAIN_HEIGHTMAP_AUDIT.json";
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_240.bin";
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FractalParamsDTO
    {
        [FieldOffset(0)] public float Frequency;
        [FieldOffset(4)] public float Amplitude;
        [FieldOffset(8)] public float Lacunarity;
        [FieldOffset(12)] public float Persistence;
        [FieldOffset(16)] public int Octaves;
        [FieldOffset(20)] public uint SeedHash;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DomainWarpParamsDTO
    {
        [FieldOffset(0)] public float Frequency;
        [FieldOffset(4)] public float StrengthMeters;
        [FieldOffset(8)] public float Lacunarity;
        [FieldOffset(12)] public float Persistence;
        [FieldOffset(16)] public int Octaves;
        [FieldOffset(20)] public uint SeedHash;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct TectonicRiftSegmentDTO
    {
        [FieldOffset(0)] public double2 StartAupXZ;
        [FieldOffset(16)] public double2 EndAupXZ;
        [FieldOffset(32)] public float WidthMeters;
        [FieldOffset(36)] public float DepthMeters;
        [FieldOffset(40)] public float EdgeSharpness;
        [FieldOffset(44)] public float FalloffPower;
        [FieldOffset(48)] public uint SeedHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct TopographyBakeConfigDTO
    {
        [FieldOffset(0)] public double3 SectorAup;
        [FieldOffset(24)] public double PixelSizeMeters;
        [FieldOffset(32)] public int Width;
        [FieldOffset(36)] public int Height;
        [FieldOffset(40)] public float HeightMinMeters;
        [FieldOffset(44)] public float HeightMaxMeters;
        [FieldOffset(48)] public float SeaFloorBiasMeters;
        [FieldOffset(52)] public float RidgeBlend;
        [FieldOffset(56)] public float TerraceSteps;
        [FieldOffset(60)] public float TerraceStrength;
        [FieldOffset(64)] public float TerraceSlopeStart;
        [FieldOffset(68)] public float TerraceSlopeEnd;
        [FieldOffset(72)] public float RiftDepthMeters;
        [FieldOffset(76)] public float RiftWidthMeters;
        [FieldOffset(80)] public uint WorldSeed;
        [FieldOffset(84)] public int SectorX;
        [FieldOffset(88)] public int SectorZ;
        [FieldOffset(92)] public int RiftCount;
        [FieldOffset(96)] public float GlobalQualityWeight;
        [FieldOffset(100)] public float HeightScaleMeters;
        [FieldOffset(104)] public uint Flags;
        [FieldOffset(108)] public uint _pad0;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct TopographyBiomeRecipeDTO
    {
        [FieldOffset(0)] public FixedString64Bytes Name;
        [FieldOffset(64)] public double2 CenterAupXZ;
        [FieldOffset(80)] public float RadiusMeters;
        [FieldOffset(84)] public float TerraceSteps;
        [FieldOffset(88)] public float TerraceStrength;
        [FieldOffset(92)] public float RidgeBlend;
        [FieldOffset(96)] public float RiftDepthMeters;
        [FieldOffset(100)] public uint SeedHash;
        [FieldOffset(104)] public uint _pad0;
        [FieldOffset(108)] public uint _pad1;
        [FieldOffset(112)] public FractalParamsDTO Ridge;
        [FieldOffset(144)] public DomainWarpParamsDTO Warp;
        [FieldOffset(176)] public ulong _pad2;
        [FieldOffset(184)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct TopographyBiomeKernelDTO
    {
        [FieldOffset(0)] public double2 CenterAupXZ;
        [FieldOffset(16)] public float RadiusMeters;
        [FieldOffset(20)] public float InvRadiusMeters;
        [FieldOffset(24)] public float InvRadiusSqMeters;
        [FieldOffset(28)] public float TerraceSteps;
        [FieldOffset(32)] public float TerraceStrength;
        [FieldOffset(36)] public float RidgeBlend;
        [FieldOffset(40)] public float RiftDepthMeters;
        [FieldOffset(44)] public uint SeedHash;
        [FieldOffset(48)] public FractalParamsDTO Ridge;
        [FieldOffset(80)] public DomainWarpParamsDTO Warp;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;
    }

    internal struct TopographyBakeSettings
    {
        public int SectorResolution;
        public int SectorCountX;
        public int SectorCountZ;
        public int MacroResolution;
        public float SectorSizeMeters;
        public float HeightMinMeters;
        public float HeightMaxMeters;
        public float SeaFloorBiasMeters;
        public float RidgeFrequency;
        public float RidgeAmplitude;
        public float RidgeLacunarity;
        public float RidgePersistence;
        public int RidgeOctaves;
        public float WarpFrequency;
        public float WarpStrengthMeters;
        public float TerraceSteps;
        public float TerraceStrength;
        public float RiftDepthMeters;
        public float RiftWidthMeters;
        public uint WorldSeed;
        public float GlobalQualityWeight;
        public double3 WorldOriginAup;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct HeightmapFileHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderBytes;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int Width;
        [FieldOffset(20)] public int Height;
        [FieldOffset(24)] public int SectorX;
        [FieldOffset(28)] public int SectorZ;
        [FieldOffset(32)] public double3 SectorAup;
        [FieldOffset(56)] public double PixelSizeMeters;
        [FieldOffset(64)] public float MinHeightMeters;
        [FieldOffset(68)] public float MaxHeightMeters;
        [FieldOffset(72)] public float HeightMinContractMeters;
        [FieldOffset(76)] public float HeightMaxContractMeters;
        [FieldOffset(80)] public uint WorldSeed;
        [FieldOffset(84)] public uint DataChecksum;
        [FieldOffset(88)] public uint PayloadBytes;
        [FieldOffset(92)] public uint ElementStrideBytes;
        [FieldOffset(96)] public uint EndianMarker;
        [FieldOffset(100)] public uint SchemaHash;
        [FieldOffset(104)] public ulong Reserved1;
        [FieldOffset(112)] public ulong Reserved2;
        [FieldOffset(120)] public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct BiomeMaskFileHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderBytes;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int Width;
        [FieldOffset(20)] public int Height;
        [FieldOffset(24)] public int SectorX;
        [FieldOffset(28)] public int SectorZ;
        [FieldOffset(32)] public double3 SectorAup;
        [FieldOffset(56)] public double PixelSizeMeters;
        [FieldOffset(64)] public uint WorldSeed;
        [FieldOffset(68)] public uint DataChecksum;
        [FieldOffset(72)] public uint PayloadBytes;
        [FieldOffset(76)] public uint ElementStrideBytes;
        [FieldOffset(80)] public uint ChannelCount;
        [FieldOffset(84)] public uint RecipeCount;
        [FieldOffset(88)] public uint EndianMarker;
        [FieldOffset(92)] public uint SchemaHash;
        [FieldOffset(96)] public uint SemanticsHash;
        [FieldOffset(100)] public uint Reserved0;
        [FieldOffset(104)] public ulong Reserved1;
        [FieldOffset(112)] public ulong Reserved2;
        [FieldOffset(120)] public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct TopographyBakeTelemetryEntry
    {
        [FieldOffset(0)] public double3 SectorAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Stage;
        [FieldOffset(32)] public float MinHeightMeters;
        [FieldOffset(36)] public float MaxHeightMeters;
        [FieldOffset(40)] public float StageMilliseconds;
        [FieldOffset(44)] public int SectorX;
        [FieldOffset(48)] public int SectorZ;
        [FieldOffset(52)] public uint WarningFlags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint DumpReason;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct TopographyBakeDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint EntryCount;
        [FieldOffset(8)] public uint EntrySize;
        [FieldOffset(12)] public uint Cursor;
        [FieldOffset(16)] public uint Reason;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct TopographyBakeMetrics
    {
        [FieldOffset(0)] public int SectorCount;
        [FieldOffset(4)] public int CompletedSectors;
        [FieldOffset(8)] public int NaNSectors;
        [FieldOffset(12)] public uint WarningFlags;
        [FieldOffset(16)] public float MinHeightMeters;
        [FieldOffset(20)] public float MaxHeightMeters;
        [FieldOffset(24)] public double RidgeMilliseconds;
        [FieldOffset(32)] public double WarpMilliseconds;
        [FieldOffset(40)] public double TerraceMilliseconds;
        [FieldOffset(48)] public double RiftMilliseconds;
        [FieldOffset(56)] public double SerializationMilliseconds;
        [FieldOffset(64)] public double MacroMilliseconds;
        [FieldOffset(72)] public double MockSectorMilliseconds;
        [FieldOffset(80)] public int RecipeCount;
        [FieldOffset(84)] public uint _pad0;
        [FieldOffset(88)] public double PipelineMilliseconds;
        [FieldOffset(96)] public ulong _pad2;
        [FieldOffset(104)] public ulong _pad3;
        [FieldOffset(112)] public ulong _pad4;
        [FieldOffset(120)] public ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct TopographyBakeRunStateDTO
    {
        [FieldOffset(0)] public TopographyBakeMetrics Metrics;
        [FieldOffset(128)] public uint BlackBoxCursor;
        [FieldOffset(132)] public uint _pad0;
        [FieldOffset(136)] public ulong _pad1;
        [FieldOffset(144)] public ulong _pad2;
        [FieldOffset(152)] public ulong _pad3;
        [FieldOffset(160)] public ulong _pad4;
        [FieldOffset(168)] public ulong _pad5;
        [FieldOffset(176)] public ulong _pad6;
        [FieldOffset(184)] public ulong _pad7;
    }
}
#endif
