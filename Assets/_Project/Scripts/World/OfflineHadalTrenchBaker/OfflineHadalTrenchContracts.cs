using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalTrenchBaker
{
    internal static class OfflineHadalTrenchContractsLayout
    {
        public const int FaultLineParamsDTOStrideBytes = 64;
        public const int ThermalVentSpawnDTOStrideBytes = 64;
        public const int HadalTrenchBakeConfigDTOStrideBytes = 160;
        public const int HadalTrenchChunkHeaderDTOStrideBytes = 160;
        public const int HadalTrenchRleRunDTOStrideBytes = 16;
        public const int HadalTrenchAdaptiveBlockDTOStrideBytes = 32;
        public const int HadalTrenchBakeTelemetryEntryStrideBytes = 64;
        public const int HadalTrenchRollbackExclusionDTOStrideBytes = 32;
    }

    public static class HadalTrenchBakeConstants
    {
        public const int TelemetryFrames = 300;
        public const int DefaultVoxelResolution = 256;
        public const int DefaultFaultGridX = 32;
        public const int DefaultFaultGridZ = 32;
        public const int MaxPreviewFaults = 4096;
        public const uint ReportVersion = 1u;
        public const uint FileVersion = 1u;
        public const uint HeaderBytes = 160u;
        public const uint PayloadEndianMarker = 0x01020304u;
        public const uint PayloadSectionAlignmentBytes = 8u;
        public const uint PayloadChecksumFnv1A64 = 1u;
        public const uint PayloadSchemaHash = 0xA2410002u;
        public const uint H8BinMagic = 0x54523848u;
        public const uint DumpMagic = 0x44523848u;
        public const uint WarningLayoutMismatch = 1u << 0;
        public const uint WarningNonFiniteDensity = 1u << 1;
        public const uint WarningCompressionExpanded = 1u << 2;
        public const uint WarningManualGeometryFound = 1u << 3;
        public const uint WarningLz4FallbackRaw = 1u << 4;
        public const uint RollbackExcludedFlag = 1u << 31;
    }

    public enum HadalTrenchCompressionMode : uint
    {
        None = 0u,
        Rle = 1u,
        RleLz4Block = 2u
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.FaultLineParamsDTOStrideBytes)]
    public struct FaultLineParamsDTO
    {
        [FieldOffset(0)] public double3 StartAUP;
        [FieldOffset(24)] public double3 EndAUP;
        [FieldOffset(48)] public float Depth;
        [FieldOffset(52)] public float Width;
        [FieldOffset(56)] public float NoiseIntensity;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.ThermalVentSpawnDTOStrideBytes)]
    public struct ThermalVentSpawnDTO
    {
        [FieldOffset(0)] public double3 PositionAUP;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float HeatCelsius;
        [FieldOffset(32)] public float PressureKPa;
        [FieldOffset(36)] public float LootAffinity01;
        [FieldOffset(40)] public uint FaultHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchBakeConfigDTOStrideBytes)]
    public struct HadalTrenchBakeConfigDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAUP;
        [FieldOffset(24)] public double3 WorldMinAUP;
        [FieldOffset(48)] public double3 WorldMaxAUP;
        [FieldOffset(72)] public double SeaFloorAUPY;
        [FieldOffset(80)] public int3 Resolution;
        [FieldOffset(92)] public float VoxelSizeMeters;
        [FieldOffset(96)] public float VoronoiCellSizeMeters;
        [FieldOffset(100)] public float DefaultDepthMeters;
        [FieldOffset(104)] public float DefaultWidthMeters;
        [FieldOffset(108)] public float NoiseIntensity;
        [FieldOffset(112)] public float NoiseFrequency;
        [FieldOffset(116)] public float GlobalQualityWeight;
        [FieldOffset(120)] public uint Seed;
        [FieldOffset(124)] public int FaultGridX;
        [FieldOffset(128)] public int FaultGridZ;
        [FieldOffset(132)] public int FaultCount;
        [FieldOffset(136)] public int MaxVentCount;
        [FieldOffset(140)] public uint Flags;
        [FieldOffset(144)] public ulong _pad0;
        [FieldOffset(152)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchChunkHeaderDTOStrideBytes)]
    public struct HadalTrenchChunkHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public int3 Resolution;
        [FieldOffset(24)] public double3 SectorOriginAUP;
        [FieldOffset(48)] public float VoxelSizeMeters;
        [FieldOffset(52)] public uint CompressionMode;
        [FieldOffset(56)] public int CompressedBytes;
        [FieldOffset(60)] public int RleRunCount;
        [FieldOffset(64)] public int VentCount;
        [FieldOffset(68)] public int AdaptiveBlockCount;
        [FieldOffset(72)] public double MaxDepthMeters;
        [FieldOffset(80)] public double ExcavatedCubicMeters;
        [FieldOffset(88)] public ulong DensityPayloadOffset;
        [FieldOffset(96)] public ulong VentPayloadOffset;
        [FieldOffset(104)] public ulong AdaptivePayloadOffset;
        [FieldOffset(112)] public ulong PayloadHash;
        [FieldOffset(120)] public uint HeaderBytes;
        [FieldOffset(124)] public uint EndianMarker;
        [FieldOffset(128)] public int UncompressedBytes;
        [FieldOffset(132)] public int DensityPreludeBytes;
        [FieldOffset(136)] public ulong TotalFileBytes;
        [FieldOffset(144)] public uint SectionAlignmentBytes;
        [FieldOffset(148)] public uint ChecksumType;
        [FieldOffset(152)] public uint SchemaHash;
        [FieldOffset(156)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchRleRunDTOStrideBytes)]
    public struct HadalTrenchRleRunDTO
    {
        [FieldOffset(0)] public uint StartVoxel;
        [FieldOffset(4)] public uint RunLength;
        [FieldOffset(8)] public sbyte Density;
        [FieldOffset(9)] public byte MaterialId;
        [FieldOffset(10)] public ushort Flags;
        [FieldOffset(12)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchAdaptiveBlockDTOStrideBytes)]
    public struct HadalTrenchAdaptiveBlockDTO
    {
        [FieldOffset(0)] public int3 MinVoxel;
        [FieldOffset(12)] public byte BlockSizeVoxels;
        [FieldOffset(13)] public sbyte MinDensity;
        [FieldOffset(14)] public sbyte MaxDensity;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(16)] public uint VoxelCount;
        [FieldOffset(20)] public float ErrorMeters;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchBakeTelemetryEntryStrideBytes)]
    public struct HadalTrenchBakeTelemetryEntry
    {
        [FieldOffset(0)] public double3 SectorOriginAUP;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int FaultCount;
        [FieldOffset(32)] public int VoxelCount;
        [FieldOffset(36)] public int RleRunCount;
        [FieldOffset(40)] public float CarvingMilliseconds;
        [FieldOffset(44)] public float SerializationMilliseconds;
        [FieldOffset(48)] public uint WarningFlags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint DumpReason;
        [FieldOffset(60)] public uint Stage;
    }

    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalTrenchContractsLayout.HadalTrenchRollbackExclusionDTOStrideBytes)]
    public struct HadalTrenchRollbackExclusionDTO
    {
        [FieldOffset(0)] public uint StaticVoxelHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong FileGuidLow;
        [FieldOffset(16)] public ulong FileGuidHigh;
        [FieldOffset(24)] public ulong _pad0;
    }

    public static class HadalTrenchBakeMath
    {
        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static float3 LocalizeAup(double3 sampleAUP, double3 anchorAUP)
        {
            double3 delta = IsFinite(sampleAUP) && IsFinite(anchorAUP) ? sampleAUP - anchorAUP : double3.zero;
            return new float3(
                (float)math.clamp(delta.x, -1000000.0d, 1000000.0d),
                (float)math.clamp(delta.y, -1000000.0d, 1000000.0d),
                (float)math.clamp(delta.z, -1000000.0d, 1000000.0d));
        }

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        public static uint Hash3(int x, int y, int z, uint seed)
        {
            uint hash = seed ^ 2166136261u;
            hash = Mix(hash ^ (uint)(x * 73856093));
            hash = Mix(hash ^ (uint)(y * 19349663));
            hash = Mix(hash ^ (uint)(z * 83492791));
            return hash;
        }

        public static float Hash01(int x, int y, int z, uint seed)
        {
            return (Hash3(x, y, z, seed) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        public static uint HashFault(in FaultLineParamsDTO fault)
        {
            uint hash = 2166136261u;
            hash = HashDouble(fault.StartAUP.x, hash);
            hash = HashDouble(fault.StartAUP.z, hash);
            hash = HashDouble(fault.EndAUP.x, hash);
            hash = HashDouble(fault.EndAUP.z, hash);
            return Mix(hash ^ math.asuint(fault.Width) ^ (math.asuint(fault.Depth) << 1));
        }

        public static sbyte QuantizeDensity(float density)
        {
            float safe = math.isfinite(density) ? density : 127f;
            return (sbyte)math.clamp((int)math.round(safe), -127, 127);
        }

        private static uint HashDouble(double value, uint hash)
        {
            long bits = math.aslong(value);
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)((ulong)bits >> shift);
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
