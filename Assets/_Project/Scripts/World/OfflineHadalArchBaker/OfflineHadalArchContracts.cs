using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalArchBaker
{
    internal static class OfflineHadalArchContractsLayout
    {
        public const int SdfShapeDTOStrideBytes = 64;
        public const int HadalArchVertexDTOStrideBytes = 64;
        public const int HadalArchBakeConfigDTOStrideBytes = 128;
        public const int HadalArchBakeTelemetryEntryStrideBytes = 64;
        public const int HadalStaticGeometryRollbackExclusionDTOStrideBytes = 32;
    }

    /// <summary>
    /// Constants for the Editor-only Hadal SDF arch bake pipeline.
    /// </summary>
    public static class HadalArchBakeConstants
    {
        public const int TelemetryFrames = 300;
        public const int DefaultResolution = 64;
        public const int MaxPreviewShapes = 64;
        public const int CriticalLod0TriangleBudget = 50000;
        public const uint ReportVersion = 1u;
        public const uint WarningLayoutMismatch = 1u << 0;
        public const uint WarningCapacityClamp = 1u << 1;
        public const uint WarningNonFiniteFallback = 1u << 2;
        public const uint WarningTriangleBudgetExceeded = 1u << 3;
        public const uint WarningRealtimeCsgDebt = 1u << 4;
        public const uint WarningBoundaryShellSealed = 1u << 5;
        public const uint WarningDegenerateTriangleRejected = 1u << 6;
        public const uint RollbackExcludedFlag = 1u << 31;
        public const uint DumpMagic = 0x48414441u;
    }

    /// <summary>
    /// Primitive SDF shape type consumed by Burst jobs.
    /// </summary>
    public enum SdfShapeType : uint
    {
        Sphere = 0u,
        Box = 1u,
        Torus = 2u,
        Cylinder = 3u
    }

    /// <summary>
    /// Boolean operation used when composing an SDF graph.
    /// </summary>
    public enum SdfBooleanOperation : uint
    {
        Add = 0u,
        Subtract = 1u,
        Intersect = 2u,
        SmoothUnion = 3u
    }

    /// <summary>
    /// ARM64-safe SDF shape record. It has only raw unmanaged fields for Burst pointer traversal.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalArchContractsLayout.SdfShapeDTOStrideBytes)]
    public struct SdfShapeDTO
    {
        [FieldOffset(0)] public uint ShapeType;
        [FieldOffset(4)] public uint Operation;
        [FieldOffset(8)] public float3 Position;
        [FieldOffset(20)] public float3 Extents;
        [FieldOffset(32)] public float BlendRadius;
        [FieldOffset(36)] public float NoiseWeight;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint MaterialHash;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    /// <summary>
    /// Interleaved vertex row written directly into Unity Mesh vertex buffer stream 0.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalArchContractsLayout.HadalArchVertexDTOStrideBytes)]
    public struct HadalArchVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public uint PackedColor;
        [FieldOffset(52)] public float3 Uv3AupLocal;
    }

    /// <summary>
    /// Bake configuration copied into Burst jobs. Values are continuous; no binary quality switch exists.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalArchContractsLayout.HadalArchBakeConfigDTOStrideBytes)]
    public struct HadalArchBakeConfigDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public double3 VolumeOriginAup;
        [FieldOffset(48)] public int3 Resolution;
        [FieldOffset(60)] public float VoxelSize;
        [FieldOffset(64)] public float GlobalQualityWeight;
        [FieldOffset(68)] public float NoiseFrequency;
        [FieldOffset(72)] public float NoiseAmplitude;
        [FieldOffset(76)] public float CavityRayDistance;
        [FieldOffset(80)] public int CavityRayCount;
        [FieldOffset(84)] public uint Seed;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public int ShapeCount;
        [FieldOffset(96)] public float Lod1KeepRatio;
        [FieldOffset(100)] public float Lod2KeepRatio;
        [FieldOffset(104)] public float SurfaceBand;
        [FieldOffset(108)] public float3 NoiseSeedJitter;
        [FieldOffset(120)] public ulong _pad2;
    }

    /// <summary>
    /// Fixed black-box telemetry row for the last 300 offline bake stages.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalArchContractsLayout.HadalArchBakeTelemetryEntryStrideBytes)]
    public struct HadalArchBakeTelemetryEntry
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int VoxelCount;
        [FieldOffset(32)] public int VertexCount;
        [FieldOffset(36)] public int IndexCount;
        [FieldOffset(40)] public float SdfMilliseconds;
        [FieldOffset(44)] public float ExtractionMilliseconds;
        [FieldOffset(48)] public uint WarningFlags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint DumpReason;
        [FieldOffset(60)] public uint Stage;
    }

    /// <summary>
    /// Static geometry exclusion row. Generated arches are immutable scenery, not rollback state.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineHadalArchContractsLayout.HadalStaticGeometryRollbackExclusionDTOStrideBytes)]
    public struct HadalStaticGeometryRollbackExclusionDTO
    {
        [FieldOffset(0)] public uint MeshHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong AssetGuidLow;
        [FieldOffset(16)] public ulong AssetGuidHigh;
        [FieldOffset(24)] public ulong _pad0;
    }

    /// <summary>
    /// Small deterministic math helpers for the Hadal arch baker.
    /// </summary>
    public static class HadalArchBakeMath
    {
        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        public static uint HashFnv1a(double3 aup)
        {
            uint hash = 2166136261u;
            hash = HashDouble(aup.x, hash);
            hash = HashDouble(aup.y, hash);
            hash = HashDouble(aup.z, hash);
            return hash == 0u ? 1u : hash;
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

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        public static float3 LocalizeAup(double3 sampleAup, double3 centerAup)
        {
            double3 delta = IsFinite(sampleAup) && IsFinite(centerAup) ? sampleAup - centerAup : double3.zero;
            double x = math.clamp(delta.x, -100000.0d, 100000.0d);
            double y = math.clamp(delta.y, -100000.0d, 100000.0d);
            double z = math.clamp(delta.z, -100000.0d, 100000.0d);
            return new float3((float)x, (float)y, (float)z);
        }

        public static float3 BuildNoiseSeedJitter(uint seed)
        {
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(Mix(seed));
            return random.NextFloat3(new float3(0f), new float3(27.64865f));
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
