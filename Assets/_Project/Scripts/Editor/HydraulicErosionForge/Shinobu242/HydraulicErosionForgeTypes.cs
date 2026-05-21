#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static class HydraulicErosionForgeConstants
    {
        public const int BlackBoxFrameCount = 300;
        public const int DefaultSectorResolution = 512;
        public const int MockResolution = 1024;
        public const int PreviewResolution = 128;
        public const int MacroResolution = 1024;
        public const int DefaultSectorSizeMeters = 512;
        public const int DefaultDropletCount = 1000000;
        public const int PreviewDropletCount = 12000;
        public const int MaxDropletLifetime = 96;
        public const int HeightmapHeaderBytes = 160;
        public const int SeamTransferHeaderBytes = 160;
        public const uint HeightmapMagic = 0x32454848u; // HHE2
        public const uint SeamTransferMagic = 0x4D455348u; // HSEM
        public const uint HeightmapVersion = 1u;
        public const uint SeamTransferVersion = 1u;
        public const uint LittleEndianMarker = 0x01020304u;
        public const uint PayloadKindHeight = 1u;
        public const uint PayloadKindSilt = 2u;
        public const uint PayloadKindMacro = 3u;
        public const uint PayloadFlagRollbackExcluded = 1u << 31;
        public const uint WarningNonFiniteHeight = 1u << 0;
        public const uint WarningQueueOverflow = 1u << 1;
        public const uint WarningScannerRuntimeTerrainMutation = 1u << 2;
        public const uint WarningSerializationFailed = 1u << 3;
        public const uint DumpMagic = 0x32343853u; // S842
        public const uint DumpReasonNaN = 1u;
        public const uint DumpReasonException = 2u;
        public const string WeatheringCsvPath = "Assets/_Project/Data/Terrain/terrain_weathering_profiles.csv";
        public const string OutputFolder = "Assets/StreamingAssets/Hecton8/TerrainErosion";
        public const string MacroOutputPath = "Assets/StreamingAssets/Hecton8/TerrainErosion/macro_erosion.h8bin";
        public const string BakeReportPath = "Docs/Reports/EROSION_BAKE_REPORT.json";
        public const string RuntimeScannerReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT.json";
        public const string SelfAuditReportPath = "Docs/Reports/SHINOBU_242_SELF_AUDIT.xml";
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_242.bin";
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ErosionDropletDTO
    {
        [FieldOffset(0)] public float2 Position;
        [FieldOffset(8)] public float2 Direction;
        [FieldOffset(16)] public float Velocity;
        [FieldOffset(20)] public float WaterVolume;
        [FieldOffset(24)] public float SedimentCapacity;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct HydraulicErosionSettingsDTO
    {
        [FieldOffset(0)] public double3 SectorAup;
        [FieldOffset(24)] public double CellSizeMeters;
        [FieldOffset(32)] public int Width;
        [FieldOffset(36)] public int Height;
        [FieldOffset(40)] public int SectorX;
        [FieldOffset(44)] public int SectorZ;
        [FieldOffset(48)] public int DropletCount;
        [FieldOffset(52)] public int MaxLifetime;
        [FieldOffset(56)] public uint WorldSeed;
        [FieldOffset(60)] public float Inertia;
        [FieldOffset(64)] public float CapacityFactor;
        [FieldOffset(68)] public float MinSedimentCapacity;
        [FieldOffset(72)] public float ErosionRate;
        [FieldOffset(76)] public float DepositRate;
        [FieldOffset(80)] public float EvaporationRate;
        [FieldOffset(84)] public float Gravity;
        [FieldOffset(88)] public float InitialWater;
        [FieldOffset(92)] public float InitialVelocity;
        [FieldOffset(96)] public float MinWater;
        [FieldOffset(100)] public float HeightScaleMeters;
        [FieldOffset(104)] public float SiltMaskGain;
        [FieldOffset(108)] public float GlobalQualityWeight;
        [FieldOffset(112)] public uint Flags;
        [FieldOffset(116)] public uint _pad0;
        [FieldOffset(120)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ErosionBakeTelemetryEntry
    {
        [FieldOffset(0)] public double3 SectorAup;
        [FieldOffset(24)] public uint Stage;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public float MinHeight;
        [FieldOffset(36)] public float MaxHeight;
        [FieldOffset(40)] public float MaxCarvedDepth;
        [FieldOffset(44)] public float SedimentTransported;
        [FieldOffset(48)] public int SectorX;
        [FieldOffset(52)] public int SectorZ;
        [FieldOffset(56)] public uint WarningFlags;
        [FieldOffset(60)] public uint DropletSample;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ErosionBakeDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint EntryCount;
        [FieldOffset(8)] public uint EntrySize;
        [FieldOffset(12)] public uint Cursor;
        [FieldOffset(16)] public uint Reason;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 160)]
    internal struct ErosionHeightmapFileHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderBytes;
        [FieldOffset(12)] public uint PayloadKind;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int Width;
        [FieldOffset(24)] public int Height;
        [FieldOffset(28)] public int SectorX;
        [FieldOffset(32)] public int SectorZ;
        [FieldOffset(40)] public double3 SectorAup;
        [FieldOffset(64)] public double CellSizeMeters;
        [FieldOffset(72)] public float MinValue;
        [FieldOffset(76)] public float MaxValue;
        [FieldOffset(80)] public uint WorldSeed;
        [FieldOffset(84)] public uint DataChecksum;
        [FieldOffset(88)] public uint PayloadBytes;
        [FieldOffset(92)] public uint ElementStrideBytes;
        [FieldOffset(96)] public uint DropletCount;
        [FieldOffset(100)] public uint MaxLifetime;
        [FieldOffset(104)] public float GlobalQualityWeight;
        [FieldOffset(108)] public float MaxCarvedDepth;
        [FieldOffset(112)] public float SedimentTransported;
        [FieldOffset(116)] public uint WarningFlags;
        [FieldOffset(120)] public uint EndianMarker;
        [FieldOffset(124)] public uint _pad0;
        [FieldOffset(128)] public ulong Reserved1;
        [FieldOffset(136)] public ulong Reserved2;
        [FieldOffset(144)] public ulong Reserved3;
        [FieldOffset(152)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 160)]
    internal struct ErosionSeamTransferFileHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderBytes;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int DirectionX;
        [FieldOffset(20)] public int DirectionZ;
        [FieldOffset(24)] public int SourceSectorX;
        [FieldOffset(28)] public int SourceSectorZ;
        [FieldOffset(32)] public int NeighborSectorX;
        [FieldOffset(36)] public int NeighborSectorZ;
        [FieldOffset(40)] public uint DropletCount;
        [FieldOffset(44)] public uint ElementStrideBytes;
        [FieldOffset(48)] public uint PayloadBytes;
        [FieldOffset(52)] public uint DataChecksum;
        [FieldOffset(56)] public uint WarningFlags;
        [FieldOffset(60)] public uint _pad0;
        [FieldOffset(64)] public double3 SourceAup;
        [FieldOffset(88)] public double3 NeighborAup;
        [FieldOffset(112)] public float MaxCarvedDepth;
        [FieldOffset(116)] public float SedimentTransported;
        [FieldOffset(120)] public float GlobalQualityWeight;
        [FieldOffset(124)] public uint EndianMarker;
        [FieldOffset(128)] public ulong Reserved0;
        [FieldOffset(136)] public ulong Reserved1;
        [FieldOffset(144)] public ulong Reserved2;
        [FieldOffset(152)] public ulong Reserved3;
    }

    internal struct WeatheringProfileDTO
    {
        public FixedString64Bytes Name;
        public float RainRate;
        public float EvaporationSpeed;
        public float SedimentCapacity;
        public float ErosionAggressiveness;
        public float RegionBlendWeight;
        public uint SeedSalt;
    }

    internal struct ErosionBakeMetrics
    {
        public int SectorCount;
        public int CompletedSectors;
        public int NaNSectors;
        public int RuntimeScannerHits;
        public int DropletsSimulated;
        public int SeamNorthTransfers;
        public int SeamSouthTransfers;
        public int SeamEastTransfers;
        public int SeamWestTransfers;
        public uint WarningFlags;
        public float MaxDepthCarved;
        public float TotalSedimentTransported;
        public double MockHeightmapMilliseconds;
        public double DropletMilliseconds;
        public double MacroMilliseconds;
        public double SerializationMilliseconds;
    }
}
#endif
