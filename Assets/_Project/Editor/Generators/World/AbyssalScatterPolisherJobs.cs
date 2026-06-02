#if UNITY_EDITOR
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.Generators.World
{
    internal static class AbyssalScatterPolisherConstants
    {
        public const int MaxGraphicsBufferElements = 1048576;
        public const int HeaderSizeBytes = 64;
        public const int MatrixStrideBytes = 64;
        public const int MetadataStrideBytes = 64;
        public const int QualityIndexStrideBytes = 4;
        public const int ConfigStrideBytes = 128;
        public const int ScatterInstanceStrideBytes = 64;
        public const int CullingBoundsStrideBytes = 64;
        public const int TelemetryFrames = 300;
        public const uint FileMagic = 0x47524248u; // HBRG little-endian
        public const uint FileVersion = 1u;
        public const uint FileFlagHasQualityIndex = 1u << 0;
        public const uint FileFlagHasMetadata = 1u << 1;
        public const float DefaultGroundPenetrationMeters = 0.18f;
        public const float DefaultCellSizeMeters = 8f;
        public const int DefaultTerrainNormalResolution = 128;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalScatterPolisherConstants.ScatterInstanceStrideBytes)]
    internal struct ScatterInstanceDTO
    {
        [FieldOffset(0)] public double3 WorldPositionAup;
        [FieldOffset(24)] public float3 FallbackNormal;
        [FieldOffset(36)] public float YawRadians;
        [FieldOffset(40)] public float UniformScale;
        [FieldOffset(44)] public float GroundPenetrationMeters;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float Importance;
        [FieldOffset(60)] public uint TemplateIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalScatterPolisherConstants.CullingBoundsStrideBytes)]
    internal struct CullingBoundsDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float3 Extents;
        [FieldOffset(36)] public uint BoundsHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float PaddingMeters;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalScatterPolisherConstants.ConfigStrideBytes)]
    internal struct ScatterPolishConfigDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAup;
        [FieldOffset(24)] public int InstanceCount;
        [FieldOffset(28)] public int TerrainNormalWidth;
        [FieldOffset(32)] public int TerrainNormalHeight;
        [FieldOffset(36)] public float TerrainCellSizeMeters;
        [FieldOffset(40)] public float2 TerrainOriginXZ;
        [FieldOffset(48)] public float DefaultGroundPenetrationMeters;
        [FieldOffset(52)] public float ScaleMultiplier;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public float MinimumScale;
        [FieldOffset(64)] public int CullingGridResolutionX;
        [FieldOffset(68)] public int CullingGridResolutionY;
        [FieldOffset(72)] public int CullingGridResolutionZ;
        [FieldOffset(76)] public float CullingCellSizeMeters;
        [FieldOffset(80)] public float3 CullingGridOrigin;
        [FieldOffset(92)] public int QualityPermutationStride;
        [FieldOffset(96)] public uint Seed;
        [FieldOffset(100)] public uint Flags;
        [FieldOffset(104)] public ulong _pad0;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalScatterPolisherConstants.HeaderSizeBytes)]
    internal struct BrgDataHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint HeaderBytes;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int MatrixCount;
        [FieldOffset(20)] public int MetadataCount;
        [FieldOffset(24)] public int QualityIndexCount;
        [FieldOffset(28)] public int MatrixStrideBytes;
        [FieldOffset(32)] public int MetadataStrideBytes;
        [FieldOffset(36)] public int QualityIndexStrideBytes;
        [FieldOffset(40)] public uint MatrixOffsetBytes;
        [FieldOffset(44)] public uint MetadataOffsetBytes;
        [FieldOffset(48)] public uint QualityIndexOffsetBytes;
        [FieldOffset(52)] public uint ChunkHash;
        [FieldOffset(56)] public uint ContentHash;
        [FieldOffset(60)] public uint HeaderHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalScatterPolisherConstants.MetadataStrideBytes)]
    internal struct BrgInstanceMetadataDTO
    {
        [FieldOffset(0)] public float Type;
        [FieldOffset(4)] public float HeightScale;
        [FieldOffset(8)] public float WidthScale;
        [FieldOffset(12)] public float Variation;
        [FieldOffset(16)] public float TemplateIndex;
        [FieldOffset(20)] public float RuntimeState;
        [FieldOffset(24)] public float RuntimeFlags;
        [FieldOffset(28)] public float PulseFrequency;
        [FieldOffset(32)] public float4 BioluminescenceColor;
        [FieldOffset(48)] public float SwaySpeed;
        [FieldOffset(52)] public float BendAmplitude;
        [FieldOffset(56)] public float HealthNormalized;
        [FieldOffset(60)] public float Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct ScatterPolisherTelemetryEntry
    {
        [FieldOffset(0)] public uint Stage;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint WarningFlags;
        [FieldOffset(12)] public int InstanceCount;
        [FieldOffset(16)] public int CulledCount;
        [FieldOffset(20)] public int NonFiniteCount;
        [FieldOffset(24)] public float JobMilliseconds;
        [FieldOffset(28)] public float CullingMilliseconds;
        [FieldOffset(32)] public double SectorOriginX;
        [FieldOffset(40)] public double SectorOriginY;
        [FieldOffset(48)] public double SectorOriginZ;
        [FieldOffset(56)] public uint ContentHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockScatterInputJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ScatterInstanceDTO> Instances;
        [ReadOnly] public ScatterPolishConfigDTO Config;
        [ReadOnly] public int ForcedInsideCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Config.InstanceCount)
                return;

            uint seed = Config.Seed == 0u ? 0xA6141614u : Config.Seed;
            uint h0 = Hash(seed + (uint)index * 747796405u);
            uint h1 = Hash(h0 ^ 0x9E3779B9u);
            uint h2 = Hash(h1 ^ 0x85EBCA6Bu);

            float lane = index < ForcedInsideCount ? 0f : ((h0 & 1023u) - 512f) * 0.75f;
            float3 position = new float3(
                lane + HashSigned01(h1) * 180f,
                -1800f + Hash01(h2) * 80f,
                (index < ForcedInsideCount ? 0f : ((h2 & 1023u) - 512f) * 0.75f) + HashSigned01(h0) * 180f);

            float3 fallbackNormal = math.normalize(new float3(
                HashSigned01(h0) * 0.35f,
                1f,
                HashSigned01(h1) * 0.35f));

            float importance = Hash01(Hash(h2 ^ 0xC2B2AE35u));
            ScatterInstanceDTO value = new ScatterInstanceDTO
            {
                WorldPositionAup = Config.SectorOriginAup + new double3(position),
                FallbackNormal = fallbackNormal,
                YawRadians = Hash01(h0) * math.PI * 2f,
                UniformScale = 0.55f + Hash01(h1) * 1.65f,
                GroundPenetrationMeters = AbyssalScatterPolisherConstants.DefaultGroundPenetrationMeters,
                SpeciesHash = ResolveSpeciesHash(index),
                Flags = 0u,
                Importance = importance,
                TemplateIndex = (uint)(index & 7)
            };

            Instances[index] = value;
        }

        private static uint ResolveSpeciesHash(int index)
        {
            int lane = index % 6;
            if (lane == 0)
                return 0x4B454C50u; // KELP
            if (lane == 1)
                return 0x434F5241u; // CORA
            if (lane == 2)
                return 0x524F434Bu; // ROCK
            if (lane == 3)
                return 0x44504252u; // DPBR
            if (lane == 4)
                return 0x54554245u; // TUBE
            return 0x414C4741u; // ALGA
        }

        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float HashSigned01(uint value)
        {
            return Hash01(value) * 2f - 1f;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockTerrainNormalsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float4> TerrainNormalHeight;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            int width = math.max(1, Config.TerrainNormalWidth);
            int height = math.max(1, Config.TerrainNormalHeight);
            if ((uint)index >= (uint)(width * height))
                return;

            int x = index % width;
            int z = index / width;
            float fx = (x + 0.5f) * 0.071f;
            float fz = (z + 0.5f) * 0.053f;
            float heightMeters = math.sin(fx) * 4.5f + math.cos(fz) * 3.25f;
            float3 normal = math.normalize(new float3(
                -math.cos(fx) * 0.32f,
                1f,
                math.sin(fz) * 0.26f));

            TerrainNormalHeight[index] = new float4(normal, heightMeters);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockCullingBoundsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<CullingBoundsDTO> Bounds;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            uint h0 = Hash(Config.Seed + (uint)index * 2246822519u);
            uint h1 = Hash(h0 ^ 0x27D4EB2Du);
            float3 center = index == 0
                ? new float3(0f, -1800f, 0f)
                : new float3(HashSigned01(h0) * 260f, -1800f + HashSigned01(h1) * 32f, HashSigned01(h1 ^ h0) * 260f);

            float3 extents = index == 0
                ? new float3(35f, 35f, 35f)
                : new float3(8f + Hash01(h0) * 26f, 6f + Hash01(h1) * 18f, 8f + Hash01(h0 ^ h1) * 26f);

            CullingBoundsDTO value = new CullingBoundsDTO
            {
                CenterAup = Config.SectorOriginAup + new double3(center),
                Extents = extents,
                BoundsHash = Hash((uint)index + 0x16140000u),
                Flags = 1u,
                PaddingMeters = 0.35f
            };

            Bounds[index] = value;
        }

        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float HashSigned01(uint value)
        {
            return Hash01(value) * 2f - 1f;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ApplyGroundPenetrationOffsetJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ScatterInstanceDTO> Instances;
        [ReadOnly, NoAlias] public NativeArray<float4> TerrainNormalHeight;
        [NoAlias] public NativeArray<float3> LocalPositions;
        [NoAlias] public NativeArray<float3> SurfaceNormals;
        [NoAlias] public NativeArray<byte> NonFiniteMask;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Config.InstanceCount)
                return;

            ScatterInstanceDTO instance = Instances[index];
            float3 localPosition = (float3)(instance.WorldPositionAup - Config.SectorOriginAup);
            float3 normal = ResolveNormal(localPosition, instance.FallbackNormal);

            float penetration = instance.GroundPenetrationMeters > 0f
                ? instance.GroundPenetrationMeters
                : Config.DefaultGroundPenetrationMeters;
            localPosition -= normal * math.max(0f, penetration);

            if (!math.all(math.isfinite(localPosition)) || !math.all(math.isfinite(normal)))
            {
                localPosition = float3.zero;
                normal = new float3(0f, 1f, 0f);
                NonFiniteMask[index] = 1;
            }

            LocalPositions[index] = localPosition;
            SurfaceNormals[index] = normal;
        }

        private float3 ResolveNormal(float3 localPosition, float3 fallbackNormal)
        {
            int width = Config.TerrainNormalWidth;
            int height = Config.TerrainNormalHeight;
            if (width <= 1 || height <= 1 || TerrainNormalHeight.Length < width * height || Config.TerrainCellSizeMeters <= 0f)
                return SafeNormal(fallbackNormal);

            float2 uv = (new float2(localPosition.x, localPosition.z) - Config.TerrainOriginXZ) / Config.TerrainCellSizeMeters;
            int x0 = math.clamp((int)math.floor(uv.x), 0, width - 1);
            int z0 = math.clamp((int)math.floor(uv.y), 0, height - 1);
            int x1 = math.min(x0 + 1, width - 1);
            int z1 = math.min(z0 + 1, height - 1);
            float tx = math.saturate(uv.x - x0);
            float tz = math.saturate(uv.y - z0);

            float3 n00 = TerrainNormalHeight[z0 * width + x0].xyz;
            float3 n10 = TerrainNormalHeight[z0 * width + x1].xyz;
            float3 n01 = TerrainNormalHeight[z1 * width + x0].xyz;
            float3 n11 = TerrainNormalHeight[z1 * width + x1].xyz;
            float3 nx0 = math.lerp(n00, n10, tx);
            float3 nx1 = math.lerp(n01, n11, tx);
            return SafeNormal(math.lerp(nx0, nx1, tz));
        }

        private static float3 SafeNormal(float3 value)
        {
            float lenSq = math.lengthsq(value);
            if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                return new float3(0f, 1f, 0f);
            return value * math.rsqrt(lenSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct AlignScatterToTerrainNormalJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ScatterInstanceDTO> Instances;
        [ReadOnly, NoAlias] public NativeArray<float3> LocalPositions;
        [ReadOnly, NoAlias] public NativeArray<float3> SurfaceNormals;
        [NoAlias] public NativeArray<float4x4> Matrices;
        [NoAlias] public NativeArray<BrgInstanceMetadataDTO> Metadata;
        [NoAlias] public NativeArray<byte> NonFiniteMask;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Config.InstanceCount)
                return;

            ScatterInstanceDTO instance = Instances[index];
            float3 position = LocalPositions[index];
            float3 normal = SafeNormal(SurfaceNormals[index]);
            float scale = math.max(Config.MinimumScale, instance.UniformScale * math.max(0.0001f, Config.ScaleMultiplier));
            quaternion rotation = BuildNormalAlignedRotation(normal, instance.YawRadians);
            float4x4 matrix = float4x4.TRS(position, rotation, new float3(scale));

            if (!IsFiniteMatrix(matrix))
            {
                matrix = float4x4.TRS(position, quaternion.identity, new float3(0f));
                NonFiniteMask[index] = 1;
            }

            float variation = Hash01(instance.SpeciesHash ^ (uint)index * 1103515245u);
            BrgInstanceMetadataDTO metadata = new BrgInstanceMetadataDTO
            {
                Type = ResolveTypeLane(instance.SpeciesHash),
                HeightScale = scale,
                WidthScale = math.max(0.1f, scale * 0.82f),
                Variation = variation,
                TemplateIndex = instance.TemplateIndex,
                RuntimeState = 0f,
                RuntimeFlags = instance.Flags,
                PulseFrequency = 0.25f + variation * 1.25f,
                BioluminescenceColor = ResolveBioluminescence(instance.SpeciesHash, variation),
                SwaySpeed = 0.4f + variation * 1.8f,
                BendAmplitude = 0.2f + variation * 0.9f,
                HealthNormalized = 1f,
                Reserved0 = instance.Importance
            };

            Matrices[index] = matrix;
            Metadata[index] = metadata;
        }

        public static quaternion BuildNormalAlignedRotation(float3 normal, float yawRadians)
        {
            normal = SafeNormal(normal);
            float3 forwardSeed = math.abs(normal.y) < 0.97f ? new float3(0f, 0f, 1f) : new float3(1f, 0f, 0f);
            float3 tangent = forwardSeed - normal * math.dot(forwardSeed, normal);
            tangent = SafeNormal(tangent);
            quaternion yaw = quaternion.AxisAngle(normal, yawRadians);
            float3 forward = math.mul(yaw, tangent);
            forward = SafeNormal(forward);
            quaternion result = quaternion.LookRotationSafe(forward, normal);
            return math.all(math.isfinite(result.value)) ? result : quaternion.identity;
        }

        private static float ResolveTypeLane(uint speciesHash)
        {
            if (speciesHash == 0x4B454C50u)
                return 1f;
            if (speciesHash == 0x434F5241u)
                return 2f;
            if (speciesHash == 0x54554245u)
                return 3f;
            return 0f;
        }

        private static float4 ResolveBioluminescence(uint speciesHash, float variation)
        {
            if (speciesHash == 0x434F5241u)
                return new float4(0.16f, 0.52f, 0.72f, 0.22f + variation * 0.12f);
            if (speciesHash == 0x4B454C50u)
                return new float4(0.04f, 0.45f, 0.28f, 0.16f + variation * 0.10f);
            if (speciesHash == 0x54554245u)
                return new float4(0.62f, 0.34f, 0.12f, 0.18f + variation * 0.16f);
            return new float4(0.05f, 0.22f, 0.34f, 0.08f + variation * 0.08f);
        }

        private static bool IsFiniteMatrix(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        private static float3 SafeNormal(float3 value)
        {
            float lenSq = math.lengthsq(value);
            if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                return new float3(0f, 1f, 0f);
            return value * math.rsqrt(lenSq);
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct CullScatterInsideBoundsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float3> LocalPositions;
        [ReadOnly, NoAlias] public NativeArray<CullingBoundsDTO> Bounds;
        [ReadOnly, NoAlias] public NativeArray<int2> CellRanges;
        [ReadOnly, NoAlias] public NativeArray<int> BoundIndices;
        [NoAlias] public NativeArray<float4x4> Matrices;
        [NoAlias] public NativeArray<byte> CulledMask;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Config.InstanceCount)
                return;

            float3 position = LocalPositions[index];
            bool culled = IsInsideAnyBound(position);
            if (culled)
            {
                float4x4 matrix = Matrices[index];
                matrix.c0 = float4.zero;
                matrix.c1 = float4.zero;
                matrix.c2 = float4.zero;
                Matrices[index] = matrix;
            }

            CulledMask[index] = culled ? (byte)1 : (byte)0;
        }

        private bool IsInsideAnyBound(float3 localPosition)
        {
            int cellIndex = ResolveCellIndex(localPosition);
            if ((uint)cellIndex >= (uint)CellRanges.Length)
                return false;

            int2 range = CellRanges[cellIndex];
            int start = math.max(0, range.x);
            int end = math.min(BoundIndices.Length, range.y);
            for (int i = start; i < end; i++)
            {
                int boundIndex = BoundIndices[i];
                if ((uint)boundIndex >= (uint)Bounds.Length)
                    continue;

                CullingBoundsDTO bound = Bounds[boundIndex];
                float3 center = (float3)(bound.CenterAup - Config.SectorOriginAup);
                float3 extents = math.max(bound.Extents + new float3(math.max(0f, bound.PaddingMeters)), new float3(0f));
                float3 delta = math.abs(localPosition - center);
                if (math.all(delta <= extents))
                    return true;
            }

            return false;
        }

        private int ResolveCellIndex(float3 localPosition)
        {
            if (Config.CullingGridResolutionX <= 0 ||
                Config.CullingGridResolutionY <= 0 ||
                Config.CullingGridResolutionZ <= 0 ||
                Config.CullingCellSizeMeters <= 0f)
                return -1;

            int3 cell = (int3)math.floor((localPosition - Config.CullingGridOrigin) / Config.CullingCellSizeMeters);
            if (cell.x < 0 || cell.y < 0 || cell.z < 0 ||
                cell.x >= Config.CullingGridResolutionX ||
                cell.y >= Config.CullingGridResolutionY ||
                cell.z >= Config.CullingGridResolutionZ)
                return -1;

            return (cell.z * Config.CullingGridResolutionY + cell.y) * Config.CullingGridResolutionX + cell.x;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateQualityDeductionMapJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<int> QualityIndices;
        [ReadOnly] public ScatterPolishConfigDTO Config;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)QualityIndices.Length)
                return;

            int count = math.max(1, Config.InstanceCount);
            int stride = math.max(1, Config.QualityPermutationStride);
            int seedOffset = (int)(Config.Seed % (uint)count);
            QualityIndices[index] = (int)(((long)index * stride + seedOffset) % count);
        }
    }
}
#endif
