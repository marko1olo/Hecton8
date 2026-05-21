using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.StaticCaveSdfBaker
{
    /// <summary>
    /// Constants for immutable static cave SDF volume bake artifacts.
    /// </summary>
    public static class StaticCaveSdfConstants
    {
        public const int HeaderSizeBytes = 64;
        public const int TelemetryFrames = 300;
        public const int DefaultResolution = 128;
        public const int MaxResolution = 256;
        public const int BvhLeafTriangleCount = 8;
        public const int BvhMaxDepth = 40;
        public const int BakeReportVersion = 1;
        public const int CriticalBudgetBytes = 16 * 1024 * 1024;
        public const uint RollbackExcludedFlag = 1u << 31;
        public const uint WarningLayoutMismatch = 1u << 0;
        public const uint WarningFileBudgetExceeded = 1u << 1;
        public const uint WarningBvhCapacityExceeded = 1u << 2;
        public const uint WarningNonFiniteFallback = 1u << 3;
        public const uint WarningScannerFinding = 1u << 4;
        public const uint WarningMockBenchmark = 1u << 5;
        public const uint DumpMagic = 0x53444642u;
    }

    /// <summary>
    /// ARM64-stable triangle stream consumed by Burst SDF jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct TriangleDTO
    {
        [FieldOffset(0)] public float3 V0;
        [FieldOffset(12)] public float3 V1;
        [FieldOffset(24)] public float3 V2;
        [FieldOffset(36)] public float3 Normal;
    }

    /// <summary>
    /// Flat BVH node. Leaves use TriangleCount > 0; internal nodes use Left/Right.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BvhNodeDTO
    {
        [FieldOffset(0)] public float3 BoundsMin;
        [FieldOffset(12)] public float3 BoundsMax;
        [FieldOffset(24)] public int Left;
        [FieldOffset(28)] public int Right;
        [FieldOffset(32)] public int TriangleStart;
        [FieldOffset(36)] public int TriangleCount;
        [FieldOffset(40)] public int Depth;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    /// <summary>
    /// Temporary BVH construction stack range. Raw fields only; no properties.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BvhBuildRangeDTO
    {
        [FieldOffset(0)] public int Start;
        [FieldOffset(4)] public int Count;
        [FieldOffset(8)] public int NodeIndex;
        [FieldOffset(12)] public int Depth;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    /// <summary>
    /// Bake configuration copied into Burst jobs. Output quality is continuous; no binary tier switch exists.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct StaticCaveSdfBakeConfigDTO
    {
        [FieldOffset(0)] public double3 AnchorAup;
        [FieldOffset(24)] public float3 BoundsMin;
        [FieldOffset(36)] public float3 BoundsMax;
        [FieldOffset(48)] public int3 Resolution;
        [FieldOffset(60)] public float MaxSdfDistance;
        [FieldOffset(64)] public float GlobalQualityWeight;
        [FieldOffset(68)] public int SubMeshIndex;
        [FieldOffset(72)] public int VoxelCount;
        [FieldOffset(76)] public int TriangleCount;
        [FieldOffset(80)] public uint Flags;
        [FieldOffset(84)] public uint _pad0;
        [FieldOffset(88)] public ulong _pad1;
    }

    /// <summary>
    /// CSV profile row for static cave SDF baking.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct StaticCaveSdfProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public int Resolution;
        [FieldOffset(8)] public float NarrowBandMeters;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public int SubMeshIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
        [FieldOffset(32)] public ulong _pad1;
        [FieldOffset(40)] public ulong _pad2;
    }

    /// <summary>
    /// Fixed-size offline black-box telemetry row for the last 300 bake stages.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct StaticCaveSdfTelemetryEntry
    {
        [FieldOffset(0)] public double3 AnchorAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int VoxelCount;
        [FieldOffset(32)] public int TriangleCount;
        [FieldOffset(36)] public int BvhNodeCount;
        [FieldOffset(40)] public float BvhMilliseconds;
        [FieldOffset(44)] public float SdfMilliseconds;
        [FieldOffset(48)] public float CompressMilliseconds;
        [FieldOffset(52)] public uint WarningFlags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Stage;
    }

    /// <summary>
    /// Immutable SDF data is excluded from rollback hashing; only entity positions are network truth.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StaticCaveSdfRollbackExclusionDTO
    {
        [FieldOffset(0)] public uint AssetHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong PayloadBytes;
        [FieldOffset(16)] public ulong Checksum64;
        [FieldOffset(24)] public ulong _pad0;
    }

}
