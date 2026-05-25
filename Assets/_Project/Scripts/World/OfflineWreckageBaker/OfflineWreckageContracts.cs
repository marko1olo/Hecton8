using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World.OfflineWreckageBaker
{
    internal static class OfflineWreckageContractsLayout
    {
        public const int MeshDamageStateMappingDTOStrideBytes = 32;
        public const int OfflineWreckageBakeVertexDTOStrideBytes = 64;
        public const int WreckageDeformationProfileDTOStrideBytes = 64;
        public const int OfflineWreckageBakeCounters64StrideBytes = 64;
        public const int OfflineWreckageTelemetryEntryStrideBytes = 64;
    }

    /// <summary>
    /// Constants for the offline structural wreckage bake pipeline.
    /// </summary>
    public static class OfflineWreckageBakeConstants
    {
        public const int DamageStateCount = 3;
        public const int MaxCollisionHullVertices = 256;
        public const int SupportHullPointCount = 8;
        public const int TelemetryFrames = 300;
        public const uint MappingLayoutVersion = 1u;
        public const uint BakeReportVersion = 1u;
        public const uint WarningHullBudgetExceeded = 1u << 0;
        public const uint WarningDegenerateTriangles = 1u << 1;
        public const uint WarningNonFiniteFallback = 1u << 2;
        public const uint WarningHullBoundsExpanded = 1u << 3;
    }

    /// <summary>
    /// Runtime damage state index. The network/rollback truth is this byte-scale state, not mesh geometry.
    /// </summary>
    public enum OfflineWreckageDamageState : byte
    {
        Pristine = 0,
        Stressed = 1,
        Ruptured = 2,
        Collapsed = 3
    }

    /// <summary>
    /// ARM64-safe mapping from a pristine mesh hash to three immutable damaged mesh hashes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineWreckageContractsLayout.MeshDamageStateMappingDTOStrideBytes)]
    public struct MeshDamageStateMappingDTO
    {
        [FieldOffset(0)] public uint PristineMeshHash;
        [FieldOffset(4)] public uint StressedMeshHash;
        [FieldOffset(8)] public uint RupturedMeshHash;
        [FieldOffset(12)] public uint CollapsedMeshHash;
        [FieldOffset(16)] public ulong _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    /// <summary>
    /// Interleaved 64-byte vertex record written directly into Unity MeshData.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineWreckageContractsLayout.OfflineWreckageBakeVertexDTOStrideBytes)]
    public struct OfflineWreckageBakeVertexDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public uint PackedColor;
        [FieldOffset(52)] public float3 Uv3AupLocal;
    }

    /// <summary>
    /// Single deformation profile parsed from CSV without string splitting.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineWreckageContractsLayout.WreckageDeformationProfileDTOStrideBytes)]
    public struct WreckageDeformationProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float GlobalQualityWeight;
        [FieldOffset(8)] public float BlastRadius;
        [FieldOffset(12)] public float TearThreshold;
        [FieldOffset(16)] public float ShearTorsion;
        [FieldOffset(20)] public float ScorchIntensity;
        [FieldOffset(24)] public float CollapseCompression;
        [FieldOffset(28)] public float NoiseAmplitude;
        [FieldOffset(32)] public float3 ShearAxis;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    /// <summary>
    /// Single cache-line counter row for chained offline bake jobs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineWreckageContractsLayout.OfflineWreckageBakeCounters64StrideBytes)]
    public struct OfflineWreckageBakeCounters64
    {
        [FieldOffset(0)] public int ActiveVertexCount;
        [FieldOffset(4)] public int TornVertexCount;
        [FieldOffset(8)] public int DegenerateTriangleCount;
        [FieldOffset(12)] public int HullVertexCount;
        [FieldOffset(16)] public uint WarningFlags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
        [FieldOffset(32)] public ulong _pad2;
        [FieldOffset(40)] public ulong _pad3;
        [FieldOffset(48)] public ulong _pad4;
        [FieldOffset(56)] public ulong _pad5;
    }

    /// <summary>
    /// Bake telemetry entry for the fixed 300-entry offline black box.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineWreckageContractsLayout.OfflineWreckageTelemetryEntryStrideBytes)]
    public struct OfflineWreckageTelemetryEntry
    {
        [FieldOffset(0)] public double3 ModuleAup;
        [FieldOffset(24)] public uint MeshHash;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public int VertexCount;
        [FieldOffset(36)] public int IndexCount;
        [FieldOffset(40)] public int TornVertexCount;
        [FieldOffset(44)] public int HullVertexCount;
        [FieldOffset(48)] public float BurstMicroseconds;
        [FieldOffset(52)] public uint WarningFlags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint DamageState;
    }

    /// <summary>
    /// Small utility surface for stable hashes and AUP-local blast math.
    /// </summary>
    public static class OfflineWreckageBakeMath
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

        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static float3 LocalizeBlastEpicenter(double3 blastAup, double3 moduleAup)
        {
            double3 delta = IsFinite(blastAup) && IsFinite(moduleAup) ? blastAup - moduleAup : double3.zero;
            double clampedX = math.clamp(delta.x, -100000.0d, 100000.0d);
            double clampedY = math.clamp(delta.y, -100000.0d, 100000.0d);
            double clampedZ = math.clamp(delta.z, -100000.0d, 100000.0d);
            return new float3((float)clampedX, (float)clampedY, (float)clampedZ);
        }

        public static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }
    }
}
