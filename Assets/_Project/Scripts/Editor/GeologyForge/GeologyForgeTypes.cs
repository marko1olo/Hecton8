using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.GeologyForge
{
    internal static class GeologyForgeConstants
    {
        public const int LodCount = 3;
        public const int VertexStrideBytes = 32;
        public const int DefaultResolution = 32;
        public const int MinimumResolution = 12;
        public const int MaximumResolution = 72;
        public const int DefaultAoRays = 24;
        public const int MaximumAoRays = 96;
        public const int MaximumVariations = 500;
        public const int MaximumAsyncResultPreallocation = 5000;
        public const int BlackBoxFrameCount = 300;
        public const int Lod0TriangleBudget = 15000;
        public const int Lod1TriangleBudget = 7500;
        public const int Lod2TriangleBudget = 1500;
        public const int CollisionTriangleBudget = 192;
        public const int CollisionProxyTriangleCount = 12;
        public const float OccluderStaticMinimumVolumeCubicMeters = 2f;
        public const float MaximumRadiusMeters = 32f;
        public const float MaximumHeightScale = 12f;
        public const float MaximumFrequency = 16f;
        public const float MaximumNoiseAmplitudeMeters = 8f;
        public const uint WarningEmptySurface = 1u << 0;
        public const uint WarningTriangleBudgetExceeded = 1u << 1;
        public const uint WarningNonFiniteTelemetry = 1u << 2;
        public const uint DumpMagic = 0x47454F46u;
        public const uint DumpReasonException = 1u;
        public const uint DumpReasonNonFinite = 2u;
        public const uint ManifestMagic = 0x38474D48u; // HMG8 little-endian
        public const uint ManifestVersion = 1u;
        public const uint ManifestFlagBrgReady = 1u << 0;
        public const string MeshOutputFolder = "Assets/_Project/BakedGeometry/Geology";
        public const string PrefabOutputFolder = "Assets/_Project/BakedGeometry/Geology/Prefabs";
        public const string ManifestPath = "Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom";
        public const string CsvPath = "Assets/_Project/Data/Geology/geology_generation_profiles.csv";
        public const string BakeReportPath = "Docs/Reports/GEOLOGY_BAKE_REPORT.json";
        public const string LayoutAuditReportPath = "Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json";
        public const string ScannerReportPath = "Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json";
        public const string DumpPath = "Docs/AgentLogs/Dump_1606_GeologyForge.bin";
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct GeologyVertex32
    {
        [FieldOffset(0)]
        public float3 Position; // 12 bytes

        [FieldOffset(12)]
        public float3 Normal;   // 12 bytes

        [FieldOffset(24)]
        public uint ColorRgba;  // 4 bytes, Color32 byte order: r,g,b,a

        [FieldOffset(28)]
        public uint Uv0Packed;  // 4 bytes, UNorm16 x 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct GeologyRawVertex
    {
        [FieldOffset(0)]
        public float3 Position;

        [FieldOffset(12)]
        public float3 Normal;

        [FieldOffset(24)]
        public float4 Tangent;

        [FieldOffset(40)]
        public float2 Uv;

        [FieldOffset(48)]
        public float AmbientOcclusion;

        [FieldOffset(52)]
        public uint Flags;

        [FieldOffset(56)]
        public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct GeologySeedDTO
    {
        [FieldOffset(0)]
        public double3 SectorAup;

        [FieldOffset(24)]
        public uint Seed;

        [FieldOffset(28)]
        public float RadiusMeters;

        [FieldOffset(32)]
        public float HeightScale;

        [FieldOffset(36)]
        public float Frequency;

        [FieldOffset(40)]
        public float NoiseAmplitude;

        [FieldOffset(44)]
        public float RidgedWeight;

        [FieldOffset(48)]
        public float VoronoiWeight;

        [FieldOffset(52)]
        public float IsoLevel;

        [FieldOffset(56)]
        public float GlobalQualityWeight;

        [FieldOffset(60)]
        public uint ProfileHash;
    }

    internal struct GeologyBakeProfile
    {
        public FixedString64Bytes Name;
        public double3 SectorAup;
        public uint Seed;
        public int Resolution;
        public int Octaves;
        public int AmbientOcclusionRays;
        public int Variations;
        public float RadiusMeters;
        public float HeightScale;
        public float Frequency;
        public float NoiseAmplitude;
        public float RidgedWeight;
        public float VoronoiWeight;
        public float IsoLevel;
        public float GlobalQualityWeight;
        public int Lod0Budget;
        public int Lod1Budget;
        public int Lod2Budget;
    }

    internal struct GeologyBakeMetrics
    {
        public FixedString64Bytes Name;
        public uint Seed;
        public int Lod0Triangles;
        public int Lod1Triangles;
        public int Lod2Triangles;
        public int CollisionTriangles;
        public int VertexStrideBytes;
        public double SdfMilliseconds;
        public double ExtractMilliseconds;
        public double AttributeMilliseconds;
        public double AoMilliseconds;
        public double SerializationMilliseconds;
        public uint WarningFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct GeologyMeshManifestHeader
    {
        [FieldOffset(0)]
        public uint Magic;

        [FieldOffset(4)]
        public uint Version;

        [FieldOffset(8)]
        public uint RecordCount;

        [FieldOffset(12)]
        public uint RecordSize;

        [FieldOffset(16)]
        public uint HeaderSize;

        [FieldOffset(20)]
        public uint VertexStrideBytes;

        [FieldOffset(24)]
        public uint LodCount;

        [FieldOffset(28)]
        public uint Flags;

        [FieldOffset(32)]
        public ulong Reserved0;

        [FieldOffset(40)]
        public ulong Reserved1;

        [FieldOffset(48)]
        public ulong Reserved2;

        [FieldOffset(56)]
        public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct GeologyMeshManifestRecord
    {
        [FieldOffset(0)]
        public double3 SectorAup;

        [FieldOffset(24)]
        public uint Seed;

        [FieldOffset(28)]
        public uint ProfileHash;

        [FieldOffset(32)]
        public int Lod0Triangles;

        [FieldOffset(36)]
        public int Lod1Triangles;

        [FieldOffset(40)]
        public int Lod2Triangles;

        [FieldOffset(44)]
        public uint VertexStrideBytes;

        [FieldOffset(48)]
        public float3 BoundsCenter;

        [FieldOffset(60)]
        public float3 BoundsExtents;

        [FieldOffset(72)]
        public ulong Lod0GuidHigh;

        [FieldOffset(80)]
        public ulong Lod0GuidLow;

        [FieldOffset(88)]
        public ulong Lod1GuidHigh;

        [FieldOffset(96)]
        public ulong Lod1GuidLow;

        [FieldOffset(104)]
        public ulong Lod2GuidHigh;

        [FieldOffset(112)]
        public ulong Lod2GuidLow;

        [FieldOffset(120)]
        public uint Flags;

        [FieldOffset(124)]
        public uint Variation;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct GeologyBakeDumpHeader
    {
        [FieldOffset(0)]
        public uint Magic;

        [FieldOffset(4)]
        public uint EntryCount;

        [FieldOffset(8)]
        public uint EntrySize;

        [FieldOffset(12)]
        public uint Cursor;

        [FieldOffset(16)]
        public uint Reason;

        [FieldOffset(20)]
        public uint Reserved0;

        [FieldOffset(24)]
        public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct GeologyBakeTelemetryEntry
    {
        [FieldOffset(0)]
        public double3 SectorAup;

        [FieldOffset(24)]
        public uint Seed;

        [FieldOffset(28)]
        public uint Stage;

        [FieldOffset(32)]
        public float StageMilliseconds;

        [FieldOffset(36)]
        public int RawVertexCount;

        [FieldOffset(40)]
        public int Lod0Triangles;

        [FieldOffset(44)]
        public int Lod1Triangles;

        [FieldOffset(48)]
        public int Lod2Triangles;

        [FieldOffset(52)]
        public uint WarningFlags;

        [FieldOffset(56)]
        public uint StateHash;

        [FieldOffset(60)]
        public uint DumpReason;
    }
}
