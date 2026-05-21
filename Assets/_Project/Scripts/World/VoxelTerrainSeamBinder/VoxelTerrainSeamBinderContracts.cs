using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.VoxelTerrainSeamBinder
{
    /// <summary>
    /// Static constants for offline voxel/heightmap seam binding.
    /// Runtime uses baked meshes only.
    /// </summary>
    public static class VoxelTerrainSeamConstants
    {
        public const int TelemetryFrames = 300;
        public const int ReportVersion = 1;
        public const int StitchedVertexStrideBytes = 32;
        public const int SeamProfileCapacity = 16;
        public const int MockResolution = 500;
        public const uint RollbackExcludedTrue = 1u;
        public const uint RollbackFenceMagic = 0x46535456u; // VTSF
        public const uint RollbackFenceVersion = 1u;
        public const uint LittleEndianMarker = 0x01020304u;
        public const uint WarningMissingBoundary = 1u << 0;
        public const uint WarningNonFiniteFallback = 1u << 1;
        public const uint WarningNoSnaps = 1u << 2;
        public const uint WarningLayoutMismatch = 1u << 3;
        public const uint WarningLodMissing = 1u << 4;
    }

    /// <summary>
    /// Interleaved ARM64/GPU-safe vertex record. Size: 32 bytes.
    /// Layout: position 12, normal 12, Color32 4, UV0 UNorm16x2 4.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeamBindVertex32
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public uint PackedColor;
        [FieldOffset(28)] public uint PackedUv0;
    }

    /// <summary>
    /// Boundary vertex payload inserted into the spatial hash. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeamBoundaryVertex64
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public float3 Normal;
        [FieldOffset(48)] public int VertexIndex;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float BoundaryWeight;
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>
    /// Terrain-side snap result. VoxelVertexIndex below zero means no seam candidate was accepted.
    /// Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeamSnapResult64
    {
        [FieldOffset(0)] public float3 OriginalLocalPosition;
        [FieldOffset(12)] public int VoxelVertexIndex;
        [FieldOffset(16)] public float3 SnappedLocalPosition;
        [FieldOffset(28)] public float DistanceMeters;
        [FieldOffset(32)] public float3 BlendedNormal;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    /// <summary>
    /// Designer-authored seam recipe parsed from seam_binding_profiles.csv. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeamBindingProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float GlobalQualityWeight;
        [FieldOffset(8)] public float SnapRadiusMeters;
        [FieldOffset(12)] public float NormalBlendDistanceMeters;
        [FieldOffset(16)] public float TextureGradientFalloffMeters;
        [FieldOffset(20)] public float SpatialCellSizeMeters;
        [FieldOffset(24)] public float LodContinuityBias;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float3 PreviewLineColor;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    /// <summary>
    /// Aggregated per-LOD bake counters. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeamBindCounters64
    {
        [FieldOffset(0)] public int TerrainVertexCount;
        [FieldOffset(4)] public int VoxelVertexCount;
        [FieldOffset(8)] public int TerrainIndexCount;
        [FieldOffset(12)] public int VoxelIndexCount;
        [FieldOffset(16)] public int SnappedVertexCount;
        [FieldOffset(20)] public int MissingBoundaryCount;
        [FieldOffset(24)] public float MaxDistanceErrorMeters;
        [FieldOffset(28)] public float BurstMicroseconds;
        [FieldOffset(32)] public uint WarningFlags;
        [FieldOffset(36)] public uint CriticalWarning;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    /// <summary>
    /// Fixed black-box telemetry entry for the last 300 offline bake stages. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeamBindTelemetryEntry
    {
        [FieldOffset(0)] public double3 TerrainRootAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int TerrainVertexCount;
        [FieldOffset(32)] public int VoxelVertexCount;
        [FieldOffset(36)] public int SnappedVertexCount;
        [FieldOffset(40)] public float MaxDistanceErrorMeters;
        [FieldOffset(44)] public float BurstMicroseconds;
        [FieldOffset(48)] public uint WarningFlags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Stage;
        [FieldOffset(60)] public uint DumpReason;
    }

    /// <summary>
    /// Immutable mesh mapping record documenting rollback/netcode exclusion for baked geometry. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SeamMeshRollbackFenceDTO
    {
        [FieldOffset(0)] public uint TerrainMeshHash;
        [FieldOffset(4)] public uint VoxelMeshHash;
        [FieldOffset(8)] public uint StitchedMeshHash;
        [FieldOffset(12)] public uint RollbackExcluded;
        [FieldOffset(16)] public uint Magic;
        [FieldOffset(20)] public uint Version;
        [FieldOffset(24)] public uint EndianMarker;
        [FieldOffset(28)] public uint Reserved;
    }

    /// <summary>
    /// Pure unmanaged helpers shared by Burst jobs and editor reporting.
    /// </summary>
    public static class VoxelTerrainSeamMath
    {
        public static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        public static uint HashBytes(byte value, uint hash)
        {
            byte c = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
            if (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r')
                return hash;

            hash ^= c;
            hash *= 16777619u;
            return hash;
        }

        public static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        public static uint ReplaceAlpha(uint packedColor, byte alpha)
        {
            return (packedColor & 0x00FFFFFFu) | ((uint)alpha << 24);
        }

        public static uint PackUvUnorm16(float2 uv)
        {
            float2 safe = math.select(float2.zero, math.saturate(uv), math.all(math.isfinite(uv)));
            uint x = (uint)math.clamp((int)math.round(safe.x * 65535f), 0, 65535);
            uint y = (uint)math.clamp((int)math.round(safe.y * 65535f), 0, 65535);
            return x | (y << 16);
        }

        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static uint HashAscii(string text)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(text))
                return Hash(hash);

            for (int i = 0; i < text.Length; i++)
                hash = HashBytes((byte)text[i], hash);

            return Hash(hash);
        }

        public static long HashCell(double3 aup, double cellSize)
        {
            double safeCell = math.max(cellSize, 0.0001d);
            long x = (long)math.floor(aup.x / safeCell);
            long y = (long)math.floor(aup.y / safeCell);
            long z = (long)math.floor(aup.z / safeCell);
            return HashCellIndices(x, y, z);
        }

        public static long HashCellIndices(long x, long y, long z)
        {
            unchecked
            {
                ulong ux = (ulong)(x * 73856093L);
                ulong uy = (ulong)(y * 19349663L);
                ulong uz = (ulong)(z * 83492791L);
                ulong mixed = ux ^ uy ^ uz;
                mixed ^= mixed >> 33;
                mixed *= 0xff51afd7ed558ccdUL;
                mixed ^= mixed >> 33;
                mixed *= 0xc4ceb9fe1a85ec53UL;
                mixed ^= mixed >> 33;
                return (long)mixed;
            }
        }

        public static void ResolveCellIndices(double3 aup, double cellSize, out long x, out long y, out long z)
        {
            double safeCell = math.max(cellSize, 0.0001d);
            x = (long)math.floor(aup.x / safeCell);
            y = (long)math.floor(aup.y / safeCell);
            z = (long)math.floor(aup.z / safeCell);
        }
    }
}
